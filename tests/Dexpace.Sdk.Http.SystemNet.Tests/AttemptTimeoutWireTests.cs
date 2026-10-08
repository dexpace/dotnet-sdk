// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// XCUT-2, P6a-23 (design fact 10): <c>RetryPolicy</c> bounds an attempt with a per-attempt token source that it disposes
/// when the drive returns. Disposing it must not cancel the later read of the response body, which the caller does after
/// the pipeline has returned.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AttemptTimeoutWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_body_read_after_the_attempt_source_is_disposed_is_not_cancelled()
    {
        var payload = new string('x', 300_000);
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok(payload));
        using var client = new SystemHttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        await using var transport = new SystemNetHttpClient(client);
        var pipeline = new PipelineBuilder().Add(new RetryPolicy()).Build(transport);
        var options = new DexpaceClientOptions { AttemptTimeout = TimeSpan.FromSeconds(5) };

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/slow").ToString()), options, Ct);

        // The attempt's source was disposed when the drive returned; the body is still readable to its end.
        var text = await response.Body.ReadAsStringAsync(Ct);
        Assert.Equal(payload.Length, text.Length);
        Assert.Empty(server.Faults);
    }
}

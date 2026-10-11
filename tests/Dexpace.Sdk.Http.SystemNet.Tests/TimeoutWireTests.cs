// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-4, -5, -6, -8 and G3 (phase 8b, P8b-6, P8b-7) on a real socket. The server never answers, so a deadline is the only
/// way a call ends; every wait is bounded by the test's own 30 second cap (the kit's bound) and the deadlines are at least
/// 200 ms except the sub-resolution row, so no row races a scheduler. A fake clock replaces the real one wherever the row is
/// about what happens after a deadline would have fired.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TimeoutWireTests
{
    private static readonly TimeSpan s_cap = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_server_that_never_answers_is_cut_by_the_per_call_timeout_as_a_retryable_timeout()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Hang());
        await using var transport = new SystemNetHttpClient();

        var call = transport.ExecuteAsync(Request.Get(server.Url("/hang").AbsoluteUri), new RequestOptions { Timeout = TimeSpan.FromMilliseconds(300) }, Ct);
        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call.WaitAsync(s_cap, Ct));

        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task A_sub_resolution_timeout_also_times_out_and_does_not_hang()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Hang());
        await using var transport = new SystemNetHttpClient();

        var call = transport.ExecuteAsync(Request.Get(server.Url("/hang").AbsoluteUri), new RequestOptions { Timeout = TimeSpan.FromTicks(1000) }, Ct);

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call.WaitAsync(s_cap, Ct));
    }

    [Fact]
    public async Task A_borrowed_clients_own_timeout_is_a_retryable_timeout_too()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Hang());
        using var borrowed = new SystemHttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMilliseconds(300) };
        await using var transport = new SystemNetHttpClient(borrowed);

        var call = transport.ExecuteAsync(Request.Get(server.Url("/hang").AbsoluteUri), RequestOptions.Empty, Ct);

        var failure = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() => call.WaitAsync(s_cap, Ct));
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task A_call_the_caller_cancels_mid_wait_is_a_cancellation_not_a_timeout()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Hang());
        await using var transport = new SystemNetHttpClient();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var call = transport.ExecuteAsync(Request.Get(server.Url("/hang").AbsoluteUri), new RequestOptions { Timeout = TimeSpan.FromSeconds(20) }, cancel.Token);
        await server.WaitForRequestAsync(0, Ct);
        await cancel.CancelAsync();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(s_cap, Ct));
        Assert.IsNotType<ServiceRequestTimeoutException>(failure);
        Assert.IsNotAssignableFrom<SdkException>(failure);
    }

    [Fact]
    public async Task The_body_stays_readable_after_the_deadline_that_covered_the_headers_is_gone()
    {
        // G3: disposing the token a SendAsync(..., ResponseHeadersRead, token) was given after it returned does not cut the body.
        var clock = new FakeTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Streamed([new("Content-Type", "text/plain")], Chunks(release.Task)));
        await using var transport = new SystemNetHttpClient(new SystemNetHttpClientOptions { TimeProvider = clock });

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/stream").AbsoluteUri), new RequestOptions { Timeout = TimeSpan.FromMilliseconds(250) }, Ct);
        clock.Advance(TimeSpan.FromHours(1));
        release.SetResult();

        Assert.Equal("onetwo", await response.Body.ReadAsStringAsync(Ct));
    }

    private static async IAsyncEnumerable<byte[]> Chunks(Task gate)
    {
        yield return "one"u8.ToArray();
        await gate.ConfigureAwait(false);
        yield return "two"u8.ToArray();
    }
}

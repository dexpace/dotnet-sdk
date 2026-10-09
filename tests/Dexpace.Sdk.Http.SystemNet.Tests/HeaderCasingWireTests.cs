// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>HTTP-21 at the wire: a custom header goes out with the casing the caller used.</summary>
/// <remarks>
/// Verified on the pinned runtime (2026-09-30): <c>HttpClient</c> sends <c>TryAddWithoutValidation("X-Trace-Id", ...)</c>
/// and <c>("x-lower", ...)</c> exactly as written, and rewrites only the names it knows (<c>aCCept</c> becomes
/// <c>Accept</c>, <c>user-agent</c> becomes <c>User-Agent</c>). If a later runtime changes that, the requirement would
/// rest on 8b (TRANSPORT-10).
/// </remarks>
[Trait("Category", "Integration")]
public sealed class HeaderCasingWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Fact]
    public async Task A_custom_header_goes_out_with_its_original_casing()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("pong"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        var request = Request.Get(server.Url("/casing").ToString())
            .WithHeader("X-Trace-Id", "v")
            .WithHeader("x-lower", "w")
            .WithHeader("X-MiXeD-CaSe", "z");
        await using var response = await transport.ExecuteAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Contains("X-Trace-Id: v", recorded.HeaderLines);
        Assert.Contains("x-lower: w", recorded.HeaderLines);
        Assert.Contains("X-MiXeD-CaSe: z", recorded.HeaderLines);
        Assert.DoesNotContain("x-trace-id: v", recorded.HeaderLines);
    }

    // Well_known_names_are_not_asserted: HttpClient substitutes its own casing for the names it knows (Accept,
    // Content-Type, User-Agent, ...). Mapping those is phase 8b's TRANSPORT-10, so this class asserts nothing about them.
}

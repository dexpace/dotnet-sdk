// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// HTTP-6 at the wire: the adapter carries a received reason phrase onto the response only when it is header-safe.
/// </summary>
/// <remarks>
/// The scripted status lines use 0x01 and DEL, never CR, LF or NUL: <c>HttpClient</c> itself rejects those three in a
/// status line with an <c>HttpRequestException</c> (verified 2026-09-30), so the adapter never sees them and a test
/// built on them would fail for the wrong reason.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ReasonPhraseWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    private static async Task<(Request Sent, Response Received)> RoundTripAsync(string statusLine)
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Raw($"{statusLine}\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var request = Request.Get(server.Url("/reason").ToString());
        var response = await transport.ExecuteAsync(request, Ct);
        return (request, response);
    }

    [Fact]
    public async Task The_response_carries_the_request_and_a_safe_reason_phrase()
    {
        var (sent, response) = await RoundTripAsync("HTTP/1.1 200 All Good");
        await using var disposable = response;

        Assert.Same(sent, response.Request);
        Assert.Equal("All Good", response.ReasonPhrase);
    }

    [Theory]
    [InlineData("HTTP/1.1 200 Fine\u0001X")]
    [InlineData("HTTP/1.1 200 Fine\u007FX")]
    public async Task An_unsafe_reason_phrase_is_dropped_to_null_not_thrown(string statusLine)
    {
        var (_, response) = await RoundTripAsync(statusLine);
        await using var disposable = response;

        Assert.Equal(Status.Ok, response.Status);
        Assert.Null(response.ReasonPhrase);
    }

    [Fact]
    public async Task Obs_text_in_a_reason_phrase_is_kept()
    {
        // HttpClient passes bytes above 0x7F through as Latin-1; the lenient inbound rule keeps them (HTTP-19).
        var (_, response) = await RoundTripAsync("HTTP/1.1 200 Café");
        await using var disposable = response;

        Assert.Equal("Café", response.ReasonPhrase);
    }
}

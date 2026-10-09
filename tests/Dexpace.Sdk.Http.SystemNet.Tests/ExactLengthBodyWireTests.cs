// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>HTTP-39 at the wire: a known-length stream body writes exactly its declared length (design position F).</summary>
[Trait("Category", "Integration")]
public sealed class ExactLengthBodyWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    private static EndOfStreamException? FindEndOfStream(Exception? error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is EndOfStreamException found)
            {
                return found;
            }
        }

        return null;
    }

    [Fact]
    public async Task A_body_shorter_than_its_declared_length_fails_the_send()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        using var source = new MemoryStream(new byte[5]);
        var request = Request.Post(server.Url("/short").ToString(), RequestBody.FromStream(source, contentLength: 10));

        var error = await Record.ExceptionAsync(async () =>
        {
            await using var response = await transport.ExecuteAsync(request, Ct);
        });

        Assert.NotNull(error);
        var endOfStream = FindEndOfStream(error);
        Assert.NotNull(endOfStream);
        Assert.Contains("of 10", endOfStream.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_source_longer_than_its_declared_length_sends_exactly_the_declared_bytes()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        using var source = new MemoryStream("0123456789"u8.ToArray());
        var request = Request.Post(server.Url("/long").ToString(), RequestBody.FromStream(source, contentLength: 5));

        await using var response = await transport.ExecuteAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("5", recorded.Header("Content-Length"));
        Assert.Equal("01234", recorded.BodyText);
    }

    [Fact]
    public async Task A_body_of_exactly_its_declared_length_round_trips()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        using var source = new MemoryStream("abcde"u8.ToArray());
        var request = Request.Post(server.Url("/exact").ToString(), RequestBody.FromStream(source, contentLength: 5));

        await using var response = await transport.ExecuteAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("5", recorded.Header("Content-Length"));
        Assert.Equal("abcde", recorded.BodyText);
    }
}

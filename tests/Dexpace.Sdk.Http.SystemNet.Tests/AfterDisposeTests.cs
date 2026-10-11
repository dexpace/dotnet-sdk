// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The transports and handlers under test are disposed by the assertions.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-15 and TRANSPORT-21 (phase 8b, P8b-16): every call after <c>Dispose</c> throws <see cref="ObjectDisposedException"/>, for
/// every construction. The asynchronous face reports it through the returned task and never throws from the call.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AfterDisposeTests
{
    private static readonly Request s_request = Request.Get("https://example.test/ping");

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    public static TheoryData<string> Constructions() => ["owned", "handler", "borrowed"];

    private static (SystemNetHttpClient Transport, SystemHttpClient? Borrowed) Build(string construction)
    {
        switch (construction)
        {
            case "owned":
                return (new SystemNetHttpClient(), null);
            case "handler":
                return (new SystemNetHttpClient(new OkHandler()), null);
            default:
                var borrowed = new SystemHttpClient(new OkHandler());
                return (new SystemNetHttpClient(borrowed), borrowed);
        }
    }

    [Theory]
    [MemberData(nameof(Constructions))]
    public async Task The_async_face_faults_the_task_after_dispose(string construction)
    {
        var (transport, borrowed) = Build(construction);
        using var scope = borrowed;
        transport.Dispose();

        var task = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
    }

    [Theory]
    [MemberData(nameof(Constructions))]
    public void The_blocking_face_throws_after_dispose(string construction)
    {
        var (transport, borrowed) = Build(construction);
        using var scope = borrowed;
        transport.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transport.Execute(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Constructions))]
    public async Task DisposeAsync_latches_the_same_way(string construction)
    {
        var (transport, borrowed) = Build(construction);
        using var scope = borrowed;
        await transport.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_async_face_never_throws_synchronously_after_dispose()
    {
        var transport = new SystemNetHttpClient();
        transport.Dispose();

        Task? task = null;
        var thrown = Record.Exception(() => { task = transport.ExecuteAsync(s_request, RequestOptions.Empty, TestContext.Current.CancellationToken); });

        Assert.Null(thrown);
        Assert.NotNull(task);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => task!);
    }

    [Fact]
    public async Task A_borrowed_native_client_keeps_working_after_the_transport_is_disposed()
    {
        var (transport, borrowed) = Build("borrowed");
        using var scope = borrowed;

        transport.Dispose();

        using var response = await borrowed!.GetAsync(new Uri("https://example.test/ping"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

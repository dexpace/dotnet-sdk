// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

// These tests call the optional-token overloads on purpose: the default token is part of what SEAM-11 pins.
#pragma warning disable xUnit1051

/// <summary>SEAM-11 (design §3.2): the option-less calls pass <see cref="RequestOptions.Empty"/> and the token.</summary>
[Trait("Category", "Unit")]
public sealed class HttpClientExtensionsTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    [Fact]
    public async Task The_option_less_ExecuteAsync_passes_RequestOptions_Empty_and_the_token()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport();
        IAsyncHttpClient client = transport;
        var request = NewRequest();

        using var response = await client.ExecuteAsync(request, cts.Token);

        var call = Assert.IsType<RecordedCall>(transport.LastCall);
        Assert.Same(request, call.Request);
        Assert.Same(RequestOptions.Empty, call.Options);
        Assert.Equal(cts.Token, call.CancellationToken);
    }

    [Fact]
    public void The_option_less_Execute_passes_RequestOptions_Empty_and_the_token()
    {
        using var cts = new CancellationTokenSource();
        using var transport = new RecordingSyncTransport();
        IHttpClient client = transport;
        var request = NewRequest();

        using var response = client.Execute(request, cts.Token);

        var call = Assert.IsType<RecordedCall>(transport.LastCall);
        Assert.Same(request, call.Request);
        Assert.Same(RequestOptions.Empty, call.Options);
        Assert.Equal(cts.Token, call.CancellationToken);
    }

    [Fact]
    public async Task The_option_less_calls_default_the_token_to_none()
    {
        var asyncTransport = new RecordingTransport();
        using var syncTransport = new RecordingSyncTransport();
        IAsyncHttpClient asyncClient = asyncTransport;
        IHttpClient syncClient = syncTransport;

        using var asyncResponse = await asyncClient.ExecuteAsync(NewRequest());
        using var syncResponse = syncClient.Execute(NewRequest());

        Assert.Equal(CancellationToken.None, asyncTransport.LastCall!.CancellationToken);
        Assert.Equal(CancellationToken.None, syncTransport.LastCall!.CancellationToken);
    }

    [Fact]
    public async Task The_option_less_calls_reject_a_null_client_and_a_null_request()
    {
        IAsyncHttpClient nullAsync = null!;
        IHttpClient nullSync = null!;
        IAsyncHttpClient asyncClient = new RecordingTransport();
        using var syncTransport = new RecordingSyncTransport();
        IHttpClient syncClient = syncTransport;

        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => { _ = nullAsync.ExecuteAsync(NewRequest()); }).ParamName);
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => { _ = nullSync.Execute(NewRequest()); }).ParamName);
        Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => { _ = syncClient.Execute(null!); }).ParamName);

        // ASYNC-2: an argument error on the async seam arrives through the returned task, never synchronously.
        var task = asyncClient.ExecuteAsync(null!);
        Assert.True(task.IsFaulted);
        Assert.Equal("request", (await Assert.ThrowsAsync<ArgumentNullException>(() => task)).ParamName);
    }

    [Fact]
    public async Task The_option_less_Execute_works_over_a_DelegateHttpClient()
    {
        RecordedCall? asyncCall = null;
        RecordedCall? blockingCall = null;
        await using var asyncClient = DelegateHttpClient.Create((r, o, ct) =>
        {
            asyncCall = new RecordedCall(r, o, ct);
            return Task.FromResult(TestResponses.Create(Status.Ok, r));
        });
        using var blockingClient = DelegateHttpClient.CreateBlocking((r, o, ct) =>
        {
            blockingCall = new RecordedCall(r, o, ct);
            return TestResponses.Create(Status.Ok, r);
        });
        using var cts = new CancellationTokenSource();

        using var a = await asyncClient.ExecuteAsync(NewRequest(), cts.Token);
        using var b = blockingClient.Execute(NewRequest(), cts.Token);

        Assert.Same(RequestOptions.Empty, asyncCall!.Options);
        Assert.Equal(cts.Token, asyncCall.CancellationToken);
        Assert.Same(RequestOptions.Empty, blockingCall!.Options);
        Assert.Equal(cts.Token, blockingCall.CancellationToken);
    }
}

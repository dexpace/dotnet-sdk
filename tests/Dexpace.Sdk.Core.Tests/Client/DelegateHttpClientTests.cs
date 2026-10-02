// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// <c>DelegateHttpClient</c> (SEAM-2, SEAM-11, SEAM-15, SEAM-16; design §3.2, position J): a bare send function as a
/// transport, in an async and a blocking form.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DelegateHttpClientTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    private static Task<Response> MethodGroupSend(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        Task.FromResult(TestResponses.Create(Status.Created, request));

    [Fact]
    public async Task A_bare_send_lambda_works_as_a_transport()
    {
        // An async lambda, a Task.FromResult lambda and a method group each bind to Create.
        await using var viaAsync = DelegateHttpClient.Create(async (request, _, _) =>
        {
            await Task.Yield();
            return TestResponses.Create(Status.Ok, request);
        });
        await using var viaFromResult = DelegateHttpClient.Create((request, _, _) => Task.FromResult(TestResponses.Create(Status.Accepted, request)));
        await using var viaMethodGroup = DelegateHttpClient.Create(MethodGroupSend);

        using var a = await viaAsync.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        using var b = await viaFromResult.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        using var c = await viaMethodGroup.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, a.Status);
        Assert.Equal(Status.Accepted, b.Status);
        Assert.Equal(Status.Created, c.Status);
    }

    [Fact]
    public async Task A_throwing_lambda_binds_without_ambiguity_and_faults_the_task()
    {
        // Design fact 3, position J: with two overloads named Create this is CS0121.
        await using var client = DelegateHttpClient.Create((_, _, _) => throw new InvalidOperationException("boom"));

        var task = client.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.True(task.IsFaulted);
        Assert.Equal("boom", (await Assert.ThrowsAsync<InvalidOperationException>(() => task)).Message);
    }

    [Fact]
    public void A_bare_blocking_lambda_works_as_a_blocking_transport()
    {
        using var client = DelegateHttpClient.CreateBlocking((request, _, _) => TestResponses.Create(Status.Accepted, request));

        using var response = client.Execute(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Accepted, response.Status);
    }

    [Fact]
    public void A_throwing_blocking_lambda_propagates()
    {
        using var client = DelegateHttpClient.CreateBlocking((_, _, _) => throw new InvalidOperationException("boom"));

        Assert.Equal(
            "boom",
            Assert.Throws<InvalidOperationException>(() => client.Execute(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public async Task The_delegate_receives_the_exact_request_options_and_token()
    {
        using var cts = new CancellationTokenSource();
        var request = NewRequest();
        var options = new RequestOptions { MaxRetries = 7 };
        RecordedCall? seen = null;
        await using var client = DelegateHttpClient.Create((r, o, ct) =>
        {
            seen = new RecordedCall(r, o, ct);
            return Task.FromResult(TestResponses.Create(Status.Ok, r));
        });

        using var response = await client.ExecuteAsync(request, options, cts.Token);

        Assert.Same(request, seen!.Request);
        Assert.Same(options, seen.Options);
        Assert.Equal(cts.Token, seen.CancellationToken);

        RecordedCall? blockingSeen = null;
        using var blocking = DelegateHttpClient.CreateBlocking((r, o, ct) =>
        {
            blockingSeen = new RecordedCall(r, o, ct);
            return TestResponses.Create(Status.Ok, r);
        });
        using var blockingResponse = blocking.Execute(request, options, cts.Token);

        Assert.Same(request, blockingSeen!.Request);
        Assert.Same(options, blockingSeen.Options);
        Assert.Equal(cts.Token, blockingSeen.CancellationToken);
    }

    [Fact]
    public async Task An_options_ignoring_transport_returns_the_same_response_with_and_without_options()
    {
        await using var client = DelegateHttpClient.Create((r, _, _) => Task.FromResult(TestResponses.Create(Status.Accepted, r)));

        using var withEmpty = await client.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        using var withOptions = await client.ExecuteAsync(NewRequest(), new RequestOptions { Timeout = TimeSpan.FromSeconds(3) }, TestContext.Current.CancellationToken);

        Assert.Equal(withEmpty.Status, withOptions.Status);
    }

    [Fact]
    public async Task A_null_result_faults_the_task_with_InvalidOperationException()
    {
        await using var client = DelegateHttpClient.Create((_, _, _) => Task.FromResult<Response>(null!));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_null_task_faults_with_InvalidOperationException()
    {
        await using var client = DelegateHttpClient.Create((_, _, _) => null!);

        var task = client.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public void CreateBlocking_null_result_throws_InvalidOperationException()
    {
        using var client = DelegateHttpClient.CreateBlocking((_, _, _) => null!);

        Assert.Throws<InvalidOperationException>(
            () => client.Execute(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Argument_errors_are_delivered_through_the_task()
    {
        await using var client = DelegateHttpClient.Create((r, _, _) => Task.FromResult(TestResponses.Create(Status.Ok, r)));

        var nullRequest = client.ExecuteAsync(null!, RequestOptions.Empty, TestContext.Current.CancellationToken);
        var nullOptions = client.ExecuteAsync(NewRequest(), null!, TestContext.Current.CancellationToken);

        Assert.True(nullRequest.IsFaulted);
        Assert.True(nullOptions.IsFaulted);
        Assert.Equal("request", (await Assert.ThrowsAsync<ArgumentNullException>(() => nullRequest)).ParamName);
        Assert.Equal("options", (await Assert.ThrowsAsync<ArgumentNullException>(() => nullOptions)).ParamName);
    }

    [Fact]
    public void Blocking_argument_errors_throw_ArgumentNullException()
    {
        using var client = DelegateHttpClient.CreateBlocking((r, _, _) => TestResponses.Create(Status.Ok, r));

        Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => client.Execute(null!, RequestOptions.Empty, CancellationToken.None)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => client.Execute(NewRequest(), null!, CancellationToken.None)).ParamName);
    }

    [Fact]
    public void Create_and_CreateBlocking_reject_a_null_delegate_eagerly()
    {
        Assert.Equal("send", Assert.Throws<ArgumentNullException>(() => DelegateHttpClient.Create(null!)).ParamName);
        Assert.Equal("send", Assert.Throws<ArgumentNullException>(() => DelegateHttpClient.CreateBlocking(null!)).ParamName);
    }

    [Fact]
    public async Task It_keeps_working_after_dispose()
    {
        // SEAM-15: the delegate adapters hold nothing, so dispose is a no-op and the transport still answers.
        var asyncClient = DelegateHttpClient.Create((r, _, _) => Task.FromResult(TestResponses.Create(Status.Ok, r)));
        var blockingClient = DelegateHttpClient.CreateBlocking((r, _, _) => TestResponses.Create(Status.Ok, r));

        await asyncClient.DisposeAsync();
        await asyncClient.DisposeAsync();
        blockingClient.Dispose();
        blockingClient.Dispose();

        using var a = await asyncClient.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        using var b = blockingClient.Execute(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(Status.Ok, a.Status);
        Assert.Equal(Status.Ok, b.Status);
    }

    [Fact]
    public async Task Cancelling_after_delivery_leaves_the_response_readable()
    {
        // SEAM-16: the token fires after the response was delivered; the response is intact and not disposed.
        using var body = new DisposalCountingBody();
        await using var client = DelegateHttpClient.Create((r, _, _) => Task.FromResult(TestResponses.Create(Status.Ok, r, body: body)));
        using var cts = new CancellationTokenSource();

        using var response = await client.ExecuteAsync(NewRequest(), RequestOptions.Empty, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(0, body.DisposeCount);
        await using var stream = await response.Body.OpenReadAsync(TestContext.Current.CancellationToken);
        Assert.True(stream.CanRead);
    }
}

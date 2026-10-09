// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable xUnit1051 // Whether GetValueAsync gets a caller token, and which, is the behaviour under test (HTTP-45).

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// HTTP-44 and HTTP-45 (P7a-14, P7a-15): the lazy typed-response wrapper. The metadata is readable without touching the body,
/// the body is parsed once, success (a <see langword="null"/> included) and failure are memoized, and no caller ever blocks a
/// thread behind another's parse.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TypedResponseTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Response Resp(ResponseBody? body = null, Request? request = null) =>
        TestResponses.Create(Status.Created, request, Headers.Empty.With("X-Trace", "t1"), body, Protocol.Http2, "Created Ok");

    // ---- HTTP-44 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Metadata_is_readable_with_the_body_never_opened()
    {
        var request = Request.Get("https://api.example.com/v1/things");
        var handler = new GateResponseHandler<string>();
        using var typed = new TypedResponse<string>(Resp(new ThrowingOpenBody(), request), handler);

        Assert.Same(request, typed.Request);
        Assert.Equal(Status.Created, typed.Status);
        Assert.Equal("t1", typed.Headers.Get("X-Trace"));
        Assert.Equal(Protocol.Http2, typed.Protocol);
        Assert.Equal("Created Ok", typed.ReasonPhrase);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task One_handler_call_across_three_sequential_GetValueAsync()
    {
        var handler = new GateResponseHandler<object>();
        var value = new object();
        handler.Gate.SetResult(value);
        using var typed = new TypedResponse<object>(Resp(), handler);

        var first = await typed.GetValueAsync(Token);
        var second = await typed.GetValueAsync(Token);
        var third = await typed.GetValueAsync();

        Assert.Equal(1, handler.Calls);
        Assert.Same(value, first);
        Assert.Same(value, second);
        Assert.Same(value, third);
    }

    [Fact]
    public async Task A_null_success_is_memoized()
    {
        var calls = 0;
        var handler = new DelegateResponseHandler<string?>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult<string?>(null);
        });
        using var typed = new TypedResponse<string?>(Resp(), handler);

        Assert.Null(await typed.GetValueAsync(Token));
        Assert.Null(await typed.GetValueAsync(Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_failure_is_memoized_as_the_same_exception_object()
    {
        var handler = new GateResponseHandler<string> { ThrowOnCall = new FormatException("boom") };
        using var typed = new TypedResponse<string>(Resp(), handler);

        var first = await Assert.ThrowsAsync<FormatException>(async () => await typed.GetValueAsync(Token));
        var second = await Assert.ThrowsAsync<FormatException>(async () => await typed.GetValueAsync(Token));

        Assert.Same(first, second);
        Assert.Same(handler.ThrowOnCall, first);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_cancelled_construction_token_is_a_memoized_failure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new GateResponseHandler<string> { ThrowOnCall = new OperationCanceledException("stopped", null, cts.Token) };
        using var typed = new TypedResponse<string>(Resp(), handler, cts.Token);

        var first = await Assert.ThrowsAsync<OperationCanceledException>(async () => await typed.GetValueAsync());
        var second = await Assert.ThrowsAsync<OperationCanceledException>(async () => await typed.GetValueAsync());

        Assert.Same(first, second);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(cts.Token, handler.RecordedToken);
    }

    // ---- HTTP-45 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sixty_four_concurrent_first_callers_run_the_handler_once()
    {
        var handler = new GateResponseHandler<string>();
        using var typed = new TypedResponse<string>(Resp(), handler);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callers = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(
                async () =>
                {
                    await start.Task;
                    return await typed.GetValueAsync();
                },
                Token))
            .ToArray();

        start.SetResult();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        handler.Gate.SetResult("the value");
        var results = await Task.WhenAll(callers);

        Assert.Equal(1, handler.Calls);
        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.Equal("the value", results[0]);
    }

    [Fact]
    public async Task A_blocked_handler_does_not_block_a_second_caller()
    {
        // The observable form of HTTP-45: while one parse waits, another caller's GetValueAsync returns, incomplete, at once.
        var handler = new GateResponseHandler<string>();
        using var typed = new TypedResponse<string>(Resp(), handler);

        var first = typed.GetValueAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        ValueTask<string> second = default;
        await Task.Run(() => second = typed.GetValueAsync(), Token);

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        handler.Gate.SetResult("v");
        Assert.Equal("v", await first);
        Assert.Equal("v", await second);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_callers_cancelled_wait_leaves_the_parse_running()
    {
        using var construction = new CancellationTokenSource();
        var handler = new GateResponseHandler<string>();
        using var typed = new TypedResponse<string>(Resp(), handler, construction.Token);
        using var impatient = new CancellationTokenSource();

        var first = typed.GetValueAsync(impatient.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await impatient.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        var patient = typed.GetValueAsync();
        handler.Gate.SetResult("v");

        Assert.Equal("v", await patient);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(construction.Token, handler.RecordedToken);
        Assert.False(handler.RecordedToken.IsCancellationRequested);
    }

    // ---- disposal, failure kinds ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Dispose_forwards_to_the_response_once()
    {
        var body = new CountingPayloadBody([]);
        var typed = new TypedResponse<string>(Resp(body), new GateResponseHandler<string>());

        typed.Dispose();
        await typed.DisposeAsync();
        typed.Dispose();

        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Disposing_during_a_parse_memoizes_the_resulting_failure()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateResponseHandler<string>(async (response, ct) =>
        {
            started.SetResult();
            await gate.Task;
            using var stream = await response.Body.OpenReadAsync(ct);
            return "unreachable";
        });
        var typed = new TypedResponse<string>(Resp(ResponseBody.FromStream(new MemoryStream([1, 2, 3]))), handler);

        var parse = typed.GetValueAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        await typed.DisposeAsync();
        gate.SetResult();

        var first = await Assert.ThrowsAsync<StreamClosedException>(async () => await parse);
        var later = await Assert.ThrowsAsync<StreamClosedException>(async () => await typed.GetValueAsync());
        Assert.Same(first, later);
    }

    [Fact]
    public async Task A_fatal_exception_is_recorded_and_rethrown()
    {
        // IsFatal means OutOfMemoryException. It is recorded in the memo first, so every awaiting caller sees it, and then
        // rethrown from the (discarded) parse task, never swallowed: that rethrow surfaces as an unobserved task exception,
        // which this test observes (and marks observed, so no other test in the process sees it).
#pragma warning disable CA2201 // IsFatal is defined as OutOfMemoryException, so the test has to throw it.
        var fatal = new OutOfMemoryException("simulated");
#pragma warning restore CA2201
        var surfaced = 0;
        void Observe(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.Flatten().InnerExceptions.Contains(fatal))
            {
                e.SetObserved();
                Interlocked.Increment(ref surfaced);
            }
        }

        TaskScheduler.UnobservedTaskException += Observe;
        try
        {
            var handler = await ParseFatalAsync(fatal);

            Assert.Equal(1, handler.Calls);
            for (var i = 0; i < 20 && Volatile.Read(ref surfaced) == 0; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Yield();
            }

            Assert.Equal(1, Volatile.Read(ref surfaced));
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Observe;
        }
    }

    // A separate frame, so nothing in the caller keeps the discarded parse task alive for the collector.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<GateResponseHandler<string>> ParseFatalAsync(OutOfMemoryException fatal)
    {
        var handler = new GateResponseHandler<string> { ThrowOnCall = fatal };
        using var typed = new TypedResponse<string>(Resp(), handler);

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(async () => await typed.GetValueAsync(Token));
        var again = await Assert.ThrowsAsync<OutOfMemoryException>(async () => await typed.GetValueAsync(Token));

        Assert.Same(fatal, thrown);
        Assert.Same(fatal, again);
        return handler;
    }

    [Fact]
    public void Argument_validation()
    {
        var handler = new GateResponseHandler<string>();

        Assert.Throws<ArgumentNullException>(() => new TypedResponse<string>(null!, handler));
        Assert.Throws<ArgumentNullException>(() => new TypedResponse<string>(Resp(), null!));
    }

    [Fact]
    public void The_wrapped_response_is_not_exposed()
    {
        // A Body accessor would let a caller consume the single-use body behind the memo, which HTTP-44 forbids.
        var publicProperties = typeof(TypedResponse<string>).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(publicProperties, p => p.PropertyType == typeof(Response) || p.PropertyType == typeof(ResponseBody));
        Assert.DoesNotContain(publicProperties, p => p.Name == "Body");
        Assert.DoesNotContain(typeof(TypedResponse<string>).GetMethods(BindingFlags.Public | BindingFlags.Instance), m => m.ReturnType == typeof(Response));
    }

    private sealed class ThrowingOpenBody : ResponseBody
    {
        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The body must not be opened by reading metadata.");
    }
}

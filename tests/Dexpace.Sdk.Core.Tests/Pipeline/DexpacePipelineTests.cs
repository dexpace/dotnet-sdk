// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// Placed in the "Instrumentation" collection so these tests do not run in parallel with
/// InstrumentationPolicyTests — both exercise DexpaceDiagnostics.ActivitySource and
/// concurrent execution causes activity leakage across test instances.
/// </summary>
[Collection("Instrumentation")]
public sealed class DexpacePipelineTests
{
    private static Request MakeGetRequest() => Request.Get("https://api.example.com/v1/items");

    private static DexpaceClientOptions ZeroRetryOptions() => new()
    {
        Retry = new RetryOptions
        {
            MaxRetryAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(5),
        }
    };

    // ─── Basic wiring ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_ReturnsWorkingPipeline_200()
    {
        var transport = new ScriptedTransport([new Response(Status.Ok)]);
        var pipeline = DexpacePipeline.CreateDefault(transport);

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions());

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task CreateDefault_AuthPolicy_IsIncluded_WhenProvided()
    {
        var auth = new MarkingPolicy("x-auth-stamped", "true");
        string? authHeaderSeen = null;
        var transport = new CapturingTransport(req =>
        {
            authHeaderSeen = req.Headers.Get("x-auth-stamped");
            return new Response(Status.Ok);
        });

        var pipeline = DexpacePipeline.CreateDefault(transport, authPolicy: auth);
        await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions());

        Assert.Equal("true", authHeaderSeen);
    }

    // ─── Retry wired ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_RetryIsWired_503ThenSuccess()
    {
        var transport = new ScriptedTransport([
            new Response(Status.ServiceUnavailable),
            new Response(Status.Ok),
        ]);
        var pipeline = DexpacePipeline.CreateDefault(transport, timeProvider: new InstantTimeProvider());

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions());

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task CreateDefault_RetryExhausted_ReturnsLastResponse()
    {
        // MaxRetryAttempts = 3 → 1 initial + 3 retries = 4 calls
        var transport = new ScriptedTransport(Enumerable.Repeat(new Response(Status.ServiceUnavailable), 4));
        var pipeline = DexpacePipeline.CreateDefault(transport, timeProvider: new InstantTimeProvider());

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions());

        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.Equal(4, transport.CallCount);
    }

    // ─── Redirect wired ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_RedirectIsWired_302ThenSuccess()
    {
        var redirectHeaders = new Headers.Builder()
            .Set("Location", "https://api.example.com/v1/redirected")
            .Build();

        Uri? finalUrl = null;
        var transport = new CapturingTransport(req =>
        {
            finalUrl = req.Url;
            if (req.Url.AbsolutePath == "/v1/items")
            {
                return new Response(Status.Found, redirectHeaders);
            }

            return new Response(Status.Ok);
        });

        var pipeline = DexpacePipeline.CreateDefault(transport);
        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions());

        Assert.Equal(Status.Ok, response.Status);
        Assert.NotNull(finalUrl);
        Assert.Equal("/v1/redirected", finalUrl.AbsolutePath);
    }

    // ─── Nested helpers ──────────────────────────────────────────────────────

    private sealed class ScriptedTransport : IAsyncHttpClient
    {
        private readonly List<Response> _script;
        private int _callCount;

        public ScriptedTransport(IEnumerable<Response> script) => _script = [.. script];

        public int CallCount => _callCount;

        public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            if (index >= _script.Count)
            {
                throw new InvalidOperationException($"Script ran out at call {index + 1}.");
            }

            return Task.FromResult(_script[index]);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingTransport(Func<Request, Response> handler) : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default) =>
            Task.FromResult(handler(request));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A policy that stamps a fixed header — used to verify auth policy injection.</summary>
    private sealed class MarkingPolicy(string header, string value) : HttpPipelinePolicy
    {
        // Auth stage so it participates correctly in the default pipeline ordering.
        public override PipelineStage Stage => PipelineStage.Auth;

        public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            context.Request = context.Request with
            {
                Headers = context.Request.Headers.Set(header, value),
            };
            await continuation.RunAsync(context).ConfigureAwait(false);
        }
    }

    private sealed class InstantTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) =>
            base.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), period);
    }
}

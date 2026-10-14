// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/retry-safety.conformance.test.ts and
// concurrency-and-lifecycle.conformance.test.ts, and ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/
// invariant_suite/ (retry safety, concurrency). The five-way matrix of XCUT-10 is chapter 19's own.
using System.Globalization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-9</c> to <c>XCUT-12</c>: a cycle-safe cause walk, one retry-safety gate for protocol and transport failures alike,
/// shared components that hold no per-call state, and a single-flight token refresh whose lock is scoped to its cache.
/// </summary>
internal static class ResilienceInvariants
{
    private const int WalkLimit = 256;

    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-9.cycle-safe-walk", ["XCUT-9"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, CycleSafeWalkAsync),
        InvariantAssertionBuilder.Define("xcut-10.safety-gate-uniform", ["XCUT-10"], RequirementLevel.Must, InvariantAssertionBuilder.Both, SafetyGateUniformAsync),
        InvariantAssertionBuilder.Define("xcut-11.shared-no-crosstalk", ["XCUT-11"], RequirementLevel.Must, InvariantAssertionBuilder.Both, SharedNoCrosstalkAsync),
        InvariantAssertionBuilder.Define("xcut-12.single-flight-scoped", ["XCUT-12"], RequirementLevel.Should, InvariantAssertionBuilder.Both, SingleFlightScopedAsync),
    ];

    // The suite only ever walks the cyclic exception and never throws it: a thrown cyclic chain hangs the test host on Windows.
    // A walk is bounded by taking at most WalkLimit steps, so a walk with no visited set ends as a failure instead of a hang.
    private static Task CycleSafeWalkAsync(InvariantContext context, CancellationToken token)
    {
        var create = InvariantContext.RequireHook(context.Subject.CreateCyclicException, nameof(InvariantSubject.CreateCyclicException));
        var walk = context.Subject.EnumerateCauses;

        var cyclic = create();
        var visited = walk(cyclic).Take(WalkLimit).ToList();
        Check.True(visited.Count < WalkLimit, "a walk over a cyclic cause chain must terminate", $"fewer than {WalkLimit} steps", $"{WalkLimit} steps without ending");
        Check.True(visited.Count > 0, "a walk over a cyclic cause chain must still visit the exception itself", "at least one cause", "none");
        Check.Equal(
            visited.Distinct(ReferenceEqualityComparer.Instance).Count(),
            visited.Count,
            "the distinct causes the walk visited (each cause is visited once, tracked by reference identity)");

        // Three distinct exceptions that are Equals to one another: a walk tracking visited causes by equality sees one.
        var third = new StructurallyEqualProbe("same", null);
        var second = new StructurallyEqualProbe("same", third);
        var first = new StructurallyEqualProbe("same", second);
        var chain = walk(first).Take(WalkLimit).ToList();
        Check.Equal(chain.Count, 3, "the causes visited along a chain of three distinct, structurally equal exceptions (visited causes are tracked by reference identity, not by Equals)");
        return Task.CompletedTask;
    }

    // Retry safety is decided at the retry step, independently of retryability, and the same way for protocol and transport
    // failures: no case may special-case a transport error.
    private static async Task SafetyGateUniformAsync(InvariantContext context, CancellationToken token)
    {
        var body = new byte[] { 1, 2, 3 };
        var transportFailure = ProbeTransport.Behavior.Of((_, _) => throw new ServiceRequestException("connection reset"));
        var protocolFailure = ProbeTransport.Behavior.Status(503);

        await ExpectSendsAsync(context, "a body-less GET failing with a transport error", Request.Get(InvariantOptions.Url), transportFailure, 3, token).ConfigureAwait(false);
        await ExpectSendsAsync(context, "a body-less POST failing with a protocol error", Request.Create(Method.Post, InvariantOptions.Url), protocolFailure, 1, token).ConfigureAwait(false);
        await ExpectSendsAsync(context, "a body-less POST failing with a transport error that never reached the server", Request.Create(Method.Post, InvariantOptions.Url), transportFailure, 1, token).ConfigureAwait(false);
        await ExpectSendsAsync(context, "a POST with a replayable body failing with a protocol error", Request.Post(InvariantOptions.Url, RequestBody.FromBytes(body)), protocolFailure, 3, token).ConfigureAwait(false);
        await ExpectSendsAsync(context, "a POST with a replayable body failing with a transport error", Request.Post(InvariantOptions.Url, RequestBody.FromBytes(body)), transportFailure, 3, token).ConfigureAwait(false);

        using var singleUse = new CountingStream(new MemoryStream(body));
        await ExpectSendsAsync(context, "a POST with a single-use streaming body failing with a transport error", Request.Post(InvariantOptions.Url, RequestBody.FromStream(singleUse)), transportFailure, 1, token).ConfigureAwait(false);
        using var singleUseProtocol = new CountingStream(new MemoryStream(body));
        await ExpectSendsAsync(context, "a POST with a single-use streaming body failing with a protocol error", Request.Post(InvariantOptions.Url, RequestBody.FromStream(singleUseProtocol)), protocolFailure, 1, token).ConfigureAwait(false);
    }

    private static async Task ExpectSendsAsync(InvariantContext context, string what, Request request, ProbeTransport.Behavior behavior, int expected, CancellationToken token)
    {
        var transport = context.Transport(behavior);
        var pipeline = context.Build(transport, InvariantOptions.Retrying(2));

        await context.FailureOfAsync(pipeline, request, what, token).ConfigureAwait(false);

        Check.Equal(transport.Sends, expected, $"the sends of {what}");
    }

    // One shared pipeline, many concurrent distinct requests, each answered by an echo of its own id: per-call state must live on
    // the call, so no response may carry another call's id.
    private static async Task SharedNoCrosstalkAsync(InvariantContext context, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.Of((request, _) =>
            ProbeTransport.Empty(request, 200, new Headers.Builder().Add("X-Echo", request.Url.Query).Build())));
        var pipeline = context.Build(transport, InvariantOptions.Retrying(0));

        var calls = Enumerable.Range(0, InvariantOptions.Concurrency)
            .Select(id => EchoAsync(context, pipeline, id, token))
            .ToArray();
        var echoes = await Bounded.WaitForAllAsync(calls, context.Bound, "the concurrent calls", token).ConfigureAwait(false);

        var crossed = echoes.Where(echo => echo.Echoed != "?id=" + echo.Id.ToString(CultureInfo.InvariantCulture)).Take(3).ToArray();
        Check.True(
            crossed.Length == 0,
            "a shared pipeline must hold no per-call state: every response must answer its own request",
            "each response echoing its own id",
            string.Join(", ", crossed.Select(echo => $"call {echo.Id} was answered with {echo.Echoed}")));
    }

    private static async Task<(int Id, string? Echoed)> EchoAsync(InvariantContext context, HttpPipeline pipeline, int id, CancellationToken token)
    {
        var request = Request.Get(string.Create(CultureInfo.InvariantCulture, $"{InvariantOptions.Url}?id={id}"));
        using var response = await Check.GuardAsync(() => context.SendAsync(pipeline, request, token), $"concurrent call {id}", token).ConfigureAwait(false);
        return (id, response.Headers.Get("X-Echo"));
    }

    // N readers race one expiring token: one fetch. A fetch blocked on one credential's cache must not hold another's.
    private static async Task SingleFlightScopedAsync(InvariantContext context, CancellationToken token)
    {
        var createCache = context.Subject.CreateTokenCache;
        var scope = new TokenRequestContext(["scope"]);

        var racing = new GatedCredential(context.Time, gated: true);
        var read = createCache(racing, context.Time);
        try
        {
            var readers = Enumerable.Range(0, InvariantOptions.Racers * 2).Select(_ => StartRead(context, read, scope, token)).ToArray();
            await Bounded.WaitAsync(racing.Started, context.Bound, "the first fetch to begin", token).ConfigureAwait(false);
            racing.Release();
            await Bounded.WaitForAllAsync(readers, context.Bound, "the racing readers", token).ConfigureAwait(false);
        }
        finally
        {
            racing.Release();
        }

        Check.Equal(racing.Fetches, 1, $"the credential fetches of {InvariantOptions.Racers * 2} readers racing one expiring token (the refresh is single-flight)");

        var held = new GatedCredential(context.Time, gated: true);
        var free = new GatedCredential(context.Time, gated: false);
        var readHeld = createCache(held, context.Time);
        var readFree = createCache(free, context.Time);
        try
        {
            var blocked = StartRead(context, readHeld, scope, token);
            await Bounded.WaitAsync(held.Started, context.Bound, "the held fetch to begin", token).ConfigureAwait(false);
            var other = StartRead(context, readFree, scope, token);
            var finished = await Poll.HoldsWithinAsync(() => other.IsCompleted, context.Bound, token).ConfigureAwait(false);
            Check.True(
                finished,
                "a token read through a different credential's cache must not wait on another cache's in-flight fetch (the lock is scoped to its cache)",
                "the unrelated read completing while the first fetch is parked",
                "it was still waiting");
            held.Release();
            await Bounded.WaitAsync(blocked, context.Bound, "the held read after its fetch is released", token).ConfigureAwait(false);
        }
        finally
        {
            held.Release();
        }
    }

    private static Task<AccessToken> StartRead(
        InvariantContext context,
        Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> read,
        TokenRequestContext scope,
        CancellationToken token) =>
        context.Face == TransportFace.Blocking
            ? Task.Factory.StartNew(
                () => read(scope, token).AsTask(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default).Unwrap()
            : read(scope, token).AsTask();
}

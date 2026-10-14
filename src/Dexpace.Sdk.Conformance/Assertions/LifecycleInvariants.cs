// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/concurrency-and-lifecycle.conformance.test.ts and ruby-sdk@5b17395
// gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/ (lifecycle, bounded maps, immutable models). The interrupt
// flag clause of XCUT-13 is vacuous on .NET (design §10 entry 8, §3.7): there is no ambient interrupt flag to preserve.
using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-13</c> to <c>XCUT-15</c>: components close once, whatever the call pattern, and never wait for a parked read; maps keyed
/// by callers or servers drain back to their cap after a burst; and a model keeps no alias to a mutable input.
/// </summary>
internal static class LifecycleInvariants
{
    private const int BoundedCapacity = 32;

    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-13.dispose-latched", ["XCUT-13"], RequirementLevel.Must, InvariantAssertionBuilder.Both, DisposeLatchedAsync),
        InvariantAssertionBuilder.Define("xcut-13.dispose-non-blocking", ["XCUT-13"], RequirementLevel.Must, InvariantAssertionBuilder.Both, DisposeNonBlockingAsync),
        InvariantAssertionBuilder.Define("xcut-14.bounded-under-burst", ["XCUT-14"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, BoundedUnderBurstAsync),
        InvariantAssertionBuilder.Define("xcut-14.drains-after-insert", ["XCUT-14"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, DrainsAfterInsertAsync),
        InvariantAssertionBuilder.Define("xcut-15.no-external-alias", ["XCUT-15"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, NoExternalAliasAsync),
    ];

    // Closing twice in a row, then from many threads at once, releases the body exactly once. Counted at the resource.
    private static async Task DisposeLatchedAsync(InvariantContext context, CancellationToken token)
    {
        for (var index = 0; ; index++)
        {
            using var body = new ReleaseCountingBody(parked: false);
            var disposables = context.Subject.CreateDisposables(body);
            try
            {
                if (index >= disposables.Count)
                {
                    Check.True(index > 0, "the subject supplies no disposable component to close", "at least one", "none");
                    return;
                }

                var component = disposables[index];
                await CloseAsync(context, component, token).ConfigureAwait(false);
                await CloseAsync(context, component, token).ConfigureAwait(false);
                await Bounded.WaitForAllAsync(
                    [.. Enumerable.Range(0, InvariantOptions.Racers).Select(_ => CloseAsync(context, component, token).ContinueWith(static _ => 0, TaskScheduler.Default))],
                    context.Bound,
                    $"closing a {component.Kind} from {InvariantOptions.Racers} threads at once",
                    token).ConfigureAwait(false);

                Check.Equal(body.Releases, 1, $"the releases of the body behind a {component.Kind} closed twice and then from {InvariantOptions.Racers} threads at once (a close is latched)");
            }
            finally
            {
                await CloseOthersAsync(context, disposables, index).ConfigureAwait(false);
            }
        }
    }

    // A read parked on the body when the component closes: the close returns without waiting for it.
    private static async Task DisposeNonBlockingAsync(InvariantContext context, CancellationToken token)
    {
        for (var index = 0; ; index++)
        {
            using var body = new ReleaseCountingBody(parked: true);
            var disposables = context.Subject.CreateDisposables(body);
            try
            {
                if (index >= disposables.Count)
                {
                    Check.True(index > 0, "the subject supplies no disposable component to close", "at least one", "none");
                    return;
                }

                await CloseWithReadParkedAsync(context, body, disposables[index], token).ConfigureAwait(false);
            }
            finally
            {
                await CloseOthersAsync(context, disposables, index).ConfigureAwait(false);
            }
        }
    }

    private static async Task CloseWithReadParkedAsync(InvariantContext context, ReleaseCountingBody body, DisposableUnderTest component, CancellationToken token)
    {
        var parked = body.Parked!;
        var stream = await body.OpenReadAsync(token).ConfigureAwait(false);
        await using var streamScope = stream.ConfigureAwait(false);
        var read = stream.ReadAsync(new byte[16], token).AsTask();
        try
        {
            await Bounded.WaitAsync(parked.ReadStarted, context.Bound, "a read to park on the body", token).ConfigureAwait(false);

            await Bounded.WaitAsync(
                CloseAsync(context, component, token),
                context.Bound,
                $"closing a {component.Kind} while a read is parked on its body (a close must not wait for it)",
                token).ConfigureAwait(false);
        }
        finally
        {
            parked.Release();
            await ObserveAsync(read).ConfigureAwait(false);
        }

        Check.Equal(body.Releases, 1, $"the releases of the body behind a {component.Kind} closed with a read parked on it");
    }

    // Each round builds a whole set over its own body and judges one member; the rest are closed when the round's counts have been read, so
    // none outlives its round (they share the body, which is why they are not closed while the member under test is being counted).
    private static async Task CloseOthersAsync(InvariantContext context, IReadOnlyList<DisposableUnderTest> components, int underTest)
    {
        for (var i = 0; i < components.Count; i++)
        {
            if (i == underTest)
            {
                continue;
            }

            try
            {
                if (context.Face == TransportFace.Blocking)
                {
                    components[i].Dispose();
                }
                else
                {
                    await components[i].DisposeAsync().ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // A member not under test this round is closed only so it outlives nothing; how it closes is judged in its own round.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }

    // Insert far more than the cap, one thread and then many: never above the cap after an insert, and not stuck above it after a burst.
    private static async Task BoundedUnderBurstAsync(InvariantContext context, CancellationToken token)
    {
        var create = InvariantContext.RequireHook(context.Subject.CreateBoundedMap, nameof(InvariantSubject.CreateBoundedMap));

        var single = create(BoundedCapacity);
        for (var i = 0; i < single.Capacity * 8; i++)
        {
            single.Insert("single-" + i.ToString(CultureInfo.InvariantCulture));
            var count = single.Count();
            Check.True(
                count <= single.Capacity,
                "a map keyed by callers or servers must never hold more than its cap after an insert",
                $"at most {single.Capacity} entries",
                $"{count} after insert {i + 1}");
        }

        var burst = create(BoundedCapacity);
        await InsertBurstAsync(context, burst, burst.Capacity * 4, token).ConfigureAwait(false);
        Check.True(
            burst.Count() <= burst.Capacity,
            "a map must not be left above its cap by a concurrent insert burst",
            $"at most {burst.Capacity} entries",
            burst.Count().ToString(CultureInfo.InvariantCulture));
    }

    // The map is one insert short of its cap when many threads insert at once: it must drain back under the cap after each insert,
    // with a loop, so the burst converges to the bound (a single pre-insert check-then-evict leaves it stuck above).
    private static async Task DrainsAfterInsertAsync(InvariantContext context, CancellationToken token)
    {
        var create = InvariantContext.RequireHook(context.Subject.CreateBoundedMap, nameof(InvariantSubject.CreateBoundedMap));
        var probe = create(BoundedCapacity);
        for (var i = 0; i < probe.Capacity - 1; i++)
        {
            probe.Insert("fill-" + i.ToString(CultureInfo.InvariantCulture));
        }

        await InsertBurstAsync(context, probe, probe.Capacity, token).ConfigureAwait(false);

        var count = probe.Count();
        Check.True(
            count <= probe.Capacity,
            "after a concurrent insert burst the map must drain back to its cap (a loop after each insert, not a single pre-insert check-then-evict)",
            $"at most {probe.Capacity} entries",
            count.ToString(CultureInfo.InvariantCulture));
    }

    private static async Task InsertBurstAsync(InvariantContext context, BoundedMapProbe probe, int perThread, CancellationToken token)
    {
        using var start = new ManualResetEventSlim(initialState: false);
        var threads = Enumerable.Range(0, InvariantOptions.Racers)
            .Select(id => Task.Factory.StartNew(
                () =>
                {
                    start.Wait(token);
                    for (var i = 0; i < perThread; i++)
                    {
                        probe.Insert(string.Create(CultureInfo.InvariantCulture, $"burst-{id}-{i}"));
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default))
            .ToArray();

        start.Set();
        await Bounded.WaitAsync(Task.WhenAll(threads), context.Bound, "the concurrent insert burst", token).ConfigureAwait(false);
    }

    // Mutating what a model was built from must not change the model; a "setter" returns a new instance.
    private static Task NoExternalAliasAsync(InvariantContext context, CancellationToken token)
    {
        var models = context.Subject.Models;

        var typeInput = new Dictionary<string, string> { ["charset"] = "utf-8" };
        var typeView = models.MediaTypeParameters(typeInput);
        typeInput["charset"] = "tampered";
        typeInput["added"] = "x";
        UnchangedParameters(typeView, "a media type's parameters");

        var challengeInput = new Dictionary<string, string> { ["charset"] = "utf-8" };
        var challengeView = models.ChallengeParameters(challengeInput);
        challengeInput["charset"] = "tampered";
        challengeInput["added"] = "x";
        UnchangedParameters(challengeView, "an authentication challenge's parameters");

        var bytes = new byte[] { 1, 2, 3 };
        var read = models.BytesBody(bytes);
        var before = (byte[])read().Clone();
        bytes[0] = 9;
        Check.SequenceEqual(read(), before, "the bytes a request body sends after the array it was built from was mutated");

        var built = models.HeadersAfterBuilderMutation();
        Check.True(built.Contains("A") && !built.Contains("B"), "headers built before a builder was mutated again must not see the mutation", "A present, B absent", built.ToString());

        var headers = new Headers.Builder().Add("A", "1").Build();
        var withB = models.WithHeader(headers, "B", "2");
        Check.True(!ReferenceEquals(withB, headers), "a header \"setter\" must return a new instance", "a new instance", "the same instance");
        Check.True(!headers.Contains("B") && withB.Get("B") == "2", "adding a header must leave the original unchanged and the copy changed", "original without B, copy with B", headers.ToString());

        var request = Request.Get(InvariantOptions.Url);
        var withHeader = models.RequestWithHeader(request, "X-New", "1");
        Check.True(!ReferenceEquals(withHeader, request), "a request \"setter\" must return a new instance", "a new instance", "the same instance");
        Check.True(!request.Headers.Contains("X-New") && withHeader.Headers.Get("X-New") == "1", "adding a request header must leave the original unchanged and the copy changed", "original without X-New, copy with it", request.Headers.ToString());
        return Task.CompletedTask;
    }

    private static void UnchangedParameters(IReadOnlyDictionary<string, string> view, string what) =>
        Check.True(
            view.Count == 1 && view.TryGetValue("charset", out var charset) && charset == "utf-8",
            $"{what} must not change when the map they were built from is mutated afterwards",
            "charset=utf-8 and nothing else",
            string.Join(", ", view.Select(pair => pair.Key + "=" + pair.Value)));

    private static async Task CloseAsync(InvariantContext context, DisposableUnderTest component, CancellationToken token)
    {
        try
        {
            if (context.Face == TransportFace.Blocking)
            {
                await Task.Factory.StartNew(component.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).ConfigureAwait(false);
            }
            else
            {
                await component.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not ConformanceException && !token.IsCancellationRequested)
        {
            throw new ConformanceException($"closing a {component.Kind} threw {ex.GetType().Name}: a repeated or concurrent close must be a no-op", ex);
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The parked read ends in whatever way the close ended it; only that it ends matters here.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}

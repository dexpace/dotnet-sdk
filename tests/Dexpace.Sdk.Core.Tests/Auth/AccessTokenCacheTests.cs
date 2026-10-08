// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

/// <summary>
/// A fake TokenCredential that counts calls and returns configurable tokens.
/// Thread-safe via Interlocked.
/// </summary>
file sealed class FakeTokenCredential : TokenCredential
{
    private int _callCount;
    private Func<TokenRequestContext, AccessToken> _factory;

    public int CallCount => _callCount;

    public FakeTokenCredential(Func<TokenRequestContext, AccessToken> factory)
        => _factory = factory;

    /// <summary>Replace the factory (used to inject failures between calls).</summary>
    public void SetFactory(Func<TokenRequestContext, AccessToken> factory) => _factory = factory;

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _callCount);
        return ValueTask.FromResult(_factory(context));
    }
}

/// <summary>
/// A fake TokenCredential that throws on demand.
/// </summary>
file sealed class ThrowingTokenCredential : TokenCredential
{
    private readonly Exception _ex;

    public ThrowingTokenCredential(Exception ex) => _ex = ex;

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        => throw _ex;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

[Trait("Category", "Unit")]
public class AccessTokenCacheTests
{
    private static TokenRequestContext Ctx(string scope = "scope") =>
        new TokenRequestContext([scope]);

    private static DateTimeOffset Now() => DateTimeOffset.UtcNow;

    // -----------------------------------------------------------------------
    // 1. Repeated GetAsync within validity calls the credential ONCE
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_WithinValidity_ReturnsTokenWithoutRefetch()
    {
        var time = new FakeTimeProvider(Now());
        var expected = new AccessToken("tok", time.GetUtcNow().AddHours(1));
        var cred = new FakeTokenCredential(_ => expected);
        var cache = new AccessTokenCache(cred, time);
        var ctx = Ctx();

        var t1 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        var t2 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        var t3 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Token, t1.Token);
        Assert.Equal(expected.Token, t2.Token);
        Assert.Equal(expected.Token, t3.Token);
        Assert.Equal(1, cred.CallCount);
    }

    // -----------------------------------------------------------------------
    // 2. Past RefreshOn the valid token is stamped and one background refresh runs
    //    (migrated: the refresh was awaited on the request path; AUTH-37, breaking 5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Past_RefreshOn_the_valid_token_is_stamped_and_one_background_refresh_runs()
    {
        var start = Now();
        var time = new FakeTimeProvider(start);

        // First token: expires in 1h, refresh hint at 50m
        var firstToken = new AccessToken("first", start.AddHours(1), start.AddMinutes(50));
        var secondToken = new AccessToken("second", start.AddHours(2));
        var callCount = 0;
        var cred = new FakeTokenCredential(_ =>
        {
            callCount++;
            return callCount == 1 ? firstToken : secondToken;
        });

        var cache = new AccessTokenCache(cred, time);
        var ctx = Ctx();

        // Call within validity window — gets first token
        var t1 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        Assert.Equal("first", t1.Token);
        Assert.Equal(1, callCount);

        // Advance past RefreshOn but still before ExpiresOn: the valid token is stamped now.
        time.Advance(TimeSpan.FromMinutes(51));

        var t2 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        Assert.Equal("first", t2.Token);
        await cache.PendingRefresh(ctx)!;
        Assert.Equal(2, callCount);

        // The refresh finished: further calls use the refreshed token and cost no additional fetch.
        var t3 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        Assert.Equal("second", t3.Token);
        Assert.Equal(2, callCount);
    }

    // -----------------------------------------------------------------------
    // 3. Advancing past ExpiresOn triggers a refresh
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_PastExpiresOn_RefreshesToken()
    {
        var start = Now();
        var time = new FakeTimeProvider(start);

        var firstToken = new AccessToken("expired", start.AddMinutes(10));
        var secondToken = new AccessToken("fresh", start.AddHours(2));
        var callCount = 0;
        var cred = new FakeTokenCredential(_ =>
        {
            callCount++;
            return callCount == 1 ? firstToken : secondToken;
        });

        var cache = new AccessTokenCache(cred, time);
        var ctx = Ctx();

        var t1 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        Assert.Equal("expired", t1.Token);

        // Jump past ExpiresOn
        time.Advance(TimeSpan.FromMinutes(11));

        var t2 = await cache.GetAsync(ctx, TestContext.Current.CancellationToken);
        Assert.Equal("fresh", t2.Token);
        Assert.Equal(2, callCount);
    }

    // -----------------------------------------------------------------------
    // 4. Concurrent GetAsync calls result in a single credential call (single-flight)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_Concurrent_SingleFlightRefresh()
    {
        var time = new FakeTimeProvider(Now());

        // Use a TaskCompletionSource to make the credential artificially slow so
        // all concurrent callers arrive before any returns.
        var gate = new TaskCompletionSource<AccessToken>(TaskCreationOptions.RunContinuationsAsynchronously);

        var slowCred = new SlowTokenCredential(gate);
        var cache = new AccessTokenCache(slowCred, time);
        var ctx = Ctx();

        // Start 50 concurrent requests before any token is available
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => cache.GetAsync(ctx).AsTask())
            .ToArray();

        // Let the credential complete
        var token = new AccessToken("concurrent_tok", time.GetUtcNow().AddHours(1));
        gate.SetResult(token);

        var results = await Task.WhenAll(tasks);

        // All callers get the same token
        Assert.All(results, r => Assert.Equal("concurrent_tok", r.Token));
        // Credential was called exactly once
        Assert.Equal(1, slowCred.CallCount);
    }

    // -----------------------------------------------------------------------
    // 5. A failed background refresh while valid stamps the valid token and logs
    //    (migrated: the failure was swallowed silently; AUTH-37, P6c-25, breaking 5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_failed_background_refresh_while_valid_stamps_the_valid_token_and_logs()
    {
        var start = Now();
        var time = new FakeTimeProvider(start);

        // Token: expires in 1h, refresh hint at 50m
        var validToken = new AccessToken("valid", start.AddHours(1), start.AddMinutes(50));
        var callCount = 0;
        var cred = new FakeTokenCredential(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                return validToken;
            }

            throw new InvalidOperationException("Credential failure");
        });

        var logger = new RecordingLogger();
        var cache = new AccessTokenCache(cred, time);
        var ctx = Ctx();

        // Populate cache
        var t1 = await cache.GetAsync(ctx, logger, TestContext.Current.CancellationToken);
        Assert.Equal("valid", t1.Token);

        // Advance past RefreshOn — triggers a background refresh attempt
        time.Advance(TimeSpan.FromMinutes(51));

        // Despite the failing refresh, the still-valid cached token is stamped
        var t2 = await cache.GetAsync(ctx, logger, TestContext.Current.CancellationToken);
        Assert.Equal("valid", t2.Token);
        await cache.PendingRefresh(ctx)!;

        // Credential was called for the failed refresh, and the failure was logged once
        Assert.Equal(2, callCount);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(160, entry.EventId.Id);
    }

    // -----------------------------------------------------------------------
    // 6. Refresh that throws with no valid token propagates
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_RefreshThrows_WithNoValidToken_Propagates()
    {
        var time = new FakeTimeProvider(Now());
        var ex = new InvalidOperationException("no token");
        var cred = new ThrowingTokenCredential(ex);
        var cache = new AccessTokenCache(cred, time);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAsync(Ctx(), TestContext.Current.CancellationToken).AsTask());

        Assert.Same(ex, thrown);
    }

    // -----------------------------------------------------------------------
    // 7. Different contexts are cached independently
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_DifferentContexts_CachedIndependently()
    {
        var time = new FakeTimeProvider(Now());
        var callCount = 0;
        var cred = new FakeTokenCredential(ctx =>
        {
            callCount++;
            return new AccessToken($"tok-{ctx.Scopes[0]}", time.GetUtcNow().AddHours(1));
        });

        var cache = new AccessTokenCache(cred, time);

        var t1 = await cache.GetAsync(Ctx("scope1"), TestContext.Current.CancellationToken);
        var t2 = await cache.GetAsync(Ctx("scope2"), TestContext.Current.CancellationToken);
        var t3 = await cache.GetAsync(Ctx("scope1"), TestContext.Current.CancellationToken); // should hit cache

        Assert.Equal("tok-scope1", t1.Token);
        Assert.Equal("tok-scope2", t2.Token);
        Assert.Equal("tok-scope1", t3.Token);
        // scope1 fetched once, scope2 fetched once = 2 calls
        Assert.Equal(2, callCount);
    }

    // =======================================================================
    // Phase 6c: zones, coalescing, background refresh, validation, sync path, bound, rejection
    // (Ruby bearer_stamper_test, async_bearer_stamper_test, bearer_provider_test; Node bearer-cache.test.ts)
    // =======================================================================

    private static readonly DateTimeOffset s_start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AccessToken Tok(string value, TimeSpan? lifetime = null, TimeSpan? refreshAfter = null) =>
        new(
            value,
            lifetime is { } l ? s_start + l : null,
            refreshAfter is { } r ? s_start + r : null);

    // ---- Zones (AUTH-34, AUTH-37) -----------------------------------------

    [Fact]
    public async Task A_fresh_token_is_returned_without_calling_the_provider_again()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential { Immediate = _ => Tok("t", TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, time);

        _ = await cache.GetAsync(Ctx(), Ct);
        time.Advance(TimeSpan.FromMinutes(29));
        var again = await cache.GetAsync(Ctx(), Ct);

        Assert.Equal("t", again.Token);
        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task The_hot_path_takes_no_lock()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential { Gated = scope => scope == "B", Immediate = c => Tok("tok-" + c.Scopes[0], TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx("A"), Ct);

        // Key B has a fetch in flight (its gate is held); key A is fresh and answers synchronously.
        var pendingB = cache.GetAsync(Ctx("B"), Ct);
        var hot = cache.GetAsync(Ctx("A"), Ct);

        Assert.True(hot.IsCompletedSuccessfully);
        Assert.Equal("tok-A", (await hot).Token);
        cred.Release(0, Tok("tok-B", TimeSpan.FromHours(1)));
        Assert.Equal("tok-B", (await pendingB).Token);
    }

    [Fact]
    public async Task A_token_inside_the_margin_is_stamped_now_and_refreshed_in_the_background()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, time);
        var first = cache.GetAsync(Ctx(), Ct);
        cred.Release(0, Tok("old", TimeSpan.FromSeconds(60)));
        Assert.Equal("old", (await first).Token);

        time.Advance(TimeSpan.FromSeconds(40)); // 20 s left: inside the 30 s margin, still valid
        var stamped = cache.GetAsync(Ctx(), Ct);

        Assert.True(stamped.IsCompletedSuccessfully);
        Assert.Equal("old", (await stamped).Token);
        await cred.WaitForCallsAsync(2); // the refresh started
        Assert.Equal(2, cred.Calls);
        cred.Release(1, new AccessToken("new", time.GetUtcNow().AddHours(1)));
        await cache.PendingRefresh(Ctx())!;
        Assert.Equal("new", (await cache.GetAsync(Ctx(), Ct)).Token);
    }

    [Fact]
    public async Task The_default_margin_is_30_seconds_and_configurable()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), AccessTokenCache.DefaultRefreshMargin);
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential { Immediate = _ => new AccessToken("t", time.GetUtcNow().AddSeconds(100)) };
        var defaultCache = new AccessTokenCache(cred, time);
        Assert.Equal(AccessTokenCache.DefaultRefreshMargin, defaultCache.RefreshMargin);
        _ = await defaultCache.GetAsync(Ctx(), Ct);

        time.Advance(TimeSpan.FromSeconds(60)); // 40 s left: fresh under the 30 s default
        _ = await defaultCache.GetAsync(Ctx(), Ct);
        Assert.Equal(1, cred.Calls);
        Assert.Null(defaultCache.PendingRefresh(Ctx()));

        var wide = new AccessTokenCache(cred, time) { RefreshMargin = TimeSpan.FromSeconds(90) };
        _ = await wide.GetAsync(Ctx(), Ct); // 100 s left: fresh under 90 s
        Assert.Null(wide.PendingRefresh(Ctx()));
        time.Advance(TimeSpan.FromSeconds(11)); // 89 s left: expiring under 90 s
        _ = await wide.GetAsync(Ctx(), Ct);
        await wide.PendingRefresh(Ctx())!;
        Assert.Equal(3, cred.Calls);
    }

    [Fact]
    public void A_negative_margin_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AccessTokenCache(new GatedCredential(), null) { RefreshMargin = TimeSpan.FromTicks(-1) });
    }

    [Fact]
    public async Task RefreshOn_before_ExpiresOn_minus_margin_wins()
    {
        var time = new FakeTimeProvider(s_start);
        var n = 0;
        var cred = new GatedCredential
        {
            Immediate = _ => Interlocked.Increment(ref n) == 1
                ? Tok("old", TimeSpan.FromHours(1), TimeSpan.FromMinutes(10))
                : new AccessToken("new", time.GetUtcNow().AddHours(1)),
        };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx(), Ct);

        time.Advance(TimeSpan.FromMinutes(11));
        var stamped = await cache.GetAsync(Ctx(), Ct);
        await cache.PendingRefresh(Ctx())!;

        Assert.Equal("old", stamped.Token);
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task A_token_at_exactly_its_expiry_is_still_valid_but_expiring()
    {
        var time = new FakeTimeProvider(s_start);
        var n = 0;
        var cred = new GatedCredential
        {
            Immediate = _ => Interlocked.Increment(ref n) == 1 ? Tok("old", TimeSpan.FromMinutes(5)) : new AccessToken("new", time.GetUtcNow().AddHours(1)),
        };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx(), Ct);

        time.Advance(TimeSpan.FromMinutes(5));
        var stamped = await cache.GetAsync(Ctx(), Ct);
        await cache.PendingRefresh(Ctx())!;

        Assert.Equal("old", stamped.Token);
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task An_expired_token_is_fetched_in_the_foreground()
    {
        var time = new FakeTimeProvider(s_start);
        var n = 0;
        var cred = new GatedCredential
        {
            Immediate = _ => Interlocked.Increment(ref n) == 1 ? Tok("old", TimeSpan.FromMinutes(5)) : new AccessToken("new", time.GetUtcNow().AddHours(1)),
        };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx(), Ct);

        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1));

        Assert.Equal("new", (await cache.GetAsync(Ctx(), Ct)).Token);
        Assert.Null(cache.PendingRefresh(Ctx()));
    }

    [Fact]
    public async Task A_token_without_expiry_is_never_refreshed()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential { Immediate = _ => new AccessToken("forever") };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx(), Ct);

        time.Advance(TimeSpan.FromDays(3650));
        _ = await cache.GetAsync(Ctx(), Ct);

        Assert.Equal(1, cred.Calls);
    }

    // ---- Coalescing (AUTH-34, AUTH-37, P6c-24) -----------------------------

    [Fact]
    public async Task N_concurrent_requests_at_expiry_cost_one_fetch()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, time);

        // Each call runs synchronously up to its first await, so all 16 are queued on the gate before the fetch ends.
        var tasks = Enumerable.Range(0, 16).Select(_ => cache.GetAsync(Ctx(), Ct).AsTask()).ToArray();
        cred.Release(0, Tok("shared", TimeSpan.FromHours(1)));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal("shared", r.Token));
        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task With_a_failing_provider_N_waiters_fail_together_after_one_attempt()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        var tasks = Enumerable.Range(0, 16).Select(_ => cache.GetAsync(Ctx(), Ct).AsTask()).ToArray();
        cred.Fail(0, new InvalidOperationException("outage"));

        foreach (var task in tasks)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Equal("outage", ex.Message);
        }

        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task A_request_arriving_after_a_failure_fetches_again()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        var failing = cache.GetAsync(Ctx(), Ct).AsTask();
        cred.Fail(0, new InvalidOperationException("outage"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);

        var next = cache.GetAsync(Ctx(), Ct).AsTask();
        cred.Release(1, Tok("recovered", TimeSpan.FromHours(1)));

        Assert.Equal("recovered", (await next).Token);
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task A_waiters_own_cancellation_leaves_the_fetch_running()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        using var cts = new CancellationTokenSource();
        var fetcher = cache.GetAsync(Ctx(), Ct).AsTask();
        var waiter = cache.GetAsync(Ctx(), cts.Token).AsTask();

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        cred.Release(0, Tok("t", TimeSpan.FromHours(1)));

        Assert.Equal("t", (await fetcher).Token);
        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task A_fetchers_own_cancellation_is_not_adopted_by_the_waiters()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        using var cts = new CancellationTokenSource();
        var fetcher = cache.GetAsync(Ctx(), cts.Token).AsTask();
        var waiter = cache.GetAsync(Ctx(), Ct).AsTask();

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetcher);
        await cred.WaitForCallsAsync(2); // the waiter fetches for itself
        cred.Release(1, Tok("second", TimeSpan.FromHours(1)));

        Assert.Equal("second", (await waiter).Token);
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task Different_keys_do_not_block_each_other()
    {
        var cred = new GatedCredential { Gated = scope => scope == "A", Immediate = c => Tok("tok-" + c.Scopes[0], TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        var blocked = cache.GetAsync(Ctx("A"), Ct).AsTask();
        var other = await cache.GetAsync(Ctx("B"), Ct);

        Assert.Equal("tok-B", other.Token);
        Assert.False(blocked.IsCompleted);
        cred.Release(0, Tok("tok-A", TimeSpan.FromHours(1)));
        Assert.Equal("tok-A", (await blocked).Token);
    }

    // ---- Background refresh (AUTH-37) --------------------------------------

    private static async Task<(AccessTokenCache Cache, GatedCredential Cred, FakeTimeProvider Time)> ExpiringAsync()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, time);
        var first = cache.GetAsync(Ctx(), Ct);
        cred.Release(0, Tok("old", TimeSpan.FromSeconds(60)));
        _ = await first;
        time.Advance(TimeSpan.FromSeconds(40));
        return (cache, cred, time);
    }

    [Fact]
    public async Task Only_one_background_refresh_runs_per_key_however_many_requests_arrive()
    {
        var (cache, cred, _) = await ExpiringAsync();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal("old", (await cache.GetAsync(Ctx(), Ct)).Token);
        }

        await cred.WaitForCallsAsync(2);
        Assert.Equal(2, cred.Calls);
        cred.Release(1, new AccessToken("new", s_start.AddHours(1)));
        await cache.PendingRefresh(Ctx())!;
    }

    [Fact]
    public async Task The_background_refresh_sees_no_ambient_Activity()
    {
        var time = new FakeTimeProvider(s_start);
        var seen = new List<Activity?>();
        var cred = new GatedCredential
        {
            Immediate = _ =>
            {
                seen.Add(Activity.Current);
                return seen.Count == 1 ? Tok("old", TimeSpan.FromSeconds(60)) : new AccessToken("new", s_start.AddHours(1));
            },
        };
        var cache = new AccessTokenCache(cred, time);
        _ = await cache.GetAsync(Ctx(), Ct);
        time.Advance(TimeSpan.FromSeconds(40));

        using var source = new ActivitySource("Dexpace.Tests.Auth.Ambient");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using (var activity = source.StartActivity("call"))
        {
            Assert.NotNull(activity);
            _ = await cache.GetAsync(Ctx(), Ct);
        }

        await cache.PendingRefresh(Ctx())!;
        Assert.Equal(2, seen.Count);
        Assert.Null(seen[1]);
    }

    [Fact]
    public async Task A_failing_background_refresh_logs_event_160_once_and_the_triggering_call_is_unaffected()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, time);
        var logger = new RecordingLogger();
        var first = cache.GetAsync(Ctx(), logger, Ct);
        cred.Release(0, Tok("old", TimeSpan.FromSeconds(60)));
        _ = await first;
        time.Advance(TimeSpan.FromSeconds(40));

        var stamped = await cache.GetAsync(Ctx(), logger, Ct);
        await cred.WaitForCallsAsync(2);
        cred.Fail(1, new InvalidOperationException("provider down"));
        await cache.PendingRefresh(Ctx())!;

        Assert.Equal("old", stamped.Token);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(160, entry.EventId.Id);
        Assert.Equal("dexpace.auth.token_refresh_failed", entry.EventId.Name);
        Assert.Equal("System.InvalidOperationException", entry["error.type"]);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task After_a_background_failure_the_old_token_stays_valid_and_the_next_request_starts_another_refresh()
    {
        var (cache, cred, _) = await ExpiringAsync();
        _ = await cache.GetAsync(Ctx(), Ct);
        await cred.WaitForCallsAsync(2);
        cred.Fail(1, new InvalidOperationException("provider down"));
        await cache.PendingRefresh(Ctx())!;

        var still = await cache.GetAsync(Ctx(), Ct); // inside the margin again: starts another refresh

        Assert.Equal("old", still.Token);
        await cred.WaitForCallsAsync(3);
        Assert.Equal(3, cred.Calls);
        cred.Release(2, new AccessToken("new", s_start.AddHours(1)));
        await cache.PendingRefresh(Ctx())!;
    }

    [Fact]
    public async Task The_refresh_task_is_observed_and_completes_without_faulting()
    {
        var (cache, cred, _) = await ExpiringAsync();
        _ = await cache.GetAsync(Ctx(), Ct);
        await cred.WaitForCallsAsync(2);
        cred.Fail(1, new InvalidOperationException("provider down"));

        var refresh = cache.PendingRefresh(Ctx())!;
        await refresh;

        Assert.Equal(TaskStatus.RanToCompletion, refresh.Status);
    }

    // ---- Validation (AUTH-35, AUTH-11) --------------------------------------

    [Fact]
    public async Task A_default_token_from_the_provider_throws_TokenProviderException()
    {
        var cache = new AccessTokenCache(new GatedCredential { Immediate = _ => default }, new FakeTimeProvider(s_start));

        await Assert.ThrowsAsync<TokenProviderException>(() => cache.GetAsync(Ctx(), Ct).AsTask());
        Assert.Throws<TokenProviderException>(() => cache.Get(Ctx(), Ct));
    }

    [Fact]
    public async Task A_token_already_expired_at_fetch_throws_TokenProviderException_and_never_contains_the_token()
    {
        var cred = new GatedCredential { Immediate = _ => new AccessToken("s3cr3t-Zx9", s_start.AddMinutes(-1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        var ex = await Assert.ThrowsAsync<TokenProviderException>(() => cache.GetAsync(Ctx(), Ct).AsTask());

        Assert.DoesNotContain("s3cr3t-Zx9", ex.Message, StringComparison.Ordinal);
        Assert.False(ex.IsRetryable);
    }

    [Fact]
    public async Task A_provider_exception_propagates_unwrapped_and_nothing_is_cached()
    {
        var failure = new InvalidOperationException("boom");
        var cred = new GatedCredential { Immediate = _ => throw failure };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(Ctx(), Ct).AsTask()));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => cache.Get(Ctx(), Ct)));
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task A_synchronous_throw_from_a_non_async_GetTokenAsync_override_surfaces_through_the_returned_task()
    {
        var failure = new InvalidOperationException("sync throw");
        var cache = new AccessTokenCache(new SyncThrowingCredential(failure), new FakeTimeProvider(s_start));

        ValueTask<AccessToken> task = default;
        var exception = Record.Exception(() => task = cache.GetAsync(Ctx(), Ct));

        Assert.Null(exception);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => task.AsTask()));
    }

    // ---- Sync path (P6c-26) ---------------------------------------------------

    [Fact]
    public async Task Get_returns_the_same_tokens_as_GetAsync()
    {
        var time = new FakeTimeProvider(s_start);
        var cred = new GatedCredential { Immediate = c => Tok("tok-" + c.Scopes[0], TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, time);

        Assert.Equal("tok-s", cache.Get(Ctx("s"), Ct).Token);
        Assert.Equal("tok-s", (await cache.GetAsync(Ctx("s"), Ct)).Token);
        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task Get_honours_cancellation_while_waiting()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        var holder = cache.GetAsync(Ctx(), Ct).AsTask(); // holds the gate
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() => cache.Get(Ctx(), cts.Token), Ct));

        cred.Release(0, Tok("t", TimeSpan.FromHours(1)));
        _ = await holder;
    }

    [Fact]
    public void Get_uses_TokenCredential_GetToken_not_GetTokenAsync()
    {
        var cred = new SyncOnlyCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        var token = cache.Get(Ctx(), Ct);

        Assert.Equal("sync", token.Token);
        Assert.Equal(1, cred.SyncCalls);
    }

    [Fact]
    public async Task A_sync_and_an_async_waiter_coalesce_onto_one_fetch()
    {
        var cred = new BlockingSyncCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        var syncFetch = Task.Run(() => cache.Get(Ctx(), Ct), Ct);
        cred.Entered.Wait(Ct);

        var asyncWaiter = cache.GetAsync(Ctx(), Ct).AsTask(); // queues on the gate the sync fetch holds
        cred.Proceed.Set();

        Assert.Equal("blocking", (await syncFetch).Token);
        Assert.Equal("blocking", (await asyncWaiter).Token);
        Assert.Equal(1, cred.Calls);
    }

    // ---- Bound (XCUT-14, P6c-28) ----------------------------------------------

    [Fact]
    public async Task The_cache_holds_at_most_1024_keys_once_quiescent()
    {
        var cred = new GatedCredential { Immediate = _ => Tok("t", TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        for (var i = 0; i < 3000; i++)
        {
            _ = await cache.GetAsync(new TokenRequestContext(["s"], "claims-" + i), Ct);
        }

        Assert.Equal(1024, cache.EntryCapacity);
        Assert.True(cache.EntryCount <= cache.EntryCapacity, $"{cache.EntryCount} entries");
    }

    [Fact]
    public async Task An_evicted_live_entry_costs_one_extra_fetch_and_no_failure()
    {
        var cred = new GatedCredential { Immediate = _ => Tok("t", TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        for (var i = 0; i < 2000; i++)
        {
            _ = await cache.GetAsync(new TokenRequestContext(["s"], "claims-" + i), Ct);
        }

        var calls = cred.Calls;
        var token = await cache.GetAsync(new TokenRequestContext(["s"], "claims-0"), Ct);

        Assert.Equal("t", token.Token);
        Assert.InRange(cred.Calls - calls, 0, 1);
    }

    [Fact]
    public async Task The_cache_key_includes_claims()
    {
        var cred = new GatedCredential { Immediate = _ => Tok("t", TimeSpan.FromHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));

        _ = await cache.GetAsync(new TokenRequestContext(["s"]), Ct);
        _ = await cache.GetAsync(new TokenRequestContext(["s"], "c1"), Ct);
        _ = await cache.GetAsync(new TokenRequestContext(["s"], "c1"), Ct);

        Assert.Equal(2, cred.Calls);
    }

    // ---- Rejection path (AUTH-36, AUTH-37) ---------------------------------------

    [Fact]
    public async Task A_rejection_of_the_current_token_evicts_it_and_fetches_fresh()
    {
        var counter = 0;
        var cred = new GatedCredential { Immediate = _ => new AccessToken("t" + Interlocked.Increment(ref counter), s_start.AddHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        Assert.Equal("t1", (await cache.GetAsync(Ctx(), Ct)).Token);

        var fresh = await cache.GetAfterRejectionAsync(Ctx(), "Bearer t1", Ct);

        Assert.Equal("t2", fresh.Token);
        Assert.Equal(2, cred.Calls);
        Assert.Equal("t3", cache.GetAfterRejection(Ctx(), "Bearer t2", Ct).Token);
    }

    [Fact]
    public async Task A_rejection_of_a_stale_header_keeps_a_token_another_request_already_refreshed()
    {
        var cred = new GatedCredential { Immediate = _ => new AccessToken("new", s_start.AddHours(1)) };
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        _ = await cache.GetAsync(Ctx(), Ct); // cached: Bearer new

        var kept = await cache.GetAfterRejectionAsync(Ctx(), "Bearer old", Ct);

        Assert.Equal("new", kept.Token);
        Assert.Equal(1, cred.Calls);
    }

    [Fact]
    public async Task Eviction_and_fetch_happen_under_one_critical_section()
    {
        var cred = new GatedCredential();
        var cache = new AccessTokenCache(cred, new FakeTimeProvider(s_start));
        var seed = cache.GetAsync(Ctx(), Ct);
        cred.Release(0, Tok("t1", TimeSpan.FromHours(1)));
        _ = await seed;

        var a = cache.GetAfterRejectionAsync(Ctx(), "Bearer t1", Ct).AsTask();
        var b = cache.GetAfterRejectionAsync(Ctx(), "Bearer t1", Ct).AsTask();
        cred.Release(1, Tok("t2", TimeSpan.FromHours(1)));

        Assert.Equal("t2", (await a).Token);
        Assert.Equal("t2", (await b).Token);
        Assert.Equal(2, cred.Calls);
    }

    [Fact]
    public async Task A_rejection_waits_for_a_background_refresh_in_flight_and_takes_its_result()
    {
        var (cache, cred, _) = await ExpiringAsync();
        _ = await cache.GetAsync(Ctx(), Ct); // starts the background refresh (holds the gate)
        await cred.WaitForCallsAsync(2);

        var rejection = cache.GetAfterRejectionAsync(Ctx(), "Bearer old", Ct).AsTask();
        cred.Release(1, new AccessToken("new", s_start.AddHours(1)));

        Assert.Equal("new", (await rejection).Token);
        Assert.Equal(2, cred.Calls);
    }
}

internal sealed class GatedCredential : TokenCredential
{
    private readonly List<TaskCompletionSource<AccessToken>> _pending = [];
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Which scopes are gated; by default every call is gated unless <see cref="Immediate"/> is set.</summary>
    public Func<string, bool>? Gated { get; init; }

    public Func<TokenRequestContext, AccessToken>? Immediate { get; init; }

    public async Task WaitForCallsAsync(int count)
    {
        while (Calls < count)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    public void Release(int index, AccessToken token) => Pending(index).SetResult(token);

    public void Fail(int index, Exception exception) => Pending(index).SetException(exception);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
    {
        var scope = context.Scopes.Count > 0 ? context.Scopes[0] : string.Empty;
        var gated = Gated is { } predicate ? predicate(scope) : Immediate is null;
        if (!gated)
        {
            Interlocked.Increment(ref _calls);
            return new ValueTask<AccessToken>(Immediate!(context));
        }

        var tcs = new TaskCompletionSource<AccessToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending)
        {
            _pending.Add(tcs);
        }

        Interlocked.Increment(ref _calls);
        return new ValueTask<AccessToken>(tcs.Task.WaitAsync(ct));
    }

    private TaskCompletionSource<AccessToken> Pending(int index)
    {
        lock (_pending)
        {
            return _pending[index];
        }
    }
}

file sealed class SyncThrowingCredential(Exception failure) : TokenCredential
{
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        throw failure;
}

file sealed class SyncOnlyCredential : TokenCredential
{
    private int _syncCalls;

    public int SyncCalls => Volatile.Read(ref _syncCalls);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        throw new InvalidOperationException("The sync path must call GetToken.");

    public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _syncCalls);
        return new AccessToken("sync");
    }
}

file sealed class BlockingSyncCredential : TokenCredential
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public ManualResetEventSlim Entered { get; } = new(false);

    public ManualResetEventSlim Proceed { get; } = new(false);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        throw new InvalidOperationException("Only the sync path is used.");

    public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        Entered.Set();
        Proceed.Wait(ct);
        return new AccessToken("blocking");
    }
}

// Helper that cannot easily be expressed as a lambda:

file sealed class SlowTokenCredential : TokenCredential
{
    private readonly TaskCompletionSource<AccessToken> _gate;
    private int _callCount;

    public int CallCount => _callCount;

    public SlowTokenCredential(TaskCompletionSource<AccessToken> gate)
        => _gate = gate;

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _callCount);
        return await _gate.Task.ConfigureAwait(false);
    }
}

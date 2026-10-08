// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// A per-key, bounded cache of access tokens in front of a <see cref="TokenCredential"/> (AUTH-11, AUTH-34 to AUTH-37).
/// </summary>
/// <remarks>
/// <para>
/// For a cached token <c>t</c> and margin <c>M</c> (<see cref="RefreshMargin"/>, 30 seconds by default), a request finds
/// the token in one of three zones. <b>Fresh</b> (<c>t</c> is not within <c>M</c> of its expiry and not past its
/// <see cref="AccessToken.RefreshOn"/>): stamped at once, no lock taken. <b>Expiring</b> (still valid, but not fresh): the
/// valid token is stamped now and at most one background refresh per key is started. <b>Expired or missing</b>: the
/// request awaits a foreground fetch. The cached token's own expiry is judged with AUTH-10's strict "after".
/// </para>
/// <para>
/// A burst of requests at expiry costs one fetch: a waiter that finds a fetch finished while it waited adopts that
/// outcome, the new token or the same failure, instead of fetching again, so a provider outage fails the waiters
/// together after one attempt. A failure is adopted only by waiters already waiting, so no failure is cached, and a
/// fetcher's own cancellation is never adopted. Coalesced failures share one exception instance. The background
/// refresh runs under <see cref="CancellationToken.None"/> and cannot be cancelled; it captures no ambient state, and a
/// failure is logged (event 160) and fails nothing. Entries live in a bounded map of 1024 keys; evicting a live entry
/// costs at most one extra fetch.
/// </para>
/// <para>
/// A token the provider returns that is <c>default</c> or already expired is a <see cref="TokenProviderException"/>, and
/// a provider exception propagates unwrapped; neither is cached.
/// </para>
/// <para>
/// <b>Breaking:</b> tokens are refreshed 30 seconds before expiry by default (was: at expiry); a still-valid token is
/// stamped while the refresh runs in the background (was: the refresh was awaited on the request path); a failed refresh
/// while the token is valid is logged (was: silent); a default or already-expired provider token is rejected; waiters
/// share one fetch's outcome; the cache is bounded.
/// </para>
/// </remarks>
public sealed class AccessTokenCache
{
    private const int Capacity = 1024;

    private readonly TokenCredential _credential;
    private readonly TimeProvider _time;
    private readonly BoundedMap<string, CacheEntry> _entries = new(Capacity);

    /// <summary>Initializes a cache over <paramref name="credential"/>.</summary>
    /// <param name="credential">The provider the cache fronts.</param>
    /// <param name="timeProvider">The clock; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is <see langword="null"/>.</exception>
    public AccessTokenCache(TokenCredential credential, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential = credential;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The default refresh margin: 30 seconds (AUTH-34).</summary>
    public static TimeSpan DefaultRefreshMargin { get; } = TimeSpan.FromSeconds(30);

    /// <summary>How long before expiry a token counts as expiring and is refreshed in the background.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan RefreshMargin
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = DefaultRefreshMargin;

    internal int EntryCount => _entries.EntryCount;

    internal int EntryCapacity => _entries.Capacity;

    /// <summary>Returns a token for <paramref name="context"/>, fetching one when none is usable.</summary>
    /// <param name="context">The scopes and claims requested.</param>
    /// <param name="ct">Cancels this caller's wait, never a fetch another caller is running.</param>
    /// <returns>The token.</returns>
    public ValueTask<AccessToken> GetAsync(TokenRequestContext context, CancellationToken ct = default) =>
        GetAsync(context, NullLogger.Instance, ct);

    /// <summary>Returns a token for <paramref name="context"/> on the synchronous path (P6c-26).</summary>
    /// <param name="context">The scopes and claims requested.</param>
    /// <param name="ct">Cancels this caller's wait.</param>
    /// <returns>The token.</returns>
    public AccessToken Get(TokenRequestContext context, CancellationToken ct = default) =>
        Get(context, NullLogger.Instance, ct);

    internal ValueTask<AccessToken> GetAsync(TokenRequestContext context, ILogger logger, CancellationToken ct)
    {
        var entry = EntryFor(context);
        if (TryUseCached(entry, context, logger, out var cached))
        {
            return new ValueTask<AccessToken>(cached);
        }

        return FetchAsync(entry, context, async: true, ct);
    }

    internal AccessToken Get(TokenRequestContext context, ILogger logger, CancellationToken ct)
    {
        var entry = EntryFor(context);
        return TryUseCached(entry, context, logger, out var cached)
            ? cached
            : SyncPath.GetResult(FetchAsync(entry, context, async: false, ct));
    }

    internal ValueTask<AccessToken> GetAfterRejectionAsync(
        TokenRequestContext context,
        string rejectedHeaderValue,
        CancellationToken ct) =>
        RejectAsync(EntryFor(context), context, rejectedHeaderValue, async: true, ct);

    internal AccessToken GetAfterRejection(TokenRequestContext context, string rejectedHeaderValue, CancellationToken ct) =>
        SyncPath.GetResult(RejectAsync(EntryFor(context), context, rejectedHeaderValue, async: false, ct));

    internal Task? PendingRefresh(TokenRequestContext context) =>
        _entries.TryGetValue(context.CacheKey, out var entry) ? entry.RefreshTask : null;

    private static ZoneKind Classify(AccessToken token, DateTimeOffset now, TimeSpan margin)
    {
        if (token.IsExpired(now, TimeSpan.Zero))
        {
            return ZoneKind.Expired;
        }

        return !token.IsExpired(now, margin) && (token.RefreshOn is not { } refreshOn || now < refreshOn)
            ? ZoneKind.Fresh
            : ZoneKind.Expiring;
    }

    private static void Validate(AccessToken token, DateTimeOffset now)
    {
        if (token.Token is null)
        {
            throw new TokenProviderException("The token provider returned a default token.");
        }

        if (token.IsExpired(now, TimeSpan.Zero))
        {
            throw new TokenProviderException("The token provider returned a token that is already expired.");
        }
    }

    private static void Publish(CacheEntry entry, FetchOutcome outcome)
    {
        if (outcome.Token is { } token)
        {
            entry.Holder = new TokenHolder(token);
        }

        entry.Last = outcome;
        entry.AdvanceGeneration();
    }

    private CacheEntry EntryFor(TokenRequestContext context) =>
        _entries.GetOrAdd(context.CacheKey, static () => new CacheEntry());

    // The hot path: no lock. A fresh token is returned; an expiring one is returned and one refresh is started.
    private bool TryUseCached(CacheEntry entry, TokenRequestContext context, ILogger logger, out AccessToken token)
    {
        token = default;
        if (entry.Holder is not { } holder)
        {
            return false;
        }

        switch (Classify(holder.Token, _time.GetUtcNow(), RefreshMargin))
        {
            case ZoneKind.Fresh:
                token = holder.Token;
                return true;
            case ZoneKind.Expiring:
                StartBackgroundRefresh(entry, context, logger);
                token = holder.Token;
                return true;
            default:
                return false;
        }
    }

    private void StartBackgroundRefresh(CacheEntry entry, TokenRequestContext context, ILogger logger)
    {
        // Wait(0) never blocks: if the gate is taken a fetch is already running and this request just uses its valid token.
        if (!entry.Gate.Wait(0))
        {
            return;
        }

        try
        {
            if (entry.Holder is { } holder && Classify(holder.Token, _time.GetUtcNow(), RefreshMargin) == ZoneKind.Fresh)
            {
                entry.Gate.Release();
                return;
            }

            entry.RefreshTask = BackgroundWork.Run(() => RefreshInBackgroundAsync(entry, context, logger));
        }
        catch
        {
            entry.Gate.Release();
            throw;
        }
    }

    private async Task RefreshInBackgroundAsync(CacheEntry entry, TokenRequestContext context, ILogger logger)
    {
        try
        {
            // The refresh outlives the request that triggered it, so it runs under no token; a provider bounds its own call.
            var token = await _credential.GetTokenAsync(context, CancellationToken.None).ConfigureAwait(false);
            Validate(token, _time.GetUtcNow());
            Publish(entry, new FetchOutcome(token, null, Canceled: false));
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Publish(entry, new FetchOutcome(null, ExceptionDispatchInfo.Capture(ex), Canceled: false));
            AuthLog.TokenRefreshFailed(logger, ex);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private async ValueTask<AccessToken> FetchAsync(
        CacheEntry entry,
        TokenRequestContext context,
        bool async,
        CancellationToken ct)
    {
        var generation = entry.ReadGeneration();
        await AcquireAsync(entry, async, ct).ConfigureAwait(false);
        try
        {
            // A fetch finished while this caller waited: adopt its outcome instead of fetching again.
            if (entry.ReadGeneration() != generation && entry.Last is { Canceled: false } last)
            {
                last.Failure?.Throw();
                if (last.Token is { } adopted)
                {
                    return adopted;
                }
            }

            if (entry.Holder is { } holder && Classify(holder.Token, _time.GetUtcNow(), RefreshMargin) != ZoneKind.Expired)
            {
                return holder.Token;
            }

            return await FetchUnderGateAsync(entry, context, async, ct).ConfigureAwait(false);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private async ValueTask<AccessToken> RejectAsync(
        CacheEntry entry,
        TokenRequestContext context,
        string rejectedHeaderValue,
        bool async,
        CancellationToken ct)
    {
        await AcquireAsync(entry, async, ct).ConfigureAwait(false);
        try
        {
            // AUTH-36: evict only the token that was rejected; one another request already replaced survives.
            if (entry.Holder is { } holder
                && string.Equals("Bearer " + holder.Token.Token, rejectedHeaderValue, StringComparison.Ordinal))
            {
                entry.Holder = null;
            }

            if (entry.Holder is { } current && Classify(current.Token, _time.GetUtcNow(), RefreshMargin) != ZoneKind.Expired)
            {
                return current.Token;
            }

            return await FetchUnderGateAsync(entry, context, async, ct).ConfigureAwait(false);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private static async ValueTask AcquireAsync(CacheEntry entry, bool async, CancellationToken ct)
    {
        if (async)
        {
            await entry.Gate.WaitAsync(ct).ConfigureAwait(false);
        }
        else
        {
            entry.Gate.Wait(ct);
        }
    }

    // The gate is held. Fetch, validate, publish and record the outcome; a failure is recorded for the waiters and rethrown.
    private async ValueTask<AccessToken> FetchUnderGateAsync(
        CacheEntry entry,
        TokenRequestContext context,
        bool async,
        CancellationToken ct)
    {
        try
        {
            var token = async
                ? await _credential.GetTokenAsync(context, ct).ConfigureAwait(false)
                : _credential.GetToken(context, ct);
            Validate(token, _time.GetUtcNow());
            Publish(entry, new FetchOutcome(token, null, Canceled: false));
            return token;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The fetcher's own cancellation is not the waiters' outcome: the next one fetches.
            Publish(entry, new FetchOutcome(null, null, Canceled: true));
            throw;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Publish(entry, new FetchOutcome(null, ExceptionDispatchInfo.Capture(ex), Canceled: false));
            throw;
        }
    }

    private enum ZoneKind
    {
        Fresh,
        Expiring,
        Expired,
    }

    // Immutable wrapper so the token can be published through a single volatile reference, giving acquire/release
    // ordering on all platforms. AccessToken is a multi-field struct; publishing it directly would allow fast-path readers
    // to observe a torn value.
    private sealed class TokenHolder(AccessToken token)
    {
        public AccessToken Token { get; } = token;
    }

    private sealed record FetchOutcome(AccessToken? Token, ExceptionDispatchInfo? Failure, bool Canceled);

    private sealed class CacheEntry
    {
        // The generation advances once per finished fetch, after Last is set.
        private int _generation;
        private volatile TokenHolder? _holder;
        private volatile FetchOutcome? _last;

        internal TokenHolder? Holder
        {
            get => _holder;
            set => _holder = value;
        }

        internal FetchOutcome? Last
        {
            get => _last;
            set => _last = value;
        }

        internal int ReadGeneration() => Volatile.Read(ref _generation);

        internal void AdvanceGeneration() => Interlocked.Increment(ref _generation);

        internal SemaphoreSlim Gate { get; } = new(1, 1);

        internal Task? RefreshTask { get; set; }
    }
}

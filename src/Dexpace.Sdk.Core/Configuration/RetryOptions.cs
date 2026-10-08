// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Resilience;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Options for the retry policy and the recovery-stack retry (<c>RetryRecovery</c>).
/// </summary>
/// <remarks>
/// <para>
/// A sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with <see langword="with"/>
/// (CFG-9). Every member is validated in its accessor (RECOV-34), so an invalid value cannot be configured and a
/// <see langword="with"/> expression that sets one throws <see cref="ArgumentOutOfRangeException"/>. Equality is by
/// value, comparing <see cref="RetryableStatusCodes"/> by content.
/// </para>
/// <para>
/// <b>Breaking:</b> this was a mutable class; assigning a property after construction no longer compiles, and equality
/// and the hash code are by value (was: by reference).
/// </para>
/// <para>
/// <b>Breaking:</b> <see cref="MaxRetryAttempts"/> defaults to <c>2</c> (was <c>3</c>: three sends, was four) and
/// <see cref="MaxDelay"/> to <c>8 s</c> (was <c>30 s</c>) (RETRY-12).
/// </para>
/// <para>
/// <b>Breaking:</b> <c>RetryNonIdempotentWhenReplayable</c> is removed. A request whose body is replayable is re-sent
/// whatever its method, and a bodiless request is re-sent only when its method is idempotent (RETRY-5, RETRY-7); there
/// is no switch for either.
/// </para>
/// <para>
/// <b>Breaking:</b> the members validate at <see langword="init"/>: a negative count or duration, a duration above
/// about 292 years, a multiplier below 1 or not finite, a jitter outside <c>[0, 1]</c>, a status outside 400 to 599 and
/// an attempt-header name that is not an HTTP token throw (RECOV-34, P6a-13).
/// </para>
/// </remarks>
public sealed record RetryOptions
{
    // RECOV-34: "representable in nanoseconds (~292-year ceiling)". TimeSpan.MaxValue is about 29 000 years, so the bound
    // is checked explicitly (design 6.1).
    private static readonly TimeSpan s_maxRepresentable = TimeSpan.FromTicks(long.MaxValue / 100);

    /// <summary>
    /// The number of retries after the initial send, so the call is sent at most <c>MaxRetryAttempts + 1</c> times;
    /// <c>0</c> disables retries. Defaults to <c>2</c> (three sends). Matches the Polly v8 /
    /// <c>Microsoft.Extensions.Http.Resilience</c> naming convention. <c>RequestOptions.MaxRetries</c> overrides it for
    /// one call (RETRY-41).
    /// </summary>
    /// <remarks><b>Breaking:</b> the default was <c>3</c>, and a negative value is now rejected.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxRetryAttempts
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 2;

    /// <summary>
    /// The delay before the first retry; the schedule is <c>BaseDelay × Multiplier^(attempt − 1)</c>, capped at
    /// <see cref="MaxDelay"/>. Defaults to <c>200 ms</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above about 292 years.</exception>
    public TimeSpan BaseDelay
    {
        get;
        init
        {
            RequireDuration(value);
            field = value;
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The growth factor of the schedule. Defaults to <c>2.0</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is below <c>1.0</c>, <c>NaN</c> or infinite.</exception>
    public double Multiplier
    {
        get;
        init
        {
            if (!double.IsFinite(value) || value < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The multiplier must be a finite number of at least 1.0.");
            }

            field = value;
        }
    } = 2.0;

    /// <summary>
    /// The maximum delay of the schedule, before jitter. Defaults to <c>8 s</c>.
    /// </summary>
    /// <remarks><b>Breaking:</b> the default was <c>30 s</c>, and jitter is now symmetric around the capped delay.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above about 292 years.</exception>
    public TimeSpan MaxDelay
    {
        get;
        init
        {
            RequireDuration(value);
            field = value;
        }
    } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// The width of the symmetric jitter band as a fraction of the delay: the wait is drawn from
    /// <c>[d × (1 − Jitter / 2), d × (1 + Jitter / 2)]</c>. <c>0</c> disables jitter. Defaults to <c>0.2</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside <c>[0, 1]</c> or <c>NaN</c>.</exception>
    public double Jitter
    {
        get;
        init
        {
            if (!(value >= 0.0 && value <= 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The jitter fraction must lie in [0, 1].");
            }

            field = value;
        }
    } = 0.2;

    /// <summary>
    /// When set, every retry waits exactly this long: no growth, no jitter and no <see cref="MaxDelay"/> cap (RETRY-43).
    /// A server pacing hint still takes precedence (RETRY-39). Defaults to <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above about 292 years.</exception>
    public TimeSpan? FixedDelay
    {
        get;
        init
        {
            if (value is { } fixedDelay)
            {
                RequireDuration(fixedDelay);
            }

            field = value;
        }
    }

    /// <summary>
    /// When <see langword="true"/>, the retry policy honours the server's pacing headers (<c>Retry-After</c> as seconds or
    /// an HTTP-date, <c>retry-after-ms</c>, <c>x-ms-retry-after-ms</c> and <c>X-RateLimit-Reset</c>) in that fixed order
    /// (RETRY-21). Defaults to <see langword="true"/>. The recovery-stack retry always honours them (RECOV-22).
    /// </summary>
    /// <remarks><b>Breaking:</b> this governed <c>Retry-After</c> alone; it now governs all four headers.</remarks>
    public bool HonorRetryAfter { get; init; } = true;

    /// <summary>
    /// The response statuses that are retried. Defaults to <c>{408, 429, 500, 502, 503, 504}</c> (XCUT-7). The set is
    /// copied when assigned, so a later change to the caller's collection has no effect.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A member is outside 400 to 599.</exception>
    public IReadOnlySet<int> RetryableStatusCodes
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var code in value)
            {
                if (code is < 400 or > 599)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "A retryable status must be an error status, 400 to 599.");
                }
            }

            field = value.ToFrozenSet();
        }
    } = RetryFacts.DefaultRetryableStatusCodes;

    /// <summary>
    /// The name of a request header that carries the 1-based send number on every attempt, or <see langword="null"/> (the
    /// default) for none (RETRY-38, RECOV-31).
    /// </summary>
    /// <exception cref="ArgumentException">The value is not an HTTP token.</exception>
    public string? AttemptHeaderName
    {
        get;
        init
        {
            if (value is not null && !HttpHeaderSyntax.IsValidName(value))
            {
                throw new ArgumentException("The attempt header name must be a valid HTTP header name (an RFC 9110 token).", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>Compares two options by value; the status sets compare by content.</summary>
    /// <param name="other">The other options.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool Equals(RetryOptions? other) =>
        other is not null
        && MaxRetryAttempts == other.MaxRetryAttempts
        && BaseDelay == other.BaseDelay
        && Multiplier.Equals(other.Multiplier)
        && MaxDelay == other.MaxDelay
        && Jitter.Equals(other.Jitter)
        && FixedDelay == other.FixedDelay
        && HonorRetryAfter == other.HonorRetryAfter
        && string.Equals(AttemptHeaderName, other.AttemptHeaderName, StringComparison.Ordinal)
        && RetryableStatusCodes.SetEquals(other.RetryableStatusCodes);

    /// <summary>Returns a hash code consistent with <see cref="Equals(RetryOptions?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MaxRetryAttempts);
        hash.Add(BaseDelay);
        hash.Add(Multiplier);
        hash.Add(MaxDelay);
        hash.Add(Jitter);
        hash.Add(FixedDelay);
        hash.Add(HonorRetryAfter);
        hash.Add(AttemptHeaderName, StringComparer.Ordinal);

        // An order-independent combination, so two sets with equal content hash alike whatever their insertion order.
        var statuses = 0;
        foreach (var code in RetryableStatusCodes)
        {
            statuses ^= code * 397;
        }

        hash.Add(statuses);
        return hash.ToHashCode();
    }

    private static void RequireDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value > s_maxRepresentable)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A duration must be zero or greater and no more than about 292 years.");
        }
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("MaxRetryAttempts = ").Append(MaxRetryAttempts);
        builder.Append(", BaseDelay = ").Append(BaseDelay);
        builder.Append(", Multiplier = ").Append(Multiplier);
        builder.Append(", MaxDelay = ").Append(MaxDelay);
        builder.Append(", Jitter = ").Append(Jitter);
        builder.Append(", FixedDelay = ").Append(FixedDelay);
        builder.Append(", HonorRetryAfter = ").Append(HonorRetryAfter);
        builder.Append(", RetryableStatusCodes = [").AppendJoin(", ", RetryableStatusCodes.Order()).Append(']');
        builder.Append(", AttemptHeaderName = ").Append(AttemptHeaderName);
        return true;
    }
}

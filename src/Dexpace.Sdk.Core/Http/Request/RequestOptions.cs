// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Auth;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// Per-call overrides that travel beside a request: a timeout, a retry budget and free-form tags (HTTP-34, HTTP-35).
/// </summary>
/// <remarks>
/// A <see langword="null"/> <see cref="Timeout"/> or <see cref="MaxRetries"/> means: do not override, so the client's
/// configured value applies. Validation lives in the <see langword="init"/> accessors, so a <see langword="with"/>
/// expression cannot bypass it. The upper bound of <see cref="Timeout"/> against a transport's ceiling is the
/// transport's rule (TRANSPORT-5), not a model rule. Equality compares <see cref="Tags"/> by content. Derive a
/// modified copy with <see langword="with"/> or <see cref="WithTag(string,string)"/>; instances never alias
/// (HTTP-3).
/// </remarks>
public sealed record RequestOptions
{
    private static readonly ImmutableDictionary<string, string> s_noTags =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>An options value that overrides nothing and carries no tags.</summary>
    public static RequestOptions Empty { get; } = new();

    /// <summary>
    /// The per-call timeout, or <see langword="null"/> to keep the configured one. When set it must be positive,
    /// which also rejects <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public TimeSpan? Timeout
    {
        get;
        init => field = RequireNullOrPositive(value);
    }

    /// <summary>
    /// The per-call retry budget, or <see langword="null"/> to keep the configured one. Zero means no retries for
    /// this call.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? MaxRetries
    {
        get;
        init => field = RequireNullOrNonNegative(value);
    }

    /// <summary>
    /// Free-form call tags; never <see langword="null"/>. Keys are compared ordinally: an assigned dictionary built
    /// with another comparer is re-based on <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public ImmutableDictionary<string, string> Tags
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = ReferenceEquals(value.KeyComparer, StringComparer.Ordinal)
                ? value
                : value.WithComparers(StringComparer.Ordinal);
        }
    } = s_noTags;

    /// <summary>
    /// The per-call authentication tier (AUTH-4): the descriptor the call is authenticated with, taking precedence over
    /// <see cref="OperationAuth"/> and the policy's own descriptor. <see langword="null"/> (the default) defers to them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A descriptor holding <see cref="AuthRequirement.NoAuth"/> sends the call anonymously through any auth policy.
    /// </para>
    /// <para>
    /// <b>Breaking:</b> equality now includes <see cref="Auth"/> and <see cref="OperationAuth"/>; two options differing
    /// only in a descriptor were equal before.
    /// </para>
    /// </remarks>
    public AuthDescriptor? Auth { get; init; }

    /// <summary>
    /// The operation authentication tier (AUTH-4), set by generated code from the operation's security list; consulted
    /// when <see cref="Auth"/> is <see langword="null"/>.
    /// </summary>
    public AuthDescriptor? OperationAuth { get; init; }

    /// <summary>Returns a copy with the tag <paramref name="key"/> added or replaced.</summary>
    /// <param name="key">The tag key (ordinal).</param>
    /// <param name="value">The tag value.</param>
    /// <returns>A new <see cref="RequestOptions"/>; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    public RequestOptions WithTag(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return this with { Tags = Tags.SetItem(key, value) };
    }

    /// <summary>Equality over <see cref="Timeout"/>, <see cref="MaxRetries"/> and the tags by content.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool Equals(RequestOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Timeout != other.Timeout || MaxRetries != other.MaxRetries || Tags.Count != other.Tags.Count
            || !Equals(Auth, other.Auth) || !Equals(OperationAuth, other.OperationAuth))
        {
            return false;
        }

        foreach (var (key, value) in Tags)
        {
            if (!other.Tags.TryGetValue(key, out var theirs) || !string.Equals(value, theirs, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var tagHash = 0;
        foreach (var (key, value) in Tags)
        {
            tagHash += HashCode.Combine(key, value);
        }

        return HashCode.Combine(Timeout, MaxRetries, tagHash, Auth, OperationAuth);
    }

    private static TimeSpan? RequireNullOrPositive(TimeSpan? value)
    {
        if (value is { } span && span <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A timeout must be positive; use null to keep the configured one.");
        }

        return value;
    }

    private static int? RequireNullOrNonNegative(int? value)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "MaxRetries must be zero or greater; zero means no retries.");
        }

        return value;
    }
}

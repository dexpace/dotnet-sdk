// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The conditional-request preconditions of RFC 9110 §13: <c>If-Match</c>, <c>If-None-Match</c>,
/// <c>If-Modified-Since</c> and <c>If-Unmodified-Since</c> (HTTP-50).
/// </summary>
/// <remarks>
/// The <see langword="init"/> accessors normalise, so <see langword="with"/> cannot bypass them: a default
/// <see cref="ImmutableArray{T}"/> becomes empty, the <see cref="ETag.Any"/> wildcard is exclusive with concrete tags
/// (a mix throws <see cref="ArgumentException"/>), and a repeated <c>*</c> collapses to one. <see cref="ApplyTo(Headers)"/>
/// writes with <c>Set</c> (one comma-joined header per list, dates in <c>"R"</c> format converted to UTC) and leaves
/// unset members' headers untouched, so applying the same conditions twice is idempotent. Equality is sequence equality
/// over the arrays (an <see cref="ImmutableArray{T}"/>'s own equality compares the underlying array reference).
/// <para>
/// An obs-text <see cref="ETag"/> is valid under HTTP-48 but cannot be sent: <see cref="ApplyTo(Headers)"/> throws
/// <see cref="ArgumentException"/> because <c>Headers.Set</c> enforces the outbound ASCII rule (HTTP-18; ruling P2a-3).
/// </para>
/// </remarks>
public sealed record RequestConditions
{
    /// <summary>Conditions that set nothing.</summary>
    public static RequestConditions None { get; } = new();

    /// <summary>The <c>If-Match</c> tags; never default (empty when unset). <c>*</c> is exclusive.</summary>
    /// <exception cref="ArgumentException">The array mixes <c>*</c> with concrete tags.</exception>
    public ImmutableArray<ETag> IfMatch
    {
        get;
        init => field = Normalise(value, nameof(IfMatch));
    } = [];

    /// <summary>The <c>If-None-Match</c> tags; never default (empty when unset). <c>*</c> is exclusive.</summary>
    /// <exception cref="ArgumentException">The array mixes <c>*</c> with concrete tags.</exception>
    public ImmutableArray<ETag> IfNoneMatch
    {
        get;
        init => field = Normalise(value, nameof(IfNoneMatch));
    } = [];

    /// <summary>The <c>If-Modified-Since</c> instant, or <see langword="null"/> when unset.</summary>
    public DateTimeOffset? IfModifiedSince { get; init; }

    /// <summary>The <c>If-Unmodified-Since</c> instant, or <see langword="null"/> when unset.</summary>
    public DateTimeOffset? IfUnmodifiedSince { get; init; }

    /// <summary>
    /// Writes each set member into <paramref name="headers"/> with <c>Set</c> and leaves the headers of unset members
    /// untouched.
    /// </summary>
    /// <param name="headers">The headers to start from.</param>
    /// <returns>A new <see cref="Headers"/>, or <paramref name="headers"/> itself when nothing is set.</returns>
    /// <exception cref="ArgumentException">
    /// A tag holds a character the outbound rule rejects (obs-text, ruling P2a-3); the message names the code point and
    /// never echoes the value.
    /// </exception>
    public Headers ApplyTo(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var result = headers;
        result = SetTags(result, HttpHeaderName.WellKnown.IfMatch, IfMatch);
        result = SetTags(result, HttpHeaderName.WellKnown.IfNoneMatch, IfNoneMatch);
        result = SetDate(result, HttpHeaderName.WellKnown.IfModifiedSince, IfModifiedSince);
        return SetDate(result, HttpHeaderName.WellKnown.IfUnmodifiedSince, IfUnmodifiedSince);
    }

    /// <summary>Returns <paramref name="request"/> with these conditions applied to its headers.</summary>
    /// <param name="request">The request to derive from; it is left unchanged.</param>
    /// <returns>A new <see cref="Request"/>.</returns>
    public Request ApplyTo(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.WithHeaders(ApplyTo(request.Headers));
    }

    /// <summary>Sequence equality over the tag arrays, and equality of the two dates.</summary>
    /// <param name="other">The conditions to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool Equals(RequestConditions? other) =>
        other is not null
        && IfModifiedSince == other.IfModifiedSince
        && IfUnmodifiedSince == other.IfUnmodifiedSince
        && IfMatch.AsSpan().SequenceEqual(other.IfMatch.AsSpan())
        && IfNoneMatch.AsSpan().SequenceEqual(other.IfNoneMatch.AsSpan());

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IfModifiedSince);
        hash.Add(IfUnmodifiedSince);
        foreach (var tag in IfMatch)
        {
            hash.Add(tag);
        }

        hash.Add(IfMatch.Length);
        foreach (var tag in IfNoneMatch)
        {
            hash.Add(tag);
        }

        return hash.ToHashCode();
    }

    private static ImmutableArray<ETag> Normalise(ImmutableArray<ETag> tags, string paramName)
    {
        if (tags.IsDefaultOrEmpty)
        {
            return [];
        }

        var anyCount = tags.Count(tag => tag.IsAny);
        if (anyCount == 0)
        {
            return tags;
        }

        if (anyCount != tags.Length)
        {
            throw new ArgumentException("The * entity tag is exclusive with concrete tags.", paramName);
        }

        return [ETag.Any];
    }

    private static Headers SetTags(Headers headers, HttpHeaderName name, ImmutableArray<ETag> tags) =>
        tags.IsEmpty ? headers : headers.Set(name, string.Join(", ", tags));

    private static Headers SetDate(Headers headers, HttpHeaderName name, DateTimeOffset? date) =>
        date is { } instant
            ? headers.Set(name, instant.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture))
            : headers;
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;
using System.Collections.Immutable;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// An immutable, case-insensitive collection of HTTP headers that preserves multiple values
/// per name (in insertion order).
/// </summary>
/// <remarks>
/// Names are stored in lower-case canonical form (RFC 7230 §3.2 — field names are
/// case-insensitive). Lookups compare names case-insensitively; iteration yields the canonical
/// lower-cased name (the order of distinct names is unspecified). Values for a given name retain
/// insertion order. Mutation is non-destructive: <see cref="With(string,string)"/>,
/// <see cref="Set(string,string)"/>, and <see cref="Without(string)"/> each return a new
/// <see cref="Headers"/>. Use <see cref="ToBuilder"/> for batched edits.
/// <para>
/// <b>Validation (HTTP-17, HTTP-18, HTTP-20, XCUT-18).</b> Every entry point that writes a header validates it before
/// any transport sees it: surrounding SP/HTAB is trimmed from the name, which must then be an RFC 9110 token; an
/// outbound value may hold only HTAB and printable ASCII (0x20–0x7E), so CR, LF, NUL, other controls, DEL and non-ASCII
/// are rejected. Received headers go through <see cref="Builder.AddInbound(string,string)"/>, which also admits
/// obs-text (HTTP-19). A rejection is an <see cref="ArgumentException"/> naming the offending character by code point
/// (<c>U+000D</c>); it never echoes the value. <b>Breaking:</b> these writes previously accepted any string. Lookups
/// (<see cref="Get"/>, <see cref="GetAll"/>, <see cref="Contains"/>, <see cref="Without"/>,
/// <see cref="Builder.Remove"/>) trim the name the same way but do not validate it.
/// </para>
/// </remarks>
public sealed class Headers : IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>
{
    /// <summary>An empty header set.</summary>
    public static Headers Empty { get; } = new(ImmutableDictionary<string, ImmutableArray<string>>.Empty);

    private readonly ImmutableDictionary<string, ImmutableArray<string>> _values;

    private Headers(ImmutableDictionary<string, ImmutableArray<string>> values) => _values = values;

    /// <summary>The number of distinct header names present.</summary>
    public int Count => _values.Count;

    /// <summary>The distinct header names present, in canonical lower-case form.</summary>
    public IEnumerable<string> Names => _values.Keys;

    /// <summary>True when a header with <paramref name="name"/> is present.</summary>
    /// <param name="name">The header name (compared case-insensitively).</param>
    /// <returns><see langword="true"/> if at least one value is present.</returns>
    public bool Contains(string name) => _values.ContainsKey(Canonical(name));

    /// <summary>
    /// Returns the first value for <paramref name="name"/>, or <see langword="null"/> if absent.
    /// </summary>
    /// <param name="name">The header name (compared case-insensitively).</param>
    /// <returns>The first value, or <see langword="null"/>.</returns>
    public string? Get(string name) =>
        _values.TryGetValue(Canonical(name), out var list) && list.Length > 0 ? list[0] : null;

    /// <summary>Returns all values for <paramref name="name"/>, or an empty list if absent.</summary>
    /// <param name="name">The header name (compared case-insensitively).</param>
    /// <returns>The values in insertion order.</returns>
    public IReadOnlyList<string> GetAll(string name) =>
        _values.TryGetValue(Canonical(name), out var list) ? list : ImmutableArray<string>.Empty;

    /// <summary>
    /// Returns a copy with <paramref name="value"/> appended under <paramref name="name"/>, keeping
    /// any existing values for that name.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The value to append.</param>
    /// <returns>A new <see cref="Headers"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB and
    /// printable ASCII. New in phase 1 (breaking).
    /// </exception>
    public Headers With(string name, string value)
    {
        var key = OutboundKey(name, value);
        var existing = _values.TryGetValue(key, out var list) ? list : ImmutableArray<string>.Empty;
        return new Headers(_values.SetItem(key, existing.Add(value)));
    }

    /// <summary>
    /// Returns a copy with <paramref name="name"/> set to exactly <paramref name="value"/>, replacing
    /// any existing values for that name.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The single value.</param>
    /// <returns>A new <see cref="Headers"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB and
    /// printable ASCII. New in phase 1 (breaking).
    /// </exception>
    public Headers Set(string name, string value) =>
        new(_values.SetItem(OutboundKey(name, value), ImmutableArray.Create(value)));

    /// <summary>Returns a copy with every value for <paramref name="name"/> removed.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>A new <see cref="Headers"/> (or this instance if the name was absent).</returns>
    public Headers Without(string name)
    {
        var key = Canonical(name);
        return _values.ContainsKey(key) ? new Headers(_values.Remove(key)) : this;
    }

    /// <summary>Returns a mutable builder seeded with this set's contents.</summary>
    /// <returns>A <see cref="Builder"/>.</returns>
    public Builder ToBuilder() => new(_values);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator()
    {
        foreach (var (key, list) in _values)
        {
            yield return new KeyValuePair<string, IReadOnlyList<string>>(key, list);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The key a lookup or removal reads: trimmed of SP/HTAB and lower-cased exactly as a write stores it, but not
    // validated, so looking up a name no write could store simply finds nothing.
    private static string Canonical(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim(' ', '\t').ToLowerInvariant();
    }

    // The key a write stores under: the trimmed, token-validated name, lower-cased; the value is checked against the
    // outbound rule (HTTP-17, HTTP-18).
    private static string OutboundKey(string name, string value)
    {
        var trimmed = HeaderSyntax.ValidateName(name, nameof(name));
        HeaderSyntax.ValidateOutboundValue(value, trimmed, nameof(value));
        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// A mutable accumulator for building a <see cref="Headers"/> without allocating an intermediate
    /// instance per edit. Not thread-safe.
    /// </summary>
    public sealed class Builder
    {
        private readonly ImmutableDictionary<string, ImmutableArray<string>>.Builder _values;

        internal Builder(ImmutableDictionary<string, ImmutableArray<string>> seed) =>
            _values = seed.ToBuilder();

        /// <summary>Creates an empty builder.</summary>
        public Builder()
            : this(ImmutableDictionary<string, ImmutableArray<string>>.Empty)
        {
        }

        /// <summary>Appends <paramref name="value"/> under <paramref name="name"/>.</summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The value to append.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB
        /// and printable ASCII. New in phase 1 (breaking).
        /// </exception>
        public Builder Add(string name, string value) => Append(OutboundKey(name, value), value);

        /// <summary>
        /// Appends a <em>received</em> header — one a transport read off a response — under the lenient inbound rule
        /// (HTTP-19): the name is validated exactly as <see cref="Add"/> validates it, and the value may carry
        /// obs-text (0x80 and above) but still no control character other than HTAB, and no DEL. Use
        /// <see cref="Add"/> for anything a caller sets.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The value to append.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a control character.
        /// </exception>
        public Builder AddInbound(string name, string value)
        {
            var trimmed = HeaderSyntax.ValidateName(name, nameof(name));
            HeaderSyntax.ValidateInboundValue(value, trimmed, nameof(value));
            return Append(trimmed.ToLowerInvariant(), value);
        }

        /// <summary>Sets <paramref name="name"/> to exactly <paramref name="value"/>.</summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The single value.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB
        /// and printable ASCII. New in phase 1 (breaking).
        /// </exception>
        public Builder Set(string name, string value)
        {
            _values[OutboundKey(name, value)] = ImmutableArray.Create(value);
            return this;
        }

        /// <summary>Removes every value for <paramref name="name"/>.</summary>
        /// <param name="name">The header name.</param>
        /// <returns>This builder, for chaining.</returns>
        public Builder Remove(string name)
        {
            _values.Remove(Canonical(name));
            return this;
        }

        /// <summary>Builds the immutable <see cref="Headers"/>.</summary>
        /// <returns>A new <see cref="Headers"/>.</returns>
        public Headers Build() => new(_values.ToImmutable());

        private Builder Append(string key, string value)
        {
            var existing = _values.TryGetValue(key, out var list) ? list : ImmutableArray<string>.Empty;
            _values[key] = existing.Add(value);
            return this;
        }
    }
}

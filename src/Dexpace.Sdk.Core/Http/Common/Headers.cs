// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;
using System.Collections.Immutable;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// An immutable, case-insensitive, insertion-ordered collection of HTTP headers that preserves multiple values per
/// name (HTTP-13, HTTP-14, HTTP-15, HTTP-16, HTTP-21).
/// </summary>
/// <remarks>
/// Names are compared with an ASCII-only fold (RFC 9110 §5.1), never a culture-aware one, so a lookup with a
/// non-ASCII name finds nothing and does not throw. The collection keeps the <em>original casing of the first
/// insertion</em> of each name, in insertion order: <see cref="Names"/> and the enumerator yield that spelling, which
/// is what a transport puts on the wire. <see cref="Set(string,string?)"/> on an existing name keeps its position and
/// its first casing, because a replacement is not an insertion; <see cref="Without(string)"/> followed by
/// <see cref="With(string,string)"/> moves the name to the end. Values for a name keep insertion order. Mutation is
/// non-destructive: each edit returns a new <see cref="Headers"/>; use <see cref="ToBuilder"/> for batched edits. Equality
/// is by value: two sets are equal when they would serialise identically apart from name casing (the same folded names
/// in the same order with the same values).
/// <para>
/// <b>Validation (HTTP-17, HTTP-18, HTTP-20, XCUT-18).</b> Every entry point that writes a header validates it before
/// any transport sees it: surrounding SP/HTAB is trimmed from the name, which must then be an RFC 9110 token; an
/// outbound value may hold only HTAB and printable ASCII (0x20–0x7E), so CR, LF, NUL, other controls, DEL and non-ASCII
/// are rejected. Received headers go through <see cref="Builder.AddInbound(string,string)"/>, which also admits
/// obs-text (HTTP-19). A rejection is an <see cref="ArgumentException"/> naming the offending character by code point
/// (<c>U+000D</c>); it never echoes the value. Lookups trim the name the same way but do not validate it.
/// </para>
/// <para>
/// <b>Breaking (phase 2a):</b> the collection enumerates and lists the original casing in insertion order (it used to
/// yield lower-cased names in unspecified order); <see cref="Names"/> is an <see cref="IReadOnlyList{T}"/> (was an
/// <see cref="IEnumerable{T}"/>); <see cref="Set(string,string?)"/> takes a nullable value and <see langword="null"/>
/// removes the header; <c>Equals</c> and <c>==</c> are value equality (they were reference equality); and a non-ASCII
/// lookup name no longer folds (the Kelvin sign, <c>U+212A</c>, is not <c>k</c>).
/// </para>
/// </remarks>
public sealed class Headers : IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>, IEquatable<Headers>
{
    /// <summary>An empty header set.</summary>
    public static Headers Empty { get; } = new([]);

    private readonly ImmutableArray<Entry> _entries;
    private readonly Dictionary<string, int> _index;

    private Headers(ImmutableArray<Entry> entries)
    {
        _entries = entries;
        _index = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
        var names = ImmutableArray.CreateBuilder<string>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            _index[entries[i].Folded] = i;
            names.Add(entries[i].Original);
        }

        Names = names.MoveToImmutable();
    }

    /// <summary>The number of distinct header names present.</summary>
    public int Count => _entries.Length;

    /// <summary>
    /// The distinct header names present, each in the casing of its first insertion, in insertion order.
    /// </summary>
    /// <remarks>
    /// <b>Breaking:</b> was an <see cref="IEnumerable{T}"/> of lower-cased names in unspecified order. This is a
    /// read-only snapshot taken with the instance (HTTP-5): a later <see cref="ToBuilder"/> edit never reaches it.
    /// </remarks>
    public IReadOnlyList<string> Names { get; }

    /// <summary>True when a header with <paramref name="name"/> is present.</summary>
    /// <param name="name">The header name (compared with an ASCII-only fold).</param>
    /// <returns><see langword="true"/> if at least one value is present.</returns>
    public bool Contains(string name) => FindIndex(LookupKey(name)) >= 0;

    /// <summary>True when a header with <paramref name="name"/> is present.</summary>
    /// <param name="name">The typed header name.</param>
    /// <returns><see langword="true"/> if at least one value is present.</returns>
    public bool Contains(HttpHeaderName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindIndex(name.CanonicalName) >= 0;
    }

    /// <summary>Returns the first value for <paramref name="name"/>, or <see langword="null"/> if absent.</summary>
    /// <param name="name">The header name (compared with an ASCII-only fold).</param>
    /// <returns>The first value, or <see langword="null"/>.</returns>
    public string? Get(string name) => FirstValue(FindIndex(LookupKey(name)));

    /// <summary>Returns the first value for <paramref name="name"/>, or <see langword="null"/> if absent.</summary>
    /// <param name="name">The typed header name.</param>
    /// <returns>The first value, or <see langword="null"/>.</returns>
    public string? Get(HttpHeaderName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FirstValue(FindIndex(name.CanonicalName));
    }

    /// <summary>Returns all values for <paramref name="name"/>, or an empty list if absent.</summary>
    /// <param name="name">The header name (compared with an ASCII-only fold).</param>
    /// <returns>The values in insertion order, as a read-only list.</returns>
    public IReadOnlyList<string> GetAll(string name) => ValuesAt(FindIndex(LookupKey(name)));

    /// <summary>Returns all values for <paramref name="name"/>, or an empty list if absent.</summary>
    /// <param name="name">The typed header name.</param>
    /// <returns>The values in insertion order, as a read-only list.</returns>
    public IReadOnlyList<string> GetAll(HttpHeaderName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ValuesAt(FindIndex(name.CanonicalName));
    }

    /// <summary>
    /// Returns a copy with <paramref name="value"/> appended under <paramref name="name"/>, keeping any existing values
    /// for that name (and its position and first casing).
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The value to append.</param>
    /// <returns>A new <see cref="Headers"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB and
    /// printable ASCII.
    /// </exception>
    public Headers With(string name, string value)
    {
        var (original, folded) = WriteKey(name);
        HeaderSyntax.ValidateOutboundValue(value, original, nameof(value));
        return Append(original, folded, value);
    }

    /// <summary>Returns a copy with <paramref name="value"/> appended under the typed <paramref name="name"/>.</summary>
    /// <param name="name">The typed header name.</param>
    /// <param name="value">The value to append.</param>
    /// <returns>A new <see cref="Headers"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> holds a character outside HTAB and printable ASCII.</exception>
    public Headers With(HttpHeaderName name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        HeaderSyntax.ValidateOutboundValue(value, name.Original, nameof(value));
        return Append(name.Original, name.CanonicalName, value);
    }

    /// <summary>
    /// Returns a copy with <paramref name="name"/> set to exactly <paramref name="value"/>, replacing any existing
    /// values and keeping the name's position and first casing; <see langword="null"/> removes the header (HTTP-15).
    /// </summary>
    /// <remarks>
    /// <b>Breaking:</b> <paramref name="value"/> was non-nullable. Setting a name that is absent to
    /// <see langword="null"/> returns this instance.
    /// </remarks>
    /// <param name="name">The header name.</param>
    /// <param name="value">The single value, or <see langword="null"/> to remove the header.</param>
    /// <returns>A new <see cref="Headers"/>, or this instance if nothing changed.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB and
    /// printable ASCII.
    /// </exception>
    public Headers Set(string name, string? value)
    {
        var (original, folded) = WriteKey(name);
        if (value is not null)
        {
            HeaderSyntax.ValidateOutboundValue(value, original, nameof(value));
        }

        return Replace(original, folded, value);
    }

    /// <summary>
    /// Returns a copy with the typed <paramref name="name"/> set to exactly <paramref name="value"/>;
    /// <see langword="null"/> removes the header (HTTP-15).
    /// </summary>
    /// <param name="name">The typed header name.</param>
    /// <param name="value">The single value, or <see langword="null"/> to remove the header.</param>
    /// <returns>A new <see cref="Headers"/>, or this instance if nothing changed.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> holds a character outside HTAB and printable ASCII.</exception>
    public Headers Set(HttpHeaderName name, string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is not null)
        {
            HeaderSyntax.ValidateOutboundValue(value, name.Original, nameof(value));
        }

        return Replace(name.Original, name.CanonicalName, value);
    }

    /// <summary>Returns a copy with every value for <paramref name="name"/> removed.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>A new <see cref="Headers"/> (or this instance if the name was absent).</returns>
    public Headers Without(string name) => RemoveAt(FindIndex(LookupKey(name)));

    /// <summary>Returns a copy with every value for the typed <paramref name="name"/> removed.</summary>
    /// <param name="name">The typed header name.</param>
    /// <returns>A new <see cref="Headers"/> (or this instance if the name was absent).</returns>
    public Headers Without(HttpHeaderName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return RemoveAt(FindIndex(name.CanonicalName));
    }

    /// <summary>Returns a mutable builder seeded with this set's contents.</summary>
    /// <returns>A <see cref="Builder"/>; edits never reach this instance.</returns>
    public Builder ToBuilder() => new(_entries);

    /// <summary>Enumerates the names in the casing of their first insertion, in insertion order.</summary>
    /// <remarks><b>Breaking:</b> yielded lower-cased names in unspecified order before HTTP-16 and HTTP-21.</remarks>
    /// <returns>An enumerator over (name, values) pairs.</returns>
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator()
    {
        foreach (var entry in _entries)
        {
            yield return new KeyValuePair<string, IReadOnlyList<string>>(entry.Original, entry.View);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Value equality: the same folded names in the same order with the same values; the casing of a name is
    /// ignored (HTTP-13).
    /// </summary>
    /// <remarks><b>Breaking:</b> equality was by reference.</remarks>
    /// <param name="other">The set to compare with.</param>
    /// <returns><see langword="true"/> when both would serialise identically apart from name casing.</returns>
    public bool Equals(Headers? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_entries.Length != other._entries.Length)
        {
            return false;
        }

        for (var i = 0; i < _entries.Length; i++)
        {
            if (!string.Equals(_entries[i].Folded, other._entries[i].Folded, StringComparison.Ordinal)
                || !_entries[i].Items.AsSpan().SequenceEqual(other._entries[i].Items.AsSpan()))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    /// <remarks><b>Breaking:</b> value equality, see <see cref="Equals(Headers)"/>.</remarks>
    public override bool Equals(object? obj) => Equals(obj as Headers);

    /// <summary>A hash over the ordered folded names and values, excluding casing.</summary>
    /// <remarks><b>Breaking:</b> consistent with the new value equality.</remarks>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in _entries)
        {
            hash.Add(entry.Folded, StringComparer.Ordinal);
            foreach (var value in entry.Items)
            {
                hash.Add(value, StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Lists the header names only (<c>Headers[Accept, X-Trace]</c>), never the values, which may be credentials.
    /// </summary>
    /// <returns>The names, never the values.</returns>
    public override string ToString() => $"Headers[{string.Join(", ", Names)}]";

    /// <summary>Value equality.</summary>
    /// <remarks><b>Breaking:</b> was reference equality.</remarks>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both are equal or both are <see langword="null"/>.</returns>
    public static bool operator ==(Headers? left, Headers? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality.</summary>
    /// <remarks><b>Breaking:</b> was reference inequality.</remarks>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(Headers? left, Headers? right) => !(left == right);

    // The folded key a lookup or removal reads: trimmed of SP/HTAB and ASCII-folded exactly as a write stores it, but
    // not validated, so a name no write could store (including any non-ASCII name) simply finds nothing: null.
    private static string? LookupKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return AsciiFold.TryToLower(name.Trim(' ', '\t'));
    }

    // The (original, folded) pair a write stores: the trimmed, token-validated name (HTTP-17).
    private static (string Original, string Folded) WriteKey(string name)
    {
        var trimmed = HeaderSyntax.ValidateName(name, nameof(name));
        return (trimmed, AsciiFold.ToLower(trimmed));
    }

    private int FindIndex(string? folded) => folded is not null && _index.TryGetValue(folded, out var i) ? i : -1;

    private string? FirstValue(int index) => index >= 0 ? _entries[index].Items[0] : null;

    private IReadOnlyList<string> ValuesAt(int index) => index >= 0 ? _entries[index].View : Entry.NoValues;

    private Headers Append(string original, string folded, string value)
    {
        var index = FindIndex(folded);
        return index >= 0
            ? new Headers(_entries.SetItem(index, _entries[index].WithItems(_entries[index].Items.Add(value))))
            : new Headers(_entries.Add(Entry.Create(original, folded, [value])));
    }

    private Headers Replace(string original, string folded, string? value)
    {
        var index = FindIndex(folded);
        if (value is null)
        {
            return RemoveAt(index);
        }

        return index >= 0
            ? new Headers(_entries.SetItem(index, _entries[index].WithItems([value])))
            : new Headers(_entries.Add(Entry.Create(original, folded, [value])));
    }

    private Headers RemoveAt(int index) => index >= 0 ? new Headers(_entries.RemoveAt(index)) : this;

    // One header: the first insertion's casing, the folded key, and the values (kept boxed once so reads never allocate).
    internal readonly record struct Entry(string Original, string Folded, ImmutableArray<string> Items, IReadOnlyList<string> View)
    {
        internal static IReadOnlyList<string> NoValues { get; } = ImmutableArray<string>.Empty;

        internal static Entry Create(string original, string folded, ImmutableArray<string> items) =>
            new(original, folded, items, items);

        internal Entry WithItems(ImmutableArray<string> items) => Create(Original, Folded, items);
    }

    /// <summary>
    /// A mutable accumulator for building a <see cref="Headers"/> without allocating an intermediate instance per
    /// edit. Not thread-safe. <see cref="Build"/> copies, so later edits never reach a built <see cref="Headers"/>
    /// (HTTP-3).
    /// </summary>
    public sealed class Builder
    {
        private readonly List<(string Original, string Folded, List<string> Values)> _entries = [];
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        internal Builder(ImmutableArray<Entry> seed)
        {
            foreach (var entry in seed)
            {
                _index[entry.Folded] = _entries.Count;
                _entries.Add((entry.Original, entry.Folded, [.. entry.Items]));
            }
        }

        /// <summary>Creates an empty builder.</summary>
        public Builder()
        {
        }

        /// <summary>Appends <paramref name="value"/> under <paramref name="name"/>.</summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The value to append.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB
        /// and printable ASCII.
        /// </exception>
        public Builder Add(string name, string value)
        {
            var (original, folded) = WriteKey(name);
            HeaderSyntax.ValidateOutboundValue(value, original, nameof(value));
            return Append(original, folded, value);
        }

        /// <summary>Appends <paramref name="value"/> under the typed <paramref name="name"/>.</summary>
        /// <param name="name">The typed header name.</param>
        /// <param name="value">The value to append.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> holds a character outside HTAB and printable ASCII.</exception>
        public Builder Add(HttpHeaderName name, string value)
        {
            ArgumentNullException.ThrowIfNull(name);
            HeaderSyntax.ValidateOutboundValue(value, name.Original, nameof(value));
            return Append(name.Original, name.CanonicalName, value);
        }

        /// <summary>
        /// Appends a <em>received</em> header — one a transport read off a response — under the lenient inbound rule
        /// (HTTP-19): the name is validated exactly as <see cref="Add(string,string)"/> validates it and keeps the
        /// sender's casing, and the value may carry obs-text (0x80 and above) but still no control character other than
        /// HTAB, and no DEL. Use <see cref="Add(string,string)"/> for anything a caller sets.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The value to append.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a control character.
        /// </exception>
        public Builder AddInbound(string name, string value)
        {
            var (original, folded) = WriteKey(name);
            HeaderSyntax.ValidateInboundValue(value, original, nameof(value));
            return Append(original, folded, value);
        }

        /// <summary>
        /// Sets <paramref name="name"/> to exactly <paramref name="value"/>, keeping the name's position and first
        /// casing; <see langword="null"/> removes the header (HTTP-15).
        /// </summary>
        /// <remarks><b>Breaking:</b> <paramref name="value"/> was non-nullable.</remarks>
        /// <param name="name">The header name.</param>
        /// <param name="value">The single value, or <see langword="null"/> to remove the header.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is blank or not a token, or <paramref name="value"/> holds a character outside HTAB
        /// and printable ASCII.
        /// </exception>
        public Builder Set(string name, string? value)
        {
            var (original, folded) = WriteKey(name);
            if (value is not null)
            {
                HeaderSyntax.ValidateOutboundValue(value, original, nameof(value));
            }

            return Replace(original, folded, value);
        }

        /// <summary>Sets the typed <paramref name="name"/> to exactly <paramref name="value"/>; <see langword="null"/> removes it.</summary>
        /// <param name="name">The typed header name.</param>
        /// <param name="value">The single value, or <see langword="null"/> to remove the header.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> holds a character outside HTAB and printable ASCII.</exception>
        public Builder Set(HttpHeaderName name, string? value)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (value is not null)
            {
                HeaderSyntax.ValidateOutboundValue(value, name.Original, nameof(value));
            }

            return Replace(name.Original, name.CanonicalName, value);
        }

        /// <summary>Removes every value for <paramref name="name"/>.</summary>
        /// <param name="name">The header name.</param>
        /// <returns>This builder, for chaining.</returns>
        public Builder Remove(string name) => RemoveFolded(LookupKey(name));

        /// <summary>Removes every value for the typed <paramref name="name"/>.</summary>
        /// <param name="name">The typed header name.</param>
        /// <returns>This builder, for chaining.</returns>
        public Builder Remove(HttpHeaderName name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return RemoveFolded(name.CanonicalName);
        }

        /// <summary>Builds the immutable <see cref="Headers"/>; later builder edits never reach it.</summary>
        /// <returns>A new <see cref="Headers"/>.</returns>
        public Headers Build()
        {
            if (_entries.Count == 0)
            {
                return Empty;
            }

            var entries = ImmutableArray.CreateBuilder<Entry>(_entries.Count);
            foreach (var (original, folded, values) in _entries)
            {
                entries.Add(Entry.Create(original, folded, [.. values]));
            }

            return new Headers(entries.MoveToImmutable());
        }

        private Builder Append(string original, string folded, string value)
        {
            if (_index.TryGetValue(folded, out var position))
            {
                _entries[position].Values.Add(value);
            }
            else
            {
                _index[folded] = _entries.Count;
                _entries.Add((original, folded, [value]));
            }

            return this;
        }

        private Builder Replace(string original, string folded, string? value)
        {
            if (value is null)
            {
                return RemoveFolded(folded);
            }

            if (_index.TryGetValue(folded, out var position))
            {
                _entries[position] = (_entries[position].Original, folded, [value]);
            }
            else
            {
                _index[folded] = _entries.Count;
                _entries.Add((original, folded, [value]));
            }

            return this;
        }

        private Builder RemoveFolded(string? folded)
        {
            if (folded is not null && _index.Remove(folded, out var position))
            {
                _entries.RemoveAt(position);
                for (var i = position; i < _entries.Count; i++)
                {
                    _index[_entries[i].Folded] = i;
                }
            }

            return this;
        }
    }
}

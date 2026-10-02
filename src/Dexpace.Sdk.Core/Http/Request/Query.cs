// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// An immutable URL query: an ordered multimap of ordinal (case-sensitive) names to values, rendered as RFC 3986
/// (HTTP-28, HTTP-29, HTTP-30, HTTP-31).
/// </summary>
/// <remarks>
/// Names keep the order of their first appearance, and each name holds its values in insertion order. A value-less
/// parameter (<c>?flag</c>) is a single empty-string value, so <c>flag</c> and <c>flag=</c> are the same query.
/// <see cref="Encode"/> percent-encodes every component (a space is <c>%20</c>, never <c>+</c>); <see cref="Parse"/>
/// is its total, lenient inverse. Equality is order-sensitive over the grouped entries and holds exactly when
/// <see cref="Encode"/> is equal. A <see cref="Query"/> is not a member of <see cref="Request"/> (HTTP-6): splicing a
/// query into a URL is the transport projection's job (SEAM-27). Instances are immutable (HTTP-1); derive a modified
/// copy through <see cref="ToBuilder"/> (HTTP-3), and the sequences it returns are read-only (HTTP-5).
/// </remarks>
public sealed class Query : IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>, IEquatable<Query>
{
    private readonly ImmutableArray<Entry> _entries;
    private readonly Dictionary<string, int> _index;

    private Query(ImmutableArray<Entry> entries)
    {
        _entries = entries;
        _index = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
        var names = ImmutableArray.CreateBuilder<string>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            _index[entries[i].Name] = i;
            names.Add(entries[i].Name);
        }

        Names = names.MoveToImmutable();
    }

    /// <summary>A query with no parameters.</summary>
    public static Query Empty { get; } = new([]);

    /// <summary>The number of distinct names.</summary>
    public int Count => _entries.Length;

    /// <summary>The distinct names, in order of first appearance.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>True when <paramref name="name"/> is present (compared ordinally).</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns><see langword="true"/> if at least one value is present.</returns>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _index.ContainsKey(name);
    }

    /// <summary>The first value for <paramref name="name"/>: <c>""</c> for a flag, <see langword="null"/> when absent.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The first value, or <see langword="null"/>.</returns>
    public string? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _index.TryGetValue(name, out var i) ? _entries[i].Values[0] : null;
    }

    /// <summary>All values for <paramref name="name"/> in insertion order, or an empty list when absent.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>A read-only list.</returns>
    public IReadOnlyList<string> GetAll(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _index.TryGetValue(name, out var i) ? _entries[i].Values : NoValues;
    }

    /// <summary>
    /// Renders the query as RFC 3986: <c>name=value</c> pairs joined by <c>&amp;</c>, every component
    /// percent-encoded, a repeated name once per value, no leading <c>?</c>, and <c>""</c> when empty (HTTP-29).
    /// </summary>
    /// <returns>The encoded query text.</returns>
    public string Encode()
    {
        if (_entries.IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var entry in _entries)
        {
            var name = Rfc3986.EncodeComponent(entry.Name);
            foreach (var value in entry.Values)
            {
                if (sb.Length > 0)
                {
                    sb.Append('&');
                }

                sb.Append(name).Append('=').Append(Rfc3986.EncodeComponent(value));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses query text, total and lenient (HTTP-31): <see langword="null"/> or blank gives <see cref="Empty"/>; one
    /// leading <c>?</c> is stripped; <c>a</c> and <c>a=</c> both give <c>""</c>; stray <c>&amp;</c> and empty-name
    /// segments are skipped; a malformed escape stays raw; <c>+</c> stays <c>+</c>; an escaped sequence that is not
    /// valid UTF-8 (including an encoded lone surrogate) stays raw; and a literal lone surrogate becomes U+FFFD, so
    /// the result always satisfies the builder's rules. It never throws.
    /// </summary>
    /// <param name="query">The query text, with or without a leading <c>?</c>.</param>
    /// <returns>The parsed query.</returns>
    public static Query Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Empty;
        }

        var span = query.AsSpan();
        if (span[0] == '?')
        {
            span = span[1..];
        }

        var builder = new Builder();
        foreach (var range in span.Split('&'))
        {
            var segment = span[range];
            if (segment.IsEmpty)
            {
                continue;
            }

            var eq = segment.IndexOf('=');
            var rawName = eq < 0 ? segment : segment[..eq];
            if (rawName.IsEmpty)
            {
                continue;
            }

            var rawValue = eq < 0 ? default : segment[(eq + 1)..];
            builder.AddTrusted(
                ReplaceLoneSurrogates(Rfc3986.DecodeComponent(rawName.ToString())),
                ReplaceLoneSurrogates(Rfc3986.DecodeComponent(rawValue.ToString())));
        }

        return builder.Build();
    }

    /// <summary>Returns a mutable builder seeded with this query's contents.</summary>
    /// <returns>A new <see cref="Builder"/>; edits never reach this instance.</returns>
    public Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (var entry in _entries)
        {
            foreach (var value in entry.Values)
            {
                builder.AddTrusted(entry.Name, value);
            }
        }

        return builder;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator()
    {
        foreach (var entry in _entries)
        {
            yield return new KeyValuePair<string, IReadOnlyList<string>>(entry.Name, entry.Values);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Order-sensitive equality over the grouped entries. It holds exactly when <see cref="Encode"/> is equal,
    /// because percent-encoding is injective over the well-formed strings the builder admits (HTTP-30).
    /// </summary>
    /// <param name="other">The query to compare with.</param>
    /// <returns><see langword="true"/> when the same names hold the same values in the same order.</returns>
    public bool Equals(Query? other)
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
            if (!string.Equals(_entries[i].Name, other._entries[i].Name, StringComparison.Ordinal)
                || !_entries[i].Values.SequenceEqual(other._entries[i].Values, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Query);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in _entries)
        {
            hash.Add(entry.Name, StringComparer.Ordinal);
            foreach (var value in entry.Values)
            {
                hash.Add(value, StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Lists the parameter names only (<c>Query[page, token]</c>). Values are deliberately left out because they may
    /// be secrets; <see cref="Encode"/> is the explicit form.
    /// </summary>
    /// <returns>The names, never the values.</returns>
    public override string ToString() => $"Query[{string.Join(", ", Names)}]";

    /// <summary>Value equality.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both are equal or both are <see langword="null"/>.</returns>
    public static bool operator ==(Query? left, Query? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(Query? left, Query? right) => !(left == right);

    private static IReadOnlyList<string> NoValues { get; } = ImmutableArray<string>.Empty;

    // Index of the first unpaired surrogate, or -1.
    private static int IndexOfLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string ReplaceLoneSurrogates(string text)
    {
        if (IndexOfLoneSurrogate(text) < 0)
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(chars[i]))
            {
                chars[i] = '�';
            }
        }

        return new string(chars);
    }

    private readonly record struct Entry(string Name, IReadOnlyList<string> Values);

    /// <summary>
    /// A mutable accumulator for building a <see cref="Query"/>. Not thread-safe. A name that already exists keeps
    /// its position when it gains or replaces values.
    /// </summary>
    public sealed class Builder
    {
        private readonly List<(string Name, List<string> Values)> _entries = [];
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        /// <summary>Creates an empty builder.</summary>
        public Builder()
        {
        }

        /// <summary>Appends a value under <paramref name="name"/>; <see langword="null"/> is stored as <c>""</c>.</summary>
        /// <param name="name">The parameter name; must not be empty.</param>
        /// <param name="value">The value, or <see langword="null"/> for a flag.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is empty, or the name or value holds an unpaired surrogate. The message names the
        /// code unit and its index and never echoes the text.
        /// </exception>
        public Builder Add(string name, string? value)
        {
            ValidateName(name);
            AddTrusted(name, ValidateValue(value));
            return this;
        }

        /// <summary>
        /// Replaces every value of <paramref name="name"/> with the single <paramref name="value"/>, keeping the name's
        /// position. <see langword="null"/> stores <c>""</c> (a flag) exactly as <see cref="Add"/> does: unlike
        /// <c>Headers.Set</c>, <see langword="null"/> never removes; use <see cref="Remove"/>.
        /// </summary>
        /// <param name="name">The parameter name; must not be empty.</param>
        /// <param name="value">The single value, or <see langword="null"/> for a flag.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">See <see cref="Add"/>.</exception>
        public Builder Set(string name, string? value)
        {
            ValidateName(name);
            Replace(name, [ValidateValue(value)]);
            return this;
        }

        /// <summary>
        /// Replaces every value of <paramref name="name"/> with <paramref name="values"/> (each <see langword="null"/>
        /// stored as <c>""</c>). An empty sequence removes the name, so <see cref="Build"/> never carries a name with
        /// no value (HTTP-30).
        /// </summary>
        /// <param name="name">The parameter name; must not be empty.</param>
        /// <param name="values">The replacement values.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">See <see cref="Add"/>.</exception>
        public Builder Set(string name, IEnumerable<string?> values)
        {
            ValidateName(name);
            ArgumentNullException.ThrowIfNull(values);
            var list = values.Select(ValidateValue).ToList();
            if (list.Count == 0)
            {
                return Remove(name);
            }

            Replace(name, list);
            return this;
        }

        /// <summary>Removes every value of <paramref name="name"/>.</summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>This builder, for chaining.</returns>
        public Builder Remove(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (_index.Remove(name, out var position))
            {
                _entries.RemoveAt(position);
                for (var i = position; i < _entries.Count; i++)
                {
                    _index[_entries[i].Name] = i;
                }
            }

            return this;
        }

        /// <summary>Builds an immutable <see cref="Query"/>; later builder edits never reach it.</summary>
        /// <returns>A new <see cref="Query"/>.</returns>
        public Query Build()
        {
            var entries = ImmutableArray.CreateBuilder<Entry>(_entries.Count);
            foreach (var (name, values) in _entries)
            {
                if (values.Count > 0)
                {
                    entries.Add(new Entry(name, values.ToImmutableArray()));
                }
            }

            return entries.Count == 0 ? Empty : new Query(entries.ToImmutable());
        }

        // Adds without validating: the caller has already proven the name and value well-formed.
        internal void AddTrusted(string name, string value)
        {
            if (_index.TryGetValue(name, out var position))
            {
                _entries[position].Values.Add(value);
                return;
            }

            _index[name] = _entries.Count;
            _entries.Add((name, [value]));
        }

        private static void ValidateName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (name.Length == 0)
            {
                throw new ArgumentException("A query parameter name must not be empty.", nameof(name));
            }

            ThrowOnLoneSurrogate(name, "name", nameof(name));
        }

        private static string ValidateValue(string? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            ThrowOnLoneSurrogate(value, "value", nameof(value));
            return value;
        }

        private static void ThrowOnLoneSurrogate(string text, string what, string paramName)
        {
            var index = IndexOfLoneSurrogate(text);
            if (index >= 0)
            {
                throw new ArgumentException(
                    $"A query {what} contains the unpaired surrogate U+{(int)text[index]:X4} at index "
                    + $"{index.ToString(CultureInfo.InvariantCulture)}; it cannot be encoded as UTF-8.",
                    paramName);
            }
        }

        private void Replace(string name, List<string> values)
        {
            if (_index.TryGetValue(name, out var position))
            {
                _entries[position] = (name, values);
                return;
            }

            _index[name] = _entries.Count;
            _entries.Add((name, values));
        }
    }
}

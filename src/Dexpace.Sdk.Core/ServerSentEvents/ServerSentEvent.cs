// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// One parsed server-sent event: the five fields a block of lines can set (SSE-1, SSE-4, SSE-20, SSE-21, SSE-22).
/// </summary>
/// <remarks>
/// <para>
/// <b>Absent versus present-but-empty (SSE-4).</b> <see cref="Id"/>, <see cref="Event"/>, <see cref="Comment"/> and
/// <see cref="Retry"/> are <see langword="null"/> when the block did not set them and the empty string (or, for the
/// retry, never) when it set them empty; a block that said <c>event:</c> yields <c>Event == ""</c>, not
/// <see langword="null"/>. <see cref="Data"/> is never <see langword="null"/> (P7b-7): it is an empty list when no
/// <c>data</c> line arrived and <c>[""]</c> when one arrived empty, and the two are different events. The event name is
/// never defaulted to <c>message</c> (SSE-10).
/// </para>
/// <para>
/// <b>Immutable (SSE-20).</b> The <see cref="Data"/> setter copies the list it is given into a private read-only
/// collection, so neither the caller's list nor a later mutation of it reaches the event, and
/// <c>ev with { Data = list }</c> copies again. The setters validate so that no event the parser could never produce
/// can be built: an <see cref="Id"/> holding U+0000 (SSE-9) and a <see cref="Retry"/> that is negative, not a whole
/// number of milliseconds, or above <see cref="int.MaxValue"/> milliseconds (SSE-11) throw; their messages never echo
/// the value. Construct with an object initializer or <c>with</c>; there is no positional constructor.
/// </para>
/// <para>
/// <b>Value semantics (SSE-21).</b> <see cref="Equals(ServerSentEvent?)"/> and <see cref="GetHashCode"/> are
/// hand-written because a record compares a collection member by reference: <see cref="Data"/> is compared element by
/// element and every string ordinally. <see cref="ToString"/> is lossless: strings are quoted with <c>"</c>, <c>\</c> and
/// control characters escaped, <see langword="null"/> prints as <c>null</c>, the data as <c>["a", "b"]</c> and the retry
/// as whole milliseconds (<c>5000ms</c>), so two unequal events never print the same. The SDK never logs an event, so the
/// string form opens no redaction path.
/// </para>
/// </remarks>
public sealed record ServerSentEvent
{
    /// <summary>The most milliseconds a <c>retry</c> value may hold (P7b-6): every .NET timer accepts it.</summary>
    internal const int MaxRetryMilliseconds = int.MaxValue;

    private static readonly ReadOnlyCollection<string> s_noData = new(Array.Empty<string>());

    private readonly string? _id;
    private readonly TimeSpan? _retry;
    private readonly ReadOnlyCollection<string> _data = s_noData;

    /// <summary>The <c>id</c> field, or <see langword="null"/> when the block set none (SSE-4, SSE-9).</summary>
    /// <exception cref="ArgumentException">The value holds U+0000, which the parser ignores (SSE-9).</exception>
    public string? Id
    {
        get => _id;
        init
        {
            RejectNul(value);
            _id = value;
        }
    }

    /// <summary>The <c>event</c> field, raw, or <see langword="null"/> when the block set none; never defaulted (SSE-10).</summary>
    public string? Event { get; init; }

    /// <summary>The <c>data</c> lines in wire order, unjoined (SSE-8); empty when the block set none (P7b-7).</summary>
    /// <exception cref="ArgumentNullException">The list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An element is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Data
    {
        get => _data;
        init => _data = CopyOf(value);
    }

    /// <summary>The comment text (a line starting with a colon), latest wins, or <see langword="null"/> (SSE-6).</summary>
    public string? Comment { get; init; }

    /// <summary>The reconnection hint, whole milliseconds, or <see langword="null"/> when the block set none (SSE-11).</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative, is not a whole number of milliseconds, or exceeds <see cref="int.MaxValue"/> milliseconds.
    /// </exception>
    public TimeSpan? Retry
    {
        get => _retry;
        init
        {
            RejectBadRetry(value);
            _retry = value;
        }
    }

    /// <summary>
    /// <see langword="true"/> only when <see cref="Id"/>, <see cref="Event"/>, <see cref="Comment"/> and
    /// <see cref="Retry"/> are all <see langword="null"/> and <see cref="Data"/> is empty (SSE-22); a comment-only event is
    /// not empty, and neither is one whose only field is present-but-empty.
    /// </summary>
    public bool IsEmpty => _id is null && Event is null && Comment is null && _retry is null && _data.Count == 0;

    /// <summary>Compares the five fields, <see cref="Data"/> element by element, every string ordinally (SSE-21).</summary>
    /// <param name="other">The event to compare with.</param>
    /// <returns><see langword="true"/> when every field is equal.</returns>
    public bool Equals(ServerSentEvent? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (string.Equals(_id, other._id, StringComparison.Ordinal)
                && string.Equals(Event, other.Event, StringComparison.Ordinal)
                && string.Equals(Comment, other.Comment, StringComparison.Ordinal)
                && _retry == other._retry
                && _data.SequenceEqual(other._data, StringComparer.Ordinal)));

    /// <summary>Hashes the five fields consistently with <see cref="Equals(ServerSentEvent?)"/> (SSE-21).</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(_id, StringComparer.Ordinal);
        hash.Add(Event, StringComparer.Ordinal);
        hash.Add(Comment, StringComparer.Ordinal);
        hash.Add(_retry);
        hash.Add(_data.Count);
        foreach (var line in _data)
        {
            hash.Add(line, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static void RejectNul(string? value)
    {
        if (value is not null && value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("An event id must not contain U+0000; the parser ignores such an id (SSE-9).", nameof(value));
        }
    }

    private static void RejectBadRetry(TimeSpan? value)
    {
        if (value is not { } retry)
        {
            return;
        }

        if (retry < TimeSpan.Zero
            || retry.Ticks % TimeSpan.TicksPerMillisecond != 0
            || retry.Ticks / TimeSpan.TicksPerMillisecond > MaxRetryMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "An event retry must be a whole number of milliseconds from zero to Int32.MaxValue (SSE-11).");
        }
    }

    private static ReadOnlyCollection<string> CopyOf(IReadOnlyList<string> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Count == 0)
        {
            return s_noData;
        }

        var copy = new string[value.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = value[i] ?? throw new ArgumentException("A data line must not be null.", nameof(value));
        }

        return new ReadOnlyCollection<string>(copy);
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Id = ");
        AppendQuoted(builder, _id);
        builder.Append(", Event = ");
        AppendQuoted(builder, Event);
        builder.Append(", Data = [");
        for (var i = 0; i < _data.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            AppendQuoted(builder, _data[i]);
        }

        builder.Append("], Comment = ");
        AppendQuoted(builder, Comment);
        builder.Append(", Retry = ");
        if (_retry is { } retry)
        {
            builder.Append((retry.Ticks / TimeSpan.TicksPerMillisecond).ToString(CultureInfo.InvariantCulture)).Append("ms");
        }
        else
        {
            builder.Append("null");
        }

        return true;
    }

    private static void AppendQuoted(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}

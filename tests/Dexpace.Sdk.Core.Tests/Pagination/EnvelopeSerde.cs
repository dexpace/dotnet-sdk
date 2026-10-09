// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Globalization;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>The body of a page was not <c>"a,b,c|next"</c>.</summary>
internal sealed class MalformedEnvelopeException : FormatException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">The message.</param>
    internal MalformedEnvelopeException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A small deterministic codec for <see cref="Envelope"/>: <c>"1,2,3|next"</c>, an empty body for an empty envelope,
/// and the text <c>null</c> for a null envelope. It implements both the synchronous and the asynchronous members (the
/// shared <c>ScriptedSerde</c> returns <see langword="null"/> from <c>Deserialize</c>, which the blocking engine
/// calls), drains the stream as a real codec does, and holds no state, so two concurrent walks are safe (PAGE-5).
/// </summary>
internal sealed class EnvelopeSerde : ISerde
{
    /// <inheritdoc/>
    public MediaType DefaultMediaType => MediaType.Of("application", "json");

    /// <inheritdoc/>
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return Parse<T>(buffer.ToArray());
    }

    /// <inheritdoc/>
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => Parse<T>(utf8);

    private static T? Parse<T>(ReadOnlySpan<byte> bytes)
    {
        if (typeof(T) != typeof(Envelope))
        {
            throw new InvalidOperationException($"EnvelopeSerde was asked for {typeof(T).Name}.");
        }

        var text = Encoding.UTF8.GetString(bytes);
        if (text.Length == 0)
        {
            return (T)(object)new Envelope([], null);
        }

        if (text == "null")
        {
            return default;
        }

        var bar = text.IndexOf('|', StringComparison.Ordinal);
        if (bar < 0)
        {
            throw new MalformedEnvelopeException("The envelope has no '|' separator.");
        }

        var items = new List<int>();
        foreach (var part in text[..bar].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            items.Add(int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var item)
                ? item
                : throw new MalformedEnvelopeException("An item is not a number."));
        }

        var next = text[(bar + 1)..];
        return (T)(object)new Envelope(items, next.Length == 0 ? null : next);
    }
}

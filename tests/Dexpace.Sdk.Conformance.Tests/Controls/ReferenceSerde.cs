// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text.Json;
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>What a <see cref="ReferenceSerde"/> gets wrong; each flaw is the negative control of one codec assertion (plan 2.2 to 2.4).</summary>
[Flags]
public enum SerdeFlaw
{
    /// <summary>A conforming codec.</summary>
    None = 0,

    /// <summary>It drops the list when it encodes a contact (SERDE-1).</summary>
    LosesField = 1,

    /// <summary>It declares a format-agnostic media type (SERDE-2).</summary>
    OctetStream = 1 << 1,

    /// <summary>Its declared media type changes from one read to the next (SERDE-2).</summary>
    UnstableMediaType = 1 << 2,

    /// <summary>It closes the caller's target on encode (SERDE-3).</summary>
    ClosesTarget = 1 << 3,

    /// <summary>It closes the caller's source on decode (SERDE-3).</summary>
    ClosesSource = 1 << 4,

    /// <summary>It lets the backing library's exception type escape (SERDE-9).</summary>
    LeaksLibraryException = 1 << 5,

    /// <summary>It raises the SDK's exception without the original failure as the cause (SERDE-9).</summary>
    DropsCause = 1 << 6,

    /// <summary>It raises a read failure as the write-path subtype (SERDE-10).</summary>
    WrongDirection = 1 << 7,

    /// <summary>It re-wraps a genuine stream I/O error as a codec failure (SERDE-12).</summary>
    WrapsIoErrors = 1 << 8,

    /// <summary>It writes a wire null for an absent member (SERDE-15).</summary>
    NullForAbsent = 1 << 9,

    /// <summary>It decodes an explicit null member as absent (SERDE-16).</summary>
    NullDecodesAsAbsent = 1 << 10,

    /// <summary>It decodes a missing key as null (SERDE-17).</summary>
    MissingKeyIsNull = 1 << 11,

    /// <summary>It has no tri-state wiring: absent and null are one thing on the wire (SERDE-19).</summary>
    NoTristateWiring = 1 << 12,

    /// <summary>It throws for a top-level absent value (SERDE-20).</summary>
    ThrowsForTopLevelAbsent = 1 << 13,
}

/// <summary>
/// A small JSON codec for exactly the codec suite's probe models, over <see cref="Utf8JsonWriter"/> and <see cref="JsonDocument"/>, with switchable
/// <see cref="SerdeFlaw"/>s: the in-kit reference the codec controls run against (plan 2.2). It uses no serializer metadata, so the kit's own tests
/// reference no codec adapter.
/// </summary>
public sealed class ReferenceSerde(SerdeFlaw flaw = SerdeFlaw.None) : ISerde
{
    private int _reads;

    /// <inheritdoc />
    public MediaType DefaultMediaType =>
        flaw.HasFlag(SerdeFlaw.OctetStream) ? MediaType.Parse("application/octet-stream")
        : flaw.HasFlag(SerdeFlaw.UnstableMediaType) && Interlocked.Increment(ref _reads) % 2 == 0 ? MediaType.Parse("application/json")
        : CommonMediaTypes.ApplicationJsonUtf8;

    /// <inheritdoc />
    public async ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default)
    {
        var bytes = Encode(value);
        await destination.WriteAsync(bytes, cancellationToken);
        if (flaw.HasFlag(SerdeFlaw.ClosesTarget))
        {
            await destination.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        try
        {
            await source.CopyToAsync(buffer, cancellationToken);
        }
        catch (IOException io) when (flaw.HasFlag(SerdeFlaw.WrapsIoErrors))
        {
            throw new DeserializationException("the stream failed", io);
        }

        if (flaw.HasFlag(SerdeFlaw.ClosesSource))
        {
            await source.DisposeAsync();
        }

        return Decode<T>(buffer.ToArray());
    }

    /// <inheritdoc />
    public T? Deserialize<T>(Stream source)
    {
        using var buffer = new MemoryStream();
        try
        {
            source.CopyTo(buffer);
        }
        catch (IOException io) when (flaw.HasFlag(SerdeFlaw.WrapsIoErrors))
        {
            throw new DeserializationException("the stream failed", io);
        }

        if (flaw.HasFlag(SerdeFlaw.ClosesSource))
        {
            source.Dispose();
        }

        return Decode<T>(buffer.ToArray());
    }

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
        var bytes = Encode(value);
        try
        {
            destination.GetSpan(bytes.Length);
        }
        catch (IOException io) when (flaw.HasFlag(SerdeFlaw.WrapsIoErrors))
        {
            throw new SerializationException("the target failed", io);
        }

        destination.Write(bytes);
        if (flaw.HasFlag(SerdeFlaw.ClosesTarget))
        {
            (destination as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => Decode<T>(utf8.ToArray());

    private byte[] Encode<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            try
            {
                Write(writer, value);
            }
            catch (NotSupportedException ex) when (!flaw.HasFlag(SerdeFlaw.LeaksLibraryException))
            {
                throw flaw.HasFlag(SerdeFlaw.DropsCause) ? new SerializationException("cannot encode") : new SerializationException("cannot encode " + typeof(T).Name, ex);
            }
        }

        return buffer.WrittenSpan.ToArray();
    }

    private void Write<T>(Utf8JsonWriter writer, T value)
    {
        switch (value)
        {
            case ProbeContact contact:
                writer.WriteStartObject();
                writer.WriteString("name", contact.Name);
                writer.WriteNumber("age", contact.Age);
                writer.WritePropertyName("address");
                WriteAddress(writer, contact.Address);
                if (!flaw.HasFlag(SerdeFlaw.LosesField))
                {
                    writer.WriteStartArray("tags");
                    foreach (var tag in contact.Tags)
                    {
                        writer.WriteStringValue(tag);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
                break;
            case ProbeAddress address:
                WriteAddress(writer, address);
                break;
            case ProbePatch patch:
                writer.WriteStartObject();
                Member(writer, "name", patch.Name, static (w, v) => w.WriteStringValue(v));
                Member(writer, "age", patch.Age, static (w, v) => w.WriteNumberValue(v));
                Member(writer, "address", patch.Address, WriteAddress);
                writer.WriteEndObject();
                break;
            case ProbeSequence sequence:
                writer.WriteStartObject();
                writer.WriteStartArray("items");
                foreach (var item in sequence.Items)
                {
                    Element(writer, item);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                break;
            case Tristate<string> top:
                if (top.IsAbsent && flaw.HasFlag(SerdeFlaw.ThrowsForTopLevelAbsent))
                {
                    throw new SerializationException("a top-level absent value cannot be encoded");
                }

                Element(writer, top);
                break;
            default:
                throw new NotSupportedException($"The type {typeof(T).Name} is not supported (delegates and unknown types have no representation).");
        }
    }

    private static void WriteAddress(Utf8JsonWriter writer, ProbeAddress address)
    {
        writer.WriteStartObject();
        writer.WriteString("street", address.Street);
        writer.WriteString("city", address.City);
        writer.WriteEndObject();
    }

    private void Member<TValue>(Utf8JsonWriter writer, string name, Tristate<TValue> member, Action<Utf8JsonWriter, TValue> write)
        where TValue : notnull
    {
        if (member.IsPresent)
        {
            writer.WritePropertyName(name);
            write(writer, member.Value);
        }
        else if (member.IsNull || flaw.HasFlag(SerdeFlaw.NullForAbsent) || flaw.HasFlag(SerdeFlaw.NoTristateWiring))
        {
            writer.WriteNull(name);
        }
    }

    private static void Element(Utf8JsonWriter writer, Tristate<string> item)
    {
        if (item.IsPresent)
        {
            writer.WriteStringValue(item.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private T? Decode<T>(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return (T?)Read(document.RootElement, typeof(T));
        }
        catch (JsonException ex) when (!flaw.HasFlag(SerdeFlaw.LeaksLibraryException))
        {
            if (flaw.HasFlag(SerdeFlaw.WrongDirection))
            {
                throw new SerializationException("cannot decode", ex);
            }

            throw flaw.HasFlag(SerdeFlaw.DropsCause) ? new DeserializationException("cannot decode") : new DeserializationException("cannot decode " + typeof(T).Name, ex);
        }
    }

    private object? Read(JsonElement element, Type type)
    {
        if (type == typeof(ProbeContact))
        {
            return new ProbeContact(
                element.GetProperty("name").GetString()!,
                element.GetProperty("age").GetInt32(),
                ReadAddress(element.GetProperty("address")),
                element.TryGetProperty("tags", out var tags) ? [.. tags.EnumerateArray().Select(static t => t.GetString()!)] : []);
        }

        if (type == typeof(ProbePatch))
        {
            return new ProbePatch(
                Member(element, "name", static e => e.GetString()!),
                Member(element, "age", static e => e.GetInt32()),
                Member(element, "address", ReadAddress));
        }

        if (type == typeof(ProbeSequence))
        {
            return new ProbeSequence([.. element.GetProperty("items").EnumerateArray().Select(static e => e.ValueKind == JsonValueKind.Null ? Tristate<string>.Null : Tristate<string>.Present(e.GetString()!))]);
        }

        if (type == typeof(Tristate<string>))
        {
            return element.ValueKind == JsonValueKind.Null ? Tristate<string>.Null : Tristate<string>.Present(element.GetString()!);
        }

        throw new JsonException("unsupported type " + type.Name);
    }

    private static ProbeAddress ReadAddress(JsonElement element) =>
        new(element.GetProperty("street").GetString()!, element.GetProperty("city").GetString()!);

    private Tristate<TValue> Member<TValue>(JsonElement owner, string name, Func<JsonElement, TValue> read)
        where TValue : notnull
    {
        if (!owner.TryGetProperty(name, out var member))
        {
            return flaw.HasFlag(SerdeFlaw.MissingKeyIsNull) ? Tristate<TValue>.Null : Tristate<TValue>.Absent;
        }

        if (member.ValueKind == JsonValueKind.Null)
        {
            return flaw.HasFlag(SerdeFlaw.NullDecodesAsAbsent) || flaw.HasFlag(SerdeFlaw.NoTristateWiring) ? Tristate<TValue>.Absent : Tristate<TValue>.Null;
        }

        return Tristate<TValue>.Present(read(member));
    }
}

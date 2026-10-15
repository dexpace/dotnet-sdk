// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/codec_suite/ (the round trip, the declared media type and the
// caller-owned target). SERDE-4 (the fixed-buffer encode) is core's SerdeExtensions.Serialize(ISerde, Span<byte>, T), not a codec property: it
// stays by reference to SerdeProfileTests (validation F6); the suite uses that extension only to hand the codec a region to write into.
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>SERDE-1</c> to <c>SERDE-3</c> and <c>SEAM-20</c>: one codec that encodes and decodes what it was given, declares a wire format of its own,
/// and reads and writes a caller-owned stream or buffer without closing it.
/// </summary>
internal static class CodecRoundTripAssertions
{
    private static readonly ProbeContact s_contact = new("Ada", 36, new ProbeAddress("1 Main Street", "Springfield"), ["alpha", "beta"]);

    internal static ConformanceAssertion[] Assertions() =>
    [
        CodecAssertionBuilder.Define("serde-1.round-trip", ["SERDE-1"], RequirementLevel.Must, CodecAssertionBuilder.Both, RoundTripAsync),
        CodecAssertionBuilder.Define("serde-2.declared-media-type", ["SERDE-2"], RequirementLevel.Must, CodecAssertionBuilder.NoFace, DeclaredMediaTypeAsync),
        CodecAssertionBuilder.Define("serde-3.target-not-closed", ["SERDE-3", "SEAM-20"], RequirementLevel.Must, CodecAssertionBuilder.Both, TargetNotClosedAsync),
    ];

    // One bundle: the same codec instance encodes a value and decodes it back, field for field.
    private static async Task RoundTripAsync(CodecContext context, CancellationToken token)
    {
        var back = await context.RoundTripAsync(s_contact, "a contact (a string, a number, a nested record and a list)", token).ConfigureAwait(false);

        Check.True(back is not null && Same(s_contact, back), "a value must survive a round trip through one codec unchanged", Describe(s_contact), back is null ? "null" : Describe(back));
    }

    // The codec declares a format-specific media type, and a request body made from a value plus the codec is typed with it.
    private static Task DeclaredMediaTypeAsync(CodecContext context, CancellationToken token)
    {
        var serde = context.Serde;
        var declared = serde.DefaultMediaType;
        Check.True(declared is not null, "a codec must declare the wire media type it produces", "a media type", "null");
        Check.True(
            declared!.FullType is not ("*/*" or "application/octet-stream"),
            "a codec's media type must not default to a format-agnostic constant",
            "a format-specific media type",
            declared.FullType);
        if (context.Subject.ExpectedMediaType is { } expected)
        {
            Check.Equal(declared.FullType, expected.FullType, "the media type the codec declares");
        }

        var body = RequestBody.FromValue(s_contact, serde);
        Check.True(
            Equals(body.ContentType, serde.DefaultMediaType),
            "the declared media type must be the default Content-Type of a request body made from a value plus the codec (and must be stable)",
            serde.DefaultMediaType.ToString(),
            body.ContentType?.ToString() ?? "null");
        return Task.CompletedTask;
    }

    // Encoding into, and decoding from, a caller-supplied target reads and writes it fully and never closes it.
    private static async Task TargetNotClosedAsync(CodecContext context, CancellationToken token)
    {
        var bytes = await Check.GuardAsync(() => context.EncodeAsync(s_contact, token), "encoding a contact", token).ConfigureAwait(false);

        if (context.Face == TransportFace.Async)
        {
            using var target = new TrackingStream();
            await Check.GuardAsync(async () => await context.Serde.SerializeAsync(target, s_contact, token).ConfigureAwait(false), "encoding into a caller-owned stream", token).ConfigureAwait(false);
            Check.Equal(target.Closes, 0, "the times the codec closed the caller's target stream");
            Check.True(target.Total > 0, "the codec must write the payload fully into the caller's stream", "bytes written", "none");
            target.WriteByte(0);
        }
        else
        {
            using var writer = new ProbeBufferWriter();
            Check.Guard(() => context.Serde.Serialize(writer, s_contact), "encoding into a caller-owned buffer writer");
            Check.Equal(writer.Disposals, 0, "the times the codec disposed the caller's buffer writer");
            Check.True(writer.Written.Length > 0, "the codec must write the payload fully into the caller's buffer", "bytes written", "none");

            // The encode-into-buffer profile touches only the target region (SEAM-20, SERDE-3).
            var region = new byte[bytes.Length + 8];
            Array.Fill(region, (byte)0xAA);
            var written = Check.Guard(() => context.Serde.Serialize(region.AsSpan(4, bytes.Length), s_contact), "encoding into a caller-owned region");
            var untouched = region.AsSpan(0, 4).ToArray().Concat(region.AsSpan(4 + written).ToArray()).All(b => b == 0xAA);
            Check.True(untouched, "an encode into a region must touch only that region", "the bytes outside it unchanged", "a byte outside it was overwritten");
        }

        using var source = new TrackingStream(bytes);
        var decoded = context.Face == TransportFace.Async
            ? await Check.GuardAsync(async () => await context.Serde.DeserializeAsync<ProbeContact>(source, token).ConfigureAwait(false), "decoding from a caller-owned stream", token).ConfigureAwait(false)
            : Check.Guard(() => context.Serde.Deserialize<ProbeContact>(source), "decoding from a caller-owned stream");
        Check.Equal(source.Closes, 0, "the times the codec closed the caller's source stream");
        Check.True(decoded is not null && Same(s_contact, decoded), "decoding from a caller-owned stream must give the value back", Describe(s_contact), decoded is null ? "null" : Describe(decoded));
        Check.True(source.Cursor == source.Total, "the codec must read a caller's stream to its end", $"position {source.Total}", $"position {source.Cursor}");
    }

    private static bool Same(ProbeContact left, ProbeContact right) =>
        left.Name == right.Name
        && left.Age == right.Age
        && left.Address == right.Address
        && right.Tags is not null
        && left.Tags.SequenceEqual(right.Tags);

    private static string Describe(ProbeContact contact) =>
        $"{contact.Name}, {contact.Age}, {contact.Address}, [{string.Join(", ", contact.Tags ?? [])}]";
}

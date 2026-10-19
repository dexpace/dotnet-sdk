// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/codec_suite/ (failure surfaces). SERDE-13 (a wire null into a
// non-null target, naming the type) is the typed readers' check in core's SerdeValues, after the codec has returned: it stays by reference
// to ReadValueTests and SerdeValuesTests (validation F6).
using System.Buffers;
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>SERDE-9</c>, <c>SERDE-10</c> and <c>SERDE-12</c>: a codec's own failures surface as the SDK's serde exceptions with the cause chained, in
/// the direction they happened, and a genuine stream error is never re-wrapped as one.
/// </summary>
internal static class CodecFailureAssertions
{
    private static readonly ProbeContact s_contact = new("Ada", 36, new ProbeAddress("1 Main Street", "Springfield"), ["alpha"]);

    internal static ConformanceAssertion[] Assertions() =>
    [
        CodecAssertionBuilder.Define("serde-9.no-library-type-escapes", ["SERDE-9"], RequirementLevel.Must, CodecAssertionBuilder.Both, NoLibraryTypeEscapesAsync),
        CodecAssertionBuilder.Define("serde-10.directional-subtypes", ["SERDE-10"], RequirementLevel.Must, CodecAssertionBuilder.Both, DirectionalSubtypesAsync),
        CodecAssertionBuilder.Define("serde-12.stream-error-unwrapped", ["SERDE-12"], RequirementLevel.Must, CodecAssertionBuilder.Both, StreamErrorUnwrappedAsync),
    ];

    // A malformed document and an unencodable value both fail as the SDK's serde exception, with the codec's own error as the cause.
    private static async Task NoLibraryTypeEscapesAsync(CodecContext context, CancellationToken token)
    {
        var read = await FailureOfAsync(() => context.DecodeAsync<ProbeContact>(context.Subject.MalformedPayload.ToArray(), token)).ConfigureAwait(false);
        AssertSdkFailure(read, "reading a malformed document");

        var write = await FailureOfAsync(() => context.EncodeAsync(new ProbeUnencodable(static () => { }), token)).ConfigureAwait(false);
        AssertSdkFailure(write, "writing a value no codec can encode");
    }

    // A read failure is a DeserializationException and a write failure a SerializationException, both of the one serde root.
    private static async Task DirectionalSubtypesAsync(CodecContext context, CancellationToken token)
    {
        var read = await FailureOfAsync(() => context.DecodeAsync<ProbeContact>(context.Subject.MalformedPayload.ToArray(), token)).ConfigureAwait(false);
        Check.True(read is DeserializationException, "a failure on the read path must be a deserialization-specific subtype", "DeserializationException", Outcome.Name(read));

        var write = await FailureOfAsync(() => context.EncodeAsync(new ProbeUnencodable(static () => { }), token)).ConfigureAwait(false);
        Check.True(write is SerializationException, "a failure on the write path must be a serialization-specific subtype", "SerializationException", Outcome.Name(write));
    }

    // A genuine error from the caller's stream (or buffer) travels out as it is, an I/O error, never re-wrapped as a codec failure.
    private static async Task StreamErrorUnwrappedAsync(CodecContext context, CancellationToken token)
    {
        var bytes = await Check.GuardAsync(() => context.EncodeAsync(s_contact, token), "encoding a contact", token).ConfigureAwait(false);
        var reset = new IOException("connection reset by peer");
        var diskFull = new IOException("no space left on device");

        Exception? readFailure;
        Exception? writeFailure;
        if (context.Face == TransportFace.Async)
        {
            readFailure = await FailureOfAsync(async () =>
            {
                using var failing = new TrackingStream(bytes, failure: reset);
                return await context.Serde.DeserializeAsync<ProbeContact>(failing, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
            writeFailure = await FailureOfAsync(async () =>
            {
                using var failing = new TrackingStream(failure: diskFull);
                await context.Serde.SerializeAsync(failing, s_contact, token).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
        }
        else
        {
            readFailure = await FailureOfAsync(() =>
            {
                using var failing = new TrackingStream(bytes, failure: reset);
                return Task.FromResult(context.Serde.Deserialize<ProbeContact>(failing));
            }).ConfigureAwait(false);
            writeFailure = await FailureOfAsync(() =>
            {
                context.Serde.Serialize(new ProbeBufferWriter(diskFull), s_contact);
                return Task.FromResult(true);
            }).ConfigureAwait(false);
        }

        Check.True(readFailure is IOException && readFailure is not SerdeException, "a stream error while reading must propagate unwrapped as an I/O error, not as a codec failure", "the IOException itself", Outcome.Name(readFailure));
        Check.True(writeFailure is IOException && writeFailure is not SerdeException, "a stream error while writing must propagate unwrapped as an I/O error, not as a codec failure", "the IOException itself", Outcome.Name(writeFailure));
    }

    private static void AssertSdkFailure(Exception? failure, string what)
    {
        Check.True(failure is not null, $"{what} must fail", "a failure", "it succeeded");
        Check.True(failure is SerdeException, $"{what} must surface as the SDK's serde exception, never the backing library's own type", "a SerdeException", Outcome.Name(failure));
        Check.True(failure!.InnerException is not null, $"{what} must chain the codec's own failure as the cause", "an inner exception", "none");
    }

    private static async Task<Exception?> FailureOfAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            _ = await operation().ConfigureAwait(false);
            return null;
        }
#pragma warning disable CA1031 // The point of the method: whatever the codec raises is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ex;
        }
    }
}

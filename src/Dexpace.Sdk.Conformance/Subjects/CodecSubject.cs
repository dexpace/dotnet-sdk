// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The codec the codec suite judges (phase 10, P10-8): a factory for the <see cref="ISerde"/> to test, built fresh for each run and
/// exactly as a consumer would build it (no extra configuration: <c>SERDE-19</c> asks whether the defaults wire the PATCH semantics), the
/// media type it is expected to declare, and a payload it must refuse. A codec that needs compile-time metadata registers the
/// <c>Dexpace.Sdk.Conformance.Codec</c> probe models in its own context.
/// </summary>
public sealed record CodecSubject
{
    /// <summary>
    /// The default bytes the codec must refuse to read: a truncated JSON object. They are not a valid document of JSON, XML or the common
    /// binary formats, so most codecs refuse them; a codec for a format that accepts them supplies its own <see cref="MalformedPayload"/>.
    /// </summary>
    public static ReadOnlyMemory<byte> DefaultMalformedPayload => "{\"name\": }"u8.ToArray();

    /// <summary>The name the report carries.</summary>
    public string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            field = value;
        }
    } = "codec";

    /// <summary>Builds the codec, fresh for each run, exactly as a consumer would. Required.</summary>
    public Func<ISerde> CreateSerde
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CreateSerde));
            field = value;
        }
    } = static () => throw new InvalidOperationException("A CodecSubject needs a CreateSerde factory.");

    /// <summary>The media type the codec must declare (<c>SERDE-2</c>), or <see langword="null"/> to check only that it declares a format-specific one.</summary>
    public MediaType? ExpectedMediaType { get; init; }

    /// <summary>Bytes the codec must refuse to read, as a failure of the SDK's deserialization type (<c>SERDE-9</c>, <c>SERDE-10</c>). Defaults to <see cref="DefaultMalformedPayload"/>.</summary>
    public ReadOnlyMemory<byte> MalformedPayload { get; init; } = DefaultMalformedPayload;
}

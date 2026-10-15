// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The codec suite's report identity: its name and what a green run does not prove.</summary>
internal static class CodecIdentity
{
    /// <summary>What a green codec run does not prove.</summary>
    internal const string Preamble =
        "Codec conformance report. A green run does not prove: the codec's own metadata for models it was not given (the probe models are "
        + "the only types the suite serializes); configuration it was not handed (the codec is built exactly as the driver's factory builds "
        + "it); the wire format byte for byte (the suite judges what a codec preserves and refuses, not how it spells it); core's typed "
        + "readers, SerdeExtensions and Tristate<T>'s own API (SERDE-4, SERDE-13, SERDE-14, SERDE-18, SERDE-30, which judge core code "
        + "and are cited by reference); or any requirement no assertion cites. It proves that every assertion that ran met its clause "
        + "over the codec it was given, or is waived below, and nothing more.";

    /// <summary>The identity the report carries.</summary>
    internal static SuiteIdentity Identity { get; } = new("codec", Preamble);
}

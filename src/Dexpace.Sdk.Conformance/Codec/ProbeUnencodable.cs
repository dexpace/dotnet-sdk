// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Codec;

/// <summary>
/// A value no codec can encode: it carries a delegate. Serializing it is the codec suite's write-path failure (<c>SERDE-9</c>,
/// <c>SERDE-10</c>), which must surface as the SDK's serialization failure with the codec's own error chained, never as the codec's own type.
/// A driver registers it with its codec's metadata, one line beside the other probe models, so the failure is the codec's own and not a
/// missing-metadata one.
/// </summary>
/// <param name="Callback">A delegate, which a data format has no representation for.</param>
public sealed record ProbeUnencodable(Action Callback);

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What the suite hands a <see cref="TransportSubject"/> factory each time it builds a transport. A record with one member
/// so that later settings are additive (design "The subject is a factory descriptor", P8a-9).
/// </summary>
public sealed record TransportSettings
{
    /// <summary>
    /// The kit's recording logger. A subject passes it to the transport it builds (for example as the transport's
    /// <see cref="ILogger"/> constructor argument) so assertions can see which header drops the transport logged and prove
    /// that no header value was ever logged (<c>TRANSPORT-11</c>, <c>TRANSPORT-13</c>, <c>XCUT-18</c>). A transport that
    /// logs nothing simply ignores it.
    /// </summary>
    public required ILogger Logger { get; init; }
}

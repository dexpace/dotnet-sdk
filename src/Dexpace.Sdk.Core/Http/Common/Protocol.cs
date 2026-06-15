// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// HTTP protocol versions the SDK can describe on a <see cref="Response.Response"/>.
/// </summary>
/// <remarks>
/// The wire form (returned by <see cref="ProtocolExtensions.ToWireString"/> and consumed by
/// <see cref="ProtocolExtensions.Parse"/>) is lower-case with a slash separator, matching the
/// ALPN identifiers (<c>http/1.1</c>, <c>h2</c>-like). <see cref="H2PriorKnowledge"/> is a
/// marker (no formal ALPN form) used when an HTTP/2 connection is opened without a prior
/// HTTP/1.1 upgrade.
/// </remarks>
public enum Protocol
{
    /// <summary>HTTP/1.0 — legacy; unlikely to be seen in practice.</summary>
    Http10,

    /// <summary>HTTP/1.1 — the default for text-protocol HTTP.</summary>
    Http11,

    /// <summary>HTTP/2 — multiplexed binary protocol negotiated via ALPN.</summary>
    Http2,

    /// <summary>HTTP/2 opened without HTTP/1.1 upgrade (prior-knowledge mode, RFC 7540 §3.4).</summary>
    H2PriorKnowledge,

    /// <summary>QUIC transport (HTTP/3 over UDP).</summary>
    Quic,
}

/// <summary>
/// Wire-form conversions for <see cref="Protocol"/>.
/// </summary>
public static class ProtocolExtensions
{
    /// <summary>Returns the canonical lower-case wire form (e.g. <c>"http/1.1"</c>).</summary>
    /// <param name="protocol">The protocol to render.</param>
    /// <returns>The ALPN-style identifier string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is not a known value.</exception>
    public static string ToWireString(this Protocol protocol) => protocol switch
    {
        Protocol.Http10 => "http/1.0",
        Protocol.Http11 => "http/1.1",
        Protocol.Http2 => "http/2",
        Protocol.H2PriorKnowledge => "h2_prior_knowledge",
        Protocol.Quic => "quic",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unknown protocol."),
    };

    /// <summary>
    /// Parses a protocol identifier (case-insensitively). Accepts the canonical forms emitted by
    /// <see cref="ToWireString"/> plus the alternative spellings <c>HTTP/2</c> and <c>HTTP/2.0</c>.
    /// </summary>
    /// <param name="value">The identifier to parse.</param>
    /// <returns>The matching <see cref="Protocol"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> does not match a known protocol.</exception>
    public static Protocol Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToUpperInvariant() switch
        {
            "HTTP/1.0" => Protocol.Http10,
            "HTTP/1.1" => Protocol.Http11,
            "HTTP/2" or "HTTP/2.0" => Protocol.Http2,
            "H2_PRIOR_KNOWLEDGE" => Protocol.H2PriorKnowledge,
            "QUIC" => Protocol.Quic,
            _ => throw new ArgumentException($"Unexpected protocol: {value}", nameof(value)),
        };
    }
}

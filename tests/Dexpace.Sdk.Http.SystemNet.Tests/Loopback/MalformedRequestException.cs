// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Loopback;

/// <summary>
/// A request the <see cref="LoopbackServer"/> could not frame: an unparseable, overflowing or conflicting
/// <c>Content-Length</c>, a bad chunk size, an over-long head, or a peer that closed mid-request. It carries every
/// byte received for the request up to the failure, and the server records it in <see cref="LoopbackServer.Faults"/>.
/// </summary>
public sealed class MalformedRequestException : Exception
{
    /// <summary>Creates the exception.</summary>
    public MalformedRequestException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What was malformed.</param>
    public MalformedRequestException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">What was malformed.</param>
    /// <param name="innerException">The cause.</param>
    public MalformedRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal MalformedRequestException(string message, byte[] raw, RecordedRequest? partial)
        : base(message)
    {
        Raw = raw;
        Partial = partial;
    }

    /// <summary>Every byte received for the request before the failure.</summary>
    public ReadOnlyMemory<byte> Raw { get; }

    /// <summary><see cref="Raw"/> decoded as Latin-1.</summary>
    public string RawText => Encoding.Latin1.GetString(Raw.Span);

    /// <summary>
    /// The request as far as it was read (request line and header lines, empty body), also recorded in
    /// <see cref="LoopbackServer.Requests"/>; <see langword="null"/> when not even a request line arrived.
    /// </summary>
    public RecordedRequest? Partial { get; }
}

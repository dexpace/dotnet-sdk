// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// A server-sent-events line was longer than the reader's cap (SSE-19), so the stream is mid-line and the reader has
/// failed.
/// </summary>
/// <remarks>
/// <para>
/// This is the one divergence from the specification chapter that SSE-19 (a MAY) licenses and design §10 entry 20
/// records: the reader holds at most <c>maxLineBytes</c> content bytes of a line, 1 MiB by default, so a hostile server
/// that never sends a line terminator cannot grow the process without limit (issue #10). The message names the cap and
/// never the content. The failure is sticky: the source now sits in the middle of a line, so every later read on the same
/// reader throws the same type again. Catch <see cref="StreamingException"/> or <see cref="SdkException"/> to see it
/// without naming this type; the cap is on <see cref="MaxLineBytes"/>.
/// </para>
/// <para>
/// The cap bounds one line, not a block: lines that each fit are accepted without limit until a blank line ends the
/// event (residual R1, P7b-5). Bound a stream from an untrusted server with a cancellation token or
/// <c>OverallTimeout</c>.
/// </para>
/// <para>
/// The type lives in <c>Dexpace.Sdk.Core.ServerSentEvents</c>, not <c>Errors</c>, so every server-sent-events type sits in
/// the namespace the SSE-37 architecture gate watches (P7b-20).
/// </para>
/// </remarks>
public sealed class ServerSentEventLineTooLongException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public ServerSentEventLineTooLongException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServerSentEventLineTooLongException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ServerSentEventLineTooLongException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance that names the cap that was exceeded.</summary>
    /// <param name="maxLineBytes">The cap, in content bytes, that the line exceeded.</param>
    public ServerSentEventLineTooLongException(int maxLineBytes)
        : this(maxLineBytes, null)
    {
    }

    /// <summary>Initializes a new instance that names the cap that was exceeded, with the failure that revealed it.</summary>
    /// <param name="maxLineBytes">The cap, in content bytes, that the line exceeded.</param>
    /// <param name="innerException">The cause, or <see langword="null"/>.</param>
    public ServerSentEventLineTooLongException(int maxLineBytes, Exception? innerException)
        : base(
            "A server-sent-events line exceeded the cap of "
            + maxLineBytes.ToString(CultureInfo.InvariantCulture)
            + " bytes; the stream is now mid-line and the reader has failed.",
            innerException)
    {
        MaxLineBytes = maxLineBytes;
    }

    /// <summary>The cap, in content bytes, that the line exceeded; <c>0</c> when this instance did not record one.</summary>
    public int MaxLineBytes { get; }
}

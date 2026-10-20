// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// The data of one server-sent event passed the reader's per-event cap (SSE-19), so the stream is mid-event and the reader has failed.
/// </summary>
/// <remarks>
/// <para>
/// This is the second cap that SSE-19 (a MAY) licenses and design §10 entry 20 records. The line cap bounds one line; this one bounds the
/// lines of one event, which each fit and never end it: 8 000 000 lines of <c>data:x</c> with no blank line were retained as a single
/// 323 MB event before it existed (residual R1, phase 11 P11-19). The data lines of an event cost their UTF-8 value bytes plus one each;
/// the default cap is <see cref="ServerSentEventReader.DefaultMaxEventBytes"/>, 8 MiB. The message names the cap and never a byte of the
/// content. The failure is sticky: every later read on the same reader throws the same type again, because the source is mid-event.
/// </para>
/// <para>
/// Catch <see cref="StreamingException"/> or <see cref="SdkException"/> to see it without naming this type; the cap is on
/// <see cref="MaxEventBytes"/>. The type lives in <c>Dexpace.Sdk.Core.ServerSentEvents</c>, not <c>Errors</c>, so every server-sent-events
/// type sits in the namespace the SSE-37 architecture gate watches (P7b-20).
/// </para>
/// </remarks>
public sealed class ServerSentEventTooLargeException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public ServerSentEventTooLargeException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServerSentEventTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ServerSentEventTooLargeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance that names the cap that was exceeded.</summary>
    /// <param name="maxEventBytes">The cap, in bytes of event data, that the event exceeded.</param>
    public ServerSentEventTooLargeException(int maxEventBytes)
        : this(maxEventBytes, null)
    {
    }

    /// <summary>Initializes a new instance that names the cap that was exceeded, with the failure that revealed it.</summary>
    /// <param name="maxEventBytes">The cap, in bytes of event data, that the event exceeded.</param>
    /// <param name="innerException">The cause, or <see langword="null"/>.</param>
    public ServerSentEventTooLargeException(int maxEventBytes, Exception? innerException)
        : base(
            "A server-sent-events event accumulated more than the cap of "
            + maxEventBytes.ToString(CultureInfo.InvariantCulture)
            + " bytes of data; the stream is now mid-event and the reader has failed.",
            innerException)
    {
        MaxEventBytes = maxEventBytes;
    }

    /// <summary>The cap, in bytes of event data, that the event exceeded; <c>0</c> when this instance did not record one.</summary>
    public int MaxEventBytes { get; }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Generic;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The one place a dropped header is logged (<c>TRANSPORT-12</c>, <c>TRANSPORT-13</c>, <c>OBS-19</c>, <c>OBS-20</c>, <c>XCUT-18</c>;
/// phase 8b, P8b-12, P8b-13): a drop is never silent, and the <see cref="HeaderDropLogging"/> policy only chooses the level of the
/// request-header drop. Only the header <em>name</em> is ever an argument, escaped for a log line; a value cannot be logged because
/// no method takes one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three events.</b> <see cref="Dropped"/> (id 110, level by policy) is a request header the native client cannot send;
/// <see cref="FramingDropped"/> (id 111, always <see cref="LogLevel.Debug"/>) is a caller-set framing header the transport computes
/// itself (<c>TRANSPORT-11</c>); <see cref="InboundDropped"/> (id 112, always <see cref="LogLevel.Debug"/>) is a response header whose
/// value carries a control character (<c>TRANSPORT-14</c>). The latter two never consult or change the seen-set.
/// </para>
/// <para>
/// <b>The seen-set.</b> For <see cref="HeaderDropLogging.FirstPerName"/> the names already warned about are a lock-guarded set,
/// compared ignoring case, of at most 128 names with the oldest evicted first (a name evicted and dropped again warns again), so a
/// process that sees endlessly many distinct names cannot grow it. It belongs to the transport instance, is pure logging state, and
/// never changes a request or a response, so <c>TRANSPORT-29</c>'s "effectively immutable" holds for the exchange.
/// </para>
/// <para>
/// A logger that throws is swallowed (<c>OBS-20</c>): logging never fails a send.
/// </para>
/// </remarks>
internal sealed partial class HeaderDropLog
{
    private const int MaxNames = 128;

    // One generated method serves both levels of the request-header drop: the event id and name are the same and only the level follows
    // the policy, and the generator rejects two methods that share an id (SYSLIB1006).
    private static readonly Action<ILogger, string> s_warning = static (logger, headerName) => LogDropped(logger, LogLevel.Warning, headerName);
    private static readonly Action<ILogger, string> s_quiet = static (logger, headerName) => LogDropped(logger, LogLevel.Debug, headerName);

    private readonly ILogger _logger;
    private readonly HeaderDropLogging _mode;
    private readonly object _gate = new();
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();

    internal HeaderDropLog(ILogger logger, HeaderDropLogging mode)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _mode = mode;
    }

    /// <summary>How many names the seen-set holds now; never more than 128.</summary>
    internal int SeenCount
    {
        get
        {
            lock (_gate)
            {
                return _seen.Count;
            }
        }
    }

    /// <summary>Logs that the request header <paramref name="name"/> was dropped, at the level the policy gives.</summary>
    /// <param name="name">The header name; escaped for the log line.</param>
    internal void Dropped(string name)
    {
        var escaped = HttpHeaderSyntax.EscapeName(name);
        var loud = _mode switch
        {
            HeaderDropLogging.Every => true,
            HeaderDropLogging.VerboseOnly => false,
            _ => FirstSeen(name),
        };
        Write(loud ? s_warning : s_quiet, escaped);
    }

    /// <summary>Logs that the caller-set framing header <paramref name="name"/> was dropped, at <see cref="LogLevel.Debug"/>.</summary>
    /// <param name="name">The header name; escaped for the log line.</param>
    internal void FramingDropped(string name) => Write(LogFramingDropped, HttpHeaderSyntax.EscapeName(name));

    /// <summary>Logs that the response header <paramref name="name"/> was dropped, at <see cref="LogLevel.Debug"/>.</summary>
    /// <param name="name">The header name; escaped for the log line.</param>
    internal void InboundDropped(string name) => Write(LogInboundDropped, HttpHeaderSyntax.EscapeName(name));

    private bool FirstSeen(string name)
    {
        lock (_gate)
        {
            if (!_seen.Add(name))
            {
                return false;
            }

            _order.Enqueue(name);
            if (_seen.Count > MaxNames)
            {
                _seen.Remove(_order.Dequeue());
            }

            return true;
        }
    }

    // OBS-20: a logger that throws must not fail the send.
    private void Write(Action<ILogger, string> message, string escapedName)
    {
        try
        {
            message(_logger, escapedName);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // Swallowed by design (OBS-20).
        }
    }

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportHeaderDropped,
        Message = "Dropped the '{HeaderName}' request header: the transport could not send it (TRANSPORT-12, XCUT-18).")]
    private static partial void LogDropped(ILogger logger, LogLevel level, string headerName);

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportFramingHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportFramingHeaderDropped,
        Level = LogLevel.Debug,
        Message = "Dropped the caller-set '{HeaderName}' header: the transport computes it (TRANSPORT-11).")]
    private static partial void LogFramingDropped(ILogger logger, string headerName);

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportInboundHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportInboundHeaderDropped,
        Level = LogLevel.Debug,
        Message = "Dropped the '{HeaderName}' response header: its value carries a control character (HTTP-19, XCUT-18).")]
    private static partial void LogInboundDropped(ILogger logger, string headerName);
}

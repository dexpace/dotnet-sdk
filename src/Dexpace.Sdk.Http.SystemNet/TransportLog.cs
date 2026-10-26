// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The one type in this assembly that holds <c>[LoggerMessage]</c> methods (<c>NFR-12</c>, phase 12 task 1.1). The <c>LoggerMessage</c> source generator writes a
/// single <c>LoggerMessage.g.cs</c> per assembly and orders the classes in it differently from one compilation to the next when there is more than one, so two
/// builds of one commit packed different bytes; keeping every generated log method here removes the ordering. A second type with such a method fails
/// <c>LoggerMessageDeterminismArchitectureTests</c>. The callers, <see cref="HeaderDropLog"/> and <see cref="ProxyWarnings"/>, own the policy and the
/// swallow-a-throwing-logger rule (<c>OBS-20</c>); this type owns the event ids, names, levels and message templates.
/// </summary>
internal static partial class TransportLog
{
    // One generated method serves both levels of the request-header drop: the event id and name are the same and only the level follows
    // the policy, and the generator rejects two methods that share an id (SYSLIB1006).
    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportHeaderDropped,
        Message = "Dropped the '{HeaderName}' request header: the transport could not send it (TRANSPORT-12, XCUT-18).")]
    internal static partial void Dropped(ILogger logger, LogLevel level, string headerName);

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportFramingHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportFramingHeaderDropped,
        Level = LogLevel.Debug,
        Message = "Dropped the caller-set '{HeaderName}' header: the transport computes it (TRANSPORT-11).")]
    internal static partial void FramingDropped(ILogger logger, string headerName);

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportInboundHeaderDroppedId,
        EventName = DexpaceLogEvents.TransportInboundHeaderDropped,
        Level = LogLevel.Debug,
        Message = "Dropped the '{HeaderName}' response header: its value carries a control character (HTTP-19, XCUT-18).")]
    internal static partial void InboundDropped(ILogger logger, string headerName);

    [LoggerMessage(
        EventId = DexpaceLogEvents.TransportProxyFeatureUnsupportedId,
        EventName = DexpaceLogEvents.TransportProxyFeatureUnsupported,
        Level = LogLevel.Warning,
        Message = "The transport cannot honour a proxy setting as written: {Feature} (ProxyOptions.{Member}) (TRANSPORT-30).")]
    internal static partial void ProxyFeatureUnsupported(ILogger logger, string feature, string member);
}

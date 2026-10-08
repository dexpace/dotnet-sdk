// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The redirect policy's five log events, ids 150 to 154 (REDIR-28, OBS-39; P6b-19, P6b-20).
/// </summary>
/// <remarks>
/// <para>
/// Written to the call's logger under the emission guard of 5b (OBS-20): a logger that is disabled builds nothing, and one
/// that throws is reported once as <c>http.instrumentation.log_failed</c> and never affects the decision or the response's
/// lifecycle. The events carry a redacted URL and no header or body, so <see cref="Configuration.HttpLoggingOptions.Level"/>
/// does not gate them. Every URL goes through the call's <see cref="UrlRedactor"/>; a malformed <c>Location</c> value, which
/// cannot be parsed, goes through <see cref="UrlRedactor.RedactHeaderValue"/>.
/// </para>
/// </remarks>
internal static class RedirectLog
{
    private static readonly RedactionCache s_redaction = new();

    // The placeholder names are the published OpenTelemetry-style state keys (OBS-39, P5b-3), which CA1727's PascalCase rule
    // and the source generator cannot express; the fixed-key Define form is the verified way (design §8.1).
#pragma warning disable CA1727
    private static readonly Action<ILogger, int, string, string, int, bool, Exception?> s_hop = LoggerMessage.Define<int, string, string, int, bool>(
        LogLevel.Information,
        new EventId(DexpaceLogEvents.RedirectHopId, DexpaceLogEvents.RedirectHop),
        "Following the {http.response.status_code} redirect from {url.full} to {dexpace.redirect.target} (hop {dexpace.redirect.hop}, cross-origin {dexpace.redirect.cross_origin}).");

    private static readonly Action<ILogger, string, string, int, Exception?> s_loop = LoggerMessage.Define<string, string, int>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.RedirectLoopDetectedId, DexpaceLogEvents.RedirectLoopDetected),
        "Not following the redirect from {url.full} to {dexpace.redirect.target}: it was already visited after {dexpace.redirect.hop} hops.");

    private static readonly Action<ILogger, string, string, Exception?> s_rejected = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.RedirectSchemeDowngradeRejectedId, DexpaceLogEvents.RedirectSchemeDowngradeRejected),
        "Refusing the redirect from {url.full} to {dexpace.redirect.target}: it downgrades https to http.");

    private static readonly Action<ILogger, string, string, Exception?> s_permitted = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.RedirectSchemeDowngradePermittedId, DexpaceLogEvents.RedirectSchemeDowngradePermitted),
        "Following the redirect from {url.full} to {dexpace.redirect.target} although it downgrades https to http.");

    private static readonly Action<ILogger, string, string, Exception?> s_malformed = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.RedirectLocationMalformedId, DexpaceLogEvents.RedirectLocationMalformed),
        "Not following the redirect from {url.full}: its Location {dexpace.redirect.location} is unusable.");
#pragma warning restore CA1727

    /// <summary>A hop is followed (before the superseded response is disposed).</summary>
    internal static void Hop(PipelineContext context, Uri current, Uri target, int status, int hop, bool crossOrigin)
    {
        var logger = context.State.Logger;
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var redactor = Redactor(context);
                s_hop(logger, status, redactor.Redact(current), redactor.Redact(target), hop, crossOrigin, null);
            }
        }
        catch (Exception ex) when (EmissionGuard.Reportable(ex, context.CancellationToken))
        {
            EmissionGuard.ReportFailure(logger, DexpaceLogEvents.RedirectHop, ex);
        }
    }

    /// <summary>The target was already visited.</summary>
    internal static void LoopDetected(PipelineContext context, Uri current, Uri target, int followed)
    {
        var logger = context.State.Logger;
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var redactor = Redactor(context);
                s_loop(logger, redactor.Redact(current), redactor.Redact(target), followed, null);
            }
        }
        catch (Exception ex) when (EmissionGuard.Reportable(ex, context.CancellationToken))
        {
            EmissionGuard.ReportFailure(logger, DexpaceLogEvents.RedirectLoopDetected, ex);
        }
    }

    /// <summary>An https to http hop is refused.</summary>
    internal static void DowngradeRejected(PipelineContext context, Uri current, Uri target)
    {
        var logger = context.State.Logger;
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var redactor = Redactor(context);
                s_rejected(logger, redactor.Redact(current), redactor.Redact(target), null);
            }
        }
        catch (Exception ex) when (EmissionGuard.Reportable(ex, context.CancellationToken))
        {
            EmissionGuard.ReportFailure(logger, DexpaceLogEvents.RedirectSchemeDowngradeRejected, ex);
        }
    }

    /// <summary>An https to http hop is followed under the opt-in.</summary>
    internal static void DowngradePermitted(PipelineContext context, Uri current, Uri target)
    {
        var logger = context.State.Logger;
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var redactor = Redactor(context);
                s_permitted(logger, redactor.Redact(current), redactor.Redact(target), null);
            }
        }
        catch (Exception ex) when (EmissionGuard.Reportable(ex, context.CancellationToken))
        {
            EmissionGuard.ReportFailure(logger, DexpaceLogEvents.RedirectSchemeDowngradePermitted, ex);
        }
    }

    /// <summary>An eligible redirect's <c>Location</c> is unusable.</summary>
    internal static void LocationMalformed(PipelineContext context, Uri current, string raw)
    {
        var logger = context.State.Logger;
        try
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var redactor = Redactor(context);
                s_malformed(logger, redactor.Redact(current), redactor.RedactHeaderValue(raw), null);
            }
        }
        catch (Exception ex) when (EmissionGuard.Reportable(ex, context.CancellationToken))
        {
            EmissionGuard.ReportFailure(logger, DexpaceLogEvents.RedirectLocationMalformed, ex);
        }
    }

    private static UrlRedactor Redactor(PipelineContext context) => s_redaction.Get(context.Options.Logging).Redactor;
}

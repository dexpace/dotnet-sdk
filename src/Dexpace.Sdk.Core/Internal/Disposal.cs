// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// Disposes a resource on a path where a failure must not mask the real outcome (design §3.7, P3b-3).
/// </summary>
/// <remarks>
/// <para>
/// A non-fatal exception from the dispose is reported and swallowed: it is recorded as an <c>exception</c> event on
/// <see cref="Activity.Current"/> tagged <c>dexpace.dispose.suppressed</c> and, when a logger is supplied, logged at
/// <see cref="LogLevel.Warning"/> with the resource and exception type names (never a message or a value). When a
/// primary exception is in flight the failure is attached to the primary's trail instead
/// (<see cref="ExceptionTrail.AddSuppressed"/>) and reported nowhere else: exactly one of the two ways, never a third
/// (design §3.7, P4b-15). The primary is never replaced. A fatal exception (<see cref="ExceptionFacts.IsFatal"/>)
/// propagates.
/// </para>
/// <para>
/// <b>Interim gap (accepted, P3b-3).</b> With no listener and no logger the failure is reported to nobody. The gap closes
/// in phase 5b, which plumbs the client's logger to the call sites.
/// </para>
/// </remarks>
internal static class Disposal
{
    private static readonly Action<ILogger, string, string, Exception?> s_suppressed = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(1, "DisposeSuppressed"),
        "Disposing {ResourceType} failed with {ExceptionType}; the failure was suppressed.");

    /// <summary>Disposes <paramref name="resource"/>, reporting and swallowing a non-fatal failure.</summary>
    /// <param name="resource">The resource, or <see langword="null"/> for a no-op.</param>
    /// <param name="primary">The exception already in flight, or <see langword="null"/>; it is never replaced.</param>
    /// <param name="logger">An optional logger for the warning.</param>
    internal static void DisposeQuietly(IDisposable? resource, Exception? primary = null, ILogger? logger = null)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            resource.Dispose();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            if (primary is not null)
            {
                ExceptionTrail.AddSuppressed(primary, ex);
            }
            else
            {
                Report(ex, resource.GetType().Name, logger);
            }
        }
    }

    /// <summary>Disposes <paramref name="resource"/> asynchronously, reporting and swallowing a non-fatal failure.</summary>
    /// <param name="resource">The resource, or <see langword="null"/> for a no-op.</param>
    /// <param name="primary">The exception already in flight, or <see langword="null"/>; it is never replaced.</param>
    /// <param name="logger">An optional logger for the warning.</param>
    /// <returns>A task that completes when the dispose has finished.</returns>
    internal static async ValueTask DisposeQuietlyAsync(
        IAsyncDisposable? resource,
        Exception? primary = null,
        ILogger? logger = null)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            if (primary is not null)
            {
                ExceptionTrail.AddSuppressed(primary, ex);
            }
            else
            {
                Report(ex, resource.GetType().Name, logger);
            }
        }
    }

    private static void Report(Exception ex, string resourceType, ILogger? logger)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            var tags = new TagList { { "dexpace.dispose.suppressed", true } };
            activity.AddException(ex, tags);
        }

        if (logger is not null)
        {
            s_suppressed(logger, resourceType, ex.GetType().Name, null);
        }
    }
}

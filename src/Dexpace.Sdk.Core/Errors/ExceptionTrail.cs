// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// Attaches a secondary failure to the primary one already in flight, so a cleanup error never replaces the real
/// error (design §5.2, §10 entry 13; RETRY-34, PAGE-13).
/// </summary>
/// <remarks>
/// An <see cref="SdkException"/> keeps its trail on <see cref="SdkException.Suppressed"/>. A foreign exception keeps
/// it under the <see cref="Exception.Data"/> key <c>"Dexpace.Sdk.Core.Suppressed"</c>, readable only through
/// <see cref="GetSuppressed"/>; its <c>ToString()</c> does not render the trail. If a foreign exception's
/// <see cref="Exception.Data"/> is read-only, the secondary is dropped rather than thrown, because throwing here would
/// replace the primary. Nothing is attached to or from a fatal exception, to the primary itself, or twice.
/// </remarks>
public static class ExceptionTrail
{
    private const string DataKey = "Dexpace.Sdk.Core.Suppressed";

    private static readonly object s_foreignGate = new();

    /// <summary>Adds <paramref name="secondary"/> to the trail of <paramref name="primary"/>.</summary>
    /// <param name="primary">The exception in flight.</param>
    /// <param name="secondary">The failure to attach.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void AddSuppressed(Exception primary, Exception secondary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);
        if (ReferenceEquals(primary, secondary) || ExceptionFacts.IsFatal(primary) || ExceptionFacts.IsFatal(secondary))
        {
            return;
        }

        if (primary is SdkException sdk)
        {
            sdk.Trail.Add(secondary);
            return;
        }

        try
        {
            lock (s_foreignGate)
            {
                if (primary.Data[DataKey] is not SuppressedTrail trail)
                {
                    trail = new SuppressedTrail();
                    primary.Data[DataKey] = trail;
                }

                trail.Add(secondary);
            }
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // Drop the secondary: throwing here (NotSupportedException from a read-only Data) would replace the
            // primary, the one failure the trail exists to prevent (design §5.2, P4b-14).
        }
    }

    /// <summary>Returns a snapshot of the exceptions suppressed under <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception to read.</param>
    /// <returns>The trail at the time of the call; empty when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<Exception> GetSuppressed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is SdkException sdk)
        {
            return sdk.Suppressed;
        }

        try
        {
            lock (s_foreignGate)
            {
                return exception.Data[DataKey] is SuppressedTrail trail ? trail.Snapshot() : [];
            }
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return [];
        }
    }
}

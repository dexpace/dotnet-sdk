// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The severity order the report's worst-of views use (design section C): <see cref="ConformanceStatus.Errored"/> beats
/// <see cref="ConformanceStatus.Failed"/> beats <see cref="ConformanceStatus.Waived"/> beats
/// <see cref="ConformanceStatus.NotExercised"/> beats <see cref="ConformanceStatus.Vacuous"/> beats
/// <see cref="ConformanceStatus.Passed"/>. A waived gap and a hole in coverage both rank above a quiet pass, so a requirement
/// is never reported greener than its weakest evidence.
/// </summary>
internal static class StatusSeverity
{
    /// <summary>The rank of <paramref name="status"/>: higher is worse.</summary>
    /// <param name="status">The status to rank.</param>
    internal static int Rank(ConformanceStatus status) => status switch
    {
        ConformanceStatus.Errored => 5,
        ConformanceStatus.Failed => 4,
        ConformanceStatus.Waived => 3,
        ConformanceStatus.NotExercised => 2,
        ConformanceStatus.Vacuous => 1,
        ConformanceStatus.Passed => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown conformance status."),
    };

    /// <summary>The most severe of <paramref name="statuses"/>.</summary>
    /// <param name="statuses">The statuses to combine; at least one.</param>
    /// <exception cref="InvalidOperationException"><paramref name="statuses"/> is empty.</exception>
    internal static ConformanceStatus Worst(IEnumerable<ConformanceStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var found = false;
        var worst = ConformanceStatus.Passed;
        foreach (var status in statuses)
        {
            if (!found || Rank(status) > Rank(worst))
            {
                worst = status;
                found = true;
            }
        }

        return found ? worst : throw new InvalidOperationException("There is no status to combine.");
    }
}

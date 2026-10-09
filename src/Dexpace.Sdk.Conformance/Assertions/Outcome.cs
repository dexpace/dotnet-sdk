// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>Observing how a call ended, bounded, without ever hanging the run (suite-contract clause 6).</summary>
internal static class Outcome
{
    /// <summary>
    /// Waits for <paramref name="call"/> to settle within <paramref name="bound"/> and returns the exception it failed with,
    /// or <see langword="null"/> when it succeeded (the response is disposed). A call that does not settle in time is a
    /// <see cref="ConformanceException"/> naming <paramref name="what"/>.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="bound">How long it may take to settle.</param>
    /// <param name="what">What the call was waiting on, named in the failure.</param>
    /// <param name="cancellationToken">The assertion's token.</param>
    internal static async Task<Exception?> OfAsync(Task<Response> call, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        await Bounded.WaitAsync(SettledAsync(call), bound, what, cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await call.ConfigureAwait(false);
            return null;
        }
#pragma warning disable CA1031 // The point of the method: whatever the transport failed with is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ex;
        }
    }

    /// <summary>The first exception in <paramref name="failure"/>'s cause chain (itself included) that is a <typeparamref name="T"/>, or <see langword="null"/>. Cycle-safe.</summary>
    /// <param name="failure">The exception to search.</param>
    internal static T? Find<T>(Exception? failure)
        where T : Exception
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        for (var current = failure; current is not null && seen.Add(current); current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>The type name of <paramref name="failure"/>, or <c>no failure</c>, for a message.</summary>
    internal static string Name(Exception? failure) => failure?.GetType().Name ?? "no failure (the call succeeded)";

    private static async Task SettledAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Settling is all that is asked here: the outcome is read from the task afterwards.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}

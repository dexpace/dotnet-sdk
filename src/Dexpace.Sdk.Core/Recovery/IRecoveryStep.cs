// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// A step that observes the terminal outcome of the response phase and may replace it (RECOV-5, RECOV-8, RECOV-9).
/// </summary>
/// <remarks>
/// <para>
/// Recovery steps run on every outcome, always, in declared order, after the response phase. A step SHOULD return a
/// <c>new Outcome.Failure(...)</c> rather than throw (RECOV-9); a throw is converted to a failure and fed to the next
/// step, so the chain never throws but for a fatal exception.
/// </para>
/// <para>
/// A step that returns a different outcome owns what it dropped, such as the response of the original success
/// (RECOV-13): the chain never disposes the original of a returned outcome.
/// </para>
/// <para>
/// <b>Concurrency (RECOV-14):</b> one instance may be applied concurrently by many calls. Keep per-call state in the
/// value, never in a field.
/// </para>
/// </remarks>
public interface IRecoveryStep
{
    /// <summary>Observes <paramref name="outcome"/> and returns the outcome to continue with.</summary>
    /// <param name="outcome">The outcome so far.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The outcome to pass on; never <see langword="null"/>.</returns>
    Outcome Apply(Outcome outcome, CancellationToken cancellationToken);

    /// <summary>Observes <paramref name="outcome"/> asynchronously and returns the outcome to continue with.</summary>
    /// <param name="outcome">The outcome so far.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The outcome to pass on; never <see langword="null"/>.</returns>
    ValueTask<Outcome> ApplyAsync(Outcome outcome, CancellationToken cancellationToken);
}

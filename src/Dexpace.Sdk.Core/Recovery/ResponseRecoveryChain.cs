// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// Folds an <see cref="Outcome"/> through the response phase and then the recovery phase (RECOV-4 to RECOV-9,
/// RECOV-12 to RECOV-14).
/// </summary>
/// <remarks>
/// <para>
/// The response phase runs only while the outcome is a success; the first throwing step turns it into a failure and the
/// rest of the phase is skipped. The recovery phase then runs on every outcome, in order, each step seeing what the
/// previous one produced. A throw from any step is converted to a failure (the response in hand is released first,
/// RECOV-12), so <see cref="Apply"/> never throws except for a fatal exception (<see cref="ExceptionFacts.IsFatal"/>,
/// RECOV-8). A step that returns <see langword="null"/> is treated as having thrown
/// <see cref="InvalidOperationException"/> (P4b-8). The chain never checks the token itself: steps observe the token they
/// are handed, so a cancelled token cannot make <see cref="Apply"/> throw.
/// </para>
/// <para>Both step lists are copied at construction (RECOV-14). The sync and async forms share one body.</para>
/// </remarks>
public sealed class ResponseRecoveryChain
{
    private readonly IResponseStep[] _responseSteps;
    private readonly IRecoveryStep[] _recoverySteps;

    /// <summary>Creates a chain over copies of both lists.</summary>
    /// <param name="responseSteps">The response-phase steps, in order.</param>
    /// <param name="recoverySteps">The recovery-phase steps, in order.</param>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A list holds a <see langword="null"/> element.</exception>
    public ResponseRecoveryChain(IEnumerable<IResponseStep> responseSteps, IEnumerable<IRecoveryStep> recoverySteps)
    {
        ArgumentNullException.ThrowIfNull(responseSteps);
        ArgumentNullException.ThrowIfNull(recoverySteps);
        _responseSteps = [.. responseSteps];
        _recoverySteps = [.. recoverySteps];
        if (Array.IndexOf(_responseSteps, null) >= 0)
        {
            throw new ArgumentException("A step must not be null.", nameof(responseSteps));
        }

        if (Array.IndexOf(_recoverySteps, null) >= 0)
        {
            throw new ArgumentException("A step must not be null.", nameof(recoverySteps));
        }

        ResponseSteps = new ReadOnlyCollection<IResponseStep>(_responseSteps);
        RecoverySteps = new ReadOnlyCollection<IRecoveryStep>(_recoverySteps);
    }

    /// <summary>The chain with no steps: it returns its input outcome by reference.</summary>
    public static ResponseRecoveryChain Empty { get; } = new([], []);

    /// <summary>A read-only view of the chain's copy of the response-phase steps.</summary>
    public IReadOnlyList<IResponseStep> ResponseSteps { get; }

    /// <summary>A read-only view of the chain's copy of the recovery-phase steps.</summary>
    public IReadOnlyList<IRecoveryStep> RecoverySteps { get; }

    /// <summary>Runs the response phase, then the recovery phase.</summary>
    /// <param name="outcome">The outcome to fold.</param>
    /// <param name="cancellationToken">A token handed to every step.</param>
    /// <returns>The terminal outcome. Never throws but for a fatal exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is <see langword="null"/>.</exception>
    public Outcome Apply(Outcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return SyncPath.GetResult(ApplyCoreAsync(outcome, async: false, cancellationToken));
    }

    /// <summary>Runs the response phase, then the recovery phase, asynchronously.</summary>
    /// <param name="outcome">The outcome to fold.</param>
    /// <param name="cancellationToken">A token handed to every step.</param>
    /// <returns>The terminal outcome. Never faults but for a fatal exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is <see langword="null"/>.</exception>
    public ValueTask<Outcome> ApplyAsync(Outcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return ApplyCoreAsync(outcome, async: true, cancellationToken);
    }

    private async ValueTask<Outcome> ApplyCoreAsync(Outcome outcome, bool async, CancellationToken cancellationToken)
    {
        var afterResponses = await RunResponsePhaseAsync(outcome, async, cancellationToken).ConfigureAwait(false);
        return await RunRecoveryPhaseAsync(afterResponses, async, cancellationToken).ConfigureAwait(false);
    }

    // 6a (P6a-5): the retry composition runs the response steps once per send and the recovery steps once per call, so the
    // two phases are reachable apart. Public Apply/ApplyAsync still run both.
    internal ValueTask<Outcome> ApplyResponsePhaseAsync(Outcome outcome, bool async, CancellationToken cancellationToken) =>
        RunResponsePhaseAsync(outcome, async, cancellationToken);

    internal ValueTask<Outcome> ApplyRecoveryPhaseAsync(Outcome outcome, bool async, CancellationToken cancellationToken) =>
        RunRecoveryPhaseAsync(outcome, async, cancellationToken);

    private async ValueTask<Outcome> RunResponsePhaseAsync(Outcome outcome, bool async, CancellationToken cancellationToken)
    {
        var current = outcome;
        foreach (var step in _responseSteps)
        {
            if (current is not Outcome.Success success)
            {
                break;
            }

            try
            {
                var next = async
                    ? await step.ApplyAsync(success.Response, cancellationToken).ConfigureAwait(false)
                    : step.Apply(success.Response, cancellationToken);
                if (next is null)
                {
                    throw NullReturn("response", step);
                }

                if (!ReferenceEquals(next, success.Response))
                {
                    current = new Outcome.Success(next);
                }
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                current = await StepFailure.ConvertAsync(ex, current, async).ConfigureAwait(false);
            }
        }

        return current;
    }

    private async ValueTask<Outcome> RunRecoveryPhaseAsync(Outcome outcome, bool async, CancellationToken cancellationToken)
    {
        var current = outcome;
        foreach (var step in _recoverySteps)
        {
            try
            {
                var next = async
                    ? await step.ApplyAsync(current, cancellationToken).ConfigureAwait(false)
                    : step.Apply(current, cancellationToken);
                current = next ?? throw NullReturn("recovery", step);
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                current = await StepFailure.ConvertAsync(ex, current, async).ConfigureAwait(false);
            }
        }

        return current;
    }

    private static InvalidOperationException NullReturn(string kind, object step) =>
        new($"The {kind} step {step.GetType().FullName} returned null.");
}

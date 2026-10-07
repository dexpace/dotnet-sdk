// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// Folds a request through a fixed list of <see cref="IRequestStep"/>s, left to right (RECOV-3).
/// </summary>
/// <remarks>
/// The steps are copied at construction (RECOV-14), so the caller's list can change afterwards and one chain serves
/// concurrent calls. A step that throws aborts the rest and the exception propagates: the
/// <see cref="RecoveryDispatcher"/> converts it to a failure. The sync and async forms share one body.
/// </remarks>
public sealed class RequestRecoveryChain
{
    private readonly IRequestStep[] _steps;

    /// <summary>Creates a chain over a copy of <paramref name="steps"/>.</summary>
    /// <param name="steps">The steps, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="steps"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="steps"/> holds a <see langword="null"/> element.</exception>
    public RequestRecoveryChain(IEnumerable<IRequestStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = [.. steps];
        if (Array.IndexOf(_steps, null) >= 0)
        {
            throw new ArgumentException("A step must not be null.", nameof(steps));
        }

        Steps = new ReadOnlyCollection<IRequestStep>(_steps);
    }

    /// <summary>The chain with no steps: it returns its input by reference.</summary>
    public static RequestRecoveryChain Empty { get; } = new([]);

    /// <summary>A read-only view of the chain's copy of the steps.</summary>
    public IReadOnlyList<IRequestStep> Steps { get; }

    /// <summary>Applies every step in order.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token handed to every step.</param>
    /// <returns>The transformed request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public Request Apply(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SyncPath.GetResult(ApplyCoreAsync(request, async: false, cancellationToken));
    }

    /// <summary>Applies every step in order, asynchronously.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token handed to every step.</param>
    /// <returns>The transformed request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyCoreAsync(request, async: true, cancellationToken);
    }

    private async ValueTask<Request> ApplyCoreAsync(Request request, bool async, CancellationToken cancellationToken)
    {
        var current = request;
        foreach (var step in _steps)
        {
            var next = async
                ? await step.ApplyAsync(current, cancellationToken).ConfigureAwait(false)
                : step.Apply(current, cancellationToken);
            current = next ?? throw new InvalidOperationException(
                $"The request step {step.GetType().FullName} returned null.");
        }

        return current;
    }
}

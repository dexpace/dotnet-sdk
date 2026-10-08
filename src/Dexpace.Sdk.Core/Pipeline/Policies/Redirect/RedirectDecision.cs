// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// What <see cref="RedirectDecider"/> decided for one hop. Carries everything the policy needs to act and to emit its
/// events, so the decider stays pure (R8).
/// </summary>
internal readonly struct RedirectDecision
{
    private RedirectDecision(
        RedirectDecisionKind kind,
        Request? next,
        Uri? target,
        bool crossOrigin,
        bool downgraded,
        RedirectStopReason reason,
        string? malformedRaw,
        Exception? exception,
        RedirectFailureKind failureKind)
    {
        Kind = kind;
        Next = next;
        Target = target;
        CrossOrigin = crossOrigin;
        Downgraded = downgraded;
        Reason = reason;
        MalformedRaw = malformedRaw;
        Exception = exception;
        FailureKind = failureKind;
    }

    /// <summary>The outcome.</summary>
    internal RedirectDecisionKind Kind { get; }

    /// <summary>The request to send next; set for a follow.</summary>
    internal Request? Next { get; }

    /// <summary>The resolved target: set for a follow, and for a loop stop.</summary>
    internal Uri? Target { get; }

    /// <summary>Whether the followed hop leaves the seed origin.</summary>
    internal bool CrossOrigin { get; }

    /// <summary>Whether the followed hop is an https to http downgrade the options permit.</summary>
    internal bool Downgraded { get; }

    /// <summary>The reason for a stop.</summary>
    internal RedirectStopReason Reason { get; }

    /// <summary>The raw <c>Location</c> of a malformed stop; <see langword="null"/> when it was merely absent.</summary>
    internal string? MalformedRaw { get; }

    /// <summary>The exception of a failure.</summary>
    internal Exception? Exception { get; }

    /// <summary>The kind of a failure.</summary>
    internal RedirectFailureKind FailureKind { get; }

    internal static RedirectDecision Follow(Request next, Uri target, bool crossOrigin, bool downgraded) =>
        new(RedirectDecisionKind.Follow, next, target, crossOrigin, downgraded, default, null, null, RedirectFailureKind.None);

    internal static RedirectDecision ReturnCurrent(RedirectStopReason reason, Uri? target = null, string? malformedRaw = null) =>
        new(RedirectDecisionKind.ReturnCurrent, null, target, false, false, reason, malformedRaw, null, RedirectFailureKind.None);

    internal static RedirectDecision Fail(Exception exception, RedirectFailureKind kind) =>
        new(RedirectDecisionKind.Fail, null, null, false, false, default, null, exception, kind);
}

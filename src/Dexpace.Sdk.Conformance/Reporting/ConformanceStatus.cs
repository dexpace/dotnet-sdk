// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The outcome of one assertion on one face (design section C, P8a-7). Six statuses, one more than Ruby's five: a subject
/// that lacks a hook is <see cref="NotExercised"/>, never <see cref="Vacuous"/>, so a lazy subject cannot report a
/// vacuity it never measured.
/// </summary>
public enum ConformanceStatus
{
    /// <summary>The assertion ran and the transport met the clause.</summary>
    Passed = 0,

    /// <summary>The assertion ran and the transport broke the clause: the body threw a <see cref="ConformanceException"/>.</summary>
    Failed = 1,

    /// <summary>
    /// The assertion could not finish: something other than a <see cref="ConformanceException"/> was thrown, a kit bug or a
    /// transport crash. Never silently a failure, and never green.
    /// </summary>
    Errored = 2,

    /// <summary>
    /// The assertion established, from inside, that the clause's antecedent is absent (it threw a
    /// <see cref="ConformanceVacuousException"/> with the reason): the clause holds for want of anything to hold on.
    /// Vacuity is measured, not claimed.
    /// </summary>
    Vacuous = 3,

    /// <summary>The assertion failed or errored and a <see cref="ConformanceWaiver"/> names one of its requirement IDs.</summary>
    Waived = 4,

    /// <summary>The subject supplies neither the face nor the capability hook the assertion needs, so nothing was exercised.</summary>
    NotExercised = 5,
}

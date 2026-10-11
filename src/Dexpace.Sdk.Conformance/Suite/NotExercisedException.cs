// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Thrown from inside an assertion when the subject lacks a capability hook the assertion needs; the runner reports
/// <see cref="ConformanceStatus.NotExercised"/> with <see cref="Exception.Message"/> (which names the hook). Never
/// <see cref="ConformanceStatus.Vacuous"/>: a missing construction is not an absent antecedent (P8a-7).
/// </summary>
internal sealed class NotExercisedException : Exception
{
    internal NotExercisedException(string message)
        : base(message)
    {
    }
}

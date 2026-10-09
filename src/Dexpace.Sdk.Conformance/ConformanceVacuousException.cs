// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Thrown from inside an assertion to say that the clause's antecedent is absent for this subject, so the clause holds
/// vacuously: the run reports <see cref="ConformanceStatus.Vacuous"/> with <see cref="Exception.Message"/> as the reason.
/// Vacuity is measured from inside the assertion, never claimed by a subject (design section C, P8a-7).
/// </summary>
public sealed class ConformanceVacuousException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public ConformanceVacuousException()
    {
    }

    /// <summary>Initializes a new instance with the reason the antecedent is absent.</summary>
    /// <param name="message">The reason.</param>
    public ConformanceVacuousException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the reason and a cause.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public ConformanceVacuousException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

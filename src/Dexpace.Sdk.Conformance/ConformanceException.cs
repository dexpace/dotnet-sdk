// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// An assertion's verdict that the transport under test broke a requirement. It derives from <see cref="Exception"/> and
/// not from <c>SdkException</c> on purpose (P8a-6): a conformance failure is a test result, and a caller's
/// <c>catch (SdkException)</c> around a send must never swallow one.
/// </summary>
/// <remarks>
/// The message names the clause and stays one line; <see cref="Expected"/> and <see cref="Actual"/> carry the comparison
/// the report renders. It never carries a request or response body, a credential or a full URL.
/// </remarks>
public sealed class ConformanceException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public ConformanceException()
    {
    }

    /// <summary>Initializes a new instance with a message naming the clause that was broken.</summary>
    /// <param name="message">The failure message.</param>
    public ConformanceException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the exception that made the assertion fail.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The cause.</param>
    public ConformanceException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with a message and the comparison that failed.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="expected">What the clause requires, rendered for the report.</param>
    /// <param name="actual">What the transport did, rendered for the report.</param>
    public ConformanceException(string message, string? expected, string? actual)
        : base(message)
    {
        Expected = expected;
        Actual = actual;
    }

    /// <summary>What the clause requires, or <see langword="null"/> when the failure is not a comparison.</summary>
    public string? Expected { get; }

    /// <summary>What the transport did, or <see langword="null"/> when the failure is not a comparison.</summary>
    public string? Actual { get; }
}

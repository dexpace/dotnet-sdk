// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// One named conformance check of a transport (design "The assertion catalogue"). A value, not a callable: it is run by
/// <c>TransportSuite.RunAsync</c>, and its body stays internal so a later release can repair an assertion without a
/// public-surface change. Names are stable public identifiers; a consumer's waiver list refers to them.
/// </summary>
public sealed class ConformanceAssertion
{
    internal ConformanceAssertion(
        string name,
        IReadOnlyList<string> requirementIds,
        RequirementLevel level,
        IReadOnlyList<TransportFace> faces,
        Func<SuiteContext, CancellationToken, Task> body)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(requirementIds);
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(body);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An assertion needs a name.", nameof(name));
        }

        if (requirementIds.Count == 0)
        {
            throw new ArgumentException("An assertion cites at least one requirement.", nameof(requirementIds));
        }

        if (faces.Count == 0)
        {
            throw new ArgumentException("An assertion declares at least one face.", nameof(faces));
        }

        Name = name;
        RequirementIds = [.. requirementIds.Select(RequirementIndex.Require)];
        Level = level;
        Faces = [.. faces];
        Body = body;
    }

    /// <summary>The assertion's check: throws <see cref="ConformanceException"/> when the transport breaks the clause.</summary>
    internal Func<SuiteContext, CancellationToken, Task> Body { get; }

    /// <summary>The stable name, for example <c>transport-24.vendor-status-readable</c>.</summary>
    public string Name { get; }

    /// <summary>The requirements the assertion is evidence for, for example <c>TRANSPORT-24</c>.</summary>
    public IReadOnlyList<string> RequirementIds { get; }

    /// <summary>
    /// The strength of the clause the assertion checks. Declared per assertion, not derived from its IDs: a requirement such
    /// as <c>TRANSPORT-11</c> is a MUST for its framing clause and a SHOULD for its logging clause, and each has its own
    /// assertion. It never exceeds the strongest level among <see cref="RequirementIds"/>.
    /// </summary>
    public RequirementLevel Level { get; }

    /// <summary>The faces the assertion runs on; the suite runs it once per face the subject supplies.</summary>
    public IReadOnlyList<TransportFace> Faces { get; }

    /// <summary>The assertion's <see cref="Name"/>.</summary>
    public override string ToString() => Name;
}

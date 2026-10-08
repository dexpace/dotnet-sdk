// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>The body a matrix row's request carries.</summary>
internal enum BodyKind
{
    /// <summary>No body.</summary>
    None = 0,

    /// <summary>A replayable bytes body.</summary>
    Bytes = 1,

    /// <summary>A single-use stream body.</summary>
    SingleUse = 2,

    /// <summary>A seekable stream body with a declared length (replayable, 3b).</summary>
    Seekable = 3,
}

/// <summary>What a matrix row expects of the decision.</summary>
internal sealed record Expect(RedirectDecisionKind Kind)
{
    /// <summary>The stop reason of a <see cref="RedirectDecisionKind.ReturnCurrent"/> row.</summary>
    public RedirectStopReason? Reason { get; init; }

    /// <summary>The exception type of a <see cref="RedirectDecisionKind.Fail"/> row.</summary>
    public Type? ExceptionType { get; init; }

    /// <summary>The method of the next request of a follow row.</summary>
    public string? NextMethod { get; init; }

    /// <summary>The exact <c>AbsoluteUri</c> of the next request of a follow row.</summary>
    public string? NextUrl { get; init; }

    /// <summary>Header names that must be present on the next request.</summary>
    public string[] HeadersPresent { get; init; } = [];

    /// <summary>Header names that must be absent from the next request.</summary>
    public string[] HeadersAbsent { get; init; } = [];

    /// <summary>Whether the next request carries the very same body instance.</summary>
    public bool? SameBody { get; init; }

    /// <summary>Whether the next request carries no body.</summary>
    public bool? NoBody { get; init; }

    /// <summary>The expected cross-origin flag of a follow.</summary>
    public bool? CrossOrigin { get; init; }

    /// <summary>The expected downgraded flag of a follow.</summary>
    public bool? Downgraded { get; init; }

    /// <summary>The number of predicate calls, when the row has a predicate.</summary>
    public int? PredicateCalls { get; init; }

    /// <summary>Whether a malformed raw value is expected (as opposed to an absent Location).</summary>
    public bool? Malformed { get; init; }
}

/// <summary>One row of the decision matrix. Built in code so a row can hold options and a predicate lambda (R9).</summary>
internal sealed record RedirectCase(string Name, int Status, Expect Expect)
{
    /// <summary>The method of the first request.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>The URL of the first request.</summary>
    public string Url { get; init; } = "https://api.example.com/a/b";

    /// <summary>The seed URL when it differs from <see cref="Url"/>.</summary>
    public string? SeedUrl { get; init; }

    /// <summary>The first request's body.</summary>
    public BodyKind Body { get; init; }

    /// <summary>Headers on the first request, as name and value.</summary>
    public (string Name, string Value)[] RequestHeaders { get; init; } = [];

    /// <summary>The <c>Location</c> values; empty for none.</summary>
    public string[] LocationValues { get; init; } = [];

    /// <summary>Further response headers.</summary>
    public (string Name, string Value)[] ResponseHeaders { get; init; } = [];

    /// <summary>The options.</summary>
    public RedirectOptions Options { get; init; } = new();

    /// <summary>Hops to take through synthetic distinct URLs before the decision.</summary>
    public int Followed { get; init; }

    /// <summary>URLs to advance through after the first request, so the last is the current hop.</summary>
    public string[] Visited { get; init; } = [];
}

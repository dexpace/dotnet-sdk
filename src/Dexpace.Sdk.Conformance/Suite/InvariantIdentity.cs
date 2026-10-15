// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The invariant suite's report identity: its name and what a green run does not prove.</summary>
internal static class InvariantIdentity
{
    /// <summary>What a green invariant run does not prove.</summary>
    internal const string Preamble =
        "Invariant conformance report. A green run does not prove: any clause whose hook the driver did not supply (such an "
        + "assertion is NotExercised, never passed, and a driver pins the list it expects); anything about core's internals "
        + "except through the hooks; behaviour over a real socket (the transport suite's); or any requirement no assertion cites. "
        + "It proves that every assertion that ran met its clause over the composition it was given, or is waived below, and "
        + "nothing more. Run against the defaults it judges core, and nothing about a third party's composition until they supply theirs.";

    /// <summary>The identity the report carries.</summary>
    internal static SuiteIdentity Identity { get; } = new("invariant", Preamble);
}

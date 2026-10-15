// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>The packaging suite's report identity: its name and what a green run does not prove.</summary>
internal static class PackagingIdentity
{
    /// <summary>What a green packaging run does not prove.</summary>
    internal const string Preamble =
        "Packaging conformance report. A green run does not prove: anything about a package it was not given (it reads the packages it was given; "
        + "a package built from another tree is a different subject); how the assemblies behave (it reads their metadata and never loads them); "
        + "the build's reproducibility, repository signing or the release path (NFR-12, NFR-16: other gates); or any requirement no assertion cites. "
        + "It proves that every assertion that ran met its clause over the packages it read, or is waived below, and nothing more.";

    /// <summary>The identity the report carries.</summary>
    internal static SuiteIdentity Identity { get; } = new("packaging", Preamble);
}

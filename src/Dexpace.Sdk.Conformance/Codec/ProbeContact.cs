// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Codec;

/// <summary>
/// The round-trip probe model: a string, a number, a nested record and a list. A codec that needs compile-time metadata (System.Text.Json
/// with a source-generated context) registers it with one line, beside the other probe models (phase 10, P10-8).
/// </summary>
/// <param name="Name">A name.</param>
/// <param name="Age">A number.</param>
/// <param name="Address">A nested record.</param>
/// <param name="Tags">A list of strings.</param>
public sealed record ProbeContact(string Name, int Age, ProbeAddress Address, IReadOnlyList<string> Tags);

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Architecture;

/// <summary>
/// P8a-4, P8a-6, roadmap constraint 2: the kit is framework-free and adapter-free. Its assembly references core, the
/// logging abstractions core already carries, and the shared framework, and nothing else: no test framework (an
/// adapter author drives it from any of them) and no transport or codec adapter (the kit certifies those, it never
/// takes one).
/// </summary>
[Trait("Category", "Unit")]
public sealed class KitDependencyArchitectureTests
{
    private static readonly string[] s_allowedByName =
    [
        "Dexpace.Sdk.Core",
        "Microsoft.Extensions.Logging.Abstractions",
    ];

    private static readonly string[] s_forbiddenPrefixes =
    [
        "xunit",
        "NUnit",
        "MSTest",
        "Microsoft.VisualStudio.TestPlatform",
        "Microsoft.Testing",
        "Dexpace.Sdk.Http",
        "Dexpace.Sdk.Serialization",
        "Dexpace.Sdk.TestSupport",
    ];

    [Fact]
    public void The_kit_references_only_core_the_logging_abstractions_and_the_shared_framework() =>
        Assert.Empty(Unexpected(ReferencedNames(typeof(RequirementLevel).Assembly)));

    [Fact]
    public void The_kit_references_no_test_framework_and_no_adapter() =>
        Assert.Empty(Forbidden(ReferencedNames(typeof(RequirementLevel).Assembly)));

    [Fact]
    public void The_unexpected_reference_check_can_fail()
    {
        // Negative control: the checks above are only worth anything if a reference they forbid is reported.
        string[] referenced = ["System.Runtime", "Dexpace.Sdk.Core", "xunit.v3.assert", "Dexpace.Sdk.Http.SystemNet", "Newtonsoft.Json"];

        Assert.Equal(["Dexpace.Sdk.Http.SystemNet", "Newtonsoft.Json", "xunit.v3.assert"], Unexpected(referenced).Order(StringComparer.Ordinal));
        Assert.Equal(["Dexpace.Sdk.Http.SystemNet", "xunit.v3.assert"], Forbidden(referenced).Order(StringComparer.Ordinal));
    }

    private static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

    private static IEnumerable<string> Unexpected(IEnumerable<string> names) =>
        names.Where(name => !IsFramework(name) && !s_allowedByName.Contains(name, StringComparer.Ordinal));

    private static IEnumerable<string> Forbidden(IEnumerable<string> names) =>
        names.Where(name => s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    // The shared framework's reference assemblies: System, System.*, netstandard and mscorlib. Microsoft.Extensions.* is
    // deliberately not here: it is not part of the shared framework and is allowed by name only.
    private static bool IsFramework(string name) =>
        name is "System" or "netstandard" or "mscorlib"
        || name.StartsWith("System.", StringComparison.Ordinal);
}

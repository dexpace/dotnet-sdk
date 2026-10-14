// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/packaging_suite/ (the dependency budget), over nuspecs instead of
// gemspecs. The same rule runs over the build output in scripts/ci/dependency-audit.cs (two checks, two subjects).
namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>NFR-1</c> and <c>NFR-2</c> over the published nuspecs: core depends on the logging facade and nothing else (design §10 entry 1), and every
/// other package depends on core, the first-party packages its policy names and at most one third-party root (design §10 entry 2), all first-party
/// ones at the lockstep version.
/// </summary>
internal static class DependencyPackagingAssertions
{
    /// <summary>The one dependency core may carry: the logging facade, compile-time only in spirit (design §10 entry 1).</summary>
    internal const string Facade = "Microsoft.Extensions.Logging.Abstractions";

    private const string FirstParty = "Dexpace.";

    internal static ConformanceAssertion[] Assertions() =>
    [
        PackagingAssertionBuilder.Define("nfr-1.core-dependencies", ["NFR-1"], RequirementLevel.Must, CoreDependencies),
        PackagingAssertionBuilder.Define("nfr-2.adapter-budget", ["NFR-2"], RequirementLevel.Should, AdapterBudget),
    ];

    // Exactly one dependency group, for the declared floor, listing exactly the facade.
    private static void CoreDependencies(PackagingContext context)
    {
        var core = context.Core;
        Check.Equal(core.DependencyGroups.Count, 1, $"the dependency groups of '{core.Id}' (one, for the declared floor)");
        var group = core.DependencyGroups[0];
        Check.Equal(group.TargetFramework, context.Subject.DeclaredFloor, $"the target framework of the dependency group of '{core.Id}'");
        Check.SequenceEqual(group.Dependencies.Select(d => d.Id), [Facade], $"the dependencies of '{core.Id}' (exactly the logging facade, design §10 entry 1)");
    }

    // Core, the first-party packages the policy names, the facade, and at most one third-party root; every first-party dependency at the lockstep version.
    private static void AdapterBudget(PackagingContext context)
    {
        foreach (var package in context.Adapters)
        {
            context.Subject.Policies.TryGetValue(package.Id, out var policy);
            var allowed = policy?.FirstPartyIds ?? [];
            foreach (var dependency in package.DependencyGroups.SelectMany(g => g.Dependencies))
            {
                if (dependency.Id.StartsWith(FirstParty, StringComparison.Ordinal))
                {
                    Check.True(
                        dependency.Id == context.Subject.CoreId || allowed.Contains(dependency.Id, StringComparer.Ordinal),
                        $"'{package.Id}' depends on the first-party package '{dependency.Id}', which its policy does not name",
                        $"core or one of: {string.Join(", ", allowed)}",
                        dependency.Id);
                    Check.Equal(dependency.Version, package.Version, $"the version of '{package.Id}''s dependency on '{dependency.Id}' (first-party packages are lock-stepped)");
                }
                else
                {
                    Check.True(
                        dependency.Id == Facade || string.Equals(dependency.Id, policy?.ThirdPartyRoot, StringComparison.Ordinal),
                        $"'{package.Id}' depends on '{dependency.Id}', a third-party root beyond the one its policy allows (at most one, design §10 entry 2)",
                        policy?.ThirdPartyRoot is null ? "the logging facade only" : $"the facade and '{policy.ThirdPartyRoot}'",
                        dependency.Id);
                }
            }
        }
    }
}

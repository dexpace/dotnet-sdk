// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// Plan 3.5 (P10-10; phase 9's hand-off "the packaging suite's allowed-dependency check reads the same policy record"): the policies table of
/// <c>scripts/ci/packaging-conformance.cs</c> equals the one in <c>scripts/ci/dependency-audit.cs</c>, library by library, third-party root by root and
/// first-party list by list, so the audit of the build output and the suite over the published packages can never drift apart.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class PackagingPolicyParityTests
{
    private const string Audit = "scripts/ci/dependency-audit.cs";
    private const string Packaging = "scripts/ci/packaging-conformance.cs";

    private static readonly string[] s_dependencyInjectionFirstParty = ["Dexpace.Sdk.Http.SystemNet"];

    [GeneratedRegex("""\[(?:"(?<id>[^"]+)"|(?<core>Core))\]\s*=\s*new\((?<root>null|"[^"]*"),\s*\[(?<first>[^\]]*)\]\)""", RegexOptions.CultureInvariant)]
    private static partial Regex PolicyLine();

    internal static Dictionary<string, (string? Root, string[] FirstParty)> Parse(string text) =>
        PolicyLine().Matches(text).ToDictionary(
            match => match.Groups["core"].Success ? "Dexpace.Sdk.Core" : match.Groups["id"].Value,
            match => (
                match.Groups["root"].Value == "null" ? null : match.Groups["root"].Value.Trim('"'),
                match.Groups["first"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static id => id.Trim('"')).ToArray()),
            StringComparer.Ordinal);

    private static string Describe(Dictionary<string, (string? Root, string[] FirstParty)> table) =>
        string.Join("; ", table.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => $"{pair.Key} -> {pair.Value.Root ?? "none"} [{string.Join(", ", pair.Value.FirstParty)}]"));

    [Fact]
    public void The_two_tables_name_the_same_five_libraries_with_the_same_roots_and_first_party_lists()
    {
        var audit = Parse(File.ReadAllText(RepositoryRoot.Of(Audit)));
        var packaging = Parse(File.ReadAllText(RepositoryRoot.Of(Packaging)));

        Assert.Equal(5, audit.Count);
        Assert.Equal(Describe(audit), Describe(packaging));
    }

    [Fact]
    public void Every_library_under_src_has_a_policy_in_both_tables()
    {
        var libraries = Directory.GetDirectories(RepositoryRoot.Of("src")).Select(static d => System.IO.Path.GetFileName(d)).Order(StringComparer.Ordinal);

        Assert.Equal(libraries, Parse(File.ReadAllText(RepositoryRoot.Of(Packaging))).Keys.Order(StringComparer.Ordinal));
        Assert.Equal(libraries, Parse(File.ReadAllText(RepositoryRoot.Of(Audit))).Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_parity_check_can_fail()
    {
        // Negative control: the audit text with one first-party entry removed must produce a mismatch.
        var audit = File.ReadAllText(RepositoryRoot.Of(Audit));
        var edited = audit.Replace("[\"Dexpace.Sdk.Http.SystemNet\"])", "[])", StringComparison.Ordinal);

        Assert.NotEqual(audit, edited);
        Assert.NotEqual(Describe(Parse(edited)), Describe(Parse(File.ReadAllText(RepositoryRoot.Of(Packaging)))));
    }

    [Fact]
    public void The_suite_policy_record_and_the_script_agree_on_the_dependency_injection_package()
    {
        var packaging = Parse(File.ReadAllText(RepositoryRoot.Of(Packaging)));

        var di = packaging["Dexpace.Sdk.Extensions.DependencyInjection"];
        Assert.Equal("Microsoft.Extensions.Http", di.Root);
        Assert.Equal(s_dependencyInjectionFirstParty, di.FirstParty);
        var core = packaging["Dexpace.Sdk.Core"];
        Assert.Null(core.Root);
        Assert.Empty(core.FirstParty);
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Housekeeping.Tests;

/// <summary>
/// Tests the CHECKS, not a copy of their logic, and not the live tree's cleanliness.
/// </summary>
/// <remarks>
/// Asserting only that each check returns nothing against the real repository passes just as happily
/// over a check whose body has become <c>[]</c> — and the real repository is clean most of the time, so
/// such a suite stays green while the checks rot. Every check therefore has a PAIR here: a fixture it
/// must be silent over, and a mutation of that fixture it must fire on.
/// </remarks>
public sealed class ProbeTests
{
    // --- helpers -------------------------------------------------------------------------------

    private static IReadOnlyList<Finding> OnFixture(
        string[]? only = null,
        Dictionary<string, string?>? overrides = null,
        Dictionary<string, string>? untracked = null)
    {
        using var fixture = Fixture.Create(overrides, untracked);
        return fixture.Probe(only ?? []);
    }

    private static void AssertFires(IReadOnlyList<Finding> findings, string pattern) =>
        Assert.True(
            findings.Any(f => Regex.IsMatch(f.Message, pattern, RegexOptions.Singleline)),
            $"expected a finding matching /{pattern}/, got: [{string.Join(" | ", findings.Select(f => f.Message))}]");

    // --- the clean tree ------------------------------------------------------------------------

    [Fact]
    public void A_clean_fixture_reports_nothing_on_every_check() => Assert.Empty(OnFixture());

    [Fact]
    public void Check_names_are_the_nine_the_documentation_states() =>
        Assert.Equal(
            ["inbox", "root", "claims", "readmes", "links", "registers", "citations", "guard", "chapters"],
            Probe.Names);

    // --- numerals ------------------------------------------------------------------------------

    [Theory]
    [InlineData("0", 0)]
    [InlineData("20", 20)]
    [InlineData("nine", 9)]
    [InlineData("Eleven", 11)]
    [InlineData("Twenty", 20)]
    [InlineData("twenty-four", 24)]
    [InlineData("ninety-nine", 99)]
    // The words that make a digits-only matcher protect one sentence per repository.
    [InlineData("published", null)]
    [InlineData("several", null)]
    [InlineData("twenty-zero", null)]
    public void Numeral_parse_reads_digits_words_and_compounds_and_rejects_prose(string token, int? expected) =>
        Assert.Equal(expected, Numeral.Parse(token));

    // --- inbox ---------------------------------------------------------------------------------

    [Fact]
    public void Inbox_catches_an_untracked_phase_document()
    {
        // The inbox's NORMAL state: a file a global skill just wrote and nobody staged.
        var found = OnFixture(["inbox"], untracked: new() { ["docs/superpowers/specs/2026-09-05-x-design.md"] = "# x\n" });

        AssertFires(found, "still in the inbox");
        Assert.Equal("docs/superpowers/specs/2026-09-05-x-design.md", found[0].Path);
    }

    [Fact]
    public void Inbox_catches_a_tracked_phase_document_too() =>
        AssertFires(OnFixture(["inbox"], new() { ["docs/superpowers/plans/2026-09-05-x.md"] = "# x\n" }), "still in the inbox");

    [Fact]
    public void Inbox_never_reports_the_readme_or_the_gitkeep() => Assert.Empty(OnFixture(["inbox"]));

    // --- root ----------------------------------------------------------------------------------

    [Fact]
    public void Root_catches_a_stray_register_at_the_repository_root() =>
        AssertFires(OnFixture(["root"], new() { ["deviations.md"] = "# deviations\n" }), "sits at the repository root");

    [Fact]
    public void Root_permits_the_changelog_and_the_community_health_files() =>
        Assert.Empty(OnFixture(["root"], new()
        {
            ["CONTRIBUTING.md"] = "# contributing\n",
            ["SECURITY.md"] = "# security\n",
            ["CHANGELOG.md"] = "# changelog\n",
            ["CODE_OF_CONDUCT.md"] = "# conduct\n",
            ["LICENSE.md"] = "# license\n",
        }));

    // --- claims --------------------------------------------------------------------------------

    [Fact]
    public void Claims_catches_a_count_stated_as_a_WORD() =>
        AssertFires(
            OnFixture(["claims"], new() { ["CLAUDE.md"] = "# CLAUDE.md\n\nSeven shipped projects live here.\n" }),
            "states \"Seven shipped projects\" but the repository has 2 shipped projects");

    [Fact]
    public void Claims_catches_a_count_stated_as_a_DIGIT() =>
        AssertFires(
            OnFixture(["claims"], new() { ["README.md"] = "# fixture\n\nThere are 9 NuGet packages.\n" }),
            "states \"9 NuGet packages\" but the repository has 2 shipped projects");

    [Fact]
    public void Claims_catches_a_stale_phase_and_topic_count()
    {
        var found = OnFixture(["claims"], new() { ["docs/README.md"] = "# docs\n\nFour phase directories, six harvested topics.\n" });

        AssertFires(found, "Four phase directories.*repository has 1 phase directories");
        AssertFires(found, "six harvested topics.*repository has 1 topics");
    }

    [Fact]
    public void Claims_topic_count_excludes_sources_md() =>
        AssertFires(
            OnFixture(["claims"], new()
            {
                ["docs/knowledge/harvested/SOURCES.md"] = "# sources\n\nNot a topic.\n",
                ["docs/knowledge/harvested/pagination.md"] = "# pagination\n\nHarvested.\n",
                ["docs/README.md"] = "# docs\n\nOne phase directory, five harvested topics.\n",
            }),
            "five harvested topics.*repository has 2 topics");

    [Fact]
    public void Claims_treats_a_quoted_historical_count_as_reported_speech() =>
        Assert.Empty(OnFixture(["claims"], new() { ["CLAUDE.md"] = "# CLAUDE.md\n\nThis once said \"seven projects\" and was wrong.\n" }));

    [Fact]
    public void Claims_ignores_a_count_inside_a_fenced_block() =>
        Assert.Empty(OnFixture(["claims"], new() { ["CLAUDE.md"] = "# CLAUDE.md\n\n```\nSeven projects, in a code sample.\n```\n" }));

    [Fact]
    public void Claims_no_ops_when_the_file_is_absent() =>
        Assert.Empty(OnFixture(["claims"], new() { ["CLAUDE.md"] = null }));

    [Fact]
    public void Claims_no_ops_when_the_pattern_is_absent() =>
        Assert.Empty(OnFixture(["claims"], new() { ["CLAUDE.md"] = "# CLAUDE.md\n\nNo counts at all.\n" }));

    [Fact]
    public void Claims_does_not_read_an_unqualified_adjective_as_a_count() =>
        // "test projects" is not a claim about shipped projects: the numeral must sit directly before
        // the noun or one of the shipped/published/library/NuGet qualifiers.
        Assert.Empty(OnFixture(["claims"], new() { ["CLAUDE.md"] = "# CLAUDE.md\n\nThree test projects and two shipped projects.\n" }));

    [Fact]
    public void Claims_counts_projects_from_the_tree_not_from_a_document() =>
        AssertFires(
            OnFixture(["claims"], new()
            {
                ["src/Dexpace.Sdk.Extensions.DependencyInjection/Dexpace.Sdk.Extensions.DependencyInjection.csproj"] =
                    Fixture.Csproj("Dexpace.Sdk.Extensions.DependencyInjection"),
                ["src/Dexpace.Sdk.Extensions.DependencyInjection/README.md"] =
                    Fixture.Readme("Dexpace.Sdk.Extensions.DependencyInjection"),
            }),
            "repository has 3 shipped projects");

    [Fact]
    public void Claims_does_not_count_a_project_that_opts_out_of_packing() =>
        Assert.Empty(OnFixture(["claims"], new()
        {
            ["src/Tools/Tools.csproj"] = Fixture.Csproj("Tools", packable: false),
        }));

    // --- readmes -------------------------------------------------------------------------------

    [Fact]
    public void Readmes_catches_a_project_with_no_readme()
    {
        var found = OnFixture(["readmes"], new() { ["src/Dexpace.Sdk.Core/README.md"] = null });

        AssertFires(found, @"is missing\. Every shipped project carries a NuGet package README \(Dexpace\.Sdk\.Core\)");
        Assert.Equal("src/Dexpace.Sdk.Core/README.md", found[0].Path);
    }

    [Fact]
    public void Readmes_reports_a_thin_readme_as_a_note()
    {
        var found = OnFixture(["readmes"], new() { ["src/Dexpace.Sdk.Core/README.md"] = "# Dexpace.Sdk.Core\n\nThin.\n" });

        AssertFires(found, @"is 3 lines\. The bar is 20");
        Assert.Equal(["note"], found.Select(f => f.Severity));
    }

    [Fact]
    public void Readmes_catches_a_readme_whose_heading_names_another_package() =>
        AssertFires(
            OnFixture(["readmes"], new() { ["src/Dexpace.Sdk.Core/README.md"] = Fixture.Readme("Dexpace.Sdk.Http.SystemNet") }),
            "opens with \"Dexpace.Sdk.Http.SystemNet\" but its .csproj declares Dexpace.Sdk.Core");

    [Fact]
    public void Readmes_falls_back_to_the_project_file_name_when_no_package_id_is_set() =>
        AssertFires(
            OnFixture(["readmes"], new()
            {
                ["src/Dexpace.Sdk.Core/Dexpace.Sdk.Core.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n" +
                    "    <PackageReadmeFile>README.md</PackageReadmeFile>\n  </PropertyGroup>\n</Project>\n",
                ["src/Dexpace.Sdk.Core/README.md"] = Fixture.Readme("Something.Else"),
            }),
            "but its .csproj declares Dexpace.Sdk.Core");

    [Fact]
    public void Readmes_catches_a_project_directory_with_no_csproj() =>
        AssertFires(
            OnFixture(["readmes"], new() { ["src/Dexpace.Sdk.Core/Dexpace.Sdk.Core.csproj"] = null }),
            @"has no \.csproj");

    [Fact]
    public void Readmes_notes_a_project_that_never_packs_its_readme()
    {
        var found = OnFixture(["readmes"], new()
        {
            ["src/Dexpace.Sdk.Core/Dexpace.Sdk.Core.csproj"] = Fixture.Csproj("Dexpace.Sdk.Core", readme: false),
        });

        AssertFires(found, "sets no <PackageReadmeFile>");
        Assert.Equal(["src/Dexpace.Sdk.Core/Dexpace.Sdk.Core.csproj"], found.Select(f => f.Path));
        Assert.Equal(["note"], found.Select(f => f.Severity));
    }

    [Fact]
    public void Readmes_skips_a_project_that_opts_out_of_packing() =>
        Assert.Empty(OnFixture(["readmes"], new() { ["src/Tools/Tools.csproj"] = Fixture.Csproj("Tools", packable: false) }));

    [Fact]
    public void Readmes_no_ops_when_there_is_no_src_directory()
    {
        using var fixture = Fixture.Create();
        Fixture.Delete(Path.Combine(fixture.Root, "src"));

        Assert.Empty(fixture.Probe("readmes"));
    }

    // --- links ---------------------------------------------------------------------------------

    [Fact]
    public void Links_catches_a_broken_link_in_a_nested_docs_file()
    {
        var found = OnFixture(["links"], new() { ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# a\n\n[gone](./nowhere.md)\n" });

        AssertFires(found, @"links \./nowhere\.md, which resolves to docs/work/mvp/phase1/nowhere\.md");
        Assert.Equal(3, found[0].Line);
    }

    [Fact]
    public void Links_catches_a_broken_link_at_the_top_of_docs() =>
        AssertFires(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n[gone](missing.md)\n" }), @"resolves to docs/missing\.md");

    [Fact]
    public void Links_catches_a_broken_link_in_a_package_readme() =>
        AssertFires(
            OnFixture(["links"], new() { ["src/Dexpace.Sdk.Core/README.md"] = $"{Fixture.Readme("Dexpace.Sdk.Core")}\n[gone](./CHANGELOG.md)\n" }),
            @"src/Dexpace\.Sdk\.Core/CHANGELOG\.md");

    [Fact]
    public void Links_does_not_treat_a_fenced_line_as_a_link() =>
        Assert.Empty(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n```md\n[gone](nowhere.md)\n```\n" }));

    [Fact]
    public void Links_skips_external_anchor_and_mailto_targets() =>
        Assert.Empty(OnFixture(["links"], new()
        {
            ["docs/README.md"] = "# docs\n\n[a](https://example.invalid/x) [b](#section) [c](mailto:x@example.invalid)\n",
        }));

    [Fact]
    public void Links_resolves_a_percent_encoded_target() =>
        Assert.Empty(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n[x](architecture%2Emd)\n" }));

    [Fact]
    public void Links_ignores_a_link_inside_an_inline_code_span()
    {
        var found = OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\nSee `[gone](nowhere.md)` for the old syntax.\n\n[real](also-gone.md)\n" });

        Assert.Single(found);
        AssertFires(found, @"also-gone\.md");
    }

    [Fact]
    public void Links_ignores_a_link_inside_an_indented_code_block()
    {
        var found = OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n    [gone](nowhere.md)\n\n[real](also-gone.md)\n" });

        Assert.Single(found);
        AssertFires(found, @"also-gone\.md");
    }

    [Fact]
    public void Links_catches_a_broken_reference_style_definition() =>
        AssertFires(
            OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\nSee [the guide][guide].\n\n[guide]: nowhere.md\n" }),
            @"references \[guide\], which resolves to docs/nowhere\.md");

    [Fact]
    public void Links_catches_an_undefined_reference() =>
        AssertFires(
            OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\nSee [the guide][missing].\n" }),
            @"references \[missing\], which has no \[missing\]: definition");

    [Fact]
    public void Links_resolves_a_reference_style_link_that_is_defined_and_valid() =>
        Assert.Empty(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\nSee [the inbox][ref].\n\n[ref]: superpowers/README.md\n" }));

    [Fact]
    public void Links_ignores_a_reference_style_link_inside_code() =>
        Assert.Empty(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n`[the guide][missing]`\n" }));

    // A normative chapter named in backticked prose is a claim about the tree, and Markdown link
    // syntax is not how this repository writes one.

    [Fact]
    public void Links_catches_a_backticked_normative_chapter_path_that_does_not_exist()
    {
        var found = OnFixture(["links"], new()
        {
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] =
                "# phase 1\n\nThe reasoning is in `docs/sdk-design-dotnet/03-seam-mapping.md` §3.7.\n",
        });

        AssertFires(found, @"names `docs/sdk-design-dotnet/03-seam-mapping\.md`, a path that does not exist");
        Assert.Equal(3, found[0].Line);
    }

    [Fact]
    public void Links_is_quiet_over_a_backticked_normative_chapter_path_that_exists() =>
        Assert.Empty(OnFixture(["links"], new()
        {
            ["docs/product-spec/04-core-http-domain-model.md"] = "# four\n",
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\nStated in `docs/product-spec/04-core-http-domain-model.md`.\n",
        }));

    [Fact]
    public void Links_ignores_a_backticked_path_that_is_a_placeholder_or_an_elision() =>
        Assert.Empty(OnFixture(["links"], new()
        {
            ["docs/README.md"] = "# docs\n\nEvery `docs/product-spec/NN-*.md` and `docs/sdk-design-dotnet/11-…-ambiguities-….md`.\n",
        }));

    [Fact]
    public void Links_does_not_claim_a_path_outside_the_two_normative_trees() =>
        // A `src/…` path in a plan names something a later phase creates; a backticked one is a plan,
        // not a claim about what is on disk.
        Assert.Empty(OnFixture(["links"], new()
        {
            ["docs/README.md"] = "# docs\n\nThe spine is `Dexpace.Sdk.sln` and `src/Dexpace.Sdk.Sse/SseReader.cs`.\n",
        }));

    [Fact]
    public void Links_ignores_a_backticked_chapter_path_inside_a_fence() =>
        Assert.Empty(OnFixture(["links"], new() { ["docs/README.md"] = "# docs\n\n```\ngrep -n x `docs/product-spec/99-nonexistent.md`\n```\n" }));

    // --- registers -----------------------------------------------------------------------------

    [Fact]
    public void Registers_catches_an_aggregate_register_in_a_phase_document()
    {
        var found = OnFixture(["registers"], new()
        {
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\nBody.\n\n## Deferred Items Log\n\n| ID | Item |\n",
        });

        AssertFires(found, "carries \"## Deferred Items Log\"");
        AssertFires(found, "No find-list register exists: an aggregate belongs with its owner");
        Assert.Equal(5, found[0].Line);
    }

    [Fact]
    public void Registers_catches_open_findings_in_an_inbox_document() =>
        AssertFires(
            OnFixture(["registers"], untracked: new() { ["docs/superpowers/specs/x-design.md"] = "# x\n\n## Open Findings\n" }),
            "carries \"## Open Findings\"");

    [Fact]
    public void Registers_does_not_report_a_moved_out_pointer_stub() =>
        Assert.Empty(OnFixture(["registers"], new()
        {
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\n## Open Items\n\n**Moved out on 2026-09-05** to docs/open-items.md.\n",
        }));

    // --- citations -----------------------------------------------------------------------------

    // This repository has never had a register, so both tables are empty and the check is silent.
    // Its mechanism is not dead code — a register is one entry away — so it is driven through the
    // constructor seam over a prefix and a register file each fixture invents: OLD for a retired
    // register, FIX for a live one.

    private static readonly Dictionary<string, RetiredRegister> s_retired = new()
    {
        ["OLD"] = new RetiredRegister("the fixture's old register, retired 2026-01-01: cite the owning plan task instead", "docs/old-items.md"),
    };

    private static readonly Dictionary<string, string> s_live = new() { ["FIX"] = "docs/fixture-register.md" };

    private const string RetiredOld = @"cites OLD-99, an ID from the fixture's old register, retired 2026-01-01: cite the owning plan task instead\.";

    private static IReadOnlyList<Finding> CitationsOn(
        Dictionary<string, string?> overrides,
        Dictionary<string, string>? registers = null,
        Dictionary<string, RetiredRegister>? retired = null)
    {
        using var fixture = Fixture.Create(overrides);
        return new Citations(registers ?? s_live, retired ?? s_retired).Run(new Repo(fixture.Root));
    }

    [Fact]
    public void Citations_has_no_register_by_default_and_is_silent() =>
        Assert.Empty(OnFixture(["citations"], new() { ["docs/README.md"] = "# docs\n\nOLD-1, FIX-2, DEF-3 and OI-4.\n" }));

    [Fact]
    public void Citations_default_tables_are_empty()
    {
        Assert.Empty(Citations.DefaultRegisters);
        Assert.Empty(Citations.DefaultRetired);
    }

    [Fact]
    public void Citations_is_quiet_over_a_tree_that_cites_no_retired_id() => Assert.Empty(CitationsOn([]));

    [Fact]
    public void Citations_does_not_claim_a_requirement_id_is_a_register_item() =>
        Assert.Empty(CitationsOn(new() { ["docs/README.md"] = "# docs\n\nHTTP-7, SEAM-1, RETRY-13 and NFR-5 are requirements.\n" }));

    [Fact]
    public void Citations_reports_a_retired_citation_with_no_register_file()
    {
        var found = CitationsOn(new() { ["docs/README.md"] = "# docs\n\nSee OLD-99 for the finding.\n" });

        AssertFires(found, RetiredOld);
        Assert.Equal(["docs/README.md"], found.Select(f => f.Path));
        Assert.Equal([3], found.Select(f => f.Line));
        Assert.Equal(["act"], found.Select(f => f.Severity));
    }

    [Fact]
    public void Citations_reports_a_retired_citation_in_a_phase_document_and_in_source()
    {
        var found = CitationsOn(new()
        {
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\nFound as OLD-99.\n",
            ["src/Dexpace.Sdk.Core/Thing.cs"] = "// See OLD-99.\nnamespace Dexpace.Sdk;\n",
        });

        Assert.Equal(
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md", "src/Dexpace.Sdk.Core/Thing.cs"],
            found.Select(f => f.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Citations_reports_a_backticked_retired_citation_in_prose()
    {
        var found = CitationsOn(new() { ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\nFound as `OLD-99`, and `OLD-98` with it.\n" });

        AssertFires(found, RetiredOld);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Citations_ignores_a_retired_citation_inside_a_fenced_block() =>
        Assert.Empty(CitationsOn(new() { ["docs/README.md"] = "# docs\n\n```\nSee OLD-99 and `OLD-98` here.\n```\n" }));

    [Fact]
    public void Citations_reports_a_retired_citation_on_an_indented_continuation_line()
    {
        var found = CitationsOn(new()
        {
            ["docs/work/mvp/phase1/2026-01-01-phase1-thing.md"] = "# phase 1\n\n1. **A task.** Its first line runs on, and\n     the continuation cites `OLD-99`.\n",
        });

        AssertFires(found, RetiredOld);
        Assert.Single(found);
        Assert.Equal(4, found[0].Line);
    }

    [Fact]
    public void Citations_reports_a_lingering_retired_register_file_once_not_per_row()
    {
        var found = CitationsOn(new() { ["docs/old-items.md"] = "# Old items\n\nOLD-1 was routed. OLD-2 was fixed.\n\n### OLD-3\n" });

        var finding = Assert.Single(found);
        Assert.Equal("docs/old-items.md", finding.Path);
        Assert.Equal(1, finding.Line);
        Assert.Matches("still exists, but it was the fixture's old register, retired 2026-01-01", finding.Message);
        Assert.Matches(@"Delete it once no OLD-<n> citation remains\.", finding.Message);
    }

    [Fact]
    public void Citations_ignores_the_skills_own_tests() =>
        Assert.Empty(CitationsOn(new() { [".claude/skills/housekeeping/tests/ProbeTests.cs"] = "// OLD-99 is a fixture literal.\n" }));

    [Fact]
    public void Citations_resolves_a_live_prefix_from_a_heading_and_from_a_table_row() =>
        Assert.Empty(CitationsOn(new()
        {
            ["docs/fixture-register.md"] = "# fixture register\n\n### FIX-1 — a real item\n\nBody.\n\n| ID | State |\n|---|---|\n| `FIX-2` | open |\n",
            ["docs/README.md"] = "# docs\n\nFIX-1 and FIX-2 both resolve.\n",
        }));

    [Fact]
    public void Citations_catches_a_dangling_live_id_outside_a_fence()
    {
        var found = CitationsOn(new()
        {
            ["docs/fixture-register.md"] = "# fixture register\n\n### FIX-1 — a real item\n",
            ["docs/README.md"] = "# docs\n\nSee FIX-9 in prose.\n",
        });

        AssertFires(found, @"cites FIX-9, which has no entry in docs/fixture-register\.md");
        Assert.Single(found);
    }

    [Fact]
    public void Citations_ignores_a_dangling_live_id_inside_a_fenced_block() =>
        Assert.Empty(CitationsOn(new()
        {
            ["docs/fixture-register.md"] = "# fixture register\n\n### FIX-1 — a real item\n",
            ["docs/README.md"] = "# docs\n\n```\nSee FIX-9 in this example.\n```\n",
        }));

    [Fact]
    public void Citations_reads_a_backticked_live_id_and_resolves_it_against_the_register()
    {
        var found = CitationsOn(new()
        {
            ["docs/fixture-register.md"] = "# fixture register\n\n### FIX-1 — a real item\n",
            ["docs/README.md"] = "# docs\n\n`FIX-1` resolves; `FIX-9` does not.\n",
        });

        AssertFires(found, @"cites FIX-9, which has no entry in docs/fixture-register\.md");
        Assert.Single(found);
    }

    [Fact]
    public void Citations_a_live_definition_outside_the_register_resolves_nothing()
    {
        // The file, not just the pattern, has to match.
        var found = CitationsOn(new()
        {
            ["docs/fixture-register.md"] = "# fixture register\n\nNo rows yet.\n",
            ["docs/architecture.md"] = "# notes\n\n### FIX-5 — wrong file\n",
            ["docs/README.md"] = "# docs\n\nSee FIX-5.\n",
        });

        AssertFires(found, @"cites FIX-5, which has no entry in docs/fixture-register\.md");
        Assert.Contains("docs/README.md", found.Select(f => f.Path));
    }

    [Fact]
    public void Citations_no_ops_for_a_live_prefix_whose_register_does_not_exist() =>
        Assert.Empty(CitationsOn(new() { ["docs/README.md"] = "# docs\n\nFIX-9 cited with no register at all.\n" }));

    // --- guard ---------------------------------------------------------------------------------

    [Fact]
    public void Guard_is_quiet_when_the_two_lists_do_not_overlap() => Assert.Empty(OnFixture(["guard"]));

    [Fact]
    public void Guard_writable_surface_and_must_refuse_list_are_disjoint_from_each_other()
    {
        Assert.Contains("docs/work", GuardCheck.WritableSurface);
        Assert.Empty(GuardCheck.WritableSurface.Intersect(GuardCheck.MustRefuse));
    }

    [Fact]
    public void Guard_catches_a_frozen_entry_that_became_a_symlink()
    {
        using var fixture = Fixture.Create();
        Fixture.Delete(Path.Combine(fixture.Root, "docs/knowledge"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "docs/elsewhere"));
        Fixture.Symlink(fixture.Root, "docs/elsewhere", "docs/knowledge");

        AssertFires(fixture.Probe("guard"), "is a symlink");
    }

    // --- the read-only contract ----------------------------------------------------------------

    [Fact]
    public void The_probe_writes_nothing()
    {
        using var fixture = Fixture.Create(
            new() { ["docs/README.md"] = "# docs\n\n[gone](missing.md) and nine phase directories.\n" },
            new() { ["docs/superpowers/specs/2026-09-05-x-design.md"] = "# x\n" });
        var before = fixture.Status();

        Assert.NotEmpty(fixture.Probe());
        Assert.Equal(0, ProbeCli.Run(["--root", fixture.Root, "--warn-only"], TextWriter.Null, TextWriter.Null));

        Assert.Equal(before, fixture.Status());
    }

    // --- selection -----------------------------------------------------------------------------

    [Fact]
    public void Only_selects_a_subset_and_an_unknown_check_is_refused()
    {
        using var fixture = Fixture.Create();
        var repo = new Repo(fixture.Root);

        Assert.Equal(["links"], new Probe(repo, ["links"]).Checks().Select(c => c.Name));
        Assert.Equal(Probe.Names, new Probe(repo).Checks().Select(c => c.Name));
        var error = Assert.Throws<ArgumentException>(() => new Probe(repo, ["links", "nope"]));
        Assert.Matches(@"unknown check\(s\): nope", error.Message);
    }

    // --- the command line ----------------------------------------------------------------------

    private static (int Status, string Out, string Err) RunCli(string root, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var status = Program.Run(["probe", "--root", root, .. args], output, error);
        return (status, output.ToString(), error.ToString());
    }

    [Fact]
    public void Cli_exits_zero_and_says_so_on_a_clean_tree()
    {
        using var fixture = Fixture.Create();
        var (status, output, error) = RunCli(fixture.Root);

        Assert.Equal(string.Empty, error);
        Assert.Contains("no drift found.", output, StringComparison.Ordinal);
        Assert.Equal(0, status);
    }

    [Fact]
    public void Cli_exits_one_on_a_finding_and_zero_under_warn_only()
    {
        using var fixture = Fixture.Create(untracked: new() { ["docs/superpowers/specs/2026-09-05-x-design.md"] = "# x\n" });
        var (status, output, _) = RunCli(fixture.Root);

        Assert.Equal(1, status);
        Assert.Contains("## inbox (1)", output, StringComparison.Ordinal);
        Assert.Contains("docs/superpowers/specs/2026-09-05-x-design.md:1: [act]", output, StringComparison.Ordinal);
        Assert.Contains("1 finding(s) across 1 check(s).", output, StringComparison.Ordinal);

        Assert.Equal(0, RunCli(fixture.Root, "--warn-only").Status);
    }

    [Fact]
    public void Cli_emits_json()
    {
        using var fixture = Fixture.Create(untracked: new() { ["docs/superpowers/plans/2026-09-05-x.md"] = "# x\n" });
        var (status, output, _) = RunCli(fixture.Root, "--json", "--only", "inbox");
        using var payload = JsonDocument.Parse(output);
        var json = payload.RootElement;

        Assert.Equal(1, status);
        Assert.Equal(["inbox"], json.GetProperty("checks").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("docs/superpowers/plans/2026-09-05-x.md", json.GetProperty("findings")[0].GetProperty("path").GetString());
        Assert.Equal(1, json.GetProperty("summary").GetProperty("findings").GetInt32());
    }

    [Fact]
    public void Cli_accepts_the_equals_form_of_an_option()
    {
        using var fixture = Fixture.Create(untracked: new() { ["docs/superpowers/plans/2026-09-05-x.md"] = "# x\n" });

        Assert.Contains("## inbox (1)", RunCli(fixture.Root, "--only=inbox,links").Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_refuses_an_unknown_check_by_name()
    {
        using var fixture = Fixture.Create();
        var (status, _, error) = RunCli(fixture.Root, "--only", "nope");

        Assert.Equal(2, status);
        Assert.Contains("unknown check", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_refuses_an_unknown_option_and_an_unknown_command()
    {
        using var fixture = Fixture.Create();

        Assert.Equal(2, RunCli(fixture.Root, "--nope").Status);
        Assert.Equal(2, Program.Run(["tidy"], TextWriter.Null, TextWriter.Null));
        Assert.Equal(2, Program.Run([], TextWriter.Null, TextWriter.Null));
    }
}

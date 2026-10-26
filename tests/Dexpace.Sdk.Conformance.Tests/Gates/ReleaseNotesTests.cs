// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// P12-16 and P12-19 (phase 12, task 4.8; roadmap card, "release notes state every item of <i>What v1 ships without</i>"): the draft release notes at the head of
/// <c>CHANGELOG.md</c>'s <c>[Unreleased]</c> section (the block headed <c>### Release notes (draft for 0.1.0)</c>, which becomes <c>## [0.1.0]</c> at the held release
/// commit) name every item the register <c>docs/first-release.md</c> lists under <i>What v1 ships without</i>. The register's own sentence is "the release notes MUST state
/// every item in this section"; this test is what keeps a later phase's new entry from being forgotten. The extraction rule (plan reading R13): every top-level bullet
/// under the four subsections <i>Unsatisfied MUSTs</i>, <i>SHOULD- and MAY-level requirements declined for v1</i>, <i>Behavioural asymmetries a consumer must know</i> and
/// <i>Post-v1 packages</i> is an item unless it is struck through (<c>~~</c>, kept for the record); its key is the leading requirement ID (<c>AUTH-15</c>), else the leading
/// kebab-case name (<c>async-redirect-pillar</c>), else the leading package name (<c>Dexpace.Sdk.Reactive</c>), else the first bold phrase up to its first parenthesis, comma or
/// full stop. A key is stated when it appears in a bullet line of the draft block.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class ReleaseNotesTests
{
    private const string BlockHeading = "### Release notes (draft for 0.1.0)";

    private static readonly string[] s_sections =
    [
        "### Unsatisfied MUSTs",
        "### SHOULD- and MAY-level requirements declined for v1",
        "### Behavioural asymmetries a consumer must know",
        "### Post-v1 packages",
    ];

    [GeneratedRegex(@"^\*\*(?<bold>.+?)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingBold();

    [GeneratedRegex(@"^(?<key>[A-Z]+-[0-9]+|[a-z][a-z0-9]*(?:-[a-z0-9]+)+|Dexpace\.Sdk(?:\.[A-Za-z]+)+)(?![A-Za-z0-9-])", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingToken();

    private static string Register => File.ReadAllText(RepositoryRoot.Of("docs/first-release.md")).ReplaceLineEndings("\n");

    private static string Changelog => File.ReadAllText(RepositoryRoot.Of("CHANGELOG.md")).ReplaceLineEndings("\n");

    [Fact]
    public void The_draft_block_names_every_item_v1_ships_without()
    {
        var block = DraftBlock(Changelog);

        Assert.NotNull(block);
        Assert.Empty(MissingKeys(Register, block));
    }

    [Fact]
    public void The_scan_is_not_vacuous()
    {
        var keys = ItemKeys(Register);

        Assert.True(keys.Count >= 25, $"{keys.Count} keys extracted: {string.Join(", ", keys)}");
        foreach (var expected in new[] { "AUTH-15", "CFG-2", "HTTP-22", "async-redirect-pillar", "Dexpace.Sdk.Reactive" })
        {
            Assert.Contains(expected, keys);
        }

        // A struck-through bullet is history, not an item.
        Assert.DoesNotContain("XCUT-24", keys);
        Assert.DoesNotContain("TRANSPORT-2", keys);
    }

    [Fact]
    public void The_block_leads_with_the_breaking_changes()
    {
        var block = DraftBlock(Changelog);

        Assert.NotNull(block);
        var firstList = string.Join('\n', FirstList(block));
        Assert.Contains("catch (SdkException)", firstList, StringComparison.Ordinal);
        Assert.Contains("ServiceRequestException", firstList, StringComparison.Ordinal);

        // The list sits under the breaking-changes heading, which comes before the list of what v1 ships without.
        var heading = block.IndexOf("#### Breaking changes", StringComparison.Ordinal);
        Assert.InRange(heading, 0, block.IndexOf("\n- ", StringComparison.Ordinal));
        Assert.True(heading < block.IndexOf("#### What v1 ships without", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scan_can_fail()
    {
        const string Register = """
            ## What v1 ships without

            ### Unsatisfied MUSTs

            - **`AUTH-15` (MUST), Digest MD5.** Reason.
            - ~~**`OLD-1` (MUST), struck.**~~ *Repaired.*

            ### SHOULD- and MAY-level requirements declined for v1

            - **A thing we decline (phase 1).** Reason.

            ### Behavioural asymmetries a consumer must know

            - **`two-meters` (design §8.1).** Enable one.

            ### Post-v1 packages

            - **`Dexpace.Sdk.Reactive`** would bridge.

            ## Release path
            """;
        const string Notes = """
            ## [Unreleased]

            ### Release notes (draft for 0.1.0)

            - **Breaking:** `catch (SdkException)` no longer catches a `ServiceRequestException`.
            - `AUTH-15` Digest MD5.
            - A thing we decline.
            - `two-meters` enable one.
            - `Dexpace.Sdk.Reactive` later.

            ### Security
            """;

        Assert.Equal(["AUTH-15", "A thing we decline", "two-meters", "Dexpace.Sdk.Reactive"], ItemKeys(Register));
        Assert.Empty(MissingKeys(Register, DraftBlock(Notes)!));

        foreach (var key in ItemKeys(Register))
        {
            var without = string.Join('\n', Notes.Split('\n').Where(line => !line.Contains(key, StringComparison.Ordinal)));
            Assert.Equal([key], MissingKeys(Register, DraftBlock(without)!));
        }

        Assert.Null(DraftBlock("## [Unreleased]\n\n### Security\n"));
    }

    private static string? DraftBlock(string changelog)
    {
        var lines = changelog.Split('\n');
        var start = Array.FindIndex(lines, static line => line == BlockHeading);
        if (start < 0)
        {
            return null;
        }

        var end = Array.FindIndex(lines, start + 1, static line => line.StartsWith("### ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal));
        return string.Join('\n', lines[start..(end < 0 ? lines.Length : end)]);
    }

    private static List<string> FirstList(string block)
    {
        var lines = block.Split('\n');
        var first = Array.FindIndex(lines, static line => line.StartsWith("- ", StringComparison.Ordinal));
        var list = new List<string>();
        for (var i = first; i >= 0 && i < lines.Length && !lines[i].StartsWith('#') && (lines[i].Length == 0 || lines[i][0] is '-' or ' '); i++)
        {
            list.Add(lines[i]);
        }

        return list;
    }

    private static List<string> MissingKeys(string register, string block)
    {
        var bullets = block.Split('\n').Where(static line => line.StartsWith("- ", StringComparison.Ordinal)).ToList();
        return [.. ItemKeys(register).Where(key => !bullets.Any(bullet => bullet.Contains(key, StringComparison.Ordinal)))];
    }

    private static List<string> ItemKeys(string register)
    {
        var keys = new List<string>();
        var lines = register.Split('\n');
        var inside = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("### ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = s_sections.Contains(line.TrimEnd());
                continue;
            }

            if (inside && line.StartsWith("- ", StringComparison.Ordinal) && !line.StartsWith("- ~~", StringComparison.Ordinal))
            {
                var key = KeyOf(line[2..]);
                if (key is not null)
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    private static string? KeyOf(string bullet)
    {
        var bold = LeadingBold().Match(bullet);
        if (!bold.Success)
        {
            return null;
        }

        var phrase = bold.Groups["bold"].Value.Replace("`", string.Empty, StringComparison.Ordinal).Trim();
        var token = LeadingToken().Match(phrase);
        if (token.Success)
        {
            return token.Groups["key"].Value;
        }

        var stop = phrase.IndexOfAny(['(', ',']);
        var cut = (stop < 0 ? phrase : phrase[..stop]).Trim().TrimEnd('.');
        return cut.Length == 0 ? null : cut;
    }
}

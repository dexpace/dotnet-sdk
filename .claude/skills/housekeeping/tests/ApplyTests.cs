// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Housekeeping.Tests;

/// <summary>
/// The apply stage is the only one that writes, so its refusals matter more than its moves. Every
/// refusal here is a batch refusal: the tree is untouched, not half-collected.
/// </summary>
/// <remarks>
/// <see cref="The_guard_call_is_load_bearing"/> exists because deleting the equivalent call in the Node
/// original once left that whole suite green.
/// </remarks>
public sealed class ApplyTests
{
    // --- helpers -------------------------------------------------------------------------------

    /// <summary>A fixture whose inbox holds <paramref name="files"/>, committed unless <paramref name="staged"/> is false.</summary>
    private static Fixture WithInbox(Dictionary<string, string> files, bool staged = true) =>
        staged
            ? Fixture.Create(files.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            : Fixture.Create(untracked: files);

    private static bool Exists(Fixture fixture, string path) => File.Exists(Path.Combine(fixture.Root, path));

    private static (int Status, string Out, string Err) RunCli(string root, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var status = Program.Run(["apply", "--root", root, .. args], output, error);
        return (status, output.ToString(), error.ToString());
    }

    // --- where a document belongs --------------------------------------------------------------

    [Fact]
    public void A_sub_phase_document_nests_under_its_phase()
    {
        using var fixture = Fixture.Create();

        Assert.Equal(
            "docs/work/mvp/phase5/phase5a",
            new Apply(fixture.Root).TargetDirectory("docs/superpowers/specs/2026-09-05-phase5a-transport-design.md"));
    }

    [Fact]
    public void A_whole_phase_document_sits_at_the_phase_level()
    {
        using var fixture = Fixture.Create();
        var apply = new Apply(fixture.Root);

        Assert.Equal("docs/work/mvp/phase6", apply.TargetDirectory("docs/superpowers/specs/2026-09-05-phase6-segmentation-design.md"));
        Assert.Equal("docs/work/mvp/phase6", apply.TargetDirectory("docs/superpowers/plans/2026-09-05-phase6.md"));
    }

    [Fact]
    public void Phase10_is_not_read_as_phase1()
    {
        using var fixture = Fixture.Create();

        Assert.Equal("docs/work/mvp/phase10", new Apply(fixture.Root).TargetDirectory("docs/superpowers/plans/2026-09-05-phase10-release.md"));
    }

    [Fact]
    public void A_document_belonging_to_no_phase_sits_directly_under_the_delivery()
    {
        using var fixture = Fixture.Create();

        // The shape of the retired 2026-06 slice designs: dated, no phase in the name.
        Assert.Equal("docs/work/mvp", new Apply(fixture.Root).TargetDirectory("docs/superpowers/specs/2026-06-14-auth-slice-design.md"));
    }

    [Fact]
    public void The_phase_flag_overrides_the_filename()
    {
        using var fixture = Fixture.Create();

        Assert.Equal("docs/work/mvp/phase5/phase5a", new Apply(fixture.Root, phase: "5a").TargetDirectory("docs/superpowers/specs/2026-09-05-anything.md"));
        Assert.Equal("docs/work/mvp/phase7", new Apply(fixture.Root, phase: "7").TargetDirectory("docs/superpowers/specs/2026-09-05-phase2-x.md"));
    }

    [Fact]
    public void The_delivery_is_a_parameter_so_a_later_effort_is_a_sibling_of_mvp()
    {
        using var fixture = Fixture.Create();

        Assert.Equal("docs/work/v2/phase1", new Apply(fixture.Root, delivery: "v2").TargetDirectory("docs/superpowers/plans/2026-09-05-phase1-x.md"));
    }

    [Fact]
    public void A_malformed_phase_is_refused_at_construction()
    {
        using var fixture = Fixture.Create();

        var error = Assert.Throws<ArgumentException>(() => new Apply(fixture.Root, phase: "five-a"));
        Assert.Contains("--phase must look like 5 or 5a", error.Message, StringComparison.Ordinal);
    }

    // --- the plan ------------------------------------------------------------------------------

    [Fact]
    public void The_plan_keeps_the_filename_date_prefix_included()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase5a-x-design.md"] = "# x\n" });
        var move = Assert.Single(new Apply(fixture.Root).Plan());

        Assert.Equal("docs/superpowers/specs/2026-09-05-phase5a-x-design.md", move.From);
        Assert.Equal("docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md", move.To);
    }

    // A filename beginning with `-` must not be read as a flag by `git mv`; `--` is what stops that.
    [Fact]
    public void Move_command_separates_paths_from_flags_with_a_double_dash() =>
        Assert.Equal(
            "git mv -- docs/superpowers/specs/x.md docs/work/mvp/x.md",
            new Move("docs/superpowers/specs/x.md", "docs/work/mvp/x.md").Command);

    [Fact]
    public void The_plan_sees_an_untracked_inbox_file()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/plans/2026-09-05-phase2-x.md"] = "# x\n" }, staged: false);

        Assert.Single(new Apply(fixture.Root).Plan());
    }

    [Fact]
    public void The_plan_never_collects_the_readme_or_the_gitkeep()
    {
        using var fixture = Fixture.Create();

        Assert.Empty(new Apply(fixture.Root).Plan());
    }

    [Fact]
    public void Rename_maps_a_basename_as_it_moves()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-x-design.md"] = "# x\n" });
        var renames = new Dictionary<string, string> { ["2026-09-05-x-design.md"] = "2026-09-05-phase5a-x-design.md" };

        Assert.Equal(
            "docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md",
            new Apply(fixture.Root, phase: "5a", renames: renames).Plan()[0].To);
    }

    // --- batch refusals ------------------------------------------------------------------------

    [Fact]
    public void Refuses_the_batch_when_a_target_already_exists()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-01-01-phase1-thing.md"] = "# again\n" });
        var apply = new Apply(fixture.Root);

        var refusal = Assert.Single(apply.Refusals(apply.Plan()));
        Assert.Matches(@"docs/work/mvp/phase1/2026-01-01-phase1-thing\.md already exists", refusal);
    }

    [Fact]
    public void Refuses_the_batch_when_two_inbox_files_collide_on_one_target()
    {
        using var fixture = WithInbox(new()
        {
            ["docs/superpowers/specs/2026-09-05-phase5-x.md"] = "# design\n",
            ["docs/superpowers/plans/2026-09-05-phase5-x.md"] = "# plan\n",
        });
        var apply = new Apply(fixture.Root);

        Assert.Contains(apply.Refusals(apply.Plan()), r => r.Contains("both land on", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../product-spec", "docs/product-spec")]
    [InlineData("../styleguide", "docs/styleguide")]
    [InlineData("../sdk-design-dotnet", "docs/sdk-design-dotnet")]
    public void Refuses_the_batch_when_the_delivery_escapes_into_a_frozen_tree(string delivery, string entry)
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase1-x.md"] = "# x\n" });
        var apply = new Apply(fixture.Root, delivery: delivery);

        Assert.Contains(apply.Refusals(apply.Plan()), r => r.Contains($"frozen entry '{entry}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Refuses_an_untracked_inbox_file_with_the_git_add_to_run()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/plans/2026-09-05-phase2-x.md"] = "# x\n" }, staged: false);
        var apply = new Apply(fixture.Root);
        var refusals = apply.Refusals(apply.Plan());

        Assert.Contains(refusals, r => r.Contains("is not tracked; `git mv` cannot move it", StringComparison.Ordinal));
        Assert.Contains(refusals, r => r.Contains("git add docs/superpowers/plans/2026-09-05-phase2-x.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Collects_every_refusal_not_just_the_first()
    {
        using var fixture = Fixture.Create(
            new()
            {
                ["docs/superpowers/specs/2026-01-01-phase1-thing.md"] = "# collides with the archive\n",
                ["docs/superpowers/specs/2026-09-05-phase5-x.md"] = "# design\n",
                ["docs/superpowers/plans/2026-09-05-phase5-x.md"] = "# plan\n",
            },
            new() { ["docs/superpowers/plans/2026-09-05-phase2-y.md"] = "# untracked\n" });
        var apply = new Apply(fixture.Root);

        Assert.Equal(3, apply.Refusals(apply.Plan()).Count);
    }

    // The guard is asserted AGAIN immediately before the first write, so deleting the
    // refusal-collecting call cannot leave this stage unguarded.
    [Fact]
    public void The_guard_call_is_load_bearing()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase1-x.md"] = "# x\n" });
        var apply = new Apply(fixture.Root, delivery: "../product-spec");

        Assert.Throws<FrozenPathException>(() => apply.Perform(apply.Plan()));
        // Nothing was created inside the normative tree, and the source is still in the inbox.
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "docs/product-spec")));
        Assert.True(Exists(fixture, "docs/superpowers/specs/2026-09-05-phase1-x.md"));
    }

    // --- performing ----------------------------------------------------------------------------

    [Fact]
    public void Perform_moves_with_git_mv_so_history_follows()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase5a-x-design.md"] = "# x\n" });
        var apply = new Apply(fixture.Root);

        Assert.Single(apply.Perform(apply.Plan()));
        Assert.True(Exists(fixture, "docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md"));
        Assert.False(Exists(fixture, "docs/superpowers/specs/2026-09-05-phase5a-x-design.md"));
        // Staged as a rename, which is what makes `git log --follow` resolve across the move.
        Assert.StartsWith("R", fixture.Status(), StringComparison.Ordinal);
    }

    // --- the command line ----------------------------------------------------------------------

    [Fact]
    public void Cli_dry_run_prints_the_exact_git_mv_commands_and_moves_nothing()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase5a-x-design.md"] = "# x\n" });
        var before = fixture.Status();
        var (status, output, error) = RunCli(fixture.Root, "--dry-run");

        Assert.Equal(string.Empty, error);
        Assert.Equal(0, status);
        Assert.Contains(
            "git mv -- docs/superpowers/specs/2026-09-05-phase5a-x-design.md docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md",
            output,
            StringComparison.Ordinal);
        Assert.Contains("1 move(s) planned. Re-run with --write to perform them.", output, StringComparison.Ordinal);
        Assert.True(Exists(fixture, "docs/superpowers/specs/2026-09-05-phase5a-x-design.md"));
        Assert.Equal(before, fixture.Status());
    }

    [Fact]
    public void Cli_is_dry_by_default()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase1-x.md"] = "# x\n" });

        Assert.Equal(0, RunCli(fixture.Root).Status);
        Assert.True(Exists(fixture, "docs/superpowers/specs/2026-09-05-phase1-x.md"));
    }

    [Fact]
    public void Cli_write_performs_the_batch_and_says_what_it_did_not_do()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/plans/2026-09-05-phase3-x.md"] = "# x\n" });
        var (status, output, error) = RunCli(fixture.Root, "--write", "--delivery", "mvp");

        Assert.Equal(string.Empty, error);
        Assert.Equal(0, status);
        Assert.Contains("1 file(s) collected.", output, StringComparison.Ordinal);
        Assert.Contains("Repoint references", output, StringComparison.Ordinal);
        Assert.True(Exists(fixture, "docs/work/mvp/phase3/2026-09-05-phase3-x.md"));
    }

    [Fact]
    public void Cli_honours_phase_and_rename_together()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-x-design.md"] = "# x\n" });
        var (status, output, _) = RunCli(
            fixture.Root, "--phase", "5a", "--rename", "2026-09-05-x-design.md=2026-09-05-phase5a-x-design.md", "--write");

        Assert.Equal(0, status);
        Assert.Contains("docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md", output, StringComparison.Ordinal);
        Assert.True(Exists(fixture, "docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-x-design.md"));
    }

    [Fact]
    public void Cli_says_the_inbox_is_empty()
    {
        using var fixture = Fixture.Create();
        var (status, output, _) = RunCli(fixture.Root, "--write");

        Assert.Equal(0, status);
        Assert.Contains("the inbox is empty; nothing to collect.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_refuses_the_whole_batch_and_leaves_the_tree_untouched()
    {
        using var fixture = WithInbox(new()
        {
            ["docs/superpowers/specs/2026-01-01-phase1-thing.md"] = "# collides\n",
            ["docs/superpowers/plans/2026-09-05-phase2-fine.md"] = "# fine\n",
        });
        var before = fixture.Status();
        var (status, _, error) = RunCli(fixture.Root, "--write");

        Assert.Equal(1, status);
        Assert.Matches("refusing: .*already exists", error);
        Assert.Contains("the whole batch was declined, so the tree is untouched", error, StringComparison.Ordinal);
        // The second file was movable and must NOT have moved.
        Assert.False(Exists(fixture, "docs/work/mvp/phase2/2026-09-05-phase2-fine.md"));
        Assert.Equal(before, fixture.Status());
    }

    [Fact]
    public void Cli_refuses_a_frozen_delivery()
    {
        using var fixture = WithInbox(new() { ["docs/superpowers/specs/2026-09-05-phase1-x.md"] = "# x\n" });
        var (status, _, error) = RunCli(fixture.Root, "--write", "--delivery", "../product-spec");

        Assert.Equal(1, status);
        Assert.Contains("frozen entry 'docs/product-spec'", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("foo.md=")]
    [InlineData("=foo.md")]
    public void Cli_refuses_a_malformed_rename_without_doubling_the_option_name(string rename)
    {
        using var fixture = Fixture.Create();
        var (status, _, error) = RunCli(fixture.Root, "--rename", rename);

        Assert.Equal(1, status);
        Assert.Contains($"--rename wants FROM=TO, got {rename}", error, StringComparison.Ordinal);
        Assert.DoesNotContain("--rename --rename", error, StringComparison.Ordinal);
    }
}

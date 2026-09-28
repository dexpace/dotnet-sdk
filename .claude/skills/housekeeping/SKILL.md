---
name: housekeeping
description: Use before handing over a phase, whenever docs/superpowers/ has files in it, after a brainstorm or a plan lands, when CLAUDE.md or README.md counts may be stale, when a src/ project may be missing its NuGet package README, or when asked to tidy docs/, find broken links or misattributed requirement IDs, and file phase documents. Probes the repository for documentation drift, reports it, and applies the one mechanical repair.
---

# Housekeeping

## Overview

Documentation drifts because nothing checks it. A `CLAUDE.md` claiming "two shipped projects"
while `src/` grew to five, a `README.md` whose layout block names a project that was renamed, a
NuGet package whose README never reached nuget.org because the `.csproj` never packed it — every
one of those is checkable against the repository in a few lines of code, and none of them is
checked by anything else. The build is this repository's lint gate for code; nothing gates the
prose.

This skill is that check. Two stages, and the order is not optional.

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe            # read-only. Report. Always first.
dotnet run --project .claude/skills/housekeeping/src -- apply            # dry run: prints the git mv commands
dotnet run --project .claude/skills/housekeeping/src -- apply --write    # performs them
```

A console project (`net10.0`, `IsPackable=false`, every type `internal`) that references **no
package**: the BCL covers process spawning, regular expressions and JSON, and the argument parser is a
few dozen hand-written lines because `System.CommandLine` is a package. It is not in `Dexpace.Sdk.sln`,
so the library build, test and pack never touch it. It inherits the root `Directory.Build.props`, so
warnings-as-errors, `latest-recommended` analyzers and code style in build apply to it exactly as to
the SDK; the only local dial-down is `CA1707` for the test method names (see `.editorconfig` here).
It is a hand-run tool, not a CI step. Run it before claiming the documentation is current, after
landing a phase, and whenever `docs/superpowers/` has something in it.

## Stage 1 — probe

Read-only, and tested to be: `ProbeTests.The_probe_writes_nothing` snapshots `git status --porcelain`
around a run and asserts it did not move. Exit code is 1 when anything is found, so it can be
promoted to a gate as it stands; `--warn-only` exits 0 without changing what it reports.

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
dotnet run --project .claude/skills/housekeeping/src -- probe --json
dotnet run --project .claude/skills/housekeeping/src -- probe --warn-only
dotnet run --project .claude/skills/housekeeping/src -- probe --root /tmp/a-fixture-tree
```

Findings are grouped by check and printed as `path:line: [severity] message`, followed by a
summary line. `act` is drift to fix; `note` is a judgement call. Exit 2 is a usage error.

Nine checks. Each is a small class deriving from `Check` with a `Name` and a `Run(repo)`, and each
derives the repository fact **once, from the repository**, then compares every document that states
it against that one derivation — never one document against another.

| Check | Finds |
|---|---|
| `inbox` | Files in `docs/superpowers/{specs,plans}/` that are not yet filed under `docs/work/` — staged or not, because unstaged is the inbox's normal state. `README.md` and `.gitkeep` are furniture |
| `root` | Markdown at the repository root that belongs under `docs/`. `README.md`, `CLAUDE.md`, `CHANGELOG.md` (conventional next to a NuGet package's release notes) and the community-health files (`CONTRIBUTING`, `CODE_OF_CONDUCT`, `SECURITY`, `LICENSE`) are the whole allowed list |
| `claims` | A count stated in `CLAUDE.md`, `README.md` or `docs/README.md` that the repository contradicts: the number of **shipped projects** under `src/` (a directory with a `.csproj` that does not set `<IsPackable>false</IsPackable>` — each one is a NuGet package), of `phaseN` directories under `docs/work/*/`, of topics under `docs/knowledge/harvested/` |
| `readmes` | A shipped project under `src/` with no `README.md`, one under 20 lines, or one whose first `# ` heading does not name the project's `<PackageId>` (the `.csproj` file name when none is set). Also a `note` when the `.csproj` sets no `<PackageReadmeFile>`, because then the README never reaches the package page. A directory under `src/` with no `.csproj` is reported; a non-packable one is skipped |
| `links` | Broken relative links in `docs/**/*.md`, root `*.md` and `src/*/README.md`. `http(s)://`, `mailto:` and anchor-only targets are skipped; fences, inline code and indented blocks are examples, not links; reference-style links are resolved against their definitions. Also a **backticked** chapter of a normative tree — `docs/product-spec/NN-….md`, `docs/sdk-design-dotnet/NN-….md` — that does not resolve: this repository writes chapter references in backticks, not link syntax, so nothing else reads them. Scoped to those two trees, so a `src/…` path a later phase creates is not reported; a wildcard or an elided path (`*`, `…`, `NN`) is not a claim about one file and is skipped |
| `registers` | An aggregate `## Open Findings` / `## Deferred Items` / `## Open Items` section living inside a spec, design or plan document (`docs/work`, `docs/superpowers`, `docs/sdk-documentation`, `docs/product-spec`, `docs/sdk-design-dotnet`). There is no find-list register: a finding belongs with its owner — the numbered plan task whose scope it falls in, or the roadmap. A `**Moved out on …` pointer stub is not a register |
| `citations` | A citation of a register item ID that does not resolve. **This repository has never had a register**, so both tables — live `DefaultRegisters` and retired `DefaultRetired` — are empty and the check reports nothing today. The mechanism is kept whole because a register is one entry away: a live prefix resolves only against its own file and no-ops when that file does not exist; a retired prefix never no-ops (a missing file is exactly what would hide the leftovers), and a lingering retired file is one finding, not one per row. A **backticked** ID counts, and so does one on a 4-space-indented continuation line; only a ``` fence is an example. The ID namespace is a declared prefix list, never `[A-Z]+-\d+`, which would swallow every requirement ID in the spec |
| `chapters` | A requirement ID a document attributes to a `docs/product-spec/NN-….md` chapter that does not carry it. The unit is a clause: a line is split at `;`, each ID pairs with the nearest preceding chapter reference in its clause (or, for an ID run followed by "appears in", with the chapter that phrase introduces), a range is expanded whether or not its endpoints are backticked, and a two-line window carrying a negation ("appears nowhere", "is not in", "doesn't appear", …) is skipped. Appendix C is exempt as a target. Two blind spots are stated in `Chapters.Gaps`: a chapter reference on the preceding line (`continued_clause`), and a chapter named by an ellipsis or a variable (`dynamic_chapter_path`) |
| `guard` | The frozen list and the writable surface overlapping, a path the guard has quietly stopped refusing, or a frozen entry that has become a symlink — the three ways the apply stage could eat a normative document |

The `claims` check is a **declarative table**: file, a pattern whose first capture is the number,
a label, and a function that derives the real value. Adding a claim is one row. A count written as
an English word is read as well as a digit — a digits-only matcher protects about one sentence per
repository — and fenced code and double-quoted spans are excluded, so a document that quotes the
historical drift it fixed does not fail its own check. The numeral must sit directly before the
noun or before one of `shipped` / `published` / `library` / `NuGet`: "three test projects" is not a
claim about shipped projects.

## Stage 2 — apply

**Only after reading the probe's report.** The apply stage does exactly one thing: drains
`docs/superpowers/{specs,plans}/` into `docs/work/<delivery>/phaseN[/phaseNx]/` with `git mv`, so
`git log --follow` resolves each file across the move. It starts `git` as a child process with an
argument vector; it never calls `File.Move`, because a plain move is a delete plus an add and the
history stops there.

```bash
dotnet run --project .claude/skills/housekeeping/src -- apply                               # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5a --write
dotnet run --project .claude/skills/housekeeping/src -- apply --rename 2026-09-05-x.md=2026-09-05-phase5a-x.md --write
```

Dry by default; `--write` performs, `--dry-run` states the default explicitly and prints the exact
`git mv -- <from> <to>` commands it would run. `--phase 5a` files everything under
`docs/work/<delivery>/phase5/phase5a/`; without it the target is read from the filename
(`-phase5a-` → sub-phase, `-phase5-` or `-phase5.` → whole phase, `phase10` is never read as
`phase1`). `--rename` is repeatable and keyed by inbox path or basename. Options also take the
`--name=value` form.

It refuses the **whole batch**, leaving the tree untouched, if any source or target is under a
frozen entry, if a target already exists, if two inbox files land on one target, or if a source is
untracked and so cannot be `git mv`-ed at all — and it reports every refusal, not the first.
Half-applying is the one outcome that leaves an operator with no good next move; if `git mv` itself
fails mid-batch, the tool prints exactly which moves completed.

**The inbox today.** `docs/superpowers/` still holds the thirteen pre-skill documents of 2026-06-14/15
(the platform design, ten slice designs, two plans). None names a phase, so `apply` would file every
one directly under `docs/work/mvp/`. That is a decision about how those slices map onto the roadmap's
phases, not a mechanical one: pick the phase for each and use `--phase` / `--rename`, in a commit
of its own.

### What it deliberately does not do

Everything else the probe reports — a stale count, a missing package README, a broken link, a
misattributed requirement ID — is **prose, and you edit it**. That is deliberate:

> A tool that rewrites prose to make its own check pass produces documentation that is true
> and useless at the same time.

The probe tells you what is wrong and where; the judgement about what the sentence should say is
yours. Two more things `--write` does not do, and says so when it finishes:

1. **Repoint references.** Re-run the probe's `links` and `citations` checks and fix what they
   report, in the same commit — a reference that no longer matches the tree is corrected with the
   change that staled it, never deferred.
2. **Commit.** A migration is its own commit, `git mv` only, so history follows every file.

## What it must never write

```
docs/knowledge/          docs/product-spec/       docs/product-spec.md
docs/styleguide/         docs/sdk-design-dotnet/  docs/sdk-design-dotnet.md
```

Both shapes are in that list on purpose, and the matcher handles each: a **directory prefix** and
an **exact file**. The list lives in one field, `Guard.Frozen` in `src/Guard.cs`, and
`GuardTests.The_frozen_list_is_pinned` pins it, so widening it is a reviewed diff rather than a
silent change. `docs/styleguide/` is on it because it is vendored from the styleguide repository:
it is re-vendored, never edited in place.

This is a guard, not a promise. `Guard` exposes `IsFrozen`, `AssertWritable` and
`AssertAllWritable`; `Apply` calls the guard twice — once when collecting refusals, once
(`AssertAllWritable`) immediately before the first write — so deleting either call still leaves the
stage guarded (`ApplyTests.The_guard_call_is_load_bearing`). `GuardTests` proves the four ways a
naive `StartsWith` fails:

- a **sibling** whose name merely starts with a frozen one (`docs/product-specs/`,
  `docs/product-spec-draft/`, `docs/product-spec.md.bak`) is writable, because the comparison is
  segment-wise;
- a `..` segment that lands **inside** after normalization is refused;
- an **absolute** path is resolved rather than treated as relative, and one outside the repository
  is not frozen;
- a **symlink** whose target is inside a frozen tree is refused, because `Directory.CreateDirectory`
  follows the link and a purely lexical guard would say yes while `git mv` wrote into the normative
  tree. The BCL resolves only a path's *final* link (`FileSystemInfo.ResolveLinkTarget`), so
  `Guard.RealPath` walks the path component by component the way POSIX `realpath(3)` does; a link
  in the middle of a path is tested separately.

The one tree worth a sentence of its own: `docs/knowledge/harvested/` **cannot** absorb a hand edit,
because a `<sub>` sha digests the whole source file rather than the entry — an edit inside an entry
changes no sha, and the next harvest regenerates or duplicates it with nothing to notice. A finding
about a harvested rule goes in `docs/knowledge/notes/`, by hand, by a human.

## Phase file conventions

The archive is `docs/work/<delivery>/phaseN[/phaseNx]/`; `mvp` is the first delivery and a later
effort becomes a sibling of it. A phase directory is `phaseN`, no hyphen; a sub-phase nests one
directory deeper as `phaseN/phaseNx`. Every file keeps a `YYYY-MM-DD-<slug>.md` prefix, and a
(sub)phase has three:

```
docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-transport-design.md   # the design
docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-transport.md          # the plan
docs/work/mvp/phase5/phase5a/2026-09-05-phase5a-transport-checklist.md
```

A document spanning a whole phase — a segmentation design, a shared checklist — sits at the
`phaseN/` level. A document belonging to no phase sits directly under the delivery.

The shipped surface is `src/<PackageId>/`, one NuGet package per project, each with its own
`README.md` packed through `<PackageReadmeFile>`. The `readmes` and `claims` checks read that shape
from the `.csproj` files, and no-op cleanly when `src/` is absent.

## What differs from the Ruby version

The Ruby port (`ruby-sdk/.claude/skills/housekeeping/`) is the source; this is a reimplementation,
not a transliteration. Every deliberate difference:

- **One console project, two subcommands** (`probe`, `apply`) instead of two scripts; tests call the
  command line in-process through `Program.Run(args, out, err)` rather than spawning `ruby -w`.
- **Frozen list** gains `docs/styleguide/` (vendored) and names `docs/sdk-design-dotnet{,.md}`.
- **`claims` / `readmes`** count and check `src/` projects by `.csproj` (`IsPackable`, `PackageId`)
  instead of `gems/` by gemspec. `readmes` adds one sub-rule Ruby has no analogue for: the
  `<PackageReadmeFile>` note, because a NuGet README that is not packed does not exist for a
  consumer.
- **`citations`**: Ruby's two retired prefixes (`DEF-`, `OI-`) and their files are history this
  repository never had, so both tables ship empty and the check is silent; the retired table is a
  constructor argument too (Ruby's was a constant), so the suite exercises the retired half with an
  invented `OLD-` prefix. Readable files are `*.md` and `*.cs`.
- **`registers`** names the plan task or the roadmap as the owner; Ruby's phase-10 inbound list and
  `docs/first-release.md` do not exist here.
- **`chapters`**: Ruby's phase-10 exemption becomes an empty, constructor-configurable prefix list
  (`Chapters.DefaultExemptDocuments`). It reads spec chapters and documents from tracked **and
  untracked** files, where Ruby read tracked only: this port's documents land before their first
  commit, and a tracked-only read made the check vacuous exactly when it matters. The two Ruby
  regression fixtures are kept verbatim, inline in `ChaptersTests`.
- **Regex dialect**: `^`/`$` need `RegexOptions.Multiline` in .NET (Ruby's are line anchors by
  default); digits are `[0-9]`, since .NET's `\d` matches every Unicode decimal digit.

## Deliberately not ported: the fence check

The Node original ships a tenth tool, `check-fences.mjs`, which extracts every ` ```typescript `
fence that imports from the workspace and typechecks the lot against `dist/`. It is **not** ported
yet: this repository has no worked-example documentation for it to protect, and the package READMEs
the `readmes` check asks for do not exist.

The .NET analogue, when it is worth building, is a **compiler** step, closer to Node's than to
Ruby's executor. Extract every ` ```csharp ` fence in `README.md`, `docs/sdk-documentation/*.md` and
`src/*/README.md` that names a `Dexpace.Sdk` namespace (a `using Dexpace.Sdk…;` or a qualified
`Dexpace.Sdk.` type — the test is for the *namespace*, not for a `using` line, which is the
single-line-import trap Node documented); write each into a generated temp project under the
scratch directory — top-level statements, `net10.0`, `<Reference>`s to the built
`src/*/bin/Release/net8.0/*.dll` rather than `ProjectReference`s, so the check proves the fence
against what ships — and `dotnet build` it with warnings as errors. A fence with no `Dexpace.Sdk`
reference is an illustrative fragment and is skipped; one that references an optional package not
in `Directory.Packages.props` is reported, not failed. The scratch directory goes through
`Guard.AssertWritable` like every other write. A C# example's failure mode is a compile error, so
compiling is the whole contract — running it would need a stubbed transport and adds nothing a
compile does not already catch.

## Its own tests

```bash
dotnet test --project .claude/skills/housekeeping/tests
dotnet test --project .claude/skills/housekeeping/tests --filter-class "*GuardTests"
```

xUnit v3 on Microsoft.Testing.Platform (global.json selects the platform's `dotnet test` mode), with versions from the root `Directory.Packages.props`. Every check has a **pair**: a
throwaway fixture repository (a temp directory, `git init`-ed with a hermetic config and deleted
afterwards) it must report clean over, and a mutation of that tree it must fire on. A suite that
only asserts the live repository is clean passes just as happily over a check whose body has become
`[]`, and the live repository is clean most of the time, so such a suite stays green while the
checks rot. No case count is written here on purpose; `dotnet test` reports it.

## Structure

```
.claude/skills/housekeeping/
  SKILL.md                  this file
  .editorconfig             the one local analyzer dial-down (CA1707, test names), with its reason
  src/
    Housekeeping.csproj     net10.0 console tool, no package references
    Program.cs              `probe` / `apply` dispatch
    Guard.cs                the frozen-path guard; every write site goes through it
    Repository.cs           Git, Repo, Finding, Numeral, Prose — what a check may read
    Checks.cs               stage 1 — eight of the nine read-only checks
    Chapters.cs             the ninth, `chapters`, in its own file
    Probe.cs                runs the checks; text and JSON rendering; the argument reader
    Apply.cs                stage 2 — git mv only, guarded twice, dry by default
  tests/
    Housekeeping.Tests.csproj
    Fixture.cs              builds the throwaway repositories the tests probe
    GuardTests.cs           prefix, traversal, absolute, symlink (final and intermediate), the pinned list
    ProbeTests.cs           every check has a fixture it fires on, the read-only contract, the CLI
    ChaptersTests.cs        the clause rule, ranges, negations, the two regression fixtures, the gaps
    ApplyTests.cs           the target mapping, every batch refusal, the CLI
```

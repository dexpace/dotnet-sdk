# Phase 12 — First Release: Checklist

The execution-time checklist for roadmap phase 12 ([phase card](../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID the phase changed, in the legend of the roadmap's cross-cutting constraint 3.
Written from what was built on the merged branch `109-phase-12-first-release` (issue #107; the plan's five stacked pull requests were replaced by one branch: three implementation agents A, B and C, each on its own
worktree branch, merged in that order, then a sequential close-out D), following the [design](2026-10-22-phase12-first-release-design.md) and the [plan](2026-10-22-phase12-first-release.md). **The first NuGet
publish is held** (the lead's stance): nothing here publishes, tags or pushes, and no remote action was taken to produce this phase (no `git push`, no `gh`, no network read of an action's README).

The phase owns **no requirement of its own** (the roadmap: "Phases 11 and 12 re-audit rows. They do not own any"). The rows below are the phase's *dated corrections*: an ID whose earlier closed row now misreports the build, or
whose held half moved. The script resolves an ID by phase order, so a row here replaces the earlier row without editing a closed checklist; where the earlier row was not an hourglass the new row says **supersedes**.
The scope is **4 rows** (1 MUST, 3 SHOULD by appendix C): **2 ✅ split / 1 ⏳ SHOULD / 1 ⏳ MUST**. The one MUST row still ⏳ is `NFR-17`, which names its owner (`docs/first-release.md`, the `main` ruleset): MUST ⏳ stays **1**.
The deliverables are the strong-named, symbol-carrying, reproducible `0.1.0-alpha.1` packages, the release preflight, the inert `release.yml`, the recorded public surface, the nine user-documentation pages with the fence compiler that
keeps them compiling, the draft release notes, and [`docs/first-release.md`](../../../first-release.md) rewritten to say exactly what the held publish waits on.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification or an unmet clause on a stated domain, with the reason and the design §10 entry. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the owner. |
| N/A | Not applicable in this port. |

## Requirement rows

Tests are named `Class.Method` as they exist in the tree.

| ID | Level | Status | Task(s) | What was built, where proven |
|---|---|---|---|---|
| `NFR-4` | SHOULD | ✅ split (supersedes the phase 10 row; the baseline half stays after the first publish) | 2.4 | **Source half met.** Each library's `PublicAPI.Unshipped.txt` moved into `PublicAPI.Shipped.txt` (`#nullable enable` first, then every member in order: Core 1 369, Conformance 475, SystemNet 39, DI 27, System.Text.Json 12) and `Unshipped` is `#nullable enable` alone; a pure move, `git diff --stat -M` reads as one. `NFR4ArchitectureTests.Shipped_holds_the_surface_and_starts_with_nullable_enable` (`Unit`, `tests/Dexpace.Sdk.Core.Tests/Architecture/NFR4ArchitectureTests.cs`) replaces the old "empty until phase 12" test (red first: all five files were header-only), and `The_scan_can_fail` gained an empty and a header-less `Shipped`. The release preflight requires every `Unshipped` to be back to `#nullable enable` at a tag. ⏳ **The baseline half is held** (design V5, P12-D2): `PackageValidationBaselineVersion` cannot be set before a package of that id exists on nuget.org, and setting it earlier breaks every build; `docs/first-release.md` *After the first publish* has the procedure. |
| `NFR-12` | SHOULD | ✅ split (supersedes the phase 11 row; the release half stays after the first publish) | 1.1, 2.2, 3.2 | **Build half met, and the cause of the red job found.** The `reproducible packages` job's failure was the `LoggerMessage` source generator ordering the two `[LoggerMessage]` classes of `Dexpace.Sdk.Http.SystemNet` differently from one build to the next (design V6; phase 11's toolchain hypothesis was not the cause). All four generated methods now live in the internal `TransportLog`; `LoggerMessageDeterminismArchitectureTests.Each_library_has_at_most_one_type_with_generated_log_methods` (`Unit`, `tests/Dexpace.Sdk.Core.Tests/Architecture/`) fails a second type in any library (red first, naming `HeaderDropLog` and `ProxyWarnings`); the generator's output is the proof (V7): eight builds give one `LoggerMessage.g.cs` hash, where two builds of one commit gave two. Symbol packages (`IncludeSymbols`, `snupkg`) put a `.snupkg` beside each `.nupkg`, and `reproducible-pack.sh --self-test` gained two cases (a `.snupkg` beside every `.nupkg`; a flipped byte in a `.snupkg` fails the compare), red before the property. Measured on this tree: `reproducible-pack.sh` reports 10 files byte-identical across two paths on every run of this close-out. ⏳ **The release half is held** (P12-12): a rebuild from the tag reproduces the attested pre-push digests byte for byte, and the nuget.org copy equals them once its `.signature.p7s` entry is removed, entry by entry; `docs/first-release.md` *After the first publish*. Not confirmed by a CI run (L1 below). |
| `NFR-16` | SHOULD | ⏳ (supersedes nothing; the phase 10 row was ⏳) | 2.1, 3.1, 3.2 | **The mechanism is built; the closure is held** (owner `docs/first-release.md`, *After the first publish*). Strong naming is built: `Dexpace.Sdk.snk` (2048-bit RSA, `CALG_RSA_SIGN` blob), `PublicKeyToken` `fb3fcc3028a3989e`, on every project under `src/` and `tests/` (the tools stay unsigned), each `InternalsVisibleTo` keyed with `DexpacePublicKey`; `StrongNameKeyTests` (`Unit`, `tests/Dexpace.Sdk.Conformance.Tests/Gates/`) pins the property against each built library and the friend test assembly, the algorithm, the token as a literal and the keyed grants, with a mutation test. The release preflight (`scripts/ci/release-preflight.cs`, self-test every case ok) fails a release build whose packed assembly is not signed with the committed key: the requirement's testable clause. Author signing with a certificate is declined (P12-13); `release.yml`'s publish job attests every package (`actions/attest-build-provenance`) and the closure is `gh attestation verify <file> --repo dexpace/dotnet-sdk` over a real publish. The workflow is pinned by `ReleaseWorkflowTests` (`Unit`, 12 tests, each rule with a mutation that must be flagged) and is inert. The action inputs are unverified against each README (no network at authoring), recorded in the file's header and the register. |
| `NFR-17` | MUST | ⏳ | 3.2 (gate set only) | The first half stays built (`CiWorkflowTests` pins every gate a named, blocking step, with the phase's new rows: *Release preflight*, *Release preflight - self-test*, *Doc fences*). The second half is a repository setting outside the file and **was not changed**: no `required_status_checks` rule names the five jobs. Lead action L3; owner `docs/first-release.md`, *Blockers before first publish*, "The `main` ruleset requires the CI jobs". If the lead declines, it becomes the fifth counted unmet clause in one commit across `docs/deviations.md` and the register (V17); not planned. |

The phase's other changed IDs keep their earlier rows, which remain true: `SEAM-29`, `HTTP-2` to `HTTP-4` (the phase 11 row covers the null spelling; task 1.2 completes the range and format spelling, with a **Breaking** `CHANGELOG.md` line and
`InitAccessorRejectionParamNameTests` / `InitAccessorParamNameTests` as the proof), `NFR-13` (`LicenseHeaderTests` now skips build output and nested worktrees), `NFR-14`, `NFR-15` (the packaging suite stays green over the repacked `0.1.0-alpha.1` set).
`TRANSPORT-12`, `TRANSPORT-13`, `TRANSPORT-30`, `OBS-19` and `OBS-20` are untouched: the generated log methods moved to `TransportLog` with their event ids, names, levels and message templates unchanged.

## Blocker dispositions

The register has eleven open or ticked blockers; the phase closes five, leaves four for the lead and two held or lead-confirmed (the design's table). Each is dated in `docs/first-release.md`.

| Blocker | Disposition | Evidence |
|---|---|---|
| One-time public-surface choices | **closed** (ticked at the move commit) | P12-15's table kept as built; `ParamName` completed (1.2); `PublicAPI` moved (2.4, `1441057`) |
| Signing decided | **closed** (decided) | strong naming built, author signing declined for attestation (2.1, 3.1, 3.2) |
| Versioning policy written | **closed** | `versioning.md`, `VersionPrefix` `0.1.0` (2.3, 4.4) |
| User documentation | **closed** | nine pages, the conformance section, `fences` 21 compiled / 4 fragments / 0 failed (4.1 to 4.7) |
| `CHANGELOG.md` complete | **closed** | the audit and the draft notes (4.8) |
| CI green on every matrix row | **lead** | cause fixed (1.1); no CI run read (L1); ticks with the first green `main` run |
| The `main` ruleset (`NFR-17`) | **lead** | L3 |
| Every MUST dispositioned | **lead** (tied to the ruleset) | open for `NFR-17` alone |
| Conformance green | **lead** (tied to CI) | green locally; ticks with the CI line |
| A reachable `SECURITY.md` contact | **lead** | file rewritten (4.7); L4 confirms the mailbox and enables private vulnerability reporting |
| NuGet ownership settled | **held** | the exact steps in P12-25's order, the scoped-key fallback recorded as such, the `release.yml` inputs marked unverified |

## Carried in from phase 11

| Item | Disposition |
|---|---|
| CI not run on `main` since `6b49af6` | **Not read.** No remote action was authorised for the implementation, so L1 (the first CI run of this branch's pull request, all five jobs) is open. The result goes into this table when the lead has it. |
| The reproducible-pack fix unproven | Root cause found and fixed (1.1); proven by the generator's output, not a run count, and by 10 of 10 byte-identical files on every local run. CI confirmation is L1. |
| macOS `FromFile` unverified | **Not read** (needs the macOS row). Contingent task 1.C1 was not triggered because nothing was read; if the row is red, it lands before the next release as a `UnixFileKind` fix. |
| About fifteen `ParamName` `value` accessors | **Fixed** (1.2): the 14 accessors of the `ThrowIf*(value…)` spelling and every `nameof(value)` and helper-routed one; the scanner learned both spellings so a future one is caught. |
| The Core flake family (`InstrumentationContextTests`, `OperationSpanLifecycleTests`) | **Not read**; contingent task 1.C2 not triggered. See the intermittent test below. |
| `NFR-17`, the ruleset | L3, open. |
| D3, webhooks | Unchanged (post-v1, the lead's, P12-23). |
| Release notes lead with phase 11's breaking changes; name the `ShouldHandle` guidance and the no-reload rule | Done (4.8): the draft block's first list is the digest and its guidance section names both; `ReleaseNotesTests`. |
| `NFR-4` baseline, `NFR-12` release half, `NFR-16` | Held, *After the first publish*. |
| The `CallKey` and `AddDexpaceClient<TClient>` triggers | Unchanged (*Post-release triggers*). |

**An intermittent test, unowned.** `RetryTraceEventsTests.The_exhausted_event_follows_the_final_predicate` failed once in one full run of the combined gate and passed three of three reruns on its own; it did not fail in the final gate's full run
(7 759 tests). Nobody owns it: it is not in the Core flake family above and no change of this phase touches the retry trace. Recorded here so the next person who sees it red has the history; a CI read (L1) or a repeat failure
is what would justify a contingent task under P12-22.

## The L1 result

**Not read.** The implementation was done under a rule that allows no push and no `gh`, so there is no pull request and no CI run. Open: (a) the five jobs, with the `reproducible-pack:` lines if the `reproducible packages`
job is red; (b) the macOS `FileRequestBodyTests` rows; (c) any `InstrumentationContextTests` / `OperationSpanLifecycleTests` red; (d) any other OS-specific red; (e) whether the repository is public (L2). Each red found triggers the matching contingent task 1.C1 to 1.C3
before the release.

## The changelog audit (P12-19)

The walk: `git log --first-parent main --oneline` is **55** commits at `f5d1ea7` (the count at the plan's start, unchanged). The foundation PRs #3 to #9 were back-filled by phase 0; every phase commit (`feat!: phase …`) has its entries by subject or phase;
the `docs:` commits need none. **Missing and back-filled** (the "Audit back-fill" block of the Phase 12 section; ten pull requests in six groups): #22 (test partition, loopback fixture, xUnit v3), #20 (the SourceLink reference and `NuGetAuditMode`), #35 (the coverage gate fails
closed), #42 (the knowledge CLI), #11/#12 (the label sync workflow), #26/#27/#34/#53 (dependency updates). One defect of the audit's own: agent A's insertion of the Phase 12 heading had **replaced** the `### Phase 11 — deviation reconciliation` heading line, leaving the
phase 11 prose under Phase 12; the heading is restored (`e0f46f3`). `ReleaseNotesTests` extracts 31 keys from the register's four "What v1 ships without" subsections and requires each in the draft block.

## Fence counts (P12-18)

`dotnet run --project .claude/skills/housekeeping/src -- fences` over the repository: at task 4.1 **4 compiled, 3 fragments, 0 skipped, 0 failed** (the seven fences naming `Dexpace.Sdk`); at the close **21 compiled, 4 fragments, 0 skipped, 0 failed** (the new pages
add 17 compiling fences and one fragment). Fences are generated into a scratch directory outside the repository, so the repository's build props, analyzers and lock files do not reach them. It is a blocking Linux CI step.

## Styleguide audit

| Group | Result |
|---|---|
| Public surface | No member added, removed or reshaped; the one `PublicAPI` change is the move of 2.4. `ParamName` changed on every validating accessor that rejected a range or format, which is behaviour, not signature. |
| Nullability | No `!` added to `src/`; the five `ThrowIfNullOrWhiteSpace` accessors now report a null with the property's name too. |
| Async | `src/` touched in two places only (1.1, 1.2), neither with an `await`. |
| Disposal | No change. |
| Analyzers | 0 warnings; no new `#pragma`; no analyzer relaxation added (`AnalyzerWaiverTests`); `MA0051` holds; `TransportLog` is one `static partial` type. |
| Tests | Every new class carries `[Trait("Category", "Unit")]`; the housekeeping tests carry none (existing convention). |
| Trim / AOT | The NativeAOT publish prints no warning and the smoke prints `aot-smoke: all checks passed`. |
| Styleguide conflicts | None new. Design §9.2 said every project is signed (phase 12 design:314); the build signs `src/` and `tests/` only (plan R8), because signing the tools breaks their keyless `InternalsVisibleTo` friends with `CS1726`; §9.2's dated correction says so. |

## Rulings taken at their defaults

All open rulings were taken at the plan's recommended default, and the lead may still rule otherwise (the plan's *Lead rulings* table lists the task each changes): **P12-7** `0.1.0` (`0.1.0-alpha.1` on `main`); **P12-9** strong naming built; **P12-13** `NFR-16` by attestation,
author signing declined; **P12-15** the public surface as built (`ServiceRequestException` stays an `IOException`); **P12-20** the ruleset is the lead's; **P12-23** webhooks post-v1; **P12-24** the `SECURITY.md` contact kept, private vulnerability reporting named conditionally;
**P12-25** the nuget.org organisation `dexpace` owns the `Dexpace.*` prefix.

## Ledger (P12-D1 to P12-D4)

Roadmap constraint 7's entries; **no design §10 entry is opened** and `docs/deviations.md` is edited only to re-derive two citations (`HeaderDropLog.cs:123` to `TransportLog.cs`).

- **P12-D1.** `release.yml` also triggers on `workflow_dispatch`, a dry run by default (`dry_run: true`); the card said "triggered by a tag only". Publishing is still tag-only: a manual run on a branch never publishes.
- **P12-D2.** The card's exit "`PackageValidationBaselineVersion` is set to the published version" is **held**, not met: setting it before a publish breaks every build (V5).
- **P12-D3.** `NFR-16`'s author signing (design §9.2) is replaced by a GitHub artifact attestation, a mechanism substitution that keeps the provenance outcome; not counted (P11-2).
- **P12-D4.** `NFR-12`'s release half is read modulo nuget.org's repository signature (P12-12); recorded in the register, becomes a §11 item at the first publish.

## The lead's open actions

| # | Action | State |
|---|---|---|
| L1 | Read the CI run of this branch's pull request and the first run on `main` | **open** (not read) |
| L2 | Say whether the repository is public (attestations; P12-13's fallback) | **open**; unanswered, so the fallback (drop the attestation step and its pins, decline `NFR-16`'s signature clause as a SHOULD) is described in the register but not applied |
| L3 | Apply the `required_status_checks` rule to the `main` ruleset and read it back | **open** |
| L4 | Confirm the `SECURITY.md` contact is monitored; enable private vulnerability reporting | **open** |
| L5 | Create the nuget.org organisation `dexpace`, request the `Dexpace.*` prefix, create the trusted-publishing policy | **open** (held) |
| L6 | Create the `nuget` environment with required reviewers and set `NUGET_USER` | **open** (held) |
| L7 | Push the branch and open the pull request | **open**; never taken by this phase |
| L8 | Rule on P12-7, -9, -13, -15, -20, -23, -24, -25 | **open**; the work proceeded on the defaults |
| L9 | The held release: release commit, tag `v0.1.0`, first publish, then *After the first publish* | **held** |

## Tasks and commits

The plan's five stacked pull requests were **replaced by the one merged branch**: the three implementation agents worked in worktrees off `f5d1ea7` on disjoint files, merged A, then B, then C, and one sequential agent D closed out.

| Task | Commit | Note |
|---|---|---|
| 1.1 | `b5203c1` | `fix: one [LoggerMessage] type per assembly so the packs are reproducible (NFR-12)` |
| 1.2 | `a3cec7f` | `fix: validating init accessors name their property for range and format rejections` |
| 2.1 | `51144dc` | `feat: strong-name the five libraries with a committed key (NFR-16, design 9.2)` |
| 2.2 | `edb6770` | `feat: pack symbol packages for every library (NFR-12)` |
| 2.3 | `497bb35` | `chore: version 0.1.0-alpha.1 on main (P12-7)` |
| 2.4 | `1441057` | `feat: move PublicAPI.Unshipped into Shipped for the first release (NFR-4)` |
| 3.1 | `293a6b1` | `ci: add the release preflight and its self-test (NFR-16, NFR-12)` |
| 3.2 | `2897dfa`, `2701224` | `ci: add the inert release workflow, pinned by ReleaseWorkflowTests (NFR-16, NFR-12)`; `ci: correct ci.yml's inert-gate header for phase 12 (V16)` |
| 4.1 | `408efeb` | `feat: housekeeping fences command compiles the documentation's C# fences (P12-18)` |
| 4.2 | `8caa809` | `docs: sdk-documentation architecture page; retire docs/architecture.md` |
| 4.3 | `993e1ad` | `docs: getting-started and end-to-end example pages` |
| 4.4 | `b20b326` | `docs: quality-gates and versioning pages` |
| 4.5 | `6d29646` | `docs: write-a transport, serde, response-handler and paging-strategy guides` |
| 4.6 | `33ab2f8` | `docs: conformance page states what a green run does not prove` |
| 4.7 | `98969ad` | `docs: refresh SECURITY, README and CONTRIBUTING for the release path` |
| 4.8 | `e0f46f3` | `docs: changelog audit, Phase 12 entry and the draft 0.1.0 release notes (P12-16, P12-19)` |
| (close-out) | `c79e2fb`, `177688e`, `3e7ccc6`, `fbf7f17` | `test: match a strong-named InternalsVisibleTo grant in NupkgReaderTests (NFR-16)`; `docs: conformance page states the decided version, not a proposal`; `test: LicenseHeaderTests skips build output and nested worktrees (NFR-13)`; `docs: re-verify phase 12 pages against the merged code; link the release files, quote the PublicKeyToken` |
| 5.1 | `db598fd` | `docs: first-release register for phase 12 (blockers, release path, after the first publish)` |
| 5.2 to 5.5 | see `git log` | this checklist; the dated corrections, `CLAUDE.md` and knowledge notes; the roadmap status note; filing |

## Deviations from the plan

1. **One branch, not five pull requests.** The plan's PR groups and branch names were dropped; the commits above are on `109-phase-12-first-release`. The per-PR gates (1.3, 2.5, 3.3, 4.9) are replaced by the one final gate.
2. **Task 1.C1 to 1.C3 and L1 to L9 not run.** No CI read was possible, so nothing triggered a contingent task.
3. **Task 2.1 broke a test the plan did not name.** `NupkgReaderTests` compared the whole `InternalsVisibleTo` argument and failed on the keyed grant; it now matches the name before the first comma (`c79e2fb`), the rule `nfr-3.no-internals-granted` already used.
4. **Task 3.2's ci.yml header** was left to the close-out (it shared `ci.yml` with agents B and C), and the local run of the release build commands was done there too: restore, build, test, `reproducible-pack.sh artifacts/release`, both audits, the preflight over `artifacts/release/1`, `sha256sum` — all as CI will run them.
5. **`LicenseHeaderTests`** (not in the plan): the scan walked a nested worktree's generated, headerless files; it now skips `bin`, `obj`, `artifacts` and `.claude/worktrees/` (`3e7ccc6`, test first).
6. **The merge of agent A replaced the Phase 11 heading in `CHANGELOG.md`**; restored with the audit commit.
7. **The `ThrowIf*(value` grep of task 1.2 is wrong as written.** Its pattern `ThrowIf(…)\(value[,)]` still matches the fixed calls (`ThrowIfNegative(value, nameof(MaxRedirects))`), so "finds nothing" can never hold; the 14 matches today are exactly the 14 fixed sites, each with a `nameof(<Property>)`. The check that holds is `ThrowIf…\(value\)` or `\(value, [A-Za-z.]+\)` without `nameof`: zero.
8. **Release preflight dry run keeps one `would fail:` line**, on purpose: `CHANGELOG.md has no ## [0.1.0-alpha.1] section`. The section is created at the held release commit with the suffix emptied, so the heading is `## [0.1.0]`, and the dry run reports it without failing; the self-test proves the tagged form. Adding a `## [0.1.0-alpha.1]` heading to silence it would put a version into the changelog that is never released.

## Open items

- **L1 to L9** above.
- **The `release.yml` action inputs** (`NuGet/login`'s `user` and `NUGET_API_KEY`, `actions/attest-build-provenance`'s `subject-path`, `actions/download-artifact`, all three majors, and that `dotnet nuget push "*.nupkg"` also pushes the `.snupkg`) are unverified against the action READMEs and the `dotnet` documentation. Verify before lifting the hold.
- **`ci.yml`'s header** still lists the SSE-37 architecture test among gates "inert until their subject exists"; it has been live since phase 7. Out of this phase's scope; a later cleanup.
- **A repository-signed package is not byte-identical** to the one pushed (nuget.org adds `.signature.p7s`); P12-12's reading handles it, and the first publish is where it is first exercised.

## The Security tests

`git diff --stat f5d1ea7..HEAD -- '**/Security/*.cs'` is empty: no `Security` test was added, edited, deleted or loosened. The `Security` suites pass: Core 316, SystemNet 38, DI 3.

## Gate results

Run on 2026-10-26 at `fbf7f17` plus the register commit `db598fd` (the tip before this section was written), .NET SDK 10.0.401, Linux, from the repository root, in the order of `CLAUDE.md`'s *Build & test*.

| Gate | Result |
|---|---|
| `dotnet restore Dexpace.Sdk.sln --locked-mode` and `tools/Dexpace.Tools.sln` | ok; no `packages.lock.json` changed |
| `dotnet build Dexpace.Sdk.sln --configuration Release` | 0 warnings, 0 errors |
| `dotnet format Dexpace.Sdk.sln --verify-no-changes` | exit 0 |
| `dotnet test --solution Dexpace.Sdk.sln --configuration Release` with coverlet | 7 759 tests: 7 736 passed, 0 failed, 23 skipped (the pinned `NotExercised` rows and permanent waivers, unchanged) |
| `coverage-gate.cs` (floor 80) and its self-test | aggregate 97.46%; self-test ok (14 cases) |
| `Category=Security` (Core, SystemNet, DI) | 316 + 38 + 3 passed |
| `Category=Conformance` (Core, SystemNet, DI, System.Text.Json, Conformance.Tests) | 51 (1 skipped), 142 (6), 126 (7), 24, 72 (9): 0 failed |
| `dotnet pack`, `dependency-audit.cs` and its self-test, `packaging-conformance.cs` | 5 `.nupkg` and 5 `.snupkg` at `0.1.0-alpha.1`; 5 libraries conform; self-test ok; packaging verdict GREEN (8 passed) |
| `release-preflight.cs` and its self-test | ok, 5 packages at `0.1.0-alpha.1`, each with symbols, each strong-named with token `fb3fcc3028a3989e` (dry run); the one `would fail:` line is explained in deviation 8; self-test every case ok |
| `reproducible-pack.sh artifacts/reproducible` and `artifacts/release` (with `SOURCE_DATE_EPOCH`), and `--self-test` | 10 files byte-identical across two paths, both runs; self-test ok (5 cases) |
| `tools/Dexpace.Tools.sln` build, format and test | 0 warnings; 461 passed |
| `housekeeping fences` | 21 compiled, 4 fragments, 0 skipped, 0 failed |
| `housekeeping probe` | no drift found |
| `requirement-coverage.cs --check` / `--check-section12` / self-test | no finding in a MUST row; section 12 current; self-test ok |
| NativeAOT publish and run | no warning; `aot-smoke: all checks passed` |
| `scripts/knowledge verify-structure` | OK: 3 346 harvested entries, 66 notes |

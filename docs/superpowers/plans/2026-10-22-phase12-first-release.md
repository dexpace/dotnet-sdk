# Phase 12 — First Release: Implementation Plan

**Status:** Draft, for review. Written 2026-10-22 against `main` at `6b49af6` (phases 0 to 11 merged), on branch `107-phase-12-first-release` at tip
`1b6b6c9` (the design commit), for issue #107. Revised 2026-10-22 after review (ParamName helper sites, signing scope, fence isolation, HEAD-only reproducible-pack, and smaller corrections). Design: [phase 12 design](../specs/2026-10-22-phase12-first-release-design.md) (filed by task 5.5 to
`docs/work/mvp/phase12/2026-10-22-phase12-first-release-design.md`), the authority for every decision below. The plan cites its facts (`V1`..`V20`),
decisions (`P12-1`..`P12-26`) and ledger entries (`P12-D1`..`P12-D4`) rather than restating them. Format precedent: the phase 11 plan
(`docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation.md`).

**What this document is.** Roadmap step 3 for phase 12 (one phase, no sub-phases, P12-1): numbered tasks in five stacked pull requests, each with its
files, the failing test (name, project, `[Trait]`, why it is red today) or, where a task cannot have one, the pin that guards it, the change, the
`PublicAPI` diff where there is one, the verify commands, the requirement IDs and the commit message. It is not the checklist (task 5.2 writes it from what
was built). The [traceability section](#traceability) maps every decision, fact, blocker, carried-in item and exit criterion to a task.

**Scope.** The V6 reproducibility fix and its gate (first), the `ParamName` completion, the one-time surface move, strong naming, symbol packages, the
version bump, the release preflight and the inert `release.yml`, the documentation set and housekeeping's fence compiler, and the close-out. **The first
publish is held** (the lead's stance, quoted in the design's *Prerequisites*): no task here publishes, tags, pushes or calls `gh`.

**Order and gates.** Five stacked pull requests, each green on its own (P12-1): **PR 1** determinism and `ParamName` (1.1 to 1.3, then the contingent
tasks 1.C1 to 1.C3 that the lead's CI read can trigger); **PR 2** the surface move, strong naming, symbols, version (2.1 to 2.5); **PR 3** the release
path (3.1 to 3.3); **PR 4** documentation and the fence compiler (4.1 to 4.9); **PR 5** close-out (5.1 to 5.6). Task 0.1 is a pre-flight with no commit;
task 0.2 is the lead's CI read. Proposed branches, each off its predecessor, the lead's to name finally: `107-phase-12-first-release` (PR 1),
`107-phase-12-surface-and-signing`, `107-phase-12-release-path`, `107-phase-12-docs`, `107-phase-12-close-out`.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the named test; run it and **see it fail for the stated reason** (a compile error is the expected red only for a new
   public type); make the minimal change; see it pass; run the group's gate. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the line it pins (never committed; `git checkout -- <file>`). A task that cannot have a failing test
   (documentation, a constant, a pure move) says so and names its pin or check. **No commit is red**: a task that changes behaviour edits every caller and
   every test that asserted the old behaviour in the same commit, and the plan names them.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`; `LicenseHeaderTests` also scans `scripts/` and `.claude/`), uses its project's
   `GlobalUsings.cs` (`ImplicitUsings` off), one type per file, file-scoped namespace. Every new `.sh` carries the same header as `#` comments.
3. **`src/` code:** `ConfigureAwait(false)` on every `await` (`CA2007`); methods at most 70 lines (`MA0051`); `///` on every public member (`CS1591`); every
   broad `catch` filters `when (!ExceptionFacts.IsFatal(ex))` or ends in `throw;` (`FatalFilterArchitectureTests`); no new `#pragma` without a why-comment
   (`AnalyzerWaiverTests`); no new `PackageReference` anywhere (the dependency audit); `IsTrimmable`/`IsAotCompatible` stay clean. Phase 12 touches `src/`
   in two places only (task 1.1, task 1.2) and `Directory.Build.props` and five `.csproj` files (tasks 2.1 to 2.3).
4. **Tests.** `[Trait("Category", …)]` on every class in `tests/` (`Unit` for the gates and scans below); tokens from
   `TestContext.Current.CancellationToken`; xUnit `Assert` only; no wall-clock sleep. The housekeeping tool's tests carry no trait (existing convention).
5. **`Security` tests are never deleted or loosened.** Phase 12 adds none and edits none. Gate: `git diff --stat main...HEAD -- '**/Security/*.cs'` is empty.
6. **`PublicAPI`.** No task changes a public member (task 1.2 changes an exception's `ParamName`, not a signature). The only `PublicAPI.*` edit is the
   move of task 2.4. If a contingent task (1.C1 to 1.C3) does change a signature, it is an `Unshipped` line and the task says so; RS0016/RS0017 are the
   authority. After 2.4 `Unshipped` is `#nullable enable` alone at the phase's exit: any `Unshipped` line a contingent fix adds after the move of 2.4 is
   folded into `Shipped` (appended in order, `Unshipped` back to the header alone) before task 5.6, in the contingent task's own commit or a `chore:` commit
   of its own, and the checklist (5.2) records it.
7. **Breaking changes** get a `CHANGELOG.md` `[Unreleased]` line **in the same commit** (constraint 8), under a `### Phase 12 — first release` heading
   (created by the first task that needs it, 1.1). Task 1.2 is the one **Breaking** line; strong naming (2.1) is an identity change recorded as *Changed*,
   not Breaking, because nothing is published.
8. **Commits** follow the repository style (`fix:`, `feat:`, `test:`, `docs:`, `ci:`, `chore:`). No AI attribution. **The plan authorises no `git push`,
   no `gh` command and no remote action.** Every action that needs one is a *lead action* (L1 to L9, listed in [Lead actions](#lead-actions)), never an
   implementer step. The implementer stops before any push and hands the lead the branch names.
9. **Shared files** (`CHANGELOG.md`, `CLAUDE.md`, the roadmap, `docs/first-release.md`, `.github/workflows/ci.yml`, `docs/README.md`, `README.md`,
   `Directory.Build.props`, `docs/deviations.md`): re-derive each hunk from the merged file; never resolve a conflict by keeping a side whole.
10. **Conformance.** Phase 12 adds no assertion and edits no waiver, so no negative control is added; the gate is that
    `git diff main...HEAD -- tests/**/ConformanceWaiver* src/Dexpace.Sdk.Conformance/Assertions` is empty and every driver's pinned waiver and
    `NotExercised` list is unchanged (V-conf).
11. **Lock files.** No package reference changes. After any edit of `Directory.Build.props` or a `.csproj`, run `dotnet restore Dexpace.Sdk.sln
    --locked-mode` and `dotnet restore tools/Dexpace.Tools.sln --locked-mode`; a changed `packages.lock.json` is committed in the same commit (none is
    expected, and one appearing is a stop).
12. **The ledger cites lines.** `docs/deviations.md` cites `path:line` and `LedgerAuditTests` (L1 to L11) fails a line past the end of its file; it does not
    check that a cited line still holds what the ledger says. A task that moves a cited line re-derives the citation by hand in the same commit. The cited
    lines this plan can move: `HeaderDropLog.cs:123` (task 1.1, `docs/deviations.md` lines 55 and 189) and `RetryOptions.cs:60`, `:178`, `:225` and
    `SystemNetHttpClientOptions.cs:116` (task 1.2 keeps its edits line-neutral, including its fourteen one-line `ThrowIf*` edits, `RetryOptions.cs:60` among them; the verify step greps the ledger).
13. **Frozen documents get dated corrections.** `docs/sdk-design-dotnet/` and `docs/product-spec/` are never rewritten; a change is a dated correction
    (`**Dated correction, 2026-10-22 (phase 12, P12-n).**`). `docs/knowledge/notes/` is hand-written and append-only; `harvested/` is never touched.
    `docs/first-release.md` keeps its `### Unsatisfied MUSTs` heading and list shape (`LedgerAudit` rule L6, V17).
14. **Housekeeping.** Both inbox documents (design and this plan) are filed by task 5.5 with `apply --delivery mvp --phase 12`; citations point at the
    `docs/work/mvp/phase12/` path, never the staging path.

### Environment and command shorthands

`dotnet` 10.0.401 (`global.json`), `net10.0`, from the repository root (the worktree). On the authoring machine the SDK is not on `PATH`:
`export DOTNET_ROOT=/home/mohammad/.dotnet` then, on its own line, `export PATH="$DOTNET_ROOT:$PATH"`. Microsoft.Testing.Platform: build first, then
`dotnet test … --no-build`; never pass MSBuild flags to `dotnet test`; a run reporting zero tests is a failed command. Throwaway probes live in
`/tmp/claude-1000/-home-mohammad-Projects-dexpace-dotnet-sdk/516317be-979f-4125-9fb9-f7dc6a42d0a7/scratchpad/p12-plan/` (**S** below), never in the tree.

- **V-build** = `dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers`
- **V-core `<Class>`** = `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --no-build --filter-class "*<Class>"`; **V-sn**,
  **V-di**, **V-stj**, **V-conf-tests** the same over `tests/Dexpace.Sdk.Http.SystemNet.Tests`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests`,
  `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests`, `tests/Dexpace.Sdk.Conformance.Tests`
- **V-conf** = `--filter-trait "Category=Conformance"` over Core, SystemNet, DI, STJ and Conformance.Tests
- **V-sec** = `--filter-trait "Category=Security"` over Core, SystemNet, DI
- **V-fmt** = `dotnet format Dexpace.Sdk.sln --verify-no-changes`
- **V-tools** = `dotnet restore tools/Dexpace.Tools.sln --locked-mode`; `dotnet build tools/Dexpace.Tools.sln --configuration Release -m:3 -nr:false
  --disable-build-servers`; `dotnet format tools/Dexpace.Tools.sln --verify-no-changes`; `dotnet test --solution tools/Dexpace.Tools.sln --configuration Release --no-build`
- **V-gate** = `dotnet restore Dexpace.Sdk.sln --locked-mode`; V-build; V-fmt; `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build --coverlet
  --coverlet-output-format cobertura --results-directory artifacts/test-results`; `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80`;
  `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`; `dotnet pack Dexpace.Sdk.sln -c Release --no-build --output artifacts/packages`;
  `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages`; `scripts/ci/dependency-audit-selftest.sh artifacts/packages`;
  `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages`; from 3.1 `dotnet run scripts/ci/release-preflight.cs -- artifacts/packages` and
  `scripts/ci/release-preflight-selftest.sh artifacts/packages`; `scripts/ci/reproducible-pack.sh artifacts/reproducible` and `--self-test
  artifacts/reproducible-selftest` (from a clean tree); `dotnet run scripts/ci/requirement-coverage.cs -- --check` and `-- --check-section12`;
  `scripts/ci/requirement-coverage-selftest.sh`; the NativeAOT publish and run (`dotnet publish tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj -c Release --output
  artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke`); V-tools; from 4.1 `dotnet run --project .claude/skills/housekeeping/src -- fences`;
  `scripts/knowledge verify-structure`; the housekeeping `probe`; the Security diff check (convention 5)

---

## Task 0.1 — Pre-flight (no commit)

1. **State.** `git -C <worktree> status --short` is empty; `git log -1 --oneline` is `1b6b6c9`; the branch is `107-phase-12-first-release`. Read the design in full.
2. **Knowledge queries** (start of the phase and of every numbered task): `scripts/knowledge --origin note --brief`; `scripts/knowledge --section conflicts
   --brief`; `scripts/knowledge --prefix-info NFR`; `scripts/knowledge --req NFR-4,NFR-12,NFR-16,NFR-17`.
3. **Baseline (V1).** V-build (0 warnings, 0 errors); the full test run (7 687 tests, 7 664 passed, 23 skipped, 0 failed at `6b49af6`; the number rises as
   this phase adds tests, so no later task pins a total); V-conf with each driver's pinned waivers and `NotExercised` lists;
   `requirement-coverage.cs --report` (MUST N/A 22, ⏳ 1, ✅ 505, 🚫 4; SHOULD ⏳ 1; MAY ⏳ 3); the housekeeping probe ("no drift found").
4. **Re-verify the design's facts** as throwaway probes in **S** (a fact that differs is a **stop**: record it and amend the affected task before writing
   code):
   - **V6**: eight builds of `src/Dexpace.Sdk.Http.SystemNet` with `-p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=$S/gen$i
     --no-incremental -nr:false --disable-build-servers`, then `find $S -name LoggerMessage.g.cs | xargs sha256sum | sort | awk '{print $1}' | uniq -c`.
     Expected today: two distinct hashes (6:2), identical but for the order of `HeaderDropLog` and `ProxyWarnings`.
   - **V3**: `wc -l src/*/PublicAPI.Unshipped.txt` (1 370, 476, 40, 28, 13) and `wc -c src/*/PublicAPI.Shipped.txt` (DI's is 0 bytes).
   - **V8**: `grep -rn "IncludeSymbols\|SymbolPackageFormat\|snupkg"` over `Directory.*`, `src/**/*.csproj`, `scripts/` (only `reproducible-pack.sh` and the audits'
     globs).
   - **V9 and V10**: in a copy of the tree under **S**, generate a key (task 2.1 gives the blob layout), set `SignAssembly` with the `Key` metadata, and
     confirm `CS1726` without it, a clean build with it, and `CS0281` for a `CALG_RSA_KEYX` blob.
   - **V11**: `grep -rn "nameof(value)" src --include='*.cs'` and, for the sites that grep cannot see (design amendment to V11),
     `grep -rnE "ThrowIf(Negative|LessThan|LessThanOrEqual|NullOrWhiteSpace|NullOrEmpty)\(value[,)]" src --include='*.cs'` (the files are listed in task 1.2; the second grep finds exactly 14 lines, all accessors).
   - **V15**: count ` ```csharp ` fences in `README.md`, `docs/sdk-documentation/*.md`, `src/*/README.md` (71) and those naming `Dexpace.Sdk` (7).
   - **V19**: `cat nuget.config`; `git remote -v` (visibility is not readable, and is not read).
5. Confirm the sibling sources task 4.5 ports exist as git objects (a local read, not a remote one):
   `git -C /home/mohammad/Projects/dexpace/nodejs-sdk show origin/main:docs/sdk-documentation/write-a-transport.md` (and the other three `write-a-*`), and Ruby's
   `architecture.md`, `quality-gates.md`.

## Task 0.2 — Lead action L1: the first CI read (P12-21)

**Not an implementer step.** The tree has never run in CI (V2: `6b49af6` was admin-merged before CI). The pull request of PR 1 is the first run. The lead,
after pushing PR 1, reads it and pastes into the checklist (task 5.2), in this order: (a) all five jobs — `build & test` on ubuntu, windows and macos,
`reproducible packages`, `NativeAOT smoke` — green or red, with the `reproducible-pack:` lines of the `reproducible packages` job if red (open question 3 of
the design: any differing entry other than `Dexpace.Sdk.Http.SystemNet.dll`); (b) the macOS row's `FileRequestBodyTests` results (carried-in item "macOS
`FromFile`"); (c) any `InstrumentationContextTests` / `OperationSpanLifecycleTests` failure (carried-in "Core flake family"); (d) any other OS-specific red;
(e) whether the repository is public (V19, P12-13's fallback). Then, after the merge, the first `main` run. **Each red found there triggers the matching
contingent task 1.C1 to 1.C3 on PR 1's branch before PR 2 is cut.** If the lead cannot read a run before PR 2 is due, PR 2 proceeds (reading R2) and the
checklist records "not read".

---

# PR 1 — Determinism and `ParamName`

## Task 1.1 — One `[LoggerMessage]` type per assembly (P12-5; V6, V7) — the first code task

**Files.** New `tests/Dexpace.Sdk.Core.Tests/Architecture/LoggerMessageDeterminismArchitectureTests.cs` and `LoggerMessageScanner.cs` (beside
`FatalFilterScanner.cs`); new `src/Dexpace.Sdk.Http.SystemNet/TransportLog.cs`; `src/Dexpace.Sdk.Http.SystemNet/HeaderDropLog.cs`,
`src/Dexpace.Sdk.Http.SystemNet/ProxyWarnings.cs`; `docs/deviations.md` (lines 55 and 189); `CHANGELOG.md`.

**Test first.** `LoggerMessageDeterminismArchitectureTests` (Core.Tests, `[Trait("Category", "Unit")]`), a source scan in the style of
`FatalFilterArchitectureTests`, over the five libraries under `src/` (skipping `obj`/`bin`):
- `Each_library_has_at_most_one_type_with_generated_log_methods` — for every library, the set of types that contain a `[LoggerMessage(` attribute site has at
  most one member. **Red today:** `Dexpace.Sdk.Http.SystemNet` yields `HeaderDropLog` (`HeaderDropLog.cs:123`, `:129`, `:136`) and `ProxyWarnings`
  (`ProxyWarnings.cs:60`); the failure message names both and says why (the generator orders classes nondeterministically, V6).
- `The_scan_sees_the_real_generator_sites` — non-vacuous: Core's `ProxyResolutionLog` (`Internal/ProxyResolutionLog.cs:36`) is found, and SystemNet has
  at least one site.
- `The_scanner_flags_two_types_and_ignores_define_and_comments` — a `[Theory]` over inline snippets: two classes each with an attribute (flagged); one class
  with three attributes (clean); `LoggerMessage.Define<…>(` (not a site); an attribute inside a `//` or `///` line (not a site); a `partial class` split across
  two snippets of one name (one type).
The scanner's rule (reading R4): an attribute site is a line whose first token is `[LoggerMessage(`; its type is the nearest preceding `class`/`struct`/`record`
declaration outside a comment line (one type per file is the repository convention). `LoggerMessage.Define` sites are not generator input and are not counted.

**Change.**
- New `internal static partial class TransportLog` in `TransportLog.cs` (MIT header, `namespace Dexpace.Sdk.Http.SystemNet`), holding all four generated
  methods as `internal static partial void`: `Dropped(ILogger, LogLevel, string headerName)` (event `TransportHeaderDropped`), `FramingDropped(ILogger,
  string)`, `InboundDropped(ILogger, string)` and `ProxyFeatureUnsupported(ILogger, string feature, string member)`. Move the four `[LoggerMessage(...)]`
  attributes with their `EventId`, `EventName`, `Level` and `Message` text **unchanged**, and the SYSLIB1006 comment (one method serves both levels of the
  request-header drop, `HeaderDropLog.cs:39-40`) with them. **Moves:** the four attributes with their declarations and that two-line comment. **Stays in
  `HeaderDropLog`:** `s_warning` and `s_quiet` (`:41-42`), which are only re-pointed at `TransportLog.Dropped`. Reading R3: the design's "one `static partial` class" is a new class, because `HeaderDropLog`
  is an instance class and `ProxyWarnings` a static one.
- `HeaderDropLog`: drop `partial` from the declaration; `s_warning`/`s_quiet` call `TransportLog.Dropped(logger, LogLevel.Warning|Debug, headerName)`; the
  framing and inbound callers call `TransportLog.FramingDropped` / `InboundDropped`; delete the three generated-method declarations.
- `ProxyWarnings`: drop nothing but the generated declaration; `Log` calls `TransportLog.ProxyFeatureUnsupported`.
- `docs/deviations.md` lines 55 and 189 cite `HeaderDropLog.cs:123` as a `[LoggerMessage]` example (design §10 entry 22): repoint both to
  `src/Dexpace.Sdk.Http.SystemNet/TransportLog.cs:<line of the first attribute>`, because the file shrinks below line 123 and L3 would fail.
  `grep -rn "HeaderDropLog.cs:\|ProxyWarnings.cs:" docs README.md CLAUDE.md` must find nothing else stale.
- `CHANGELOG.md`: a `### Phase 12 — first release` heading with a **Fixed** line: the `reproducible packages` job's cause (V6), no behaviour change.
No test asserts the old structure: `HeaderDropLogTests` and `ProxyAdapterTests` (SystemNet.Tests) call `HeaderDropLog` and `ProxyWarnings.Emit` only and
assert event ids, names and messages, which are unchanged.

**Verify.** V-build; V-core `LoggerMessageDeterminismArchitectureTests`; `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests -c Release --no-build`
(whole project); V-conf-tests `LedgerAuditTests`; V-fmt. **The fix is judged by the generator's output, not by a run count (V7, reading R5):** repeat the
task-0.1 probe: eight builds must give **one** distinct `LoggerMessage.g.cs` hash (before: two). Then **commit, then run** `scripts/ci/reproducible-pack.sh
artifacts/reproducible` three times as a smoke only: the script packs two `git worktree` checkouts of committed `HEAD` (its header, lines 9-10), never the
working tree, so an uncommitted fix is not what it measures.
**IDs.** `NFR-12` (build half). `TRANSPORT-12`, `TRANSPORT-13`, `TRANSPORT-30`, `OBS-19`, `OBS-20` are untouched (event ids and messages unchanged).
**Commit.** `fix: one [LoggerMessage] type per assembly so the packs are reproducible (NFR-12)`.

## Task 1.2 — Validating `init` accessors name their property for range and format rejections (P12-14; V11 and its amendment)

**Files.** `src/Dexpace.Sdk.Core/Configuration/ProxyOptions.cs`, `RetryOptions.cs`, `DexpaceClientOptions.cs`, `HttpLoggingOptions.cs`, `RedirectOptions.cs`;
`src/Dexpace.Sdk.Core/Http/Request/RequestOptions.cs`; `src/Dexpace.Sdk.Core/Auth/AuthRequirement.cs`, `AccessTokenCache.cs`, `AuthCredentials.cs`;
`src/Dexpace.Sdk.Http.SystemNet/SystemNetHttpClientOptions.cs`; `src/Dexpace.Sdk.Conformance/Subjects/TransportSubject.cs`, `CodecSubject.cs`, `InvariantSubject.cs`,
`PackagingSubject.cs`, `SuiteOptions.cs`, `TransportSuiteOptions.cs`; `tests/Dexpace.Sdk.TestSupport/Source/InitAccessorScanner.cs`; new
`tests/Dexpace.Sdk.Core.Tests/Configuration/InitAccessorRejectionParamNameTests.cs`; `tests/Dexpace.Sdk.Core.Tests/Configuration/InitAccessorParamNameTests.cs` (coverage test
re-pointed, scanner fixtures added); `tests/Dexpace.Sdk.Http.SystemNet.Tests/InitAccessorParamNameTests.cs` (rows added); the two tests that pin the defect,
`tests/Dexpace.Sdk.Core.Tests/Configuration/RedirectOptionsTests.cs:31` and `HttpLoggingOptionsTests.cs:57`; `CHANGELOG.md`.

**The defect has two spellings (the design's V11 amendment).** (a) `nameof(value)` passed to an exception, which V11 listed; (b) a framework throw helper called on `value`,
`ArgumentOutOfRangeException.ThrowIf*(value…)` and `ArgumentException.ThrowIfNullOrWhiteSpace(value)`, which name the exception after the argument *expression* (`CallerArgumentExpression`),
so `ParamName` is `"value"` and a `nameof(value)` grep cannot see them. Fourteen accessors are in (b): `HttpLoggingOptions.cs:57` (`BodyPreviewSize`), `RedirectOptions.cs:45`
(`MaxRedirects`), `RetryOptions.cs:60` (`MaxRetryAttempts`), `SystemNetHttpClientOptions.cs:121` (`ResendBufferLimit`), `AccessTokenCache.cs:72` (`RefreshMargin`), `AuthCredentials.cs:39`
(`TokenRefreshMargin`), and in the kit `CodecSubject.cs:29` (`Name`), `InvariantSubject.cs:49` (`Name`), `PackagingSubject.cs:19` (`Name`), `:41` (`CoreId`), `:63` (`DeclaredFloor`),
`TransportSuiteOptions.cs:32` (`AssertionTimeout`), `:47` (`ReleaseTimeout`), `SuiteOptions.cs:35` (`AssertionTimeout`). The five `ThrowIfNullOrWhiteSpace(value)` sites also report `"value"` for a
**null** (an `ArgumentNullException`), which phase 11's null table did not see.

**Tests first.**
- **The scanner learns spelling (b) first** (`InitAccessorScanner`, TestSupport, which today recognises three null spellings). Inside an accessor body it also recognises
  `ArgumentOutOfRangeException.ThrowIf*(value…)`, `ArgumentException.ThrowIfNullOrWhiteSpace(value)` / `ThrowIfNullOrEmpty(value)` and `new Argument*Exception(…, nameof(value))` (the last
  is `TransportSubject.cs:41`). A site's `NamedParameter` is the `nameof(X)` it passes, else `null` (`NamesItsProperty` is unchanged); `Site` gains a `Kind` (`Null` for the existing
  spellings and the null half of `ThrowIfNullOrWhiteSpace`, `Rejection` for the rest). A call whose first argument is not `value` is an element and keeps the existing `Element` rule. The
  scanner's own fixtures in `InitAccessorParamNameTests.The_scanner_flags_a_bare_ThrowIfNull_and_ignores_a_method_parameter` gain one snippet per new spelling (flagged) and a method whose
  parameter is `value` (ignored). **Red today:** `Every_validating_accessor_in_the_libraries_names_its_property` now reports every site of (b) and `The_table_covers_every_validating_accessor`
  reports the sites with no row; this replaces the file-level `nameof(value)` allow-list the first draft of this task had (reading R6), so a future accessor of either spelling is caught by
  the scan, not by a list of nine file names.
- `The_table_covers_every_validating_accessor` is re-pointed: `Null` sites must equal the null table's keys (today's equality, unchanged), and every `Rejection` site's key must be a key of the
  rejection table below (the rejection table may hold more rows than the scan sees; see the helper rows).
- `InitAccessorRejectionParamNameTests.A_rejected_value_names_the_property` (Core.Tests, `Unit`): a table keyed `Type.Property` (a ` (null)` suffix selects a null row, stripped before the
  comparison) → an action that sets a rejected value, asserting `Assert.ThrowsAny<ArgumentException>` and `ParamName == Property`. The scanner-visible rows (a) and (b):
  `ProxyOptions.Type` (undefined enum), `.Host` (blank), `.Port` (70000); `RetryOptions.Multiplier` (0.5), `.Jitter` (1.5), `.RetryableStatusCodes` ({200}), `.AttemptHeaderName` (twice: a
  non-token, and `Idempotency-Key`), `.MaxRetryAttempts` (-1); `AuthRequirement.Scopes` (a list holding a `null`); `TransportSubject.Name` (blank); `HttpLoggingOptions.BodyPreviewSize` (-1);
  `RedirectOptions.MaxRedirects` (-1); `AccessTokenCache.RefreshMargin` (negative, over a stub `TokenCredential`); `AuthCredentials.TokenRefreshMargin` (negative);
  `CodecSubject.Name`, `InvariantSubject.Name`, `PackagingSubject.Name`, `.CoreId`, `.DeclaredFloor` (each blank, and ` (null)`); `TransportSuiteOptions.AssertionTimeout`, `.ReleaseTimeout`
  and `SuiteOptions.AssertionTimeout` (zero). The helper-routed rows the scanner cannot see are kept as behavioural rows: `RetryOptions.BaseDelay`, `.MaxDelay`, `.FixedDelay` (negative; `RequireDuration`),
  `RequestOptions.Timeout` (zero; `RequireNullOrPositive`), `.MaxRetries` (-1; `RequireNullOrNonNegative`), `DexpaceClientOptions.BaseAddress` (a relative URI; `RequireUsable`), `.OverallTimeout`
  and `.AttemptTimeout` (zero; `RequireTimeout`). **Red today:** every row reports `"value"`.
- `InitAccessorParamNameTests` (SystemNet.Tests) gains `A_rejected_value_names_the_property` rows `SystemNetHttpClientOptions.Timeout` (zero), `.HeaderDropLogging` (undefined enum) and
  `.ResendBufferLimit` (-1). **Red today:** `"value"` (`SystemNetHttpClientOptions.cs:85`, `:101`, `:121`). Its coverage test applies the same `Null`/`Rejection` split to the transport tree.
- The two existing tests that assert the defect are edited **in this commit**: `RedirectOptionsTests.cs:31` (`Assert.Equal("value", ex.ParamName)` becomes `"MaxRedirects"`) and
  `HttpLoggingOptionsTests.cs:57` (`Assert.Equal("value", thrown.ParamName, ignoreCase: true)` becomes `"BodyPreviewSize"`). `grep -rn '"value"' tests --include='*.cs'` found no other test
  that pins an accessor (the other hits are `Parse`/`ServerSentEvent`/`Tristate`/`QuerySplice` methods whose parameter is `value`).

**Change.** Line-neutral where possible (convention 12): pass the property name instead of `nameof(value)` / the implicit expression, on the same line.
- `ProxyOptions.cs:53` `nameof(Type)`, `:69` `nameof(Host)`, `:82` `nameof(Port)`.
- `RetryOptions.cs:91` `nameof(Multiplier)`, `:125` `nameof(Jitter)`, `:204` `nameof(RetryableStatusCodes)`, `:232` and `:237` `nameof(AttemptHeaderName)`. The helper
  `RequireDuration(TimeSpan value)` (`:289`) becomes `RequireDuration(TimeSpan value, string paramName)`, throwing with `paramName`; its callers (`:75`
  `BaseDelay`, `:108` `MaxDelay`, `:144` `FixedDelay`, where the local is `fixedDelay`) pass `nameof(BaseDelay)`, `nameof(MaxDelay)`, `nameof(FixedDelay)`. **`:60` stays on `:60`:**
  `ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MaxRetryAttempts));` (`docs/deviations.md` line 294 cites `RetryOptions.cs:60` as `ThrowIfNegative`).
- `RequestOptions.cs`: `RequireNullOrPositive(TimeSpan? value, string paramName)` (`:144`, caller `:36` `Timeout`) and `RequireNullOrNonNegative(int? value, string
  paramName)` (`:154`, caller `:47` `MaxRetries`).
- `DexpaceClientOptions.cs` (reading R6: not in V11's list, same defect): `RequireUsable(Uri? value, string paramName)` (`:165`, caller `:52` `BaseAddress`) and
  `RequireTimeout(TimeSpan? value, string paramName)` (`:186`, callers `:90` `OverallTimeout`, `:115` `AttemptTimeout`).
- `AuthRequirement.cs:57` `nameof(Scopes)`; `SystemNetHttpClientOptions.cs:85` `nameof(Timeout)`, `:101` `nameof(HeaderDropLogging)`; `TransportSubject.cs:41`
  `nameof(Name)`.
- The fourteen of spelling (b), each a one-line edit of the `ThrowIf*` call: add `nameof(<Property>)` as the `paramName` argument (`ThrowIfNegative(value, nameof(MaxRedirects))`,
  `ThrowIfLessThan(value, TimeSpan.Zero, nameof(RefreshMargin))`, `ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(AssertionTimeout))`, `ThrowIfNullOrWhiteSpace(value, nameof(CoreId))`), at
  the lines listed above. `SystemNetHttpClientOptions.cs:116` (cited by the ledger) is the property's declaration line above `:121`; both stay put.
- `CHANGELOG.md`: one **Breaking** line — a rejected range or format value now reports the property in `ParamName` (was `"value"`); lists the properties.

**Verify.** V-build; V-core `InitAccessorRejectionParamNameTests`, `InitAccessorParamNameTests`, `RetryOptionsTests`, `RequestOptionsTests`, `ProxyOptionsTests`,
`DexpaceClientOptionsTests`, `RedirectOptionsTests`, `HttpLoggingOptionsTests`; V-sn `InitAccessorParamNameTests`; V-conf-tests `LedgerAuditTests`; `grep -rnE "ThrowIf(Negative|LessThan|LessThanOrEqual|NullOrWhiteSpace|NullOrEmpty)\(value[,)]" src --include='*.cs'`
finds nothing (before the change: exactly the 14 sites); `grep -n "RetryOptions.cs:\|SystemNetHttpClientOptions.cs:" docs/deviations.md` and confirm each cited line
still holds the declaration it cites (`public IReadOnlyList<string> PacingHeaders` at `:178`, `public string?
AttemptHeaderName` at `:225`, `ThrowIfNegative` at `:60`, `public long ResendBufferLimit` at `:116`); V-fmt.
**IDs.** `SEAM-29`, `HTTP-2` to `HTTP-4` (phase 11 task 2.5's family); `CFG` options validation untouched.
**Breaking.** `ParamName` of the listed rejections. **Commit.** `fix: validating init accessors name their property for range and format rejections`.

## Task 1.3 — PR 1 gate

V-gate (every item that exists at this point; the preflight, fences and release rows are not yet built); the Security diff (convention 5);
`git diff main...HEAD -- src/*/PublicAPI.*` is empty. Hand the lead the branch name and the L1 request (task 0.2). **No push.**

## Contingent tasks 1.C1 to 1.C3 (P12-22) — executed only if L1 shows them

Each is TDD and lands on PR 1's branch before PR 2 is cut (a surface-touching one must precede task 2.4; otherwise it is a reviewed `Unshipped` diff, reading
R2). If L1 reports nothing, there is no commit and the checklist records "not triggered". None is ever a skip, a retry attribute or a waiver.

- **1.C1 — macOS `FromFile` (carried-in).** Trigger: a red `FileRequestBodyTests` (`tests/Dexpace.Sdk.Core.Tests/Http/Request/FileRequestBodyTests.cs`, for example
  `A_fifo_is_rejected_at_construction_without_blocking`, `A_fifo_the_process_may_not_open_is_rejected_by_its_kind_and_not_its_path`) or a `UnixFileKind`
  classification miss on the macOS row. **Test first:** the macOS log's failing test is the evidence; add a platform-independent unit test of
  `UnixFileKind.IsUnsupportedFileType` (`src/Dexpace.Sdk.Core/IO/UnixFileKind.cs:48`) fed the exact exception type and message the log shows, red until the
  classifier recognises it. **Change:** the classifier, nothing wider. `FromFile`'s refusal is part of the frozen surface (P12-15), so this precedes 2.4.
  **Commit.** `fix: classify the macOS special-file failure in UnixFileKind (BODY-11)`.
- **1.C2 — the Core flake family (carried-in).** Trigger: a red `InstrumentationContextTests` or `OperationSpanLifecycleTests` (V12: not reproduced locally in ten
  runs). **Test first:** the CI log's test; find the cross-test leak (a process-wide `ActivityListener` seen by a parallel class) and write a deterministic
  reproducer (a second class that holds a recording listener while the first runs), red before the fix. **Change:** join the leaking class to the
  `NoDiagnosticListeners` collection or scope its listener with `ActivityRecorder.Scoped` (5c convention), or fix the production race; never a retry.
  **Commit.** `test: isolate <class> from process-wide activity listeners`.
- **1.C3 — any other OS-specific red, or another differing entry in the `reproducible packages` log (V6 risk).** Trigger: a red row that 1.C1/1.C2 do not cover, or a
  `reproducible-pack:   entry … differs` line naming anything but the SystemNet DLL. **Test first:** the log is the evidence; reduce it to a unit test or a
  `reproducible-pack.sh` self-test case. **Change:** the fix the log dictates, recorded in the checklist. **Commit.** `fix: <cause> (<ID>)`.

---

# PR 2 — The one-time surface, strong naming, symbols, version

## Task 2.1 — Strong-name the five libraries and their friend tests (P12-9, P12-10; V9, V10)

**Files.** New `Dexpace.Sdk.snk` (binary, repository root); `.gitattributes`; `Directory.Build.props`; the five `src/*/*.csproj` (`InternalsVisibleTo`); new
`tests/Dexpace.Sdk.Conformance.Tests/Gates/StrongNameKeyTests.cs`; `CHANGELOG.md`.

**Test first.** `StrongNameKeyTests` (Conformance.Tests/Gates, `Unit`), reading the repository through `RepositoryRoot`:
- `The_public_key_property_is_the_key_the_assemblies_are_signed_with` — parses `DexpacePublicKey` (hex) from `Directory.Build.props` and compares it with the public key
  of **each of the five built libraries** (P12-10: "each built library"), read with `AssemblyName.GetAssemblyName` on `src/*/bin/Release/net10.0/<Id>.dll` (no project reference, so the
  three libraries this project cannot reference are covered too; the test fails with a message to build Release first if a file is missing, never skips), and with this test assembly's own
  key (the friend test assemblies are signed, V9). **Red today:** the property is absent and the assemblies carry no key.
- `The_key_is_a_signing_key` — the first four bytes are `00 24 00 00` (`CALG_RSA_SIGN`; the `CALG_RSA_KEYX` trap of V10 is `00 A4 00 00` and gives `CS0281`).
- `The_public_key_token_is_the_documented_one` — SHA-1 of the key, last eight bytes reversed, equals a literal in the test (the token the docs quote); a key
  rotation is therefore a visible test edit.
- `Every_internals_visible_to_item_carries_the_key` — each of the five `.csproj` files has `Key="$(DexpacePublicKey)"` on every `InternalsVisibleTo` item
  (`CS1726` otherwise). **Red today.**
- `The_scan_can_fail` — inline mutations (a csproj with a keyless item; a property with `00 A4 00 00`) are flagged.

**Change.**
1. **Generate the key once**, as a throwaway program in **S** (not committed; reading R7): a 2048-bit RSA key, written as a strong-name `.snk`
   (PRIVATEKEYBLOB: `07 02 00 00`, `00 24 00 00` (`CALG_RSA_SIGN`), `52 53 41 32` (`RSA2`), bit length, public exponent, then modulus, P, Q, DP, DQ,
   InverseQ and D, all little-endian, from `RSA.ExportParameters(true)`) and its public key (`00 24 00 00 04 80 00 00`, length, then PUBLICKEYBLOB `06 02 00 00
   00 24 00 00 52 53 41 31`, bit length, exponent, modulus). The design's "160-byte" figure (V9) is the probe's 1024-bit key; the size is the plan's choice.
   Commit `Dexpace.Sdk.snk`. The public key is not a secret (design §9.2).
2. `.gitattributes`: `*.snk binary` (the file has `* text=auto eol=lf`, which must not touch the key).
3. `Directory.Build.props`: in a new `Label="Strong naming"` group, `DexpacePublicKey` (the hex) unconditionally, and `SignAssembly` `true` with `AssemblyOriginatorKeyFile`
   `$(MSBuildThisFileDirectory)Dexpace.Sdk.snk` **only for projects under `src/` and `tests/`**, by a path test in the style of `DexpaceIsLibrary` (`Directory.Build.props:52`): a new
   `DexpaceIsSigned` property true when `MakeRelative` of the project directory starts with `src/` or `tests/`. (V9: the friend test assemblies must be signed, so `tests/` is in.) This is
   decided here, not left as a fallback (reading R8): the reviewer's probe of signing every project broke the tools solution with `CS1726` in `Housekeeping.Tests`, `Knowledge.Tests` and
   `KnowledgeHarvest.Tests`, because `tools/Knowledge/Knowledge.csproj`, `.claude/skills/housekeeping/src/Housekeeping.csproj` and `.claude/skills/knowledge-harvest/src/KnowledgeHarvest.csproj`
   carry keyless `InternalsVisibleTo` items and a signed friend demands a keyed grant. The tools are not packed, so they stay unsigned and keep their keyless grants. With the condition the
   reviewer measured the main solution, the NativeAOT smoke, packaging conformance, the dependency audit and the file-based `scripts/ci/*.cs` fine with signing on, and no
   `packages.lock.json` changed. `AssemblyVersion` stays the SDK default.
4. Each of the five library `.csproj`: `<InternalsVisibleTo Include="…Tests" Key="$(DexpacePublicKey)" />`.
5. `CHANGELOG.md`: *Changed* — the five assemblies are strong-named (`PublicKeyToken` in the line).

**Verify.** V-build (0 warnings: no `CS1726`, `CS8002`, `CS0281`); `StrongNameKeyTests`; `dotnet pack` then
`dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages` (8 of 8, V9: `nfr-3.no-internals-granted` compares only the name before the first
comma); **the tools solution (reading R8)** — V-tools, because `Directory.Build.props` reaches `tools/` and `.claude/skills/*`: it must build and test green **unsigned**, with no
`CS1726`, `CS8002` or analyzer finding (and `AssemblyName.GetAssemblyName` on a tools DLL shows no public key); the AOT publish and run; **commit, then run**
`scripts/ci/reproducible-pack.sh artifacts/reproducible` (signing is deterministic; the script packs committed `HEAD`, not the working tree, header lines 9-10); `git status` shows no
`packages.lock.json` change.
**IDs.** `NFR-16` (strong-naming half of design §9.2), `NFR-3` (the grant is still the library's own test project, by name). **No `PublicAPI` change.**
**Alternative if the lead declines (P12-9):** drop this task, the token check in 3.1, `StrongNameKeyTests`, and write §9.2's correction as "declined".
**Commit.** `feat: strong-name the five libraries with a committed key (NFR-16, design 9.2)`.

## Task 2.2 — Pack symbol packages (P12-11; V8)

**Files.** `scripts/ci/reproducible-pack.sh`; `Directory.Build.props` (the library-gates group).

**Test first.** Extend `reproducible-pack.sh --self-test` (the script's own regression check, as `coverage-gate-selftest.sh` is for coverage) with two cases:
every `.nupkg` of the self-test pack has a `.snupkg` beside it, and one flipped byte in a `.snupkg` of the copy fails the compare (exit 1, the `.snupkg` named).
**Red today:** `ls "$out/1"/*.snupkg` is empty, so the case prints `FAILED` and the self-test exits 1. The script already compares `*.snupkg` (`:132`).
**The red run uses committed `HEAD`:** the script packs two `git worktree` checkouts of `HEAD` (header lines 9-10, `pack_checkout`; `--self-test` too), but runs its own working-tree text. So
edit the script only, leave it uncommitted, run `--self-test` (it packs `HEAD`, which has no `IncludeSymbols`: red), then make the `Directory.Build.props` change and commit both together, and
run it again (green, on the commit that carries `IncludeSymbols`). No commit on the branch is red: a red-first commit of the case alone would fail the `reproducible packages` job's self-test step.

**Change.** `Directory.Build.props`, `Label="Library gates"`: `IncludeSymbols` `true`, `SymbolPackageFormat` `snupkg`. The audits ignore `.snupkg`
(`dependency-audit.cs:170` globs `{library}.*.nupkg`; `packaging-conformance.cs:49` reads `*.nupkg`; V8). Update the script's header comment ("`.snupkg` once symbols
are packed" becomes "`.snupkg`").
**Verify (after the commit; the script reads `HEAD`).** `scripts/ci/reproducible-pack.sh --self-test artifacts/reproducible-selftest` (all cases ok); `scripts/ci/reproducible-pack.sh artifacts/reproducible` (ten
files identical across two paths, symbols included — this is the V6 fix in action); `dotnet pack` leaves five `.nupkg` and five `.snupkg`; both audits green.
**IDs.** `NFR-12`. **Commit.** `feat: pack symbol packages for every library (NFR-12)`.

## Task 2.3 — The next version is `0.1.0` (P12-7; lead ruling, recommended default)

**Files.** `Directory.Build.props` (`VersionPrefix`, its comment); the version mentions in `README.md`, `CONTRIBUTING.md`, `SECURITY.md`; `CHANGELOG.md`.

**No failing test is possible** (a constant). **Pins:** the packaging suite's `NFR-14`/`NFR-15` assertions (version consistency of the nuspec and
`AssemblyInformationalVersion`) stay green over the repacked set, and five `Dexpace.Sdk.*.0.1.0-alpha.1.nupkg` appear; the preflight of 3.1 later holds the
version train. **Change.** `VersionPrefix` `0.1.0`, `VersionSuffix` `alpha.1` unchanged (the held release commit empties it). The three documents' "`0.0.1-alpha.1`" become
"`0.1.0-alpha.1`" (their wider stale text is task 4.7's). **Verify.** V-build; `dotnet pack`; packaging conformance; both restores, no lock-file diff;
`BuildInfoTests` (its `InlineData` is a string table, unaffected). **Alternative (publish `0.1.0-alpha.1` first):** this task is identical; only the held release
commit, the tag name and the register's wording differ (task 5.1). **Commit.** `chore: version 0.1.0-alpha.1 on main (P12-7)`.

## Task 2.4 — Move `PublicAPI.Unshipped.txt` into `Shipped` (P12-8; V3, V4) — after every surface-changing task

**Files.** The ten `src/*/PublicAPI.{Shipped,Unshipped}.txt`; `tests/Dexpace.Sdk.Core.Tests/Architecture/NFR4ArchitectureTests.cs`;
`tests/Dexpace.Sdk.Core.Tests/Architecture/Nfr11PublicSurfaceArchitectureTests.cs` (doc comment, `:13`); `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` (row B.9.6, `:84`);
`docs/sdk-documentation/cross-cutting-invariants.md` (lines 56 and 61); `Directory.Build.props` (the comment at `:64-67`); `CLAUDE.md` (the "stays empty" sentence at `:66`);
`docs/first-release.md` (the public-surface line); `CHANGELOG.md`.

**Test first.** Rewrite `NFR4ArchitectureTests` (Core.Tests, `Unit`): keep `Every_library_has_both_public_api_files` and `The_scan_sees_all_five_libraries`; replace
`Shipped_is_empty_or_nullable_enable_only_until_phase_12` with `Shipped_holds_the_surface_and_starts_with_nullable_enable` (every library's `Shipped` has
`#nullable enable` as its first line and at least one member line); and `The_scan_can_fail` gains fixtures for an empty `Shipped` and a header-less one. **Red today:**
all five `Shipped` files are empty or header-only (V4 measured the inverse). `Unshipped` is **not** asserted empty: later changes legitimately fill it, and the
"settled at a tag" rule is the preflight's (3.1).

**Change (a pure move; `PublicAPI` diff).** For each library: `Shipped` = `#nullable enable` followed by every line of `Unshipped` after its own header (order
preserved; DI's `Shipped`, 0 bytes today, gains the header); `Unshipped` = `#nullable enable` alone. No `*REMOVED*` line exists (checked: `grep -c REMOVED` is 0 in all
five). Resulting `Shipped` member counts equal the `Unshipped` counts of V3 (Core 1 369, Conformance 475, SystemNet 39, DI 27, STJ 12, each plus the header line).
Comments and prose that say "`Shipped` stays empty / empty until phase 12" are corrected in the files above in this commit. `docs/first-release.md`: tick *One-time
public-surface choices* with the date and evidence, replacing "The list for phase 12" with the decision: every item of P12-15's table kept, `ParamName` completed
by task 1.2, and the move at this commit (the table itself stays in the design; the register links to it).
**Verify.** V-build (0 warnings: V4); V-core `NFR4ArchitectureTests`, `Nfr11PublicSurfaceArchitectureTests`; V-conf-tests `AppendixBCoverageTests`, `LedgerAuditTests`;
`wc -l src/*/PublicAPI.*.txt`; `git diff --stat -M main...HEAD -- src/*/PublicAPI.*` reads as a move; the housekeeping `probe`.
**IDs.** `NFR-4` (source half; the baseline half is held, V5). **Breaking.** None (nothing is published). **Commit.** `feat: move PublicAPI.Unshipped into Shipped for the first release (NFR-4)`.

## Task 2.5 — PR 2 gate

V-gate; the Security diff; `git diff main...HEAD -- tests/Dexpace.Sdk.Conformance.Tests` shows no waiver or catalogue change (convention 10). Hand the lead the branch.

---

# PR 3 — The release path

## Task 3.1 — The release preflight and its self-test (P12-4, P12-13; `NFR-16`, `NFR-12`)

**Files.** New `scripts/ci/release-preflight.cs`, `scripts/ci/release-preflight-selftest.sh`; `.github/workflows/ci.yml` (two steps after *Packaging conformance*);
`tests/Dexpace.Sdk.Conformance.Tests/Gates/CiWorkflowTests.cs` (`Gates` rows); `CHANGELOG.md`.

**Test first.** `release-preflight-selftest.sh` (modelled on `coverage-gate-selftest.sh`: an `expect <case> <exit-code> <pattern> <args…>` helper, one `ok`/`FAILED`
line per case, exit 0 only when every case holds) is written first and **fails today because the script does not exist**. Its cases, each over a scratch copy
(file operations only, so it runs on all three OS rows without `zip`):
- the unaltered `artifacts/packages` with no tag: exit 0 (or nothing below proves anything);
- a `.nupkg` removed: exit 1 naming the package (the five-package rule); a `.snupkg` removed: exit 1 naming the `.nupkg` with none beside it;
- one package renamed to a different version: exit 1 naming both versions (one version train);
- `--expected-token 0000000000000000`: exit 1 naming the assembly and both tokens (an assembly not signed with the committed key; an unsigned one shares the code
  path, reading R9);
- with `--tag`: a tag whose version is not the packages': exit 1; a `v<version>` tag: accepted, and `v0.1.0` for `0.1.0-alpha.1` refused; a `--repo` copy whose
  `CHANGELOG.md` lacks `## [<version>]`: exit 1; a copy whose `PublicAPI.Unshipped.txt` holds a member line: exit 1; a temporary git repository whose tag commit is not
  an ancestor of its `main`: exit 1 (`--main-ref main`);
- without `--tag`, each tag-dependent finding prints `would fail:` and the exit stays 0 (the dry-run rule).

**Change.** `release-preflight.cs`, a file-based app (MIT header, `#:property RestorePackagesWithLockFile=false`, `#:property PublishAot=false`, BCL only):
`dotnet run scripts/ci/release-preflight.cs -- <package-dir> [--tag <tag>] [--repo <root>] [--main-ref <ref>] [--expected-token <16 hex>]`. It checks, naming the
cause (the design's list): the five packages present at one version (read from the file names `<Id>.<version>.nupkg`, as the audits do); a `.snupkg` beside each
`.nupkg`; each packed `lib/net10.0/<Id>.dll`, read with `System.Reflection.Metadata` in memory and never loaded, has the `StrongNameSigned` flag and a public-key token equal to
the expected one (derived by default from `DexpacePublicKey` in `Directory.Build.props`: SHA-1 of the blob, last eight bytes reversed); and, only with `--tag`: the tag
`v<version>` equals the packages' version (a pre-release suffix is accepted), no `PublicAPI.Unshipped.txt` under `--repo` holds more than `#nullable enable`, `CHANGELOG.md`
has `## [<version>]`, and `git merge-base --is-ancestor <tag commit> <main-ref>` (default `origin/main`) holds. Exit 0 / 1 / 2 (usage), like the other scripts.
`ci.yml`: step **Release preflight** (`dotnet run scripts/ci/release-preflight.cs -- artifacts/packages`) and **Release preflight - self-test**
(`scripts/ci/release-preflight-selftest.sh artifacts/packages`), unconditional, on all three rows (reading R10); `CiWorkflowTests.Gates` gains both rows in the same commit
(without the rows the pin test would not notice a dropped step).
**Verify.** V-build; `dotnet pack` then the preflight and its self-test (every case `ok`); V-conf-tests `CiWorkflowTests`, `LicenseHeaderTests`; `git diff` shows no
`Conditionals` change. **IDs.** `NFR-16` (a release build fails an unsigned publication: its testable clause), `NFR-12`, `NFR-14`, `NFR-15`.
**Commit.** `ci: add the release preflight and its self-test (NFR-16, NFR-12)`.

## Task 3.2 — The inert `release.yml`, pinned by `ReleaseWorkflowTests` (P12-2, P12-3, P12-13; V16)

**Files.** New `.github/workflows/release.yml`; new `tests/Dexpace.Sdk.Conformance.Tests/Gates/ReleaseWorkflowTests.cs`; `.github/workflows/ci.yml` (header lines 31-32 only);
`CHANGELOG.md`.

**Test first.** `ReleaseWorkflowTests` (Conformance.Tests/Gates, `Unit`), in `CiWorkflowTests`' style (the file read as text, line-based rules, and a `The_checks_can_fail`
test that edits copies): **red today because the file does not exist**. It pins (design "The release workflow", items 1 to 5):
- exactly two triggers, a `v*` tag push and `workflow_dispatch` with a boolean `dry_run` whose default is `true`; no `branches`, no `pull_request`;
- top-level `permissions: contents: read`; `id-token: write` and `attestations: write` appear only inside the `publish` job; no `secrets.` reference and no
  `continue-on-error` anywhere;
- the `publish` job: `needs: build`, `environment: nuget`, `if: github.ref_type == 'tag' && (github.event_name == 'push' || inputs.dry_run == false)`;
- the `build` job: `fetch-depth: 0`, `--locked-mode`, `SOURCE_DATE_EPOCH`, `scripts/ci/reproducible-pack.sh`, the dependency audit, the packaging conformance, the preflight (with
  `--tag` on a tag ref) and the `SHA256SUMS` upload;
- the `publish` job's first step is the guard (fails naming `docs/first-release.md` when `vars.NUGET_USER` is empty), followed in order by the checksum verification, the
  attestation, `NuGet/login`, and `dotnet nuget push … --skip-duplicate`;
- the header carries "THIS WORKFLOW IS INERT" and the three unmet conditions;
- mutations that must each be flagged: the guard removed, a `secrets.NUGET_API_KEY` added, `id-token: write` moved to the top level, `continue-on-error`, a `branches:` filter.

**Change.** `release.yml` as the design's sketch (header, `name: Release`, `concurrency: release`), the two jobs, using the same action major-version style as `ci.yml`
(`actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`); `actions/download-artifact`, `actions/attest-build-provenance` and `NuGet/login@v1`
with their input and output names are **copied from each action's README at authoring** (design: medium-high confidence; unverified in this plan), and the header says so.
Build job: ubuntu only (the three-OS matrix and the AOT smoke are CI's, on `main`), restore locked, build, full test run without coverage, `SOURCE_DATE_EPOCH=$(git log -1
--format=%ct)`, `scripts/ci/reproducible-pack.sh artifacts/release`, audit and conformance over `artifacts/release/1`, preflight over the same, `sha256sum *.nupkg *.snupkg >
SHA256SUMS`, upload. Publish job as pinned: it has **no checkout and no `setup-dotnet`** (it needs nothing from the repository, only the downloaded artifact), so `dotnet nuget push` runs on the runner image's
preinstalled .NET SDK, and the header says so; `global.json` does not apply to it, which is harmless for a push of finished packages (adding `setup-dotnet` would need a checkout for
`global-json-file`, which the publish job deliberately lacks). `dotnet nuget push` pushes the `.snupkg` beside each `.nupkg` automatically when given the `.nupkg` glob, so the step names
`*.nupkg` and no second push is written; the implementer confirms this against the `dotnet nuget push` documentation when authoring the step (not verified in this plan). `ci.yml`'s header: the "Package-validation baseline" line now says held until the first publish (V5), and the "Strong naming
and author signing" line says strong naming is built (phase 12) and `NFR-16` rests on the attestation (P12-13). `CiWorkflowTests` is unchanged by this file (reading R10).
**Verify.** V-conf-tests `ReleaseWorkflowTests`, `CiWorkflowTests`; run the build job's commands locally in order (restore, build, test, `reproducible-pack.sh
artifacts/release` from a committed `HEAD`, the audits, the preflight, `sha256sum`) — they are the same commands CI will run; `actionlint .github/workflows/release.yml` if it is installed (optional).
**IDs.** `NFR-16`, `NFR-12`, `NFR-17` (the file is a gate set the ruleset will later name only if the lead adds it; it is not a PR check). **No remote action.**
**If the repository is private** (L2) the attestation step and its pins are removed and the register records the fallback (task 5.1).
**Commit.** `ci: add the inert release workflow, pinned by ReleaseWorkflowTests (NFR-16, NFR-12)`.

## Task 3.3 — PR 3 gate

V-gate (now with the preflight and its self-test); the Security diff. Hand the lead the branch.

---

# PR 4 — Documentation and the fence compiler

## Task 4.1 — Housekeeping's `fences` command (P12-18; V15)

**Files.** New `.claude/skills/housekeeping/src/Fences.cs`; `.claude/skills/housekeeping/src/Program.cs` (command and usage); new
`.claude/skills/housekeeping/tests/FencesTests.cs` (and `Fixture.cs` additions); `.claude/skills/housekeeping/SKILL.md` ("Deliberately not ported: the fence check" becomes
"The fence check", with the info-string rule and the scratch-outside-the-repository rule; it keeps the "reported, not failed" sentence); `.github/workflows/ci.yml` (one step); `CiWorkflowTests.cs` (`Gates` row and `Conditionals` entry);
the existing fences that name `Dexpace.Sdk` (`README.md`, `docs/sdk-documentation/*.md`, `src/*/README.md`).

**Test first.** `FencesTests` (housekeeping tests, no trait), a fixture pair for every rule (the suite's convention): **red today because `fences` is an unknown command
(`Program.Run` returns 2)**:
- `A_fence_that_names_a_namespace_is_extracted_with_its_page_and_line`; `A_qualified_type_without_a_using_line_counts` (the single-line-import trap);
  `A_fence_with_no_Dexpace_reference_is_skipped`; `A_fragment_fence_is_counted_and_never_compiled` (info string ` ```csharp fragment `).
- `A_clean_fence_compiles` and `A_broken_fence_fails_naming_page_and_line` — these compile against the real `src/*/bin/Release/net10.0/*.dll`, so they need the solution
  built in Release first (reading R12; they fail with a message saying so, never skip).
- `The_scratch_project_is_written_through_the_guard` and `The_command_writes_nothing_in_the_repository` (a `git status --porcelain` snapshot around a run, as
  `ProbeTests.The_probe_writes_nothing`); `The_scratch_root_is_outside_the_repository_and_inherits_no_build_props` (the generated project's directory is under `Path.GetTempPath()`, and a
  fence that needs `ILogger` compiles, which a repository-rooted or logging-less project would not: `NU1008` / `CS0012`).
- `A_fence_needing_an_unpinned_optional_package_is_reported_and_not_failed` — a fence naming a `Dexpace.Sdk` type and a namespace of a package absent from `Directory.Packages.props`: the
  line is `skipped: needs <package>`, `s skipped` is 1, the exit is 0 (red today with the rest of the command: it does not exist).

**Change.** `fences [--root <dir>] [--libs <dir>]`: extract every ` ```csharp ` fence in `README.md`, `docs/sdk-documentation/*.md`, `src/*/README.md` whose text names a
`Dexpace.Sdk` namespace (a `using Dexpace.Sdk…;` or a qualified `Dexpace.Sdk.` type); write one generated project per fence under a scratch directory **outside the repository**
(`Directory.CreateTempSubdirectory("dexpace-fences")` under `Path.GetTempPath()`, removed in a `finally`), each path through `Guard.AssertWritable` (which refuses only frozen
repository paths, `Guard.cs:80-84`, so it is the convention here and not the isolation). *Why outside, not stopper files in the scratch root:* MSBuild finds `Directory.Build.props`,
`.targets` and `Directory.Packages.props` by walking up from the project, so a project anywhere under the repository would inherit `TreatWarningsAsErrors`, the analyzers, lock files,
`NuGetAudit=all`, signing and central package management (a versioned `PackageReference` is then `NU1008`); outside the tree none of them is found, and nothing needs keeping in step with
a future repository prop or a fourth inherited file. Generated project: `net10.0`, `OutputType` `Exe`, top-level statements, `Nullable` on, `ImplicitUsings` on, `TreatWarningsAsErrors`,
`NuGetAudit` `false` (the audit would otherwise need the network for a throwaway project), `<Reference>`s to the built DLLs, and **always** a `PackageReference` to
`Microsoft.Extensions.Logging.Abstractions` at the version pinned in `Directory.Packages.props` (read by the tool as text), because Core's public surface exposes `ILogger` and a raw
`<Reference>` does not bring it (`CS0012` otherwise); the other `Microsoft.Extensions.*` packages pinned there are added for the fences whose namespaces need them. A fence whose
namespace needs an **optional package not pinned** in `Directory.Packages.props` is **reported, not failed** (the rule housekeeping `SKILL.md` already holds for the check): the output line
is `path:line: [fence] skipped: needs <package>`, it is counted (`s skipped`), and the exit code is unaffected, each file starting with a `#line <n> "<page>"` directive so the compiler's
diagnostics name page and line; build them all in one `dotnet build` of a generated traversal project. Output `path:line: [fence] message`, then `n compiled, m fragments, s skipped,
k failed`; exit 0 clean, 1 on any failure, 2 usage. The command's own project references no package (the tool's rule). `ci.yml`: step **Doc fences** (`if: runner.os == 'Linux'`,
`dotnet run --project .claude/skills/housekeeping/src -- fences`) after *Tools - test*; `CiWorkflowTests.Gates` row `("Doc fences", "-- fences")` and
`Conditionals["Doc fences"] = "runner.os == 'Linux'"` in the same commit. Run the command over the repository: each of today's seven fences compiles or becomes
` ```csharp fragment ` with a one-line reason beside it; record the counts for the checklist.
**Verify.** V-tools (the new tests green); V-build first; the command over the repository (exit 0); V-conf-tests `CiWorkflowTests`; the probe.
**Commit.** `feat: housekeeping fences command compiles the documentation's C# fences (P12-18)`.

## Task 4.2 — `architecture.md`, and the superseded `docs/architecture.md` retires (P12-17)

**Files.** New `docs/sdk-documentation/architecture.md`; delete `docs/architecture.md`; `docs/README.md` (drop the `architecture.md` row, add the new page to the
`sdk-documentation/` row); `CLAUDE.md`/`README.md` links if the probe finds any; `.claude/skills/housekeeping/{src/Checks.cs,tests/*}` only if the guard test requires it
(reading R18). **No failing test is possible (prose).** **Pins:** the fence compiler (every fence compiles or is a marked fragment), the probe's `links` and `claims` checks.
**Content.** Ported and restated per .NET area from Ruby's and Node's `architecture.md` (read with `git show origin/main:` from the sibling checkouts) and design §1: the six layers
(CLAUDE.md "Architecture — Big Picture"), the five-package graph and the dependency rule, the seams (`IHttpClient`, `IAsyncHttpClient`, `ISerde`, `IResponseHandler<T>`), and a
table pointing each subsystem to its page. **Verify.** V-build; `fences`; `probe`; V-tools. **Commit.** `docs: sdk-documentation architecture page; retire docs/architecture.md`.

## Task 4.3 — `getting-started.md` and `example-end-to-end.md` (P12-17)

**Files.** New `docs/sdk-documentation/getting-started.md`, `docs/sdk-documentation/example-end-to-end.md`; `docs/README.md` rows. **No failing test (prose); the pin is the fence
compiler**, which makes every fence here a complete program that compiles (a fence is `fragment` only with a stated reason). **Content.** Getting started: install the packages,
build a client with and without DI, a typed call, error handling (`HttpResponseException`, `IOException` for a transport failure — `catch (SdkException)` does not catch it), disposal.
End-to-end: one worked SDK slice (an operation, a typed response handler, bearer auth, pagination over a `Link` header, retry and redirect on, the DI registration, logging).
Read the APIs from source (`src/Dexpace.Sdk.Core/…`); do not write a signature from memory. **Verify.** V-build; `fences`; `probe`. **Commit.** `docs: getting-started and end-to-end example pages`.

## Task 4.4 — `quality-gates.md` and `versioning.md` (P12-6, P12-17)

**Files.** New `docs/sdk-documentation/quality-gates.md`, `docs/sdk-documentation/versioning.md`; `docs/README.md` rows. **No failing test (prose); pins:** `fences`; `probe`;
every gate command quoted is copied from `CLAUDE.md`/`ci.yml` and checked by running it in the verify step.
**Content.** `quality-gates.md` (ported from Ruby's): every gate of design §9's table with what it enforces, the local command, and whether it blocks; the 3-OS matrix; the
reproducible-pack lesson of V6 (a generator that orders output nondeterministically breaks a byte-for-byte compare; one run proves nothing, V7). `versioning.md`: the policy of P12-6
verbatim in substance (lockstep SemVer, what the public API is, `0.y` minor/patch rules, `CHANGELOG` **Breaking** lines, `*REMOVED*`, `[Obsolete]` first, a conformance assertion
is at least a minor, 1.0 strictness, `0.1.0` and the `-alpha.1` train). **Verify.** run the quoted commands; `fences`; `probe`. **Commit.** `docs: quality-gates and versioning pages`.

## Task 4.5 — The four `write-a-*` guides (P12-17)

**Files.** New `docs/sdk-documentation/write-a-transport.md`, `write-a-serde.md`, `write-a-response-handler.md`, `write-a-paging-strategy.md`; `docs/README.md`. **No failing test (prose);
pins:** `fences`, and for the transport and serde guides a driver fence copied from the real drivers so it cannot rot (`SystemNetConformanceTests`, `CodecSuite` driver). **Content.**
Ported from Node's four guides (`git -C /home/mohammad/Projects/dexpace/nodejs-sdk show origin/main:docs/sdk-documentation/write-a-<name>.md`), restated for the .NET seams: implement
the seam, run the matching conformance suite (transport, codec) with the waiver rules (a waiver whose assertion passes fails the run). **Verify.** `fences`; `probe`. **Commit.** `docs: write-a transport, serde, response-handler and paging-strategy guides`.

## Task 4.6 — The consolidated "What a green run does not prove" (P12-17; V14)

**Files.** `docs/sdk-documentation/conformance.md` (one new section, last); no other file. **No failing test; pins:** `ConformancePageTests` (the page's sample, catalogue table and
preamble stay identical to the code — a new section must not touch them) and `fences`. **Content.** One section assembling each suite's existing paragraph (`:23`, `:307`, `:413`, `:448`),
the permanent waivers (`TRANSPORT-14`'s name clause, `transport-2.bodyless-not-resent` for a caller-built handler), the `NotExercised` pins per driver, and `APPENDIX_B.md`'s *by reference* rows
(Ruby's R11 note). **Verify.** V-conf-tests `ConformancePageTests`; `fences`. **Commit.** `docs: conformance page states what a green run does not prove`.

## Task 4.7 — Stale text: `SECURITY.md`, `README.md`, `CONTRIBUTING.md`, `docs/README.md` (P12-24; V13)

**Files.** `SECURITY.md`, `README.md`, `CONTRIBUTING.md`, `docs/README.md`. **No failing test (prose); pins:** the probe's `claims` check, `fences` (README fences). **Change.**
`SECURITY.md`: five packages (was "three", `:5-8`), the supported policy "the latest minor of 0.x only", the contact kept (`oaljarrah@dexpace.org`) and GitHub private vulnerability
reporting named as a second channel **in conditional wording** ("when enabled on the repository"), because enabling it is the lead's (L4) and the file must not claim it is on (reading R16).
`README.md:26-27`: "What is not built yet — the deviation audit and the release path" becomes the as-built status (built; the first publish held, link to `docs/first-release.md`).
`CONTRIBUTING.md`: a short "Releasing" section pointing to the register's *Release path*. `docs/README.md`: the "Today:" list gains the new pages. **Verify.** `probe`; `fences`. **Commit.** `docs: refresh SECURITY, README and CONTRIBUTING for the release path`.

## Task 4.8 — `CHANGELOG.md`: the audit, the Phase 12 entry, the draft release notes, and `ReleaseNotesTests` (P12-16, P12-19)

**Files.** `CHANGELOG.md`; new `tests/Dexpace.Sdk.Conformance.Tests/Gates/ReleaseNotesTests.cs`.

**Test first.** `ReleaseNotesTests` (Conformance.Tests/Gates, `Unit`): reads `docs/first-release.md`'s *What v1 ships without* and `CHANGELOG.md`'s draft block (the section headed
`### Release notes (draft for 0.1.0)` at the head of `[Unreleased]`).
- `The_draft_block_names_every_item_v1_ships_without` — for every non-struck top-level bullet under `### Unsatisfied MUSTs`, `### SHOULD- and MAY-level requirements declined for v1`,
  `### Behavioural asymmetries a consumer must know` and `### Post-v1 packages`, its key (reading R13: the leading requirement ID such as `AUTH-15`, else the leading kebab-case name
  such as `async-redirect-pillar`, else the leading package name such as `Dexpace.Sdk.Reactive`, else the first bold phrase) appears in the block. **Red today:** the block does not exist.
- `The_scan_is_not_vacuous` — it extracts at least 25 keys, including `AUTH-15`, `CFG-2`, `HTTP-22`, `async-redirect-pillar`, `Dexpace.Sdk.Reactive`; struck-through bullets (`~~`) are not keys.
- `The_block_leads_with_the_breaking_changes` — the block's first list names the transport-failure change (`catch (SdkException)` no longer catches a `ServiceRequestException`).
- `The_scan_can_fail` — removing an item from a copy of the block is flagged.

**Change.** (1) **The audit** (P12-19): walk `git log --first-parent main --oneline` (55 commits at `1b6b6c9`, the count at the task's start, so re-count when starting; the `feat:`/`fix:`/`test:`/`ci:` ones, since PR #3); for each, find its
entry in `CHANGELOG.md`; add what is missing (earlier entries are not rewritten); record the walk, with the missing ones, in the checklist (task 5.2). (2) The Phase 12 entry (the
lines added by the earlier tasks, gathered under `### Phase 12 — first release`). (3) The draft release-notes block: the summary, the breaking-change digest that phase 11 asked to lead
with (assembled from the `**Breaking:**` lines of `### Phase 11 — deviation reconciliation`), the `ShouldHandle` guidance and the no-reload rule (phase 9 hand-off), and **every item**
of *What v1 ships without* (a line each, with its key). It becomes `## [0.1.0]` at the held release commit.
**Verify.** V-conf-tests `ReleaseNotesTests`, `LedgerAuditTests`; `probe`. **Commit.** `docs: changelog audit, Phase 12 entry and the draft 0.1.0 release notes (P12-16, P12-19)`.

## Task 4.9 — PR 4 gate

V-gate including `fences` and the probe; the Security diff. Hand the lead the branch.

---

# PR 5 — Close-out

## Task 5.1 — `docs/first-release.md` (design "Corrections owed", blocker dispositions, P12-12, P12-20, P12-25)

**Files.** `docs/first-release.md`. **No failing test (prose); pins:** `LedgerAuditTests` rule L6 (the `### Unsatisfied MUSTs` list stays at four, heading unchanged), `requirement-coverage.cs --check` (a ⏳ MUST names its owner), the probe.
**Change.**
- *Packages*: the version column reads `0.1.0-alpha.1` with a dated note (every target framework is `net10.0`, D1, replacing the stale `net8.0` column).
- *Blockers*: tick *Signing decided* (strong naming built, author signing declined for attestation, P12-13), *Versioning policy written*, *User documentation*, *`CHANGELOG.md` complete*, each
  with date and evidence (2.4 already ticked the surface line); rewrite and leave open, per the design's table: *CI green* (the lead records the first green `main` run, L1), *the ruleset*
  (L3, with the request text), *Every MUST dispositioned* and *Conformance green* (tied to those), *A reachable `SECURITY.md` contact* (L4), *NuGet ownership settled* (**held**: the
  exact steps in P12-25's order — organisation `dexpace`, `Dexpace.*` prefix request, trusted-publishing policy for `dexpace/dotnet-sdk` / `release.yml` / environment `nuget`, environment
  protection and `NUGET_USER`, ruleset, tag — and the scoped-key fallback for a first brand-new-ID push, recorded as such). Phase 12 closes 5 of 11, leaves 4 for the lead, 2 held or lead-confirmed.
- *Release path*: rewritten to the as-built workflow (tag or dry-run dispatch; build job; separate publish job; the guard; "do not tag before P12-25's order completes").
- *After the first publish*: the `PackageValidationBaselineVersion` procedure (set to the published version in `Directory.Build.props`; a reviewed `CompatibilitySuppressions.xml` per
  break), `NFR-12`'s release half **as read by P12-12** (a rebuild from the tag reproduces the attested pre-push digests byte for byte, and the nuget.org copy equals them with its
  `.signature.p7s` entry removed, compared entry by entry), `NFR-16`'s closure (`gh attestation verify <file> --repo dexpace/dotnet-sdk`), the release commit (empty `VersionSuffix`, dated `## [0.1.0]`, tag `v0.1.0`) and the next-train commit.
- *SHOULD declined*: add `NFR-16`'s repository-signature fallback **only if** L2 reports the repository private (P12-13).
- **If the lead declines `NFR-17` (P12-20 alternative):** a fifth unmet-clause row in `docs/deviations.md` and a fifth line under `### Unsatisfied MUSTs` in this same commit (V17); not planned.
**Verify.** V-conf-tests `LedgerAuditTests`; `dotnet run scripts/ci/requirement-coverage.cs -- --check`; `probe`. **Commit.** `docs: first-release register for phase 12 (blockers, release path, after the first publish)`.

## Task 5.2 — The checklist (roadmap step 6; P12-26)

**Files.** New `docs/work/mvp/phase12/2026-10-22-phase12-first-release-checklist.md` (written directly into the archive, as the earlier phases did). **No failing test; pins:**
`requirement-coverage.cs --check` and `--check-section12`. **Content.** One row per ID in scope — `NFR-4` (SHOULD, ✅ split: source half met by 2.4, baseline half held), `NFR-12` (SHOULD, ✅ split:
build half met by 1.1/2.2, release half held), `NFR-16` (SHOULD, ⏳: mechanism built by 2.1/3.1/3.2, closure held, owner `docs/first-release.md`), `NFR-17` (MUST, ⏳: merge half is the lead's ruleset,
owner `docs/first-release.md` *Blockers*) — with level, mark, tasks and where proven; the earlier phases' ⏳ text satisfies the carry rule. Then: the blocker dispositions; the carried-in dispositions; the
L1 result (or "not read"); the `CHANGELOG` audit record (4.8); the fence counts (4.1); the styleguide audit groups (public API surface, nullability, async and `ConfigureAwait`, disposal, analyzers
and format, test conventions, trim and AOT, styleguide-versus-design conflicts); the rulings taken at their defaults; the ledger P12-D1 to P12-D4; the lead's open actions. §12 is regenerated
**only if** a resolved mark moved (`--check-section12` says): `dotnet run scripts/ci/requirement-coverage.cs -- --section12`. **Verify.** both coverage commands. **Commit.** `docs: phase 12 checklist`.

## Task 5.3 — Dated corrections, `CLAUDE.md` and the knowledge notes

**Files.** `docs/sdk-design-dotnet/02-project-and-solution-layout.md` (§2.3), `09-toolchain-and-quality-gates.md` (§9 gate table rows, §9.2), as **dated corrections only** (the version
policy and `0.1.0`; the signed-publications, fence-check and release-workflow rows; strong naming built and `NFR-16` by attestation with author signing declined); `CLAUDE.md` (the "What is
genuinely unbuilt" paragraph becomes phase 12 built and the first publish held; the release path; strong naming; one `[LoggerMessage]` type per assembly; the new commands (preflight and its
self-test, `fences`) in the command block; the file list); `docs/knowledge/notes/tooling-and-quality-gates.md` (three hand-written entries in its `<sub>review · … · high · sha:manual-…</sub>`
format, citing the checklist of 5.2: the `[LoggerMessage]` generator's class-order nondeterminism (V6, V7); the strong-name `InternalsVisibleTo` key-algorithm trap and the key blob layout (V10);
nuget.org's repository signature and `NFR-12` (P12-12)). `docs/deviations.md` gets no new row (design: no §10 entry opened; P12-D1 to P12-D4 live in the design's ledger and the checklist).
**No failing test; pins:** `scripts/knowledge verify-structure`, `LedgerAuditTests`, the probe's `claims`. **Commit.** `docs: phase 12 dated corrections, CLAUDE.md and knowledge notes`.

## Task 5.4 — The roadmap

`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`: a dated correction on the Phase 12 card (the dry-run trigger, P12-D1; the held exits, P12-D2 to P12-D4; strong naming and attestation),
the Phase List row 12 links the design, plan and checklist (filed paths), a `*Status, 2026-10-22: …*` line under the card, and a dated Phase Status Note (what was built; the V6 finding that
phase 11's toolchain hypothesis was not the cause; the rulings at their defaults; the lead's open actions L1 to L9; hand-offs). **Commit.** `docs: phase 12 status note`.

## Task 5.5 — File the documents and probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 12            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 12 --write    # git mv (design and this plan)
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Repoint every citation of the inbox paths to `docs/work/mvp/phase12/`; the last probe reports nothing. **Commit.** `docs: file the phase 12 documents`.

## Task 5.6 — Final gate

V-gate in full, including `--check-section12`, the preflight and its self-test, the reproducible pack and its self-test, `fences`, the AOT smoke, V-tools and `verify-structure`; the Security diff
(empty); `requirement-coverage.cs --report` (MUST ⏳ 1: `NFR-17`); `git diff main...HEAD -- 'src/*/PublicAPI.Shipped.txt'` is the only `PublicAPI` change and every `Unshipped` is `#nullable enable`
alone (a contingent fix's `Unshipped` lines having been folded into `Shipped`, convention 6 and R2). Hand the lead the five branch names and the [lead actions](#lead-actions). **No push, no `gh`.**

---

## Lead actions

Never an implementer step. The plan lists them so the checklist can record each as done or open.

| # | Action | Source |
|---|---|---|
| L1 | Read the CI run of PR 1 (all five jobs; the `reproducible-pack:` lines; macOS `FromFile`; the Core flake family) and the first run on `main`; paste the result into the checklist | P12-21, task 0.2 |
| L2 | Say whether the repository is public (attestations; P12-13's fallback) | V19 |
| L3 | Apply the `required_status_checks` rule to the `main` ruleset (the five jobs) and read it back with `gh api repos/dexpace/dotnet-sdk/rulesets` | P12-20, `NFR-17` |
| L4 | Confirm the `SECURITY.md` contact is monitored; enable GitHub private vulnerability reporting | P12-24 |
| L5 | Create the nuget.org organisation `dexpace`, request the `Dexpace.*` prefix reservation, create the trusted-publishing policy | P12-25 |
| L6 | Create the `nuget` environment with required reviewers and set its variable `NUGET_USER` | P12-3, P12-25 |
| L7 | Push the five branches, open and merge the pull requests | convention 8 |
| L8 | Rule on P12-7, -9, -13, -15, -20, -23, -24, -25 (the plan proceeds on the defaults) | design open questions |
| L9 | The held release: the release commit (empty `VersionSuffix`, dated `## [0.1.0]`), the tag, the first publish, then *After the first publish* | `docs/first-release.md` |

### Lead rulings and the tasks that change if the lead rules otherwise

| Ruling (default) | If ruled otherwise |
|---|---|
| P12-7 `0.1.0` | publish `0.1.0-alpha.1` first: task 2.3 is identical; 5.1 and the held release commit change (tag `v0.1.0-alpha.1`, suffix kept) |
| P12-9 strong naming | decline: drop 2.1 and `StrongNameKeyTests`, drop the token check from 3.1, 5.3's §9.2 correction says "declined" |
| P12-13 attestation | certificate signing: 3.2 gains a signing step and secrets, so `ReleaseWorkflowTests`' no-secrets pin changes; **private repository:** 3.2 loses the attestation step and its pins, 5.1 adds the declined-SHOULD entry |
| P12-15 surface as built | rooting `ServiceRequestException` in `SdkException` reverses P11-7: a new contingent task before 2.4, a fifth unmet-clause row and register line (task 5.1), the budget is spent |
| P12-20 ruleset by the lead | declined: the fifth clause (task 5.1, one commit across `docs/deviations.md` and the register) |
| P12-23 webhooks post-v1 | a v1 webhook phase: outside this plan |
| P12-24 contact kept | a role address: wording in 4.7 and L4 only |
| P12-25 org and `Dexpace.*` | reserve `Dexpace.Sdk.*` only: wording in 3.2's header and 5.1 only |

---

## Traceability

### Design facts used

| Fact | Task(s) |
|---|---|
| V1 baseline | 0.1, 5.6 |
| V2 no CI run | 0.2 (L1), 1.C1 to 1.C3 |
| V3 the five libraries and file sizes | 0.1, 2.3, 2.4 |
| V4 the move is mechanical and the test fails | 2.4 |
| V5 baseline cannot be set | 5.1 (held), 3.2 (ci.yml header) |
| V6 the generator's class order | 1.1, 2.2, 4.4, 5.3 (note) |
| V7 one green run proves nothing | 1.1 (generator-output proof), 4.4 |
| V8 symbols safe once V6 is fixed | 2.2 |
| V9 strong naming measured | 2.1 |
| V10 `CALG_RSA_SIGN` trap | 2.1, 5.3 (note) |
| V11 `ParamName` accessors (and its amendment: the helper-routed accessors) | 1.2 |
| V12 flake not reproduced | 0.2, 1.C2 |
| V13 stale `SECURITY.md`, `README.md` | 4.7 |
| V14 documentation inventory | 4.2 to 4.6 |
| V15 fences | 4.1 |
| V16 `CiWorkflowTests` reads `ci.yml` only | 3.1, 3.2, 4.1 |
| V17 `LedgerAudit` L6, the coverage owner rule | 5.1, 5.2 |
| V18 thirteen blockers | 5.1 |
| V19 repository visibility | 0.2 (L2), 3.2, 5.1 |
| V20 Ruby's SSE cap at parity | 2.4 (recorded in the register line) |

### Decisions

| Decision | Task(s) |
|---|---|
| P12-1 structure | the plan's PR groups |
| P12-2 triggers | 3.2 |
| P12-3 guard | 3.2 |
| P12-4 pinning test and preflight | 3.1, 3.2 |
| P12-5 one `[LoggerMessage]` type | 1.1 |
| P12-6 versioning policy | 4.4 |
| P12-7 `0.1.0` | 2.3, 5.1 |
| P12-8 the move and `NFR4ArchitectureTests` | 2.4 |
| P12-9 strong naming | 2.1 |
| P12-10 `DexpacePublicKey` and its pin | 2.1 |
| P12-11 symbols | 2.2 |
| P12-12 `NFR-12` reading | 5.1, 5.3 (note) |
| P12-13 attestation, author signing declined | 3.1, 3.2, 5.1, 5.3 |
| P12-14 `ParamName` | 1.2 |
| P12-15 surface rulings | 2.4 (tick), 5.1 |
| P12-16 `ReleaseNotesTests` | 4.8 |
| P12-17 documentation set | 4.2 to 4.6 |
| P12-18 fence compiler | 4.1 |
| P12-19 `CHANGELOG` | 4.8 |
| P12-20 ruleset | L3, 5.1 |
| P12-21 first CI read | 0.2 |
| P12-22 contingent tasks | 1.C1 to 1.C3 |
| P12-23 D3 webhooks | 5.1 (post-v1 entry unchanged), 5.4 |
| P12-24 `SECURITY.md` | 4.7, L4 |
| P12-25 NuGet ownership | 5.1, L5, L6 |
| P12-26 checklist re-rows | 5.2 |
| P12-D1 dry-run trigger | 3.2, 5.4 |
| P12-D2 baseline held | 5.1, 5.4 |
| P12-D3 attestation substitution | 3.2, 5.3 |
| P12-D4 `NFR-12` reading | 5.1 |

### `docs/first-release.md` blockers

| Blocker | Disposition | Task(s) |
|---|---|---|
| CI green on every matrix row | lead | 1.1 (cause), 0.2, 5.1 |
| `main` ruleset (`NFR-17`) | lead | L3, 5.1 |
| Nine P0 defects | ticked (phase 11) | — |
| Every MUST dispositioned | lead, with the ruleset | 5.1, 5.2 |
| Conformance green | lead, with CI | 5.1 |
| Deviations audited | ticked (phase 11) | — |
| One-time public-surface choices | closed | 1.2, 2.4 |
| Reachable `SECURITY.md` contact | lead | 4.7, L4, 5.1 |
| NuGet ownership | held | 3.2, 5.1, L5, L6 |
| Signing decided | closed | 2.1, 3.1, 3.2, 5.1 |
| Versioning policy | closed | 2.3, 4.4, 5.1 |
| User documentation | closed | 4.1 to 4.7, 5.1 |
| `CHANGELOG.md` complete | closed | 4.8, 5.1 |

### Carried in from phase 11

| Item | Task(s) |
|---|---|
| CI not run on `main` since `6b49af6` | 0.2 (L1) |
| The reproducible-pack fix unproven | 1.1 (root cause), 2.2, 1.C3 |
| macOS `FromFile` | 1.C1 |
| ~15 `ParamName` accessors (plus the 14 `ThrowIf*(value)` sites of the design's V11 amendment) | 1.2 |
| The Core flake family | 1.C2 |
| `NFR-17`, the ruleset | L3, 5.1, 5.2 |
| D3, webhooks | 5.1, 5.4 |
| Release notes lead with phase 11's breaking changes | 4.8 |
| `NFR-4` baseline, `NFR-12` release half, `NFR-16` held | 5.1, 5.2 |
| The `CallKey` and `AddDexpaceClient<TClient>` triggers | unchanged (5.1 *Post-release triggers*) |
| Release notes name the `ShouldHandle` guidance and the no-reload rule | 4.8 |

### Roadmap card

| Card item | Task(s) |
|---|---|
| Clear every blocker | 5.1 (dispositions), L1 to L6 |
| Decide the one-time choices, move `Unshipped` into `Shipped` | 1.2, 2.4 |
| Author `release.yml` | 3.1, 3.2 |
| Reserve the prefix, configure trusted publishing | held: 5.1, L5, L6 |
| Versioning policy, the next version | 2.3, 4.4 |
| Strong naming and author signing | 2.1, 3.1, 3.2 (attestation in place of author signing) |
| User docs | 4.2 to 4.7 |
| Fence compiler | 4.1 |
| **Exit:** first publish happened or held with the reason | held: 5.1 |
| **Exit:** `PackageValidationBaselineVersion` set | held: 5.1 (V5, P12-D2) |
| **Exit:** `NFR-12` release half and `NFR-16` closed or held | held: 5.1, 3.2 |
| Always: every gate green, no `Security` test loosened, the probe clean | 1.3, 2.5, 3.3, 4.9, 5.6 |

### Design "Corrections owed at close-out"

`first-release.md` (5.1); design §2.3, §9 table, §9.2, §12 (5.3, and 5.2 for §12); the roadmap (5.4); `CLAUDE.md` (2.4 for the Shipped sentence, 5.3 for the rest); `docs/README.md`
(4.2 to 4.7); `README.md`, `SECURITY.md`, `CONTRIBUTING.md` (2.3, 4.7); `CHANGELOG.md` (every task, 4.8); `ci.yml` header (3.2); housekeeping `SKILL.md` (4.1); three knowledge notes (5.3).

---

## Plan-level readings

- **R1 — Task 0.2 is first, its evidence is not.** The design makes the CI read the plan's first task (P12-21). A run needs a pull request, so the read can happen only after PR 1 is pushed
  by the lead. The plan lists it first and fires the contingent tasks from it.
- **R2 — The contingent tasks do not block the move.** The design puts them "before the `Shipped` move if it touches the surface". If the lead cannot read CI before PR 2 is due, PR 2
  proceeds, and a later surface fix is a reviewed `Unshipped` diff against the 0.1.0 candidate, which costs nothing before a publish. Such a diff is folded into `Shipped`
  before task 5.6 (5.6 requires every `Unshipped` to hold `#nullable enable` alone) and the checklist records the fold, or "not read".
- **R3 — "One `static partial` class" is a new class.** `HeaderDropLog` is an instance class and `ProxyWarnings` a static one, so neither can host the other. `TransportLog` holds all four
  generated methods and the two existing classes stop being `partial`.
- **R4 — The gate is a source scan of attribute sites.** It counts types holding a `[LoggerMessage(` line, by the nearest preceding type declaration (one type per file). `LoggerMessage.Define`
  is not generator input. It lives in Core.Tests beside the other source scans, because Core.Tests may not reference a transport (SEAM-2).
- **R5 — V6 is judged by the generator's output.** Eight builds hashing to one `LoggerMessage.g.cs`; the repeated `reproducible-pack.sh` runs are a smoke only (V7).
- **R6 — V11 is incomplete, twice.** (1) `DexpaceClientOptions.cs:180` and `:191` (the `BaseAddress` and two timeout accessors), and the `fixedDelay` caller at `RetryOptions.cs:144`, have the same
  defect and are fixed with the listed ones. (2) Fourteen more accessors call `ArgumentOutOfRangeException.ThrowIf*(value…)` or `ArgumentException.ThrowIfNullOrWhiteSpace(value)`, which name the exception
  after the argument expression, so a `nameof(value)` grep cannot see them (design, "Amendment 2026-10-22 (plan review)"). The guard is not a file-level allow-list but the extended
  `InitAccessorScanner`, which recognises both spellings inside accessor bodies so the coverage test covers every site; the helper-routed accessors a line scan cannot see (`RequireDuration`,
  `RequireNullOrPositive`, `RequireNullOrNonNegative`, `RequireUsable`, `RequireTimeout`) keep behavioural rows in the rejection table.
- **R7 — Strong naming details the design left open.** The key is 2048-bit (the design's 160-byte figure was the probe's 1024-bit key); it is generated by a throwaway program and the blob
  layout is recorded in the knowledge note, not committed as a script; `*.snk binary` is added to `.gitattributes` because `* text=auto eol=lf` would otherwise touch it; the pin test compares
  the property with all five built library DLLs (read by `AssemblyName.GetAssemblyName`, no project reference needed) and the test assembly's own key; the preflight (3.1) repeats the
  check on the packed assemblies.
- **R8 — Signing is scoped to `src/` and `tests/`.** `Directory.Build.props` applies to `tools/` and `.claude/skills/*` too, and V9 measured only `Dexpace.Sdk.sln`. The reviewer's probe showed
  that signing the tools breaks their friend tests with `CS1726` (keyless `InternalsVisibleTo` in three tools projects), so task 2.1 conditions `SignAssembly` on the project path from the start
  (not as a fallback on the wrong diagnostic) and the tools solution is verified unsigned. The main solution, NativeAOT smoke, packaging conformance, dependency audit and file-based scripts were measured fine with signing on.
- **R9 — Preflight inputs.** The version is read from the package file names; the expected token is derived from `DexpacePublicKey`; an unsigned assembly and a wrongly-signed one share the
  token compare (the report distinguishes them), so the self-test proves the compare with `--expected-token`.
- **R10 — CI placement.** The preflight and its self-test are unconditional on all three rows (no `Conditionals` entry); `Doc fences` is Linux-only (a `Conditionals` entry). The design's
  "`CiWorkflowTests` is unchanged" is read as: unchanged by `release.yml`; the new `ci.yml` steps (P12-4, P12-18) add rows.
- **R11 — `release.yml` action pins are unverified.** The design's confidence in the NuGet and attestation actions' names, inputs and outputs is medium-high; the plan has the implementer copy
  them from each README and says so in the file header. Nothing in the plan runs the workflow.
- **R12 — Fence compiler.** One generated project per fence, built in one invocation, with `#line` directives so diagnostics name page and line; `ImplicitUsings` on in the generated projects, which live under `Path.GetTempPath()` outside the repository (no inherited build props, `NuGetAudit` off, the logging abstractions always referenced, an unpinned optional package reported and not failed); the
  compile tests need the Release libraries built and fail with a message rather than skip.
- **R13 — `ReleaseNotesTests` key rule.** The design says each item's "IDs or titles" appears; the plan fixes the extraction rule (leading ID, else kebab-case name, else package name, else first bold
  phrase; struck-through bullets excluded) and a non-vacuity floor.
- **R14 — Ticks.** The public-surface blocker ticks at the move commit (the design says so); the other ticks wait for task 5.1, where each cites its evidence.
- **R15 — No ledger row for P12-D1 to P12-D4.** The design opens no §10 entry; `docs/deviations.md` is edited only to re-derive a citation (task 1.1). The four entries live in the design and the checklist.
- **R16 — `SECURITY.md` wording.** GitHub private vulnerability reporting is named conditionally; enabling it is L4.
- **R17 — Absolute test counts are not pinned.** Only V1's baseline is recorded (task 0.1); phase 12 adds tests.
- **R18 — The guard list in housekeeping.** `GuardCheck.WritableSurface` and its test fixtures name `docs/architecture.md`. Deleting the file should not break them (they are path names); if a
  housekeeping test requires the file to exist, task 4.2 removes the entry and its fixture row in the same commit.
- **R19 — Not verified in this plan:** that the repository is public (V19), the exact `NuGet/login` and attestation action inputs (R11), whether the eight generator builds reproduce V6's 6:2 split on
  the implementer's machine (task 0.1 re-measures it), and that the tools solution builds green unsigned with the scoped condition (R8; the reviewer measured it, this plan did not). No CI run, remote read or `gh` call was made in writing it.

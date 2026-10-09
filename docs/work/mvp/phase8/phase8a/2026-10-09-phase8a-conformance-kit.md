# Phase 8a — The Conformance Kit: Implementation Plan

**Status:** Draft, for review. Written 2026-10-09 against `main` at `ce0f68c` (phases 0 to 7c merged), on branch `87-phase-8-planning`,
for issue [#88](https://github.com/dexpace/dotnet-sdk/issues/88). Design: [phase 8a conformance kit design](2026-10-09-phase8a-conformance-kit-design.md),
the authority for every decision below; the plan cites its census rows (`TRANSPORT-2` … `ASYNC-22`), facts (`F1`–`F7`) and rulings
(`P8a-1` … `P8a-27`) rather than restating them. Scope authority: the roadmap's Phase 8 card
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`) and decision D3. Format precedent: the phase 7 plans
(`docs/work/mvp/phase7/phase7c/2026-10-09-phase7c-pagination.md` and its 7a and 7b siblings), read first. 8b (issue
[#89](https://github.com/dexpace/dotnet-sdk/issues/89), reference-transport hardening) is planned in parallel by another author; this plan touches
only 8a's files and lists the shared ones it will later edit (design, "Shared files with 8b").

**What this document is.** The roadmap's step 3 for sub-phase 8a: numbered TDD tasks in the design's three pull requests (P8a-27), each with its
failing tests, production change, `PublicAPI.Unshipped.txt` diff, requirement IDs and verification commands. It is not the checklist (step 4,
written from what was built in task 3.5) and it writes no production code itself beyond the load-bearing snippets. The
[checklist section](#checklist-one-row-per-owned-id) is the plan's own row table: exactly one row per ID 8a owns.

**Scope.** 38 rows: `TRANSPORT-2`, `-3`, `-7`, `-11`, `-15`, `-16`, `-19`, `-20`, `-21`, `-22`, `-23`, `-24`, `-25`, `-26`, `-27`, `-29` (16) and
`ASYNC-1`..`ASYNC-22` (22). Planned exits: 34 ✅ (16 `TRANSPORT` and 18 `ASYNC`; `ASYNC-6` is ✅ on the bridges with its runtime-facade half vacuous), 1 ✅ with one clause ⏳ (`ASYNC-2`: the
worker-pool-rejection clause, finding F-A2 below), 3 N/A or vacuous (`ASYNC-4` vacuous, `ASYNC-16` N/A, `ASYNC-21` N/A adapter-scoped), 0 rows wholly ⏳. The 14 rows of 8b are listed in the checklist as ⏳ 8b
with their assertion names. 8a writes **all 40 assertions**, including those for 8b's rows, which ship waived on the SystemNet driver (owner `8b`).

**Order and gates.** Three stacked pull requests (P8a-27), each green on its own, each carrying code and tests together: **PR 1 / group 1**
(tasks 1.1–1.12): the package, the report machinery, the catalogue generator and the promoted fixture. **PR 2 / group 2** (2.1–2.16): the
subject and runner, the raw-socket client, the 40 assertions with negative controls, the two drivers, the AOT slice. **PR 3 / group 3**
(3.1–3.8): the `ASYNC` pins, documentation, checklist, changelog, status note and corrections. Branch names `<issue>-phase-8a-<slug>` off `main`
(`88-phase-8a-conformance-kit`; the stacked branches `88-phase-8a-conformance-kit-2` and `-3` are the implementer's naming).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile error counts and is the
   expected red for a new type); make the production change; run them green; then run the wider gate of the group's last task. A test that passes
   before the change is a **pin** and says so; a pin is proven able to fail by temporarily breaking the thing it pins (never committed). **No commit
   is red:** a task that changes a signature edits every caller in the same commit. For an assertion, the "failing test" is its **negative control**
   (a deliberately broken subject must yield `Failed`) plus the driver rows that appear when the assertion joins `TransportSuite.Assertions`.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the project's curated
   `GlobalUsings.cs` only (`ImplicitUsings` is off). One type per file, named for the type (a private nested type is allowed).
3. **`src/Dexpace.Sdk.Conformance` code:** `ConfigureAwait(false)` on every `await` including `await using` and `await foreach` (`CA2007`, **also the
   promoted fixture**); methods at most 70 lines (`MA0051`; assertion bodies are one `internal static class` per group, a method per assertion,
   split into helpers where a body passes 60); `///` XML docs on every public member (CS1591), each citing its requirement IDs; no
   `PackageReference` beyond core's closure (constraint 2); no reflection (`IsAotCompatible`); the banned list applies in full: waits go through
   `TimeProviderWaits.DelayAsync` (never `Task.Delay`), `Thread.Interrupt`, `Task.Wait`/`.Result`, `Task<T>.WaitAsync`, `TaskCompletionSource<T>.SetResult`,
   `AsyncLocal<T>`, `Console`, `DateTime.UtcNow` and `Environment.GetEnvironmentVariable` are banned, `HttpClient` may not be constructed (the kit never
   builds one), and `Uri.ToString()` never reaches a message (use `OriginalString` or `UrlRedactor`). Non-generic `TaskCompletionSource` and
   `Task.WaitAsync(...)` are allowed. A `ConformanceException` message names the assertion's clause and carries `Expected` and `Actual`; it never carries a
   request or response body, a credential or a full URL.
4. **Tests:** `[Trait("Category", ...)]` on every class (`TestCategoryTests` enforces it per suite): `Unit`, `Integration`, `Conformance`, `AotSmoke`.
   The kit adds **no** `Security` class and edits none. Tokens come from `TestContext.Current.CancellationToken` (xUnit v3); no test sleeps on the wall
   clock (a condition wait or `TimeProviderWaits`). Assertions are xUnit `Assert` only in test projects; the kit itself uses no test framework.
5. **Security tests are never deleted or loosened.** 8a edits no `Security` class (the `FramingHeaderDropWireTests` etc. are `using`-swapped by task
   1.8 only if they import the fixture; the swap is the one permitted edit). The V-gate's diff check is
   `git diff -U0 main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security | grep '^[+-][^+-]' | grep -v '^[+-]using '`,
   expected to print nothing (`--stat` shows counts only and cannot show that a hunk is a `using` swap).
6. **`PublicAPI.Unshipped.txt`.** Only the kit's file is created; `PublicAPI.Shipped.txt` is empty, the file is sorted ordinally, the build's
   `RS0016`/`RS0017` output is the authority (apply the analyzer's code fix, then compare against the design's "The public surface" plus the plan-level
   additions R3, R5, R6 below). **No other project's `PublicAPI.Unshipped.txt` changes** (the V-gate checks core's, SystemNet's and STJ's).
7. **Zero change to `src/Dexpace.Sdk.Http.SystemNet` and `src/Dexpace.Sdk.Core`** (P8a-22). Anything the kit needs from SystemNet is built test-side over a
   borrowed `HttpClient`. If a task seems to need a src edit, stop: it is 8b's, and the assertion ships waived instead. (Finding F-A2: `ASYNC-2`'s
   rejecting-scheduler clause needs a core fix, so it is dispositioned ⏳ with an owner instead of breaking this convention; see the readings.)
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*`; the kit is `Dexpace.Sdk.Conformance.*`, so `System.IO`
   is unaffected, but `using Dexpace.Sdk.Core.IO;` is avoided.
9. **Commits** follow the repository style: `feat:` for the package and kit (nothing breaking: no published surface changes), `test:`, `docs:`, `chore:`,
   `ci:` for the workflow header. **No AI attribution** in a commit message, changelog line or document. **The plan authorises no `git push`, no `gh`
   command and no remote action** (global hard rule); issue #88 and the PRs are the lead's to drive.
10. **Siblings own shared files too.** `CHANGELOG.md`, `CLAUDE.md`, `Dexpace.Sdk.sln`, the roadmap status notes, `docs/first-release.md`,
    `tests/Dexpace.Sdk.Http.SystemNet.Tests/Dexpace.Sdk.Http.SystemNet.Tests.csproj` and `SystemNetConformanceTests.cs` are shared with 8b: re-derive each
    hunk from the merged file and never resolve a conflict by keeping either side whole.
11. **Central package versions.** The new test project uses `xunit.v3`, `coverlet.MTP` and `Microsoft.Testing.Extensions.TrxReport`, all pinned already;
    `Directory.Packages.props` does not move (design, "Shared files with 8b"). Two new `packages.lock.json` files are generated by restore and committed, **and `tests/Dexpace.Sdk.Http.SystemNet.Tests/packages.lock.json` changes too**
    (it lists project references, and task 1.7 adds the kit; the design's "Shared files with 8b" table omits it, and 8b also edits that project, so the hunk is
    re-derived from the merged file). Three lock files in all; CI's locked restore fails on any miss.
12. **Every assertion has a negative control** (P8a-9, design E) and no assertion is merged without one. A control is a `DelegateHttpClient` (or a small
    purpose-built `IAsyncHttpClient`) that violates exactly the clause, run with `ReleaseTimeout = 500 ms` and `AssertionTimeout = 10 s`.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. If a SDK is needed on this host:
`export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`; builds run one at a time with `-m:3 -nr:false --disable-build-servers` (the machine is shared).
Warnings are errors: a green build is the lint gate. The test runner is Microsoft.Testing.Platform: **build first, then `dotnet test ... --no-build`; never pass
MSBuild flags (`-m`, `-nr`, `-p:`) to `dotnet test`**, because the platform forwards them to the test host and zero tests run (check the reported test
count is non-zero after every run). Filters are `--filter-class "*Name"` and `--filter-trait "Category=Unit"`, not VSTest's `--filter`.

### Verification blocks

**V-build** (before any test run; the lint gate):

```bash
dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers
```

**V-fast** (per task; one class; after V-build):

```bash
dotnet test --project tests/Dexpace.Sdk.Conformance.Tests --configuration Release --no-build --filter-class "*<ClassName>"
```

(For SystemNet and Core suites replace the project; the class filter is the same.)

**V-gate** (the last task of every group; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release --no-build
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --no-build --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build --filter-trait "Category=Security"
git diff -U0 main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security | grep '^[+-][^+-]' | grep -v '^[+-]using '   # convention 5: expect empty (using-line hunks only)
git diff --stat main...HEAD -- src/Dexpace.Sdk.Http.SystemNet src/Dexpace.Sdk.Core src/Dexpace.Sdk.Serialization.SystemTextJson   # convention 7: expect empty
dotnet run scripts/ci/requirement-catalog.cs -- --check                                                                    # from task 1.5: the generated file is current
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build --coverlet --coverlet-output-format cobertura
--results-directory artifacts/test-results`, then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and
`scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) runs before each PR is offered for push; **from PR 1 the kit is a measured library**
(P8a-16), so a kit with no covering suite fails the gate closed.

### Phase-start queries (re-run at the start of each group)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info TRANSPORT       # 30 IDs, 24 MUST + 6 SHOULD
scripts/knowledge --prefix-info ASYNC           # 22 IDs, 18 MUST + 4 SHOULD
scripts/knowledge --gaps TRANSPORT ; scripts/knowledge --gaps ASYNC     # 0 gaps each
scripts/knowledge --req TRANSPORT-2             # per row in scope of the task
```

---

## Plan-level readings (where the design was silent, or the tree differs)

The design's rulings are not changed. **R1–R4 are corrections of fact against the tree; R5–R12 are additions.** Additions that add a public member or
behaviour are flagged for the lead.

- **R1 — Appendix B.7 has six items, not five.** `docs/product-spec/appendix-b-conformance-test-checklist.md` lists B.6 as five bullets
  (`TRANSPORT-1,2` | `-3`..`-9` | `-10`..`-14` | `-15`..`-19` | `-20`..`-30`) and B.7 as **six** (`ASYNC-1,2` | `-3`..`-7` | `-8`..`-12` | `-13,14` |
  `-15`..`-17` | `-18`..`-22`). The design says "B.6 (5 items), B.7 (5 items)". `ByAppendixBItem` therefore has keys `B.6.1`–`B.6.5` and `B.7.1`–`B.7.6`
  (11 items). The drift test reads the counts from the file, so a third correction is a regeneration, not a code edit.
- **R2 — `Status`, `Ok` and every scripted reply close the connection today.** `LoopbackResponse.Status` writes `Connection: close` and sets
  `closeConnection: true`. `transport-15.owned-released`, `transport-25.dispose-releases` and `transport-2.no-silent-resend` need a keep-alive reply. Task
  1.9 adds an optional `keepAlive` parameter (default `false`) to `Status`, `Ok`, `Large` and `Streamed`; existing callers are unchanged. (Not in the design's
  member list; flagged.)
- **R3 — `ConformanceWaiver` gains two narrowing properties, `Assertion` and `Face`.** A waiver by ID alone cannot express "`TRANSPORT-11`'s SHOULD log
  clause is not met" without also waiving (and then staleness-failing) the passing MUST assertion that cites the same ID. `Assertion` (an assertion
  name) and `Face` (a `TransportFace?`) are optional; null means "all". The stale-waiver rule is evaluated per result. (Flagged: not in the design;
  public surface +2 members.)
- **R4 — The design's raw-socket waivers shrink.** The design has the raw-socket driver waive `TRANSPORT-28` and `-13`. `transport-28.file-range-replayable`
  checks the byte range and replayability only (R3 of the design's residuals), which the raw client meets, so a `TRANSPORT-28` waiver would be stale and fail
  the run. The raw-socket driver therefore waives only `TRANSPORT-13` and `TRANSPORT-11` narrowed to `transport-11.drop-logged`, both `Owner = null`, reason
  "test-only transport", and declares `AfterDispose = Unspecified` (so `seam-15` is `Vacuous`). Task 2.4 records the measured set.
- **R5 — An assertion's `Level` is declared, not derived.** The catalogue gives a *row's* level; `TRANSPORT-11` is MUST for the framing clause and SHOULD for the
  logging clause, so `transport-11.drop-logged` is `Should` while its ID is `Must`. `ConformanceAssertion.Level` is declared per assertion, and a
  `Unit` test requires it not to exceed the strongest level among the assertion's IDs. (Reading of "the clause's level, from the generated catalogue".)
- **R6 — A passing xUnit `Skip` needs `Assert.Skip`.** The driver maps `Waived`/`Vacuous`/`NotExercised` to `Assert.Skip(reason)`. Task 0.1 confirms
  `Assert.Skip` exists in `xunit.v3` 4.0.1 (xUnit v3's dynamic skip) before the driver is written; if it does not, the driver uses `[Fact(SkipUnless=…)]`
  data rows and the plan's task 2.13/2.14 text changes to match.
- **R7 — `SystemNetHttpClient` is both faces.** It implements `IAsyncHttpClient` and `IHttpClient`, so the SystemNet subject supplies `CreateAsync` and
  `CreateBlocking` from one constructor. The blocking face is sync-over-async until 8b (design, "Two consequences"): the blocking-face assertions pass
  against it because they observe the async path's behaviour.
- **R8 — The kit reaches `Microsoft.Extensions.Logging.Abstractions` through core.** `TransportSettings.Logger` is an `ILogger`; the kit adds no
  `PackageReference` (the `ProjectReference` to core carries the band-matched package into the closure, and `dependency-audit.cs` verifies it).
- **R9 — Appendix files reach the test run as content.** The drift tests read `appendix-b-…` and `appendix-c-…` from the test output directory, copied by the
  csproj (`<None Include="..\..\docs\product-spec\appendix-*.md" Link="spec\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />`), the same
  pattern as core's tests' `..\vectors\**` copy. No test walks up the directory tree looking for the repository root.
- **R10 — `Hang`, `Gated` and `Abort` need the server's writer reshaped.** `LoopbackResponse` today is bytes plus an optional chunk stream. Task 1.9 gives it an
  internal `Gate` (`Task?`), a `Mode` (`Normal`, `Abort`, `Hang`) and a `Then` (the reply sent once the gate opens); the server's `WriteAsync` awaits the gate
  while watching the peer for a close, so a hanging reply still records the connection as released when the client gives up.
- **R11 — The `DelegateHttpClient` driver needs `TransportSubject.CreateBlocking` from the raw client too.** The raw client is written once as an async
  core with a blocking façade that calls the same code through a documented sync bridge in the test project (`AsBlocking` on the async delegate); the blocking
  face therefore tests the SDK's bridge plus the raw client, which is exactly what `SEAM-18` ships.
- **R13 — "Released" must be client-caused (finding F-A3).** A connection that the *server* closed after a `CloseConnection` reply (`Status`, `Ok`, `Large`,
  `Streamed` by default) is released whatever the client does, so a release check after such a reply is vacuous and its negative control cannot fail. The fixture
  therefore records, per connection, whether the server closed first (internal `LoopbackServer.ServerClosedFirst(connection)`, set before the release is signalled),
  and `Check.ReleasedAsync` raises a kit-bug `InvalidOperationException` (so `Errored`, never `Passed`) when the connection was server-closed. Every assertion whose
  clause is "the client released the connection" (`transport-7.*`, `transport-9`, `transport-15.owned-released`, `transport-22`, `transport-24`'s dispose clause,
  `transport-25.dispose-releases`) uses **keep-alive** replies (`Ok`/`Status`/`Large`/`Streamed` with `keepAlive: true`) or a reply that never closes (`Hang`, `Gated`).
  Task 1.9 adds `keepAlive` to `Streamed` as well. (Not in the design's member list; internal, flagged.)
- **F-A2 — `ASYNC-2`'s worker-pool-rejection clause is not met by core.** `SyncToAsyncAdapter.ExecuteAsync` returns `Task.Factory.StartNew(..., scheduler)` without a
  try/catch, so a scheduler whose `QueueTask` throws makes `AsAsync(scheduler).ExecuteAsync` throw `TaskSchedulerException` (wrapping the scheduler's exception)
  **synchronously**, contrary to appendix B.7.1 ("construction failures via the failure channel"). Fixing it is a core change (wrap and `Task.FromException`), which
  P8a-22 and convention 7 forbid for 8a, and the plan cannot override a design ruling. **Disposition:** that clause is ⏳ with owner "lead (core follow-up, any later
  phase that may touch core)"; no pin asserting the defect is added; the rest of `ASYNC-2` (work runs on the supplied scheduler, argument validation, kit
  `transport-21`) stays ✅. The finding is recorded in the task 3.7 status note and the 3.5 checklist's deviations. If the lead rules the core fix into 8a, it is a
  TDD red/green in `SyncToAsyncBridgeTests` (a scheduler throwing `InvalidOperationException` yields a faulted task whose exception is the `TaskSchedulerException`
  or its inner, the blocking client never called) plus a one-line `Task.FromException` wrap, and the row returns to ✅.
- **R12 — Ruby sources are absent (design R-sources, P8a-25).** Task 0.1 step 4 records whether any `TRANSPORT` row lacks a case after the Node and Ruby-design
  ports; only then would Ruby files be fetched (a read-only GitHub fetch the lead must authorise, per the global rule), so the plan assumes not.

---

## Task 0.1 — Pre-flight: queries, baseline and the facts to re-verify (no commit)

**Why first.** The design's facts F1–F7 are measurements on one host. Tasks 2.8–2.11 write assertions whose expected SystemNet result depends on them,
and the SystemNet waiver list is "exactly the expected failures" (design, catalogue); a wrong fact reopens a waiver, not a test.

1. Run the phase-start queries above. Record any difference from the design (30 + 22 IDs, 0 gaps, no `TRANSPORT`/`ASYNC` note or conflict).
2. Baseline: `dotnet restore Dexpace.Sdk.sln --locked-mode`, V-build, then `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build`
   must be green; note the per-suite counts. `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build
   --filter-class "*LoopbackServerTests"` is the fixture baseline (record the count; task 1.8 must reproduce it after the move).
3. In the scratchpad directory (not the repo) write a file-based app (`#:property PublishAot=false`) and `dotnet run` it against a raw `TcpListener` peer,
   printing: F1 (a response header name with a space is a whole-response `HttpRequestException`; a control byte in a value is not), F2 (non-numeric
   `Content-Length` on `Connection: close` gives 200 and a readable body), F3 (`CancelPendingRequests` and `Dispose` mid-call give
   `TaskCanceledException` with an `IOException` inner; `Timeout` gives a `TimeoutException` inner), F4 (body-less `POST` has `Content-Length: 0`, `GET`
   neither), F6 (a single-use `POST` body written once when the peer closes before a response on a reused connection). Any difference from the design's table is
   written into the task 3.4 knowledge note and, if it flips an expected-first-result column, into task 2.13's expected waiver list.
4. `xunit.v3` 4.0.1: confirm `Assert.Skip(string)` exists (a one-line scratch test in the scratchpad, or `grep` the package's XML docs in
   `~/.nuget/packages/xunit.v3.assert/*/lib/net8.0/*.xml`). Record the result against R6.
5. Confirm the appendix counts: `grep -c '^| [A-Z]*-[0-9]* |' docs/product-spec/appendix-c-consolidated-normative-requirement-index.md` is 645, and the
   B.6/B.7 bullet counts of R1 (5 and 6).
6. Confirm no file in `src/Dexpace.Sdk.Http.SystemNet` or `src/Dexpace.Sdk.Core` is on 8a's edit list (convention 7). If the other agent's 8b branch exists
   locally, `git diff main...<branch> --stat -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Loopback` must be empty (design, "Shared files with 8b").

No commit. If a fact differs, write the difference in the task 3.4 note draft and carry on; stop only if F6 or F3 is wrong in a way that makes a catalogue
row unreachable (then open the catalogue row as a design question in the final status note, not as a silent change).

---

# PR 1 — Group 1: the package, the report machinery, the catalogue and the fixture

## Task 1.1 — Scaffold `Dexpace.Sdk.Conformance` (the empty package)

**Files.** New: `src/Dexpace.Sdk.Conformance/Dexpace.Sdk.Conformance.csproj`, `GlobalUsings.cs`, `README.md`, `PublicAPI.Shipped.txt` (empty),
`PublicAPI.Unshipped.txt` (`#nullable enable`), `Requirements/RequirementLevel.cs` (the first public type, so the package is not empty). Edit:
`Dexpace.Sdk.sln` (via `dotnet sln`), `scripts/ci/dependency-audit.cs`.

**Tests first.** None can run yet: the test project arrives in 1.2. The red here is "the solution has no such project"; `dotnet build` of the new csproj is
the check.

**Production change.**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Dexpace.Sdk.Conformance</RootNamespace>
    <AssemblyName>Dexpace.Sdk.Conformance</AssemblyName>
    <PackageId>Dexpace.Sdk.Conformance</PackageId>
    <Description>
      Transport conformance kit for the dexpace SDK: framework-free assertions for the TRANSPORT and ASYNC requirements,
      a loopback HTTP/1.1 wire fixture, per-requirement reporting and waivers. Drive it from any test framework.
    </Description>
    <PackageTags>http;sdk;conformance;testing;dexpace</PackageTags>
    <IsPackable>true</IsPackable>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Dexpace.Sdk.Core\Dexpace.Sdk.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Dexpace.Sdk.Conformance.Tests" />
  </ItemGroup>

</Project>
```

No `Version`/`VersionPrefix` override (P8a-5: lockstep). `GlobalUsings.cs` mirrors the core's set (`System`, `System.Collections.Generic`, `System.IO`,
`System.Linq`, `System.Threading`, `System.Threading.Tasks`) with the license header and the same comment. `README.md`: a stub (title, the pre-release
callout, "Drive the kit from a test: see `docs/sdk-documentation/conformance.md`", the minor-version rule of P8a-5), completed in task 3.3. The `RequirementLevel`
enum (`Must`, `Should`, `May`) with XML docs citing `XCUT`-style RFC 2119 wording.

`dotnet sln Dexpace.Sdk.sln add src/Dexpace.Sdk.Conformance/Dexpace.Sdk.Conformance.csproj --solution-folder src`. In `scripts/ci/dependency-audit.cs` add
`["Dexpace.Sdk.Conformance"] = null,` to the `policies` table (core plus no third-party library; the table is where a reviewer sees it).

Run `dotnet restore Dexpace.Sdk.sln` (not locked: this creates `src/Dexpace.Sdk.Conformance/packages.lock.json`), then V-build.

**PublicAPI (`Unshipped`).** `Dexpace.Sdk.Conformance.RequirementLevel` plus its three members (`Must = 0`, `Should = 1`, `May = 2`); apply the code fix.

**Verify.** V-build; `dotnet pack src/Dexpace.Sdk.Conformance --configuration Release --output artifacts/packages` succeeds with package validation.

**IDs.** None (infrastructure). Rulings `P8a-4`, `P8a-5`, `P8a-16`.

## Task 1.2 — Scaffold `tests/Dexpace.Sdk.Conformance.Tests`

**Files.** New: `tests/Dexpace.Sdk.Conformance.Tests/Dexpace.Sdk.Conformance.Tests.csproj`, `GlobalUsings.cs`, `TestCategoryTests.cs`,
`Architecture/KitDependencyArchitectureTests.cs`. Edit: `Dexpace.Sdk.sln` (`dotnet sln … add tests/Dexpace.Sdk.Conformance.Tests/… --solution-folder tests`).

**Tests first.**

- `TestCategoryTests` (copy of the SystemNet suite's, using `Dexpace.Sdk.TestSupport.TestCategories`) needs a `TestSupport` reference. **Ruling:** the
  suite references `TestSupport` for `TestCategories` and the time doubles only; `Seam2ArchitectureTests`-style closure for this suite is therefore
  `{ Dexpace.Sdk.Core, Dexpace.Sdk.Conformance, Dexpace.Sdk.TestSupport, Dexpace.Sdk.Conformance.Tests }` (the design's "references core and the kit only" is
  read as "no transport": `Dexpace.Sdk.Http.*` and `Dexpace.Sdk.Serialization.*` never appear). Flagged.
- `KitDependencyArchitectureTests` (`Unit`): (a) the kit assembly's referenced assemblies, by simple name, are a subset of `{ Dexpace.Sdk.Core,
  Microsoft.Extensions.Logging.Abstractions }` plus framework assemblies (`System.*`, `Microsoft.Extensions.*` is excluded from the framework test: it is
  checked by name); (b) none starts with `xunit`, `NUnit`, `MSTest`, `Microsoft.VisualStudio.TestPlatform`, `Dexpace.Sdk.Http`, `Dexpace.Sdk.Serialization`.
  Written against `typeof(RequirementLevel).Assembly`.

**Production change.** csproj as the SystemNet suite's (`OutputType Exe`, `IsTestProject true`, `GenerateDocumentationFile false`, `NoWarn CS1591`,
`xunit.v3`, `coverlet.MTP`, `Microsoft.Testing.Extensions.TrxReport`), project references to core, the kit and `TestSupport`, and the appendix copy of R9:

```xml
<ItemGroup>
  <None Include="..\..\docs\product-spec\appendix-b-conformance-test-checklist.md" Link="spec\appendix-b.md" CopyToOutputDirectory="PreserveNewest" />
  <None Include="..\..\docs\product-spec\appendix-c-consolidated-normative-requirement-index.md" Link="spec\appendix-c.md" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`GlobalUsings.cs` as the SystemNet suite's plus `global using System.Threading;` already; `restore` (unlocked) to create the lock file.

**Verify.** V-build; V-fast `*TestCategoryTests` and `*KitDependencyArchitectureTests` (both green on the first run: they are pins; prove (b) can fail by
temporarily adding a `ProjectReference` to `Dexpace.Sdk.Http.SystemNet` to the **kit** csproj **and a use of one of its types in a kit source file**, for example
`_ = typeof(Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient);` — the compiler emits an `AssemblyRef` only for assemblies whose metadata the code uses, so an unused
reference leaves the test green and proves nothing — then revert both).

**IDs.** None. `P8a-26`.

## Task 1.3 — Statuses, faces, exceptions and the waiver

**Files.** New (kit): `Reporting/ConformanceStatus.cs`, `Reporting/TransportFace.cs`, `Subjects/AfterDisposeBehavior.cs`,
`ConformanceException.cs`, `ConformanceVacuousException.cs`, `Reporting/ConformanceWaiver.cs`. New (tests): `Reporting/ConformanceExceptionTests.cs`,
`Reporting/ConformanceWaiverTests.cs`. Edit: kit `PublicAPI.Unshipped.txt`.

**Tests first** (`Unit`; red = missing types).

- `ConformanceException`: is an `Exception` and **not** an `SdkException` (`Assert.False(typeof(SdkException).IsAssignableFrom(typeof(ConformanceException)))`, the
  reason of P8a-6); the `(message, expected, actual)` constructor sets `Expected`/`Actual`; the plain constructors leave them `null`; `Message` does not
  contain `Expected`/`Actual` text unless the author put it there (the report renders them, the message stays one line). `ConformanceVacuousException` likewise.
- `ConformanceWaiver`: `new ConformanceWaiver("TRANSPORT-8", "reason") { Owner = "8b" }` keeps its fields; an unknown ID (`"TRANSPORT-99"`, `"NOPE-1"`, `""`,
  null) throws `ArgumentException` (null: `ArgumentNullException`) whose message does not echo an attacker-length input beyond 64 chars; an empty
  `Reason` throws; `Assertion` and `Face` default to `null` (R3); records compare by value. **The unknown-ID check needs the catalogue (task 1.5): this task's
  test for it is written now and marked `[Fact(Skip = "enabled in task 1.5")]` using the plain xUnit skip, then un-skipped in 1.5. Nothing else is skipped.**

**Production change.** `ConformanceStatus { Passed, Failed, Errored, Vacuous, Waived, NotExercised }` (documented with the six definitions of the design,
section C); `TransportFace { Async, Blocking }`; `AfterDisposeBehavior { Unspecified, ThrowsObjectDisposedException }`. The two exceptions are `sealed`, in
the design's shape (four constructors for the first; the three for the second, plus the CA1032-style standard set the analyzer demands). The waiver:

```csharp
public sealed record ConformanceWaiver(string RequirementId, string Reason)
{
    /// <summary>The phase that will remove this waiver (for example "8b"), or <see langword="null"/> for a permanent decision.</summary>
    public string? Owner { get; init; }

    /// <summary>Narrows the waiver to one assertion by name; <see langword="null"/> means every assertion citing <see cref="RequirementId"/>.</summary>
    public string? Assertion { get; init; }

    /// <summary>Narrows the waiver to one face; <see langword="null"/> means both.</summary>
    public TransportFace? Face { get; init; }
}
```

(The primary-constructor parameters are validated by a `private readonly` backing pattern or an explicit constructor; the unknown-ID check against the requirement
index is added in 1.5, the non-empty checks now. `Assertion` is **never** validated against the assertion catalogue at construction: a `Reporting` value type must not
depend on the static catalogue, and at task 2.2 the catalogue is empty. An unknown `Assertion` name is detected when waivers are applied, in the runner; see 2.2.)

**PublicAPI.** The enum members, the exception constructors and properties, the waiver record's members including `Assertion`/`Face` (R3); apply the code fix.

**Verify.** V-build, V-fast with the two classes. **IDs.** `P8a-6`, `P8a-7`, `P8a-8`.

## Task 1.4 — `ConformanceAssertion`, `ConformanceResult`, `ConformanceReport`

**Files.** New (kit): `Reporting/ConformanceAssertion.cs`, `Reporting/ConformanceResult.cs`, `Reporting/ConformanceReport.cs`,
`Reporting/StatusSeverity.cs` (internal). New (tests): `Reporting/ConformanceReportTests.cs`, `Reporting/StatusSeverityTests.cs`.

**Tests first** (`Unit`).

- `StatusSeverity.Rank`: the order of the design (`Errored` > `Failed` > `Waived` > `NotExercised` > `Vacuous` > `Passed`) is a total order; a theory over all
  36 ordered pairs of the six statuses; `Worst(IEnumerable<ConformanceStatus>)` of an empty sequence throws.
- `ConformanceReport.Create("subject", results)`: `IsGreen` is true for `Passed`/`Waived`/`Vacuous`/`NotExercised` mixes, false with one `Failed` or one
  `Errored`; `ByRequirement` maps **each ID of each result's assertion** to the worst status among results citing it (a hand-built result list of three
  assertions over `TRANSPORT-1` and `SEAM-13` checks the max and the multi-ID fan-out); an ID with no result is absent from the map;
  `Results` is read-only and a defensive copy.
- `ToString()`: starts with `ConformanceReport.Preamble`; the preamble states the two omissions (no TLS and HTTP/2, no connect-timeout scripting) and the
  words "does not prove" (P8a-12); the per-ID table lists IDs in `RequirementId` natural order (`TRANSPORT-2` before `TRANSPORT-10`); every `Waived` result's
  waiver reason appears; every `NotExercised` assertion name appears with its reason (the result `Detail`); waivers are listed even when the run is green
  (§9.3). `ToString()` is stable: two calls are equal.
- `ConformanceAssertion` is a sealed class whose constructor is **internal** (`name`, `ids`, `level`, `faces`) **with no body**: the body's delegate type takes
  `SuiteContext`, which task 2.2 creates, so task 2.2 adds the `body` parameter (and an internal `Body` property) and updates every 1.4 test that builds an instance in
  the same commit (no red commit). `Name`, `RequirementIds`, `Level`, `Faces` and `ToString()` (= `Name`) are public. Tests build instances through `InternalsVisibleTo`.

**Production change.** The three types per the design's surface. `ConformanceResult(ConformanceAssertion Assertion, TransportFace Face, ConformanceStatus
Status, string Detail)` with `Exception? Exception { get; init; }` and `ConformanceWaiver? Waiver { get; init; }`. `ConformanceReport` is built by
`Create`; `ByAppendixBItem` is wired in 1.6 (returns an empty dictionary until then; its test lands in 1.6). Rendering uses `StringBuilder` with
`CultureInfo.InvariantCulture`; no `Console`.

**PublicAPI.** The three types' public members; apply the code fix. **Verify.** V-build; V-fast with the three classes. **IDs.** `P8a-7`, `P8a-8`, `P8a-12`.

## Task 1.5 — The requirement catalogue, generated from appendix C

**Files.** New: `scripts/ci/requirement-catalog.cs`, `src/Dexpace.Sdk.Conformance/Requirements/RequirementCatalog.g.cs` (generated, checked in, `internal`),
`src/Dexpace.Sdk.Conformance/Requirements/RequirementIndex.cs` (the internal lookup; **not** named `Requirements`, which would collide with a namespace of that name: a
caller in `Dexpace.Sdk.Conformance` would resolve `Requirements.Contains` to the namespace, CS0234. No namespace called `Requirements` exists anywhere: the `Requirements/`
folder's files, the generated file included, declare `namespace Dexpace.Sdk.Conformance;`, and the test project's folder uses the namespace `Dexpace.Sdk.Conformance.Tests.Catalogue`), `tests/Dexpace.Sdk.Conformance.Tests/Requirements/RequirementCatalogTests.cs` (namespace `…Tests.Catalogue`).
Edit: `ConformanceWaiver.cs` (validate `RequirementId` against `RequirementIndex`), `ConformanceWaiverTests.cs` (un-skip the 1.3 test), `.editorconfig` only if the generated file
needs `generated_code = true` (it should carry `// <auto-generated/>` instead, which the analyzers already skip, and the license header on its second block).

**Tests first** (`Unit`; red = missing `RequirementCatalog`).

- Drift: parse `spec\appendix-c.md` in the test (independently of the script: each row matching `^\| ([A-Z]+-\d+) \| (MUST NOT|MUST|SHOULD|MAY) \|`) and
  compare to `RequirementCatalog.Rows`: same IDs in the same order, same levels (`MUST NOT` maps to `Must`). Count is 645 (read from the file's own
  "lists 645 requirements" sentence and compared to both).
- Lookup: `RequirementIndex.TryGetLevel("TRANSPORT-24", out var level)` is `Must`; `"TRANSPORT-6"` is `Should`; `"SEAM-15"` is `May`; `"NOPE-1"` is false;
  lookup is ordinal and case-sensitive.
- The un-skipped waiver test of 1.3.
- The script's `--check` mode (a manual V-gate step, not a unit test): exits 0 when `RequirementCatalog.g.cs` matches a fresh render, 1 with a diff
  summary when not, 2 on usage error.

**Production change.** The script is a file-based app in the style of `scripts/ci/coverage-gate.cs` (license header, `#:property
RestorePackagesWithLockFile=false`, `#:property PublishAot=false`, usage comment). `dotnet run scripts/ci/requirement-catalog.cs --` writes the file;
`-- --check` compares. The generated file:

```csharp
// <auto-generated> by scripts/ci/requirement-catalog.cs from docs/product-spec/appendix-c-consolidated-normative-requirement-index.md. Do not edit. </auto-generated>
// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

internal static class RequirementCatalog
{
    internal static readonly (string Id, RequirementLevel Level)[] Rows =
    [
        ("SEAM-1", RequirementLevel.Must),
        // … 645 rows, in appendix order …
    ];
}
```

`RequirementIndex` builds a `FrozenDictionary<string, RequirementLevel>` once (`TryGetLevel`, `Contains`, `All`). A row of the generated file is one line, so the
file diffs cleanly when appendix C changes. `ConformanceWaiver` validates `RequirementId` with `RequirementIndex.Contains` and, for `Assertion`, only that it is
non-empty when set; membership of an assertion name is checked by the runner when waivers are applied (2.2) and by a catalogue unit test (2.12).

**Verify.** `dotnet run scripts/ci/requirement-catalog.cs` (writes), then `-- --check` (exit 0), V-build, V-fast `*RequirementCatalogTests`,
`*ConformanceWaiverTests`. **IDs.** `P8a-13`, `P8a-8`.

## Task 1.6 — The appendix B view (`B.6` and `B.7`)

**Files.** New (kit): `Requirements/AppendixBMap.cs` (internal). New (tests): `Requirements/AppendixBMapTests.cs`. Edit: `ConformanceReport.cs`.

**Tests first** (`Unit`).

- Drift: the test parses `spec\appendix-b.md`: the lines between `### B.6` and `### B.7` and between `### B.7` and `### B.8` that start with `- [ ]`, in order, each
  scanned for `(TRANSPORT-n)` / `(ASYNC-n)` tokens (the sibling bullet's ID groups may be parenthesised singly or in lists); the result must equal `AppendixBMap.Items`
  exactly: `B.6.1`→{`TRANSPORT-1`,`-2`}, `B.6.2`→{`-3`..`-9`}, `B.6.3`→{`-10`..`-14`}, `B.6.4`→{`-15`..`-19`}, `B.6.5`→{`-20`..`-30`}, `B.7.1`→{`ASYNC-1`,`-2`},
  `B.7.2`→{`-3`..`-7`}, `B.7.3`→{`-8`..`-12`}, `B.7.4`→{`-13`,`-14`}, `B.7.5`→{`-15`..`-17`}, `B.7.6`→{`-18`..`-22`} (R1). Every ID of the map exists in the catalogue.
- `ByAppendixBItem`: a hand-built report with `TRANSPORT-3` `Passed` and `TRANSPORT-8` `Failed` has `B.6.2 == Failed`, `B.6.1` absent (no result cites its IDs);
  with `ASYNC-21` `Vacuous` and the rest of `B.7.6` `Passed`, `B.7.6 == Vacuous`. Worst-of per `StatusSeverity`.

**Production change.** `AppendixBMap.Items` is a hand-written `FrozenDictionary<string, string[]>` with a comment naming the appendix section and the phase
that widens it (10: all 61 items). `ConformanceReport.ByAppendixBItem` is computed from `ByRequirement`. `ToString()` gets a second table ("Appendix B items").

**Verify.** V-build; V-fast `*AppendixBMapTests`, `*ConformanceReportTests`. **IDs.** `P8a-14`.

## Task 1.7 — Library-grade fixture (1/2): move the files, keep the behaviour

**Files.** `git mv` `tests/Dexpace.Sdk.Http.SystemNet.Tests/Loopback/{LoopbackServer,LoopbackResponse,RecordedRequest,RawRequestReader,MalformedRequestException}.cs`
to `src/Dexpace.Sdk.Conformance/Wire/`; `git mv` `LoopbackServerTests.cs` to `tests/Dexpace.Sdk.Conformance.Tests/Wire/`. Edit: every file that says
`using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;` (the SystemNet suite's wire tests, found by `grep -rl "SystemNet.Tests.Loopback" tests`), the SystemNet test csproj
(add a `ProjectReference` to the kit and update its fixture comment; then run `dotnet restore Dexpace.Sdk.sln` and **commit the changed
`tests/Dexpace.Sdk.Http.SystemNet.Tests/packages.lock.json`**, which lists project references), the moved files (namespace, usings, `ConfigureAwait`, docs, splits).

**Tests first.** The moved `LoopbackServerTests` is the pin: it must compile and pass unedited in behaviour. Change only its namespace
(`Dexpace.Sdk.Conformance.Tests.Wire`), its usings, and replace the SystemNet-specific client by a plain `HttpClient` over `SocketsHttpHandler`
**in the test project** (banned only in `src/`; the test project is not a library). The three tests that drive `SystemNetHttpClient` there are kept by moving
them to a new `tests/Dexpace.Sdk.Http.SystemNet.Tests/LoopbackFixtureViaTransportTests.cs` (`Integration`) **or** rewritten over `HttpClient`; choose per test
by whether the assertion is about the transport or about the fixture (the fixture's own claims — it records exact bytes, it sends exact bytes — need only a
`HttpClient`). Record the before/after counts: the moved class plus any relocated tests must equal the 0.1 baseline.

**Production change** (mechanical, behaviour-preserving).

1. Namespace `Dexpace.Sdk.Conformance.Wire`; `LoopbackServer`, `LoopbackResponse`, `RecordedRequest`, `MalformedRequestException` stay `public`, `RawRequestReader`
   becomes `internal`.
2. `ConfigureAwait(false)` on every await in the five files (`await foreach … .WithCancellation(_stopping.Token).ConfigureAwait(false)`, `await using`,
   `await _stopping.CancelAsync().ConfigureAwait(false)`, …). `AcceptTcpClientAsync`, `ReadAsync`, `WriteAsync`, `FlushAsync` follow.
3. `MA0051`: `RawRequestReader` methods over 70 lines are split at their natural seams (head parse / body framing / chunk parse); `LoopbackServer.ServeRequestsAsync`
   stays under the cap after 1.8–1.9 add to it, so extract `ReadRequestAsync` (the malformed-request branch) now.
4. The `CA1031` pragmas keep their why-comments; `LoopbackServer`'s remarks drop the sentence "roadmap phase 8a promotes it" (now true) and gain the
   library-grade contract: BCL-only, no test-framework types, every await configured, thread-safe observation members.
5. Full XML docs on every public member (`CS1591`); `RecordedRequest`'s internal constructor stays internal (tests reach it through `InternalsVisibleTo`).
6. The kit's `GlobalUsings.cs` already has `System.Linq` (the fixture uses it); add `System.Net`, `System.Net.Sockets`, `System.Text` per-file as today.

**PublicAPI.** The fixture's public surface (as today: the existing members of the four public types), applied by the code fix and compared by eye with the
design's "promoted" list.

**Verify.** V-build; `dotnet test --project tests/Dexpace.Sdk.Conformance.Tests … --filter-class "*LoopbackServerTests"` equals the 0.1 count;
`dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests … --no-build` fully green (every wire test and the `Security` classes with a `using` swap only).
`git diff -U0 -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security | grep '^[+-][^+-]' | grep -v '^[+-]using '` prints nothing (convention 5).

**IDs.** `P8a-11`. (Constraint 4: the fixture is the one wire seam.)

## Task 1.8 — Fixture (2/2a): request arrival and release observation

**Files.** Edit: `Wire/LoopbackServer.cs` (and, if it grows past ~330 lines, new `Wire/ConnectionTracker.cs`, internal). Tests: `Wire/LoopbackServerObservationTests.cs`
(`Integration`, new, in the kit's test project).

**Tests first** (red = missing members; clients are plain `HttpClient`/`TcpClient` in the test).

- `WaitForRequestAsync(0, ct)` completes when the first request has been recorded and returns the same `RecordedRequest` as `Requests[0]`; waited before the
  request is sent, it completes after; it honours cancellation (`OperationCanceledException`); it never completes for an index never reached, and disposing the
  server faults the wait with `ObjectDisposedException`-or-cancellation, not a hang (bounded by the test's `WaitAsync(5 s)` on the **non-generic** task).
- `WaitForConnectionReleasedAsync(connection, ct)` completes when: (a) the client closes the connection after a keep-alive reply; (b) the client sends a second request
  on the connection; (c) the client closes while the server is **mid-write** of a chunked/large reply (write fails with `IOException`); (d) the client closes while
  the reply is gated (needs 1.9, so this case is added there); (e) the server itself closes after a `CloseConnection` reply (the exchange and connection are gone; the wait completes for completeness, but
  `ServerClosedFirst(connection)` is `true`, which a test asserts, and `false` for cases (a)-(d); R13). It
  does **not** complete while the client holds a keep-alive connection idle and silent (assert still-pending after a short bounded wait using a
  `TimeProviderWaits.DelayAsync` of 250 ms on the real clock; this is the one timed negative of suite-contract clause 7).
- An out-of-range `connection` index throws `ArgumentOutOfRangeException` synchronously; a not-yet-accepted index waits for that connection.

**Production change.** A per-connection `TaskCompletionSource` (non-generic; allowed) held in a `ConcurrentDictionary<int, TaskCompletionSource>`-equivalent
list created on accept. It is completed in `ServeAsync`'s `finally` (any exit of the connection), on the second and later request of a connection in
`ServeRequestsAsync`, on a read that returns EOF between requests, and on a write `IOException`/`SocketException`. `WaitForRequestAsync` is a loop over a
`TaskCompletionSource` "arrival" signal reset per enqueue (a `SemaphoreSlim`-free design: complete-and-replace under a lock). Both waits link `_stopping` so
disposal ends them.

The tracker also records `ServerClosedFirst` (internal, R13) before it signals release when the server closes after a `CloseConnection` reply or resets (`Abort`).

**PublicAPI.** `LoopbackServer.WaitForRequestAsync`, `LoopbackServer.WaitForConnectionReleasedAsync`; apply the code fix (`ServerClosedFirst` is internal).

**Verify.** V-build; V-fast `*LoopbackServerObservationTests` and `*LoopbackServerTests`. **IDs.** `P8a-21` (supports `TRANSPORT-7`, `-15`, `-22`, `-25`).

## Task 1.9 — Fixture (2/2b): the new replies (gated, abort, hang, large, echo, keep-alive)

**Files.** Edit: `Wire/LoopbackResponse.cs`, `Wire/LoopbackServer.cs`. New: `Wire/LargeBody.cs` (internal: the deterministic generator and its SHA-256).
Tests: `Wire/LoopbackResponseScriptTests.cs` (`Integration`).

**Tests first** (red = missing factories; clients are `TcpClient`/`HttpClient` in the test).

- `Gated(gate, then)`: nothing is written until `gate` completes (read with a 250 ms bounded no-bytes check on the raw socket), then `then`'s bytes arrive
  intact; a faulted or cancelled gate ends the connection without a reply.
- `HeadersThenGate(200, headers, gate, rest)`: the status line and headers arrive (chunked or with a declared `Content-Length` that matches
  `rest.Length`) and **no** body byte before the gate; `rest` after.
- `Abort()`: the request is read and recorded, then the connection is reset (`LingerState(true, 0)` then close) with zero response bytes; the client sees an
  `IOException`/`SocketException`/`HttpRequestException`, and `ConnectionCount` is unchanged by it.
- `Hang()`: the request is read and recorded, nothing is ever written, the connection stays open; disposing the server ends it; if the client closes first,
  `WaitForConnectionReleasedAsync` completes (the 1.8 case (d), asserted here for `Hang` and `Gated`).
- `Large(bytes, seed)`: `200`, `Content-Length: bytes`, body of `bytes` pseudo-random octets from `new Random(seed)`; two calls with equal arguments are
  byte-identical; `LargeBody.Sha256(bytes, seed)` equals the SHA-256 of what a client reads; `bytes <= 0` throws `ArgumentOutOfRangeException`.
- `EchoId(request)`: a `200` whose body is `request.Header("X-Conformance-Id") ?? ""`; used as `Start(r => LoopbackResponse.EchoId(r))`.
- Keep-alive (R2, R13): `Ok("x", keepAlive: true)` and `Streamed(..., keepAlive: true)` have no `Connection: close` header, does not close the connection, and a second request on the same
  connection is served; default `keepAlive: false` bytes are byte-identical to today's (a pin on the existing format).

**Production change** (R10). `LoopbackResponse` gains private fields `Gate`, `Mode`, `Then` and the six factories; `Bytes`/`Chunks`/`CloseConnection` keep their
meaning for `Normal`. `LoopbackServer.WriteAsync` becomes `WriteReplyAsync(client, connection, reply)` which: awaits the gate through `WatchWhileAsync`
(starts a one-byte `stream.ReadAsync` on the connection concurrently; a 0-byte read or an exception means the peer left: mark released, stop); for `Abort`
sets `client.Client.LingerState = new LingerOption(true, 0)` and disposes; for `Hang` awaits a non-generic `TaskCompletionSource` bound to `_stopping`
(`await tcs.Task.WaitAsync(_stopping.Token)` on the non-generic task is allowed); otherwise writes as today. `Large` writes its body in 64 KiB slices so a client
that disposes mid-body produces a write failure the server records as a release, not as a `Faults` entry (a peer reset mid-write is expected, like
shutdown). `LargeBody` generates by `new Random(seed).NextBytes(buffer)` over the whole array once (8 MiB is fine) and caches nothing static.

**PublicAPI.** The six factories and the `keepAlive` parameters (changing `Status`/`Ok` signatures: edit the existing lines).

**Verify.** V-build; V-fast `*LoopbackResponseScriptTests`, `*LoopbackServerObservationTests`, `*LoopbackServerTests`; then
`dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --no-build` still green (the existing wire tests never use the new members).

**IDs.** `P8a-11`, `P8a-21`.

## Task 1.10 — Kit internals: the recording logger, counting streams and the bounded wait

**Files.** New (kit, all `internal`): `Internals/RecordingLogger.cs`, `Internals/CountingStream.cs`, `Internals/ParkedReadStream.cs`, `Internals/Bounded.cs`.
Tests: `Internals/RecordingLoggerTests.cs`, `Internals/CountingStreamTests.cs`, `Internals/BoundedTests.cs` (`Unit`).

**Tests first.**

- `RecordingLogger : ILogger` records `(LogLevel, EventId, string Message, IReadOnlyList<KeyValuePair<string,object?>> State)` per call, thread-safely;
  `Entries` is a snapshot; `IsEnabled` is true for every level; `BeginScope` returns a no-op disposable. `Dropped(HeaderName)` convenience: entries whose message
  names the header case-insensitively (the Node `captureDroppedHeaders` port, used by `transport-11.drop-logged`/`transport-13`).
- `CountingStream` wraps a `Stream`, counts bytes read (`BytesRead`), `ReadCalls`, and disposal (`IsDisposed`, `DisposeCount`); it is non-seekable by
  configuration; `ParkedReadStream` blocks its first `ReadAsync` until a gate (a non-generic `TaskCompletionSource`) opens or the token fires, exposing
  `ReadStarted` (a task) and `ObservedCancellation` (set when the parked read saw its token) — the `transport-19` instrument.
- `Bounded.RunAsync(Func<CancellationToken, Task> body, TimeSpan bound, string assertionName, CancellationToken outer)`: runs `body` under a token that fires after
  `bound` (`CancellationTokenSource.CancelAfter`, not `Task.Delay`); a timeout throws `ConformanceException("… exceeded the 30 s bound …")` naming the bound, never
  `OperationCanceledException`; the caller's own cancellation propagates as `OperationCanceledException`. `Bounded.WaitAsync(Task, TimeSpan, string what, ct)`
  applies a bound to a **non-generic** task.

**Production change.** The four internal types (<= 70 lines per method), documented. No reflection, no `Console`.

**Verify.** V-build; V-fast the three classes. **IDs.** `P8a-9` (suite-contract clauses 6, 7), `P8a-23`.

## Task 1.11 — Coverage, packaging and CI wiring for PR 1

**Files.** Edit: `scripts/ci/dependency-audit.cs` (done in 1.1; verify), `.github/workflows/ci.yml` (header only, deferred to 2.16), `README.md` of the kit (stub).

1. `dotnet pack Dexpace.Sdk.sln --configuration Release --output artifacts/packages` then `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages`:
   `Dexpace.Sdk.Conformance` is named and passes (closure: core, `Microsoft.Extensions.Logging.Abstractions`, nothing else). If the audit reports the kit's nuspec lists
   `Dexpace.Sdk.Core` with a version range, that is expected (it does for SystemNet).
2. `scripts/ci/reproducible-pack.sh`: packs twice, byte-compares, now including the kit.
3. Coverage: run the coverage command of "Verification blocks" over the whole solution and `coverage-gate.cs … 80`. The kit shows a line in the per-library table. PR 1
   carries the report, fixture and catalogue code with `Unit` + `Integration` tests, so the kit's own aggregate should be well above the floor; if the kit pulls the
   aggregate under 80, the assertion code of PR 2 will be added with its controls, so a temporary dip is **not** acceptable in a PR that must be green on its own:
   add targeted tests (the report's rendering branches, the fixture's malformed-request branch) until the gate is green. The selftest
   (`coverage-gate-selftest.sh`) must still fail closed on a missing report.

**Verify.** The commands above. **IDs.** `P8a-16`.

## Task 1.12 — Group 1 gate

Run V-gate. Expected: the new `Unit` + `Integration` rows in `Dexpace.Sdk.Conformance.Tests`; SystemNet's suite unchanged in count except the relocated fixture
tests; zero `src/Dexpace.Sdk.Http.SystemNet` or `src/Dexpace.Sdk.Core` diff; the kit's `PublicAPI.Unshipped.txt` holds exactly: `RequirementLevel`, the statuses/faces/
behaviours, the two exceptions, the waiver (with `Assertion`/`Face`), `ConformanceAssertion`/`Result`/`Report`, and the four fixture types. Commit style `feat:`
(one commit per task is fine; PR title `feat: phase 8a conformance kit, package and wire fixture (TRANSPORT, ASYNC; part 1/3)`).

---

# PR 2 — Group 2: subject, runner, raw-socket client, the 40 assertions and the two drivers

Phase-start queries re-run. Group 2 follows group 1 (stacked: branch off PR 1's head, base the PR on it).

## Task 2.1 — The subject, its hooks and the suite options

**Files.** New (kit): `Subjects/TransportSettings.cs`, `Subjects/BorrowedTransport.cs`, `Subjects/InternalCancellation.cs`, `Subjects/TransportSubject.cs`,
`Subjects/TransportSuiteOptions.cs`. Tests: `Subjects/TransportSubjectTests.cs`, `Subjects/BorrowedTransportTests.cs` (`Unit`).

**Tests first.**

- `TransportSubject` requires `Name` (the `required` member); every hook defaults to `null`; `AfterDispose` defaults to `Unspecified`. A subject with neither factory
  is legal (the runner reports every row `NotExercised`).
- `BorrowedTransport`: null arguments throw; `DisposeAsync` disposes the native owner exactly once however many times it is called (counting double) and **never
  disposes the transport itself** (that is the assertion's subject, `TRANSPORT-15`); `SendThroughNativeClient` is exposed as given.
- `InternalCancellation`: null arguments throw; `CancelInFlight` is the supplied delegate.
- `TransportSuiteOptions` defaults: `Waivers` empty (never null), `AssertionTimeout` 30 s, `ReleaseTimeout` 10 s; a non-positive timeout throws
  `ArgumentOutOfRangeException` at the `init` (use explicit `init` accessors that validate).
- `TransportSettings.Logger` is `required`.

**Production change.** The five types per the design's surface (documented with the hook table; each hook's XML summary names the clause that needs it and the
status a missing hook produces). `TransportSettings` is a `sealed record` with only `Logger`; it exists so later settings are additive.

**PublicAPI.** The five types; apply the code fix. **Verify.** V-build; V-fast the two classes. **IDs.** `P8a-9`, `P8a-19`, `P8a-22`.

## Task 2.2 — The runner: `TransportSuite`, the per-face send primitive and the waiver rule

**Files.** New (kit): `Suite/TransportSuite.cs` (public), `Suite/SuiteRunner.cs`, `Suite/SuiteContext.cs`, `Suite/IFaceTransport.cs`,
`Suite/AsyncFaceTransport.cs`, `Suite/BlockingFaceTransport.cs`, `Suite/NotExercisedException.cs` (all internal but the first). Edit: `ConformanceAssertion.cs`
(add the internal `body` constructor parameter and `Body` property, and update the 1.4 tests that construct assertions in the same commit), `ConformanceReport`-facing
code unchanged. `ConformanceWaiver.cs` is **not** edited (an unknown `Assertion` name is detected by the runner, below). Tests: `Suite/SuiteRunnerTests.cs`,
`Suite/SuiteContextTests.cs`, `Suite/FaceTransportTests.cs` (`Unit`).

**Tests first** (red = missing types; the runner is driven with hand-built assertions through `InternalsVisibleTo`, so these tests do not depend on the catalogue
of 40 and stay stable as it grows).

- **Statuses.** A body that returns is `Passed`; throwing `ConformanceException("m", "exp", "act")` is `Failed` with `Detail` containing `m`, `exp` and `act` and
  `Exception` set; any other exception (`InvalidOperationException`) is `Errored`; throwing `ConformanceVacuousException("no antecedent")` is `Vacuous` with that reason;
  `SuiteContext.RequireHook(subject.CreateBorrowed, nameof(...))` on a missing hook is `NotExercised` naming the hook; a subject without the requested face is
  `NotExercised` naming the face; a body that outlives `AssertionTimeout` (set to 300 ms) is `Failed` naming the bound and the assertion; the outer token cancelled
  rethrows `OperationCanceledException` (not `Errored`).
- **Unknown waiver target.** The runner validates `Assertion` names when it applies waivers, against the assertions it actually runs: a waiver with `Assertion` set
  to a name no run assertion has makes that run's report non-green through an extra `Failed` result (assertion name `waiver`, detail `unknown waiver target
  '<name>'`, truncated to 64 chars); a waiver with `Assertion` null is never an unknown target. Tests use hand-built assertions, so this does not depend on the
  catalogue.
- **Waivers.** A matching waiver turns `Failed` and `Errored` into `Waived` carrying the waiver; it does **not** touch `Passed`'s sibling results; a waiver whose
  assertion already `Passed` turns that result into `Failed` with detail `waiver for TRANSPORT-n is no longer needed` (P8a-8); narrowing by `Assertion` and `Face`
  (R3) matches only that pair; a waiver for an ID the assertion does not cite does nothing; a waiver never converts `Vacuous` or `NotExercised`.
- **Lifecycle (suite-contract clauses 1, 8).** Every transport the context built is disposed on pass, fail, error, timeout and cancellation (a counting factory
  asserts built == disposed); two assertions get two transports and two servers (a factory call counter and distinct `server.BaseUri.Port`s); the internal `SuiteRunner.RunAllAsync(subject, IReadOnlyList<ConformanceAssertion> assertions, options, ct)` overload (the public
  `TransportSuite.RunAllAsync(subject, options, ct)` delegates to it with `TransportSuite.Assertions`; the public signature has no assertion parameter, so the tests
  that use hand-built assertions drive this overload through `InternalsVisibleTo`) runs assertions sequentially in the given order and again in reverse order with identical per-row statuses (order independence, using a pair of hand-built assertions
  that would interfere if they shared a server).
- **Faces (clause 10 of P8a-10).** An assertion declaring both faces yields two results from the internal `RunAllAsync` overload, one declaring `Async` yields one; the `Async` face
  routes through `ExecuteAsync`, the `Blocking` face through `Execute` on a dedicated thread (a recording transport proves which member ran and that two blocked calls
  do not starve each other: both complete with `LongRunning`); undeclared face via `RunAsync` throws `ArgumentException`.
- **`TransportSuite.RunAsync/RunAllAsync`** argument checks (null subject/assertion/face out of range); `Assertions` is a stable, read-only list.
- **Report wiring.** `RunAllAsync` (either overload) returns `ConformanceReport.Create(subject.Name, results)` with every result and, because of the stale rule, a green report only when no
  waiver is stale.

**Production change.** The heart is the runner, shown in full because every assertion depends on it:

```csharp
internal static class SuiteRunner
{
    internal static async Task<ConformanceResult> RunAsync(
        TransportSubject subject, ConformanceAssertion assertion, TransportFace face, TransportSuiteOptions options, CancellationToken ct)
    {
        if (!assertion.Faces.Contains(face))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' does not declare the {face} face.", nameof(face));
        }

        if (!SuiteContext.Supplies(subject, face))
        {
            return Result(assertion, face, ConformanceStatus.NotExercised, $"the subject supplies no {face} factory");
        }

        await using var context = new SuiteContext(subject, face, options);
        var outcome = await ExecuteBodyAsync(assertion, context, options, ct).ConfigureAwait(false);
        return ApplyWaivers(outcome, assertion, face, options.Waivers);
    }

    private static async Task<ConformanceResult> ExecuteBodyAsync(
        ConformanceAssertion assertion, SuiteContext context, TransportSuiteOptions options, CancellationToken ct)
    {
        try
        {
            await Bounded.RunAsync(token => assertion.Body(context, token), options.AssertionTimeout, assertion.Name, ct).ConfigureAwait(false);
            return Result(assertion, context.Face, ConformanceStatus.Passed, "passed");
        }
        catch (NotExercisedException ex) { return Result(assertion, context.Face, ConformanceStatus.NotExercised, ex.Message); }
        catch (ConformanceVacuousException ex) { return Result(assertion, context.Face, ConformanceStatus.Vacuous, ex.Message); }
        catch (ConformanceException ex) { return Result(assertion, context.Face, ConformanceStatus.Failed, Describe(ex)) with { Exception = ex }; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
#pragma warning disable CA1031 // A transport crash or a kit bug is a result (Errored), never an unhandled exception that ends the run.
        catch (Exception ex) { return Result(assertion, context.Face, ConformanceStatus.Errored, $"{ex.GetType().Name}: {ex.Message}") with { Exception = ex }; }
#pragma warning restore CA1031
    }
    // ApplyWaivers: Failed/Errored + matching waiver -> Waived{Waiver}; Passed + matching waiver -> Failed("waiver for X is no longer needed").
}
```

`SuiteContext` (`IAsyncDisposable`) owns: `Subject`, `Face`, `Options`, a `RecordingLogger` surfaced as `Settings` (`new TransportSettings { Logger = logger }`),
`StartServer(params LoopbackResponse[])` and `StartServer(Func<RecordedRequest, LoopbackResponse>)` (tracking each for disposal), `CreateTransport()` (calls
`CreateAsync(Settings)` or `CreateBlocking(Settings)` per face and wraps it in `AsyncFaceTransport`/`BlockingFaceTransport`, tracked), `Track(IAsyncDisposable)`, and the
hook accessors `RequireBorrowed()`, `RequireFaultingAdaptation()`, `RequireInternalCancel()`, `RequireNativeResend()`, `RequireProxy()` which throw `NotExercisedException`
naming the hook. `Supplies(subject, face)` is `face == Async ? subject.CreateAsync is not null : subject.CreateBlocking is not null`. A `BlockingFaceTransport.SendAsync` is
`Task.Factory.StartNew(() => client.Execute(request, options, ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default)` (cancellation of a *blocking* call is cooperative: the
token is passed into `Execute`, which is the `TRANSPORT-3` row's own face). `TransportSuite.Assertions` is `TransportCatalogue.All` (the list built in 2.5–2.11; empty
at this task, so `Assertions` is `[]` and the driver tests of 2.13/2.14 are written after the catalogue exists). The public `RunAllAsync` iterates `Assertions` (through the internal overload), then each declared face.

**PublicAPI.** `TransportSuite` (three members). **Verify.** V-build; V-fast the three classes. **IDs.** `P8a-7`, `P8a-8`, `P8a-9`, `P8a-10`.

## Task 2.3 — The test-only raw-socket HTTP/1.1 client (D3's second wire stack)

**Files.** New (tests, `Dexpace.Sdk.Conformance.Tests/RawSocket/`): `RawSocketHttpClient.cs` (the `IAsyncHttpClient`), `RawSocketRequestWriter.cs`,
`RawSocketResponseReader.cs`, `RawSocketResponseBody.cs`, `RawSocketExchange.cs` (the per-call connection holder), `RawSocketHttpClientTests.cs` (`Integration`). About 400
lines in all, never packed, shares no code with `HttpClient` (P8a-2). Allowed: `System.Net.Sockets`; banned-in-`src/` rules do not bind tests, but the client avoids
`Task.Delay` and thread interrupts anyway.

**Tests first** (`Integration`, against the promoted `LoopbackServer`; red = missing client).

- Framing: a `GET` writes `GET /p?q=1 HTTP/1.1`, `Host: 127.0.0.1:{port}`, the caller's headers, `Connection: close`, no body bytes, no `Content-Length`; a bytes `POST` writes
  `Content-Length: n`; an unknown-length stream `POST` writes `Transfer-Encoding: chunked` with correct chunk framing (the fixture's reader de-chunks and the recorded
  `Body` equals the source); a body-less `POST`/`PUT`/`PATCH` writes `Content-Length: 0`.
- Headers: the framing set (`Host`, `Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Upgrade`, `TE`, `Expect`) from the caller is not written; the
  caller's `Content-Type` wins over the body's, the body's is used when absent (`TRANSPORT-10`).
- Response: status code, reason phrase, `Protocol`, headers (case preserved in the model's lookup case-insensitively), a `Content-Length` body, a chunked body, a
  close-delimited body, `HEAD`/`204`/`304` bodiless; a non-numeric `Content-Length` gives `ContentLength == -1` and a readable close-delimited body; a malformed
  `Content-Type` gives `ContentType == null`; a header line with a bad name or a control byte in the value is dropped, the rest kept (`TRANSPORT-14`); obs-text kept.
- Laziness and release: `ExecuteAsync` returns after the head is parsed (a `Streamed` reply's first chunk is readable before the second is written); disposing the
  `Response` closes the socket (`WaitForConnectionReleasedAsync` completes).
- Failures: a refused port, a reset before any byte and a close before the status line are `ServiceRequestException` (`IsRetryable`), with the I/O exception as
  `InnerException`; a close mid-body surfaces from the body read as an `IOException`; a caller token signalled before or during the call gives `OperationCanceledException`
  whose `CancellationToken` is the caller's, and the socket is closed; `RequestOptions.Timeout` elapsing is `ServiceRequestTimeoutException` (retryable), not a cancellation;
  an invalid status line is `ServiceResponseException`.
- Never follows a redirect; never retries or re-sends a body; `Dispose`/`DisposeAsync` are no-ops and idempotent (it owns nothing: a connection belongs to its response).
- Concurrency: 64 parallel calls through one instance do not cross (shared nothing).

**Production change.** `RawSocketHttpClient : IAsyncHttpClient` with: `ExecuteAsync` (an `async` method, so argument errors fault the task, `TRANSPORT-21`) that
creates a linked token (caller token plus `Timeout`), opens a `TcpClient` to `Url.Host:Port`, registers `token.Register(socket.Dispose)` so cancellation aborts a pending read, writes
the head and the body (`RequestBody.WriteToAsync`, which latches single-use bodies), reads the head with `RawSocketResponseReader` (own line reader; no `StreamReader.ReadLine`),
and returns `new Response(request, Status.FromCode(code), Protocol.Http11, headers, new RawSocketResponseBody(...), reason)`. `RawSocketResponseBody : ResponseBody` overrides
`Dispose(bool)` (not `Dispose()`; CLAUDE.md) and closes the socket exactly once; `OpenReadAsync` returns a framing stream (fixed-length, chunked or until-close) that
throws `IOException` on early EOF. The error mapping is the `ServiceRequestException` family above; classification of "was the cause the caller's token?" uses
`token.IsCancellationRequested` on the **caller's** token, never exception type alone (the lesson of F3). A blocking façade, `RawSocketBlockingClient : IHttpClient`, is
`rawClient.AsBlocking()` (R11).

**PublicAPI.** None (tests). **Verify.** V-build; V-fast `*RawSocketHttpClientTests`. **IDs.** `P8a-2`.

## Task 2.4 — Group-2 scaffolding: the catalogue registry and the shared driver helper

**Files.** New (kit): `Assertions/TransportCatalogue.cs` (internal; `All`, built from per-group `Register(...)` calls added by 2.5–2.11), `Assertions/AssertionBuilder.cs`
(internal; fluent `Assertion("transport-24.vendor-status-readable", ["TRANSPORT-24"], Level.Must, Faces.Both, Body)`), `Assertions/Check.cs` (internal static helpers:
`Check.Equal(actual, expected, what)`, `Check.True`, `Check.Fail(message, expected, actual)`, `Check.NoFaults(server)`, `Check.HeaderValue(request, name)` with ASCII-case-folded names
(suite-contract clause 3), `Check.ReleasedAsync(server, connection, ctx)` which awaits `WaitForConnectionReleasedAsync` under `ReleaseTimeout` and fails with the connection index, and which throws a kit-bug
`InvalidOperationException` (`Errored`) if `server.ServerClosedFirst(connection)` is true, R13).
New (tests): `Assertions/CheckTests.cs`, `Assertions/AssertionCatalogueTests.cs`, `Drivers/AssertionControl.cs` (the negative-control helper below), `Drivers/ConformingHooks.cs` (the positive counterparts of the hook assertions).

**Tests first.**

- `CheckTests` (`Unit`): each helper throws `ConformanceException` with `Expected`/`Actual` populated; `Check.NoFaults` lists `server.Faults` types, never their messages' bodies;
  `Check.HeaderValue` compares case-folded and refuses to look at `Content-Length` of a *response* (clause 4 is enforced by simply not offering that overload).
- `AssertionControl.RunAsync(assertionName, face, IAsyncHttpClient broken)` (test helper): builds a `TransportSubject` whose `CreateAsync` returns `broken` (and `CreateBlocking`
  its `.AsBlocking()`), runs `TransportSuite.RunAsync` with `ReleaseTimeout = 500 ms`, `AssertionTimeout = 10 s`, and returns the result. A second overload,
  `AssertionControl.RunAsync(assertionName, face, TransportSubject subject)`, takes a full subject so a control can break a **hook** (`CreateBorrowed`,
  `CreateWithFaultingAdaptation`, `CreateWithInternalCancel`, `CreateWithNativeResend`, `CreateWithProxy`) or declare `AfterDispose = ThrowsObjectDisposedException`
  over a broken client; the hook controls of 2.9-2.11 (`transport-8`, `-15.borrowed`, `-18`, `-22`, `-30`, `seam-15`) use it. Every assertion task asserts
  `ConformanceStatus.Failed` (or `Errored` where the contract says so) from it. The **positive counterpart** (`Passed`) is: for client-only assertions, the same helper
  over `RawSocketHttpClient`; for hook assertions (`NotExercised` on the raw-socket driver, so the raw client cannot supply it), a conforming test-side hook set in
  `Drivers/ConformingHooks.cs` (borrowed: a counting native owner over the raw client; faulting adaptation: a raw-client variant that throws after the head and
  closes its own socket; internal cancel: a raw client with a linked-source trigger; native resend: a raw client whose resend replays a buffered copy; `seam-15`:
  a wrapper that throws `ObjectDisposedException` after dispose), or, where building one is disproportionate (`transport-30`: needs a proxy-aware client), the
  SystemNet driver's un-waived result is named as the positive evidence in the control test's comment and the checklist.
- **Control coverage is declarative, not order-dependent.** Each `Controls/*ControlTests` class exposes a `public static` table `Rows` (assertion name, face, broken
  subject factory) used as the `MemberData` of its `[Theory]`, and a `public static IReadOnlyCollection<string> Covered` derived from `Rows`; there is **no** run-time
  registry filled by `[Fact]`s (xUnit v3 orders test classes arbitrarily and may run them in parallel). `AssertionCatalogueTests` (2.12) reads the `Covered`
  members of all control classes statically, so it depends on no other test having run.
- `AssertionCatalogueTests` (`Unit`, **written now with the final expectations; red until 2.11 completes, so it is added to the PR in 2.12**): see 2.12.

**Production change.** The registry and helpers. `TransportCatalogue.All` is built once, ordered as in the design's catalogue table (the order of `TRANSPORT` ID, then the four
cross-referenced ones); each group class exposes `internal static IEnumerable<ConformanceAssertion> Define()`.

**Verify.** V-build; V-fast `*CheckTests`. **IDs.** `P8a-9`, `P8a-10`.

## Task 2.5 — Assertions: response shape (`transport-21`, `transport-23`, `async-1`, `transport-24`)

**Files.** New (kit): `Assertions/ResponseShapeAssertions.cs`. New (tests): `Controls/ResponseShapeControlTests.cs` (`Integration`).

**Negative controls first** (each must be `Failed`; red = the assertion names do not exist):

| Assertion | Broken subject (a `DelegateHttpClient.Create` lambda) | Expected |
|---|---|---|
| `transport-21.pre-dispatch-failure-via-task` | a hand-written `IAsyncHttpClient` whose `ExecuteAsync` throws `ArgumentNullException` synchronously on a `null` request (non-`async` method) | `Failed` |
| `transport-23.never-null` | `send => Task.FromResult<Response>(null!)` | `Failed` (naming the script step that returned null) |
| `async-1.single-non-null-response` | answers every call with a *fixed* response for another request (`response.Request` differs from the request sent) | `Failed` |
| `transport-24.vendor-status-readable` | maps any status `>= 500` to `new ServiceResponseException(...)` | `Failed`; and a variant that returns the right status with an empty body | `Failed` |

**Assertion bodies.**

- `transport-21` (async only): call `transport.SendAsync(null!, RequestOptions.Empty, ct)` and `SendAsync(request, null!, ct)` **through the raw interface**, not the face wrapper;
  the call itself must return (no synchronous throw: wrap the invocation in a try/catch that fails with "threw synchronously"), and the returned task must be `IsFaulted` with
  `ArgumentNullException` (unwrap the single inner). Level MUST; IDs `TRANSPORT-21`, `ASYNC-2`.
- `transport-23`: four scripts in one server (`Ok`, `Status(404)`, `Status(503)`, `Abort`); for each, `SendAsync` either returns a non-null `Response` or fails; never completes
  with null. For `Abort` the failure is asserted by `transport-20`, here only "not null". IDs `TRANSPORT-23`, `ASYNC-1`, `SEAM-16`. Faces: async.
- `async-1.single-non-null-response`: a `Func<RecordedRequest,LoopbackResponse>` echoing the request path in the body; send three different paths; each response's body equals its own
  path and `response.Request.Url.AbsolutePath` equals the path sent. IDs `ASYNC-1`. Faces: async.
- `transport-24`: codes `{499, 520, 521, 522, 523, 524, 525, 526, 530}` each with body `"vendor-{code}"` sent as keep-alive `Status(code, body, keepAlive: true)` replies (R13): `response.Status.Code == code`,
  body reads equal, disposing the response releases the connection (`Check.ReleasedAsync`, client-caused only); no exception. Both faces.

**Verify.** V-build; V-fast `*ResponseShapeControlTests` (controls Failed; `RawSocketHttpClient` Passed for the same four). **IDs.** `TRANSPORT-21`, `-23`, `-24`, `ASYNC-1`, `ASYNC-2`.

## Task 2.6 — Assertions: cancellation and close (`transport-3`, `transport-7` x2, `transport-16`, `async-20`)

**Files.** New (kit): `Assertions/CancellationAssertions.cs`, `Assertions/CloseAssertions.cs`. New (tests): `Controls/CancellationControlTests.cs`, `Controls/CloseControlTests.cs`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-3.cancel-is-terminal` | maps cancellation to `new ServiceRequestTimeoutException("cancelled")` (a retryable SDK exception), on both faces | `Failed` |
| `transport-3.cancel-is-terminal` | ignores the token (awaits the hang to completion) | `Failed` via the 30 s-bounded `Bounded` (control uses `AssertionTimeout = 2 s`) |
| `transport-7.cancel-releases-exchange` | accepts the token but leaves the socket open (a `TcpClient` that is never disposed on cancel) | `Failed` (server never sees the release within 500 ms) |
| `transport-7.attempt-timeout-aborts` | a transport that ignores its token | `Failed` |
| `transport-16.close-idempotent-nonblocking` | throws on the second `DisposeAsync`; and a variant whose `DisposeAsync` blocks on an in-flight hang | `Failed` (two controls) |
| `async-20.late-cancel-leaves-response-open` | disposes the response body when the token cancels after delivery | `Failed` |

**Assertion bodies.**

- `transport-3.cancel-is-terminal` (both faces; IDs `TRANSPORT-3`, `SEAM-13`, `XCUT-1`): server `Hang()`; start the call with a `CancellationTokenSource`; wait for
  `server.WaitForRequestAsync(0)` (the server has the request: a condition, not a sleep); cancel; the task ends in `OperationCanceledException` whose `CancellationToken ==
  token` (or whose cause chain carries it), the token is still signalled, and the exception is not an `SdkException` with `IsRetryable`. For `Blocking` the call runs on its dedicated
  thread (the face wrapper), which *is* the row's own face.
- `transport-7.cancel-releases-exchange` (async; `TRANSPORT-7`): same script; after the task ends cancelled, `Check.ReleasedAsync(server, 0)`.
- `transport-7.attempt-timeout-aborts` (async; `TRANSPORT-7`, 6a hand-off): build `var pipeline = DexpacePipeline.CreateDefault(transport)` and send with
  `pipeline.SendAsync(request, new DexpaceClientOptions { AttemptTimeout = TimeSpan.FromMilliseconds(300), Retry = new RetryOptions { MaxRetryAttempts = 0 } }, ct)`
  (`AttemptTimeout` is a `DexpaceClientOptions` member, `MaxRetryAttempts` a `RetryOptions` member, and options are passed per call; `RetryOptions` has no
  `AttemptTimeout`/`MaxRetries`; see `tests/Dexpace.Sdk.Http.SystemNet.Tests/AttemptTimeoutWireTests.cs` for the shape; no `AddStandardResilience` rebuild); script `Hang()`
  (a single entry is enough because retries are off: a retried timeout would hit a one-entry script and record a `Faults` entry); the call fails within `AssertionTimeout`
  (not hangs) with an `OperationTimeoutException`-or-retry-exhausted `SdkException` whose trail holds a timeout, and the server sees the connection released (the `Hang`
  reply never closes, so the release is client-caused).
- `transport-16.close-idempotent-nonblocking` (both faces; `TRANSPORT-16`, `ASYNC-15`): dispose the transport three times (`DisposeAsync` on `Async`, `Dispose` on `Blocking`) — no throw; with a
  call parked on `Hang()` (request seen), dispose returns within `ReleaseTimeout` (assert via `Bounded.WaitAsync` on the dispose task, not a sleep); after the signalled token cancels the
  call, `token.IsCancellationRequested` is still true (the "preserve the flag" clause, free on .NET, asserted anyway).
- `async-20.late-cancel-leaves-response-open` (async; `ASYNC-20`, `SEAM-16`): `Ok(body)`; after the response is delivered, cancel the call's token, then read the whole body and compare.

**Verify.** V-build; V-fast the two control classes. **IDs.** `TRANSPORT-3`, `-7`, `-16`, `ASYNC-15`, `ASYNC-20`; owned elsewhere `SEAM-13`, `XCUT-1`.

## Task 2.7 — Assertions: bodies (`transport-25` x3, `transport-26`, `transport-27`, `http-39`)

**Files.** New (kit): `Assertions/BodyAssertions.cs`, `Assertions/InboundAssertions.cs`. New (tests): `Controls/BodyControlTests.cs`, `Controls/InboundControlTests.cs`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-25.lazy-body` | reads the whole body into bytes before returning (a buffering wrapper around the raw client) | `Failed` (first chunk not readable before the gate opens; the gate is opened only after the first read, so the broken subject is detected by the `Bounded` expiry of the "first chunk" wait — control `AssertionTimeout = 3 s`) |
| `transport-25.large-round-trip` | truncates the body at 1 MiB | `Failed` (SHA-256 differs) |
| `transport-25.dispose-releases` | `Response.Dispose` that does not close the socket | `Failed` |
| `transport-26.bodyless-methods` | adds `Transfer-Encoding: chunked` to a body-less `GET` | `Failed` |
| `transport-27.inbound-downgrade` | throws `ArgumentException` on the malformed `Content-Type` | `Failed` |
| `http-39.short-source-fails` | pads a short source with zeros to the declared length | `Failed` |

**Assertion bodies.**

- `transport-25.lazy-body` (both; `TRANSPORT-25`, `SEAM-11`): `LoopbackResponse.Streamed` with a gated enumerable (the 7b `ServerSentEventWireTests` pattern): chunk 1 immediately, chunk 2 only
  after a `TaskCompletionSource` the test opens. The call returns; the first chunk is read from `OpenReadAsync`/`OpenRead` **before** the gate opens (this is the property: a pre-buffering
  transport cannot return); then open the gate; the rest reads equal. A 1-chunk-late opening is detected by `Bounded`.
- `transport-25.large-round-trip`: `Large(8 MiB, seed 8)`, `LargeBody.Sha256` equals the SHA-256 of the streamed read (computed incrementally, never buffering 8 MiB twice).
- `transport-25.dispose-releases`: `Large(8 MiB, keepAlive: true)`; read the first 64 KiB; dispose the response; `Check.ReleasedAsync`.
- `transport-26.bodyless-methods`: echo server; `POST`/`PUT`/`PATCH` with no body: the recorded request has `Content-Length: 0` (or an empty chunked body) and `Body.Length == 0`; `GET` and
  `HEAD`: no `Content-Length` and no `Transfer-Encoding`.
- `transport-27.inbound-downgrade` (SHOULD): three raw replies (`Content-Type: text/plain; foo`, `Content-Length: abc` with `Connection: close`, no length and no `Content-Type`):
  `ContentType == null` for the first, `ContentLength == -1` for the second and third, the body reads in all three. IDs `TRANSPORT-27`.
- `http-39.short-source-fails` (both; `HTTP-39`): a stream body with `contentLength: 10` over a 5-byte source: the send fails and the cause chain holds an `EndOfStreamException` whose message
  contains `of 10`; the server does not see a complete 10-byte body. (Lifted from `ExactLengthBodyWireTests`.)

**Verify.** V-build; V-fast the two control classes. **IDs.** `TRANSPORT-25`, `-26`, `-27`; owned elsewhere `SEAM-11`, `HTTP-39`.

## Task 2.8 — Assertions: outbound and concurrency (`transport-11` x2, `transport-29`, `transport-2`)

**Files.** New (kit): `Assertions/OutboundAssertions.cs`, `Assertions/ConcurrencyAssertions.cs`. New (tests): `Controls/OutboundControlTests.cs`, `Controls/ConcurrencyControlTests.cs`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-11.framing-recomputed` | forwards a caller-set `Host` and `Content-Length` verbatim (a raw-client variant with the drop set removed, built by the control as a subclass-free copy of the writer parameterised by a flag) | `Failed` |
| `transport-11.drop-logged` | drops the headers silently (no log) | `Failed`; a variant that logs the **value** | `Failed` |
| `transport-29.concurrent-no-crosstalk` | serialises through a shared field (`static Request _last`) so responses cross | `Failed` |
| `transport-2.no-silent-resend` | writes the single-use body, on a failed first attempt, a second time (retry loop around the raw client) | `Failed` |

**Assertion bodies.**

- `transport-11.framing-recomputed` (both; `TRANSPORT-11`): echo server; send `Host: evil.example`, `Content-Length: 9999`, `Transfer-Encoding: chunked`, `Connection: keep-alive`, `X-Pass:
  kept` with a 5-byte body; the recorded `Host` header is `127.0.0.1:{port}`, no `evil.example` byte anywhere in `RawText`, exactly one `Content-Length` header whose value is `5`
  (or chunked framing with no `Content-Length`), and `X-Pass` survived. Names compared case-folded.
- `transport-11.drop-logged` (async, SHOULD; `TRANSPORT-11`): the same request through a subject built with the context's `RecordingLogger`; each dropped name appears in `Logger.Dropped(name)`
  at least once, and **no entry's message or state contains `evil.example` or `9999`** (values are never logged; XCUT-18).
- `transport-29.concurrent-no-crosstalk` (both; `TRANSPORT-29`, `SEAM-12`, `ASYNC-22`): `EchoId` server; 64 concurrent sends through **one** transport (`Async`: `Task.WhenAll` over 64 started
  tasks; `Blocking`: the face wrapper already gives one dedicated thread each), each with `X-Conformance-Id: id-{i}`; every response body equals its own id and `response.Request` is its own request.
- `transport-2.no-silent-resend` (both; `TRANSPORT-2`): script `Ok(keepAlive: true)` for request 0 (warm connection), `Abort()` for request 1; send a single-use stream `POST` body (a
  `CountingStream`-wrapped source, non-seekable, 1 KiB); the second send fails with an `SdkException` that `IsRetryable` **or** whose chain carries `StreamConsumedException`, never a 2xx; the server
  has seen the body's 1024 bytes at most once across **all** connections (`server.Requests.Sum(r => r.Body.Length)` for requests at index >= 1 is <= 1024), and the source's `BytesRead` <= 1024.

**Verify.** V-build; V-fast the two control classes. **IDs.** `TRANSPORT-2`, `-11`, `-29`; owned elsewhere `SEAM-12`, `ASYNC-22`.

## Task 2.9 — Assertions: failures and lifecycle (`transport-20` x2, `transport-22`, `transport-15` x2, `transport-19`)

**Files.** New (kit): `Assertions/FailureAssertions.cs`, `Assertions/LifecycleAssertions.cs`. New (tests): `Controls/FailureControlTests.cs`, `Controls/LifecycleControlTests.cs`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-20.no-response-is-retryable` | wraps a refused connection in a non-retryable `SdkException`; and a variant that surfaces `OperationCanceledException` | `Failed` (two controls) |
| `transport-20.retried-by-the-pipeline` | marks the failure `ServiceResponseException` (not retryable) so the pipeline stops | `Failed` |
| `transport-22.adaptation-failure-releases` | a `CreateWithFaultingAdaptation` hook (full-subject `AssertionControl` overload) that leaks the socket when adaptation throws; the reply is keep-alive, so the leak is observable | `Failed` |
| `transport-15.borrowed-survives` | a `BorrowedTransport` whose `DisposeAsync` also disposes the native client | `Failed` |
| `transport-15.owned-released` | an owned transport that keeps an idle connection open after dispose | `Failed` |
| `transport-19.abandoned-body-unblocks` | ignores the token while the body source is parked | `Failed` |

**Assertion bodies.**

- `transport-20.no-response-is-retryable` (both; `TRANSPORT-20`, `XCUT-4`): (a) a URL on a port the test binds then releases (connection refused); (b) `Abort()`. Each failure is an `SdkException`,
  `IsRetryable == true`, its cause chain holds an `IOException`, `HttpRequestException`, `SocketException` or `TimeoutException` (design §10 entry 7's reading), and no link of the chain is an
  `OperationCanceledException` (a timeout is not a cancellation, but TRANSPORT-20's family excludes the caller's cancel).
- `transport-20.retried-by-the-pipeline` (async; `TRANSPORT-20`, 6a): `DexpacePipeline.CreateDefault(transport)` over `Abort()` then `Ok("done")`: a `GET` returns `200 done`;
  `server.ConnectionCount == 2`; `Check.NoFaults`. (Uses the default retry; the idempotent `GET` is retried.)
- `transport-22.adaptation-failure-releases` (async; `TRANSPORT-22`): `ctx.RequireFaultingAdaptation()` (else `NotExercised`); `Ok("x", keepAlive: true)` (R13: a closing reply would make the release check vacuous and the control unable to fail); the send fails; `Check.ReleasedAsync`. Whether the failure is re-typed
  as an SDK exception is **not** asserted (8b's decision, outside any row).
- `transport-15.borrowed-survives` (async; `TRANSPORT-15`, `XCUT-22`): `ctx.RequireBorrowed()`; dispose the transport (`DisposeAsync`); then `borrowed.SendThroughNativeClient(server.Url("/"), ct)`
  completes against a keep-alive `Ok`; finally `borrowed.DisposeAsync()`.
- `transport-15.owned-released` (async; `TRANSPORT-15`): `CreateAsync`, keep-alive `Ok`, read and dispose the response (the connection goes idle in the transport's pool), dispose the transport;
  `Check.ReleasedAsync(server, 0)` — a connection-per-request transport passes trivially, a pooling one passes because dispose closes the pool.
- `transport-19.abandoned-body-unblocks` (async, SHOULD; `TRANSPORT-19`): a `ParkedReadStream` as the body source (`RequestBody.FromStream`); start the call, wait for the stream's `ReadStarted`,
  cancel the token; within `ReleaseTimeout` the parked read has observed cancellation **or** its stream was disposed; then, a `FileRequestBody` over a temp file: send it to `Hang()`, cancel, and
  `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)` opens afterwards (no stranded handle; meaningful on Windows). Temp file removed in `finally`.

**Verify.** V-build; V-fast the two control classes. **IDs.** `TRANSPORT-15`, `-19`, `-20`, `-22`; owned elsewhere `XCUT-4`, `XCUT-22`.

## Task 2.10 — Assertions for 8b's rows, part 1 (`transport-1`, `-4`, `-5`, `-6`, `-8`, `-9`)

**Files.** New (kit): `Assertions/RedirectAndTimeoutAssertions.cs`. New (tests): `Controls/RedirectAndTimeoutControlTests.cs`. These assertions are 8a's work; the rows are 8b's.
They are expected to **fail** on SystemNet for `-4`, `-5`, `-6`, `-8` (waived `8b`, task 2.13) and to pass for `-1` and `-9`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-1.redirect-not-followed` | follows a `302` to the second server | `Failed` |
| `transport-4.timeout-is-retryable` | maps the timeout to `OperationCanceledException`; and a variant that ignores `Timeout` | `Failed` (two) |
| `transport-5.per-call-timeout` | applies the first call's timeout to the second call (a field) | `Failed` |
| `transport-6.sub-resolution-timeout` | truncates `100 µs` to `0` (no timeout) so the hang is never cut | `Failed` |
| `transport-8.internal-cancel-is-terminal` | maps an internal cancel to `ServiceRequestTimeoutException` (the F3 defect) | `Failed` |
| `transport-9.settle-race-releases` | leaks the response when the cancel loses the race (never disposes a late `Response`); the replies are keep-alive, so the leaked connection stays open and the check can fail | `Failed` |

**Assertion bodies.**

- `transport-1.redirect-not-followed` (both): `Redirect(302, "{server2}/target")`; the response is the `302` with its `Location`; server 2 saw zero requests; `Faults` empty.
- **Bounded hangs (flake and cost budget).** `transport-4`, `-5` and `-6` (and the timeout half of `-8`) hang when a transport ignores `RequestOptions.Timeout`, as SystemNet does until
  8b. Each therefore bounds its own wait at `TimeoutAssertionBound = 3 s` (a `Bounded.WaitAsync` on the call task, failing with `ConformanceException("the call outlived 15x its Timeout")`)
  instead of running to `AssertionTimeout = 30 s`; the SystemNet driver also sets `AssertionTimeout = 10 s`. Waived SystemNet rows cost about 6 face-runs x 3 s per pass, doubled by
  the theory plus `RunAllAsync` fact, so about 40 s per SystemNet run per OS row (about 3.5 min for the 5-run flake pass of 2.16); this is budgeted, not hidden.
- `transport-4.timeout-is-retryable` (both; `TRANSPORT-4`, `XCUT-2`): `Hang()`, `RequestOptions { Timeout = 200 ms }`; fails with a retryable `ServiceRequestTimeoutException`-family `SdkException`; **not**
  an `OperationCanceledException`; the caller's token is not signalled.
- `transport-5.per-call-timeout` (async): server A `Gated(1 s)`, server B the same; call 1 `Timeout = 200 ms` (times out), call 2 `Timeout = 5 s` against A's gate opened early (succeeds), call 3 with `null` timeout
  (no per-call bound; succeeds): the first call's bound does not leak to the next.
- `transport-6.sub-resolution-timeout` (SHOULD, both): `Hang()` with `Timeout = TimeSpan.FromTicks(1000)` (100 µs): fails as a timeout within `AssertionTimeout` (clamped up, not truncated to none).
- `transport-8.internal-cancel-is-terminal` (async; `TRANSPORT-8`): `ctx.RequireInternalCancel()` (hook: transport plus the trigger); `Hang()`, wait for the request, call `CancelInFlight()`; the failure is **not**
  a retryable timeout (an `SdkException` with `IsRetryable == false`, or an `OperationCanceledException`); then, the timeout half: a real `Timeout` expiry **is** a retryable timeout (both halves in one assertion).
- `transport-9.settle-race-releases` (async; `TRANSPORT-9`, `SEAM-30`): 50 iterations; keep-alive `Streamed(..., keepAlive: true)` replies (R13: with the default closing reply the server closes the
  connection anyway, `ServerClosedFirst` is true and the check would be vacuous) with a body of one chunk; the token is cancelled at offsets `{0, 1, 2, 4, 8…}` ms around header arrival via
  `server.WaitForRequestAsync`-relative scheduling with no sleeping (cancel from a continuation of the request-arrival task, plus a variant cancelling immediately after the call returns); every iteration ends either
  with a delivered response (disposed by the test) or `OperationCanceledException`, and **in both cases** `Check.ReleasedAsync` for that iteration's connection — no response is ever orphaned.

**Verify.** V-build; V-fast `*RedirectAndTimeoutControlTests`. **IDs.** `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9` (8b's rows; 8a writes the assertions); `SEAM-30`, `XCUT-2`.

## Task 2.11 — Assertions for 8b's rows, part 2 (`transport-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30`, `seam-15`)

**Files.** New (kit): `Assertions/HeaderAssertions.cs`, `Assertions/ResendAssertions.cs`, `Assertions/DisposeAssertions.cs`. New (tests): `Controls/HeaderControlTests.cs`, `Controls/ResendControlTests.cs`,
`Controls/DisposeControlTests.cs`.

**Negative controls first.**

| Assertion | Broken subject | Expected |
|---|---|---|
| `transport-10.content-type-authoritative` | overwrites the caller's `Content-Type` with the body's (the F7 defect) | `Failed` |
| `transport-12.native-rejected-header-dropped` | throws on a header its native layer refuses | `Failed`; and a variant that drops silently with no log | `Failed` |
| `transport-13.drop-log-once-per-name` | logs every drop every time | `Failed` |
| `transport-14.inbound-lenient` | fails the whole response on a control byte in a header value | `Failed` |
| `transport-17.single-use-written-once` | writes the body twice (first attempt discarded) | `Failed` |
| `transport-18.native-resend-identical` | re-sends a different (empty) body on the native resend | `Failed` |
| `transport-28.file-range-replayable` | ignores `offset`/`count` | `Failed` |
| `transport-30.proxy-discoverable-no-leak` | forwards the origin `Authorization` to the proxy | `Failed` |
| `seam-15.after-dispose` | declares `ThrowsObjectDisposedException` but keeps serving | `Failed` |

**Assertion bodies.**

- `transport-10.content-type-authoritative` (both): echo server; (a) body type `application/json`, caller `Content-Type: application/vnd.x+json` ⇒ the wire carries the caller's; (b) body type set, no caller header ⇒
  the body's; (c) no body type, caller header ⇒ the caller's. Names case-folded; `Content-Type` appears exactly once.
- `transport-12.native-rejected-header-dropped` (both): a body-less and a bodied request each carrying a content-header-class header (`Content-Language`, `Content-Encoding`, `Allow`) and a set of awkward-but-model-valid
  names/values; **each** header is either on the wire or has a log entry from `Logger.Dropped(name)`; no request throws; no header silently vanishes (the F7 defect).
- `transport-13.drop-log-once-per-name` (async, SHOULD; `TRANSPORT-13`, `OBS-19`): three sends dropping two distinct names, default policy: each name is logged at most once for the transport's lifetime; the
  second name is still logged once (so a bounded latch, not a global silence); name comparison case-insensitive.
- `transport-14.inbound-lenient` (both; `TRANSPORT-14`): `Raw` replies: obs-text (`0xE9`) in a value ⇒ kept; a control byte in a value ⇒ that header dropped, the rest delivered, the response not failed; a header **name**
  with a space/control/non-ASCII byte ⇒ that header dropped, the response delivered. *(Expected `Failed` on SystemNet for the name half by F1; waived `8b`. The kit asserts the full clause; 8b dispositions the native refusal.)*
- `transport-17.single-use-written-once` (both): a `CountingStream`-backed non-seekable stream body over `Ok`; the body source was read once (`BytesRead == length`, `ReadCalls` consistent with one pass) and the server
  received exactly the bytes once.
- `transport-18.native-resend-identical` (async): `ctx.RequireNativeResend()`; a 1 MiB stream body; the hook's transport re-sends the request natively (twice through the same body); the server sees two identical
  1 MiB bodies **or** the send fails cleanly with a buffering failure (`SdkException`), never two different ones; no truncated duplicate.
- `transport-28.file-range-replayable` (both, SHOULD; `TRANSPORT-28`): `RequestBody.FromFile(path, type, offset: 3, count: 10)` sent twice (replayable by `IsReplayable`): both wires carry the same ten bytes; the
  zero-copy part is **not** asserted (design R3: not observable).
- `transport-30.proxy-discoverable-no-leak` (async, SHOULD+MUST no-leak; `TRANSPORT-30`): `ctx.RequireProxy()`; a second `LoopbackServer` as the forward proxy answering `407` with `Proxy-Authenticate`, then the
  origin `401`; the proxy credential never reaches the origin and the origin `Authorization` never reaches the proxy. `NotExercised` on SystemNet until 8b supplies `CreateWithProxy`.
- `seam-15.after-dispose` (both, MAY; `SEAM-15`): if `subject.AfterDispose == Unspecified` throw `ConformanceVacuousException("subject declares no post-dispose behaviour (SEAM-15 is a MAY)")`; else dispose,
  then a call must throw `ObjectDisposedException` (directly or as the task's exception).

**Verify.** V-build; V-fast the three control classes. **IDs.** `TRANSPORT-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30`, `SEAM-15` (8b's rows / 2b's `SEAM-15`; 8a writes the assertions), `OBS-19`.

## Task 2.12 — The catalogue is complete: counts, IDs, levels and coverage (a pin)

**Files.** New (tests): `Assertions/AssertionCatalogueTests.cs` (`Unit`). Edit: `TransportCatalogue.cs` (final ordering).

**Tests first** (these pass once 2.5–2.11 exist; they are the **pin** that guards the whole catalogue against a later edit).

- `TransportSuite.Assertions.Count == 40`; names unique, stable and in the design's table order (compare against the literal list of 40 names written into the test).
- Every name matches `^[a-z]+-\d+\.[a-z0-9-]+$`; every cited ID exists in `RequirementIndex`; each assertion's `Level` does not exceed the strongest level among its IDs (R5); a `May`-level assertion cites
  only `May` rows.
- **Row coverage.** Every ID in `{TRANSPORT-1..30}` is cited by at least one assertion; each of `ASYNC-1`, `ASYNC-2`, `ASYNC-15`, `ASYNC-20`, `ASYNC-22` is cited by one; `SEAM-11`, `-12`, `-13`, `-15`, `-16`,
  `-30`, `HTTP-39`, `XCUT-1`, `-2`, `-4`, `-22`, `OBS-19` are cited; no `ASYNC` ID outside the five is claimed (the other 17 are Core.Tests' evidence, not the kit's).
- **Every assertion has a negative control.** Static and declarative (2.4): each control class's `Covered` is derived from its `Rows` table; a single static list `ControlClasses.Covered` concatenates the
  `Covered` members of every `Controls/*ControlTests` class, and this test asserts `ControlClasses.Covered ⊇ TransportSuite.Assertions.Select(Name)` without relying on any other test having run
  (also true under `--filter-class`). A new control class that is not added to `ControlClasses` leaves its assertions uncovered, so the test fails; there is no run-time registration.
- **Waiver targets exist.** Every `Assertion` name used by the SystemNet and raw-socket drivers' waivers (read from `SystemNetSubject.Options` is not reachable here, so the drivers' own tests do it; this
  class checks the converse): a `[Theory]` over `TransportSuite.Assertions` shows `RequirementIndex.Contains` for each cited ID, and the drivers' `Only_known_waiver_targets_remain` facts (2.13, 2.14) check each
  waiver's `Assertion`, when set, is a name in `TransportSuite.Assertions` and its `RequirementId` is cited by that assertion.
- `Faces`: the design's table (e.g. `transport-5`, `-7`, `-8`, `-9`, `-13`, `-15` `Async` only; `transport-3`, `-24` both).

**Production change.** Only the ordering fix of `TransportCatalogue` if the test finds drift. **Verify.** V-build; V-fast `*AssertionCatalogueTests`. **IDs.** all 40 assertions.

## Task 2.13 — The SystemNet driver and its waiver list

**Files.** New: `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs`. Edit: the SystemNet test csproj
(kit `ProjectReference` was added in 1.7; update the header comment to say the fixture now lives in the kit).

**Tests first.** The driver is ~40 lines (P8a-15) and is the test. It compiles as written: the `switch` form with `case … : Assert.Fail(…);` falling into `default:` is CS0163/CS8070 because
`[DoesNotReturn]` does not affect C# reachability, so the shape is `if`/`else`. The report goes to the **test output** (`TestOutputHelper.WriteLine`), not a diagnostic message, which
is hidden unless `diagnosticMessages` is enabled and would hide the waiver list design §9.3 wants on every run:

```csharp
[Trait("Category", "Conformance")]
public sealed class SystemNetConformanceTests
{
    public static TheoryData<string, TransportFace> Rows() { /* one row per (assertion, declared face) from TransportSuite.Assertions */ }

    [Theory, MemberData(nameof(Rows))]
    public async Task Assertion_holds(string name, TransportFace face)
    {
        var assertion = TransportSuite.Assertions.Single(a => a.Name == name);
        var result = await TransportSuite.RunAsync(SystemNetSubject.Create(), assertion, face, SystemNetSubject.Options, TestContext.Current.CancellationToken);
        if (result.Status == ConformanceStatus.Passed)
        {
            return;
        }

        if (result.Status is ConformanceStatus.Failed or ConformanceStatus.Errored)
        {
            Assert.Fail(result.Detail);
        }
        else
        {
            Assert.Skip($"{result.Status}: {result.Detail}");
        }
    }

    [Fact]
    public async Task The_whole_suite_is_green_and_its_report_is_written()
    {
        var report = await TransportSuite.RunAllAsync(SystemNetSubject.Create(), SystemNetSubject.Options, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
    }

    [Fact]
    public void Only_8b_waivers_remain() => Assert.All(SystemNetSubject.Options.Waivers, w => Assert.Equal("8b", w.Owner));

    [Fact]
    public void Only_known_waiver_targets_remain() => Assert.All(
        SystemNetSubject.Options.Waivers.Where(w => w.Assertion is not null),
        w => Assert.Contains(TransportSuite.Assertions, a => a.Name == w.Assertion && a.RequirementIds.Contains(w.RequirementId)));
}
```

**Production change** (test code). `SystemNetSubject.Create()` returns a `TransportSubject { Name = "Dexpace.Sdk.Http.SystemNet" }` with:

- `CreateAsync = s => new SystemNetHttpClient(s.Logger)` and `CreateBlocking = s => new SystemNetHttpClient(s.Logger)` (R7; the owned form; the logger is the kit's recorder).
- `CreateBorrowed`: build `new System.Net.Http.HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })` (the test project may construct it: the ban is `src/` only), `new
  SystemNetHttpClient(client, s.Logger)`, and `sendThroughNativeClient = (uri, ct) => client.GetAsync(uri, ct)` disposed with the response; owner disposes the `HttpClient`.
- `CreateWithFaultingAdaptation` (P8a-22): a borrowed client over a `DelegatingHandler` on a real `SocketsHttpHandler` that takes the **live** response from the inner handler and replaces its `Content` with a
  `HttpContent` subclass whose `TryComputeLength` throws `InvalidOperationException` and whose `Dispose(bool)` disposes the original response (the technique of
  `MalformedContentTypeWireTests.The_native_response_is_disposed_when_adaptation_throws`, which uses a socket-less stub handler; the kit needs the real connection so the server can observe its release).
- `CreateWithInternalCancel` (F3): a borrowed client plus `client.CancelPendingRequests` as the trigger.
- `CreateWithNativeResend`: a borrowed client whose `DelegatingHandler` calls `base.SendAsync(request, ct)` twice (the re-send design §11 item 18 verified), disposing the first response.
- `CreateWithProxy`: **not set** (8b adds it) ⇒ `transport-30` is `NotExercised`.
- `AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException`.
- `Options`: `AssertionTimeout = 10 s` (the 2.10 bounded-hang budget), `Waivers` initially the design's expected set, `Owner = "8b"`: `TRANSPORT-4`, `-5`, `-6`, `-8`, `-10`, `-12`, `-13`, `-14`, `-18`, `SEAM-15`. **First run decides:** run the whole suite, list every non-`Passed` result,
  and make the waiver list exactly the failing set — add nothing speculative, remove any waiver the run calls stale. Each waiver's `Reason` names the fact (F1, F3, F7) or the phase-1/5b/6c hand-off from the
  design's table. `Assertion`/`Face` narrowing is used only where a single ID is cited by a passing and a failing assertion.

**Record the actual outcome** in the checklist draft (task 3.5): every row where the measured result differs from the design's "SystemNet at `ce0f68c`" column goes into the status note as a finding. If an assertion
that the design expects to pass (`transport-1`, `-2`, `-3`, `-7`, `-9`, `-11`, `-15`, `-16`, `-17`, `-19`..`-29`) fails, **stop**: that is either a kit bug (fix the assertion and its control) or a SystemNet defect
outside the design's 14 (a new 8b row; do not edit SystemNet; waive with `Owner = "8b"` and record it).

**Verify.** V-build; `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --no-build --filter-class "*SystemNetConformanceTests"` is green; the count is non-zero
(assertion-face pairs + 2). Skipped rows are exactly the waived ones, `transport-30` and any `Vacuous`. **IDs.** all 16 `TRANSPORT` rows of 8a on the reference transport; `ASYNC-1`, `-2`, `-15`, `-20`, `-22`.

## Task 2.14 — The raw-socket driver

**Files.** New: `tests/Dexpace.Sdk.Conformance.Tests/Drivers/RawSocketConformanceTests.cs`, `Drivers/RawSocketSubject.cs`.

**Tests first.** The same driver as 2.13 (a copy, P8a-15; wrapped in `// <driver>` / `// </driver>` marker comments, which task 3.3's page test uses to keep the documented copy identical to the compiled one), with a different subject:

- `CreateAsync = _ => DelegateHttpClient.Create((request, options, ct) => rawClient.ExecuteAsync(request, options, ct))` — **through `DelegateHttpClient`** (D3), over a fresh `RawSocketHttpClient`;
  `CreateBlocking = _ => DelegateHttpClient.CreateBlocking((request, options, ct) => blocking.Execute(request, options, ct))` where `blocking = rawClient.AsBlocking()` (R11).
- No `CreateBorrowed`, `CreateWithFaultingAdaptation`, `CreateWithInternalCancel`, `CreateWithNativeResend` or `CreateWithProxy`: those rows are `NotExercised` with the hook name (no native client to
  borrow). `AfterDispose = Unspecified` ⇒ `seam-15` is `Vacuous`.
- `Waivers` (R4): `TRANSPORT-13` (no log latch) and `TRANSPORT-11` narrowed to `Assertion = "transport-11.drop-logged"` (the client does not log), both `Owner = null`, reason `"test-only transport"`. The run's actual
  outcome decides the final list; anything else that fails is a kit bug or a raw-client bug, fixed (with a control), not waived.
- Tests: `Assertion_holds` theory, `The_whole_suite_is_green_and_its_report_is_written`, `Only_known_waiver_targets_remain` (2.13's fact), and `No_assertion_is_NotExercised_beyond_the_hook_rows` (the `NotExercised` names are exactly `transport-8`, `-15.borrowed`,
  `-18`, `-22`, `-30`).

**Verify.** V-build; V-fast `*RawSocketConformanceTests`. Together with 2.13 this is exit criterion 2: green against both drivers, waivers listed by ID. **IDs.** the same rows on a second wire stack (D3).

## Task 2.15 — AOT smoke slice

**Files.** Edit: `tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj` (add the kit `ProjectReference`), `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (call `CheckPhase8aConformanceAsync` from `RunAllAsync`), new
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.Conformance.cs`.

**Tests first.** The check is a plain method on `SmokeChecks` (no trait, like the other AOT checks). Write it first, publish, see it fail by pointing it at a nonexistent assertion name (temporary), then correct:
`CheckPhase8aConformanceAsync` builds a `TransportSubject` over `SystemNetHttpClient` (owned) and runs `transport-21.pre-dispatch-failure-via-task`, `transport-24.vendor-status-readable` and
`transport-25.large-round-trip` through `TransportSuite.RunAsync`; each must be `Passed` (P8a-20), else throw `InvalidOperationException` naming the assertion and detail (the file's existing failure style).

**Production change.** The check plus the `ProjectReference`. If the AOT publish warns (`IL2026`, `IL3050`) in the kit, **fix the kit** (replace the construct), never suppress.

**Verify.** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints success. **IDs.** `NFR-8`, `NFR-9` (kit stays AOT-clean).

## Task 2.16 — CI header and group 2 gate

**Files.** Edit: `.github/workflows/ci.yml` (header comment only).

Replace the header line `#   - Transport conformance kit (Dexpace.Sdk.Conformance, TRANSPORT-*): phase 8.` with: `# Transport conformance kit (Dexpace.Sdk.Conformance): on. Two drivers run in the default test step — the reference
transport (tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs) and a test-only raw-socket client through DelegateHttpClient (tests/Dexpace.Sdk.Conformance.Tests/Drivers/); both carry
[Trait("Category", "Conformance")] and run on every OS row.` (and remove it from the inert-gates list). No job or step changes: `dotnet test --solution` already runs both. Run V-gate in full. Expected additions:
the kit's controls (`Integration`), the two `Conformance` drivers, the AOT slice; zero diff in `src/Dexpace.Sdk.Http.SystemNet` and core; the kit's `PublicAPI.Unshipped.txt` now also lists the subject types and
`TransportSuite`. Flake pass: run both drivers 5 times locally (`for i in 1 2 3 4 5; do dotnet test --project … --no-build --filter-trait "Category=Conformance"; done`); any intermittent row is fixed in the
assertion (a missing condition wait), not retried. PR title `feat: phase 8a conformance kit, assertions and drivers (TRANSPORT, ASYNC; part 2/3)`.

---

# PR 3 — Group 3: the `ASYNC` pins, documentation and close-out

Phase-start queries re-run. Group 3 follows group 2 (stacked). It adds no kit code except the README's final text.

## Task 3.1 — `ASYNC` pins in core (1/2): the bridges' scheduler, cancellation and independence rows

**Files.** Edit: `tests/Dexpace.Sdk.Core.Tests/Client/SyncToAsyncBridgeTests.cs`, `tests/Dexpace.Sdk.Core.Tests/Client/AsyncToSyncBridgeTests.cs`. Doubles: `tests/Dexpace.Sdk.TestSupport/Threading/` (reuse
`RecordingTaskScheduler`-style schedulers already there; read the folder first and add only what is missing, in the framework-free style).

Pins (`Unit`; each **passes on arrival** — it is a pin — and is proven able to fail by a temporary mutation, never committed; a pin that is red on arrival means the plan is wrong about the code, so the row is re-dispositioned, as `ASYNC-2` was):

- **`ASYNC-2`**: **no pin is added for the worker-pool-rejection clause** (finding F-A2). Measured on .NET 10.0.401, `AsAsync(scheduler).ExecuteAsync` over a scheduler whose `QueueTask` throws
  `InvalidOperationException` throws `TaskSchedulerException` (inner: that exception) **synchronously**, because `SyncToAsyncAdapter.ExecuteAsync` returns `Task.Factory.StartNew(...)` unwrapped; a
  pin asserting "returns a faulted task" is red on arrival, and a pin asserting the throw would lock the defect in. The clause is ⏳ (owner: lead, core follow-up); the checklist row says so. What
  this task *does* pin, if absent after reading `SyncToAsyncBridgeTests` (add only what is missing): `AsAsync_runs_the_blocking_call_on_the_supplied_scheduler` (a recording scheduler sees exactly the
  one `LongRunning` task; the blocking client runs on a thread that scheduler started). Mutation: pass `TaskScheduler.Default` to `StartNew`.
- **`ASYNC-7`** `AsAsync_aborts_a_token_honouring_client_and_a_token_ignoring_one_runs_on_but_its_call_still_completes_cancelled`: client A observes the token and throws `OperationCanceledException` when it fires: cancel after
  start, A's task is `Canceled`. Client B ignores the token and returns a `Response` (a counting `Response`/body disposal probe): cancel after start, then release B; B's `Execute` **runs to completion** (observed by
  a counter or event, the cooperative-cancellation point of ASYNC-7), but the awaited task ends `Canceled` (not with the response) and the produced response is disposed **exactly once**: the bridge checks the
  token after `Execute` returns, disposes the response and throws `OperationCanceledException` (`SyncToAsyncAdapter.Run`; the `AsAsync` remark: "A response produced after the call's token is signalled is
  disposed and the call completes cancelled", SEAM-30/ASYNC-5). Read `A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces` first and add only what it does not cover
  (A's half and the "B kept running" observation). The XML doc cannot be read at run time, so pin the behaviour and cite the remark in the test comment.
- **`ASYNC-14`** `A_future_cancelled_by_its_own_source_surfaces_OperationCanceledException_not_an_IO_error`: an `IAsyncHttpClient` returning a task cancelled by its own `CancellationTokenSource`; `AsBlocking().Execute`
  throws `OperationCanceledException` (not `IOException`, not `AggregateException`) and `ex.CancellationToken` is that source's token.
- **`ASYNC-13`** `A_self_referential_cause_chain_terminates_when_unwrapped`: using `TestSupport/Recovery/CyclicExceptions`, a faulted task whose exception's `InnerException` loops: `AsBlocking().Execute` surfaces an exception
  within a bounded call (the test uses the existing time-bound helper; no infinite loop) and the surfaced exception is the original, not an `AggregateException`.

**Verify.** V-build; `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --no-build --filter-class "*SyncToAsyncBridgeTests"` and `"*AsyncToSyncBridgeTests"`. `Dexpace.Sdk.Core.Tests` still references
no transport and not the kit (`Seam2ArchitectureTests` stays green). **IDs.** `ASYNC-2` (scheduler clause only; rejection clause ⏳), `-7`, `-13`, `-14`.

## Task 3.2 — `ASYNC` pins in core (2/2): logging-context rows

**Files.** Edit: `tests/Dexpace.Sdk.Core.Tests/Client/SyncToAsyncBridgeTests.cs` (or a new `Client/BridgeLoggingContextTests.cs` if the file passes ~600 lines; one class per concern is the house style).

Pins (`Unit`; passes on arrival; each proven able to fail by mutation):

- **`ASYNC-8`/`ASYNC-12`** `A_logging_scope_opened_on_the_caller_is_visible_inside_the_blocking_Execute`: a `LoggerExternalScopeProvider` scope (`using (provider.Push("caller"))`) is read, inside the wrapped blocking
  client, through the same provider (the `ExecutionContext`-flowing `AsyncLocal` the provider uses); run twice, once with `TaskScheduler.Default` and once with a scheduler that starts a **dedicated thread per task**
  (`LongRunning`, the thread-creating case of `ASYNC-12`): both see the scope.
- **`ASYNC-9`** `The_workers_own_scope_is_intact_after_a_call_returns_and_after_one_throws`: a one-thread scheduler; the worker opens its own scope before the bridged call; after a call returns and after a throwing
  call, that scope is still current on the worker (save/install/restore, including on throw).
- **`ASYNC-10`** `Capture_happens_per_call_not_at_AsAsync_construction`: one bridge built under scope A, called under B then C: the blocking client logs B then C.
- **`ASYNC-11`** `With_no_scope_provider_a_round_trip_raises_nothing_and_sees_no_scope`: a plain `NullLogger`, no provider: the round trip completes, nothing thrown, the worker sees no scope.

**Verify.** V-build; V-fast `*SyncToAsyncBridgeTests` (and `*BridgeLoggingContextTests` if split); count rises by exactly 8 across 3.1 and 3.2 (4 + 4, theory rows aside; the executing agent records the actual delta). **IDs.** `ASYNC-8`, `-9`, `-10`, `-11`, `-12`.

## Task 3.3 — Documentation: `conformance.md` and the kit's README

**Files.** New: `docs/sdk-documentation/conformance.md`. Edit: `src/Dexpace.Sdk.Conformance/README.md` (final text), `docs/sdk-documentation/seams.md` (one pointer paragraph), `docs/README.md` only if its ownership
table lists `sdk-documentation/` pages individually (read it first).

`conformance.md` is a user page (house style: read `docs/sdk-documentation/sse.md` and `pagination.md` first), with: **What the kit is** (framework-free; what a green run does not prove, quoting
`ConformanceReport.Preamble`); **Driving it** (the xUnit driver verbatim, extracted from the `// <driver>` … `// </driver>` region of `RawSocketConformanceTests` (2.14), which compiles; `ConformancePageTests` also compares the page's code block to that region, the file copied to the test output like the appendices, so the documented sample cannot drift into a non-compiling copy; with a note that NUnit/MSTest are the same shape, and the `RunAllAsync` form); **Describing a transport** (`TransportSubject`, the two
faces, the five hooks with the table of 2.1 and the status each missing hook produces); **Statuses and waivers** (the six statuses; waivers by ID with `Owner`, `Assertion`, `Face`; the stale-waiver rule; waivers are listed every
run); **The fixture** (`Dexpace.Sdk.Conformance.Wire`: `LoopbackServer`, the scripts table, `WaitForRequestAsync`, `WaitForConnectionReleasedAsync`, "released means closed or reused"); **The assertion catalogue** (the 40 names,
their IDs, level and faces, generated by hand from `TransportSuite.Assertions` and checked by a `Unit` test in `Conformance.Tests` that every name in the page exists and vice versa — add
`Docs/ConformancePageTests.cs` reading `docs/sdk-documentation/conformance.md` copied to the output like the appendices); **Versioning** (lockstep; "a release that adds an assertion is at least a minor version"; names are frozen
at the first `PublicAPI.Shipped.txt`; the proposed first version `0.1.0`, open for the lead); **What 8a does not cover** (TLS, HTTP/2, connect timeout, the `SERDE`/pagination lifts for phase 10, `Dexpace.Sdk.Reactive`).

The README: install line (pre-release callout), the four-line example, the minor-version rule, the link to the page.

**Verify.** V-build; V-fast `*ConformancePageTests`; `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`. **IDs.** none (documentation); `P8a-5`, `P8a-12`.

## Task 3.4 — Knowledge note

**Files.** New: `docs/knowledge/notes/transport-adapter.md` (the topic exists under `harvested/`; no note file does yet — read `docs/knowledge/README.md` and `notes/testing.md` for the entry format first).

Entries (each with the review marker line the other notes carry, `<sub>review · docs/work/mvp/phase8/phase8a/2026-10-09-phase8a-conformance-kit-checklist.md · high · sha:manual-p8a-…</sub>`, matching the file's existing
form): F1 (a bad response header **name** fails the whole response in `SocketsHttpClient`; a control byte in a **value** passes), F3 (`CancelPendingRequests`/`Dispose` mid-call raise `TaskCanceledException` with an `IOException` inner;
`HttpClient.Timeout` with a `TimeoutException` inner — the discriminator), F6 (no native re-send after a single-use body was fully written; the stale-connection re-send path remains), and any difference task 0.1 found.
Then `scripts/knowledge verify-structure`.

**Verify.** `scripts/knowledge verify-structure`; `scripts/knowledge --origin note --brief` lists the new file. **IDs.** `P8a-24`.

## Task 3.5 — The checklist

**Files.** New: `docs/superpowers/specs/2026-10-09-phase8a-conformance-kit-checklist.md` (the docs inbox, filed in 3.8) in the format of
`docs/work/mvp/phase7/phase7c/2026-10-09-phase7c-pagination-checklist.md`: title, legend, intro with the as-built adaptation and red evidence (mutations performed), the **requirement rows** table, the cross-owner table
("Rows 8a also proves, owned elsewhere"), the 8b hand-over table (14 rows ⏳ 8b, each with its assertion name and its SystemNet waiver), deviations from the plan, and the closing hand-offs.

One row per owned ID, **38 rows**, each with its mark and a named test (`Class.Method`, `[Trait]`, file) per the design's census. Pre-filled marks (adjust to what was built):

- `TRANSPORT-2`, `-3`, `-7`, `-11`, `-15`, `-16`, `-19`, `-20`, `-21`, `-22`, `-23`, `-24`, `-25`, `-26`, `-27`, `-29`: ✅, evidence = the kit assertion(s) (`transport-n.…`) by name, run by `SystemNetConformanceTests` and
  `RawSocketConformanceTests` (`Conformance`), their negative control class (`Integration`), plus the existing test the design's census cites.
- `ASYNC-1`: ✅ (kit + `DelegateHttpClientTests`). `ASYNC-2`: ✅ for the scheduler and argument-failure clauses (kit `transport-21.*`, `SyncToAsyncBridgeTests`, the 3.1 scheduler pin), **⏳ for the worker-pool-rejection clause** (F-A2: `TaskSchedulerException` escapes synchronously; owner lead / core follow-up). `ASYNC-3`, `-14`: ✅ cooperative (design §10 entry 8). `ASYNC-4`: 🚫 vacuous (banned `Thread.Interrupt`).
  `ASYNC-5`: ✅. `ASYNC-6`: ✅ on the bridges, vacuous for runtime facades (P8a-3). `ASYNC-7`..`-13`: ✅ with the 3.1/3.2 pins. `ASYNC-15`: ✅. `ASYNC-16`: N/A. `ASYNC-17`, `-18`, `-19`: ✅. `ASYNC-20`: ✅ (kit + core).
  `ASYNC-21`: N/A adapter-scoped (§11 item 21; reported `Vacuous`). `ASYNC-22`: ✅ (kit + `ConcurrencyTests`).
- Totals line: **34 ✅, 1 ✅ with one clause ⏳ (`ASYNC-2`), 3 N/A or vacuous (`ASYNC-4`, `-16`, `-21`), 0 rows wholly ⏳** (16 + 22 = 38); `ASYNC-6` is ✅ with its facade half vacuous.

The checklist also records: the measured SystemNet waiver list (task 2.13) against the design's expected one; the raw-socket driver's waivers (R4); R1 (B.7 has six items); R3 (waiver narrowing); F-A2 (`ASYNC-2` rejection clause) and R13 (client-caused release); the D3 and `Dexpace.Sdk.Reactive`
verdicts; the proposed first version.

**Verify.** `dotnet run --project .claude/skills/housekeeping/src -- probe --only citations,links` (the checklist cites tests by name; the probe checks IDs resolve). **IDs.** all 38.

## Task 3.6 — `CHANGELOG.md`

**Files.** Edit: `CHANGELOG.md` (`[Unreleased]`).

Add under `### Added` (create the heading if absent, matching the file's existing section order): one entry, "**Transport conformance kit** (phase 8a; `TRANSPORT-2`, `-3`, `-7`, `-11`, `-15`, `-16`, `-19`..`-27`, `-29`, `ASYNC-1`..`ASYNC-22`).
New package `Dexpace.Sdk.Conformance`: framework-free `TransportSuite` with 40 named assertions, `TransportSubject` with capability hooks, `ConformanceReport` with per-requirement and appendix B.6/B.7 views, waivers by ID, and the promoted
loopback wire fixture `Dexpace.Sdk.Conformance.Wire` (gated, aborting, hanging, large and echo replies; release observation). Driven against `SystemNetHttpClient` and, through `DelegateHttpClient`, a test-only raw-socket client.
**Test-only move:** `Dexpace.Sdk.Http.SystemNet.Tests.Loopback.*` is now `Dexpace.Sdk.Conformance.Wire.*`. No change to any published package surface." No **Breaking** marker (nothing published changes). No AI attribution.

**Verify.** `git diff -- CHANGELOG.md` is one hunk; the housekeeping probe. **IDs.** none.

## Task 3.7 — Roadmap status note, design corrections, register and `CLAUDE.md`

**Files.** Edit: `docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md` (append a dated entry to `## Phase Status Notes`; the Phase 8 card's 8a status line), `docs/sdk-design-dotnet/` §2.1, §9.3 and §12 as **dated
corrections** (the design is frozen to routine work: a correction with a date, never a silent edit; read the file's existing correction format), `docs/first-release.md` (*Packages* row), `CLAUDE.md`.

1. **Status note** (`### 2026-10-09 — Phase 8a (conformance kit) built`), in the form of the 7c entry: the census (38 rows: 34 ✅, 1 ✅ with one clause ⏳ (`ASYNC-2`), 3 N/A or vacuous; 14 rows ⏳ 8b by assertion name), the `ASYNC-2` finding F-A2 (rejecting scheduler throws `TaskSchedulerException` synchronously from `SyncToAsyncAdapter.ExecuteAsync`; a core `Task.FromException` wrap is the follow-up, for the lead to rule into a later phase), the segmentation (P8a-1) and the
   **reassignment of `TRANSPORT-2`, `-11`, `-22`, `-27`** from the earlier informal 8b labels (6a checklist, phase 1 S2/S9) with its reason; D3 confirmed on three findings (P8a-2) and **still for the lead to rule**;
   `Dexpace.Sdk.Reactive` not built (P8a-3); the first published version proposed (`0.1.0`, **open for the lead**, P8a-5); R1/R3/R4/R13 as deviations from the plan; the measured SystemNet waiver list; and **hand-offs**: 8b (start from
   `SystemNetConformanceTests`' waiver list, delete each waiver as its row closes; F1/F3/F7; `SEAM-15`'s latch; add `CreateWithProxy` and a handler-taking-constructor subject; keep the 38 green across the sync-path rewrite; the
   adaptation re-typing decision), 9 (a third driver over the DI-built client; `transport-1` is the proof), 10 (invariant, packaging and codec suites in the same package; the `SERDE-3`, `-4`, `-9`, `-10`, `-12`, `-13`, `-15`..`-20`
   lift of 7a and 7c's `link-header.json` and splice properties; `APPENDIX_B.md` over all 61 items; `RequirementCatalog` reused for the row-count gate), 12 (publish at the lockstep version; `conformance.md` joins the user docs).
   The Phase 8 card gets "*Status, 2026-10-09: 8a is built (issue #88); see the Phase Status Notes.*" in the style of the Phase 7 card.
2. **Design corrections** (dated): §2.1 As built — `Conformance` exists; §9.3 — the kit **is** measured by the coverage gate (P8a-16), the fixture gained release observation (P8a-21), statuses are six with `NotExercised`
   (P8a-7), the `DelegateHttpClient` driver runs over a test-only raw-socket client (P8a-2); §12 — `ASYNC-6` met on the bridges (P8a-3), the `TRANSPORT` row names the assertion catalogue as per-ID evidence.
3. **Roadmap card corrections** (dated, appended to the card or status note): the Ruby source paths do not exist locally (P8a-25); "`ASYNC-6` … vacuous by antecedent" refined (P8a-3).
4. **`docs/first-release.md`**: the *Packages* row for `Dexpace.Sdk.Conformance` (exists, `0.0.1-alpha.1`, unpublished, owner 12) and the proposed first version.
5. **`CLAUDE.md`**: add `src/Dexpace.Sdk.Conformance/` and `tests/Dexpace.Sdk.Conformance.Tests/` to the layout tree; add "the transport conformance kit (`Dexpace.Sdk.Conformance`, phase 8a)" to the built list of "What is
   genuinely unbuilt" with a pointer to `docs/sdk-documentation/conformance.md`, and remove "the transport conformance kit (8)" from the unbuilt list (leave 8b's reference-transport hardening as unbuilt); add one sentence to
   "Things That Will Bite You" that a kit assertion needs a negative control and a stale waiver fails the run; add the kit to the `dotnet test` filter hint (`--filter-trait "Category=Conformance"`). Recount any number in
   `CLAUDE.md` that the housekeeping probe flags.

**Verify.** `dotnet run --project .claude/skills/housekeeping/src -- probe` reports nothing for these files. **IDs.** none.

## Task 3.8 — Filing, final gate and handover

1. File the documents: `dotnet run --project .claude/skills/housekeeping/src -- probe`, then `apply --delivery mvp --phase 8a` (dry run), then `apply --delivery mvp --phase 8a --write` (`git mv`): the design, this plan and
   the checklist land under `docs/work/mvp/phase8/phase8a/`; fix what the next probe reports (links from the status note to the filed paths).
2. Run V-gate in full, then the coverage gate and `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`.
3. Final greps: no AI attribution in any new file; no `Task.Delay`/`Console` in the kit; `git diff --stat main...HEAD -- src/Dexpace.Sdk.Http.SystemNet src/Dexpace.Sdk.Core` empty; every new `.cs` file has the license header
   (`IDE0073` enforces it).
4. Stop. Do **not** push, open a PR, comment on issue #88 or run any `gh` command: those are the lead's (global hard rule). The handover message lists: the three PR branches and titles; the exit criteria status; the open items for the lead (P8a-1 segmentation file placement, P8a-2 D3 and raw-socket cost, P8a-5 first version, R3 waiver narrowing, R1 B.7 count).

PR title `docs: phase 8a ASYNC pins, conformance documentation and close-out (TRANSPORT, ASYNC; part 3/3)`.

---

## Checklist: one row per owned ID

The plan's own row table for step 4 (the checklist document is written from what was built; this is the intent). `Kit` = the assertion named in the design's catalogue, run by `SystemNetConformanceTests` and
`RawSocketConformanceTests`; `Core` = an existing or new `Dexpace.Sdk.Core.Tests/Client` test.

| ID | Level | Planned mark | Task(s) | Evidence to name |
|---|---|---|---|---|
| TRANSPORT-2 | MUST | ✅ | 2.8 | Kit `transport-2.no-silent-resend`; control `OutboundControlTests` |
| TRANSPORT-3 | MUST | ✅ | 2.6 | Kit `transport-3.cancel-is-terminal` (both faces); `SystemNetHttpClientTests` cancel facts |
| TRANSPORT-7 | MUST | ✅ | 2.6 | Kit `transport-7.cancel-releases-exchange`, `transport-7.attempt-timeout-aborts` |
| TRANSPORT-11 | MUST (+SHOULD) | ✅ | 2.8 | Kit `transport-11.framing-recomputed`, `transport-11.drop-logged`; `FramingHeaderDropWireTests` (`Security`, unchanged) |
| TRANSPORT-15 | MUST | ✅ | 2.9 | Kit `transport-15.borrowed-survives`, `transport-15.owned-released`; `SystemNetHttpClientOwnershipTests` |
| TRANSPORT-16 | MUST | ✅ | 2.6 | Kit `transport-16.close-idempotent-nonblocking`; `SystemNetHttpClientDisposeTests` |
| TRANSPORT-19 | SHOULD | ✅ | 2.9 | Kit `transport-19.abandoned-body-unblocks` |
| TRANSPORT-20 | MUST | ✅ | 2.9 | Kit `transport-20.no-response-is-retryable`, `transport-20.retried-by-the-pipeline` |
| TRANSPORT-21 | MUST | ✅ | 2.5 | Kit `transport-21.pre-dispatch-failure-via-task`; `SystemNetHttpClientTests` |
| TRANSPORT-22 | MUST | ✅ | 2.9 | Kit `transport-22.adaptation-failure-releases`; `MalformedContentTypeWireTests` |
| TRANSPORT-23 | MUST | ✅ | 2.5 | Kit `transport-23.never-null` |
| TRANSPORT-24 | MUST | ✅ | 2.5 | Kit `transport-24.vendor-status-readable` |
| TRANSPORT-25 | MUST | ✅ | 2.7 | Kit `transport-25.lazy-body`, `.large-round-trip`, `.dispose-releases` |
| TRANSPORT-26 | MUST | ✅ | 2.7 | Kit `transport-26.bodyless-methods` |
| TRANSPORT-27 | SHOULD | ✅ | 2.7 | Kit `transport-27.inbound-downgrade`; `MalformedContentTypeWireTests` |
| TRANSPORT-29 | MUST | ✅ | 2.8 | Kit `transport-29.concurrent-no-crosstalk` |
| ASYNC-1 | MUST | ✅ | 2.5 | Kit `async-1.single-non-null-response`, `transport-23.never-null`; `DelegateHttpClientTests` |
| ASYNC-2 | MUST | ✅ scheduler and argument clauses; ⏳ worker-pool-rejection clause (F-A2, owner lead / core follow-up) | 2.5, 3.1 | Kit `transport-21.*`; Core pin `AsAsync_runs_the_blocking_call_on_the_supplied_scheduler`; no rejecting-scheduler pin (red on arrival, core change needed) |
| ASYNC-3 | MUST | ✅ (cooperative, §10 entry 8) | — | `SyncToAsyncBridgeTests.A_token_cancelled_before_the_send_never_runs_it` |
| ASYNC-4 | MUST | 🚫 vacuous | — | `BannedSymbols.txt` (`Thread.Interrupt`); §3.3, §10 entry 8, §12 |
| ASYNC-5 | MUST | ✅ | — | `SyncToAsyncBridgeTests.A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces` |
| ASYNC-6 | MUST | ✅ on the bridges; vacuous for facades | — | `AsyncToSyncBridgeTests.Cancelling_the_token_…`; `SyncToAsyncBridgeTests.The_exact_options_and_token_instances_reach_Execute` |
| ASYNC-7 | SHOULD | ✅ (docs + pin) | 3.1 | Core pin `AsAsync_aborts_a_token_honouring_client_and_a_token_ignoring_one_…` (ignoring client runs on, call completes cancelled, response disposed once; SEAM-30) |
| ASYNC-8 | SHOULD | ✅ | 3.2 | Core pin `A_logging_scope_opened_on_the_caller_is_visible_…` |
| ASYNC-9 | MUST | ✅ | 3.2 | Core pin `The_workers_own_scope_is_intact_…` |
| ASYNC-10 | MUST | ✅ | 3.2 | Core pin `Capture_happens_per_call_…` |
| ASYNC-11 | MUST | ✅ | 3.2 | Core pin `With_no_scope_provider_…` |
| ASYNC-12 | MUST | ✅ | 3.2 | Core pin (dedicated-thread scheduler case) |
| ASYNC-13 | MUST | ✅ | 3.1 | Core pin `A_self_referential_cause_chain_terminates_…`; `AsyncToSyncBridgeTests.A_faulted_InvalidOperationException_…` |
| ASYNC-14 | MUST | ✅ (cooperative) | 3.1 | Core pin `A_future_cancelled_by_its_own_source_…` |
| ASYNC-15 | MUST | ✅ | 2.6, 2.9 | Kit `transport-15.*`, `transport-16.*`; `ClientDisposeIdempotenceTests` |
| ASYNC-16 | SHOULD | N/A | — | §3.7 (no SDK adapter owns an executor) |
| ASYNC-17 | SHOULD | ✅ | — | `DelegateHttpClientTests.It_keeps_working_after_dispose` |
| ASYNC-18 | MUST | ✅ | — | `TimeProviderWaitsTests` (four rows) |
| ASYNC-19 | MUST | ✅ | — | `SyncToAsyncBridgeTests.The_exact_options_and_token_instances_reach_Execute`; `AsyncToSyncBridgeTests.Options_and_token_reach_ExecuteAsync_by_reference` |
| ASYNC-20 | MUST | ✅ | 2.6 | Kit `async-20.late-cancel-leaves-response-open`; `DelegateHttpClientTests`, `SyncToAsyncBridgeTests` late-cancel facts |
| ASYNC-21 | MUST | N/A (adapter-scoped; reported `Vacuous`) | — | §11 item 21; `SSE-39` |
| ASYNC-22 | MUST | ✅ | 2.8 | Kit `transport-29.concurrent-no-crosstalk`; `ConcurrencyTests` |

Planned totals: **34 ✅, 1 ✅ with one clause ⏳ (`ASYNC-2`), 3 N/A or vacuous, 0 rows wholly ⏳**; with the 14 rows of 8b listed as ⏳ 8b, the phase-8 census stays 52.

## Exit criteria (the design's list, mapped to tasks)

1. Segmentation recorded and the checklist at 38 rows: design, tasks 3.5, 3.7.
2. Green against both drivers on every OS row with waivers by ID: tasks 2.13, 2.14, 2.16 (CI's matrix is the proof; local flake pass in 2.16).
3. Packable with README, `PublicAPI` files and package validation; audit and reproducible pack name it; AOT clean: tasks 1.1, 1.11, 2.15.
4. First published version named: tasks 3.3, 3.7 (proposed `0.1.0`, open for the lead).
5. D3 and `Dexpace.Sdk.Reactive` verdicts recorded in the status note for the lead: task 3.7.
6. Every gate green incl. coverage with the kit measured: the V-gate of 1.12, 2.16, 3.8.
7. `conformance.md`, README, `CHANGELOG.md`, status note, corrections, knowledge note, probe clean: tasks 3.3, 3.4, 3.6, 3.7, 3.8.

## Open items carried to the lead (none blocks execution)

- **P8a-1**: the segmentation lives in the design, not a separate file; liftable verbatim.
- **P8a-2**: D3 confirmed; the raw-socket client costs ~400 test lines (task 2.3). Fallback: replace its subject in 2.14 by a `DelegateHttpClient` decorator over `SystemNetHttpClient` and record "type-agnostic" only.
- **P8a-5**: the first published version, proposed `0.1.0`.
- **R3**: `ConformanceWaiver.Assertion`/`Face` (public surface +2). Fallback: split the multi-assertion IDs' waivers by removing the narrowing and accepting that `TRANSPORT-11`'s SHOULD log clause on the raw client is
  expressed by implementing the log in the raw client instead (about 15 lines).
- **R1**: appendix B.7 has six items; the design said five.
- **F-A2**: `ASYNC-2`'s worker-pool-rejection clause needs a core fix (`SyncToAsyncAdapter.ExecuteAsync` must wrap `StartNew` and return `Task.FromException`); the plan leaves it ⏳ because P8a-22 forbids a
  core edit in 8a. Ruling wanted: accept ⏳ (default) or admit a one-method core change into 8a (then TDD red/green in 3.1, and the row returns to ✅).
- **R13**: `LoopbackServer.ServerClosedFirst` (internal) and `keepAlive` on `Streamed` are additions to the design's fixture list.

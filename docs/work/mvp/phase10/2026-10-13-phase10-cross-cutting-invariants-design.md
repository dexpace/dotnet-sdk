# Phase 10 — Cross-Cutting Invariants and Conformance: Design

**Status:** Draft, for review. Written 2026-10-13 against `main` at `af603d2` (phases 0 to 9 merged), on branch
`99-phase-10-planning`, for issue [#99](https://github.com/dexpace/dotnet-sdk/issues/99). Brainstormed without a human in the
loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling (`P10-n`)
with its options and a recommended default, and the ones the lead may still reverse are marked **open for the lead**. The
scope authority is the roadmap's Phase 10 card and Phase List row 10
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`), issue #99, and every Phase Status Note bullet and checklist
"rows owned elsewhere" row addressed to 10 (4c, 5b, 5c, 6a, 6b, 6c, 7a, 7b, 8a, 8b, 9). The format follows the phase 9 design
(`docs/work/mvp/phase9/2026-10-12-phase9-host-integration-design.md`) and the phase 8 designs. An independent validation of this
design and its plan (13 findings) was resolved on 2026-10-13: see [Validation resolution (2026-10-13)](#validation-resolution-2026-10-13),
whose items amend the text below where they say so.

**What this document is.** The phase design for 10, which has no sub-phases (P10-1). It holds:

- the segmentation decision, recorded here rather than in a file of its own (P8a-1's and P9-1's precedent);
- a **measured** verdict for each of the 41 rows (`XCUT-1`–`XCUT-24`, `NFR-1`–`NFR-17`) at `af603d2`, replacing the roadmap's
  starting counts, and the disposition the checklist is expected to record;
- the disposition of every hand-off addressed to 10;
- three new suites in `Dexpace.Sdk.Conformance` (invariant, codec, packaging), the `NFR-11` signature scan, the completed
  NativeAOT smoke consumer, `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` and its gate, the appendix-C coverage script,
  and the disposition of every NFR gate (phase 0 left no gate checklist; this design's gate table is the first);
- the decisions on the two optional gates of design §9.3 (Stryker.NET, `benchmarks/`);
- sixteen verifications run for this design (V1 to V16) on .NET SDK 10.0.401 (runtime 10.0.12), Linux;
- the rulings (which double as deviation-ledger IDs, roadmap constraint 7), the test strategy, PR segmentation, hand-offs,
  close-out corrections and exit criteria.

**What this document is not.** It is not the plan and not the checklist. It edits no design chapter, roadmap cell, register,
checklist or `CLAUDE.md` line, and it changes no file under `docs/product-spec/`, `docs/sdk-design-dotnet/`, `docs/styleguide/`
or `docs/knowledge/harvested/`; the corrections it owes are proposals, listed in
[Corrections owed at close-out](#corrections-owed-at-close-out-proposals). It does not re-argue design §10 entries 1, 2, 7, 8 and 9
or §11 items 2, 8, 9, 23, 32 and 37; it cites them and records where the as-built tree refines them.

---

## Prerequisites

| Predecessor | Kind | State at `af603d2` |
|---|---|---|
| **7** — 7a's serde (the `SERDE` lift handed to the kit), 7b's SSE (`XCUT-1` evidence), 7c's pagination (vectors offered to the kit) | **dependency** (card "Entry") | Met (7a, 7b, 7c merged). |
| **8a** — `Dexpace.Sdk.Conformance`: assertions, six statuses, waivers that must stay needed, `RequirementCatalog` generated from appendix C, `AppendixBMap` for B.6 and B.7, the negative-control rule (`AssertionCatalogueTests`) | **dependency** (card "Entry") | Met. The three new suites extend this package; its csproj, version and release stay 8a's and 12's (the card's "does not own"). |
| **8b** — the reference transport's two permanent waivers (§10 entries 32, 33) and the classification table (`XCUT-2`), the wire re-check (`XCUT-18`), ownership (`XCUT-22`) | **dependency** | Met; audited here against the 8b checklist. |
| **9** — `Dexpace.Sdk.Extensions.DependencyInjection`: `XCUT-10`'s DI clause, `XCUT-22`'s lease, `XCUT-23`'s single-registration check, `NFR-2`'s audit policy record, `CheckPhase9Async` | **dependency** (card "Entry") | Met (#98). `XCUT-10` is the one row another phase left ⏳ 10. |
| **0** — the NFR machinery: coverage gate and its self-test, dependency audit, reproducible pack, AOT smoke, `IDE0073`, `RS0016`/`RS0017`, `RS0030`, warnings as errors | **dependency** | Wired; **no phase 0 design or checklist exists** (the 2026-09-29 status note: phase 0 "has no design document of its own"), so no gate has been dispositioned yet. This design's gate table is the first record. |
| **4c, 5b, 5c, 6a, 6b, 6c, 7b** — evidence recorded against `XCUT` rows in their "rows owned elsewhere" tables | **dependency** (evidence, not code) | Read and audited row by row below. |
| Phase 11 | **dependent** | Takes the findings this phase routes (P10-2), the §10/§11 proposals and the unmet-clause count question. |

No dependency edge inverts the roadmap's order: 7, 8 and 9 exited before this design (issue #99, "Entry").

## Governing documents, and the phase-start queries

Read for this design: product-spec chapters 19 and 20 in full, appendix B in full and appendix C rows `XCUT-1`–`XCUT-24`,
`NFR-1`–`NFR-17`; design §9 (gate table, §9.1–§9.4 with every dated correction), §10 entries 1, 2, 7, 8, 9, 23, 32 and 33, §11
items 2, 8, 9, 21, 23, 24, 25, 32 and 37, §12 in full; the roadmap's constraints 1–10, the segmentation rule, the Phase 0, 8,
9, 10, 11 and 12 cards, the Maturity Definition and every status note; `docs/first-release.md`; the 4c, 5b, 5c, 6a, 6b, 6c,
7a, 7b, 8a, 8b and 9 checklists; the phase 9 and 8a designs; `docs/sdk-documentation/conformance.md`; the code under
`src/Dexpace.Sdk.Conformance` (all files), `src/Dexpace.Sdk.Core/{Errors,Resilience,Internal,Diagnostics}`, `scripts/ci/*`,
`tests/Dexpace.Sdk.AotSmoke`, `.github/workflows/ci.yml`, `Directory.Build.props`, `Directory.Packages.props`,
`.editorconfig` and `BannedSymbols.txt`.

```bash
scripts/knowledge --origin note --brief          # no note on XCUT or NFR; tooling-and-quality-gates and testing notes read (LangVersion, Shouldly)
scripts/knowledge --section conflicts --brief    # no conflict on XCUT/NFR; styleguide 11.8 (Stryker.NET) becomes one (P10-21)
scripts/knowledge --prefix-info XCUT             # 24 IDs, 22 MUST / 2 SHOULD; 24 of 24 substantive; topics retry-and-resilience, cross-cutting-invariants, redaction-and-security
scripts/knowledge --prefix-info NFR              # 17 IDs, 4 MUST / 13 SHOULD; 17 of 17 substantive; topics tooling-and-quality-gates, package-and-dependency-layout, non-functional-requirements
scripts/knowledge --gaps XCUT / --gaps NFR       # 0 gaps each
scripts/knowledge --prefix XCUT --section rules --brief   # read; the cross-cutting-invariants topic is appendix-B roll-ups only, so each row was read in ch.19 itself
```

The CLI warns that `docs/sdk-design-dotnet/05-pipeline-architecture.md` is stale in the harvest (harvested at `1608fcd4b329`,
now `651d4371350d`); §5 was read from the file. Every `XCUT` entry under `cross-cutting-invariants` is an `[appendix-B roll-up]`,
not an answer, so each row was read in chapter 19 and appendix C, per the skill's roll-up path.

**Sibling sources.** Both siblings are on disk at the commits the roadmap names: `ruby-sdk@5b17395` and `nodejs-sdk@54aeed4`
(`~/Projects`). The paths the issue names all exist, which corrects the 8a status note's "the Ruby paths under Port from siblings
do not exist in the local checkout" for this phase's paths:

- `ruby-sdk/gems/dexpace-conformance/lib/dexpace/conformance/{invariant_suite,packaging_suite,codec_suite}/` and their entry
  files: 28 invariant assertions over the 24 `XCUT` IDs (`XCUT-11`, `-13`, `-14`, `-18` carry two each), 8 packaging assertions
  (`NFR-1`, `-2`, `-3`, `-10`, `-11`, `-13`, `-14`, `-15`), 2 codec assertions (`SEAM-20`/`SERDE-3`, `SERDE-9`);
- `ruby-sdk/gems/dexpace-conformance/APPENDIX_B.md` (61 rows, statuses `suite`, `by reference`, `restated per §9.3`,
  `scoped out`), `ruby-sdk/tools/appendix_b.rb` and `ruby-sdk/tools/requirement_levels.rb`;
- Ruby's own phase design for this scope, `ruby-sdk/docs/work/mvp/phase9/2026-09-12-phase9-cross-cutting-invariants-and-conformance-design.md`
  (its `R2`, `R3`, `R4`, `R6` and `R7` are the method this design adapts);
- `nodejs-sdk/tests/conformance/xcut/` (six files, one per §19 subsection: `cancellation-and-timeout`, `error-taxonomy`,
  `retry-safety`, `concurrency-and-lifecycle`, `security-by-default`, `diagnostic-previews`) and
  `nodejs-sdk/docs/work/mvp/phase9/2026-07-28-phase9-cross-cutting-conformance-design.md`.

---

## Segmentation

The roadmap's rule binds "a build phase (1 through 9)", so it does not reach phase 10 by its letter; the card says "Phase 10
decides", and issue #99 asks the design to record the answer. Measured against the rule's three triggers anyway: the ID count
(41) does not clearly exceed an earlier phase's (phase 6 has 111 plus 15 carried, phase 7 has 107, phase 8 has 52); the phase spans
two ID-bearing chapters (ch.19 and ch.20), so the chapter trigger fires; and it ships into one package
(`Dexpace.Sdk.Conformance`) whose csproj and release it does not own, plus test projects and scripts. **The answer: one phase,
no sub-phases** (P10-1, open for the lead).

Why one phase suffices, and why the obvious cut is worse:

- **The obvious cut is by chapter: 10a `XCUT` (invariant and codec suites), 10b `NFR` (packaging suite, gates, AOT, coverage
  script).** It fails on the shared contract. All three suites need the same kit generalisation first (a suite-neutral runner, a
  face for assertions with no call surface, a report that combines suites, per-suite preambles; P10-7). That contract would either
  sit in 10a with 10b depending on it (a dependency chain dressed as two phases) or become a contract sub-phase whose only job is
  to restate 8a's model, which Ruby's phase 9 recorded as the failure the rule exists to prevent.
- **Appendix B straddles both chapters.** B.8 (XCUT) and B.9 (NFR) are two of nine sections; the other seven are mapped `by
  reference` or `restated` to earlier phases' evidence. The map and its gate are one artefact with one test.
- **The coverage script needs this phase's own checklist rows.** It can only pass once all 41 rows exist, so it closes the
  phase rather than either half.
- **Precedent.** Ruby's phase 9 (the same scope) and Node's phase 9 were not segmented; neither was this roadmap's phase 9.

The order inside the phase is a **dependency** chain, expressed as stacked pull requests ([PR segmentation](#pr-segmentation)):
the kit generalisation and the invariant suite first, then the codec suite, then the packaging suite with the NFR gates, then
appendix B, the coverage script, the AOT smoke and the close-out. If the lead prefers sub-phases, the cut above (10a = PRs 1–2,
10b = PRs 3–4, ordered as a **dependency**) can be lifted from this section verbatim.

---

## Facts the design rests on

Every spike ran on .NET SDK 10.0.401 (runtime 10.0.12), Linux, over a `git archive` (or, for V3, two `git clone`s) of `af603d2`
in the session scratchpad. None is committed; the plan's first task re-runs each as a test or a CI step.

| # | Verification | Result | Consequence |
|---|---|---|---|
| V1 | Baseline: locked restore, Release build, the full test run with coverage, the coverage gate, pack, the dependency audit | Build green (0 warnings). 6857 tests: 6835 passed, 0 failed, 22 skipped, every one a conformance driver's `NotExercised` or permanent-waiver row (the raw-socket driver 9, the DI driver 5, the SystemNet driver 8; none in the core, codec or kit unit suites). Aggregate line coverage **97.60%** (Conformance 97.73, Core 97.74, DI 97.67, SystemNet 95.17, STJ 99.04). Audit: "5 libraries conform". | `NFR-5` is met with a 17-point margin; the gate is dispositioned, not raised (P10-15's table). |
| V2 | A console spike over **core's public surface only** (what a kit assertion can reach without `InternalsVisibleTo`) | `HttpResponseException.IsRetryable` matches the classifier for 400, 404, 408, 429, 500–505, 507, 599 (0 mismatches, `XCUT-5`). Through `PipelineBuilder.AddStandardResilience` over `DelegateHttpClient`: 501 in the configured set is sent 3 times, 500 outside it once (`XCUT-7`); a body-less `POST` failing with a `ServiceRequestException` over a `SocketException` is sent **once**, a body-less `GET` 3 times (`XCUT-10`); a custom non-SDK exception implementing `IRetryableError` is retried 3 times (`XCUT-6`). Cancelling during a 30 s backoff (recording `TimeProvider`): `OperationCanceledException` after 2 ms carrying the caller's token, the token still cancelled, 1 send, 1 timer created and 1 disposed (`XCUT-1`, `XCUT-3`). `ExceptionFacts.EnumerateCauses` over a self-cycle (built by reflection on `Exception._innerException`) yields 1 (`XCUT-9`). `HttpHeaderSyntax`: a name with HTAB or DEL invalid, an outbound value with HTAB valid, a non-ASCII outbound value invalid, an obs-text inbound value valid, an LF inbound value invalid (`XCUT-18`). `ErrorMappingStep.Instance.Apply` returns a 200 unchanged; **the public `HttpResponseException(Response)` constructor accepts a 200** (`XCUT-8`). | Every `XCUT` clause is reachable from the kit through public API **except** `XCUT-8`'s strict factory (internal `ErrorMapping`), a cyclic chain (needs private reflection), `XCUT-14`'s map size (internal `BoundedMap`), `XCUT-21`'s source identity and `XCUT-23` (the DI package). Four become subject hooks with no default (`MapStrict`, `CreateCyclicException`, `CreateBoundedMap`, `SeamResolution`, P10-4); the fifth, `XCUT-21`'s source identity, cannot be supplied by any driver (the factory is a private field set from a lambda in `DigestChallengeHandler`'s public constructor) and is cited by reference (validation resolution F5). |
| V3 | `NFR-12` across paths: pack from two locations with `SOURCE_DATE_EPOCH` and `ContinuousIntegrationBuild=true` | From two **`git clone`s** at different paths (`…/x/repo`, `…/yy/zz/repo2`): all five `.nupkg` byte-identical. From two **`git archive`** extractions (no `.git`): every package differs, only in the DLL, which embeds the absolute `obj/…/*.pdb` path (no `SourceRoot`, so no path map). | The build is reproducible across clean environments when built from a git checkout, which CI always is. `scripts/ci/reproducible-pack.sh` packs twice **in the same directory**, so it cannot see a path leak; it should pack from two clones at different paths (P10-14). A source tarball is not a reproducible input (recorded, not fixed). |
| V4 | Negative controls for `NFR-4` and `NFR-6`: a new public type, and a call to an `[Obsolete]` member, in `Dexpace.Sdk.Core` | Build fails: three `RS0016` errors (the undeclared type and members) and `CS0618` as an error. | `NFR-4`'s source half and `NFR-6` (deprecations included) block the build. |
| V5 | Stryker.NET 5.0.0 (`dotnet-stryker`, installed to the scratchpad) over `Dexpace.Sdk.Core.Tests`, mutating `Http/Common/HttpHeaderSyntax.cs` | Fails: "Project … is using Microsoft.Testing.Platform which is not yet supported by Stryker, see stryker-net#3094"; 0 tests discovered. | The mutation gate cannot run against this repository's test projects (xUnit v3 on MTP, roadmap constraint 6) without a parallel VSTest harness (P10-21). |
| V6 | The packed `Dexpace.Sdk.Core` DLL's assembly attributes, read with `System.Reflection.Metadata` and `System.IO.Compression` only, from a .NET 10 **file-based app** that references the kit through `#:project` | `AssemblyMetadata("IsTrimmable","True")`, `AssemblyMetadata("IsAotCompatible","True")`, `AssemblyInformationalVersion("0.0.1-alpha.1")`, `RepositoryUrl`; the nuspec has one `net10.0` group listing the facade. The file-based app inherits `Directory.Build.props` (it needed explicit `using`s and the header, `IDE0073`) and resolved a kit type. | The packaging suite needs no package beyond the BCL, and it can run over `artifacts/packages` as a CI step after `Pack` (P10-10). `NFR-9`'s "every shipped rule file is present" is readable from the shipped assembly itself. |
| V7 | The appendix-C coverage count over every `docs/work/mvp/**/*-checklist.md` | 645 IDs in appendix C (532 MUST incl. one MUST NOT, 99 SHOULD, 14 MAY). A row in a table whose header starts `\| ID \| Level \|` is an **owning** row: **604 of 645 IDs are owned, and the 41 that are not are exactly `XCUT-1`–`XCUT-24` and `NFR-1`–`NFR-17`** (26 of them MUST). 0 unknown IDs, 0 level mismatches against appendix C. 6 IDs are owned twice, each a carried row (`SEAM-5`, `SEAM-6`: 2b and 9; `HTTP-44`, `HTTP-45`: 3b and 7a; `OBS-35`: 5b and 9; `TRANSPORT-2`: 8a and 8b). Of all owning rows, 2 carry no legend mark in their first three cells (8a's `SEAM-15` and `HTTP-39` contributed rows sit in an owner-labelled table). | The script is feasible over the checklists as written, with one rule for carried rows (P10-20). After phase 10's checklist lands it must report 645 of 645. |
| V8 | Appendix B parsed with Ruby's nineteen-prefix pattern | 61 items (B.1 10, B.2 6, B.3 7, B.4 8, B.5 6, B.6 5, B.7 6, B.8 6, B.9 7); **276 distinct IDs, none in two items, at most 12 in one** (B.2.1); B.1.6 lists `PAGE-20` before `PAGE-19`; **`PAGE-4` and `PAGE-5` appear in no item**. | The gate compares ID *sets* per row; the map states that appendix B is silent on `PAGE-4`/`PAGE-5` (it is a strictly weaker claim, §11 item 9). |
| V9 | The repository's merge rules, read with `gh api` (read-only) | `main` has **no classic branch protection** (404) and one active ruleset: `deletion`, `non_fast_forward`, `pull_request` (1 approval, code-owner review, squash only) and `code_quality`. **No `required_status_checks` rule.** `ci.yml` has no `continue-on-error`. | Every gate fails its CI job, but a red job does not block a merge. `NFR-17`'s "blocking … rather than advisory" is met per job and unmet at the merge (P10-16). |
| V10 | `NFR-11`, a text scan of every `PublicAPI.Unshipped.txt` (`PublicAPI.Shipped.txt` is empty until phase 12) | Core's signatures name only `System.*` (including the in-box `System.Collections.Immutable`), `Microsoft.Extensions.Logging` and `Dexpace.*`. Adapters add `System.Net.Http` (SystemNet), `System.Text.Json` (STJ), `Microsoft.Extensions.DependencyInjection`/`Configuration` (DI). | `NFR-11` holds today for core; the gate is a reflection test over the assembly, not over the text file (P10-11), with an explicit ban list beside the allow-list because `System.Net.Http` and `System.Text.Json` sit *under* `System` (validation resolution F12). |
| V11 | `NFR-13`, every tracked `.cs` file | All **956** carry the two-line header (`RequirementCatalog.g.cs` after its `<auto-generated>` block, which `IDE0073` skips). The three `scripts/ci/*.cs` file-based apps are outside the solution, so `IDE0073` never sees them, and they carry it. The `.sh` scripts carry it; no `.csproj`/`.props`/`.targets` does. | `NFR-13` is met for source; a repository test closes the file-based-app blind spot (P10-12). Build files are not "source files" under this reading. |
| V12 | `NFR-7`: `.editorconfig` severities set to `none`, and every `#pragma warning disable` under `src/` | Library scope dials `CA1308`, `CA1054`–`CA1056` and `CA1062` to `none`, each with a rationale and **none with a re-enable condition**; tests/tools dial `CA2007` and `CA1707` to `none`; three `dotnet_code_quality.CA2000.*` options (line 52, library scope, `dispose_ownership_transfer_at_constructor`; lines 155 and 156, test scope, `dispose_ownership_transfer_at_method_call` and `excluded_type_names_with_derived_types`) widen what `CA2000` accepts. All 43 `#pragma warning disable` lines under `src/`, in 31 files (13 `CA1031`, 6 `CA1727`, 6 `CA2000`, 2 `CA5351`, 1 `CA5394`, 15 `RS0030`), carry a why-comment on the line or the line above (the 2026-10-13 validation re-counted the first draft's "19 sites"). | `NFR-7`'s "documented reason **and** re-enable condition" is half met (P10-13). |
| V13 | `XCUT-14`: every map-like field in `src/` | `ConcurrentDictionary` appears only inside `BoundedMap`. The caller- or server-keyed, instance-lived maps are `AccessTokenCache` (1024), `DigestChallengeHandler`'s nonces (1024) and `ContextStore`, all `BoundedMap`s, and `HeaderDropLog` (a lock-guarded case-insensitive set capped at 128, oldest evicted). Every other dictionary is per-instance and sized by its input (`Headers`, `Query`), per call (`CallState`, `RedirectChain`) or a static table (`Status`). | Met; the invariant is mechanised by a ban on `ConcurrentDictionary`'s constructors, so a second unbounded concurrent map cannot land silently (P10-18, as amended by validation resolution F1). |
| V14 | `XCUT-9`: every cause walk in `src/` | `ExceptionFacts.EnumerateCauses` (reference identity, depth 64), the kit's `Outcome` (a seen-set), `SendFailures` (one level, a pattern, no walk). `Exception.GetBaseException`, which walks to the end with no cycle check, is used nowhere. | Met; banning `GetBaseException` in `src/` keeps it so (P10-18). |
| V15 | `XCUT-4`: every `throw`/construction site of the SDK's exception types | `ServiceResponseException` is public, documented "always retryable (XCUT-4, RETRY-4)" and **constructed nowhere in `src/`**; no design chapter names it. | A third, never-thrown exchange-failure type beside the two branches `XCUT-4` asks for; a finding for phase 11 and the one-time public-surface choices (P10-25). |
| V16 | The NativeAOT smoke consumer, published and run | Publish clean (no trim or AOT warning), `aot-smoke: all checks passed`, exit 0; the smoke checks the `User-Agent` prefix (`dexpace-`) and `BuildInfo.RuntimeVersion`, **not `BuildInfo.SdkVersion`**; it references the five libraries as projects. No test asserts `IsTrimmable`/`IsAotCompatible` on every library. | `NFR-8` is met; `NFR-9`'s rule-presence clause and `NFR-15`'s packaged-version clause are not asserted (P10-15, P10-17). |

---

## Scope: the rows

Legend (roadmap constraint 3). This is a design, so a mark is the disposition the checklist is expected to record, with the
evidence that will prove it. **Split** means one clause of the row is ✅ and another rests on an existing §10 entry or §11 item
(vacuous, judged mechanism, or declined); no new §10 entry is opened by this phase (P10-26).

### What the measurement changed

The roadmap's starting counts (XCUT: 5 met, 9 partial, 7 missing, 1 N/A candidate; NFR: every MUST partial) are from `d45e64b`.
Measured at `af603d2`:

- **`XCUT`: every one of the 24 rows already has a mechanism and a passing test**, built by phases 1 to 9; none is missing.
  What is missing is (a) a test that cites the row's ID for nine rows (`XCUT-9`, `-12`, `-13`, `-15` and others are proven but
  uncited), (b) the portable, suite form the card asks for, and (c) two mechanical gates (`XCUT-9`, `XCUT-14`). Three findings
  came out of the measurement: `ServiceResponseException` is dead public surface (V15), the public `HttpResponseException`
  constructor accepts a non-error status (V2), and the error-body snapshot is consuming above 1 MiB (below).
- **`NFR`: 11 of 17 are met and evidenced today** (`NFR-1`, `-2`, `-3`, `-5`, `-6`, `-8`, `-10`, `-12`'s build half, `-13`, `-14`,
  `-4`'s source half); 5 are partial (`NFR-7` no re-enable conditions, `NFR-9` rule presence unasserted, `NFR-11` no scan,
  `NFR-15` packaged version unasserted, `NFR-17` no required status checks); 1 has no subject until the release (`NFR-16`).

### `XCUT` rows (24: 22 MUST, 2 SHOULD)

| ID | Level | Measured at `af603d2` | Disposition | Evidence (existing, then planned) |
|---|---|---|---|---|
| `XCUT-1` | MUST | met | ✅ **split**: the cancellation clause ✅; "re-assert the interrupt flag" is cooperative (§10 entry 8, sanctioned by `XCUT-1`'s own text) | 6a `OperationPolicyTests.A_caller_cancellation_still_surfaces_OperationCanceledException`; 7b `ServerSentEventStreamFailureTests.A_cancelled_caller_token_surfaces_OperationCanceledException_and_is_not_rewritten`; 8a kit `transport-3.cancel-is-terminal` (both faces); V2. Planned: invariant `xcut-1.cancel-is-terminal` (both faces: one send, the caller's token on the exception, the token still cancelled, no retry). |
| `XCUT-2` | MUST | met | ✅ | 6a `AttemptTimeoutWireTests`, `RetryPolicyEngineTests.An_attempt_timeout_*`; 8a kit `transport-4.timeout-is-retryable`; 8b `SendFailuresTests` (the one classification table: a deadline is a retryable `ServiceRequestTimeoutException`, decided by the deadline source's state, timeout branch first; §11 item 32). Planned: invariant `xcut-2.timeout-is-retryable` (an `AttemptTimeout` over a hanging subject transport: retryable, caller token clear, re-issued). |
| `XCUT-3` | MUST | met | ✅ | 6a `RetryPolicyTests` (the wait is promptly cancellable), `TimeProviderWaitsTests`; V2 (the armed timer is disposed). Planned: invariant `xcut-3.wait-promptly-cancellable` (both faces, a recording `TimeProvider`: cancellation within a bound, every armed timer disposed). |
| `XCUT-4` | MUST | met by mechanism; finding V15 | ✅ **split**: the two branches ✅ (`HttpResponseException` carries status, headers and body; a no-response failure is an `SdkException` with `IsRetryable` and an I/O-family cause); "belonging to the runtime's I/O-error family … catchable as a generic I/O error" per §10 entry 7 and §11 item 23 (judged, P14) | 8a kit `transport-20.no-response-is-retryable`; `FailureClassificationTests`. Planned: invariant `xcut-4.two-branches`. Finding P10-25 (dead `ServiceResponseException`) routed to phase 11. |
| `XCUT-5` | MUST | met | ✅ | 5a/6a `RetryFactsTests.IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505`; V2. Planned: invariant `xcut-5.one-classifier` (the baked flag through the public constructor, 507 included). |
| `XCUT-6` | MUST | met | ✅ | 6a `RetryClassifierTests`, `RetryFactsTests.IsRetryableCause_*`; V2. Planned: invariant `xcut-6.capability-queried` (a non-SDK exception declaring `IRetryableError` is retried with no classifier edit; one declaring `false` is not). |
| `XCUT-7` | MUST | met | ✅ | 6a `RetryClassifierTests`, `RetryFactsTests.The_configured_RetryPolicy_set_is_not_the_classifier`; V2. Planned: invariant `xcut-7.configured-set-authoritative` (widen 501, narrow 500, and the same set re-classifies a re-sent error status). |
| `XCUT-8` | MUST | met for every core mapping path; reading of the public constructor | ✅ (reading P10-24, proposed as a §11 item) | 4c `ErrorMappingFactoryTests.A_non_error_status_is_rejected`; `ErrorMappingStep` and both `EnsureSuccess` forms return a non-error response untouched (the convenience form). Planned: invariant `xcut-8.strict-factory-rejects` over the hook `MapStrict` (no default; the core driver supplies the internal factory) and `xcut-8.convenience-returns-response` (public). |
| `XCUT-9` | MUST | met; uncited | ✅ | `ExceptionFactsTests.EnumerateCauses_terminates_on_a_self_cycle`, `…_on_a_two_node_cycle`, `…_uses_reference_identity`, `…_stops_below_depth_64`; `AsyncToSyncBridgeTests` (`ASYNC-13`); V14. Planned: invariant `xcut-9.cycle-safe-walk` over the hook `CreateCyclicException` (walks, never throws one: the Windows hang `CyclicExceptions` documents); the `GetBaseException` ban (P10-18). |
| `XCUT-10` | MUST | met (row left ⏳ 10 by 9) | ✅ | 6a `RetryResendGateTests`; 9 `NoRetryingHandlerTests`, `ResilienceCompositionTests`; V2. Planned: invariant `xcut-10.safety-gate-uniform` (the five-way matrix of ch.19 on both faces). |
| `XCUT-11` | MUST | met | ✅ | 4c `Pipeline/ConcurrencyTests.Concurrent_calls_share_no_per_call_state`, `Every_shipped_policy_holds_only_readonly_instance_fields`; `RedactionCacheTests`; 8a kit `transport-29.concurrent-no-crosstalk`. Planned: invariant `xcut-11.shared-no-crosstalk` (64 concurrent distinct requests through the subject's shared pipeline, each response its own; both faces). |
| `XCUT-12` | SHOULD | met; the embedded MUST (scoped lock) met | ✅ | 6c `AccessTokenCacheTests.N_concurrent_requests_at_expiry_cost_one_fetch`, `The_hot_path_takes_no_lock`, `Different_keys_do_not_block_each_other`. Planned: invariant `xcut-12.single-flight-scoped` (public `AccessTokenCache`: N racers, one fetch; a fetch blocked on one key does not hold another). |
| `XCUT-13` | MUST | met; uncited | ✅ **split**: latched and non-blocking ✅; "preserves the ambient interrupt flag" vacuous (§10 entry 8, §3.7) | `ResponseDisposeLatchTests`, `ClientDisposeIdempotenceTests`, `SystemNetHttpClientDisposeTests`; 8a kit `transport-16.close-idempotent-nonblocking` (cited by reference: it cites `TRANSPORT-16` and `ASYNC-15`, not `XCUT-13`). Planned: invariant `xcut-13.dispose-latched` and `xcut-13.dispose-non-blocking` over `Response`, `TypedResponse<T>` and `ServerSentEventStream` with a release-counting `ResponseBody` (counted at the resource). |
| `XCUT-14` | MUST | met; no gate | ✅ | 4a `BoundedMapTests`, `ContextStoreTests`; 6c `AccessTokenCacheTests.The_cache_holds_at_most_1024_keys_once_quiescent`, `NonceCounterTests.The_store_is_a_BoundedMap_of_1024`; 8b `HeaderDropLogTests` (10 000 names leave 128); V13. Planned: invariant `xcut-14.bounded-under-burst` and `xcut-14.drains-after-insert` over the hook `CreateBoundedMap`; the `ConcurrentDictionary` ban (P10-18). |
| `XCUT-15` | MUST | met; uncited | ✅ | 2a `ModelImmutabilityArchitectureTests` (readonly fields, no non-`init` setter, read-only collections, sealed), `HeadersTests`; 3a `FromBytes` copies (`IO-30` row). Planned: invariant `xcut-15.no-external-alias` (mutate the inner collection handed to `Headers`, `MediaType`, `AuthenticationChallenge`, `RequestBody.FromBytes`; every `With*` returns a new instance). |
| `XCUT-16` | MUST | met | ✅ | Phase 1 S4 `AuthHttpsGuardTests` (`Security`); 6c `HttpsRequiredExceptionTests`; 6b `RedirectCredentialLeakTests` "a permitted downgrade, then a retry". Planned: invariant `xcut-16.no-credential-over-http` (no token fetched and no header written over `http://`; a credential-free cross-origin hop proceeds). |
| `XCUT-17` | MUST | met | ✅ | Phase 1 S3 `RedirectWireTests` (`Security`); 6b `RedirectCredentialHygieneTests`, `RedirectCredentialLeakTests`, `RedirectLogTests`; 9 `NamedClientRedirectWireTests` (`Security`). Planned: invariant `xcut-17.redirect-hygiene` ((a) to (d) through the subject pipeline over an in-memory transport). |
| `XCUT-18` | MUST | met | ✅ | Phase 1 S1 `HeaderInjectionValidationTests`, `HeaderInjectionWireTests` (`Security`); 2a `HttpHeaderSyntaxTests`; 6c `ApiKeyCredentialTests…grammar_refuses`, `DigestChallengeHandlerTests.An_unechoable_realm_nonce_or_opaque_is_declined`; 8b `HeaderPartitionTests`, `HeaderDropLogTests` (the wire re-check); 8a kit outbound assertions (by reference: they cite `TRANSPORT-1x`, not `XCUT-18`). Planned: invariant `xcut-18.model-validation` (the ch.19 byte table: C0, DEL, non-ASCII, HTAB by position, inbound obs-text). The dispatch-time half stays a transport assertion (P10-6). |
| `XCUT-19` | MUST | met | ✅ | (a), (b): phase 1 S5 `UrlRedactionDefaultDenyTests` (`Security`), 5c `OperationTelemetryTests` (span `url.full`); (c), (e): 5b `HeaderLogRendererTests`, `HttpLoggingDefaultsTests`, `BodyLoggingTests.At_Headers_and_None_no_wrapper_is_constructed`; (d): 6c `CredentialRedactionTests` (`Security`); 7a `StatusAwareHandlerLocationRedactionTests` (`Security`). Planned: invariant `xcut-19.redaction-default-deny` (port of Node's `security-by-default` vectors). |
| `XCUT-20` | MUST | met under §11 item 37 | ✅ (reading §11 item 37: the SDK's own recording code is total; listener callbacks are the `OBS-30` party) | 5b `EmissionGuardTests`, `UrlRedactorHeaderValueTests.The_method_is_total`; 5c (as `OBS-20`). Planned: invariant `xcut-20.observability-never-throws` (a malformed URL renders `[malformed url]`; a throwing `ILogger` at `Body` level, and the request completes). |
| `XCUT-21` | MUST | met | ✅ | 6c `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator`; design §6.3. Planned: invariant `xcut-21.cnonce-unpredictable` (public: 32 lower-hex characters, 128 bits, no repeat over 10 000 draws, through `DigestChallengeHandler.Authorize` by default or the optional `DrawCnonce` hook). The "operating system's CSPRNG" source clause is **not** asserted by an output test (a `System.Random` draw of 128 bits is indistinguishable by output): it is cited by reference to `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` (validation resolution F5). |
| `XCUT-22` | MUST | met | ✅ | 8a kit `transport-15.borrowed-survives` (both SystemNet constructions); 8b `SystemNetHttpClientOwnershipTests`; 9 `PrimaryHandlerLeaseTests`; the bridges own nothing (`HttpClientExtensions`: "SEAM-14, XCUT-22"). Planned: invariant `xcut-22.core-borrows` (an `HttpPipeline`, `AsAsync` and `AsBlocking` over a counting transport: disposing them disposes nothing, the transport still works). |
| `XCUT-23` | MUST | DI half met; DI-less vacuous | ✅ **split**: DI half ✅; the discovery half vacuous (§10 entry 9, §11 item 8: no discovery, a seam is a non-nullable parameter) | 9 `SingleRegistrationTests`, `ProviderValidationTests`. Planned: invariant `xcut-23.resolution-deterministic` over the hook `SeamResolution` (no default; the DI driver supplies it). |
| `XCUT-24` | SHOULD | MUST half met; one SHOULD clause unmet | ✅ **split**: byte-capped (the embedded MUST) ✅ for previews and error snapshots; non-consuming ✅ for body-log previews; **🚫 declined for the error-body snapshot** above `Response.MaxBufferedErrorBytes` (1 MiB), which is drained, truncated without a marker and replaces the original (P10-23) | 5b `BodyPreviewTests` (10 MiB body, 1 KiB cap, under 2 MiB allocated, full read intact); 3b `ErrorBodyPreviewTests` (`BODY-33`). Planned: invariant `xcut-24.preview-capped-non-consuming`. |

### `NFR` rows (17: 4 MUST, 13 SHOULD)

| ID | Level | Measured at `af603d2` | Disposition | Evidence (existing, then planned) |
|---|---|---|---|---|
| `NFR-1` | MUST | met | ✅ **split**: zero concrete dependencies ✅; "compile-time-only" facade per §10 entry 1 (sanctioned in part by `SEAM-1`) | `scripts/ci/dependency-audit.cs` (CI "Dependency audit"); `Seam1ArchitectureTests`; V1, V6. Planned: packaging `nfr-1.core-dependencies` over the packed nuspec. |
| `NFR-2` | SHOULD | met | ✅ (§10 entry 2: the hosting family counts as one root) | The audit's `Policy(ThirdParty, Projects)` record (P9-3). Planned: packaging `nfr-2.adapter-budget`. |
| `NFR-3` | SHOULD | met | ✅ | `GenerateDocumentationFile` + `CS1591` as an error; `PublicAPI` files on all five libraries; `InternalsVisibleTo` granted only to each library's own `*.Tests` (measured). Planned: packaging `nfr-3.no-internals-granted` (every `InternalsVisibleTo` in a packed assembly names `<id>.Tests`). |
| `NFR-4` | SHOULD | source half met | ✅ **split**: the snapshot and its drift gate ✅ (`RS0016`/`RS0017`, V4); the binary half (`PackageValidationBaselineVersion`) ⏳ `docs/first-release.md` *After the first publish* (phase 12) | `Directory.Build.props` "Library gates"; V4. Planned: `NFR4ArchitectureTests` (every `src/` library has both files; `Shipped` is empty or `#nullable enable` only until phase 12). |
| `NFR-5` | SHOULD | met | ✅ | `scripts/ci/coverage-gate.cs` at 80% (CI "Coverage floor"), measured 97.60% (V1); `coverage-gate-selftest.sh` (the negative control). |
| `NFR-6` | SHOULD | met | ✅ | `TreatWarningsAsErrors` in `Directory.Build.props` for every project; V4 (`CS0618` is an error). |
| `NFR-7` | SHOULD | partial: no re-enable conditions | ✅ once P10-13 lands | `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `dotnet format --verify-no-changes`; V12. Planned: each `none` in `.editorconfig` gains a `Re-enable when:` line; `AnalyzerWaiverTests` (every `none` has a rationale and a re-enable condition; every `#pragma warning disable` under `src/` has a why-comment). |
| `NFR-8` | MUST | met | ✅ (applies literally on .NET, design §9.2: the keep configuration is `IsTrimmable`/`IsAotCompatible` plus annotations carried in the assembly) | `Directory.Build.props` "Library gates"; the trim, single-file and AOT analyzers as errors; the AOT smoke (V16). Planned: packaging `nfr-8.trim-and-aot-declared` (the packed assembly's `AssemblyMetadata`, V6). |
| `NFR-9` | SHOULD | partial: rule presence unasserted | ✅ once P10-17 lands | The `aot-smoke` CI job (V16). Planned: `nfr-8.trim-and-aot-declared` reads as `NFR-9`'s "every shipped rule file is present" (§9.2's own reading); the smoke gains `CheckPhase10Async` (the invariant and codec suites under NativeAOT, P10-17). |
| `NFR-10` | MUST | met | ✅ (the "higher-floor unit isolated" clause vacuous: none exists) | `TargetFramework` `net10.0` once in `Directory.Build.props` (D1); the audit requires exactly one `net10.0` dependency group; CI runs on the pinned SDK's runtime on three OSes. Planned: packaging `nfr-10.single-floor` (one `lib/net10.0`, one group, `TargetFrameworkAttribute` `.NETCoreApp,Version=v10.0`). |
| `NFR-11` | SHOULD | partial: no scan | ✅ once P10-11 lands | §11 item 2; V10. Planned: `Nfr11PublicSurfaceArchitectureTests` (reflection over core's exported members); invariant `nfr-11.scheduler-agnostic` (the same pipeline call from the thread pool, dedicated threads, an exclusive `ConcurrentExclusiveSchedulerPair` scheduler and a single-threaded `SynchronizationContext`, both faces). |
| `NFR-12` | SHOULD | build half met | ✅ **split**: build half ✅ (strengthened to cross-path, P10-14); release half ⏳ `docs/first-release.md` *After the first publish* (phase 12) | CI `reproducible` job, `scripts/ci/reproducible-pack.sh`; V3. |
| `NFR-13` | SHOULD | met | ✅ | `file_header_template` + `IDE0073` as an error; V11. Planned: `LicenseHeaderTests` over every `.cs` under the repository, `scripts/ci` included. |
| `NFR-14` | SHOULD | met | ✅ | `Directory.Packages.props` (central versions, transitive pinning), `VersionPrefix`/`VersionSuffix` once, `global.json`, `DexpaceToolchainPackVersion`; no `Version=` outside the catalogue (measured). Planned: packaging `nfr-14.lockstep-version` (every package and every inter-package dependency at one version). |
| `NFR-15` | SHOULD | partial: packaged version unasserted | ✅ once P10-15 lands | `BuildInfo.SdkVersion` over `AssemblyInformationalVersion` (`BuildInfoTests`). Planned: packaging `nfr-15.version-resolvable` (informational version, `+` metadata stripped, equals the nuspec version and is never `unknown`/`0.0.0`); the smoke asserts `BuildInfo.SdkVersion` equals its own informational version. |
| `NFR-16` | SHOULD | no subject (no release path) | ⏳ `docs/first-release.md` *Blockers before first publish*, "Signing decided" (phase 12) | Design §9.2 (repository signature; author signing on the release job only). |
| `NFR-17` | MUST | partial: jobs block, merges do not (V9) | ✅ once P10-16's two halves land; **⏳ `docs/first-release.md` blocker if the ruleset change is not made by close-out** | CI has no `continue-on-error`. Planned: `CiWorkflowTests` (every gate of the gate table is a step of `ci.yml`; no step or job is advisory); the `main` ruleset gains `required_status_checks` naming every job (an admin action, P10-16). |

**Tally of the 41 rows (planned): 39 ✅, of which 10 split (`XCUT-1`, `-4`, `-13`, `-23`, `-24`, `NFR-1`, `-4`, `-12`, plus `XCUT-8`
and `XCUT-20` on readings); 1 ⏳ (`NFR-16`, phase 12); 1 ✅ conditional (`NFR-17`, falling to ⏳ phase 12 if the ruleset is not
changed); 0 🚫 rows; 0 N/A rows.** By level: `XCUT` 22 MUST + 2 SHOULD, `NFR` 4 MUST + 13 SHOULD.

**The unmet-MUST count is unchanged by this phase.** No row adds a clause to the seven already over the parity target of five
(the 8b status note). One count question is raised for phase 11, not decided here: `XCUT-4`'s "catchable as a generic I/O error"
is unmet in letter on .NET, and §10 entry 7 classes it as a judged mechanism change (outside the count), as entry 25 does for
`CFG-2`'s fall-through; the auditor decides whether either belongs in the count (P10-25). `XCUT-24`'s declined clause is a SHOULD
clause and goes to *SHOULD declined*, not the MUST count.

### Hand-offs addressed to 10

| From | Hand-off | Disposition |
|---|---|---|
| 4c (checklist) | `XCUT-8`: the guarded factory, `ErrorMappingFactoryTests` | **Taken**: row above; the public constructor's reading is P10-24. |
| 4a (checklist) | `XCUT-14` "(phase 10) not a 4a row" | **Taken**: row above. |
| 5b (checklist) | `XCUT-19`(c), (e), `XCUT-20`, `XCUT-24` | **Taken**; `XCUT-24`'s error-snapshot clause is P10-23. |
| 5c (checklist) | `XCUT-19`, `XCUT-20` (§11 item 37) | **Taken**. |
| 6a (status note, checklist) | "audits `XCUT-1`–`XCUT-10` against the 6a evidence" | **Taken**: each row cites the 6a test it rests on; V2 re-measures the public behaviour. |
| 6b | "audits `XCUT-17` against the 6b evidence" | **Taken**. |
| 6c | `XCUT-12`, `-14`, `-16`, `-18`, `-19`(d), `-21` cite 6c's tests | **Taken**. |
| 7a | "the AOT smoke keeps `CheckPhase7aSerdeAsync`"; the kit lifts `SERDE-3`, `-9`, `-10`, `-12`, `-15`–`-17`, `-19`, `-20` (not `-4`, `-13`, `-18`, F6) | **Taken in part**: kept; the lift is the codec suite (D), 11 assertions; `SERDE-4`, `-13`, `-14`, `-18`, `-30` judge core code and stay by reference (F6). |
| 7b (checklist) | `XCUT-1` evidence | **Taken**. |
| 7c (design, via 8a P8a-17) | `link-header.json` and the splice properties "candidates for the shared kit" | **Declined** (P10-9): one implementation; B.1 rows are `by reference`. |
| 8a (status note, P8a-14, P8a-17) | the invariant, packaging and codec suites in the kit; `APPENDIX_B.md` over 61 items; `RequirementCatalog` reused for the row-count gate; `AppendixBMap` widened | **Taken** (B–E, I, J). The coverage script reads appendix C directly (it is a repository gate, not a kit feature), and the kit's catalogue test keeps the two in step. |
| 8b (status note, checklist) | audit `XCUT-2` (the classification table), `XCUT-18` (wire re-check, drop logs), `XCUT-22` | **Taken**: rows above. |
| 9 | `XCUT-10` cites `NoRetryingHandlerTests`, `ResilienceCompositionTests`; `XCUT-22` cites `PrimaryHandlerLeaseTests`; `XCUT-23` cites `SingleRegistrationTests`; `NFR-2` cites the policy record; `NFR-8`/`NFR-9` keep `CheckPhase9Async`; "the packaging suite's allowed-dependency check reads the same policy record" | **Taken**; the packaging suite's policies mirror the audit's record (P10-10), and a test fails if they disagree. |
| Phase 0 (card, roadmap constraint 1) | "phase 10 completes" the AOT smoke; `NFR` gates stood up as machinery, dispositioned in 10; CI's header comment "NFR-11's public-signature type scan: phase 10" | **Taken** (F, H and the gate table). |
| 8a status note (F-A2, `ASYNC-2`) | "rule it into a later phase that may touch core" | **Not taken**: not a phase 10 row; P10-2 keeps core code out of this phase. It stays with the lead and phase 11's inbound list. |

---

## Argued positions

### A. Measure, route, and the production-code boundary (P10-2)

The roadmap gives repair to phase 11 ("the audit leads, and the phase ships code wherever the audit finds a defect"). Phase 10
asks whether the build meets the specification; phase 11 asks whether the design's claims are true. Following Ruby's `R6`, which
this roadmap's card ordering copies, **phase 10 measures and reports; it changes no production behaviour**:

- **In scope to change:** `src/Dexpace.Sdk.Conformance` (the three suites; the card gives the package these suites), every test
  project, `tests/Dexpace.Sdk.AotSmoke`, `scripts/ci/*`, `.github/workflows/ci.yml`, `.editorconfig` comments, and gate wiring
  that changes no behaviour: two `BannedSymbols.txt` bans (eight lines) and the one scoped `#pragma` the constructor ban needs in `BoundedMap.cs` (P10-18).
- **Out of scope:** any behavioural change under `src/Dexpace.Sdk.{Core,Http.SystemNet,Serialization.SystemTextJson,Extensions.DependencyInjection}`.
  A defect found there is routed: a bullet on phase 11's inbound list in the dated status note at close, and, for a MUST, a
  `docs/first-release.md` line. None was found that breaks a MUST (the three findings are a dead type, a reading and a SHOULD
  clause).
- **The one exception** is the kit itself: a kit defect that stops a suite running is phase 10's to fix (Ruby's `R6` exception).

Options: **A** as ruled; **B** allow test-first repairs of at most a few lines in core when they close an owned row (would let
P10-25's dead type be deleted here). **Recommended default: A** — "small" is the judgement that erodes the boundary, and the one
candidate (deleting `ServiceResponseException`) is a public-surface choice that phase 12 decides anyway. Open for the lead.

### B. Three suites in the kit: core only, BCL only (P10-3, P10-7)

**Where.** `Dexpace.Sdk.Conformance`, beside `TransportSuite` (the card, and 8a's hand-off). The kit's dependency rule is
unchanged: it references core and nothing else, and `KitDependencyArchitectureTests` keeps it so. The packaging suite reads
`.nupkg` files with `System.IO.Compression`, nuspecs with `System.Xml.Linq` and assemblies with `System.Reflection.Metadata`, all
in the `Microsoft.NETCoreApp` reference pack (V6), so no package is added and `scripts/ci/dependency-audit.cs` needs no change
(P10-3).

**One result model** (P10-7, open for the lead: it changes the kit's unshipped public surface). The kit's `ConformanceAssertion`,
`ConformanceResult`, `ConformanceStatus`, `ConformanceWaiver` and `ConformanceReport` serve all four suites:

- **Faces.** The invariant and codec suites have the same two faces as the transport suite: the asynchronous and the blocking
  call surface (`HttpPipeline.SendAsync`/`Send`; `ISerde.DeserializeAsync`/`Deserialize`). The packaging suite, and the few
  invariant assertions that judge a value rather than a call (for example `xcut-5`, `xcut-9`, `xcut-14`, `xcut-15`), drive no call surface:
  `TransportFace` gains **`None = 2`** (additive) for them. Options: add `None` (recommended: additive, keeps
  every existing signature); rename the enum to `ConformanceFace` (clearer, a breaking rename of unshipped surface, allowed by
  constraint 8 with a `CHANGELOG` line); make `ConformanceResult.Face` nullable (breaks deconstruction).
- **The runner.** `SuiteRunner`'s status, waiver-must-stay-needed and timeout logic is extracted to an internal runner generic
  over the suite's context, so the four suites share one implementation of the six statuses. `ConformanceAssertion`'s public
  shape (name, requirement IDs, level, faces) is unchanged; its body delegate stays internal.
- **Options.** A new `SuiteOptions` sealed record (`AssertionTimeout`, `Waivers`) for the three new suites; `TransportSuiteOptions`
  (which adds `ReleaseTimeout`) is unchanged.
- **Reports.** Each suite has its own preamble saying what a green run does not prove (the invariant suite's: "an assertion whose
  hook the driver did not supply is `NotExercised`, never passed; core's internals are reached only through the hooks"; the
  packaging suite's: "it reads the packages it was given; a package built from another tree is a different subject"). The static
  `ConformanceReport.Preamble` stays the transport text; a report records which suite produced it. `ConformanceReport.Combine`
  merges reports into one by-requirement and by-appendix-B view (worst status wins, 8a's rule), which the appendix-B gate and the
  coverage of B.6–B.9 read.
- **`AppendixBMap`** widens from 11 items to all 61 (P8a-14), and its existing drift test widens with it.

### C. The invariant suite (P10-4, P10-5, P10-6, P10-28)

**The subject is a set of hooks, each defaulting to core** (P10-4). The kit's discipline (8a) is that every assertion has a
negative control: a deliberately broken subject it must fail. An assertion that only ever exercises core cannot have one. So every
invariant assertion reads the component it judges through `InvariantSubject`, whose members default to core's public components,
and a negative control replaces one member with a broken one (a pipeline whose retry re-sends a body-less `POST`, a redactor that
allow-lists everything, a model factory that aliases its input). This is Ruby's shape (`run(core:, seam:, bounded_map:, …)`) in
.NET clothes, and it is what makes the suite portable: a third party who composes their own pipeline policies, credentials or
codec runs the same assertions over their composition.

Four hooks have **no default**, because the thing they reach is internal or lives in another package (V2): `MapStrict`
(`XCUT-8`'s factory), `CreateCyclicException` (`XCUT-9`), `CreateBoundedMap` (`XCUT-14`, a `BoundedMapProbe` of insert, count and
capacity) and `SeamResolution` (`XCUT-23`). An assertion whose hook is absent is `NotExercised` with a reason naming the hook,
never `Passed`. `DrawCnonce` is an **optional** hook, not a no-default one: null means "draw through the public
`DigestChallengeHandler.Authorize`", so `xcut-21` is always exercised; it asserts the draw's format (32 lower-hex characters, 128
bits) and that it does not repeat, and the *source* clause (the operating system's CSPRNG) is cited by reference to
`DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator`, because no output test can tell a CSPRNG from `System.Random`
and the production factory is a private field a driver cannot reach (validation resolution F5; this corrects P10-4 and the
catalogue row, which disagreed on whether `DrawCnonce` has a default).

**The first-party drivers** (P10-5, open for the lead):

- **Core driver** in `tests/Dexpace.Sdk.Core.Tests/Conformance/InvariantConformanceTests.cs` (`Conformance`): supplies the three
  internal hooks of the core side (`ErrorMapping.ToException`, `TestSupport`'s `CyclicExceptions`, a `BoundedMap`-backed probe;
  the Digest cnonce is not a hook the driver supplies, F5). This needs `Dexpace.Sdk.Core.Tests` to reference `Dexpace.Sdk.Conformance`, which today's
  `Seam2ArchitectureTests.Core_suite_loads_only_core_and_the_in_memory_fakes` forbids by its exact allow-list. The allow-list gains
  `Dexpace.Sdk.Conformance`, argued: `SEAM-2`'s conformance clause is that core's suite runs "without any adapter", and the kit
  references core alone (`KitDependencyArchitectureTests`), so it brings no transport or codec; the build-graph test keeps failing
  on any `Dexpace.Sdk.Http.*` or `Dexpace.Sdk.Serialization.*` arrival. Options: **A** as ruled; **B** keep the allow-list and run
  the invariant suite only from `Dexpace.Sdk.Conformance.Tests` over public API, leaving those hooks `NotExercised` and citing
  core's unit tests `by reference` for them (weaker: five MUST-level `NotExercised`). **Recommended default: A.**
- **DI driver** in `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Conformance/` supplies `SeamResolution` over
  `AddDexpaceClient` (none fails with the install hint, two fail listing both, an explicit `UseTransport` wins) and runs the
  suite with the default pipeline replaced by the DI-built one.
- **The AOT smoke** runs the suite with core's defaults (H).

**No duplication with the transport suite** (P10-6). The transport-level clauses of `XCUT-1`, `-2`, `-4` and `-22`
are already assertions of `TransportSuite` that cite those IDs (`transport-3`, `-4`, `-20`, `-15`; measured in the kit's
catalogue: no other `XCUT` ID is cited there). The invariant suite asserts the core-level clause of each (the pipeline, the models,
the bridges) and `ConformanceReport.Combine` merges the two by requirement. For `XCUT-13` and `XCUT-18` the transport assertions
(`transport-16`, which cites `TRANSPORT-16` and `ASYNC-15`; the outbound header assertions, which cite `TRANSPORT-1x`) are evidence
**by reference only**: this phase adds no ID to the transport catalogue, and `Combine` merges nothing for those two rows.
Re-implementing a transport assertion inside the invariant suite would give one clause two owners.

**Expected `NotExercised` is pinned** (P10-28). Each first-party driver asserts the exact list of assertions it expects to be
`NotExercised` (the transport drivers already pin their waivers). An unexpected MUST-level `NotExercised` fails the driver; this
is Ruby's "a MUST-level vacuity blocks the report", applied to the hook gap, so "not supplied" never reads as "conforming".

**The catalogue (28 assertions, each with a negative control under `tests/Dexpace.Sdk.Conformance.Tests/Controls/`).**
Faces: "both" means async and blocking.

| Assertion | IDs | Level | Faces | Subject member | Negative control |
|---|---|---|---|---|---|
| `xcut-1.cancel-is-terminal` | XCUT-1 | MUST | both | `CreatePipeline` | a policy that retries an `OperationCanceledException` |
| `xcut-2.timeout-is-retryable` | XCUT-2 | MUST | both | `CreatePipeline` | a policy that maps a deadline to `OperationCanceledException` |
| `xcut-3.wait-promptly-cancellable` | XCUT-3 | MUST | both | `CreatePipeline`, a recording `TimeProvider` | a wait that ignores the token; a wait that leaks its timer |
| `xcut-4.two-branches` | XCUT-4 | MUST | both | `CreatePipeline` | a mapping that throws a non-retryable exception for a refused connection |
| `xcut-5.one-classifier` | XCUT-5 | MUST | none | `CreateResponseException` (default: the public constructor) | a factory baking `false` for 507 |
| `xcut-6.capability-queried` | XCUT-6 | MUST | both | `CreatePipeline` | a retry policy that matches concrete types |
| `xcut-7.configured-set-authoritative` | XCUT-7 | MUST | both | `CreatePipeline` | a retry policy reading the baked flag |
| `xcut-8.strict-factory-rejects` | XCUT-8 | MUST | none | `MapStrict` (no default) | a factory that maps a 200 |
| `xcut-8.convenience-returns-response` | XCUT-8 | MUST | both | `CreatePipeline` (error mapping on) | a step that throws for a 302 |
| `xcut-9.cycle-safe-walk` | XCUT-9 | MUST | none | `CreateCyclicException` (no default), `EnumerateCauses` (default: `ExceptionFacts`) | a walk tracking by `Equals` over an exception type that overrides it |
| `xcut-10.safety-gate-uniform` | XCUT-10 | MUST | both | `CreatePipeline` | a gate that special-cases a transport failure |
| `xcut-11.shared-no-crosstalk` | XCUT-11 | MUST | both | `CreatePipeline` (one shared instance) | a policy holding the attempt counter in a field |
| `xcut-12.single-flight-scoped` | XCUT-12 | SHOULD | both | `CreateTokenCache` (default: `AccessTokenCache`) | a cache with one global lock; a cache that fetches per caller |
| `xcut-13.dispose-latched` | XCUT-13 | MUST | both | the core disposables over a counting body | a body whose release runs twice |
| `xcut-13.dispose-non-blocking` | XCUT-13 | MUST | both | as above, with a parked read | a dispose that waits for the read |
| `xcut-14.bounded-under-burst` | XCUT-14 | MUST | none | `CreateBoundedMap` (no default) | an unbounded probe |
| `xcut-14.drains-after-insert` | XCUT-14 | MUST | none | as above | a single pre-insert check-then-evict probe |
| `xcut-15.no-external-alias` | XCUT-15 | MUST | none | `Models` (default: the public factories) | a factory that keeps the caller's list |
| `xcut-16.no-credential-over-http` | XCUT-16 | MUST | both | `CreateAuthPolicy` (default: `BasicAuthPolicy`, `BearerTokenAuthPolicy`) | a policy that fetches before the guard |
| `xcut-17.redirect-hygiene` | XCUT-17 | MUST | both | `CreatePipeline` (redirect and auth) | a redirect policy judging origin against the previous hop |
| `xcut-18.model-validation` | XCUT-18 | MUST | none | `Models` | a header model accepting HTAB in a name |
| `xcut-19.redaction-default-deny` | XCUT-19 | MUST | both | `CreateRedactor`, `Credentials`, `CreatePipeline` (logging) | an allow-all redactor; a credential whose `ToString` shows the secret |
| `xcut-20.observability-never-throws` | XCUT-20 | MUST | both | `CreateRedactor`, `CreatePipeline` with a throwing `ILogger` | a renderer that throws on a malformed URL |
| `xcut-21.cnonce-unpredictable` | XCUT-21 | MUST | none | `DrawCnonce` (optional; default: through `DigestChallengeHandler.Authorize`) | a draw of 64 bits (fails the width); a draw cycling through a short fixed sequence (fails no-repeat) |
| `xcut-22.core-borrows` | XCUT-22 | MUST | both | the pipeline and the two bridges over a counting transport | a bridge that disposes what it wraps |
| `xcut-23.resolution-deterministic` | XCUT-23 | MUST | none | `SeamResolution` (no default) | a resolver that picks the last of two |
| `xcut-24.preview-capped-non-consuming` | XCUT-24 | SHOULD | both | `CreatePipeline` (logging at `Body`) | a preview that buffers the whole body |
| `nfr-11.scheduler-agnostic` | NFR-11 | SHOULD | both | `CreatePipeline` | a policy that blocks on a `SynchronizationContext` |

Ch.19's five-way `XCUT-10` matrix is one assertion with five cases, so a broken gate fails it on the case it breaks. The count is
27 assertions over the 24 `XCUT` IDs (`XCUT-8`, `-13` and `-14` carry two each) plus `nfr-11.scheduler-agnostic`, 28 in all.
"none" in the Faces column is `TransportFace.None`: the assertion drives no call surface. Case tables port from
`nodejs-sdk/tests/conformance/xcut/*.conformance.test.ts` and `ruby-sdk/.../invariant_suite/*.rb`, each file citing its source in
its header (roadmap constraint 10). The kit never calls `Task.Delay` (8a's rule): time-bound assertions use a recording
`TimeProvider` the kit implements over `TimeProvider.System`.

### D. The codec suite (P10-8, P10-9)

**What it lifts** (P10-8). 7a's hand-off list, plus the two seam rows Ruby's codec suite carries, as assertions over an `ISerde`
subject on both faces: `SERDE-1` (round trip), `SERDE-2` (the declared media type is the default `Content-Type`), `SERDE-3` with
`SEAM-20` (a target stream is never closed), `SERDE-9` and `SERDE-10` (failures surface `SerdeException` subtypes chaining the
cause; no library type escapes), `SERDE-12` (a genuine stream I/O error propagates unwrapped), `SERDE-15`–`SERDE-17`, `-19`, `-20`
(`Tristate<T>`: absent omits, null emits null, present emits, the `{}`/`{"x":null}`/`{"x":v}` round trip, missing key is absent, the
codec's wiring, the top-level degrade). **11 assertions**, each with a negative control (a codec that disposes its target, one that
leaks `JsonException`, one that writes `null` for an absent field).

**Not lifted, by reference (2026-10-13 validation, F6).** `SERDE-4`, `SERDE-13`, `SERDE-18` (with `SERDE-14` and `SERDE-30`, which
were already cited only as clauses of `SERDE-18`) judge **core code, not the codec seam**, so a negative control cannot be built by
breaking a codec through `CodecSubject.CreateSerde`: `SERDE-4` is core's `SerdeExtensions.Serialize(ISerde, Span<byte>, T)`
(`ISerde.Serialize` returns `void` over an `IBufferWriter<byte>` and has no offset or length to get wrong); `SERDE-13`'s wire-`null`
rejection naming the type lives in core's `SerdeValues` and the typed readers, after the codec has returned; `SERDE-18`'s
`Tristate<T>.Present` rejecting `null` has no bypass to break. Redefining each over a codec-observable property would restate
`SERDE-1`/`-9`/`-15`/`-16`, so the choice is **by reference** for all five, in B.3: `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeProfileTests.cs`
(`SERDE-4`), `ReadValueTests.cs` and `SerdeValuesTests.cs` (`SERDE-13`), `TristateTests.cs` and `TristateSentinelTests.cs`
(`SERDE-14`, `-18`, `-30`).

**The subject** is `CodecSubject { Name, CreateSerde, ExpectedMediaType }`. A codec that needs compile-time metadata (System.Text.Json
with a source-generated context, design §7.3) must be able to describe the suite's models, so the kit publishes a small set of
**probe models** (sealed records with `Tristate<T>` members, a nested record, a list) in
`Dexpace.Sdk.Conformance.Codec`, and the driver registers them in its own context. Options: **public probe models** (recommended:
one context line per type for a driver; reflection-based codecs need nothing); a generic `CodecSubject<TContext>` (couples the
kit to one codec's metadata model). Open for the lead (kit surface).

**The first driver** is `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Conformance/CodecConformanceTests.cs`
(`Conformance`) over `new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ProbeContext.Default))`.

**7c's vectors are not lifted** (P10-9). `link-header.json` and the splice properties test the pagination engine, which has one
implementation and is not a seam (`PAGE-8` makes it stateless and shared, not pluggable). A suite assertion over a single subject
is a test with one subject living in a package whose purpose is many (Ruby `P9-1`'s argument), and it would need the kit to reach
internal parsers. B.1 rows are `by reference` to `LinkHeaderParserTests` and `QuerySplicePropertyTests`. Options: decline
(recommended); lift as public helpers on the kit (new surface with no second implementation to judge). Open for the lead.

### E. The packaging suite, and where it runs (P10-10)

**Assertions** (each over every package handed in; `TransportFace.None`; a negative control is a synthetic `.nupkg` built in
memory by `tests/Dexpace.Sdk.Conformance.Tests`):

| Assertion | IDs | Level | Checks |
|---|---|---|---|
| `nfr-1.core-dependencies` | NFR-1 | MUST | the core package's nuspec: one `net10.0` group, exactly the facade (§10 entry 1) |
| `nfr-2.adapter-budget` | NFR-2 | SHOULD | each other package: core, the first-party packages its policy names, and the closure of at most one third-party root, every Dexpace dependency at the lockstep version |
| `nfr-3.no-internals-granted` | NFR-3 | SHOULD | every `InternalsVisibleToAttribute` in a packed assembly names `<package id>.Tests` |
| `nfr-8.trim-and-aot-declared` | NFR-8, NFR-9 | MUST | `AssemblyMetadata("IsTrimmable","True")` and `("IsAotCompatible","True")` on every packed library (V6) |
| `nfr-10.single-floor` | NFR-10 | MUST | one `lib/<tfm>/` folder, one dependency group, and the assembly's `TargetFrameworkAttribute`, all the subject's declared floor (`net10.0`) |
| `nfr-14.lockstep-version` | NFR-14 | SHOULD | every package's version, and every inter-package dependency's, equal |
| `nfr-15.version-resolvable` | NFR-15 | SHOULD | `AssemblyInformationalVersion` without `+` metadata equals the nuspec version; never `unknown` or `0.0.0` |
| `nfr-13.license-declared` | NFR-13 | SHOULD | the nuspec's license expression and copyright are present and equal across packages (the package-level face of the header rule; source headers are the repository gate, F) |

**Subject.** `PackagingSubject { PackagePaths, CoreId, Policies, DeclaredFloor }`, with `PackagePolicy(ThirdPartyRoot, FirstPartyIds)`
mirroring `scripts/ci/dependency-audit.cs`'s `Policy(ThirdParty, Projects)` record. A unit test reads both tables and fails if they
disagree, so the two checks cannot drift (9's hand-off).

**Where it runs.** CI's build job gains a step after `Pack`: `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages`,
a file-based app that references the kit with `#:project` (V6), runs `PackagingSuite.RunAll`, prints the report and exits non-zero
on a red one. It runs on every OS row, like the audit. The dependency audit stays: it checks the `deps.json` closure of the built
output, the packaging suite the published metadata, which is the subject `NFR-1`'s own conformance sentence names; two checks,
two subjects (Ruby `P9-2`). Options: the CI step (recommended); a test that packs in a fixture (slow, and couples the test run to
`dotnet pack`); folding the checks into the audit script (loses the portable form a third party can run against their own
package).

### F. The NFR gates, one by one (P10-11 to P10-16)

- **`NFR-11`** (P10-11). `tests/Dexpace.Sdk.Core.Tests/Architecture/Nfr11PublicSurfaceArchitectureTests.cs`: reflection over
  `typeof(Request).Assembly.GetExportedTypes()`, every public and protected member's signature types (parameters, returns,
  generic arguments, base types and interfaces, recursively) must sit under `System`, `Microsoft.Extensions.Logging` or
  `Dexpace.Sdk.Core`, **with an explicit ban list beside the allow-list**: `System.Net.Http`, `System.Text.Json` and
  `Microsoft.Extensions.DependencyInjection` are refused even though the first two sit under `System` (an allow-list under `System`
  alone would admit `System.Net.Http.HttpRequestMessage`, the scan's own negative control; validation resolution F12). Design §9.2 says "a test scanning `PublicAPI.Shipped.txt`"; `Shipped` is empty until phase 12 and the text
  format is not a type model, so the scan reads the assembly (the same surface the files describe). A negative control is a
  test-local type list the scan must flag (`HttpRequestMessage` in a signature). The behavioural half is the invariant
  `nfr-11.scheduler-agnostic` (C).
- **`NFR-13`** (P10-12). `LicenseHeaderTests` (in `tests/Dexpace.Sdk.Conformance.Tests/Gates/`, located by walking up to
  `Dexpace.Sdk.sln`): every `*.cs` under `src/`, `tests/`, `tools/`, `scripts/` and `.claude/` outside `bin`/`obj` starts with the
  two-line header, an `<auto-generated>` block allowed before it. It closes the one blind spot `IDE0073` has: the file-based apps
  under `scripts/ci/`, which no project compiles (V11). Project and build files are not "source files" for this reading.
- **`NFR-7`** (P10-13). Every `dotnet_diagnostic.*.severity = none` line in `.editorconfig` gains, beside its rationale, a
  `# Re-enable when: …` line (`CA1308`: never for HTTP tokens, so "when a non-protocol string is lower-cased"; `CA1054`–`CA1056`:
  "when the string-URL entry points are removed"; `CA1062`: "if nullable annotations are turned off for any library"; the test-scope
  `CA2007` and `CA1707`: "never for `src/`"). **Consecutive settings are one block** sharing the one comment above them
  (`CA1054`–`CA1056` today; the test-scope `CA2000` pair at lines 155 and 156, whose `excluded_type_names_with_derived_types` line has
  no comment of its own, shares the block above it and gains one `Re-enable when: never for src/`). The measured
  `dotnet_code_quality.*` lines are three (V12): lines 155 and 156 are test-scope relaxations and are gated as above; **line 52**,
  `CA2000.dispose_ownership_transfer_at_constructor = true` in library scope, is **exempted by name as a hand-off rule, not a
  relaxation** (it makes the analyzer follow a constructor taking ownership, which styleguide 13.1 requires; it silences no
  diagnostic), and the test pins that allow-list to exactly that key, so any other undocumented `dotnet_code_quality.*` line fails.
  `AnalyzerWaiverTests` (`Gates/`) parses `.editorconfig` and every `#pragma warning disable` under `src/` and fails on a `none` or a
  relaxing option without both lines or a pragma without a why-comment on its line or the line above (V12).
- **`NFR-12`** (P10-14). `scripts/ci/reproducible-pack.sh` packs from **two clones at different paths** (`git worktree add` of
  `HEAD` into two directories under the output, removed afterwards) rather than twice in place, so a path leaking into an artefact
  fails the job (V3). The release half stays phase 12's. The proof that the structure can see a leak applies the same
  `-p:DeterministicSourcePaths=false` to **both** packs: from one directory both packs stay identical (the baseline passes), from two
  worktrees they differ (the cross-path run fails); applied to only one pack it would differ even in one directory and prove nothing
  (verified; validation resolution F8).
- **`NFR-15`** (P10-15). The packaging assertion (E), and the AOT smoke asserts `BuildInfo.SdkVersion` equals its own assembly's
  informational version without `+` metadata (the same `Directory.Build.props` version), which is the "packaged artefact at
  runtime" check closest to a consumer.
- **`NFR-17`** (P10-16, open for the lead). Two halves. (1) `CiWorkflowTests` (`Gates/`): `ci.yml` contains a step for every gate
  of the gate table below, no step or job sets `continue-on-error`, and no gate step is conditional except the documented Linux-only
  ones (Ruby's `ci_workflow_test.rb` idea, named in the Phase 0 card and never built). (2) **The `main` ruleset gains a
  `required_status_checks` rule** naming every job (`build & test` on three OSes, `reproducible packages`, `NativeAOT smoke`).
  This is a repository setting only an administrator can change (V9); the plan's task asks the lead, and the checklist records the
  ruleset as read with `gh api` after the change. If it is not changed by close-out, `NFR-17` is ⏳ with a `docs/first-release.md`
  blocker line.

### G. The two `XCUT` gates (P10-18)

Both are `BannedSymbols.txt` entries under `RS0030`, the house mechanism for "this must not appear in `src/`" (design §9.1):

- **`M:System.Exception.GetBaseException`**, banned in `src/`: it walks the inner-exception chain to its end with no cycle check
  (the walk behind the Windows test-host hang `CyclicExceptions` documents). Every cause walk goes through
  `ExceptionFacts.EnumerateCauses` (`XCUT-9`, V14). No current use, so no pragma.
- **`ConcurrentDictionary`'s seven public constructors**, banned in `src/` (`M:System.Collections.Concurrent.ConcurrentDictionary`2.#ctor(…)`,
  one line per overload) with **one** scoped `#pragma warning disable RS0030` / `restore` and a why-comment around the single construction
  site, `BoundedMap`'s `new ConcurrentDictionary<TKey, Slot>(comparer)` (`XCUT-14`, V13): a process- or instance-lived concurrent map
  is a `BoundedMap` or it does not land. Plain `Dictionary`/`HashSet` fields stay allowed (they are per instance, per call or static
  tables), and `HeaderDropLog`'s capped set is recorded in the checklist as the one lock-guarded bounded set.

  **Why constructors and not the type (validation resolution F1).** The first draft banned
  `T:System.Collections.Concurrent.ConcurrentDictionary`2` with "one sanctioned pragma". Measured, the type ban flags **12 lines** of
  `BoundedMap.cs` (the `cref` at 18, and 49, 61, 81, 88, 103, 110 twice, 123, 139, 154, 171), not one, and the file's existing
  `#pragma warning disable RS0030` / `restore` pair around the value-comparing `TryRemove` (lines 178 to 180) would **re-enable** the
  rule for the rest of any file-wide region put around them. A type-wide ban therefore needs a file-scoped disable *and* the removal
  or merge of that inner pair, which also widens the `TryRemove` exemption to the whole file. Banning the constructors keeps the
  mechanism ("a map cannot be built outside `BoundedMap`") with exactly one site (line 49), leaves the inner pair as it is, and was
  verified on a scratch copy of the tree: the entry
  `M:System.Collections.Concurrent.ConcurrentDictionary`2.#ctor(System.Collections.Generic.IEqualityComparer{`0})` reports `RS0030` at
  `BoundedMap.cs(49,16)` and nowhere else; the bare `…#ctor` form is the parameterless overload's ID and matches only that one (a `new ConcurrentDictionary<int,int>()` is flagged, line 49 is not), so every overload is
  listed with its parameters (the seven public ones: `()`, `(int,int)`, `(IEnumerable<KVP>)`, `(IEqualityComparer)`,
  `(IEnumerable<KVP>,IEqualityComparer)`, `(int,IEnumerable<KVP>,IEqualityComparer)`, `(int,int,IEqualityComparer)`; the eighth is
  internal). A derived type's implicit base call is not seen, which is accepted: a subclass is itself a new type and a review item.

`BannedSymbolsSiteTests` (existing) gains a source scan (`ConcurrentDictionary` is constructed only in `BoundedMap.cs`), the
`GetBaseException` scan, and the one pragma site. Options: the bans (recommended: compile-time, no new tool); a Roslyn or IL scan
test (Ruby's AST gates; more code for the same answer here).

### H. The NativeAOT smoke, completed (P10-17)

Roadmap constraint 1 says phase 10 "completes" the smoke. Completion means the smoke exercises every package's runtime-wired
surface through the suites that now describe it:

- `CheckPhase10Async`: `InvariantSuite.RunAllAsync` with core's defaults (the hook-less assertions, both faces) must be green with
  exactly the pinned `NotExercised` list; `CodecSuite.RunAllAsync` over `SystemTextJsonSerde` and a source-generated context over the
  kit's probe models; `BuildInfo.SdkVersion` equals the build version (P10-15).
- Existing checks stay (`CheckPhase7aSerdeAsync` per 7a, `CheckPhase9Async` per 9, the 8a conformance slice).
- The smoke keeps **project references**, not the packed `.nupkg`s. Consuming packages would need a local feed, a version-pinned
  `PackageReference` set and a restore that cannot be locked across OSes (the csproj already explains why it carries no lock
  file); the packaging suite proves the packed assemblies carry the trim and AOT declarations (E), and the assemblies are the same
  build output. Options: project references (recommended); a second publish over the packages (truer to "only the shipped rules",
  costs a feed and a CI step). Open for the lead.
- The project's header comment and `ci.yml`'s ("phase 10 completes it") become past tense.

### I. Appendix B: `APPENDIX_B.md` and its gate (P10-19)

**The file** is `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` (the card's path), hand-written, one row per appendix-B item
in the specification's order: `| Section | Item | IDs | Evidence | Status | Notes |`. Statuses are the card's four:

| Status | Meaning here |
|---|---|
| `suite` | an assertion of `Dexpace.Sdk.Conformance` (any of the four suites), named in the Evidence cell as `<suite>:<assertion name>` |
| `by reference` | a test file or gate in this repository that owns the item (repository-relative path) |
| `restated` | the design restates the item for .NET (§9.3's restatements, a §10 entry or a §11 item named in Notes), and the restatement is what the evidence checks |
| `scoped out` | the port declines the whole item (none is planned) |

A row's status is that of its least portable evidence. A clause that is vacuous, declined or deferred inside an otherwise-evidenced
item is named in **Notes** (with its §10/§11 citation), so a row never reads better than its worst clause. The map opens with the
two limits a reader must not miss: appendix B covers nine of nineteen prefixes and omits `PAGE-4` and `PAGE-5` (§11 item 9, V8);
and a `by reference` row proves an ID is claimed and a file exists, never that the test asserts the behaviour (Ruby `R7`).

**The gate** is `tests/Dexpace.Sdk.Conformance.Tests/Requirements/AppendixBCoverageTests.cs` (`Unit`), reading the spec copy the
project already places in its output (`spec/appendix-b.md`) and the map:

1. the map has exactly 61 rows, and each section's count equals the count parsed from the specification (10, 6, 7, 8, 6, 5, 6, 6, 7);
2. each row's ID set equals the set parsed from that item's own text with the nineteen-prefix pattern (276 distinct IDs, none in two
   rows), so no row can be wrong about the requirements its item covers;
3. every status is one of the four, and every `by reference` path exists under the repository root;
4. **every `suite` row names at least one assertion that exists in a kit catalogue and cites at least one of the row's IDs** (stronger
   than Ruby's existence check: the named assertion is real and on topic);
5. `AppendixBMap` (the kit's internal view, now 61 items) equals the map's ID column, so the report's appendix-B view and the
   document cannot drift.

A negative control per rule (a map missing a row, a row with one ID too many, a dangling path, a `suite` row naming an assertion
that cites none of its IDs). Options: hand-written plus gate (recommended: 61 rows, few changes, and the gate does the checking);
a generator that preserves hand-written cells (Ruby's `tools/appendix_b.rb`; worth it only if the appendix changes, which it is
frozen against).

**Planned map** (the plan re-verifies each path; IDs as parsed, V8):

| Item | IDs | Planned evidence | Status | Notes |
|---|---|---|---|---|
| B.1.1 | PAGE-1, -2, -3 | `tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs` | by reference | `PAGE-3` 🚫 (§10 entry 17) |
| B.1.2 | PAGE-6, -7, -8 | `…/Pagination/PageableTests.cs` | by reference | |
| B.1.3 | PAGE-9, -10 | `…/Pagination/PageableTests.cs`, `BlockingPageableTests.cs` | by reference | |
| B.1.4 | PAGE-11, -12, -14, -15 | `…/Pagination/PaginationLifecycleTests.cs` | by reference | `PAGE-12`'s look-ahead and `PAGE-15`'s two-page case vacuous (§10 entry 17) |
| B.1.5 | PAGE-13 | `…/Pagination/PageStepTests.cs` | by reference | |
| B.1.6 | PAGE-16, -17, -18, -20, -19 | `…/Pagination/LinkHeaderParserTests.cs`, `PaginationStrategiesTests.cs` | by reference | P10-9 |
| B.1.7 | PAGE-21, -22, -23, -24 | `…/Pagination/QuerySpliceTests.cs`, `QuerySplicePropertyTests.cs` | by reference | `PAGE-21` on a stated domain (§10 entry 18) |
| B.1.8 | PAGE-25 … -33 | `…/Pagination/PageableTests.cs`, `BlockingPageableTests.cs` | by reference | `PAGE-29`, `PAGE-30` 🚫 (§10 entry 19) |
| B.1.9 | PAGE-34, -35 | `…/Pagination/FetcherPageableTests.cs` | by reference | |
| B.1.10 | PAGE-36 | `…/Pagination/PageableTests.cs` | by reference | |
| B.2.1 | SSE-1 … -12 | `…/ServerSentEvents/ServerSentEventParserTests.cs` | by reference | |
| B.2.2 | SSE-13 … -19 | `…/ServerSentEvents/EventAccumulatorTests.cs`, `ServerSentEventReaderTests.cs` | by reference | `SSE-19`'s cap (§10 entry 20) |
| B.2.3 | SSE-20, -21, -22 | `…/ServerSentEvents/ServerSentEventTests.cs` | by reference | |
| B.2.4 | SSE-23 … -32 | `…/ServerSentEvents/ServerSentEventStreamLifecycleTests.cs`, `ServerSentEventStreamReleaseTableTests.cs` | by reference | |
| B.2.5 | SSE-33 … -36 | `…/ServerSentEvents/ServerSentEventStreamTypedTests.cs` | by reference | |
| B.2.6 | SSE-37 … -41 | `…/Architecture/Sse37ArchitectureTests.cs`, `Sse38ArchitectureTests.cs` | by reference | `SSE-41` N/A (§11 item 21) |
| B.3.1 | SERDE-1, -2 | `codec:serde-1.round-trip`, `codec:serde-2.declared-media-type` | suite | |
| B.3.2 | SERDE-3, -4 | `codec:serde-3.target-not-closed`; `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeProfileTests.cs` | by reference | `SERDE-4` is core's `SerdeExtensions.Serialize(ISerde, Span<byte>, T)`, not a codec property (F6) |
| B.3.3 | SERDE-5 … -8 | `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/SystemTextJsonSerdeTests.cs` | restated | the type witness is a generic argument (§3.4) |
| B.3.4 | SERDE-9 … -13 | `codec:serde-9.no-library-type-escapes`, `codec:serde-10.*`, `codec:serde-12.*`; `tests/Dexpace.Sdk.Core.Tests/Serialization/ReadValueTests.cs`, `SerdeValuesTests.cs` | by reference | `SERDE-13` is core's typed readers (F6); `SERDE-11` restated: every .NET exception is unchecked |
| B.3.5 | SERDE-14 … -20, -30 | `codec:serde-15.*`, `-16.*`, `-17.*`, `-19.*`, `-20.*`; `tests/Dexpace.Sdk.Core.Tests/Serialization/TristateTests.cs`, `TristateSentinelTests.cs` | by reference | `SERDE-14`, `-18`, `-30` are `Tristate<T>`'s own API (F6); `SERDE-14`'s covariance SHOULD (§10 entry 21) |
| B.3.6 | SERDE-21 … -26, -29 | `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/StrictCoercionTests.cs`, `SerdeOptionsTests.cs` | by reference | default-codec properties, not seam properties |
| B.3.7 | SERDE-27, -28 | `tests/Dexpace.Sdk.Core.Tests/Serialization/ResponseHandlersTests.cs` | by reference | |
| B.4.1 | OBS-1 … -9, -40 | `…/Pipeline/Policies/DisabledPathAllocationTests.cs`, `…/Diagnostics/LogCatalogueTests.cs` | restated | `OBS-1`'s inert event as zero allocation (§9.3, §10 entry 22); `OBS-8`, `OBS-40` vacuous |
| B.4.2 | OBS-10 | `…/Pipeline/Policies/DiagnosticContextFlowTests.cs` | by reference | |
| B.4.3 | OBS-11 … -19 | `…/Security/UrlRedactionDefaultDenyTests.cs`, `…/Diagnostics/UrlRedactorTests.cs`, SystemNet `HeaderDropLogTests.cs` | by reference | |
| B.4.4 | OBS-20 | `…/Pipeline/Policies/EmissionGuardTests.cs` | by reference | §11 item 37 |
| B.4.5 | OBS-21 … -25 | `…/Diagnostics/ActivityScopeTests.cs`, `UntracedAllocationTests.cs` | restated | `Activity` (§10 entry 23) |
| B.4.6 | OBS-26 … -30 | `…/Diagnostics/TraceIdTests.cs`, `ListenerContractTests.cs` | by reference | `OBS-27`'s Datadog flavour (§10 entry 24) |
| B.4.7 | OBS-31, -32, -33 | `…/Diagnostics/HttpClientMetricsTests.cs` | by reference | §11 item 38 |
| B.4.8 | OBS-34 … -39 | `…/Pipeline/Policies/InstrumentationPolicyTests.cs`, `BodyPreviewTests.cs`; DI `LogLevelBindingTests.cs` | by reference | `OBS-35` split (§10 entry 26) |
| B.5.1 | CFG-1, -2, -3, -4, -38 | `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/ConfigurationPrecedenceTests.cs` | restated | `IConfiguration` tiers through the DI package's reader (§10 entry 25, not "binding": the 9 correction to §9.3) |
| B.5.2 | CFG-5, -6, -7 | DI `DurationGrammarTests.cs`, `IntegerGrammarTests.cs`, `BooleanGrammarTests.cs` | restated | fail fast (§10 entry 26) |
| B.5.3 | CFG-8 … -14, -37 | `…/Configuration/OptionsImmutabilityTests.cs`; DI `KeyConstantsTests.cs`, `NullGuardTests.cs` | restated | `CFG-10`, `CFG-12` N/A; `CFG-13` declined |
| B.5.4 | CFG-15 … -21 | `…/Configuration/TimeProviderWaitsTests.cs`, `…/Internal/LateResultTests.cs` | by reference | `CFG-20` retired (§10 entry 8) |
| B.5.5 | CFG-22 … -28 | `…/Configuration/ProxyOptionsTests.cs`, `ProxyGlobTests.cs`, `NoProxyListTests.cs`, `NoImplicitProxyReadTests.cs` | by reference | |
| B.5.6 | CFG-29 … -36 | `…/Http/Common/HttpDateTests.cs`, `…/Internal/DeepValueTests.cs`, `…/Configuration/BuildInfoTests.cs` | by reference | |
| B.6.1–B.6.5 | TRANSPORT-1 … -30 | `transport:*` (8a's catalogue) | suite | entries 32, 33 waivers |
| B.7.1 | ASYNC-1, -2 | `transport:async-1.single-non-null-response`, `transport:transport-21.*` | suite | `ASYNC-2`'s scheduler clause ⏳ (F-A2) |
| B.7.2 | ASYNC-3 … -7 | `…/Client/SyncToAsyncBridgeTests.cs`, `AsyncToSyncBridgeTests.cs` | restated | cooperative cancellation (§10 entry 8); `ASYNC-4` vacuous |
| B.7.3 | ASYNC-8 … -12 | `…/Client/BridgeLoggingContextTests.cs` | restated | logging scopes over `AsyncLocal` flow (§9.3) |
| B.7.4 | ASYNC-13, -14 | `…/Client/AsyncToSyncBridgeTests.cs` | by reference | |
| B.7.5 | ASYNC-15, -16, -17 | `transport:transport-16.close-idempotent-nonblocking`, `transport:transport-15.*` | suite | `ASYNC-16` N/A |
| B.7.6 | ASYNC-18 … -22 | `transport:async-20.late-cancel-leaves-response-open`, `transport:transport-29.concurrent-no-crosstalk` | suite | `ASYNC-21` N/A (§11 item 21) |
| B.8.1 | XCUT-1, -2, -3 | `invariant:xcut-1.*`, `xcut-2.*`, `xcut-3.*` | suite | |
| B.8.2 | XCUT-4 … -9 | `invariant:xcut-4.*` … `xcut-9.*` | suite | `XCUT-4` (§10 entry 7), `XCUT-8` (P10-24) |
| B.8.3 | XCUT-10 | `invariant:xcut-10.safety-gate-uniform` | suite | |
| B.8.4 | XCUT-11, -12, -13, -22, -23 | `invariant:xcut-11.*`, `-12.*`, `-13.*`, `-22.*`, `-23.*` | suite | `XCUT-13`'s flag clause vacuous; `XCUT-23` discovery vacuous (§10 entry 9) |
| B.8.5 | XCUT-14, -15 | `invariant:xcut-14.*`, `xcut-15.*` | suite | |
| B.8.6 | XCUT-16 … -21, -24 | `invariant:xcut-16.*` … `xcut-21.*`, `xcut-24.*` | suite | `XCUT-24`'s error-snapshot clause declined (P10-23) |
| B.9.1 | NFR-1, -2 | `packaging:nfr-1.core-dependencies`, `nfr-2.adapter-budget` | suite | §10 entries 1, 2 |
| B.9.2 | NFR-3, -4 | `packaging:nfr-3.*`; `Directory.Build.props` (`RS0016`/`RS0017`) | by reference | `NFR-4`'s baseline ⏳ 12 |
| B.9.3 | NFR-5, -6, -7, -17 | `scripts/ci/coverage-gate.cs`, `Directory.Build.props`, `.editorconfig`, `Gates/CiWorkflowTests.cs` | by reference | |
| B.9.4 | NFR-8, -9 | `packaging:nfr-8.trim-and-aot-declared`; `tests/Dexpace.Sdk.AotSmoke/` | by reference | |
| B.9.5 | NFR-10 | `packaging:nfr-10.single-floor` | suite | |
| B.9.6 | NFR-11 | `…/Architecture/Nfr11PublicSurfaceArchitectureTests.cs`; `invariant:nfr-11.scheduler-agnostic` | by reference | |
| B.9.7 | NFR-12 … -16 | `scripts/ci/reproducible-pack.sh`, `Gates/LicenseHeaderTests.cs`, `packaging:nfr-14.*`, `packaging:nfr-15.*` | by reference | `NFR-12`'s release half and `NFR-16` ⏳ 12 |

(B.6 is five rows in the file; it is collapsed here.) **Planned tally: 17 `suite`, 8 `restated`, 36 `by reference`, 0 `scoped
out`** (the first draft's 20/8/33 became 17/8/36 when B.3.2, B.3.4 and B.3.5 took the least portable evidence of their items, F6).

### J. The appendix-C coverage script (P10-20)

`scripts/ci/requirement-coverage.cs`, a file-based app beside `requirement-catalog.cs`, run from the repository root:

- **Input**: appendix C (canonical IDs and levels, the same row pattern as `requirement-catalog.cs`) and every
  `docs/work/**/*-checklist.md`.
- **Owning rows**: a row whose first cell is a requirement ID, in a table whose header starts `| ID | Level |` (V7). Tables with any
  other header (`| ID (owner) | … |`, `| ID | Mark | … |`, `| ID | As built | … |`) are contributed or carried evidence and do not count.
- **Carried IDs**: an ID with owning rows in more than one checklist is resolved by phase order (the checklist's `phaseN[x]`
  directory); the latest row's mark is the ID's mark, and every earlier row must carry ⏳ or a split naming the later phase, or the
  script reports the pair. Today's six carried IDs (V7) all resolve.
- **Checks**, each a finding: an appendix-C ID with no owning row; an owning row naming an unknown ID; a level cell disagreeing with
  appendix C; an owning row whose status cells carry no legend mark (✅ 🚫 ⏳ N/A, "vacuous" counted as N/A).
- **Output**: a tally per prefix and per mark, and per level (MUST rows by mark).
- **Modes**: `--report` (exit 0, print) and `--check` (exit 1 on any finding in a MUST row, 2 on input errors). `--check` is a CI
  step on the Linux row (structural, like `verify-structure`), turned on in phase 10's close-out PR, when the 41 rows exist; before
  that it would report exactly them (V7).
- **The kit stays out of it.** `RequirementCatalog` is the kit's generated table for consumers; the coverage script is a
  repository gate and reads appendix C itself. Both use the same row pattern, and `RequirementCatalogTests` already proves the
  catalogue current, so the two cannot disagree on the ID set.

Options: a `scripts/ci` app and CI step (recommended: the card asks for "a script", and the release blocker "phase 10's row-count
gate proves the total" needs it in CI); a housekeeping probe check (hand-run by its own `SKILL.md`, so not a gate); a test reading
`docs/` (couples the test run to documentation files outside any project).

### K. Optional gates: Stryker.NET and `benchmarks/` (P10-21, P10-22)

**Stryker.NET: not scheduled for v1** (P10-21, open for the lead). V5: Stryker.NET 5.0.0 refuses every test project here because
they run on Microsoft.Testing.Platform (stryker-net#3094), which roadmap constraint 6 migrated to deliberately. Running it would
need a VSTest-compatible shadow of each test project (xUnit v3 can be driven through `xunit.runner.visualstudio`), maintained
beside the real one for a gate that is optional and non-blocking by §9.3's own text; that is a second test harness to keep in step
for a report nobody is obliged to read. Recorded: a post-release trigger in `docs/first-release.md` ("Stryker.NET supports
Microsoft.Testing.Platform"), a knowledge note under `docs/knowledge/notes/testing.md` resolving styleguide 11.8's optional gate
against the MTP runner, and a dated correction proposal to §9.3. When the trigger fires, the scope §9.3 names stands: header and
media-type parsing, the SSE line reader, the query splice, URL redaction and retry classification, on a schedule, with a break
threshold. Options: decline with trigger (recommended); a VSTest shadow project for `Dexpace.Sdk.Core.Tests` only, run weekly,
non-blocking.

**`benchmarks/`: not built** (P10-22). §9.3 makes BenchmarkDotNet results a requirement of "any performance-motivated change";
there has been none, and the allocation guarantees that matter are already blocking unit tests (`DisabledPathAllocationTests`,
`UntracedAllocationTests`, the `OBS-1` and `OBS-25` restatements). An empty project would add a package, lock files and a CI build
for nothing measured. The first performance-motivated pull request creates `benchmarks/` with its own benchmark; recorded as a
post-release trigger and a §9.3 correction proposal. Options: decline with trigger (recommended); a minimal project with three
benchmarks (header parse, URL redaction, pipeline overhead), on demand, outside CI.

### L. Findings the measurement produced (P10-23, P10-24, P10-25)

- **P10-23, `XCUT-24`'s error-body snapshot.** `ErrorBodyBuffer.Capture` drains at most 1 MiB, truncates without a marker and
  replaces the original response (RECOV-16, P4b-16), so for an error body above the cap the consumer's read of the exception's body
  is the truncated copy. The MUST half (byte-capped) holds; the SHOULD half (non-consuming) holds for body-log previews and not for
  this snapshot. **Ruling: record as a declined SHOULD clause** (`docs/first-release.md` *SHOULD declined*: "an error body above 1 MiB
  reaches the caller truncated, without a marker"), no code change; the option of a truncation marker or a pass-through of the
  remainder goes to phase 11's inbound list as a repair candidate. Open for the lead.
- **P10-24, `XCUT-8` and the public constructor.** Every core mapping path goes through the guarded internal factory, and the
  convenience forms return a non-error response. The public `HttpResponseException(Response, string?)` constructor accepts a 200
  (V2); 4c kept it unguarded deliberately. **Ruling: read `XCUT-8`'s "status-to-exception mapping factory" as the SDK's mapping
  path, not a constructor a caller invokes with a response of their choosing**, proposed as a new §11 item; whether to guard the
  constructor too (a breaking change for a caller who builds one from a 2xx) is listed for phase 12's one-time public-surface
  choices. Open for the lead.
- **P10-25, `ServiceResponseException`.** Public, documented as part of `XCUT-4`'s taxonomy, never thrown (V15), absent from the
  design. It is a third exchange-failure type beside the two branches `XCUT-4` defines. **Ruling: route** to phase 11's inbound
  list (audit: delete, or argue it as part of branch (b) in §3.2) and to phase 12's public-surface choices; the `XCUT-4` row is ✅ on
  the two branches the SDK actually raises. Open for the lead.

---

## The public surface

Phase 10 changes the **unshipped** surface of one package, `Dexpace.Sdk.Conformance` (reviewed `PublicAPI.Unshipped.txt` diff,
`RS0016`/`RS0017`, a `CHANGELOG.md` line, constraint 8). No other package's surface changes. Indicative signatures; the plan fixes
the exact lines:

```csharp
namespace Dexpace.Sdk.Conformance;

public enum TransportFace { Async = 0, Blocking = 1, None = 2 }                       // None: added (P10-7)

public sealed record SuiteOptions { TimeSpan AssertionTimeout { get; init; } IReadOnlyList<ConformanceWaiver> Waivers { get; init; } }

public static class InvariantSuite
{
    public static string Preamble { get; }
    public static IReadOnlyList<ConformanceAssertion> Assertions { get; }
    public static Task<ConformanceReport> RunAllAsync(InvariantSubject subject, SuiteOptions? options = null, CancellationToken cancellationToken = default);
    public static Task<ConformanceResult> RunAsync(InvariantSubject subject, ConformanceAssertion assertion, TransportFace face, SuiteOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed record InvariantSubject
{
    public string Name { get; init; }
    public Func<IAsyncHttpClient, DexpaceClientOptions, TimeProvider, HttpPipeline> CreatePipeline { get; init; }   // default: core's standard pipeline
    public Func<Response, SdkException> CreateResponseException { get; init; }                                       // default: the public constructor
    public Func<Response, Exception>? MapStrict { get; init; }                                                         // XCUT-8, no default
    public Func<Exception>? CreateCyclicException { get; init; }                                                       // XCUT-9, no default
    public Func<int, BoundedMapProbe>? CreateBoundedMap { get; init; }                                                 // XCUT-14, no default
    public Func<string>? DrawCnonce { get; init; }                                                                     // XCUT-21 draw under test; null = through DigestChallengeHandler (optional, not a no-default hook)
    public SeamResolution? SeamResolution { get; init; }                                                               // XCUT-23, no default
    // plus CreateTokenCache, CreateAuthPolicy, CreateRedactor, Credentials, Models (all defaulting to core)
}

public sealed class BoundedMapProbe { public BoundedMapProbe(int capacity, Action<string> insert, Func<int> count); /* … */ }
public sealed record SeamResolution(Action ResolveWithNone, Action ResolveWithTwo, Func<bool> ExplicitWins);

public static class CodecSuite   { /* Preamble, Assertions, RunAllAsync(CodecSubject, SuiteOptions?, CancellationToken), RunAsync(…) */ }
public sealed record CodecSubject { public string Name { get; init; } public Func<ISerde> CreateSerde { get; init; } public MediaType? ExpectedMediaType { get; init; } }

public static class PackagingSuite { /* Preamble, Assertions, RunAll(PackagingSubject, SuiteOptions?) */ }
public sealed record PackagingSubject { IReadOnlyList<string> PackagePaths; string CoreId; IReadOnlyDictionary<string, PackagePolicy> Policies; string DeclaredFloor; }
public sealed record PackagePolicy(string? ThirdPartyRoot, IReadOnlyList<string> FirstPartyIds);

public sealed partial class ConformanceReport { public static ConformanceReport Combine(string subjectName, IEnumerable<ConformanceReport> reports); }

namespace Dexpace.Sdk.Conformance.Codec;   // probe models a codec driver registers (P10-8)
```

The kit stays `IsTrimmable` and `IsAotCompatible` (no reflection in the suites except `System.Reflection.Metadata` over bytes,
which is trim-safe), and it runs in the AOT smoke (H).

## Packages, projects and dependencies

No new package, project or `PackageVersion`. `Dexpace.Sdk.Core.Tests` gains a `ProjectReference` to the kit (P10-5) and its lock
file moves; `Dexpace.Sdk.Serialization.SystemTextJson.Tests` gains one too (D). `BannedSymbols.txt` gains two bans, eight lines (G).
`scripts/ci/` gains `packaging-conformance.cs` and `requirement-coverage.cs`; `reproducible-pack.sh` changes (P10-14).

## The gate table at `af603d2`

Phase 0 left no gate checklist, so this is the first disposition of design §9's table and the roadmap's Maturity Definition gate
list. "Blocking" means it fails its CI job; merge blocking is `NFR-17`'s second half (P10-16).

| Gate | Requirement | Mechanism | State measured | Phase 10 action |
|---|---|---|---|---|
| Warnings as errors | NFR-6 | `TreatWarningsAsErrors`, NuGet audit | blocking (V4) | none |
| Analyzers, code style, format | NFR-7 | `AnalysisLevel`, `EnforceCodeStyleInBuild`, `dotnet format --verify-no-changes` | blocking | re-enable conditions + `AnalyzerWaiverTests` (P10-13) |
| Documentation gate | NFR-3 | `GenerateDocumentationFile`, `CS1591` | blocking | none |
| Public-API files | NFR-4 | `RS0016`/`RS0017`, five libraries | blocking (V4) | `NFR4ArchitectureTests` |
| Package validation | NFR-4 | `EnablePackageValidation` in pack | blocking; baseline phase 12 | none |
| Coverage floor ≥ 80% | NFR-5 | `coverage-gate.cs` + self-test | blocking, 97.60% (V1) | none |
| Dependency audit | NFR-1, NFR-2, SEAM-1 | `dependency-audit.cs` | blocking | packaging suite beside it (P10-10) |
| `SEAM-2`, `SSE-37`, `SSE-38` architecture tests | SEAM-2, SSE-37, SSE-38 | `Core.Tests/Architecture` | blocking | `SEAM-2` allow-list gains the kit (P10-5) |
| Trim and AOT analyzers | NFR-8 | `IsTrimmable`, `IsAotCompatible` on `src/` | blocking | packaging `nfr-8.*` |
| NativeAOT smoke | NFR-9 | `aot-smoke` job | blocking per job (V16) | `CheckPhase10Async` (P10-17) |
| Lock files | NFR-14 | `--locked-mode` | blocking | none |
| License headers | NFR-13 | `IDE0073` | blocking for compiled files | `LicenseHeaderTests` (P10-12) |
| Banned APIs | §9.1 | `RS0030` | blocking | two entries (P10-18) |
| Reproducible pack | NFR-12 | `reproducible-pack.sh` | blocking, same-path only | cross-path (P10-14) |
| 3-OS matrix | NFR-10, NFR-17 | `build` job matrix | blocking per job | none |
| Tools solution | — | tools build and test | blocking | none |
| Knowledge structure | — | `verify-structure` | blocking (Linux) | none |
| Conformance kit | TRANSPORT, ASYNC | three drivers | blocking | three suites added |
| `NFR-11` signature scan | NFR-11 | — | **not wired** | `Nfr11PublicSurfaceArchitectureTests` (P10-11) |
| Appendix-B gate | — | — | **not wired** | `AppendixBCoverageTests` (P10-19) |
| Appendix-C coverage | — | — | **not wired** | `requirement-coverage.cs --check` (P10-20) |
| Packaging conformance | NFR-1, -2, -3, -8, -10, -13, -14, -15 | — | **not wired** | CI step (P10-10) |
| CI workflow completeness | NFR-17 | — | **not wired** | `CiWorkflowTests` (P10-16) |
| Merge requires the checks | NFR-17 | ruleset | **absent** (V9) | the lead's ruleset change (P10-16) |
| Mutation testing | (§9.3, optional) | Stryker.NET | cannot run (V5) | declined with trigger (P10-21) |
| Benchmarks | (§9.3, optional) | BenchmarkDotNet | none | declined with trigger (P10-22) |
| Ledger audit | — | — | phase 11 | none |
| Strong naming, author signing | NFR-16 | release job | phase 12 | none |

---

## Test strategy

| Class (project) | Category | Rows / purpose |
|---|---|---|
| `Controls/Invariant*ControlTests`, `Controls/Codec*ControlTests`, `Controls/Packaging*ControlTests` (`Conformance.Tests`) | Unit | a negative control per new assertion; `AssertionCatalogueTests` widened to all four catalogues |
| `Suite/InvariantSuiteTests`, `CodecSuiteTests`, `PackagingSuiteTests`, `Reporting/ConformanceReportCombineTests` (`Conformance.Tests`) | Unit | the runner over each suite, `TransportFace.None`, `Combine`'s worst-of, per-suite preambles |
| `Support/NupkgBuilder` (`Conformance.Tests`) | — | synthetic packages for the packaging controls |
| `Requirements/AppendixBMapTests` (widened), `Requirements/AppendixBCoverageTests` (`Conformance.Tests`) | Unit | P10-19's five rules with their controls |
| `Gates/LicenseHeaderTests`, `Gates/AnalyzerWaiverTests`, `Gates/CiWorkflowTests`, `Gates/PackagingPolicyParityTests` (`Conformance.Tests`) | Unit | `NFR-13`, `NFR-7`, `NFR-17`, the audit/suite policy parity (P10-12, P10-13, P10-16, P10-10) |
| `Conformance/InvariantConformanceTests` (`Core.Tests`) | Conformance | the core driver with the three internal hooks; the pinned `NotExercised` list (P10-5, P10-28) |
| `Architecture/Nfr11PublicSurfaceArchitectureTests`, `Architecture/NFR4ArchitectureTests`, `Architecture/Seam2ArchitectureTests` (edited), `Architecture/BannedSymbolsSiteTests` (edited) (`Core.Tests`) | Unit | `NFR-11`, `NFR-4`, `SEAM-2`'s allow-list, the two bans |
| `Conformance/CodecConformanceTests` (`Serialization.SystemTextJson.Tests`) | Conformance | the codec driver over the probe context |
| `Conformance/InvariantResolutionConformanceTests` (`Extensions.DependencyInjection.Tests`) | Conformance | `SeamResolution`; the suite over the DI-built pipeline |
| `ConformancePageTests` (edited, `Conformance.Tests`) | Unit | `conformance.md`'s catalogue tables match all four suites |
| `SmokeChecks.Phase10.cs` (`AotSmoke`) | AotSmoke (exit code) | P10-17 |

Every new test class carries `[Trait("Category", …)]`; the phase-1 `Security` classes are not edited. TDD order per assertion:
the control first (it must fail against the assertion), then the assertion, then the first-party driver green.

## CI changes

`build` job: after `Pack`, a **Packaging conformance** step (`dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages`);
on Linux, a **Requirement coverage** step (`dotnet run scripts/ci/requirement-coverage.cs -- --check`). `reproducible` job: the
cross-path script (P10-14). `aot-smoke` job: unchanged (the smoke gains checks). The header comment's "phase 10 completes it" and
"`NFR-11`'s public-signature type scan: phase 10" lines become past tense. The ruleset change (P10-16) is outside the file.

## PR segmentation

Four stacked pull requests on `<issue>-phase-10-cross-cutting-invariants` (code and tests together, roadmap step 5), in
**dependency** order:

1. **The kit contract and the invariant suite.** The runner extraction, `TransportFace.None`, `SuiteOptions`, `Combine`, per-suite
   preambles; `InvariantSuite`, `InvariantSubject` and the 28 assertions with their controls; the core driver (`SEAM-2` allow-list
   edit, the kit reference) and the DI driver; the two bans (the `GetBaseException` entry and `ConcurrentDictionary`'s constructors). The coverage gate stays green because the kit's new code is exercised
   by its controls in the same PR.
2. **The codec suite.** `CodecSuite`, the probe models, 11 assertions and controls; the STJ driver.
3. **The packaging suite and the NFR gates.** `PackagingSuite` and its controls, `NupkgBuilder`, the CI step and file-based app,
   `PackagingPolicyParityTests`; `Nfr11PublicSurfaceArchitectureTests`, `NFR4ArchitectureTests`, `LicenseHeaderTests`,
   `AnalyzerWaiverTests` with the `.editorconfig` re-enable lines, `CiWorkflowTests`; the cross-path reproducible pack.
4. **Appendix B, coverage and close-out.** `APPENDIX_B.md`, `AppendixBMap` to 61, `AppendixBCoverageTests`; `requirement-coverage.cs`
   and its CI step; `CheckPhase10Async`; `conformance.md` and the phase page (P10-27); the checklist (41 rows, the contributed table,
   the gate table, the styleguide audit groups); `CHANGELOG.md`; the corrections below; the knowledge notes; the housekeeping probe;
   the request to the lead for the ruleset (P10-16).

## Breaking changes

None to a library a consumer of the SDK uses. `Dexpace.Sdk.Conformance` gains public surface (additive; `TransportFace.None` is a
new enum member, which an exhaustive `switch` over the enum in a third-party driver would have to handle), recorded in
`CHANGELOG.md` `[Unreleased]`. P8a-5's versioning rule ("a release that adds an assertion is at least a minor version") applies
at the first publish; nothing is published yet.

## Hand-offs to later phases

- **11** — inbound list bullets: `ServiceResponseException` (P10-25: delete or argue in §3.2); `XCUT-24`'s error-snapshot repair
  candidate (P10-23); the unmet-clause count question for `XCUT-4`'s "catchable as a generic I/O error" beside `CFG-2`'s entry 25
  (both judged mechanisms today); `XCUT-8`'s reading (P10-24) as a §11 item to confirm; F-A2 (`ASYNC-2`) unchanged; the `TRACE`
  question and the parity over-budget from 8b unchanged.
- **12** — the one-time public-surface choices gain the `HttpResponseException` constructor guard (P10-24) and
  `ServiceResponseException` (P10-25); `NFR-4`'s baseline, `NFR-12`'s release half and `NFR-16` stay *After the first publish* and
  *Blockers*; `quality-gates.md` absorbs this phase's gate table; `conformance.md` joins the user documentation with all four suites;
  the kit's first published version carries the three suites; if P10-16's ruleset change was not made, it is a release blocker.

## Corrections owed at close-out (proposals)

Applied as dated corrections when the checklist lands, not by this document. The frozen trees (`docs/product-spec/`,
`docs/sdk-design-dotnet/`) take them only by a human's deliberate edit; nothing here is a change to the specification.

1. **Design §9** (gate table) — the `NFR-11` row: the scan reads the assembly, not `PublicAPI.Shipped.txt` (P10-11). **§9.1** — the
   banned list gains `Exception.GetBaseException` and `ConcurrentDictionary<,>`'s constructors (P10-18). **§9.2** — `NFR-9`'s rule-file clause is
   the packed assembly's `AssemblyMetadata` (V6); the packaging suite beside the audit (P10-10); the reproducible-pack job is
   cross-path (V3). **§9.3** — "Coverage, mutation and benchmarks": Stryker.NET is not scheduled (V5, P10-21) and `benchmarks/` is
   created by the first performance-motivated change (P10-22); the Appendix B paragraph: B.5 is exercised against the DI package's
   reader (the 2026-10-13 correction already says binding is not used), and the map is `APPENDIX_B.md` with its gate (P10-19); the
   kit has four suites. **§10 entry 7** — a note that `ServiceResponseException` is never raised (P10-25). **§11** — a new item for
   `XCUT-8`'s reading (P10-24). **§12** — the `XCUT` and `NFR` rows' notes (phase 10 evidence; 24 and 17 rows dispositioned).
2. **Roadmap** — the Phase List row 10 links this design, the plan and the checklist (issue #99's deliverable); the Phase 10 card's
   starting counts are superseded by the measured ones (a dated correction); a dated status note at close.
3. **`docs/first-release.md`** — *SHOULD declined*: `XCUT-24`'s error-snapshot clause (P10-23); *Post-release triggers*:
   Stryker.NET on MTP (P10-21), the first performance-motivated change (P10-22); *Blockers*: "Every MUST dispositioned" ticked by the
   coverage script's `--check`; the `NFR-17` ruleset line if P10-16 is not done; *Packages*: the kit's row says phase 10 completed it.
4. **Checklists** (dated corrections): 9's `XCUT-10` row (⏳ 10 → the phase 10 row); 4c, 5b, 5c, 6a, 6b, 6c, 7b, 8a, 8b "rows owned
   elsewhere" rows gain "taken by phase 10".
5. **`CLAUDE.md`** — the layout's `Dexpace.Sdk.Conformance` line names the four suites; the Build & test block gains the two new
   scripts; "What is genuinely unbuilt" gains phase 10's summary; "Things that will bite you" gains one bullet (an invariant hook left
   `null` is `NotExercised`, and a driver pins its expected list; a new `ConcurrentDictionary` or `GetBaseException` in `src/` fails
   `RS0030`).
6. **`docs/sdk-documentation/conformance.md`** — the three suites, their subjects, hooks and catalogues; **the phase page** (P10-27).
7. **Knowledge** — `docs/knowledge/notes/testing.md` (styleguide 11.8's optional mutation gate vs MTP, V5); `docs/knowledge/notes/tooling-and-quality-gates.md`
   (V3's cross-path fact and the git-checkout precondition; V9's ruleset fact).
8. **`src/Dexpace.Sdk.Conformance/README.md`** — the four suites.

## Risks and open questions (all resolved here)

| # | Question | Resolution |
|---|---|---|
| Q1 | Does the segmentation rule split phase 10? | No: one phase, dependency-ordered PRs (P10-1), open for the lead. |
| Q2 | Can the kit reach the `XCUT` clauses without internals? | All but five clauses (V2): four are hooks with no default (P10-4), the fifth (`XCUT-21`'s source identity) is cited by reference (F5). |
| Q3 | Where does the driver that supplies the internal hooks live? | `Core.Tests`, with `SEAM-2`'s allow-list widened to the core-only kit (P10-5), open. |
| Q4 | One result model or one per suite? | One, with `TransportFace.None` and `Combine` (P10-7), open. |
| Q5 | Does phase 10 fix the defects it finds? | No; it routes them (P10-2), open. |
| Q6 | Is Stryker.NET feasible? | Not on MTP (V5); declined with a trigger (P10-21). |
| Q7 | Where does the packaging suite run? | A file-based CI step after `Pack` (V6, P10-10). |
| Q8 | Is the build reproducible across environments? | From a git checkout, yes (V3); the job becomes cross-path (P10-14). |
| Q9 | Are the gates blocking? | Per job, yes; at merge, no (V9); the ruleset change is the lead's (P10-16). |
| Q10 | Does the coverage script work over the checklists as written? | Yes, with the `\| ID \| Level \|` rule and phase-ordered carried rows (V7, P10-20). |

**Residuals.** R1: `by reference` rows prove a claim and a file, not the behaviour (stated in the map). R2: the invariant suite's
hook-less defaults are core's own components, so a green run against the defaults proves core and nothing about a third party's
composition until they supply theirs. R3: `NFR-17`'s merge half depends on a setting outside the repository and can be undone without
a diff; `CiWorkflowTests` cannot see it. R4: V3's reproducibility holds for a git checkout only. R5: the probe models are kit surface
that a codec driver must register; renaming one breaks every driver (kit versioning, P8a-5).

## Exit criteria

1. The checklist has **41 rows** as tabled (39 ✅ with 10 split, 1 ⏳ `NFR-16`, `NFR-17` ✅ or ⏳ per P10-16), each ✅ with a named test,
   `[Trait]` and file; a second table records the contributed evidence and every hand-off; a third is the gate table.
2. The invariant suite is green on the core driver and the DI driver, the codec suite on the STJ driver, the packaging suite in CI
   over `artifacts/packages`, each with exactly its pinned `NotExercised` list and waivers; the three transport drivers stay green.
3. `APPENDIX_B.md` maps all 61 items (planned: 17 `suite`, 8 `restated`, 36 `by reference`) and `AppendixBCoverageTests` is green.
4. `scripts/ci/requirement-coverage.cs --check` reports **645 of 645 IDs owned, every MUST with a legend mark**, and runs in CI.
5. The AOT smoke publishes and runs `CheckPhase10Async`.
6. Every gate green: build, format, the full test run with the phase-1 `Security` classes unedited, coverage ≥ 80%, the dependency
   audit, the packaging step, the cross-path reproducible pack, `scripts/knowledge verify-structure`.
7. `conformance.md`, the phase page, the READMEs, `CHANGELOG.md`, the dated roadmap status note, the corrections above, the knowledge
   notes, and a housekeeping probe that reports nothing.

## Rulings

| ID | Ruling | Open for the lead |
|---|---|---|
| P10-1 | The segmentation rule does not bind phase 10 by its letter; applied anyway, one phase, no sub-phases; the chapter cut (10a XCUT, 10b NFR) is recorded and liftable | **yes** |
| P10-2 | Phase 10 measures and routes; no behavioural change outside `Dexpace.Sdk.Conformance`, tests, scripts and gate wiring; the kit's own defects are the exception. Options: **A** as ruled, **B** small test-first core repairs for owned rows. **Recommended: A** | **yes** |
| P10-3 | The suites live in the kit, core-only and BCL-only; no new package | — |
| P10-4 | Every invariant assertion reads its subject through a hook defaulting to core; four hooks have no default (`MapStrict`, `CreateCyclicException`, `CreateBoundedMap`, `SeamResolution`); absent means `NotExercised`; `DrawCnonce` is optional (default through `DigestChallengeHandler.Authorize`; format and no-repeat only, the source clause by reference; F5) | — |
| P10-5 | The core driver lives in `Dexpace.Sdk.Core.Tests`; `SEAM-2`'s allow-list gains the kit. Options: **A** as ruled, **B** public-only driver in `Conformance.Tests` with five MUST-level `NotExercised`. **Recommended: A** | **yes** |
| P10-6 | No transport assertion is duplicated; the invariant suite asserts core-level clauses; `Combine` merges by requirement | — |
| P10-7 | One result model across suites: `TransportFace.None`, an extracted runner, `SuiteOptions`, per-suite preambles, `ConformanceReport.Combine`, `AppendixBMap` at 61. Options: add `None` (recommended), rename to `ConformanceFace`, nullable face | **yes** (kit surface) |
| P10-8 | The codec suite lifts `SERDE-1`–`-3`, `-9`, `-10`, `-12`, `-15`–`-17`, `-19`, `-20` and `SEAM-20` (11 assertions; `SERDE-4`, `-13`, `-18`, `-14`, `-30` stay by reference, F6); public probe models in `Dexpace.Sdk.Conformance.Codec`; STJ is the first driver. **Recommended: public probe models** | **yes** (probe models) |
| P10-9 | 7c's `link-header.json` and splice properties are not lifted; B.1 is `by reference`. **Recommended: decline** | **yes** |
| P10-10 | The packaging suite reads packed `.nupkg`s with BCL readers; eight assertions; runs as a file-based CI step after `Pack`; the audit stays; a parity test ties their policies | — |
| P10-11 | The `NFR-11` scan is a reflection test over core's exported surface (not the text file), an allow-list under `System`, `Microsoft.Extensions.Logging`, `Dexpace.Sdk.Core` plus an explicit ban list (`System.Net.Http`, `System.Text.Json`, `Microsoft.Extensions.DependencyInjection`); the behavioural half is `nfr-11.scheduler-agnostic` | — |
| P10-12 | `NFR-13`'s repository test covers every `.cs`, the `scripts/ci` file-based apps included; build files are not source | — |
| P10-13 | Every `none` severity and relaxing `dotnet_code_quality.*` option gains a re-enable condition (consecutive settings share one block; `dispose_ownership_transfer_at_constructor` is exempted by name as a hand-off rule); `AnalyzerWaiverTests` gates rationale, condition and every `src/` pragma's why-comment | — |
| P10-14 | The reproducible-pack job packs from two worktrees at different paths | — |
| P10-15 | `NFR-15`: the packaging assertion and the smoke's `BuildInfo.SdkVersion` check | — |
| P10-16 | `NFR-17`: `CiWorkflowTests`, and the `main` ruleset gains `required_status_checks` for every job (an admin action); without it, `NFR-17` is ⏳ with a release blocker | **yes** (the setting) |
| P10-17 | The AOT smoke is completed by `CheckPhase10Async` (invariant and codec suites, the version check) and keeps project references. Options: project references (recommended), a second publish over the packages | **yes** |
| P10-18 | Two `RS0030` bans in `src/`: `Exception.GetBaseException` (`XCUT-9`) and `ConcurrentDictionary<,>`'s seven public constructors, one scoped pragma at `BoundedMap`'s single construction site (`XCUT-14`; F1) | — |
| P10-19 | `APPENDIX_B.md` is hand-written (`Section`, `Item`, `IDs`, `Evidence`, `Status`, `Notes`), status = least portable evidence, clauses in Notes; the gate's five rules, including "a `suite` row names a real assertion citing one of its IDs" | — |
| P10-20 | `scripts/ci/requirement-coverage.cs`: owning rows by the `\| ID \| Level \|` header, carried rows by phase order, four finding kinds, `--report`/`--check`, a Linux CI step from the close-out PR | — |
| P10-21 | Stryker.NET is not scheduled (MTP unsupported, V5); post-release trigger, knowledge note, §9.3 proposal. **Recommended: decline** | **yes** |
| P10-22 | `benchmarks/` is not built; the first performance-motivated change creates it. **Recommended: decline** | **yes** |
| P10-23 | `XCUT-24`'s non-consuming clause is declined for the error-body snapshot above 1 MiB (*SHOULD declined*); the repair is a phase 11 candidate | **yes** |
| P10-24 | `XCUT-8` reads as the SDK's mapping path, not the public constructor; a §11 proposal; the constructor guard is a phase 12 choice | **yes** |
| P10-25 | `ServiceResponseException` (never thrown) is routed to phase 11 and phase 12's surface choices; `XCUT-4` is ✅ on the two branches raised | **yes** |
| P10-26 | No new specification deviation and no new §10 entry; every split rests on an existing §10 entry or §11 item, or on P10-23/P10-24 | — |
| P10-27 | The phase page is `docs/sdk-documentation/cross-cutting-invariants.md` (where each `XCUT` row is enforced, the gate table, how to run the suites against a third-party composition); `conformance.md` gains the suites; phase 12's `quality-gates.md` absorbs the gate table. Options: as ruled, or only extend `conformance.md` | **yes** |
| P10-28 | Every first-party driver pins its expected `NotExercised` list; an unexpected MUST-level one fails the driver | — |
| P10-29 | Four stacked PRs (contract and invariant suite; codec suite; packaging suite and gates; appendix B, coverage and close-out) | — |
| P10-30 | Ports from `ruby-sdk@5b17395` (`invariant_suite/`, `packaging_suite/`, `codec_suite/`, `APPENDIX_B.md`, `tools/appendix_b.rb`'s format) and `nodejs-sdk@54aeed4` (`tests/conformance/xcut/` case tables), each ported file citing its source; `tools/requirement_levels.rb` is already ported (`requirement-catalog.cs`, 8a) | — |

## Deviation Ledger

Entries for roadmap constraint 7. **No new specification deviation and no new §10 entry** (P10-26). The split rows rest on existing
entries: `XCUT-1` and `XCUT-13`'s interrupt-flag clauses on §10 entry 8; `XCUT-4`'s I/O-family clause on §10 entry 7 and §11 item 23;
`XCUT-23`'s discovery half on §10 entry 9 and §11 item 8; `XCUT-20` on §11 item 37; `NFR-1`'s compile-only facade on §10 entry 1;
`NFR-2` on §10 entry 2. Two readings are new and proposed for §11: `XCUT-8`'s factory (P10-24) and the `NFR-11` scan's subject (the
assembly, P10-11). One SHOULD clause is declined for v1 (`XCUT-24`'s error snapshot, P10-23). Departures from the design's text, each
a close-out correction: §9.3's Stryker.NET schedule (P10-21) and `benchmarks/` project (P10-22); §9.2's `NFR-11` scan over
`PublicAPI.Shipped.txt` (P10-11); §9.2's "assert every shipped rule file is present" read as assembly metadata (P10-10). Departures
from the roadmap card: the starting counts are superseded by measurement; the card's "invariant suite, over all 24 XCUT IDs" holds
together with the transport suite's existing `XCUT` assertions (P10-6) rather than by duplicating them. One departure from the
styleguide: 11.8's optional mutation gate (P10-21), recorded as a knowledge note.

## Validation resolution (2026-10-13)

An independent validation of this design and its plan (13 findings, F1 to F13: 2 high-confidence structural, the rest medium or low, each reproduced)
was resolved in both documents on 2026-10-13. Each fact was re-verified against the tree first. One line each; the plan follows the design, and
where this section and an earlier paragraph disagree, this section wins.

- **F1** (the `ConcurrentDictionary` type ban flags 12 lines, not one pragma's worth, and the inner `TryRemove` pair re-enables the rule): fixed by **banning the
  constructors** instead of the type (G, P10-18, plan R7 and task 1.12). Measured: the type ban reports `BoundedMap.cs` lines 18 (cref), 49, 61, 81, 88, 103, 110 twice,
  123, 139, 154, 171; a constructor ban reports line 49 only (scratch build; the bare `#ctor` ID is the parameterless overload alone, so the seven public overloads are
  listed). One scoped pragma at line 49; the `TryRemove` pair stays. The file-scoped alternative was rejected because it would widen the `TryRemove` exemption to the file.
- **F2** (`AssertionCatalogueTests.Every_control_names_an_assertion_that_exists` requires every control's IDs to be in `TransportSuite.Assertions`): task 1.3 edits it to
  compare against the union of all kit catalogues (`KitCatalogues.All`, grown by 2.1 and 3.1) and adds a cross-suite name-uniqueness check; the "every existing kit test unedited"
  claims in 1.1 and 1.13 now name the exact edits.
- **F3** (V-conf names projects that have no `Conformance` class until 1.10 and 2.5; MTP exits 8 on zero tests): V-conf is defined per PR: SystemNet, DI and Conformance.Tests
  from the start, Core.Tests from 1.10, the STJ tests from 2.5; task 0.1's baseline uses the first three.
- **F4** (the coverage script reads `docs/work/**/*-checklist.md` but 4.6 drafted the checklist in the inbox, so 4.6 and 4.9 would read 604 of 645): the checklist is written
  directly at `docs/work/mvp/phase10/` in 4.6; 4.9 depends on 4.3 and 4.6; the design and plan are what 4.10 files.
- **F5** (`DrawCnonce` has a default in one place and none in another; the core driver cannot supply the Digest source; an output test cannot tell a CSPRNG from `System.Random`):
  `DrawCnonce` is an **optional** hook (null = draw through the public `Authorize`); `xcut-21` asserts format and no-repeat only; the CSPRNG-source clause is cited by reference to
  `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator`; its controls fail on width and on repetition, not on "predictability" an output cannot show. The
  no-default hooks are **four**, not five (V2, C, P10-4, Q2, sketch, catalogue row; plan R3, R13 and new R17, tasks 1.3, 1.9, 1.10); the core driver supplies three internal hooks.
- **F6** (`SERDE-4`, `-13`, `-18` judge core code, so their controls cannot be built through `CodecSubject.CreateSerde`): **moved to by-reference, per ID**: `SERDE-4`
  (core's `SerdeExtensions.Serialize(ISerde, Span<byte>, T)`; `ISerde.Serialize` returns `void`, no offset to get wrong) to `SerdeProfileTests.cs`; `SERDE-13` (core's `SerdeValues` and the
  typed readers) to `ReadValueTests.cs` and `SerdeValuesTests.cs`; `SERDE-18` (`Tristate<T>.Present` rejecting `null`, no bypass), with `-14` and `-30`, to `TristateTests.cs` and
  `TristateSentinelTests.cs`. Redefining any over a codec-observable property would restate `SERDE-1`/`-9`/`-15`/`-16`, so no redefinition was chosen. **Counts now:** codec suite
  **11** assertions (was 14); invariant 28; packaging 8; transport 43 unchanged. **Appendix B tally: 17 `suite`, 8 `restated`, 36 `by reference`** (was 20/8/33): B.3.2, B.3.4 and B.3.5 take the
  least portable evidence of their items and become `by reference` with both the `codec:` and the core-test references. The 61-row map, the 276-ID gate and the 41-row checklist
  are unchanged.
- **F7** (`.editorconfig` has three `dotnet_code_quality.*` lines, not one): all three are named (52, 155, 156). Consecutive settings are one block sharing one comment; lines 155 and 156 share the
  block above 155 and gain one `Re-enable when: never for src/`; **line 52 is exempted by name as a hand-off rule, not a relaxation**, and the test pins the allow-list to that one key (F; plan R6, 3.9).
- **F8** (the cross-path red applied the flag to one pack only, so it proves nothing): the flag is applied to **both** packs; the same-directory baseline must pass and the cross-path run must
  fail (task 3.11, F). Verified by the validator: one directory both flagged identical; two worktrees both flagged differ; two worktrees unflagged identical.
- **F9** (a body exceeding `AssertionTimeout` is `Failed`, not `Errored`): task 1.1's `GenericRunnerTests` expects `Failed`, as `SuiteRunnerTests` pins for the transport.
- **F10** (task 1.1 edits two existing test files that R1 says do not change): R1 and 1.1 now state the edits: additions only to `SuiteRunnerTests` and `ConformanceAssertionTests`, plus the
  one rule of F2; the "unedited" claims are narrowed to "no existing test body edited" except that rule.
- **F11** (the transport catalogue cites only `XCUT-1`, `-2`, `-4`, `-22`): corrected in C (P10-6) and the `XCUT-13` and `XCUT-18` rows; `transport-16` and the outbound header assertions are evidence
  by reference only and no ID is added to the transport catalogue (checked: `transport-3`, `-4`, `-20`, `-15` are the only assertions citing an `XCUT` ID).
- **F12** (V12's "19 sites" and the NFR-11 allow-list): V12 corrected to 43 `#pragma warning disable` lines in 31 files (13 `CA1031`, 6 `CA1727`, 6 `CA2000`, 2 `CA5351`, 1 `CA5394`, 15 `RS0030`),
  all with why-comments; the NFR-11 scan adopts the plan's explicit ban list (`System.Net.Http`, `System.Text.Json`, `Microsoft.Extensions.DependencyInjection`) beside the allow-list (V10, F, P10-11).
- **F13** (`RepositoryRoot` is created in 3.8 but needed by 3.5; `CiWorkflowTests` must allow `if: always()`): `Gates/RepositoryRoot.cs` is created in 3.5; `Only_the_documented_gates_are_conditional`
  allows `if: always()` on "Upload test results" beside the Linux-only steps.

**Readings fixed at the validator's request:** plan R6 (F7), R7 (F1) and R13 (F5, with the new R17); the other readings were accepted unchanged.

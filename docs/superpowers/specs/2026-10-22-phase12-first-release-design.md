# Phase 12 — First Release: Design

**Status:** Draft, for review. Written 2026-10-22 against `main` at `6b49af6` (phases 0 to 11 merged), on branch
`107-phase-12-first-release`, for issue #107. Brainstormed without a human in the loop: every judgement the brainstorming skill
would have put to the lead is taken here as a numbered decision (`P12-n`, see [Decisions](#decisions)); the ones that are the lead's to
make are marked **lead ruling — recommended default**, with the alternative and its cost, so the plan can proceed on the default. The
scope authority is the roadmap's Phase 12 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md:1072-1116`), its Phase List
row 12 (`:276`), every Phase Status Note bullet addressed to 12 (phases 9, 10 and 11), `docs/first-release.md`, and the lead's publishing
stance for this phase (binding, quoted under [Prerequisites](#prerequisites)). The format follows the phase 11 design
(`docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-design.md`) and the phase 10 design.

**What this document is.** The phase design for 12, which has no sub-phases (P12-1). It holds the verified facts the phase rests on,
the decisions, the scope and the non-scope, a disposition for every open `docs/first-release.md` blocker and every item phase 11 carried
in, the shape of the release workflow and of NuGet trusted publishing, the versioning policy, the one-time public-surface rulings, the
documentation set, the risks, the exit criteria mapped to the roadmap card, and the phase's deviation ledger.

**What this document is not.** It is not the plan and not the checklist, and it edits nothing outside this file. Every correction it
owes is listed under [Corrections owed at close-out](#corrections-owed-at-close-out).

**The headline.** The first publish is **held**, by the lead's stance: there is no nuget.org account, organisation or `Dexpace` prefix
reservation yet. Everything that needs no registry proceeds. The phase builds an inert, reviewable `release.yml` (tag or manual
dry run; a separate trusted-publishing job behind a `nuget` environment), decides the one-time public surface and moves every
`PublicAPI.Unshipped.txt` into `Shipped`, writes the versioning policy, strong-names the five assemblies with a committed key, packs
symbols, writes the user documentation and builds housekeeping's `csharp` fence compiler. The pre-design probes also found **why the
`reproducible packages` CI job has been red** since phase 10: the `[LoggerMessage]` source generator emits
`Dexpace.Sdk.Http.SystemNet`'s two logging classes in a nondeterministic order, so that assembly's PDB, and through it the DLL's debug
identifiers, differ between two packs about one run in three (V6). Phase 11's toolchain hypothesis was not the cause. The fix is
small and comes first.

---

## Prerequisites

| Predecessor | Kind | State at `6b49af6` |
|---|---|---|
| **11**: `docs/deviations.md`, `LedgerAuditTests`, the one-time public-surface list in `docs/first-release.md`, the four unmet clauses | **dependency** (card "Entry") | Merged as #106 (squash `6b49af6`), **admin-merged before CI ran**. No CI run exists for this tree (phase 11 leftovers note, "Merged without CI"). |
| **10**: the gate set, `CiWorkflowTests`, the packaging suite, `reproducible-pack.sh`, `requirement-coverage.cs` | **dependency** (the release workflow reuses each gate) | Merged (#102); the `reproducible packages` job was red at merge (V6 explains it). |
| **0–9**: the five packages | **dependency** (what is released) | Merged. |
| **The lead's publishing stance** | **binding constraint** | Quoted below. |

The lead's stance, which this design applies: *we have no registry account or ownership yet. Apply the siblings' pattern: `release.yml`
triggered by a `v*` tag or a manual run (dry run by default) that runs a locked restore, build, test, pack with `SOURCE_DATE_EPOCH`,
package validation, and uploads the `.nupkg`/`.snupkg` as workflow artifacts; a separate publish job using NuGet trusted publishing
(`NuGet/login` OIDC, no long-lived key) behind a `nuget` environment, inert until the environment and the `Dexpace` ID-prefix
reservation exist, with a header comment saying so (Node style). The first publish is held in `docs/first-release.md` with its
conditions; `NFR-16`, `NFR-12`'s release half and `PackageValidationBaselineVersion` are held until it. Everything that does not need a
registry proceeds.*

The roadmap's entry condition ("every `docs/first-release.md` blocker has an owner") holds: every unticked line names phase 12, the
lead, or this register (V18).

## Governing documents, and the phase-start queries

Read for this design: `CLAUDE.md`; `docs/README.md`; the roadmap's cross-cutting constraints 1–10, Decisions D1–D3, the Phase 11 and
12 cards, the Maturity Definition, "How Phases Get Executed" and the Phase Status Notes for phases 9, 10 and 11;
`docs/first-release.md` (all of it); `docs/deviations.md`; design §2.1 and §2.3 (`02-project-and-solution-layout.md`), §9 and §9.2
(`09-toolchain-and-quality-gates.md`), §10–§12; the styleguide overlay (`docs/styleguide/README.md`) and styleguide 10.6–10.7
(`docs/styleguide/csharp/10-api-design.md:117-140`); the phase 10 checklist's `NFR-*` rows and *Open items for the lead*; the phase 11
design and its leftovers note (`~/Notes/dexpace-knowledge-hub/projects/dexpace/dotnet-sdk/phase11-leftover-findings.md`, read-only);
`.github/workflows/ci.yml`; `scripts/ci/*`; `Directory.Build.props`; `SECURITY.md`; `CHANGELOG.md`; `README.md`;
`.claude/skills/housekeeping/SKILL.md`.

Queries run (2026-10-22, SDK 10.0.401): `scripts/knowledge --origin note --brief` (27 notes; none is about the release, signing or
versioning; none contradicts a fact here); `--prefix-info NFR`; `--req NFR-3,NFR-4,NFR-12,NFR-14,NFR-15,NFR-16,NFR-17`. Two harvested
design chapters are reported stale (`02-…` and `09-…`, both re-dated by phase 11's as-built pass); this design reads those files
directly. `NFR-4` (SHOULD), `NFR-12` (SHOULD), `NFR-15` (SHOULD), `NFR-16` (SHOULD) and `NFR-17` (MUST) are read from
`docs/product-spec/20-non-functional-requirements-and-quality-bar.md:13`, `:37`, `:40`, `:41` and `:20`.

**Siblings** (each read at `origin/main`; local checkouts may be stale):

- `python-sdk@b045292` `.github/workflows/release.yml`: `push: tags: ["v*"]` or `workflow_dispatch` with `dry_run` default `true`
  (`:7-15`); a build job that pins `SOURCE_DATE_EPOCH` to the commit time (`:43-46`), builds every distribution, verifies one version
  train (`:64-65`) and uploads the artifacts (`:70-75`); a separate `publish` job (`:77-105`) that runs only on a push or a non-dry
  manual run (`:83-85`), behind `environment: pypi` (`:86-88`), with `id-token: write` only there (`:89-91`), trusted publishing with
  attestations (`:99-105`).
- `nodejs-sdk@54aeed4` `.github/workflows/release.yml`: a header that says the workflow is **inert** until a registry credential exists
  and lists the unmet prerequisites (`:1-29`); `docs/first-release.md` *Blockers* (`:81-104`): "Exercising the provenance path needs a
  real registry … Nothing short of a first real publish verifies it". Node's `docs/sdk-documentation/` has `architecture.md`,
  `quality-gates.md`, `errors.md` and the four `write-a-{transport,serde,response-handler,paging-strategy}.md` guides.
- `ruby-sdk@5b17395` `docs/first-release.md`: the ownership blocker "RubyGems ownership settled … and trusted publishing configured
  (OIDC-based, no long-lived API key committed anywhere)" (`:290-291`); *Release path* "Not yet defined", with `NFR-16` and `NFR-12`'s
  release half held on those two conditions (`:657-678`); *What v1 ships without* (`:336-351`). Its phase 10 design `R11`
  (`docs/work/mvp/phase10/2026-09-13-phase10-deviation-reconciliation-and-release-readiness-design.md:1015-1070`) walks the register
  with four dispositions (closed, narrowed, open with reason, outside the repository) and "does not mark `NFR-4` ✅ … there is no
  previous release tag". Ruby's `docs/sdk-documentation/` has `architecture.md`, `quality-gates.md` and `conformance.md`.
- `java-sdk@941cb74` `build-logic/src/main/kotlin/dexpace.published-module.gradle.kts`: publishes to a local staging repository
  ("CI must override this to publish to a real remote", `:65-71`); signing is required only when `CI=true` (`:74-83`), the
  "optional locally" split of `NFR-16`.

So the **pattern** is Python's (tag or dry-run dispatch; build job; separate publish job behind an environment with OIDC), the
**inertness and its header** are Node's, the **hold and its conditions** are Ruby's, and the **local/CI signing split** is Java's.

## Facts the design rests on

Each was run for this design on .NET SDK 10.0.401, Linux x64, against `6b49af6`, unless it says otherwise. Probe sources and outputs are
in the session scratchpad (`…/scratchpad/p12-spec/`), never in the tree; the probe worktree was removed.

| # | Fact | Consequence |
|---|---|---|
| V1 | `dotnet build Dexpace.Sdk.sln -c Release`: 0 errors. `dotnet test --solution Dexpace.Sdk.sln -c Release --no-build`: **7 687 tests, 7 664 passed, 23 skipped, 0 failed** (the leftovers note's numbers for the PR tip). The housekeeping probe: "no drift found". `requirement-coverage.cs --report`: MUST rows N/A 22, ⏳ 1, ✅ 505, 🚫 4; SHOULD ⏳ 1; MAY ⏳ 3; design §12's deferred column lists `HTTP-22`, `BODY-36`, `REDIR-27` (declined MAYs, owner `first-release.md`), `NFR-16` and `NFR-17`. | The baseline is green locally. The only open MUST is `NFR-17`. |
| V2 | **No CI run exists for `6b49af6`.** Reading one is a remote action this design may not take. | Every "CI-only" claim below is unproven until the lead reads the first run (P12-21). |
| V3 | Five libraries, all `0.0.1-alpha.1` (`Directory.Build.props:75-76`); `PublicAPI.Shipped.txt` holds only `#nullable enable` in four of them and is **0 bytes** in `Dexpace.Sdk.Extensions.DependencyInjection`; the `Unshipped` files hold 1 370 (Core), 476 (Conformance), 40 (SystemNet), 28 (DI) and 13 (SystemTextJson) lines. | The move to `Shipped` must also give DI's `Shipped` its `#nullable enable` header. |
| V4 | Probe: copying every `Unshipped` into `Shipped` and leaving `Unshipped` as `#nullable enable` builds the solution with **0 warnings, 0 errors**; `NFR4ArchitectureTests.Shipped_is_empty_or_nullable_enable_only_until_phase_12` (`tests/Dexpace.Sdk.Core.Tests/Architecture/NFR4ArchitectureTests.cs:27-33`) then **fails**, naming all five files. | The move is mechanical; the test is rewritten in the same commit (P12-8). |
| V5 | Probe: `dotnet pack … -p:PackageValidationBaselineVersion=0.1.0` fails at restore with `NU1101: Unable to find package Dexpace.Sdk.Core. No packages exist with this id in source(s): nuget.org` (and the same for `Dexpace.Sdk.Serialization.SystemTextJson`). | The baseline cannot be set before a publish (it would break every build), and the two IDs are unclaimed on nuget.org today. The prefix reservation itself is not readable without an account (unverified). |
| V6 | **The reproducible-pack failure, reproduced.** `scripts/ci/reproducible-pack.sh` at `6b49af6` (unmodified, clean tree) failed **1 of 5** runs, and a variant with symbol packages failed **2 of 3**, and a further loop failed on its 4th run; every failure is the same: `Dexpace.Sdk.Http.SystemNet.0.0.1-alpha.1.nupkg differs … entry ./lib/net10.0/Dexpace.Sdk.Http.SystemNet.dll differs`. The two DLLs are the same size and differ in 72 bytes: the MVID, the CodeView PDB GUID, the PE time stamp and the PDB checksum, nothing else (`System.Reflection.Metadata` read of both). Building the project eight times with `EmitCompilerGeneratedFiles` gave **two different `LoggerMessage.g.cs` files** (6:2), identical but for the order of the two partial classes `HeaderDropLog` and `ProxyWarnings` (generator `Microsoft.Extensions.Logging.Generators` 10.0.14.42308). The project has `[LoggerMessage]` methods in two types (`src/Dexpace.Sdk.Http.SystemNet/HeaderDropLog.cs:35`, `:123`, `:129`, `:136`; `ProxyWarnings.cs:19`, `:60`); every other library has at most one (`src/Dexpace.Sdk.Core/Internal/ProxyResolutionLog.cs:18`, `:36`). | The CI job's redness is this generator nondeterminism, carried into the PDB (`EmbedUntrackedSources`, `Directory.Build.props:85`) and through the PDB checksum into the DLL. Hypothesis (a) of P11-22 was not the cause. Fix: one `[LoggerMessage]` type per assembly, held by a test (P12-5). |
| V7 | Phase 11's local reproducible-pack pass was a sample, not a proof: at `6b49af6` the same script passes most runs (four of five above). | A single green run of the job proves nothing; the fix is judged by the generator output (deterministic by construction), not by a run count. |
| V8 | No package carries symbols: no `IncludeSymbols`, `SymbolPackageFormat` or `.snupkg` anywhere in the build (`grep`); `reproducible-pack.sh:132` already compares `*.snupkg` when present; `packaging-conformance.cs:49` reads `*.nupkg` only and `dependency-audit.cs:170` globs `{library}.*.nupkg`, neither matching a `.snupkg`. With `IncludeSymbols` + `SymbolPackageFormat=snupkg` set, five `.snupkg` are produced and the four packages without the V6 defect compare byte-identical across two paths, symbols included. | Symbol packages are safe to add once V6 is fixed (P12-11). |
| V9 | **Strong naming, measured.** With a probe key and `SignAssembly` on every project: `CS1726` (*"Strong-name signed assemblies must specify a public key in their InternalsVisibleTo declarations"*) for every library's `InternalsVisibleTo`. With the `InternalsVisibleTo` items given the public key (`Key` metadata) and the same key signing the test projects: the whole solution builds with **0 warnings, 0 errors**, the packed `Dexpace.Sdk.Core.dll` carries a 160-byte public key and the `StrongNameSigned` CLR flag, and `scripts/ci/packaging-conformance.cs` is **GREEN (8 of 8)** over the signed packages, because `nfr-3.no-internals-granted` already compares only the name before the first comma (`src/Dexpace.Sdk.Conformance/Assertions/AssemblyPackagingAssertions.cs:34-35`). No `CS8002` appeared: every third-party reference is strong-named. | Strong naming costs a committed key, one property set and the `Key` metadata on five `InternalsVisibleTo` items; no gate changes. |
| V10 | `InternalsVisibleTo`'s public key must use `CALG_RSA_SIGN` (`0x2400`) as the key algorithm: a key blob exported as `CALG_RSA_KEYX` (`0xA400`) produces `CS0281` ("the public key of the output assembly does not match"). The SDK has no cross-platform `sn -p`. | The plan derives the public key once, commits it as a property, and pins it with a test (P12-10). |
| V11 | About fifteen validating `init` accessors still report `ParamName` `"value"` for a range or format rejection (phase 11 fixed only the null rejections): `ProxyOptions.cs:53`, `:69`, `:82`; `RetryOptions.cs:91`, `:125`, `:204`, `:232`, `:237` and the helper `:293`; `RequestOptions.cs:148`, `:158` (helpers called from `init`); `SystemNetHttpClientOptions.cs:85`, `:101`; `AuthRequirement.cs:57`; and the kit's `TransportSubject.cs:41`. The other hits of `nameof(value)` are methods whose parameter really is `value` (`MediaType.Parse`, `Protocol.Parse`, `ETag.Parse`, `Query`, `QuerySplice`, `AsciiFold`, `ServerSentEvent`'s private helpers). The existing tables are `tests/Dexpace.Sdk.Core.Tests/Configuration/InitAccessorParamNameTests.cs` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/InitAccessorParamNameTests.cs`. | `ParamName` is observable; it is fixed before the surface is frozen (P12-14). |
| V12 | `InstrumentationContextTests` run alone ten times: 0 failures (15 tests each). The full suite (V1): 0 failures. The leftovers note's 2 of 10 failures were under full-suite load at `7919190`. | Not reproduced; routed to the CI read (P12-22). |
| V13 | `SECURITY.md:5-8` says "The three packages under `src/` are built … at `0.0.1-alpha.1`"; there are five. `README.md:26-27` says "What is not built yet — the deviation audit (roadmap phase 11) and the release path (phase 12)". The housekeeping probe does not read either. | Both are stale; the docs task corrects them. |
| V14 | `docs/sdk-documentation/` has 20 pages; `conformance.md` already states, per suite, what a green run does not prove (`:23`, `:307`, `:413`, `:448`); there is no `architecture.md`, `getting-started.md`, `quality-gates.md` or `write-a-*.md`. `docs/architecture.md` is marked superseded and "kept until roadmap phase 12 writes `sdk-documentation/architecture.md`" (`docs/README.md:21`). | The documentation set is eight new pages and one consolidated section (P12-17). |
| V15 | 71 ` ```csharp ` fences in `README.md`, `docs/sdk-documentation/*.md` and `src/*/README.md`; **7** name `Dexpace.Sdk` and 5 have a `using Dexpace.Sdk…` line. The fence check described in `.claude/skills/housekeeping/SKILL.md:206-225` (compile every fence that names a `Dexpace.Sdk` namespace against the built `src/*/bin/Release/net10.0/*.dll`, warnings as errors) would compile those 7 today. | The checker is cheap but nearly vacuous over today's pages; the new pages are written with complete fences (P12-18). |
| V16 | `CiWorkflowTests` reads `.github/workflows/ci.yml` only (`tests/Dexpace.Sdk.Conformance.Tests/Gates/CiWorkflowTests.cs:87`). `ci.yml:31-32` lists "Package-validation baseline … phase 12" and "Strong naming and author signing (NFR-16): phase 12" as inert gates. | A `release.yml` needs its own pinning test (P12-4); `ci.yml`'s header is corrected. |
| V17 | `LedgerAudit` reads `docs/first-release.md`'s `### Unsatisfied MUSTs` heading and its list against the ledger's unmet-clause rows (rule L6, `tests/Dexpace.Sdk.Conformance.Tests/Gates/LedgerAudit.cs:317-357`). `requirement-coverage.cs:120-124` requires a ⏳ MUST to name its owner (a `first-release.md` entry or a plan task). | Editing `first-release.md` keeps that heading and list shape; a fifth unmet clause (P12-20's alternative) is a ledger row and a register line in one commit. |
| V18 | `docs/first-release.md` has **13** blockers, **2** ticked (nine P0 defects, deviations audited) and **11** open (`:37-103`); *Release path* (`:257-265`) says "A `v*` tag triggers … Nothing else triggers it"; *After the first publish* (`:267-272`) holds the baseline and `NFR-12`/`NFR-16`. | Every open blocker is dispositioned below. |
| V19 | `nuget.config` clears every source and names only `https://api.nuget.org/v3/index.json`; `origin` is `https://github.com/dexpace/dotnet-sdk.git`. The repository's visibility (public or private) is not readable without a remote call. | Attestation availability depends on visibility (unverified, P12-13). |
| V20 | Ruby's SSE event cap is `MAX_EVENT_BYTES = 8 * 1024 * 1024` (`ruby-sdk@5b17395 gems/dexpace-core/lib/dexpace/sse.rb:48`), the same default as this port's `ServerSentEventReader.DefaultMaxEventBytes = 8388608` (`src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt:1283`). | The 8 MiB default phase 11 flagged "to check" is at parity (P12-15). |

**NuGet trusted publishing, from knowledge, not re-read from the source.** I could not re-read nuget.org's documentation from this
session, so the mechanics below are my knowledge, with my confidence:

- nuget.org trusted publishing (GA in 2025): an account or organisation owner creates a **trusted publishing policy** naming the GitHub
  repository owner, the repository, the workflow file name (`release.yml`) and optionally an environment (`nuget`). *High confidence.*
- The workflow job needs `permissions: id-token: write`, runs `NuGet/login@v1` with the nuget.org **profile name** (not an email) as
  `user`, and gets back a **short-lived API key** (about an hour) as the step output `NUGET_API_KEY`, which `dotnet nuget push … --api-key
  ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json` uses. No long-lived key exists anywhere.
  *Medium-high confidence in the action's name, input and output names; the plan copies them from the action's README at authoring
  time and the header comment says so.*
- A policy for a private repository is temporarily active (about 7 days) until its first use binds it to the repository's and owner's
  immutable IDs. Whether the short-lived key may push a **brand-new** package ID under the owner is something I am **not sure of**; the
  register keeps the fallback the lead already wrote (a scoped key in a protected environment, recorded as such) for the first push
  only if it cannot.
- ID-prefix reservation is requested from nuget.org by its documented request process (by email to the nuget.org account team, at my
  last knowledge), granted to an owner, and shows the verified mark on the IDs. *Medium confidence in the channel; high that it is an
  owner-requested, nuget.org-granted act no repository change can make.*
- `dotnet nuget push *.nupkg` pushes a `.snupkg` lying beside each `.nupkg` to the symbol server. *High confidence.*
- nuget.org **repository-signs every package**, which inserts a `.signature.p7s` entry into the `.nupkg` it serves. *High confidence.*
  So the bytes on nuget.org are never the bytes the workflow pushed (P12-12).
- GitHub artifact attestations (`actions/attest-build-provenance`, permissions `id-token: write` and `attestations: write`) sign a
  build-provenance statement over each file's digest with the workflow's OIDC identity (Sigstore), verifiable with
  `gh attestation verify <file> --repo dexpace/dotnet-sdk`. Available on public repositories on every plan; on private ones only on
  GitHub Enterprise Cloud, at my last knowledge. *Medium-high confidence.*
- An environment referenced by a workflow that does not exist is **created on first use, with no protection rules**. *High
  confidence.* So "inert until the environment exists" cannot rest on the environment alone (P12-3).

---

## Scope and non-scope

**In scope (one phase, no sub-phases).**

1. **Determinism first.** The V6 fix and its gate, before anything else lands, so every later commit is judged by a reproducible-pack
   job that can be green.
2. **The one-time public surface.** Rule on every item of `first-release.md`'s list (P12-15), fix V11, then move every
   `PublicAPI.Unshipped.txt` into `Shipped` and rewrite `NFR4ArchitectureTests` (P12-8).
3. **Signing and symbols.** Strong naming with a committed key (P12-9, P12-10); symbol packages (P12-11); the attestation step in the
   publish job (P12-13, built and held).
4. **Versioning.** The policy page and the version bump (P12-6, P12-7).
5. **The release path.** `.github/workflows/release.yml`, its pinning test, the release preflight script with its self-test, the release
   notes gate (P12-2 to P12-4, P12-16).
6. **Documentation.** The eight pages, the consolidated "what a green run does not prove", `SECURITY.md`, `README.md`,
   `CONTRIBUTING.md`'s release section, `CHANGELOG.md`'s audit and draft release notes (P12-17, P12-19).
7. **Housekeeping's `csharp` fence compiler** and its CI step (P12-18).
8. **Close-out.** The checklist, `first-release.md` (ticks, holds, the rewritten *Release path*), dated corrections, the status note.

**Out of scope, explicitly.**

- **The first publish**, the nuget.org account, organisation, prefix reservation and trusted-publishing policy, the `nuget` environment's
  protection rules and variables, the `main` ruleset, and the confirmation that the `SECURITY.md` contact is monitored. Each is a human
  act outside the repository (the lead's), recorded in `first-release.md` with the condition that releases it.
- `PackageValidationBaselineVersion` (V5), `NFR-12`'s release half and closing `NFR-16`: held until the first publish.
- Author signing with an organisation certificate (P12-13's default declines it).
- A second transport, webhooks (D3, P12-23), `Dexpace.Sdk.Testing`, an OpenTelemetry package: post-v1, unchanged.
- New features or new requirement coverage. The phase owns no requirement beyond `NFR-4`'s, `NFR-12`'s, `NFR-16`'s and `NFR-17`'s
  release-path halves (Phase List row 12).
- Reading a CI run, pushing, opening a pull request, or any `gh` call: the lead's (P12-21).
- Deleting the stale local branches the leftovers note lists: the lead's housekeeping.

**What the deliverables are.**

| Kind | Deliverables |
|---|---|
| **Code, TDD** (a test that fails first) | The one-`[LoggerMessage]`-type gate and the SystemNet merge (P12-5); the `ParamName` fixes (P12-14); the rewritten `NFR4ArchitectureTests` (P12-8); the strong-name key, properties and its pinning test (P12-10); `IncludeSymbols` with the reproducible-pack self-test extended to a `.snupkg` (P12-11); `ReleaseWorkflowTests` (P12-4); `scripts/ci/release-preflight.cs` with `release-preflight-selftest.sh` (P12-4); `ReleaseNotesTests` (P12-16); the fence compiler in the housekeeping tool with its fixture pairs (P12-18); contingent fixes from the first CI read (P12-22) |
| **Workflow** (pinned by a test, not run by this phase) | `.github/workflows/release.yml` (P12-2, P12-3) |
| **Mechanical** | The `Unshipped` → `Shipped` move; the version bump (P12-7) |
| **Documentation** | Eight pages and one section (P12-17); `SECURITY.md`; `README.md`; `CONTRIBUTING.md`; `CHANGELOG.md`; `docs/README.md`; `CLAUDE.md`; `first-release.md`; the checklist; the roadmap status note; dated corrections to the design; knowledge notes |
| **Held** (recorded with its condition) | The first publish; the baseline; `NFR-12`'s release half; `NFR-16`'s closure; the prefix, organisation and policy; the environment's protection; the ruleset; the `SECURITY.md` confirmation |

---

## The release workflow (P12-2 to P12-4)

**Shape.** One file, `.github/workflows/release.yml`, two jobs, modelled on Python's and carrying Node's inertness header.

```yaml
# .github/workflows/release.yml
#
# NFR-16 (SHOULD), NFR-12's release half and the first NuGet publish. THIS WORKFLOW IS INERT: nothing is published
# until all of the following exist, none of which a commit can create (docs/first-release.md, "Blockers" and "Release path"):
#   1. a nuget.org organisation owning the Dexpace.* ID prefix (reserved by nuget.org on request);
#   2. a nuget.org trusted-publishing policy for dexpace/dotnet-sdk, workflow release.yml, environment nuget;
#   3. the repository environment `nuget`, with required reviewers, and its variable NUGET_USER (the nuget.org profile name).
# Until then the publish job stops at its first step, naming what is missing. The build job runs on every trigger, so the
# release path is reviewable and exercised now (a manual run is a dry run by default), not improvised at the first tag.
name: Release
on:
  push:
    tags: ["v*"]
  workflow_dispatch:
    inputs:
      dry_run: { description: "Build and verify only; never publish.", type: boolean, default: true }
permissions:
  contents: read
concurrency: { group: release, cancel-in-progress: false }
jobs:
  build:      # checkout (full history), setup-dotnet from global.json, locked restore, build, test,
              # SOURCE_DATE_EPOCH = the commit time, pack twice at two paths (reproducible-pack.sh) and keep pack 1,
              # package validation (inside pack), dependency audit, packaging conformance, release preflight,
              # SHA256SUMS, upload "packages" (*.nupkg, *.snupkg, SHA256SUMS)
  publish:
    needs: build
    if: github.ref_type == 'tag' && (github.event_name == 'push' || inputs.dry_run == false)
    environment: nuget
    permissions: { contents: read, id-token: write, attestations: write }
              # guard (vars.NUGET_USER set, else fail naming docs/first-release.md), download, verify SHA256SUMS,
              # attest-build-provenance over *.nupkg and *.snupkg, NuGet/login, dotnet nuget push --skip-duplicate
```

The sketch fixes the shape. The plan writes the steps, and `ReleaseWorkflowTests` pins them. The pinned facts:

1. **Triggers.** The workflow has exactly two triggers: a `v*` tag push, and `workflow_dispatch` with a boolean `dry_run` that defaults to
   `true`. This departs from the card's "triggered by a tag only" (`:1079`), at the lead's instruction (ledger P12-D1). **Only a tag
   publishes.** Two kinds of run publish: a tag push, and a manual run that is started on a tag ref with `dry_run` set to false. A
   manual run on a branch never publishes, whatever `dry_run` says. That is stricter than Python (`:83-85`), and the reason is
   `NFR-12`: its release half rebuilds the published package from its tag, so a publish with no tag would leave nothing to rebuild.
2. **Least privilege.** The top-level permissions are `contents: read`. `id-token: write` and `attestations: write` appear only on
   `publish`. No `secrets.*` reference names an API key, because the key comes from `NuGet/login`.
3. **The build job is the release candidate.** The job runs:
   - a locked restore, a Release build and the full test suite on `ubuntu-latest` (the three-OS matrix and the NativeAOT smoke are CI's
     job on `main`);
   - `reproducible-pack.sh artifacts/release`, which packs the commit twice at two paths with `SOURCE_DATE_EPOCH` set to the commit
     time and `ContinuousIntegrationBuild`, and fails on any difference. Package validation runs inside each pack.
   - the dependency audit and packaging conformance over pack 1;
   - the release preflight;
   - an upload of pack 1 with a `SHA256SUMS` file.

   So what is uploaded, and later published, is a pack whose rebuild at another path has already been shown byte-identical.
4. **The publish job is inert until the conditions hold (P12-3).** Its first step fails, naming `docs/first-release.md`, when
   `vars.NUGET_USER` is empty. The environment's existence cannot be the guard: a referenced environment is created on first use with
   no protection rules. Without a nuget.org policy, `NuGet/login`'s token exchange fails as well, so nothing can reach the registry
   either way.

   Once the guard passes, the job:
   - downloads the artifact;
   - verifies `SHA256SUMS`;
   - attests every file;
   - logs in;
   - pushes with `--skip-duplicate`. Versions are immutable, so a re-run after a partial push completes the set and cannot change a
     package that is already published.
5. **The tag must point at `main`.** The preflight refuses a tag whose commit is not an ancestor of `origin/main`. Together with the
   ruleset (`NFR-17`), this makes every released commit one that CI has passed on all three operating systems.

**The release preflight** (`scripts/ci/release-preflight.cs`, a file-based app like the other `scripts/ci/*.cs`, with
`release-preflight-selftest.sh` proving each refusal as `coverage-gate-selftest.sh` does). Over a package directory and an optional tag,
it fails, naming the cause, when any of these holds:

- the five packages are not all present, at one version;
- a `.nupkg` has no `.snupkg` beside it;
- a packed assembly is not strong-name signed with the committed public key token;
- a tag is given and its version is not the packages' version;
- a tag is given and some `PublicAPI.Unshipped.txt` holds more than `#nullable enable`, so the release would ship members that are not
  recorded as shipped;
- a tag is given and `CHANGELOG.md` has no `## [<version>]` section;
- the tag's commit is not on `main`.

The same checks run on a dry run, with the tag-dependent ones reported but not failed. This is the "verify one version train" step of
Python (`:64-65`), and it gives `NFR-16` its testable clause: a release build fails an unsigned publication (P12-13).

**`ReleaseWorkflowTests`** (`tests/Dexpace.Sdk.Conformance.Tests/Gates/`, beside `CiWorkflowTests`, in the same style: it parses the
file and can fail over edited copies). It pins:

- the two triggers and the `dry_run` default;
- the permissions split;
- the publish condition and `environment: nuget`;
- the absence of any `secrets.*` API key and of `continue-on-error`;
- `SOURCE_DATE_EPOCH` and `--locked-mode`;
- the guard step, the attestation step and the preflight step;
- the header's "INERT" paragraph.

`CiWorkflowTests` is unchanged, and `ci.yml`'s header loses its two "phase 12" inert lines (V16).

## Signing (P12-9, P12-10, P12-13)

`NFR-16` has three candidate mechanisms. This design separates them:

- **Strong naming** is an assembly *identity*, not provenance (design §9.2: "not a security measure on .NET (Core)").
  - The case for it: a strong-named consumer that references an unsigned assembly gets `CS8002`, which is an error under that
    consumer's `TreatWarningsAsErrors`.
  - The case for deciding it now: once 0.1.0 has shipped, adding or changing a strong name changes every assembly's identity, which
    package validation's assembly-identity rule reports as a break. That is `CP0003` as I recall it; the plan verifies the code.
  - V9 measured the cost: small.
  - The default: sign, with the key committed at the repository root as `Dexpace.Sdk.snk`, as Microsoft's library guidance and design
    §9.2 both say.
  - `SignAssembly` and `AssemblyOriginatorKeyFile` go in `Directory.Build.props` for every project, because the friend test assemblies
    must be signed too (V9).
  - The public key goes in one property, `DexpacePublicKey`, read by every `InternalsVisibleTo`'s `Key` metadata (V10).
  - A test pins the token of each built library.
  - `AssemblyVersion` stays the SDK's default, `Major.Minor.Patch.0` from `VersionPrefix`. On .NET (Core) the loader accepts a higher
    version and there are no binding redirects, so the .NET Framework convention of pinning `AssemblyVersion` per major buys nothing for
    a `net10.0`-only package.
- **Repository signing** is applied by nuget.org to every package, whatever this repository decides (`first-release.md:92`).
- **Provenance, which is what `NFR-16` asks for** ("cryptographically signed for provenance, enforced on the release/CI path, gracefully
  optional locally").
  - The default: a GitHub artifact attestation over every `.nupkg` and `.snupkg` in the publish job, before the push. A failed
    attestation fails the job, and nothing is pushed.
  - The release preflight also refuses an unsigned (non-strong-named) assembly.
  - A local `dotnet pack` signs nothing beyond the strong name, and publishes nothing.
  - This is Python's model (PEP 740 attestations, `:99-105`) and Node's (`--provenance`), ported.
  - Author signing with an organisation code-signing certificate is declined for v1 (P12-13, lead ruling). The certificate costs money
    every year. Current CA rules keep the private key on hardware, which on CI means a cloud HSM and a signing service. An author
    signature also rewrites the `.nupkg` (another `.signature.p7s`). And the attestation already gives a verifiable, keyless link from
    the package digest to this repository's workflow at a commit.
  - The declined clause is recorded as a mechanism substitution, not counted: the outcome `NFR-16` protects, a consumer verifying who
    built the bytes, is delivered by the attestation.

`NFR-16` stays ⏳ until the first publish exercises the attest-then-push path (held). Nothing short of a real publish verifies it, as Node
recorded.

## Versioning (P12-6, P12-7)

The policy is written once, in a new user page, `docs/sdk-documentation/versioning.md`. `docs/first-release.md`'s *Release path* holds
the maintainer procedure.

- **Lockstep SemVer 2.0** (design §2.3). One `VersionPrefix` and `VersionSuffix` in `Directory.Build.props` stamps:
  - all five packages;
  - their dependencies on one another;
  - every assembly's `AssemblyInformationalVersion` (`NFR-14`, `NFR-15`).

  A release is all five packages at one version, even a package with no change.
- **The public API** is what the five `PublicAPI.Shipped.txt` files list. On top of that:
  - the documented behaviour of those members;
  - the conformance kit's assertion IDs and statuses;
  - the configuration keys in `DexpaceConfigurationKeys`;
  - the log event names and the span and metric names (`OBS-*`).
- **Before 1.0** (`0.y.z`):
  - A **minor** (`0.y` → `0.y+1`) may break the public API or documented behaviour. Each break is:
    - a `CHANGELOG.md` line marked **Breaking**;
    - a reviewed `PublicAPI` diff, where a removal is a `*REMOVED*` line in `Unshipped`;
    - from the first publish on, a reviewed `CompatibilitySuppressions.xml` entry for package validation against the baseline.

    Where it costs nothing, a removal is preceded by one minor with `[Obsolete]` (styleguide 10.6).
  - A **patch** never breaks either: fixes only, and additions only when they fix a defect.
  - Adding a conformance assertion is at least a minor, as `first-release.md:29` already says, because it can turn an adapter's green
    run red.
- **From 1.0**: strict SemVer. A break is a major, and goes through `[Obsolete]` first.
- **The next version is `0.1.0`** (P12-7, lead ruling). `main` carries the next version with a pre-release label: the bump writes
  `VersionPrefix` `0.1.0` and `VersionSuffix` `alpha.1`. The release commit (held) empties `VersionSuffix`, dates the `CHANGELOG.md`
  section, and is tagged `v0.1.0`. The commit after it opens the next train (`0.2.0`, `alpha.1`). A tag carrying a suffix, such as
  `v0.1.0-alpha.1`, is also accepted by the preflight, so the lead can choose to exercise trusted publishing with a pre-release first
  (the alternative under P12-7).

## The one-time public surface (P12-15)

Each item is a line of `first-release.md:68-77`. The ruling is the recommended default (lead ruling); nothing is shipped, so a reversal
is still free until the `Shipped` move.

| Choice | Ruling | Alternative and its cost |
|---|---|---|
| `ServiceRequestException` (and `ServiceRequestTimeoutException`) derive from `IOException`, not `SdkException` (P11-7) | **Keep.** `TRANSPORT-20` says "MUST be a subtype of the platform IOException"; the classifier reads `IRetryableError`, not the root (knowledge note on the taxonomy) | Root it in `SdkException`: a counted unmet MUST clause (the fifth, at the budget), and `catch (IOException)` misses a transport failure, which is the more common catch in .NET code |
| `ServiceResponseException` removed; `HttpResponseException`'s constructor throws `ArgumentNullException` and `ArgumentException` (P11-9) | **Keep** (`XCUT-8`) | Restore the type: public surface with no producer, which `NFR-3` argues against |
| A second open returns the same stream; an open after dispose throws (P11-8) | **Keep** (`BODY-14`; the BCL's `ReadAsStreamAsync` agrees) | — |
| `AddInbound` refuses a name with surrounding whitespace; `Request` refuses a non-canonicalizing `Uri`; `FromFile` refuses a Unix special file | **Keep** (`HTTP-19`, `XCUT-18`; V9 of phase 11; `BODY-11`). macOS confirmation is P12-22's | — |
| `TraceIdGeneration` (`W3C`, `Datadog`, `FormatDatadog`) and `WaitOrDisposeAsync` | **Keep.** `OBS-27` names the Datadog flavour, so the vendor name is the specification's | Move to an extension package: surface churn for a MUST row |
| `RetryOptions.PacingHeaders`, `DexpaceConfigurationKeys.PacingHeaders`; `AttemptHeaderName` may not be `Idempotency-Key` | **Keep** (`RETRY-21`, `RETRY-38`) | — |
| The SSE per-event cap: `DefaultMaxEventBytes` 8 MiB, the overloads, `ServerSentEventTooLargeException` | **Keep 8 MiB**: the same as Ruby's `MAX_EVENT_BYTES` (V20) and 8× the line cap | 1 MiB (equal to the line cap): refuses multi-line events Ruby accepts |
| Validating `init` accessors report their property in `ParamName`; an empty configuration value falls through (P11-17) | **Keep**, and **complete**: the ~15 range and format rejections of V11 are fixed before the move (P12-14) | Ship them as is: `ParamName` `"value"` becomes a compatible-forever wart |

No other surface change is proposed. The move happens after P12-14, so that `Shipped` is the decided surface and `Unshipped` is empty at
the phase's exit (`#nullable enable` alone).

## Documentation (P12-17, P12-18, P12-19)

New pages under `docs/sdk-documentation/`, each ported as the roadmap card says. They are "restated per .NET area", not copied
(constraint 10):

| Page | Source to port | Content |
|---|---|---|
| `architecture.md` | Ruby's and Node's `architecture.md`; design §1; the superseded `docs/architecture.md` | The six layers, the package graph, the seams, where each subsystem's page is. Replaces `docs/architecture.md`, which is deleted and its `docs/README.md` row removed |
| `getting-started.md` | — | Install the packages, build a client (with and without DI), make a typed call, handle errors (`HttpResponseException`, `IOException`), dispose. Every fence is a complete program (P12-18) |
| `example-end-to-end.md` | — | One worked SDK slice: an operation, a typed response handler, bearer auth, pagination over a `Link` header, retry and redirect on, the DI registration, logging; complete fences |
| `quality-gates.md` | Ruby's `quality-gates.md` | Every gate of design §9's table, what it enforces, the command to run it locally, and whether it blocks; the reproducible-pack lesson of V6 |
| `write-a-transport.md`, `write-a-serde.md`, `write-a-response-handler.md`, `write-a-paging-strategy.md` | Node's four guides | Implement the seam, run the matching conformance suite against it (transport, codec), with the waiver rules |
| `versioning.md` | — | The policy above, for consumers |
| `conformance.md` (new section) | Ruby's R11 note | One consolidated "What a green run does not prove": each suite's paragraph, the permanent waivers, `NotExercised` pins, and `APPENDIX_B.md`'s *by reference* rows |

Beside the new pages:

- **Stale text.**
  - `SECURITY.md` names five packages and the 0.x support policy (the latest minor only).
  - `README.md:26-27` is corrected.
  - `CONTRIBUTING.md` gains a short "Releasing" section that points to the register.
- **`docs/README.md`.** The new rows, and the "Today:" list updated.
- **`CHANGELOG.md`** (P12-19).
  - An audit that every merged feature pull request since PR #3 has its entry. This is a manual walk of `git log --first-parent main`,
    and it is recorded in the checklist.
  - A Phase 12 entry.
  - A **draft release-notes block** at the head of `[Unreleased]`, which becomes `## [0.1.0]` at the release commit. It holds the
    summary, the breaking-change digest that phase 11 asked to lead (`catch (SdkException)` no longer catches a transport failure, and the
    rest of that list), and **every item of `first-release.md`'s *What v1 ships without***. The register says "The release notes MUST
    state every item", and `ReleaseNotesTests` checks that each of those items' IDs or titles appears in the block (P12-16).

**The fence compiler (P12-18).** It is built as a new `fences` command of the housekeeping tool (`tools/Dexpace.Tools.sln`), exactly as
`SKILL.md:206-225` specifies:

- it extracts every ` ```csharp ` fence that names a `Dexpace.Sdk` namespace in `README.md`, `docs/sdk-documentation/*.md` and
  `src/*/README.md`;
- it writes each fence as top-level statements into a generated project under the scratch directory, through `Guard.AssertWritable`;
- that project references the built `src/*/bin/Release/net10.0/*.dll`;
- the project builds with warnings as errors.

It also has a per-fence opt-out: the info string ` ```csharp fragment `, which is counted and reported, never compiled. It has fixture
pairs, as every check does: a clean fence passes, a broken one fails naming page and line, and a fragment is skipped. It runs as a
blocking Linux step of `ci.yml`, after Build. `CiWorkflowTests`' gate table gains the row. The new pages are written so that their
fences compile. Each existing fence that names `Dexpace.Sdk` either compiles or is marked `fragment` with a reason (V15: 7 today).
`SKILL.md`'s "Deliberately not ported" section becomes "The fence check".

## Disposition of every open `docs/first-release.md` blocker

Dispositions as Ruby's R11 used them: **closed** by this phase, **decided** (the decision is made and the line ticks on it), **held** (outside the
repository, with the condition that releases it), **lead** (a human act the phase cannot take).

| Blocker (`first-release.md`) | Disposition | What phase 12 does; what ticks the line |
|---|---|---|
| CI green on every matrix row (`:37-44`) | **lead**, with the cause fixed | The V6 fix and gate are the phase's first commit (P12-5). The line ticks when the lead records the first green run on `main` after the phase merges, on all five jobs (P12-21). |
| The `main` ruleset requires the CI jobs, `NFR-17`'s merge half (`:45-47`) | **lead** | Not changeable from the repository. The default ruling (P12-20) is that the lead applies it before the first publish. If the lead declines, it becomes the fifth counted clause, one commit across `deviations.md` and this register (V17). |
| Every MUST dispositioned (`:53-57`) | **lead** (tied to the ruleset) | Open for `NFR-17` alone. It ticks with the ruleset line. |
| Conformance green (`:58-59`) | **lead** (tied to CI) | The kit runs in every CI row and in the release build job. It ticks with the CI line. |
| One-time public-surface choices (`:63-77`) | **decided** → **closed** | P12-15's rulings, P12-14's fixes, then the `Shipped` move (P12-8). It ticks at the move commit. |
| A reachable `SECURITY.md` contact (`:78-79`) | **lead** | The phase rewrites the stale text (V13) and recommends enabling GitHub private vulnerability reporting (P12-24). It ticks when the lead confirms the address is monitored. |
| NuGet ownership settled (`:80-86`) | **held** | `release.yml` is authored inert. The prefix, the organisation and the trusted-publishing policy are the lead's (P12-25). The line is rewritten with the exact setup steps and stays open. |
| Signing decided (`:87-92`) | **decided** → **closed** | Strong naming built (P12-9, P12-10); author signing declined in favour of attestations (P12-13). It ticks on the decision. `NFR-16`'s *closure* is held separately (*After the first publish*). |
| Versioning policy written (`:93-94`) | **closed** | `versioning.md`, the bump to `0.1.0-alpha.1`, the procedure in *Release path* (P12-6, P12-7). |
| User documentation (`:95-101`) | **closed** | The pages above, the fence check green (P12-17, P12-18). |
| `CHANGELOG.md` complete (`:102-103`) | **closed** | The audit, the Phase 12 entry and the draft notes (P12-19). |

Phase 12 is expected to **close 5** of the 11 (public surface, signing, versioning, documentation, changelog). It leaves **4 for the
lead** (CI, ruleset, every-MUST, conformance; the last two follow the first two) and **2 held or lead-confirmed** (`SECURITY.md`,
ownership). *Release path* is rewritten to the as-built workflow, and *After the first publish* gains the `NFR-12` reading of P12-12
and the order of the steps the hold releases (P12-25).

## Disposition of every item phase 11 carried in

| Item (leftovers note; roadmap status note 2026-10-21) | Disposition |
|---|---|
| **CI not run on `main` since `6b49af6`** | Lead action P12-21: read the first run of this branch's pull request, and the first run on `main` after it. The phase cannot read a run. Until the read, each "CI-only" fact (V2) stays unproven. |
| **The reproducible-pack fix unproven** | **Root cause found** (V6: the generator's class order), so the toolchain pin was not it. The fix and its gate come first (P12-5). The toolchain check stays: it is correct and cheap. The CI read confirms the fix. |
| **macOS `FromFile` (`TarWriter`) unverified** | Confirmed or refuted by the first macOS row of P12-21's read. If it is red, a contingent TDD task under P12-22 fixes it before the `Shipped` move, because `FromFile`'s refusal is part of the frozen surface. |
| **~15 `ParamName` `"value"` accessors** | Fixed, TDD, before the move (P12-14, V11). |
| **The Core flake family** (`InstrumentationContextTests`, `OperationSpanLifecycleTests`) | Not reproduced (V12; V1's full run is green). Routed to the CI read. A flake that shows there is a contingent task (P12-22) that fixes the race, never a skip or a retry attribute. |
| **`NFR-17`, the ruleset** | Lead action (P12-20). |
| **D3, webhooks** | Lead ruling P12-23: confirm post-v1. The entry is already under *Post-v1 packages*. |
| The release notes lead with phase 11's breaking changes | The draft release notes (P12-19). |
| `NFR-4`'s baseline, `NFR-12`'s release half, `NFR-16` | Held under *After the first publish*, as the stance says. `NFR-16`'s mechanism is built (P12-13). |
| The `CallKey` and `AddDexpaceClient<TClient>` triggers (phase 9) | Unchanged under *Post-release triggers*. |
| The release notes name the `ShouldHandle` guidance and the no-reload rule (phase 9 hand-off) | Both are in *What v1 ships without* or the DI page. The release-notes block names them (P12-16). |

## Decisions

| ID | Decision | Rationale | Lead ruling? |
|---|---|---|---|
| P12-1 | One phase, no sub-phases. The plan is a stack of commit groups, each ending green: (1) determinism and `ParamName`; (2) surface move, strong naming, symbols, version; (3) release workflow, preflight, gates; (4) documentation and the fence compiler; (5) close-out | Group 1 must land before anything is judged by the reproducible-pack job. The move must follow the surface fixes. The documentation needs the final surface for its fences | — |
| P12-2 | `release.yml` triggers on a `v*` tag or `workflow_dispatch` (`dry_run` default `true`). Only a tag ref publishes | The lead's stance; `NFR-12`'s release half needs a tag to rebuild from | — (stance) |
| P12-3 | The publish job's guard is `vars.NUGET_USER` being set, plus the nuget.org policy. The environment `nuget` gets required reviewers from the lead | A referenced environment is auto-created unprotected, so it cannot be the guard alone | — |
| P12-4 | `ReleaseWorkflowTests` pins the workflow, and `release-preflight.cs` (with its self-test) is a step in it | A workflow nobody runs rots. The pinning test and the preflight's self-test run in CI on every pull request | — |
| P12-5 | **One `[LoggerMessage]`-bearing type per `src/` assembly.** SystemNet's `HeaderDropLog` and `ProxyWarnings` generated methods move into one `static partial` class. A test fails on a second type in any library. It fails first, today, on SystemNet | V6: the generator orders classes nondeterministically. One class removes the order. A reproducible-pack run count could never prove it (V7) | — |
| P12-6 | The versioning policy as written above, in `versioning.md` | Design §2.3; styleguide 10.6; `first-release.md:29` | — |
| P12-7 | **The next version is `0.1.0`.** `main` moves to `0.1.0-alpha.1` in this phase, and the held release commit drops the suffix | `0.y` already says "unstable"; `0.1.0` is the number the register and design §2.1 proposed (`02-project-and-solution-layout.md:55-56`). **Alternative:** publish `0.1.0-alpha.1` first to exercise trusted publishing, attestation and `NFR-12`'s release half on a pre-release; it costs one more publish, and the preflight already allows it | **lead ruling — recommended default** |
| P12-8 | Move every `Unshipped` into `Shipped` in this phase (DI's gains `#nullable enable`). `NFR4ArchitectureTests` becomes: every library has both files; every `Shipped` is non-empty and starts with `#nullable enable`; the scan sees five libraries; it can fail. The "`Unshipped` settled at a tag" rule lives in the preflight | The stance; V4. Later changes show as `Unshipped` diffs against the 0.1.0 candidate, which is the point | — |
| P12-9 | **Strong-name** the five libraries (and the friend test projects) with a key committed at the repository root | Design §9.2; V9 measured the cost as small; adding it after 0.1.0 is an identity break. **Alternative:** decline. That needs a dated correction to §9.2, and strong-named consumers get `CS8002`; reversing it after the first publish is a break | **lead ruling — recommended default** |
| P12-10 | The public key is one property, `DexpacePublicKey`, used by every `InternalsVisibleTo`'s `Key`. A test pins each built library's public key token to the committed key's | V10: the key-algorithm trap and no cross-platform `sn` | — |
| P12-11 | Pack symbols: `IncludeSymbols` with `SymbolPackageFormat` `snupkg`. The reproducible-pack self-test gains a flipped `.snupkg` byte | V8; nuget.org's symbol server; `NFR-12` covers `.snupkg` once packed (`reproducible-pack.sh:6`) | — |
| P12-12 | **`NFR-12`'s release half is read** as: a rebuild from the tag reproduces the attested pre-push digests byte for byte, and the nuget.org copy equals them with its `.signature.p7s` entry removed. The comparison is entry by entry, because removing an entry rewrites the zip directory. The reading goes into *After the first publish*, and becomes a design §11 item only when it is first exercised | nuget.org repository-signs every package, so "byte-identical to the published artifact" is unsatisfiable as worded; this keeps the outcome. Adding a §11 item now would churn the ledger gate for an unexercised reading | yes (a reading) |
| P12-13 | **`NFR-16` by attestation, author signing declined for v1.** `actions/attest-build-provenance` in the publish job before the push; the preflight refuses an unsigned assembly | The certificate's cost and its HSM requirement; attestations are keyless and verifiable; they are the siblings' model. **Alternative:** an organisation code-signing certificate through a cloud HSM and `NuGetKeyVaultSignTool` or `sign`. That means a yearly certificate, a vault, and a signing step with a secret. **If the repository is private** and not on GitHub Enterprise Cloud, attestations are unavailable (V19), and `NFR-16` falls back to the repository signature, declined as a SHOULD in the register | **lead ruling — recommended default** |
| P12-14 | Fix every validating `init` accessor's range and format `ParamName` (V11) by extending the two `InitAccessorParamNameTests` tables first | Behaviour a consumer can assert; free now, a break later | — |
| P12-15 | Every one-time surface item is kept as built (the table above) | Each is a spec MUST or a measured safety bound | **lead ruling — recommended default** (`ServiceRequestException : IOException` is the one most worth a look) |
| P12-16 | `ReleaseNotesTests` holds the draft release-notes block to *What v1 ships without*: every listed ID or asymmetry name appears | "The release notes MUST state every item" (`first-release.md:107`) is otherwise a promise with no gate | — |
| P12-17 | The documentation set above. `docs/architecture.md` is deleted (its last reader goes with it) | Roadmap card; parity row "Docs" | — |
| P12-18 | The fence compiler as the housekeeping `fences` command, a blocking Linux CI step, with a ` ```csharp fragment ` opt-out | `SKILL.md`'s own design; V15 says opt-in by namespace alone would check 7 fences, so the new pages are written complete | — |
| P12-19 | `CHANGELOG.md`: an audit by merged PR, a Phase 12 entry, and a draft release-notes block at the head of `[Unreleased]`. Earlier entries are not rewritten | Keep a Changelog; the history is the record | — |
| P12-20 | `NFR-17`: the lead adds the `required_status_checks` rule (the five CI jobs, request text in the phase 10 checklist's *Open items* 1) before the first publish | The MUST's merge half. **Alternative:** decline it and count it as the fifth unmet clause, at the budget of five, which leaves no slot for anything the first CI read finds | **lead ruling — recommended default** |
| P12-21 | The plan's first task is a lead action: read the first CI run of the phase's pull request (all five jobs) and the first `main` run after the merge, and paste the result into the checklist | V2. No remote read is the agent's to take | lead action |
| P12-22 | Contingent tasks, written in the plan but executed only if the CI read shows them: macOS `FromFile`, a Core flake, any OS-specific red. Each is TDD and lands before the `Shipped` move if it touches the surface | The tree was merged without CI | — |
| P12-23 | D3: webhooks stay a post-v1 package with the trigger already in the register; no `Webhooks/` folder | Roadmap D3; neither sibling ships them. **Alternative:** a v1 phase for Standard Webhooks verification (design §7.4), which needs a new prefix of work with no requirement IDs | **lead ruling — recommended default** |
| P12-24 | `SECURITY.md`: keep `oaljarrah@dexpace.org` as the contact, add GitHub private vulnerability reporting as a second channel, and support the latest minor of 0.x only | The contact exists; a second channel reduces single-inbox risk. **Alternative:** a role address such as `security@dexpace.org`, which needs a mailbox the lead creates | **lead ruling — recommended default** |
| P12-25 | NuGet ownership: a nuget.org **organisation** `dexpace` owns the packages, reserves the prefix **`Dexpace.*`**, and holds the trusted-publishing policy (`dexpace/dotnet-sdk`, `release.yml`, environment `nuget`). The order the hold releases: organisation → prefix → policy → environment protection and `NUGET_USER` → ruleset → tag | One owner for all five IDs and for every future `Dexpace.*` package. **Alternative:** reserve only `Dexpace.Sdk.*`, which leaves a future non-SDK package unprotected | **lead ruling — recommended default** |
| P12-26 | The phase's checklist re-rows `NFR-4`, `NFR-12`, `NFR-16` and `NFR-17`. The earlier rows' ⏳ text satisfies the coverage script's carry rule. §12 is regenerated only if a resolved mark moves | `requirement-coverage.cs:120-129`; constraint 3 | — |

## Risks

| Risk | Mitigation |
|---|---|
| V6 is not the only cause of the red CI job (phase 10's log mentioned zip metadata) | The job's per-entry diagnostics stay. P12-21 reads them. If another entry differs, it is a contingent task (P12-22), not a waiver. |
| The merged-without-CI tree is red on Windows or macOS | P12-21 is the first task. Contingent fixes land before the move. |
| The trusted-publishing mechanics differ from my knowledge (action inputs, new-ID pushes) | The header comment says the steps are copied from the action's README when authored. The first-push fallback is already in the register. Nothing publishes until the lead walks P12-25's order. |
| The environment is auto-created unprotected | The guard is `vars.NUGET_USER` plus the nuget.org policy (P12-3), and the header names both. |
| The strong-name key is committed (public by design) | It is identity, not security (§9.2). Microsoft's guidance commits it. Nothing trusts it. |
| The `Shipped` move hides a later accidental removal | `RS0017` and `*REMOVED*` keep a removal a visible diff. The preflight refuses a tag with a non-empty `Unshipped`. |
| `first-release.md` edits break `LedgerAuditTests` L6 or the coverage owner rule | Keep the `### Unsatisfied MUSTs` heading and list shape (V17). Run both gates in every commit group. |
| The fence compiler finds broken fences in old pages | Each is fixed or marked `fragment` with a reason, in the same commit. The count is reported. |
| Attestations are unavailable (private repository) | P12-13's fallback is written into the register now, not discovered at the tag. |
| A tag is pushed before the hold lifts | The build job runs and the publish job fails at the guard: a red run, nothing published. *Release path* says not to tag before P12-25's order completes. |

## Exit criteria, mapped to the roadmap card

| Card (`:1072-1116`) | Phase 12 exit |
|---|---|
| Clear every blocker | Five closed by the phase; four tied to the lead's CI read and ruleset; two held or lead-confirmed (`SECURITY.md`, ownership), each with its condition (table above) |
| Decide the one-time choices, then move `Unshipped` into `Shipped` | P12-15 ruled; P12-14 fixed; every `Shipped` populated, every `Unshipped` `#nullable enable` only; `NFR4ArchitectureTests` rewritten and green |
| Author `release.yml` (tag only; locked restore, build, test, pack with `SOURCE_DATE_EPOCH`; package validation; signing; push) | Authored with a dry-run dispatch (P12-D1), pinned by `ReleaseWorkflowTests`, preflight self-tested; inert until P12-25 |
| Reserve the prefix, configure trusted publishing | **Held** (P12-25), with the exact steps in the register |
| Versioning policy; the next version | `versioning.md`; `0.1.0-alpha.1` on `main`; `0.1.0` at the release commit (P12-7) |
| Strong naming and author signing | Strong naming built (P12-9); author signing declined for attestation (P12-13) |
| User docs | Eight pages and one section; `docs/architecture.md` retired |
| Fence compiler | Built, blocking in CI |
| **Exit:** first publish happened or held with the reason | **Held**, the reason and conditions in `first-release.md` |
| **Exit:** `PackageValidationBaselineVersion` set | **Held** (V5: it cannot be set before a publish; P12-D2) |
| **Exit:** `NFR-12` release half and `NFR-16` closed or held | **Held**, with P12-12's reading and P12-13's built mechanism |
| (always) every gate green; no `Security` test loosened; the probe clean | Every local gate; the CI read (P12-21) |

## Open questions for the lead

1. The rulings marked above: P12-7 (version), P12-9 (strong naming), P12-13 (author signing), P12-15 (the surface, especially
   `ServiceRequestException : IOException`), P12-20 (ruleset), P12-23 (D3), P12-24 (`SECURITY.md`), P12-25 (organisation and prefix).
   The plan proceeds on the defaults.
2. **Actions only the lead can take:**
   - read the CI runs (P12-21);
   - apply the ruleset;
   - confirm the security contact and enable private vulnerability reporting;
   - create the nuget.org organisation, request the prefix, create the policy;
   - protect the `nuget` environment and set `NUGET_USER`;
   - say whether the repository is public (P12-13's fallback).
3. Whether phase 10's "zip metadata" diagnostics in the red job's log (never read) show anything besides V6's DLL entry.

## Corrections owed at close-out

- **`docs/first-release.md`.**
  - The blocker ticks and the rewritten lines in the table above.
  - *Packages*: the version column and the dated note.
  - *Release path*: rewritten to the as-built workflow and the P12-25 order.
  - *After the first publish*: P12-12's reading, plus the baseline and `CompatibilitySuppressions.xml` procedure.
  - *SHOULD declined*: only if P12-13's private-repository fallback fires.
- **The design, as dated corrections only.**
  - §2.3: the version policy, `0.1.0`.
  - §9's gate table: the signed-publications row, the fence check row, the release workflow.
  - §9.2: strong naming built; `NFR-16` by attestation, author signing declined.
  - §12: regenerated only if a mark moves.
- **The roadmap.** The Phase 12 card's dated correction (the dry-run trigger, the held exits); the Phase List row's design link; a dated
  status note.
- **Other files.**
  - `CLAUDE.md`: the "What is genuinely unbuilt" paragraph, the release path, strong naming, one `[LoggerMessage]` type.
  - `docs/README.md`: the new pages; `architecture.md` retired.
  - `README.md`, `SECURITY.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.
  - `ci.yml`'s header.
  - `.claude/skills/housekeeping/SKILL.md`.
- **Knowledge notes** (`docs/knowledge/notes/`, hand-written):
  - the `[LoggerMessage]` generator's class-order nondeterminism (V6);
  - the strong-name `InternalsVisibleTo` key-algorithm trap (V10);
  - nuget.org's repository signature and `NFR-12` (P12-12).
- **`docs/deviations.md`.** No new §10 entry is opened. Strong naming conforms to §9.2; author signing is a mechanism substitution
  recorded in §9.2's correction and this ledger. If P12-20's alternative is taken, the ledger gains the `NFR-17` unmet-clause row in the
  same commit as the register line.

## Deviation Ledger

Roadmap constraint 7's entries for this phase. **No new design §10 entry is opened.**

- **P12-D1.** `release.yml` also triggers on `workflow_dispatch`, a dry run by default. This departs from the card's "triggered by a
  tag only" (`:1079`). Publishing is still tag-only (P12-2).
- **P12-D2.** The card's exit "`PackageValidationBaselineVersion` is set to the published version" (`:1104`) is held, not met. The
  card gives that bullet no "or held" clause, but setting it before a publish breaks every build (V5). It is held with the publish,
  under the lead's stance.
- **P12-D3.** `NFR-16`'s author signing (design §9.2) is replaced by a GitHub artifact attestation (P12-13), a mechanism substitution
  that keeps the provenance outcome. It is not counted (P11-2).
- **P12-D4.** `NFR-12`'s release half is read modulo nuget.org's repository signature (P12-12). The reading is recorded in the register
  and becomes a §11 item at the first publish.

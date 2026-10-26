# First Release

This is the release-readiness register: everything the first NuGet publish must know that no phase owns. It is one
of this repository's two registers. The other is [`docs/deviations.md`](./deviations.md), the as-built audit of the deviation ledger, which phase 11 created (2026-10-20). The register is
indexed by the [v1 roadmap](./work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md), and every entry below names its
owner: a roadmap phase, or this file. When a blocker clears it is ticked, dated and pointed at its evidence, never
deleted. A finding is routed to its owner when it is found, not registered here, unless the owner is the release
itself.

**Nothing has been published.** This register was opened on 2026-09-27, against commit `d45e64b`:

- **Packages and versions.** Three packages exist, all at `0.0.1-alpha.1`: `VersionPrefix` `0.0.1` and
  `VersionSuffix` `alpha.1` in `Directory.Build.props`, stamped on every package in lockstep. There is no tag and no
  release workflow.
- **Build.** `dotnet build -c Release` fails with `NU1902`. That is a phase-0 task.
- **Security.** The gap analysis verified nine security and robustness defects in the merged code. That is phase 1.
- **Requirement coverage.** Of the 532 MUSTs in the specification, 137 are met at `d45e64b`.

## Packages

Design §2.1 lists the MVP set, and §2.3 fixes lockstep versioning: one `VersionPrefix`, and one coherent set per
release.

| Package | Exists? | Target framework | Published? | Owner of what is missing |
|---|---|---|---|---|
| `Dexpace.Sdk.Core` | yes | `net10.0` | no — `0.1.0-alpha.1` | built (phases 0–7, 10); phase 12 holds the publish |
| `Dexpace.Sdk.Http.SystemNet` | yes | `net10.0` | no — `0.1.0-alpha.1` | built (phases 1, 8b); phase 12 holds the publish |
| `Dexpace.Sdk.Serialization.SystemTextJson` | yes | `net10.0` | no — `0.1.0-alpha.1` | built (phase 7a); phase 12 holds the publish |
| `Dexpace.Sdk.Conformance` | yes (phase 8a) | `net10.0` | no — `0.1.0-alpha.1`; the first published version is the first release's lockstep version, `0.1.0` (decided by phase 12, P12-7: a release that adds an assertion is at least a minor version) | built; phase 10 completed it on 2026-10-14 (the invariant, codec and packaging suites beside the transport suite: four suites); phase 12 holds the publish |
| `Dexpace.Sdk.Extensions.DependencyInjection` | yes (phase 9) | `net10.0` | no — `0.1.0-alpha.1`; it references `Dexpace.Sdk.Core`, `Dexpace.Sdk.Http.SystemNet` and `Microsoft.Extensions.Http` (the hosting family's one root), at the lockstep version | built (phase 9); phase 12 holds the publish |

*Updated 2026-10-26 (phase 12):* the table reads the packages as built at the end of phase 12 (all five at `0.1.0-alpha.1`, every one `net10.0`; the `net8.0` column the register was opened with is gone, per the paragraph below). The first-publish
status is the blockers' below.

The target frameworks changed in phase 0. The lead approved the roadmap's decision D1 on 2026-09-28: every package
targets `net10.0` only, set once in `Directory.Build.props` (PR #21), because .NET 8 leaves support on 2026-11-10.

## Blockers before first publish

- [ ] **CI green on every matrix row.** The green build, format check, locked restore, coverage floor, `PublicAPI`
      files, package validation, dependency audit and 3-OS matrix are phase 0's; the complete gate set is phase 10's
      (built and dispositioned on 2026-10-14: the gate table in the [phase 10 checklist](./work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-checklist.md);
      `CiWorkflowTests` pins that every gate is a step and none is advisory). This line is ticked when the first CI run on `main` after phase 11 is green on every row.
      *Phase 11 (2026-10-20):* the `reproducible packages` job was red when phase 10 merged, and the run was not green on every row. Task 1.1 added a toolchain check (a job-local `global.json` with `rollForward: disable`, printing
      `dotnet --version` from inside each checkout) and a self-test (`reproducible-pack.sh --self-test`) on hypothesis (a), that the runner's SDK patch differs from `global.json`'s, **without reading the failing job's log** (a remote read);
      whether the job is now green is the first thing to read in the next CI run, and if it is red its `reproducible-pack:` lines name the entry that differs. Phase 11 also adds blocking steps: the dependency-audit self-test, the
      reproducible-pack self-test and `Section 12`, and the ledger audit runs in the existing `Test` step.
      *Phase 12 (2026-10-26), still open:* the cause is found and fixed. Hypothesis (a) was not it: the `LoggerMessage` source generator orders the `[LoggerMessage]` classes of one assembly differently from one build to the next, and `Dexpace.Sdk.Http.SystemNet` held two; every library now holds at most one (`TransportLog`), held by `LoggerMessageDeterminismArchitectureTests`, and eight builds give one `LoggerMessage.g.cs` hash. Locally `reproducible-pack.sh` reports 10 of 10 files byte-identical across two paths, symbols included, repeatedly. **No CI run has been read:** the implementation was done under a no-remote-action rule (nothing pushed, no `gh`), so the lead action L1 (read the first run of the phase 12 pull request on all five jobs, the `reproducible-pack:` lines, the macOS `FileRequestBodyTests` rows, any `InstrumentationContextTests` or `OperationSpanLifecycleTests` red, and whether the repository is public) is open, and so is the first green run on `main`. This line ticks when the lead records that run.
- [ ] **The `main` ruleset requires the CI jobs (`NFR-17`'s merge half).** A `required_status_checks` rule naming `build & test (ubuntu-latest)`, `build & test (windows-latest)`,
      `build & test (macos-latest)`, `reproducible packages` and `NativeAOT smoke`; design V9 found none (no classic protection either), so a red job does not block a merge.
      An admin-only repository setting that phase 10 did not apply (request text in the phase 10 checklist's *Open items*). Read it back with `gh api repos/dexpace/dotnet-sdk/rulesets` and tick this line.
      *Phase 12 (2026-10-26), still open (lead action L3, P12-20):* nothing in the repository can apply it. The default ruling is that the lead applies the rule before the first publish; the alternative, declining it, is a fifth counted unmet clause (one commit across `docs/deviations.md` and the list below, V17), which would spend the budget of five.
- [x] **The nine P0 defects closed, with permanent `Security` regression tests.** The defects are header CR/LF
      injection, a caller-set `Host`, transport auto-redirect with cross-origin credential forwarding, credentials
      over `http`, deny-list URL redaction, the attempt-leaking `PipelineContext`, the `Retry-After` overflow, the
      throw on 304, and the leak on a malformed `Content-Type`. Phase 1. *Ticked 2026-10-20 (phase 11), closed 2026-09-29:* each defect has its permanent `Security` test in the
      [phase 1 checklist](./work/mvp/phase1/2026-09-28-phase1-security-fixes-checklist.md), and the phase 11 audit added three more and edited none.
- [ ] **Every MUST dispositioned.** Every one of the 532 MUSTs has a checklist row: ✅ with a named test; N/A or
      vacuous with a design §10 or §11 citation; or listed below under *Unsatisfied MUSTs*. Phases 2–10 own the
      rows, and phase 10's row-count gate proves the total. **Phase 10 (2026-10-14) built the gate and it is green:** `dotnet run scripts/ci/requirement-coverage.cs -- --check` reports all 645 IDs of appendix C owned by a checklist row, each with its level and a legend mark,
      and no finding in a MUST row; it is a blocking Linux step of `ci.yml`. ~~**The line stays open** because the script's tally still reads 16 MUST rows as ⏳ in the row that owns them: `NFR-17` (the ruleset line above), and hand-offs whose closing evidence sits in a later phase's checklist in a table
      the script does not read as owning (the 12 `RECOV` rows `RECOV-17` to `-24`, `-27`, `-28`, `-29` and `-34`, which 4b's table leaves ⏳ and 6a's table closes, and `BODY-4`, `BODY-5` and `BODY-10`, 3b's hand-offs to 6a, 6b, 6c and 3a: `BODY-4` and `BODY-5` carry a 2026-10-08 correction under 3b's table, not in the row, and `BODY-10` rests on 3a's `HTTP-39` row). `HTTP-44` and `HTTP-45` are not among them: 7a's checklist owns them last, at ✅. Phase 11 gives those 15 rows their dated corrections and ticks this line; it also decides whether `NFR-17` is the only MUST left open.~~ *Phase 11 (2026-10-20):* the 15 hand-off rows have their closing rows in the [phase 11 checklist](./work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-checklist.md) (with the rows of every ID a repair changed, and the five SHOULD/MAY hand-offs the same measurement found), and the script's tally of resolved MUST rows reads ⏳ 1: `NFR-17`, the ruleset line above, which names this register as its owner. **The line stays open for `NFR-17` alone.** *Phase 12 (2026-10-26):* unchanged: the tally still reads ⏳ 1, `NFR-17`, and this line ticks with the ruleset line above.
- [ ] **Conformance green.** `Dexpace.Sdk.Conformance` passes against every shipped transport on every CI row, with
      waivers listed by requirement ID. It is written by phase 8a and completed by phase 10 (2026-10-14: the invariant, codec and packaging suites, with three first-party drivers that each pin their `NotExercised` list). *Phase 12 (2026-10-26):* green locally on this tree (the three drivers and the kit's own tests); it is also a step of the release build job. It ticks with the CI line, which is the first green run on every row.
- [x] **Deviations audited.** `docs/deviations.md` exists and every row reads `as-built: …`. Phase 11. *Ticked 2026-10-20 (phase 11):* [`docs/deviations.md`](./deviations.md) holds all 33 design §10 entries and 72 §11 readings, each with an
      `as-built:` verdict (or `retired (repaired, task n.m)`), a disposition and a cited reason, and `LedgerAuditTests` (rules L1 to L11, a blocking step of the `Test` job) fails the build on a cited line past the end of its file, a missing row, a scan that reads no file or a count that
      disagrees with this file. The clauses unmet on a stated domain are four (`AUTH-15`, `PAGE-21`, `TRANSPORT-14`'s name half and `CFG-2` for a section of an unregistered configuration; the first count of 2026-10-20 said three and missed the last, found by the 2026-10-21 review), below the roadmap's five.
- [x] **One-time public-surface choices decided before the first `PublicAPI.Shipped.txt`.** Adding a public member
      later is compatible; removing or narrowing one after the tag is a break. Phase 11 lists the choices here, and
      phase 12 decides them. ~~Candidates found so far (phase 10, 2026-10-14): whether `HttpResponseException(Response)` should refuse a non-error status (`XCUT-8`, P10-24;
      today every core mapping path goes through a guarded internal factory and the public constructor accepts a 200), and whether `ServiceResponseException`, public and never
      thrown (`XCUT-4`, P10-25), is deleted or argued into design §3.2.~~ *Both decided by phase 11 (2026-10-20): the constructor refuses `null` and a non-error status, and `ServiceResponseException` is deleted.*
      *Ticked 2026-10-26 (phase 12, P12-15):* every item of the list below was kept as built (the design's P12-15 table is the evidence, and `ServiceRequestException` stays rooted in `IOException`); task 1.2 completed
      the `ParamName` item (a rejected range or format value now names its property too, with a **Breaking** `CHANGELOG.md` line), and the move happened in the commit `feat: move PublicAPI.Unshipped into Shipped for the first release (NFR-4)`:
      each `src/*/PublicAPI.Shipped.txt` is `#nullable enable` plus the whole surface (Core 1 369 members, Conformance 475, SystemNet 39, DI 27, System.Text.Json 12), each `Unshipped` is `#nullable enable` alone, and
      `NFR4ArchitectureTests` now asserts the `Shipped` shape. The list as decided (each was in `PublicAPI.Unshipped.txt` and a `CHANGELOG.md` line):
      - `ServiceRequestException` (and `ServiceRequestTimeoutException`) derives from `IOException`, not `SdkException`; `catch (SdkException)` no longer catches a transport failure (P11-7, the choice most worth a second look).
      - `ServiceResponseException` is removed; `HttpResponseException`'s public constructor throws `ArgumentNullException` and `ArgumentException` (P11-9).
      - A second open of a response body returns the same stream; an open after dispose throws `StreamClosedException` (P11-8).
      - `Headers.Builder.AddInbound` refuses a name with surrounding whitespace; `Request` refuses a `Uri` built without canonicalization; `RequestBody.FromFile` refuses a Unix special file.
      - `Dexpace.Sdk.Core.Diagnostics.TraceIdGeneration` (`W3C`, `Datadog`, `FormatDatadog`) and `ResponseTaskExtensions.WaitOrDisposeAsync`.
      - `RetryOptions.PacingHeaders` (and `DexpaceConfigurationKeys.PacingHeaders`); `AttemptHeaderName` may not be `Idempotency-Key`.
      - The SSE per-event cap: `ServerSentEventReader.DefaultMaxEventBytes`, the `maxEventBytes` overloads of the reader's constructor, `ReadAll`, `ReadAllAsync` and `ServerSentEventStream.FromResponse` (the optional `maxLineBytes`
        parameters became overloads), and `ServerSentEventTooLargeException` (default 8 MiB, P11-19, the default to check).
      - Validating `init` accessors report their property in `ParamName`; an empty configuration value falls through to the next provider (P11-17).
- [ ] **A reachable `SECURITY.md` contact.** Phase 0 creates the file; this line is ticked only when the contact is
      confirmed to be monitored. *Phase 12 (2026-10-26), still open (lead action L4, P12-24):* `SECURITY.md` is rewritten (five packages, only the latest minor of `0.x` supported, the `oaljarrah@dexpace.org` contact kept, and GitHub private vulnerability
      reporting named as a second channel *when enabled*, because the file must not claim a setting only the lead can turn on). Ticked when the lead confirms the mailbox is monitored and enables private vulnerability reporting.
- [ ] **NuGet ownership settled.** **Held (phase 12, 2026-10-26):** `.github/workflows/release.yml` is authored and inert; every item below is outside the repository, and the lead's (L5, L6). The order in which the hold releases (P12-25):
      1. Create the nuget.org **organisation** `dexpace` (the owner of all five IDs and of every future `Dexpace.*` package). The alternative is reserving only `Dexpace.Sdk.*`, which leaves a later non-SDK package unprotected.
      2. Request the **`Dexpace.*` package-ID prefix** reservation for it.
      3. Create the **trusted-publishing policy** (nuget.org OIDC) for repository `dexpace/dotnet-sdk`, workflow `release.yml`, environment `nuget`. No long-lived API key is committed or stored as a secret anywhere:
         `NuGet/login` exchanges the publish job's OIDC token for a short-lived key. If trusted publishing is unavailable to the organisation at release time, a scoped key in the protected `nuget` environment is the
         fallback, **recorded here as such**; a first push of a brand-new package ID is the case to check at that point (not verified here).
      4. Create the GitHub environment **`nuget`** with required reviewers, and set its variable **`NUGET_USER`** (the nuget.org profile name). The workflow's first publish step fails naming this register while the variable is empty,
         because an environment referenced by a job is created on first use with no protection rules, so its existence cannot be the guard.
      5. Apply the `main` ruleset (the line above).
      6. Only then tag (below). **Do not tag before this order completes.**
      The workflow's action inputs (`NuGet/login`'s `user` input and `NUGET_API_KEY` output, `actions/attest-build-provenance`'s `subject-path`, `actions/download-artifact`'s inputs and the majors of all three) were written without
      network access and are **unverified against each action's README**; verify them before lifting the hold (the file's header says so).
- [x] **Signing decided.** Two decisions, both phase 12's:
      - Strong naming, with a committed key. Design §9.2 says keep it for strong-named consumers, not as a security
        measure.
      - Author signing with an organisation certificate (`NFR-16`).

      nuget.org's repository signature applies to every package regardless of these decisions.
      *Ticked 2026-10-26 (phase 12, P12-9, P12-10, P12-13):* **strong naming is built**: `Dexpace.Sdk.snk` (2048-bit RSA, committed, public by design), `PublicKeyToken` `fb3fcc3028a3989e`, applied to every project under `src/` and `tests/` (the tools stay
      unsigned), each library's `InternalsVisibleTo` keyed with `DexpacePublicKey`; `StrongNameKeyTests` pins the property against each built library and the token as a literal; the release preflight re-checks the token on every packed assembly.
      **Author signing is declined for v1:** a code-signing certificate costs money and wants an HSM, and `NFR-16` (SHOULD) rests instead on a GitHub artifact attestation over every package (`actions/attest-build-provenance` in `release.yml`'s publish job),
      verified with `gh attestation verify <file> --repo dexpace/dotnet-sdk`. That mechanism is built and held: `NFR-16` closes at the first publish (*After the first publish*), not here.
- [x] **Versioning policy written.** Lockstep SemVer. The pre-1.0 policy states what a `0.x` minor may break. The
      next version after `0.0.1-alpha.1` is phase 12's decision.
      *Ticked 2026-10-26 (phase 12, P12-6, P12-7):* [`docs/sdk-documentation/versioning.md`](./sdk-documentation/versioning.md) states the policy (what the public API is, what a `0.y` minor and patch may contain, **Breaking** `CHANGELOG.md` lines,
      `*REMOVED*`, `[Obsolete]` first, a conformance assertion is at least a minor, strictness from 1.0). The next version is **`0.1.0`**: `VersionPrefix` `0.1.0` with `VersionSuffix` `alpha.1` on `main`, so the packages build as `0.1.0-alpha.1`.
      Had the lead ruled to publish `0.1.0-alpha.1` first, only the release commit's suffix and the tag name (`v0.1.0-alpha.1`) change.
- [x] **User documentation.**
      - `docs/sdk-documentation/`, with one page per area, each written at its phase's close.
      - An architecture page, a getting-started page, and one end-to-end example.
      - The `write-a-{transport,serde,response-handler,paging-strategy}` guides.
      - A conformance page stating what a green conformance run does **not** prove.

      Phase 12 assembles these pages.
      *Ticked 2026-10-26 (phase 12, P12-17, P12-18):* the set is complete: `architecture.md`, `getting-started.md`, `example-end-to-end.md`, `quality-gates.md`, `versioning.md`, the four `write-a-*` guides and the conformance page's "What a green run does not prove" section, beside
      the per-area pages; `docs/architecture.md`, which they supersede, is retired. They cannot rot silently: `housekeeping fences` compiles every fence that names `Dexpace.Sdk` (21 compiled, 4 marked fragments, 0 failed) and is a blocking Linux CI step.
- [x] **`CHANGELOG.md` complete.** Phase 0 back-filled PRs #3–#9 on 2026-09-29, and every later phase adds its own
      `[Unreleased]` entry.
      *Ticked 2026-10-26 (phase 12, P12-16, P12-19):* the audit walked the 55 first-parent commits of `main` and back-filled the ten pull requests, in six groups, that had no entry (#11/#12, #20, #22, #26/#27/#34/#53, #35, #42; recorded in the checklist and under "Audit back-fill"); the Phase 12 section is
      written; and the head of `[Unreleased]` carries the draft release notes (summary, the breaking changes to read first, the `ShouldHandle` and no-reload guidance, and every item of *What v1 ships without*), which `ReleaseNotesTests` keeps complete. The draft becomes `## [0.1.0]` at the release commit.

## What v1 ships without

**The release notes MUST state every item in this section.** Today it holds only what is already decided. The
phases add the rest as they decline or defer something. Nothing below is a blocker.

### Unsatisfied MUSTs

Every candidate must first be argued in design §10 and then pass phase 11's audit. The roadmap's parity target is at most five clauses
unsatisfied or unmet on a stated domain, each one argued.

**Audited by phase 11 (2026-10-20), corrected by its review (2026-10-21): the count is four, inside the target.** [`deviations.md`](./deviations.md) re-derived every §10 entry from the source under the
counting rule P11-2 (a clause counts when the outcome it protects is not delivered on a stated domain; a mechanism substitution that keeps the outcome is argued there and not
counted). The four below are the only clauses that count, and `LedgerAuditTests` holds this list, the ledger's `unmet-clause` rows and its count line to one another. The seven
that phase 8b counted are resolved: entries 24 (`OBS-27`), 31 (`BODY-11`) and 33 (`TRANSPORT-2`) were repaired, 26 (`CFG-5` to `CFG-7`) is a stricter host rule that meets the
accessor letter, and 16, 18 and 32 stay; the review added `CFG-2`'s residual under entry 25, which the first count had written off as repaired. `TRANSPORT-2`'s line is struck through below; it is not unsatisfied.

- **`AUTH-15` (MUST), Digest MD5 and MD5-sess where the host's crypto refuses MD5.** .NET bundles no MD5; on Linux it is OpenSSL's, which a FIPS-enabled provider refuses.
  The handler probes once per process, treats any non-fatal failure as a refusal and drops the MD5 algorithms from its preference, so an MD5-only challenge is declined and a
  server offering SHA-256 is answered with it. A vendored MD5 would silently take a FIPS deployment out of its compliance boundary. The mapping has never run on a real FIPS host. Phase 6c
  (2026-10-08), re-audited by phase 11 (2026-10-20); design §10 entry 16.
- **`PAGE-21` (MUST), a byte-for-byte query splice for percent-encoded unreserved characters.** `Request.Url` is a `System.Uri`, which decodes `%7E` to `~` before the splice sees the query;
  the only byte-preserving API disables `Uri`'s validation, which puts CR/LF and the fragment on the wire. The residual is RFC 3986 section 6.2.2.2 equivalence of unreserved characters; reserved
  characters, value-less flags, empty segments, order and `+` are preserved. Phase 7c (2026-10-09), re-audited by phase 11 (2026-10-20); design §10 entry 18.
- **`TRANSPORT-14` (MUST), the malformed inbound header *name* clause.** `SocketsHttpHandler` rejects the whole response on a header name holding
  a space, a control byte or a non-ASCII byte, before the adapter sees any header, and exposes no leniency switch; the value and obs-text clauses are met. Phase 11 corrected the reason: a
  `PlaintextStreamFilter` could drop such a line, but only by parsing every response head ahead of the runtime's parser, which is a security and correctness risk out of proportion to
  tolerating a non-compliant server (contrast the native-retry filter, which never inspects a byte). Phase 8b (2026-10-11), re-audited by phase 11 (2026-10-20); design §10 entry 32. The
  conformance drivers carry a permanent waiver for `transport-14.name-malformed-dropped` alone.
- **`CFG-2` (MUST), an empty configuration value falls through, for a section of an unregistered configuration.** The fall-through walks the providers of the root the section was taken from, and an
  `IConfigurationSection` exposes no way to that root (`ConfigurationSection` keeps it private; reading it by reflection is not AOT-safe). So the reader walks the providers of the `IConfiguration` registered in the container when that is a
  root whose slice at the section's path holds the same pairs as the section, and otherwise reads the merged view, where an empty environment value in the highest provider still hides the lower one and the record default applies. A section of the
  container's configuration (`builder.Configuration.GetSection(...)`, the case a host produces) is fully met. Phase 9 (2026-10-13) recorded the whole clause as unmet (`di-empty-shadows`); phase 11 (2026-10-20) repaired it for the registered root and wrote the rest off;
  the phase 11 review (2026-10-21) counted the rest, because by P11-2 the outcome is still not delivered on that domain; design §10 entry 25. A public overload that takes the root and a path is a post-v1 candidate.

Superseded, kept for the record (struck through, never deleted):

- ~~**Open item for the lead and phase 11 (phase 8b, 2026-10-11): the count is seven, over the target of five by two.** Design §10 records seven clauses unmet on a stated domain: entries 16, 18, 24, 26 and 31 (earlier phases; their lines are not in this list yet) and, from phase 8b, 32 and 33 below. The P8b-14 and P8b-T2 rulings spent "one of the five" and "a second of the five" on a budget the earlier entries had already used up; the two lines below are therefore over the target, and phase 11 must either retire two clauses, re-rule the target, or argue the excess. 8b raised no other clause, and its `TRACE` question (checklist) would shrink entry 33's residual but not remove it.~~ *Superseded 2026-10-20 by phase 11: the count is three, above.*
- ~~- **`TRANSPORT-2` (MUST), the native retry for a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE`.** `SocketsHttpHandler` has no switch for its own connection-failure retry, and re-sends a request with no content whose connection ends before any response byte up to three more times. The transport keeps it off for a body-less `POST`, `PUT`, `PATCH` and extension methods (a zero-length content, the same bytes on the wire), and leaves the idempotent methods, which RFC 9110 section 9.2.2 permits the retry for and to which attaching content would break `TRANSPORT-26`. There is no consumer-side switch for those methods short of a different primary handler. Phase 8b (2026-10-11); design §10 entry 33 and P8b-T2. The driver carries a permanent waiver for `transport-2.bodyless-not-resent`, which turns stale (and fails the run) if the runtime stops the retry.~~ *Repaired by phase 11, task 4.1 (2026-10-20): the transport the SDK builds (`new SystemNetHttpClient()` and the DI named client) installs a pass-through `PlaintextStreamFilter` that
  turns "the connection ended before any response byte" into an I/O failure, so a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` arrives once (not up to four times; one idle-connection window remains, asymmetry `idle-connection-native-retry` below). A handler or `HttpClient` the caller supplies keeps
  `SocketsHttpHandler`'s native retry; that is the caller's own client and outside `TRANSPORT-2`'s scope (design §11 item 71, asymmetry `borrowed-handler-native-retry` below). The conformance
  waiver for `transport-2.bodyless-not-resent` is kept for the caller-built-handler subject alone.*

### SHOULD- and MAY-level requirements declined for v1

Each phase adds an entry for each requirement it declines, with the requirement ID and the reason.

- **`HTTP-22` (MAY): the header-name interning pool is not built.** The `WellKnown` statics already share the hot
  names, and the observable contract is value equality (`HTTP-21`). Phase 2a; design §4.1 and §12's deferred list.
- **`BODY-36` (MAY): no memory-mapped view of a file body.** `FileRequestBody` copies through an unbuffered `FileStream`; a
  mapped view is offered later only if a signing use case asks for random access over the bytes. Phase 3b; design §3.1 and
  §12's deferred list.

- **`REDIR-27` (MAY): the redirect target header is not configurable.** The header is `Location`; a `LocationHeader` option has no consumer
  and no trigger is named. Phase 6b (2026-10-08); design §12's deferred list and P6b-21.

- **`TRANSPORT-28` (SHOULD), the zero-copy file path.** A `FileRequestBody` is replayable and each write copies exactly its range (the MUST half is met), but
  `HttpClient` writes content to its own connection stream, possibly an `SslStream`, so no sendfile path is reachable through it and the transport does not
  special-case file bodies. Phase 8b (2026-10-11); P8b-15.

- ~~**`XCUT-24` (SHOULD), the non-consuming clause for an error-body snapshot above 1 MiB.** A body-log preview is byte-capped and does not pull the body ahead of its reader (the embedded MUST and the clause are met there, `invariant:xcut-24.preview-capped-non-consuming`), but `ErrorBodyBuffer.Capture` drains at most `Response.MaxBufferedErrorBytes` (1 MiB), truncates without a marker and replaces the original body, so the original stream is consumed. A repair candidate for phase 11 (retain the stream, or mark the truncation). Phase 10 (2026-10-14); P10-23.~~ *Struck by phase 11 (2026-10-20): the decline misread `BODY-30` and `RECOV-16`, which require the cap to drain and release; the snapshot's primary read path is the buffered copy and reads of it are non-consuming (design §11 item 69, amendment N11-1). The clause is met.*
- **`BODY-12` (SHOULD): the kernel file-to-socket path is not built.** `HttpClient` writes request content to its own connection stream, possibly an `SslStream`, so no sendfile path is reachable through it, as for
  `TRANSPORT-28`. Phase 3b (2026-10-03), declined in this register by phase 11 (2026-10-20, P11-26).

- **`OBS-28` (SHOULD), the request and response byte-count milestones.** The span vocabulary is built; the response count exists only after the consumer drains the body, after the attempt span has ended, and the
  transport's own milestones are the runtime's spans (design §10 entry 23). Phase 5c, declined in this register by phase 11 (2026-10-20, P11-26).

- **`SEAM-28` (MAY): the operation id is not attached to the context chain.** `OperationDescriptor.OperationId` is carried and never reaches the request; there is no carrier on `RequestOptions` and no consumer, so attaching it
  would be surface without a reader. Adding one later is additive. Phase 2b, declined in this register by phase 11 (2026-10-20, P11-26).

- **Authentication features outside the requirements (phase 6c, P6c-13, P6c-19, P6c-35, P6c-40).** Not built, and not planned for v1: **claims challenges**
  (`WWW-Authenticate: Bearer … claims="…"`, continuous access evaluation; the bearer hook re-fetches with the same `TokenRequestContext`), an **`apiKey` in a query
  string or a cookie** (`AUTH-26` is header-only; no policy stamps either), a **preemptive Digest** (the handler answers a challenge and remembers only counts),
  **`407` proxy challenges in the pipeline** (a forward proxy's `407` is answered by the transport from `ProxyOptions.ChallengeCredentials` or the Basic pair, phase 8b), and a
  **`NamedKeyCredential`** (a prefixed key is `new ApiKeyCredential(key, scheme: "SharedAccessKey")`). Digest's MD5 algorithms are unavailable on a host whose crypto
  provider refuses MD5 (design §10 entry 16).

### Behavioural asymmetries a consumer must know

- **`async-redirect-pillar` (design §10 entry 14; `PIPE-32`, `REDIR-25`).** The reference contract has the asynchronous standard pipeline
  not follow redirects. Here one `RedirectPolicy` serves both: `DexpacePipeline.CreateDefault` and
  `PipelineBuilder.AddStandardResilience` install it whether the call goes through `SendAsync` or `Send`, so both follow redirects. The invariant the
  asymmetry protected, that exactly one layer follows redirects, is the transport's (`AllowAutoRedirect = false` on the SDK-created client). A caller who
  wants the 3xx verbatim sets `MaxRedirects` to zero or builds a pipeline without the policy. Recorded by phase 4c (2026-10-07).
- **`two-meters` (design §8.1, §11 item 38; `OBS-32`).** A consumer that enables both the `Dexpace.Sdk` meter and `System.Net.Http`'s sees each
  attempt measured twice under `http.client.request.duration`. Enable one or the other. Recorded by phase 5c (2026-10-07); see
  [`tracing-and-metrics.md`](./sdk-documentation/tracing-and-metrics.md).
- **`borrowed-client-traceparent` (design §8.1, P5c-11).** `SystemNetHttpClient` drops the SDK's own `traceparent` stamp when a
  `System.Net.Http` listener exists, so the runtime's child span id is on the wire. A caller-supplied `HttpClient` whose handler chain does not
  propagate sends no `traceparent` for a traced call while that listener exists. Recorded by phase 5c (2026-10-07).
- **`link-cross-origin-guard` (design §11 item 66, P7c-12; `PAGE-19`).** `PAGE-19` says an absolute `Link` target is used as it stands. The
  `Link` strategy instead **ends the walk** on a target on another origin than the walk's first request (another host or port, or `https` to
  `http`), unless the caller passes `allowCrossOrigin: true`, and removes userinfo from a target it follows. Each page is a fresh pipeline call
  whose authorization policy stamps the credential for any origin its own request is same-origin with, so following a server-chosen origin
  would hand that server the bearer token. An API that pages across hosts (a CDN, a regional host) opts in, knowing the credential then goes to
  the new origin. A custom strategy and `PaginationStrategies.Create` get no such guard. Recorded by phase 7c (2026-10-09); see
  [`pagination.md`](./sdk-documentation/pagination.md).
- **`sync-borrowed-handlers` (phase 8b, P8b-9; `SEAM-11`, `PIPE-28`).** `SystemNetHttpClient.Execute` is genuinely synchronous (`HttpClient.Send`) for every
  construction. A primary handler you supply that overrides only `SendAsync` makes it throw `NotSupportedException`, and a `DelegatingHandler` in the chain
  that overrides only `SendAsync` is skipped silently on that face, because the runtime forwards the synchronous call straight to its inner handler. The async
  face runs it. Implement `Send` in such handlers, or call `ExecuteAsync`. Recorded by phase 8b (2026-10-11); see
  [`transport.md`](./sdk-documentation/transport.md).
- **`explicit-proxy` (phase 8b, P8b-4; `CFG-28`).** `new SystemNetHttpClient()` goes direct and no longer inherits the runtime's `HTTP_PROXY`, `HTTPS_PROXY` and
  `NO_PROXY` handling; a consumer who wants the environment writes `Proxy = ProxyOptions.FromEnvironment()` in `SystemNetHttpClientOptions`. A transport over a
  caller's `HttpClient` or handler leaves the proxy to that client or handler. Recorded by phase 8b (2026-10-11).
- **`borrowed-client-default-deadline` (phase 8b, P8b-6; `TRANSPORT-5`).** The transport's 100 second default deadline (`SystemNetHttpClientOptions.Timeout`) applies to a
  transport over a caller's `HttpClient` as well as an owned one, so a borrowed client whose own `Timeout` is longer than 100 seconds (or infinite) is cut at 100 seconds
  on a call that sets no `RequestOptions.Timeout`, where before only that client's `Timeout` bounded it. The shorter of the two wins; `Timeout = null` in the options leaves
  the client's own `Timeout` the only bound. Recorded by the phase 8b review (2026-10-11), as a breaking change, not an asymmetry the design chose to hide; see
  [`transport.md`](./sdk-documentation/transport.md).
- **`di-no-reload` (phase 9, P9-11).** A named client's settings are read once, at first resolution; the package registers no change-token source, so a reloaded `appsettings.json` does not reach a built client. A built client is immutable
  (`CFG-8`), and rebuilding a pipeline mid-flight would orphan the token cache and the transport's per-instance state. Recorded by phase 9 (2026-10-13); see [`dependency-injection.md`](./sdk-documentation/dependency-injection.md).
- **`di-handler-lifetime` (phase 9, P9-13, P9-14).** The DI package holds one handler chain per named client for the process, with an infinite `IHttpClientFactory` handler lifetime and `Transport:PooledConnectionLifetime` (default 2 minutes) doing
  the rotation's job inside the one handler; a container-owned lease releases the primary the package built. `builder.HttpClient.ConfigureHttpClient(...)` has no effect (the package uses the handler, never `CreateClient`), and the factory's own logging
  handlers are removed so the SDK's redacted events are the one HTTP log stream. Recorded by phase 9 (2026-10-13).
- ~~**`di-empty-shadows` (phase 9, P9-7; design §10 entry 25).** An empty or whitespace-only configuration value is absent, so the record's default applies, and `IConfiguration` merges its providers before the package reads a key: an empty environment value over a value in `appsettings.json` therefore resolves the record default, silently. The alternative (an empty value fails start) is the lead's to rule on. Recorded by phase 9 (2026-10-13).~~ *Repaired by phase 11 (2026-10-20, task 5.4, design §10 entry 25): when the `IConfiguration` registered in the container is the root that holds the client's section, an empty or whitespace-only value falls through to the next provider with a value. A section from a configuration that is not registered, or not a root, keeps the merged read.*
- **`borrowed-handler-native-retry` (phase 11, P11-12; design §11 item 71; `TRANSPORT-2`).** The transport the SDK builds (`new SystemNetHttpClient()` and the DI named client) installs a stream filter so
  `SocketsHttpHandler`'s own connection-failure retry cannot fire, and a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` reaches the server once (not up to four times; see `idle-connection-native-retry` below). A handler or `HttpClient` the caller supplies is the caller's
  native client: the SDK does not wrap it, and it keeps the runtime's retry, so such a request can still arrive up to four times. Retry through the SDK's pipeline, or build the handler with
  `SystemNetHttpClient.CreatePrimaryHandler`. The conformance waiver for `transport-2.bodyless-not-resent` is kept for that subject alone. Recorded by phase 11 (2026-10-20).
- **`idle-connection-native-retry` (phase 11 review, 2026-10-21; design §10 entry 33; `TRANSPORT-2`).** The filter that ends the runtime's re-send of a request whose connection ended before any response byte is armed by the request's first
  write, so a read of 0 before it passes through (an idle close is not a failed request, and the pool relies on that read). A server close that lands after the runtime's reuse check of a pooled connection and before that first
  write can still make the runtime re-send the request once on a new connection; the first copy was written to an already-closed connection and usually never reached the server's application. Reasoned, not reproduced: a test cannot land a close inside that window. "Reaches
  the server once" means not up to four times, not "never twice". The SDK's own retry layer is unaffected. See [`transport.md`](./sdk-documentation/transport.md).
- **`di-empty-shadows`, narrowed (phase 9, P9-7; narrowed by phase 11, 2026-10-20; counted as `CFG-2` by the phase 11 review, 2026-10-21; design §10 entry 25).** For a section taken from any `IConfiguration` other than the one registered in the
  container, or from a root with no path, an empty or whitespace-only value is still absent but still shadows: `IConfiguration` merges its providers before the package reads the key, so an empty environment value over a value in `appsettings.json`
  resolves the record default, silently. For a section of the registered root the value falls through to the next provider with one. Read the section from the configuration you register (`builder.Configuration.GetSection(...)`); a standalone
  `ConfigurationBuilder().Build()` handed to `AddDexpaceClient` keeps the merged read. Pinned by `EmptyValueTests.A_section_from_an_unregistered_configuration_keeps_the_merged_read`; see [`dependency-injection.md`](./sdk-documentation/dependency-injection.md).
- **`public-query-splice` (post-1.0 candidate, P7c-9).** The query splice behind `Cursor` and `PageNumber` is internal in v1, so a custom
  strategy builds its own query with `Query` and `Request.WithUrl`. Making the splice public is a candidate for a later release, if custom
  strategies turn out to need it. Recorded by phase 7c (2026-10-09).

### Post-v1 packages

Design §2.2 is the authority. Each package is a release-notes entry, not a phase, and each is gated by a trigger.

- **`Dexpace.Sdk.Reactive`** would bridge `IAsyncEnumerable<T>` to `IObservable<T>` for pagination and SSE.
  Trigger: a consumer that composes with Rx operators.
  An optional `SseItem<T>` bridge for callers who want `System.Net.ServerSentEvents`' strict-WHATWG semantics (design §11 item 17) would sit beside it
  rather than in core (recorded by phase 7b, 2026-10-09; `SSE-41` is N/A until an adapter ships).
- **`Dexpace.Sdk.Serialization.NewtonsoftJson`.** Trigger: a consumer whose DTOs are modelled with Newtonsoft
  attributes.
- **`Dexpace.Sdk.Serialization.Xml`.** Trigger: a real consumer of a second wire format.
- **A second first-party transport.** None is planned: design §2.2 argues that alternative stacks plug in under
  `HttpMessageHandler`. Trigger: a transport property the reference transport cannot exercise.
- **Webhook verification** (design §7.4, outside the reference contract). This is proposed as post-v1 by roadmap
  decision D3. Trigger: the first consuming SDK that receives webhooks.
- **A published test-doubles package** (`Dexpace.Sdk.Testing`). Until then, `tests/Dexpace.Sdk.TestSupport` stays
  non-packable. Trigger: an adapter or SDK author outside this repository asks for the fakes.

There is deliberately **no** OpenTelemetry package, because the OTel SDK subscribes to the `ActivitySource` and
`Meter` by name (design §2.2, §8.1).

## Release path

**Built and held (phase 12, 2026-10-26).** The path exists end to end and has never run: `.github/workflows/release.yml` is pinned by `ReleaseWorkflowTests` and inert until the setup under *NuGet ownership settled* exists. What it does:

1. **Triggers.** A `v*` tag push, or a manual run (`workflow_dispatch`) whose boolean `dry_run` defaults to `true`. Nothing else triggers it. Only a tag publishes: a tag push, or a manual run started on a tag ref with `dry_run` set to `false`.
2. **The `build` job** (ubuntu; the three-OS matrix and the NativeAOT smoke are `ci.yml`'s, on `main`): a full-history checkout, a locked restore, the build, the whole test run, then `scripts/ci/reproducible-pack.sh artifacts/release` with `SOURCE_DATE_EPOCH` at the
   commit time (the commit is packed twice, at two paths, and must be byte-identical, `.snupkg` included), the dependency audit and the packaging conformance over the first pack, [`scripts/ci/release-preflight.cs`](../scripts/ci/release-preflight.cs) over
   it (with `--tag` on a tag ref), and a `SHA256SUMS` file; the packages are uploaded as one artifact. The preflight refuses unless the five packages are at one version with a `.snupkg` beside each, every packed assembly is strong-named with the committed key's
   token, and, with a tag, the tag is `v<version>` on a commit of `main`, every `PublicAPI.Unshipped.txt` holds `#nullable enable` alone and this changelog has a `## [<version>]` section.
3. **The `publish` job** (`needs: build`, `environment: nuget`; the only job granted `id-token: write` and `attestations: write`) has no checkout. Its **first step is the guard**: it fails, naming this file, while `vars.NUGET_USER` is empty. After it: verify
   the checksums, attest every package (GitHub artifact attestation, in place of author signing), `NuGet/login` for a short-lived key, and `dotnet nuget push` with `--skip-duplicate`, which also pushes the `.snupkg` beside each package (unverified, see above).
4. **A dry run on `main` is expected to print one `would fail:` line** from the preflight (`CHANGELOG.md has no ## [0.1.0-alpha.1] section`): the changelog section is created at the release commit, with the suffix emptied, and a run without `--tag` reports it and exits 0.
5. **If the repository is private** (lead action L2, unanswered), GitHub artifact attestations may be unavailable without GitHub Enterprise Cloud: the attestation step and its pins in `ReleaseWorkflowTests` are removed, and `NFR-16` falls back to nuget.org's
   repository signature, which would then be declined as a SHOULD in *SHOULD- and MAY-level requirements declined for v1* in the same change. Visibility was not read.

The release itself is the lead's (L9), in this order, after *NuGet ownership settled*, the CI line and the ruleset line are done: the **release commit** (empty `VersionSuffix`; the draft release notes become `## [0.1.0] - <date>`; every `Unshipped` file is
`#nullable enable` alone), then the tag **`v0.1.0`** on it, which runs the workflow, then the **next-train commit** (`0.2.0-alpha.1`). The stages and their commands are in [`versioning.md`](./sdk-documentation/versioning.md) and [`quality-gates.md`](./sdk-documentation/quality-gates.md).

### After the first publish

Each item is only checkable against a real publish, so each waits (held, phase 12):

- **`PackageValidationBaselineVersion`** (`NFR-4`'s binary half): set it to the published version in `Directory.Build.props` (the library-gates group; it cannot be set earlier, because the package does not exist on nuget.org to resolve and setting it would break every
  build), so API compatibility runs against it from then on; each intended break needs a reviewed `CompatibilitySuppressions.xml` entry.
- **`NFR-12`'s release half**, as read by P12-12: nuget.org repository-signs every package, so "byte-identical to the published artifact" is unsatisfiable as worded; the clause is read as: a **rebuild from the tag reproduces the attested pre-push digests byte for byte**, and
  **the nuget.org copy equals them once its `.signature.p7s` entry is removed**, compared entry by entry (removing an entry rewrites the zip directory, so the whole-file digest differs). Exercise it once and then record it as a design §11 item.
- **`NFR-16`'s closure:** `gh attestation verify <file> --repo dexpace/dotnet-sdk` over a downloaded package succeeds. Until then the mechanism is built and the requirement is open.
- Then the release commit, the tag and the next-train commit above.

## Post-release triggers

Each entry names an event that no v1 phase can produce, and the one job to do when the event fires.

- **Stryker.NET supports Microsoft.Testing.Platform** (stryker-net#3094). Version 5.0.0 refuses every test project here, and a VSTest shadow of each would be a second harness for an optional, non-blocking report (design §9.3; styleguide 11.8). The job is to schedule a non-blocking
  mutation run over the core tests (phase 10, P10-21, 2026-10-14).
- **The first performance-motivated change.** Design §9.3 makes BenchmarkDotNet results a requirement of such a change; there has been none, and the allocation guarantees that matter are blocking tests already. The job is to create `benchmarks/` with that change (phase 10, P10-22, 2026-10-14).
- **The first external adapter author.** The job is to confirm that `Dexpace.Sdk.Conformance` runs under a test
  framework other than xUnit, which is the claim design §9.3 makes and nothing in this repository exercises.
- **The first consumer that needs a call's context below the transport.** The `CallKey` carrier on `RequestOptions`, the stamp from `SystemNetHttpClient` and a reader (a DI-registered `DelegatingHandler`, say) are not built: phase 9 registered no handler
  that reads a call's context, so the stamp would have been surface without a consumer, which `NFR-4` locks at the first release (P9-20; design §11 item 30). The job is to build the three together.
- **The first generated SDK that asks for a typed-client overload.** `AddDexpaceClient<TClient>` is not built (P9-5): it needs `ActivatorUtilities` under trimming annotations for a convenience a generated SDK's own `AddXClient` already provides. The job is to add it
  additively if one asks.

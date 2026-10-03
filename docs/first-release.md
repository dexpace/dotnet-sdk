# First Release

This is the release-readiness register: everything the first NuGet publish must know that no phase owns. It is one
of this repository's two registers. The other is `docs/deviations.md`, which phase 11 creates. The register is
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

| Package | Exists? | Target frameworks at `d45e64b` | Published? | Owner of what is missing |
|---|---|---|---|---|
| `Dexpace.Sdk.Core` | yes | `net8.0` | no — `0.0.1-alpha.1` | phases 0–7, 10 |
| `Dexpace.Sdk.Http.SystemNet` | yes | `net8.0` | no — `0.0.1-alpha.1` | phases 1, 8b |
| `Dexpace.Sdk.Serialization.SystemTextJson` | yes | `net8.0;net10.0` | no — `0.0.1-alpha.1` | phase 7a |
| `Dexpace.Sdk.Conformance` | no | — | no | phase 8a owns the csproj and version; phase 12 publishes it |
| `Dexpace.Sdk.Extensions.DependencyInjection` | no | — | no | phase 9 |

The target frameworks changed in phase 0. The lead approved the roadmap's decision D1 on 2026-09-28: every package
targets `net10.0` only, set once in `Directory.Build.props` (PR #21), because .NET 8 leaves support on 2026-11-10.

## Blockers before first publish

- [ ] **CI green on every matrix row.** The green build, format check, locked restore, coverage floor, `PublicAPI`
      files, package validation, dependency audit and 3-OS matrix are phase 0's; the complete gate set is phase 10's.
- [ ] **The nine P0 defects closed, with permanent `Security` regression tests.** The defects are header CR/LF
      injection, a caller-set `Host`, transport auto-redirect with cross-origin credential forwarding, credentials
      over `http`, deny-list URL redaction, the attempt-leaking `PipelineContext`, the `Retry-After` overflow, the
      throw on 304, and the leak on a malformed `Content-Type`. Phase 1.
- [ ] **Every MUST dispositioned.** Every one of the 532 MUSTs has a checklist row: ✅ with a named test; N/A or
      vacuous with a design §10 or §11 citation; or listed below under *Unsatisfied MUSTs*. Phases 2–10 own the
      rows, and phase 10's row-count gate proves the total.
- [ ] **Conformance green.** `Dexpace.Sdk.Conformance` passes against every shipped transport on every CI row, with
      waivers listed by requirement ID. It is written by phase 8a and completed by phase 10.
- [ ] **Deviations audited.** `docs/deviations.md` exists and every row reads `as-built: …`. Phase 11.
- [ ] **One-time public-surface choices decided before the first `PublicAPI.Shipped.txt`.** Adding a public member
      later is compatible; removing or narrowing one after the tag is a break. Phase 11 lists the choices here, and
      phase 12 decides them.
- [ ] **A reachable `SECURITY.md` contact.** Phase 0 creates the file; this line is ticked only when the contact is
      confirmed to be monitored.
- [ ] **NuGet ownership settled.**
      - The `Dexpace` package-ID prefix is reserved on nuget.org.
      - The owning account or organisation is settled.
      - Publishing uses nuget.org trusted publishing (OIDC) from the release workflow, with no long-lived API key
        committed or stored as a plain secret. If trusted publishing is not available to the organisation at
        release time, a scoped key in a protected GitHub environment is the fallback, and it is recorded here as
        such. Phase 12.
- [ ] **Signing decided.** Two decisions, both phase 12's:
      - Strong naming, with a committed key. Design §9.2 says keep it for strong-named consumers, not as a security
        measure.
      - Author signing with an organisation certificate (`NFR-16`).

      nuget.org's repository signature applies to every package regardless of these decisions.
- [ ] **Versioning policy written.** Lockstep SemVer. The pre-1.0 policy states what a `0.x` minor may break. The
      next version after `0.0.1-alpha.1` is phase 12's decision.
- [ ] **User documentation.**
      - `docs/sdk-documentation/`, with one page per area, each written at its phase's close.
      - An architecture page, a getting-started page, and one end-to-end example.
      - The `write-a-{transport,serde,response-handler,paging-strategy}` guides.
      - A conformance page stating what a green conformance run does **not** prove.

      Phase 12 assembles these pages.
- [ ] **`CHANGELOG.md` complete.** Phase 0 back-filled PRs #3–#9 on 2026-09-29, and every later phase adds its own
      `[Unreleased]` entry.

## What v1 ships without

**The release notes MUST state every item in this section.** Today it holds only what is already decided. The
phases add the rest as they decline or defer something. Nothing below is a blocker.

### Unsatisfied MUSTs

None is recorded yet. Every candidate must first be argued in design §10 and then pass phase 11's audit. The
roadmap's parity target is at most five clauses unsatisfied or unmet on a stated domain, each one argued.

### SHOULD- and MAY-level requirements declined for v1

Each phase adds an entry for each requirement it declines, with the requirement ID and the reason.

- **`HTTP-22` (MAY): the header-name interning pool is not built.** The `WellKnown` statics already share the hot
  names, and the observable contract is value equality (`HTTP-21`). Phase 2a; design §4.1 and §12's deferred list.
- **`BODY-36` (MAY): no memory-mapped view of a file body.** `FileRequestBody` copies through an unbuffered `FileStream`; a
  mapped view is offered later only if a signing use case asks for random access over the bytes. Phase 3b; design §3.1 and
  §12's deferred list.

### Behavioural asymmetries a consumer must know

None is recorded yet. The first candidate is the design §10 topic `async-redirect-pillar`: one `RedirectPolicy`
serves both the synchronous and the asynchronous pipeline, which reverses `PIPE-32` and `REDIR-25`. Phase 4 records
it here if it ships as designed.

### Post-v1 packages

Design §2.2 is the authority. Each package is a release-notes entry, not a phase, and each is gated by a trigger.

- **`Dexpace.Sdk.Reactive`** would bridge `IAsyncEnumerable<T>` to `IObservable<T>` for pagination and SSE.
  Trigger: a consumer that composes with Rx operators.
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

Not yet wired. Phase 12 owns this path:

1. A `v*` tag triggers `.github/workflows/release.yml`. Nothing else triggers it.
2. The workflow runs a locked restore, build and test.
3. It runs `dotnet pack` with `SOURCE_DATE_EPOCH` set to the commit time, and package validation.
4. It author-signs the packages, if that is decided.
5. It pushes through trusted publishing.

### After the first publish

- Set `PackageValidationBaselineVersion` to the published version, so API compatibility runs against it from then
  on (`NFR-4`'s binary half).
- Close `NFR-12`'s release half, which requires a published package byte-identical to a rebuild from its tag, and
  close `NFR-16`. Both are only checkable against a real publish.

## Post-release triggers

Each entry names an event that no v1 phase can produce, and the one job to do when the event fires.

- **The first external adapter author.** The job is to confirm that `Dexpace.Sdk.Conformance` runs under a test
  framework other than xUnit, which is the claim design §9.3 makes and nothing in this repository exercises.

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
| `Dexpace.Sdk.Conformance` | yes (phase 8a) | `net10.0` | no — `0.0.1-alpha.1`; the first published version is the first release's lockstep version, `0.1.0` proposed (a release that adds an assertion is at least a minor version) | phase 10 completed it on 2026-10-14 (the invariant, codec and packaging suites beside the transport suite: four suites); phase 12 publishes it and decides the number |
| `Dexpace.Sdk.Extensions.DependencyInjection` | yes (phase 9) | `net10.0` | no — `0.0.1-alpha.1`; it references `Dexpace.Sdk.Core`, `Dexpace.Sdk.Http.SystemNet` and `Microsoft.Extensions.Http` (the hosting family's one root), at the lockstep version | phase 12 (the release path) |

The target frameworks changed in phase 0. The lead approved the roadmap's decision D1 on 2026-09-28: every package
targets `net10.0` only, set once in `Directory.Build.props` (PR #21), because .NET 8 leaves support on 2026-11-10.

## Blockers before first publish

- [ ] **CI green on every matrix row.** The green build, format check, locked restore, coverage floor, `PublicAPI`
      files, package validation, dependency audit and 3-OS matrix are phase 0's; the complete gate set is phase 10's
      (built and dispositioned on 2026-10-14: the gate table in the [phase 10 checklist](./work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants-checklist.md);
      `CiWorkflowTests` pins that every gate is a step and none is advisory). This line is ticked when the first CI run on `main` after phase 10 is green on every row.
- [ ] **The `main` ruleset requires the CI jobs (`NFR-17`'s merge half).** A `required_status_checks` rule naming `build & test (ubuntu-latest)`, `build & test (windows-latest)`,
      `build & test (macos-latest)`, `reproducible packages` and `NativeAOT smoke`; design V9 found none (no classic protection either), so a red job does not block a merge.
      An admin-only repository setting that phase 10 did not apply (request text in the phase 10 checklist's *Open items*). Read it back with `gh api repos/dexpace/dotnet-sdk/rulesets` and tick this line.
- [ ] **The nine P0 defects closed, with permanent `Security` regression tests.** The defects are header CR/LF
      injection, a caller-set `Host`, transport auto-redirect with cross-origin credential forwarding, credentials
      over `http`, deny-list URL redaction, the attempt-leaking `PipelineContext`, the `Retry-After` overflow, the
      throw on 304, and the leak on a malformed `Content-Type`. Phase 1.
- [ ] **Every MUST dispositioned.** Every one of the 532 MUSTs has a checklist row: ✅ with a named test; N/A or
      vacuous with a design §10 or §11 citation; or listed below under *Unsatisfied MUSTs*. Phases 2–10 own the
      rows, and phase 10's row-count gate proves the total. **Phase 10 (2026-10-14) built the gate and it is green:** `dotnet run scripts/ci/requirement-coverage.cs -- --check` reports all 645 IDs of appendix C owned by a checklist row, each with its level and a legend mark,
      and no finding in a MUST row; it is a blocking Linux step of `ci.yml`. **The line stays open** because the script's tally still reads 16 MUST rows as ⏳ in the row that owns them: `NFR-17` (the ruleset line above), and hand-offs whose closing evidence sits in a later phase's checklist in a table
      the script does not read as owning (the `RECOV` rows closed in 6a's table, `HTTP-44` and `HTTP-45` closed by a dated correction in the same cell). Phase 11 gives those rows their dated corrections and ticks this line; it also decides whether `NFR-17` is the only MUST left open.
- [ ] **Conformance green.** `Dexpace.Sdk.Conformance` passes against every shipped transport on every CI row, with
      waivers listed by requirement ID. It is written by phase 8a and completed by phase 10 (2026-10-14: the invariant, codec and packaging suites, with three first-party drivers that each pin their `NotExercised` list).
- [ ] **Deviations audited.** `docs/deviations.md` exists and every row reads `as-built: …`. Phase 11.
- [ ] **One-time public-surface choices decided before the first `PublicAPI.Shipped.txt`.** Adding a public member
      later is compatible; removing or narrowing one after the tag is a break. Phase 11 lists the choices here, and
      phase 12 decides them. Candidates found so far (phase 10, 2026-10-14): whether `HttpResponseException(Response)` should refuse a non-error status (`XCUT-8`, P10-24;
      today every core mapping path goes through a guarded internal factory and the public constructor accepts a 200), and whether `ServiceResponseException`, public and never
      thrown (`XCUT-4`, P10-25), is deleted or argued into design §3.2.
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

Every candidate must first be argued in design §10 and then pass phase 11's audit. The roadmap's parity target is at most five clauses
unsatisfied or unmet on a stated domain, each one argued.

**Open item for the lead and phase 11 (phase 8b, 2026-10-11): the count is seven, over the target of five by two.** Design §10 records seven clauses unmet on a stated domain: entries
16, 18, 24, 26 and 31 (earlier phases; their lines are not in this list yet) and, from phase 8b, 32 and 33 below. The P8b-14 and P8b-T2 rulings spent "one of the five" and "a second of the five" on
a budget the earlier entries had already used up; the two lines below are therefore over the target, and phase 11 must either retire two clauses, re-rule the target, or argue the excess. 8b raised
no other clause, and its `TRACE` question (checklist) would shrink entry 33's residual but not remove it.

- **`TRANSPORT-14` (MUST), the malformed inbound header *name* clause.** `SocketsHttpHandler` rejects the whole response on a header name holding
  a space, a control byte or a non-ASCII byte, before the adapter sees any header, and exposes no leniency switch; the value and obs-text clauses are met.
  Phase 8b (2026-10-11); design §10 entry 32 and P8b-14. The conformance driver carries a permanent waiver for `transport-14.name-malformed-dropped` alone.
- **`TRANSPORT-2` (MUST), the native retry for a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE`.** `SocketsHttpHandler` has no switch for its
  own connection-failure retry, and re-sends a request with no content whose connection ends before any response byte up to three more times. The
  transport keeps it off for a body-less `POST`, `PUT`, `PATCH` and extension methods (a zero-length content, the same bytes on the wire), and leaves
  the idempotent methods, which RFC 9110 section 9.2.2 permits the retry for and to which attaching content would break `TRANSPORT-26`. There is no
  consumer-side switch for those methods short of a different primary handler. Phase 8b (2026-10-11); design §10 entry 33 and P8b-T2. The driver carries a
  permanent waiver for `transport-2.bodyless-not-resent`, which turns stale (and fails the run) if the runtime stops the retry.

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

- **`XCUT-24` (SHOULD), the non-consuming clause for an error-body snapshot above 1 MiB.** A body-log preview is byte-capped and does not pull the body ahead of its reader (the embedded MUST and the clause are met there, `invariant:xcut-24.preview-capped-non-consuming`),
  but `ErrorBodyBuffer.Capture` drains at most `Response.MaxBufferedErrorBytes` (1 MiB), truncates without a marker and replaces the original body, so the original stream is consumed. A repair candidate for phase 11 (retain the stream, or mark the truncation). Phase 10 (2026-10-14); P10-23.

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
- **`di-empty-shadows` (phase 9, P9-7; design §10 entry 25).** An empty or whitespace-only configuration value is absent, so the record's default applies, and `IConfiguration` merges its providers before the package reads a key: an empty environment value
  over a value in `appsettings.json` therefore resolves the record default, silently. The alternative (an empty value fails start) is the lead's to rule on. Recorded by phase 9 (2026-10-13).
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

- **Stryker.NET supports Microsoft.Testing.Platform** (stryker-net#3094). Version 5.0.0 refuses every test project here, and a VSTest shadow of each would be a second harness for an optional, non-blocking report (design §9.3; styleguide 11.8). The job is to schedule a non-blocking
  mutation run over the core tests (phase 10, P10-21, 2026-10-14).
- **The first performance-motivated change.** Design §9.3 makes BenchmarkDotNet results a requirement of such a change; there has been none, and the allocation guarantees that matter are blocking tests already. The job is to create `benchmarks/` with that change (phase 10, P10-22, 2026-10-14).
- **The first external adapter author.** The job is to confirm that `Dexpace.Sdk.Conformance` runs under a test
  framework other than xUnit, which is the claim design §9.3 makes and nothing in this repository exercises.
- **The first consumer that needs a call's context below the transport.** The `CallKey` carrier on `RequestOptions`, the stamp from `SystemNetHttpClient` and a reader (a DI-registered `DelegatingHandler`, say) are not built: phase 9 registered no handler
  that reads a call's context, so the stamp would have been surface without a consumer, which `NFR-4` locks at the first release (P9-20; design §11 item 30). The job is to build the three together.
- **The first generated SDK that asks for a typed-client overload.** `AddDexpaceClient<TClient>` is not built (P9-5): it needs `ActivatorUtilities` under trimming annotations for a convenience a generated SDK's own `AddXClient` already provides. The job is to add it
  additively if one asks.

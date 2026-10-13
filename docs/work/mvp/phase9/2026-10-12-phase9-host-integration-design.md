# Phase 9 — Host Integration: Design

**Status:** Draft, for review. Written 2026-10-12 against `main` at `9a66c15` (phases 0 to 8b merged), on branch
`94-phase-9-planning`, for issue [#94](https://github.com/dexpace/dotnet-sdk/issues/94). Brainstormed without a human in the
loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling (`P9-n`) with
its options and rationale, and the ones the lead may still reverse are marked **open for the lead**. The scope authority is the
roadmap's Phase 9 card and Phase List row 9 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`), the phase-1 hand-off
table's row 9, and every Phase Status Note bullet addressed to 9 (2b, 5a, 5b, 6a, 6b, 6c, 7a, 7c, 8a, 8b). The format follows
the phase 8 designs (`docs/work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport-design.md`) and 5a's
(`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration-design.md`).

**What this document is.** The phase design for 9, which has no sub-phases (P9-1). It holds:

- the segmentation decision the roadmap's rule asks for, recorded here rather than in a file of its own (P8a-1's precedent);
- one disposition per requirement row phase 9 owns (12 `CFG` rows, plus `SEAM-5`, `SEAM-6` and `OBS-35` carried from 2b and 5b),
  and the evidence it adds to three rows other phases own (`SEAM-7`, `TRANSPORT-1`, `XCUT-10`);
- the disposition of every non-row hand-off addressed to 9 (5a's three, 5b's V4, 6a's two, 6b's, 6c's, 7a's, 8a's, 8b's four,
  phase 1's S3 and P8b-18's `CallKey` stamp);
- the new package `Dexpace.Sdk.Extensions.DependencyInjection` (registration API, configuration reader, validation, the named
  client), one addition to `Dexpace.Sdk.Http.SystemNet`, and the change to `scripts/ci/dependency-audit.cs`;
- nine verifications run for this design (V1 to V9) on .NET SDK 10.0.401 (runtime 10.0.12), Linux;
- the rulings (which double as deviation-ledger IDs, roadmap constraint 7), the test strategy, PR segmentation, hand-offs,
  close-out corrections and exit criteria.

**What this document is not.** It is not the plan and not the checklist. It does not re-argue design §3.6's mapping of
`SEAM-5`–`SEAM-10` (§10 entry 9, topic `no-provider-registry`), §8.2's tier mapping (§10 entry 25, `configuration-via-iconfiguration`),
§10 entry 26 (`typed-accessors-fail-fast`) or §10 entry 2 (the hosting family counted as one library); it cites them and
records where the as-built shape refines them. It edits no design chapter, roadmap cell, register, checklist or `CLAUDE.md`
line; the corrections it owes are listed in [Corrections owed at close-out](#design-roadmap-register-checklist-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

| Predecessor | Kind | State at `9a66c15` |
|---|---|---|
| **5a** — options as sealed records with validating `init` accessors (P5a-3, P5a-4); `ProxyOptions` and `ProxyOptions.FromEnvironment(Func<string, string?>, ILogger)` (P5a-16) | **dependency** (card "Entry"; ordering rationale 7) | Met. Phase 9 binds the records; 6a and 6b have since added the numeric range checks to the accessors. |
| **8b** — `SystemNetHttpClient(HttpMessageHandler, SystemNetHttpClientOptions)` with the redirect-following-primary check (P8b-3); `SystemNetHttpClientOptions`; the strict `CFG-28` reading (P8b-4); the kit green on two SystemNet subjects | **dependency** (card "Entry") | Met. The handler constructor is the named client's seam; the reading forbids any implicit environment read. |
| 2b — `SEAM-5`/`SEAM-6` N/A for the DI-less half, DI half ⏳ 9; `SEAM-7` N/A citing "the singleton lifetime, which phase 9 cites" | **dependency** | Met; phase 9 flips the two ⏳ halves. |
| 4c — `PipelineBuilder.Flatten`, `Build(transport, DexpaceClientOptions)`; `HttpPipeline` as a transport, owning no transport (PIPE-27) | **dependency** | Met. The DI package builds pipelines with no core change (V3). |
| 5b — `HttpLoggingOptions.Level` (`HttpLogLevel`); `OBS-35` ⏳ 9 with V4 not run | **dependency** | Met; V4 is run here (V1). |
| 6a — `RetryOptions` incl. `IReadOnlySet<int> RetryableStatusCodes`; the one retry layer; the `AddStandardResilienceHandler` guidance left to 9 | **dependency** | Met. The guidance is refined by V6: the SDK-side option is withdrawn and the handler-side spelling is pinned (P9-19). |
| 6b — `RedirectOptions.AllowedMethods` (`IReadOnlySet<Method>`), `Predicate` code-only | **dependency** | Met. |
| 6c — `AuthCredentials` (redacting), `AuthDescriptor`, `MultiSchemeAuthPolicy` | **dependency** | Met. |
| 7a — `SystemTextJsonSerde`, `SystemTextJsonSerde.CreateDefaultOptions(IJsonTypeInfoResolver)` | convenience | Met; the DI package does not reference the codec (P9-17). |
| 8a — `Dexpace.Sdk.Conformance`, `TransportSubject` hooks, the two SystemNet drivers and their two permanent waivers | **dependency** (for the third driver) | Met. |
| Phase 10 | **dependent** | Audits `XCUT-10`, `XCUT-22`, `XCUT-23`, `NFR-2`, `NFR-8` against 9's evidence; completes the AOT smoke. |

No dependency edge inverts the roadmap's order: 5a and 8b exited before this design (issue #94, "Entry").

## Governing documents, and the phase-start queries

Read for this design: product-spec chapter 16 in full and appendix C rows `CFG-1`–`CFG-38`, `SEAM-5`–`SEAM-10`, `TRANSPORT-1`,
`TRANSPORT-2`, `TRANSPORT-15`, `XCUT-10`, `XCUT-22`, `XCUT-23`, `OBS-35`, `NFR-2`, `NFR-8`, `NFR-9`; design §2.1, §2.4, §3.2 (the
`TRANSPORT-1` paragraph), §3.6, §6.1 ("The layer underneath"), §8.2, §9.2, §9.3, §10 entries 2, 9, 25 and 26, §11 items 4, 8, 14, 24
and 30, §12's `CFG`, `SEAM` and `OBS` rows; the roadmap's constraints 1–10, the Phase 9 card, the segmentation rule, the phase-1
hand-off table and the status notes above; `docs/first-release.md`; the styleguide companion
`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md` (1.2–1.5, 1.7) and `02-dependency-injection.md` (2.3–2.8); the code
under `src/Dexpace.Sdk.Core/{Configuration,Auth,Serialization,Pipeline,Execution}`, `src/Dexpace.Sdk.Http.SystemNet` (all files),
`src/Dexpace.Sdk.Serialization.SystemTextJson`, `src/Dexpace.Sdk.Conformance` (public API), `tests/Dexpace.Sdk.Http.SystemNet.Tests/{SystemNetConformanceTests.cs,Conformance/SystemNetSubject.cs}`,
`tests/Dexpace.Sdk.AotSmoke`, `Directory.Build.props`, `Directory.Packages.props`, `BannedSymbols.txt` and
`scripts/ci/dependency-audit.cs`.

```bash
scripts/knowledge --origin note --brief          # no note on configuration binding or DI; transport notes (8b) read for the handler facts
scripts/knowledge --section conflicts --brief    # no conflict on configuration; the styleguide 1.3 tension below is new (P9-4)
scripts/knowledge --prefix-info CFG              # 38 IDs, 29 MUST / 8 SHOULD / 1 MAY; 38 of 38 substantive; topic configuration
scripts/knowledge --gaps CFG                     # 0 gaps
scripts/knowledge --req CFG-2 / CFG-7 / CFG-14 / OBS-35 / SEAM-6 / XCUT-10   # read; the corpus repeats §8.2 and §3.6
```

Two corpus entries are contradicted by measurement and get a knowledge note at close-out: `configuration/bfbcbf8c` ("an empty
environment value … fails `ValidateOnStart`": the binder maps an empty value to `null` for a nullable target, V1, and this design
reads it as absent, P9-7) and `observability/4997350a` ("`OBS-35`'s layered lookup is satisfied by `IConfiguration` binding": the
binder accepts a numeric and an undefined level, V1, so the satisfaction is the reader's, P9-8).

**Sibling sources.** Both siblings are on disk: `ruby-sdk@5b17395` and `nodejs-sdk@54aeed4` (`~/Projects`). Neither has a DI package
(the card says so); the configuration-tier case tables port from `ruby-sdk/gems/dexpace-core/test/dexpace/configuration_test.rb`
(the `CFG-5` integer table, the `CFG-6` token table, the `CFG-7` duration table), `ruby-sdk/gems/dexpace-core/test/dexpace/configuration/keys_test.rb`
(`CFG-14` / `OBS-35`: a published key that no resolver reads), `nodejs-sdk/packages/core/src/config/duration.test.ts` (`P5M` rejected,
`P`/`PT` rejected, surrounding whitespace tolerated, the three-grammars-one-scale property) and
`nodejs-sdk/packages/core/src/config/configuration.test.ts` (the `CFG-1` precedence and `CFG-2` fall-through rows), re-expressed
against `IConfiguration` providers (P9-26). `ruby-sdk/gems/dexpace-core/test/dexpace/config_test.rb` is entirely `CFG-13` (the
global slot, 🚫 in 5a) and ports nothing. The roadmap card's path `nodejs-sdk/packages/core/src/config/` is correct; `ruby-sdk/.../{configuration,config_test.rb}`
resolves to the `configuration/` directory, `configuration_test.rb` and `config_test.rb`.

---

## Segmentation

The roadmap's rule asks for a segmentation design when a build phase "spans more than one ID-bearing spec chapter" or "ships more
than one package". Phase 9's rows span ch.16 (`CFG`), ch.03 (`SEAM-5`, `SEAM-6`) and ch.15 (`OBS-35`), and it contributes evidence
to rows of ch.17 (`TRANSPORT-1`) and ch.19 (`XCUT-10`); it ships one new package and adds one public method to an existing one
(P9-12). **The rule applies by its letter. This section is the segmentation design, and its answer is: one phase, no
sub-phases** (P9-1, open for the lead, who may ask for it as a file of its own, as P8a-1 left open).

Why one phase suffices:

- **The ID count is the smallest of any build phase**: 15 owned rows (12 `CFG` + 3 carried), against 52 for phase 8 and 71 for phase 2.
- **Every row converges on one code path.** `AddDexpaceClient` reads configuration (`CFG-*`, `OBS-35`), validates it (`CFG-5`–`CFG-7`,
  `SEAM-5`), registers the transport and codec (`SEAM-5`–`SEAM-7`) and builds the named client (`TRANSPORT-1`, `XCUT-10`). A cut
  between "configuration" and "registration" would put the reader and its only caller in different sub-phases, and the reader has no
  public surface of its own to land first.
- **The cross-package edits are small and leaf-level**: one public static method on `SystemNetHttpClient` and a policy-table change in
  the audit script, both consumed only by phase 9.

The order inside the phase is a **dependency** chain, expressed as stacked pull requests rather than sub-phases
([PR segmentation](#pr-segmentation)): the SystemNet seam and the audit change first (the package cannot pass the audit without it),
then the reader, then registration, then the drivers and documents.

---

## Facts the design rests on

Every spike ran on .NET SDK 10.0.401 against packages from nuget.org (the `Microsoft.Extensions.*` 10.0.x band's latest is `10.0.12`,
matching `Directory.Packages.props`' facade pin; `Microsoft.Extensions.Http.Resilience`'s latest is `10.10.0`). The spikes were
scratch projects outside the repository, over a `git archive` of `9a66c15`; none is committed, and the plan's first task re-runs
each as a test.

| # | Verification | Result | Consequence |
|---|---|---|---|
| V1 | The configuration binder over the core records, in both modes: the source generator (`EnableConfigurationBindingGenerator`) and reflection | **Generator:** binds **no** `init` accessor and says nothing: every value of every record stayed at its default (`MaxRetryAttempts = "5"` → 2, `BaseDelay = "PT5S"` → 200 ms, `Level = "Body"` → `None`), the generated `BindCore` for `RetryOptions` only validates key names. It fails to compile over `ProxyOptions` (`CS9035`, the `required` `Host`/`Port`) and reports `SYSLIB1100`/`SYSLIB1101` for `IReadOnlySet<Method>` and `ICredentials`. **Reflection:** 32 `IL2026` + 32 `IL3050` warnings (build errors under `IsAotCompatible` and warnings-as-errors); sets `init` accessors, surfacing an accessor's exception as `TargetInvocationException`; `TimeSpan` `"1000"` → 1000 days, `"PT5S"`/`"500ms"` rejected; `int` `"0x10"` → 16, `"+3"` and `" 5 "` accepted; `bool` `"1"`/`"yes"` rejected, `" TRUE "` accepted; enum `"2"` → `Body` and `"7"` → the **undefined** value 7, accepted silently; `IReadOnlySet<int> RetryableStatusCodes` silently **not bound** (array or comma form); `IReadOnlyCollection<string> AllowedHeaderNames` **appended to** the default instead of replacing it; `AllowedMethods` silently not bound; an empty `AttemptTimeout` → `null`. Both modes' conversion errors quote the value (`Failed to convert configuration value '…'`). | Neither binder can serve: one silently binds nothing, the other is not AOT-safe and silently mis-binds four members. Binding is a hand-written reader over `IConfiguration` (P9-4). This is 5b's V4 (enum conversion), answered: case-insensitive and whitespace-tolerant, but numeric and undefined values pass. |
| V2 | `XmlConvert.ToTimeSpan` (design §8.2's ISO path) and `int.TryParse(…, AllowLeadingSign, Invariant)` | `PT5S`, `P1D`, `P1DT2H3M4S`, `PT0.5S`, `" PT5S "` parse; **`pt5s` throws** (lower case); `PT-5S`, `P`, `PT`, `P1W`, `P1DT`, `PT1.5H` throw; **`-PT5S` → −5 s**; **`P5M` → 150 days and `P1Y` → 365 days**; `PT0.00000001S` → 0 (silent truncation); `P99999999D` throws `OverflowException`. `int.TryParse` with `AllowLeadingSign` accepts `010` (10), `-5`, `+3`; rejects `" 5 "`, `0x10`, `1_000`, `1e3`, `1.5`, `٣`, overflow. | `XmlConvert` is not `CFG-7` on four counts (case, sign, calendar units, truncation); the ISO grammar is the reader's own (P9-8). The integer rule is `AllowLeadingSign` over a trimmed value. |
| V3 | `IHttpClientFactory` and the 8b handler constructor | The factory's default chain is `LifetimeTrackingHttpMessageHandler → LoggingScopeHttpMessageHandler → LoggingHttpMessageHandler → SocketsHttpHandler` with **`AllowAutoRedirect = true`**; `new SystemNetHttpClient(factory.CreateHandler(name))` **throws** naming `AllowAutoRedirect`. With `ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, … })`, `RemoveAllLoggers()` and a caller's `AddHttpMessageHandler`, the chain is `LifetimeTrackingHttpMessageHandler → <caller's handler> → SocketsHttpHandler`, the 8b check walks it to the primary and accepts it, and `PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(…)).Build(transport, options)` builds the default seven policies over it with the bound options. | The handler constructor composes with the factory and is the `TRANSPORT-1` backstop: a caller who swaps in a redirect-following primary fails at first resolution. No core change is needed to build a pipeline with bound options (P9-12). |
| V4 | Options validation with and without a host | `ValidateOnStart()` registers `Microsoft.Extensions.Options.IStartupValidator`; without a host, `IStartupValidator.Validate()` runs the named validators and throws `OptionsValidationException` naming the failure; a provider built with `ValidateScopes` and `ValidateOnBuild` does **not** run options validation; resolving a keyed singleton whose factory reads `IOptionsMonitor<T>.Get(name)` throws `OptionsValidationException` lazily. Two keyed `IAsyncHttpClient` registrations under one key: `GetRequiredKeyedService` silently returns the **last** (§11 item 8, measured), and a validator scanning the captured `IServiceCollection` lists both. | Validation runs at host start and, with no host, at first resolution (P9-11); the `SEAM-5` multi-candidate check scans descriptors (P9-16). |
| V5 | The package's dependency closure and the trim/AOT analyzers | A library referencing `Microsoft.Extensions.Http` 10.0.12 (plus the SystemNet project) carries 13 packages, all 10.0.12: `Http`, `Configuration.Abstractions`, `Configuration`, `Configuration.Binder`, `DependencyInjection.Abstractions`, `DependencyInjection`, `Diagnostics.Abstractions`, `Diagnostics`, `Logging.Abstractions`, `Logging`, `Options`, `Options.ConfigurationExtensions`, `Primitives`. `Microsoft.Extensions.Http` alone is the root of all 13: its closure contains `DependencyInjection.Abstractions`, `Options`, `Configuration.Abstractions` and `Logging.Abstractions`. With `IsAotCompatible` and `IsTrimmable`, `AddHttpClient`, `ConfigurePrimaryHttpMessageHandler`, `SetHandlerLifetime`, `RemoveAllLoggers`, `AddKeyedSingleton`, `AddOptions<T>(name).Configure(…).ValidateOnStart()`, `TryAddEnumerable` and `IValidateOptions<T>` raise **zero** analyzer warnings. | One third-party root, `Microsoft.Extensions.Http`, carries the family (§10 entry 2, measured); the audit names it once (P9-3). The reflection binder arrives transitively and is banned in `src/`, not removed (P9-4). |
| V6 | `Microsoft.Extensions.Http.Resilience` 10.10.0, the standard handler | `AddStandardResilienceHandler(o => o.Retry.MaxRetryAttempts = 0)` **fails its own options validation** at first handler creation ("must be between 1 and 2147483647"). Its defaults: 3 retries, exponential, 10 s attempt timeout, 30 s total. Over a loopback server answering `503`, a **`POST` with a body** reached the server **4 times** under the defaults and **once** with `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`. | The handler-side spelling, `o.Retry.MaxRetryAttempts = 0`, cannot disable the handler's retry: its own validation refuses it. The existing guidance (§6.1, `retry.md`, the 6a hand-off) offers two options: the **SDK's own** `MaxRetryAttempts = 0`, or a resilience handler **without its retry strategy**. The first leaves the handler retrying behind the SDK's re-send gate, so a non-idempotent `POST` body is re-sent, which is `XCUT-10`'s hazard (§11 item 24) measured; the second is the one-retry-layer rule, and the way to spell it with the standard handler is `ShouldHandle` returning `false`, which keeps the circuit breaker, rate limiter and timeouts (P9-19). The SDK-side option is withdrawn on `XCUT-10`'s ground (the SDK is the one retry layer), not because the guidance misread the handler's options. |
| V7 | NativeAOT publish of a consumer of the V3 registration path | Published with `PublishAot`, `TrimmerSingleWarn=false`: **no** trim or AOT warning; the binary built the provider (`ValidateScopes`, `ValidateOnBuild`), ran `IStartupValidator.Validate()`, resolved the keyed pipeline and transport, received a loopback `302` unchanged through the transport, and failed a bad configuration with `OptionsValidationException`. | The AOT smoke consumer can exercise the DI package (P9-21). V7's consumer predates the zero-codec rule (G): the smoke's client calls `UseSerde` with a stub (K), and the measured result carries over. |
| V8 | The 8a kit (`TransportSuite.RunAllAsync`) over the DI-built transport, both faces, with the SystemNet drivers' two permanent waivers | **Red** as first built: `transport-15.owned-released` failed (the pooled connection outlived the disposed container, because the factory owns and never releases a handler with an infinite lifetime). **Green** after a container-owned lease disposes the primary handler the package created: 59 passed, 4 waived (the two waivers on two faces), 5 not exercised (hooks not supplied), 0 failed; `transport-1.redirect-not-followed` passed on both faces. | The lease is part of the design (P9-13); the third driver is feasible and the 8a hand-off's proof holds (P9-22). |
| V9 | The factory's default logging handlers | At `Information`, per request: `Start processing HTTP request GET http://127.0.0.1:…/secret-path/token123?*` and `Sending HTTP request …` (query reduced to `*`, userinfo dropped, **path verbatim**), plus header-name dumps at `Trace`. | A second, differently-redacting HTTP log stream beside the SDK's own (`OBS-11`–`OBS-18`); the named client removes it (P9-14). |

---

## Scope: the rows

Legend (roadmap constraint 3). This is a design, so a mark is the disposition the checklist is expected to record, with the test
that will prove it. **Split** means one clause of the row is ✅ and another is 🚫 on an existing §10 entry; no new §10 entry is opened
by this phase (P9-9).

### Rows phase 9 owns (15)

| ID | Level | Disposition | Mechanism and evidence (planned test, `[Trait]`) |
|---|---|---|---|
| `CFG-1` | MUST | ✅ (mechanism per §10 entry 25) | Tier 4 is the record's initializer, tiers 2 and 3 are the caller's `IConfiguration` providers (environment variables above `appsettings.json` in every default host; §11 item 4), tier 1 is `DexpaceClientBuilder.Configure(Func<DexpaceClientOptions, DexpaceClientOptions>)`, applied last, in call order (P9-6). `ConfigurationPrecedenceTests` (`Unit`): three in-memory providers standing for appsettings, environment and an override, plus the transform; removing each from the top resolves the next (ports `configuration.test.ts` "layered precedence"). |
| `CFG-2` | MUST | **Split**: ✅ "treated as absent … rather than resolving to the empty string"; 🚫 "falls through to the system-property layer" (§10 entry 25) | An empty or whitespace-only value is absent: the record's default applies (P9-7, **open for the lead**: the shadowed lower provider's value is dropped silently; the alternative is a start-up failure). `IConfiguration` merges providers before the reader sees a key, so a lower provider's value that an empty higher one shadows is not consulted. `EmptyValueTests` (`Unit`): empty env-provider value over a JSON-provider value → the record default, never `""`, for a string, an integer, a duration and a list key. |
| `CFG-3` | MUST | ✅ (normalisation per §10 entry 25) | .NET's rule: `Dexpace__Retry__MaxRetryAttempts` → `Dexpace:Retry:MaxRetryAttempts`, case-insensitive. `EnvironmentNormalizationTests` (`Integration`): a process variable under a per-test GUID prefix through `AddEnvironmentVariables(prefix)` (in `Microsoft.Extensions.Configuration.EnvironmentVariables`, a test-only `PackageVersion` outside V5's closure), removed in `finally`; also proves `CFG-11`'s production default. |
| `CFG-4` | MUST | N/A (vacuous; §8.2, §10 entry 25, §12) | Nothing normalises casing away, so there is no raw accessor to provide. `EnvironmentNormalizationTests` pins that a camel-case and an upper-case spelling of one key resolve alike. |
| `CFG-5` | MUST | **Split**: ✅ base-10 integers, negatives returned as-is; 🚫 "MUST NOT throw" (§10 entry 26) | Trimmed, `[+-]?[0-9]+` in ASCII, in the target's range (`int` or `long`); `0x10`, `1_000`, `1e3`, `1.5` and overflow fail `ValidateOnStart` naming the key (V1, V2). A negative is parsed and handed to the record, whose accessor decides (`MaxRetryAttempts = -1` is the record's rejection, reported under the key). `IntegerGrammarTests` (`Unit`), ported from `configuration_test.rb` (`010` → 10, `-5` → −5; `1_000` is **rejected** here where Ruby tolerates it, P9-26). |
| `CFG-6` | MUST | **Split**: ✅ only case-insensitive `true`/`false` (after an ASCII trim); 🚫 fall-back-to-default (§10 entry 26) | `1`, `0`, `yes`, `no`, `on`, `off`, `t`, `f` fail naming the key. Surrounding ASCII whitespace is trimmed before the token test (Ruby does not; P9-27, recorded as a §11 resolution, P9-29). `BooleanGrammarTests` (`Unit`), ported. |
| `CFG-7` | MUST | **Split**: ✅ the grammar, read as the day-time ISO subset with digits-only bare numbers (P9-29); 🚫 return-the-default on rejection (§10 entry 26) | The reader's own grammar (P9-8): ISO-8601 with a leading `P`/`p`, shorthand `<number><unit>` (`ms`, `s`, `m`, `h`, `d`, case-insensitive), a bare integer as milliseconds; a negative in any form, an unknown unit, `P`, `PT`, `Y`/month/`W` designators and more than seven fractional second digits fail. `DurationGrammarTests` (`Unit`): the `configuration_test.rb` table and `duration.test.ts` rows, plus V2's four `XmlConvert` divergences as explicit cases, plus the three-grammars-one-scale property over 0–100 000 s. |
| `CFG-10` | MUST | N/A (vacuous; §8.2, §10 entry 25, §12) | No override map exists to remove from: a transform is code, and a record has no removal. Unchanged from the design. |
| `CFG-11` | MUST | ✅ | The seams are `IConfiguration` providers (`AddInMemoryCollection` in every test; no test reads the real environment except `EnvironmentNormalizationTests`, which proves the production default delegates to it) and `UseProxyFromEnvironment(Func<string, string?>)`, the lookup seam of the one environment resolver. `ConfigurationSeamTests` (`Unit`): a recording lookup observes every proxy variable the resolver reads, and no other. |
| `CFG-14` | SHOULD | ✅ | `DexpaceConfigurationKeys`: `SectionName = "Dexpace"`, `MaxRetryAttempts = "Retry:MaxRetryAttempts"`, `LogLevel = "Logging:Level"`, `HttpProxy`, `HttpsProxy`, `NoProxy` (P9-23). `KeyConstantsTests` (`Unit`): the values; the proxy names equal what `ProxyOptions.FromEnvironment` reads (recording lookup); no member of the package reads `SectionName` (IL scan, the `keys_test.rb` "published, not read" rule, which is also `OBS-35`'s no-baked-key clause). |
| `CFG-37` | MUST | ✅ | `ArgumentNullException.ThrowIfNull` on every public parameter of the extension class and the builder; `ArgumentException` for a blank client name. `NullGuardTests` (`Unit`): every public method, every reference parameter, by reflection over the public surface (a test-only use of reflection). |
| `CFG-38` | MUST | ✅ | Every typed value reaches its parser through the one reader over the one `IConfiguration`. `TypedLookupTests` (`Unit`): an integer, a boolean and a duration supplied only by a lower provider resolve; the record default applies only when no provider has the key. |
| `SEAM-5` | MUST | DI half ✅; DI-less half N/A (2b) | Explicit `UseTransport`/`UseSerde` wins and replaces the package's default; no codec → `ValidateOnStart` fails with the install hint naming `UseSerde` and the client; two or more keyed `IAsyncHttpClient` or `ISerde` descriptors for one client → fails listing every registered implementation (P9-16). `SingleRegistrationTests` (`Unit`). |
| `SEAM-6` | MUST | DI half ✅; DI-less half N/A (2b) | `UseTransport(a)` twice with the same instance (or the same factory delegate) is a no-op; a different one throws `InvalidOperationException` at the call, naming the client and both implementation types (P9-16, which refines §3.6's "the validator's failure" to fail earlier). `SingleRegistrationTests`. |
| `OBS-35` | SHOULD | **Split**: ✅ layered, case-insensitive, trimmed level by name, no baked key; 🚫 fall-back on an unrecognised value (§10 entry 26, P5b-16) | `Logging:Level` parsed by name only (`None`, `Headers`, `Body`); a numeric or unknown value fails naming the key and listing the names (V1 showed the binder admits both). The section is always the caller's argument (P9-23). `LogLevelBindingTests` (`Unit`). |

### Rows owned elsewhere that phase 9 adds evidence to (3)

| ID | Owner | Disposition in the owner's checklist after 9 | Phase 9's evidence |
|---|---|---|---|
| `SEAM-7` | 2b (N/A) | N/A stays; dated correction cites the test | Each client's transport, codec and pipeline are keyed singletons created once (the "cached process-wide" reading); "an unresolved state stays re-evaluable" has nothing to apply to (a built container gains no registrations). `KeyedSingletonTests` (`Unit`): 64 concurrent first resolutions run each factory once. |
| `TRANSPORT-1` | 8b (✅) | ✅; dated correction adds the named-client clause | The named client's primary handler is `SystemNetHttpClient.CreatePrimaryHandler` (`AllowAutoRedirect = false`); the 8b check refuses a caller-substituted redirect-following primary. `NamedClientRedirectWireTests` (`Security`, phase 1 S3's row 9) and `transport-1.redirect-not-followed` on the third driver (`Conformance`). |
| `XCUT-10` | 10 | ⏳ 10 (the row); phase 9's clause ✅ | The package registers no retrying handler (`NoRetryingHandlerTests`, `Unit`: the named client's chain holds no handler the package added besides the primary); the documented composition sends once per SDK attempt (`ResilienceCompositionTests`, `Integration`, V6) (P9-19). |

**Tally of the 15 owned rows: 13 ✅ (`CFG-1`, `-2`, `-3`, `-5`, `-6`, `-7`, `-11`, `-14`, `-37`, `-38`, `SEAM-5`, `SEAM-6`, `OBS-35`; of which 5 split with a 🚫 clause on an existing §10 entry: `CFG-2`, `CFG-5`, `CFG-6`, `CFG-7`,
`OBS-35`), 2 N/A (`CFG-4`, `CFG-10`), 0 🚫, 0 ⏳.** `SEAM-5` and `SEAM-6` count as ✅ for their DI half. Of the 3 contributed rows:
`SEAM-7` N/A, `TRANSPORT-1` ✅, `XCUT-10` ⏳ 10. By level, the 15 owned rows are 13 MUST and 2 SHOULD (`CFG-14`, `OBS-35`).

No row adds a clause to the *Unsatisfied MUSTs* count: the never-throw clauses of `CFG-5`–`CFG-7` are already §10 entry 26 (one of
the seven entries the 8b addendum counts), and `CFG-2`'s fall-through is §10 entry 25, a judged mechanism change that the count does
not include. Phase 11 may disagree about entry 25; the design says so in the hand-offs.

### Non-row hand-offs addressed to 9

| From | Hand-off | Disposition |
|---|---|---|
| 5a | "bind the records (the staging-type fallback if the binder cannot set `init`)" | **Taken, by a third route.** The generator sets no `init` accessor and the reflection binder is not AOT-safe (V1); a staging type would still need the generator to convert into it, and the generator's conversions are wrong for `CFG-5`–`CFG-7`. The reader builds each record with object initialisers and `with` (P9-4). |
| 5a (P5a-4) | "`IValidateOptions` for the cross-property and range rules" | **Taken, narrowed.** The range rules landed in the accessors in 6a/6b; the reader maps each accessor rejection to a failure naming the key. No cross-property failure is added for combinations the records accept (`BaseDelay > MaxDelay` caps, 6a's ruling); the user page documents them (P9-10, open). |
| 5a (P5a-16), 8b (P8b-4) | "`FromEnvironment(key => configuration[key], logger)`"; "`Proxy` through the one explicit `FromEnvironment` call" | **Taken as code-only:** `UseProxyFromEnvironment()` and `UseProxyFromEnvironment(Func<string, string?> lookup)` are the one explicit call; `UseProxyFromEnvironment(key => builder.Configuration[key])` is the documented `IConfiguration` form. No configuration value switches the environment read on (P9-15). |
| 5b (P5b-16) | `OBS-35`, "V4 (the binder's enum conversion) was not run" | **Taken**: row above; V4 answered by V1. |
| 6a | "binds `IReadOnlySet<int> RetryableStatusCodes` (through a staging type if needed)" | **Taken**: a list key of base-10 integers (P9-8); V1 shows both binders drop it silently. |
| 6a, issue #94, §6.1, §11 item 24 | "tells consumers of `AddStandardResilienceHandler` to set `MaxRetryAttempts = 0`" | **Taken, refined** (V6): the SDK-side `MaxRetryAttempts = 0` option is withdrawn (it leaves a handler retry that re-sends bodies behind the SDK's gate, `XCUT-10`); the handler-side `o.Retry.MaxRetryAttempts = 0` is refused by the handler's own validation; the guidance is `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`, pinned by a test (P9-19). |
| 6b | "`AllowedMethods` needs a string to `Method` conversion (`Method.Of`); `Predicate` is code-only" | **Taken**: a list key through `Method.Of` (which trims and never echoes, `HTTP-20`); `Predicate` through `Configure`. |
| 6c | "DI registration of an auth policy (an `AuthCredentials` bound from configuration)" | **Taken** (P9-18). |
| 7a | "DI registers `ISerde` from `CreateDefaultOptions(context)`" | **Taken as composition, not a reference**: `UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default)))` is the documented form and a test in the DI test project; the package does not reference the codec (P9-17, open). |
| 7c | "nothing (pageables are per-call)" | Confirmed: nothing. |
| 8a | "a third driver over the transport `AddDexpaceClient` builds from `IHttpClientFactory`; `transport-1.redirect-not-followed` is the proof" | **Taken** (P9-22, V8). |
| 8b | "the handler constructor is the named clients' primary-handler seam" | **Taken**, with one SystemNet addition: `CreatePrimaryHandler` (P9-12, open). |
| 8b | "`SystemNetHttpClientOptions` is the binding target" | **Taken**: `Transport:*` keys; `Logger` and `TimeProvider` come from the container, never from configuration. |
| 8b (P8b-18), 4a (P4a-13) | "the `CallKey` stamp lands with its first reader" | **Not built** (P9-20, open): phase 9 has no reader; routed to a `docs/first-release.md` post-release trigger and to phase 11's inbound list for §11 item 30. |
| Phase 1 (S3) | "the DI package configures each named client's primary handler with `AllowAutoRedirect = false`" (evidence `RedirectWireTests`) | **Taken**: `NamedClientRedirectWireTests` (`Security`) in the DI test project; the phase-1 `Security` classes are untouched. |
| 2b | `SEAM-7` "under DI, caching is the singleton lifetime, which phase 9 cites" | **Taken**: `KeyedSingletonTests`. |

---

## Argued positions

### A. One package over core, the reference transport and one third-party root (P9-2, P9-3)

**The need.** Design §2.1 lists the package's dependencies as core plus the hosting family. The card adds named clients whose
primary handler is built for the 8b handler constructor, which lives in `Dexpace.Sdk.Http.SystemNet`; 7a's hand-off adds the codec.

**Options.** (1) Reference core, `Dexpace.Sdk.Http.SystemNet` and `Microsoft.Extensions.Http` (whose closure is the family, V5);
(2) reference core only and let a consumer wire the transport (then the package cannot build the card's named client); (3) also
reference `Dexpace.Sdk.Serialization.SystemTextJson` and default the codec. **Chosen: (1).** (2) drops `TRANSPORT-1`'s named-client
clause, the 8a driver and phase 1's S3 row-9 hand-off. (3) makes every DI consumer carry a codec it may not use and picks the
format for them; `SystemTextJsonSerde`'s own constructor is one line at the call site (P9-17). (1) means a consumer who supplies
its own transport still carries the SystemNet assembly; it is small and over the in-box `System.Net.Http`, and it is recorded
against `NFR-2` in §10 entry 2's correction. **Open for the lead** (the SystemNet reference departs from §2.1's cell).

**Third-party root.** The csproj references exactly one package, `Microsoft.Extensions.Http` (10.0.12, a new `PackageVersion` in the
`Runtime` group). `DependencyInjection.Abstractions`, `Options`, `Configuration.Abstractions` and `Logging.Abstractions` are in its
closure (V5) and are used transitively, so the nuspec lists core, SystemNet and `Microsoft.Extensions.Http` (plus, optionally, the
pinned facade). `Microsoft.Extensions.Options.ConfigurationExtensions` and `Configuration.Binder` arrive transitively and are not
called (P9-4's ban).

**The audit (P9-3).** `scripts/ci/dependency-audit.cs` holds each library to "core, plus at most one third-party library", reads
the allowed packages as the closure of that one root, and refuses any project reference outside core's closure. Phase 9 changes
exactly this:

- the policies table's value becomes a record, `Policy(string? ThirdParty, string[] Projects)`; the existing four libraries keep
  `ThirdParty = null, Projects = []`, and `Dexpace.Sdk.Extensions.DependencyInjection` is
  `new("Microsoft.Extensions.Http", ["Dexpace.Sdk.Http.SystemNet"])`;
- `deps.json`: the allowed packages stay `Closure(Facade) ∪ Closure(ThirdParty)` (the 13 of V5, all band-matched); the allowed
  projects become `Closure(Core) ∪ ⋃ Closure(p) for p in Projects ∪ {library}`;
- nuspec: the expected set becomes `{Core} ∪ Projects ∪ {ThirdParty}` (the facade optional, as now), and every listed Dexpace
  project must carry the library's own lockstep version, the check core already gets;
- the header comment names the reading: "at most one third-party library" counts a root package whose closure is one publisher's
  lock-stepped family as one library (§10 entry 2), and first-party Dexpace packages are not third-party.

`NFR-2` is a SHOULD and phase 10's row; §10 entry 2 already judges it. The correction this phase owes to entry 2 is a measurement,
not a new argument: the family is reached through one root, its closure is the 13 packages of V5, and the package also references
the reference transport.

### B. Registration: named, keyed, returning a builder (P9-5, P9-6)

```csharp
services.AddDexpaceClient("github", builder.Configuration.GetSection("Dexpace:GitHub"))
    .UseSerde(_ => new SystemTextJsonSerde(GitHubJsonContext.Default))
    .UseAuth(GitHubAuth.Descriptor)
    .Configure(o => o with { Redirect = o.Redirect with { Predicate = GitHubRedirects.Allow } });

public sealed class GitHubClient([FromKeyedServices("github")] HttpPipeline pipeline,
                                 [FromKeyedServices("github")] ISerde serde,
                                 [FromKeyedServices("github")] DexpaceClientOptions options) { … }
```

- **Named clients as keyed singletons.** One call registers, under the key `name`: `HttpPipeline`, `IAsyncHttpClient` (the
  transport), `ISerde` and `DexpaceClientOptions` (the snapshot a generated client needs for `OperationDescriptor.BuildRequest`).
  Keyed services are the house rule for several implementations of one role (styleguide aspnetcore 2.6) and are AOT-clean (V5).
- **No generic typed-client overload** (`AddDexpaceClient<TClient>`). A generated SDK ships its own `AddGitHubClient(this
  IServiceCollection, IConfiguration)` that calls `AddDexpaceClient` and registers its client against the keyed services; a generic
  overload would need `ActivatorUtilities` with trimming annotations for a convenience that is additive later (`NFR-4` locks every
  name at the first release). **Open for the lead.**
- **A sealed builder class, not an interface.** `DexpaceClientBuilder` carries `Name`, `Services` and `HttpClient` (the
  `IHttpClientBuilder` of the named client, so `AddHttpMessageHandler`, `AddStandardResilienceHandler` and the like compose
  underneath), and the `Use*`/`Configure*` methods. Third-party adapters extend it with extension methods, which a sealed class allows.
- **One name registers once** (`SEAM-6` applied to the client): a second `AddDexpaceClient` for the same name returns the existing
  builder when the configuration argument is the same instance (or both absent) and throws `InvalidOperationException` naming the
  client otherwise.
- **Tier 1 is a transform** (P9-6). The records are immutable (`CFG-8`), so §8.2's `Action<DexpaceClientOptions>` cannot compile;
  `Configure(Func<DexpaceClientOptions, DexpaceClientOptions>)` and `ConfigureTransport(Func<SystemNetHttpClientOptions,
  SystemNetHttpClientOptions>)` take `with` transforms, applied after the configuration values in call order. A transform returning
  `null` is a failure naming the client.
- **What the container supplies.** `ILoggerFactory` (registered by `AddHttpClient`) gives the pipeline the category `Dexpace.Sdk` and
  the transport `Dexpace.Sdk.Http.SystemNet`; `TimeProvider` is `GetService<TimeProvider>() ?? TimeProvider.System`, so a test
  registers a `FakeTimeProvider` and nothing is registered on the caller's behalf.
- **Factories run on the root provider.** `UseSerde(Func<IServiceProvider, ISerde>)` and the like resolve from the root, so a scoped
  dependency there fails under `ValidateScopes` (the house rule 2.4, which the provider test turns on).

### C. Configuration is read by the package, not bound by the binder (P9-4, P9-7, P9-8, P9-9)

**The tension.** Design §8.2 expects the binder plus two registered converters, with a staging type if the generator cannot set
`init`. V1 shows the generator sets nothing (silently), and the reflection binder is 64 AOT warnings, mis-converts `TimeSpan`, `int`
and enums, drops two set members, appends to a list default, and quotes the value it failed on. Converters cannot be "registered"
with the generator at all: it emits `int.Parse` and `TimeSpan.Parse` calls of its own.

**Options.** (1) The reflection binder with `RequiresUnreferencedCode` suppressions (rejected: `IsAotCompatible` exists to stop
exactly this, and V1's silent drops would still need hand-written code); (2) a staging type for the generator, with `string` members
for every typed value and hand-written conversion into the records (rejected: the generator then contributes only the reading of
strings by key, which `IConfiguration`'s indexer already does, plus the `ErrorOnUnknownConfiguration` key check, which is a few
lines); (3) **a hand-written reader**: an internal `ConfigurationReader` that walks the section by known keys, converts each value
with the package's own grammar, and builds each record with object initialisers and `with`. **Chosen: (3).** It is AOT-safe with no
generator, implements `CFG-5`–`CFG-7` exactly, can name every failure by key without quoting the value, and gives every collection
replace semantics. The cost is that the key table must follow the records; a drift test guards it (below). `BannedSymbols.txt` gains
`T:Microsoft.Extensions.Configuration.ConfigurationBinder` so no `src/` code calls either binder. **Open for the lead**, because it
departs from §8.2's text and from the styleguide companion's 1.3 ("the Options pattern is the only sanctioned reader"): the reader is
the binding layer itself and no consumer code indexes `IConfiguration`, which is 1.3's intent; the conflict is recorded as a
knowledge note (constraint 6) at close-out.

**Tiers** (`CFG-1`, §8.2, §11 item 4): the record initializer < the caller's `IConfiguration` (its providers already ordered:
environment variables over `appsettings.{Environment}.json` over `appsettings.json` in a default host) < `Configure` transforms.

**The section.** The caller passes the client's section; the package never reads a key the caller did not hand it, so no default
key is baked in (`OBS-35`'s MUST NOT, P9-23). `AddDexpaceClient(name)` with no configuration binds nothing and uses the defaults
plus transforms. The parameter is an `IConfigurationSection`, not an `IConfiguration`: every failure is `"<path>: <rule>"` and only a section has a `Path`, and a root would put every process variable (`PATH`, `HOME`, `Logging`, `AllowedHosts` in a default host) under the unknown-key rule, so passing the root could never boot. A section with an empty `Path` (`GetSection("")`) is rejected at registration with a pointed message ("pass the client's own section, not the root").

**The key table.** Keys are relative to the section, case-insensitive, `:`-separated (`__` in environment variables).

| Key | Target | Grammar |
|---|---|---|
| `BaseAddress` | `DexpaceClientOptions.BaseAddress` | absolute URI (`Uri.TryCreate`, `UriKind.Absolute`); the record checks scheme and fragment |
| `UserAgent` | `.UserAgent` | string, checked with the header-value rule (no CR, LF or other control: the record's accessor checks only for `null`, so a bad value would otherwise pass start-up and fail on every call); a blank `User-Agent` is code-only, since an empty value is absent |
| `OverallTimeout`, `AttemptTimeout` | `.OverallTimeout`, `.AttemptTimeout` | duration or `none` |
| `Retry:MaxRetryAttempts` | `RetryOptions.MaxRetryAttempts` | integer |
| `Retry:BaseDelay`, `Retry:MaxDelay` | `.BaseDelay`, `.MaxDelay` | duration |
| `Retry:FixedDelay` | `.FixedDelay` | duration or `none` |
| `Retry:Multiplier`, `Retry:Jitter` | `.Multiplier`, `.Jitter` | decimal (`NumberStyles.Float`, invariant, finite) |
| `Retry:HonorRetryAfter` | `.HonorRetryAfter` | boolean |
| `Retry:RetryableStatusCodes` | `.RetryableStatusCodes` | list of integers, or `none` for the empty set |
| `Retry:AttemptHeaderName` | `.AttemptHeaderName` | string |
| `Redirect:MaxRedirects` | `RedirectOptions.MaxRedirects` | integer |
| `Redirect:AllowHttpsToHttpDowngrade`, `Redirect:FollowSeeOther` | the two flags | boolean |
| `Redirect:AllowedMethods` | `.AllowedMethods` | list of method tokens through `Method.Of`, or `none` |
| `Logging:Level` | `HttpLoggingOptions.Level` | enum by name (`OBS-35`) |
| `Logging:BodyPreviewSize` | `.BodyPreviewSize` | integer |
| `Logging:AllowedHeaderNames`, `Logging:AllowedQueryParameters`, `Logging:UrlValuedHeaderNames` | the three lists | list of strings, or `none` |
| `Logging:OmitDisallowedHeaders` | `.OmitDisallowedHeaders` | boolean |
| `Transport:Timeout` | `SystemNetHttpClientOptions.Timeout` | duration or `none` (no transport default) |
| `Transport:HeaderDropLogging` | `.HeaderDropLogging` | enum by name |
| `Transport:ResendBufferLimit` | `.ResendBufferLimit` | integer (`long`) |
| `Transport:PooledConnectionLifetime` | the primary handler (P9-13) | duration or `none`; default 2 minutes |
| `Transport:Proxy:Type`, `Host`, `Port`, `NonProxyHosts`, `UserName`, `Password`, `BypassAll` | `ProxyOptions` | enum by name; string; integer; list of strings; string; string; boolean (P9-15) |
| `Auth:ApiKey:Key`, `HeaderName`, `Scheme` | `AuthCredentials.ApiKey` (`new ApiKeyCredential(key, header, scheme)`) | strings |
| `Auth:Basic:Username`, `Password`; `Auth:Digest:Username`, `Password` | `.Basic`, `.Digest` | strings |
| `Auth:TokenRefreshMargin` | `.TokenRefreshMargin` | duration |

The three `Logging:*` lists take any string: a name that is not a token simply never matches a header or query parameter, so the reader does not validate them.

Code-only, never read from configuration: `RedirectOptions.Predicate`, `SystemNetHttpClientOptions.Logger` and `TimeProvider`,
`ProxyOptions.ChallengeCredentials`, `AuthCredentials.Token`. **Drift guard:** `KeyTableTests` (`Unit`) enumerates, by reflection (test-only), every public `init`
property of the seven records (the six option records and `AuthCredentials`) and every public constructor parameter of the three credential
classes (`ApiKeyCredential`, `BasicCredential` and `DigestCredential` are classes with get-only properties set by their constructors, so
there is no `init` member on them to find), and asserts each is in the key table or on the code-only list; every key-table entry maps to a
real member or carries an explicit **handler-level** category (`Transport:PooledConnectionLifetime` configures the primary handler, not a
record property). A member added later fails the test, not a deployment.

**The grammars** (P9-8; `CFG-5`–`CFG-7`, `OBS-35`). Every scalar value is trimmed of ASCII whitespace first (P9-27); a value that is
then empty is **absent** (P9-7), and the record default applies.

- *Duration* (`CFG-7`): `none` (case-insensitive) is `null` where the target is nullable and a failure otherwise. A value starting
  with `P` or `p` is ISO-8601 under the grammar `P[nD][T[nH][nM][n[.f]S]]` (upper-cased first; `n` ASCII digits; `f` one to seven
  digits, the tick resolution, so `PT0.00000001S` is a failure, not V2's silent zero; at least one component; `T` only with a time
  component); `Y`, a date-part `M` (Node's "ambiguous month") and `W` fail. Otherwise `<number>[ ]<unit>` with `number` digits with an
  optional fraction and `unit` one of `ms`, `s`, `m`, `h`, `d` (case-insensitive; Ruby's `"250 ms"` with a space is admitted). Otherwise
  digits alone, as milliseconds (a bare fraction such as `1.5` fails, where both siblings accept it). A sign anywhere fails (`-PT5S`, `PT-5S`, `-3s`, `-1`). A result above `TimeSpan.MaxValue` fails; the
  target's own range (timeouts `(0, 49 days]`) is the record's. These readings narrow `CFG-7`'s "ISO-8601 durations" to the day-time
  subset (Java's `Duration.parse`, the reference, accepts the same; the fraction cap is the tick, seven digits, where Java allows nine) and its
  "bare number" to digits; they are recorded as a §11 resolution (P9-29, open for the lead).
- *Integer* (`CFG-5`): `[+-]?[0-9]+`, ASCII digits only, parsed with `NumberStyles.AllowLeadingSign` and the invariant culture into
  the target type; leading zeros are decimal. Hex, separators, exponents, fractions, non-ASCII digits and overflow fail (V2).
- *Boolean* (`CFG-6`): `true` or `false`, case-insensitive, after the ASCII trim (P9-27, P9-29). Nothing else.
- *Decimal*: `NumberStyles.Float`, invariant, finite (`NaN` and infinities fail; the record's range applies after).
- *Enum*: a case-insensitive match against `Enum.GetNames<T>()` (AOT-safe); a numeric string or any other name fails, and the
  failure lists the names (`OBS-35`'s tolerant matching, with fail-fast instead of fallback, §10 entry 26).
- *List*: either child keys (`Key:0`, `Key:1`, …, in the configuration's order) or one scalar split on commas and each item trimmed;
  both present is a failure; `none` is the empty list; a list **replaces** the record's default (V1's append is avoided).
  Items parse with the element grammar.

**Failures** (P9-9). The reader never stops at the first failure: it builds each record member by member (`retry = retry with {
MaxRetryAttempts = v }` under a `catch (ArgumentException)`), so one boot reports every bad key. A failure reads `"<path>: <rule>"`,
where `<path>` is the section's `Path` plus the key and `<rule>` the grammar's or the record accessor's message. **No failure quotes a
configured string, URL or credential** (a value can be a password, an API key or a URL with a token; `XCUT-19`), which the core messages
honour (`Method.Of`, `HttpHeaderName`, `UrlRedactor` for `BaseAddress`). One exception is deliberate (P9-28, open for the lead): an
accessor's numeric range message may echo the rejected **number**, because the BCL throw helpers print it (`ArgumentOutOfRangeException.ThrowIfNegative`:
"value ('-7') must be a non-negative value", for `RetryOptions.MaxRetryAttempts`, `RedirectOptions.MaxRedirects`,
`HttpLoggingOptions.BodyPreviewSize`, `SystemNetHttpClientOptions.ResendBufferLimit` and `AuthCredentials.TokenRefreshMargin`); no numeric option
carries a secret, and the alternative (a per-key rule text replacing the accessor's message) is a table that must follow the records. Unknown keys under the section fail too, naming the key (the
binder's `ErrorOnUnknownConfiguration`, made the default): a misspelt key is a misconfiguration nobody sees otherwise. **Open for the
lead**: the alternative is to ignore unknown keys, which keeps a generated SDK's own settings in the same section legal.

### D. Validation and its timing (P9-11)

One internal options type per client name, `DexpaceClientState` (mutable, internal; the options pattern's `TOptions`), holds the
snapshot (`DexpaceClientOptions`, `SystemNetHttpClientOptions` without logger and clock, `ProxyOptions?`, `AuthCredentials`, the
pooled-connection lifetime) and the failure list. It is wired as:

- `AddOptions<DexpaceClientState>(name)`, configured by an internal `IConfigureNamedOptions<DexpaceClientState>` that runs the reader
  and then the transforms;
- one `IValidateOptions<DexpaceClientState>` (`TryAddEnumerable`) that returns the reader's failures plus the wiring checks (`SEAM-5`,
  P9-16; the proxy and auth checks, P9-15 and P9-18);
- `.ValidateOnStart()`, so a host fails its start with `OptionsValidationException` listing every failure (V4);
- every keyed factory reads `IOptionsMonitor<DexpaceClientState>.Get(name)` first, so with no host the first resolution fails the
  same way (V4). A client that registers no codec therefore fails `IStartupValidator.Validate()` **and** the resolution of each of its keyed
  services, its transport included: every test and sample that resolves a client calls `UseSerde` (a shared stub in the tests), and so does the
  AOT smoke (K).

**No reload.** The snapshot is taken once, at first resolution; the package registers no `IOptionsChangeTokenSource`, so a reloaded
`appsettings.json` does not reach a built client. A built client is immutable (`CFG-8`), and a pipeline rebuilt mid-flight would
orphan the token cache and the transport's per-instance state. Documented; the styleguide companion's 1.5 asks the accessor choice
to be stated, and it is (`IOptionsMonitor` is the only named accessor; its change callback is not subscribed).

**Cross-property rules** (P9-10, P5a-4's remainder). None fails. The combinations the records accept keep their core meaning:
`BaseDelay > MaxDelay` waits `MaxDelay` (6a), an `AttemptTimeout` above `OverallTimeout` never fires, a `Transport:Timeout` below
`AttemptTimeout` bounds the attempt at the transport. Failing them only under DI would give one record two validities. A warning
would need new event ids, which live in core's `DexpaceLogEvents` (P8b-13), so the user page lists them instead. **Open for the
lead** (the alternative is a 170–179 event block in core and three warnings).

### E. The named client and its transport (P9-12, P9-13, P9-14)

Per client the package calls `services.AddHttpClient("Dexpace." + name)` and:

1. **`ConfigurePrimaryHttpMessageHandler(sp => …)`** builds the primary with a new public SystemNet method,
   `SystemNetHttpClient.CreatePrimaryHandler(SystemNetHttpClientOptions options)`, which returns the `SocketsHttpHandler` the owned
   construction builds today: `AllowAutoRedirect = false`; `UseProxy = options.Proxy is not null` with the internal
   `ProxyOptionsWebProxy`; server `Credentials` unset; the `TRANSPORT-30` warnings (event 113) through `options.Logger`. The package
   then sets `PooledConnectionLifetime` on it (the package's own instance) to `Transport:PooledConnectionLifetime`, default 2 minutes,
   the factory's own default rotation interval, so DNS changes are picked up although the chain is held for the process (step 3).
   **Why a new SystemNet method** (open for the lead): the proxy adapter is internal to SystemNet, and the handler constructor refuses
   `options.Proxy` (`TRANSPORT-15`: a caller's handler owns its proxy). The alternatives were a public `ProxyOptionsWebProxy`
   (rejected: a second public type for one use; the rationale is single-sourcing the owned-handler invariants and a smaller public
   surface, not a credentials guard, since `CreatePrimaryHandler` also returns a mutable `SocketsHttpHandler` on which a caller can set
   server `Credentials`, which P8b-5's caller-handler rule leaves to the caller) and `InternalsVisibleTo` the DI package (rejected: the convention grants it to a library's own test project only). The
   method single-sources the owned-handler invariants, and the existing `CreateOwnedClient` is refactored onto it.
2. **`RemoveAllLoggers()`** (P9-14): V9's default logging handlers write the request URI with its path verbatim at `Information`,
   beside the SDK's own redacted `http.request`/`http.response` events. One HTTP log authority, the SDK's; a caller who wants the
   factory's logs adds them back on `builder.HttpClient`.
3. **`SetHandlerLifetime(Timeout.InfiniteTimeSpan)`** (P9-13). The transport is a singleton that holds one handler chain from
   `IHttpMessageHandlerFactory.CreateHandler("Dexpace." + name)` for its life, so factory rotation would never reach it; the infinite
   lifetime states that, and step 1's `PooledConnectionLifetime` does the rotation's job (DNS) inside the one handler, as Microsoft's
   guidance for long-lived clients prescribes.
4. **The transport** is `new SystemNetHttpClient(chain, transportOptions with { Proxy = null, Logger = …, TimeProvider = … })`, the 8b
   handler construction. Its check walks the chain and refuses a primary that follows redirects (V3), so a caller who replaces the
   primary through `builder.HttpClient.ConfigurePrimaryHttpMessageHandler(...)` with a default handler fails at first resolution:
   `TRANSPORT-1` holds by construction and by the backstop.
5. **The lease** (P9-13, V8). The factory owns the chain and never releases a handler whose lifetime is infinite, so disposing the
   container left the pool open (`transport-15.owned-released` failed). The primary is the package's own creation, so the package
   releases it: an internal `PrimaryHandlerLease : IDisposable`, registered per client as a keyed singleton **factory** (the
   container disposes what its factories return) and resolved by the transport factory first, records the instance step 1 built and
   disposes it when the container disposes; the transport (created later) is disposed before it. The caller's `DelegatingHandler`s
   are the factory's and are not touched (`XCUT-22`). If the caller replaced the primary, the lease holds the discarded, never-used
   instance and disposes only that.

`builder.HttpClient.ConfigureHttpClient(...)` (`BaseAddress`, `Timeout`, `DefaultRequestHeaders`) has no effect: the package uses the
handler, never `CreateClient`. The user page says so; `BaseAddress` is `DexpaceClientOptions.BaseAddress`.

### F. Proxy (P9-15)

`CFG-28` (strict, P8b-4): nothing reads the environment unless a resolver is explicitly invoked. Two sources, at most one per client:

- **Explicit keys** `Transport:Proxy:*` build a `ProxyOptions` (`CFG-24`'s first-layer stand-in, §10 entry 25). `Host` and `Port` are
  required when any proxy key is present.
- **`UseProxyFromEnvironment()`** calls `ProxyOptions.FromEnvironment(logger)` (the process environment, the one sanctioned site,
  P5a-17), and **`UseProxyFromEnvironment(Func<string, string?> lookup)`** calls `FromEnvironment(lookup, logger)`; the documented
  `IConfiguration` form is `UseProxyFromEnvironment(key => builder.Configuration[key])`, which serves `HTTPS_PROXY` from any provider.
  The call is resolved at the transport's first resolution, never at registration (a registration-time read would run before the
  host's configuration exists).

Both present is a `ValidateOnStart` failure ("two proxy sources"); `ConfigureTransport` setting `Proxy` counts as an explicit
source. **No configuration value turns the environment read on** (no `Transport:Proxy:FromEnvironment = true`): a value that makes the
package read the environment is the implicit read P8b-4 forbids by another route. **Open for the lead.** The extended
`NoImplicitEnvironmentReadArchitectureTests` scan (8b's) is copied into the DI test project: in `Dexpace.Sdk.Extensions.DependencyInjection`,
`ProxyOptions.FromEnvironment` is called only from the two `UseProxyFromEnvironment` paths and `Environment.GetEnvironmentVariable(s)`
from nowhere.

### G. Single registration (`SEAM-5`, `SEAM-6`, `SEAM-7`; P9-16)

- **The default.** `AddDexpaceClient` registers the SystemNet named-client transport as the client's keyed `IAsyncHttpClient` and
  keeps the `ServiceDescriptor` it added.
- **Explicit install wins** (`SEAM-5`'s precedence): `UseTransport(IAsyncHttpClient)` removes that one descriptor and adds an
  **instance** descriptor (the container never disposes an instance it did not create, `XCUT-22`); `UseTransport(Func<IServiceProvider,
  IAsyncHttpClient>)` adds a **factory** descriptor (the container disposes what it returns). The same for `UseSerde`; there is no
  default codec, so the zero-candidate branch is live for `ISerde`.
- **`SEAM-6`** at the call: the same instance or the same delegate again is a no-op; a different one throws `InvalidOperationException`
  naming the client, the incumbent and the rejected implementation types. This is earlier than §3.6's "the validator's
  'different provider' failure": a registration mistake surfaces at the line that made it.
- **`SEAM-5`'s multi-candidate branch** at start: the validator scans the `IServiceCollection` the builder captured (complete once
  the provider is built, V4) for keyed `IAsyncHttpClient` and `ISerde` descriptors under the client's key; more than one fails,
  listing each implementation type (instance type, implementation type, or the factory's declaring type and method). This catches a raw
  `services.AddKeyedSingleton<IAsyncHttpClient>("github", …)` that `GetRequiredKeyedService` would otherwise resolve silently as the
  last (V4, §11 item 8).
- **Zero candidates**: no `ISerde` fails with "client 'github' has no ISerde: call UseSerde(...) on AddDexpaceClient(\"github\")", at
  `IStartupValidator.Validate()` and at the first resolution of any of the client's keyed services (D).
- **Disposal.** Every `IAsyncHttpClient` is `IAsyncDisposable` (`IAsyncHttpClient.cs:53`) and not necessarily `IDisposable`, so a synchronous
  `ServiceProvider.Dispose()` throws the container's "only implements IAsyncDisposable" `InvalidOperationException` for an async-only
  transport (a test's `ScriptedTransport`, a caller's adapter). A host disposes asynchronously; the disposal tests and `dependency-injection.md`
  use and say `DisposeAsync` (F21).
- **`SEAM-7`**: every keyed registration is a singleton; `KeyedSingletonTests` proves one construction under 64 concurrent first
  resolutions. `SEAM-8`, `SEAM-9` and `SEAM-10` stay N/A (2b) with nothing added.

### H. Auth, codec and extra policies (P9-17, P9-18)

- **Codec** (P9-17): `UseSerde` is required. The 7a hand-off is met by the documented composition and
  `SerdeRegistrationTests.CreateDefaultOptions_with_a_context_round_trips_through_the_registered_serde` in the DI test project, which
  references the codec. **Open for the lead** (the alternative is A's option (3)).
- **Auth** (P9-18): `Auth:*` keys bind `ApiKeyCredential`, `BasicCredential`, `DigestCredential` and `TokenRefreshMargin` into an
  `AuthCredentials`; `ConfigureCredentials(Func<IServiceProvider, AuthCredentials, AuthCredentials>)` adds what configuration cannot
  hold (a `TokenCredential` from the container). The policy is `new MultiSchemeAuthPolicy(descriptor, credentials, timeProvider)` over
  `UseAuth(AuthDescriptor)`, the descriptor a generated SDK owns; `UseAuthPolicy(Func<IServiceProvider, HttpPipelinePolicy>)` installs any
  other policy (a `ChallengeAuthPolicy`, a caller's own). Credentials with neither is a failure (credentials that are silently not
  sent are the misconfiguration nobody sees); both throws at the call (`SEAM-6`'s rule for the one auth authority). The credentials
  stay redacting: `AuthCredentials.ToString` lists schemes only (P6c-20), no failure quotes a value, and `CredentialRedactionTests`
  asserts that no failure message, `ToString` or log line from a bad `Auth:*` configuration contains the configured secret.
- **Extra policies**: `AddPolicy(Func<IServiceProvider, HttpPipelinePolicy>)` adds a caller's policy to the client's pipeline; the
  pipeline is `PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport, authPolicy, logger, timeProvider))`, then each added
  policy, then `.Build(transport, options)` (V3), so the stage sort places them (PIPE-35).

### I. One retry layer (`XCUT-10`, §6.1, §11 item 24; P9-19)

The package adds no retrying handler and no handler at all besides the primary (`NoRetryingHandlerTests`). V6 measured why that
matters: the standard resilience handler's defaults re-send a `POST` body three more times, behind the SDK's safety gate, which is
`XCUT-10`(b) broken underneath a conforming pipeline. The user page's composition, the one the test pins:

```csharp
services.AddDexpaceClient("github", section)
    .HttpClient.AddStandardResilienceHandler(o => o.Retry.ShouldHandle = _ => ValueTask.FromResult(false));
```

keeps the rate limiter, the total and attempt timeouts and the circuit breaker, and removes the second retry engine.
The existing guidance (§6.1, `retry.md`, the 6a hand-off) offers the SDK's own `MaxRetryAttempts = 0` or a handler without its retry strategy.
The first is **withdrawn**: it leaves the handler retrying behind the SDK's re-send gate (the `POST` measured in V6), which is `XCUT-10`'s hazard.
The second stands, and `Retry.ShouldHandle` returning `false` is how to spell it, because the handler-side `o.Retry.MaxRetryAttempts = 0`
is refused by the handler's own options validation (V6). The 6a status note's and issue #94's ambiguous "`MaxRetryAttempts = 0`" is
clarified accordingly (the card's "without its retry" stands). `ResilienceCompositionTests` (`Integration`): over a loopback server answering `503`, a `POST` through the client
with the composition reaches the server exactly `MaxRetryAttempts + 1` times for a replayable body and once for a single-use body,
and the standard handler's default arrangement is shown multiplying (the regression the guidance exists for), with the SDK's
`MaxRetryAttempts = 0` and the handler's `Retry.Delay` shortened so the row neither multiplies the SDK's attempts into tens of seconds of
wall time nor needs the real clock (F23). This needs
`Microsoft.Extensions.Http.Resilience` in the test project only (P9-19, **open for the lead**: the alternative is an untested
documentation snippet).

### J. The `CallKey` stamp (P8b-18; P9-20)

P8b-18 moved the stamp "to phase 9 … with its first reader: a DI-registered `DelegatingHandler`". Phase 9 registers no
`DelegatingHandler` and reads no call context below the transport, so it has no reader either. Building the route anyway is three
changes outside this package: a `CallKey?` on `RequestOptions` set by the pipeline (P4a-13's option 1, a `SEAM` surface change), a public
`HttpRequestOptionsKey<CallKey>` stamped by SystemNet, and a handler to read it; it would be surface without a consumer, which `NFR-4`
locks at the first release. **Not built** (open for the lead): a `docs/first-release.md` post-release trigger ("the first consumer that
needs a call's context below the transport") and a phase 11 inbound bullet (§11 item 30's "kept, with a .NET reader" has no production
reader as built; the store is read only through `DexpaceCallContexts.TryGet` in policies).

### K. Trimming and NativeAOT (P9-21)

The package is under `src/`, so `IsTrimmable`, `IsAotCompatible`, warnings as errors, `EnablePackageValidation`, the public-API files
and the banned list apply unchanged. Nothing in it uses reflection: the reader is plain code, the factories are lambdas, enum names come
from `Enum.GetNames<T>()`, and `ConfigurationBinder` is banned (C). V5 found no analyzer warning on the registration APIs used, and V7
published and ran the path under NativeAOT. **The AOT smoke consumer exercises the package**: `tests/Dexpace.Sdk.AotSmoke` gains a project
reference and `SmokeChecks.DependencyInjection.cs` with `CheckPhase9Async`: a client registered with a stub `UseSerde` (the zero-codec rule fails one without, D) over an in-memory configuration with a shorthand duration, a
level by name, a method list and a status list; `IStartupValidator.Validate()`; a keyed pipeline resolved; a loopback `302` returned
unchanged by the keyed transport (the kit's `LoopbackServer`); a bad value failing with `OptionsValidationException`. Phase 10 keeps the
check when it completes the smoke.

---

## The public surface

### `src/Dexpace.Sdk.Extensions.DependencyInjection/PublicAPI.Unshipped.txt` (new; signatures)

```csharp
namespace Dexpace.Sdk.Extensions.DependencyInjection;

public static class DexpaceServiceCollectionExtensions
{
    public static DexpaceClientBuilder AddDexpaceClient(this IServiceCollection services, string name);
    public static DexpaceClientBuilder AddDexpaceClient(this IServiceCollection services, string name, IConfigurationSection configuration);
}

public sealed class DexpaceClientBuilder
{
    public string Name { get; }
    public IServiceCollection Services { get; }
    public IHttpClientBuilder HttpClient { get; }

    public DexpaceClientBuilder Configure(Func<DexpaceClientOptions, DexpaceClientOptions> configure);
    public DexpaceClientBuilder ConfigureTransport(Func<SystemNetHttpClientOptions, SystemNetHttpClientOptions> configure);
    public DexpaceClientBuilder ConfigureCredentials(Func<IServiceProvider, AuthCredentials, AuthCredentials> configure);
    public DexpaceClientBuilder UseProxyFromEnvironment();
    public DexpaceClientBuilder UseProxyFromEnvironment(Func<string, string?> lookup);
    public DexpaceClientBuilder UseTransport(IAsyncHttpClient transport);
    public DexpaceClientBuilder UseTransport(Func<IServiceProvider, IAsyncHttpClient> factory);
    public DexpaceClientBuilder UseSerde(ISerde serde);
    public DexpaceClientBuilder UseSerde(Func<IServiceProvider, ISerde> factory);
    public DexpaceClientBuilder UseAuth(AuthDescriptor descriptor);
    public DexpaceClientBuilder UseAuthPolicy(Func<IServiceProvider, HttpPipelinePolicy> factory);
    public DexpaceClientBuilder AddPolicy(Func<IServiceProvider, HttpPipelinePolicy> factory);
}

public static class DexpaceConfigurationKeys
{
    public const string SectionName = "Dexpace";
    public const string MaxRetryAttempts = "Retry:MaxRetryAttempts";
    public const string LogLevel = "Logging:Level";
    public const string HttpProxy = "HTTP_PROXY";
    public const string HttpsProxy = "HTTPS_PROXY";
    public const string NoProxy = "NO_PROXY";
}
```

No overload has an optional parameter (`RS0026`/`RS0027`, the 4c finding). Keyed services resolved by a consumer: `HttpPipeline`,
`IAsyncHttpClient`, `ISerde`, `DexpaceClientOptions`, each under the client name. Internal types: `ConfigurationReader`,
`DurationGrammar`, `ScalarGrammar` (integer, boolean, decimal, enum, list), `DexpaceClientState`, `DexpaceClientStateSetup`
(`IConfigureNamedOptions`), `DexpaceClientValidator` (`IValidateOptions`), `DexpaceClientRegistration` (the per-name record the builder
writes), `PrimaryHandlerLease`. `InternalsVisibleTo` names `Dexpace.Sdk.Extensions.DependencyInjection.Tests` only.

### `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt` (added line)

```text
static Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.CreatePrimaryHandler(Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions! options) -> System.Net.Http.SocketsHttpHandler!
```

The caller owns the returned handler (its XML remarks say so, and that it must be passed to the handler constructor, never with
`options.Proxy` again). `Dexpace.Sdk.Core`'s public API does not change.

---

## Packages, projects and dependencies

- **New** `src/Dexpace.Sdk.Extensions.DependencyInjection/` (packable, `0.0.1-alpha.1` lockstep, `README.md` packed, `GlobalUsings.cs`,
  `PublicAPI.{Shipped,Unshipped}.txt`, `packages.lock.json`) and `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/` (references the
  package, core, SystemNet, SystemTextJson, Conformance, TestSupport; `TestCategoryTests`), both added to `Dexpace.Sdk.sln`.
- **`Directory.Packages.props`**: `Runtime` gains `Microsoft.Extensions.Http` 10.0.12, with a comment naming §10 entry 2 and the audit;
  `Tests` gains `Microsoft.Extensions.Http.Resilience` 10.10.0 (P9-19) and `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.12 (`AddEnvironmentVariables(prefix)` for `CFG-3`'s test; it is not in V5's closure). The
  `Directory.Packages.props` diff is those three lines. Lock files: the two new projects'; no other project's graph moves
  (the AOT smoke has no lock file and gains the package's closure through its project reference).
- **`BannedSymbols.txt`**: `T:Microsoft.Extensions.Configuration.ConfigurationBinder` ("bind through the DI package's reader; the binder's
  generator binds no `init` accessor and its reflection mode is not AOT-safe (P9-4)").
- **`scripts/ci/dependency-audit.cs`**: A's policy change. `.github/workflows/ci.yml`: no step changes (the new test project runs in the
  solution's test step, its coverage report is picked up by name, the audit iterates `src/`); its header comment gains the third driver.
- The conformance hook handlers the third driver needs (`FaultingAdaptationHandler`, `ResendingHandler`, `CancelOnDemandHandler`, today
  private to `SystemNetSubject`) are shared by linked source (`<Compile Include="…" Link="…" />`), not moved into `TestSupport`, so
  `Dexpace.Sdk.Core.Tests`' `SEAM-2` position does not change.

## Breaking changes

None to a published or unreleased contract: the package is new, and `CreatePrimaryHandler` is additive. The `CHANGELOG.md`
`[Unreleased]` entry is an `### Added` block (the package, the SystemNet method) plus one `### Changed` line for the guidance
change (the SDK-side `MaxRetryAttempts = 0` option is withdrawn for `Retry.ShouldHandle` returning `false`, documentation only).

---

## Test strategy

All in `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests`, every class with `[Trait("Category", …)]`.

| Class | Category | Rows / purpose |
|---|---|---|
| `ConfigurationPrecedenceTests`, `EmptyValueTests`, `TypedLookupTests` | Unit | `CFG-1`, `CFG-2`, `CFG-38` (ports of `configuration.test.ts` precedence and `CFG-2` rows) |
| `EnvironmentNormalizationTests` | Integration | `CFG-3`, `CFG-4`, `CFG-11` (process variables under a GUID prefix) |
| `DurationGrammarTests`, `IntegerGrammarTests`, `BooleanGrammarTests`, `ScalarGrammarTests` | Unit | `CFG-5`–`CFG-7`, decimals, enums, lists (ports of `configuration_test.rb`, `duration.test.ts`; V1, V2 cases) |
| `LogLevelBindingTests` | Unit | `OBS-35` |
| `KeyConstantsTests`, `KeyTableTests`, `NullGuardTests`, `ConfigurationSeamTests` | Unit | `CFG-14`, the drift guard, `CFG-37`, `CFG-11` |
| `ValidationTests` | Unit | every failure reported at once; `"<path>: <rule>"`; no string, URL or credential quoted (a numeric accessor echo is allowed and pinned, P9-28); unknown keys; a root section rejected; a control character in `UserAgent`; host-less lazy failure; `IStartupValidator` |
| `ProviderValidationTests` | Unit | a provider built with `ValidateScopes` and `ValidateOnBuild` resolves every keyed registration from a scope and from the root, every client with a `UseSerde` (the card's test; styleguide 2.5) |
| `SingleRegistrationTests`, `KeyedSingletonTests` | Unit | `SEAM-5`, `SEAM-6`, `SEAM-7` |
| `NamedClientTests` | Unit | the chain shape (V3), `RemoveAllLoggers`, infinite lifetime, `PooledConnectionLifetime`, `ConfigureHttpClient` ignored, a substituted redirect-following primary refused at resolution |
| `PrimaryHandlerLeaseTests` | Integration | the pool is released when the container is disposed; a caller's `DelegatingHandler` is not disposed (`XCUT-22`) |
| `NamedClientRedirectWireTests` | **Security** | phase 1 S3, row 9: two loopback origins; the named client returns the 3xx and never contacts the target; through the pipeline, `Authorization` is stripped on the hop |
| `ProxyRegistrationTests` | Unit | explicit keys, `UseProxyFromEnvironment` with a recording lookup, two sources fail, no environment read at registration |
| `NoImplicitEnvironmentReadArchitectureTests` | Unit | the IL scan of F |
| `AuthRegistrationTests`, `CredentialRedactionTests` | Unit | P9-18; no secret in any failure, `ToString` or log line |
| `SerdeRegistrationTests` | Unit | the 7a composition |
| `NoRetryingHandlerTests` | Unit | `XCUT-10`'s DI clause |
| `ResilienceCompositionTests` | Integration | V6 as a test |
| `DependencyInjectionConformanceTests` | Conformance | the third driver (P9-22) |
| `TestCategoryTests` | Unit | every test carries a category |

**The third driver** (P9-22). `DependencyInjectionSubject` builds a fresh `ServiceCollection` per transport (the kit's `TransportSettings.Logger`
forwarded through a single-logger `ILoggerFactory`), registers `AddDexpaceClient("kit")` over an in-memory configuration with a `UseSerde` stub, and hands the kit
a wrapper whose disposal disposes the provider. Hooks: `CreateWithInternalCancel`, `CreateWithNativeResend` and
`CreateWithFaultingAdaptation` through `HttpClient.AddHttpMessageHandler` with the linked handlers; `CreateWithProxy` through
`Transport:Proxy:*` keys (exercising `CreatePrimaryHandler`'s proxy path); `CreateBorrowed` is not supplied (the package never borrows a
client), so `transport-15.borrowed-survives` stays `NotExercised` with that reason in the driver's remarks. Waivers: the same two permanent,
assertion-scoped ones as the SystemNet drivers (§10 entries 32 and 33), each turning stale if its assertion passes. The kit must be green
on both faces.

The phase-1 `Security` classes and 8b's tests are not edited; `CreateOwnedClient`'s refactor onto `CreatePrimaryHandler` keeps every
SystemNet test green, and `SystemNetHttpClientSurfaceTests` gains the method.

## PR segmentation

Four stacked pull requests on `<issue>-phase-9-host-integration` (code and tests together, roadmap step 5):

1. **Seams and gates.** `SystemNetHttpClient.CreatePrimaryHandler` (+ its tests, `CreateOwnedClient` refactored onto it); the audit's policy
   change (an entry for a library not yet on disk is unused, so the audit stays green). The package is **not** scaffolded here: the CI coverage
   gate fails closed on any `src/` library with no instrumented line, so a package that lands empty, or with only the constants class, would
   turn PR 1 red.
2. **The package and the reader.** The package and test-project scaffold (csproj, README stub, `GlobalUsings.cs`, PublicAPI files, IVT, solution
   entries, lock files), `Directory.Packages.props`, the `ConfigurationBinder` ban, `DexpaceConfigurationKeys`; the grammars, `ConfigurationReader`, `DexpaceClientState`, its setup and validator, `ValidateOnStart`; every `CFG` row,
   `OBS-35`, the drift guard, the redaction rule.
3. **Registration.** `AddDexpaceClient`, the builder, the named client (`RemoveAllLoggers`, lifetime, lease), the keyed transport, codec,
   options and pipeline, proxy, auth, extra policies; `SEAM-5`/`SEAM-6`/`SEAM-7`; `ProviderValidationTests`; `NamedClientRedirectWireTests`;
   the IL scan.
4. **Drivers and close-out.** The third conformance driver; `ResilienceCompositionTests`; `CheckPhase9Async` in the AOT smoke; the package
   README; `docs/sdk-documentation/dependency-injection.md`; `CHANGELOG.md`; the checklist (15 rows, the 3 contributed rows and the non-row
   hand-offs as a second table, the styleguide audit groups); the corrections below; the knowledge notes; the housekeeping probe.

## Hand-offs to later phases

- **10** — `XCUT-10` cites `NoRetryingHandlerTests` and `ResilienceCompositionTests`; `XCUT-22` cites `PrimaryHandlerLeaseTests` and the instance
  descriptors of `UseTransport`; `XCUT-23` cites `SingleRegistrationTests`; `NFR-2` cites the audit's policy record and §10 entry 2's
  measurement; `NFR-8`/`NFR-9` keep `CheckPhase9Async`; the packaging suite's allowed-dependency check reads the same policy record.
- **11** — §10 entries 25 and 26 gain the as-built facts (P9-7, P9-9) and entry 26 adds `OBS-35` to its touches; the auditor decides
  whether `CFG-2`'s shadowing clause belongs in the unmet-MUST count (this design argues it is entry 25's judged mechanism, not a new
  clause); §11 item 30's reader is unbuilt (P9-20).
- **12** — the package joins the lockstep release; `PublicAPI.Shipped.txt` moves; the release notes name the `ShouldHandle` guidance and the
  no-reload rule; the `CallKey` trigger stays in `docs/first-release.md`.

## Design, roadmap, register, checklist and `CLAUDE.md` corrections owed at close-out

Proposals, applied as dated corrections when the checklist lands, not by this document:

1. **Design §2.1** — the package is built; its dependencies are core, `Dexpace.Sdk.Http.SystemNet` and `Microsoft.Extensions.Http` (the
   family's one root, V5); the **As built** line. **§3.2** — the `TRANSPORT-1` paragraph: the DI package builds the named client's primary
   through `CreatePrimaryHandler` and holds one chain per client with an infinite handler lifetime and a pooled-connection lifetime.
   **§3.6** — **As built**: `SEAM-6`'s different-provider failure is thrown at the `Use*` call; the validator scans keyed descriptors;
   `SEAM-7` is the keyed singleton. **§6.1** ("The layer underneath") — the guidance is `Retry.ShouldHandle` returning `false`; the
   SDK-side `MaxRetryAttempts = 0` option is withdrawn (the handler would re-send a `POST` body behind the SDK's gate, `XCUT-10`), the handler-side
   `MaxRetryAttempts = 0` is refused by the handler's validation (V6, replacing "cited from its documentation, not verified here"). **§8.2** — binding is the package's reader, not the binder (V1: the generator binds no `init`
   accessor; reflection is 64 AOT warnings and mis-binds four members); the duration grammar is the reader's (V2's four `XmlConvert`
   divergences); `CFG-2` reads empty as absent (P9-7); tier 1 is a `with` transform; no reload; the proxy sources (P9-15); `OBS-35`'s key is
   the caller's section. **§9.1** — the banned list gains `ConfigurationBinder`. **§9.2** — the audit's policy record; the AOT smoke covers
   the DI package. **§9.3** — the third conformance driver. **§10 entry 2** — measured closure and the SystemNet reference; **entry 9** —
   **As built**; **entry 25** — `CFG-2`: an empty value is absent and the record default applies, the shadowed lower provider is not consulted
   (replacing "fails binding at startup"); **entry 26** — touches `OBS-35`; the binder evidence. **§11 item 8** — measured (V4); **item 24**
   — the corrected composition; **item 30** — the reader is unbuilt (P9-20); **two new §11 items** (re-read the last number on the merged file): the
   grammar readings of P9-29 (ISO-8601 means the day-time subset, a bare number is digits only, scalars are ASCII-trimmed first, booleans included)
   and the empty-value shadow of P9-7. **§12** — the `CFG` row's notes (phase 9 rows), `SEAM` and `OBS-35`. **Styleguide departure** (not a design
   chapter): `02-dependency-injection.md` 2.7 says a feature extension "returns the collection so calls chain"; `AddDexpaceClient` returns a
   `DexpaceClientBuilder` (the `IHttpClientBuilder` precedent; `.Services` chains back), recorded beside P9-4's 1.3 departure.
2. **Roadmap** — the Phase List row 9 links this design, the plan and the checklist; the 6a status note's and issue #94's ambiguous "`MaxRetryAttempts = 0`" guidance is clarified to
   the `ShouldHandle` composition and the SDK-side option withdrawn (the Phase 9 card itself says only "without its retry", which stands); two
   departures from the card's build list are recorded as dated corrections: "`IConfiguration` binding under section `Dexpace`" (the section is the
   caller's argument and `SectionName` is published, never read, P9-23) and "the TimeSpan converter … and the base-10 integer converter" (a reader,
   P9-4, V1); a dated status note at close; the phase-1 hand-off
   table's row 9 names `NamedClientRedirectWireTests`.
3. **Checklists** (dated corrections): 2b `SEAM-5`, `SEAM-6` (DI half ✅), `SEAM-7` (cites the test); 5b `OBS-35` (✅, V4 answered); 8a (the third
   driver); 8b `TRANSPORT-1` (named-client clause) and P8b-18's disposition; phase 1 S3 (row 9's ⏳ closed).
4. **`docs/first-release.md`** — *Packages*: the DI row (exists, unpublished, phase 12); *Behavioural asymmetries*: `di-no-reload`,
   `di-handler-lifetime` (one chain per client for the process, `PooledConnectionLifetime` instead of rotation, `ConfigureHttpClient`
   ignored, the factory's loggers removed); *Post-release triggers*: the `CallKey` reader (P9-20) and, if P9-5 stands, a typed-client overload
   on the first generated SDK that asks for one.
5. **`CLAUDE.md`** — the layout gains the package and its test project with one line each; "What is genuinely unbuilt" loses the DI
   package; "Things that will bite you" gains one bullet (configuration is read by the package, fails at start naming the key and never the
   value; no reload; a codec is required; the resilience composition); the commands list gains the third driver's filter;
   `docs/sdk-documentation/dependency-injection.md` is linked. `README.md` (root) lists the package.
6. **`docs/README.md`** — the `sdk-documentation/` row gains `dependency-injection.md` (phase 9).
7. **READMEs** — the package's own (registration, the key table, the grammars, the resilience composition, proxy, auth, the no-reload rule);
   `src/Dexpace.Sdk.Http.SystemNet/README.md` gains `CreatePrimaryHandler`.
8. **`docs/sdk-documentation/retry.md`** — lines 40–41 ("turn its retries off or set `MaxRetryAttempts = 0` here. The DI package's guidance (phase 9)
   says the same"): a dated correction pointing at `dependency-injection.md`: the SDK-side option is withdrawn, the composition is
   `Retry.ShouldHandle` returning `false` (P9-19).
9. **Knowledge** — `docs/knowledge/notes/configuration.md` (V1, V2, overriding `configuration/bfbcbf8c` and `observability/4997350a`;
   the styleguide 1.3 departure, P9-4, as a `Conflicts` note per constraint 6); `docs/knowledge/notes/transport-adapter.md` (V3, V8, V9).

## Risks and open questions (all resolved here)

| # | Question | Resolution |
|---|---|---|
| Q1 | Does the segmentation rule apply? | Yes, by its chapter clause; one phase, recorded here (P9-1), open for the lead. |
| Q2 | Can the binder set `init` accessors? | The generator cannot (silently); reflection can but is not AOT-safe (V1). A reader instead (P9-4), open. |
| Q3 | Does `MaxRetryAttempts = 0` disable the standard handler's retry? | No: it fails validation (V6). `ShouldHandle` (P9-19). |
| Q4 | Does `IHttpClientFactory`'s chain compose with the 8b handler constructor? | Yes once the primary has redirects off; the default is refused (V3). |
| Q5 | Does disposing the container release the named client's connections? | Not with the factory alone (V8); the lease does (P9-13). |
| Q6 | How does `ProxyOptions` reach a factory-built primary? | Through `CreatePrimaryHandler` (P9-12), open. |
| Q7 | Does `ValidateOnStart` work without a host? | Through `IStartupValidator`, and lazily at first resolution (V4). |
| Q8 | Is the package AOT-clean? | Yes (V5, V7); the smoke covers it (P9-21). |
| Q9 | Where does the `CallKey` stamp land? | Not in 9: no reader (P9-20), open. |
| Q10 | Does `OBS-35`'s "no baked key" conflict with `CFG-14`'s constants? | No: the constants are published and never read; the section is the caller's (P9-23). |

**Residuals.** R1: the key table must follow the records; `KeyTableTests` turns a forgotten member into a red test, not a silent default. R2:
`CFG-2`'s shadowing clause is unmet by `IConfiguration`'s merge, on entry 25's judged mechanism. R3: unknown keys fail the boot, so a
configuration written for a newer package fails an older one (P9-9's open half). R4: a caller who changes the named client's handler
lifetime or primary through `builder.HttpClient` steps outside the lease's guarantee; the redirect check still holds. R5: holding one handler
chain per client means a caller's scoped `DelegatingHandler` lives for the process, the factory's own behaviour for a long-lived chain. R7: `CFG-2`'s empty-value shadow is silent (P9-7, open): an empty
environment value over an `appsettings.json` value resolves the record default, not the JSON value; the user page and `first-release.md` (`di-empty-shadows`)
say so. R8: a synchronous `ServiceProvider.Dispose()` throws for an async-only transport (G). R6:
`Microsoft.Extensions.Http.Resilience` is versioned on its own `10.x` line (10.10.0), not the runtime band; it is test-only, so the audit's band
rule does not reach it.

## Exit criteria

1. The checklist has **15 rows** as tabled (13 ✅ with 5 split, 2 N/A), each ✅ with a named test, `[Trait]` and file; a second table
   records the 3 contributed rows and every non-row hand-off; `SEAM-5`, `SEAM-6`, `SEAM-7`, `OBS-35`, `TRANSPORT-1` and phase 1's S3 flip in
   their own checklists by dated correction.
2. The kit is green against the DI-built transport on both faces (the third driver), with exactly the two permanent assertion-scoped waivers;
   the two SystemNet drivers and the raw-socket driver stay green.
3. The package is packable with its README; `scripts/ci/dependency-audit.cs` passes with the policy record; the AOT smoke publishes and runs
   `CheckPhase9Async`.
4. Every gate green: build (warnings as errors, `RS0016`/`RS0017` for the two changed `PublicAPI` files, `RS0030` with the new ban, `MA0051`,
   `CA2007`), `dotnet format --verify-no-changes`, the full test run with the phase-1 `Security` classes unedited, the 80% coverage gate, the
   reproducible pack, `scripts/knowledge verify-structure`.
5. `docs/sdk-documentation/dependency-injection.md`, the READMEs, `CHANGELOG.md`, the dated roadmap status note, the corrections above, the
   knowledge notes, and a housekeeping probe that reports nothing.

## Rulings

| ID | Ruling | Open for the lead |
|---|---|---|
| P9-1 | The segmentation rule applies (chapter clause); one phase, no sub-phases; the segmentation is recorded in this design | **yes** (a file of its own) |
| P9-2 | The package references core, `Dexpace.Sdk.Http.SystemNet` and one third-party root, `Microsoft.Extensions.Http`; not the codec | **yes** (§2.1's "core only" cell) |
| P9-3 | The audit's policy becomes `Policy(ThirdParty, Projects)`; the DI package's is `("Microsoft.Extensions.Http", ["Dexpace.Sdk.Http.SystemNet"])`; first-party projects at the lockstep version; §10 entry 2 measured | — |
| P9-4 | Binding is a hand-written reader over `IConfiguration`; neither binder mode is used, and `ConfigurationBinder` is banned in `src/` (V1); the styleguide 1.3 departure recorded as a note | **yes** (§8.2's binder-plus-converters text) |
| P9-5 | `AddDexpaceClient(name[, IConfigurationSection])` (a root is not accepted, C) returns a sealed `DexpaceClientBuilder`; named clients as keyed singletons (`HttpPipeline`, `IAsyncHttpClient`, `ISerde`, `DexpaceClientOptions`); no generic typed-client overload; one registration per name | **yes** (the generic overload) |
| P9-6 | Tier 1 is `Configure`/`ConfigureTransport`/`ConfigureCredentials` `with` transforms, applied after configuration in call order | — |
| P9-7 | An empty or whitespace-only value is absent and the record default applies; a shadowed lower provider is not consulted (entry 25). Options: **A** as ruled (silent shadow, documented), **B** an empty value fails start naming the key (loud, entry 25's current text; turns a blank templated variable into a boot failure and drops `CFG-2`'s ✅ half). **Recommended default: A**, with the shadowing named on the user page and in `first-release.md` | **yes** (A or B) |
| P9-8 | The grammars: `CFG-7` durations (ISO by the reader's grammar, shorthand, bare ms, `none`), base-10 integers, strict booleans, invariant finite decimals, enums by name, lists (children or comma scalar, `none` empty, replace semantics) | — |
| P9-9 | Every unparseable value, accessor rejection and unknown key fails `ValidateOnStart`, all reported at once as `"<path>: <rule>"`, never quoting a configured string, URL or credential (an accessor's numeric range echo is allowed, P9-28; entry 26, now touching `OBS-35`) | **yes** (unknown keys only) |
| P9-10 | No cross-property failure for combinations the records accept; documented, no new log events | **yes** (a 170–179 event block and warnings) |
| P9-11 | One internal options type per name, configured by the reader, validated by one validator, `ValidateOnStart`; host-less failure at first resolution; one snapshot, no reload | — |
| P9-12 | The named client's primary is the new `SystemNetHttpClient.CreatePrimaryHandler(options)`; the transport is the 8b handler construction over `IHttpMessageHandlerFactory.CreateHandler` | **yes** (new SystemNet surface) |
| P9-13 | Infinite handler lifetime with a `PooledConnectionLifetime` (2 min default); a container-owned lease disposes the primary the package created (V8) | — |
| P9-14 | `RemoveAllLoggers()` on the named client: the SDK's redacted events are the one HTTP log stream (V9) | — |
| P9-15 | Proxy from explicit `Transport:Proxy:*` keys or from code `UseProxyFromEnvironment()`/`(lookup)`; both is a failure; no configuration switch reads the environment; the IL scan | **yes** (a configuration switch) |
| P9-16 | `SEAM-5`/`SEAM-6`/`SEAM-7`: explicit `Use*` replaces the default; a different second `Use*` throws at the call naming both; keyed duplicates fail at start listing all; no codec is the install hint; keyed singletons | — |
| P9-17 | A codec is required (`UseSerde`); the package does not reference `SystemTextJson`; the 7a composition is documented and tested | **yes** (an STJ default) |
| P9-18 | `Auth:*` binds the redacting credentials; `TokenCredential` through `ConfigureCredentials`; `MultiSchemeAuthPolicy` over `UseAuth(descriptor)`, or `UseAuthPolicy`; credentials with neither fail; both throw | — |
| P9-19 | No retrying handler; the documented composition is `Retry.ShouldHandle` returning `false` (V6): the SDK-side `MaxRetryAttempts = 0` option is withdrawn (`XCUT-10`), the handler-side one is refused by its validation; pinned by a test that needs `Microsoft.Extensions.Http.Resilience` in the test project | **yes** (the test-only dependency) |
| P9-20 | The `CallKey` carrier, stamp and reader are not built (no reader in 9); a post-release trigger and a phase 11 bullet | **yes** |
| P9-21 | The package is under every library gate; the AOT smoke gains `CheckPhase9Async` (V7) | — |
| P9-22 | The third conformance driver in the DI test project, hooks through `AddHttpMessageHandler` and proxy keys, borrowed not supplied, the two permanent waivers; green on both faces | — |
| P9-23 | `DexpaceConfigurationKeys` publishes the section name and `CFG-14`'s keys; nothing reads them implicitly; the section is always the caller's argument (`OBS-35`) | — |
| P9-24 | One new `Security` class (`NamedClientRedirectWireTests`, phase 1 S3 row 9); phase-1 classes untouched | — |
| P9-25 | Four stacked PRs (seams and gates; reader; registration; drivers and close-out) | — |
| P9-26 | Ports from `ruby-sdk@5b17395` and `nodejs-sdk@54aeed4` as tabled; `1_000` rejected where Ruby tolerates it; `config_test.rb` (all `CFG-13`) not ported | — |
| P9-27 | Every scalar is trimmed before parsing, booleans included (Ruby does not trim booleans); recorded as a §11 resolution (P9-29) | — |
| P9-28 | **New (validation).** The "no value quoted" rule means no configured string, URL or credential; an accessor's numeric range message may echo the rejected number. Options: **narrow the rule** (as ruled), or strip the BCL text for a per-key rule table. **Recommended default: narrow** | **yes** |
| P9-29 | **New (validation).** Grammar readings recorded as a §11 resolution, not silent: ISO-8601 is the day-time subset `PnDTnHnMn.nS` (seven-digit fraction), a bare duration is digits only (siblings accept `1.5`), scalars are ASCII-trimmed first, booleans included (Ruby does not). Options: keep, or accept bare fractions and/or leave booleans untrimmed. **Recommended default: keep and record** | **yes** |

## Deviation Ledger

Entries for roadmap constraint 7. **No new specification deviation and no new §10 entry.** The split rows rest on existing entries:
`CFG-2`'s fall-through on §10 entry 25 (refined by P9-7: an empty value is now absent rather than a start-up failure, which changes the deviation's
shape from a loud failure to a silent shadow; open for the lead), `CFG-5`–`CFG-7` and `OBS-35`'s never-throw/fall-back clauses on §10 entry 26 (P9-9; `OBS-35` joins its touches), `CFG-4` and
`CFG-10` on §10 entry 25's vacuous reading, `SEAM-5`–`SEAM-7` on §10 entry 9, and `NFR-2` on §10 entry 2 (P9-2, P9-3: measured). Departures
from the design's text, each a close-out correction: P9-4 (§8.2's binder and registered converters), P9-6 (§8.2's `Action<DexpaceClientOptions>`),
P9-7 (§10 entry 25's "fails binding at startup"), P9-16 (§3.6's validator timing for `SEAM-6`), P9-19 (§6.1's and §11 item 24's
"set the SDK's `MaxRetryAttempts` to 0": that option is withdrawn on `XCUT-10`, V6; the "handler without its retry" option stands and gains its spelling), P9-2 (§2.1's dependency cell). Two departures from the styleguide companion: P9-4 against
`01-host-and-configuration.md` 1.3, recorded as a knowledge note, and the builder return type against `02-dependency-injection.md` 2.7 (see the
corrections). Departures from the roadmap card's build list: two, both recorded as dated roadmap corrections ("`IConfiguration` binding under
section `Dexpace`": the section is the caller's, P9-23; "the TimeSpan converter … and the base-10 integer converter": the reader, P9-4); the card's
"without its retry" is what P9-19 builds, and the ambiguous "`MaxRetryAttempts = 0`" wording it refines is the 6a status note's and issue #94's; the `CallKey` stamp handed here by P8b-18, which the card does
not list, is not built (P9-20, open).

## Validation resolution (2026-10-12)

An independent validation of this design and its plan (23 findings, F1 to F23: 4 major, 13 minor, 6 nit) was resolved in both documents. Each
finding was checked against the source first. One line each; the plan follows the design.

- **F1** (zero-codec rule vs tests and the smoke that register none): fixed. D and G state that a client without `UseSerde` fails validation and every
  keyed resolution, the transport's included; the AOT smoke (K), the third driver, `ProviderValidationTests` and all later tests register a stub through
  one shared helper (`TestClients.Add`), which task 3.3 changes in one place; task 3.3 also edits 2.6's `ValidationTests` and `ConfigurationPrecedenceTests`.
- **F2** (PR 1 fails the coverage gate): fixed, and wider than proposed: the gate (`coverage-gate.cs:128-147`) fails on any `src/` library with no
  instrumented line, so an empty package fails PR 1 too. The package scaffold, the `ConfigurationBinder` ban and `DexpaceConfigurationKeys` move to PR 2
  (which brings the reader's covered code); PR 1 is `CreatePrimaryHandler` and the audit's policy change; every PR's V-gate gains the coverage gate.
- **F3** (the `AddStandardResilienceHandler` correction misreads the guidance): fixed. V6, I, P9-19, the ledger, the corrections and the plan's tasks 4.5, 4.7
  and 4.8 now say: the SDK-side `MaxRetryAttempts = 0` option is withdrawn on `XCUT-10`'s ground (V6 shows the handler re-sending a `POST` body behind the SDK's
  gate), the "handler without its retry" option stands with the `ShouldHandle` spelling, and the handler-side `o.Retry.MaxRetryAttempts = 0` is refused by the
  handler's own validation (evidence, not a correction of a wrong statement). `docs/sdk-documentation/retry.md:40-41` is added to the close-out corrections.
- **F4** (P9-7 trades a loud failure for a silent wrong value): ruling P9-7 is now **open for the lead**, with options A (as ruled, silent shadow) and B (an empty
  value fails start); recommended default A, with the shadowing named on the user page and in a new `first-release.md` asymmetry (`di-empty-shadows`); the ledger no longer
  says "narrows the deviation". PLAUSIBLE (a judgement), recorded as a ruling.
- **F5** (the ✅ tally): fixed to 13 ✅ in the design tally, exit criterion 1 and the plan's status-note text.
- **F6** (task 1.5's red step): fixed (now task 2.0a): the red is the diagnostic diff (no `RS0030` before the entry, `RS0030` at both sites after; `IL2026`/`IL3050` throughout).
- **F7** ("no failure quotes a value" false for accessor rejections): ruling **P9-28 (new, open)**; recommended default narrows the rule to configured strings, URLs and
  credentials and lets a numeric range message echo the number; a test pins it.
- **F8** (`EnvironmentNormalizationTests` needs an unlisted package): fixed: `Microsoft.Extensions.Configuration.EnvironmentVariables` 10.0.12 joins the `Tests` group in
  task 2.8; the props diff is three lines; the lock diff stays the two DI lock files (the plan's "three" was also wrong: the test project's lock is one of the two new files).
- **F9** (`PrimaryHandlerLeaseTests` subclasses a sealed type): fixed: the caller's replacement is a custom counting non-Sockets handler; the discarded instance is observed
  through the lease's internal list.
- **F10** (`KeyTableTests` vacuous on three types, false on one key): fixed in C and plan R2/2.7: constructor parameters for the credential classes, an explicit
  `HandlerLevel` category for `Transport:PooledConnectionLifetime`; "six records" corrected to seven.
- **F11** (two PR-1 commits red on the audit): fixed by the same restructure as F2: the audit's policy record lands first, before the package exists (an entry for an absent
  library is unused); the scaffold is a single task in PR 2.
- **F12** (two unrecorded departures from the roadmap card): fixed: both listed under "Departures from the roadmap card" and added to the roadmap's dated corrections.
- **F13** (`CFG-6` ✅ vs P9-27's trimmed boolean): ruling **P9-29 (new, open)**; recorded as a §11 resolution, the row text now says "after an ASCII trim".
- **F14** (`CFG-7` "the grammar exactly" narrows it unrecorded): folded into **P9-29**: the day-time ISO subset, digits-only bare numbers (siblings accept fractions) and the
  seven-digit cap are stated in C and the row, and recorded as a §11 resolution; the row text no longer says "exactly".
- **F15** (`UserAgent` not validated at start; PLAUSIBLE): confirmed (the accessor checks only `null`) and fixed: the reader applies the header-value rule to `UserAgent`
  with a test row; the three `Logging:*` lists need none (a non-token name never matches).
- **F16** (a root configuration fails the boot): fixed: the parameter is an `IConfigurationSection` (the reader needs its `Path`), and a section with an empty `Path` is rejected
  with a pointed message; tests in 2.3 and 3.1.
- **F17** (builder return type departs from styleguide 2.7): fixed: recorded beside the 1.3 departure in the corrections, the ledger and the plan's checklist task.
- **F18** (the `SectionName` scan needs an `ldstr` reader): fixed in plan task 3.6 (the copied scanner gains an `ldstr` enumerator and a pin).
- **F19** (task 4.1's parameterless constructors need a rewrite): fixed in R1 and task 4.1 (two explicit constructors per handler, CS8862).
- **F20** (contradictory wording): fixed: R12 no longer says a "real Host" is built, "two-type public surface" is now one type (and PR 1 builds no package), and the design's
  "green with an empty package" is gone with the PR-1 restructure.
- **F21** (async-only transports break a synchronous container dispose; PLAUSIBLE): confirmed from the container's rule; documented in G, R8 and the plan's conventions, and
  the disposal tests use `DisposeAsync`.
- **F22** (the proxy-adapter rationale also applies to `CreatePrimaryHandler`; PLAUSIBLE): confirmed (`CreatePrimaryHandler` returns a mutable `SocketsHttpHandler`); the
  rationale is restated as single-sourcing and a smaller public surface.
- **F23** (the default-arrangement test runs on the real clock; PLAUSIBLE): fixed in I and task 4.3: the SDK's `MaxRetryAttempts = 0` and the handler's `Retry.Delay` shortened to
  1 ms; whether the handler accepts 1 ms is a residual to confirm at implementation (the plan says so).

**Open rulings after validation** (taken at their recommended defaults): P9-1, P9-2, P9-4, P9-5, **P9-7 (now open)**, P9-9 (unknown keys), P9-10, P9-12, P9-15, P9-17, P9-19, P9-20,
**P9-28 (new)** and **P9-29 (new)**. Row tally unchanged by the resolution: 15 owned rows, 13 ✅ (5 split), 2 N/A, 0 🚫, 0 ⏳.

## Correction (2026-10-13): which keyed services fail on invalid configuration

D and the "Zero candidates" bullet say the client fails the resolution of "each of its keyed services" (and "any of the client's keyed services"). That is true of
every service **the package builds** (the transport, the pipeline, the options, a codec created by a `UseSerde(Func<…>)` factory) and untrue of a caller-supplied
instance: `UseSerde(instance)` and `UseTransport(instance)` are returned as given and never consult the validated state, so resolving the keyed `ISerde` with bad
configuration succeeds. The code stays as built: wrapping a caller's instance in a factory would make the container dispose it, breaking `XCUT-22`. The start-up
failure (`ValidateOnStart`, `IStartupValidator.Validate()`) is unchanged and still reports every failure. `dependency-injection.md` carries the corrected wording.

A second, same-day finding: a value set on a **group** key (`Transport:Proxy`, `Auth`, `Auth:ApiKey`, `Retry`) failed nothing and was dropped silently, against P9-9. It
now fails as `"<path>: a group of keys, not a value (…)"` with a fixed rule text that never quotes the value; an empty or blank value stays absent (P9-7).

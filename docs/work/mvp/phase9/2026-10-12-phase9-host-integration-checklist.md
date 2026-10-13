# Phase 9 — Host Integration: Checklist

The execution-time checklist for roadmap phase 9 ([phase card](../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's cross-cutting constraint 3. Written from what was built on branch
`96-phase-9-host-integration` (issue #96, planning #94), following the [design](2026-10-12-phase9-host-integration-design.md) and the [plan](2026-10-12-phase9-host-integration.md). User page:
[`dependency-injection.md`](../../../sdk-documentation/dependency-injection.md).

The scope is the **15 rows phase 9 owns** (`CFG-1`, `-2`, `-3`, `-4`, `-5`, `-6`, `-7`, `-10`, `-11`, `-14`, `-37`, `-38`, `SEAM-5`, `SEAM-6`, `OBS-35`; 13 MUST and 2 SHOULD by appendix C): **13 ✅ / 2 N/A / 0 🚫 / 0 ⏳**. Five of the 13 ✅ are split with a 🚫 clause on an existing §10 entry
(`CFG-2` on entry 25; `CFG-5`, `CFG-6`, `CFG-7` and `OBS-35` on entry 26); `SEAM-5` and `SEAM-6` are ✅ for their DI half, their DI-less half staying N/A (2b). Evidence is added to three rows other phases own (`SEAM-7`, `TRANSPORT-1`, `XCUT-10`) and the design's non-row hand-offs are
dispositioned in the [second table](#rows-owned-elsewhere-and-non-row-hand-offs). The plan's four stacked pull requests were built as one working tree on the one branch, in the plan's order (seams and gates; the package and the reader; registration; drivers and close-out), and left uncommitted for the lead. The kit is **green against the
DI-built transport on both faces, the two SystemNet constructions and the raw-socket driver**, with exactly the two permanent assertion-scoped waivers per SystemNet-family driver and no `Owner =` waiver anywhere.

**Red evidence.** Taken where it was informative, and said as it was: most tests were written in the same step as the production code they pin, so the compile-red of a new type or member is the red for those rows (the DI test project was first built against types that did not exist for 2.1 to 2.9 and 3.1). What was measured: mutations
performed and reverted (never committed), each failing the pins it targets: `AllowAutoRedirect = true` in `CreatePrimaryHandler` (`NamedClientRedirectWireTests` and `NamedClientTests`: the factory chain refuses the primary at resolution, so the 3xx test fails at resolution rather than on the wire; the wire form is still
the structural proof that B is never contacted), and the `lease.Track(handler)` call removed (`PrimaryHandlerLeaseTests.Disposing_the_container_releases_the_pooled_connection`, which waits for the pooled connection and times out). The `ConfigurationBinder` ban: a scratch `GetValue<int>` in the package gave `IL2026`
and no `RS0030` before the entry, and `RS0030` after it (the diagnostic diff, F6). The audit's controls: with the `Projects` entry emptied it names `references project Dexpace.Sdk.Http.SystemNet`; with the `ThirdParty` entry set to `null` it names the closure packages it no longer allows; with the real record it prints `5 libraries conform`; the
nuspec lockstep control was not separately run. `KeyTableTests.A_new_member_would_fail_the_guard`, `NoImplicitEnvironmentReadArchitectureTests.The_scan_would_catch_a_configuration_switch` and `The_scanner_finds_a_planted_ldstr` prove their guards able to fail with planted members. Not taken: the `ResilienceCompositionTests` negative
(its default-arrangement row is the regression it pins).

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification or an unmet clause on a stated domain, with the reason and the design §10 entry. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the owner. |
| N/A | Not applicable in this port. |

## Requirement rows

Tests are in `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/` unless a row says otherwise.

| ID | Level | Mark | Evidence |
|---|---|---|---|
| `CFG-1` | MUST | ✅ | Tier 4 is the record's initializer, tiers 2 and 3 the caller's `IConfiguration` providers, tier 1 the `Configure` / `ConfigureTransport` / `ConfigureCredentials` transforms applied last in call order (§10 entry 25, P9-6). `ConfigurationPrecedenceTests` (`Unit`, `ConfigurationPrecedenceTests.cs`): three in-memory providers, removing each from the top resolves the next; two transforms resolve the second; a transform overrides a configuration value; one returning `null` or throwing fails naming the client. |
| `CFG-2` | MUST | ✅ split | ✅ an empty or whitespace-only value is absent (the record default applies, never `""`); 🚫 the fall-through to a lower tier (§10 entry 25, P9-7: `IConfiguration` merges providers before the reader sees a key, so the shadowed lower value is not consulted). `EmptyValueTests` (`Unit`, `EmptyValueTests.cs`): an empty or blank value over a lower provider's value resolves the record default, for a string, an integer, a duration and a list key. Open for the lead (A: as built, a silent shadow; B: an empty value fails start). |
| `CFG-3` | MUST | ✅ | .NET's `__` to `:` rule, case-insensitive. `EnvironmentNormalizationTests` (`Integration`, `EnvironmentNormalizationTests.cs`): a process variable under a per-test GUID prefix through `AddEnvironmentVariables(prefix)` resolves `Retry:MaxRetryAttempts` through the reader; removed in `finally`. |
| `CFG-4` | MUST | N/A | Vacuous: nothing normalises casing away, so there is no raw accessor. `EnvironmentNormalizationTests.A_camel_case_and_an_upper_case_spelling_resolve_alike` pins that two spellings resolve alike. |
| `CFG-5` | MUST | ✅ split | ✅ base-10 `[+-]?[0-9]+` in ASCII, in the target's range, a negative parsed and handed to the record; 🚫 the never-throw letter (§10 entry 26): a bad integer fails the boot naming the key. `IntegerGrammarTests` (`Unit`, `IntegerGrammarTests.cs`; ports `configuration_test.rb`, `1_000` rejected where Ruby tolerates it, P9-26), `RecordBindingTests` (`Unit`). |
| `CFG-6` | MUST | ✅ split | ✅ only case-insensitive `true`/`false`, after an ASCII trim; 🚫 the fall-back to a default (§10 entry 26). `BooleanGrammarTests` (`Unit`, `BooleanGrammarTests.cs`): the eight tokens of the Ruby table fail; `" false "` reads as `false` (P9-27, §11 item 67). |
| `CFG-7` | MUST | ✅ split | ✅ the grammar, read as the day-time ISO subset with digits-only bare numbers (P9-29, §11 item 67); 🚫 return-the-default on rejection (§10 entry 26). `DurationGrammarTests` (`Unit`, `DurationGrammarTests.cs`): the sibling tables, the four `XmlConvert` divergences (case accepted; a sign, calendar units and sub-tick fractions rejected), the three-grammars-one-scale property over 0 to 100 000 s, no throw for 2 000 arbitrary strings, no input quoted. |
| `CFG-10` | MUST | N/A | Vacuous: a transform is code, and a record has no removal. `ConfigurationPrecedenceTests.Precedence_has_no_removal_operation` (`Unit`): the registration exposes no member named `Remove*` or `Clear*`. |
| `CFG-11` | MUST | ✅ | The seams are `IConfiguration` providers (in-memory in every test but `EnvironmentNormalizationTests`, which proves the production default) and `UseProxyFromEnvironment(Func<string, string?>)`. `ConfigurationSeamTests` (`Unit`, `ConfigurationSeamTests.cs`): a recording lookup observes exactly the proxy variables the resolver reads and no other; `EnvironmentNormalizationTests` (`Integration`). |
| `CFG-14` | SHOULD | ✅ | `DexpaceConfigurationKeys`: `SectionName`, `MaxRetryAttempts`, `LogLevel`, `HttpProxy`, `HttpsProxy`, `NoProxy`. `KeyConstantsTests` (`Unit`, `KeyConstantsTests.cs`): the values, the closed set of six, the proxy names equal what `ProxyOptions.FromEnvironment` reads, and an IL scan finds no `ldstr "Dexpace"` in the package (published, not read). |
| `CFG-37` | MUST | ✅ | `ArgumentNullException.ThrowIfNull` on every public parameter; `ArgumentException` for a blank name. `NullGuardTests` (`Unit`, `NullGuardTests.cs`): by reflection over the whole public surface of the extension class and the builder, every reference parameter of every method; it fails when a public parameter type has no example value. |
| `CFG-38` | MUST | ✅ | Every typed value reaches its parser through the one reader over the one `IConfiguration`. `TypedLookupTests` (`Unit`, `TypedLookupTests.cs`): an integer, a boolean and a duration supplied only by a lower provider resolve; the record default applies only when no provider has the key; `KeyTableTests` (`Unit`) is the drift guard over the seven records and the three credential classes. |
| `SEAM-5` | MUST | ✅ DI half | DI half: an explicit `UseTransport`/`UseSerde` wins and replaces the default; no codec fails with the install hint; two or more keyed descriptors fail listing every implementation. DI-less half N/A (2b). `SingleRegistrationTests` (`Unit`, `SingleRegistrationTests.cs`), `ProviderValidationTests` (`Unit`). |
| `SEAM-6` | MUST | ✅ DI half | DI half: the same instance or delegate twice is a no-op; a different one throws `InvalidOperationException` at the call naming the client and both implementations (P9-16: earlier than design §3.6's validator timing); one name registers once. `SingleRegistrationTests`, `AddDexpaceClientTests` (`Unit`), `AuthRegistrationTests` (the one auth authority). |
| `OBS-35` | SHOULD | ✅ split | ✅ layered, case-insensitive, trimmed level by name from the caller's section, no baked key; 🚫 the fall-back on an unrecognised value (§10 entry 26, which now touches `OBS-35`). `LogLevelBindingTests` (`Unit`, `LogLevelBindingTests.cs`): a number or an unknown name fails naming `Logging:Level` and listing `None, Headers, Body` (V1: both binder modes admit them); a section at another path binds the same; a section without the key leaves the default. |

**Totals.** 15 rows, 13 MUST and 2 SHOULD. 13 ✅ (`CFG-1`, `-2`, `-3`, `-5`, `-6`, `-7`, `-11`, `-14`, `-37`, `-38`, `SEAM-5`, `SEAM-6`, `OBS-35`; of which 5 split: `CFG-2`, `CFG-5`, `CFG-6`, `CFG-7`, `OBS-35`), 2 N/A (`CFG-4`, `CFG-10`), 0 🚫, 0 ⏳: 13 + 2 = 15. No row adds to the *Unsatisfied MUSTs* count: the never-throw
clauses are §10 entry 26 and `CFG-2`'s fall-through is entry 25's judged mechanism.

## Rows owned elsewhere and non-row hand-offs

| ID / hand-off (owner) | Disposition | Evidence |
|---|---|---|
| `SEAM-7` (2b) | N/A stays; each client's transport, codec, pipeline and options are keyed singletons created once ("an unresolved state stays re-evaluable" has nothing to apply to) | `KeyedSingletonTests` (`Unit`): 64 concurrent first resolutions run each factory once |
| `TRANSPORT-1` (8b) | ✅; the named-client clause added | `PrimaryHandlerTests` (SystemNet suite, `Unit`), `NamedClientTests` (`Unit`), `NamedClientRedirectWireTests` (`Security`), `transport-1.redirect-not-followed` on the third driver (`Conformance`) |
| `XCUT-10` (10) | ⏳ 10; phase 9's clause ✅ | `NoRetryingHandlerTests` (`Unit`), `ResilienceCompositionTests` (`Integration`) |
| 5a: bind the records | taken by a hand-written reader (a third route: neither binder mode serves, V1) | the reader's tests (`RecordBindingTests`, the grammar tests) |
| 5a (P5a-4): `IValidateOptions` | the reader's failures plus the wiring checks in one validator; no cross-property failure (P9-10) | `ValidationTests` (`Unit`) |
| 5a (P5a-16), 8b (P8b-4): `FromEnvironment` | code-only `UseProxyFromEnvironment()` and `(lookup)`; no configuration switch (P9-15) | `ProxyRegistrationTests`, `NoImplicitEnvironmentReadArchitectureTests` (`Unit`) |
| 5b (P5b-16): V4 | answered by V1 (the binder admits a numeric and an undefined level) | `ScalarGrammarTests`, `LogLevelBindingTests` |
| 6a: `RetryableStatusCodes` | a list key of integers (V1: both binders drop it silently) | `RecordBindingTests` |
| 6a, #94, §6.1, §11 item 24: the resilience guidance | refined: the SDK-side `MaxRetryAttempts = 0` option withdrawn, the composition is `Retry.ShouldHandle` returning `false` (P9-19) | `ResilienceCompositionTests`, `dependency-injection.md`, `retry.md` correction |
| 6b: `AllowedMethods`, `Predicate` | a list key through `Method.Of`; `Predicate` code-only | `RecordBindingTests` |
| 6c: auth registration | `Auth:*`, `UseAuth`, `UseAuthPolicy`, `ConfigureCredentials`; no secret in any failure, `ToString` or log line | `AuthKeyTests`, `AuthRegistrationTests`, `CredentialRedactionTests` (`Unit`) |
| 7a: `ISerde` from `CreateDefaultOptions` | composition, not a reference (P9-17) | `SerdeRegistrationTests` (`Unit`) |
| 7c: nothing | confirmed | (design) |
| 8a: the third driver | built | `DependencyInjectionConformanceTests` (`Conformance`) |
| 8b: the handler constructor, `CreatePrimaryHandler` | built | `PrimaryHandlerTests` |
| 8b: `SystemNetHttpClientOptions` as the binding target | `Transport:*` keys; `Logger` and `TimeProvider` from the container | `TransportBindingTests` |
| 8b (P8b-18), 4a (P4a-13): `CallKey` stamp | not built (P9-20): no reader | `docs/first-release.md` trigger; §11 item 30 correction; phase 11 inbound |
| Phase 1 (S3) | row 9 closed | `NamedClientRedirectWireTests` (`Security`, `Security/NamedClientRedirectWireTests.cs`) |
| 2b: `SEAM-7`'s singleton lifetime | cited | `KeyedSingletonTests` |

**`Security`.** One class is added (`NamedClientRedirectWireTests`) and none is edited: `git status` over `tests/Dexpace.Sdk.Core.Tests/` and `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/` is empty, and `Dexpace.Sdk.Core` and `Dexpace.Sdk.Serialization.SystemTextJson` (source and `PublicAPI.Unshipped.txt`) are untouched.

## Styleguide audit groups

`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md` and `02-dependency-injection.md`, audited for the package. **Conforming:** keyed services for several implementations of one role (2.6); no service locator in the package (factories resolve from the root provider); `ValidateOnStart` with an accessor choice stated
(`IOptionsMonitor.Get(name)` only, no change callback subscribed, 1.5); lifetimes are singletons; `ValidateScopes` and `ValidateOnBuild` are exercised by `ProviderValidationTests` (2.5); no `Environment.GetEnvironmentVariable` in the package. **Departures, recorded:** (1) 1.3 ("the Options pattern is the only sanctioned reader"): the package reads the section itself
(P9-4), a `Conflicts` note in `docs/knowledge/notes/configuration.md`; (2) 2.7 ("a feature extension returns the collection so calls chain"): `AddDexpaceClient` returns a `DexpaceClientBuilder` (the `IHttpClientBuilder` precedent; `builder.Services` chains back), same note.

## Deviations from the plan and the design

1. **One working tree, no commits.** The four stacked PRs, their branches and commits were not made (the instruction was to leave everything as working-tree changes); the task order was the plan's.
2. **No `TransportSnapshot.cs`.** `DexpaceClientState` holds a `SystemNetHttpClientOptions` (the logger and clock left at their defaults and replaced by the container) and the pooled-connection lifetime directly; the extra record carried nothing the two did not.
3. **Per-name registrations are plain `AddSingleton` factories**, not `TryAddEnumerable`: the setup and the validator are one instance per client name, constructed over that name's registration, so there is no implementation type to deduplicate on.
4. **`KeyTable` gained a `Groups` list and the reader a `Touched` set** (the plan had a `CodeOnly` list and `HandlerLevel` only): the nested records (`Retry`, `Redirect`, `Logging`, `Proxy`, the three credentials) are covered by their children's keys, and `KeyTableTests.The_reader_asks_for_exactly_the_keys_of_the_table` holds the reader to the table so a key cannot be read and not accepted.
5. **`BaseAddress` and every credential failure carry fixed rule text**, not the accessor's message: the record's own message includes the redacted URL (design C says `UrlRedactor`; the plan's test says the failure does not contain the URL, which wins), and an accessor message over a credential could echo it.
6. **`PrimaryHandlerLease.Track` after disposal disposes the late handler at once and keeps every tracked handler** (R7 said a second call disposed the earlier instance); the caller-replaced-primary case is observed through the lease's tracked list.
7. **The lease ordering test uses a tracked stand-in handler and a factory transport** rather than the package's own transport, because the package transport needs a live chain; the real ordering is covered by `Disposing_the_container_releases_the_pooled_connection`.
8. **`AddPolicy`'s stage test uses `PerCall` and `PerAttempt`**: `Diagnostics` is a pillar and refuses a second occupant.
9. **`UseProxyFromEnvironment` called twice keeps the last**, where the design was silent.
10. **R5's `NU1109` risk did not materialise**: the `Microsoft.Extensions.Http.Resilience` 10.10.0 restore succeeded with the transitive pins in place, and the handler accepts a 1 ms `Retry.Delay` (design F23's residual); the three props lines are `Microsoft.Extensions.Http`, `...Configuration.EnvironmentVariables` and `...Http.Resilience`.
11. **`Only_the_borrowed_assertion_is_not_exercised_and_the_driver_says_why`** was added to the third driver's tests; the kit reports `transport-15.borrowed-survives` as the one `NotExercised` assertion.
12. **The `ci.yml` header comment** names the third driver; no step changed.
13. The design's open rulings were taken at their recommended defaults, as the plan says: P9-1, 2, 4, 5, 7, 9, 10, 12, 15, 17, 19, 20, 28 and 29.

## Gates run

`dotnet restore Dexpace.Sdk.sln --locked-mode`; `dotnet build Dexpace.Sdk.sln -c Release` (0 warnings, 0 errors: `RS0016`/`RS0017` for the two changed `PublicAPI` files, `RS0030` with the new ban, `MA0051`, `CA2007`); `dotnet format --verify-no-changes`; `dotnet test --solution Dexpace.Sdk.sln -c Release` with coverage (6 857 tests, 0 failed, 22 skipped; the DI
suite is 387 tests (5 skipped: the waived and not-exercised kit rows), 97.5 % line coverage; the gate's aggregate is 97.6 % against 80 %); the coverage gate's self-test; `dotnet pack`, the dependency audit (`5 libraries conform`) and the reproducible-pack check; the NativeAOT smoke publish and run (no trim or AOT warning, `all checks passed`); the tools solution build and tests; `scripts/knowledge verify-structure`; the housekeeping probe.

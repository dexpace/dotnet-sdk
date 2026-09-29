## 9. Toolchain and Quality Gates

Every gate the reference build enforces has a .NET counterpart, and on this host most of them are not assembled from
parts: the SDK ships the compiler, the analyzers, the formatter, the trimmer, the NativeAOT compiler, deterministic
compilation, SourceLink and package validation. The work is wiring them so a single `dotnet build` plus
`dotnet test` in CI blocks on all of them together (**NFR-17**), the way `./gradlew build` does for the reference.
The table records each gate and its state at `d45e64b`; "wired" means it blocks CI today. Toolchain facts marked
verified were checked on .NET SDK 10.0.401 against a scratch copy of the tree.

| Reference gate | .NET gate | State at d45e64b |
|---|---|---|
| Warnings as errors (**NFR-6**) | `TreatWarningsAsErrors` in `Directory.Build.props`, covering compiler, analyzer, deprecation (`CS0618`) and NuGet audit warnings | wired — and currently *failing* on `NU1902` (below) |
| Style/static analysis, findings fatal (**NFR-7**) | `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild` + `.editorconfig` severities; `dotnet format --verify-no-changes` | analyzers wired; `dotnet format` passes (verified) but is not a CI step |
| Explicit public API (**NFR-3**) | `GenerateDocumentationFile` + `CS1591` as error; `internal` by default; `Microsoft.CodeAnalysis.PublicApiAnalyzers` | docs gate wired; the analyzer is pinned in `Directory.Packages.props` and referenced by no project |
| API-surface snapshot (**NFR-4**) | `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` per project (`RS0016`, `RS0017`) plus `EnablePackageValidation` with `PackageValidationBaselineVersion` for binary compatibility | not wired |
| Aggregate line-coverage floor (**NFR-5**) | coverlet threshold (`Threshold=80`, `ThresholdType=line`, `ThresholdStat=total`) on the library assemblies | coverage collected in CI, no threshold |
| Zero-dependency audit (**SEAM-1**, **NFR-1**, **NFR-2**) | nuspec dependency assertion per package; architecture tests over assembly references (§9.2) | not wired |
| Shrink-survival (**NFR-8**, **NFR-9**) | `IsTrimmable`/`IsAotCompatible` on every library (trim, single-file and AOT analyzers as errors) plus a NativeAOT-published smoke consumer run against a loopback server (§9.2) | `IsAotCompatible` on the STJ package only |
| Runtime floor (**NFR-10**) | per-TFM reference packs; tests executed on each TFM's runtime; higher-floor capability isolated by TFM or package | CI installs the .NET 8 runtime; `Core` and `Http.SystemNet` are `net8.0`-only |
| Concurrency-model agnosticism (**NFR-11**) | a test scanning `PublicAPI.Shipped.txt` for types outside `System.*`, `Microsoft.Extensions.Logging.*` and `Dexpace.*` | not wired |
| Version single source (**NFR-14**) | `Directory.Packages.props` (central package management) and `VersionPrefix`/`VersionSuffix` in `Directory.Build.props` | wired |
| Reproducible artifacts (**NFR-12**) | `Deterministic`, `ContinuousIntegrationBuild` in CI, `SOURCE_DATE_EPOCH` for the `.nupkg` container; a pack-twice-and-compare job | compiler determinism wired; no comparison job |
| License headers (**NFR-13**) | `file_header_template` + `IDE0073` at error severity under `EnforceCodeStyleInBuild` | review convention only |
| Runtime version metadata (**NFR-15**) | `AssemblyInformationalVersion` read by `SdkVersion` for the User-Agent | wired (fallback `0.0.0`, §8.2) |
| Signed publications (**NFR-16**) | strong naming (`SignAssembly`, committed key) and NuGet author signing on the release job only | not wired |
| Dependency CVE scanning | NuGet audit (`NuGetAudit`, on by default) under warnings-as-errors | wired, and it is what is failing |
| Formatting and lock files | `dotnet format --verify-no-changes`; `RestorePackagesWithLockFile` + `dotnet restore --locked-mode` | neither in CI |
| Banned APIs (§8.1, §8.3) | `Microsoft.CodeAnalysis.BannedApiAnalyzers` (`RS0030`) with a committed `BannedSymbols.txt` | not wired |
| Serde independence of streaming and paging (**SSE-37**) | architecture test over type references (§9.2) | not wired |
| Core names no concrete seam implementation (**SEAM-2**) | architecture test: no `Dexpace.Sdk.Core` type references a `Dexpace.Sdk.Http.*` or `Dexpace.Sdk.Serialization.*` assembly | holds by project graph; not asserted |

**The baseline break, stated once.** `dotnet build` of `Dexpace.Sdk.sln` fails at `d45e64b` with `error NU1902:
Warning As Error: Package 'Microsoft.Build.Tasks.Git' 8.0.0 has a known moderate severity vulnerability` (verified).
The package arrives transitively through the explicit `Microsoft.SourceLink.GitHub` 8.0.0 reference each `src`
project carries. That reference is redundant: the .NET 8+ SDK ships SourceLink in-box. Verified on the scratch copy:
with the three `PackageReference`s and the `Directory.Packages.props` row removed, the solution builds clean, and the
Core PDB still carries the `https://raw.githubusercontent.com/dexpace/dotnet-sdk/<commit>/*` source-link mapping. So
the fix is a deletion, and it is P2 applied to tooling: a package whose only job duplicates something the SDK ships
retires. It is roadmap phase 0, because until it lands the "warnings as errors" gate is not a gate but a wall — and
the
wall is the gate working as intended, since NuGet audit is exactly the CVE check the table asks for.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

### 9.1 API-surface lock, and what the analyzers can and cannot see

**NFR-4** is the one gate .NET provides twice, and both halves are needed. `PublicApiAnalyzers` makes the source-level
surface data under version control: every public member must appear in `PublicAPI.Shipped.txt` or
`PublicAPI.Unshipped.txt`, `RS0016` fails the build on an undeclared addition and `RS0017` on a removal the file still
lists, so an addition shows up in the pull-request diff where a reviewer can refuse it — the workflow **NFR-4**
describes, down to "regeneration is deliberate" (the code fix writes the file; review decides whether to keep it).
It is already pinned in `Directory.Packages.props` at 3.3.4 and referenced by no project, which is the cheapest gap in
this chapter to close. Package validation is the second half: `EnablePackageValidation` with a
`PackageValidationBaselineVersion` runs API compatibility against the previously *published* package, catching the
binary breaks a source listing hides (a parameter gaining a default value, a method moving to a base class, a
`readonly` removed from a struct). It also checks that every target framework in one package exposes a compatible
surface, which matters the moment `Core` becomes `net8.0;net10.0` (§9.2). `dotnet pack
-p:EnablePackageValidation=true` ran clean on the scratch copy with no baseline (verified); the baseline is set at the
first published version.

**NFR-3**'s explicit surface is carried by three mechanisms that already exist or are one line away:
`GenerateDocumentationFile` with `CS1591` as an error, which is wired and is why every public member has a `///`
comment; `internal` as the default visibility with `InternalsVisibleTo` for test assemblies only (§2.3 withdraws the
as-built grant to the reference transport); and the public-API files above. `CA1515` ("consider making public types
internal") is not relied on — it targets applications, not libraries whose public surface is the product.

**The banned-API gate is new and is this port's counterpart of Ruby's `Dexpace/NoThreadInterrupt` cop.**
`BannedApiAnalyzers` fails the build with `RS0030` on any symbol listed in a committed `BannedSymbols.txt`: the
`Thread.Interrupt` and flow-suppressing `ExecutionContext.SuppressFlow`/`ThreadPool.UnsafeQueueUserWorkItem` of §8.3
and §8.1 (the latter two outside the one background-launch helper of §5.4); `Task.Result`, `Task.Wait` and
`GetAwaiter().GetResult()` outside the documented sync bridges of §3.3 and §5.3
(`docs/styleguide/csharp/09-concurrency.md` 9.1, whose own enforcement names `VSTHRD002`, an analyzer this repository
does not reference); `Uri.ToString()` on any wire, log or equality path (§3.5); the value-comparing
`ConcurrentDictionary.TryRemove(KeyValuePair<,>)` overload outside the context store's slot type, and
`ConditionalWeakTable`/`WeakReference<T>` in core (§5.4); `TypeNameHandling` other than `None` and `BinaryFormatter`
(§3.4); `new HttpClient()` outside the transport's owned-client factory
(`docs/styleguide/csharp/13-resource-management.md` 13.8); and `Console.Write*`
(`docs/styleguide/csharp-aspnetcore/06-logging-and-observability.md` 6.8). A justified exception is a scoped
`#pragma warning disable RS0030` with a why-comment, which is the house rule for every waiver
(`docs/styleguide/csharp/01-formatting-and-tooling.md` 1.2).

### 9.2 The zero-dependency gate, and NFR-8 applying literally

**NFR-8** exempts ecosystems without whole-program dead-code elimination; .NET has two — ILLink trimming and
NativeAOT — and both remove what they cannot see referenced statically, so the requirement applies as written. This
reverses the Ruby conclusion (which retargeted the gate) and the Node one (which scoped it down because bundlers see a
static graph): .NET's shrinker has exactly the reflection blind spot the reference's R8 gate guards against. The .NET
mechanism is better than a keep-rules file, and the difference is the argument for how the gate is built: instead of
shipping rules a downstream shrinker must honour, a library declares `IsTrimmable` and `IsAotCompatible`, which turn
on the trim (`IL2xxx`), single-file (`IL3000`-range) and AOT (`IL3050`-range) analyzers at *the library's own build*,
so reflection the trimmer would break is a compile error here rather than a runtime failure in a consumer. **NFR-8**'s
"keep/retain configuration" becomes annotations (`[DynamicallyAccessedMembers]`, `[RequiresUnreferencedCode]`) on the
few members that need them, carried inside the assembly where no consumer can lose them. As built, only
`Dexpace.Sdk.Serialization.SystemTextJson` declares `IsAotCompatible`; `Core` does not, although it is already clean —
verified, building `Core` with `-p:IsAotCompatible=true` produced no `IL` warnings — so the property costs nothing to
add to every library, and the STJ package's analyzers currently cannot see into `Core` at all.

**NFR-9**'s regression guard is a NativeAOT smoke consumer: a tiny console project in `tests/` referencing the
packages, published with `PublishAot=true` and trim warnings as errors, whose `Main` performs a live round trip
against
an in-process loopback server — a JSON request through the pipeline with a source-generated context, a `Tristate<T>`
PATCH body (§7.3), a paged walk and an SSE stream — and exits non-zero on any mismatch. It is the only gate that can
catch the class of failure §7.3's `Tristate` converter factory suppresses a warning for; the analyzer proves the code
is *annotated*, the smoke test proves the annotation is *true*. It is feasible in CI as it stands: the scratchpad
toolchain published and ran NativeAOT binaries on Linux with only the cached `Microsoft.DotNet.ILCompiler` package and
the system `clang` (verified during §7.3's converter comparison). **NFR-9**'s "assert every shipped rule file is
present" becomes asserting the `IsTrimmable`/`IsAotCompatible` properties are set in each packable project.

**The zero-dependency audit** (**SEAM-1**, **NFR-1**, **NFR-2**) has three parts. (1) A packaging test reads each
`.nuspec` produced by `dotnet pack` and asserts the dependency groups: `Dexpace.Sdk.Core` lists exactly
`Microsoft.Extensions.Logging.Abstractions` (the recorded exception, §8.1 and §10
entry 1); each adapter lists `Dexpace.Sdk.Core` plus at most one other package.
`Dexpace.Sdk.Serialization.SystemTextJson` lists only `Core`, because System.Text.Json is in the shared framework on
both targets — the adapter's "one third-party library" is the runtime's own. (2) Architecture tests over the compiled
assemblies — reflection over type references in a test, no NetArchTest dependency needed for three rules — assert that
nothing in `Dexpace.Sdk.Core` references a transport or serializer assembly (**SEAM-2**), and that
`Dexpace.Sdk.Core.ServerSentEvents` and the paging engine reference nothing in `Dexpace.Sdk.Core.Serialization`
(**SSE-37**). (3) The in-box-versus-package split, which is .NET's version of Ruby's default-gem trap and the one gate
here with no reference counterpart: `System.IO.Pipelines`, `System.Net.ServerSentEvents` and
`System.Linq.AsyncEnumerable`
are in the shared framework on .NET 10 and absent from the .NET 8 reference pack (verified), so a convenience that
reaches for one on the `net8.0` target silently acquires a NuGet dependency. The nuspec assertion catches it, per
target framework group.

**NFR-10**'s trap is mostly closed by construction on .NET and reopens in one place. A `net8.0` build compiles against
the `net8.0` reference assemblies, so a call to an API added in .NET 9 is a compile error, not the call-time failure
the requirement warns of — the "emitted-artifact target and the visible-API level must agree" clause is the SDK's
default. It reopens through packages whose `net8.0` asset differs from their `net10.0` one, and through
`#if NET10_0_OR_GREATER` sections, both of which only running the tests *on each runtime* catches. CI does install the
.NET 8 runtime for this reason; the gate is that the `net8.0` test target actually runs there, on every pull request.
The declared floor is `net8.0`, with a caveat the design must carry: .NET 8 leaves support on 2026-11-10, so the floor
rises to `net10.0` in the roadmap, and that single change retires §7.2's hand-written line-reader justification for
`PipeReader`, the `net8.0` STJ gap for `RespectNullableAnnotations` (§7.3) and the `System.Linq.AsyncEnumerable`
caveat (§7.1). Until then, the target-framework settings are inconsistent: the pre-roadmap modern multi-target decision
(Porting Method) and the
styleguide overlay say libraries are `net8.0;net10.0`, but `Dexpace.Sdk.Core` and `Dexpace.Sdk.Http.SystemNet` target
`net8.0`
only, while the STJ package and both test projects multi-target — so the `net10.0` test run exercises `Core`'s
`net8.0` binary. Moving `TargetFrameworks` into `Directory.Build.props` fixes both at once
(`docs/styleguide/csharp/01-formatting-and-tooling.md` 1.1).

**Correction (2026-09-28): roadmap decision D1 is approved, so the floor is `net10.0` only.** The lead ruled on
D1 on 2026-09-28. The target framework is `net10.0` for every project, set once in `Directory.Build.props`. The
paragraph above, which declares a `net8.0` floor and a `net8.0` test leg as the **NFR-10** gate, is superseded.
There is now one target and one runtime, and the gate is that the tests run on it on every matrix row.

Part (3) of the zero-dependency audit also has no subject now. That is the in-box-versus-package split for
`System.IO.Pipelines`, `System.Net.ServerSentEvents` and `System.Linq.AsyncEnumerable`, and all three are in-box on
`net10.0`. The nuspec assertion still reads every dependency group, and it now also requires there to be exactly one,
`net10.0`.

The same ruling corrects §9.3's CI matrix: each row installs only the SDK that `global.json` pins, which carries the
.NET 10 runtime, and the tests run on one target framework. The text above stands as written, and this paragraph is
the correction.

**Correction (2026-09-28): the adapters' nuspecs also list the logging facade.** Part (1) above says
`Dexpace.Sdk.Serialization.SystemTextJson` lists only `Core`. That no longer holds, because
`CentralPackageTransitivePinningEnabled` is on (§2.3). NuGet promotes a centrally pinned transitive package to a
direct nuspec dependency. The facade reaches each adapter through `Core`, so pinning it promotes it. As packed, both
adapter nuspecs (`Dexpace.Sdk.Http.SystemNet` and `Dexpace.Sdk.Serialization.SystemTextJson`) have a single
`net10.0` group. That group lists `Dexpace.Sdk.Core` 0.0.1-alpha.1 and `Microsoft.Extensions.Logging.Abstractions`
10.0.12. `Dexpace.Sdk.Core` lists the facade alone. Roadmap constraint 2 holds adapters to the same rule as core,
plus at most one third-party library, and that rule allows the facade, so the result is consistent with it. Neither
adapter lists a third-party library. `scripts/ci/dependency-audit.cs` accepts the facade in an adapter's nuspec, and
in no other extra dependency. The sentence above stands as written, and this paragraph is the correction.

**NFR-11**'s "leaks no async-framework types" is satisfied in spirit before any gate: `Task`, `ValueTask`,
`IAsyncEnumerable<T>` and `CancellationToken` are the runtime's own, not a framework's. The mechanical form is a test
over `PublicAPI.Shipped.txt` asserting every type named in a public signature is from `System.*`,
`Microsoft.Extensions.Logging.*` or `Dexpace.*` — which also catches a transport's `HttpRequestMessage` escaping into
a
core signature.

**Artifact hygiene** (**NFR-12**–**NFR-16**). Compiler output is deterministic (`Deterministic` is on, and
`ContinuousIntegrationBuild` normalises paths in CI), and two packs of the same tree produced byte-identical DLLs
(verified); the `.nupkg` *containers* differed only by zip-entry timestamps, and with `SOURCE_DATE_EPOCH` set, two
packs
a minute apart produced byte-identical `.nupkg` files (verified), so **NFR-12**'s gate is a CI job that packs twice
with `SOURCE_DATE_EPOCH` set to the commit time and compares digests. **NFR-13** becomes mechanical rather than a
review
convention: `file_header_template` in `.editorconfig` with `IDE0073` at error severity fails the build on a file
missing the two-line MIT header (verified: a header-less file raised `IDE0073` under `EnforceCodeStyleInBuild`).
**NFR-16**:
nuget.org repository-signs every package, which satisfies provenance for consumers; author signing with the
organisation's certificate runs on the release job only, optional locally, per the requirement's own split. Strong
naming, which the pre-roadmap platform design listed among its native defaults and nothing has built yet, is not a
security measure on .NET
(Core); it is kept because
strong-named consumers cannot reference an unsigned assembly, with the key committed as Microsoft's library guidance
recommends.

### 9.3 Tests, and why a local server rather than only a stubbing library

**xUnit is the framework, and the conformance kit is framework-free.** The house guide wants xUnit v3 on
Microsoft.Testing.Platform with Shouldly (`docs/styleguide/csharp/11-testing.md` 11.1, 11.5); the repository runs
xUnit v2
with `Assert`, and the overlay records the migration as a roadmap item, not a deviation. The tree has one test project
per *two* production assemblies — `Dexpace.Sdk.Core.Tests` also covers `Http.SystemNet`, whose csproj grants
`InternalsVisibleTo` to a `Dexpace.Sdk.Http.SystemNet.Tests` that does not exist — against 11.1's one-to-one rule; the
transport tests move to their own project, which is also what makes them the first consumer of the conformance kit.
Tests are partitioned by `[Trait("Category", ...)]` into `Unit` (hand-built fakes, `TimeProvider` fakes, no socket —
the large majority), `Integration` (the reference transport against the loopback server), `Conformance` (the kit
below), and `AotSmoke` (the §9.2 consumer, run by its own CI job), so a filter selects each and the default run is
fast.

**The transport conformance suite ships as a package from day one** — `Dexpace.Sdk.Conformance` (§2.1) — for the same
reason Ruby's `dexpace-conformance` gem and Node's `transport-conformance` package exist: an adapter author outside
this repository must be able to prove **TRANSPORT-1**–**TRANSPORT-30** against their own transport with the same
assertions the first-party transport passes. Two .NET-specific choices. First, **the assertions are plain methods that
throw a `ConformanceException` carrying expected and actual values**, with no test-framework dependency in the
package — each first-party adapter's test project drives the kit from xUnit — because an adapter author on NUnit
or MSTest should not inherit this repository's
test framework — the same reason Ruby kept Minitest out of the kit's runtime. Second, **the fixture is a `TcpListener`
speaking HTTP/1.1 by hand, not Kestrel**, for the reasons Ruby rejected a stubbing library and one more. A stub or a
custom `HttpMessageHandler` cannot express socket-level behaviour: connect-versus-read timeout classification
(**TRANSPORT-3**, **TRANSPORT-4**), a lazily-read body whose disposal releases the connection (**TRANSPORT-25**),
chunked framing, a half-closed peer, and vendor status codes surfaced faithfully (**TRANSPORT-24**); and a handler
stub is specific to `HttpClient`, so the same assertions could not run against a non-`HttpClient` transport. Kestrel
would speak the wire correctly — which is the problem: **TRANSPORT-14** needs a response with a control byte in one
header value, delivered while the rest of the response is intact, and a conforming server refuses to emit it. The
loopback server used for this chapter's instrumentation verification is the seed (§8.1). The kit also carries the
lifecycle assertions — disposal is idempotent, a caller-supplied `HttpClient` survives it (**TRANSPORT-15**,
**XCUT-22**), a send after disposal fails — because those are the clauses an adapter satisfies by accident on the
first
call and not the second. Reporting is per requirement ID, with the Appendix B items as a many-to-one view whose status
is the worst of their assertions, the unit Ruby settled on after finding one B.7 item vacuous for one ID and failing
for another.

**Coverage, mutation and benchmarks.** **NFR-5**'s floor is 80% aggregate line coverage over the library assemblies,
enforced by a coverlet threshold on the default test run, with the conformance kit, the smoke consumer and test
support excluded. Line coverage says a line ran, not that a test checked it, so the modules whose bugs are silent —
header and media-type parsing, the SSE line reader and field grammar, the query splice, URL redaction, retry
classification — run under Stryker.NET on a schedule with a mutation-score break threshold, the optional gate
`docs/styleguide/csharp/11-testing.md` 11.8 names; it is not a per-PR gate because its cost scales with the suite, and
**NFR-17** binds the NFR gates, which mutation testing is not. BenchmarkDotNet lives in a `benchmarks/` project with
`MemoryDiagnoser`, run on demand, and its results are required in the pull request for any performance-motivated
change
(`docs/styleguide/csharp/15-performance.md` 15.8). Allocation *guarantees* — **OBS-1**'s disabled path (§8.1),
**OBS-25**'s untraced path — are not benchmarks but unit tests asserting a zero delta in
`GC.GetAllocatedBytesForCurrentThread()` around the call, because a guarantee belongs in the blocking gate.

**The CI matrix.** One workflow, blocking, per pull request: `ubuntu-latest`, `windows-latest` and `macos-latest`,
each
with the .NET 8 and .NET 10 runtimes installed and the SDK from `global.json`; locked restore; build (warnings as
errors,
analyzers, `IDE0073`, `RS0016`/`RS0017`, `RS0030`); `dotnet format --verify-no-changes`; tests on both target
frameworks with the coverage threshold; `dotnet pack` with package validation and the nuspec dependency assertions;
and,
on Linux only, the NativeAOT smoke job and the pack-twice reproducibility job. The operating-system axis earns its
cost on this SDK specifically: proxy discovery, certificate handling and `HttpClient`'s handler differ by platform,
and
§8.2's proxy semantics were verified on Linux only. `global.json` pins `10.0.100` with `rollForward: latestFeature`
where the house guide shows `latestPatch` (`docs/styleguide/csharp/01-formatting-and-tooling.md` 1.6); the feature
band governs analyzer behaviour, so `latestPatch` is adopted, and the pin moves to the band the team actually builds
with.

**Appendix B.** The specification's conformance checklist covers PAGE, SSE, SERDE, OBS, CFG, TRANSPORT, ASYNC, XCUT
and
NFR only, so Appendix B conformance is a strictly weaker claim than full conformance, and this port says so
(§11 item 9). **B.1**–**B.3** are exercised as written except the items §10 records —
the live-page and executor-mode items of B.1 (§10 entry 17, §10 entry 19). **B.4**'s shared-inert-event identity item
(**OBS-1**) is restated as the zero-allocation assertion above, because there is no event object to be identical
(§10 entry 22). **B.5** is exercised against `IConfiguration` binding with the four-tier order of §8.2.
**B.6** is exercised per transport by the kit. **B.7** needs the most restating, because .NET has no async-runtime
adapters to test: the pivot is the runtime's `Task` (§3.3), so **ASYNC-1**, **ASYNC-2**, **ASYNC-13**,
**ASYNC-14**, **ASYNC-19**, **ASYNC-20** and **ASYNC-22** run against the transport SPI and the sync bridge,
**ASYNC-8**–**ASYNC-12** against `AsyncLocal` flow, and the adapter-scoped items (**ASYNC-21**) are reported as
vacuous-by-antecedent (§11 item 21). **B.9** is this chapter's table, with **NFR-8**/**NFR-9**
applicable and exercised. A failing item the port has decided not to satisfy is reported as a failure by the kit and
suppressed in this repository's build by a named waiver listing the requirement ID, so the gap stays visible.

### 9.4 The vendored styleguide as quality gates

The styleguide under `docs/styleguide/` is binding for every `.cs` file here, and it is only as real as its
enforcement.
The table maps each chapter's enforceable rules to the gate that enforces them in this repository: an analyzer ID
wired
through `.editorconfig` and `TreatWarningsAsErrors`, `dotnet format`, a build property, a test, or review. It is the
design-level half of the overlay in `docs/styleguide/README.md`, which remains the index of *departures*; a rule this
table marks "not wired" is a roadmap item, not a departure. "Wired" means failing the build today at `d45e64b`. Which
`CA` rules `latest-recommended` actually enables was probed rather than assumed: a class library under that level
raised `CA1848`, `CA1860`, `CA1869`, `CA2016`, `CA2201` and `CA2254` as warnings, and did *not* raise `CA1031` (a bare
`catch (Exception)`) or `CA2000` (an undisposed `MemoryStream`) — so the guide rules those two back need explicit
severities before they are gates (verified).

| Chapter | Rules → gate | State |
|---|---|---|
| 01 Formatting & tooling | 1.1 props centralised → review + one `Directory.Build.props`; 1.2 → `TreatWarningsAsErrors`; 1.3 → `AnalysisLevel`, `EnforceCodeStyleInBuild`; 1.4 → `Nullable=enable`; 1.5 → `.editorconfig` + `dotnet format`; 1.6 → `global.json`, `Deterministic`, lock files; 1.7 → `MA0051` at 70 lines | 1.2–1.4 wired; 1.1 broken by per-csproj `TargetFramework`; 1.5's `ImplicitUsings=disable` not followed (§ below); `dotnet format` passes but is not in CI; no lock files; `MA0051` not referenced |
| 02 Naming | 2.1–2.4 → `dotnet_naming_rule` entries + `IDE1006`, `IDE0049`, `IDE0044`; 2.5 → `CA2208`; 2.8 → `CA1715`; 2.6/2.7 (no `I`, no `Async`) → departed | analyzer-backed rules partly wired (`CA*` via the recommended set); no `dotnet_naming_rule` entries yet; 2.6/2.7 departed, §10 entry 27 |
| 03 Nullability | 3.1 → `CS86xx` as errors; 3.3 (`!` banned) → review (no analyzer exists for bare `!`); 3.4 → `IDE0041`; 3.2 → `CA1062` | 3.1 wired; `CA1062` dialled to `none` (below) |
| 04 Variables | 4.1 → `IDE0007`/`IDE0008`; 4.2 → `IDE0090`; 4.3 → `IDE0044`; 4.4 → `CA1802`, `IDE0036`; 4.5 → `IDE0040`; 4.6 → `IDE0003`/`IDE0009`; 4.9 → `IDE0018` | only `csharp_style_var_for_built_in_types` configured (as a suggestion); the rest need `.editorconfig` severities |
| 05 Methods | 5.1 → `MA0051`; 5.3 → `CA1062` + review; 5.4 → `IDE0022`/`IDE0025`; 5.7 → `IDE0062`; 5.8 (no recursion) → review | not wired beyond the recommended set |
| 06 Types | 6.2 sealed → review (`CA1852` for internal types); 6.3 → `CS8509` as error; 6.6 → `CA1815`, `CA1051`; 6.8 → `CA1714`/`CA1717`/`CA1027`/`CA2217` | `CS8509` wired; `CA*` where the recommended set enables them |
| 07 Idioms | 7.1 → `IDE0066`/`IDE0078`/`IDE0260`; 7.3 → `IDE0300`/`IDE0301`/`IDE0305`; 7.5 → `IDE0031`/`IDE0270`; 7.7 → `CA2208` | `IDE` severities not configured |
| 08 Errors | 8.1 → `CA2201`; 8.2/8.4 → `CA1031`; 8.3 → `CA2200`; 8.5 → `CA1062`; 8.6 → `CA1032`, `CA1064` | `CA2201` wired (verified); `CA1031` is not in the recommended set (verified) and needs an explicit severity |
| 09 Concurrency | 9.1 → `RS0030` (§9.1) in place of the unreferenced `VSTHRD002`; 9.2 → review (`VSTHRD100` not referenced); 9.3 → `CA2016`; 9.4 → `CA2007`; 9.5 → `CA2012`; 9.6 → `CS4014`; 9.8 → `CS1996` | `CA2016` (verified), `CS4014`, `CS1996` wired; `CA2007` dialled to `none` (below) |
| 10 API design | 10.3 → `CA1002`, `CA1819`, `CA2227`; 10.5 → `CA1068`; 10.6 → `[Obsolete]` + package validation; 10.7 → `RS0016`/`RS0017` | `CA*` where the recommended set enables them; 10.7 not wired (§9.1) |
| 11 Testing | 11.1 → one test project per assembly, xUnit v3; 11.2 → `xUnit1003`/`xUnit1008`; 11.4 (no Moq), 11.5 (no FluentAssertions ≥ 8) → `Directory.Packages.props` review; 11.6 → `TimeProvider` fakes + review; 11.8 → coverage threshold, Stryker.NET | xUnit analyzers wired via `xunit`; v2 not v3 (overlay); coverage not gated; `Http.SystemNet` has no test project |
| 12 Project organisation | 12.1 → `IDE0161`; 12.3 → central package management; 12.5 → `PublicAPI` files; 12.6/12.9 → architecture tests (§9.2); 12.7 → `ImplicitUsings=disable`, `IDE0005` | 12.3 wired; `IDE0161` wired through `csharp_style_namespace_declarations = file_scoped:warning` (the `:warning` suffix is honoured by `EnforceCodeStyleInBuild`; verified); 12.7 not followed |
| 13 Resources | 13.1 → `CA2000`; 13.3 → `CA1816`; 13.4 → `CA2213`; 13.5 → `CA2215`; 13.8 → `RS0030` on `new HttpClient()` | `CA2000` is not in the recommended set (verified) and needs an explicit severity; `RS0030` not wired |
| 14 Documentation | 14.1/14.6 → `GenerateDocumentationFile` + `CS1591`; 14.7 → `CA1200`, cref warnings; 14.8 → `IDE0005` + review | wired (`CS1591` is the most-exercised gate in the tree) |
| 15 Performance | 15.2/15.5 → `CA1860`; 15.6 → `CA1834`; 15.7 → `CA1822`, `CA1859`, `IsAotCompatible`; 15.8 → BenchmarkDotNet evidence | `CA1860` wired (verified); AOT analyzers on the STJ package only |

The hosting companion bears on two surfaces only (per the overlay). For `Dexpace.Sdk.Extensions.DependencyInjection`:
`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md` 1.3–1.5 (typed options, `ValidateOnStart`, accessor
by
lifetime) and `docs/styleguide/csharp-aspnetcore/02-dependency-injection.md` 2.3–2.6 (deliberate lifetimes, no captive
dependencies, keyed services for multiple clients) are enforced by a test that builds a `ServiceProvider` with
`ValidateScopes` and `ValidateOnBuild` on and resolves every registration. For the serializer:
`docs/styleguide/csharp-aspnetcore/05-serialization-and-validation.md` 5.2 → the `SYSLIB1030`–`SYSLIB1039`
source-generator
diagnostics as errors plus the AOT analyzers; 5.3 → `CA1869`; 5.6 (absent versus null) → §7.3's `Tristate<T>` and its
tests. For instrumentation: `docs/styleguide/csharp-aspnetcore/06-logging-and-observability.md` 6.1 → `CA2254`; 6.2 →
`CA1848` (satisfied by both the generator and `LoggerMessage.Define`, §8.1); 6.7 → §8.1's redaction tests.
`docs/styleguide/csharp-aspnetcore/08-build-and-deployment.md` 8.2 (trim/AOT warnings as errors, publish-and-run smoke
test) is §9.2's gate verbatim; 8.7/8.8 (reproducible, locked, gated) are §9.2 and the CI matrix.

**Where the repository departs from the guide, and the overlay is incomplete.** The overlay records six rows; the
build configuration carries more departures than that, and each is either conformed or recorded. `.editorconfig`
dials five analyzers to `none`, each with a rationale: `CA1308` (lower-casing is correct for HTTP tokens), `CA1054`–
`CA1056` (string URLs at ergonomic entry points), `CA1062`, `CA2007`, and `CA1707` for test names. `CA1308` and
`CA1054`–`CA1056` do not collide with a guide rule. `CA1062` and `CA2007` do: the guide's own `.editorconfig` example
sets both to `error` (`docs/styleguide/csharp/01-formatting-and-tooling.md` 1.3), and 3.2, 5.3, 8.5 and 9.4 name them
as their enforcement. `CA1062` is defensible under nullable reference types, which make a non-nullable parameter a
compile-time contract, and `ThrowIfNull` still guards the public entry points; it is recorded, not conformed.
**`CA2007`'s recorded rationale is factually wrong**, and the overlay repeats it: `.editorconfig` says "`await using`
/
`await foreach` emit implicit awaits the rule cannot see", and the overlay says "The analyzer cannot see the implicit
awaits". Verified on 10.0.401 with `CA2007` at warning in a class library: the rule *does* report on
`await using var m = new MemoryStream();` and on `await foreach (var i in Gen())`. The real cost is ergonomic —
satisfying it on `await using` requires `await using var x = y.ConfigureAwait(false)`, which changes the local's type
to
`ConfiguredAsyncDisposable`, and on `await foreach` a `.ConfigureAwait(false)` on the sequence — and whether that cost
justifies disabling a correctness rule the guide requires in libraries is the decision to record, with the correct
reason. Both are §10 entry 28. `ImplicitUsings=enable` departs from 1.5 and 12.7 and is
not in the overlay; the port conforms (a committed `GlobalUsings.cs`, `ImplicitUsings` off) rather than recording it,
because the guide's reason — every dependency visible at the top of the file — applies with full force to a library
whose dependency surface is under audit (§9.2). The `rollForward` and lock-file departures are conformed likewise
(§9.3), and `MA0051` is the overlay's own roadmap row.

**Correction (2026-09-29): `CA2007` is re-enabled for libraries, so the `CA2007` half of the departure above is
conformed, not recorded.** Roadmap phase 0 (PR #21, 2026-09-28) set `CA2007` to `warning` for everything under
`src/`, an error under `TreatWarningsAsErrors`, and to `none` for tests, repository tools and the AOT smoke consumer
(`[{tests,tools,.claude}/**/*.cs]` in `.editorconfig`), which is the split styleguide 9.4 itself makes (enabled in
libraries, suppressed in app and host projects). Library code satisfies it on `await using` with
`await using var x = y.ConfigureAwait(false)` and on `await foreach` with `.ConfigureAwait(false)` on the sequence,
accepting the ergonomic cost this section weighed. The wrong `.editorconfig` rationale is gone, and the overlay row was
corrected the same day. So three statements above are superseded: the 09 Concurrency row's "`CA2007` dialled to
`none`", `CA2007` in the list of five analyzers `.editorconfig` dials to `none`, and "Both are §10 entry 28" (only
`CA1062` remains a recorded departure; §10 entry 28 carries the matching correction). The `CA1062` reasoning is
unchanged. The text above stands as written, and this paragraph is the correction.

**As built (d45e64b):** partial: warnings-as-errors, recommended analyzers, code-style enforcement, documentation
gate,
central package management, deterministic compilation and NuGet audit wired; build currently broken by `NU1902`;
missing: public-API files, package validation baseline, coverage threshold, `dotnet format` and locked restore in CI,
trim/AOT properties on `Core` and `Http.SystemNet`, AOT smoke consumer, nuspec and architecture tests, `IDE0073`,
banned-API list, strong naming, signing, conformance kit, cross-OS matrix.

---

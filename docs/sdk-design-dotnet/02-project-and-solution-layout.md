## 2. Project and Solution Layout

The port is a single repository and a single solution (`Dexpace.Sdk.sln`) with one project per published NuGet
package under `src/` and one test project per package under `tests/`, the shape `dotnet/extensions` and the Azure
SDK for .NET use for the same problem (one lean core plus independently installable satellites, shared CI, one
central build configuration). The dependency topology is the one **NFR-2** asks for: "each optional capability ...
SHOULD be a separately installable unit depending on the core plus at most one third-party library." The granularity is
the one PR #3 set when it laid out the packages, and this chapter adopts it: **one cohesive `Dexpace.Sdk.Core` toolkit**
holding models, bodies, the transport SPI, errors, the
serde abstraction, pipeline and policies, context, instrumentation, auth, SSE, pagination and webhooks, plus
separate packages for each concrete implementation and for host integration. The alternative set aside then — a
NuGet package per subsystem (`.Auth`, `.Sse`, `.Webhooks`, …), because the subsystems are co-designed and share their
dependencies — is rejected here for the same reason and one more: **NFR-2** is about *optional capabilities that carry a
third-party dependency*, and none of those
subsystems carries one, so splitting them buys release-cadence independence at the price of eight co-versioned
packages and no dependency saving. The one-repository-per-package model is rejected as the Ruby port rejected it:
adapters track the core SPI closely enough during the pre-1.0 period that cross-repository CI would cost more than it
saves.

Package identifiers, assembly names, root namespaces and folder names are the same string, following the scheme
`Dexpace.Sdk.<Area>[.<Impl>]` — `Dexpace.Sdk.Http.SystemNet` is the package, the assembly, the namespace root and
`src/Dexpace.Sdk.Http.SystemNet/` — so a reader can derive any one from any other, which is also what
`docs/styleguide/csharp/12-project-organization.md` rule 12.1 (namespaces mirror folders) requires.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

### 2.1 MVP packages

| Package | Runtime dependencies (NuGet) | Purpose |
|---|---|---|
| `Dexpace.Sdk.Core` | `Microsoft.Extensions.Logging.Abstractions` only — the recorded deviation of §2.4 | Domain model (§4), the byte-stream contract on `Stream` (§3.1), bodies, both transport interfaces and the `Task` pivot (§3.2, §3.3), the serde abstraction (§3.4), the operation projection (§3.5), pipeline and policies (§5), retry/redirect/auth (§6), pagination, SSE and webhooks (§7), options and diagnostics (§8). |
| `Dexpace.Sdk.Http.SystemNet` | `Dexpace.Sdk.Core`; `System.Net.Http` is in the shared framework | The reference transport, implementing **both** `IAsyncHttpClient` and `IHttpClient` over `System.Net.Http.HttpClient`. Because `HttpClient` accepts any `HttpMessageHandler`, this one adapter covers `SocketsHttpHandler`, `WinHttpHandler`, the browser/WASM handler and the mobile native handlers, and composes under `IHttpClientFactory` (§3.2). |
| `Dexpace.Sdk.Serialization.SystemTextJson` | `Dexpace.Sdk.Core`; `System.Text.Json` is in the shared framework | The reference wire codec over source-generated `JsonSerializerContext` metadata. Ships separately per P3 even though `System.Text.Json` costs nothing to embed — §3.4. |
| `Dexpace.Sdk.Extensions.DependencyInjection` | `Dexpace.Sdk.Core`; the `Microsoft.Extensions` hosting family (`DependencyInjection.Abstractions`, `Http`, `Options`, `Options.ConfigurationExtensions`) | Host integration: `AddDexpaceClient(...)`, options binding and validation with `ValidateOnStart`, `IHttpClientFactory` wiring, and the single-registration check that replaces discovery (§3.6, §8.2). Counting that family as one library in **NFR-2**'s sense is a judgement, recorded as §10 entry 2. |
| `Dexpace.Sdk.Conformance` | `Dexpace.Sdk.Core` only | The shared adapter conformance kit (§9.3), published from day one because a third-party adapter author has no other way to prove an adapter against the assertions the first-party adapters run. Its checks are plain methods that throw a `ConformanceException` on failure, with no xUnit, NUnit or MSTest dependency, so the package imposes no test framework on its consumers; each first-party adapter's test project drives it from its own framework. |

**What the Ruby MVP had and this list does not, and why.** The Ruby port shipped two async packages in its MVP — a
thread-pool driver so the async seam was usable without a reactor, and a reactor-native transport so the seam's
properties (multiplexing, structured cancellation, scheduler-native suspension) were exercised rather than merely
shaped. Neither has a .NET counterpart. The pivot is the runtime's `Task` (§3.3), so there is nothing to drive; and
`Dexpace.Sdk.Http.SystemNet`'s `SendAsync` path is already natively asynchronous — no thread is held across the
network wait, cancellation reaches the socket through the `CancellationToken`, and HTTP/2 multiplexing is on by
default — so the one reference transport already exercises every property the async seam exists for. The Ruby rule
this list keeps is the discriminator: **a seam ships in the MVP together with at least one adapter that exercises the
property the seam exists for; a second adapter over the same property is a later package.**

**As built (d45e64b):** partial: `Core`, `Http.SystemNet` and `Serialization.SystemTextJson` exist;
`Extensions.DependencyInjection` and `Conformance` are not built.

### 2.2 Later packages

Deliberately deferred, in rough priority order: `Dexpace.Sdk.Reactive` (`System.Reactive`) bridging pagination and
SSE's `IAsyncEnumerable<T>` to `IObservable<T>` for teams that compose with Rx operators — an adapter, not a seam
(§3.3); `Dexpace.Sdk.Serialization.NewtonsoftJson` (`Newtonsoft.Json`), because a large installed base still models
DTOs with its attributes; `Dexpace.Sdk.Serialization.Xml` over the shared framework's `System.Xml` only if a real
consumer needs a second format to prove **SEAM-19**'s undefaulted media type against. Two packages the reference
module map would suggest are **not** planned. There is no `.Instrumentation.OpenTelemetry` package: on .NET the
OpenTelemetry SDK subscribes directly to an `ActivitySource` and a `Meter` by name, and both types ship with the
runtime (§8.1), so the seam such a package would implement has already retired (P2). And there is no second
first-party transport over a different HTTP library: the .NET ecosystem has converged on `HttpMessageHandler` as the
connection-layer extension point, alternative stacks plug in *there* (under `Dexpace.Sdk.Http.SystemNet`), and a
second adapter would exercise no property the first does not (§2.1's discriminator).

The line between the two lists is Ruby's and survives unchanged: not "how useful" but "what would be unproven
without it."

### 2.3 Layout and versioning

```
dotnet-sdk/
  Dexpace.Sdk.sln
  global.json                         # pins the SDK (10.0.100, rollForward: latestFeature)
  Directory.Build.props               # compiler, analyzer and package metadata for every project
  Directory.Packages.props            # Central Package Management: the only place a version is written
  .editorconfig                       # formatting and analyzer severities (the lint gate)
  nuget.config                        # a single, cleared package source
  src/
    Dexpace.Sdk.Core/
      Dexpace.Sdk.Core.csproj
      Http/{Common,Request,Response}/  Client/  Errors/  Serialization/  Pipeline/{,Policies}/
      Auth/  Pagination/  Configuration/  Diagnostics/  Internal/  (IO/, Operations/, ServerSentEvents/,
      Webhooks/ as they land)
    Dexpace.Sdk.Http.SystemNet/
    Dexpace.Sdk.Serialization.SystemTextJson/
    Dexpace.Sdk.Extensions.DependencyInjection/
    Dexpace.Sdk.Conformance/
  tests/
    Dexpace.Sdk.Core.Tests/                        # references Core only, plus fakes; never a transport
    Dexpace.Sdk.Http.SystemNet.Tests/              # drives Dexpace.Sdk.Conformance against the adapter
    Dexpace.Sdk.Serialization.SystemTextJson.Tests/
    Dexpace.Sdk.Extensions.DependencyInjection.Tests/
    Dexpace.Sdk.AotSmoke/                          # NativeAOT-published consumer: NFR-9's guard (§9)
  docs/
```

**Folders are features, and namespaces mirror them.** Inside a project the layout is by feature (`Http/Request`,
`Pipeline/Policies`, `Auth`) rather than by technical layer, per `docs/styleguide/csharp/12-project-organization.md`
rules 12.1 and 12.4, and one top-level type lives in one file named after it (rule 12.2). The as-built tree already
follows this except where several small exception types share a file (`Errors/TransportExceptions.cs`), which is a
styleguide finding, not a design question.

**Central build configuration is the single source of truth (NFR-14).** `Directory.Build.props` carries the
compiler switches every project shares (`LangVersion` `latest`, `Nullable`, `TreatWarningsAsErrors`,
`AnalysisLevel` `latest-recommended`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`, `Deterministic`) and
the package metadata (`VersionPrefix`, `VersionSuffix`, authors, license expression, repository URLs). The target
frameworks belong there too, rather than in each `.csproj`: every library targets `net8.0;net10.0` (the
pre-roadmap modern multi-target decision, Porting Method, which PR #3 applied only to the STJ package and the test
projects; .NET 8 leaves support on 2026-11-10, after which the floor rises
to `net10.0` in one edit), and every
library sets `IsTrimmable` and `IsAotCompatible` so the trim and AOT analyzers run as part of the lint gate
(**NFR-8**, §9). Test projects override `GenerateDocumentationFile` and the CS1591 suppression locally, as they do
today. `Directory.Packages.props` turns on Central Package Management, so a `PackageReference` never carries a `Version`
(rule 12.3), and additionally sets `CentralPackageTransitivePinningEnabled`, so a transitive package whose version
matters — the `System.Diagnostics.DiagnosticSource` that §2.4 shows the logging facade drags in — is pinned in the
same file rather than floating with whatever a direct dependency asks for. `global.json` pins the SDK with
`rollForward: latestFeature` as built, which is why the 10.0.401 SDK used to verify this document satisfies the
10.0.100 pin; §9.3 moves it to `latestPatch` on the feature band the team actually builds with.

**`InternalsVisibleTo` goes to test assemblies only.** Implementation helpers are `internal`
(`docs/styleguide/csharp/10-api-design.md` rule 10.1) and each library grants `InternalsVisibleTo` to its own test
project. The as-built `Dexpace.Sdk.Core` also grants it to `Dexpace.Sdk.Http.SystemNet`, and `CLAUDE.md` describes
that as the convention ("with `InternalsVisibleTo` for the test and transport assemblies"). This design overturns
it. A first-party adapter that reaches core internals proves nothing about the sufficiency of the public SPI, which
is the one thing a third-party adapter author depends on, and it turns an `internal` member into a cross-package
contract that the public-API snapshot (**NFR-4**) does not track. The grant is also unused: no `internal` member of
core is referenced from the transport at `d45e64b`. Adapters are built against core's public surface exactly as an
outside author would be; if an adapter needs something, that something becomes public and reviewed. Conversely the
transport's own grant names `Dexpace.Sdk.Http.SystemNet.Tests`, a project that does not exist yet — its tests live
in `Dexpace.Sdk.Core.Tests/Transport/`, which is why that test project references the transport.

**Test projects are partitioned by the package they test, and core's tests never see a transport.**
`Dexpace.Sdk.Core.Tests` referencing `Dexpace.Sdk.Http.SystemNet` makes it impossible to tell from the build graph
whether core's behaviour depends on the reference transport; **SEAM-2**'s conformance clause ("substitute a fake
implementation of each seam and confirm the core operates unchanged") is only structurally true when the core suite
compiles without any concrete adapter. So core's suite uses in-memory fakes of `IAsyncHttpClient`, `IHttpClient`
and `ISerde`; each adapter's suite runs `Dexpace.Sdk.Conformance` against that adapter plus its adapter-specific
tests; and the NativeAOT smoke consumer is a separate executable project because it must be *published*, not
merely tested. Test projects multi-target the same frameworks as the libraries so both the `net8.0` and `net10.0`
builds of every library are exercised; CI installs the .NET 8 runtime for that reason.

**Versioning is lockstep, not per package.** The Ruby port versioned each gem independently. This port overturns that
for the reason the pre-roadmap platform decisions gave — the packages are co-designed and share one SPI — and one
.NET-specific
reason: NuGet expresses a `ProjectReference` as a floor-only dependency (`>= x.y.z`), and .NET convention discourages
upper bounds, so independently versioned adapters would admit a core/adapter pair that was never built together. One
`VersionPrefix` in `Directory.Build.props` stamps every package, every assembly's `AssemblyInformationalVersion`
(which `SdkVersion` already reads for the User-Agent, **NFR-15**), and every package's dependency on core, so a
release is one coherent set. The residual risk is a consumer who pins core explicitly below an adapter's floor; NuGet
reports that as a package downgrade (NU1605, which SDK-style projects treat as an error by default — documented
behaviour, not exercised here), and otherwise version skew surfaces as a `MissingMethodException` or
`TypeLoadException` at the first call into a changed member rather than at load. Before 1.0 that is accepted; from 1.0
SemVer plus the public-API snapshot of **NFR-4** makes the floor sufficient. Ruby's registration-time version
assertion has no counterpart: it would be a runtime check duplicating what the package graph already enforces, and P11
applies.

**What retires from the Ruby layout.** Ruby's explicit-`require` tree existed because every Ruby autoloader is a
gem; .NET assemblies are loaded by the runtime on first type reference and there is nothing to choose. Ruby's
`sig/` tree of RBS signatures has no counterpart either: the C# signatures are the types.

**Correction (2026-09-28): roadmap decision D1 is approved, and the floor is `net10.0` only.** The lead ruled on
D1 on 2026-09-28 and accepted the roadmap's proposal. Every library and test project targets `net10.0` alone, set once
as `TargetFramework` in `Directory.Build.props`, so the per-project settings noted below are gone. Three statements
in this section assumed `net8.0;net10.0`, and all three are superseded: the `net8.0;net10.0` library target with its
one-edit rise after 2026-11-10 (the rise happened in phase 0, before any package was published); test projects
multi-targeting "so both the `net8.0` and `net10.0` builds of every library are exercised"; and CI installing the
.NET 8 runtime. The text above stands as written, and this paragraph is the correction. `global.json` now pins
`10.0.401` with `rollForward: latestPatch` (§9.3).

**As built (d45e64b):** built — diverges: libraries declare `TargetFramework` per project (Core and
`Http.SystemNet` are `net8.0` only; `Serialization.SystemTextJson` is `net8.0;net10.0`); only
`Serialization.SystemTextJson` sets `IsAotCompatible`; core grants `InternalsVisibleTo` to the transport; transport
tests live in `Dexpace.Sdk.Core.Tests`, which references the transport; `Microsoft.CodeAnalysis.PublicApiAnalyzers`
is versioned in `Directory.Packages.props` but referenced by no project; the `Microsoft.SourceLink.GitHub` 8.0.0
reference fails restore with NU1902 under `TreatWarningsAsErrors` (the .NET 8+ SDK embeds Source Link for GitHub
repositories itself, per its documentation, so the reference can be removed rather than bumped — phase 0 confirms by
inspecting the produced PDB).

### 2.4 Enforcing the zero-dependency invariant, and what "standard library" means in .NET

**SEAM-1** says core "MUST depend at runtime on nothing beyond its language's standard library plus a
compile-time-only logging facade" (the chapter text; appendix C's row reads "nothing beyond its language's standard
library plus a logging facade"), and **NFR-1** restates it: "The core module MUST depend only on the language standard
library, the language runtime, and a compile-time-only logging facade abstraction." Two things make the .NET reading
of that sentence non-obvious, and both are hazards a well-meaning contributor will walk into.

**The first hazard: .NET's "standard library" is a moving, TFM-dependent set, and package names do not tell you
which side of the line an assembly is on.** The platform is the `Microsoft.NETCore.App` shared framework; its
compile-time contract is the reference pack for the library's target framework. Membership grows between releases,
and several `System.*` assemblies exist *both* in-box and as NuGet packages with higher versions. Verified against
the `Microsoft.NETCore.App.Ref` 8.0.31 reference pack and the 10.0.12 and 9.0.18 runtimes on the authoring machine:
`System.Collections.Immutable`, `System.Text.Json`, `System.Diagnostics.DiagnosticSource` and
`System.Text.Encoding.CodePages` are in-box on `net8.0`; `System.IO.Pipelines` is **not** in the 8.0 reference pack
and is in-box only from .NET 9; `Microsoft.Extensions.Logging.Abstractions` and
`Microsoft.Extensions.DependencyInjection.Abstractions` are in no `Microsoft.NETCore.App` version at all (they ship
in the ASP.NET Core shared framework and as NuGet packages). The trap has two directions. Using `PipeReader` in a
`net8.0` build silently adds a package dependency that the `net10.0` build does not have. And adding a
`PackageReference` to an in-box assembly — the serde design that preceded PR #3 proposed `System.Text.Json` in
`Directory.Packages.props`; PR #3 did not adopt it, and this design rules it out — replaces the runtime's serviced copy
with a package copy,
so security fixes stop arriving through runtime servicing and start depending on this SDK re-releasing.

The rule this port adopts, stated precisely: **`Dexpace.Sdk.Core` may reference only (a) assemblies present in the
`Microsoft.NETCore.App` reference pack of its lowest target framework, (b) `Microsoft.Extensions.Logging.Abstractions`
under the terms below, and (c) build-only packages marked `PrivateAssets="all"`.** When the floor rises to
`net10.0`, (a) widens automatically — `System.IO.Pipelines` becomes platform — and no other edit is needed. Adapter
packages are held to the same rule plus **NFR-2**'s "at most one third-party library".

**The second hazard: there is no compile-time-only scope for a facade the core calls at run time.** The reference
compiles against SLF4J's API with a provided scope and lets the application supply the binding. NuGet has asset
controls (`ExcludeAssets="runtime"`, `PrivateAssets`), but a library whose code calls `ILogger.Log` at run time needs
the assembly present at run time; excluding it trades a declared dependency for a `FileNotFoundException` on the
first log call. So the facade is a runtime dependency, full stop. P2's test is unambiguous that it is not platform:
it is installed from a registry. What justifies it is P14 — `ILogger`/`ILoggerFactory` is the single logging shape
every .NET host, test framework and logging backend (Serilog, NLog, OpenTelemetry, Application Insights) already
speaks, and a core-owned facade would be the "parallel vocabulary" P14 forbids, forcing every consumer to write an
adapter to the thing they already have. PR #6, which added the reference, rested on the same conclusion (the pre-roadmap
platform decision that core may
take the standard abstraction packages, Porting Method). This is a
deviation, and it is recorded as one rather than argued away: §10 entry 1. Per P10,
it is *sanctioned* for "a logging facade" by SEAM-1's appendix-C wording and *judged* against NFR-1's
"compile-time-only".

The deviation's cost must be measured on the real graph, not the direct reference, and the measurement is the
second finding of this section. Verified from the `project.assets.json` of a `net8.0` build of core at `d45e64b` and
from the package's own `.nuspec`: `Microsoft.Extensions.Logging.Abstractions` 9.0.5 declares, for its `net8.0`
group, dependencies on `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.5 **and
`System.Diagnostics.DiagnosticSource` 9.0.5**. So on `net8.0` today core's runtime closure is three packages, and
the third one *replaces the in-box `DiagnosticSource`* — the assembly that defines the `ActivitySource` and `Meter`
core instruments with — by a package copy. The common statement that "`DiagnosticSource` is in the shared framework
on net8+" is true of the platform and false of this build. The terms under which (b) above is allowed are therefore:

- **Band-matched versions per target framework.** The facade's version tracks the runtime band of each TFM
  (8.0.x for `net8.0`, 10.0.x for `net10.0`), declared once per TFM in `Directory.Packages.props`, so the facade
  never asks for a `DiagnosticSource` newer than the framework it runs on. Whether the 8.0.x facade's `net8.0`
  group resolves to the in-box assembly with no package is a claim this document could not verify offline; the
  dependency audit below is what establishes it, and until it passes the transitive pin of §2.3 keeps the closure
  deterministic.
- **The dependency audit is a test, not a review note.** A test in the default test run reads core's
  `*.deps.json` for each target framework and asserts that its package set equals the allow-list exactly — the
  facade and whatever the facade's band-matched version requires, nothing else. That test *is* the **SEAM-1**
  dependency audit, as the Ruby port's empty-`runtime_dependencies` test was; a new `PackageReference` in core fails
  it before review sees it.
- **The facade is used as a facade.** Core logs through `ILogger` obtained from the caller (constructor parameter or
  `ILoggerFactory`), defaulting to `NullLogger.Instance`; it never references a logging *provider*, never configures
  logging, and never touches `Microsoft.Extensions.DependencyInjection` types even though they are in the closure.
  §8.1 carries the rest of the logging design.

`ActivitySource` and `Meter` do get a P2 pass: they are in-box on every supported framework, and choosing them is
choosing the platform. Configuration binding (`IConfiguration`, `IOptions<T>`) does not enter core at all; plain
options types live in core and the binding lives in `Dexpace.Sdk.Extensions.DependencyInjection` (§8.2), which is
where the options of PR #4 already put it. `CLAUDE.md` still says core "builds against the BCL only";
that sentence has been false since the logging facade was referenced and is corrected by this section, not by
relaxing the rule further.

**Single-instance guarantee.** The reference and any package manager with nested resolution must ask how two copies
of core could be loaded at once, because type-identity checks — the exception hierarchy of **XCUT-4**, the recovery
outcome variants of **RECOV-1**, the three-state variants of **SERDE-14** — break silently under duplication. On
.NET the answer is structural for the default case: NuGet resolves exactly one version of each package per
application graph, and the default `AssemblyLoadContext` loads one assembly per simple name, so two
`Dexpace.Sdk.Core` assemblies cannot both define `Dexpace.Sdk.Core.Errors.SdkException` in one ordinary process.
The residual risk is the one .NET adds: an application that isolates plugins in their own `AssemblyLoadContext`s
can load core once per context, and an `SdkException` thrown in one context is not catchable as the other context's
`SdkException`. That is the plugin host's isolation choice, not something a library can prevent; core documents
that its types are not designed to cross load-context boundaries, and the version-skew risk the Ruby section
discussed is covered by lockstep versioning (§2.3).

**Correction (2026-09-28): D1 approved, and the band is measured.** With the floor at `net10.0` only (§2.3's
correction), a single band remains. `Directory.Packages.props` pins the facade at 10.0.12. The closure is read from
core's `net10.0` `deps.json`, and it is exactly `Microsoft.Extensions.Logging.Abstractions` 10.0.12 plus
`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12. `System.Diagnostics.DiagnosticSource` is no longer a
package in the graph; it resolves to the in-box assembly. The 8.0.x `net8.0` band described above is retired. The
dependency audit is `scripts/ci/dependency-audit.cs`, a blocking CI step after `dotnet pack` rather than a test in
the default run, because its nuspec half needs the packed `.nupkg`. It asserts that audit, the band, and the single
`net10.0` dependency group. `CentralPackageTransitivePinningEnabled` is on.

**As built (d45e64b):** built — diverges: core's `net8.0` runtime closure is
`Microsoft.Extensions.Logging.Abstractions` 9.0.5 plus `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.5
and `System.Diagnostics.DiagnosticSource` 9.0.5, with no band-matching, no transitive pinning and no dependency-audit
test; `CLAUDE.md` still states the BCL-only rule.

---

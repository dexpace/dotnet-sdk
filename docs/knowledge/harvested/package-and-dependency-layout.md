# package-and-dependency-layout

## Rules
- The core stays small and dependency-free so a consumer's footprint is proportional to the features actually used (SEAM-1, NFR-1, NFR-2).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:29-29` · high · sha:8014d2ec2c9d</sub>
- Optional capabilities (each transport, codec, I/O backend and async bridge) are separately installable units depending on the core plus at most one third-party library.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:29-29` · high · sha:8014d2ec2c9d</sub>
- NFR-1 Concrete capabilities are supplied by separate adapter units that depend on the core, never the reverse.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:7-7` · high · sha:5f4684bf7123</sub>
- NFR-2 (SHOULD) Each optional capability (transport, serialization format, I/O backend, async bridge) SHOULD be a separately installable unit depending on the core plus at most one third-party library, so a consumer composes only the units it uses.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:8-8` · high · sha:5f4684bf7123</sub>
- NFR-10 (MUST) The SDK MUST declare a lowest-supported-runtime floor and target it for all general-purpose units.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29-29` · high · sha:5f4684bf7123</sub>
- NFR-10 A capability that genuinely requires a newer runtime MUST be isolated into its own unit that declares the higher floor explicitly, and that unit MUST NOT be a hard dependency of the general-purpose core.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29-29` · high · sha:5f4684bf7123</sub>
- NFR-10 No produced artifact may reference runtime/stdlib APIs absent on the floor it declares, so the emitted-artifact target and the visible-API level must agree, not just the compiler toolchain.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29-29` · high · sha:5f4684bf7123</sub>
- The core carries no concrete transport, codec or byte-stream dependency (SEAM-1, NFR-1), so a consumer adopts it without inheriting a NuGet version conflict and can swap any one concern independently.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:31-34` · high · sha:d7cea7b15cf3</sub>
- Package identifiers, assembly names, root namespaces and folder names are the same string following the scheme Dexpace.Sdk.<Area>[.<Impl>] (for example Dexpace.Sdk.Http.SystemNet is the package, assembly, namespace root and src/Dexpace.Sdk.Http.SystemNet/), so any one can be derived from any other, as styleguide rule 12.1 (namespaces mirror folders) requires.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:20-23` · high · sha:c9a7834ab04d</sub>
- A seam ships in the MVP together with at least one adapter that exercises the property the seam exists for; a second adapter over the same property is a later package.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:48-50` · high · sha:c9a7834ab04d</sub>
- The line between MVP and later packages is not how useful a package is but what would be unproven without it.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:69-70` · high · sha:c9a7834ab04d</sub>
- Inside a project the layout is by feature (Http/Request, Pipeline/Policies, Auth) rather than by technical layer, with namespaces mirroring folders (styleguide 12.1, 12.4), and one top-level type lives in one file named after it (rule 12.2).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:101-103` · high · sha:c9a7834ab04d</sub>
- A PackageReference to an in-box assembly (such as System.Text.Json) is ruled out because it replaces the runtime's serviced copy with a package copy, so security fixes stop arriving through runtime servicing and depend on this SDK re-releasing; the serde design preceding PR #3 proposed it and PR #3 did not adopt it.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:201-205` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Core may reference only (a) assemblies present in the Microsoft.NETCore.App reference pack of its lowest target framework, (b) Microsoft.Extensions.Logging.Abstractions under the stated terms, and (c) build-only packages marked PrivateAssets="all".
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:207-209` · high · sha:c9a7834ab04d</sub>
- Adapter packages are held to the same dependency rule as core plus NFR-2's at most one third-party library.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:210-211` · high · sha:c9a7834ab04d</sub>
- Before roadmap decision D1 (superseded 2026-09-28: the floor is now net10.0 only), the logging facade's version is band-matched per target framework (8.0.x for net8.0, 10.0.x for net10.0), declared once per TFM in Directory.Packages.props, so it never asks for a DiagnosticSource newer than the framework it runs on.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:237-240` · high · sha:c9a7834ab04d</sub>
- The dependency audit is a test, not a review note: it reads core's *.deps.json per target framework and asserts the package set equals the allow-list exactly (the facade plus what its band-matched version requires, nothing else), constituting the SEAM-1 dependency audit so a new PackageReference in core fails it before review.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:243-247` · high · sha:c9a7834ab04d</sub>
- Core uses the logging facade as a facade: it logs through an ILogger obtained from the caller (constructor parameter or ILoggerFactory) defaulting to NullLogger.Instance, never references a logging provider, never configures logging, and never touches Microsoft.Extensions.DependencyInjection types even though they are in the closure.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:248-251` · high · sha:c9a7834ab04d</sub>
- Configuration binding (IConfiguration, IOptions<T>) does not enter core; plain options types live in core and the binding lives in Dexpace.Sdk.Extensions.DependencyInjection (section 8.2), where PR #4 already put it.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:254-256` · high · sha:c9a7834ab04d</sub>
- Core documents that its types are not designed to cross load-context boundaries, and version-skew risk is covered by lockstep versioning (section 2.3).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:269-270` · high · sha:c9a7834ab04d</sub>
- Declare every package version once, centrally in Directory.Packages.props with ManagePackageVersionsCentrally set to true, and have leaf .csproj files name packages with no Version attribute (styleguide 12.3).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:60-78` · high · sha:a44b6f9eaba9</sub>

## Constraints
- NFR-1 (MUST) The core module MUST depend only on the language standard library, the runtime, and a compile-time-only logging facade.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:7-7` · high · sha:5f4684bf7123</sub>
- NFR-1 (MUST) The core module MUST NOT carry a runtime dependency on any concrete HTTP transport, serialization library, I/O implementation, or async framework.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:7-7` · high · sha:5f4684bf7123</sub>
- .NET's standard library is a moving, TFM-dependent set: the platform is the Microsoft.NETCore.App shared framework, its compile-time contract is the reference pack for the target framework, membership grows between releases, and several System.* assemblies exist both in-box and as higher-versioned NuGet packages, so package names do not show which side of the line an assembly is on.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:191-195` · high · sha:c9a7834ab04d</sub>
- Before roadmap decision D1 (superseded 2026-09-28: the floor is now net10.0 only), using a type such as PipeReader in a net8.0 build silently adds a package dependency that the net10.0 build does not have.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:200-201` · high · sha:c9a7834ab04d</sub>
- There is no compile-time-only scope for a logging facade the core calls at run time: excluding the assembly (ExcludeAssets="runtime") trades a declared dependency for a FileNotFoundException on the first log call, so the facade is a runtime dependency, unlike the reference's SLF4J provided scope.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:213-217` · high · sha:c9a7834ab04d</sub>
- Two copies of Dexpace.Sdk.Core cannot both define SdkException in one ordinary process because NuGet resolves one version per package per application graph and the default AssemblyLoadContext loads one assembly per simple name.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:260-266` · high · sha:c9a7834ab04d</sub>
- An application isolating plugins in separate AssemblyLoadContexts can load core once per context, and an SdkException thrown in one context is not catchable as another context's SdkException; this is the plugin host's isolation choice that a library cannot prevent.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:266-269` · high · sha:c9a7834ab04d</sub>

## Conclusions
- NFR-10 rationale is that compiling against a newer stdlib while emitting artifacts declared for an older runtime links on the build machine but fails at call time.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29-29` · high · sha:5f4684bf7123</sub>
- Microsoft.Extensions.Logging.Abstractions' ILogger is the one case in core where the ecosystem convergence point is a NuGet package, so P14 and P2 pull in opposite directions, and §2.4 resolves it and records it as a deviation rather than pretending the package is platform.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:62-65` · high · sha:8e66b82361d2</sub>
- Core may take the standard abstraction packages, a cross-cutting platform decision argued in §2.4.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:72-72` · high · sha:8e66b82361d2</sub>
- The "no dependency" value proposition cannot be literal on .NET because ILogger lives in a NuGet package rather than the shared framework, so the port deliberately takes that one package and records it (§2.4).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:33-35` · high · sha:d7cea7b15cf3</sub>
- The port is a single repository with a single solution (Dexpace.Sdk.sln), one project per published NuGet package under src/ and one test project per package under tests/, the shape dotnet/extensions and the Azure SDK for .NET use (one lean core plus independently installable satellites, shared CI, one central build configuration).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:3-6` · high · sha:c9a7834ab04d</sub>
- Package granularity is one cohesive Dexpace.Sdk.Core toolkit holding models, bodies, the transport SPI, errors, the serde abstraction, pipeline and policies, context, instrumentation, auth, SSE, pagination and webhooks, plus separate packages for each concrete implementation and for host integration (the granularity PR #3 set).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:7-11` · high · sha:c9a7834ab04d</sub>
- A NuGet package per subsystem (.Auth, .Sse, .Webhooks and so on) is rejected because the subsystems are co-designed and share their dependencies, and because NFR-2 concerns optional capabilities that carry a third-party dependency, which none of those subsystems does, so splitting would buy release-cadence independence at the price of eight co-versioned packages and no dependency saving.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:11-16` · high · sha:c9a7834ab04d</sub>
- The one-repository-per-package model is rejected, as the Ruby port rejected it, because adapters track the core SPI closely enough during the pre-1.0 period that cross-repository CI would cost more than it saves.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:16-18` · high · sha:c9a7834ab04d</sub>
- One SystemNet adapter covers SocketsHttpHandler, WinHttpHandler, the browser/WASM handler and the mobile native handlers because HttpClient accepts any HttpMessageHandler, and it composes under IHttpClientFactory.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:37-37` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Serialization.SystemTextJson is the reference wire codec over source-generated JsonSerializerContext metadata and ships as a separate package per principle P3 even though System.Text.Json costs nothing to embed.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:38-38` · high · sha:c9a7834ab04d</sub>
- Counting the Microsoft.Extensions hosting family as one library in NFR-2's sense is a judgement recorded as deviation ledger section 10 entry 2.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:39-39` · high · sha:c9a7834ab04d</sub>
- The Ruby MVP's two async packages (thread-pool driver and reactor-native transport) have no .NET counterpart because the pivot is the runtime's Task, so there is nothing to drive, and SystemNet's SendAsync is natively asynchronous (no thread held across the network wait, cancellation reaching the socket via CancellationToken, HTTP/2 multiplexing on by default), so the one reference transport exercises every property the async seam exists for.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:42-48` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Reactive (System.Reactive), bridging pagination and SSE IAsyncEnumerable<T> to IObservable<T>, is a deferred later package that is an adapter rather than a seam.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:57-59` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Serialization.NewtonsoftJson (Newtonsoft.Json) is a deferred later package because a large installed base still models DTOs with its attributes.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:59-60` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Serialization.Xml over the shared framework's System.Xml is deferred and built only if a real consumer needs a second format to prove SEAM-19's undefaulted media type against.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:60-61` · high · sha:c9a7834ab04d</sub>
- No Dexpace.Sdk.Instrumentation.OpenTelemetry package is planned because the OpenTelemetry SDK subscribes directly to an ActivitySource and a Meter by name and both types ship with the runtime, so the seam such a package would implement has already retired (P2).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:62-64` · high · sha:c9a7834ab04d</sub>
- No second first-party transport over a different HTTP library is planned because the .NET ecosystem has converged on HttpMessageHandler as the connection-layer extension point, alternative stacks plug in under Dexpace.Sdk.Http.SystemNet, and a second adapter would exercise no property the first does not.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:64-67` · high · sha:c9a7834ab04d</sub>
- Ruby's explicit-require tree and its sig/ RBS signature tree have no .NET counterpart because assemblies are loaded by the runtime on first type reference and the C# signatures are the types.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:161-163` · high · sha:c9a7834ab04d</sub>
- Microsoft.Extensions.Logging.Abstractions is accepted as a core dependency under P14 because ILogger/ILoggerFactory is the single logging shape every .NET host, test framework and backend (Serilog, NLog, OpenTelemetry, Application Insights) speaks, and a core-owned facade would be the parallel vocabulary P14 forbids; PR #6 rested on the same conclusion.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:217-223` · high · sha:c9a7834ab04d</sub>
- The logging facade is not platform by P2's test (it is installed from a registry); it is a recorded deviation (section 10 entry 1), sanctioned for "a logging facade" by SEAM-1's appendix-C wording and judged against NFR-1's "compile-time-only" per P10.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:217-226` · high · sha:c9a7834ab04d</sub>
- ActivitySource and Meter pass P2 because they are in-box on every supported framework, so choosing them is choosing the platform.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:253-254` · high · sha:c9a7834ab04d</sub>
- The CLAUDE.md sentence that core "builds against the BCL only" has been false since the logging facade was referenced and is corrected by section 2.4 rather than by relaxing the rule further.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:256-258` · high · sha:c9a7834ab04d</sub>
- The dependency audit is scripts/ci/dependency-audit.cs, a blocking CI step after dotnet pack rather than a test in the default run, because its nuspec half needs the packed .nupkg; it asserts the allow-list, the band and the single net10.0 dependency group, and the 8.0.x net8.0 band is retired.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:276-279` · high · sha:c9a7834ab04d</sub>
- Microsoft.Extensions.Logging.Abstractions is installed from NuGet and not shipped with the runtime, so it does not get P2's pass and is instead justified by P14, because every .NET host, OpenTelemetry's log bridge and every major sink (Serilog, NLog) consume ILogger and a bespoke SDK logging interface would need an adapter for each.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:25-28` · high · sha:ddf8f695ff61</sub>
- The ILogger package is a runtime dependency whose assembly must be present when core loads, which deviates from the letter of SEAM-1 ("nothing beyond its language's standard library plus a logging facade") and NFR-1 ("a compile-time-only logging facade"), and is recorded as section 10 entry 1.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:28-31` · high · sha:ddf8f695ff61</sub>
- Central Package Management is chosen because a version repeated in every .csproj diverges (for example System.Text.Json at 9.0.0 in one project and 9.0.2 in another, with the runtime loading whichever wins), and it pairs with the thin-.csproj rule so the leaf says what it depends on while the central file says which version.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:62-64` · high · sha:a44b6f9eaba9</sub>

## Reference
- NFR-1 conformance is that the core artifact's published dependency metadata lists zero runtime dependencies beyond the stdlib, with the logging facade appearing compile-scope only.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:7-7` · high · sha:5f4684bf7123</sub>
- NFR-2 conformance is asserting that each adapter's dependency metadata lists the core plus at most one external library, and that the core is not required to pull in any adapter.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:8-8` · high · sha:5f4684bf7123</sub>
- NFR-10 conformance is running each unit's artifact on its declared minimum runtime with no missing-symbol failures, statically scanning each artifact for references to symbols newer than its floor (none), and confirming higher-floor units are optional.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29-29` · high · sha:5f4684bf7123</sub>
- On .NET, an assembly in the Microsoft.NETCore.App shared framework (the reference pack of the library's lowest target framework) counts as the platform, while anything arriving as a NuGet PackageReference counts as a dependency regardless of a System.* or Microsoft.* name.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:14-19` · high · sha:8e66b82361d2</sub>
- NFR-2 asks that each optional capability SHOULD be a separately installable unit depending on the core plus at most one third-party library.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:6-7` · high · sha:c9a7834ab04d</sub>
- The MVP package list is Dexpace.Sdk.Core, Dexpace.Sdk.Http.SystemNet, Dexpace.Sdk.Serialization.SystemTextJson, Dexpace.Sdk.Extensions.DependencyInjection and Dexpace.Sdk.Conformance.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:34-40` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Core has Microsoft.Extensions.Logging.Abstractions as its only runtime NuGet dependency (the recorded deviation of section 2.4) and contains the domain model, the byte-stream contract on Stream, bodies, both transport interfaces and the Task pivot, the serde abstraction, the operation projection, pipeline and policies, retry/redirect/auth, pagination, SSE and webhooks, and options and diagnostics.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:36-36` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Http.SystemNet depends on Dexpace.Sdk.Core (System.Net.Http is in the shared framework) and is the reference transport implementing both IAsyncHttpClient and IHttpClient over System.Net.Http.HttpClient.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:37-37` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Extensions.DependencyInjection depends on Dexpace.Sdk.Core and the Microsoft.Extensions hosting family (DependencyInjection.Abstractions, Http, Options, Options.ConfigurationExtensions) and provides AddDexpaceClient(...), options binding and validation with ValidateOnStart, IHttpClientFactory wiring, and the single-registration check that replaces discovery.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:39-39` · high · sha:c9a7834ab04d</sub>
- As built at d45e64b, Core, Http.SystemNet and Serialization.SystemTextJson exist while Extensions.DependencyInjection and Conformance are not built.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:52-53` · high · sha:c9a7834ab04d</sub>
- The repository layout has Dexpace.Sdk.sln, global.json, Directory.Build.props, Directory.Packages.props, .editorconfig, nuget.config (a single, cleared package source), src/ with the five package projects, tests/ with a test project per package plus Dexpace.Sdk.AotSmoke, and docs/.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:74-99` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.Core folders are Http/{Common,Request,Response}, Client, Errors, Serialization, Pipeline/{,Policies}, Auth, Pagination, Configuration, Diagnostics and Internal, with IO, Operations, ServerSentEvents and Webhooks added as they land.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:83-87` · high · sha:c9a7834ab04d</sub>
- Dexpace.Sdk.AotSmoke is a NativeAOT-published consumer project that serves as NFR-9's guard.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:97-97` · high · sha:c9a7834ab04d</sub>
- The as-built tree follows the feature layout except where several small exception types share a file (Errors/TransportExceptions.cs), which is recorded as a styleguide finding rather than a design question.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:103-105` · high · sha:c9a7834ab04d</sub>
- As built at d45e64b, libraries declared TargetFramework per project (Core and Http.SystemNet net8.0 only; Serialization.SystemTextJson net8.0;net10.0), only Serialization.SystemTextJson set IsAotCompatible, core granted InternalsVisibleTo to the transport, transport tests lived in Dexpace.Sdk.Core.Tests which referenced the transport, and Microsoft.CodeAnalysis.PublicApiAnalyzers was versioned but referenced by no project.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:174-178` · high · sha:c9a7834ab04d</sub>
- SEAM-1 says core MUST depend at runtime on nothing beyond its language's standard library plus a compile-time-only logging facade (appendix C wording: nothing beyond its language's standard library plus a logging facade), and NFR-1 restates that the core module MUST depend only on the language standard library, the language runtime, and a compile-time-only logging facade abstraction.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:185-189` · high · sha:c9a7834ab04d</sub>
- Verified against Microsoft.NETCore.App.Ref 8.0.31 and the 10.0.12 and 9.0.18 runtimes, System.Collections.Immutable, System.Text.Json, System.Diagnostics.DiagnosticSource and System.Text.Encoding.CodePages are in-box on net8.0.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:195-197` · high · sha:c9a7834ab04d</sub>
- System.IO.Pipelines is not in the net8.0 reference pack and is in-box only from .NET 9.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:197-198` · high · sha:c9a7834ab04d</sub>
- Microsoft.Extensions.Logging.Abstractions and Microsoft.Extensions.DependencyInjection.Abstractions are in no Microsoft.NETCore.App version (they ship in the ASP.NET Core shared framework and as NuGet packages).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:198-200` · high · sha:c9a7834ab04d</sub>
- When the floor rises to net10.0, clause (a) of the core dependency rule widens automatically (System.IO.Pipelines becomes platform) and no other edit is needed.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:209-210` · high · sha:c9a7834ab04d</sub>
- Measured on a net8.0 build of core at d45e64b, Microsoft.Extensions.Logging.Abstractions 9.0.5 depends for net8.0 on Microsoft.Extensions.DependencyInjection.Abstractions 9.0.5 and System.Diagnostics.DiagnosticSource 9.0.5, so core's runtime closure there was three packages and the third replaced the in-box DiagnosticSource (which defines ActivitySource and Meter) with a package copy.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:228-235` · high · sha:c9a7834ab04d</sub>
- Type-identity checks such as the exception hierarchy of XCUT-4, the recovery outcome variants of RECOV-1 and the three-state variants of SERDE-14 break silently under duplicate loading of core.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:260-264` · high · sha:c9a7834ab04d</sub>
- After D1 (2026-09-28), Directory.Packages.props pins the logging facade at 10.0.12, and core's net10.0 deps.json closure is exactly Microsoft.Extensions.Logging.Abstractions 10.0.12 plus Microsoft.Extensions.DependencyInjection.Abstractions 10.0.12, with System.Diagnostics.DiagnosticSource resolving to the in-box assembly.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:272-276` · high · sha:c9a7834ab04d</sub>
- As built at d45e64b, core's net8.0 runtime closure was Logging.Abstractions 9.0.5, DependencyInjection.Abstractions 9.0.5 and DiagnosticSource 9.0.5 with no band-matching, no transitive pinning and no dependency-audit test.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:281-284` · high · sha:c9a7834ab04d</sub>
- As built, the logging facade's 9.0.5 package closure replaces the in-box System.Diagnostics.DiagnosticSource assembly on net8.0 with a package copy; section 2.4 measures it and band-matching the logging package to the target removes it.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:23-25` · medium · sha:ddf8f695ff61</sub>
- The as-built Dexpace.Sdk.Core.csproj already references Microsoft.Extensions.Logging.Abstractions pinned at 9.0.5 (which supports net8.0), so CLAUDE.md's "BCL-only" rule has been stale since PR #6 added the reference, a correction anticipated by the pre-roadmap decision that core may take the standard abstraction packages.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:30-33` · high · sha:ddf8f695ff61</sub>
- A Version attribute on a leaf PackageReference is a review finding under rule 12.3.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:78-78` · high · sha:a44b6f9eaba9</sub>

## Conflicts

## Superseded


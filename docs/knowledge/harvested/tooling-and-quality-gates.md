# tooling-and-quality-gates

## Rules
- NFR-5 (SHOULD) The build SHOULD enforce a minimum aggregate line-coverage floor (currently 80%) across the library units, wired into the default build lifecycle.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:17-17` · high · sha:5f4684bf7123</sub>
- NFR-6 (SHOULD) Compiler warnings SHOULD be treated as errors across every unit, including deprecations.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:18-18` · high · sha:5f4684bf7123</sub>
- NFR-7 (SHOULD) The build SHOULD run automated style/lint and static-analysis checks with findings treated as fatal, so that a nonzero issue budget fails the build.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:19-19` · high · sha:5f4684bf7123</sub>
- NFR-7 Where an analyzer cannot run on a unit's toolchain, disabling it SHOULD be a narrowly-scoped, documented exception with explicit re-enable conditions, not a silent global relaxation.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:19-19` · high · sha:5f4684bf7123</sub>
- NFR-17 (MUST) The quality gates backing the NFRs (compatibility snapshot, coverage floor, warnings-as-errors, lint/static-analysis, shrink-survival where applicable, and runtime-floor checks) MUST be enforced automatically and be blocking, failing the standard build/CI rather than being advisory.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:20-20` · high · sha:5f4684bf7123</sub>
- NFR-8 (MUST) In target ecosystems that support whole-program dead-code elimination, tree-shaking or minification, the SDK MUST ship the keep/retain configuration a downstream shrinker needs so its reflectively-reached and runtime-wired surface survives shrinking.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:24-24` · high · sha:5f4684bf7123</sub>
- NFR-8 The keep configuration MUST cover the runtime-wired SPI seams (I/O provider, transport clients, serde) and the immutable models and reflectively-bound types (request/response models, the Tristate type, and the reflective metadata serializers read).
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:24-24` · high · sha:5f4684bf7123</sub>
- NFR-9 (SHOULD) The shipped shrinker keep-configuration SHOULD be guarded by an automated regression check, wired into the default build, that shrinks a real consumer using only the shipped rules and runs it end-to-end against a live round-trip, failing the build if any runtime-wired or reflectively-reached surface is stripped.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:25-25` · high · sha:5f4684bf7123</sub>
- NFR-9 The keep-configuration guard SHOULD also assert that every shipped rule file is present.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:25-25` · high · sha:5f4684bf7123</sub>
- NFR-12 (SHOULD) Build artifacts SHOULD be reproducible, so identical source inputs yield byte-for-byte identical output artifacts (normalized/stripped embedded timestamps, deterministic entry ordering).
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:37-37` · high · sha:5f4684bf7123</sub>
- NFR-13 (SHOULD) Every source file SHOULD carry the project's license/SPDX header block.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:38-38` · high · sha:5f4684bf7123</sub>
- NFR-14 (SHOULD) Dependency versions, plugin/tool versions and project coordinates SHOULD live in a single source of truth rather than being restated per unit, so a bump is ideally a one-line edit applying uniformly.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:39-39` · high · sha:5f4684bf7123</sub>
- NFR-14 A port SHOULD avoid restating coordinates in per-unit build config.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:39-39` · high · sha:5f4684bf7123</sub>
- NFR-15 (SHOULD) Published artifacts SHOULD embed self-identifying version metadata the SDK can resolve at runtime, so runtime-emitted identifiers (e.g. a User-Agent) report the real version rather than an "unknown" placeholder.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:40-40` · high · sha:5f4684bf7123</sub>
- NFR-16 (SHOULD) Published artifacts SHOULD be cryptographically signed for provenance, with signing enforced on the release/CI path and made gracefully optional in local builds lacking signing keys.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:41-41` · high · sha:5f4684bf7123</sub>
- The .NET form of NFR-8's "keep configuration" is annotation rather than a rules file, namely IsTrimmable/IsAotCompatible on every library, the trim/AOT/single-file analyzers promoted to errors, and [DynamicallyAccessedMembers] where reflection is unavoidable (§9).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:81-84` · high · sha:d7cea7b15cf3</sub>
- A published NativeAOT smoke consumer serves as NFR-9's regression guard.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:83-84` · high · sha:d7cea7b15cf3</sub>
- Central build configuration is the single source of truth (NFR-14), and Directory.Build.props carries the compiler switches every project shares (LangVersion latest, Nullable, TreatWarningsAsErrors, AnalysisLevel latest-recommended, EnforceCodeStyleInBuild, GenerateDocumentationFile, Deterministic) and the package metadata (VersionPrefix, VersionSuffix, authors, license expression, repository URLs).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:107-110` · high · sha:c9a7834ab04d</sub>
- Target frameworks are set in Directory.Build.props rather than in each .csproj, and every library sets IsTrimmable and IsAotCompatible so the trim and AOT analyzers run as part of the lint gate (NFR-8).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:110-116` · high · sha:c9a7834ab04d</sub>
- Directory.Packages.props turns on Central Package Management so a PackageReference never carries a Version (styleguide rule 12.3), and sets CentralPackageTransitivePinningEnabled so a transitive package whose version matters (such as System.Diagnostics.DiagnosticSource dragged in by the logging facade) is pinned in the same file.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:117-120` · high · sha:c9a7834ab04d</sub>
- The ConcurrentDictionary.TryRemove(KeyValuePair<TKey,TValue>) overload is added to the banned-API list (design section 9) for any other value type.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:393-394` · high · sha:1608fcd4b329</sub>
- ConditionalWeakTable and WeakReference<T> are banned in core by Microsoft.CodeAnalysis.BannedApiAnalyzers (design section 9), because a weakly held context could be collected mid-call along with an unread body pinning a connection (CTX-19 prohibits weak references).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:402-405` · high · sha:1608fcd4b329</sub>
- The NativeAOT smoke test (section 9.2) is what keeps the Tristate factory's IL2067 suppression honest (NFR-9).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:359-359` · high · sha:68af5c6bf0ea</sub>
- NFR-6 warnings-as-errors is implemented as TreatWarningsAsErrors in Directory.Build.props, covering compiler, analyzer, deprecation (CS0618) and NuGet audit warnings.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:12-12` · high · sha:27bc3ba15ac4</sub>
- NFR-7 static-analysis findings are made fatal with AnalysisLevel=latest-recommended, EnforceCodeStyleInBuild and .editorconfig severities, plus dotnet format --verify-no-changes.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:13-13` · high · sha:27bc3ba15ac4</sub>
- NFR-3 explicit public API is implemented with GenerateDocumentationFile plus CS1591 as error, internal by default, and Microsoft.CodeAnalysis.PublicApiAnalyzers.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:14-14` · high · sha:27bc3ba15ac4</sub>
- NFR-4 API-surface snapshot is implemented with PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt per project (RS0016, RS0017) plus EnablePackageValidation with PackageValidationBaselineVersion for binary compatibility.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:15-15` · high · sha:27bc3ba15ac4</sub>
- NFR-5 aggregate line-coverage floor is a coverlet threshold (Threshold=80, ThresholdType=line, ThresholdStat=total) on the library assemblies.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:16-16` · high · sha:27bc3ba15ac4</sub>
- The zero-dependency audit (SEAM-1, NFR-1, NFR-2) is a nuspec dependency assertion per package plus architecture tests over assembly references.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:17-17` · high · sha:27bc3ba15ac4</sub>
- Shrink-survival (NFR-8, NFR-9) is gated by IsTrimmable/IsAotCompatible on every library (trim, single-file and AOT analyzers as errors) plus a NativeAOT-published smoke consumer run against a loopback server.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:18-18` · high · sha:27bc3ba15ac4</sub>
- The NFR-10 runtime floor is gated by per-TFM reference packs, tests executed on each TFM's runtime, and higher-floor capability isolated by TFM or package (later superseded by the net10.0-only ruling D1).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:19-19` · high · sha:27bc3ba15ac4</sub>
- NFR-11 concurrency-model agnosticism is gated by a test scanning PublicAPI.Shipped.txt for types outside System.*, Microsoft.Extensions.Logging.* and Dexpace.*.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:20-20` · high · sha:27bc3ba15ac4</sub>
- NFR-14 version single source is Directory.Packages.props (central package management) plus VersionPrefix/VersionSuffix in Directory.Build.props.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:21-21` · high · sha:27bc3ba15ac4</sub>
- NFR-12 reproducible artifacts use Deterministic, ContinuousIntegrationBuild in CI, SOURCE_DATE_EPOCH for the .nupkg container, and a pack-twice-and-compare job.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:22-22` · high · sha:27bc3ba15ac4</sub>
- NFR-13 license headers are enforced by file_header_template plus IDE0073 at error severity under EnforceCodeStyleInBuild.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:23-23` · high · sha:27bc3ba15ac4</sub>
- NFR-15 runtime version metadata reads AssemblyInformationalVersion through SdkVersion for the User-Agent, with fallback 0.0.0.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:24-24` · high · sha:27bc3ba15ac4</sub>
- NFR-16 signed publications use strong naming (SignAssembly, committed key) and NuGet author signing on the release job only.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:25-25` · high · sha:27bc3ba15ac4</sub>
- Dependency CVE scanning uses NuGet audit (NuGetAudit, on by default) under warnings-as-errors.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:26-26` · high · sha:27bc3ba15ac4</sub>
- Formatting and lock-file gates are dotnet format --verify-no-changes and RestorePackagesWithLockFile plus dotnet restore --locked-mode.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:27-27` · high · sha:27bc3ba15ac4</sub>
- Banned APIs are enforced with Microsoft.CodeAnalysis.BannedApiAnalyzers (RS0030) and a committed BannedSymbols.txt.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:28-28` · high · sha:27bc3ba15ac4</sub>
- SSE-37 serde independence of streaming and paging is gated by an architecture test over type references.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:29-29` · high · sha:27bc3ba15ac4</sub>
- SEAM-2 is gated by an architecture test asserting no Dexpace.Sdk.Core type references a Dexpace.Sdk.Http.* or Dexpace.Sdk.Serialization.* assembly.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:30-30` · high · sha:27bc3ba15ac4</sub>
- NFR-4 requires every public member to appear in PublicAPI.Shipped.txt or PublicAPI.Unshipped.txt, with RS0016 failing the build on an undeclared addition and RS0017 on a removal the file still lists, so additions show in the pull-request diff where a reviewer can refuse them.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:52-56` · high · sha:27bc3ba15ac4</sub>
- Regeneration of the public-API files is deliberate: the code fix writes the file and review decides whether to keep it.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:55-56` · high · sha:27bc3ba15ac4</sub>
- NFR-3's explicit surface is carried by GenerateDocumentationFile with CS1591 as an error, internal as default visibility with InternalsVisibleTo for test assemblies only, and the public-API files.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:66-69` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans Thread.Interrupt, and the flow-suppressing ExecutionContext.SuppressFlow and ThreadPool.UnsafeQueueUserWorkItem (the latter two outside the one background-launch helper of §5.4).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:73-75` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans Task.Result, Task.Wait and GetAwaiter().GetResult() outside the documented sync bridges of §3.3 and §5.3, citing styleguide 09-concurrency 9.1 whose own enforcement names VSTHRD002, an analyzer the repository does not reference.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:75-78` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans Uri.ToString() on any wire, log or equality path (§3.5).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:78-78` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans the value-comparing ConcurrentDictionary.TryRemove(KeyValuePair<,>) overload outside the context store's slot type, and ConditionalWeakTable/WeakReference<T> in core (§5.4).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:79-80` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans TypeNameHandling other than None and BinaryFormatter (§3.4).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:80-81` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans new HttpClient() outside the transport's owned-client factory (styleguide 13-resource-management 13.8).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:81-82` · high · sha:27bc3ba15ac4</sub>
- BannedSymbols.txt bans Console.Write* (styleguide csharp-aspnetcore 06-logging-and-observability 6.8).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:82-83` · high · sha:27bc3ba15ac4</sub>
- A justified exception to a banned API is a scoped #pragma warning disable RS0030 with a why-comment, which is the house rule for every waiver (styleguide 01-formatting-and-tooling 1.2).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:83-85` · high · sha:27bc3ba15ac4</sub>
- NFR-9's regression guard is a NativeAOT smoke consumer, a console project in tests/ referencing the packages, published with PublishAot=true and trim warnings as errors, whose Main performs a live round trip against an in-process loopback server and exits non-zero on any mismatch.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:103-107` · high · sha:27bc3ba15ac4</sub>
- The smoke consumer's round trip covers a JSON request through the pipeline with a source-generated context, a Tristate<T> PATCH body (§7.3), a paged walk and an SSE stream.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:105-107` · high · sha:27bc3ba15ac4</sub>
- NFR-9's "assert every shipped rule file is present" becomes asserting that the IsTrimmable/IsAotCompatible properties are set in each packable project.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:111-112` · high · sha:27bc3ba15ac4</sub>
- A packaging test reads each .nuspec produced by dotnet pack and asserts that Dexpace.Sdk.Core lists exactly Microsoft.Extensions.Logging.Abstractions (the recorded exception, §8.1 and §10 entry 1) and each adapter lists Dexpace.Sdk.Core plus at most one other package.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:114-118` · high · sha:27bc3ba15ac4</sub>
- Architecture tests use reflection over type references in a test (no NetArchTest dependency) to assert nothing in Dexpace.Sdk.Core references a transport or serializer assembly (SEAM-2), and that Dexpace.Sdk.Core.ServerSentEvents and the paging engine reference nothing in Dexpace.Sdk.Core.Serialization (SSE-37).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:119-123` · high · sha:27bc3ba15ac4</sub>
- TargetFrameworks is moved into Directory.Build.props, which fixes the target-framework inconsistency (styleguide 01-formatting-and-tooling 1.1).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:144-145` · high · sha:27bc3ba15ac4</sub>
- Roadmap constraint 2 holds adapters to the same dependency rule as core plus at most one third-party library, allowing the logging facade, and neither adapter lists a third-party library.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:167-169` · high · sha:27bc3ba15ac4</sub>
- scripts/ci/dependency-audit.cs accepts the logging facade in an adapter's nuspec and in no other extra dependency.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:169-170` · high · sha:27bc3ba15ac4</sub>
- NFR-11's mechanical form is a test over PublicAPI.Shipped.txt asserting every type named in a public signature is from System.*, Microsoft.Extensions.Logging.* or Dexpace.*, which also catches a transport's HttpRequestMessage escaping into a core signature.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:172-176` · high · sha:27bc3ba15ac4</sub>
- NFR-12's gate is a CI job that packs twice with SOURCE_DATE_EPOCH set to the commit time and compares digests.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:183-184` · high · sha:27bc3ba15ac4</sub>
- NFR-5's floor is 80% aggregate line coverage over the library assemblies, enforced by a coverlet threshold on the default test run, with the conformance kit, the smoke consumer and test support excluded.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:234-236` · high · sha:27bc3ba15ac4</sub>
- The CI is one blocking workflow per pull request on ubuntu-latest, windows-latest and macos-latest, each with the .NET 8 and .NET 10 runtimes installed and the SDK from global.json (the runtime list later corrected by D1 to the global.json SDK only).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:247-249` · high · sha:27bc3ba15ac4</sub>
- The CI steps are locked restore; build (warnings as errors, analyzers, IDE0073, RS0016/RS0017, RS0030); dotnet format --verify-no-changes; tests with the coverage threshold; dotnet pack with package validation and nuspec dependency assertions; and, on Linux only, the NativeAOT smoke job and the pack-twice reproducibility job.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:249-254` · high · sha:27bc3ba15ac4</sub>
- The vendored styleguide under docs/styleguide/ is binding for every .cs file in the repository, and it is only as real as its enforcement.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:279-280` · high · sha:27bc3ba15ac4</sub>
- For Dexpace.Sdk.Extensions.DependencyInjection, styleguide csharp-aspnetcore 01-host-and-configuration 1.3-1.5 (typed options, ValidateOnStart, accessor by lifetime) and 02-dependency-injection 2.3-2.6 (deliberate lifetimes, no captive dependencies, keyed services for multiple clients) are enforced by a test that builds a ServiceProvider with ValidateScopes and ValidateOnBuild on and resolves every registration.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:309-314` · high · sha:27bc3ba15ac4</sub>
- For the serializer, styleguide csharp-aspnetcore 05-serialization-and-validation 5.2 is enforced by SYSLIB1030-SYSLIB1039 source-generator diagnostics as errors plus the AOT analyzers, 5.3 by CA1869, and 5.6 (absent versus null) by section 7.3's Tristate<T> and its tests.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:314-318` · high · sha:27bc3ba15ac4</sub>
- For instrumentation, styleguide csharp-aspnetcore 06-logging-and-observability 6.1 is enforced by CA2254, 6.2 by CA1848 (satisfied by both the generator and LoggerMessage.Define, design section 8.1), and 6.7 by section 8.1's redaction tests.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:318-319` · high · sha:27bc3ba15ac4</sub>
- Styleguide csharp-aspnetcore 08-build-and-deployment 8.2 (trim/AOT warnings as errors, publish-and-run smoke test) is enforced by section 9.2's gate verbatim, and 8.7/8.8 (reproducible, locked, gated) by section 9.2 and the CI matrix.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:320-321` · high · sha:27bc3ba15ac4</sub>
- CA2007 is set to warning (an error under TreatWarningsAsErrors) for everything under src/, and to none for tests, repository tools and the AOT smoke consumer via the [{tests,tools,.claude}/**/*.cs] section in .editorconfig, which is the split styleguide 9.4 itself makes (enabled in libraries, suppressed in app and host projects).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:346-350` · high · sha:27bc3ba15ac4</sub>
- Library code satisfies CA2007 on await using with await using var x = y.ConfigureAwait(false) and on await foreach with .ConfigureAwait(false) on the sequence, accepting the ergonomic cost.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:350-352` · high · sha:27bc3ba15ac4</sub>
- Formatting is enforced as a build artifact through one .editorconfig, one dotnet format and one analyzer baseline applied identically on every machine and in CI.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:3-3` · high · sha:2598baa9074f</sub>
- Build configuration (target framework, language version, nullable, analyzer gates) is centralized in a single Directory.Build.props at the repository root, and no .csproj restates those properties.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:24-24` · high · sha:2598baa9074f</sub>
- Centralize build configuration in Directory.Build.props and keep .csproj files thin, carrying only their own PackageReferences and ProjectReferences.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:28-32` · high · sha:2598baa9074f</sub>
- A TargetFramework or Nullable element inside a leaf .csproj is a review finding.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:43-43` · high · sha:2598baa9074f</sub>
- Treat every warning as an error by setting TreatWarningsAsErrors to true.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:45-49` · high · sha:2598baa9074f</sub>
- Suppress a warning only with a narrow, justified #pragma warning disable CSxxxx plus a why-comment around the single offending line, never with a project-wide NoWarn list.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:49-49` · high · sha:2598baa9074f</sub>
- NoWarn entries are rejected in review absent a recorded reason.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:57-57` · high · sha:2598baa9074f</sub>
- Run the Roslyn code-quality (CA) and code-style (IDE) analyzers at build by setting AnalysisLevel to latest-Recommended and EnforceCodeStyleInBuild to true.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:59-63` · high · sha:2598baa9074f</sub>
- Analyzer rule severities are tuned in .editorconfig, not by weakening the AnalysisLevel.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:63-63` · high · sha:2598baa9074f</sub>
- Set Nullable to enable solution-wide and never weaken it per file; a #nullable disable directive is banned and is a review finding.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:74-84` · high · sha:2598baa9074f</sub>
- Format with .editorconfig plus dotnet format using Allman braces, four spaces with no tabs, one statement and one declaration per line, and no more than one blank line between members.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:86-89` · high · sha:2598baa9074f</sub>
- Run dotnet format --verify-no-changes in pre-commit and CI so layout is a gate.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:90-104` · high · sha:2598baa9074f</sub>
- Use file-scoped namespaces (namespace X;), one namespace per file.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:90-90` · high · sha:2598baa9074f</sub>
- Place using directives outside the namespace, with System.* first and the rest alphabetical.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:90-90` · high · sha:2598baa9074f</sub>
- Pin the SDK with a global.json, set Deterministic to true, and restore with dotnet restore --locked-mode against a committed packages.lock.json.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:106-116` · high · sha:2598baa9074f</sub>
- The language version is named explicitly (14.0), never latest, so an upgrade is a reviewed diff.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:110-110` · high · sha:2598baa9074f</sub>
- Cap method length at 70 lines via an analyzer rule that fails the build.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:118-122` · high · sha:2598baa9074f</sub>
- Enforce the method-length cap with a Roslyn analyzer (Roslynator RCS1213-class or csharp-extensions method-length) rather than relying on review to count lines.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:122-122` · high · sha:2598baa9074f</sub>
- Configure Meziantou.Analyzer MA0051 (or equivalent) with severity error and maximum_lines = 70.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:124-130` · high · sha:2598baa9074f</sub>
- The analyzer baseline (AnalysisLevel latest-Recommended, TreatWarningsAsErrors true) and the formatter are final, and formatting is a non-discussion.
  <sub>styleguide · `docs/styleguide/csharp/README.md:13-13` · high · sha:1e6ba36fc337</sub>
- Zero technical debt: what exists must meet the design goals, and the work is done right the first time because debt never gets paid.
  <sub>styleguide · `docs/styleguide/csharp/README.md:70-70` · high · sha:1e6ba36fc337</sub>
- Style changes or migrations away from deprecated patterns are applied at assembly/namespace level or larger, and two styles are never mixed within one project.
  <sub>styleguide · `docs/styleguide/csharp/README.md:93-93` · high · sha:1e6ba36fc337</sub>

## Constraints
- NFR-8 does not apply in ecosystems without a whole-program shrinking build step.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:24-24` · high · sha:5f4684bf7123</sub>
- An open-generic [JsonConverter(typeof(TristateConverter<>))] is not supported by the System.Text.Json source generator (verified: SYSLIB1220 at build and NotSupportedException at run time), so a factory must close the generic at run time.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:350-352` · high · sha:68af5c6bf0ea</sub>
- The conventional MakeGenericType plus Activator.CreateInstance converter factory raises IL3050 (verified), and under NativeAOT a value-type instantiation not seen statically cannot be created.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:353-354` · high · sha:68af5c6bf0ea</sub>
- Before roadmap decision D1 (superseded 2026-09-28: the floor is now net10.0 only), System.IO.Pipelines, System.Net.ServerSentEvents and System.Linq.AsyncEnumerable are in the shared framework on .NET 10 and absent from the .NET 8 reference pack (verified), so reaching for one on the net8.0 target silently acquires a NuGet dependency, which the nuspec assertion catches per target framework group.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:123-128` · high · sha:27bc3ba15ac4</sub>
- Before roadmap decision D1 (superseded 2026-09-28: the floor is now net10.0 only), the NFR-10 trap reopens through packages whose net8.0 asset differs from their net10.0 one and through #if NET10_0_OR_GREATER sections, both of which only running the tests on each runtime catches.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:130-135` · high · sha:27bc3ba15ac4</sub>
- A class library under latest-recommended does not raise CA1031 (bare catch (Exception)) or CA2000 (undisposed MemoryStream), so the styleguide rules that depend on them need explicit severities before they are gates (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:287-289` · high · sha:27bc3ba15ac4</sub>
- With Nullable enabled, a single #nullable disable file lets null flow untyped into code that trusts annotations, with failures surfacing far from the disabled file.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:77-77` · medium · sha:2598baa9074f</sub>

## Conclusions
- NFR-17 rationale is that a quality bar only holds if mechanically enforced, and its conformance is introducing a violation for each gate to confirm the ordinary build fails and that no gate is report-only.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:20-20` · high · sha:5f4684bf7123</sub>
- The modern multi-target, trim- and AOT-safe decision originally targeted libraries on net8.0;net10.0, and has been superseded by roadmap decision D1's net10.0 only (§2.3, §9.2).
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:73-74` · high · sha:8e66b82361d2</sub>
- The whole-program-shrinker constraint HOLDS on .NET literally, unlike Ruby, since ILLink trimming and NativeAOT both remove code they cannot see referenced and both are mainstream deployment modes (containers, serverless, mobile), so NFR-8 applies as written.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:79-82` · high · sha:d7cea7b15cf3</sub>
- The pre-D1 design had every library target net8.0;net10.0 and test projects multi-target the same frameworks so both builds of each library are exercised; .NET 8 leaves support on 2026-11-10, after which the floor was to rise to net10.0 in one edit.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:110-114` · high · sha:c9a7834ab04d</sub>
- The original design pinned the SDK in global.json with rollForward latestFeature (10.0.100 pin, satisfied by the 10.0.401 SDK), and section 9.3 moves it to latestPatch on the feature band the team actually builds with.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:120-122` · high · sha:c9a7834ab04d</sub>
- Roadmap decision D1 (approved 2026-09-28) sets every library and test project to target net10.0 alone, declared once as TargetFramework in Directory.Build.props, superseding the net8.0;net10.0 library target and its one-edit rise after 2026-11-10, the multi-targeted test projects, and CI installing the .NET 8 runtime.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:165-171` · high · sha:c9a7834ab04d</sub>
- The Microsoft.SourceLink.GitHub 8.0.0 reference fails restore with NU1902 under TreatWarningsAsErrors, and because the .NET 8+ SDK embeds Source Link for GitHub repositories itself the reference can be removed rather than bumped (phase 0 confirms by inspecting the produced PDB).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:178-181` · medium · sha:c9a7834ab04d</sub>
- Every gate the reference build enforces has a .NET counterpart that is mostly shipped in the SDK (compiler, analyzers, formatter, trimmer, NativeAOT compiler, deterministic compilation, SourceLink, package validation), so the work is wiring them so a single dotnet build plus dotnet test blocks CI on all of them together (NFR-17).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:3-6` · high · sha:27bc3ba15ac4</sub>
- The Microsoft.SourceLink.GitHub reference is deleted because the .NET 8+ SDK ships SourceLink in-box; with the three PackageReferences and the Directory.Packages.props row removed the solution builds clean and the Core PDB still carries the raw.githubusercontent.com/dexpace/dotnet-sdk/<commit> source-link mapping (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:34-38` · high · sha:27bc3ba15ac4</sub>
- A package whose only job duplicates something the SDK ships is retired (principle P2 applied to tooling), and the baseline break fix is scheduled as roadmap phase 0.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:37-39` · high · sha:27bc3ba15ac4</sub>
- The NU1902 failure is the warnings-as-errors gate working as intended, since NuGet audit is exactly the CVE check the gate table asks for.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:39-41` · high · sha:27bc3ba15ac4</sub>
- Package validation (EnablePackageValidation with PackageValidationBaselineVersion) is the second half of NFR-4 because it runs API compatibility against the previously published package and catches binary breaks a source listing hides, such as a parameter gaining a default value, a method moving to a base class, or readonly removed from a struct.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:55-62` · high · sha:27bc3ba15ac4</sub>
- CA1515 ("consider making public types internal") is not relied on because it targets applications, not libraries whose public surface is the product.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:69-70` · high · sha:27bc3ba15ac4</sub>
- The banned-API gate is new and is this port's counterpart of Ruby's Dexpace/NoThreadInterrupt cop; BannedApiAnalyzers fails the build with RS0030 on any symbol listed in a committed BannedSymbols.txt.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:72-74` · high · sha:27bc3ba15ac4</sub>
- NFR-8 applies literally on .NET because ILLink trimming and NativeAOT both remove what they cannot see referenced statically, reversing the Ruby conclusion (which retargeted the gate) and the Node conclusion (which scoped it down because bundlers see a static graph).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:89-93` · high · sha:27bc3ba15ac4</sub>
- Instead of shipping keep-rules a downstream shrinker must honour, a library declares IsTrimmable and IsAotCompatible, which turn on the trim (IL2xxx), single-file (IL3000-range) and AOT (IL3050-range) analyzers at the library's own build, so reflection the trimmer would break is a compile error rather than a consumer runtime failure.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:93-96` · high · sha:27bc3ba15ac4</sub>
- NFR-8's keep/retain configuration becomes annotations ([DynamicallyAccessedMembers], [RequiresUnreferencedCode]) on the few members that need them, carried inside the assembly where no consumer can lose them.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:96-98` · high · sha:27bc3ba15ac4</sub>
- The AOT smoke test is the only gate that can catch the class of failure the Tristate converter factory suppresses a warning for: the analyzer proves the code is annotated, the smoke test proves the annotation is true.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:107-109` · high · sha:27bc3ba15ac4</sub>
- The in-box-versus-package split is .NET's version of Ruby's default-gem trap and the one audit gate with no reference counterpart.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:123-124` · high · sha:27bc3ba15ac4</sub>
- On .NET the NFR-10 trap is mostly closed by construction because a net8.0 build compiles against net8.0 reference assemblies, so calling an API added in .NET 9 is a compile error, making the emitted-artifact-target and visible-API-level agreement the SDK's default.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:130-133` · high · sha:27bc3ba15ac4</sub>
- Roadmap decision D1 (approved 2026-09-28) sets the target framework to net10.0 for every project, set once in Directory.Build.props, superseding the net8.0 floor and net8.0 test leg as the NFR-10 gate; there is one target and one runtime and the gate is that tests run on it on every matrix row.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:147-150` · high · sha:27bc3ba15ac4</sub>
- Part (3) of the zero-dependency audit (in-box-versus-package split) has no subject after D1 because all three packages are in-box on net10.0; the nuspec assertion still reads every dependency group and now also requires exactly one, net10.0.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:152-155` · high · sha:27bc3ba15ac4</sub>
- Per the D1 ruling, each CI matrix row installs only the SDK that global.json pins (which carries the .NET 10 runtime) and tests run on one target framework, correcting §9.3's CI matrix.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:157-159` · high · sha:27bc3ba15ac4</sub>
- Because CentralPackageTransitivePinningEnabled is on (§2.3), NuGet promotes the centrally pinned transitive Microsoft.Extensions.Logging.Abstractions to a direct nuspec dependency of each adapter, so the adapters' nuspecs list the logging facade in addition to Core (correction of 2026-09-28).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:161-166` · high · sha:27bc3ba15ac4</sub>
- NFR-11's "leaks no async-framework types" is satisfied in spirit before any gate because Task, ValueTask, IAsyncEnumerable<T> and CancellationToken are the runtime's own, not a framework's.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:172-174` · high · sha:27bc3ba15ac4</sub>
- NFR-13 becomes mechanical rather than a review convention: file_header_template in .editorconfig with IDE0073 at error severity fails the build on a file missing the two-line MIT header (verified under EnforceCodeStyleInBuild).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:184-187` · high · sha:27bc3ba15ac4</sub>
- For NFR-16, nuget.org repository-signs every package, satisfying provenance for consumers, and author signing with the organisation's certificate runs on the release job only and is optional locally, per the requirement's own split.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:188-190` · high · sha:27bc3ba15ac4</sub>
- Strong naming is not a security measure on .NET but is kept because strong-named consumers cannot reference an unsigned assembly, with the key committed as Microsoft's library guidance recommends; it was listed among the pre-roadmap platform design's native defaults and had not been built.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:190-195` · high · sha:27bc3ba15ac4</sub>
- The operating-system axis of the CI matrix earns its cost because proxy discovery, certificate handling and HttpClient's handler differ by platform, and §8.2's proxy semantics were verified on Linux only.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:254-257` · high · sha:27bc3ba15ac4</sub>
- global.json was pinned at 10.0.100 with rollForward latestFeature, but latestPatch is adopted (the house guide shows latestPatch, styleguide 01-formatting-and-tooling 1.6) because the feature band governs analyzer behaviour, and the pin moves to the band the team actually builds with.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:257-260` · high · sha:27bc3ba15ac4</sub>
- A styleguide rule that the 9.4 table marks "not wired" is a roadmap item, not a departure.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:284-285` · high · sha:27bc3ba15ac4</sub>
- Which CA rules latest-recommended enables was established by probing rather than assuming.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:285-289` · high · sha:27bc3ba15ac4</sub>
- CA1308 and CA1054-CA1056 are dialled to none without colliding with any styleguide rule.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:326-327` · high · sha:27bc3ba15ac4</sub>
- CA1062 is dialled to none and recorded as a departure rather than conformed, because nullable reference types make a non-nullable parameter a compile-time contract and ThrowIfNull still guards the public entry points.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:329-330` · high · sha:27bc3ba15ac4</sub>
- The original recorded rationale for disabling CA2007 (await using and await foreach emit implicit awaits the rule cannot see) is factually wrong, as verified on SDK 10.0.401 where CA2007 at warning reports on both await using var m = new MemoryStream() and await foreach (var i in Gen()).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:331-335` · high · sha:27bc3ba15ac4</sub>
- ImplicitUsings=enable departs from styleguide 1.5 and 12.7 and is not in the overlay, so the port conforms (committed GlobalUsings.cs, ImplicitUsings off) rather than recording a departure, because the guide's reason - every dependency visible at the top of the file - applies with full force to a library whose dependency surface is under audit (design section 9.2).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:340-343` · high · sha:27bc3ba15ac4</sub>
- The rollForward and lock-file departures are conformed rather than recorded (design section 9.3), and MA0051 is the overlay's own roadmap row.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:343-344` · high · sha:27bc3ba15ac4</sub>
- Correction (2026-09-29): CA2007 is re-enabled for libraries, so the CA2007 half of the departure is conformed, not recorded.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:346-346` · high · sha:27bc3ba15ac4</sub>
- Three earlier statements are superseded by the CA2007 correction - the 09 Concurrency row's "CA2007 dialled to none", CA2007 in the list of five analyzers dialled to none, and "Both are section 10 entry 28" - so only CA1062 remains a recorded departure, with section 10 entry 28 carrying the matching correction.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:353-356` · high · sha:27bc3ba15ac4</sub>
- The CA1062 reasoning is unchanged by the correction, and the original text stands as written with the correction paragraph as the authority.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:355-356` · high · sha:27bc3ba15ac4</sub>
- Configuration that must be identical everywhere lives in exactly one file because a property repeated in every .csproj drifts, leaving the gate uneven across the solution.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:31-31` · high · sha:2598baa9074f</sub>
- Nullable enable is chosen over annotations because annotations alone records intent without enforcing it, while enable turns on both the annotation context and the warning context.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:78-78` · high · sha:2598baa9074f</sub>
- Language version and analyzer behaviour track the SDK, so an unpinned SDK makes builds differ between laptops and CI.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:109-109` · high · sha:2598baa9074f</sub>
- A method over 70 lines is treated as doing more than one thing, so the cap is a forcing function that turns a vague feeling into a build failure.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:121-121` · high · sha:2598baa9074f</sub>
- The 70-line cap is the deliberate dexpace value, set at Go's level and not scaled down for C#, and is an addition the runtime style does not make, recorded in the README ledger.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:122-122` · high · sha:2598baa9074f</sub>

## Reference
- NFR-5 excludes sample/example code, test-only guards and test fixtures from the aggregate coverage, and its conformance is that deleting tests so aggregate line coverage drops below the floor fails the default build.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:17-17` · high · sha:5f4684bf7123</sub>
- NFR-6 conformance is that introducing a deprecation (or any) warning fails the build until resolved.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:18-18` · high · sha:5f4684bf7123</sub>
- NFR-7 conformance is that introducing a style/static-analysis violation fails the build, and that any disabled analyzer has a documented reason and re-enable condition.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:19-19` · high · sha:5f4684bf7123</sub>
- NFR-8 conformance is shrinking a program depending only on the shipped keep-configuration and running it against a live round-trip, which succeeds, while removing any shipped keep-rule makes the run fail.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:24-24` · high · sha:5f4684bf7123</sub>
- NFR-9 conformance is that dropping a shipped keep-rule or renaming a runtime-wired type makes the guard fail the ordinary build.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:25-25` · high · sha:5f4684bf7123</sub>
- NFR-12 conformance is building the same commit twice in clean environments and comparing artifact digests, which must be identical.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:37-37` · high · sha:5f4684bf7123</sub>
- In the reference implementation the NFR-13 license header is a review convention rather than a mechanical gate, and conformance is scanning all source files for the required header.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:38-38` · high · sha:5f4684bf7123</sub>
- NFR-14 conformance is grepping for version/coordinate literals outside the central catalog and bumping a dependency version once to confirm it propagates everywhere.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:39-39` · high · sha:5f4684bf7123</sub>
- NFR-15 conformance is that the SDK's self-reported version queried at runtime from a packaged artifact equals the build version and never the placeholder.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:40-40` · high · sha:5f4684bf7123</sub>
- NFR-16 conformance is that a CI/release build fails an unsigned publication while a local build without keys still publishes unsigned.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:41-41` · high · sha:5f4684bf7123</sub>
- Test projects override GenerateDocumentationFile and the CS1591 suppression locally.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:116-117` · high · sha:c9a7834ab04d</sub>
- After D1, global.json pins SDK 10.0.401 with rollForward latestPatch (section 9.3).
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:171-172` · high · sha:c9a7834ab04d</sub>
- Both converter-factory shapes ran correctly under a NativeAOT publish for a value-type argument on SDK 10.0.401, but only the interface shape is correct by construction.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:357-359` · high · sha:68af5c6bf0ea</sub>
- At d45e64b the analyzers were wired and dotnet format passed (verified) but was not a CI step.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:13-13` · high · sha:27bc3ba15ac4</sub>
- At d45e64b the docs gate was wired while PublicApiAnalyzers was pinned in Directory.Packages.props and referenced by no project.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:14-14` · high · sha:27bc3ba15ac4</sub>
- At d45e64b coverage was collected in CI with no threshold.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:16-16` · high · sha:27bc3ba15ac4</sub>
- At d45e64b IsAotCompatible was set on the System.Text.Json package only.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:18-18` · high · sha:27bc3ba15ac4</sub>
- At d45e64b dotnet build of Dexpace.Sdk.sln failed with error NU1902 (Warning As Error) because Microsoft.Build.Tasks.Git 8.0.0 has a known moderate severity vulnerability.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:32-33` · high · sha:27bc3ba15ac4</sub>
- Microsoft.Build.Tasks.Git arrived transitively through the explicit Microsoft.SourceLink.GitHub 8.0.0 reference that each src project carried.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:33-35` · high · sha:27bc3ba15ac4</sub>
- PublicApiAnalyzers is pinned in Directory.Packages.props at version 3.3.4 and was referenced by no project at d45e64b.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:57-58` · high · sha:27bc3ba15ac4</sub>
- Package validation also checks that every target framework in one package exposes a compatible surface.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:61-62` · high · sha:27bc3ba15ac4</sub>
- dotnet pack -p:EnablePackageValidation=true ran clean on the scratch copy with no baseline (verified), and the baseline is set at the first published version.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:62-64` · high · sha:27bc3ba15ac4</sub>
- At d45e64b only Dexpace.Sdk.Serialization.SystemTextJson declared IsAotCompatible; Core did not although building Core with -p:IsAotCompatible=true produced no IL warnings (verified), so the property costs nothing to add to every library and the STJ package's analyzers cannot see into Core.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:98-101` · high · sha:27bc3ba15ac4</sub>
- NativeAOT smoke publishing is feasible in CI because the scratchpad toolchain published and ran NativeAOT binaries on Linux with only the cached Microsoft.DotNet.ILCompiler package and the system clang (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:109-111` · high · sha:27bc3ba15ac4</sub>
- As originally written, Dexpace.Sdk.Serialization.SystemTextJson was to list only Core because System.Text.Json is in the shared framework on both targets, so the adapter's one third-party library is the runtime's own.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:118-119` · high · sha:27bc3ba15ac4</sub>
- .NET 8 leaves support on 2026-11-10, so the floor was to rise to net10.0 in the roadmap, which retires §7.2's hand-written line-reader justification for PipeReader, the net8.0 STJ gap for RespectNullableAnnotations (§7.3) and the System.Linq.AsyncEnumerable caveat (§7.1).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:136-139` · high · sha:27bc3ba15ac4</sub>
- At d45e64b Core and Http.SystemNet targeted net8.0 only while the STJ package and both test projects multi-targeted, so the net10.0 test run exercised Core's net8.0 binary, contradicting the net8.0;net10.0 decision and the styleguide overlay.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:139-144` · high · sha:27bc3ba15ac4</sub>
- As packed, both adapter nuspecs (Dexpace.Sdk.Http.SystemNet and Dexpace.Sdk.Serialization.SystemTextJson) have a single net10.0 group listing Dexpace.Sdk.Core 0.0.1-alpha.1 and Microsoft.Extensions.Logging.Abstractions 10.0.12, while Dexpace.Sdk.Core lists the facade alone.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:164-167` · high · sha:27bc3ba15ac4</sub>
- Compiler output is deterministic (Deterministic on, ContinuousIntegrationBuild normalising paths in CI) and two packs of the same tree produced byte-identical DLLs (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:179-181` · high · sha:27bc3ba15ac4</sub>
- The .nupkg containers differed only by zip-entry timestamps, and with SOURCE_DATE_EPOCH set two packs a minute apart produced byte-identical .nupkg files (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:181-183` · high · sha:27bc3ba15ac4</sub>
- The section 9.4 table maps each styleguide chapter's enforceable rules to the gate that enforces them (an analyzer ID wired through .editorconfig and TreatWarningsAsErrors, dotnet format, a build property, a test, or review), and is the design-level half of the overlay in docs/styleguide/README.md, which remains the index of departures.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:281-285` · high · sha:27bc3ba15ac4</sub>
- "Wired" means failing the build today at commit d45e64b.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:285-285` · high · sha:27bc3ba15ac4</sub>
- A class library under AnalysisLevel latest-recommended was probed and raised CA1848, CA1860, CA1869, CA2016, CA2201 and CA2254 as warnings (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:285-289` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 01 (Formatting and tooling) maps 1.1 props centralised to review plus one Directory.Build.props, 1.2 to TreatWarningsAsErrors, 1.3 to AnalysisLevel and EnforceCodeStyleInBuild, 1.4 to Nullable=enable, 1.5 to .editorconfig plus dotnet format, 1.6 to global.json, Deterministic and lock files, and 1.7 to MA0051 at 70 lines.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:293-293` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, chapter 01 rules 1.2-1.4 are wired; 1.1 is broken by per-csproj TargetFramework; 1.5's ImplicitUsings=disable is not followed; dotnet format passes but is not in CI; there are no lock files; and MA0051 is not referenced.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:293-293` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 02 (Naming) maps 2.1-2.4 to dotnet_naming_rule entries plus IDE1006, IDE0049 and IDE0044, 2.5 to CA2208, 2.8 to CA1715, and 2.6/2.7 (no I prefix, no Async suffix) to a departure.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:294-294` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, naming analyzer-backed rules are partly wired through the recommended CA set, no dotnet_naming_rule entries exist yet, and 2.6/2.7 are departed (design section 10 entry 27).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:294-294` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 03 (Nullability) maps 3.1 to CS86xx as errors, 3.3 (bare ! banned) to review because no analyzer exists for bare !, 3.4 to IDE0041, and 3.2 to CA1062.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:295-295` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, nullability rule 3.1 is wired and CA1062 is dialled to none.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:295-295` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 04 (Variables) maps 4.1 to IDE0007/IDE0008, 4.2 to IDE0090, 4.3 to IDE0044, 4.4 to CA1802 and IDE0036, 4.5 to IDE0040, 4.6 to IDE0003/IDE0009, and 4.9 to IDE0018.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:296-296` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, only csharp_style_var_for_built_in_types is configured for chapter 04 (as a suggestion), and the remaining rules need .editorconfig severities.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:296-296` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 05 (Methods) maps 5.1 to MA0051, 5.3 to CA1062 plus review, 5.4 to IDE0022/IDE0025, 5.7 to IDE0062, and 5.8 (no recursion) to review; none are wired beyond the recommended set at d45e64b.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:297-297` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 06 (Types) maps 6.2 sealed to review (CA1852 for internal types), 6.3 to CS8509 as error, 6.6 to CA1815 and CA1051, and 6.8 to CA1714/CA1717/CA1027/CA2217.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:298-298` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, CS8509 is wired for chapter 06, and the CA rules apply only where the recommended set enables them.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:298-298` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 07 (Idioms) maps 7.1 to IDE0066/IDE0078/IDE0260, 7.3 to IDE0300/IDE0301/IDE0305, 7.5 to IDE0031/IDE0270, and 7.7 to CA2208; the IDE severities are not configured at d45e64b.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:299-299` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 08 (Errors) maps 8.1 to CA2201, 8.2/8.4 to CA1031, 8.3 to CA2200, 8.5 to CA1062, and 8.6 to CA1032 and CA1064.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:300-300` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, CA2201 is wired (verified), while CA1031 is not in the recommended set (verified) and needs an explicit severity.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:300-300` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 09 (Concurrency) maps 9.1 to RS0030 (design section 9.1) in place of the unreferenced VSTHRD002, 9.2 to review (VSTHRD100 not referenced), 9.3 to CA2016, 9.4 to CA2007, 9.5 to CA2012, 9.6 to CS4014, and 9.8 to CS1996.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:301-301` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, CA2016 (verified), CS4014 and CS1996 are wired for chapter 09, and CA2007 was dialled to none (superseded by the 2026-09-29 correction).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:301-301` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 10 (API design) maps 10.3 to CA1002, CA1819 and CA2227, 10.5 to CA1068, 10.6 to [Obsolete] plus package validation, and 10.7 to RS0016/RS0017.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:302-302` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, chapter 10 CA rules apply where the recommended set enables them, and 10.7 (public API files) is not wired (design section 9.1).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:302-302` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 11 (Testing) maps 11.1 to one test project per assembly on xUnit v3, 11.2 to xUnit1003/xUnit1008, 11.4 (no Moq) and 11.5 (no FluentAssertions 8 or later) to Directory.Packages.props review, 11.6 to TimeProvider fakes plus review, and 11.8 to a coverage threshold and Stryker.NET.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:303-303` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, xUnit analyzers are wired via the xunit package, the repository is on xUnit v2 rather than v3 (overlay), coverage is not gated, and Http.SystemNet has no test project.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:303-303` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 12 (Project organisation) maps 12.1 to IDE0161, 12.3 to central package management, 12.5 to PublicAPI files, 12.6/12.9 to architecture tests (design section 9.2), and 12.7 to ImplicitUsings=disable and IDE0005.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:304-304` · high · sha:27bc3ba15ac4</sub>
- IDE0161 is wired through csharp_style_namespace_declarations = file_scoped:warning, because the :warning suffix is honoured by EnforceCodeStyleInBuild (verified).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:304-304` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, chapter 12 rule 12.3 is wired and 12.7 is not followed.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:304-304` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 13 (Resources) maps 13.1 to CA2000, 13.3 to CA1816, 13.4 to CA2213, 13.5 to CA2215, and 13.8 to RS0030 on new HttpClient().
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:305-305` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, CA2000 is not in the recommended set (verified) and needs an explicit severity, and RS0030 is not wired for chapter 13.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:305-305` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 14 (Documentation) maps 14.1/14.6 to GenerateDocumentationFile plus CS1591, 14.7 to CA1200 and cref warnings, and 14.8 to IDE0005 plus review; it is wired, and CS1591 is the most-exercised gate in the tree.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:306-306` · high · sha:27bc3ba15ac4</sub>
- Styleguide chapter 15 (Performance) maps 15.2/15.5 to CA1860, 15.6 to CA1834, 15.7 to CA1822, CA1859 and IsAotCompatible, and 15.8 to BenchmarkDotNet evidence.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:307-307` · high · sha:27bc3ba15ac4</sub>
- At d45e64b, CA1860 is wired (verified) and AOT analyzers are enabled on the System.Text.Json package only.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:307-307` · high · sha:27bc3ba15ac4</sub>
- The hosting companion styleguide bears on two surfaces only (per the overlay): the DI package and the serializer/instrumentation surfaces.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:309-310` · medium · sha:27bc3ba15ac4</sub>
- The styleguide overlay records six departure rows, but the build configuration carries more departures, each of which is either conformed or recorded.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:323-324` · high · sha:27bc3ba15ac4</sub>
- As originally written, .editorconfig dials five analyzers to none, each with a rationale - CA1308 (lower-casing is correct for HTTP tokens), CA1054-CA1056 (string URLs at ergonomic entry points), CA1062, CA2007, and CA1707 for test names.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:324-326` · high · sha:27bc3ba15ac4</sub>
- CA1062 and CA2007 collide with the styleguide: the guide's own .editorconfig example (csharp/01-formatting-and-tooling 1.3) sets both to error, and styleguide rules 3.2, 5.3, 8.5 and 9.4 name them as their enforcement.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:327-329` · high · sha:27bc3ba15ac4</sub>
- Satisfying CA2007 on await using requires await using var x = y.ConfigureAwait(false), which changes the local's type to ConfiguredAsyncDisposable, and on await foreach requires .ConfigureAwait(false) on the sequence; this ergonomic cost is the real cost of the rule.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:335-338` · high · sha:27bc3ba15ac4</sub>
- Roadmap phase 0 (PR #21, 2026-09-28) set CA2007 to warning for src/.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:347-348` · high · sha:27bc3ba15ac4</sub>
- The wrong .editorconfig rationale for CA2007 was removed and the overlay row was corrected on 2026-09-29.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:352-353` · high · sha:27bc3ba15ac4</sub>
- As built at d45e64b, toolchain and quality gates are partial, with warnings-as-errors, recommended analyzers, code-style enforcement, the documentation gate, central package management, deterministic compilation and NuGet audit wired, and the build currently broken by NU1902.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:358-360` · high · sha:27bc3ba15ac4</sub>
- As built at d45e64b, the following were missing - public-API files, package validation baseline, coverage threshold, dotnet format and locked restore in CI, trim/AOT properties on Core and Http.SystemNet, AOT smoke consumer, nuspec and architecture tests, IDE0073, banned-API list, strong naming, signing, conformance kit, and cross-OS matrix.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:360-363` · high · sha:27bc3ba15ac4</sub>
- The reference Directory.Build.props sets TargetFramework net10.0, LangVersion 14.0, Nullable enable, ImplicitUsings disable, TreatWarningsAsErrors true, AnalysisLevel latest-Recommended, EnforceCodeStyleInBuild true, GenerateDocumentationFile true and Deterministic true.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:7-22` · high · sha:2598baa9074f</sub>
- The guide promotes CA2007 (ConfigureAwait in libraries), CA1062 (validate public arguments) and IDE0005 (remove unnecessary usings) to error severity in .editorconfig.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:66-71` · high · sha:2598baa9074f</sub>
- Formatting is enforced through .editorconfig settings csharp_style_namespace_declarations = file_scoped and dotnet_sort_system_directives_first = true.
  <sub>styleguide · `docs/styleguide/csharp/01-formatting-and-tooling.md:104-104` · high · sha:2598baa9074f</sub>
- Chapter 01 covers dotnet format and .editorconfig, Allman braces, four spaces, file-scoped namespaces, Nullable enable, TreatWarningsAsErrors, AnalysisLevel, C# 14 / .NET 10 and the 70-line cap.
  <sub>styleguide · `docs/styleguide/csharp/README.md:33-33` · medium · sha:1e6ba36fc337</sub>

## Conflicts
- **Target framework (1.1) vs the net8.0;net10.0 floor (design §2.3, §9.2)** — The styleguide targets .NET 10 and the design first planned libraries on net8.0 and net10.0, but roadmap decision D1, approved 2026-09-28, raised the floor so every library, test project and tool targets net10.0 only, set once in Directory.Build.props; the port CONFORMS to the styleguide on the target framework and no note is owed (the C# language version is a separate, kept departure).
  <sub>styleguide `docs/styleguide/csharp/01-formatting-and-tooling.md:11-11` · design `docs/sdk-design-dotnet/02-project-and-solution-layout.md:165-170` · conformed 2026-10-02</sub>
- **Curated explicit global usings (1.5, 12.7) vs ImplicitUsings enabled (design §9.4)** — The styleguide requires curated, explicit global usings and disables ImplicitUsings, while the repository had ImplicitUsings enabled as an undocumented departure; ImplicitUsings is now disabled in Directory.Build.props with a committed GlobalUsings.cs per project, so the port CONFORMS and no note is owed.
  <sub>styleguide `docs/styleguide/csharp/12-project-organization.md:127-138` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:340-343` · conformed 2026-10-02</sub>
- **70-line method cap (1.7) vs a review-only cap (design §9.4)** — The styleguide requires the 70-line method cap to be analyzer-enforced as a build error, while the repository first enforced it by review only; Meziantou.Analyzer MA0051 is now wired at 70 lines as a build error for libraries, so the port CONFORMS and no note is owed.
  <sub>styleguide `docs/styleguide/csharp/01-formatting-and-tooling.md:118-130` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:343-344` · conformed 2026-10-02</sub>
- **LangVersion latest vs the named 14.0 (1.6, design §2.3)** — The styleguide says to name the C# language version explicitly (14.0), never latest, so that an upgrade is a reviewed diff, but the SDK sets LangVersion to latest in Directory.Build.props, a recorded unchanged row of the SDK overlay; the port KEEPS this departure and records it in a note.
  <sub>styleguide `docs/styleguide/csharp/01-formatting-and-tooling.md:105-110` · design `docs/sdk-design-dotnet/02-project-and-solution-layout.md:165-170` · kept 2026-10-02</sub>

## Superseded


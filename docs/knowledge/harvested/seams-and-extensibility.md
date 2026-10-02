# seams-and-extensibility

## Rules
- Each concrete seam implementation lives outside the core in its own module that depends on the core plus at most one third-party library (NFR-2).
  <sub>spec · `docs/product-spec/01-product-overview.md:9-9` · high · sha:4f786c44354d</sub>
- Each external concern is exposed as exactly one narrow interface that the core depends on but never implements, and the core never references a concrete implementation by name.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:5-5` · high · sha:8014d2ec2c9d</sub>
- SEAM-1 (MUST) - The core library MUST NOT embed a concrete HTTP transport, byte-stream I/O implementation, or wire codec, and MUST depend at runtime on nothing beyond its language's standard library plus a compile-time-only logging facade.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:7-7` · high · sha:8014d2ec2c9d</sub>
- SEAM-2 (MUST) - Each external concern with a core-owned contract MUST be exposed as exactly one narrow interface, and the core MUST NOT reference any concrete implementation of a seam by name.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:8-8` · high · sha:8014d2ec2c9d</sub>
- SEAM-5 (MUST) - Provider resolution MUST follow a fixed precedence in which an explicitly installed provider always wins, and otherwise the runtime auto-discovers providers registered on the classpath/plugin registry.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:34-34` · high · sha:0adae2d6a47f</sub>
- Resolution MUST throw a descriptive error when ZERO providers are discoverable (telling the caller to install one) and when MORE THAN ONE distinct provider is discoverable (listing all candidates), while exactly one discoverable provider is selected silently (SEAM-5).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:34-34` · high · sha:0adae2d6a47f</sub>
- SEAM-6 (MUST) - Explicit provider installation MUST be idempotent for the same instance and MUST reject installing a DIFFERENT provider when one is already installed, naming both.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:35-35` · high · sha:0adae2d6a47f</sub>
- SEAM-7 (MUST) - A successful auto-resolution MUST be cached process-wide, while an UNRESOLVED state (zero or multiple, which throws) MUST remain re-evaluable so a later-registered provider or explicit install can still take effect.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:36-36` · high · sha:0adae2d6a47f</sub>
- SEAM-9 (MUST) - Provider resolution/install/swap state MUST be concurrency-safe, so reads observe the latest install without blocking, writes are serialized so two concurrent installs cannot both pass the conflict check, and a concurrent first-access cannot run the discovery scan twice.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:37-37` · high · sha:0adae2d6a47f</sub>
- SEAM-8 (SHOULD) - When an explicit install replaces a DIFFERENT provider that had already been auto-resolved AND handed out, the runtime SHOULD emit a warning (not fail), because objects may already exist against the previous provider.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:38-38` · high · sha:0adae2d6a47f</sub>
- SEAM-10 (SHOULD) - The provider registry SHOULD tolerate one logical provider seen through more than one loader without misreporting it as multiple, de-duplicating by concrete implementation identity, and SHOULD recognize a thin delegating shim as its canonical target.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:39-39` · high · sha:0adae2d6a47f</sub>
- XCUT-23 (MUST) A pluggable single-implementation seam (the I/O provider and any similar SPI) MUST resolve deterministically: an explicit install always wins, otherwise the implementation is auto-discovered from the environment/classpath.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:32-32` · high · sha:d6123be82c9e</sub>
- XCUT-23 (MUST) Zero or multiple provider candidates with no explicit selection MUST fail loudly with an actionable error rather than silently picking one or no-op'ing, and reads of the resolved provider MUST be concurrency-safe.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:32-32` · high · sha:d6123be82c9e</sub>
- Provider resolution precedence is: explicit install always wins; else auto-discover; zero discoverable candidates yields a descriptive install-hint error; more than one yields an error listing all candidates; exactly one is selected silently (SEAM-5, restated as XCUT-23).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:692-695` · high · sha:da6000c93fc5</sub>
- Explicit install is idempotent for the same instance and a hard failure for a different one (SEAM-6); a successful auto-resolution is cached process-wide while an unresolved state stays re-evaluable (SEAM-7); replacing an already-handed-out auto-resolved provider is a warning (SEAM-8); reads see the latest install without blocking and writes are serialised (SEAM-9).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:696-699` · high · sha:da6000c93fc5</sub>
- Dexpace.Sdk.Extensions.DependencyInjection validates at ValidateOnStart that each configured client resolves exactly one IAsyncHttpClient and one ISerde, failing with a message listing every registered implementation type when several exist and with an install hint naming the Use.../Add... call when there is none, because IServiceCollection accepts several registrations and GetService silently returns the last.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:714-719` · high · sha:da6000c93fc5</sub>
- A transport or codec is never activated by mere presence, because "whatever happens to be installed silently wins" is an auditability failure.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:733-734` · high · sha:da6000c93fc5</sub>

## Constraints

## Conclusions
- Discovery by reflection over loaded assemblies (AppDomain.GetAssemblies, Assembly.GetTypes, Activator.CreateInstance) is rejected because ILLink and NativeAOT cannot see it, so a trimmed app would discover nothing and NFR-8 would be violated by the mechanism itself.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:701-705` · high · sha:da6000c93fc5</sub>
- A [ModuleInitializer] registry registration in each adapter is rejected because it runs only when the adapter assembly loads (lazily, on first type reference), so the candidate set would depend on call order, opposite to SEAM-5's determinism.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:705-708` · high · sha:da6000c93fc5</sub>
- The port keeps SEAM-5's outcomes and drops the registry: without DI, every component takes its transport or codec as a non-nullable constructor or factory parameter (as DexpacePipeline.CreateDefault(IAsyncHttpClient transport, ...) does), making the zero-candidate branch a compile-time error and the multiple-candidate branch unrepresentable.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:710-714` · high · sha:da6000c93fc5</sub>
- DI registration uses TryAdd, which provides SEAM-6's idempotence for the same registration, while a second different explicit registration is the validator's "different provider" failure naming both.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:719-721` · high · sha:da6000c93fc5</sub>
- SEAM-7's caching is the container's singleton lifetime and its re-evaluable clause is inapplicable because a built container gains no registrations; SEAM-8's warning is moot because an auto-resolved provider never exists; SEAM-9's single-construction guarantee is the container's singleton behaviour; SEAM-10's de-duplication across loaders is vacuous per design section 2.4.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:721-725` · high · sha:da6000c93fc5</sub>
- The branch-by-branch SEAM-5..SEAM-10 mappings are recorded as section 10 entry 9 because SEAM-5's letter (auto-discovery from a classpath/plugin registry) names a step the port deliberately does not take.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:725-727` · high · sha:da6000c93fc5</sub>

## Reference
- The core depends on a small enumerated set of interfaces it never implements, namely a byte-stream provider, a synchronous transport, an asynchronous transport, a wire codec, and an operation-input projection (SEAM-2).
  <sub>spec · `docs/product-spec/01-product-overview.md:9-9` · high · sha:4f786c44354d</sub>
- A single implementation on the classpath is auto-discovered with no bootstrap call, while zero or multiple candidates fail loudly (SEAM-5).
  <sub>spec · `docs/product-spec/01-product-overview.md:9-9` · high · sha:4f786c44354d</sub>
- SEAM-1 rationale is that a dependency-free core lets consumers pay only for the runtimes they choose and swap any concern independently.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:7-7` · high · sha:8014d2ec2c9d</sub>
- SEAM-1 conformance is a dependency audit of the core module that finds only the standard library plus the compile-scope logging facade, with no transport, codec or stream symbol referenced from core.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:7-7` · high · sha:8014d2ec2c9d</sub>
- The enumerated seams under SEAM-2 are byte-stream provider, synchronous transport, asynchronous transport, wire codec, and operation-input-to-request projection.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:8-8` · high · sha:8014d2ec2c9d</sub>
- SEAM-2 conformance is to substitute a fake implementation of each seam and confirm the core operates unchanged; its rationale is that one seam per concern keeps the dependency surface flat and each capability independently replaceable.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:8-8` · high · sha:8014d2ec2c9d</sub>
- SEAM-6 conformance is that install(A) then install(A) is a no-op and install(A) then install(B not equal A) throws naming both.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:35-35` · high · sha:0adae2d6a47f</sub>
- SEAM-7 rationale is that caching a failure would permanently wedge a process that later gains a provider; conformance is that first success scans, second does not, and a failing access re-scans next time.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:36-36` · high · sha:0adae2d6a47f</sub>
- SEAM-9 conformance is to race N threads installing the same instance and reading, and assert exactly-once resolution and no torn state.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:37-37` · high · sha:0adae2d6a47f</sub>
- SEAM-8 conformance is that auto-resolve A, hand it out, then install B yields a warning, while installing before any access yields no warning.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:38-38` · high · sha:0adae2d6a47f</sub>
- In the reference implementation, providers are resolved via a ServiceLoader over classpath service entries, with a registration shim because a JVM singleton object cannot be reflectively instantiated.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:39-39` · high · sha:0adae2d6a47f</sub>
- As built (d45e64b), core has no registry and takes the transport explicitly as designed; the DI package and its single-registration validation are not built.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:736-737` · high · sha:da6000c93fc5</sub>
- Seam outcomes: byte-stream provider (SEAM-3..10) retired; synchronous transport (SEAM-11..15) kept as IHttpClient with a real synchronous path; asynchronous transport and pivot (SEAM-16..18, SEAM-30) kept as IAsyncHttpClient over Task<Response>; wire codec (SEAM-19..23) kept as ISerde with four profiles, two derived in core; operation projection (SEAM-26..28) kept as OperationDescriptor; discovery (SEAM-5..10, XCUT-23) retired in favour of explicit construction and DI single-registration validation; lifecycle (SEAM-14, SEAM-25, XCUT-13, XCUT-22) is IDisposable/IAsyncDisposable, latched and ownership-aware.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:822-830` · high · sha:da6000c93fc5</sub>

## Conflicts

## Superseded


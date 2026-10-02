# testing

## Rules
- Test projects are partitioned by the package they test, and Dexpace.Sdk.Core.Tests never references a concrete transport, so that SEAM-2's conformance clause (substitute a fake of each seam and confirm the core operates unchanged) is structurally true.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:136-140` · high · sha:c9a7834ab04d</sub>
- Core's test suite uses in-memory fakes of IAsyncHttpClient, IHttpClient and ISerde, and each adapter's suite runs Dexpace.Sdk.Conformance against that adapter plus its adapter-specific tests.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:140-142` · high · sha:c9a7834ab04d</sub>
- Tests are partitioned by [Trait("Category", ...)] into Unit (hand-built fakes, TimeProvider fakes, no socket, the large majority), Integration (reference transport against the loopback server), Conformance (the kit), and AotSmoke (the §9.2 consumer run by its own CI job), so a filter selects each and the default run is fast.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:206-209` · high · sha:27bc3ba15ac4</sub>
- The conformance kit carries lifecycle assertions: disposal is idempotent, a caller-supplied HttpClient survives disposal (TRANSPORT-15, XCUT-22), and a send after disposal fails, because adapters satisfy those clauses by accident on the first call and not the second.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:226-230` · high · sha:27bc3ba15ac4</sub>
- Conformance reporting is per requirement ID, with the Appendix B items as a many-to-one view whose status is the worst of their assertions, the unit Ruby settled on after finding one B.7 item vacuous for one ID and failing for another.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:230-232` · high · sha:27bc3ba15ac4</sub>
- BenchmarkDotNet lives in a benchmarks/ project with MemoryDiagnoser, run on demand, and its results are required in the pull request for any performance-motivated change (styleguide 15-performance 15.8).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:240-243` · high · sha:27bc3ba15ac4</sub>
- Allocation guarantees (OBS-1's disabled path, OBS-25's untraced path) are unit tests asserting a zero delta in GC.GetAllocatedBytesForCurrentThread() around the call, not benchmarks, because a guarantee belongs in the blocking gate.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:243-245` · high · sha:27bc3ba15ac4</sub>
- Tests run on xUnit v3 over Microsoft.Testing.Platform, with one version pinned across the solution through Directory.Build.props.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:40-43` · high · sha:7aad1d7b7110</sub>
- Each production assembly is mirrored by exactly one test project (Dexpace.Billing maps to Dexpace.Billing.Tests), so a failure points at one assembly and InternalsVisibleTo grants that one project access to internals.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:40-43` · high · sha:7aad1d7b7110</sub>
- Tests must be isolated, with no shared mutable static state, no reliance on execution order, and no file or port that a sibling test also touches, because xUnit runs test classes in parallel by default.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:44-44` · high · sha:7aad1d7b7110</sub>
- Each test constructs its own dependencies, using the class constructor as setup and IDisposable or IAsyncDisposable as teardown, so nothing leaks between cases.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:44-53` · high · sha:7aad1d7b7110</sub>
- Review rejects shared mutable state between tests, and dotnet test runs in CI.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:54-54` · high · sha:7aad1d7b7110</sub>
- Each test is structured Arrange-Act-Assert with a single act, and the phases are separated by blank lines so a reader can find the act at a glance.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:56-59` · high · sha:7aad1d7b7110</sub>
- A test is named for the behaviour under test, so the name reads as a sentence (for example Total_sums_line_items_and_applies_tax) and identifies what broke from the failure list alone.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:56-59` · high · sha:7aad1d7b7110</sub>
- Use [Fact] for a single concrete case, [Theory] with [InlineData] for a handful of literal rows, and [MemberData] when the cases come from a method because they are not compile-time constants.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:60-60` · high · sha:7aad1d7b7110</sub>
- Write property-based tests with FsCheck for invariants and round-trips, asserting facts that must hold for all inputs.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:75-78` · high · sha:7aad1d7b7110</sub>
- Use property tests where a law governs the code, namely round-trips (serialize/parse, encode/decode), invariants (a sorted list stays sorted, a balance never goes negative), and idempotence.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:79-79` · high · sha:7aad1d7b7110</sub>
- Keep FsCheck-generated values inside the domain by constraining the generator rather than discarding most inputs, so the run stays fast and every case is meaningful.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:79-79` · high · sha:7aad1d7b7110</sub>
- Property tests complement example tests rather than replace them, since properties prove the law and examples pin specific cases, and review expects properties where a law governs the code.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:79-90` · high · sha:7aad1d7b7110</sub>
- Prefer hand-written fakes (small in-memory implementations of an injected interface, named for their role) to mocks.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:92-95` · high · sha:7aad1d7b7110</sub>
- Moq is banned at the package level.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:96-107` · high · sha:7aad1d7b7110</sub>
- NSubstitute is the sanctioned mocking framework and is used only when an interface is wide or stubbing it by hand is genuinely impractical.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:96-96` · high · sha:7aad1d7b7110</sub>
- Assert with xUnit's built-in Assert plus Shouldly, and do not adopt FluentAssertions v8 or later.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:109-113` · high · sha:7aad1d7b7110</sub>
- The Shouldly version is pinned in Directory.Build.props so that no one drifts onto the paid FluentAssertions line by accident, and review rejects new FluentAssertions usage.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:113-121` · medium · sha:7aad1d7b7110</sub>
- Every test is deterministic, injecting TimeProvider wherever time is read and substituting FakeTimeProvider in tests, which sets and advances the clock explicitly instead of waiting.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:123-126` · high · sha:7aad1d7b7110</sub>
- Any Random used in a test is seeded with a fixed value so a generated case is reproducible from the log.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:126-126` · high · sha:7aad1d7b7110</sub>
- Tests never use Thread.Sleep to synchronize; they await actual completion or advance the fake clock.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:127-127` · high · sha:7aad1d7b7110</sub>
- Tests use no real network or live service (fake the boundary or use a container), and make no assumption about the order parallel tests run or the order a dictionary enumerates.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:127-127` · high · sha:7aad1d7b7110</sub>
- Review rejects Thread.Sleep, DateTime.Now, real network access, and order-dependence in tests.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:136-136` · high · sha:7aad1d7b7110</sub>
- Integration tests exercise the real thing, using WebApplicationFactory<TEntryPoint> to boot the real ASP.NET Core host pipeline in-process and Testcontainers for the database.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:138-142` · high · sha:7aad1d7b7110</sub>
- Integration tests back the host with a real datastore (an actual Postgres or Redis started by Testcontainers per test run), and review rejects in-memory database substitutes such as the EF in-memory provider or a fake Redis.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:142-152` · high · sha:7aad1d7b7110</sub>
- Cover the negative space (invalid input, cancellation, boundaries) as deliberately as the happy path, testing the empty list, maximum value, negative amount, duplicate key, and cancelled token.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:154-157` · high · sha:7aad1d7b7110</sub>
- Tests assert that invalid input throws the specific exception, that a cancelled CancellationToken aborts promptly with OperationCanceledException, and that boundary values (0, int.MaxValue, empty, one element) behave.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:157-157` · high · sha:7aad1d7b7110</sub>
- Where it pays, gate coverage of the negative space with Stryker.NET mutation testing, since surviving mutants with high line coverage mean tests execute code without checking it.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:158-158` · high · sha:7aad1d7b7110</sub>
- Review requires negative-space and boundary cases per behaviour, a coverage threshold applies in CI, and an optional Stryker.NET mutation gate applies on critical modules.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:170-170` · high · sha:7aad1d7b7110</sub>
- Enforce the dependency direction (acyclic, inward-pointing graph and layered seams) with an executable architecture test, using NetArchTest or an equivalent, run in CI with the suite (styleguide 12.9).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:156-173` · high · sha:a44b6f9eaba9</sub>
- Architecture tests assert that the domain depends on nothing of ours, that no context references a sibling it should not, and that the host owns no domain types.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:160-160` · high · sha:a44b6f9eaba9</sub>

## Constraints

## Conclusions
- The NativeAOT smoke consumer is a separate executable project because it must be published, not merely tested.
  <sub>design · `docs/sdk-design-dotnet/02-project-and-solution-layout.md:142-143` · high · sha:c9a7834ab04d</sub>
- xUnit is the test framework and the conformance kit is framework-free; the house guide wants xUnit v3 on Microsoft.Testing.Platform with Shouldly (styleguide 11-testing 11.1, 11.5), while the repository ran xUnit v2 with Assert and the overlay records the migration as a roadmap item, not a deviation.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:199-202` · high · sha:27bc3ba15ac4</sub>
- The transport tests move to their own project, which also makes them the first consumer of the conformance kit.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:204-205` · high · sha:27bc3ba15ac4</sub>
- The transport conformance suite ships as a package from day one, Dexpace.Sdk.Conformance (§2.1), so an adapter author outside the repository can prove TRANSPORT-1 through TRANSPORT-30 against their own transport with the same assertions the first-party transport passes, as Ruby's dexpace-conformance gem and Node's transport-conformance package do.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:211-214` · high · sha:27bc3ba15ac4</sub>
- Conformance assertions are plain methods that throw a ConformanceException carrying expected and actual values, with no test-framework dependency in the package, because an adapter author on NUnit or MSTest should not inherit this repository's test framework (the same reason Ruby kept Minitest out of the kit's runtime); each first-party adapter's test project drives the kit from xUnit.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:214-218` · high · sha:27bc3ba15ac4</sub>
- The conformance fixture is a TcpListener speaking HTTP/1.1 by hand, not Kestrel, and not a stubbing library or custom HttpMessageHandler.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:218-219` · high · sha:27bc3ba15ac4</sub>
- A stub or custom HttpMessageHandler cannot express socket-level behaviour such as connect-versus-read timeout classification (TRANSPORT-3, TRANSPORT-4), a lazily-read body whose disposal releases the connection (TRANSPORT-25), chunked framing, a half-closed peer, and vendor status codes surfaced faithfully (TRANSPORT-24).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:219-222` · high · sha:27bc3ba15ac4</sub>
- A handler stub is specific to HttpClient, so the same assertions could not run against a non-HttpClient transport.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:222-223` · high · sha:27bc3ba15ac4</sub>
- Kestrel is rejected as the conformance fixture because it speaks the wire correctly, while TRANSPORT-14 needs a response with a control byte in one header value delivered with the rest of the response intact, which a conforming server refuses to emit.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:223-226` · high · sha:27bc3ba15ac4</sub>
- Modules whose bugs are silent (header and media-type parsing, the SSE line reader and field grammar, the query splice, URL redaction, retry classification) run under Stryker.NET on a schedule with a mutation-score break threshold (styleguide 11-testing 11.8, optional gate), because line coverage says a line ran, not that a test checked it.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:236-239` · high · sha:27bc3ba15ac4</sub>
- Mutation testing is not a per-PR gate because its cost scales with the suite, and NFR-17 binds the NFR gates, which mutation testing is not.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:239-240` · high · sha:27bc3ba15ac4</sub>
- Test tool choices (runner, assertion library, mocking framework) are constrained by supply-chain reality, because a test tool that becomes a license trap or injects ads is not fit for an enterprise codebase.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:3-3` · high · sha:7aad1d7b7110</sub>
- Fakes are preferred over mocks because they test behaviour rather than call sequences, whereas a mock verifying that Save was called once with given arguments couples the test to the implementation and makes harmless refactors redden the suite.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:95-95` · high · sha:7aad1d7b7110</sub>
- Moq is banned because its 4.20 release bundled the SponsorLink dependency, which harvested developers' email addresses at build time, making it unfit for an enterprise supply chain regardless of later reversals.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:96-96` · high · sha:7aad1d7b7110</sub>
- Shouldly is chosen because it is MIT-licensed and free for commercial use, with readable failure messages that quote the expression and both values.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:112-112` · high · sha:7aad1d7b7110</sub>
- FluentAssertions v8 (Xceed, January 2025) is rejected because it moved to a paid commercial license of roughly $130 per developer per year, with only the v7 line remaining free, which is an unacceptable supply-chain and budget risk.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:113-113` · high · sha:7aad1d7b7110</sub>
- Integration tests must not fake the boundary because the bugs they exist to catch live in the seams, such as the SQL the ORM emits, connection-string parsing, and the JSON the host actually serializes.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:141-141` · high · sha:7aad1d7b7110</sub>
- In-memory datastore substitutes are rejected because they ignore constraints, transactions, and concurrency that the real engine enforces, so a passing test can ship a bug the real database would catch.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:142-142` · high · sha:7aad1d7b7110</sub>
- Dependency-direction invariants are made executable rather than left to review because an invariant guarded only by review erodes the first time a hurried reference points the wrong way.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:159-159` · high · sha:a44b6f9eaba9</sub>

## Reference
- At d45e64b Dexpace.Sdk.Core.Tests also covered Http.SystemNet, whose csproj granted InternalsVisibleTo to a nonexistent Dexpace.Sdk.Http.SystemNet.Tests, against the styleguide 11.1 one-to-one rule for test projects.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:202-204` · high · sha:27bc3ba15ac4</sub>
- The loopback server used for the chapter's instrumentation verification is the seed of the conformance fixture (§8.1).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:226-226` · high · sha:27bc3ba15ac4</sub>
- xUnit v3 on Microsoft.Testing.Platform, the successor to the VSTest host, provides a real Main, deterministic startup, native dotnet test integration, and trimmable, AOT-friendly test executables.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:43-43` · high · sha:7aad1d7b7110</sub>
- A [Theory] collapses near-identical tests into one body where the data is the variation, and each row reports as its own result.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:60-60` · high · sha:7aad1d7b7110</sub>
- Test naming and one-act structure are enforced by review, theory data well-formedness by analyzers xUnit1003 and xUnit1008, and a [Theory] without data is flagged by an analyzer.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:73-73` · high · sha:7aad1d7b7110</sub>
- FsCheck generates hundreds of inputs including boundary and pathological ones and shrinks a counterexample to the smallest failing case.
  <sub>styleguide · `docs/styleguide/csharp/11-testing.md:78-78` · high · sha:7aad1d7b7110</sub>
- Chapter 11 covers xUnit v3 on Microsoft.Testing.Platform, FsCheck property tests, fakes over mocks (NSubstitute when needed), Shouldly not FluentAssertions, WebApplicationFactory plus Testcontainers, TimeProvider, and determinism.
  <sub>styleguide · `docs/styleguide/csharp/README.md:43-43` · medium · sha:1e6ba36fc337</sub>

## Conflicts
- **xUnit v3 on Microsoft.Testing.Platform (11.1) vs xUnit v2 (design §9.3)** — The styleguide requires xUnit v3 on Microsoft.Testing.Platform, while the design recorded xUnit v2 at the time of the as-built audit; the port has migrated to xUnit v3 on Microsoft.Testing.Platform and CONFORMS on the runner, so no note is owed (assertion style is a separate, kept departure).
  <sub>styleguide `docs/styleguide/csharp/11-testing.md:40-54` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:199-201` · conformed 2026-10-02</sub>
- **Shouldly assertions (11.5) vs xUnit Assert only (design §9.3)** — The styleguide says to assert with xUnit's Assert plus Shouldly, but the repository's tests assert with xUnit's Assert alone and reference no Shouldly package, which the overlay folded into its xUnit migration row; the port KEEPS xUnit Assert as its assertion style for now and records it in a note.
  <sub>styleguide `docs/styleguide/csharp/11-testing.md:109-122` · design `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:199-202` · kept 2026-10-02</sub>

## Superseded


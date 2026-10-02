# porting-method

## Rules
- Where a MUST-level requirement's intent is separable from its host-specific mechanism, the .NET port keeps the intent and finds the .NET-native mechanism.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:3-8` · high · sha:8e66b82361d2</sub>
- Where collapsing or retiring a reference concept is what idiomatic .NET design demands, the design says so plainly and cites the requirement whose letter, not spirit, is being adjusted.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:3-8` · high · sha:8e66b82361d2</sub>
- P1 — For every MUST requirement, ask what invariant it protects and what platform constraint made the reference express it that way, because the rationale clause is usually where the constraint hides.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:9-11` · high · sha:8e66b82361d2</sub>
- P2 — A seam whose only job is dependency avoidance retires when the host has a runtime standard; the test is whether the type is shipped and versioned with the runtime itself or installed from a package registry.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:12-19` · high · sha:8e66b82361d2</sub>
- P3 — A seam is kept even when embedding its implementation would be free, because zero dependency cost is not zero coupling cost; a seam that exists to avoid a dependency is retired, while one that exists to avoid a policy default is kept.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:20-22` · high · sha:8e66b82361d2</sub>
- P4 — Two reference concepts are collapsed into one only when the host genuinely has one, since preserving a split the host cannot honour manufactures a fake API and preserves no invariant.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:23-26` · high · sha:8e66b82361d2</sub>
- P4 — The strongest argument for a collapse is to find and cite a spec clause that already condemns the shape that would otherwise have to be built.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:24-25` · high · sha:8e66b82361d2</sub>
- P4 converse — A split the host genuinely has must not be collapsed.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:25-26` · high · sha:8e66b82361d2</sub>
- P5 — After merging two requirements, both must be quoted and the single primitive shown to satisfy each clause verbatim; a collapse that cannot be argued clause-by-clause is a narrowing in disguise.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:27-29` · high · sha:8e66b82361d2</sub>
- P6 — A residual difference is named precisely rather than glossed, using the sentence pattern "this changes how the requirement is satisfied, not whether".
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:30-31` · high · sha:8e66b82361d2</sub>
- P7 — When the host makes a requirement free, the design says so, says why, and states explicitly the hidden precondition under which it stops being free, because that is where the simplification can be silently misapplied.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:32-34` · high · sha:8e66b82361d2</sub>
- P8 — When the host makes a requirement harder or impossible, the deviation catalogue admits it, states the mitigation, states that it narrows rather than eliminates the gap, and records it as a language-level limitation.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:35-37` · high · sha:8e66b82361d2</sub>
- P8 — Honest partial conformance is preferred over a fake proof of conformance.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:35-37` · high · sha:8e66b82361d2</sub>
- P9 — No deviation may narrow a MUST-level correctness guarantee; deviation is permitted in mechanism and packaging, never in observable guarantee.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:38-40` · high · sha:8e66b82361d2</sub>
- P9 — Where the port's answer is arguably stronger than the reference's, the design says so and argues it rather than quietly claiming parity.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:39-40` · high · sha:8e66b82361d2</sub>
- P10 — A deviation the specification sanctions is labelled as sanctioned with the sanctioning text quoted, while a deviation that rests on the author's judgment carries the full burden of P5 and P9.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:41-43` · high · sha:8e66b82361d2</sub>
- P11 — No tier, seam or module is fabricated to preserve symmetry, because a shape preserved without its substance is worse than an honestly missing shape.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:44-46` · high · sha:8e66b82361d2</sub>
- P11 — A fabricated tier must not be routed through the same underlying source under a different key, which would be one lookup wearing two names.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:45-46` · high · sha:8e66b82361d2</sub>
- P12 — The deviation set is consolidated into one numbered catalogue that is cross-referenced and contains nothing new, with every entry back-referencing an argument made in full elsewhere so that no deviation can hide inside prose.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:47-49` · high · sha:8e66b82361d2</sub>
- P13 — The design hunts for the obvious-but-wrong platform tool, meaning something the host ships that looks like the requirement's answer but silently violates it, and each instance gets a named gotcha with the precise divergence verified against a real runtime rather than asserted.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:50-53` · high · sha:8e66b82361d2</sub>
- P14 — Where the host ecosystem has converged on one dominant API shape, the seam is defined as a structural subset of that shape so existing infrastructure plugs in with zero adapter code, adopting the ecosystem's shape rather than its package and thereby preserving SEAM-1.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:54-57` · high · sha:8e66b82361d2</sub>
- Every design section ends with a one-line "As built (d45e64b):" status.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:67-68` · high · sha:8e66b82361d2</sub>
- Where verification contradicted secondary research or the as-built code's own documentation, the verified behaviour is what is written down and the conflict is noted at the point of use.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:83-85` · high · sha:8e66b82361d2</sub>
- A faithful port audits each of the reference's JVM-derived constraints for whether it holds on .NET rather than inheriting or discarding the set wholesale.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:44-45` · high · sha:d7cea7b15cf3</sub>

## Constraints
- .NET is nominally typed, so a type satisfies an interface only by naming it, and P14's "shape, not package" clause can be honoured literally only when the convergent shape lives in the shared framework.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:59-62` · high · sha:8e66b82361d2</sub>
- No .NET 8 runtime was available at authoring, so net8.0-specific behaviour is stated from the Microsoft.NETCore.App.Ref 8.0.31 reference pack and local-cache NuGet package manifests and is marked so at the point of use.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:81-84` · high · sha:8e66b82361d2</sub>

## Conclusions
- Every deviation call is collected in the §10 catalogue, and pre-committing to that consolidated catalogue is what makes the rest of the design document safe to read charitably.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:6-8` · high · sha:8e66b82361d2</sub>
- When a seam's replacement is a shared-framework type, choosing it is choosing the platform rather than taking a dependency, so the seam's discovery, registration and precedence apparatus is moot.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:17-19` · high · sha:8e66b82361d2</sub>
- The P13 named gotchas are judged the highest-value paragraphs in a port design.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:52-53` · high · sha:8e66b82361d2</sub>
- Unlike the Ruby port, the .NET design is written after code, with the tree at d45e64b already shipping the HTTP models, bodies, transport SPI, errors, the System.Net.Http transport, the System.Text.Json codec, options, diagnostics, the pipeline and its policies, auth and pagination.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:65-68` · high · sha:8e66b82361d2</sub>
- The .NET decisions taken before the design document, built by PRs #3-#9, are treated as the existing .NET decisions, adopted unless the specification or the method argues otherwise and overturned explicitly where they are.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:68-71` · high · sha:8e66b82361d2</sub>
- Native-first is a cross-cutting platform decision under which the sibling ports inform what to build, not how.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:71-72` · high · sha:8e66b82361d2</sub>

## Reference
- The shared-framework convergent shapes named for P14 are Stream, Task<T>, CancellationToken, IAsyncEnumerable<T>, IDisposable, ActivitySource, Meter and TimeProvider.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:61-62` · high · sha:8e66b82361d2</sub>
- Four of the pre-document .NET decisions were cross-cutting platform decisions that later chapters name by topic (native-first, standard abstraction packages in core, the connection-layer split, trim- and AOT-safe multi-targeting), while the rest were per-subsystem choices that the adopting or overturning chapter states inline, naming the pull request that built it.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:70-76` · high · sha:8e66b82361d2</sub>
- .NET facts in the design were verified against .NET SDK 10.0.401 and runtime 10.0.12 using file-based apps run with dotnet run check.cs, some against a local HttpListener/TcpListener peer, unless marked as holding only from a later release or as read from a reference pack, package manifest or runtime source.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:78-81` · high · sha:8e66b82361d2</sub>
- Sibling port designs for Ruby and Node.js follow the same method, nothing in the .NET design depends on them, and where the .NET port reverses one of their conclusions the reversal is argued in the .NET design.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:85-86` · high · sha:8e66b82361d2</sub>
- As built (d45e64b) for the sync transport seam: Execute(Request) has no options or token and is sync-over-async; RequestBodyContent lacks the synchronous override; the parameterless constructor follows redirects; headers go through TryAddWithoutValidation unvalidated, lower-cased, with Host/framing headers passed and Content-Type mis-partitioned; an inbound Content-Type the parser rejects throws and leaks the message; no ObjectDisposedException latch; no delegate-backed transport factory.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:397-401` · high · sha:da6000c93fc5</sub>
- As built (d45e64b) for the async transport seam: ExecuteAsync(Request, CancellationToken) takes no RequestOptions; AsAsync offloads to the shared pool with no scheduler parameter and does not pass the token into Execute; AsBlocking drops the token; there is no quiet-dispose rule for TaskCompletionSource losers (none exist yet); no Rx bridge.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:531-534` · high · sha:da6000c93fc5</sub>
- Section 10 entry 22 records the OBS clauses whose letter is not met (OBS-1's singleton, OBS-3's text rendering, OBS-5, OBS-9) and the ones that are stronger (OBS-3's key validation).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:69-71` · high · sha:ddf8f695ff61</sub>
- The span-model residuals (the null no-op, the key names, the host-owned allow-list default) are recorded together as section 10 entry 23.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:119-121` · high · sha:ddf8f695ff61</sub>
- How the specification left the system-property tier without porting guidance is section 11 item 4; the substitution and the retired global slot are section 10 entry 25.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:233-234` · high · sha:ddf8f695ff61</sub>

## Conflicts

## Superseded


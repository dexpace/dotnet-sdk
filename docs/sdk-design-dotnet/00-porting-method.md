## Porting Method

**Porting method.** The method this document follows is stated once here and applied throughout. Its governing
rule: **where a MUST-level requirement's intent is separable from its host-specific mechanism, keep the intent and
find the .NET-native mechanism; where collapsing or retiring a reference concept is what idiomatic design demands,
say so plainly and cite the requirement whose letter, not spirit, is being adjusted.** Every such call is
collected in §10. Pre-committing to a consolidated deviation catalogue is what makes the rest of the document safe
to read charitably. The fourteen working principles:

- **P1 — Separate intent from mechanism, per requirement.** For every MUST ask two questions: what invariant does
  this protect, and what platform constraint made the reference express it *this* way? The rationale clause is
  usually where the constraint hides.
- **P2 — A seam whose only job is dependency avoidance retires when the host has a runtime standard.** The test is
  not "is this type good enough" but "is it shipped and versioned with the runtime itself, or installed from a
  package registry?" On .NET the line is concrete: an assembly in the `Microsoft.NETCore.App` shared framework (the
  reference pack of the library's *lowest* target framework) is the platform; anything that arrives as a NuGet
  `PackageReference` is a dependency, whatever its `System.*` or `Microsoft.*` name suggests. If the former,
  choosing it is choosing the platform, not taking a dependency, and there is nothing left to keep out of the core
  — so the seam's whole discovery/registration/precedence apparatus is moot for that seam.
- **P3 — Keep a seam even when embedding its implementation would be free.** Zero *dependency* cost is not zero
  *coupling* cost. The distinguishing question: does the seam exist to avoid a dependency (retire it) or to avoid
  a *policy default* (keep it)?
- **P4 — Collapse two reference concepts into one only when the host genuinely has one.** Preserving a split the
  host cannot honour manufactures a fake API and preserves no invariant. The strongest form of the argument is to
  find a spec clause that already condemns the shape you would otherwise have to build, and cite it. The converse
  binds equally: **do not collapse a split the host genuinely has.**
- **P5 — Prove a collapse satisfies both requirements' letter, not just their spirit.** After merging two
  requirements, quote both and show the single primitive satisfies each clause verbatim. A collapse that cannot be
  argued clause-by-clause is a narrowing in disguise.
- **P6 — Name the residual difference precisely instead of glossing it.** The sentence pattern is "this changes
  *how* the requirement is satisfied, not *whether*."
- **P7 — When the host makes a requirement free, say so, say why, then find the hidden precondition.** Every "free
  on this host" claim needs the condition under which it stops being free, stated explicitly, because that is the
  one place the simplification can be silently misapplied.
- **P8 — When the host makes a requirement harder or impossible, admit it in the catalogue.** Honest partial
  conformance beats a fake proof: state the mitigation, state that it narrows rather than eliminates the gap, and
  record it as a language-level limitation.
- **P9 — No deviation may narrow a MUST-level correctness guarantee.** Deviation is permitted in *mechanism* and
  *packaging*, never in *observable guarantee*. Where the port's answer is arguably stronger, say so and argue it
  rather than quietly claiming parity.
- **P10 — Distinguish "the spec sanctions this" from "we judged this."** The specification occasionally anticipates
  a port's situation directly. Where such a clause exists the deviation is sanctioned and must be labelled so, with
  the sanctioning text quoted; where it does not, the deviation carries the full burden of P5 and P9.
- **P11 — Do not fabricate a tier, seam, or module to preserve symmetry.** A shape preserved without its substance
  is worse than an honestly missing shape. The specific failure to avoid: routing a fabricated tier through the
  same underlying source under a different key, which is one lookup wearing two names.
- **P12 — Consolidate: one numbered catalogue, cross-referenced, containing nothing new.** Every catalogue entry
  back-references an argument made in full elsewhere, so a reviewer can audit the complete deviation set without
  reading the whole document, and no deviation can hide inside prose.
- **P13 — Hunt for the obvious-but-wrong platform tool.** The recurring failure: the host ships something that
  *looks* like the requirement's answer and silently violates it. Each instance gets a named gotcha with the
  precise divergence, verified against a real runtime rather than asserted. These are the highest-value
  paragraphs in a port design.
- **P14 — Reuse the host ecosystem's convergences; do not invent parallel vocabulary.** Where the ecosystem has
  converged on one dominant API shape for a concern, define the seam as a *structural subset* of that shape so
  existing infrastructure plugs in with zero adapter code — adopting the ecosystem's *shape*, not its *package*,
  and so preserving **SEAM-1**.

**Two host notes on applying the method to .NET.** First, .NET is nominally typed: a type satisfies an interface
only by naming it, so P14's "shape, not package" clause can be honoured literally only when the convergent shape
lives in the shared framework (`Stream`, `Task<T>`, `CancellationToken`, `IAsyncEnumerable<T>`, `IDisposable`,
`ActivitySource`, `Meter`, `TimeProvider`). Where the ecosystem's convergence point is a NuGet package —
`Microsoft.Extensions.Logging.Abstractions`' `ILogger` is the one case in core — P14 and P2 pull in opposite
directions, and §2.4 argues the resolution and records it as a deviation rather than pretending the package is
platform. Second, unlike the Ruby port, this design is written *after* code: the tree at `d45e64b` already ships the
HTTP models, bodies, transport SPI, errors, the `System.Net.Http` transport, the `System.Text.Json` codec, options,
diagnostics, the pipeline and its policies, auth and pagination. Every design section therefore ends with a one-line
**As built (d45e64b):** status, and the .NET decisions taken before this document, which PRs #3–#9 built, are
treated as the existing .NET decisions — adopted unless the specification or this method argues otherwise, and
overturned explicitly where they are. Four of them were cross-cutting platform decisions, and later chapters name
them by topic: **native-first** (the siblings inform *what* to build, not *how*); **core may take the standard
abstraction packages** (§2.4); the **connection-layer split** (*SDK-domain* concerns in the SDK pipeline,
*connection-level* concerns in the `HttpMessageHandler` chain underneath it; §1, §6.1); and **modern multi-target, trim-
and AOT-safe** (libraries on `net8.0;net10.0`, since superseded by roadmap decision D1's `net10.0` only; §2.3, §9.2).
The rest were per-subsystem choices, and the chapter that adopts or overturns one states it
inline, naming the pull request that built it.

.NET facts asserted below were verified against .NET SDK 10.0.401 / runtime 10.0.12 on the authoring machine
(file-based apps run with `dotnet run check.cs`, some of them against a local `HttpListener`/`TcpListener` peer)
unless explicitly marked as holding only from a later release, as read from a reference pack or package manifest,
or as read from runtime source. No .NET 8 runtime was available, so behaviour specific to the `net8.0` target is
stated from the `Microsoft.NETCore.App.Ref` 8.0.31 reference pack and the NuGet package manifests in the local
cache, and is marked so at the point of use. Where verification contradicted secondary research or the as-built
code's own documentation, the verified behaviour is what is written down and the conflict is noted at the point of
use. Sibling port designs for other host languages (Ruby, Node.js) exist and follow this same method; nothing in
this document depends on them, and where this port reverses one of their conclusions the reversal is argued here.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

---

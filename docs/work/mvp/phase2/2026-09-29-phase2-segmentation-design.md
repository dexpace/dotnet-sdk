# Phase 2 — Segmentation Design

**Status:** Draft, for review. Written 2026-09-29 against `main` at `2f08ec9`, before any phase-2 sub-phase design
exists. Its three open questions were ruled by the lead on 2026-09-29; see
[Decisions](#decisions-ruled-by-the-lead). GitHub issue [#30](https://github.com/dexpace/dotnet-sdk/issues/30).

**What this document is.** The segmentation design the roadmap's
[Segmentation rule](../2026-09-27-dotnet-sdk-v1-roadmap-design.md#segmentation-rule) requires of phase 2, which spans
two ID-bearing spec chapters (`HTTP`, `SEAM`). It decides how phase 2 is cut and in what order, says for every
ordering whether it is a **dependency** or a **convenience**, assigns each of the 71 requirement IDs to exactly one
sub-phase, and names what each sub-phase inherits from phase 1 and owes to phases 3, 4, 7, 8 and 9.

**What this document is not.** It is not a sub-phase design, a plan or a checklist. Where it says a sub-phase design
must argue something, it stops there: a segmentation design that settles its sub-phases' content is the same failure
as a sub-phase plan that silently re-imposes a chain, arriving from the other direction. It cites the domain-model
construction pattern (design §4), the constraints that bite (`CLAUDE.md`, design §3.2's `HttpClient` gotchas) and
the P1–P14 porting method (`docs/sdk-design-dotnet/00-porting-method.md`) rather than restating them (roadmap
constraint 9).

## Governing documents, and what the corpus could and could not answer

- **Normative.** `docs/product-spec/04-core-http-domain-model.md` states `HTTP-3`–`HTTP-35`, `HTTP-46`–`HTTP-50` and
  `HTTP-53`.
- `docs/product-spec/02-architectural-principles.md` states `HTTP-1`, `HTTP-2`, `SEAM-1`, `SEAM-2`, `SEAM-13` and
  `SEAM-29`.
- `docs/product-spec/03-pluggable-seams-and-extension-model.md` states the rest of the `SEAM` family, `SEAM-29`
  included, except five.
- `SEAM-15`, `SEAM-20`, `SEAM-22`, `SEAM-23` and `SEAM-28` are stated **only** in
  `docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`, which is canonical for every ID and
  level below.
- **Design** (non-normative, binding by convention): `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md`
  §3.1–§3.7, `docs/sdk-design-dotnet/04-domain-model-construction.md` §4 and §4.1–§4.4, with their **As built
  (d45e64b)** lines; §5.3 of `docs/sdk-design-dotnet/05-pipeline-architecture.md` for the bridges; the §10 entries,
  §11 items and §12 rows for `HTTP` and `SEAM`.
- **Styleguide**: `docs/styleguide/csharp/` and the SDK overlay in `docs/styleguide/README.md` (the `I` prefix and
  `Async` suffix departures that the SPI signature keeps).
- **Process**: the [roadmap](../2026-09-27-dotnet-sdk-v1-roadmap-design.md), the
  [phase 1 checklist](../phase1/2026-09-28-phase1-security-fixes-checklist.md), [`docs/README.md`](../../../README.md)
  and the `housekeeping` skill.

**The phase-start queries, run 2026-09-29** (roadmap "How Phases Get Executed", step 1):

| Query | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | exit 1: no notes exist |
| `scripts/knowledge --section conflicts --brief` | exit 1: no Conflicts entry exists, because neither the design nor the styleguide role is harvested |
| `scripts/knowledge --prefix-info HTTP` | 53 IDs: 43 MUST, 9 SHOULD, 1 MAY; 53 of 53 substantive |
| `scripts/knowledge --prefix-info SEAM` | 30 IDs: 23 MUST, 5 SHOULD, 2 MAY; 25 substantive, 5 uncited |
| `scripts/knowledge --gaps HTTP,SEAM` | `HTTP`: none. `SEAM`: `SEAM-15`, `SEAM-20`, `SEAM-22`, `SEAM-23`, `SEAM-28`, "appendix C only" — the roadmap's gap table exactly |
| `scripts/knowledge --phase 1 --brief` | the one phase-1 document cites 54 IDs; of phase 2's, `HTTP-13`, `HTTP-17`–`HTTP-21`, `HTTP-26` and `SEAM-2` |

Only the `spec` role is harvested today. The `design` and `styleguide` roles are phase 0 task 8
([#29](https://github.com/dexpace/dotnet-sdk/issues/29)), so every design and styleguide statement in this document
was read from the chapter files directly, not through the corpus.

One tool behaviour surprised the reading: `scripts/knowledge --gaps HTTP SEAM`, with the prefixes space-separated
as the roadmap's `--gaps <PREFIXES>` suggests, reports **only `HTTP`** and exits 0, so it silently hides all five
`SEAM` gap IDs. The comma-separated form above is correct. That is a finding against `tools/Knowledge`'s argument
parsing, and it is owned by [#31](https://github.com/dexpace/dotnet-sdk/issues/31), which also covers `--phase` not
expanding ID ranges (why this document spells out `HTTP-30` in its 2a table).

---

## Prerequisites: each ordering, dependency or convenience

| Predecessor | Kind | Evidence |
|---|---|---|
| Phase 0 task 1, the green build ([#14](https://github.com/dexpace/dotnet-sdk/issues/14)) | **dependency** | Nothing lands on a red build (ordering rationale 1). Met. |
| Phase 0 tasks 2 and 3, CI and the analyzers (#15, PR #21) | **dependency** | Constraint 8 makes the `PublicAPI.Unshipped.txt` diff under `RS0016`/`RS0017` the evidence of 2b's SPI change, and task 3 wired `PublicApiAnalyzers`. The same PR put `M:System.Uri.ToString` in `BannedSymbols.txt` (line 35) for every library project, so the `RS0030` ban the phase 2 card assigns to 2b already exists. Met. |
| Phase 0 task 4, the test partition (#16, PR #22) | **dependency** | `SEAM-2`'s row cites `Architecture/Seam2ArchitectureTests`, which task 4 created. 2b's signature change edits the in-memory fakes task 4 moved into `tests/Dexpace.Sdk.TestSupport` (`RecordingTransport`, `RecordingSyncTransport`, `ScriptedTransport`). 2a's wire-casing proof (`HTTP-21`) needs the loopback fixture in `tests/Dexpace.Sdk.Http.SystemNet.Tests`. Met. |
| Phase 0 tasks 5–7, hygiene, drift and the legacy documents (#19, PR #25) | **convenience** | Nothing in phase 2 reads them. Task 6 put a "not yet read by anything" warning on `DexpaceClientOptions.BaseAddress`, and 2b removes it when it wires the property. Met. |
| Phase 0 task 8, the harvest ([#29](https://github.com/dexpace/dotnet-sdk/issues/29), open) | **convenience** | The harvest indexes `docs/sdk-design-dotnet/` and `docs/styleguide/`. It adds no normative content, and both trees are frozen and readable directly, as this document read them. The spec role, which `--req`, `--gaps` and `--prefix-info` need, is already seeded. The one output that could change phase 2's public surface is the constraint-6 conflict notes, but the two conflicts that touch it (the `I` prefix and the `Async` suffix, both **kept**) are already decided in constraint 6's table, the overlay and design §10's `styleguide-naming` topic. The cost of proceeding is that phase 2's designs cite the design and styleguide by path, not by corpus key. |
| Phase 1 S1, header and media-type injection (#18, PR #24) | **entry criterion**, convenience in kind | The roadmap draws `P1 -.-> P2a` dashed and makes S1 an entry criterion. Both are right. 2a could have absorbed S1's work, so S1 is not a dependency. But constraint 5 needs S1's `Security` tests to exist before 2a restructures `Headers`, or there is nothing for 2a to keep green. Met. |
| Phase 1 S2–S9 | **convenience** | 2a edits their tests mechanically (see [Phase 1's routed items](#phase-1s-routed-items-and-the-security-tests-constraint-5)), but no phase-2 requirement needs them. Met. |
| 2a, for 2b | **dependency** on one 2a artefact, `RequestOptions` merged, for 2b's SPI signature work only; **convenience** for the rest of 2b | Narrower than the roadmap's "2a then 2b": the gate is `RequestOptions`, not 2a's exit. See [The cut](#the-cut-and-the-order). |

**The roadmap's entry criterion "Phase 0 has exited" is not literally met.** Phase 0's exit requires `--role design`
and `--role styleguide` queries to return entries, and those wait on #29. This document records the lead-visible
consequence rather than hiding it: phase 2 proceeds on the argument above, and 2a's and 2b's designs each restate
it in their own Prerequisite sections.

**No dependency edge inverts the bottom-up order.** Every later-phase touch found below is a `⏳` against a phase
the roadmap already schedules after phase 2 (3b, 4a, 4c, 7a, 8a, 8b, 9), never a precondition of phase-2 work, so
the roadmap needs no inversion status note. One edge is a candidate for the opposite correction: the dependency
graph draws `P2b --> P3`, but the phase 3 card's entry reads "2a has exited" and phase 3's scope needs no 2b artefact
(see [Coupling](#coupling-with-later-phases)). If phase 3's segmentation design confirms that, 2b runs in parallel
with 3a and 3b.

---

## The cut and the order

**Two ways, on the chapter line. 2a leads 2b, but the dependency is narrower than the roadmap states: 2b is
hard-gated on one 2a artefact, `RequestOptions`, not on 2a's exit.** The roadmap's cut is adopted on the evidence,
and its order is overturned in part. Its stated reason is half right, and only one of the edges it implies is a
code dependency.

| Sub-phase | Name | Owns | Rows | Order |
|---|---|---|---|---|
| **2a** | Domain model | `HTTP-1`–`HTTP-35`, `HTTP-46`–`HTTP-50`, `HTTP-53`, `SEAM-29` | 42 | leads |
| **2b** | Seams | `SEAM-1`–`SEAM-28`, `SEAM-30` | 29 | **dependency** on 2a's `RequestOptions` being merged, for the SPI signature work only; **convenience** for everything else, which may start at once |

### 2a leads 2b: the edges, named

The roadmap's reason is "`SEAM-29`'s construction contract and the `RequestOptions` type that 2b's SPI signature
carries are 2a's". At `2f08ec9` the **`RequestOptions` half is a hard edge**: `grep -r RequestOptions src` finds
nothing, and design §3.2/§3.3's signatures `Execute(Request, RequestOptions, CancellationToken)` and
`ExecuteAsync(Request, RequestOptions, CancellationToken)` cannot compile without the type. The **`SEAM-29` half is a
convention, not a code edge**. The construction pattern 2b's new types must follow (`OperationDescriptor` as a
`sealed record`, per-member invariants in `field`-backed `init` accessors) is already written in design §4, and 2b
could follow it from the text. What 2a adds is the first built and tested instance of the pattern, which is a
convenience.

The full edge set, every one of them one-directional:

| 2b requirement | Needs from 2a | Kind |
|---|---|---|
| `SEAM-11`, `SEAM-18` (the options thread through the SPI and both bridges) | `HTTP-34`/`HTTP-35`'s `RequestOptions`, with its `Empty` instance | **dependency**: the type does not exist |
| `SEAM-27` ("the query MUST be RFC-3986 rendered") | `HTTP-28`–`HTTP-32`'s `Query` and its encoder | **convenience**: design §3.5 never names `Query`. It gives `OperationDescriptor` "four projection lists" and calls `Uri.EscapeDataString` "the right tool for **HTTP-29**, **HTTP-32** and path segments"; the only cross-reference runs the other way, from §4.4 to §3.5. Rendering through `Query` keeps one RFC 3986 renderer instead of two that could drift, which is a reason to prefer it, not a precondition |
| `SEAM-26`/`SEAM-27` (`BuildRequest` produces a `Request`) | `HTTP-7`'s validating constructor and the get-only `Request` of design §4.2 | **convenience**: "deriving a request cannot install a body-carrying GET" is the conformance clause of `SEAM-29`/`HTTP-2` (ch.02), both 2a rows. `SEAM-26` and `SEAM-27` say nothing about a body-carrying GET; `SEAM-26`'s conformance is only that "a parameterless GET … assembles the right request". `BuildRequest` inherits `HTTP-7` through the `Request` constructor it calls, whenever 2a lands it, and owes no row for it |
| `SEAM-26`'s descriptor itself | design §4's construction pattern | **convenience**: written down already |
| the transport and fake edits (`SystemNetHttpClient`, `TestSupport`, `AotSmoke`) | 2a's `Response(request, …)` constructor (`HTTP-4`, `HTTP-6`) and get-only `Request` touch the same files | **convenience**: parallel edits would conflict, not fail |

Nothing in 2a needs anything from 2b. The one hard edge, `RequestOptions`, is what makes any part of the order a
dependency. **2b's design must restate these edges, with their kinds, in its own Prerequisite section** rather than
inherit "2a first" by habit.

### What 2b is gated on, and what it may start at once

The rule against silent linear chains applies to the sub-phase boundary and inside 2b.

**Hard-gated on `RequestOptions` (`HTTP-34`, `HTTP-35`) being merged:** the SPI signature change and everything that
compiles against it. That covers `SEAM-11` (the options and token on both interfaces), `SEAM-13` (the token),
`SEAM-18` and `SEAM-25`'s bridge clauses (2b's, by [decision 1](#decisions-ruled-by-the-lead)), `DelegateHttpClient` (whose
delegate type carries `RequestOptions`), the `SEAM-16`/`SEAM-30` pins written against it, and the matching edits to
`SystemNetHttpClient`, `TestSupport`, `AotSmoke` and `PipelineRunner`.

**Free to start at once, with no edge into 2a:**

- **The serde seam (`SEAM-19`–`SEAM-23`).** `ISerde`'s only model type is `MediaType DefaultMediaType`, and 2a's
  `MediaType` work (`HTTP-24`, `HTTP-53`) changes its parsing, not its shape. The unsealed `SerdeException` root and
  the three derived profiles touch `Errors/`, `Serialization/` and their tests only.
- **The disposition rows** (`SEAM-1`–`SEAM-10`, `SEAM-12`, `SEAM-17`, `SEAM-24`, `SEAM-25`'s vacuous clause). They
  need a design argument and, where a gate or test already exists, a citation, not 2a's code.
- **The operation projection (`SEAM-26`–`SEAM-28`) and the `BaseAddress` wiring.** Each edge into 2a is a
  convenience: `Query` for a single renderer, and the final `Request` constructor and `Response` shape to avoid
  merge conflicts. 2b's plan may still prefer to land the projection after `Query` merges.

So 2b may open (brainstorm, design, plan and the free work) without waiting for 2a, and its SPI tasks wait only for
`RequestOptions`. Leaving 2b in its own sub-phase after 2a's letter costs nothing: splitting it further to free five
small MUSTs buys nothing on the schedule (see cut C below).

### Alternatives considered and rejected

**A — split 2a by size (headers and value types, then `Request`/`Response` and the new helpers).** 2a is 42 rows, and
the `Response` reshape ripples far: adding the `HTTP-6`/`HTTP-4` request parameter touches 145 `new Response(` sites
in `tests/`. But 42 rows is ordinary for this roadmap (3b is 49, 7b 41, 4c 40), and the split would be a strict line:

- `HTTP-46`'s request equality needs `HTTP-13`'s `Headers` value equality;
- `HTTP-7` needs `Method`'s body-forbidden classification;
- `HTTP-50`'s `RequestConditions` applies itself through the rebuilt `Headers.Set`.

A linear split buys only smaller pull requests, and roadmap step 5 already returns each sub-phase as its own stack.
Rejected. The ordering belongs in 2a's plan instead, stated below.

**B — 2a's rework in parallel with a sub-phase of new leaf types (`Query`, `RequestOptions`, `ETag`, `HttpRange`,
`RequestConditions`), with 2b after both.** This is the only candidate that buys real independence. The leaf types
share no code with the rework, and 2b's one hard edge (`RequestOptions`) and its `Query` preference are both leaves. It is rejected for three reasons:

- `HTTP-1`–`HTTP-5` and `SEAM-29` are construction rules over **every** model type, so each would need an owner on
  one side and a cross-reference on the other. That is the split-row pattern the one-row-per-ID convention exists
  to avoid.
- `HTTP-50`'s `RequestConditions` needs the rebuilt `Headers`.
- The critical path does not run through the leaves. Phase 3 waits on 2a's rework (`MediaType`, `Response`'s
  shape, the header predicate), not on `Query` or `ETag`.

What B would have bought is recovered more cheaply by gating 2b on `RequestOptions` alone and by a constraint on 2a's plan, below.

**C — the serde seam as an independent third sub-phase (2c).** It is honest: no edge ties it to 2a. It is rejected
on cost against benefit:

- it is five IDs of small work (a base exception, unsealing two types, three extension methods);
- 2b is not on phase 3's critical path, and if the `P2b --> P3` edge is a convenience, 2b has slack behind 3a and 3b,
  so freeing the serde cluster saves no calendar time;
- phase 7a re-enters the same types (`SERDE-9`–`SERDE-12`, `SERDE-26`), so a sub-phase of its own would be a third
  document set around one small, twice-touched surface.

If the lead wants the parallelism anyway, the cut is clean: every ID in `SEAM-19`–`SEAM-23` moves as a block, and
nothing else changes.

**D — 2b wholly first.** Impossible for the SPI work, which does not compile without `RequestOptions`. The rest of 2b may run first; see above.

### Constraints this document places on the sub-phase plans

- **2a's plan lands `RequestOptions` (`HTTP-34`, `HTTP-35`) first in its stack, and `Query` (`HTTP-28`–`HTTP-32`)
  early.** Neither has an edge into the rework. `RequestOptions` is 2b's only hard gate, so merging it first unblocks
  2b's SPI work. Landing `Query` early is a plan preference: it lets 2b's projection use one RFC 3986 renderer. The
  `Request`/`Response` reshape and 2b's SPI edits touch the same files, so the two plans sequence those changes to
  avoid merge conflicts. That is a convenience, not a gate.
- **2a's plan lands the `Headers` rebuild before the `Request`/`Response` reshape,** because `HTTP-46`'s equality
  needs it. It also lands the rebuild as its own reviewable change, because that change carries phase 1's S1
  obligations (constraint 5).
- **2b's plan may order the serde cluster and the disposition rows first.** It lands the SPI signature change
  (core, `SystemNet`, `TestSupport`, `AotSmoke` and `PipelineRunner`) as one change, because a half-changed seam
  does not compile.

---

## Scope: every ID, assigned exactly once

**71 IDs**: 41 `HTTP` plus 30 `SEAM`. The levels come from appendix C: **55 MUST, 13 SHOULD, 3 MAY**.

| | MUST | SHOULD | MAY | Rows |
|---|---|---|---|---|
| **2a**: `HTTP-1`–`HTTP-35` (35), `HTTP-46`–`HTTP-50` (5), `HTTP-53` (1), `SEAM-29` (1) | 33 | 8 | 1 | **42** |
| **2b**: `SEAM-1`–`SEAM-30` less `SEAM-29` | 22 | 5 | 2 | **29** |
| **Phase 2** | **55** | **13** | **3** | **71** |

**Reconciliation.**

- `HTTP` partitions with no residue: phase 2's 41 plus phase 3's 12 (`HTTP-36`–`HTTP-45`, `HTTP-51`, `HTTP-52`) is
  53.
- `SEAM`'s 30 are all here. `SEAM-5`/`SEAM-6`'s DI half is **carried** to phase 9 as a `⏳` on 2b's rows, not a
  second row.
- `SEAM-29` sits in 2a, as the roadmap's own reason implies. The roadmap's gap table places all five gap IDs in 2b,
  and they stay there.

**Two figures on the phase 2 card are prefix-wide, not in-scope.** "HTTP (43 MUSTs): 12 met, 15 partial, 15
missing" counts all 43 `HTTP` MUSTs, 11 of which are phase 3's. The in-scope `HTTP` MUSTs number **32**. Its
categories also sum to 42, not 43. "SEAM (23 MUSTs): 7 met, 4 partial, 2 missing, 9 N/A candidates" sums to 22.
The per-ID classification behind these aggregates (the gap analysis of 2026-09-27) is not in the tree. The "Today"
columns below are therefore this document's reading of the source at `2f08ec9` together with the design's **As
built** lines. Constraint 3 still applies: a row that is met today becomes ✅ only when a named test pins it.

### 2a — Domain model (42 rows)

Projects: `Dexpace.Sdk.Core` (`Http/`, and mechanically `Pipeline/` and `Pagination/`) and
`Dexpace.Sdk.Http.SystemNet` (see [Coupling](#coupling-with-later-phases)). User page:
`docs/sdk-documentation/http.md`.

| ID | Level | Design | Today at `2f08ec9` | Row expected at exit |
|---|---|---|---|---|
| `HTTP-1` | MUST | §4, §4.5 | partial: records and immutable collections; the `Request` `init` bypass; struct defaults on `Method` and `HttpHeaderName` | build → ✅ |
| `HTTP-2` | MUST | §4; §10 `no-builder-objects`, `construction-bypass` | partial: `with` bypasses the `Request` constructor | build → ✅, citing both §10 topics for the builder clause and the reflection residue |
| `HTTP-3` | MUST | §4; §10 `no-builder-objects` | partial: `Headers.ToBuilder` exists and does not alias; no `Query.Builder` | build → ✅, citing §10 for the non-multimap models |
| `HTTP-4` | MUST | §4, §4.2; §10 `no-builder-objects` | partial: `Response` requires neither request nor protocol | build → ✅; the message form cites §10 |
| `HTTP-5` | MUST | §4 | met, untested: immutable collections | pin → ✅ (the downcast probe) |
| `HTTP-6` | MUST | §4.2 | partial: `Response` has no `Request` and no `ReasonPhrase` | build → ✅ |
| `HTTP-7` | MUST | §4, §4.2 | missing: verified in §4 that `new Request(Method.Get, url, null, body)` succeeds | build → ✅ |
| `HTTP-8` | SHOULD | §4.2 | unrepresentable: `Method` is a required constructor parameter | **N/A candidate**, argued by §4.2 |
| `HTTP-9` | MUST | §4.3; §11 item 22 | diverges: public `IsSafe`/`IsIdempotent`, with TRACE counted idempotent; `Method.Of` accepts any non-blank token | build → ✅: an internal single-source set, no public accessor, a token-validated `Of` |
| `HTTP-10` | MUST | §4.3 | partial: `FromCode` is total; there is no `TryGetKnown` | build → ✅ |
| `HTTP-11` | MUST | §4.2, §4.3 | partial: no `IsError`; `Response` exposes only `IsSuccess` | build → ✅ |
| `HTTP-12` | MUST | §4.3 | met: `Status_EqualityIsByCode` | ✅ (cite) |
| `HTTP-13` | MUST | §4.1 | partial: the fold is `ToLowerInvariant`, and unvalidated lookups fold U+212A to `k`; `Headers` has no value equality or hash | build → ✅ (phase 1 routed the lookup fold here) |
| `HTTP-14` | MUST | §4.1 | met | pin → ✅ |
| `HTTP-15` | MUST | §4.1 | missing: `Set(string, string)` takes no `null` | build → ✅ |
| `HTTP-16` | SHOULD | §4.1 | missing: hash order | build → ✅ |
| `HTTP-17` | MUST | §4.1; §11 item 36 | ✅ phase 1 S1 | keep → ✅ (cite `HeaderInjectionValidationTests`) |
| `HTTP-18` | MUST | §4.1 | ✅ phase 1 S1 | keep → ✅ (same) |
| `HTTP-19` | MUST | §4.1 | ✅ phase 1 S1 (`Headers.Builder.AddInbound`) | keep → ✅; the rebuild carries `AddInbound` |
| `HTTP-20` | MUST | §4.1 | ✅ phase 1 S1 | keep → ✅ |
| `HTTP-21` | MUST | §4.1, §3.2 | partial: `HttpHeaderName` is a struct; names are lower-cased on the wire; no typed overloads | build → ✅, including a loopback proof of casing |
| `HTTP-22` | MAY | §4.1; §12 (deferred) | not built | `⏳`, owned by a `docs/first-release.md` "declined for v1" entry that 2a writes |
| `HTTP-23` | MUST | §4.4 | met | pin → ✅ |
| `HTTP-24` | MUST | §4.4, §3.1 | diverges: `charset=utf-7` throws `NotSupportedException` | build → ✅ |
| `HTTP-25` | MUST | §4.4 | met | pin → ✅ (ported round-trip table) |
| `HTTP-26` | MUST | §4.4 | ✅ phase 1 S1 | keep → ✅ |
| `HTTP-27` | SHOULD | §4.4 | met: `Of` rejects `*/json`; `Includes` | pin → ✅ |
| `HTTP-28`, `HTTP-29`, `HTTP-30`, `HTTP-31` | MUST (4) | §4.4, §3.5 | missing: no `Query` | build → ✅ |
| `HTTP-32` | SHOULD | §3.5, §4.4 | missing as an SDK surface (`Uri.EscapeDataString` is verified exact) | build → ✅ |
| `HTTP-33` | MUST | §4.3 | met: `Protocol_ParseAndWireRoundTrip` | ✅ (cite, widened to the aliases) |
| `HTTP-34`, `HTTP-35` | MUST (2) | §4.4 | missing: no `RequestOptions` | build → ✅ (early in the stack) |
| `HTTP-46` | MUST | §4.1, §4.2 | diverges: `Uri.Equals` equality, which ignores userinfo and fragment, and no header value equality | build → ✅; **2a's design must argue the body clause** (below) |
| `HTTP-47` | SHOULD | §4.2, §3.5 | partial: the message omits the input | build → ✅ |
| `HTTP-48`–`HTTP-50` | SHOULD (3) | §4.4 | missing | build → ✅ |
| `HTTP-53` | MUST | §4.4 | partial: `text/plain; a=` parses to an empty value | build → ✅ |
| `SEAM-29` | MUST | §4; §10 `no-builder-objects`, `construction-bypass`; §11 item 6 | partial, as `HTTP-2` | ✅ for immutability, factories and required fields; 🚫 for the generic `IBuilder<T>` clause, citing §10 `no-builder-objects` |

**What 2a's design must argue that the design chapters leave open:**

1. **`HTTP-46`'s "body by value".** Design §4.2 fixes URL equality (`AbsoluteUri`, ordinal) and §4.1 fixes header
   equality, but no section says how a `RequestBody`, an open abstract class with stream-backed single-use variants,
   compares by value. Ruby completed the same clause in its phase 3b by narrowing the body member.
2. **What `Headers` enumerates.** It could yield the folded or the original name. The `HTTP-21` wire emission and
   the as-built `Names` assertion in S1's tests (below) both depend on the answer.
3. **Where `HTTP-9`'s internal set lives.** Design §4.3 places it in `RetryFacts` (§6.1), which is 6a's. Removing
   the public `IsIdempotent` breaks `RetryPolicy` (`RetryPolicy.cs:171`), so 2a must create the single source now,
   and 6a grows it. TRACE stops being retried: a behaviour change with a test and a `CHANGELOG.md` line.
4. **`HTTP-8`'s N/A**, argued from §4.2's "unrepresentable rather than defaulted".

### 2b — Seams (29 rows)

Projects: `Dexpace.Sdk.Core` (`Client/`, `Serialization/`, `Errors/`, a new operation type, and
`Pipeline/PipelineRunner` for the signature), `Dexpace.Sdk.Http.SystemNet` (the signature only),
`tests/Dexpace.Sdk.TestSupport` and `tests/Dexpace.Sdk.AotSmoke`. User page: `docs/sdk-documentation/seams.md`.

| ID | Level | Design | Today at `2f08ec9` | Row expected at exit |
|---|---|---|---|---|
| `SEAM-1` | MUST | §2.4; §10 `logging-abstractions-dependency` | met: CI's `scripts/ci/dependency-audit.cs` | ✅ (cite the gate), with the §10 topic |
| `SEAM-2` | MUST | §2.3, §3.2; §11 item 3 | met: `Seam2ArchitectureTests` | ✅; **2b's design must argue `DelegateHttpClient`** (below) |
| `SEAM-3`, `SEAM-4` | MUST (2) | §3.1; §10 `byte-stream-provider-retired` | the seam is retired | **N/A candidates** |
| `SEAM-5`, `SEAM-6` | MUST (2) | §3.6; §10 `no-provider-registry`; §11 item 14 | no registry; the transport is a required parameter | DI-less half argued in §3.6; **DI half `⏳` against phase 9** |
| `SEAM-7`, `SEAM-9` | MUST (2) | §3.6; §10 `no-provider-registry` | no discovery, install or swap state | **N/A candidates** |
| `SEAM-8`, `SEAM-10` | SHOULD (2) | §3.6; §12 (vacuous) | no antecedent | N/A, vacuous |
| `SEAM-11` | MUST | §3.2 | partial: no options or token; sync-over-async | signature → ✅ for the contract; **`⏳` against 8b** for the real synchronous path (coupling obligation 5) |
| `SEAM-12` | MUST | §3.2, §1 | met by construction, untested | 2b documents the contract; proof per transport `⏳` against 8a's kit |
| `SEAM-13` | SHOULD | §3.2, §3.3 | missing: the synchronous seam has no token | 2b adds the token; honouring it in blocking I/O is `⏳` against 8b |
| `SEAM-14` | MUST | §3.7; §10 `cooperative-cancellation` | partial: ownership-aware, untested; no latch | 2b pins ownership; the idempotence latch is `⏳` against 3b's dispose latches (roadmap phase 3 card), a different latch from `SEAM-15`'s; clause (3) cites §10 |
| `SEAM-15` | MAY | §3.2, §3.7 | not built | `⏳` against 8b's `ObjectDisposedException` latch (roadmap phase 8 card), which is not 3b's idempotence latch of `SEAM-14`; 2b documents the choice on the seam (gap ID) |
| `SEAM-16` | MUST | §3.3 | met structurally: a non-nullable `Task<Response>`, and `HttpPipeline` rejects a null | pin → ✅ with a `DelegateHttpClient` returning `null` |
| `SEAM-17` | SHOULD | §3.3 | met: `Task<Response>` is the pivot; the adapter-module sentence has nothing to apply to (P4) | ✅ for the pivot sentence, citing §3.3; the second sentence per the [N/A table](#na-candidates-and-who-must-argue-each) |
| `SEAM-18` | MUST | §3.3, §5.3; §10 `cooperative-cancellation` | diverges: `Task.Run` on the shared pool, no token into `Execute`, `AsBlocking` drops the token, both bridges dispose what they wrap | build → ✅ in 2b ([decision 1](#decisions-ruled-by-the-lead)): a caller-supplied `TaskScheduler` with no default, the token and options threaded through, no dispose of the wrapped client; 4c's `PIPE-33`/`PIPE-34` cite these tests |
| `SEAM-19` | MUST | §3.4 | met: `ISerde.DefaultMediaType` is required, with no default | pin → ✅ |
| `SEAM-20` | MUST | §3.4 | partial: the stream and buffer profiles only | build → ✅ (gap ID) |
| `SEAM-21` | MUST | §3.4 | met: reified generics; the STJ adapter leaves the stream open | pin → ✅ |
| `SEAM-22` | MUST | §3.4 | vacuous for the generic API; no `Type` overload exists | **N/A candidate**, but see below (gap ID) |
| `SEAM-23` | MUST | §3.4 | diverges: no `SerdeException` base; both subtypes sealed | build → ✅ in 2b: the root, with both subtypes unsealed and re-parented under it ([decision 2](#decisions-ruled-by-the-lead)) (gap ID) |
| `SEAM-24` | SHOULD | §1, §8.1 | `ExecutionContext` flows across the bridge; no adapter modules | ✅ for the bridge; the adapter clause is vacuous |
| `SEAM-25` | MUST | §3.3, §3.7, §5.3 | no executor-owning adapter; the bridges dispose the client they wrap (an `XCUT-22` failure) | the first sentence is vacuous; the caller-executor clause builds with the bridges in 2b (decision 1) |
| `SEAM-26` | MUST | §3.5 | not built | build → ✅ |
| `SEAM-27` | MUST | §3.5; §11 item 35 | not built | build → ✅ |
| `SEAM-28` | MAY | §3.5 | not built | 2b carries the id on the descriptor and proves it never reaches URL, headers or body; attaching it to the context chain is `⏳` against 4a (gap ID) |
| `SEAM-30` | MUST | §3.3; §11 item 5 | structurally free: no `TaskCompletionSource` in core; the producer-internal half is S9's guard | pin → ✅ with a bridge test; cite `MalformedContentTypeWireTests` |

2b also **wires `DexpaceClientOptions.BaseAddress`** into the projection (design §3.5). It does not re-ban
`Uri.ToString()`, which phase 0 already banned; it only confirms that no `RS0030` waiver has crept in.

**What 2b's design must argue that the design chapters leave open:**

1. **`DelegateHttpClient` against `SEAM-2`'s "the core depends on [the seam] but never implements [it]".** Design
   §3.2 introduces a core-shipped implementation of both transport interfaces. PIPE-26 (a pipeline that implements
   the SPI) and the as-built private bridges already sit in the same tension. The reading that dissolves it is that
   a delegate adapter performs no I/O and embeds no transport (`SEAM-1`'s concern). No §11 item states that
   reading. It becomes a §11 candidate through 2b's ledger.
2. **`SEAM-22` as N/A versus ✅ by construction.** Design §3.4 argues vacuity for the generic API. But `SEAM-22` is
   neither in §12's vacuous list nor under a §10 topic, and the roadmap's exit asks every `SEAM` N/A to cite §3.1,
   §3.6 or a §10 topic. 2b decides between an N/A with a ledger entry and a ✅ whose test pins the closed-type
   binding (plus the `ContainsGenericParameters` rejection, if a `Type` overload is added).
3. **`SEAM-24` and `SEAM-25`'s vacuous clauses.** These are argued in §1, §3.3 and §3.7, which are also outside the
   exit criterion's list of citable sections. The same treatment as item 2 applies.
4. **Where per-call options come from inside the pipeline.** The as-built runner calls
   `ExecuteAsync(context.Request, context.CancellationToken)`. After the signature change it must pass
   `RequestOptions.Empty`, because carrying a caller's `RequestOptions` on the call-scoped context is 4c's
   (`PIPE-26`, design §5.3). 2b states that, and `SEAM-11`'s "a transport that ignores options MUST behave
   identically" is what makes it safe.

### Exclusions: IDs a reader would expect here, and their owners

| Excluded | Owner |
|---|---|
| `HTTP-36`–`HTTP-45`, `HTTP-51`, `HTTP-52`: numbered into ch.06 | phase 3 (`HTTP-44`/`HTTP-45`'s work in 7a) |
| `SERDE-1`–`SERDE-12`, which restate `SEAM-19`–`SEAM-23` at codec depth | 7a, which cites 2b's rows for the seam-level clauses |
| `PIPE-26`, `PIPE-33`, `PIPE-34`: the pipeline as a transport, and the bridges as pipeline requirements | 4c |
| `TRANSPORT-5` (the per-call timeout), `TRANSPORT-10`–`TRANSPORT-14` (header mapping), `ASYNC-13`/`ASYNC-14`/`ASYNC-19` | 8b / 8a |
| `XCUT-15`, `XCUT-18`, `XCUT-22`, `XCUT-23`, which phase 2's models and seams make satisfiable | 10 |
| `CFG-8`/`CFG-9`: `BaseAddress`'s declaration as an immutable options record | 5a |

---

## Gap IDs: what appendix C alone requires

All five are 2b's, as the roadmap's table says. Each was read from its appendix-C row, because no chapter states it:

- **`SEAM-15` (MAY).** Post-close sends may be undefined, and "a port MAY choose a mode but SHOULD document it".
  Design §3.2/§3.7 choose `ObjectDisposedException`. The latch is 8b's to build, and 2b documents the mode on both
  interfaces. `DelegateHttpClient`'s no-op dispose makes its own post-dispose mode "keeps working", and that is
  documented too.
- **`SEAM-20` (MUST).** Four allocation profiles. The streaming and buffer variants never close the target. An
  encode failure surfaces as the serialization subtype, chaining its cause. A genuine stream-write `IOException`
  propagates unwrapped. A fixed-buffer overflow raises a bounds error: `ArgumentOutOfRangeException` per §3.4,
  deliberately not an `SdkException`.
- **`SEAM-22` (MUST).** A generic capture must bind a concrete argument, and one bound to an unresolved type variable
  is rejected. On .NET this is vacuous for `DeserializeAsync<T>`; see 2b's item 2 above.
- **`SEAM-23` (MUST).** A stable, SDK-owned, **open** hierarchy: a base with encode and decode subtypes, thrown
  instead of the codec's own exception, always chaining the cause, and unchecked (free on .NET). The as-built pair
  is `sealed` under `SdkException` with no base.
- **`SEAM-28` (MAY).** An optional operation id, "attached to the request's context chain but MUST NOT affect the
  assembled request's URL, headers, or body". The MUST NOT is 2b's to prove. The attachment needs 4a's context
  chain.

---

## N/A candidates, and who must argue each

An N/A candidate is not N/A until a design section argues it (constraint 3). The roadmap's phase-2 exit narrows the
citable sources for `SEAM` to §3.1, §3.6 or a §10 topic.

| Candidate | Sub-phase | Argued by | Inside the exit criterion's list? |
|---|---|---|---|
| `HTTP-8` | 2a | §4.2 (unrepresentable) | n/a (`HTTP`) |
| `SEAM-3`, `SEAM-4` | 2b | §3.1; §10 `byte-stream-provider-retired` | yes |
| `SEAM-5`, `SEAM-6` (DI-less half; DI half `⏳` 9) | 2b | §3.6; §10 `no-provider-registry` | yes |
| `SEAM-7`, `SEAM-9` | 2b | §3.6; §10 `no-provider-registry` | yes |
| `SEAM-8`, `SEAM-10` (vacuous SHOULDs) | 2b | §3.6; §12 | yes |
| `SEAM-22` | 2b | §3.4 | **no**: 2b's design decides |
| `SEAM-24`'s adapter clause, `SEAM-25`'s first sentence, `SEAM-17`'s second sentence | 2b | §1, §3.3, §3.7 | **no**: 2b's design decides |
| `SEAM-29`'s generic-builder clause (🚫, not N/A) | 2a | §4; §10 `no-builder-objects` | yes (a §10 topic) |
| `SEAM-14`'s clause (3) and `SEAM-18`'s interrupt clause (mechanism retired) | 2b | §3.3; §10 `cooperative-cancellation` | yes |

The §10 topic labels used here follow the roadmap's rule of citing a deviation by topic, never by entry number, and
name these §10 entries:

- `logging-abstractions-dependency`: "Core takes a runtime dependency on
  `Microsoft.Extensions.Logging.Abstractions`";
- `byte-stream-provider-retired`: "The byte-stream provider seam is retired; its behavioural contract is not";
- `cooperative-cancellation`: "Cancellation is a cooperative `CancellationToken`; interrupts … have no counterpart";
- `no-provider-registry`: "No provider registry or auto-discovery";
- `no-builder-objects`: "No builder objects beyond the multimap builders, and no generic `IBuilder<T>`";
- `construction-bypass`: "Reflection and open body subclassing can bypass construction-time validation".

---

## Phase 1's routed items, and the Security tests (constraint 5)

**Routed to 2a** by the roadmap's 2026-09-29 phase 1 exit note and S1's `⏳` column:

- the ordered, casing-preserving `Headers` (`HTTP-16`, `HTTP-21`), with value equality (`HTTP-13`);
- the `System.Text.Ascii` fold for lookups as well as writes;
- `HttpHeaderName` as a `sealed record` with typed overloads;
- **one public syntax predicate the adapter can call**, which retires the two predicates `SystemNetHttpClient`
  restates today (`SystemNetHttpClient.cs:366`–`:377`).

Nothing is routed to 2b. S1's other `⏳` items, `TRANSPORT-12`'s silent-drop clause and `TRANSPORT-13`/`TRANSPORT-14`,
are 8b's.

**Security classes 2a must keep green.**

- **By behaviour**, as the status note names them: `HeaderInjectionValidationTests` and `HeaderInjectionWireTests`.
- **One assertion there encodes the as-built defect.**
  `Surrounding_whitespace_is_trimmed_from_a_name_before_validation` asserts
  `Assert.Equal("x-trace", Assert.Single(headers.Names))`, which is the lower-casing that `HTTP-21` removes. If 2a's
  design makes `Headers` enumerate original casing, that assertion's expected value becomes `"X-Trace"`. That is a
  changed expected value that leaves the security property (the name is trimmed, then validated) intact, not a
  loosening, and it goes into 2a's checklist's "existing assertions changed" table, as phase 1 did for its own.
- **By mechanical edit**, where semantics must not move:
  - `EnsureSuccessErrorMappingTests`, `RetryPacingOverflowTests`, `AuthHttpsGuardTests`,
    `RedirectCredentialHygieneTests` and `ReDriveRequestIsolationTests` (`new Response(…)` gains its request; four
    sites in the last, lines 36, 55, 74 and 94);
  - `ReDriveRequestIsolationTests` (its `context.Request with { Headers = … }` becomes a `With*` call once `Request`
    is get-only);
  - `MediaTypeTryParseTests` and `MalformedContentTypeWireTests` (the `HTTP-24`/`HTTP-53` parser changes must leave
    every row's outcome as it is).

Outside the `Security` classes, `MethodAndStatusTests.Method_SafetyAndIdempotency` (a `Unit` test, so constraint 5
does not apply) is rewritten against the internal idempotent set, and its `IsSafe` assertions are removed, because
`HTTP-9` allows "no separate safe-method classification".

**Security classes 2b must keep green.** The four wire classes (`FramingHeaderDropWireTests`,
`HeaderInjectionWireTests`, `RedirectWireTests` and `MalformedContentTypeWireTests`) call
`transport.ExecuteAsync(request, ct)`. Design §3.2's option-less **extension method**, deliberately not a default
interface member, keeps those calls compiling unchanged. They should need **no** edit, and a 2b change that forces
one is a signal to re-read §3.2. The core `Security` classes that drive `ScriptedTransport` change only through the
fake's signature.

---

## As-built facts at `2f08ec9` that the roadmap does not state

1. **2a touches `Dexpace.Sdk.Http.SystemNet`, which the phase-2 row says only 2b does** ("for the SPI signature change
   only"). The adapter builds `new Response(status, headers, body, protocol)` (`SystemNetHttpClient.cs:332`), which
   gains its request. It restates the header predicates that 2a makes public. And it is where `HTTP-21`'s original
   casing reaches the wire. The row is corrected in place (decision 3).
2. **2a touches `Pipeline/` and `Pagination/`.** `Request` `with` expressions stop compiling when `Request` becomes
   get-only: ten in `src/` outside `Request.cs`, in seven files, plus nineteen in nine test files
   (`ApiKeyAuthPolicyTests` ×5, `BasicAuthPolicyTests` ×4, `BearerTokenAuthPolicyTests` ×4, and one each in
   `SetDatePolicyTests`, `ClientIdentityPolicyTests`, `IdempotencyPolicyTests`, `DexpacePipelineTests`,
   `PageableTests` and `ReDriveRequestIsolationTests`). The `src/` sites are `AuthorizationPolicy.cs:80` and `:96`,
   `ClientIdentityPolicy.cs:27`, `IdempotencyPolicy.cs:65`, `InstrumentationPolicy.cs:125`, `RedirectPolicy.cs:162`,
   `SetDatePolicy.cs:42`, and `PaginationStrategies.cs:55`, `:94` and `:147`. Separately, `RetryPolicy.cs:171` reads
   the public `IsIdempotent` that `HTTP-9` removes. These are mechanical
   edits to the policies, not the policy rework, which remains 4c's and 6a's.
3. **The `Response` reshape is the largest ripple in phase 2:** 145 `new Response(` sites in `tests/`, plus 2 in
   `src/`.
4. **The phase-2 `sdk-design refs` cell is narrower than the IDs it owns.** It lists §3.4–§3.6 and §4–§4.4, but the
   `SEAM` rows are argued in §3.1 (`SEAM-3`/`SEAM-4`), §3.2/§3.3 (`SEAM-11`–`SEAM-18`, `SEAM-30`), §3.7 (`SEAM-14`,
   `SEAM-15`, `SEAM-25`) and §1 (`SEAM-24`). The phase-2 card itself cites §3.2 and §3.3. The cell is corrected in
   place (decision 3).
5. **`SerdeException` does not exist.** The failure pair is `sealed` in `Errors/SerializationExceptions.cs`. The root
   is already 2b's: the phase 2 card says 2b "adds … the unsealed `SerdeException` root" (the phase 2 card), and the
   gap table gives `SEAM-23` to 2b. The 7a card's bullet read "`SerdeException` re-parenting", which left open only
   who re-parents the two existing sealed exceptions. Decision 2 gives that to 2b, and the 7a bullet is corrected.
6. **The `Uri.ToString()` ban is done** (see Prerequisites).
7. **The roadmap's "ch.04 — `HTTP-1`–`HTTP-35` …" is loose.** `HTTP-1` and `HTTP-2` are stated in ch.02, not ch.04.
   It is unbackticked, so the probe's `chapters` check does not fire, and the design documents should not copy it.
   The cell is corrected in place, with the card's prefix-wide counts under Scope (decision 3).

---

## Porting from the siblings (constraint 10)

Neither sibling segmented its phase 2: each built `HTTP` as its phase 1 and `SEAM` as its phase 2
(`ruby-sdk@5b17395` `docs/work/mvp/phase2/2026-09-06-phase2-seam-foundations-design.md`, `nodejs-sdk@54aeed4`
`docs/work/mvp/phase2/2026-07-23-phase2-seam-foundations-design.md`). Their order, `HTTP` before `SEAM`, is 2a
before 2b. The **method** of this document is taken from `ruby-sdk@5b17395`
`docs/work/mvp/phase3/2026-09-08-phase3-segmentation-design.md` (named edges, rejected cuts, constraints on the
plans) and `nodejs-sdk@54aeed4` `docs/work/mvp/phase6/2026-07-28-phase6-segmentation-design.md` (a sizing case
against the spec's own boundaries). Their text is not.

Every ported test cites its source path and sha in a header comment. Host-language facts are never ported: Ruby's
encoding-tag cases, `*_bare_require_test.rb`, `matrix_facts_test.rb`, and Ruby's `registry*_test.rb`, whose
discovery apparatus is retired here.

**2a ports**, as case tables into `[Theory]`/`MemberData`:

- `nodejs-sdk@54aeed4` `packages/core/src/http/{headers,media-type,query-params,rfc3986,etag,http-range,request-conditions,request-options,method,status,protocol,request,response}.test.ts`;
- `ruby-sdk@5b17395` `gems/dexpace-core/test/dexpace/http/{headers_test,header_name_test,media_type_test,method_test,protocol_test,query_test,percent_encoding_test,request_test,request_options_test}.rb`
  and the `{headers,query,request,request_options}/builder_test.rb` files;
- `model_test.rb`, for the uniform required-field failure (`HTTP-4`), less its generic-builder cases, which §10
  `no-builder-objects` retires.

The header-syntax tables are **already ported** by S1 into `HeaderInjectionValidationTests` and are not ported
twice.

**`tests/vectors/http/*.json` candidates:** media-type parse and render (`HTTP-23`, `HTTP-25`, `HTTP-53`), query
encode and parse (`HTTP-29`–`HTTP-32`), RFC 3986 component encoding, `ETag` (`HTTP-48`) and `HttpRange`
(`HTTP-49`). 2a's design decides which it extracts; the tables are large enough to earn it, and each is upstreamable.

**2b ports:**

- `ruby-sdk@5b17395`:
  - `gems/dexpace-core/test/dexpace/operation_build_request_test.rb` and `operation_test.rb` (`SEAM-26`–`SEAM-28`;
    the composition table is a JSON-vector candidate);
  - `transport_test.rb`, `async_transport_test.rb` and `seam_surface_test.rb`;
  - `bridge/{sync_over_test,async_over_test}.rb` (`SEAM-18`);
  - `closeable_test.rb`, for ownership only (the latch is 3b's);
  - `serde_test.rb` and `serde/{error_test,serialization_error_test,deserialization_error_test}.rb` (`SEAM-20`,
    `SEAM-23`).
- `nodejs-sdk@54aeed4`: `packages/core/src/seams/{transport,serde,operation}.test.ts` and
  `packages/core/src/serde/errors.test.ts`.

**User pages:** `ruby-sdk/docs/sdk-documentation/{http,seams}.md` and
`nodejs-sdk/docs/sdk-documentation/{http,errors}.md`, as the page structure for `http.md` (2a) and `seams.md` (2b).

---

## Coupling with later phases

Each item is something 2a or 2b fixes **now** that a later phase builds on. Changing it later is a cross-phase
change, not a local one.

- **Constraint 8, pre-1.0 breaking changes, every one visible.** Each pull request carries its `RS0016`/`RS0017`
  diff in `PublicAPI.Unshipped.txt` (core and `SystemNet`) and a `CHANGELOG.md` `[Unreleased]` line.
  - 2a's breaking changes:
    - `Method` and `HttpHeaderName` go from struct to `sealed record`, which breaks binary compatibility;
    - the public `IsSafe`/`IsIdempotent` are removed;
    - `Request`'s `init` properties become get-only;
    - `Response`'s constructor changes;
    - `Headers.Set` takes `string?`.
  - 2b's breaking changes:
    - `IHttpClient.Execute` and `IAsyncHttpClient.ExecuteAsync` gain `RequestOptions` and `CancellationToken`,
      which is constraint 8's own named example;
    - the bridge signatures change (`AsAsync` takes a `TaskScheduler`; decision 1);
    - `SerializationException` and `DeserializationException` are unsealed and re-parented (decision 2).
- **Phase 3** (entry: "2a has exited") inherits from 2a:
  - `MediaType`'s charset lookup (`HTTP-24`), which is `HTTP-42`'s single decode boundary;
  - the public header predicate, which `HTTP-51`'s multipart part headers quote against;
  - `Response`'s request and reason phrase.

  3b owns the dispose latch that closes `SEAM-14`'s idempotence clause (`⏳` on 2b's row). The graph's
  `P2b --> P3` edge looks like a convenience (see Prerequisites).
- **Phase 4.**
  - 4c consumes 2b's SPI when `HttpPipeline` implements both interfaces (`PIPE-26`), and it must carry the caller's
    `RequestOptions` on the call-scoped context. Until then the runner passes `RequestOptions.Empty` (2b's item 4).
  - 4c's `PIPE-28` and 2b's `SEAM-11` are both `⏳` against 8b (coupling obligation 5; 2b marks its own).
  - 4c's `PIPE-33`/`PIPE-34` rows cite 2b's bridge tests (decision 1); 4c keeps the pipeline's own sync/async
    surfaces.
  - 4a owns `SEAM-28`'s attachment to the context chain.
  - `HTTP-9`'s internal idempotent set is the single source 6a's `RetryFacts` grows (2a's item 3), not a second set.
- **Phases 5 and 6.**
  - 5a turns `DexpaceClientOptions` into records (`CFG-8`, `CFG-9`), including `BaseAddress`, which 2b wires. The
    read site must survive the declaration change.
  - `HTTP-50` formats RFC 1123 dates with the in-box `"R"` format and does not wait for 5a's shared **parser**
    (`CFG-29`–`CFG-31`).
  - 6a is the consumer of `HTTP-35`'s "`MaxRetries = 0` disables retries for this call".
- **Phase 7.**
  - 7a builds on 2b's `SerdeException` root, the re-parented subtypes and the profiles (`SERDE-9`–`SERDE-12` cite
    them; decision 2). It also needs `Response`'s
    request and reason phrase for `HTTP-44`'s wrapper.
  - 7c may move pagination's private query reader onto 2a's `Query`, but `PAGE-21`'s byte-for-byte splice (a recorded
    §10 deviation) means that is 7c's decision, not an obligation 2a creates.
- **Phase 8.**
  - 8a drives the kit against 2b's `DelegateHttpClient` as its second driver (the roadmap's D3 proposal), so
    `DelegateHttpClient`'s shape is fixed by 2b.
  - 8b builds the real synchronous `Execute` (`SEAM-11`, `SEAM-13`), the `ObjectDisposedException` latch
    (`SEAM-15`), the per-call timeout from `RequestOptions.Timeout` (`TRANSPORT-5`), and header mapping over 2a's
    ordered, cased `Headers` and its public predicate. That mapping includes the method-token re-check that design
    §10's `construction-bypass` topic requires at the wire.
- **Phase 9** flips `SEAM-5`/`SEAM-6`'s DI half, and cites §3.6 for `SEAM-7`'s singleton reading.

---

## Decisions ruled by the lead

The draft of this document put three open questions to the lead. Each was ruled by the lead on 2026-09-29, and each
ruling is quoted verbatim below. The reasoning is kept, because the ruling adopted it.

1. **The client bridges, `HttpClientExtensions.AsAsync`/`AsBlocking` (`SEAM-18`, `SEAM-25`), are 2b's.** Ruled by the
   lead, 2026-09-29: "2b rewrites them (Recommended)".

   The question: the phase 4 card gave 4c "bridges that take a `TaskScheduler` and a token", and design §5.3 argues
   them as `PIPE-33`/`PIPE-34`. But 2b must edit that same file, because both bridges implement the interfaces whose
   signature it changes, and `SEAM-18` is a 2b row. So 2b does the whole rewrite once:
   - the caller-supplied `TaskScheduler` with no default;
   - the token and the options threaded into the wrapped call;
   - no dispose of the wrapped client.

   `SEAM-18` and `SEAM-25`'s bridge clause close in 2b, with no `⏳` against 4c. 4c's `PIPE-33`/`PIPE-34` rows stay in
   phase 4 and cite 2b's tests. The alternative, a 2b pass-through with `SEAM-18` `⏳` against 4c, would have
   rewritten the file twice.
2. **2b re-parents `SerializationException` and `DeserializationException` under the unsealed `SerdeException`
   root.** Ruled by the lead, 2026-09-29: "2b (Recommended)".

   The root itself was never in question: it was already 2b's (fact 5). 2b unseals and re-parents both, in the same
   change that adds the root. `SEAM-23` requires the base "with encode/decode subtypes", and a root with no subtypes
   under it cannot be proven against that row. Leaving the pair sealed under `SdkException` until 7a would also keep
   a public shape that 2b's own row calls non-conforming. `SEAM-23` closes in 2b, with no `⏳` against 7a. 7a's
   `SERDE-9`/`SERDE-10` rows stay in phase 7 and cite 2b's tests.
3. **The roadmap's wrong cells are corrected in place in this change.** Ruled by the lead, 2026-09-29: "Apply them
   in this PR (Recommended)".

   Each correction is stated and dated in the cell itself ("corrected 2026-09-29, per the lead's ruling …"), in the
   style of the roadmap's existing D2 corrections, under its rule for a wrong cell. No requirement ID changes its
   owning phase: only sub-phase work items move, and every ID row stays where the Phase List puts it.

   | Cell | Read | Now reads | Why |
   |---|---|---|---|
   | Phase List, phase 2, "Projects / packages" | "`Dexpace.Sdk.Core`, and `Dexpace.Sdk.Http.SystemNet` for the SPI signature change only" | "… for 2a's `Response` construction, public header predicate and wire casing and 2b's SPI signature change" | as-built fact 1 |
   | Phase List, phase 2, "sdk-design refs" | "§3.4, §3.5, §3.6, §4, §4.1–§4.4" (then this document's link) | the same, plus "§1, §3.1–§3.3, §3.7" (then the link) | as-built fact 4; the design citation is kept, not replaced |
   | Phase List, phase 2, "Product-spec refs" | "ch.04 — `HTTP-1`–`HTTP-35`, …" | "ch.02 and ch.04 — `HTTP-1`–`HTTP-35`, …" | as-built fact 7 |
   | Phase 2 card, Scope, `HTTP` and `SEAM` lines | "**HTTP** (43 MUSTs): 12 met, 15 partial, 15 missing." / "**SEAM** (23 MUSTs): 7 met, 4 partial, 2 missing, 9 N/A candidates." | the `HTTP` count stated as prefix-wide (32 in scope, 11 phase 3's), and both breakdowns' shortfall (42 of 43, 22 of 23) stated | the reconciliation under [Scope](#scope-every-id-assigned-exactly-once) |
   | Phase 2 card, 2a bullet | "drops TRACE from `Method.IsIdempotent`;" | "replaces the public `IsSafe`/`IsIdempotent` with one internal idempotent set that excludes TRACE (`HTTP-9`)" | `HTTP-9` removes the public accessor the old wording implied survives |
   | Phase 2 card, 2b bullets | the serde bullet named only the root, and no bullet named the bridges | the serde bullet adds the re-parenting, and a new bullet names the bridge rewrite | decisions 1 and 2; keeps the phase 2 card consistent with the corrected phase 4 and phase 7 cards |
   | Phase 4 card, 4c bullet | "bridges that take a `TaskScheduler` and a token." | "the pipeline's own sync/async surfaces; the client bridges are 2b's, and 4c's `PIPE-33`/`PIPE-34` rows cite 2b's tests" | decision 1 |
   | Phase 7 card, 7a bullet | "`SerdeException` re-parenting;" | "the `SERDE-9`/`SERDE-10` rows cite 2b's `SerdeException` hierarchy" | decision 2 |

   No status note is added. The roadmap's change rules make a dated in-place correction a complete record of a
   wrong cell, and a status note is required only for a phase exit, an inverted dependency edge or a finding routed
   to phase 11. None of the three rulings is any of those. The rulings are recorded here and in each corrected cell,
   which is where a later reader of either document looks.

---

## Deviation Ledger

None. This document opens no deviation. The candidates it names, the body clause of `HTTP-46` and the `SEAM-2`
reading of `DelegateHttpClient`, belong to 2a's and 2b's designs and ledgers (`P2a-<n>`, `P2b-<n>`, or `P2-<n>` if the
lead prefers one ledger for the phase).

None of the three rulings warrants an entry either. A deviation is a departure from the specification's letter or
mechanism (constraint 7, design §10). The rulings move work items between sub-phases (the bridges from 4c to 2b,
the serde re-parenting from 7a to 2b) and correct the roadmap's own cells. Every requirement is satisfied exactly as
before, and every ID keeps its owning phase.

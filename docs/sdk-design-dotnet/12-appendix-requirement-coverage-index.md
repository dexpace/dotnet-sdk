## 12. Appendix: Requirement Coverage Index

All 19 prefixes and 645 requirements, with the sections addressing each family and, by ID, every requirement this
document does not address. The index is derived mechanically: a requirement counts as *addressed* when chapters 1–11
cite its ID — in bold or in a heading, with a cited range such as **IO-19**–**IO-24** expanded to its members — and
every cited ID was checked to exist in `docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`.
The "Addressed in" column lists the sections that cite two or more IDs of the prefix, then a count of sections that
cite exactly one; §10 and §11 are omitted there, because they summarise arguments made elsewhere. A range counts every
member it spans, so a section that names **TRANSPORT-1**–**TRANSPORT-30** as the conformance kit's scope (§9.3)
addresses the family at the level of a gate, not requirement by requirement.

Four kinds of entry appear, and they are not interchangeable:

- **Not met on a stated domain** — a MUST clause this port leaves unmet on a domain it names, admitted under P8
  rather than argued away: **AUTH-15**'s MD5 algorithms where the host's crypto refuses MD5 (§10 entry 16),
  **PAGE-21**'s byte-for-byte splice for percent-encoded unreserved characters (§10 entry 18), **OBS-27**'s Datadog
  trace-id flavour (§10 entry 24), and the never-throw clauses of **CFG-5**–**CFG-7**, inverted into fail-fast startup
  validation (§10 entry 26). Two more are met in structured form only: **OBS-3**'s literal `null` in rendered message
  text (§10 entry 22) and the cooperative reading of **ASYNC-3**'s and **PIPE-33**'s interrupt mode, whose residual is
  a blocking transport that ignores its token (§10 entry 8). Every other §10 entry changes mechanism or packaging and
  argues that the guarantee is kept.
- **Vacuous** — a requirement whose antecedent this port never reaches, so the guarantee holds without an
  implementation: **ASYNC-4** (no interrupt is ever delivered, §3.3, §10 entry 8); **ASYNC-21**, adapter-scoped with
  no reactive adapter shipping, though its property is implemented on the pull-based path (§7.2, §11 item 21);
  **SEAM-8** and **SEAM-10** (§3.6, §10 entry 9); **CFG-4**'s raw accessor and **CFG-10**'s override removal (§8.2,
  §10 entry 25); **OBS-8**'s emit-once and **OBS-40**'s collision diagnostic (§8.1, §10 entry 22); and **PAGE-15**'s
  two-page case with **PAGE-12**'s look-ahead machinery (§7.1, §10 entry 17). A vacuous item is reported as vacuous
  by the conformance kit, never as passing (§9.3, §11 item 21).

  **Correction (2026-10-02): phase 2b extends the vacuous list for `SEAM`.** Four clauses of rows that are otherwise built
  describe an antecedent this port never reaches, and are vacuous, not N/A (P2b-3, §11 items 41–43): **SEAM-17**'s second
  sentence (no ecosystem facade module ships, because .NET has one async ecosystem, §3.3); **SEAM-21**'s no-codec clause
  (core ships no format-agnostic deserializer, §3.4); **SEAM-24**'s adapter-module clause (§3.3); and **SEAM-25**'s first
  sentence (no adapter owns an executor, §3.7). The conformance kit reports each as vacuous, never as passing.

  **Correction (2026-10-03): phase 3b extends the vacuous list for `BODY`.** One clause of a row that is otherwise built describes an antecedent this port never reaches: **BODY-32**'s
  capless-snapshot failure above `Array.MaxLength`. Both wrappers' captures are bounded by a cap that is itself clamped to `Array.MaxLength`, so a capture can never exceed it (phase 3b design, position F). The conformance kit reports it as vacuous, never as passing. **BODY-6** for the seekable variant is not a second case: its antecedent (a
  single-use body) is not reached, and the classification test covers it.
- **Deferred or declined** — a SHOULD or MAY the port does not implement for the first release: **HTTP-22** (MAY,
  name interning, §4.1); **BODY-36** (MAY, memory-mapped view, offered only on demand, §3.1); **RETRY-38** and
  **RECOV-31**, one feature under two IDs (§11 items 10 and 20); **CFG-13**'s global slot (SHOULD, declined, §8.2,
  §10 entry 25); **CFG-20**'s interruptible-task future (SHOULD, retired, §8.3, §10 entry 8); and the four MAYs in
  the not-cited list below (**RETRY-29**, **RETRY-43**, **REDIR-27**, **SSE-41**). Deferred means not implemented,
  never reinterpreted.
- **Not cited by ID** — the 36 requirements listed per prefix below. For most of them the design states the
  substance without naming the ID, and the row says where; the rest are genuinely unaddressed and are roadmap
  inputs. None of them is claimed to be met by this document.

What this index does **not** claim: that a cited requirement is implemented. Most sections end with an **As built
(d45e64b)** line recording that a designed behaviour is partial or not built; this index maps the *design*. And, as
§9.3 and §11 item 9 say, Appendix B covers nine of nineteen prefixes, so neither Appendix B nor this table is by
itself a conformance claim.

| Prefix | Count | Cited | Addressed in | Not cited by ID / notes |
|---|---|---|---|---|
| SEAM | 30 | 30 | §1, §3.1–§3.8, §9, §9.2 (+6 single-ID sections) | *Notes:* the byte-stream provider and its apparatus (**SEAM-3**–**SEAM-10** as applied to it) retire (§10 entry 3); discovery retires for the surviving seams too (§10 entry 9); **SEAM-15** (MAY) taken — a send after dispose throws (§3.7); **SEAM-17**'s pivot is `Task<Response>` and its adapter apparatus collapses (§3.3) |
| HTTP | 53 | 52 | §1, §3.1, §3.2, §3.5, §3.7, §4, §4.1–§4.4, §7.3 (+3) | **HTTP-40** (MUST, file-body fail-fast validation) — substance in §3.1 under **BODY-11**. *Deferred:* **HTTP-22** (MAY). *Notes:* **HTTP-9**'s set is an internal constant with no public accessor (§4.3, §11 item 22); **HTTP-2**/**HTTP-3**/**HTTP-4** builder clauses (§10 entries 10, 11) |
| IO | 42 | 41 | §3.1 | **IO-7** (MUST, the `Buffer` as a FIFO source-and-sink) — no `Buffer` type ships (§3.1, §10 entry 3), and the retirement is not argued by ID *(closed by dated correction, 2026-10-03, phase 3a: argued in the phase 3a design, position C, with **IO-10** in §11 item 44)*. *Notes:* **IO-30**–**IO-36**, **IO-39** retire with the provider (§10 entry 3); the −1 sentinel family is §10 entry 4 |
| BODY | 37 | 35 | §1, §3.1, §3.7, §5.1 (+5) | **BODY-1** (MUST, the replayability property) — substance in §3.1 (`IsReplayable`); **BODY-33** (SHOULD, non-consuming error preview) — unaddressed *(closed by dated correction, 2026-10-03, phase 3b: met by construction, because the exception's error body is the replayable bytes body of phase 1's S8, and pinned by `ErrorBodyPreviewTests`)*. *Deferred:* **BODY-36** (MAY). *Notes:* **BODY-8** is §10 entry 5, **BODY-14** §10 entry 6 |
| CTX | 20 | 20 | §5.4, §8.1 | *Notes:* the store is kept with a .NET reader (§11 item 30); **CTX-14**/**CTX-15** are `InstrumentationContext` (composing `ActivityContext`) and its `None` default; see the phase 4a design |
| PIPE | 40 | 36 | §3.3, §5.1, §5.3 (+3) | **PIPE-1**, **PIPE-7**, **PIPE-13** and **PIPE-39** are cited by ID in the phase 4c design's decision table and checklist (*dated correction, 2026-10-07, phase 4c*); the substance was already in §5.1. *Notes:* **PIPE-32** reversed (§10 entry 14); **PIPE-33**/**PIPE-34** (§10 entry 8); **PIPE-30** (§10 entry 12) |
| RECOV | 34 | 21 | §5.1, §5.2 (+2) | **RECOV-18**–**RECOV-24**, **RECOV-26**–**RECOV-29** (MUST) and **RECOV-25**, **RECOV-30** (SHOULD) — the recovery-chain retry engine's rows, stated in appendix C only (§11 item 33). The engine is designed only as "the recovery-chain retry with its deadline-shrinking budget" (§6.1, not built); its stage-stack twins are cited as **RETRY-5**, **RETRY-9**, **RETRY-10**, **RETRY-15**, **RETRY-18**, **RETRY-20**, **RETRY-22**, **RETRY-26**, **RETRY-27**. *Deferred:* **RECOV-31** (MAY; ⏳ 6a with **RETRY-38**, P4b-2). *Notes:* **RECOV-2** is §10 entry 12; **RECOV-17** is §10 entry 7; every **RECOV-17**–**RECOV-34** citation is against appendix C only |
| RETRY | 45 | 35 | §1, §5.1, §5.2, §6.1 (+3) | **RETRY-3** (MUST, a response-bearing exception's flag from the classifier) — substance in §6.1 (the baked flag of **XCUT-5**); **RETRY-8** (MUST, both gates) — substance in §6.1's retry-safety paragraph; **RETRY-16** (MUST, total pacing parser) — substance in §6.1 ("unparseable yielding no hint"); **RETRY-21** (MUST, pacing precedence) — partly, through **RETRY-39** in §6.1; **RETRY-24** (MUST, a read timeout is not cancellation) — substance in §5.2, §6.1 and §11 item 32; **RETRY-31** (MUST, non-blocking async delays) — substance in §6.1's wait; **RETRY-42** (MUST, immutable, stateless configuration) — partly, in §6.1's construction-time validation; **RETRY-40** (SHOULD, throwing overrides) — unaddressed. *Deferred:* **RETRY-29** (MAY), **RETRY-38** (§11 item 10), **RETRY-43** (MAY). *Notes:* neither unification sanction is invoked (§6.1, §11 item 19) |
| REDIR | 28 | 26 | §1, §6.2 (+3) | **REDIR-24** (MUST, the redirect loop wraps auth) — substance in §6.2 (stage order REDIRECT → RETRY → AUTH, auth per hop). *Deferred:* **REDIR-27** (MAY, configurable target header). *Notes:* **REDIR-11**'s marker retires (§10 entry 15); **REDIR-25** is 🚫 by §10 entry 14. *Correction 2026-10-08 (phase 6b):* all 26 are built and evidenced in the 6b checklist; the count is unchanged |
| AUTH | 38 | 37 | §1, §6.3 (+3) | **AUTH-26** (MUST, static key stamping with an optional prefix) — unaddressed; the as-built `ApiKeyAuthPolicy` exists and is not reconciled against it. *Notes:* **AUTH-15**/**AUTH-16** on a FIPS host (§10 entry 16); no loopback exemption from the HTTPS guard (§11 item 25) |
| PAGE | 36 | 35 | §1, §3.7, §7.1 (+1) | **PAGE-7** (MUST, forward-only, on-demand fetching; idempotent end probes) — substance in §7.1 (`null` as the single end signal), not argued by ID. *Notes:* §10 entries 17, 18, 19. *Correction 2026-10-09 (phase 7c):* 33 are built and evidenced in the 7c checklist (**PAGE-1**, **PAGE-2**, **PAGE-4**–**PAGE-28**, **PAGE-31**–**PAGE-36**), 3 are 🚫 (**PAGE-3** by entry 17, **PAGE-29** and **PAGE-30** by entry 19); the count is unchanged, and **PAGE-19**'s cross-origin reading is §11 item 66 |
| SSE | 41 | 40 | §1, §7.1, §7.2 (+5) | *Not applicable:* **SSE-41** (MAY, reactive-adapter error latitude) — no reactive adapter ships, so the clause is adapter-scoped and vacuous (§11 item 21), reported as that and never as satisfied or deferred (*dated correction, 2026-10-09, phase 7b*). *Notes:* no strict-WHATWG mode (§11 item 17); **SSE-19**'s cap is sanctioned (§10 entry 20) |
| SERDE | 30 | 30 | §1, §3.4, §7.3 (+3) | *Notes:* **SERDE-4**'s fixed-buffer profile is met by a core derivation (§3.4, §7.3); **SERDE-14**'s covariance SHOULD is §10 entry 21. *Correction 2026-10-09 (phase 7a):* all 30 rows are built and cite the [7a checklist](../work/mvp/phase7/phase7a/2026-10-09-phase7a-serde-checklist.md) (`HTTP-44` and `HTTP-45` are carried there from 3b); **SERDE-13** is read as the non-null overloads (P7a-9, open) |
| OBS | 40 | 40 | §8.1, §9.3 | *Notes:* §10 entries 22, 23, 24; **OBS-32**'s naming conflict is §11 item 38. *Correction 2026-10-07 (phase 5b):* the roadmap's 5b owns 28 rows and 5c 12; the HTTP events are `ILogger.Log<TState>` records for the header-bearing pair and `Define` for the fixed-key ones (§8.1, P5b-3); **OBS-19** is ⏳ 8b and **OBS-35** ⏳ 9. *Correction 2026-10-07 (phase 5c):* `OBS-21`-`OBS-23` and `OBS-25`-`OBS-33` are built (§8.1, §10 entries 23 and 24, §11 item 38); the operation span opens at call entry (P5c-2), success leaves its status `Unset` (P5c-5), `OBS-27`'s zero-draw clause is admitted (P5c-14), `OBS-28`'s byte-count milestones are not emitted |
| CFG | 38 | 38 | §6.1, §8.2, §8.3 (+3) | *Deferred:* **CFG-13** (declined), **CFG-20** (retired). *Notes:* §10 entries 25, 26; **CFG-34**'s floating-point clause is live on .NET (§11 item 15). *Phase 5a (2026-10-07):* **CFG-12** is N/A (no configuration builder, §11 item 51); **CFG-21** is built as the internal `LateResult` |
| TRANSPORT | 30 | 30 | §3.2, §3.3, §3.7, §9.3 (+5) | *Notes:* **TRANSPORT-3**'s interrupt shape is §10 entry 8 and **TRANSPORT-20**'s I/O family §10 entry 7; **TRANSPORT-18** binds the SystemNet transport (§11 item 18) |
| ASYNC | 22 | 22 | §1, §3.3, §3.7, §8.1, §8.3, §9.3 (+3) | *Vacuous:* **ASYNC-4**, **ASYNC-21**. *Notes:* **ASYNC-3** is met cooperatively (§10 entry 8) |
| XCUT | 24 | 24 | §1, §3.2, §3.3, §3.7, §3.8, §4, §5.2, §6.1, §6.3, §8.1 (+11) | *Notes:* **XCUT-4**'s I/O family (§10 entry 7, §11 item 23); **XCUT-20** versus **OBS-30** (§11 item 37) |
| NFR | 17 | 17 | §1, §2.3, §2.4, §9, §9.1–§9.3 (+8) | *Notes:* **NFR-1** is §10 entry 1 and **NFR-2** §10 entry 2; **NFR-8** applies literally on .NET and is gated by `IsTrimmable`/`IsAotCompatible` and a NativeAOT smoke consumer (§9.2) |

**Totals: 645 requirements, 609 cited, 36 not cited by ID.** SEAM 30/30, HTTP 52/53, IO 41/42, BODY 35/37, CTX 20/20,
PIPE 36/40, RECOV 21/34, RETRY 35/45, REDIR 26/28, AUTH 37/38 (333 of 367); PAGE 35/36, SSE 40/41, SERDE 30/30,
OBS 40/40, CFG 38/38, TRANSPORT 30/30, ASYNC 22/22, XCUT 24/24, NFR 17/17 (276 of 278).

**MUST-level summary.** The specification has 531 MUST, 1 MUST NOT, 99 SHOULD and 14 MAY requirements. Of the 36 not
cited by ID, **27 are MUST** — **HTTP-40**, **IO-7**, **BODY-1**, **PIPE-1**, **PIPE-7**, **PIPE-13**, eleven
**RECOV** rows (**RECOV-18**–**RECOV-24**, **RECOV-26**–**RECOV-29**), **RETRY-3**, **RETRY-8**, **RETRY-16**,
**RETRY-21**, **RETRY-24**, **RETRY-31**, **RETRY-42**, **REDIR-24**, **AUTH-26** and **PAGE-7** — 5 are SHOULD and 4
are MAY. Among the cited MUSTs, four are not met on a stated domain and two are met in structured form only (the list
at the head of this appendix); the vacuous ones are listed there too. Every other cited MUST is designed as written or
covered by a §10 entry that argues its guarantee is kept.

---

**Correction 2026-10-08 (phase 6a).** The `RETRY` row is now 45 of 45: **RETRY-29**, **RETRY-38** and **RETRY-43** are built (the `ShouldRetry` hook,
`RetryOptions.AttemptHeaderName`, `RetryOptions.FixedDelay`), and nothing in `RETRY` is deferred. The `RECOV` rows **RECOV-17**–**RECOV-31** and
**RECOV-34** are built (**RECOV-31** with **RETRY-38**, one feature under two IDs); see the 6a checklist's carried-rows table.

**Correction 2026-10-08 (phase 6c).** The `AUTH` row is now 38 of 38: **AUTH-26** is addressed (the key policy and credential stamp and validate the value once, P6c-19) and every other
`AUTH` row is built (the 6c checklist). The notes gain **AUTH-16**'s `-sess`-without-`qop` decline (§11 item 62) and the vacuous named-key clauses of **AUTH-8**/**AUTH-9** (§11 item 64).

**Correction 2026-10-09 (phase 7b).** The `SSE` row is now 40 built and 1 not applicable: **SSE-41** is N/A, adapter-scoped (§11 item 21), where this index said "deferred"; **SSE-19**'s cap is built as `ServerSentEventLineTooLongException` (§10 entry 20, P7b-4); **SSE-18** is a documented
contract and **SSE-2**'s line layer was met by phase 3a. The notes gain **SSE-37**'s turned-on gate and **SSE-38**'s architecture test (the 7b checklist).

**Correction 2026-10-09 (phase 8a).** The `TRANSPORT` and `ASYNC` rows keep their counts (30 of 30 and 22 of 22) and gain per-ID evidence: the kit's 43-assertion catalogue (`docs/sdk-documentation/conformance.md`) names, for every `TRANSPORT` row, the
assertion that checks it, and the [8a checklist](../work/mvp/phase8/phase8a/2026-10-09-phase8a-conformance-kit-checklist.md) maps all 38 rows 8a owns. **ASYNC-6** is met on the two bridges (cancellation crosses `AsAsync` and `AsBlocking` in both
directions) and vacuous only for a runtime facade, refining the *Vacuous* note above; **ASYNC-4** and **ASYNC-21** stay vacuous. **ASYNC-2**'s worker-pool-rejection clause is not met: a scheduler whose `QueueTask` throws makes
`AsAsync(scheduler).ExecuteAsync` throw `TaskSchedulerException` synchronously (finding F-A2); the fix is a core change left for a later phase.
**TRANSPORT-2** is not met by the reference transport, measured by the kit: `SocketsHttpHandler` has no switch for its own connection-failure retry (§3 said so, "per runtime source; not reproduced here") and re-sends a body-less request reset before any response byte up to three more times, so such a request reached the server four times for one SDK attempt; a single-use body is never re-written, so the specification's own conformance sentence passes. The clause is waived for phase 8b, which either turns the retry off or records the MUST as unmet on a stated domain (a §10 entry), a design question raised in the 8a status note (finding F-T2).

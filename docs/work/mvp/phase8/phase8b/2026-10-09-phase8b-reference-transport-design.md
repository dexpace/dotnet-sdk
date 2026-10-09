# Phase 8b — Reference-Transport Hardening: Design

**Status:** Draft, for review. Written 2026-10-09 against `main` at `ce0f68c` (phases 0 to 7c merged), on branch
`87-phase-8-planning`, for issue [#89](https://github.com/dexpace/dotnet-sdk/issues/89). Brainstormed without a human in the
loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling (`P8b-n`) with
its options and rationale, and the ones the lead may still reverse are marked **open for the lead**. Sub-phase 8a (the conformance
kit, issue [#88](https://github.com/dexpace/dotnet-sdk/issues/88)) is designed in
`docs/work/mvp/phase8/phase8a/2026-10-09-phase8a-conformance-kit-design.md`; this document takes 8a's segmentation, its assertion
catalogue and its waiver model **as given**, and designs the `Dexpace.Sdk.Http.SystemNet` changes that make the kit's 8b-owned
assertions go green. The scope authority is the roadmap's Phase 8 card
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`, "Phase 8 — Transports and the Conformance Kit"), decision D3, the
dependency-graph line "8a then 8b", and every Phase Status Note bullet addressed to 8b. The format follows the phase 7 designs
(`docs/work/mvp/phase7/phase7b/2026-10-09-phase7b-sse-design.md`) and 8a's.

**What this document is.** The sub-phase design for 8b. It holds:

- one disposition per requirement row 8b owns (14 `TRANSPORT` rows), each closed by deleting an `Owner = "8b"` waiver from
  8a's SystemNet driver or by a stated N/A / unmet-on-domain argument;
- the twelve non-row hand-offs earlier phases addressed to 8b (`SEAM-11`'s and `PIPE-28`'s real synchronous path, `SEAM-15`,
  `OBS-19`, the `CFG-28` IL scan, the event ids, `LateResult`, `Disposal`, the `CallKey` stamp, and two inherit-only notes),
  each taken or declined with a reason;
- the transport's new public surface (`SystemNetHttpClientOptions`, `HeaderDropLogging`, four constructors) and the eight log
  event constants added to core's `DexpaceLogEvents`;
- the failure-classification table, the header-mapping table, and the proxy installation;
- the rulings (which double as deviation-ledger IDs, roadmap constraint 7), the breaking changes, the test strategy, PR
  segmentation, hand-offs, close-out corrections and exit criteria.

**What this document is not.** It is not the plan and not the checklist. It does not re-argue 8a's segmentation (P8a-1), D3's
confirmation (P8a-2), the kit's shape (P8a-4 to P8a-27), design §10 entry 7 (transport failures are `SdkException`s, not
`IOException`s), §10 entry 8 (cooperative cancellation) or §11 item 18 (`TRANSPORT-18` binds SystemNet); it cites them. It writes
no kit assertion (8a writes every one, including those for 8b's rows); the one kit amendment 8b needs is requested in
[Shared files with 8a](#shared-files-with-8a), not designed here. It edits no design chapter, roadmap cell or `CLAUDE.md` line; the
corrections it owes are listed in [Corrections owed at close-out](#design-roadmap-register-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

| Predecessor or sibling | Kind | State at `ce0f68c` |
|---|---|---|
| **8a** — `Dexpace.Sdk.Conformance`, the promoted `Dexpace.Sdk.Conformance.Wire` fixture, `SystemNetConformanceTests` with the `Owner = "8b"` waivers | **dependency** (roadmap: "8b is proven by 8a's kit") | Designed, not built. 8b's plan may be written in parallel; 8b's **execution starts after 8a's PR 2** (assertions and drivers) has merged, because every 8b PR closes rows by deleting waivers that PR 2 creates, and 8a's stale-waiver rule (P8a-8) fails the run the moment a fixed row's waiver is left behind. |
| Phase 1 — S1, S2, S3, S9 fixes and their `Security` classes (`HeaderInjectionWireTests`, `FramingHeaderDropWireTests`, `RedirectWireTests`, `RedirectCredentialLeakWireTests`, `MalformedContentTypeWireTests`) | **dependency** (constraint 5) | Met. 8b keeps every one green and **unedited** (checked: none asserts an event id, only levels and messages; the default drop policy keeps the first drop a `Warning`, which `HeaderInjectionWireTests` relies on). |
| Phase 2b — the SPI with `RequestOptions` and a token; `SEAM-11` ✅ with a ⏳ 8b clause; `SEAM-15` ⏳ 8b | **dependency** | Met; 8b closes both ⏳. |
| Phase 3a/3b — `RequestBody.WriteTo` (sync twin), the dispose latches, `FileRequestBody`, `Disposal` | **dependency** | Met. Every body the SDK creates overrides `WriteTo`. |
| Phase 4c — `HttpPipeline.Send` drives policies synchronously to the transport's `Execute` (`PIPE-28` ⏳ 8b: "the real synchronous terminal") | **dependency** | Met; 8b closes the ⏳ clause. |
| Phase 5a — `ProxyOptions`, `ProxyOptions.FromEnvironment`, `TimeProviderWaits`, `LateResult`, the clock/delay/environment bans | **dependency** | Met. `ProxyOptions` is the model 8b installs; `ToString` already masks credentials. |
| Phase 5b — `DexpaceLogEvents` with 110-119 reserved for "transport header drops (OBS-19)" | **dependency** | Met; 8b fills the block. |
| Phase 5c — the `traceparent` strip in `ToHttpRequestMessage` | **dependency** | Met; the sync terminal inherits it by sharing the request builder. |
| Phase 6a — `RetryFacts` classifier, `AttemptTimeout` on the attempt token, `ServiceRequestTimeoutException` | **dependency** (card "Entry") | Met. 6a's hand-off: "`RequestOptions.Timeout` stays the transport's". |
| Phase 6b — `RedirectPolicy` as the only redirect authority; borrowed-client redirect detection | **dependency** (card "Entry") | Met; 6b's hand-off: "`RedirectWireTests` and the borrowed-client detection are unchanged" — honoured. |
| Phase 6c — `407` handling left to the transport | **dependency** | Met; 8b answers it natively from `ProxyOptions`. |
| Phase 7a/7b/7c — `HttpResponseMessageBody.OpenRead` (already built by 7b), the blocking pager over `Execute` | convenience | Met; 7c's "blocking pager inherits the real sync path and the timeout with no 7c change" is pinned by one wire row. |
| Phase 9 | **dependent** | Reads 8b's handler-taking constructor (its named clients' primary handler) and `SystemNetHttpClientOptions` (binding). |

## Governing documents, and the phase-start queries

Read for this design: product-spec chapter 17 in full, chapter 18 (to confirm no `ASYNC` row is 8b's), appendix B (B.6), and
appendix C rows `TRANSPORT-1`..`-30`, `SEAM-11`, `SEAM-13`..`-15`, `SEAM-30`, `PIPE-28`, `OBS-19`, `CFG-22`, `CFG-28`, `XCUT-2`,
`XCUT-18`, `HTTP-21`; design §2.1, §2.2, §3.2 (the whole section, including its six "obvious-but-wrong" behaviours), §3.3
("Deadlines, end to end" and the `SEAM-30` passage), §3.7, §8.2 ("The proxy model"), §9.3, §10 entries 7 and 8, §11 items 18 and
36, §12's `TRANSPORT` row; the roadmap's constraints 2, 3, 5 and 7, D3, the Phase 8 card, the phase-1 hand-off table (row 8b),
the security-defect table (S1, S2, S3, S9) and the status notes of 2b, 3b, 4a, 4c, 5a, 5b, 5c, 6a, 6b, 6c, 7a, 7b and 7c;
`docs/first-release.md` (*Unsatisfied MUSTs*, *SHOULD- and MAY-level requirements declined*, *Behavioural asymmetries*); the code
under `src/Dexpace.Sdk.Http.SystemNet` (all four files), `src/Dexpace.Sdk.Core/{Client,Configuration/ProxyOptions.cs,Internal/{LateResult,Disposal}.cs,Resilience/RetryFacts.cs,Errors/TransportExceptions.cs,Diagnostics/DexpaceLogEvents.cs}`,
`BannedSymbols.txt`, and `tests/Dexpace.Sdk.Http.SystemNet.Tests` (every class, including `Security/` and `Architecture/`).

```bash
scripts/knowledge --origin note --brief           # transport notes: the traceparent strip condition (5c) and OpenRead arriving with 7b
scripts/knowledge --section conflicts --brief     # no TRANSPORT conflict; CA2007-on-src conformed (binds every new await)
scripts/knowledge --prefix-info TRANSPORT         # 30 IDs, 30 of 30 substantive; topics transport-adapter, testing, http-domain-model
scripts/knowledge --gaps TRANSPORT                # 0 gaps
scripts/knowledge --topic transport-adapter --brief
```

The corpus holds nothing for 8b's rows beyond the chapter and the design. One harvested source is stale
(`03-seam-by-seam-idiomatic-mapping.md`, 9 entries derive from it); §3.2 and §3.3 were read from the file, not the corpus.

**Sibling sources.** Ruby's analogue of 8b is its phase 8a (the `Net::HTTP` transport) and 8c (`async-http`):
`ruby-sdk/docs/work/mvp/phase8/phase8a/2026-09-11-phase8a-synchronous-transport-and-conformance-design.md` (`R5`'s split of
`TRANSPORT-28`, `P8-6`'s per-call timeout on a borrowed client) and `…/phase8c/…-asynchronous-transport-design.md` (`P8-38`: the
malformed inbound header **name** is unreachable on a strict parser and waived). Node's `packages/transport-shared/src/`
(`nodejs-sdk@c0ff3fd`): `drop-log.ts` (three modes, a 128-name FIFO-bounded dedup set), `abort-mapping.ts`, `default-timeout.ts`
(a construction-time range check on the default), `header-mapping.ts`, `body-less.ts`. P8b-24.

---

## Facts the design rests on

8a's F1 to F7 (its "Facts the design rests on") are taken as measured. 8b re-measured the facts its own rulings turn on, on .NET
SDK 10.0.401, Linux, with a raw `TcpListener` peer; the probe was a scratch file-based app, not committed, and the plan's task 0
re-runs each as a test.

| # | Fact | Consequence |
|---|---|---|
| G1 | `HttpClient.Send` through a handler that overrides only `SendAsync` throws `NotSupportedException` ("The synchronous method is not supported by '…'"). | A real sync `Execute` cannot run over a borrowed client whose primary handler is async-only; the failure is loud, not a hang (P8b-9). |
| G2 | `HttpClient.Send` through a `DelegatingHandler` that overrides only `SendAsync` **silently bypasses that handler**: `DelegatingHandler.Send` forwards to the inner handler's `Send`, the override never runs, and the request succeeds. | The one hazard of a real sync path over a borrowed handler chain. Documented as a behavioural asymmetry, not engineered around (P8b-9). |
| G3 | Cancelling (and disposing) the token passed to `SendAsync(…, ResponseHeadersRead, token)` **after** it returned does not affect reading the body: a gated second chunk still arrives. | A per-call deadline source may be disposed when `SendAsync` returns; the deadline bounds the exchange until the response headers, as `AttemptTimeout` already does (P8b-6). |
| G4 | On the **sync** path, `CancelPendingRequests` during `Send` throws `TaskCanceledException` with an `IOException` inner; `HttpClient.Timeout` elapsing throws `TaskCanceledException` with a `TimeoutException` inner. | Same shapes as 8a's F3 on the async path: one classification helper serves both faces (P8b-7). |
| G5 | Through an HTTP forward proxy answering `407 Proxy-Authenticate: Basic`, `SocketsHttpHandler` asks the proxy's `ICredentials.GetCredential(uri, "Basic")`, re-sends with `Proxy-Authorization`, and **serialises the request content twice**. The origin's later `401` was **not** answered with any credential (`handler.Credentials` unset). | `TRANSPORT-18`'s re-send is live on the owned client whenever `ProxyOptions` carries credentials; the proxy credential never reaches an origin (`TRANSPORT-30`'s MUST) as long as the owned handler's server `Credentials` stay unset (P8b-5, P8b-10). |
| G6 | The Basic proxy challenge is **not** cached: a second `http://` request on the same keep-alive connection is again sent without `Proxy-Authorization`, gets a `407`, and is re-sent (two serialisations again). | For `http://` targets through a credentialed proxy, **every** single-use body is re-sent: the capture of P8b-10 is on the common path there, not an edge. |
| G7 | For an `https://` target through the same proxy, the `407` arrives on the `CONNECT`, before any request byte: the content was serialised **zero** times. | The re-send of G5/G6 is confined to `http://` targets; an `https://` upload through an authenticating proxy never re-sends its body, so a small capture bound costs little (P8b-10). |
| G8 | `RetryFacts.IsRetryableCause` walks `InnerException` and `AggregateException` (not the `ExceptionTrail` suppressed list) and calls any `IOException` in the chain retryable. | A terminal internal cancellation must **not** carry the native `TaskCanceledException → IOException` as its `InnerException`, or the classifier retries it; the native exception is attached through `ExceptionTrail.AddSuppressed` instead (P8b-7). |

---

## Scope: the 14 rows 8b owns

8a's split is taken verbatim: **8b owns `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`,
`-28`, `-30`** (14 rows); 8a owns the other 16 `TRANSPORT` rows and all 22 `ASYNC` rows (38). 8b owns **no** `ASYNC` row: chapter
18 is answered by the SPI and the bridges (P8a-3), and nothing in 8b changes either. P8b-1.

Legend (roadmap constraint 3): **build** = 8b changes SystemNet code and the row closes when the named kit assertion passes on the
SystemNet driver with its `Owner = "8b"` waiver deleted, plus the named SystemNet test; **met** = already true, with evidence, and
8b adds only the pinning test or waiver deletion the row still needs; **unmet on a stated domain** = a MUST clause the reference
transport cannot reach, argued in a new design §10 entry.

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| TRANSPORT-1 | MUST | **build** (handler path); owned path met | Owned: `CreateOwnedClient` sets `AllowAutoRedirect = false` (phase 1, `RedirectWireTests.The_sdk_managed_transport_returns_the_3xx_and_never_contacts_the_target`). Build: the new `SystemNetHttpClient(HttpMessageHandler …)` walks the `DelegatingHandler` chain to its primary and throws `ArgumentException` naming `AllowAutoRedirect` when that primary is a `SocketsHttpHandler` or `HttpClientHandler` with `AllowAutoRedirect == true` (both default to `true`), without mutating it (P8b-3). Kit: `transport-1.redirect-not-followed` on a second SystemNet subject built through the handler constructor. Unit: `HandlerConstructorTests` (redirect-on primary rejected directly and under two `DelegatingHandler`s; redirect-off accepted; an unknown primary accepted, with the last-line URI check intact; a `DelegatingHandler` with no inner handler accepted). |
| TRANSPORT-4 | MUST | **build** | The timeout branch of the classification table (P8b-7): a fired per-call deadline, or a `TaskCanceledException` whose inner exception is a `TimeoutException` (a borrowed `HttpClient.Timeout`), with the caller's token unsignalled, is a `ServiceRequestTimeoutException` (retryable, `XCUT-2`); the caller's token is never touched, because the deadline lives on a separate timer source (design §3.3). Kit: `transport-4.timeout-is-retryable` on both faces; waiver deleted. Unit: `FailureClassificationTests` over stub handlers on both faces with a `FakeTimeProvider`. |
| TRANSPORT-5 | MUST | **build** | A per-call deadline: a `CancellationTokenSource` armed for `options.Timeout ?? SystemNetHttpClientOptions.Timeout` on `SystemNetHttpClientOptions.TimeProvider`, linked with the caller's token, disposed when `SendAsync`/`Send` returns (G3). The shared `HttpClient` is never mutated: the owned client's `HttpClient.Timeout` is `InfiniteTimeSpan` (design §3.2) and a borrowed client's own `Timeout` stays an outer bound (P8b-6). A `null` per-call value leaves the transport default (100 s unless configured). Kit: `transport-5.per-call-timeout`; waiver deleted. Retires `SystemNetHttpClientTests.Options_are_accepted_and_ignored_until_8b`. |
| TRANSPORT-6 | SHOULD | **build** | The hazard is inverted on .NET (a zero-millisecond `CancelAfter` cancels at once, it never means "no deadline"; design §3.2), but the clamp ships anyway so the behaviour is stated: a positive deadline below 1 ms is raised to 1 ms, and one above the timer's ceiling is lowered to 49 days (`DexpaceClientOptions`' own limit), never thrown on (P8b-6). Kit: `transport-6.sub-resolution-timeout` (100 µs times out, does not hang); waiver deleted. Unit: the clamp table. |
| TRANSPORT-8 | MUST | **build** | The internal-cancel branch (P8b-7): an `OperationCanceledException` from the native client with the caller's token unsignalled, the deadline not fired and no `TimeoutException` inner (8a's F3, G4: `CancelPendingRequests`, a borrowed client disposed by its owner mid-call, the owned client disposed by `Dispose` mid-call) completes the call with a **new** `OperationCanceledException` (terminal, non-retryable by G8) carrying the native exception on its `ExceptionTrail`; a genuine timeout on the same path stays retryable (`TRANSPORT-4`'s branch, which is checked first). Kit: `transport-8.internal-cancel-is-terminal` through the `CreateWithInternalCancel` hook; waiver deleted. |
| TRANSPORT-9 | MUST | **met, structurally** + kit | No SystemNet path abandons a `Task<Response>` or settles a `TaskCompletionSource<Response>`: `ExecuteAsync` is an `async` method that returns the response `SendAsync` produced, and the per-call deadline is enforced by cancelling the token `SendAsync` observes, never by `WaitAsync` or `WhenAny` (design §3.3, the `SEAM-30` precondition is absent). Adaptation after the native response is live is `TRANSPORT-22`'s guard. `LateResult` is therefore **not** used (P8b-8); the 5a hand-off is closed as unneeded. Kit: `transport-9.settle-race-releases` (8a expects it to pass on `ce0f68c`; it must stay green across the deadline work). |
| TRANSPORT-10 | MUST | **build** | Header partition (P8b-11): the caller's `Content-Type` is written to the content headers and the body-derived media type only when the caller set none (case-insensitive name match). Today `RequestBodyContent`'s constructor stamps the body's type and `ToHttpRequestMessage` skips the caller's (8a's F7). Kit: `transport-10.content-type-authoritative` on both faces; waiver deleted. Integration: `ContentTypeWireTests` (body X + header Y → Y; body X, no header → X; body-less `POST` + header Y → Y with `Content-Length: 0`; multi-part body with a caller type lacking the boundary → the caller's value, documented). |
| TRANSPORT-12 | MUST | **build** | Per-header drop on **both** faces (P8b-11): every header the native collections refuse (`TryAddWithoutValidation` false on the request collection and, for a content header, on the content collection), every content header on a body-forbidden method, and every header failing the wire re-check (`IsWireSafe`) is dropped alone and logged by name under the drop policy; the send proceeds. Today a body-less request's content header vanishes without a log (F7) and a failed content-collection add is ignored silently. Kit: `transport-12.native-rejected-header-dropped` on both faces; waiver deleted. The reachable model-valid-but-wire-refused header on SystemNet is the inbound-lenient value re-used on a request (`Headers.Builder.AddInbound` with obs-text), which `HeaderInjectionWireTests` already pins; the kit uses the same construction. |
| TRANSPORT-13 | SHOULD | **build** | `SystemNetHttpClientOptions.HeaderDropLogging` with three modes (P8b-12): `FirstPerName` (default: `Warning` for the first drop of a name, `Debug` after), `Every` (`Warning` each time), `VerboseOnly` (`Debug` each time). The per-name set is per transport instance, `OrdinalIgnoreCase`, bounded at 128 names with oldest-first eviction (Node `drop-log.ts`). Closes `OBS-19` (⏳ 8b in 5b's checklist). Kit: `transport-13.drop-log-once-per-name`; waiver deleted. Unit: `HeaderDropLogTests` (the three modes, case folding, the bound under 10 000 distinct names, concurrent first drops of one name warn once). |
| TRANSPORT-14 | MUST (+SHOULD) | **value half met; obs-text SHOULD met; name half unmet on a stated domain** | Value half: `AddInbound` drops a header whose value carries a control byte and keeps the rest (`HeaderInjectionWireTests.An_inbound_header_is_validated_leniently_so_obs_text_parses_and_a_control_byte_is_dropped`, phase 1); the drop's event moves to id 112 (P8b-13). Obs-text: preserved (same test). Name half: `SocketsHttpHandler` rejects the **whole response** on a malformed header name before the adapter sees it (8a's F1), and it has no leniency switch; no handler-level workaround exists short of replacing the HTTP/1.1 parser. Recorded as **design §10 entry 32** and a `docs/first-release.md` *Unsatisfied MUSTs* line, and waived **permanently** (`Owner = null`) on the SystemNet drivers for the name assertion only (P8b-14). This needs 8a's waiver to scope to an assertion, not only an ID (see [Shared files with 8a](#shared-files-with-8a)). |
| TRANSPORT-17 | MUST | **build** (sync path); async path met | Async: a single-use body's own latch throws `StreamConsumedException` on a second `WriteToAsync`, and the transport writes it once (F6). Build: `RequestBodyContent` overrides the synchronous `SerializeToStream(Stream, TransportContext?, CancellationToken)` over `RequestBody.WriteTo`, so `HttpClient.Send` writes the body on the calling thread exactly once (P8b-9). With P8b-10's capture, a native re-send replays the captured copy and never re-reads the source: the source is read once on every path. Kit: `transport-17.single-use-written-once` on both faces, now over the real sync path. Integration: `SyncExecuteWireTests.A_single_use_body_is_written_once_on_the_sync_path`. |
| TRANSPORT-18 | MUST | **build** | Bounded capture-and-replay (P8b-10): on a construction that can re-send (borrowed client, handler-built client, owned client whose `ProxyOptions` carries credentials), `RequestBodyContent` tees the **first** write of a non-replayable body into an unpooled capture of at most `ResendBufferLimit` bytes (default 1 MiB); a later serialisation replays the capture byte-for-byte. If the first write did not complete, or the body outgrew the limit, a later serialisation throws `StreamConsumedException` naming the limit and `ToReplayableAsync`, which `HttpClient` wraps in `HttpRequestException` and the transport maps to `ServiceRequestException`: the send fails with the transport-failure type and never ships a truncated body. A source that fails mid-write fails the first send the same way. Kit: `transport-18.native-resend-identical` through `CreateWithNativeResend` (1 MiB stream, both serialisations identical; a mid-write failure → `ServiceRequestException`); waiver deleted. Integration: `ResendCaptureWireTests` (the `407` re-send of G5/G6 through the fixture as a forward proxy; a body over the limit fails with the named message; an owned client without proxy credentials never captures). |
| TRANSPORT-28 | SHOULD (+MUST) | **MUST half met; zero-copy SHOULD declined** | Met: `FileRequestBody.IsReplayable` is `true` and each `WriteTo`/`WriteToAsync` opens a fresh handle and copies exactly `[Offset, Offset + ContentLength)` (3b), so a retry, redirect or native re-send carries the same range; `TryComputeLength` reports the length. Declined: `HttpClient` writes content to its own connection stream (possibly an `SslStream`); no sendfile path is reachable through it, so "zero-copy where supported" has no supported case. The transport does **not** special-case `FileRequestBody` (the 3b hand-off's "may recognise") because nothing observable would change (P8b-15). Kit: `transport-28.file-range-replayable` (passes on `ce0f68c`, per 8a). `docs/first-release.md` gains a *SHOULD declined* line. Ruby's `R5` took the same split. |
| TRANSPORT-30 | SHOULD (+MUST) | **build** | `SystemNetHttpClientOptions.Proxy` installs an internal `IWebProxy` over `ProxyOptions` on the **owned** handler (P8b-4, P8b-5): spec-correct bypass globs, `GetProxy` returning the proxy URI without userinfo, credentials from `ChallengeCredentials` or a Basic-only `UserName`/`Password` fallback. Unhonourable combinations are surfaced once at construction as a `Warning` (event 113) naming the feature and never a credential; the owned handler's server `Credentials` stay unset, so a proxy credential is never answered to an origin `401` (G5). Kit: `transport-30.proxy-discoverable-no-leak` through the `CreateWithProxy` hook 8b supplies; it moves from `NotExercised` to `Passed`. Security: new `ProxyCredentialWireTests`. |

Totals: 14 rows, 10 MUST and 4 SHOULD by appendix C. **Build** 11 (`-1`, `-4`, `-5`, `-6`, `-8`, `-10`, `-12`, `-13`, `-17`, `-18`,
`-30` — `-1` and `-17` partly), **met** 2 (`-9` structurally, `-28`'s MUST half), **unmet on a stated domain** 1 clause
(`-14`'s name half, §10 entry 32), **declined SHOULD clause** 1 (`-28`'s zero-copy). Every row also stays covered by its 8a
assertion; 8b's checklist cites both. `TRANSPORT-11` stays 8a's row; 8b only moves its framing-drop event to id 111 (P8b-13).

### Non-row hand-offs addressed to 8b

| From | Hand-off | Disposition |
|---|---|---|
| 2b, 4c, 7c (roadmap coupling obligation 5) | "8b makes `SystemNetHttpClient.Execute` call `HttpClient.Send`"; `SEAM-11` ⏳ 8b, `PIPE-28` ⏳ 8b | **Taken** (P8b-9). `Execute` calls `HttpClient.Send` with `ResponseHeadersRead` on every construction; `RequestBodyContent.SerializeToStream` calls `WriteTo`. Proof that the path is genuinely synchronous: `SyncExecuteTests.Execute_runs_through_a_handler_that_implements_only_Send` (its `SendAsync` throws) and the converse G1 row. Both ⏳ clauses close by dated correction. |
| 2b, 3b | `SEAM-15`: `ObjectDisposedException` after dispose, owned and borrowed | **Taken** (P8b-16). Kit: `seam-15.after-dispose` (declared `ThrowsObjectDisposedException`); waiver deleted. |
| 5b | `OBS-19` and `TRANSPORT-13`; event ids 1-3 into 110-119 | **Taken** (P8b-12, P8b-13). |
| 5a | the `IWebProxy` adapter over `ProxyOptions`; "the transport constructor's `FromEnvironment` call and its `CFG-28` reading" | **Taken, with the strict reading** (P8b-4): no constructor calls `FromEnvironment`; the caller does. |
| 5a (F8) | the `CFG-28` IL scan over the transport assembly | **Taken**: `NoImplicitEnvironmentReadArchitectureTests` (`Unit`) scans every method body in `Dexpace.Sdk.Http.SystemNet` for calls to `Environment.GetEnvironmentVariable(s)` (one permitted caller: `TraceContextStripping.ReadRuntimeSwitch`, the runtime's own switch, P5c-11), `ProxyOptions.FromEnvironment` (none permitted), `HttpClient.DefaultProxy` and `WebRequest.DefaultWebProxy` (none permitted). `BannedSymbols.txt` gains `HttpClient.DefaultProxy` (get and set). |
| 5a | `LateResult` for `TRANSPORT-9` | **Declined as unneeded** (P8b-8): no abandonment exists to dispose after. `LateResult` stays internal to core. |
| 3b | "decides between a local equivalent of `Disposal` and making it public for `TRANSPORT-22`'s dispose-on-throw" | **Local equivalent** (P8b-17): an internal `NativeDisposal.DisposeQuietly(IDisposable, Exception primary)` in SystemNet over the public `ExceptionFacts.IsFatal` and `ExceptionTrail.AddSuppressed`. `Disposal` stays internal; no core surface for it. |
| 3b | "may recognise `FileRequestBody`" | **Declined** (P8b-15). |
| 4a (P4a-13) | "8b stamps the `HttpRequestOptionsKey<CallKey>`" | **Not taken; handed to phase 9** (P8b-18, open for the lead). `RequestOptions` carries no `CallKey` (P4a-13's option 1 was never ruled), so the transport has nothing to stamp, and adding the carrier is a `SEAM`/`CTX` surface change outside every 8b row. |
| 5c | "the sync terminal inherits the strip" | **Inherited**: both faces build the message with the one `ToHttpRequestMessage`. Pinned: `TracePropagationWireTests` gains a sync row. |
| 6c | "the transport answers a proxy's `407`; the handlers' `proxy` flag lets a caller adapt them to `ICredentials`" | **Taken** (P8b-5): the `407` is answered natively from `ProxyOptions`; adapting a core `IChallengeHandler` to `ICredentials` is the caller's, documented in `transport.md`, not shipped. |
| 8a | "whether an adaptation failure is re-typed as an SDK exception" (phase 1's S9 note) | **Not re-typed** (P8b-17): an exception thrown while adapting a live response is an adapter defect or the redirect guard's own `SdkException`; wrapping a defect in a retryable SDK type would hide it. |

---

## Argued positions

### A. Construction: one options record, four new constructors, the existing four kept (P8b-2, P8b-3)

**The need.** The card adds a handler-taking constructor and proxy installation; `TRANSPORT-5` needs a configured default
timeout, `TRANSPORT-13` a drop-log policy, P8b-10 a capture limit, and the deadline a `TimeProvider`. Four settings times three
client sources (owned, handler, borrowed) does not fit in positional constructor overloads.

**Options.** (1) A `SystemNetHttpClientOptions` sealed record passed to new constructors, the four existing constructors kept as
sugar; (2) static factories (`SystemNetHttpClient.Create(options)`, `.FromHandler(handler, options)`, `.Borrow(client, options)`)
replacing every constructor; (3) more positional overloads. **Chosen: (1).** (3) is unmaintainable and invites `RS0026`. (2) reads
well and names ownership in the method, but it breaks every existing call site (`new SystemNetHttpClient(client)` is in the
README, every wire test and the sibling docs) for a naming gain, while constructors already state ownership through their
parameter type (`HttpClient` = borrowed, `HttpMessageHandler` = handler borrowed / client owned, none = owned). **Open for the
lead** only on (2) versus (1).

The record follows the house options shape (phase 5a: validation in `init` accessors, so `with` cannot bypass it):

| Member | Type | Default | Rule |
|---|---|---|---|
| `Logger` | `ILogger` | `NullLogger.Instance` | `null` throws `ArgumentNullException`. |
| `Proxy` | `ProxyOptions?` | `null` (direct connection, **no** environment read) | Applies to the owned construction only; a non-null value passed with a borrowed client or a handler throws `ArgumentException` at transport construction (the caller's client or handler owns its proxy, `TRANSPORT-15`). |
| `Timeout` | `TimeSpan?` | 100 seconds (`HttpClient`'s own default, so no owned-client behaviour changes) | `null` = no transport default; otherwise `(0, 49 days]`, else `ArgumentOutOfRangeException` (`DexpaceClientOptions`' range; Node's `requireValidDefaultTimeoutMs` is the same check). |
| `HeaderDropLogging` | `HeaderDropLogging` | `FirstPerName` | An undefined value throws `ArgumentOutOfRangeException`. |
| `ResendBufferLimit` | `long` | `1_048_576` (1 MiB) | `>= 0`; `0` disables the capture (every native re-send of a single-use body fails). |
| `TimeProvider` | `TimeProvider` | `TimeProvider.System` | `null` throws. Arms the per-call deadline; tests pass a `FakeTimeProvider`. |

The constructors and their ownership:

| Constructor | Native client | Handler | Proxy from options | Re-send capable (P8b-10) |
|---|---|---|---|---|
| `()`, `(ILogger)` *(existing)*, `(SystemNetHttpClientOptions)` *(new)* | owned, built by the one factory | owned `SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = Proxy is not null, Proxy = adapter }` | yes | only when `Proxy` carries credentials (G5) |
| `(HttpMessageHandler)`, `(HttpMessageHandler, SystemNetHttpClientOptions)` *(new)* | owned, `new HttpClient(handler, disposeHandler: false)` in the same factory, `Timeout = InfiniteTimeSpan` | **borrowed**, never disposed or mutated; rejected when its primary follows redirects | no (`ArgumentException` when set) | yes (the chain is opaque) |
| `(HttpClient)`, `(HttpClient, ILogger)` *(existing)*, `(HttpClient, SystemNetHttpClientOptions)` *(new)* | **borrowed**, never disposed or mutated; its `Timeout` stays an outer bound | invisible | no (`ArgumentException` when set) | yes |

**The handler check (P8b-3).** Walk `DelegatingHandler.InnerHandler` until a non-delegating handler or `null`; reject when it is
a `SocketsHttpHandler` or an `HttpClientHandler` with `AllowAutoRedirect == true`, with `ArgumentException` naming
`AllowAutoRedirect` and `TRANSPORT-1`. An unknown primary (`WinHttpHandler`, a platform handler, a test double) is accepted —
inspecting it would take reflection, which `IsAotCompatible` forbids — and the existing last-line check (a final request URI
differing in scheme, host, port or path fails the call) still applies. A chain whose innermost `InnerHandler` is still `null`
(`IHttpClientFactory` assigns it later) is accepted; `HttpClient` fails its first send by itself. Rejecting rather than flipping
`AllowAutoRedirect` keeps `TRANSPORT-15`'s "BYO … MUST NOT be mutated". The banned `HttpClient` constructor is called only from
the one owned-client factory (`RS0030`'s existing pragma), which gains the handler overload.

### B. Proxy: installed on the owned client only, never read from the environment implicitly (P8b-4, P8b-5)

**The tension.** Design §8.2 says "an SDK-managed `Http.SystemNet` transport calls that resolver explicitly at construction and
installs its own glob-matching `IWebProxy`". `CFG-28` says "the environment MUST be consulted only when a resolver is explicitly
invoked — nothing may read proxy configuration implicitly at construction/startup". A parameterless constructor that consults the
environment is exactly an implicit read at construction, whoever's code calls the resolver. And today the owned
`SocketsHttpHandler` has `UseProxy = true` with no `Proxy`, so the runtime's `HttpClient.DefaultProxy` reads `HTTP(S)_PROXY` and
`NO_PROXY` on first use, with the four divergences §8.2 lists (no `NO_PROXY=*` bypass, port defaulting, userinfo in the URI, regex
bypass entries): a live `CFG-23`/`CFG-25`/`CFG-27`/`CFG-28` defect on the reference transport.

**Options.** (1) The owned constructors call `ProxyOptions.FromEnvironment()` and install the result (§8.2's letter); (2) **strict**:
the owned handler has `UseProxy = false` unless `SystemNetHttpClientOptions.Proxy` is set, and the caller who wants the environment
writes `Proxy = ProxyOptions.FromEnvironment()`; (3) keep the runtime default. **Chosen: (2).** (3) keeps four MUST violations. (1)
is the read `CFG-28` forbids, and it would make the phase 9 DI package's `FromEnvironment(key => configuration[key], logger)`
collide with a second, process-environment read inside the transport. (2) puts the one explicit call in the caller's code, which
is what "explicitly invoked" means, and costs one line. It is **breaking** (a consumer relying on `HTTP_PROXY` through
`new SystemNetHttpClient()` now connects directly), recorded in the changelog with the migration line, and in `docs/first-release.md`.
**Open for the lead**, since it overrides §8.2's sentence; the fallback is (1), which needs only a constructor change.

Enforcement: `BannedSymbols.txt` gains `P:System.Net.Http.HttpClient.DefaultProxy` (no `src/` code may read or set the
process-wide default); the IL scan of the hand-off table; and a `Unit` test through the existing internal
`SystemNetHttpClient(Action<SocketsHttpHandler>)` seam that the owned handler's `UseProxy` is `false` and its `Proxy` `null` when
no `ProxyOptions` is given.

**The adapter (P8b-5).** `ProxyOptionsWebProxy : IWebProxy`, internal and sealed:

- `GetProxy(destination)` returns `http://host:port`, `socks4://host:port` or `socks5://host:port` by `ProxyOptions.Type`, an IPv6
  host bracketed, **never** with userinfo (so no runtime log or exception message can carry a credential).
- `IsBypassed(destination)` is `ProxyOptions.IsBypassed(destination.IdnHost)` (the spec's glob dialect, `CFG-23`, `CFG-27`).
- `Credentials`: `ChallengeCredentials` when set (the native client asks it per scheme on a `407`, which is `CFG-22`'s challenge
  hook, P5a-13); otherwise, when `UserName` is set, a `BasicOnlyCredentials` that answers `GetCredential(uri, authType)` with a
  `NetworkCredential` only for `authType` `Basic` on an HTTP proxy — `TRANSPORT-30`'s "proxy auth SHOULD fall back to Basic", and
  no NTLM/Negotiate exchange the caller did not ask for. For a SOCKS proxy the pair is passed for the SOCKS handshake (plan task 0
  verifies the `authType` the runtime passes there; G5 measured only HTTP).
- The owned handler's server-side `Credentials` and `UseDefaultCredentials` stay unset and `PreAuthenticate` `false`, so a proxy
  credential can be answered only to a `407` from that proxy, never to an origin's `401` (G5; `TRANSPORT-30`'s MUST).

**Discoverability (`TRANSPORT-30`'s SHOULD).** At construction, each combination the native client cannot honour is logged once
as a `Warning` with event 113, naming the feature and the `ProxyOptions` member, never a value: a `Password` on a SOCKS4 proxy
(SOCKS4 carries a user id only); `ChallengeCredentials` on a SOCKS proxy (no challenge exists); `ChallengeCredentials` and
`UserName` both set (`ChallengeCredentials` wins, the pair is ignored). The requirement's "custom (non-Basic) proxy challenge
handler" has no unhonourable form here: `ICredentials` is the native challenge hook and `SocketsHttpHandler` answers Basic, Digest,
NTLM and Negotiate proxy challenges through it. Core's `IChallengeHandler` (6c) is not a `ProxyOptions` member, so it cannot be
misconfigured into the transport; adapting one is documented.

### C. Deadlines and the classification table (P8b-6, P8b-7)

**The deadline.** Per call, when `options.Timeout ?? settings.Timeout` is not `null`: a timer source
`new CancellationTokenSource(clamped, timeProvider)` and a linked source over the caller's token and the timer's, whose token is
passed to `SendAsync`/`Send`; both are disposed when the native call returns (G3), so the deadline bounds the exchange until the
response headers, exactly like `AttemptTimeout` (6a) and like `HttpClient.Timeout` under `ResponseHeadersRead`. The body read is
bounded by the token the caller passes to `OpenReadAsync`/`ReadAsync`. Clamps (`TRANSPORT-6`): below 1 ms → 1 ms; above 49 days →
49 days. With no deadline at all, the caller's token is passed straight through and no source is allocated. **Options
considered**: bounding the body read too (rejected: it would cancel a long streaming download the caller is actively reading, and
`SEAM-11` hands the body to the caller); `HttpClient.Timeout` per call (impossible without mutating the shared client,
`TRANSPORT-5`); raising on a borrowed client as Ruby `P8-6` does (rejected: on .NET a linked token shortens a borrowed client's
deadline without touching it, so the override can be honoured; it cannot *lengthen* past the borrowed client's own `Timeout`, which
the XML docs state).

**The classification (one helper, both faces).** Checked in this order; the first match decides:

| # | Caught | Condition | Surfaces as | Row |
|---|---|---|---|---|
| 1 | any | the caller's token is signalled | the `OperationCanceledException` as is (rethrow; never wrapped) | `TRANSPORT-3`, `XCUT-1` |
| 2 | `OperationCanceledException` or `HttpRequestException` | the per-call timer fired | `ServiceRequestTimeoutException` (retryable), inner = the native exception | `TRANSPORT-4`, `-5`, `XCUT-2` |
| 3 | `OperationCanceledException` | its inner exception is a `TimeoutException` (a borrowed client's `HttpClient.Timeout`) | `ServiceRequestTimeoutException` | `TRANSPORT-4`, `-8` (timeout half) |
| 4 | `OperationCanceledException` | otherwise (`CancelPendingRequests`, the native client disposed mid-call, this transport disposed mid-call) | a **new** `OperationCanceledException("The HTTP client cancelled the request internally; it was not a timeout and was not requested by the caller.")` with `CancellationToken.None`; the native exception attached through `ExceptionTrail.AddSuppressed`, **not** as `InnerException` (G8) | `TRANSPORT-8` |
| 5 | `HttpRequestException` | — | `ServiceRequestException` (retryable), inner = the native exception | `TRANSPORT-20` |
| — | `ObjectDisposedException`, `NotSupportedException`, anything else | — | propagates unwrapped (a configuration error or a defect, not a transport failure) | — |

Row 1 first means a call the caller cancelled is terminal even if its deadline also fired; row 2 before row 3 means the transport's
own deadline is recognised by state, not by the shape of the runtime's exception (`XCUT-2`'s "checked first to stay reachable").
**Why a plain `OperationCanceledException` for row 4 (open for the lead).** Options: (a) a new public `SdkException` subtype
(`TransportCancelledException`, `IsRetryable = false`); (b) `OperationCanceledException`. A new type is one more public surface
for a rare path, and `IsRetryable = false` does not veto the classifier (P6a-8) if an I/O cause is anywhere in the chain, so it
would still need the trail trick. (b) is §10 entry 8's cancellation shape, which the pipeline already treats as terminal when the
caller did not cancel (`RetryFacts.IsRetryableFailure` finds no retryable cause, and `RetryPolicy`/`OperationPolicy` catch only
their own deadline's cancellation). **Chosen: (b).**

### D. The real synchronous path (P8b-9)

`Execute` builds the message with the shared `ToHttpRequestMessage`, arms the shared deadline, calls
`_client.Send(message, HttpCompletionOption.ResponseHeadersRead, token)`, classifies with the shared helper and adapts with the
shared guarded `ToResponse`. `RequestBodyContent` gains `SerializeToStream(Stream, TransportContext?, CancellationToken)` over
`RequestBody.WriteTo` (and `CreateContentReadStream` is not needed: request content is never read back). The `RS0030`/`CA2000`
pragma on today's `Execute` is deleted; no sync-over-async remains in the package.

**Options for a borrowed client.** (a) `HttpClient.Send` for every construction; (b) `Send` for owned and handler-built clients,
sync-over-async kept for a borrowed `HttpClient`. **Chosen: (a), open for the lead.** (b) keeps a banned pattern in a shipped
package to protect handlers that implement only `SendAsync` — but G2 shows the hazard is the runtime's documented semantics, not
the SDK's, and the runtime's own handlers (`SocketsHttpHandler`, `HttpClientHandler`, the `IHttpClientFactory` logging handlers,
`Microsoft.Extensions.Http.Resilience`'s handler) implement `Send`. A borrowed chain with an async-only primary fails loudly with
`NotSupportedException` (G1); a chain with an async-only `DelegatingHandler` silently skips it (G2). Both are stated in the XML
remarks, the README and `transport.md`, and recorded as a *Behavioural asymmetry* (`sync-borrowed-handlers`) in
`docs/first-release.md`. A third-party `RequestBody` that does not override `WriteTo` fails the sync send with the base
`NotSupportedException` (wrapped by `HttpClient`), as core designed (P3a-5).

`HTTP/2`: `SocketsHttpHandler.Send` is HTTP/1.1-only; the transport never sets `HttpRequestMessage.Version`, so requests stay 1.1
on both faces, as today.

### E. Request headers: partition, authority and drops (P8b-11, P8b-12, P8b-13)

Per header, in today's loop order, after the trace-context strip and the framing drop:

| Header | Body present | Body-less, body-permitted method (`POST`, `PUT`, `PATCH`, `DELETE`, `OPTIONS`, extension methods) | Body-less, body-forbidden method (`GET`, `HEAD`, `TRACE`, `CONNECT`) |
|---|---|---|---|
| framing set (`TRANSPORT-11`) | dropped, `Debug`, event 111 | same | same |
| fails `IsWireSafe` | dropped under the policy, event 110 | same | same |
| content header (`Allow`, `Content-*` except `Content-Length`, `Expires`, `Last-Modified`) | content collection; the caller's `Content-Type` wins over the body's (`TRANSPORT-10`) | an empty `ByteArrayContent` carries it; the wire gets `Content-Length: 0` (unchanged, F4) plus the header | dropped under the policy, event 110 (attaching content would violate `TRANSPORT-26`) |
| any other | request collection; if refused there and it is not a content header, dropped under the policy | same | same |

The body-derived `Content-Type` is stamped only when no caller `Content-Type` exists (case-insensitive), decided once before the
loop. A content collection that refuses a value is a drop under the policy, never a silent loss. Values are never logged
(`XCUT-18`); names go through `HttpHeaderSyntax.EscapeName` as today.

**The drop policy** (`TRANSPORT-13`, `OBS-19`). `HeaderDropLogging.FirstPerName` (default), `.Every`, `.VerboseOnly`. The third mode
is `OBS-19`'s "log only at verbose" rather than `TRANSPORT-13`'s "all quiet" (Node ships "quiet"): a dropped header is never
invisible at `Debug`. The seen-set is a small lock-guarded `HashSet<string>` plus insertion queue (`OrdinalIgnoreCase`, 128 names,
oldest evicted), per transport instance; it is the transport's only mutable state, is pure logging state, and does not affect any
response, so `TRANSPORT-29`'s "effectively immutable" holds for the exchange (stated in the remarks). A logger that throws is
swallowed (`OBS-20`; the existing `LoggerMessage` delegates already guard).

**Events (P8b-13).** The 5b block 110-119 is filled; the constants go into core's `DexpaceLogEvents` (the vocabulary's one home,
`OBS-39`), and SystemNet's three private `EventId(1..3)` disappear:

| Id | Name constant | Name | Level |
|---|---|---|---|
| 110 | `TransportHeaderDropped` | `http.transport.header_dropped` | per policy (`Warning` / `Debug`) |
| 111 | `TransportFramingHeaderDropped` | `http.transport.framing_header_dropped` | `Debug` |
| 112 | `TransportInboundHeaderDropped` | `http.transport.inbound_header_dropped` | `Debug` |
| 113 | `TransportProxyFeatureUnsupported` | `http.transport.proxy_feature_unsupported` | `Warning` |

`LogVocabularyTests.Id_ranges_are_inside_the_reserved_blocks` (5b) covers the new ids; its reserved-block remark loses
"(reserved for the transport conformance phase)".

### F. Single-use bodies and native re-sends (P8b-10)

**Options.** (1) Buffer every single-use body up front (`ToReplayable`) on a re-send-capable construction; (2) **tee the first
write** into a bounded capture and replay it on a later serialisation; (3) never replay: a re-send always fails loudly
(`StreamConsumedException` → `ServiceRequestException`). **Chosen: (2).** (1) delays the first byte until the whole body is read
and holds it all, for every upload, even the ones that never re-send (G7: every `https://` target). (3) meets `TRANSPORT-18`'s
second sentence but not its first ("each subscription produce identical bytes"), and G6 shows it would fail **every** single-use
`http://` upload through a credentialed proxy. (2) costs one copy of at most `ResendBufferLimit` bytes during the send, and only
where a re-send can happen.

Mechanics: the capture is an unpooled chunk list (`IO-22`, the `ArrayPool` ban) filled by a write-through `Stream` wrapper around
the first serialisation's destination; it is complete only when `WriteTo`/`WriteToAsync` returned normally. A later serialisation
writes the chunks if complete, else throws `StreamConsumedException` ("The transport re-sent a single-use request body larger than
`ResendBufferLimit` (… bytes) or whose first write failed; buffer it with `ToReplayableAsync` first."). The capture is released
when the content is disposed (with the request message, at the end of the call). Replayable bodies, including seekable stream
bodies with a declared length (3b) and `FileRequestBody`, are never captured. The owned construction without proxy credentials is
provably re-send-free (no server credentials, no proxy credentials, no redirects, HTTP/1.1 only, and `SocketsHttpHandler` retries a
pooled connection only before the body starts, F6), so it skips the capture entirely.

**The default limit, 1 MiB, is open for the lead.** It is sized for G6's case — single-use `http://` uploads through an
authenticating proxy, a narrow population — and for the cost on the broad one (every single-use upload through a borrowed or
handler-built client pays up to the limit in memory during its send). A consumer with larger streams raises it or buffers.

### G. After dispose, and the adaptation guard (P8b-16, P8b-17)

`SEAM-15`: both faces call `ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this)` first; on the async face it
runs inside the `async` method, so it faults the task (`TRANSPORT-21`). Borrowed clients now refuse too, which retires the
documented asymmetry in today's remarks. A call already in flight when `Dispose` runs is cancelled by the owned `HttpClient`'s
dispose (row 4 of the table) or, for a borrowed client, completes normally; `Dispose` never waits (`TRANSPORT-16`).

`TRANSPORT-22`: the guard's `response.Dispose()` becomes `NativeDisposal.DisposeQuietly(response, primary)`, so a throwing native
dispose is attached to the primary's trail and never replaces it (design §3.7's rule, without making core's `Disposal` public).

---

## The public surface

### `src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt` (added lines; nothing removed)

```text
Dexpace.Sdk.Http.SystemNet.HeaderDropLogging
Dexpace.Sdk.Http.SystemNet.HeaderDropLogging.Every = 1 -> Dexpace.Sdk.Http.SystemNet.HeaderDropLogging
Dexpace.Sdk.Http.SystemNet.HeaderDropLogging.FirstPerName = 0 -> Dexpace.Sdk.Http.SystemNet.HeaderDropLogging
Dexpace.Sdk.Http.SystemNet.HeaderDropLogging.VerboseOnly = 2 -> Dexpace.Sdk.Http.SystemNet.HeaderDropLogging
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.SystemNetHttpClient(Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions! options) -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.SystemNetHttpClient(System.Net.Http.HttpClient! client, Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions! options) -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.SystemNetHttpClient(System.Net.Http.HttpMessageHandler! handler) -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient.SystemNetHttpClient(System.Net.Http.HttpMessageHandler! handler, Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions! options) -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.SystemNetHttpClientOptions() -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.HeaderDropLogging.get -> Dexpace.Sdk.Http.SystemNet.HeaderDropLogging
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.HeaderDropLogging.init -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Logger.get -> Microsoft.Extensions.Logging.ILogger!
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Logger.init -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Proxy.get -> Dexpace.Sdk.Core.Configuration.ProxyOptions?
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Proxy.init -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.ResendBufferLimit.get -> long
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.ResendBufferLimit.init -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.TimeProvider.get -> System.TimeProvider!
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.TimeProvider.init -> void
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Timeout.get -> System.TimeSpan?
Dexpace.Sdk.Http.SystemNet.SystemNetHttpClientOptions.Timeout.init -> void
// plus the compiler-generated record members (Equals, GetHashCode, ToString, PrintMembers, <Clone>$, the copy constructor,
// op_Equality, op_Inequality, EqualityContract), which RS0016 lists and the plan copies verbatim
```

The four existing constructors keep their signatures; `(ILogger)` becomes `this(new SystemNetHttpClientOptions { Logger = logger })`
and `(HttpClient, ILogger)` likewise. The internal test seam `SystemNetHttpClient(Action<SocketsHttpHandler>)` gains an options
parameter. No member is removed, so no `PublicAPI.Shipped.txt` concern arises (it is empty until phase 12).

### `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt` (added lines)

```text
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportHeaderDropped = "http.transport.header_dropped" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportHeaderDroppedId = 110 -> int
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportFramingHeaderDropped = "http.transport.framing_header_dropped" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportFramingHeaderDroppedId = 111 -> int
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportInboundHeaderDropped = "http.transport.inbound_header_dropped" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportInboundHeaderDroppedId = 112 -> int
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportProxyFeatureUnsupported = "http.transport.proxy_feature_unsupported" -> string!
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportProxyFeatureUnsupportedId = 113 -> int
```

### Packages, projects and dependencies

No new package, csproj or test project (P8b-19). `Dexpace.Sdk.Http.SystemNet` keeps its single `ProjectReference` to core and no
third-party dependency (constraint 2; `scripts/ci/dependency-audit.cs` unchanged). `Directory.Packages.props` unchanged
(`Microsoft.Extensions.TimeProvider.Testing` is already pinned and referenced by the test project through TestSupport). No lock
file changes. Internal types added: `ProxyOptionsWebProxy`, `BasicOnlyCredentials`, `HeaderDropLog`, `ResendCapture`,
`SendFailures` (the classification helper), `NativeDisposal`, `HandlerChain` (the redirect check). Each method stays under
`MA0051`'s 70 lines; `ToHttpRequestMessage` is split along the header table's rows.

---

## Breaking changes

All in `CHANGELOG.md` `[Unreleased]`, each with a migration line. Nothing is published, so none breaks a released contract.

1. **The owned transport no longer reads `HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY` implicitly** (P8b-4). Migration:
   `new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() })`.
2. **`Execute` is genuinely synchronous** (P8b-9): over a borrowed client whose primary handler implements only `SendAsync` it
   throws `NotSupportedException`; an async-only `DelegatingHandler` in a borrowed chain is skipped on the sync face (G2). Migration:
   implement `Send` in the handler, or call `ExecuteAsync`.
3. **`RequestOptions.Timeout` is enforced** (was ignored), until the response headers; the owned `HttpClient.Timeout` is now
   infinite and the transport's 100-second default replaces it (same observable bound).
4. **An internal cancellation is terminal** (P8b-7): `CancelPendingRequests` on a borrowed client, or its disposal mid-call, used
   to surface as a retryable `ServiceRequestTimeoutException` and is now an `OperationCanceledException` (not retried).
5. **Every call after `Dispose` throws `ObjectDisposedException`**, borrowed clients included (`SEAM-15`).
6. **The caller's `Content-Type` wins over the body's** (`TRANSPORT-10`); content headers on a body-less request are sent (with
   `Content-Length: 0`) on body-permitted methods and dropped with a log on `GET`/`HEAD`/`TRACE`/`CONNECT` (were dropped silently).
7. **Header-drop logging**: event ids 1-3 become 110-112 with `http.transport.*` names; under the default `FirstPerName` policy a
   repeated drop of the same name is logged at `Debug`, not `Warning`.
8. **The handler-taking constructor rejects a redirect-following primary handler** (new surface; listed because
   `new SystemNetHttpClient(new SocketsHttpHandler())` throws: `AllowAutoRedirect` defaults to `true`).
9. **A native re-send of a single-use body is replayed** up to `ResendBufferLimit` (was: always failed with a wrapped
   `StreamConsumedException`); beyond it the failure message names the limit.

## Shared files with 8a

| File or folder | 8a | 8b |
|---|---|---|
| `src/Dexpace.Sdk.Http.SystemNet/**` | none | all of 8b's code |
| `src/Dexpace.Sdk.Core/Diagnostics/DexpaceLogEvents.cs`, core `PublicAPI.Unshipped.txt`, `BannedSymbols.txt` | none | the four events; the `DefaultProxy` ban |
| `src/Dexpace.Sdk.Conformance/**` | creates | **one amendment requested** (below); may fix an assertion bug it finds, with its negative control, in its own PR (P8a's own rule) |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs` | creates, with the `Owner = "8b"` waivers | deletes each waiver in the PR that closes its row; adds the handler-constructor subject and the `CreateWithProxy` hook; adds the permanent `TRANSPORT-14` name waiver |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/*WireTests.cs`, `Security/*` | `using` swap to `Dexpace.Sdk.Conformance.Wire` | new classes only; existing `Security` classes unedited |
| `CHANGELOG.md`, roadmap status notes, `CLAUDE.md`, `docs/first-release.md` | own entries, append-only | own entries, append-only |

**The requested kit amendment (8a, before 8b's PR 3).** `TRANSPORT-14` is one requirement with three clauses, of which SystemNet
meets two and cannot meet one. 8a's waivers are by requirement ID and a waiver whose assertions pass is reported stale (P8a-8),
so an ID-wide permanent waiver would either hide the two met clauses or fail as stale. 8b asks 8a to (1) split
`transport-14.inbound-lenient` into `transport-14.value-control-dropped`, `transport-14.obs-text-preserved` and
`transport-14.name-malformed-dropped`, and (2) give `ConformanceWaiver` an optional `Assertion` (a stable assertion name) that
narrows the waiver to that assertion, with the stale rule computed per (ID, assertion). Ruby's 8c needed the same shape for the
same clause (`P8-38`). If 8a declines (2), the fallback is (1) alone plus a reading of the stale rule that counts a waiver stale only
when every assertion citing its ID passes; 8b's design is unaffected either way. P8b-14.

## Test strategy

| Project / class | Category | What |
|---|---|---|
| `SystemNet.Tests/HandlerConstructorTests` | `Unit` | The chain walk (P8b-3) over stub handlers: rejections, acceptances, no mutation, the borrowed handler never disposed (counting handler). |
| `SystemNet.Tests/SystemNetHttpClientOptionsTests` | `Unit` | Defaults, every `init` rule, `with` cannot bypass them, `ToString` masks the proxy credential (via `ProxyOptions.ToString`), the `Proxy`-with-borrowed rejection. |
| `SystemNet.Tests/FailureClassificationTests` | `Unit` | The six rows of the table on both faces, over stub handlers that throw each native shape (G4, F3) and a `FakeTimeProvider`-driven deadline; the row-4 exception has no I/O cause and `RetryFacts`' public behaviour through a `DexpacePipeline` does not retry it; the row-2/3 exceptions are retried by the pipeline. |
| `SystemNet.Tests/DeadlineTests` | `Unit` | `TRANSPORT-5`/`-6` with `FakeTimeProvider`: per-call over default, `null` keeps the default, two concurrent calls with different deadlines, clamps, no source allocated without a deadline (an allocation-free assertion is not attempted; a counting `TimeProvider` sees no timer). |
| `SystemNet.Tests/HeaderPartitionTests` | `Unit` | The header table over a capturing handler (`HttpRequestMessage` inspection): every cell on both faces. |
| `SystemNet.Tests/HeaderDropLogTests` | `Unit` | P8b-12's modes, case folding, bound, concurrency, events 110-112 with names and levels, no value logged. |
| `SystemNet.Tests/ProxyAdapterTests` | `Unit` | `GetProxy` per type (IPv6 bracketed, no userinfo), `IsBypassed` through the glob dialect (leading-dot literal, `BypassAll`), `BasicOnlyCredentials` answers only `Basic`, the three construction warnings (event 113, no value), the owned handler's `UseProxy`/`Proxy`/`Credentials` through the internal seam. |
| `SystemNet.Tests/SyncExecuteTests` | `Unit` | `Execute` through a `Send`-only handler (proves the path is real), G1's `NotSupportedException`, the after-dispose `ObjectDisposedException` on both faces, the rewritten `The_sync_Execute_passes_the_token_through` (its handler gains `Send`). |
| `SystemNet.Tests/Architecture/NoImplicitEnvironmentReadArchitectureTests` | `Unit` | The `CFG-28` IL scan (hand-off table), with a self-test that the scanner finds a planted call in a test-only type. |
| `SystemNet.Tests/SyncExecuteWireTests` | `Integration` | Over the 8a fixture: a sync GET and a sync POST with a single-use stream body written once; the sync `traceparent` strip row (5c); the blocking pager over `Execute` with a per-call timeout (7c's inherit-only hand-off). |
| `SystemNet.Tests/ContentTypeWireTests`, `ResendCaptureWireTests`, `TimeoutWireTests` | `Integration` | `TRANSPORT-10` cases; G5/G6 re-send through the fixture as a forward proxy and the over-limit failure; real-time deadlines (generous bounds; condition waits, no sleeps). |
| `SystemNet.Tests/Security/ProxyCredentialWireTests` | `Security` | `TRANSPORT-30`'s MUST: the proxy's `407` is answered, an origin `401` behind it is not, no `Authorization` header ever carries the proxy credential, and no log entry (at any level) contains the user name or password. Permanent, like the phase 1 classes. |
| `SystemNet.Tests/SystemNetConformanceTests` | `Conformance` | 8a's driver: the owned subject (now with `CreateWithProxy`) and a new handler-constructor subject; every `Owner = "8b"` waiver deleted; the one permanent assertion-scoped `TRANSPORT-14` waiver per subject, citing §10 entry 32. |
| existing classes | as today | `Options_are_accepted_and_ignored_until_8b` is replaced by `DeadlineTests`; every other existing test stays, the phase-1 `Security` classes unedited. |
| `tests/Dexpace.Sdk.AotSmoke` | `AotSmoke` | `CheckPhase8bAsync`: a sync `Execute` and an async call against a loopback listener through the handler constructor, a deadline that fires, and a constructed `ProxyOptionsWebProxy` (proves the adapter, the classification and the sync path survive trimming). |

**Ports.** Node `drop-log.ts` → `HeaderDropLog` (modes, the 128 bound, oldest-first eviction; "quiet" replaced by `VerboseOnly`);
Node `default-timeout.ts` → the options' `Timeout` range check; Node `abort-mapping.ts` and `dispatch-classification.ts` → the
classification table's shape (state first, exception shape second). Ruby 8a `R5` → `TRANSPORT-28`'s split; Ruby 8c `P8-38` →
`TRANSPORT-14`'s name half; Ruby 8a `P8-6` (raise on a borrowed per-call timeout) **not** ported, with the reason in §C.

**Flake budget.** Deadline behaviour is pinned with `FakeTimeProvider` in `Unit`; the real-time `Integration` and kit rows use the
kit's bounds (30 s per assertion) and deadlines of at least 200 ms against a fixture that never answers, so no row races a
scheduler.

## PR segmentation

Four stacked pull requests, after 8a's PR 2 has merged; each closes its rows by deleting their waivers in the same PR, so every PR
is green under the stale-waiver rule. Code and tests travel together (roadmap step 5).

1. **Construction and proxy.** `SystemNetHttpClientOptions`, `HeaderDropLogging` (type only), the four constructors, the handler
   check (`TRANSPORT-1`), `SEAM-15`, the proxy adapter and warnings (`TRANSPORT-30`), the owned handler's `UseProxy = false`, the
   `DefaultProxy` ban, the IL scan, `NativeDisposal`, event 113 in core. Waivers deleted: `seam-15.after-dispose`; hook supplied:
   `CreateWithProxy`; subject added: the handler constructor.
2. **Send path.** The classification helper and deadline (`TRANSPORT-4`, `-5`, `-6`, `-8`, `-9`), the real `Execute` and
   `SerializeToStream` (`TRANSPORT-17`, `SEAM-11`, `PIPE-28`), the re-send capture (`TRANSPORT-18`). Waivers deleted:
   `transport-4.*`, `-5.*`, `-6.*`, `-8.*`, `-18.*`.
3. **Headers.** The partition and `Content-Type` authority (`TRANSPORT-10`), per-header drops on both faces (`TRANSPORT-12`), the
   policy (`TRANSPORT-13`, `OBS-19`), events 110-112 in core, the inbound event (`TRANSPORT-14`'s value half), the permanent
   `TRANSPORT-14` name waiver (needs 8a's amendment). Waivers deleted: `transport-10.*`, `-12.*`, `-13.*`, `-14.*` (owner 8b).
4. **Close-out.** `docs/sdk-documentation/transport.md` (construction and ownership, proxy, deadlines and the classification table,
   the sync path and its handler hazard, header mapping and the drop policy, events, the re-send limit); the package README; the
   checklist (14 rows, plus the non-row hand-offs as a second table); `CHANGELOG.md`; design §10 entry 32 and the other
   corrections below; `docs/first-release.md` entries; the dated roadmap status note; the knowledge note; the housekeeping probe.

## Hand-offs to later phases

- **9** — the DI package builds named clients over the handler constructor (its primary handler with `AllowAutoRedirect = false`;
  the constructor now refuses anything else) and binds `SystemNetHttpClientOptions` from `IConfiguration` (`Timeout` through the
  phase 9 TimeSpan converter; `Proxy` through `ProxyOptions.FromEnvironment(key => configuration[key], logger)`, the one explicit
  resolver call, P8b-4); the third conformance driver (P8a's hand-off) runs over it. The `CallKey` carrier and the
  `HttpRequestOptionsKey<CallKey>` stamp (P8b-18), if the lead rules P4a-13, land with the first reader: a DI-registered
  `DelegatingHandler`.
- **10** — audits `XCUT-2` (the classification table), `XCUT-18` (the wire re-check and drop logs) and `XCUT-22` against 8b's
  evidence.
- **11** — `TRANSPORT-14`'s name half is one of the at-most-five *Unsatisfied MUSTs* the parity audit weighs.
- **12** — release notes carry breaking changes 1, 2 and 4 prominently; the `sync-borrowed-handlers` asymmetry and the
  `TRANSPORT-28` declined SHOULD are in `docs/first-release.md`.

## Design, roadmap, register and `CLAUDE.md` corrections owed at close-out

Proposals, applied as dated corrections when the checklist lands, not by this document:

1. **Design §3.2** — As built (8b): the real sync path, the header partition, the handler constructor, `SEAM-15`. Its
   `TRANSPORT-2` paragraph gains P8b-10 (bounded capture rather than "fails the send"). **§3.3** "Deadlines" — the transport's
   deadline bounds until the response headers (G3) and runs on the options' `TimeProvider`. **§3.7** — `SEAM-15` built for every
   construction. **§8.2** — "calls that resolver explicitly at construction" corrected to the strict reading (P8b-4).
   **§9.1** — the banned list gains `HttpClient.DefaultProxy`. **§10** — new **entry 32**: `TRANSPORT-14`'s malformed-name clause
   is unmet on `SocketsHttpHandler` (F1), touching `TRANSPORT-14` only. **§11 item 18** — the re-send is measured (G5, G6, G7) and
   answered by capture. **§12** — the `TRANSPORT` row cites 8b's checklist.
2. **Roadmap** — a dated status note: the 14 rows; every non-row hand-off's disposition (the table above), including `LateResult`
   not needed, `Disposal` kept internal, the `CallKey` stamp moved to 9, and the proxy reading; D3 untouched (8a's).
3. **Checklists** (dated corrections): phase 2b `SEAM-11` and `SEAM-15`; phase 4c `PIPE-28`; phase 5b `OBS-19`; phase 1's S1, S2,
   S3 and S9 rows' 8b clauses; phase 5a's F8 (the transport IL scan).
4. **`docs/first-release.md`** — *Unsatisfied MUSTs*: `TRANSPORT-14` (name half, §10 entry 32). *SHOULD declined*: `TRANSPORT-28`'s
   zero-copy clause. *Behavioural asymmetries*: `sync-borrowed-handlers` (G1, G2) and `explicit-proxy` (P8b-4). The 6c line
   "`407` … answered by the transport from `ProxyOptions.ChallengeCredentials`, phase 8b" gains "or the Basic pair".
5. **`CLAUDE.md`** — "Transports are ownership-aware" gains the handler constructor's borrow-and-reject rule and the explicit proxy;
   "What is genuinely unbuilt" loses phase 8's transport half; `docs/sdk-documentation/transport.md` is linked.
6. **Knowledge** — `docs/knowledge/notes/transport-adapter.md` (8a creates it) gains G1 to G8.
7. **`src/Dexpace.Sdk.Http.SystemNet/README.md`** — construction table, proxy migration line, sync-path hazard.

## Risks and open questions (all resolved here)

| # | Question | Resolution |
|---|---|---|
| Q1 | Does 8b own any `ASYNC` row? | No (8a's split; P8b-1). |
| Q2 | Can `TRANSPORT-14`'s name half be met? | No: the native parser refuses the response (F1). §10 entry 32, permanent assertion-scoped waiver (P8b-14), open for the lead because it spends one of phase 11's five. |
| Q3 | Does a constructor-time `FromEnvironment` satisfy `CFG-28`? | No, by its "at construction" letter: strict reading (P8b-4), open for the lead. |
| Q4 | Real sync for borrowed clients, given G2? | Yes, with the hazard documented (P8b-9), open for the lead. |
| Q5 | Does the deadline cover the body read? | No: until headers, like `AttemptTimeout` (P8b-6). |
| Q6 | What does an internal cancel surface as, and why not a new SDK type? | `OperationCanceledException`, native exception on the trail (P8b-7, G8), open for the lead. |
| Q7 | Is `LateResult` needed for `TRANSPORT-9`? | No (P8b-8). |
| Q8 | Buffer, tee or fail on a native re-send? | Tee, bounded, 1 MiB default (P8b-10), default open for the lead. |
| Q9 | Where do the 110-119 constants live? | Core's `DexpaceLogEvents` (P8b-13). |
| Q10 | Make `Disposal` public? | No; a local helper over public trail APIs (P8b-17). |
| Q11 | Stamp the `CallKey`? | Not in 8b; no carrier exists (P8b-18), open for the lead. |
| Q12 | Special-case `FileRequestBody`? | No (P8b-15). |
| Q13 | Constructors or factories? | Constructors plus an options record (P8b-2), open for the lead. |

**Residuals.** R1: a borrowed client's own `Timeout` caps a longer per-call deadline (documented; cannot be lifted without
mutation). R2: G2's silent handler bypass on the sync face is the runtime's semantics and is only documented. R3: an unknown
primary handler that follows redirects is caught only by the last-line URI check, which misses a redirect ending on the URI sent
(phase 1's stated limit). R4: the drop-log seen-set is per instance, so a process with many short-lived transports warns once per
transport, not once per process (acceptable; transports are meant to be long-lived). R5: SOCKS credential handling is verified in
plan task 0, not here (G5 measured HTTP only). R6: 8b cannot start before 8a's PR 2, and its PR 3 needs 8a's waiver amendment.

## Exit criteria

From the card, for 8b's share:

1. 8b's checklist has **14 rows**: each ✅ with a named test (kit assertion and SystemNet test), `[Trait]` and file; `TRANSPORT-14`
   ✅ for its value and obs-text clauses and 🚫 for its name clause with §10 entry 32; `TRANSPORT-28` ✅ for its MUST and 🚫 (declined
   SHOULD) for zero-copy. A second table records the twelve non-row hand-offs' dispositions; `SEAM-11`, `SEAM-15`, `PIPE-28` and
   `OBS-19` flip to ✅ in their own checklists by dated correction.
2. The kit is **green against both SystemNet subjects and the raw-socket driver on every OS row** (constraint from the card), with
   waivers listed by ID: **no waiver with `Owner = "8b"` remains**, `transport-30.*` is no longer `NotExercised`, and the only
   permanent SystemNet waiver is the assertion-scoped `TRANSPORT-14` name clause.
3. No sync-over-async remains in `src/Dexpace.Sdk.Http.SystemNet` (no `RS0030` pragma on `Execute`); the IL scan passes.
4. Every gate green: build (warnings as errors, `RS0016`/`RS0017` for both changed `PublicAPI` files, `RS0030`, `MA0051`, `CA2007`),
   `dotnet format --verify-no-changes`, the full test run with the phase-1 `Security` classes unedited, the 80% coverage gate, the
   dependency audit (unchanged), the reproducible pack, the AOT smoke (with `CheckPhase8bAsync`), `scripts/knowledge
   verify-structure`.
5. `docs/sdk-documentation/transport.md`, the README, `CHANGELOG.md` `[Unreleased]` (nine breaking changes with migrations), the
   dated roadmap status note, the corrections above, the knowledge note, and a housekeeping probe that reports nothing.

## Rulings

| ID | Ruling | Open for the lead |
|---|---|---|
| P8b-1 | 8a's split taken as given: 8b owns 14 `TRANSPORT` rows and no `ASYNC` row; plus twelve non-row hand-offs, each dispositioned | — |
| P8b-2 | One `SystemNetHttpClientOptions` sealed record and four new constructors; the four existing constructors kept as sugar; no static factories | **yes** (factories instead) |
| P8b-3 | The handler constructor borrows the handler, owns an `HttpClient` over it, walks the `DelegatingHandler` chain, rejects a redirect-following `SocketsHttpHandler`/`HttpClientHandler` with `ArgumentException`, accepts unknown primaries, never mutates | — |
| P8b-4 | Proxy installed on the owned client only; no implicit environment read (strict `CFG-28`): `UseProxy = false` unless `Proxy` is set; `HttpClient.DefaultProxy` banned in `src/`; the IL scan | **yes** (§8.2's constructor-time resolver instead) |
| P8b-5 | `ProxyOptionsWebProxy`: no userinfo in the proxy URI, glob bypass, `ChallengeCredentials` else Basic-only fallback; server credentials never set; three construction warnings (event 113), no value logged | — |
| P8b-6 | Per-call deadline = timer source on the options' `TimeProvider` linked with the caller's token, until response headers; default 100 s; owned `HttpClient.Timeout` infinite; borrowed `Timeout` an outer bound; clamps 1 ms / 49 days | — |
| P8b-7 | One classification helper for both faces, in the table's order; an internal cancel is a new `OperationCanceledException` with the native exception on its `ExceptionTrail`, not its `InnerException` | **yes** (a new SDK exception type instead) |
| P8b-8 | `TRANSPORT-9` met structurally (no abandonment, no `TaskCompletionSource<Response>`); `LateResult` not used | — |
| P8b-9 | `Execute` calls `HttpClient.Send` on every construction; `SerializeToStream` over `WriteTo`; no sync-over-async fallback; G1/G2 documented as the `sync-borrowed-handlers` asymmetry | **yes** (keep sync-over-async for borrowed clients) |
| P8b-10 | Bounded tee-and-replay of a single-use body's first write on re-send-capable constructions; `ResendBufferLimit` default 1 MiB; over-limit or incomplete capture fails with `StreamConsumedException` → `ServiceRequestException`; the owned client without proxy credentials never captures | **yes** (the default limit) |
| P8b-11 | Header partition by the content-header set; caller `Content-Type` authoritative; zero-length content carries content headers on body-less body-permitted methods; dropped and logged on `GET`/`HEAD`/`TRACE`/`CONNECT`; every refused add is a logged drop | — |
| P8b-12 | `HeaderDropLogging` `FirstPerName` (default) / `Every` / `VerboseOnly`; per-instance, case-insensitive, 128-name FIFO-bounded seen-set; framing and inbound drops always `Debug` | — |
| P8b-13 | Events 110-113 (`http.transport.*`) as constants in core's `DexpaceLogEvents`; SystemNet's private ids 1-3 retired | — |
| P8b-14 | `TRANSPORT-14`'s name clause unmet on a stated domain: §10 entry 32, *Unsatisfied MUSTs*, a permanent assertion-scoped waiver; 8a asked to split the assertion and scope waivers by assertion | **yes** (spends one of the five) |
| P8b-15 | `TRANSPORT-28`: MUST half met by `FileRequestBody`; zero-copy SHOULD declined; no `FileRequestBody` special case | — |
| P8b-16 | `SEAM-15`: `ObjectDisposedException` from both faces after dispose for every construction, as a faulted task on the async face | — |
| P8b-17 | `TRANSPORT-22`'s guard disposes through a local `NativeDisposal` over public `ExceptionTrail`/`ExceptionFacts`; core's `Disposal` stays internal; adaptation failures are not re-typed | — |
| P8b-18 | The `HttpRequestOptionsKey<CallKey>` stamp is not built in 8b (no carrier on `RequestOptions`; P4a-13 unruled); handed to phase 9 | **yes** |
| P8b-19 | No new package, project, dependency or lock-file change | — |
| P8b-20 | Test categories as tabled; one new `Security` class (`ProxyCredentialWireTests`); the phase-1 `Security` classes unedited | — |
| P8b-21 | The SystemNet conformance driver gains a handler-constructor subject and the `CreateWithProxy` hook; each PR deletes the waivers its rows close | — |
| P8b-22 | Four stacked PRs (construction and proxy; send path; headers; close-out), starting after 8a's PR 2 | — |
| P8b-23 | Owned-client native defaults the card does not name (`UseCookies`, `PooledConnectionLifetime`, `AutomaticDecompression`) are left as they are | — |
| P8b-24 | Port sources: Node `transport-shared@c0ff3fd` (`drop-log`, `default-timeout`, `abort-mapping`, `dispatch-classification`), Ruby 8a `R5` and 8c `P8-38`; Ruby `P8-6` not ported | — |

## Deviation Ledger

Entries for roadmap constraint 7. **One new specification deviation**: `TRANSPORT-14`'s malformed-inbound-name clause, unmet on
`SocketsHttpHandler` (P8b-14), proposed as design §10 entry 32 and a `docs/first-release.md` *Unsatisfied MUSTs* line. One declined
SHOULD clause: `TRANSPORT-28`'s zero-copy path (P8b-15). Everything else rests on existing entries: `TRANSPORT-20`'s and
`TRANSPORT-4`'s "I/O family" letter is §10 entry 7, the cancellation shape of `TRANSPORT-3` and `TRANSPORT-8` is §10 entry 8, and
`TRANSPORT-18`'s binding is §11 item 18. Departures from the design's text, each a close-out correction (item 1 above): P8b-4
(§8.2's constructor-time resolver), P8b-6 (§3.3's deadline scope), P8b-10 (§3.2's "fails the send"). Departures from the roadmap
card: none — every card item for 8b is built (the real `Execute` and `SerializeToStream`, the linked deadline with timeout
classified apart from cancellation, the content-header partition with the caller's `Content-Type` authoritative, per-header drops,
`TryParse` and dispose-on-throw on both faces, the `ObjectDisposedException` latch, the redirect-refusing handler constructor, and
`ProxyOptions` installation).

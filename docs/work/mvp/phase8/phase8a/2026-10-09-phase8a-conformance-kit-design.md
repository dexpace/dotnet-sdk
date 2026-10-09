# Phase 8a — The Conformance Kit: Design

**Status:** Draft, for review. Written 2026-10-09 against `main` at `ce0f68c` (phases 0 to 7c merged), on branch
`87-phase-8-planning`, for issue [#88](https://github.com/dexpace/dotnet-sdk/issues/88). Brainstormed without a human in the
loop: every judgement call the brainstorming skill would have put to the lead is taken here as a numbered ruling (`P8a-n`) with
its options and rationale, and the ones the lead may still reverse are marked **open for the lead**. Sub-phase 8b
(reference-transport hardening, issue [#89](https://github.com/dexpace/dotnet-sdk/issues/89)) is being designed in parallel by
another author; 8b depends on 8a (roadmap, "Segmentation rule": "8b is proven by 8a's kit"), and the files both touch are listed
in [Shared files with 8b](#shared-files-with-8b). The scope authority is the roadmap's Phase 8 card
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`, "Phase 8 — Transports and the Conformance Kit") together with
decision D3 and the dependency-graph line "8a then 8b". The format follows the phase 7 designs
(`docs/work/mvp/phase7/phase7b/2026-10-09-phase7b-sse-design.md`).

**What this document is.** The sub-phase design for 8a, and the phase 8 segmentation. It holds:

- the **segmentation of phase 8**: every one of the 52 `TRANSPORT`/`ASYNC` IDs assigned to exactly one of 8a and 8b, with the
  rule that decides it ([Segmentation](#segmentation-the-52-ids-split-between-8a-and-8b));
- the verdicts on decision D3's no-second-transport argument and on `Dexpace.Sdk.Reactive`;
- one disposition per requirement row 8a owns (38 rows), and the hand-over list for the 14 rows 8b owns;
- the new package `Dexpace.Sdk.Conformance`: its csproj, its first published version, its public surface
  (`PublicAPI.Unshipped.txt`), the promoted wire fixture, the assertion catalogue, the reporting and waiver model, and the
  capability hooks 8b's tests will drive;
- the rulings, which double as the deviation-ledger IDs (roadmap constraint 7);
- the test strategy, the sibling ports, PR segmentation, hand-offs, close-out corrections and exit criteria.

**What this document is not.** It is not the plan and not the checklist. It does not re-argue design §10 entry 7 (transport
failures are `SdkException`s, not `IOException`s), §10 entry 8 (cooperative cancellation), §11 item 18 (`TRANSPORT-18` binds
SystemNet) or §11 item 21 (`ASYNC-21` adapter-scoped); it cites them. It changes no line of `src/Dexpace.Sdk.Http.SystemNet`
(that is 8b's), edits no design chapter, roadmap cell or `CLAUDE.md` line; the corrections it owes are listed in
[Corrections owed at close-out](#design-roadmap-register-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

| Predecessor or sibling | Kind | State at `ce0f68c` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051`, `CA2007` for `src/`, `IsTrimmable`/`IsAotCompatible`, package validation, the dependency audit, the coverage gate, reproducible pack, `TestCategoryTests`) | **dependency** | Met. The kit is a `src/` library and is written under all of them from its first line. The CI header in `.github/workflows/ci.yml` lists "Transport conformance kit (Dexpace.Sdk.Conformance, TRANSPORT-*): phase 8" as a gate wired-but-inert; 8a turns it on (roadmap constraint 1). |
| Phase 0 — the loopback fixture (`tests/Dexpace.Sdk.Http.SystemNet.Tests/Loopback/`) | **dependency** | Met. `LoopbackServer`, `LoopbackResponse`, `RecordedRequest`, `RawRequestReader`, `MalformedRequestException`: BCL-only, framework-free, written "because roadmap phase 8a promotes it into `Dexpace.Sdk.Conformance`" (its own remarks). It is not yet library-grade: no `ConfigureAwait(false)`, test-project analyzers only. |
| Phase 2b — the SPI with `RequestOptions` and a token, `DelegateHttpClient.Create`/`CreateBlocking`, the bridges | **dependency** (card "Entry") | Met. `DelegateHttpClient`'s two names exist "because a lambda that only throws is … exactly the failing transport a conformance kit needs" (P2b-6). |
| Phase 3a/3b — exact-length stream bodies, `FileRequestBody`, the dispose latches, `Disposal` | **dependency** | Met. `HTTP-39` and `TRANSPORT-28`'s byte range are expressible against public `RequestBody.FromStream(stream, length)` and `RequestBody.FromFile(path, type, offset, count)`. |
| Phase 4c — `HttpPipeline` as a transport, `PipelineRunner`'s null-result check | **dependency** (card "Entry") | Met. |
| Phase 5a — `TimeProviderWaits` (public), the clock/delay/environment bans | **dependency** | Met. The kit waits through `TimeProviderWaits.DelayAsync`, never `Task.Delay` (banned in `src/`). |
| Phase 6a / 6b — retry and redirect authority | **dependency** (card "Entry") | Met. 6a's hand-off asks the kit to assert "a transport maps a pre-response failure to something the classifier calls retryable and honours the attempt-linked token"; taken (P8a-18). |
| Phase 7b — `LoopbackResponse.Streamed` | convenience | Met. Promoted with the fixture; `ServerSentEventWireTests`' gated-delivery property becomes the kit's lazy-body assertion (P8a-17). |
| 8b | **dependent** | 8b consumes the kit: its rows close when the kit's assertions go green against `SystemNetHttpClient` and its pending waivers are deleted. 8b may plan in parallel; it executes after 8a's package and fixture move have merged. |

## Governing documents, and the phase-start queries

Read for this design: product-spec chapters 17 and 18 in full and appendix B; appendix C rows `TRANSPORT-1`..`-30`,
`ASYNC-1`..`-22`, `SEAM-11`..`-16`, `SEAM-30`, `HTTP-39`, `XCUT-22`; design §2.1, §2.2, §3.2, §3.3, §3.7, §9.3, §10 entries 7
and 8, §11 items 18 and 21, §12's `TRANSPORT` and `ASYNC` rows and its "Vacuous" paragraph; the roadmap's constraints 1, 3, 4,
5 and 7, decision D3, the Phase 8 card, and every Phase Status Note bullet addressed to 8a or 8b; `docs/first-release.md`
(*Packages*, *Post-v1 packages*, *Post-release triggers*); the code under `src/Dexpace.Sdk.Http.SystemNet`,
`src/Dexpace.Sdk.Core/Client`, `tests/Dexpace.Sdk.Http.SystemNet.Tests` (including `Loopback/`),
`tests/Dexpace.Sdk.Core.Tests/Client` and `tests/Dexpace.Sdk.TestSupport`.

```bash
scripts/knowledge --origin note --brief           # 34 entries in 10 note files; two touch the transport: the traceparent strip rule (5c) and
                                                   # HttpResponseMessageBody.OpenRead arriving with 7b
scripts/knowledge --section conflicts --brief     # no TRANSPORT/ASYNC conflict; CA2007-on-src is conformed (binds the kit)
scripts/knowledge --prefix-info TRANSPORT         # 30 IDs, 24 MUST + 6 SHOULD, 30 of 30 substantive
scripts/knowledge --prefix-info ASYNC             # 22 IDs, 18 MUST + 4 SHOULD, 22 of 22 substantive
scripts/knowledge --gaps TRANSPORT ; scripts/knowledge --gaps ASYNC   # 0 gaps: nothing to read out of appendix C alone
```

The corpus holds nothing for these prefixes that the chapters and the design do not. One harvested source is stale
(`09-toolchain-and-quality-gates.md`, 6 entries); §9.3 was read from the file, not the corpus.

**Sibling sources.** The card names `ruby-sdk/gems/dexpace-conformance/lib/dexpace/conformance/` (`transport_suite/` with 34
assertions, `wire_server/`, `scripts.rb`, `levels.rb`, `result.rb`, `report.rb`, `vacuous.rb`, `aggregate.rb`). **None of it
exists in the local checkout on any branch** (`ruby-sdk@90075b1`, `mvp` at `fa402cd`, `main`): Ruby has design documents only.
Its phase 8a design (`ruby-sdk/docs/work/mvp/phase8/phase8a/2026-09-11-phase8a-synchronous-transport-and-conformance-design.md`,
sections `R7` and `R16` "The suite contract") is the port source for the kit's shape: a factory not a mixin, five result statuses
with `vacuous` among them, waivers by ID, a fresh server per assertion, condition waits instead of sleeps, and twelve suite-contract
clauses. Node's `packages/transport-conformance/src/{run-suite,fixtures}.ts` at `nodejs-sdk@c0ff3fd` is the port source for the
assertion rows (its `describe` blocks are keyed by `TRANSPORT` ID) and for `TransportCapabilities` (an internal-cancel flag, a
proxy flag, a per-transport drop-set flag, an unsupported-proxy factory). Node's `transport-shared` helpers
(`drop-log`, `abort-mapping`, `body-less`, `header-mapping`) are the checklist for the header-drop and abort assertions. P8a-25.

---

## Facts the design rests on

Measured on .NET SDK 10.0.401 / runtime 10.0.12, Linux, against a raw `TcpListener` peer, with a `SocketsHttpHandler
{ AllowAutoRedirect = false }` client (the owned-client configuration). The probe was a scratch file-based app, not committed;
the plan re-runs each as a kit assertion.

| # | Fact | Consequence |
|---|---|---|
| F1 | A response header **name** with a space, a control byte or a non-ASCII byte makes `SocketsHttpHandler` fail the **whole response** with `HttpRequestException` ("Received an invalid header name: …"). A control byte in a header **value** passes through to `HttpResponseMessage.Headers`. | `TRANSPORT-14`'s name half is unreachable on the reference transport: the native client refuses before the adapter sees anything. The value half is met by `AddInbound`'s per-header drop. 8b owns the row and the `Unsatisfied MUSTs` entry (P8a-24). |
| F2 | A non-numeric `Content-Length` (`abc`) on a `Connection: close` response is tolerated: status 200, the body reads to close. | `TRANSPORT-27`'s length half is reachable on SystemNet; the kit asserts `ContentLength == -1` and a readable body. |
| F3 | `HttpClient.CancelPendingRequests()` and `HttpClient.Dispose()` during an in-flight `SendAsync`/`Send` both throw `TaskCanceledException` whose `InnerException` is an `IOException`; `HttpClient.Timeout` elapsing throws `TaskCanceledException` whose `InnerException` is a `TimeoutException`. | `SystemNetHttpClient` maps every `OperationCanceledException` the caller did not request to `ServiceRequestTimeoutException` (retryable). A borrowed client cancelled internally is therefore reported as a **retryable timeout**: a `TRANSPORT-8` defect. The discriminator 8b needs is `InnerException is TimeoutException` (or the state of 8b's own per-call source). |
| F4 | A body-less `POST` goes out with `Content-Length: 0`; a `GET` goes out with neither a body nor `Content-Length`. | `TRANSPORT-26` is met by the native client; the kit pins it. |
| F5 | A `520` with a body is surfaced as status 520 with a readable body. | `TRANSPORT-24` met. |
| F6 | On a reused keep-alive connection, a `POST` whose single-use content was fully written and whose response never began (peer closed) fails with `HttpRequestException` → `HttpIOException (ResponseEnded)`, and the content's `SerializeToStreamAsync` ran **once**: no internal re-send. | `TRANSPORT-2`'s observable clause holds today. The stale-connection re-send path (a write that fails before the body is sent) remains, and is guarded from the body side by `StreamConsumedException` (design §3.2). |
| F7 | `SystemNetHttpClient` skips the caller's `Content-Type` header and lets `RequestBodyContent` stamp the body's media type; on a body-less request a content header (`Content-Language`) that `HttpRequestHeaders` refuses is dropped without a log (the content-header fallback runs only when content exists). | `TRANSPORT-10` and `TRANSPORT-12` are defects in the current code: 8b's. |

---

## Segmentation: the 52 IDs split between 8a and 8b

The roadmap's segmentation rule applies to phase 8 (two ID-bearing chapters, and a new package beside the transport), and the
card leaves the cut to "8's segmentation design". This section is that design (P8a-1). It is written to stand alone, so the lead
can lift it verbatim into `docs/work/mvp/phase8/2026-10-09-phase8-segmentation-design.md` if the rule's letter (a separate file)
is wanted; by default it lives here and the housekeeping skill files this document under `phase8/phase8a/`.

**Order.** 8a, then 8b. A **dependency**, as the roadmap says: 8b's rows are proven by 8a's assertions, and 8b's test project
consumes the promoted fixture.

**The rule.** *An ID belongs to 8b when closing its row needs a change to `src/Dexpace.Sdk.Http.SystemNet` — a defect in the code
at `ce0f68c`, a work item the Phase 8 card lists under 8b, or a Phase Status Note hand-off addressed to 8b. Every other ID belongs
to 8a, which closes it with a kit assertion green against both drivers on the code as it stands, or with existing evidence, or as
N/A or vacuous with its section.* 8a writes **every** assertion, including those for 8b's rows; an 8b row's assertion ships
waived on the SystemNet driver (owner `8b`, P8a-8) wherever it fails at `ce0f68c`, and 8b closes the row by deleting the waiver.

**Two consequences, stated so they are not rediscovered.** First, the observable conformance of a sync face does not depend on
whether `Execute` is genuinely synchronous: `SystemNetHttpClient.Execute` is sync-over-async until 8b, and the kit's blocking-face
assertions pass against it because the behaviour they observe is the async path's. The *realness* of the synchronous path is
`SEAM-11`'s and `PIPE-28`'s (both already ⏳ 8b), not a `TRANSPORT` clause. So a row like `TRANSPORT-20` ("both sync throw and
async exceptional completion") closes in 8a, and 8b inherits the obligation to keep it green when it replaces the sync path.
Second, the informal "(8b)" labels earlier checklists put on `TRANSPORT-2` (6a), and the ⏳-to-8b clauses of phase 1's S2
(`TRANSPORT-11`) and S9 (`TRANSPORT-22`, `TRANSPORT-27`), predate this cut. Each is met by the code as it stands; their remaining
clauses ("re-judged against `HttpClient.Send`'s synchronous path", "the real synchronous path keeping the same guard",
"`TRANSPORT-27`'s `Content-Length` half pinned by a test") are exactly what the kit's blocking face and its `-1` assertion check.
They move to 8a, and the move is recorded in 8a's close-out status note (P8a-1).

### The split

| Sub-phase | IDs | Count |
|---|---|---|
| **8a** | `TRANSPORT-2`, `-3`, `-7`, `-11`, `-15`, `-16`, `-19`, `-20`, `-21`, `-22`, `-23`, `-24`, `-25`, `-26`, `-27`, `-29`; `ASYNC-1`..`ASYNC-22` | 16 + 22 = **38** |
| **8b** | `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28`, `-30` | **14** |
| | | **52** |

Why each 8b row is 8b's:

| ID | Reason it needs SystemNet code |
|---|---|
| `TRANSPORT-1` | Card: "a handler-taking constructor that rejects a redirect-enabled handler" — a new SDK-managed construction path the row must cover. The owned path is already met (`RedirectWireTests.The_sdk_managed_transport_returns_the_3xx_and_never_contacts_the_target`). |
| `TRANSPORT-4` | Card: "timeout classified separately from cancellation"; F3's internal-cancel misclassification lives in the same `catch`. |
| `TRANSPORT-5`, `TRANSPORT-6` | Card: "per-call timeout through a linked CTS"; `RequestOptions.Timeout` is ignored today (`SystemNetHttpClientTests.Options_are_accepted_and_ignored_until_8b`). |
| `TRANSPORT-8` | F3: an internal cancel of a borrowed client is reported as a retryable timeout. |
| `TRANSPORT-9` | Phase 5a hand-off: "8b: … `LateResult` for `TRANSPORT-9`". 8b decides whether its timeout plumbing introduces a settle race; the kit's race assertion proves the outcome. |
| `TRANSPORT-10` | F7, and the phase 1 hand-off table ("8b: header mapping (`TRANSPORT-10`, …)"). |
| `TRANSPORT-12` | F7, and card: "per-header drops". |
| `TRANSPORT-13` | Phase 1 and 5b hand-offs ("8b: `OBS-19` and `TRANSPORT-13`; move `SystemNetHttpClient`'s event ids 1-3 into 110-119"). |
| `TRANSPORT-14` | Phase 1 hand-off; F1 makes the name half a native-client refusal that 8b must disposition (§10 entry and `Unsatisfied MUSTs`, or a handler-level workaround if one exists). |
| `TRANSPORT-17` | Card: "a `SerializeToStream` override" — the single-write guarantee must hold on the real sync path `HttpClient.Send` drives. |
| `TRANSPORT-18` | Design §11 item 18 binds it to SystemNet; today a native re-send of a single-use body fails with `StreamConsumedException` instead of replaying identical bytes; 8b's proxy work makes the `407` re-send live. |
| `TRANSPORT-28` | Phase 3b hand-off: "8b … may recognise `FileRequestBody`" (the zero-copy SHOULD). |
| `TRANSPORT-30` | Card: "installing `ProxyOptions`"; phase 5a and 6c hand-offs (the `IWebProxy` adapter, the transport answering a proxy's `407`). |

### D3: the second transport — confirmed

D3 proposes that phase 8's segmentation design "confirms that argument against the kit's results rather than reopening it", and
that "the kit's transport-agnosticism is proven by running it against `DelegateHttpClient` … as well as against
`SystemNetHttpClient`, not by building a second product". **Confirmed, on three findings** (P8a-2, open for the lead because D3
is still unruled):

1. **No `TRANSPORT` or `ASYNC` property is unexercisable through `SystemNetHttpClient`.** Walking the 30 `TRANSPORT` IDs: every
   one is either exercised by the reference transport (with `SocketsHttpHandler`, any `HttpMessageHandler` a caller borrows,
   or a handler-taking constructor after 8b), or is a native-client refusal that a second transport over the *same* runtime would
   not lift. The one clause the reference transport cannot reach, `TRANSPORT-14`'s malformed-name half (F1), is a property of
   `SocketsHttpHandler`'s parser; a second product over raw sockets would reach it, but shipping and supporting an HTTP client to
   reach a SHOULD-adjacent leniency clause inverts design §2.1's discriminator ("a second adapter over the same property is a later
   package"). `TRANSPORT-8`, which Node and Ruby scope to "the OkHttp reference", **is** reachable here (F3, `CancelPendingRequests`),
   so the reference transport is the richer subject, not the poorer.
2. **The kit's agnosticism needs a second *wire stack*, not a second *product*.** A kit validated against one transport can encode
   that transport's quirks (Ruby's clause 6: one adapter normalises header case, the other preserves it). 8a therefore drives the
   kit against `DelegateHttpClient.Create`/`CreateBlocking` over a deliberately small, test-only HTTP/1.1 client on raw sockets
   (`tests/Dexpace.Sdk.Conformance.Tests/RawSocket/`, never packed). It shares no code with `HttpClient`, so an assertion that
   passes on both is about the contract, not about `SocketsHttpHandler`. This is the D3 sentence given teeth (P8a-2).
3. **`HttpMessageHandler` remains the ecosystem's connection seam.** `WinHttpHandler`, the browser handler, the mobile native
   handlers and `IHttpClientFactory` pipelines all plug in under `SystemNetHttpClient`; a borrowed client is exactly that, and the
   kit's `CreateBorrowed` hook (below) runs the suite over a caller-built handler chain.

The post-release trigger in `docs/first-release.md` ("A second first-party transport … Trigger: a transport property the reference
transport cannot exercise") stands unchanged, and the kit is now the instrument that would detect the trigger: a kit assertion that
is `NotExercised` or permanently waived on SystemNet for a reason a different native client would remove.

### `Dexpace.Sdk.Reactive` — not built

The card: the `IObservable<T>` bridge "is **not** built unless 8's segmentation design finds a consumer". **No consumer found**
(P8a-3): nothing in `src/` or `tests/` references `IObservable<T>` or `System.Reactive`; no phase card or status note names an
Rx-composing caller; `System.Linq.AsyncEnumerable` (in-box on `net10.0`) already gives `IAsyncEnumerable<T>` the LINQ operators a
consumer would otherwise reach for Rx to get; and an Rx user's bridge is one `ToObservable()` call over the pageable or the SSE
stream. Node ships `@dexpace/rx` because RxJS is a distinct ecosystem beside `Promise` and `AsyncIterable`; .NET has no such
split (design §3.3). Consequences:

- `ASYNC-21` stays **N/A, adapter-scoped vacuous** (§11 item 21; its property is implemented on the pull-based reader, `SSE-39`).
- `ASYNC-6` is **met on the two bridges and vacuous for runtime facades** — refining the card's "vacuous by antecedent": `AsAsync`
  and `AsBlocking` are adapters in chapter 18's sense (they bridge the canonical future to another execution shape, `SEAM-18`), and
  cancellation crosses both of them in both directions, which existing tests already pin (see the `ASYNC` table).
- The `docs/first-release.md` *Post-v1 packages* entry and its trigger ("a consumer that composes with Rx operators") stand.

---

## Scope and the 38-row census

Legend (roadmap constraint 3): **build** = 8a adds a kit assertion (and its negative control) that pins the row against both
drivers; **met** = already true with named evidence, and 8a adds whatever pinning test the checklist row still needs; **N/A** /
**vacuous** = not applicable, with the section that retires it. Every **build** row whose behaviour already holds is marked
"met + kit". Assertion names are in the [assertion catalogue](#the-assertion-catalogue).

### `TRANSPORT` rows owned by 8a (16)

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| TRANSPORT-2 | MUST | met + kit | F6: no native re-send after the body was written. Design §3.2's body-side guard: a second `SerializeToStream` of a single-use body throws `StreamConsumedException`. Kit: `transport-2.no-silent-resend` (single-use body, pre-response peer abort on a reused connection; the server counts the body's bytes across every connection; the failure is a retryable `SdkException` or carries `StreamConsumedException`, never a 2xx). |
| TRANSPORT-3 | MUST | met + kit | `SystemNetHttpClient` rethrows `OperationCanceledException` when the caller's token is signalled (out-of-band discriminator, `catch … when (cancellationToken.IsCancellationRequested)`); the interrupt shape is §10 entry 8. Kit: `transport-3.cancel-is-terminal` on the **blocking** face (the row's own face) and the async face (`XCUT-1`): `OperationCanceledException` carrying the caller's token, the token still signalled, never `SdkException.IsRetryable`. Also pins `SEAM-13`. |
| TRANSPORT-7 | MUST | met + kit | The token reaches `HttpClient.SendAsync`. Kit: `transport-7.cancel-releases-exchange` — cancel while the server hangs before headers; the task ends cancelled and the server observes the connection released within the bound. |
| TRANSPORT-11 | MUST (+SHOULD log) | met + kit | `FramingHeaderDropWireTests` (phase 1, `Security`). Kit: `transport-11.framing-recomputed` (MUST: bogus `Host`, `Content-Length`, `Transfer-Encoding` plus a pass-through header; the wire carries the real host and correct framing, the pass-through survives) and `transport-11.drop-logged` (SHOULD: each dropped name appears in the kit logger, no value ever does). Header names compared case-folded (P8a-9). |
| TRANSPORT-15 | MUST | met + kit | `SystemNetHttpClientOwnershipTests`, `SystemNetHttpClientDisposeTests`. Kit: `transport-15.borrowed-survives` (through the `CreateBorrowed` hook: dispose the transport, the native client still completes a request to the fixture) and `transport-15.owned-released` (an idle keep-alive connection of an owned transport is closed by the client when the transport is disposed; a connection-per-request transport passes trivially). Also pins `XCUT-22`. |
| TRANSPORT-16 | MUST | met + kit | The 3b latch (`Interlocked.Exchange`), no wait in `Dispose`. Kit: `transport-16.close-idempotent-nonblocking` — three disposes in each available form throw nothing; a dispose issued while a call hangs returns within the bound; the "preserve the cancellation flag" clause is free (§10 entry 8: a signalled token cannot be un-signalled) and asserted anyway. |
| TRANSPORT-19 | SHOULD | met + kit | No producer thread exists on .NET: `HttpContent` serialisation runs on the sending task. Kit: `transport-19.abandoned-body-unblocks` — a stream body whose source parks a read; cancel the call; the parked read observes cancellation (or its stream is disposed) within the bound, and a `FileRequestBody`'s file can be opened `FileShare.None` afterwards (no stranded handle; meaningful on Windows). |
| TRANSPORT-20 | MUST | met + kit | `SystemNetHttpClientTests.ExecuteAsync_WrapsTransportFailure`. Kit: `transport-20.no-response-is-retryable` on both faces: connection refused (a port bound then released) and peer reset before any response byte; the failure is an `SdkException` with `IsRetryable == true` whose cause chain holds an I/O-family exception (§10 entry 7's reading: `IOException`, `HttpRequestException` without a status, `SocketException`, `TimeoutException`), never an `OperationCanceledException`. The "subtype of the platform I/O error" letter is §10 entry 7. |
| TRANSPORT-21 | MUST | met + kit | `SystemNetHttpClient.ExecuteAsync` is `async`, so `ThrowIfNull` faults the task; `A_null_options_argument_faults_the_task_with_ArgumentNullException`. Kit: `transport-21.pre-dispatch-failure-via-task` — a `null` request and `null` options on the async face return a faulted task, never a synchronous throw (also `ASYNC-2`). |
| TRANSPORT-22 | MUST | met + kit | `MalformedContentTypeWireTests.The_native_response_is_disposed_when_adaptation_throws`; the guard around `ThrowIfRedirected`/`ToResponse`. Kit: `transport-22.adaptation-failure-releases` through the `CreateWithFaultingAdaptation` hook (the SystemNet driver supplies it from the test project with a `DelegatingHandler`, P8a-22): the send fails and the server observes the connection released. Whether an adaptation failure is re-typed as an SDK exception is not a `TRANSPORT-22` clause; it is offered to 8b as a non-row decision. |
| TRANSPORT-23 | MUST | met + kit | Non-nullable `Task<Response>` plus `PipelineRunner`'s null check (design §3.3). Kit: `transport-23.never-null` across a success, a 4xx, a 5xx and a failure script. |
| TRANSPORT-24 | MUST | met + kit | F5; `(int)StatusCode`. Kit: `transport-24.vendor-status-readable` over 499, 520–526 and 530, each with a body that reads; disposing releases the connection. |
| TRANSPORT-25 | MUST | met + kit | `HttpCompletionOption.ResponseHeadersRead`; `HttpResponseMessageBody` disposes the message. Kit: `transport-25.lazy-body` (a gated `Streamed` reply: the call returns and the first chunk is readable before the server writes the second — the 7b property lifted, P8a-17; also `SEAM-11`'s no-pre-buffering), `transport-25.large-round-trip` (8 MiB, SHA-256 equal), `transport-25.dispose-releases` (partial read then dispose; connection released). |
| TRANSPORT-26 | MUST | met + kit | F4. Kit: `transport-26.bodyless-methods` — body-less `POST`/`PUT`/`PATCH` carry `Content-Length: 0` (or an empty chunked body) and no body bytes; a body-less `GET`/`HEAD` carries neither a body nor `Transfer-Encoding`. |
| TRANSPORT-27 | SHOULD | met + kit | `MalformedContentTypeWireTests` (media-type half); F2 (length half). Kit: `transport-27.inbound-downgrade` — a malformed `Content-Type` gives `ContentType == null`; a non-numeric or absent `Content-Length` gives `ContentLength == -1`; the body reads in every case. |
| TRANSPORT-29 | MUST | met + kit | Design §3.2; `SystemNetHttpClient` holds only readonly fields. Kit: `transport-29.concurrent-no-crosstalk` — 64 concurrent calls on one transport (tasks for the async face, dedicated threads for the blocking face), each carrying a unique id the server echoes; each response matches its request. Also pins `SEAM-12`. |

### `ASYNC` rows (22, all 8a's)

The card: "**ASYNC** is answered by the SPI and the bridges. No adapter package ships (design §3.3)." Most rows are already pinned by
`tests/Dexpace.Sdk.Core.Tests/Client/`; the kit re-asserts the per-transport ones (`ASYNC-1`, `-2`, `-20`, `-22`) against every
subject, and 8a adds the missing pinning tests to `Dexpace.Sdk.Core.Tests` (category `Unit`).

| ID | Level | Disposition | How / evidence |
|---|---|---|---|
| ASYNC-1 | MUST | met + kit | Non-nullable `Task<Response>`; `DelegateHttpClientTests.A_null_result_faults_the_task_with_InvalidOperationException`; `PipelineRunner`. Kit: `async-1.single-non-null-response` (the response delivered is the one for the request sent) with `transport-23.never-null`. |
| ASYNC-2 | MUST | met + kit | `DelegateHttpClientTests.Argument_errors_are_delivered_through_the_task`, `SyncToAsyncBridgeTests.Argument_errors_are_delivered_through_the_returned_task_not_thrown_synchronously`. Kit: `transport-21.pre-dispatch-failure-via-task`. The "worker-pool rejection" clause maps to `AsAsync` over a scheduler that throws from `QueueTask`: new pinning test `AsAsync_over_a_rejecting_scheduler_faults_the_task`. |
| ASYNC-3 | MUST | met (cooperative, §10 entry 8) | "Cancel with interruption" = the token passed into the blocking `Execute`; "without" = the caller's own `WaitAsync`; "queued never runs" = `SyncToAsyncBridgeTests.A_token_cancelled_before_the_send_never_runs_it`. §12 lists the cooperative reading as "met in structured form only". |
| ASYNC-4 | MUST | vacuous | No interrupt is ever delivered: `Thread.Interrupt` is banned in `src/` (`BannedSymbols.txt`); §3.3, §10 entry 8, §12 "Vacuous". |
| ASYNC-5 | MUST | met | `SyncToAsyncBridgeTests.A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces`; `TaskCompletionSource<T>.TrySetResult` is banned without a `SEAM-30` pragma. |
| ASYNC-6 | MUST | met on the bridges; vacuous for runtime facades (P8a-3) | `AsyncToSyncBridgeTests.Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException`, `SyncToAsyncBridgeTests.The_exact_options_and_token_instances_reach_Execute` + `A_token_cancelled_before_the_send_never_runs_it`. No reactive or coroutine facade ships (§11 item 21). |
| ASYNC-7 | SHOULD | met (docs) + pin | `AsAsync`'s remarks document that cancellation is cooperative and reaches only as far as the blocking transport honours its token. New pinning test `AsAsync_aborts_a_token_honouring_client_and_lets_a_token_ignoring_one_run_to_completion` (the documented outcome, both ways). |
| ASYNC-8 | SHOULD | met + pin | `ExecutionContext` flows into `AsAsync`'s task (`SyncToAsyncBridgeTests.The_callers_AsyncLocal_and_Activity_reach_the_worker_thread`). New pin: a `LoggerExternalScopeProvider` scope opened on the caller is visible to a log written inside the blocking `Execute` on the worker. |
| ASYNC-9 | MUST | met + pin | `ExecutionContext.Run` restores the thread's prior context after the task, including on throw. New pin over a one-thread scheduler (`RecordingTaskScheduler`-style): the worker's own scope is intact after a call returns and after one throws. |
| ASYNC-10 | MUST | met + pin | Capture happens per `ExecuteAsync` call, not at `AsAsync` construction. New pin: one bridge built under scope A, called under B then C, logs B then C. |
| ASYNC-11 | MUST | met + pin | With no scope provider (or `NullLogger`), a full bridge round trip raises nothing and the worker sees no scope. New pin. |
| ASYNC-12 | MUST | met + pin | `LongRunning` creates a dedicated thread per call and the `Task` carries the captured `ExecutionContext` onto it. Covered by the `ASYNC-8` pin run with `TaskScheduler.Default` (the thread-creating case). |
| ASYNC-13 | MUST | met + pin | `AsBlocking` waits with `GetAwaiter().GetResult()`, which throws the original exception, not `AggregateException` (`AsyncToSyncBridgeTests.A_faulted_InvalidOperationException_surfaces_as_itself_not_as_an_AggregateException`). New pin with `TestSupport/Recovery/CyclicExceptions`: a self-referential cause terminates. |
| ASYNC-14 | MUST | met (cooperative, §10 entry 8) | Cancellation instead of interruption: `AsyncToSyncBridgeTests.Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException`; unwrapping as `ASYNC-13`; "a future cancelled independently surfaces its cancellation as-is" — new pin (a task cancelled by its own source surfaces `OperationCanceledException`, not an I/O exception). |
| ASYNC-15 | MUST | met + kit | `SystemNetHttpClient`'s latch and ownership (`SystemNetHttpClientDisposeTests`); the bridges own nothing (`ClientDisposeIdempotenceTests.Bridge_and_delegate_disposes_are_idempotent`, `SyncToAsyncBridgeTests.Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client`). The interrupt-safe clause is §10 entry 8. Kit: `transport-15.*`, `transport-16.*`. |
| ASYNC-16 | SHOULD | N/A | No SDK adapter owns an executor or worker threads (§3.7: "none of the MVP packages owns any"); `AsAsync` runs on the caller's scheduler and owns it not. |
| ASYNC-17 | SHOULD | met | `DelegateHttpClient`'s adapters have no-op disposal (`DelegateHttpClientTests.It_keeps_working_after_dispose`); design §3.7. |
| ASYNC-18 | MUST | met | `TimeProviderWaitsTests`: `A_negative_delay_is_rejected`, `DelayAsync_of_zero_completes_synchronously_without_a_timer`, `DelayAsync_completes_when_the_fake_clock_advances`, `DelayAsync_cancelled_mid_wait_disposes_the_timer`. |
| ASYNC-19 | MUST | met | `SyncToAsyncBridgeTests.The_exact_options_and_token_instances_reach_Execute`, `AsyncToSyncBridgeTests.Options_and_token_reach_ExecuteAsync_by_reference`; the pageables (`PAGE-36`, phase 7c). |
| ASYNC-20 | MUST | met + kit | `DelegateHttpClientTests.Cancelling_after_delivery_leaves_the_response_readable`, `SyncToAsyncBridgeTests.Cancelling_after_delivery_leaves_the_response_readable`. Kit: `async-20.late-cancel-leaves-response-open` against each real transport (cancel the call's token after delivery, then read the whole body). |
| ASYNC-21 | MUST | N/A, adapter-scoped vacuous | No reactive adapter ships (P8a-3); §11 item 21; the property lives on the pull-based reader (`SSE-39`). Reported as `Vacuous`, never as passing. |
| ASYNC-22 | MUST | met + kit | `ConcurrencyTests.No_cross_talk_through_the_bridges`, `No_cross_talk_through_both_DelegateHttpClient_forms`. Kit: `transport-29.concurrent-no-crosstalk` (async face). |

### Rows 8a also proves, owned elsewhere

Status-note hand-offs ask 8a for per-transport proofs of IDs whose rows live in earlier checklists. 8a adds the assertion and the
close-out status note cites it; the owning rows are not re-opened.

| ID | Owner | Kit assertion |
|---|---|---|
| `SEAM-11` (no pre-buffering), `SEAM-12`, `SEAM-13` | 2b (2b hand-off: "8a … owns the per-transport proofs") | `transport-25.lazy-body`, `transport-29.concurrent-no-crosstalk`, `transport-3.cancel-is-terminal` (blocking face) |
| `SEAM-15` | 2b row, 8b work | `seam-15.after-dispose` — asserts the subject's declared `AfterDisposeBehavior`; SystemNet declares `ThrowsObjectDisposedException` and is waived (owner `8b`) until 8b's latch lands for borrowed clients. |
| `SEAM-16`, `SEAM-30` | 2b | `async-1.*`, `transport-23.never-null`, `transport-9.*` |
| `HTTP-39` | 3a (3a hand-off: "8a: the per-transport `HTTP-39` proof") | `http-39.short-source-fails` (lifted from `ExactLengthBodyWireTests`) |
| `XCUT-22` | 10 | `transport-15.*` |
| Retry linkage (6a hand-off) | 6a | `transport-20.retried-by-the-pipeline` (a GET through `DexpacePipeline.CreateDefault` over a script whose first connection aborts pre-response and second succeeds: two connections, a 200) and `transport-7.attempt-timeout-aborts` (an `AttemptTimeout` over a hang-before-headers script ends the attempt within the bound and the server sees the connection released). |

---

## Argued positions

### A. One package, framework-free, under every library gate (P8a-4, P8a-6)

**Options.** (1) `Dexpace.Sdk.Conformance` as a `src/` library with assertions as plain methods throwing `ConformanceException`
(design §2.1, §9.3); (2) an xUnit-specific package (`[Theory]` data and `Assert` calls); (3) a source-only package (`.cs` files
dropped into the consumer's project). **Chosen: (1).** (2) imposes this repository's test framework on every adapter author, which
§9.3 rejects in terms; (3) escapes the analyzers, `PublicAPI` lock and package validation, so the kit's own contract would be
unversioned. Consequences: the kit references `Dexpace.Sdk.Core` and nothing else (its `TcpListener`, `Socket` and
`System.Net.Http` types are in the shared framework); it is `IsTrimmable` and `IsAotCompatible`; `CA2007` binds every `await` in
it, including the promoted fixture's; `Task.Delay`, `Thread.Sleep`, `DateTime.UtcNow`, `Console`, `AsyncLocal<T>` and the
sync-over-async members are banned in it as in every `src/` project. The kit carries its own internal recording logger and counting
streams; it does **not** reference `tests/Dexpace.Sdk.TestSupport`, which is unpacked and cannot be a package dependency (P8a-23).

`ConformanceException` derives from `Exception`, **not** `SdkException`: a conformance failure is a test result, and a caller's
`catch (SdkException)` around a send must never swallow one (Ruby's `P8-8`, same reasoning).

### B. The subject is a factory descriptor with capability hooks (P8a-9, P8a-10)

**Options.** (1) A base class the adapter author subclasses; (2) a descriptor of factories passed to a static runner (Ruby `R7`:
"a factory, not a mixin"); (3) a pre-built transport instance. **Chosen: (2).** (3) cannot give each assertion a fresh transport
(order independence, Ruby `testing/4ef070df`) or express the borrowed half of `TRANSPORT-15`; (1) ties the assertion set to one test
class per adapter and makes the framework the unit of composition. A descriptor is a value: the first-party build runs it twice in
one process (two drivers), phase 9 adds a third over the DI-built transport, and a third-party author writes one.

The descriptor carries the two **faces** (`CreateAsync`, `CreateBlocking`; either may be absent) and a small set of optional
**capability hooks**, each for a clause only a transport-specific construction can reach. A missing hook makes the assertion that
needs it report `NotExercised` with the hook's name — never `Passed`, never `Vacuous` (P8a-7). The hooks:

| Hook | For | SystemNet driver supplies it as |
|---|---|---|
| `CreateBorrowed` | `TRANSPORT-15`, `XCUT-22` | `new SystemNetHttpClient(httpClient)` over a test-built `HttpClient`, with a send-through-the-native-client probe |
| `CreateWithFaultingAdaptation` | `TRANSPORT-22` | a borrowed client whose `DelegatingHandler` makes every live response fail adaptation (P8a-22) |
| `CreateWithInternalCancel` | `TRANSPORT-8` | a borrowed client plus `HttpClient.CancelPendingRequests` as the trigger (F3) |
| `CreateWithNativeResend` | `TRANSPORT-18` | a borrowed client whose `DelegatingHandler` calls `base.SendAsync` twice (the re-send design §11 item 18 verified) |
| `CreateWithProxy` | `TRANSPORT-30` | absent until 8b adds a `ProxyOptions`-taking construction; then 8b supplies it |

`AfterDispose` (`Unspecified` or `ThrowsObjectDisposedException`) is a declaration, not a hook: `SEAM-15` is a MAY, so a subject
declaring `Unspecified` gets `Vacuous` for `seam-15.after-dispose`.

**What the suite never assumes (the suite contract, ported from Ruby's twelve clauses and re-cut for .NET).**

1. A subject is built by a factory per assertion, never shared across assertions; the kit disposes every transport it built, on
   every path.
2. The kit asks the SDK model, never the native object: `ResponseBody.ContentLength` is `-1` when unknown, `ContentType` is `null`
   when absent or unparseable.
3. **No assertion compares a header name against the wire's exact spelling**; names are compared ASCII-case-folded. (Casing
   fidelity, `HTTP-21`, is SystemNet's own `HeaderCasingWireTests`, not a kit clause.)
4. **No assertion expects `Content-Length` among a response's headers**; the length is asked of the body.
5. **No assertion branches on the transport's identity** (no `is SystemNetHttpClient`, no type-name checks). A clause that differs
   per transport is a hook or a waiver.
6. Every assertion is bounded by `TransportSuiteOptions.AssertionTimeout` (default 30 s); a hang is a `Failed` result naming the
   bound, never a hung run.
7. Every "has the server seen it" check is a condition wait on the fixture (`WaitForRequestAsync`, `WaitForConnectionReleasedAsync`),
   never a sleep-and-poll; the one timed negative ("no second request arrives") waits `ReleaseTimeout`.
8. Each assertion starts a **fresh** `LoopbackServer`; pathological scripts (abort, hang) never share a server or an origin with
   another assertion, so a poisoned pooled connection cannot fail a later row (Node's `isolatedUrl` lesson).
9. Cancellation in assertions is a `CancellationToken`, never a thread interrupt (banned in `src/`).
10. Plaintext HTTP/1.1 only: no TLS fixture, no HTTP/2, no connect-timeout scripting (a local listener cannot portably accept slowly).
    These are stated in the report preamble as what a green run does **not** prove (P8a-12).

**Faces.** Each assertion declares the faces it runs on; the runner runs it once per declared face the subject supplies, through
one internal send primitive (`SendAsync(face, request, options, token)`), so no assertion is written twice (Ruby clause 8). A face
the subject does not supply yields `NotExercised` for that (assertion, face) pair.

### C. Results, waivers and the report (P8a-7, P8a-8)

**Statuses.** `Passed`; `Failed` (a `ConformanceException`, carrying expected and actual); `Errored` (anything else thrown — a kit
bug or a transport crash, never silently a failure); `Vacuous` (the assertion established, from inside, that the clause's
antecedent is absent — it throws `ConformanceVacuousException` with the reason, so vacuity is measured, not claimed, Ruby `R7`);
`Waived` (the assertion failed or errored and a waiver names one of its IDs); `NotExercised` (the subject lacks the face or hook).
Six statuses, one more than Ruby's five: Ruby folded "no hook" into its driver's choice of factory; here hooks are optional
members, and conflating "this transport has no such construction" with "the antecedent is absent" would let a lazy subject report
vacuity it never measured.

**Waivers are by requirement ID, carry a reason and an optional owner, and must stay needed.** `ConformanceWaiver(RequirementId,
Reason) { Owner }` — `Owner` is a phase (`"8b"`) for a pending fix, or `null` for a permanent decision. A waiver whose ID is not in
the requirement catalogue throws at construction. A waived assertion that **passes** is reported `Failed` with "waiver for
`TRANSPORT-n` is no longer needed" — so 8b cannot fix a row and forget its waiver, and a third-party author learns when their
transport improved. The report lists every waiver on every run, failing or not (§9.3: "so the gap stays visible").

**The report.** `ConformanceReport` holds the results; `IsGreen` is "no `Failed`, no `Errored`"; `ByRequirement` is per ID, the
**worst** status of the assertions citing it (order: `Errored` > `Failed` > `Waived` > `NotExercised` > `Vacuous` > `Passed`);
`ByAppendixBItem` is the many-to-one view over B.6 (5 items) and B.7 (5 items), worst-of again (§9.3). `ToString()` renders the
preamble (what a green run does not prove), the per-ID table, the waivers and the `NotExercised` list.

**The ID→level table is generated from appendix C** (card). `scripts/ci/requirement-catalog.cs` (a file-based app, like the
coverage gate) reads `docs/product-spec/appendix-c-consolidated-normative-requirement-index.md` and writes
`src/Dexpace.Sdk.Conformance/Requirements/RequirementCatalog.g.cs`: all 645 rows (ID, level), internal. A `Unit` drift test in
`Dexpace.Sdk.Conformance.Tests` regenerates it in memory and compares, so an edit to appendix C without a regeneration fails the
build's test run (P8a-13). The appendix-B map for B.6/B.7 is a hand-written internal table with a drift test that parses the
item's ID list out of appendix B (P8a-14). Phase 10 widens both views; the mechanism is 8a's.

### D. The wire fixture, promoted (P8a-11, P8a-21)

**Options.** (1) Move `Loopback/` from `tests/Dexpace.Sdk.Http.SystemNet.Tests` into the kit as public types and delete the test
copy; (2) copy it and keep both; (3) keep it test-only and give the kit a second, private server. **Chosen: (1)**, as roadmap
constraint 4 says ("Phase 8a promotes that fixture into the conformance kit"). (2) forks the one fixture §9.3 wants every adapter
proved against; (3) means SystemNet's own wire tests and its conformance run would observe the wire through different code.

The promoted types live in `Dexpace.Sdk.Conformance.Wire`: `LoopbackServer`, `LoopbackResponse`, `RecordedRequest`,
`MalformedRequestException` (public, as today), with `RawRequestReader` internal. Library-grade changes: `ConfigureAwait(false)` on
every await; `MA0051` splits where a method passes 70 lines; the `CA1031` pragmas keep their why-comments; full XML docs. Behaviour
is unchanged for every existing caller. Additions, each driven by an assertion:

- **Release observation.** `WaitForConnectionReleasedAsync(int connection, CancellationToken)` completes when the client closes that
  connection **or** sends a further request on it. "Released" is either, because a pooling client returns a connection to its pool
  rather than closing it (P8a-21); a leaked response is a connection that does neither within `ReleaseTimeout`.
- **Request arrival.** `WaitForRequestAsync(int index, CancellationToken)`.
- **Scripts** (new `LoopbackResponse` factories, the checklist being Ruby's named scripts plus the .NET additions): fixed-length
  (`Status`, `Ok` — exist), chunked/streamed (`Streamed` — exists), close-delimited (`Raw` with `closeConnection` — exists),
  redirect (`Redirect` — exists), vendor status with a body (`Status(520, …)` — exists), malformed inbound headers and malformed
  `Content-Length` (`Raw` — exists), **`Gated(Task gate, LoopbackResponse then)`** (write nothing until the gate opens: hang-before-
  headers), **`HeadersThenGate(…)`** (hang-after-headers / dribbled body), **`Abort()`** (read the request, then reset the connection
  with no response byte: first-connection-failure and peer-reset), **`Hang()`** (never answer; the server's disposal ends it),
  **`Large(int bytes, int seed)`** (a deterministic multi-megabyte body with its SHA-256), and **`EchoId()`** (answers with the
  request's `X-Conformance-Id` value as the body, for the concurrency row). A script is a `LoopbackResponse`; there is no separate
  scripts class.
- **Body accounting.** `RecordedRequest` already holds the body; `LoopbackServer.Requests` across connections is what
  `transport-2.no-silent-resend` counts.

Breaking for the test project only: `tests/Dexpace.Sdk.Http.SystemNet.Tests` swaps `using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;`
for `using Dexpace.Sdk.Conformance.Wire;` in its wire test classes, and `LoopbackServerTests` moves to
`tests/Dexpace.Sdk.Conformance.Tests/Wire/`.

### E. The two drivers (P8a-2, P8a-15)

**Options for the `DelegateHttpClient` driver** (D3's "run it against `DelegateHttpClient`"). (1) `DelegateHttpClient` decorating
`SystemNetHttpClient` — cheap, but proves only that the kit does not type-check its subject; (2) `DelegateHttpClient` over a
test-only HTTP/1.1 client on raw sockets — proves the assertions are about the contract, because nothing is shared with `HttpClient`;
(3) `DelegateHttpClient` over in-memory fakes with no socket — every wire assertion `NotExercised`, SPI rows only. **Chosen: (2)**,
plus (3)'s fakes as the kit's negative controls (below). **Open for the lead** on cost: the raw client is roughly 400 lines of test
code (request writer over `RequestBody.WriteToAsync`/`WriteTo`, status-line and header parser, `Content-Length`/chunked/close-
delimited body as a lazy `Stream` that closes the socket on dispose, token-driven abort, the `ServiceRequestException` mapping,
connection-per-request, no pooling, no proxy). If the lead prefers (1), the driver shrinks to a decorator and D3's agnosticism claim
is weakened to "type-agnostic", recorded as such.

The raw client is honest about what it does not do, which exercises the waiver machinery for real: it declares no
`CreateBorrowed` (no native client to borrow → `TRANSPORT-15`'s borrowed half `NotExercised`), no proxy, no internal cancel, no
native re-send, and waives the SHOULDs it does not implement (`TRANSPORT-28` zero-copy, `TRANSPORT-13` policy) with
`Owner = null` and the reason "test-only transport". It must pass every MUST assertion it can reach.

**The xUnit driver lives in each consuming test project, not in a shared library** (P8a-15). It is about 40 lines: a
`TheoryData<string, TransportFace>` over `TransportSuite.Assertions`, one `[Theory]` that calls `TransportSuite.RunAsync` and maps
`Failed`/`Errored` to `Assert.Fail`, `Waived`/`Vacuous`/`NotExercised` to `Assert.Skip` with the reason (xUnit v3's dynamic skip,
visible in every run's report), plus one `[Fact]` that runs `RunAllAsync` and writes `report.ToString()` to the test output. It is
exactly the code a third-party author writes, so keeping two copies (SystemNet's, the raw client's) costs nothing and shows that no
shared driver library is needed. `tests/Dexpace.Sdk.TestSupport` is framework-free by charter and cannot host it.

**Negative controls: every assertion can fail.** For each assertion, `Dexpace.Sdk.Conformance.Tests` builds at least one
deliberately non-conforming subject (a `DelegateHttpClient` that buffers the body, returns `null`, throws synchronously, maps a
timeout to cancellation, leaks the native response, follows a 302, re-writes a single-use body, …) and asserts the result is
`Failed` (or `Errored` where that is the contract). An assertion with no failing control is not merged. These run with a short
`ReleaseTimeout` (500 ms) so the leak controls stay fast.

### F. Version and packaging (P8a-5, P8a-16)

`src/Dexpace.Sdk.Conformance/Dexpace.Sdk.Conformance.csproj`: `PackageId`, `AssemblyName` and `RootNamespace`
`Dexpace.Sdk.Conformance`; `IsPackable` true; `PackageReadmeFile` `README.md` (packed by `Directory.Build.targets`); `PackageTags`
`http;sdk;conformance;testing;dexpace`; one `ProjectReference` to core; `InternalsVisibleTo` `Dexpace.Sdk.Conformance.Tests` only;
empty `PublicAPI.Shipped.txt`; `packages.lock.json` committed. No `Version`/`VersionPrefix` override: it inherits `0.0.1-alpha.1`
from `Directory.Build.props` like every package (design §2.3, lockstep).

**The kit's first published version is the first release's lockstep version — proposed `0.1.0`** — and it is never published
ahead of, or apart from, the core and transport it certifies. **Open for the lead**: `docs/first-release.md` makes "the next version
after `0.0.1-alpha.1`" phase 12's decision; if phase 12 picks another number the kit follows with no csproj edit. One versioning
rule is the kit's own and is written into its README: **a release that adds an assertion is at least a minor version**, because a
new assertion can turn a previously green third-party run red; a consumer absorbs it with a waiver by ID.

The kit is measured by the coverage gate, as every packable `src/` library is (`scripts/ci/coverage-gate.cs` fails closed on a
packable library without coverage data), contrary to design §9.3's "with the conformance kit … excluded". A library third-party
authors depend on deserves the floor, and the negative controls exercise its failure paths. Correction owed (P8a-16).

`scripts/ci/dependency-audit.cs` gains `["Dexpace.Sdk.Conformance"] = null` (core plus no third-party library). `reproducible-pack.sh`
needs no edit (it packs the solution). The CI header's inert-gate line for the kit is replaced by a sentence naming the two drivers.

---

## The public surface (`PublicAPI.Unshipped.txt`)

New file `src/Dexpace.Sdk.Conformance/PublicAPI.Unshipped.txt`; no existing package's surface changes. Every member is documented
(`CS1591`). Signatures at design level; the plan writes the file and `RS0016`/`RS0017` checks it.

```csharp
namespace Dexpace.Sdk.Conformance;

public enum RequirementLevel { Must, Should, May }
public enum TransportFace { Async, Blocking }
public enum ConformanceStatus { Passed, Failed, Errored, Vacuous, Waived, NotExercised }
public enum AfterDisposeBehavior { Unspecified, ThrowsObjectDisposedException }

public sealed class ConformanceException : Exception            // not an SdkException (P8a-6)
{
    public ConformanceException();
    public ConformanceException(string message);
    public ConformanceException(string message, Exception? innerException);
    public ConformanceException(string message, string? expected, string? actual);
    public string? Expected { get; }
    public string? Actual { get; }
}

public sealed class ConformanceVacuousException : Exception      // thrown from inside an assertion; Message is the reason
{
    public ConformanceVacuousException();
    public ConformanceVacuousException(string message);
    public ConformanceVacuousException(string message, Exception? innerException);
}

public sealed record ConformanceWaiver(string RequirementId, string Reason)
{
    public string? Owner { get; init; }                           // e.g. "8b"; null = permanent
}

public sealed class ConformanceAssertion
{
    public string Name { get; }                                   // stable, e.g. "transport-24.vendor-status-readable"
    public IReadOnlyList<string> RequirementIds { get; }
    public RequirementLevel Level { get; }                        // the clause's level, from the generated catalogue
    public IReadOnlyList<TransportFace> Faces { get; }
    public override string ToString();
}

public sealed record ConformanceResult(ConformanceAssertion Assertion, TransportFace Face, ConformanceStatus Status, string Detail)
{
    public Exception? Exception { get; init; }
    public ConformanceWaiver? Waiver { get; init; }
}

public sealed class ConformanceReport
{
    public static ConformanceReport Create(string subjectName, IEnumerable<ConformanceResult> results);
    public static string Preamble { get; }                        // what a green run does not prove
    public string SubjectName { get; }
    public IReadOnlyList<ConformanceResult> Results { get; }
    public bool IsGreen { get; }
    public IReadOnlyDictionary<string, ConformanceStatus> ByRequirement { get; }
    public IReadOnlyDictionary<string, ConformanceStatus> ByAppendixBItem { get; }   // "B.6.1".."B.7.5" in 8a
    public override string ToString();
}

public sealed record TransportSettings
{
    public required ILogger Logger { get; init; }                 // the kit's recorder; a subject passes it to its transport
}

public sealed class BorrowedTransport : IAsyncDisposable
{
    public BorrowedTransport(IAsyncHttpClient transport, Func<Uri, CancellationToken, Task> sendThroughNativeClient,
        IAsyncDisposable nativeClientOwner);
    public IAsyncHttpClient Transport { get; }
    public Func<Uri, CancellationToken, Task> SendThroughNativeClient { get; }
    public ValueTask DisposeAsync();                              // disposes the native owner, never the transport twice
}

public sealed class InternalCancellation
{
    public InternalCancellation(IAsyncHttpClient transport, Action cancelInFlight);
    public IAsyncHttpClient Transport { get; }
    public Action CancelInFlight { get; }
}

public sealed class TransportSubject
{
    public required string Name { get; init; }
    public Func<TransportSettings, IAsyncHttpClient>? CreateAsync { get; init; }
    public Func<TransportSettings, IHttpClient>? CreateBlocking { get; init; }
    public Func<TransportSettings, BorrowedTransport>? CreateBorrowed { get; init; }
    public Func<TransportSettings, IAsyncHttpClient>? CreateWithFaultingAdaptation { get; init; }
    public Func<TransportSettings, InternalCancellation>? CreateWithInternalCancel { get; init; }
    public Func<TransportSettings, IAsyncHttpClient>? CreateWithNativeResend { get; init; }
    public Func<TransportSettings, ProxyOptions, IAsyncHttpClient>? CreateWithProxy { get; init; }
    public AfterDisposeBehavior AfterDispose { get; init; }
}

public sealed class TransportSuiteOptions
{
    public IReadOnlyList<ConformanceWaiver> Waivers { get; init; }          // default empty
    public TimeSpan AssertionTimeout { get; init; }                         // default 30 s
    public TimeSpan ReleaseTimeout { get; init; }                           // default 10 s
}

public static class TransportSuite
{
    public static IReadOnlyList<ConformanceAssertion> Assertions { get; }
    public static Task<ConformanceResult> RunAsync(TransportSubject subject, ConformanceAssertion assertion, TransportFace face,
        TransportSuiteOptions? options = null, CancellationToken cancellationToken = default);
    public static Task<ConformanceReport> RunAllAsync(TransportSubject subject, TransportSuiteOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

```csharp
namespace Dexpace.Sdk.Conformance.Wire;   // promoted from tests/Dexpace.Sdk.Http.SystemNet.Tests/Loopback, members as today plus:

public sealed class LoopbackServer : IAsyncDisposable
{
    // existing: Start(params IEnumerable<LoopbackResponse>), Start(Func<RecordedRequest, LoopbackResponse>), BaseUri, Url,
    //           Requests, Faults, ConnectionCount, DisposeAsync
    public Task<RecordedRequest> WaitForRequestAsync(int index, CancellationToken cancellationToken);
    public Task WaitForConnectionReleasedAsync(int connection, CancellationToken cancellationToken);
}

public sealed class LoopbackResponse
{
    // existing: Raw(ReadOnlySpan<byte>, bool), Raw(string, bool), Status, Streamed, Ok, Redirect, Bytes, Chunks, CloseConnection
    public static LoopbackResponse Gated(Task gate, LoopbackResponse then);
    public static LoopbackResponse HeadersThenGate(int statusCode, IEnumerable<KeyValuePair<string, string>> headers, Task gate, ReadOnlyMemory<byte> rest);
    public static LoopbackResponse Abort();
    public static LoopbackResponse Hang();
    public static LoopbackResponse Large(int bytes, int seed);
    public static LoopbackResponse EchoId(RecordedRequest request);
}
// RecordedRequest and MalformedRequestException: unchanged members.
```

**Kept internal.** `RawRequestReader`; `RequirementCatalog` (generated); the appendix-B map; the assertion bodies (one internal
static class per `TRANSPORT`/`ASYNC` group, each method ≤ 70 lines); the recording logger and counting streams; the per-face send
primitive. Exposing assertion bodies as public methods (one per ID, Ruby's "callable") was considered and rejected: the
`ConformanceAssertion` value plus `RunAsync` is the callable, and keeping the bodies internal lets 8b, 10 and later releases repair an
assertion without a public-surface change.

---

## The assertion catalogue

One assertion per row; names are stable public identifiers. "Owner" is the row's sub-phase; "SystemNet at `ce0f68c`" is the
expected first result on the SystemNet driver (the plan confirms each, and the waiver list is exactly the expected failures).

| Assertion | IDs | Level | Faces | Script / hook | Owner | SystemNet at `ce0f68c` |
|---|---|---|---|---|---|---|
| `transport-1.redirect-not-followed` | TRANSPORT-1 | MUST | both | `Redirect(302)` to a second server | 8b | pass |
| `transport-2.no-silent-resend` | TRANSPORT-2 | MUST | both | `Ok` then `Abort` on a reused connection | 8a | pass (F6) |
| `transport-3.cancel-is-terminal` | TRANSPORT-3, SEAM-13, XCUT-1 | MUST | both | `Hang` | 8a | pass |
| `transport-4.timeout-is-retryable` | TRANSPORT-4, XCUT-2 | MUST | both | `Hang`, `RequestOptions.Timeout` 200 ms | 8b | **waived 8b** (options ignored) |
| `transport-5.per-call-timeout` | TRANSPORT-5 | MUST | async | two servers `Gated` 1 s; timeouts 200 ms / 5 s; and `null` | 8b | **waived 8b** |
| `transport-6.sub-resolution-timeout` | TRANSPORT-6 | SHOULD | both | `Hang`, `Timeout` = 100 µs | 8b | **waived 8b** |
| `transport-7.cancel-releases-exchange` | TRANSPORT-7 | MUST | async | `Hang` | 8a | pass |
| `transport-7.attempt-timeout-aborts` | TRANSPORT-7 (6a hand-off) | MUST | async | `Hang` under `DexpacePipeline` with `AttemptTimeout` | 8a | pass |
| `transport-8.internal-cancel-is-terminal` | TRANSPORT-8 | MUST | async | `Hang` + `CreateWithInternalCancel`; timeout half via `Timeout` | 8b | **waived 8b** (F3) |
| `transport-9.settle-race-releases` | TRANSPORT-9, SEAM-30 | MUST | async | 50 iterations, cancel at staggered offsets around header arrival | 8b | pass expected |
| `transport-10.content-type-authoritative` | TRANSPORT-10 | MUST | both | echo request; body type X, header Y / none | 8b | **waived 8b** (F7) |
| `transport-11.framing-recomputed` | TRANSPORT-11 | MUST | both | echo request | 8a | pass |
| `transport-11.drop-logged` | TRANSPORT-11 | SHOULD | async | echo + kit logger | 8a | pass |
| `transport-12.native-rejected-header-dropped` | TRANSPORT-12 | MUST | both | body-less and bodied requests with content headers and the awkward-but-valid set; each is on the wire or logged as dropped | 8b | **waived 8b** (F7) |
| `transport-13.drop-log-once-per-name` | TRANSPORT-13, OBS-19 | SHOULD | async | three sends, two names, default policy | 8b | **waived 8b** |
| `transport-14.inbound-lenient` | TRANSPORT-14 | MUST (+SHOULD obs-text) | both | `Raw` with obs-text value, control-byte value, control/non-ASCII name | 8b | **waived 8b** (F1, name half) |
| `transport-15.borrowed-survives` | TRANSPORT-15, XCUT-22 | MUST | async | `CreateBorrowed` | 8a | pass |
| `transport-15.owned-released` | TRANSPORT-15 | MUST | async | keep-alive `Ok`, then dispose | 8a | pass |
| `transport-16.close-idempotent-nonblocking` | TRANSPORT-16, ASYNC-15 | MUST | both | `Hang` during dispose | 8a | pass |
| `transport-17.single-use-written-once` | TRANSPORT-17 | MUST | both | counting non-seekable stream body | 8b | pass expected (async); blocking via the bridge |
| `transport-18.native-resend-identical` | TRANSPORT-18 | MUST | async | `CreateWithNativeResend`, 1 MiB stream body; buffering failure mid-write | 8b | **waived 8b** |
| `transport-19.abandoned-body-unblocks` | TRANSPORT-19 | SHOULD | async | parked stream body; file body | 8a | pass |
| `transport-20.no-response-is-retryable` | TRANSPORT-20, XCUT-4 | MUST | both | refused port; `Abort` | 8a | pass |
| `transport-20.retried-by-the-pipeline` | TRANSPORT-20 (6a hand-off) | MUST | async | `Abort` then `Ok` under `DexpacePipeline` | 8a | pass |
| `transport-21.pre-dispatch-failure-via-task` | TRANSPORT-21, ASYNC-2 | MUST | async | none | 8a | pass |
| `transport-22.adaptation-failure-releases` | TRANSPORT-22 | MUST | async | `CreateWithFaultingAdaptation` | 8a | pass |
| `transport-23.never-null` | TRANSPORT-23, ASYNC-1, SEAM-16 | MUST | async | `Ok`, 404, 503, `Abort` | 8a | pass |
| `transport-24.vendor-status-readable` | TRANSPORT-24 | MUST | both | `Status(499/520..526/530)` | 8a | pass (F5) |
| `transport-25.lazy-body` | TRANSPORT-25, SEAM-11 | MUST | both | `Streamed`, gated second chunk | 8a | pass |
| `transport-25.large-round-trip` | TRANSPORT-25 | MUST | both | `Large(8 MiB)` | 8a | pass |
| `transport-25.dispose-releases` | TRANSPORT-25 | MUST | both | keep-alive `Large`, partial read, dispose | 8a | pass |
| `transport-26.bodyless-methods` | TRANSPORT-26 | MUST | both | echo request | 8a | pass (F4) |
| `transport-27.inbound-downgrade` | TRANSPORT-27 | SHOULD | both | `Raw` malformed type / length | 8a | pass (F2) |
| `transport-28.file-range-replayable` | TRANSPORT-28 | SHOULD (+MUST replayable) | both | `FromFile(offset, count)` sent twice | 8b | pass expected; zero-copy is not observable on the wire and stays 8b's design call |
| `transport-29.concurrent-no-crosstalk` | TRANSPORT-29, SEAM-12, ASYNC-22 | MUST | both | `EchoId` × 64 | 8a | pass |
| `transport-30.proxy-discoverable-no-leak` | TRANSPORT-30 | SHOULD (+MUST no-leak) | async | `CreateWithProxy`; the fixture as a forward proxy answering `407`, an origin `401` | 8b | **NotExercised** until 8b supplies the hook |
| `async-1.single-non-null-response` | ASYNC-1 | MUST | async | `Ok` | 8a | pass |
| `async-20.late-cancel-leaves-response-open` | ASYNC-20, SEAM-16 | MUST | async | `Ok` with a body | 8a | pass |
| `seam-15.after-dispose` | SEAM-15 | MAY | both | none | 8b's work | **waived 8b** (borrowed client keeps working) |
| `http-39.short-source-fails` | HTTP-39 | MUST | both | exact-length stream shorter than declared | 3a's row | pass |

Forty assertions over 30 `TRANSPORT` IDs, the four per-transport `ASYNC` IDs and six cross-referenced IDs. `NotExercised` on the
SystemNet driver is allowed only for `transport-30.*` and only until 8b; 8a's exit criterion lists it.

---

## Tests, vectors and ports

| Project | Category | What |
|---|---|---|
| `tests/Dexpace.Sdk.Conformance.Tests` (new; xUnit v3 on MTP, `coverlet.MTP`, TRX; references core and the kit only) | `Unit` | Report, status ordering, worst-of views, waiver validation and the stale-waiver rule; `RequirementCatalog` drift against appendix C; the appendix-B map drift; an architecture test that the kit assembly references only `Dexpace.Sdk.Core`, `Microsoft.Extensions.Logging.Abstractions` and the shared framework (no `xunit*`, `NUnit*`, `MSTest*`, no `Dexpace.Sdk.Http.*`); every assertion's IDs exist in the catalogue; every 8a `TRANSPORT` row and every per-transport `ASYNC` row has at least one assertion. |
| | `Integration` | The promoted `LoopbackServerTests` (moved); the new fixture members; **negative controls** — every assertion fails against at least one deliberately broken subject. |
| | `Conformance` | The `DelegateHttpClient` driver over the test-only raw-socket client (`RawSocket/`), with its permanent SHOULD waivers. |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests` | `Conformance` | `SystemNetConformanceTests`: the SystemNet driver (owned, with every hook it can supply) and its waiver list — the `Owner = "8b"` waivers of the catalogue above, nothing else. |
| | (existing) | Every existing wire test, repointed to `Dexpace.Sdk.Conformance.Wire`, unedited otherwise; the phase-1 `Security` classes stay green and unedited (constraint 5). |
| `tests/Dexpace.Sdk.Core.Tests/Client` | `Unit` | The `ASYNC` pins named in the census: `ASYNC-2` (rejecting scheduler), `-7`, `-8`/`-12`, `-9`, `-10`, `-11`, `-13` (cyclic cause), `-14` (independent cancellation). No transport reference (SEAM-2 holds: the kit is not referenced here). |
| `tests/Dexpace.Sdk.AotSmoke` | `AotSmoke` | A slice of the kit under NativeAOT against `SystemNetHttpClient`: `transport-21.*`, `transport-24.*`, `transport-25.large-round-trip`, required `Passed` (P8a-20). It proves the kit and the transport survive trimming; the full suite stays on the JIT run, because consumers run the kit in test projects that are not AOT-published. |

**Ports.** From Node `run-suite.ts` (`nodejs-sdk@c0ff3fd`): the row inventory per `describe` block (`TRANSPORT-1/2/21/23`,
`-24/26/27`, `-17/19/25`, `-19`, `-20`, `-4/5/6/20`, `-3/7/9`, `-15/16/29`, `-10/11`, `-11/12/13`, `-28`, `-14`, `-30`, `-8`),
`TransportCapabilities` → the hooks, the `isolatedUrl` lesson → suite-contract clause 8, `captureDroppedHeaders` → the kit logger's
drop query. From Ruby's 8a design: the five statuses (extended to six), waivers by ID, the factory shape, the suite contract, the
named-script checklist, `await_closed_connection` → `WaitForConnectionReleasedAsync`, and the report preamble's two omissions (TLS,
connect timeout). From this repository: `ExactLengthBodyWireTests` → `http-39.short-source-fails`; `ServerSentEventWireTests`'
gated delivery → `transport-25.lazy-body`; `FramingHeaderDropWireTests` → `transport-11.*` (the `Security` class stays as is).

**Not ported, and why.** 7a's hand-off ("the conformance kit lifts `SERDE-3`, `-4`, `-9`, …") and 7c's (`link-header.json`, the
splice properties) are codec and core-algorithm suites, not transport assertions; the roadmap's phase 10 card gives the
`Conformance` package "the invariant, packaging and codec suites". They are handed to phase 10 (P8a-17).

**Flake budget.** Three OS rows run every assertion against two drivers. Bounds are generous (30 s per assertion, 10 s for
release), every wait is on a condition, servers are per assertion, and the concurrency row uses 64 calls (not hundreds) to stay
clear of Windows ephemeral-port pressure. A flaky assertion is fixed or quarantined by the lead, never retried in a loop.

---

## Shared files with 8b

| File or folder | 8a | 8b |
|---|---|---|
| `src/Dexpace.Sdk.Http.SystemNet/**` | **none** | all of 8b's code |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/Loopback/**` | moved out (deleted here) | must not edit; consumes `Dexpace.Sdk.Conformance.Wire` |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/*WireTests.cs`, `Security/*` | `using` swap only | free after 8a merges |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetConformanceTests.cs` | creates it, with the `Owner = "8b"` waivers | deletes waivers as rows close; adds hooks (`CreateWithProxy`, a handler-constructor subject) |
| `tests/Dexpace.Sdk.Http.SystemNet.Tests/*.csproj` | adds the kit `ProjectReference` | — |
| `src/Dexpace.Sdk.Conformance/**` | creates | may fix an assertion bug it finds, with the negative control, in its own PR |
| `scripts/ci/dependency-audit.cs`, `Dexpace.Sdk.sln`, `.github/workflows/ci.yml` header | edits | — |
| `CHANGELOG.md`, roadmap status notes, `CLAUDE.md`, `docs/first-release.md` | own entries, append-only | own entries, append-only |
| `Directory.Packages.props` | **none** (every package the new test project needs is already pinned) | — |

## PR segmentation

Three pull requests, stacked, each green on its own. Code and tests are not split: the kit's assertions are only meaningful with
their negative controls, so each PR carries both (roadmap step 5 allows it when the split would leave a red intermediate).

1. **Package and fixture.** The csproj, README, `PublicAPI` files, lock file; the fixture moved and made library-grade, with its new
   members; the report, status, waiver and catalogue machinery with the generator script and drift tests;
   `tests/Dexpace.Sdk.Conformance.Tests` with `Unit` and fixture `Integration` tests; SystemNet.Tests repointed; the dependency audit
   and solution edits.
2. **Assertions and drivers.** The 40 assertions with negative controls; the raw-socket client and the `DelegateHttpClient` driver;
   the SystemNet driver with the `8b` waivers; the AOT smoke slice.
3. **`ASYNC` pins and close-out.** The `Core.Tests` pins; `docs/sdk-documentation/conformance.md`; the kit's `README.md` final text;
   the checklist (38 rows, plus the 14 8b rows listed as ⏳ 8b with their assertion names); `CHANGELOG.md`; the status note
   (including the reassignment of `TRANSPORT-2`, `-11`, `-22`, `-27` and the D3 / Reactive verdicts); the knowledge note; the
   corrections below; the housekeeping probe.

## Hand-offs to later phases

- **8b** — the 14 rows, each closed by deleting its `Owner = "8b"` waiver from `SystemNetConformanceTests` (8b's exit: no waiver with
  owner `8b` remains, and `transport-30.*` is no longer `NotExercised`). Specific inputs: F3's discriminator for `TRANSPORT-8`/`-4`;
  F1's native refusal for `TRANSPORT-14` (a §10 entry and a `docs/first-release.md` *Unsatisfied MUSTs* line for the name half,
  unless 8b finds a handler-level path); F7 for `TRANSPORT-10`/`-12`; `SEAM-15`'s latch flips `seam-15.after-dispose`; the
  handler-taking constructor gets its own `TransportSubject` in the same test class; event ids 1-3 move to 110-119 (5b). 8b keeps
  8a's 38 rows green across its rewrite of the sync path; a regression there is 8b's to fix, not a re-opened 8a row. The decision
  whether an adaptation failure is re-typed as an SDK exception (phase 1's S9 note) is 8b's, outside any row.
- **9** — a third driver over the transport `AddDexpaceClient` builds from `IHttpClientFactory` (its named client's primary handler has
  redirects off: `transport-1.redirect-not-followed` is the proof).
- **10** — the invariant, packaging and codec suites in the same package (the 7a `SERDE` lift and 7c's vectors go there);
  `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` over all 61 items, extending 8a's B.6/B.7 view; `RequirementCatalog` reused for
  the row-count gate.
- **12** — publishes the kit at the lockstep version; `conformance.md` joins the user documentation; the post-release trigger
  "first external adapter author" (a non-xUnit runner) stays open.

## Design, roadmap, register and `CLAUDE.md` corrections owed at close-out

Proposals, applied as dated corrections when the checklist lands, not by this document:

1. **Design §2.1** — As built: `Conformance` exists. **§9.3** — the kit **is** measured by the coverage gate (P8a-16); the fixture
   gained release observation (P8a-21); statuses are six with `NotExercised` (P8a-7); the `DelegateHttpClient` driver runs over a
   test-only raw-socket client (P8a-2). **§12** — the `ASYNC` row: `ASYNC-6` met on the bridges (P8a-3); the `TRANSPORT` row: name
   the kit's assertion catalogue as the per-ID evidence.
2. **Roadmap** — Phase 8 card: the Ruby source paths do not exist locally (P8a-25); "`ASYNC-6` … vacuous by antecedent" refined
   (P8a-3); the segmentation of this document. A status note records the reassignment of `TRANSPORT-2`, `-11`, `-22`, `-27` from
   the earlier "(8b)" labels and ⏳ clauses (6a checklist, phase 1 S2 and S9) and D3's confirmation (still for the lead to rule).
3. **`docs/first-release.md`** — the *Packages* row for `Dexpace.Sdk.Conformance` (exists, `0.0.1-alpha.1`, unpublished, owner 12);
   the proposed first version (P8a-5).
4. **`.github/workflows/ci.yml`** — the header's inert-gate line becomes "on: two drivers".
5. **`CLAUDE.md`** — add `src/Dexpace.Sdk.Conformance/` and `tests/Dexpace.Sdk.Conformance.Tests/` to the layout, move "the
   transport conformance kit (8)" out of "What is genuinely unbuilt", and point to `docs/sdk-documentation/conformance.md`.
6. **Knowledge** — a new note file `docs/knowledge/notes/transport-adapter.md` (the harvested topic exists; no note file does yet) recording F1, F3 and F6, the facts a later reader would
   otherwise re-measure.

## Breaking changes

None to a published or public surface: nothing is published, no existing package's `PublicAPI.Unshipped.txt` changes, and no
behaviour of `Dexpace.Sdk.Core` or `Dexpace.Sdk.Http.SystemNet` changes in 8a.

- **New package**, additive: `Dexpace.Sdk.Conformance`.
- **Test-only move**: `Dexpace.Sdk.Http.SystemNet.Tests.Loopback.*` becomes `Dexpace.Sdk.Conformance.Wire.*`. Internal to this
  repository.
- **Forward-looking**: once published, the fixture and the assertion names are public contract. Adding an assertion is a minor
  version (P8a-5); renaming one breaks a consumer's waiver list, so names are frozen at the first `PublicAPI.Shipped.txt`.

## Risks and open questions (all resolved here)

| # | Question | Resolution |
|---|---|---|
| Q1 | Does the segmentation rule's "own document" bind? | Recorded here, liftable verbatim (P8a-1), open for the lead. |
| Q2 | Is `TRANSPORT-14`'s name half satisfiable on SystemNet? | Not by the adapter: F1. 8b dispositions it. |
| Q3 | Can 8a close rows with a sync-over-async blocking face? | Yes: the `TRANSPORT` clauses are observable; the realness of sync is `SEAM-11`/`PIPE-28`, already 8b's. |
| Q4 | Where does the xUnit driver live? | In each test project (P8a-15); TestSupport is framework-free by charter. |
| Q5 | Does a missing hook mean vacuous? | No: `NotExercised` (P8a-7). |
| Q6 | How is "connection released" observed without seeing the client's pool? | Closed **or** reused, from the server side (P8a-21). |
| Q7 | Can the kit use TestSupport's doubles? | No: a package cannot depend on an unpacked project; it carries internal copies of what it needs (P8a-23). |
| Q8 | Does `TRANSPORT-22` need a source seam in SystemNet? | No: a test-side `DelegatingHandler` on a borrowed client (P8a-22). |
| Q9 | Does the SERDE lift belong here? | No: phase 10's codec suite (P8a-17). |
| Q10 | Version? | Lockstep; first published = the first release's version, proposed `0.1.0` (P8a-5), open for the lead. |
| Q11 | Coverage of the kit? | Measured (P8a-16). |
| Q12 | Is the raw-socket driver worth its cost? | Yes for D3 (P8a-2), open for the lead with the decorator fallback. |

**Residuals.** R1: no TLS, HTTP/2 or connect-timeout coverage (preamble). R2: release observation is server-side and bounded, so a
transport that leaks a connection for longer than `ReleaseTimeout` and then frees it passes; the bound is the kit's definition of
"promptly". R3: `TRANSPORT-28`'s zero-copy SHOULD is not observable on the wire; the kit checks the byte range and replayability only.
R4: timing-sensitive assertions on three OS rows (mitigated, Flake budget).

## Exit criteria

From the card, for 8a's share and the phase's segmentation:

1. The segmentation is recorded (this document): 52 IDs, each to exactly one sub-phase; 8a's checklist has **38 rows**, each ✅
   with a named test (kit assertion or existing test), `[Trait]` and file, or N/A / vacuous with its section (`ASYNC-4`, `ASYNC-16`,
   `ASYNC-21`, and `ASYNC-6`'s facade half); the 14 8b rows are listed as ⏳ 8b with their assertion names.
2. The kit is **green against both drivers on every OS row**, with waivers listed by ID: on SystemNet only `Owner = "8b"` waivers and
   `transport-30.*` `NotExercised`; on the raw-socket driver only its stated SHOULD waivers and missing hooks.
3. `Dexpace.Sdk.Conformance` is packable, with its README, `PublicAPI` files and package validation; the dependency audit names it;
   the reproducible pack includes it; `IsAotCompatible` holds and the AOT smoke slice passes.
4. The kit's first published version is named (P8a-5).
5. D3's second-transport argument confirmed against the kit's results and the `Dexpace.Sdk.Reactive` question answered (no
   consumer), both recorded in the status note for the lead.
6. Every gate green: build (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051`, `CA2007`), `dotnet format
   --verify-no-changes`, the full test run, the 80% coverage gate (now including the kit), the dependency audit, the reproducible
   pack, `scripts/knowledge verify-structure`.
7. `docs/sdk-documentation/conformance.md` (how to drive the kit, the subject and hooks, statuses and waivers, the preamble), the
   kit's `README.md`, a `CHANGELOG.md` `[Unreleased]` entry, a dated roadmap status note, the close-out corrections, the knowledge
   note, and a housekeeping probe that reports nothing.

## Rulings

| ID | Ruling | Open for the lead |
|---|---|---|
| P8a-1 | Segmentation: the "needs a SystemNet change" rule; 8a 38 rows (16 `TRANSPORT` + 22 `ASYNC`), 8b 14; `TRANSPORT-2`, `-11`, `-22`, `-27` reassigned from earlier informal 8b labels; recorded here rather than in a separate segmentation file | **yes** |
| P8a-2 | D3 confirmed: no second first-party transport; agnosticism proven by a `DelegateHttpClient` driver over a test-only raw-socket HTTP/1.1 client (decorator fallback if its cost is refused) | **yes** |
| P8a-3 | `Dexpace.Sdk.Reactive` not built (no consumer); `ASYNC-21` N/A adapter-scoped; `ASYNC-6` met on the bridges, vacuous for facades | — |
| P8a-4 | One `src/` package, core-only, every library gate on, `IsAotCompatible` | — |
| P8a-5 | Lockstep version, no override; first published version = the first release's, proposed `0.1.0`; adding an assertion is a minor version | **yes** |
| P8a-6 | Framework-free assertions; `ConformanceException : Exception`, not `SdkException` | — |
| P8a-7 | Six statuses: `Passed`, `Failed`, `Errored`, `Vacuous` (measured from inside), `Waived`, `NotExercised` (missing face or hook) | — |
| P8a-8 | Waivers by ID with reason and optional owner; unknown IDs rejected; a waiver that is no longer needed fails the run; all waivers printed on every run | — |
| P8a-9 | The subject is a factory descriptor with optional capability hooks; the ten-clause suite contract | — |
| P8a-10 | Each assertion runs per declared face through one send primitive | — |
| P8a-11 | The fixture is moved (not copied) into `Dexpace.Sdk.Conformance.Wire`, made library-grade, and extended; scripts are `LoopbackResponse` factories | — |
| P8a-12 | Plaintext HTTP/1.1 only; the omissions are stated in the report preamble | — |
| P8a-13 | The ID→level catalogue is generated from appendix C by a file-based script into a checked-in internal file, with a drift test | — |
| P8a-14 | Appendix-B view for B.6 and B.7 only in 8a, with a drift test; phase 10 extends | — |
| P8a-15 | The xUnit driver is per test project, not shared | — |
| P8a-16 | The kit is measured by the coverage gate (correcting §9.3's exclusion) | — |
| P8a-17 | Taken: 7b's gated delivery (as `transport-25.lazy-body`), 2b/3a per-transport proofs; handed to 10: 7a's SERDE lift and 7c's vectors | — |
| P8a-18 | 6a's hand-off taken as two pipeline-level assertions (`transport-20.retried-by-the-pipeline`, `transport-7.attempt-timeout-aborts`) | — |
| P8a-19 | `SEAM-15` is asserted against a declared `AfterDisposeBehavior`, waived on SystemNet until 8b | — |
| P8a-20 | The AOT smoke consumer runs a three-assertion slice | — |
| P8a-21 | "Released" = closed or reused, observed server-side within `ReleaseTimeout` | — |
| P8a-22 | `TRANSPORT-22`, `-8`, `-18` hooks are built test-side over a borrowed `HttpClient`, so 8a changes no SystemNet source | — |
| P8a-23 | The kit carries internal recording logger and counting streams; no dependency on `Dexpace.Sdk.TestSupport` | — |
| P8a-24 | F1, F3 and F7 routed to 8b as row inputs; F1–F7 filed as a knowledge note | — |
| P8a-25 | Port from Ruby's 8a design document (its gem is absent locally) and Node `@c0ff3fd` | — |
| P8a-26 | One new test project, `tests/Dexpace.Sdk.Conformance.Tests`, with `InternalsVisibleTo` from the kit | — |
| P8a-27 | Three stacked PRs, code and tests together | — |

## Deviation Ledger

Entries for roadmap constraint 7. None introduces a new specification deviation: `TRANSPORT-20`'s I/O-family letter is §10 entry 7,
the interrupt shapes of `TRANSPORT-3`, `ASYNC-3`, `-4`, `-14` are §10 entry 8, `ASYNC-21` is §11 item 21. The port-level departures
are from design §9.3's text (P8a-7, P8a-16, P8a-21, P8a-2: close-out correction 1) and from the roadmap card (P8a-1, P8a-3, P8a-25:
correction 2). `TRANSPORT-14`'s name half (F1) is a candidate §10 entry that 8b, which owns the row, opens or refutes.

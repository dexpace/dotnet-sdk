# Phase 2b — Seams: Design

**Status:** Draft, for review. Written 2026-09-30 against `main` at `bf8f4ae`, on branch
`36-phase-2a-domain-model-design`, beside the uncommitted 2a design and plan. GitHub issue
[#36](https://github.com/dexpace/dotnet-sdk/issues/36). The scope authority is the
[phase 2 segmentation design](../2026-09-29-phase2-segmentation-design.md), which gives 2b 29 requirement rows and
names four things this document must argue. The precedent for its structure is the
[2a design](../phase2a/2026-09-29-phase2a-domain-model-design.md).

**What this document is.** The sub-phase design for 2b: one explicit decision per requirement row, the public shape
of every seam type 2b changes or adds, argued positions on every judgement call the segmentation design hands to 2b
and on every N/A candidate, a migration plan from the as-built code, the breaking-changes list, and a landing order
in pull-request-sized steps that separates the work free to start now from the SPI work gated on 2a's
`RequestOptions`.

**What this document is not.** It is not the [plan](2026-09-30-phase2b-seams.md) (numbered TDD tasks) and not the checklist. It does not restate
the seam mapping of [design §3](../../../../sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md), the bridge
argument of [design §5.3](../../../../sdk-design-dotnet/05-pipeline-architecture.md) or the construction pattern of
[design §4](../../../../sdk-design-dotnet/04-domain-model-construction.md). It cites them (roadmap constraint 9).
Where the design already decides something, this document records the decision against its row. It argues only what
the design and the segmentation design leave open, and the places where verification on the pinned runtime changed
an obvious answer.

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience**, as the roadmap's step 2 requires. The segmentation
design argues the phase-level edges; this section restates the ones 2b stands on, and the 2a→2b edges in full, as
that design requires of 2b rather than inheriting "2a first" by habit.

| Predecessor | Kind | State at `bf8f4ae` |
|---|---|---|
| Phase 0 tasks 1–4 (green build, CI and analyzers, test partition) | **dependency** | Met. The `PublicAPI.Unshipped.txt` diff under `RS0016`/`RS0017` is the evidence of the SPI change (constraint 8). `Seam2ArchitectureTests` exists, and the in-memory fakes live in `tests/Dexpace.Sdk.TestSupport`. `M:System.Uri.ToString` is already in `BannedSymbols.txt`; the five `RS0030` pragmas in `src/` (`TokenCredential.cs:46`, `HttpClientExtensions.cs:52`, `HttpPipeline.cs:79`, `SystemNetHttpClient.cs:212` and `:234`) are all sync-bridge or `HttpClient`-construction waivers, and none waives the `Uri.ToString()` ban. |
| Phase 0 task 8, the design and styleguide harvest ([#29](https://github.com/dexpace/dotnet-sdk/issues/29)) | **convenience** | Open. The consequence is the segmentation design's: phase 0 has not literally exited, and 2b proceeds anyway, citing the design and the styleguide by path, not by corpus key. |
| Phase 1 (S1–S9) | **convenience** | Met. No phase-1 item is routed to 2b. 2b keeps the four wire `Security` classes and the core `Security` classes green without editing them (see [Migration](#migration-plan-from-the-as-built-code)). |
| **2a, `RequestOptions` merged** (2a PR 1: `HTTP-34`, `HTTP-35`) | **dependency**, for the SPI work only | Not yet built. The signatures `Execute(Request, RequestOptions, CancellationToken)` and `ExecuteAsync(Request, RequestOptions, CancellationToken)` do not compile without the type. This is 2b's **only** hard gate. |
| 2a, the rest | **convenience** | See the edge table below. Nothing in 2a needs anything from 2b. |

**The 2a→2b edges, each with its kind.** Every edge runs one way.

| 2b work | Needs from 2a | Kind | Consequence here |
|---|---|---|---|
| PR 3 and PR 4: `SEAM-11`, `SEAM-13`, `SEAM-18`, `SEAM-25`'s bridge clause, `DelegateHttpClient`, the `SEAM-16`/`SEAM-30` pins, and the matching edits to `SystemNetHttpClient`, `TestSupport`, `AotSmoke` and `PipelineRunner` | `RequestOptions` with its `Empty` instance (2a PR 1) | **dependency**: the type does not exist | PR 3 opens only after 2a PR 1 merges |
| PR 5: `SEAM-27`'s "the query MUST be RFC-3986 rendered" | `Query`, `Query.Encode` and the internal `Rfc3986` encoder, plus the `tests/vectors/` loader (2a PR 2) | **convenience** in the specification's terms (design §3.5 never names `Query`). **This design takes it as a code edge by choice:** `OperationDescriptor.Query` is typed `Query`, so one RFC 3986 renderer serves both (position H) | PR 5 opens only after 2a PR 2 merges. 2a lands `Query` second in its stack, so the choice costs no calendar time |
| PR 5: `BuildRequest` produces a `Request` | the validating `Request` constructor (`HTTP-7`, 2a PR 6) | **convenience** | `BuildRequest` calls `new Request(method, url, headers, body)`, which compiles against the as-built and the rebuilt constructor alike. It inherits `HTTP-7` whenever PR 6 lands and owes no row for it |
| PR 5: `OperationDescriptor`'s construction | design §4's pattern | **convenience** | Written down already; 2a's `RequestOptions` is the first built instance to copy |
| PR 5: `OperationDescriptor`'s `Method` null check | `Method` as a `sealed record` (2a PR 4) | **convenience** | Before 2a PR 4 `Method` is a `readonly record struct`, so a `null` `Method` cannot be written and the check has nothing to reject. If PR 5 lands first, the null check and its test are added by the first 2b PR that follows 2a PR 4's merge, and PR 6 at the latest (PR 6 then also waits for 2a PR 4) |
| PR 5: `SEAM-28`'s "MUST NOT affect the headers" test, and `OperationDescriptor.Equals` over `Headers` | `Headers` value equality (`HTTP-13`, 2a PR 5) | **convenience** | Before 2a PR 5 `Headers` has no `Equals` override, so the test compares header enumerations, and `OperationDescriptor.Equals` (which delegates to `Headers.Equals`) is reference equality on `Headers` until then |
| PR 5: `OperationDescriptor.Equals` over `Body` | `RequestBody` equality (`HTTP-46`, 2a PR 6) | **convenience** | As built, `RequestBody` has no equality members, so `Body` compares by reference until 2a PR 6 gives it its contract; `OperationDescriptor.Equals` delegates and inherits it with no 2b change |
| PR 5: URL error messages | the shared internal `UrlRedactor` instance (2a PR 6) | **convenience** | If 2b's PR 5 lands first, it introduces the shared instance and 2a's PR 6 reuses it |
| PR 3: the transport and fake edits | 2a PR 7's `Response(request, …)` constructor and its task 7.1 `TestResponses` chore touch the same files | **convenience**: parallel edits would conflict, not fail | Sequenced in [Landing order](#landing-order) |

---

## Governing documents, and the phase-start queries

- **Normative.** The canonical text of every row is its appendix-C row
  (`docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`).
- `docs/product-spec/02-architectural-principles.md` states `SEAM-1`, `SEAM-2` and `SEAM-13`.
- `docs/product-spec/03-pluggable-seams-and-extension-model.md` states `SEAM-3`–`SEAM-12`, `SEAM-14`,
  `SEAM-16`–`SEAM-19`, `SEAM-21`, `SEAM-24`–`SEAM-27` and `SEAM-30`, with their conformance clauses.
- `SEAM-15`, `SEAM-20`, `SEAM-22`, `SEAM-23` and `SEAM-28` are the gap IDs: appendix C is their only normative
  statement, and each was read from its appendix-C row.
- **Design.** §3.1–§3.7 of `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md`, with their **As built
  (d45e64b)** lines; §5.3 of `docs/sdk-design-dotnet/05-pipeline-architecture.md` (the bridges); §7.3 of
  `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md` (the serde failure model); §1 of
  `docs/sdk-design-dotnet/01-overview.md` (`ExecutionContext` flow, `SEAM-24`). In §10, the topics
  `logging-abstractions-dependency`, `byte-stream-provider-retired`, `cooperative-cancellation`,
  `no-provider-registry` and `no-builder-objects`. In §11, items 2, 3, 5, 14 and 35. In §12, the vacuous list.
- **Styleguide.** `docs/styleguide/csharp/08-error-handling.md` (8.6: custom exceptions are sealed, which `SEAM-23`
  overrides for the serde hierarchy), `09-concurrency.md` (9.1: no blocking on async outside a documented bridge),
  `10-api-design.md` (10.1 minimal surface, 10.5 token last), `13-resource-management.md` (13.4: never dispose an
  injected dependency). The SDK overlay's `I`-prefix and `Async`-suffix departures are kept on the SPI.

| Query, run 2026-09-30 | Result |
|---|---|
| `scripts/knowledge --origin note --brief` | exit 1: no notes exist |
| `scripts/knowledge --section conflicts --brief` | exit 1: no Conflicts entry in either tree |
| `scripts/knowledge --prefix-info SEAM` | 30 IDs: 23 MUST, 5 SHOULD, 2 MAY; 25 substantive, 5 uncited |
| `scripts/knowledge --gaps SEAM` | `SEAM-15`, `SEAM-20`, `SEAM-22`, `SEAM-23`, `SEAM-28`, "appendix C is their only normative statement" |

The single-prefix `--gaps` form was used; the comma form is required only for several prefixes, because the space
form silently drops all but the first ([#31](https://github.com/dexpace/dotnet-sdk/issues/31)).

**Scope.** 29 rows: `SEAM-1`–`SEAM-28` and `SEAM-30`. That is 22 MUST, 5 SHOULD (`SEAM-8`, `SEAM-10`, `SEAM-13`,
`SEAM-17`, `SEAM-24`) and 2 MAY (`SEAM-15`, `SEAM-28`). `SEAM-29` is 2a's.

---

## Verified facts that shape the decisions

Each was verified on 2026-09-30 on the pinned SDK (10.0.401) with a throwaway program in the scratchpad, never in the
repository. Each is cited by number from the rows and positions below.

1. **An option-less extension binds on a class-typed receiver.** With `ExecuteAsync(Request, RequestOptions,
   CancellationToken)` as the class's instance member and `ExecuteAsync(this IAsyncHttpClient, Request,
   CancellationToken = default)` as an extension, both `transport.ExecuteAsync(request, ct)` and
   `transport.ExecuteAsync(request)` on a variable typed as the class bind to the extension.
2. **A defaulted token on the three-parameter member is a trap.** If the implementation declares
   `ExecuteAsync(Request, RequestOptions, CancellationToken cancellationToken = default)`, then
   `transport.ExecuteAsync(request, default)` binds the **instance** member with `options: null` (instance members
   win over extensions, and `default` converts to a reference type), with only a nullable warning. So no
   three-parameter SPI member takes a default, on the interfaces or on any implementation.
3. **Two `Create` overloads over `Func<…, Response>` and `Func<…, Task<Response>>` are ambiguous for a throwing
   lambda.** `Create((Request r, RequestOptions o, CancellationToken ct) => throw …)` is `CS0121`. Sync, `async` and
   `Task.FromResult` lambdas and method groups resolve correctly.
4. **`Task.Factory.StartNew(fn, token, options, scheduler)`**:
   - a token cancelled after the delegate started, with the delegate returning normally, gives `RanToCompletion`
     with the result;
   - a token cancelled before the delegate starts gives `Canceled`, and the delegate never runs;
   - a delegate that throws `OperationCanceledException` on the same token gives `Canceled`;
   - with `TaskScheduler.Default` and `TaskCreationOptions.LongRunning`, the delegate runs on a dedicated thread
     (`IsThreadPoolThread` is `false`); without `LongRunning` it runs on a pool thread;
   - on a custom scheduler that starts its own thread, an `AsyncLocal<T>` value and `Activity.Current` set by the
     caller are visible inside the delegate.
5. **`System.Uri` and SEAM-27's composition.**
   - `new Uri("https://h/c#").Fragment` is `"#"`: an empty fragment is still a fragment;
   - `new Uri("https://h/c?sig=100%")` escapes the bare `%` to `%25` at parse, so a `Uri`-typed base query is always
     RFC 3986 (the Ruby port's `QUERY_LITERAL` check has nothing to catch here);
   - `new Uri("https://h/c?")`: `Query` is `"?"`, `GetComponents(Query, UriEscaped)` is `""`;
   - `GetComponents(Path, UriEscaped)` has no leading slash (`c/`, and `""` for a root base);
   - `GetLeftPart(UriPartial.Authority)` keeps userinfo and port (`https://u:p@h:8443`);
   - `Uri.EscapeDataString("..")` is `..`, and a lone surrogate becomes `%EF%BF%BD` silently.
6. **An abstract exception with protected standard constructors passes `CA1032`** under
   `AnalysisLevel=latest-recommended` with warnings as errors.

---

## Decisions, one per requirement row

**How to read the table.**

- **Exit** uses the roadmap's constraint-3 legend. A combined mark (`N/A ⏳`) is used as phase 1 used `✅ ⏳`: each
  clause has its own mark and the row says which is which.
- **Tests** are `[Trait("Category", "Unit")]` unless another category is named. Core test classes mirror the source
  folders under `tests/Dexpace.Sdk.Core.Tests/{Client,Serialization,Operations,Pipeline,Architecture}/`.
- A "pin" adds a test for behaviour that is already correct.
- **PR** points to [the landing order](#landing-order).

| ID | Level | Decision | Rationale / source | Types affected | Test approach | PR | Exit |
|---|---|---|---|---|---|---|---|
| `SEAM-1` | MUST | Already met. Core embeds no transport, byte-stream implementation or codec, and depends at run time only on the shared framework plus the logging facade. Pin the "no concrete capability" half as an architecture test beside the existing dependency gate. | Design §2.4; §10 `logging-abstractions-dependency` (the facade is sanctioned). | core assembly | Cite `scripts/ci/dependency-audit.cs` (the `deps.json`/nuspec gate). New `Seam1ArchitectureTests.Core_references_no_transport_or_codec_assembly`: core's referenced assemblies include none of `System.Net.Http`, `System.Net.Sockets`, `System.Net.Security`, `System.Text.Json` (verified 2026-09-30 on the as-built Release assembly, which references only the runtime, collection, threading, LINQ and diagnostics assemblies and the logging facade). | 2 | ✅, with the §10 topic |
| `SEAM-2` | MUST | Keep the three seams (`IHttpClient`, `IAsyncHttpClient`, `ISerde`) plus the projection's `OperationDescriptor`, each narrow. Core ships **adapters over** a seam (the two bridges, `DelegateHttpClient`, `BuildRequest`) but no implementation **of the external concern**; that reading is position A and P2b-1. | Design §2.3, §3.2; §11 item 3 (a retired seam is not a violation). | `Client/*`, `Operations/*` | Cite `Seam2ArchitectureTests` (core references no Dexpace adapter; core's suite loads none). New `SeamImplementationArchitectureTests.Core_implements_a_seam_only_through_the_allow_listed_adapters`: every core type implementing `IHttpClient`, `IAsyncHttpClient` or `ISerde` is non-public and on the allow-list (the two private bridge adapters in PR 2; the two private `DelegateHttpClient` adapters added in PR 4). | 2, 4 | ✅, reading P2b-1 |
| `SEAM-3` | MUST | **N/A.** The byte-stream provider seam is retired; `Stream`, `Memory<byte>`, `IBufferWriter<byte>` and `ArrayPool<byte>` are the shared framework's, and core ships no factory. | Design §3.1; §10 `byte-stream-provider-retired`. | — | None of its own. The retained IO contract is phase 3a's. | 6 | N/A |
| `SEAM-4` | MUST | **N/A**, as `SEAM-3`: there is no factory to make concurrency-safe. | Design §3.1; §10 `byte-stream-provider-retired`. | — | None. | 6 | N/A |
| `SEAM-5` | MUST | **N/A for the DI-less half.** A transport or codec is a non-nullable parameter, so zero candidates is a compile error and several are unrepresentable. **The DI half is ⏳ against phase 9**, whose `ValidateOnStart` single-registration check is the resolution rule under a container. | Design §3.6; §10 `no-provider-registry`; §11 item 14. | `DexpacePipeline.CreateDefault`, `RequestBody.FromValue` | Pin the DI-less outcome: `CreateDefault(null!)` throws `ArgumentNullException` (`ParamName` `transport`), and `RequestBody.FromValue(v, null!)` throws for `serde`. | 2 | N/A ⏳ (phase 9 card, `Dexpace.Sdk.Extensions.DependencyInjection`) |
| `SEAM-6` | MUST | **N/A for the DI-less half** (there is no install call to be idempotent); **DI half ⏳ against phase 9** (`TryAdd` plus the validator's different-provider failure). | Design §3.6; §10 `no-provider-registry`; §11 item 14. | — | None in 2b. | 6 | N/A ⏳ (phase 9 card) |
| `SEAM-7` | MUST | **N/A.** No discovery scan exists to cache; under DI, caching is the singleton lifetime, which phase 9 cites. | Design §3.6; §10 `no-provider-registry`. | — | None. | 6 | N/A |
| `SEAM-8` | SHOULD | **N/A, vacuous**: no auto-resolved provider exists to be replaced. | Design §3.6; §12's vacuous list. | — | None. | 6 | N/A |
| `SEAM-9` | MUST | **N/A.** There is no resolution, install or swap state to publish. | Design §3.6; §10 `no-provider-registry`. | — | None. | 6 | N/A |
| `SEAM-10` | SHOULD | **N/A, vacuous**: one default load context, no registry to de-duplicate. | Design §3.6; §12's vacuous list. | — | None. | 6 | N/A |
| `SEAM-11` | MUST | `IHttpClient.Execute(Request, RequestOptions, CancellationToken)`, no defaults (fact 2), plus the option-less `HttpClientExtensions.Execute(this IHttpClient, Request, CancellationToken = default)` that passes `RequestOptions.Empty`. The response body is never pre-buffered: documented on the interface. A bare send lambda is a transport through `DelegateHttpClient.CreateBlocking`. **The real synchronous path is ⏳ against 8b** (coupling obligation 5): `SystemNetHttpClient.Execute` stays sync-over-async until then. | Design §3.2; roadmap coupling obligation 5. | `IHttpClient`, `HttpClientExtensions`, `SystemNetHttpClient`, `RecordingSyncTransport` | `HttpClientExtensionsTests.The_option_less_Execute_passes_RequestOptions_Empty_and_the_token` (in PR 3 the recording fakes' `LastCall` captures both by reference, because `DelegateHttpClient` does not exist until PR 4; PR 4 adds the `DelegateHttpClient` variant). `DelegateHttpClientTests.A_bare_send_lambda_works_as_a_blocking_transport`. `…An_options_ignoring_transport_returns_the_same_response_with_and_without_options` (the conformance clause). The no-pre-buffering proof per transport is 8a's kit (`TRANSPORT-25`). | 3, 4 | ✅ for the contract; ⏳ 8b for the real synchronous path |
| `SEAM-12` | MUST | Document the concurrency contract on both interfaces. Core's own implementations (the bridges, `DelegateHttpClient`) keep all per-call state in locals. | Design §3.2, §1. | both interfaces | `ConcurrencyTests.No_cross_talk_through_the_bridges_or_DelegateHttpClient`: 64 concurrent calls with distinct requests and options through `AsAsync`, `AsBlocking` and both `DelegateHttpClient` forms, each response paired with its own request and options. The per-transport proof is ⏳ against 8a's kit. | 3, 4 | ✅ for core's implementations; ⏳ 8a per transport |
| `SEAM-13` | SHOULD | The synchronous seam gains its `CancellationToken` (it had none), and both bridges pass the call's token into the wrapped call. Honouring it inside blocking socket I/O is 8b's (`HttpClient.Send`); the per-transport async abort proof is 8a's. | Design §3.2, §3.3; §10 `cooperative-cancellation`. | `IHttpClient`, bridges | `AsyncToSyncBridgeTests.Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException`; `SyncToAsyncBridgeTests.The_calls_token_reaches_the_blocking_Execute` (the delegate observes the token and returns when it fires). | 3 | ✅ for the token on both seams; ⏳ 8b (blocking I/O) and 8a (async abort per transport) |
| `SEAM-14` | MUST | **Ownership** is pinned now: the adapter never disposes a caller-supplied `HttpClient`, and disposes its owned one; the bridges dispose nothing they wrap. Repeated dispose is safe today (it relies on `HttpClient`'s own idempotence); the **SDK-owned idempotence latch is ⏳ against 3b** (the roadmap's phase 3 card: "the dispose latches on `Response`, bodies and transports"). Clause (3), interrupt-safety, maps to cancellation. `DelegateHttpClient`'s dispose is a no-op, which the clause allows. | Design §3.7; §10 `cooperative-cancellation` for clause (3). | `SystemNetHttpClient`, bridges, `DelegateHttpClient` | `SystemNetHttpClientOwnershipTests` (in the SystemNet suite, over an in-process `HttpMessageHandler`): after disposing a transport over a borrowed `HttpClient`, that client still sends; after disposing the owned form, a send throws `ObjectDisposedException`; `Dispose` twice does not throw. `…Bridges_never_dispose_the_wrapped_client` in both bridge test classes (`RecordingTransport.IsDisposed` stays `false`). | 2, 3 | ✅ for ownership; ⏳ 3b for the latch; clause (3) cites §10 `cooperative-cancellation` |
| `SEAM-15` | MAY | Document the post-dispose mode on both interfaces: implementation-defined, and each SDK-shipped implementation states its own. `DelegateHttpClient` and the two bridges **keep working** after dispose (they hold nothing). `SystemNetHttpClient`'s owned client throws `ObjectDisposedException` (inherited from `HttpClient`) and a borrowed one keeps working, until **8b's `ObjectDisposedException` latch** makes both throw, as design §3.2 and §3.7 choose. | Design §3.2, §3.7 (gap ID). | both interfaces, `DelegateHttpClient` | `DelegateHttpClientTests.It_keeps_working_after_dispose` pins the documented mode. | 3, 4 | ⏳ 8b (the latch); the mode is documented in 2b |
| `SEAM-16` | MUST | The async seam returns the non-nullable `Task<Response>`. Because nullability is compile-time only, **the pipeline runner asserts the transport's result** and converts a `null` into `PipelineAbortedException` at the transport boundary, before any policy sees it; `DelegateHttpClient`, `AsAsync` and `AsBlocking` reject a `null` from what they wrap (a `null` result, and for the async side a `null` task) with `InvalidOperationException`, so `AsBlocking` neither hands a `null` to its caller nor fails with a `NullReferenceException` from `GetAwaiter()`. A signalled token after delivery touches nothing. | Design §3.3 ("the pipeline's terminal runner therefore asserts the transport's result"). | `PipelineRunner`, `DelegateHttpClient`, `AsAsync`, `AsBlocking` | `PipelineRunnerTests.A_null_response_from_the_transport_fails_at_the_runner` (a test-local `IAsyncHttpClient` returning `Task.FromResult<Response>(null!)`; no policy observes `Response == null`). `DelegateHttpClientTests.A_null_result_faults_the_task`. `AsyncToSyncBridgeTests.A_null_Task_or_a_null_Response_from_the_wrapped_client_fails_with_InvalidOperationException`. `…Cancelling_after_delivery_leaves_the_response_readable`. | 3, 4 | ✅ |
| `SEAM-17` | SHOULD | The pivot is `Task<Response>`: satisfied verbatim. The second sentence (ecosystem facades as separate adapter modules) is **vacuous**: .NET has one async ecosystem, so no facade module ships (position C, P2b-3). | Design §3.3; §1. | `IAsyncHttpClient` | `SeamSurfaceTests.The_async_seam_returns_Task_of_Response` (reflection: not `ValueTask`). | 3 | ✅ for the pivot; the adapter-module sentence vacuous (P2b-3) |
| `SEAM-18` | MUST | Rewrite both bridges (the lead's decision 1): `AsAsync(this IHttpClient, TaskScheduler scheduler)` with **no** zero-scheduler overload, running each call through `Task.Factory.StartNew` on the caller's scheduler with `LongRunning \| DenyChildAttach` (fact 4); options and token threaded into `Execute`; `AsBlocking(this IAsyncHttpClient)` blocks with `GetAwaiter().GetResult()`, which unwraps, and threads options and token into `ExecuteAsync`, so cancelling the token cancels the in-flight task. Neither disposes what it wraps. The interrupt clause maps to the token. | Design §3.3, §5.3; §10 `cooperative-cancellation`; segmentation decision 1. | `HttpClientExtensions` | `SyncToAsyncBridgeTests`: no `AsAsync` overload lacks a `TaskScheduler` (reflection); a `null` scheduler throws `ArgumentNullException`; the call runs on the given scheduler (a `RecordingTaskScheduler` counts `QueueTask`); the exact options and token instances reach `Execute`. `AsyncToSyncBridgeTests`: a faulted `InvalidOperationException` surfaces as itself, not `AggregateException`; options and token reach `ExecuteAsync`. `PIPE-33`/`PIPE-34` (4c) cite these classes. | 3 | ✅; the interrupt clause cites §10 `cooperative-cancellation` |
| `SEAM-19` | MUST | Already met: `ISerde.DefaultMediaType` is a required interface property with no default implementation, and `RequestBody.FromValue` stamps it when no media type is given. | Design §3.4. | `ISerde` | Pin: `SerdeSeamTests.DefaultMediaType_has_no_default_implementation` (reflection: the property's getter is abstract); `…FromValue_stamps_the_codecs_own_media_type` with a fake codec declaring `application/xml` (`TestSupport`'s new `BytesSerde`, because `ScriptedSerde` declares a fixed `application/json` and writes nothing). | 1 | ✅ |
| `SEAM-20` | MUST | The seam keeps its two primitives (stream, `IBufferWriter<byte>`); core adds the other two profiles once, as extension methods: `SerializeToUtf8Bytes`, `SerializeToString` and the fixed-buffer `Serialize(Span<byte>, T)`. The fixed buffer returns the count, honours the offset by `buffer.AsSpan(offset)`, and throws `ArgumentOutOfRangeException` (not an `SdkException`, chaining nothing) when the payload does not fit, leaving the destination untouched. `SerializeToString` returns the codec's own string when it implements the optional `IStringSerde`, and otherwise decodes the buffer as UTF-8 (position G; design §3.4 as written, ruling 4). Encode failures come from the primitive as `SerializationException` and are not re-wrapped; a stream `IOException` propagates unwrapped. | Design §3.4; §7.3 (`SERDE-4`) (gap ID). | `SerdeExtensions`, `IStringSerde` (new), the STJ adapter's tests | `SerdeProfileTests` over `BytesSerde` (a fake codec that writes a given payload): each profile's bytes; two `SerializeToUtf8Bytes` calls return distinct arrays; overflow throws `ArgumentOutOfRangeException` and leaves every byte of the destination as it was, including those before the offset; a `SerializationException` from the primitive surfaces as the same instance; `SerializeToString` decodes UTF-8 whatever charset the codec declares, and an `IStringSerde` override wins. In `SystemTextJsonSerdeTests`: `SerializeAsync_leaves_the_destination_open`; `An_IOException_from_the_destination_propagates_unwrapped`; the cycle tests gain an `InnerException is JsonException` assertion. | 1 | ✅ |
| `SEAM-21` | MUST | Already met: `DeserializeAsync<T>` is the explicit, reified type token; decode failures are `DeserializationException` chaining the cause; a read `IOException` propagates; the adapter reads to EOF and leaves the stream open. The "no-codec deserializer" clause is **vacuous**: core ships no format-agnostic deserializer (`SEAM-1`). | Design §3.4, §7.3. | `ISerde`, the STJ adapter | Pin in `SystemTextJsonSerdeTests`: `DeserializeAsync_leaves_the_source_open`; `An_IOException_from_the_source_propagates_unwrapped`; the malformed-JSON test gains `InnerException is JsonException`. | 1 | ✅; the no-codec clause vacuous (P2b-3) |
| `SEAM-22` | MUST | **✅ by construction, not N/A** (position B, P2b-2). The capture is the generic argument itself, which the CLR binds to a closed type at run time; no `Type`-taking decode overload exists, so no open generic can reach the codec. A tripwire keeps it that way. | Design §3.4 (gap ID). | `ISerde` | `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type` (reflection; the failure message cites `SEAM-22`'s `ContainsGenericParameters` rule). `SystemTextJsonSerdeTests.A_generic_helper_decodes_into_the_closed_type` (`Decode<T>() => serde.DeserializeAsync<List<T>>` called with `Widget` yields a `List<Widget>`). | 1 | ✅ (P2b-2) |
| `SEAM-23` | MUST | New `public abstract class SerdeException : SdkException` with protected standard constructors; `SerializationException` and `DeserializationException` **unsealed** and re-parented under it (the lead's decision 2). Unchecked is free. Adapters keep throwing the subtypes instead of the codec's own exception, chaining it. | Design §3.4, §7.3 (gap ID); segmentation decision 2; position F. | `Errors/SerializationExceptions.cs` | `SerdeExceptionHierarchyTests`: `SerdeException` is abstract and derives from `SdkException`; both subtypes derive from it and are not sealed; a test-local `sealed class WidgetDecodeException : DeserializationException` compiles and is caught by `catch (SerdeException)`; the chained cause survives. `SERDE-9`/`SERDE-10` (7a) cite this class. | 1 | ✅ |
| `SEAM-24` | SHOULD | The `AsAsync` bridge is the one SDK component that hands work to another thread. `ExecutionContext` flows through `Task.Factory.StartNew` on any scheduler (fact 4), so `AsyncLocal<T>`, `Activity.Current` and `ILogger` scopes reach the worker with no code. Cancellation maps both ways through the one token: the caller's token reaches the blocking call, and a cancelled call surfaces `OperationCanceledException` to the caller. The adapter-module clause is **vacuous** (position C). | Design §1, §3.3, §8.1. | `AsAsync` | `SyncToAsyncBridgeTests.The_callers_AsyncLocal_and_Activity_reach_the_worker_thread` (on a `RecordingTaskScheduler` that starts its own thread). | 3 | ✅ for the bridge; the adapter-module clause vacuous (P2b-3) |
| `SEAM-25` | MUST | The first sentence (an adapter that **owns** an executor) is **vacuous**: `AsAsync` takes the caller's scheduler and nothing in the SDK owns one. The last sentence holds: disposing the bridge touches neither the scheduler nor the wrapped client. | Design §3.3, §3.7, §5.3; segmentation decision 1. | `AsAsync` | `SyncToAsyncBridgeTests.Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client` (a `RecordingTaskScheduler` that implements `IDisposable` is not disposed; `RecordingSyncTransport.IsDisposed` stays `false`). | 3 | ✅ for the caller-executor clause; the owning-adapter sentence vacuous (P2b-3) |
| `SEAM-26` | MUST | New `sealed record OperationDescriptor` in `Dexpace.Sdk.Core.Operations`: `required` `Method` and `PathTemplate` (a missing one is `CS9035`, §10 `no-builder-objects`), and four typed projections defaulting to empty: `PathParameters`, `Query`, `Headers`, `Body`. The body is carried by reference, never encoded. Validation lives in `field`-backed `init` accessors (design §4). A placeholder name is any non-empty run of characters other than `{` and `}` (Ruby's `\{([^{}]*)\}`), compared ordinally, and a repeated name takes the same value. A literal segment of the template that is a dot-segment (`.` or `..`, with `%2E`/`%2e` read as `.`) is rejected (position K). | Design §3.5, §4; position H; position K. | `OperationDescriptor` | `OperationDescriptorTests`: a parameterless GET setting only `Method` and `PathTemplate` assembles `GET <base>/pets` (the conformance example); each projection lands in its request part; the built request's `Body` is the same instance; template validation (unbalanced brace, empty placeholder, `?`, `#`, a space, a bare `%`, and a literal dot-segment `/../x`, `/%2e%2e/x`, `/.%2E/x`, `/./x`) throws `ArgumentException` naming the template, through the initializer and through `with`. | 5 | ✅ |
| `SEAM-27` | MUST | `BuildRequest(Uri baseAddress)` composes over the base's components, never with `new Uri(base, relative)` (design §3.5). Path values go through the internal `Rfc3986.EncodeComponent` one segment each; a value of exactly `.` or `..` is rejected naming the placeholder (§11 item 35), and so is a lone surrogate (fact 5). A placeholder with no value, or a path parameter naming no placeholder, throws `InvalidOperationException` naming it. After substitution, a rendered segment that is a dot-segment (reachable only by concatenating literal dots with an empty value, as in `/.{a}.` with `a` empty) throws the same type naming the template (position K). The query is `Query.Encode()`. A trailing slash normalises to exactly one separator; an empty template leaves the base path untouched; the base query is kept with its dangling `&` dropped and the operation query appended; a base with any fragment (including an empty `#`, fact 5), a relative base or a non-http(s) base is rejected with `ArgumentException` whose message carries the base through `UrlRedactor` (the 2a ruling that URL errors carry the redacted input). | Design §3.5; §11 item 35. | `OperationDescriptor.BuildRequest` | `OperationBuildRequestTests`, driven by `tests/vectors/seam/operation-compose.json` (base, template, parameters, query → expected `AbsoluteUri`, or the expected error kind): the specification's `host/c?sig=..` + `/pets` case; `a/b` → `a%2Fb`; root and trailing-slash bases; `?`-only and `&`-dangling base queries; a missing placeholder; `..`; a rendered dot-segment from an empty value; the fragment cases; userinfo and port kept. | 5 | ✅ |
| `SEAM-28` | MAY | `OperationDescriptor.OperationId` (`string?`; when set, non-blank). It never reaches the URL, headers or body. **Attaching it to the request's context chain is ⏳ against 4a**, which builds the chain. | Design §3.5 (gap ID). | `OperationDescriptor` | `OperationDescriptorTests.The_operation_id_never_reaches_the_url_headers_or_body`: two descriptors differing only in `OperationId` build requests with equal `Url.AbsoluteUri`, equal headers and the same body instance, and no header value or URL contains the id. | 5 | ✅ for the MUST NOT; ⏳ 4a for the attachment |
| `SEAM-30` | MUST | Core has no `TaskCompletionSource<Response>`, so the completion race does not exist for its `async` methods (design §3.3). The one place core can still orphan a response is `AsAsync` under design §5.3's "cancel without interruption" mode, where the caller stops awaiting (`WaitAsync(token)`) while the worker runs on. So `AsAsync` **checks after return**: a response produced after the call's token is signalled is disposed, and the task completes cancelled (position E, P2b-4). The producer-internal half is phase 1 S9's guard. `BannedSymbols.txt` gains `TaskCompletionSource<T>.SetResult`/`TrySetResult` and every overload of `Task<T>.WaitAsync` (each overload is its own documentation ID: `(CancellationToken)`, `(TimeSpan)`, `(TimeSpan, CancellationToken)`, `(TimeSpan, TimeProvider)`, `(TimeSpan, TimeProvider, CancellationToken)`), so reintroducing either needs a `#pragma` citing this row. The non-generic `TaskCompletionSource` and `Task.WaitAsync` are **not** banned: neither carries a `Response`, so neither can orphan one, and banning them would tax unrelated code in later phases. | Design §3.3, §3.7, §5.3; §11 item 5. | `AsAsync`, `BannedSymbols.txt` | `SyncToAsyncBridgeTests.A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces` (the conformance clause: the delegate starts, the test cancels, the delegate returns a response whose body counts disposals; the task is `Canceled`, the count is 1). `…A_token_cancelled_before_the_send_never_runs_it`. Cite `MalformedContentTypeWireTests` (Security, S9) for the adapter-side guard. | 2, 3 | ✅ |

---

## Argued positions

Each position answers a judgement call the segmentation design hands to 2b, or one this design found. Positions A–D
are the segmentation design's four items; E–K are this design's.

### A. `DelegateHttpClient` and `BuildRequest` against `SEAM-2`'s "never implements"

**Position.** `SEAM-2`'s "exposed as exactly one narrow interface seam that the core depends on but never
implements" is read as: **core never supplies the external concern behind a seam** (the I/O of a transport, the
format of a codec, the declarations of an operation), and never names a concrete supplier. Core may ship **adapters
that re-shape a caller-supplied implementation** of the same seam, provided they perform no I/O and embed no
transport or codec. Under that reading core's four seam-typed artefacts conform:

- `DelegateHttpClient` wraps the caller's function. The function is the implementation; the adapter only gives it the
  interface shape C#'s nominal typing requires (design §3.2: "a C# lambda cannot implement an interface").
- The two bridges wrap the caller's transport.
- `OperationDescriptor.BuildRequest` assembles what generated code declared. The declaration (method, template,
  projections) is the seam's content, and it comes from outside core. Both siblings put the assembly in core
  (`ruby-sdk@5b17395` `gems/dexpace-core/lib/dexpace/operation.rb`, `nodejs-sdk@54aeed4`
  `packages/core/src/seams/operation.ts`).
- 4c's `HttpPipeline` implementing both transport interfaces (`PIPE-26`) is the same shape one layer up: it delegates
  to the transport it was given.

**For.** The requirement's rationale is decoupling: core must not embed or name a concrete capability (`SEAM-1`), so
consumers can swap it. None of the four embeds one, names one, or can be used without a caller-supplied
implementation underneath. Reading "never implements" as "no type in core may declare `: IHttpClient`" would forbid
`PIPE-26`, a MUST, and the bridges `SEAM-18` itself requires ("the provided sync<->async bridges"), which is a
contradiction inside the specification, not a constraint on the port.

**Against, and why it does not outweigh.** The letter says "never implements". A stricter port could ship
`DelegateHttpClient` in `TestSupport` only. But the conformance clause of `SEAM-11` ("a bare send lambda works as a
transport") is a product surface, not a test helper, and 8a's kit drives `DelegateHttpClient` as its second driver
(the roadmap's D3 proposal), so it must ship.

**Made mechanical.** `SeamImplementationArchitectureTests` asserts that every core type implementing a seam interface
is non-public and on a named allow-list, and `Seam1ArchitectureTests` asserts core references no transport or codec
assembly. A new implementation needs an allow-list edit with a reason; 4c's `HttpPipeline` (public, `PIPE-26`) is the
one expected addition, and 4c owns that edit. Recorded as P2b-1, a design §11 candidate.

### B. `SEAM-22`: ✅ by construction, not N/A

**Position: ✅, pinned by a tripwire.** `SEAM-22` protects against decoding with a type argument that was erased to its
bound. On the CLR a generic method's `T` is always the caller's closed type at run time, so the capture that
`SEAM-22` describes is the type argument itself, and "an unresolved type variable" cannot reach the codec through the
generic API. The one door through which it could, a `Type`-taking overload receiving `typeof(List<>)`, does not exist.

- **N/A would be the wrong mark.** N/A means the requirement does not apply to this port, and the roadmap's exit asks
  every `SEAM` N/A to cite §3.1, §3.6 or a §10 topic. No §10 topic covers `SEAM-22`, and none should: the port
  meets the requirement's guarantee (the codec always decodes into the intended closed type), so writing a deviation
  for it would record a departure that does not exist.
- **✅ needs a test that could fail.** Two do. `A_generic_helper_decodes_into_the_closed_type` would fail on a runtime
  that erased generics. `No_seam_member_takes_a_System_Type` fails the day someone adds a `Type` overload without the
  `ContainsGenericParameters` rejection that design §3.4 requires of it.
- **"Exposes both the full generic type and its erased raw class."** Both are one expression away from `T`
  (`typeof(T)`, and `GetGenericTypeDefinition()` where it is generic). There is no capture object to expose them
  from, and inventing one would be a fabricated tier (P11).

Recorded as P2b-2, a reading, for design §11.

### C. The vacuous clauses outside the exit criterion's list

`SEAM-17`'s second sentence, `SEAM-24`'s adapter-module clause and `SEAM-25`'s first sentence are argued in design §1,
§3.3 and §3.7, which the roadmap's exit criterion does not list for N/A citations. So is `SEAM-21`'s no-codec clause.

**Position: none of them is N/A, and none needs to be.** Each row has a built, tested clause, and each vacuous clause
describes an antecedent this port never reaches: a separate async-ecosystem adapter module (none ships, because .NET
has one async ecosystem), an adapter that owns an executor (none does), a format-agnostic deserializer in core (core
has none, by `SEAM-1`). Design §12 already has a category for exactly this, "Vacuous — a requirement whose
antecedent this port never reaches, so the guarantee holds without an implementation", distinct from N/A, and it
already lists `SEAM-8` and `SEAM-10`. The rows are therefore marked ✅ for the built clause, with the vacuous clause
named in the row and in the checklist, the way 2a marks `SEAM-29` ✅ and 🚫 by clause.

The exit criterion governs N/A marks, so it does not bite. The consequence worth recording is that §12's vacuous list
is incomplete for `SEAM`: P2b-3 routes the four clauses to it as a dated correction, so 8a's kit reports each as
vacuous rather than passing (design §9.3).

### D. Where the pipeline's per-call options come from

The as-built runner calls `ExecuteAsync(context.Request, context.CancellationToken)`. After the signature change it
passes **`RequestOptions.Empty`**, by reference. Carrying a caller's `RequestOptions` on the call-scoped context is
4c's (`PIPE-26`, design §5.3), and `HttpPipeline.SendAsync` does not gain an options parameter in 2b.

This is safe because of `SEAM-11`'s own clause: a transport that ignores options behaves identically with and without
them. Today every transport ignores them: `SystemNetHttpClient` reads no member until 8b wires
`RequestOptions.Timeout` (`TRANSPORT-5`), and `MaxRetries` is read by 6a's retry policy through 4c's context, never
by a transport. So `Empty` changes nothing observable. `PipelineRunnerTests.The_transport_receives_RequestOptions_Empty`
pins it, and 4c replaces that test when it threads real options.

### E. The bridges: the scheduler, the check after return, and disposal

**The scheduler: accepted as given, with `LongRunning`.** `SEAM-18` requires a caller-supplied executor with no
default, and forbids a shared pool because blocking calls starve it. `AsAsync` therefore takes a non-null
`TaskScheduler` and has no overload without one. It does **not** reject `TaskScheduler.Default`:

- a check for `Default` is theatre, because a `ConcurrentExclusiveSchedulerPair` over the pool, or any custom
  scheduler that queues to the pool, evades it;
- the bridge always passes `TaskCreationOptions.LongRunning`, and on `TaskScheduler.Default` that runs each call on a
  dedicated thread, not a pool thread (fact 4). So even the obvious argument does not starve the pool; it costs a
  thread per call, which the XML docs state.

A custom scheduler receives `LongRunning` as a hint in `task.CreationOptions` and may ignore it. `DenyChildAttach` is
passed too, as `Task.Run` does.

**The check after return (`SEAM-30`).** Design §5.3 defines "cancelling without interruption" as the caller awaiting
`task.WaitAsync(token)`, which completes cancelled while the worker runs on. That is abandonment, and when the worker
later returns a response, nobody receives it: exactly `SEAM-30`'s orphan. Design §3.3 says no SDK path abandons a
`Task<Response>`, but the SDK's own bridge documents a mode in which the caller does. So the worker, after `Execute`
returns, checks the call's token; if it is signalled, it disposes the response and throws
`OperationCanceledException` on that token, and `StartNew` completes the task `Canceled` (fact 4). With the caller
awaiting `WaitAsync` on the same token, the response is closed exactly once and never surfaces, which is `SEAM-30`'s
conformance clause verbatim. A caller who awaits the task directly sees a cancelled call rather than a response to a
call it cancelled, which is the least surprising outcome. The disposal is a direct `Dispose()` today, so a failure
propagates rather than being swallowed (styleguide 8.4); 3b's `Disposal.DisposeQuietly` replaces it. **The consequence
of propagating** (verified 2026-09-30): a `Dispose` that throws there runs before `ThrowIfCancellationRequested`, so the
task completes **`Faulted`** with the disposal exception, not `Canceled`; and a caller who abandoned the task through
`WaitAsync` never observes it, so it surfaces only as an unobserved task exception. This is accepted until 3b as the
price of not swallowing, and is recorded against ruling 3. Recorded as P2b-4, because it resolves a tension
between §3.3 and §5.3.

**Disposal.** A bridge creates nothing, so its `Dispose`/`DisposeAsync` touch nothing (design §5.3, `XCUT-22`,
styleguide 13.4). The as-built bridges disposed the client they wrapped; that is breaking item 5. A caller who
wrapped a transport they own keeps disposing it themselves.

**`AsBlocking` stays a last resort.** It blocks with `GetAwaiter().GetResult()` under a scoped `RS0030` waiver citing
design §3.3, and documents the `SynchronizationContext` deadlock hazard of design §1 when the wrapped transport does
not use `ConfigureAwait(false)`. It does not hop to the pool to dodge that hazard, because that would reintroduce the
shared-pool dependency `SEAM-18` rules out for the other direction.

### F. `SerdeException` is abstract

Design §3.4 says "an unsealed `SerdeException : SdkException`"; §7.3 says "an abstract `SerdeException`". Abstract is
unsealed, so the two agree, and this design takes the narrower of them:

- `SEAM-23` wants "a base serde failure **with encode/decode subtypes**". An abstract base makes every thrown serde
  failure one of the two (or a subtype of one), so a handler that distinguishes encode from decode never meets a third,
  unclassified kind.
- `SEAM-23`'s "open for codegen/adapters to add more specific subtypes" holds: both subtypes are unsealed, and the base
  can be subclassed too.
- Its constructors are `protected` (`CA1012`); `CA1032` accepts that (fact 6).

The subtypes depart from styleguide 8.6's "seal it" and 6.2's "sealed by default"; `SEAM-23` is the reason, stated in
their XML remarks. No typed context property (the target `Type`) is added: `SERDE-9`–`SERDE-12` are 7a's, and 7a may
add one.

### G. The string profile: UTF-8, with an optional override

**Ruled 2026-09-30 (ruling 4): design §3.4 as written.** `SerializeToString` decodes the seam's buffer as UTF-8, and a
codec whose wire format is not UTF-8 text supplies its own string form by implementing a new optional interface,
`IStringSerde`, which the extension checks for first. The shipped System.Text.Json codec does not implement it, so its
output is the UTF-8 decode.

- **The seam stays UTF-8.** `ISerde`'s buffer primitives are documented as UTF-8 (`Deserialize`'s parameter is named
  `utf8`), and the fresh-bytes profile is `SerializeToUtf8Bytes`. Decoding with a declared charset instead would have
  made that name false for a non-UTF-8 codec and widened the seam's documented contract, which cannot be narrowed later
  without a break.
- **No guessing.** The string profile never reads `DefaultMediaType.Charset`, so it has no edge on 2a's `HTTP-24` fix
  (`MediaType.Charset` throws `NotSupportedException` for `utf-7` until 2a PR 3).
- **The alternatives weighed.** The declared-charset rule (this design's first default) needed no new type but had
  the two costs above. Always-UTF-8 with no interface was the smallest surface, but leaves a non-UTF-8 text codec with
  no correct string form. The lead chose the design's own answer: the override exists now, as an explicit capability a
  codec opts into, and the UTF-8 default covers every codec that ships.

The interface carries only the string profile (`SEAM-20` asks for "produce a fresh string"), not a string decode:
`Deserialize` keeps its UTF-8 buffer, and a string-decoding capability is 7a's to add if `SERDE-*` asks for it.

### H. `OperationDescriptor` carries typed values, not a projection table

The siblings differ. Ruby's `Operation` is a static table mapping input keys to `[:path | :query | :header | :body,
wire_name]`, with the inputs passed to `build_request` as a `Hash`. Node's `OperationDescriptor` is a per-call value
carrying the projected parts themselves (`pathParams`, `query`, `headers`, `body`). Design §3.5 says "a `sealed
record OperationDescriptor` — `Method`, template `string`, and the four projection lists, each defaulting to empty".
This design takes Node's shape:

- The "typed projections" of `SEAM-26` are then 2a's own validated types: a `Query` (RFC 3986 by construction,
  `HTTP-29`), a `Headers` (header validation at assembly time, `HTTP-17`/`HTTP-18`), a `RequestBody`. A string-keyed
  table would re-validate what those types already guarantee, and would move typing from the compiler to a lookup.
- The typing lives where C# can check it: the generated method's signature takes typed arguments and fills the
  descriptor. Formatting a non-string path value (an `int`, a `DateTimeOffset`) with the invariant culture is the
  generator's job (`CA1305`), stated in the XML docs.
- Ruby's construction-time checks that need the static table (a placeholder with no projection, a projection with no
  placeholder) become `BuildRequest`-time checks, which is when `SEAM-27` requires "a missing one is an error"
  anyway. The per-value checks (`.`, `..`, lone surrogates) stay at construction, in `PathParameters`' `init`.

Taking `Query` as the member type makes 2a PR 2 a code edge of PR 5 (see [Prerequisites](#prerequisites)). That is
deliberate: it buys one RFC 3986 renderer instead of two that could drift, which the segmentation design names as the
reason to prefer it.

### I. The option-less convenience, and the wire tests

Design §3.2 makes the option-less overload an **extension method**, not a default interface member, and the
segmentation design expects the four wire `Security` classes to keep compiling "unchanged". Fact 1 confirms the
binding. But none of the `tests/Dexpace.Sdk.Http.SystemNet.Tests` files imports `Dexpace.Sdk.Core.Client`, and
neither does that project's `GlobalUsings.cs`, so the extension is not in scope there, and every
`transport.ExecuteAsync(request, ct)` call would fail with `CS7036`.

**Position:** add `global using Dexpace.Sdk.Core.Client;` to that project's curated `GlobalUsings.cs`, in the SPI PR.
Every test file, the four `Security` classes included, stays byte-identical, which is the property constraint 5 and
the segmentation design ask for. The alternative, a two-parameter `ExecuteAsync` overload on `SystemNetHttpClient`
itself, would give the class two routes to one operation and would drift from the interface's documented
convenience. `AotSmoke/SmokeChecks.cs` already imports the namespace, and its one-argument call binds to the
extension.

The no-defaults rule of fact 2 applies to every three-parameter implementation, `SystemNetHttpClient` included, and
the plan adds a reflection test that no implementation in the repository declares a default on those members.

### J. `DelegateHttpClient`'s factories are `Create` and `CreateBlocking`

Design §3.2 says "core ships `DelegateHttpClient.Create(...)` factories". Two overloads named `Create` are ambiguous
for the most natural failing transport, a throwing lambda (fact 3), and 8a's kit needs exactly that transport. So the
async pivot takes the plain name, `Create(Func<Request, RequestOptions, CancellationToken, Task<Response>>)`, and the
synchronous one is `CreateBlocking(Func<Request, RequestOptions, CancellationToken, Response>)`, matching `AsBlocking`.
Recorded as P2b-6 for a dated correction to §3.2's wording.

### K. Dot-segments in the template, not only in the values

Design §3.5 and §11 item 35 reject a path-parameter **value** of exactly `.` or `..`, because `System.Uri` removes dot
segments, percent-encoded ones included. The same removal applies to the template's **literal** text, which the
`RFC 3986 path` check lets through: verified 2026-09-30 on 10.0.401, `https://h/c/../x`, `https://h/c/%2e%2e/x`,
`https://h/c/%2E%2E/x`, `https://h/c/.%2e/x` and `https://h/c/%2e./x` all become `https://h/x`, and `https://h/c/./x`
becomes `https://h/c/x`. So a template such as `/../admin` escapes the base path, which is exactly the signed-base
prefix `SEAM-27`'s rationale protects (a value that is correctly encoded stays inert: `%252e%252e` survives). Neither
sibling checks the literal.

**Position: reject both routes.**

- **At construction**, `PathTemplate`'s `init` rejects a literal segment (one with no placeholder in it) that is a
  dot-segment once `%2E`/`%2e` is read as `.`: `ArgumentException` naming the template, as the other template rules.
- **At `BuildRequest`**, each rendered segment of the operation path is checked the same way, which closes the one
  remaining route, literal dots concatenated with an empty value (`/.{a}.` with `a` = `""` renders `/..`). The failure
  is the build-time assembly type of ruling 6 (`InvalidOperationException`), naming the template and
  never a value.

A template is generator-authored, so this is defence in depth rather than a fix for caller input; it costs one scan of
a string that is already being parsed. Empty values stay legal: `SEAM-27` asks for a *supplied* value and Ruby treats
only `nil` as missing, so rejecting `""` is not done here. Recorded as P2b-7, extending §11 item 35 by dated
correction.

---

## N/A candidates, decided

| Candidate | Decision | Cites | Inside the exit criterion's list? |
|---|---|---|---|
| `SEAM-3`, `SEAM-4` | N/A | §3.1; §10 `byte-stream-provider-retired` | yes |
| `SEAM-5`, `SEAM-6` | N/A (DI-less half) and ⏳ phase 9 (DI half) | §3.6; §10 `no-provider-registry` | yes |
| `SEAM-7`, `SEAM-9` | N/A | §3.6; §10 `no-provider-registry` | yes |
| `SEAM-8`, `SEAM-10` | N/A, vacuous | §3.6; §12 | yes (§3.6) |
| `SEAM-22` | **not N/A**: ✅ by construction (position B) | §3.4; P2b-2 | not needed |
| `SEAM-17`'s second sentence, `SEAM-24`'s adapter clause, `SEAM-25`'s first sentence, `SEAM-21`'s no-codec clause | **not N/A**: vacuous clauses of ✅ rows (position C) | §1, §3.3, §3.4, §3.7; P2b-3 | not needed |
| `SEAM-14`'s clause (3), `SEAM-18`'s interrupt clause | mapped to the token, inside ✅ rows | §3.3; §10 `cooperative-cancellation` | yes |

---

## Type shapes

The public surfaces 2b leaves behind. Every member carries `///` XML docs, and every breaking member says
**Breaking** in them, stating what it was. Implementation-only members are omitted, except where a row depends on an
`internal` one. `RequestOptions`, `Query` and `Headers` are 2a's shapes, used as that design fixes them.

### The transport SPI

```csharp
namespace Dexpace.Sdk.Core.Client;

public interface IHttpClient : IDisposable
{
    // Breaking: was Execute(Request). No defaults (fact 2). Remarks state SEAM-11 (one request, one response, body
    // not pre-buffered, options optional and ignorable), SEAM-12 (concurrency), SEAM-13 (the token), SEAM-15 (the
    // post-dispose mode is implementation-defined and each SDK type documents its own) and SEAM-16's non-null rule.
    Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
}

public interface IAsyncHttpClient : IAsyncDisposable
{
    // Breaking: was ExecuteAsync(Request, CancellationToken = default). No defaults (fact 2).
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}
```

Both interfaces document that an implementation rejects a `null` request or options with `ArgumentNullException`,
delivered through the returned task on the async seam (design §3.3, `ASYNC-2`).

### `HttpClientExtensions`: the option-less calls and the two bridges

```csharp
namespace Dexpace.Sdk.Core.Client;

public static class HttpClientExtensions
{
    // SEAM-11: passes RequestOptions.Empty.
    public static Response Execute(this IHttpClient client, Request request, CancellationToken cancellationToken = default);
    public static Task<Response> ExecuteAsync(this IAsyncHttpClient client, Request request, CancellationToken cancellationToken = default);

    // Breaking: was AsAsync(this IHttpClient). SEAM-18: the caller's scheduler, no default; LongRunning | DenyChildAttach;
    // options and token threaded into Execute; the check after return (position E); dispose touches nothing.
    public static IAsyncHttpClient AsAsync(this IHttpClient client, TaskScheduler scheduler);

    // Signature unchanged. Breaking behaviour: no longer disposes the wrapped client. Blocks with GetAwaiter().GetResult()
    // (unwraps); options and token threaded into ExecuteAsync; a null task or null result from the wrapped client is
    // InvalidOperationException (SEAM-16); documented as a last resort (SynchronizationContext).
    public static IHttpClient AsBlocking(this IAsyncHttpClient client);
}
```

The two bridge adapters stay `private sealed` nested classes. The synchronous adapter's worker is, in outline:

```csharp
var response = inner.Execute(request, options, cancellationToken)
    ?? throw new InvalidOperationException("The wrapped IHttpClient returned null (SEAM-16).");
if (cancellationToken.IsCancellationRequested)
{
    response.Dispose();                                   // SEAM-30; DisposeQuietly once 3b lands
    cancellationToken.ThrowIfCancellationRequested();     // StartNew completes the task Canceled (fact 4)
}
return response;
```

### `DelegateHttpClient`

```csharp
namespace Dexpace.Sdk.Core.Client;

public static class DelegateHttpClient
{
    // SEAM-11's "a bare send lambda works as a transport". Every failure, argument checks and a synchronous throw from
    // the delegate included, is delivered through the task (ASYNC-2). A null result or a null task faults with
    // InvalidOperationException (SEAM-16). Dispose is a no-op; the transport keeps working after it (SEAM-15).
    public static IAsyncHttpClient Create(Func<Request, RequestOptions, CancellationToken, Task<Response>> send);

    // The blocking form. Same null rule; Dispose is a no-op.
    public static IHttpClient CreateBlocking(Func<Request, RequestOptions, CancellationToken, Response> send);
}
```

A static factory class returning the interfaces, with two private sealed adapters, rather than one public class
implementing both interfaces: a delegate is either blocking or asynchronous, and an object implementing both would
need one of them to be a bridge in disguise.

### `ISerde`, the profiles, and the serde failure hierarchy

```csharp
namespace Dexpace.Sdk.Core.Serialization;

public interface ISerde                                    // unchanged shape; XML remarks gain SEAM-19–SEAM-22's contract
{
    MediaType DefaultMediaType { get; }
    ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default);
    ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default);
    void Serialize<T>(IBufferWriter<byte> destination, T value);
    T? Deserialize<T>(ReadOnlySpan<byte> utf8);
}

public interface IStringSerde : ISerde                     // new, optional (position G, design §3.4): a codec whose wire
{                                                          // format is not UTF-8 text supplies its own string form
    string SerializeToString<T>(T value);
}

public static class SerdeExtensions                        // new; SEAM-20's derived profiles, written once
{
    public static byte[] SerializeToUtf8Bytes<T>(this ISerde serde, T value);          // a fresh array per call
    public static string SerializeToString<T>(this ISerde serde, T value);              // IStringSerde override, else UTF-8 (position G)
    public static int Serialize<T>(this ISerde serde, Span<byte> destination, T value); // count written; ArgumentOutOfRangeException
                                                                                       // when it does not fit, destination untouched
}

namespace Dexpace.Sdk.Core.Errors;

public abstract class SerdeException : SdkException        // new (SEAM-23; position F)
{
    protected SerdeException();
    protected SerdeException(string message);
    protected SerdeException(string message, Exception? innerException);
}

public class SerializationException : SerdeException        // Breaking: was sealed, deriving from SdkException
{
    public SerializationException();
    public SerializationException(string message);
    public SerializationException(string message, Exception? innerException);
}

public class DeserializationException : SerdeException      // Breaking: was sealed, deriving from SdkException
{
    public DeserializationException();
    public DeserializationException(string message);
    public DeserializationException(string message, Exception? innerException);
}
```

The fixed-buffer profile serializes into a scratch `ArrayBufferWriter<byte>` first and copies only when the payload
fits, which is what leaves the destination untouched on overflow. The extension named `Serialize` does not collide
with the interface's `Serialize(IBufferWriter<byte>, T)`: a `Span<byte>` argument is not convertible to
`IBufferWriter<byte>`, so the instance member is never applicable to it.

### `OperationDescriptor`

```csharp
namespace Dexpace.Sdk.Core.Operations;                     // new namespace and folder

public sealed record OperationDescriptor
{
    public required Method Method { get; init; }           // SEAM-26: required (CS9035 when omitted); null → ArgumentNullException
    public required string PathTemplate { get; init; }     // "" allowed (the base path is left untouched); braces balanced,
                                                            // names non-empty and brace-free, compared ordinally; outside
                                                            // placeholders an RFC 3986 path (pchar, "/", "%HH"), so "?",
                                                            // "#", " " and a bare "%" throw, and so does a literal
                                                            // dot-segment ("." or "..", %2E read as ".", position K)
    public ImmutableDictionary<string, string> PathParameters { get; init; }   // default empty; ordinal keys; a value of "."
                                                            // or "..", or holding a lone surrogate, throws naming the key
    public Query Query { get; init; }                      // default Query.Empty; rendered by Query.Encode()
    public Headers Headers { get; init; }                  // default Headers.Empty; carried onto the request as is
    public RequestBody? Body { get; init; }                // carried by reference, never encoded
    public string? OperationId { get; init; }              // SEAM-28; null or non-blank; never reaches URL, headers or body
    public OperationDescriptor WithPathParameter(string name, string value);
    public Request BuildRequest(Uri baseAddress);          // SEAM-27; a rendered dot-segment is rejected (position K)
    public Request BuildRequest(DexpaceClientOptions options);  // reads options.BaseAddress; ArgumentException when it is null
    public bool Equals(OperationDescriptor? other);        // PathParameters by content; Query, Headers, Body by their own Equals
    public override int GetHashCode();
    public override string ToString();                     // "GET /pets/{id}": method and template, never values
}
```

`Equals` is explicit because a record's generated equality compares `ImmutableDictionary` by reference, the same
reason 2a gives for `RequestConditions`. It compares `PathParameters` by content and delegates the rest to each member's
own `Equals`, so it is value equality over `Query` from the start (2a PR 2 is PR 5's gate) and over `Headers` and `Body`
only once 2a PR 5 and PR 6 give those types their equality; until then those two compare by reference (see
[Prerequisites](#prerequisites)). No 2b change is needed when they land. `ToString` is overridden because the generated one would print path values,
the query and the body. The placeholder list is computed once in `PathTemplate`'s `init` and kept `internal`.

### `DexpaceClientOptions.BaseAddress`

```csharp
namespace Dexpace.Sdk.Core.Configuration;

public sealed class DexpaceClientOptions
{
    public Uri? BaseAddress { get; set; }   // shape unchanged; the "Not yet read by anything" remark is replaced by: read by
                                            // OperationDescriptor.BuildRequest(DexpaceClientOptions); must be an absolute
                                            // http(s) URI with no fragment, checked at BuildRequest (5a may check at
                                            // construction when it makes the options a record, CFG-8/CFG-9)
}
```

### The fakes and the pipeline runner

```csharp
namespace Dexpace.Sdk.TestSupport.Transports;

public sealed record RecordedCall(Request Request, RequestOptions Options, CancellationToken CancellationToken);  // new

public sealed class RecordingTransport(Func<Request, Response>? respond = null) : IAsyncHttpClient
{
    public IReadOnlyList<Request> Requests { get; }        // unchanged
    public IReadOnlyList<RecordedCall> Calls { get; }      // new
    public RecordedCall? LastCall { get; }                 // new
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
    // LastRequest, CallCount, IsDisposed, DisposeAsync unchanged
}

// RecordingSyncTransport: the same additions, with Execute(Request, RequestOptions, CancellationToken).
// ScriptedTransport: the same additions, with ExecuteAsync(Request, RequestOptions, CancellationToken).

namespace Dexpace.Sdk.TestSupport.Threading;               // new

// Starts each queued task on its own thread, counts QueueTask calls and records whether Dispose ran (SEAM-18, SEAM-24,
// SEAM-25). Never inlines.
public sealed class RecordingTaskScheduler : TaskScheduler, IDisposable;

namespace Dexpace.Sdk.TestSupport.Transports;

// A ResponseBody that counts its Dispose calls (SEAM-30).
public sealed class DisposalCountingBody : ResponseBody;

namespace Dexpace.Sdk.TestSupport.Serialization;

// An ISerde that declares the media type it is given and writes a fixed payload (or throws a given exception) from
// both encode primitives; the decode primitives return default. ScriptedSerde writes nothing and declares a fixed
// application/json, so it cannot drive SEAM-19's application/xml pin or any SEAM-20 profile test (PR 1).
public sealed class BytesSerde(MediaType mediaType, byte[] payload, Exception? failure = null) : ISerde;
```

`RequestLog` becomes a log of `RecordedCall`s; `Requests` projects it, so no existing test changes. The responder
stays `Func<Request, Response>`: tests that need to see the options or block on the token use `DelegateHttpClient`.
`OperationPolicyTests.HangingTransport` and `AotSmoke`'s `EchoTransport` get the new signature.

`PipelineRunner` keeps its shape; `RunAsync` calls
`_transport.ExecuteAsync(context.Request, RequestOptions.Empty, context.CancellationToken)` and throws
`PipelineAbortedException` when the result is `null` (`SEAM-16`).

---

## Migration plan from the as-built code

The migration follows [the landing order](#landing-order). Counts are at `bf8f4ae` and are recounted by the plan.

| Change | Sites | Mechanical rewrite |
|---|---|---|
| `IAsyncHttpClient.ExecuteAsync` gains `RequestOptions` and loses its token default | Implementations: `SystemNetHttpClient`, the private `SyncToAsyncAdapter`, `TestSupport`'s `RecordingTransport` and `ScriptedTransport`, `Core.Tests`' `OperationPolicyTests.HangingTransport` (a fifth test-side implementation the segmentation design does not list) and `AotSmoke`'s `EchoTransport`. Callers: `PipelineRunner.cs:55`; 22 calls in `tests/Dexpace.Sdk.Http.SystemNet.Tests` (13 in the four `Security` classes, 3 in `SystemNetHttpClientTests`, 6 in `LoopbackServerTests`); one in `AotSmoke/SmokeChecks.cs:82` | Implementations add the parameter, no defaults (fact 2). The runner passes `RequestOptions.Empty` (position D). The test callers are **not edited**: they bind to the new extension once `SystemNet.Tests/GlobalUsings.cs` gains `global using Dexpace.Sdk.Core.Client;` (position I). `SmokeChecks.cs` already imports the namespace |
| `IHttpClient.Execute` gains `RequestOptions` and `CancellationToken` | Implementations: `SystemNetHttpClient`, the private `AsyncToSyncAdapter`, `RecordingSyncTransport`. Callers: none outside the bridge | `SystemNetHttpClient.Execute` keeps its sync-over-async body and `RS0030`/`CA2000` waiver until 8b, now passing options and token through |
| `AsAsync` gains `TaskScheduler`; both bridges stop disposing | `HttpClientExtensions.cs` only. **No caller exists** in `src/` or `tests/`, and no test covers either bridge today | Rewritten whole (the lead's decision 1); `SEAM-18`'s tests are new, and there is no existing test to keep green |
| Serde exceptions re-parented and unsealed | `Errors/SerializationExceptions.cs`. Throw sites: `SystemTextJsonSerde.cs` (six). Catch sites by type: tests only | Source-compatible at every site: the constructors are unchanged and `SdkException` still catches both |
| `BaseAddress` becomes read | `DexpaceClientOptions.cs` remark | Remark replaced; `DexpaceClientOptionsTests` gains `BaseAddress_is_read_by_BuildRequest` |
| New `Operations/` folder | none | `CLAUDE.md`'s repository layout tree gains `Operations/` in PR 5, and `Client/`'s line gains `DelegateHttpClient` in PR 4 |
| Prose and samples that state the old SPI | `docs/architecture.md:45-47` (the Transport SPI paragraph names `ExecuteAsync(Request, CancellationToken)` and `Execute(Request)`); `src/Dexpace.Sdk.Http.SystemNet/README.md:33`, packed into the NuGet package, whose sample calls `transport.ExecuteAsync(Request.Get(…))` without `using Dexpace.Sdk.Core.Client;` and so stops compiling (`CS7036`) once only the three-parameter member is on the class; `src/Dexpace.Sdk.Core/README.md:15`'s `Client` row | PR 3 rewrites the architecture paragraph (new signatures, the option-less extensions, `AsAsync(TaskScheduler)`) and adds the `using` line and one sentence on the extension to the README sample; PR 4 adds `DelegateHttpClient` to the Core README's `Client` row |
| `BannedSymbols.txt` gains `TaskCompletionSource<T>.SetResult`/`TrySetResult` and the five `Task<T>.WaitAsync` overloads | none today (`src/` has no use; `AccessTokenCache`'s `WaitAsync` is `SemaphoreSlim`'s, a different symbol) | The plan verifies each documentation ID fires with a throwaway probe |

**`Security` classes kept green** (constraint 5), with **no file edited**:

- `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `RedirectWireTests` and `MalformedContentTypeWireTests`
  compile through the extension (position I). A 2b change that forces an edit to one of them is a signal to re-read
  design §3.2, as the segmentation design says.
- The core `Security` classes that drive `ScriptedTransport` or `RecordingTransport` construct them as before; only
  the fakes' `ExecuteAsync` signature changes, inside `TestSupport`.

The 2b checklist lists each `Security` class with "unedited".

---

## Breaking changes

Each item is marked **Breaking** in the XML docs of the member it changes and appears as a `CHANGELOG.md`
`[Unreleased]` line in the PR that makes it (constraint 8).

| # | Change | Kind | Evidence | PR |
|---|---|---|---|---|
| 1 | `SerializationException` and `DeserializationException` are unsealed and derive from the new abstract `SerdeException` (was: sealed, deriving from `SdkException`) | binary | `PublicAPI.Unshipped.txt` records neither sealing nor base types, so its diff shows only `SerdeException`'s new lines; the evidence is `SerdeExceptionHierarchyTests` and the `CHANGELOG.md` line | 1 |
| 2 | `IHttpClient.Execute(Request)` becomes `Execute(Request, RequestOptions, CancellationToken)` | binary and source (every implementer) | `RS0016`/`RS0017` diff in core; constraint 8's own named example | 3 |
| 3 | `IAsyncHttpClient.ExecuteAsync(Request, CancellationToken = default)` becomes `ExecuteAsync(Request, RequestOptions, CancellationToken)` | binary and source for implementers; source-compatible for callers that import `Dexpace.Sdk.Core.Client` (the extension) | `RS0016`/`RS0017` diff in core | 3 |
| 4 | `SystemNetHttpClient.Execute` and `ExecuteAsync` take the same three parameters | binary; source-compatible for callers that import `Dexpace.Sdk.Core.Client` | `RS0016`/`RS0017` diff in `SystemNet` | 3 |
| 5 | `AsAsync(this IHttpClient)` becomes `AsAsync(this IHttpClient, TaskScheduler)`; neither bridge disposes what it wraps any more | binary, source and behaviour | `RS0016`/`RS0017` diff; bridge tests | 3 |
| 6 | `AsAsync`: a response produced after the call's token is signalled is disposed and the call completes cancelled | behaviour | `SyncToAsyncBridgeTests` | 3 |
| 7 | A transport returning `null` now fails at the pipeline runner with `PipelineAbortedException`, before any policy sees a `null` response (was: policies saw `null`, and `HttpPipeline` threw the same type at the end) | behaviour | `PipelineRunnerTests` | 3 |

Additive only, with no **Breaking** marker: `SerdeException`, `SerdeExtensions`, the option-less `Execute`/
`ExecuteAsync` extensions, `DelegateHttpClient`, `OperationDescriptor`, `BaseAddress` being read, and the
`BannedSymbols.txt` entries (a build-time rule for `src/`, not public surface).

---

## Landing order

Each step is one pull request carrying its code **and** its tests together, as 2a's are: splitting them would leave a
red commit per breaking change (roadmap step 5's one-PR allowance). Each PR carries its `PublicAPI.Unshipped.txt`
diff (core, and `SystemNet` where touched) and its `CHANGELOG.md` line.

| PR | Content | Rows | Gate | Notes |
|---|---|---|---|---|
| **1** | Serde seam: `SerdeException`, the unsealed and re-parented subtypes, `SerdeExtensions`, `IStringSerde`, `SerdeSeamArchitectureTests`, the STJ adapter pins; `TestSupport`'s `BytesSerde` | `SEAM-19`–`SEAM-23` | **free now** | No edge into 2a (segmentation "What 2b is gated on"). Breaking 1 |
| **2** | Seam-surface guards: `Seam1ArchitectureTests`, `SeamImplementationArchitectureTests` (the two bridge adapters on its allow-list), the `SEAM-5` null-parameter pins, `SystemNetHttpClientOwnershipTests`, the `BannedSymbols.txt` entries | `SEAM-1`, `SEAM-2`, `SEAM-5`, `SEAM-14` (ownership), `SEAM-30` (the bans) | **free now** | Tests and a build rule only; no public surface |
| **3** | **The SPI change, as one change** (segmentation constraint): both interfaces with their contract remarks; `HttpClientExtensions` rewritten whole (option-less calls, both bridges); `PipelineRunner` (`Empty`, the null guard); `SystemNetHttpClient`'s signatures; `SystemNet.Tests/GlobalUsings.cs`; `TestSupport`'s fakes, `RecordedCall`, `RecordingTaskScheduler`, `DisposalCountingBody`; `HangingTransport`; `AotSmoke`'s `EchoTransport`; `docs/architecture.md`'s SPI paragraph and the `SystemNet` README sample | `SEAM-11`–`SEAM-18`, `SEAM-24`, `SEAM-25`, `SEAM-30` (the bridge) | **2a PR 1** (`RequestOptions`) | Breaking 2–7. Sequenced against 2a PR 7 (below) |
| **4** | `DelegateHttpClient`; its allow-list entries; the `SEAM-11` lambda, `SEAM-15` mode, `SEAM-16` null and `SEAM-12` concurrency tests that use it; `CLAUDE.md`'s `Client/` line and the Core README's `Client` row | `SEAM-2` (allow-list), `SEAM-11`, `SEAM-12`, `SEAM-15`, `SEAM-16` | PR 3 | Additive. Split from PR 3 to keep the one change that cannot be split as small as it can be; PR 3's bridge tests use test-local delegates until PR 4 lands, and PR 4 moves them onto `DelegateHttpClient` |
| **5** | `OperationDescriptor`, `BuildRequest`, the `BaseAddress` read; `tests/vectors/seam/operation-compose.json`; `CLAUDE.md`'s `Operations/` line | `SEAM-26`–`SEAM-28` | **2a PR 2** (`Query`, `Rfc3986`, the vector loader), by this design's choice (position H) | Additive. Independent of PRs 3 and 4, so it may be reviewed in parallel with them |
| **6** | Close-out: the NativeAOT smoke extended over `DelegateHttpClient`, both bridges, the serde profiles and `BuildRequest`; the user page `docs/sdk-documentation/seams.md`; the 2b checklist (including the N/A and vacuous rows `SEAM-3`–`SEAM-10`); the ledger's dated corrections to design §3.2, §3.4, §11 (including item 35, P2b-7) and §12; the roadmap status note | all 29 (closing) | 1–5 | The docs close the phase (roadmap step 7) |

**Free now, with no edge into 2a:** PRs 1 and 2. They may open, review and merge before 2a's PR 1 does.

**Gated:** PR 3 (and PR 4 behind it) on 2a PR 1; PR 5 on 2a PR 2. Since 2a lands `RequestOptions` first and `Query`
second, both gates open early in 2a's stack.

**Interleaving with 2a's PR 7 (`Response`).** 2b's PR 3 and 2a's PR 7 edit the same files: `SystemNetHttpClient.cs`
(2b the `Execute`/`ExecuteAsync` signatures, 2a `ToResponse` and its caller), the three `TestSupport` fakes (2b their
signatures, 2a their `new Response(…)` bodies) and `AotSmoke/SmokeChecks.cs` (2b `EchoTransport`'s signature, 2a its
`new Response(…)`). Neither is a gate for the other. The rule:

1. If 2a's task 7.1 (the behaviour-free `TestResponses.Create` chore) has merged, 2b's PR 3 builds on it, so its fake
   edits are signature lines only.
2. Otherwise, whichever of 2b PR 3 and 2a PR 7 is ready first lands first, and the other rebases.
3. Whoever rebases re-runs the whole of the verification gate and re-derives its change on the new base, rather than
   resolving conflict hunks textually, as 2a's plan says for its side. 2a's task 7.5 then threads the request into
   `ToResponse` from the new three-parameter `ExecuteAsync`, whose body it shares.

---

## Tests, vectors and ports

- **Vectors extracted** to `tests/vectors/seam/operation-compose.json` (`SEAM-27`), loaded through 2a's vector
  loader, with a top-level `"source"` field naming the sibling path and sha, as 2a's files do. The table is upstreamable.
- **Ported, with a header comment citing path and sha:**
  - from `ruby-sdk@5b17395`, `gems/dexpace-core/test/dexpace/`: `operation_build_request_test.rb` and
    `operation_test.rb` (`SEAM-26`–`SEAM-28`; the composition cases become the vector file);
    `bridge/sync_over_test.rb` and `bridge/async_over_test.rb` (`SEAM-18`, `SEAM-30`, including "closes the orphaned
    response when cancellation wins the completion race" and "a token cancelled before the block runs never reaches
    the transport at all"); `transport_test.rb`'s "an options-ignoring transport behaves identically with and without
    options" and "a conforming transport survives concurrent calls with no cross-talk"; `closeable_test.rb`, for
    ownership only (the latch is 3b's); `serde_test.rb` and `serde/{error_test,serialization_error_test,deserialization_error_test}.rb`
    (`SEAM-20`, `SEAM-23`);
  - from `nodejs-sdk@54aeed4`: `packages/core/src/seams/{transport,serde,operation}.test.ts` and
    `packages/core/src/serde/errors.test.ts`.
- **Not ported:** Ruby's registry cases (`registry*_test.rb`, and the install/swap/register cases of
  `transport_test.rb`, `async_transport_test.rb` and `seam_surface_test.rb`), whose discovery apparatus is retired
  here (§10 `no-provider-registry`); Ruby's `*_bare_require_test.rb`; Node's `hasOwn` prototype cases, a host fact.
- **Categories:** `Unit` throughout, including `SystemNetHttpClientOwnershipTests` (it uses an in-process handler, no
  socket). 2b adds no `Security` class, because it fixes no phase-1 defect.
- **NativeAOT:** PR 6 extends `AotSmoke` so the scheduler bridge, `DelegateHttpClient`, the profiles over the
  source-generated context and `BuildRequest` run inside the published binary.

---

## Coupling with later phases

Each item is something 2b fixes now that a later phase builds on; changing it later is a cross-phase change.

- **3b** builds the dispose latches (`SEAM-14`'s idempotence, ⏳ here) and `Disposal.DisposeQuietly`, which replaces
  `AsAsync`'s direct `Dispose()` of a response produced after cancellation (position E).
- **4a** attaches `OperationDescriptor.OperationId` to the request's context chain (`SEAM-28`, ⏳ here).
- **4c** makes `HttpPipeline` implement both interfaces (`PIPE-26`): it adds `HttpPipeline` to
  `SeamImplementationArchitectureTests`' allow-list, citing P2b-1; it carries a caller's `RequestOptions` on the
  call-scoped context and replaces `PipelineRunnerTests.The_transport_receives_RequestOptions_Empty` (position D); its
  `PIPE-33`/`PIPE-34` rows cite 2b's bridge tests (decision 1); its own synchronous path is the second strand of
  coupling obligation 5.
- **5a** turns `DexpaceClientOptions` into records (`CFG-8`, `CFG-9`). `BuildRequest(DexpaceClientOptions)` reads one
  property, which survives that change; 5a may move `BaseAddress`'s validation to construction.
- **6a** reads `RequestOptions.MaxRetries` through 4c's context, never through the SPI.
- **7a** builds on `SerdeException`, the unsealed subtypes and the profiles: `SERDE-4` cites the fixed-buffer
  profile, `SERDE-9`–`SERDE-12` cite `SerdeExceptionHierarchyTests` (decision 2). 7a also owns `SERDE-26`'s private
  copy of the caller's `JsonSerializerOptions` and `SERDE-13`'s top-level `null` check, which 2b does not touch.
- **8a** drives the kit against `DelegateHttpClient` as its second driver (the roadmap's D3 proposal, not yet ruled),
  so `Create`/`CreateBlocking`'s shape is fixed here. The kit owns the per-transport proofs 2b defers: `SEAM-11`'s
  no-pre-buffering, `SEAM-12`'s concurrency and `SEAM-13`'s async abort.
- **8b** builds the real synchronous `Execute` over `HttpClient.Send` (`SEAM-11`, `SEAM-13`), the
  `ObjectDisposedException` latch (`SEAM-15`), and `RequestOptions.Timeout` (`TRANSPORT-5`); until then
  `SystemNetHttpClient` ignores its options, which `SEAM-11` permits.
- **9** flips `SEAM-5`/`SEAM-6`'s DI half and cites design §3.6 for `SEAM-7`'s singleton reading.
- **2a's PR 4** makes `Method` a reference type; if 2b's PR 5 precedes it, 2b adds `OperationDescriptor`'s `Method` null
  check afterwards (see [Prerequisites](#prerequisites)), so 2a's PR 4 owes nothing to 2b.
- **2a's PR 9**, if it lands after 2b's PR 5, lists `OperationDescriptor` in `ModelImmutabilityArchitectureTests`;
  otherwise 2b's PR 5 adds it there.

---

## Rulings (2026-09-30)

The lead weighed the six judgement calls this design left open. Five defaults stand; ruling 4 takes design §3.4 as
written.

1. **`SerdeException` is abstract** (position F), with `protected` constructors. Abstract is also the reversible
   choice: making it concrete later is additive.
2. **`AsAsync` accepts `TaskScheduler.Default`** (position E), running each call on a dedicated thread through
   `LongRunning`, documented. Rejecting it by reference is evaded by any pool-backed custom scheduler.
3. **`AsAsync`'s check after return is adopted** (position E, P2b-4), so design §5.3's `WaitAsync` mode cannot orphan a
   response (`SEAM-30`'s conformance clause). Accepted consequence until 3b: a `Dispose` that throws in the check
   faults the task with that exception instead of cancelling it, unobserved under `WaitAsync`.
4. **The string profile follows design §3.4** (position G): UTF-8 by default, overridden by a codec implementing the
   optional `IStringSerde`. The declared-charset rule is withdrawn (P2b-5), and with it the edge on 2a PR 3.
5. **`OperationDescriptor` is Node's typed-value descriptor** (position H), so one RFC 3986 renderer serves both
   `Query` and the operation query, at the cost of 2a PR 2 as a code gate for PR 5.
6. **`InvalidOperationException`** is the build-time assembly failure: a placeholder with no value, a path parameter
   naming no placeholder, and a rendered dot-segment (position K). A missing placeholder is a generator bug that no
   caller catches distinctly, so styleguide 8.6 asks for no custom type.

---

## Deviation Ledger

| ID | Decision | Touches | Kind | Argued in | Route |
|---|---|---|---|---|---|
| P2b-1 | `SEAM-2`'s "never implements" is read as "never supplies the external concern": core ships adapters over a seam (`DelegateHttpClient`, the two bridges, `OperationDescriptor.BuildRequest`, and 4c's `HttpPipeline`) that perform no I/O and embed no transport or codec. Pinned by an allow-list architecture test | `SEAM-2`, `SEAM-11`, `SEAM-18`, `PIPE-26` | a reading: the letter would forbid the bridges `SEAM-18` requires | [Position A](#a-delegatehttpclient-and-buildrequest-against-seam-2s-never-implements) | Design §11 numbered item, dated correction (PR 6) |
| P2b-2 | `SEAM-22`'s "full generic type capture" is the reified type argument itself; with no `Type`-taking decode overload, an unresolved type variable cannot reach a codec. Marked ✅, pinned by a tripwire, not N/A | `SEAM-22` | a reading | [Position B](#b-seam-22--by-construction-not-na) | Design §11 numbered item, dated correction (PR 6) |
| P2b-3 | `SEAM-17`'s second sentence, `SEAM-24`'s adapter-module clause, `SEAM-25`'s first sentence and `SEAM-21`'s no-codec clause are vacuous clauses of ✅ rows, not N/A | `SEAM-17`, `SEAM-21`, `SEAM-24`, `SEAM-25` | classification: the antecedent is never reached | [Position C](#c-the-vacuous-clauses-outside-the-exit-criterions-list) | Design §12's vacuous list, dated correction (PR 6) |
| P2b-4 | The `AsAsync` bridge disposes a response produced after the call's token is signalled and completes cancelled, so design §5.3's "cancel without interruption" (`WaitAsync`) cannot orphan it | `SEAM-30`, `SEAM-18`, `PIPE-33` | mechanism: resolves a tension between design §3.3 ("no SDK path abandons a `Task<Response>`") and §5.3 (which documents a caller-side abandonment mode) | [Position E](#e-the-bridges-the-scheduler-the-check-after-return-and-disposal) | Design §11 numbered item, dated correction (PR 6) |
| P2b-5 | *Withdrawn by ruling 4 (2026-09-30).* It had the string profile decode with the codec's declared charset instead of design §3.4's optional override interface; the lead ruled for §3.4 as written, so there is no deviation | `SEAM-20`, `SERDE-4` | — | [Position G](#g-the-string-profile-utf-8-with-an-optional-override) | None: §3.4 stands; PR 6 only names the interface (`IStringSerde`) in its dated correction |
| P2b-6 | `DelegateHttpClient`'s factories are `Create` (async) and `CreateBlocking` (sync), not two `Create` overloads, because a throwing lambda is ambiguous between them (`CS0121`, verified) | `SEAM-11` | naming, design-level | [Position J](#j-delegatehttpclients-factories-are-create-and-createblocking) | Dated correction to design §3.2's wording (PR 6) |
| P2b-7 | A dot-segment is rejected in the template's literal text at construction and in each rendered segment at `BuildRequest`, not only as a whole path-parameter value | `SEAM-27` | mechanism: extends §11 item 35's resolution to the other routes by which `System.Uri`'s dot-segment removal escapes the base path | [Position K](#k-dot-segments-in-the-template-not-only-in-the-values) | Design §11 item 35, dated correction (PR 6) |

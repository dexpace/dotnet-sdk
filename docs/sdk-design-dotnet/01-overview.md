## 1. Overview

The thesis — "an HTTP-client toolkit, not an HTTP client" — needs restating rather than repeating for .NET, because
.NET's default reflex is the opposite of the reference audience's, and for a different reason than Ruby's. On the
JVM, teams compose a toolkit because no single dominant client exists. On .NET one does, and it ships with the
runtime: `System.Net.Http.HttpClient` over `SocketsHttpHandler`, with `IHttpClientFactory` for lifetime management
and `Microsoft.Extensions.Http.Resilience` (Polly v8) for retries, hedging and circuit breaking. Around it sit
complete, opinionated client layers — Refit, RestSharp, Flurl — and two generated-client runtimes, Microsoft's Kiota
abstractions and `Azure.Core`, the latter being the closest prior art to this SDK (its `HttpPipelinePolicy`/
`HttpPipeline` shape is the one the as-built pipeline deliberately follows). A .NET engineer's default move when they
need to call an HTTP API is `new HttpClient()` or `services.AddHttpClient<T>()`, not assembling a pipeline from parts.

This SDK does not compete with `HttpClient` on "easiest way to fetch a JSON endpoint", and it does not replace
`HttpClient` at all: it sits *above* it. It targets a narrower audience: **authors of generated or hand-written
service-client SDKs** — an internal platform team publishing a client for a company's own API surface, or an
OpenAPI-codegen backend targeting C# — who need the correctness-sensitive plumbing solved exactly once:
idempotency-aware retry that never double-sends a one-shot body (**RETRY-5**, **XCUT-10**), redirects that never leak
a bearer token or a cookie cross-origin (**REDIR-7**–**REDIR-13**, **XCUT-17**), RFC 7235 challenge parsing and RFC
7616 Digest (**AUTH-12**–**AUTH-22**), cursor/page-number/Link-header pagination (**PAGE-16**–**PAGE-24**), WHATWG SSE
parsing (**SSE-1**–**SSE-19**), and PATCH's absent/null/present three-state semantics (**SERDE-14**–**SERDE-20**) — so
that no codegen backend re-solves any of it. The secondary audience is application teams who want those guarantees
while keeping their own `HttpClient` configuration (proxy, mTLS, HTTP/2 and HTTP/3, corporate `DelegatingHandler`s)
and their own codec. The connection-layer split, a platform decision taken before this document (Porting Method)
and the one PR #6's pipeline was built on, is the division of labour this document keeps: *SDK-domain* concerns (auth,
idempotency keys, typed-error- and
`Retry-After`-aware retry, redirect semantics, SDK spans and metrics) live in the SDK pipeline; *connection-level*
concerns live in the `HttpMessageHandler` chain underneath it. One P13 consequence of that split is load-bearing
enough to state in the overview: the handler chain underneath **also follows redirects by default**, which silently
removes the SDK's redirect authority unless the transport turns it off (§3.2).

The value proposition survives with one amendment. The core carries no concrete transport, codec or byte-stream
dependency (**SEAM-1**, **NFR-1**), so a consumer adopts it without inheriting a version conflict in a NuGet graph,
and swaps any one concern independently. The amendment is that "no dependency" cannot be literal on .NET: the
ecosystem's logging convergence point, `ILogger`, lives in a NuGet package rather than the shared framework, and the
port takes that one package deliberately and records it (§2.4). The correctness-sensitive decisions —
idempotent-method classification (**HTTP-9**/**RETRY-6**), retryable-status classification
(**RETRY-1**/**XCUT-5**/**XCUT-7**), body replayability (**BODY-3**/**BODY-4**), header-injection defence
(**HTTP-17**–**HTTP-19**/**XCUT-18**), cancellation-versus-timeout classification (**XCUT-1**/**XCUT-2**) — are made
once in `Dexpace.Sdk.Core` so every transport adapter behaves identically. Header-injection defence is not
theoretical here: verified on 10.0.401, `HttpRequestHeaders.TryAddWithoutValidation("X-Custom", "a\r\nInjected: yes")`
returns `true` and `HttpClient` writes both lines to the socket verbatim, so the model-layer check is the only line
of defence a transport built on the "without validation" API has (§3.2, §4).

**Which of the reference's constraints hold on .NET.** The reference's structural choices exist because of specific
JVM constraints; a faithful port must audit each one rather than inherit or discard the set wholesale.

- **Two genuinely different I/O execution models: HOLDS, but weakly.** .NET has a real blocking path — a thread
  parked in a socket read under `HttpClient.Send` (synchronous `SocketsHttpHandler` send since .NET 5),
  `Stream.Read`, `HttpContent.SerializeToStream` — and a real asynchronous path — `SendAsync`, `ReadAsync`,
  completion-port or epoll-driven continuations with no thread held. Both transport seams (**SEAM-11**, **SEAM-16**)
  therefore survive (§3.2, §3.3), and by P4's converse the port must not collapse a split the host genuinely has.
  The split is weak rather than strong because the ecosystem is overwhelmingly async-first: the styleguide forbids
  sync-over-async outright (`docs/styleguide/csharp/09-concurrency.md` rule 9.1), ASP.NET Core's server rejects
  synchronous body I/O unless `AllowSynchronousIO` is set (its documented default; not exercised here), and the only
  reason to keep the blocking seam is the set of genuinely synchronous call
  sites (console tools, synchronous interface implementations, `Dispose`) that would otherwise be forced into
  `.GetAwaiter().GetResult()`. So the design is async-first, and the sync seam is a *real* sync path where the
  transport supports one — never sync-over-async dressed as a seam.
- **Async-ecosystem fragmentation: DOES NOT HOLD.** `Task`/`Task<T>`, `ValueTask<T>`, `CancellationToken`,
  `IAsyncEnumerable<T>` and `IAsyncDisposable` ship with the runtime and every .NET framework, library and test
  runner speaks them; `System.Threading.Channels` ships in the shared framework too, and even `IObservable<T>` is a
  BCL interface (only the Rx operators live in the `System.Reactive` package). **SEAM-17**'s canonical pivot is
  therefore simply `Task<Response>`, and its per-ecosystem adapter apparatus collapses (P2, P4; §3.3). An
  `IObservable<T>` bridge for pagination and SSE is an optional convenience package, not a seam.
- **No standard byte-stream type good enough to build a wire protocol on: DOES NOT HOLD.** `Stream` (with
  `ReadExactly`/`ReadAtLeast` since .NET 7), `Memory<byte>`/`Span<byte>`, `IBufferWriter<byte>`, `MemoryStream` and
  `ArrayPool<byte>` ship with the runtime. The byte-stream *provider* seam retires; its behavioural contract does
  not, and is mapped onto the `Stream` contract with its differences named (§3.1). One nuance the phrase
  "ships with the runtime" hides: `System.IO.Pipelines` (`PipeReader`/`PipeWriter`) is in `Microsoft.NETCore.App` only
  from .NET 9 — verified absent from the 8.0.31 reference pack — so on the `net8.0` target it is a NuGet dependency
  and core does not use it.
- **Generic erasure: DOES NOT HOLD.** The CLR reifies generics: inside `Deserialize<T>`, `typeof(T)` is the full
  closed type, including `List<Dto>`, so **SERDE-5**–**SERDE-8**'s type-witness machinery is nearly free (§3.4,
  §7.3). *Hidden precondition (P7):* the free path is reflection over `typeof(T)`, and reflection is exactly what the
  next constraint removes, so on .NET the witness that actually works under trimming is a `JsonTypeInfo<T>` from a
  source-generated context. Verified on 10.0.401: a file-based app defaults to AOT-compatible settings, and a
  reflection-based `JsonSerializer.SerializeAsync(stream, 1)` in it throws `InvalidOperationException` ("Reflection-
  based serialization has been disabled").
- **A whole-program shrinker with a reflection blind spot: HOLDS — unlike Ruby, and literally.** ILLink trimming
  and NativeAOT both remove code they cannot see referenced, and both are mainstream deployment modes (containers,
  serverless, mobile). **NFR-8** therefore applies as written; the .NET form of its "keep configuration" is
  annotation rather than a rules file — `IsTrimmable`/`IsAotCompatible` on every library, the trim/AOT/single-file
  analyzers promoted to errors, `[DynamicallyAccessedMembers]` where reflection is unavoidable, and a published
  NativeAOT smoke consumer as **NFR-9**'s regression guard (§9).
- **An ambient execution context that flows across awaits: NEW, and it cuts both ways.** `ExecutionContext` carries
  every `AsyncLocal<T>` — including `Activity.Current` — across `await`, `Task.Run` and thread-pool hand-offs
  automatically, so **SEAM-24**/**ASYNC-8**–**ASYNC-12**'s diagnostic-context propagation is largely free (§8.1). The
  same mechanism leaks context into places that outlive the call (timers, cached tasks) and makes a context that the
  SDK sets visible to the caller's continuation only if set before the first `await`, so the port treats ambient
  state as read-mostly and threads per-call state explicitly (§5).
- **A `SynchronizationContext` that can deadlock sync-over-async: NEW.** Under a single-threaded
  `SynchronizationContext` (WinForms, WPF, legacy ASP.NET, some test hosts), blocking on a task whose continuation
  wants that context deadlocks. This is why every library `await` uses `ConfigureAwait(false)`
  (`docs/styleguide/csharp/09-concurrency.md` rule 9.4) and why the async→blocking bridge is a documented hazard
  rather than a free convenience (§3.3).
- **A global interpreter lock: DOES NOT APPLY.** .NET has true parallel threads and a JVM-like memory model;
  `Volatile`, `Interlocked` and immutable publication are the tools, exactly as the reference's `volatile` and CAS
  are. **XCUT-11**, **SEAM-12**, **TRANSPORT-29** and **ASYNC-22** read on .NET as they read on the JVM.

A faithful port preserves the seams and their invariants; it does not preserve the reference's module count. This
port ends up with *fewer* seams surviving than the Ruby port — the async pivot collapses into the runtime's `Task`,
and discovery collapses into explicit construction and the DI container (§3.6) — and with fewer packages than the
reference's module map, because the runtime already ships the pieces the reference had to wrap.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

---

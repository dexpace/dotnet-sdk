## 10. Deliberate Deviations from the Reference Contract

Every place where the .NET-idiomatic answer changes the *mechanism* by which a requirement is satisfied, or leaves
a requirement's letter unmet, rather than merely relocating it. Each entry states the IDs it touches, whether the
specification sanctions it (with the sanctioning text quoted) or this port judged it (P10), and the section that
argues it in full. Nothing new is argued here (P12); an entry that seems to need more than its summary is a defect in
the section it points at.

**What the catalogue does and does not concede.** Most entries change mechanism or packaging and argue, per P9, that
the guarantee the requirement protects is kept or strengthened. **Four leave a MUST clause unmet on a stated domain,
and say so rather than argue it away (P8):** AUTH-15's MD5 algorithms on a host whose crypto refuses MD5 (entry 16),
PAGE-21's byte-for-byte splice for percent-encoded unreserved characters (entry 18), OBS-27's Datadog trace-id
flavour (entry 24), and the never-throw clauses of CFG-5–CFG-7, inverted deliberately into fail-fast startup
validation (entry 26). **Two more meet a MUST clause in structured form only** — OBS-3's literal `null` in rendered
message text (entry 22) and the cooperative reading of ASYNC-3's and PIPE-33's interrupt mode, whose residual is a
blocking transport that ignores its token (entry 8). Entries 27 and 28 are departures from the house styleguide, not
from the specification, and are collected here because the styleguide overlay indexes them.

Unlike the Ruby catalogue, this one is written against a partial as-built tree (`d45e64b`). An as-built *divergence*
from the specification that the design corrects — a bug, such as the default-allow URL redactor of §8.1 or the
full-jitter backoff of §6.1 — is not a deviation and does not appear here; it appears in the section's **As built**
line and in the roadmap.

1. **Core takes a runtime dependency on `Microsoft.Extensions.Logging.Abstractions`.** *Touches* **SEAM-1**,
   **NFR-1**. **Sanctioned in part**: **SEAM-1**'s appendix-C row allows core to depend "at runtime on nothing
   beyond its language's standard library plus a logging facade"; *judged* against **NFR-1**'s "a compile-time-only
   logging facade abstraction" and against **SEAM-1**'s chapter wording, which says the same. NuGet has no
   compile-only scope for a facade called at run time, and P2 does not pass the package, because it is installed
   from a registry; P14 justifies it, because every .NET host and sink already speaks `ILogger`. The cost is measured
   on the real graph: on `net8.0` at `d45e64b` the facade 9.0.5 also brings `DependencyInjection.Abstractions` 9.0.5
   and a `System.Diagnostics.DiagnosticSource` 9.0.5 package copy that displaces the in-box assembly. That closure is
   wider than "a logging facade", so the terms are band-matched facade versions per target framework, transitive
   pinning, and a `deps.json` allow-list test that is the **SEAM-1** audit. §2.4 (terms and measurement), §8.1, §9.2
   (the nuspec assertion).
2. **The DI package's hosting family counts as one library.** *Touches* **NFR-2**. *Judged* (a SHOULD).
   `Dexpace.Sdk.Extensions.DependencyInjection` depends on `DependencyInjection.Abstractions`, `Http`, `Options` and
   `Options.ConfigurationExtensions` — one publisher's lock-stepped hosting family, which is what "integrate with the
   host" means on .NET. Splitting it would hand the consumer four packages to wire one feature. The transport and
   codec adapters stay within the one-library budget. §2.1.
   *Dated correction, 2026-10-13, phase 9 (P9-2, P9-3):* measured: `Microsoft.Extensions.Http` is the one root, and its closure is the 13 `Microsoft.Extensions.*` packages (`Http`, `Configuration.Abstractions`, `Configuration`, `Configuration.Binder`,
   `DependencyInjection.Abstractions`, `DependencyInjection`, `Diagnostics.Abstractions`, `Diagnostics`, `Logging.Abstractions`, `Logging`, `Options`, `Options.ConfigurationExtensions`, `Primitives`), all band-matched; the package also references the reference transport
   (`Dexpace.Sdk.Http.SystemNet`), a first-party project the audit's policy names. The audit counts the root as one library.
3. **The byte-stream provider seam is retired; its behavioural contract is not.** *Touches* **SEAM-3**–**SEAM-10**
   as applied to this seam, **IO-30**–**IO-36**, **IO-39**, **XCUT-23** as applied to this seam. *Judged* (P2).
   `Stream`, `Memory<byte>`, `IBufferWriter<byte>`, `MemoryStream`, `ArrayPool<byte>` and `Encoding` are in the
   `Microsoft.NETCore.App` reference pack of the `net8.0` floor, so there is nothing to plug in, and no factory,
   install call or discovery. The argument does not lean on `System.IO.Pipelines`, which is a NuGet package on the
   floor. The IO-* invariants are kept, most free on the `Stream` contract and the rest as internal core helpers; no
   Okio-shaped `Source`/`Sink`/`Buffer` vocabulary ships (P11, P14). §3.1.
4. **End of stream is `Stream.Read` returning 0.** *Topic label `eof-is-zero-read`, added by dated correction, 2026-10-03 (phase 3a).* *Touches* **IO-1**, **IO-2**, **IO-11**, **IO-15**, **IO-16**,
   **IO-17** and **BODY-10** (their zero-read clauses), **IO-18**, **BODY-25**. *Judged* (P1, P6). `Stream.Read`
   returns 0 both at EOF and for a zero-count request (verified), exactly the collapse **IO-2**'s rationale
   describes, and the port cannot change `Stream`. Core never issues a zero-count read. Because 0 *is* EOF by
   contract, a misbehaving source cannot make a pump spin, and a known-length copy still detects truncation; a
   premature 0 on an unknown-length body is undetectable on any host. The −1 sentinel, `exhausted()`, `skip`, emit
   and the native-stream bridge constrain surfaces the port does not expose. §3.1.
5. **One stream-ownership rule for request bodies; `ResponseBody.FromStream` takes ownership.** *Touches*
   **BODY-8**, and **IO-6** at the helper layer. **Sanctioned** by **BODY-8**: "A port MUST decide its
   stream-ownership/close rule deliberately rather than assume every single-use body closes its input." A request
   body closes exactly what it opened — a file body opens and disposes a `FileStream` per write, and
   `RequestBody.FromStream` never disposes the caller's stream — which resolves the reference's per-variant
   inconsistency in one direction. `ResponseBody.FromStream` is documented as an ownership transfer, because its
   callers are transports handing over a stream they opened. Internal helpers follow the BCL `leaveOpen` convention.
   §3.1.
   *Dated correction, 2026-10-03 (phase 3b, P3b-1):* the verdict stands and is extended to the 3b variants. `FromStream`, single-use or seekable, never disposes the
   caller's stream and moves its position only when seekable; `FromFile` opens and disposes a `FileStream` per write; `FromForm` and `Multipart` open nothing and dispose
   nothing they were given; the request-logging wrapper's tee leaves the destination open (`leaveOpen`); the response-logging wrapper takes ownership of its delegate, as a
   `Response` owns its body.
6. **A second `OpenReadAsync` throws instead of returning the same handle.** *Touches* **BODY-14**, **HTTP-41**.
   *Judged.* **BODY-14** says "requesting the handle repeatedly MUST return the same underlying handle, not a fresh
   replay"; the as-built and designed body throws `StreamConsumedException` on the second request. Handing the same
   partially read `Stream` to a second caller is how two consumers interleave reads silently; throwing is the louder
   form of the same no-replay guarantee and narrows no correctness property (P9). §3.1.
   *Dated correction, 2026-10-03 (phase 3a, P3a-3):* the verdict covers both forms. Each SDK response variant keeps one
   consume latch, flipped by whichever of `OpenRead` and `OpenReadAsync` runs first, and the loser throws
   `StreamConsumedException` whatever its form; the BCL itself is not uniform here (`HttpContent.ReadAsStream` after
   `ReadAsStreamAsync` throws, while a second `ReadAsStreamAsync` returns the same stream). The replayable error body stays
   replayable. A transport's own body must flip the latch before it touches `HttpContent` (phase 8b).
7. **Transport failures are `SdkException`s chaining the native exception, not members of an I/O-error family.**
   *Touches* **TRANSPORT-20**, **XCUT-4** (its "I/O-error family" clause), **RETRY-2**, **RETRY-4**, **RECOV-17**
   (stated in appendix C only). *Judged* (P14). On .NET the existing catch sites for HTTP failures are
   `catch (HttpRequestException)`, and neither `HttpRequestException` nor `SocketException` is an `IOException`
   (verified). With single inheritance and `SdkException` as the SDK root, `ServiceRequestException` can be neither.
   The native exception is kept as `InnerException` and the failure reports itself retryable, so **TRANSPORT-20**'s
   other clause holds. For classification the retryable I/O family is read as `IOException`, `HttpRequestException`
   without a status, `SocketException` and `TimeoutException`, plus the `IRetryableError` capability (**XCUT-6**);
   that reading is §11 item 23. §3.2 (failure mapping), §6.1 (classification).
   *Correction 2026-10-08 (phase 6a, P6a-8):* the capability widens the classification and never vetoes it: an SDK wrapper whose
   `IsRetryable` is `false` around an `IOException` is still retried, because **RETRY-2** says "anywhere in its cause chain".
8. **Cancellation is a cooperative `CancellationToken`; interrupts, interrupt modes and interrupt-flag restoration
   have no counterpart.** *Touches* **TRANSPORT-3**, **XCUT-1**, **ASYNC-3**, **ASYNC-4**, **ASYNC-14**, **SEAM-18**
   and **PIPE-34** (their interrupt clauses), **SEAM-14** clause (3), **XCUT-13** (its interrupt-flag clause),
   **PIPE-33**, **CFG-20**, **CFG-21**. **Sanctioned** for the shape of **XCUT-1** and **TRANSPORT-3** by **XCUT-1**'s
   own text: "On the reference (JVM) runtime the pattern is: catch the interrupt, re-assert the runtime's
   interrupt/cancellation state, then throw the I/O-family cancellation type (InterruptedIOException). A port MUST
   preserve the portable intent"; *judged* for the mechanism-level MUSTs (**ASYNC-3**, **ASYNC-4**, **PIPE-33**'s
   interrupt clause) and for **CFG-20** (a SHOULD). "Abort an in-flight exchange" is cancelling the token passed to
   `SendAsync`; "don't poison a pooled thread" is vacuous, because a token is per call and leaves nothing on a
   thread; "preserve the ambient flag" is free, because a signalled token cannot be un-signalled. Transports rethrow
   `OperationCanceledException` untouched. The sync→async bridge takes a caller-supplied `TaskScheduler` with no
   default, and its two modes are "cancel with interruption" = the call's token passed into the blocking `Execute`,
   where `HttpClient.Send` aborts the socket operation, and "cancel without interruption" = the caller's
   `WaitAsync(token)`. `Thread.Abort` is unsupported on .NET and `Thread.Interrupt` is banned, so **CFG-20**'s
   interruptible-task future retires; **CFG-21**'s obligation survives in cooperative form, because SDK code that
   stops awaiting a `Response`-producing task attaches a disposing continuation. *Residual (P6):* a blocking
   transport that ignores its token cannot be pre-empted by any bridge. §3.3 (argument), §3.2 (failure shape), §5.3
   (bridges), §8.3 (**CFG-20**/**CFG-21**).
9. **No provider registry or auto-discovery: explicit construction, plus single-registration validation under DI.**
   *Touches* **SEAM-5**–**SEAM-10**, **XCUT-23**, for the transports and the codec. *Judged* (P9 for the
   zero-candidate branch, P13 for the rejected mechanisms). Assembly scanning is invisible to ILLink and NativeAOT
   (**NFR-8**), and `[ModuleInitializer]` self-registration depends on lazy assembly load order. Without DI, a
   transport or codec is a non-nullable parameter: zero candidates is a compile error and multiple are
   unrepresentable. Under DI, `ValidateOnStart` asserts exactly one `IAsyncHttpClient` and one `ISerde` per client
   and lists every registered type on ambiguity; `TryAdd` gives **SEAM-6**'s idempotence and singleton lifetime
   **SEAM-7**'s caching. **SEAM-8** has no auto-resolved provider to replace, and **SEAM-10** is vacuous in one
   default load context. §3.6.
   *Dated correction, 2026-10-13, phase 9 (P9-16): as built:* `SEAM-6`'s different-provider failure is thrown at the `UseTransport`/`UseSerde` call; the validator scans the keyed descriptors for `SEAM-5`; `SEAM-7` is the keyed singleton.
10. **No builder objects beyond the multimap builders, and no generic `IBuilder<T>`.** *Touches* **SEAM-29** (its
    generic builder contract), **HTTP-2** (its builder clause), **HTTP-3** (`newBuilder()` for the non-multimap
    models), **HTTP-4** (its message form). *Judged* (P11, P14). Object initializers with `required` members,
    validating `init` accessors and `with` are the language's builder, and `required` turns a missing field into a
    compile error (CS9035), stronger than **HTTP-4**'s runtime error. Real builders survive only for batched
    multimap edits (`Headers.Builder`, `Query.Builder`). An `IBuilder<T>` with no implementers would be a fabricated
    tier; composition takes `Func<T, T>`. A value missing at run time throws `ArgumentNullException` with the field in
    `ParamName`, not the literal "`<name>` is required". §4.
11. **Reflection and open body subclassing can bypass construction-time validation.** *Touches* **HTTP-2**,
    **SEAM-29**, **XCUT-15**. *Judged, and admitted as a platform limitation* (P8). Private constructors, sealed
    types, the struct-default rule and the `init`/`with` rule close every language-level path.
    `RuntimeHelpers.GetUninitializedObject`, reflection and `Unsafe.As` cannot be closed, and
    `RequestBody`/`ResponseBody` are open by design. The exploitable invariants — header name and value validation
    (**HTTP-17**, **HTTP-18**, **XCUT-18**) and the method token — are re-checked at the model-to-wire boundary in
    every transport, which is mandatory on .NET because `TryAddWithoutValidation` writes CR/LF to the wire
    (verified). §4.
12. **The orchestrator's "every throwable" is every non-fatal `Exception`.** *Touches* **RECOV-2**, **RECOV-8**,
    **RETRY-25**, **PIPE-30**. *Judged* (P10): **RETRY-25** motivates it, but no clause narrows **RECOV-2**'s "catch
    EVERY throwable". Every orchestrator and retry loop catches with `when (!ExceptionFacts.IsFatal(ex))`, the fatal
    set being `OutOfMemoryException` and its subtypes; `StackOverflowException` is uncatchable and
    `AccessViolationException` is not delivered on .NET, so both are outside any port's reach. A filter runs before
    unwinding, so a fatal exception propagates with its stack untouched. On the async path the runtime captures it
    into the faulted task, so **PIPE-30**'s "propagate synchronously" holds only in the sense that no SDK frame
    catches it. §5.2, §5.3.
    *Topic label:* `fatal-exception-filter`. *Dated amendment (2026-10-07, phase 4b, P4b-8):* the filter sits at exactly four
    sites (the dispatcher's request-chain and transport calls, the response chain's two step calls), and
    `OperationCanceledException` is converted like any non-fatal exception, against styleguide 8.8's exclusion, because the
    recovery hooks must observe it and the token stays cancelled.
13. **Suppressed exceptions are an SDK-owned trail, stored in `Exception.Data` on foreign exceptions.** *Touches*
    **RECOV-12**, **RETRY-34**, **PAGE-13**, **SSE-29**, **SSE-36** and every clause that says "attach as
    suppressed". *Judged* (P8, P10). .NET has no suppressed list: `InnerException` is a single causal parent, and
    `AggregateException` is a wrapper that would break **RECOV-10**'s "rethrow UNCHANGED".
    `ExceptionTrail.AddSuppressed` appends to `SdkException.Suppressed` (rendered by `ToString`) or, on a foreign
    exception, to a list under a namespaced `Data` key readable through `ExceptionTrail.GetSuppressed`, and applies
    **RETRY-34**'s self-suppression guard. *Residual (P6):* a foreign exception's `ToString()` does not render the
    trail (verified). §5.2.
    *Topic label:* `suppressed-trail`. *Dated amendment (2026-10-07, phase 4b, P4b-14):* the trail is a snapshot; a foreign
    exception whose `Exception.Data` is read-only drops the secondary instead of throwing, because throwing would replace
    the primary (*residual*, verified: `Data` is virtual and an override can return a read-only dictionary).
14. **The async standard pipeline follows redirects, as the sync one does.** *Touches* **PIPE-32**, **REDIR-25**.
    *Topic label `async-redirect-pillar`, added by dated correction, 2026-10-07 (phase 4c).*
    **Sanctioned in part**: **REDIR-25** lists "(c) let callers install their own async redirect step" and ends "This
    asymmetry with the synchronous pipeline ... MUST be preserved or explicitly documented if changed"; *judged*
    against **PIPE-32**'s MUST NOT, which the standard async preset installing the step departs from. The async
    pipeline is the primary one and `RedirectPolicy` is already async, so a behavioural split would contradict the
    one-implementation-per-policy rule. The invariant the asymmetry protected — exactly one layer follows redirects —
    is kept by forcing `AllowAutoRedirect = false` on SDK-created transports and detecting a transport-level follow
    on caller-supplied ones. A caller who wants the 3xx verbatim sets `MaxRedirects = 0` (**REDIR-17**). This entry is
    the documentation **REDIR-25** asks for; the reading is §11 item 29. §5.3, §6.2.
    **Correction (2026-10-07, phase 4c): entry 14 gains the topic label `async-redirect-pillar`.** Entry 14 stands as written.
    Phase 4c built it: `PipelineBuilder.AddStandardResilience` installs one `RedirectPolicy` at `PipelineStage.Redirect`
    whether the pipeline is driven through `SendAsync` or `Send`, and both `DexpacePipeline.CreateDefault` paths follow
    redirects (`ResiliencePresetTests.The_async_and_sync_standard_pipelines_both_follow_a_redirect`). `PIPE-32`'s checklist row
    is ✅ for its documentation clause and 🚫 for the async no-follow, citing this entry; `REDIR-25`'s row (6b) cites it the
    same way. The single-layer invariant stays the transport's (phase 1 S3's `AllowAutoRedirect = false` and the borrowed-client
    detection; 8b, 9). A caller who wants the 3xx verbatim sets `MaxRedirects = 0` (`REDIR-17`, 6b) or omits the policy.
15. **No cross-origin marker: redirect and auth both compare against the immutable seed origin.** *Touches*
    **REDIR-11**, **AUTH-29**, **REDIR-8**, **XCUT-17**(b). *Judged* (P10), and stronger (P9). **REDIR-11** asks for
    an out-of-band signal from redirect to auth; the port sends none. `HttpPipeline.SendAsync` records the seed
    request's origin on the call-scoped context, immutable; `RedirectPolicy` judges cross-origin against it, and
    `AuthorizationPolicy` withholds the credential whenever the request it would stamp is not same-origin with it. A
    `Location` value cannot reach the field, the comparison can only withhold, nothing is added to the request (so
    **REDIR-11**'s own no-auth-step trap cannot occur), and it works under a custom redirect policy. It hardens the
    as-built variant, which recorded the auth policy's first-seen origin in a public string-keyed property bag.
    §6.2, §5.1.
16. **Digest MD5 and MD5-sess are supported only where the platform crypto provides MD5.** *Touches* **AUTH-15**,
    **AUTH-16**. *Judged, and admitted as a platform limitation* (P8). .NET bundles no MD5; on Linux it is OpenSSL's,
    which a FIPS-enabled provider refuses (published behaviour, not reproducible on the authoring machine). The port
    does not vendor one: it probes once, and where MD5 is refused the Digest handler declines MD5 and MD5-sess
    challenges as unsupported, falling through to SHA-256 when offered, instead of throwing. **AUTH-15**'s "MUST
    support exactly" all four holds only on hosts whose crypto provides MD5. §6.3.
    *Correction 2026-10-08 (phase 6c, P6c-30):* the probe runs once per process (`Md5Availability`): `net10.0` has no `MD5.IsSupported`, so it tries
    `MD5.HashData` and treats `CryptographicException` and `PlatformNotSupportedException` as "refused". Construction never fails on such a host, even with an
    MD5-only preference; the handler declines everything it cannot answer.
17. **Pages are materialized values; no page owns a live response.** *Touches* **PAGE-3**, **PAGE-12**, **PAGE-15**
    (its two-page case), and the "live response" of the page view described in the 12.1 preamble that **PAGE-1**
    rests on. *Judged* (P10). An async iterator's `finally` runs only when the consumer disposes the enumerator; a
    hand-driven, abandoned enumerator never runs it, even after forced collections (verified). So the engine never
    holds a response across a `yield`: it deserializes, captures status and headers, computes the next request and
    disposes before yielding, and `Page<T>` is an immutable, non-disposable carrier. The guarantee **PAGE-3** and
    **PAGE-12** protect — no stranded connection — is strengthened; the capability lost is streaming a page's raw
    body from the page view. **PAGE-12**'s look-ahead machinery and **PAGE-15**'s two-page case become unreachable
    (P4). §7.1.

    *Dated amendment, 2026-10-09 (phase 7c, P7c-15).* The entry also touches **PAGE-34**'s ownership clause ("the fetcher MUST NOT close;
    ownership transfers to the page"): a page owns no response, so nothing can transfer, and `Pageable.FromFetchers` inverts the clause the
    way this entry inverts **PAGE-3**. A fetcher reads its response, builds a materialized page and disposes the response itself
    (`await using`); the engine never sees one. The guarantee the clause protects, no leaked response, is kept by the fetcher's own scope, and
    a fetcher that throws before building its page owns what it opened, as **PAGE-34** says.
18. **The query splice is verbatim relative to `System.Uri`'s canonical form, not the caller's original bytes.**
    *Touches* **PAGE-21**, and **PAGE-24** insofar as `UriBuilder` writes an explicit default port.
    *Judged, and admitted as a platform limitation* (P8). `System.Uri` decodes percent-encoded unreserved characters
    in the query before SDK code sees it (verified: `?x=%7E%41` reads back as `?x=~A`), and the transport sends the
    canonical form, so byte-for-byte preservation is unattainable while `Request.Url` is a `Uri`. The residual is RFC
    3986 §6.2.2.2 normalization of unreserved characters only, which the RFC defines as equivalent; reserved
    characters, value-less flags, order and `+` are preserved (verified). The as-built splice's own defects
    (case-insensitive key match, retained duplicates) are bugs, not part of this entry. §7.1.

    *Dated amendment, 2026-10-09 (phase 7c).* The **PAGE-24** clause ("insofar as `UriBuilder` writes an explicit default port") is retired:
    the splice no longer uses `UriBuilder`. It rebuilds the URL from `Uri.GetComponents(SchemeAndServer | UserInfo | Path, UriEscaped)`, the
    new query and the fragment, so `https://h:443/p` is rebuilt as `https://h/p` and scheme, userinfo, host, port, path and fragment survive
    (verified on SDK 10.0.401; `QuerySpliceTests`). The **PAGE-21** residual stands, and the as-built defects it names are fixed.
19. **One pull-based paging engine: no executor mode and no rejection path.** *Touches* **PAGE-29** (its executor
    clause), **PAGE-30**. *Judged.* .NET has one iteration protocol and it is already non-blocking, so the
    sync/async engine split collapses (P4). A pull-based `IAsyncEnumerable<T>` never invokes a consumer callback —
    the consumer's own `await foreach` owns its context, and serial in-order delivery is the protocol — so an
    executor mode has nothing to configure and executor rejection cannot occur. **PAGE-25**–**PAGE-28** and
    **PAGE-31**–**PAGE-33** are met as written. §7.1.
20. **SSE lines longer than a configurable cap (default 1 MiB) fail the read.** *Touches* **SSE-19**.
    **Sanctioned** by **SSE-19**'s chapter text (`docs/product-spec/13-server-sent-events-and-streaming.md`): "a port
    MAY add a configurable cap and reject/truncate oversized lines, documenting the divergence." An unbounded line
    from a hostile server is an unbounded allocation; the byte-level line reader rejects lines over the cap, whose
    default matches **BODY-30**'s error-body cap. This entry is the documentation the clause requires. §7.2.
    *Dated correction, 2026-10-09, phase 7b (P7b-4, P7b-5, P7b-20):* the failure is `ServerSentEventLineTooLongException`, a `StreamingException` in `Dexpace.Sdk.Core.ServerSentEvents` (not `Errors`: the `SSE-37` stray guard watches that
    namespace), carrying `MaxLineBytes`; the reader stays failed afterwards because the source is mid-line. The cap bounds a *line*, not a block: lines that each fit accumulate until a blank line (residual R1, left open for the lead, documented
    in `docs/sdk-documentation/sse.md`). Closes issue #10.
21. **`Tristate<T>` is an invariant struct, so the covariance SHOULD cannot be met.** *Touches* **SERDE-14** (its
    "SHOULD be covariant" clause). *Judged* (P8). .NET variance exists only on interfaces and delegates, never on
    structs, and a struct is chosen because `default(Tristate<T>)` being Absent makes **SERDE-17** free. Absent and
    Null remain assignable to any parameterization through the non-generic `Tristate.Absent`/`Tristate.Null` markers
    and implicit conversions, which covers the use covariance served. §7.3.
22. **No structured log-event object: `ILogger` and `LoggerMessage` delegates deliver the guarantees.** *Touches*
    **OBS-1** (its shared-inert-event conformance step), **OBS-3** (the rendered `null`), **OBS-4**, **OBS-5**,
    **OBS-8**, **OBS-9**, **OBS-40**. *Judged* (P4, P14). .NET's logging convergence checks `IsEnabled` once at the
    call and passes arguments on the stack into a generated state struct, so the disabled path allocates and emits
    nothing (verified) with no event object, and **OBS-1**'s reference-identity step has no referent. **OBS-3**'s key
    validation becomes compile-time (stronger), but a `null` value renders as `(null)` in the message text while the
    structured value stays `null` (verified). Precedence and global context (**OBS-5**, **OBS-9**) belong to the
    provider and host. Emit-once (**OBS-8**) and the reserved-key collision diagnostic (**OBS-4**, **OBS-40**) are
    vacuous, because `EventId.Name` carries the category outside the key/value state. §8.1.
    *Correction 2026-10-07 (phase 5b, P5b-18):* **OBS-10** (the diagnostic-context fold) and **OBS-24** (context flow) rest on entry 23 and
    §8.1 rather than on this entry: the fold is `LoggerFactoryOptions.ActivityTrackingOptions`, a host allow-list, and the flow is the
    runtime's `AsyncLocal`, pinned by a test. **OBS-5**, **OBS-8**, **OBS-9** and **OBS-40** are N/A on this entry. No new entry is opened.
23. **Tracing is `System.Diagnostics.Activity`: its no-op is `null`, and log correlation is the host's.** *Touches*
    **OBS-25** (the shared no-op span), **OBS-21** (the recording flag's form), **OBS-23** and **OBS-10** (the key
    names and default allow-list), **OBS-28** (the vocabulary as spans and events). *Judged* (P2, P14).
    OpenTelemetry .NET's tracing API is `Activity`, in the shared framework. With no listener `StartActivity` returns
    `null` (verified), so the untraced path allocates nothing, but there is no shared no-op span object. Log
    correlation is `ActivityTrackingOptions`, a host-configured allow-list whose keys (`TraceId`, `SpanId`) and default
    set (which also folds `ParentId`) differ from `{trace.id, span.id}`. The HTTP-tracer vocabulary is an operation
    span, per-attempt spans and `ActivityEvent`s; **OBS-29**'s ordering holds through the span lifecycle. §8.1.
24. **Only the W3C trace-id flavour and the invalid sentinel; no Datadog flavour.** *Touches* **OBS-27**. *Judged,
    and admitted* (P8). `Activity` generates W3C 128-bit ids; `Activity.TraceIdGenerator` is a settable hook, but a
    64-bit decimal Datadog flavour is a propagation format the runtime does not model and Datadog's own .NET tracer
    owns. The no-op flavour is `default(ActivityTraceId)`, 32 zeros (verified). §8.1.
25. **Layered lookup is `Microsoft.Extensions.Configuration` in the DI package; appsettings stands in for the
    system-property tier; there is no global slot.** *Touches* **CFG-1**, **CFG-2**, **CFG-3**, **CFG-4**,
    **CFG-10**, **CFG-13**, **CFG-14**, **CFG-24** (its first layer). *Judged*: the specification gives the
    system-property tier no porting guidance (§11 item 4). Core holds options records and no lookup; the DI package
    binds them through `IConfiguration`, a NuGet dependency and so a separate unit (**NFR-2**). Tier order is kept —
    explicit `Action<Options>` over environment variables over `appsettings.json` (a genuinely different source,
    P11) over the record initializer. Key normalization is .NET's case-insensitive `__` → `:` rule instead of
    **CFG-3**'s lower-casing underscore → dot; an empty environment value is *present* to `IConfiguration`, not
    absent as **CFG-2** requires, and then fails binding at startup (entry 26); **CFG-4**'s raw accessor and
    **CFG-10**'s override removal are vacuous; the process-wide slot of **CFG-13** is not provided (the
    service-locator pattern the house guide rejects). The explicit `ProxyOptions` argument replaces **CFG-24**'s
    system-property layer — the same substitution applied twice. §8.2.
    *Dated correction, 2026-10-07, phase 5a (P5a-15):* `FromEnvironment` reads the upper- and lower-case names, skips upper-case
    `HTTP_PROXY` when `GATEWAY_INTERFACE` is present, serves one proxy for every target, and never falls through on a malformed value.
    *Dated correction, 2026-10-13, phase 9 (P9-7; open for the lead):* an empty or whitespace-only value is **absent**: the record default applies, and a lower provider's value that the empty one shadows is not consulted (`IConfiguration` merges providers before the reader sees a key).
    This replaces "an empty environment value is *present* ... and then fails binding at startup": the deviation is now a silent shadow rather than a loud failure, recorded as `di-empty-shadows` in `docs/first-release.md`. The bind is the DI package's reader, not the binder (§8.2).
26. **Unparseable configuration fails at startup instead of falling back silently.** *Touches* **CFG-5**, **CFG-6**,
    **CFG-7** (their never-throw letters; the **CFG-6** and **CFG-7** grammars are kept). *Judged, against the
    specification's letter and with the house guide* (`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md`
    1.4). The binder's `TypeConverter`s throw on bad input (verified: `"1"` → `bool` is a `FormatException`), and the
    house guide requires `ValidateOnStart`; a silent fallback hides misconfiguration until production, so the boot
    fails instead. The grammars are kept exactly: a custom `TimeSpan` converter implements **CFG-7** (the default one
    reads `"1000"` as 1000 days, verified), and a base-10 integer converter replaces `Int32Converter`, which accepts
    `"0x10"` (verified). §8.2.
    *Dated correction, 2026-10-13, phase 9 (P9-8, P9-9):* touches **OBS-35** as well (an unrecognised level fails the boot naming the key and listing the names, where the specification falls back). The grammars are the DI package's reader's, not converters registered with the binder: both binder modes
    were measured (V1, V2) and neither can serve; every failure is reported at once as `"<path>: <rule>"` and never quotes a configured string, URL or credential (an accessor's numeric range message may echo the number, P9-28).

### Departures from the house styleguide

Not deviations from the specification: the styleguide in `docs/styleguide/` binds this repository's code, and these
two departures from it are recorded here because the `docs/styleguide/README.md` overlay points at them.

27. **Public API keeps the `I` interface prefix and the `Async` suffix.** *Touches* no requirement ID;
    `docs/styleguide/csharp/02-naming-conventions.md` rules 2.6 and 2.7. *Judged* (P14): the Framework Design
    Guidelines are the stronger authority for a public NuGet surface. A prefix-less `HttpClient` would collide with
    `System.Net.Http.HttpClient` in every consumer's scope, and the SDK ships `Execute` beside `ExecuteAsync`, so the
    suffix carries information. Applies to `IHttpClient`, `IAsyncHttpClient`, `ISerde` and every `Task`- or
    `ValueTask`-returning public member. §3.2, §9.4.
28. **`CA1062` and `CA2007` are dialled to `none`.** *Touches* no requirement ID;
    `docs/styleguide/csharp/01-formatting-and-tooling.md` 1.3 and the rules that name the two analyzers as their
    enforcement (3.2, 5.3, 8.5, 9.4). *Judged.* `CA1062` is defensible under nullable reference types plus
    `ThrowIfNull` at entry points. `CA2007`'s recorded rationale — "`await using` / `await foreach` emit implicit
    awaits the rule cannot see" — is factually wrong: `CA2007` reports on both (verified). The real cost is
    ergonomic (`ConfiguredAsyncDisposable` changes the local's type), and the `.editorconfig` comment and the overlay
    row should carry that reason instead. §9.4.

    **Correction (2026-09-29): the `CA2007` half of this entry is retired.** Roadmap phase 0 (PR #21, 2026-09-28)
    re-enabled `CA2007` at `warning` for library code under `src/` and kept it at `none` only for tests, repository
    tools and the AOT smoke consumer, the split styleguide 9.4 makes; the library code now uses
    `ConfigureAwait(false)` throughout, including on `await using` and `await foreach`. `CA2007` is therefore
    conformed and no longer a departure. The entry now records `CA1062` alone, whose reasoning is unchanged. The
    entry keeps its number and the text above stands as written (§9.4 carries the matching correction).

**Correction (2026-10-02): phase 2a adds entries 29 and 30.** Both were ruled or recorded during the domain-model
rework (the [phase 2a design](../work/mvp/phase2/phase2a/2026-09-29-phase2a-domain-model-design.md), rulings of 2026-09-30).
Entries 1–28 stand as written.

29. **A URL error carries the redacted input, not the verbatim one.** *Touches* **HTTP-47** (a SHOULD), **OBS-11**,
    **XCUT-19**. *Judged* (P10): **HTTP-47** asks for an argument error "carrying the offending input", and the
    verbatim form would put a userinfo password or a query token into an exception message that is logged and
    surfaced. `Request.Create` and the constructor therefore pass the input through `UrlRedactor` (one shared internal
    instance): `ftp://h/x`, `::bad` and `/rel` carry no userinfo or query and redact to themselves, so the message still
    names them, while `https://user:secret@h:bad/` becomes `[malformed url]`. Log and secret hygiene outranks the letter
    of a SHOULD. §4.2.
30. **An absent response body is an empty, replayable buffered body.** *Touches* **HTTP-6** (its "optional body").
    *Mechanism*, not a departure: the body is optional at construction and never `null` on read, so a consumer never
    branches on a missing body, and reading it twice yields empty both times. The as-built default was already a
    non-null empty body; phase 2a made it replayable. §4.2.

**Correction (2026-10-03): phase 3b adds entry 31.** Ruled by the lead on 2026-10-02 as a §10 entry, not a §11 reading (the [phase 3b design](../work/mvp/phase3/phase3b/2026-10-02-phase3b-bodies-design.md), P3b-5).
Entries 1–30 stand as written.

31. **"Regular file" is enforced only where the platform can see it.** *Touches* **BODY-11**, **HTTP-40** (a MUST clause unmet on a stated domain). *Judged*: the clause asks
    `FromFile` to reject a path that is not a regular file. On Unix the shared framework cannot tell a FIFO or a character device from a regular file (`File.Exists` is true
    and `FileInfo.Attributes` is `Normal` for a regular file, `/dev/null`, `/dev/zero` and a FIFO), opening a FIFO for reading blocks, and a symbolic link reports its own
    path length until it is resolved. `FromFile` therefore resolves a link to its final target before taking the size, throws `FileNotFoundException` for a missing path and
    `ArgumentException` for a directory (and, on Windows, a `Device` path), and does not try to detect special files: they report a size of 0, a **count of 0 never opens
    the file**, and the write is a legitimate empty write. The residue is that a Unix FIFO or device uploads as an empty body instead of failing at construction. A
    P/Invoke `stat` was rejected: platform-specific struct layouts and an AOT-visible native dependency for one validation clause. §3.1.

**Correction (2026-10-11): phase 8b adds entries 32 and 33.** Entry 32 was ruled by the lead on 2026-10-11 (the [phase 8b design](../work/mvp/phase8/phase8b/2026-10-09-phase8b-reference-transport-design.md), P8b-14) and
entry 33 the same day (P8b-T2, the lead's disposition of the finding the 8a review raised as F-T2). Entries 1–31 stand as written. With entry 31 these leave the count of MUST clauses unmet on a stated domain
at seven, not the four the paragraph above counts; the paragraph is not rewritten. **The count is over the parity target.** The roadmap's parity table (*MUST disposition*) allows *at most five*
unsatisfied clauses; entries 16, 18, 24, 26 and 31 already used all five, so entry 32 is the sixth and entry 33 the seventh. Both rulings were taken on the premise that budget remained ("spend one of
the five"), which was false; the over-budget of two is an **open item for the lead and for phase 11's parity audit** (recorded in the phase 8b checklist and `docs/first-release.md`), and neither
entry is within the five.

32. **A malformed inbound header *name* fails the whole response on `SocketsHttpHandler`.** *Touches* **TRANSPORT-14** (a MUST clause unmet on a stated domain, for its name half only). *Judged*: the clause asks
    the adapter to drop a response header whose name holds a space, a control byte or a non-ASCII byte and deliver the rest. `SocketsHttpHandler`'s HTTP/1.1 parser raises `HttpRequestException` ("Received an invalid
    header name") for the whole response before the adapter sees any header, and exposes no leniency switch (measured by the kit on 10.0.401); the only workaround is replacing the parser. The value half is met
    (`Headers.Builder.AddInbound` drops a header whose value carries a control byte and keeps the rest) and so is the obs-text SHOULD. The conformance kit asserts the three clauses separately
    (`transport-14.value-control-dropped`, `.obs-text-preserved`, `.name-malformed-dropped`), and the SystemNet driver carries a permanent waiver for the last alone, which turns stale, and so fails the run, the
    day the runtime stops rejecting. It is the sixth clause unmet on a stated domain (after entries 16, 18, 24, 26 and 31) against the parity target of at most five, so it is over the
    target and open for the lead and for the audit of roadmap phase 11 (see the correction above). §3.2.
33. **`SocketsHttpHandler` re-sends a body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` whose connection ends before any response byte.** *Touches* **TRANSPORT-2** (a MUST clause unmet on a stated domain, for
    its native-retry clause only). *Judged*: the clause asks the transport to turn the native client's own retry off so the SDK's pipeline is the only retry authority. `SocketsHttpHandler` has no switch for it
    (runtime 10.0, `HttpConnection.SendAsync`: `_canRetry` is cleared on every send and set only when the first read for the response returns zero bytes, for a request with no content or with an
    `Expect: 100-continue`; an `IOException` becomes a connection-failure retry only while it is set), and a request that qualifies is sent up to three more times. The transport closes the clause for every method whose
    framing it can keep byte-identical: a body-less `POST`, `PUT`, `PATCH` or extension-method request is given a zero-length content, which the native client writes as `Content-Length: 0` for those methods anyway,
    so the retry cannot fire (a caller `Expect` is a framing header and is dropped). It leaves `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` as they are: all are idempotent (RFC 9110 section 9.2.2, which permits the
    automatic retry), and attaching content would break **TRANSPORT-26** or RFC 9110 section 8.6's "SHOULD NOT" (`TRACE` natively writes `Content-Length: 0`, so a content there would be wire-identical, but RFC 9110
    section 9.3.8 forbids content in a `TRACE`). A single-use body is never re-written, so the specification's own conformance sentence passes. The kit's `transport-2.bodyless-not-resent` carries a permanent waiver
    for the residual, pinned by `BodylessRetryWireTests` so a runtime change is noticed. It is the seventh clause unmet on a stated domain, so it is over the parity target of at most five by
    two together with entry 32, and open for the lead and for the audit of roadmap phase 11 (see the correction above). §3.2.

Three things that are deliberately **not** deviations, recorded because a reader may expect them. The fixed-buffer
encode profile of **SERDE-4** is met, not bent: core derives `Serialize<T>(ISerde, Span<byte>, T)` from the
`IBufferWriter<byte>` primitive, the caller expresses the offset as `buffer.AsSpan(offset)`, the count is returned,
and a payload that does not fit throws `ArgumentOutOfRangeException` (§3.4, §7.3). The two retry stacks are **not**
unified, so neither **RETRY-28**'s nor `08-execution-pipelines.md`'s unification sanction is invoked (§6.1, §11 item
19). And both transport seams and both pipeline bridges survive, so no single-execution-model collapse appears above
(§1, §3.3, §5.3).

---

**Correction 2026-10-07 (phase 5c, P5c-14, P5c-18).** Entry 23: `OBS-29`'s evidence is an `ActivityListener` ordering test over a succeeding and a
retry-exhausted operation, and `OBS-21`'s mutators are inert for the SDK's own writes only (every SDK tag, event and status write is guarded by
`IsAllDataRequested`; `Activity.SetTag` on a non-recording activity is not itself a no-op). Entry 24: `OBS-27` also asks that a zero draw be coerced
to a non-zero id; the runtime's generator is not shown to do so (no runtime source was available to verify, and its public contract does not say), so
the clause is **admitted** (probability 2^-128 per draw), the SDK never sets the process-wide `Activity.TraceIdGenerator` (a library must not set a
hook the application owns), and `BannedSymbols.txt` enforces that in `src/`. No new entry is opened.

---

**Correction 2026-10-20 (phase 11, P11-3, P11-21): the as-built audit of this chapter.** Phase 11 re-derived every entry above from the source, not from another document, and records the result in
[`docs/deviations.md`](../deviations.md): one row per entry (status, disposition, reason class, `path:line` evidence), a block per retained entry, the negative scans that back every absence claim, and the
specification amendment set. `LedgerAuditTests` holds that file to this chapter. Every entry keeps its number and its text; this block is the dated correction, appended and not substituted (P11-3). **Verdicts:** of the 33
entries, 19 confirmed, 10 narrowed and 4 contradicted (12, 31, 32 and 33); six are retired by repair, entry 12 is a documentation correction and the rest are retained, several with a repair made on the way.

- **The count (P11-2).** The paragraph at the head of this chapter counts four clauses unmet on a stated domain, and the phase 8b correction seven. By the counting rule a clause counts when the *observable outcome* it protects
  is not delivered on some stated domain; a mechanism substitution that keeps the outcome, a clause the specification sanctions changing (`REDIR-25`, `BODY-8`, `SSE-19`) and one it makes unsatisfiable (`AUTH-16`, `AUTH-17`,
  `AUTH-22`, §11 item 62) do not count. The count is **three**, inside the parity target of five: **AUTH-15** on a host whose crypto refuses MD5 (entry 16), **PAGE-21** for percent-encoded unreserved characters (entry 18) and
  **TRANSPORT-14**'s malformed header *name* (entry 32). Entries 24, 31 and 33 were repaired, 26 meets the accessor letter and is reclassified, and the "meet a MUST clause in structured form only" pair
  (entries 22 and 8) are mechanism substitutions that deliver the outcome. The paragraph's "four" and the 8b correction's "seven" and "over the parity target" stand as history.
- **Retired by repair:** entries **6** (task 3.1), **7** (2.1), **24** (5.1), **28** (1.6), **31** (3.3) and **33** (4.1). Their text above describes a departure that no longer exists; the audit row is their record.
- **Entry 1** (narrowed, retained). Measured on `net10.0`: the facade 10.0.12 brings `DependencyInjection.Abstractions` 10.0.12 and nothing else, with no `DiagnosticSource` package; the `net8.0` 9.0.5 closure the entry measured is superseded
  (`net8.0` is gone). The audit is the CI script `scripts/ci/dependency-audit.cs`, not a `deps.json` test, and allows exactly those two packages (task 1.3), proved by `dependency-audit-selftest.sh`.
- **Entry 2** (confirmed, retained). One root, `Microsoft.Extensions.Http`, and a 13-package band-matched closure.
- **Entry 3** (confirmed, retained). Every `IO/` type is `internal`; scan N1 finds no `Source`, `Sink` or `Buffer` vocabulary. "The `net8.0` floor" reads `net10.0` (decision D1).
- **Entry 4** (confirmed, retained). Core issues no zero-count read, and a known length is checked.
- **Entry 5** (confirmed, retained). `BODY-8` sanctions the choice.
- **Entry 6** (**retired**, task 3.1). A second `OpenRead`/`OpenReadAsync` returns the first handle in both forms (`BODY-14`, `HTTP-41`); an open after dispose throws `StreamClosedException`. The response-logging wrapper keeps its single-use
  throw past its capture cap, because `BODY-24` (MUST) says a second read there fails.
- **Entry 7** (**retired**, task 2.1). `ServiceRequestException` derives from `IOException` and implements `IRetryableError` (`TRANSPORT-20`, `XCUT-4`); `catch (SdkException)` no longer catches a transport failure. The never-thrown
  `ServiceResponseException` is deleted (task 2.2). The classification reading of §11 item 23 stands.
- **Entry 8** (narrowed, retained). "Transports rethrow `OperationCanceledException` untouched" is narrower than built: the transport replaces the native exception when a deadline is armed or the cancel is internal. Repaired on the way:
  `AsAsync` faults the task when the scheduler rejects the work or the token is already signalled (`ASYNC-2`, task 2.3), and "cancel without interruption" is the public `WaitOrDisposeAsync` (`SEAM-30`, task 2.4; §11 item 43).
  `Thread.Abort` and `[ModuleInitializer]` are now banned.
- **Entry 9** (narrowed, retained). `TryAdd` is used nowhere: `ServiceSlot` (identity-checked, throwing at `UseTransport`/`UseSerde`) and the validator give `SEAM-6` and `SEAM-5`.
- **Entry 10** (narrowed, retained). "No builder objects" means no *model* builder: four public `*Builder` types exist (`Headers.Builder`, `Query.Builder`, `PipelineBuilder`, `DexpaceClientBuilder`). A validating `init` accessor now names its
  property in `ParamName` (task 2.5); about fifteen range and format checks inside accessors still report `value`.
- **Entry 11** (confirmed, retained).
- **Entry 12** (contradicted, documentation corrected). The filter sits at six recovery sites among many filtered catch-alls, not "exactly four", and two redaction catch-alls swallowed `OutOfMemoryException` (repaired, task 1.4); a source
  scan now holds every catch-all in the four runtime libraries.
- **Entry 13** (confirmed, retained). **Entry 14** (confirmed, retained; `REDIR-25` sanctions it). **Entry 15** (confirmed, retained; stronger than the letter).
- **Entry 16** (confirmed, retained, **counted**). The probe treats any non-fatal failure as a refusal (task 5.8); the mapping has never run on a real FIPS host.
- **Entry 17** (narrowed, retained). `Page` stored the caller's list, so a fetcher page could change after it was yielded; it now copies (task 3.5).
- **Entry 18** (narrowed, retained, **counted**). The splice drops no empty `&` segment (task 3.6), and `Request` refuses a `Uri` created without canonicalization, the only route that writes CR/LF to the wire (task 3.4); the residual is
  unreserved-character equivalence.
- **Entry 19** (narrowed, retained). A consumer with no `SynchronizationContext` resumes inline on the completing thread; `await Task.Yield()` moves it.
- **Entry 20** (confirmed, retained). Residual R1 is repaired: an event whose data passes `maxEventBytes` (8 MiB by default) fails with `ServerSentEventTooLargeException` (task 5.6); the per-line cap stays.
- **Entry 21** (confirmed, retained).
- **Entry 22** (narrowed, retained). A null field value in the header-bearing events' state is the literal `null` (task 5.2); the plain-name fixed-key events use the `LoggerMessage` generator (task 5.3) and the dotted-key ones keep `Define`.
- **Entry 23** (narrowed, retained). `Disposal` wrote to a non-recording activity at one site, repaired (task 1.5); every other SDK write was guarded as the 5c correction said.
- **Entry 24** (**retired**, task 5.1). `TraceIdGeneration.W3C()`, `Datadog()` and `FormatDatadog` re-draw a zero draw; the SDK still never sets the process-wide hook. The runtime's own generator tests no zero draw (V13), so the
  clause was *unmet by the runtime*, not merely "not shown".
- **Entry 25** (narrowed, retained). The `CFG-2` fall-through is repaired (task 5.4): when the registered `IConfiguration` is the root that holds the client's section, a blank value falls through to the next provider with one. The "empty
  value is *present*" sentence and the 9 correction's silent shadow are superseded; no property tier, raw accessor or global slot remains the deviation.
- **Entry 26** (confirmed, retained, **reclassified**; P11-10). The accessor letter of `CFG-5` to `CFG-7` is met (the grammars never throw and keep the default); the boot failure is a stricter host rule from the house guide, not an unmet
  clause. This removes it from the count.
- **Entry 27** (confirmed, retained).
- **Entry 28** (**retired**, task 1.6). `CA1062` is `error` for `src/`; its single finding was a real null dereference in `HttpResponseException`'s constructor, now guarded, which also rejects a non-error status (`XCUT-8`).
  It stays relaxed for tests and tools only.
- **Entry 29** (confirmed, retained). **Entry 30** (confirmed, retained; a mechanism, not a departure).
- **Entry 31** (contradicted, **retired**, task 3.3). `System.Formats.Tar` classifies a Unix path without opening it and is AOT-clean, so `FromFile` rejects a FIFO, device or socket at construction; verified on Linux only. *Corrected 2026-10-21 (review):* the writer opens a **regular** file to attach its content, so "without opening it" holds for the special files only; a refusal to open is read as "regular" and an empty file the process may not read is accepted, as before phase 11.
- **Entry 32** (contradicted in its reason, retained, **counted**; P11-11). "The only workaround is replacing the parser" is wrong: a `PlaintextStreamFilter` can drop the line (V7). It stays unmet because that filter must parse every response head
  ahead of the runtime's parser, a security and correctness risk out of proportion to tolerating a non-compliant server, where entry 33's filter never inspects a byte. Trigger to revisit: a consumer whose server sends such names.
- **Entry 33** (contradicted, **retired**, task 4.1; P11-12). A pass-through `PlaintextStreamFilter` on every handler the SDK builds turns a connection that ends before any response byte into the I/O failure the runtime raises itself, so a
  body-less `GET`, `HEAD`, `OPTIONS`, `DELETE` and `TRACE` reaches the server once. A handler or `HttpClient` the caller supplies is the caller's native client and keeps its retry (§11 item 71).
- **Departures from the house styleguide.** The list above names two. After the audit it names **one**, entry 27 (`I` prefix and `Async` suffix): entry 28 is retired, and the `LoggerMessage` source-generator departure the overlay
  records (the dotted keys the specification fixes cannot be generator placeholders, `SYSLIB1014`) is added to the list, narrowed to the fixed-key events whose names have dots; the six plain-name events conform.

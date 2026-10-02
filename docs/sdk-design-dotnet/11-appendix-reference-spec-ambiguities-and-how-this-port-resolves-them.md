## 11. Appendix: Reference-Spec Ambiguities and How This Port Resolves Them

Tensions, gaps and internal inconsistencies in `product-spec.md` surfaced by this exercise, each resolved rather
than worked around silently, so a future reader can tell a deliberate reading from an oversight. Entries 1–21
re-examine, in the same order and under the same numbers, the ambiguities the Ruby port recorded; several dissolve
on .NET, three reverse in whole or part, and the rest carry over with a .NET mechanism. Entries from 22 on are new to
this port.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

1. **CFG-15's blocking sleep versus RETRY-26's no-carrier-pinning.** *Dissolves on .NET.* The tension exists where a
   blocking sleep might pin a lightweight carrier; .NET has no virtual threads or fibers for a sleep to pin. The
   async path waits with `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, a timer that holds no thread and
   aborts promptly on cancellation (verified: 53 ms for a delay cancelled at 50 ms); the sync path blocks the calling
   thread, which is what the caller of a sync API asked for, on a `TimeProvider` timer and a token-aware wait, never
   on a shared pool thread (§6.1, §8.3). One new edge replaces the old one: `Task.Delay` rejects anything above
   ~49.7 days, below **RETRY-18**'s 365-day clamp, so long waits are chunked (§6.1).
2. **NFR-11's "plain blocking operations" versus SEAM-17's pivot in core's public surface.** *Dissolves.*
   **NFR-11** targets third-party async-framework types; `Task`, `ValueTask`, `CancellationToken` and
   `IAsyncEnumerable<T>` ship with the runtime, so exposing them leaks no framework (§3.3). The sync seam remains a
   plain blocking operation, and — unlike the as-built tree — a real one rather than sync-over-async (§5.3).
3. **SEAM-2 enumerates exactly five seams; is retiring one a violation?** *Resolved as in Ruby*, by reading
   **SEAM-2** as constraining how a *retained* concern is exposed. On .NET the argument is stronger: `Stream`,
   `Memory<byte>` and `ArrayPool<byte>` are runtime-shipped, so the byte-stream provider seam retires by P2's test
   without the "stable across the supported range" narrowing Ruby needed (§3.1). Stated as an assumption because no
   clause grants it.
4. **CFG-1's system-property tier has no porting guidance, unlike RETRY-28.** *Carries over.* .NET has no
   JVM-style system properties; the nearest analogues, `AppContext` switches and `runtimeconfig.json` knobs, are
   runtime settings, not application configuration. *Resolved* (§8.2): a genuinely different source takes the
   third position — `appsettings.json` and its environment overlay, which every default .NET host places below
   environment variables — so override > environment > third source > default is preserved without routing the
   environment through a second name (P11). The substitution is §10 entry 25. The inconsistency in the
   specification — anticipating ports in one place and not the other — stands.
5. **SEAM-30's conformance step presumes externally pre-emptible futures.** *Carries over, with a better
   mechanism.* A `Task` cannot be pre-empted from outside either; cancellation is cooperative through
   `CancellationToken`. The difference from Ruby is that the runtime's own transport honours the token inside the
   socket operation, so the producer's check-after-resume point is reached promptly. The invariant — an orphaned
   response closed exactly once, never surfaced — is tested with a fake transport observing the token (§3.3).
6. **HTTP-2/SEAM-29 presume enforceable constructor privacy.** *Dissolves.* C# has `private`/`internal`
   constructors enforced by the compiler and the runtime; only reflection bypasses them, which is outside any
   language's conformance model (§4).
7. **BODY-3's atomic compare-and-set is a property of the reference implementation, not of the requirement.**
   *Dissolves.* The requirement's text asks only for "fail loudly on a second write" and race safety; .NET has the
   reference's mechanism natively (`Interlocked.Exchange`/`CompareExchange`), so the port uses it and the question
   of whether the mechanism is normative never arises (§3.1).
8. **XCUT-23's resolution rule needs at least one instance after a seam retires.** *Changes shape.* .NET has no
   classpath discovery; transports and codecs are constructed explicitly or registered in a DI container. The
   obvious container is the obvious-but-wrong tool for **XCUT-23**'s "zero or multiple candidates … MUST fail
   loudly": `Microsoft.Extensions.DependencyInjection` resolves a single service to its *last* registration and
   silently ignores earlier ones (documented behaviour). The DI package must detect multiple registrations of the
   SDK's seams itself (§3.6, §8.2).
9. **Appendix B covers only nine of nineteen prefixes.** *Carries over.* Appendix B (PAGE, SSE, SERDE, OBS, CFG,
   TRANSPORT, ASYNC, XCUT, NFR) reads like a conformance definition but is not one. *Resolved* (§9.3): Appendix B
   conformance is stated as a strictly weaker claim than full conformance; the conformance kit reports per
   requirement ID, with the Appendix B items as a many-to-one view whose status is the worst of their assertions.
10. **RETRY-38's modal tag contradicts its prose; RETRY-45 is the only MUST NOT.** *Carries over unchanged*:
    **RETRY-38** is treated as SHOULD and deferred; **RETRY-45** is indexed as a MUST.
11. **Embedded MUSTs inside SHOULD-tagged requirements** (**CTX-16**, **CTX-20** and others). *Carries over*: the
    feature is optional, its behaviour is not. Where the port ships the feature — **CTX-20**'s no-op tracer is
    `ActivitySource` returning `null`, shipped — every embedded MUST is implemented (§5.4).
12. **Four reference sync/async drifts pushed onto the porter** (**BODY-8**, **RECOV-14**, **RETRY-34**,
    **AUTH-31**). *Resolved as in Ruby*, toward the stricter, uniform behaviour, and
    made structurally undriftable on .NET: each shipped policy has one implementation serving both paths through a
    `ProcessCoreAsync(…, bool async)` core, so a sync/async difference would have to be written deliberately
    (§5.2, §5.3).
13. **Citation hygiene.** *Carries over*: "HTTP-16-body" is cited as **BODY-16**; **SEAM-29** is defined three times;
    **RETRY-1**/**RETRY-6** are summarised before their canonical text. See also entry 33.
14. **SEAM-6 and SEAM-8 describe overlapping scenarios.** *Dissolves*: both describe an install/replace apparatus
    that this port does not have for any seam. The byte-stream provider retires (entry 3), and for the transports
    and the codec explicit construction has no install call, while under DI `TryAdd` plus the single-registration
    validator give **SEAM-6**'s same-instance and different-instance branches and **SEAM-8**'s replace-after-hand-out
    scenario has no auto-resolved provider to replace (§3.6, §10 entry 9). There is no prior-state table to draw.
15. **Clauses with no host manifestation.** *Reverses in part.* On Ruby, **CFG-34**'s boxed-versus-primitive array
    distinction was inapplicable; on .NET it is live — `double[]` and `object[]` are distinct array kinds — and so is
    its floating-point clause, which .NET's default equality gets half wrong. Verified on .NET 10.0.401:
    `double.NaN.Equals(double.NaN)` is `true` (as **CFG-34** requires) but `0.0.Equals(-0.0)` is also `true`, where
    **CFG-34** requires +0.0 and −0.0 unequal. The deep-equality helper therefore compares sign explicitly for zeros
    (§8.2). **SERDE-11** (unchecked serde failures) is satisfied by the language, which has no checked exceptions.
    **SERDE-14**'s covariance SHOULD becomes live too: C# supports variance only on interfaces and delegates, not on
    the struct a tri-state would naturally be; §7.3 keeps an invariant struct, recorded as §10
    entry 21.
16. **PIPE-32's sync/async redirect asymmetry.** *Reversed* (entry 29). Ruby preserved it; this port's async path
    is primary, so both standard pipelines follow redirects, recorded as §10 entry 14.
17. **The SSE preamble offers a strict-WHATWG mode as an alternative to replicating the deviations.** *Carries over,
    sharpened*: the deviations are replicated. On .NET the question is concrete, because the
    runtime ships the strict mode: `System.Net.ServerSentEvents.SseParser` implements WHATWG semantics and, verified
    in §7.2, violates **SSE-6**, **SSE-8**, **SSE-10**, **SSE-13**, **SSE-14** and **SSE-16** on the specification's
    own fixtures. Core ships its own parser; a `SseItem<T>` adapter for callers who want WHATWG semantics is a
    possible convenience, not a mode.
18. **SERDE-26 presumes a mutable codec engine; TRANSPORT-18 presumes a re-subscribable body producer.** *Reverses:
    both are live on .NET.* `JsonSerializerOptions` is mutable until first use and then frozen — verified on .NET
    10.0.401, a mutation after one `Serialize` throws `InvalidOperationException`, while the copy constructor `new
    JsonSerializerOptions(options)` yields an independent mutable copy — so wiring tri-state converters into a
    caller's instance would either mutate it or fail, and **SERDE-26**'s private-copy branch is the required path,
    always available (§7.3). And `HttpContent` is re-serialised whenever something below the SDK re-sends a request:
    verified, a `DelegatingHandler` that calls `base.SendAsync` twice re-invokes the content's serialisation, which
    for a single-use body raises `StreamConsumedException`. `SocketsHttpHandler`'s own credential and proxy
    challenge handling re-sends the same way (documented behaviour, not exercised here), so **TRANSPORT-18** binds
    the SystemNet transport (§3.2).
19. **The retry-unification sanction is stated twice with different scopes**.
    *Carries over*: both quoted separately, neither invoked, because the port keeps both stacks (§6.1). The
    recommendation to consolidate stands.
20. **RECOV-31 and RETRY-38 are the same feature under two IDs at two modal levels.** *Carries over*: one deferred
    feature, listed under both IDs (§12).
21. **ASYNC-21 is a MUST whose antecedent is an optional adapter.** *Carries over.* **ASYNC-21** (and several
    TRANSPORT-* clauses) are MUST-tagged but conditioned on an adapter the port may not ship ("An adapter exposing a
    streaming source (SSE) as a reactive stream MUST ..."), so a port without one satisfies them vacuously, invisibly
    in a MUST count. *Resolved* (§7.2, §9.3): such items are reported as adapter-scoped and vacuous, never as
    satisfied or deferred. No reactive adapter ships in the MVP — the `IObservable<T>` bridge is an optional package
    (§2.2) — and the property **ASYNC-21** protects, one poll per unit of demand, is implemented anyway on the
    pull-based `IAsyncEnumerable<T>` reader (**SSE-39**), which any later bridge inherits.
22. **The idempotent set excludes TRACE, which RFC 9110 calls idempotent.** **RETRY-6** fixes the set as exactly
    `{GET, HEAD, OPTIONS, PUT, DELETE}`; the as-built `Method.IsIdempotent` follows the RFC and includes TRACE.
    *Resolved*: the specification's set is the only classification. **HTTP-9** leaves no room for a second,
    RFC-meaning property beside it — "There is no separate safe-method classification, and the idempotent set is an
    internal constant rather than a public accessor on the method type" — so TRACE is dropped, the set lives in the
    internal `RetryFacts`, and the public `IsSafe`/`IsIdempotent` properties are removed before 1.0 (§4.3, §6.1).
23. **"The runtime's I/O-error family" presumes HTTP failures are I/O errors**. **XCUT-4**(b) and
    **RETRY-2** read naturally on a runtime whose HTTP stack throws I/O errors. On .NET neither
    `HttpRequestException` nor `SocketException` is an `IOException` (verified). *Resolved*: classification reads the
    family as `IOException`, `HttpRequestException` without a status, `SocketException` and `TimeoutException`; the
    wrapper-type clause is a deviation, §10 entry 7 (§6.1).
24. **No clause covers a second retry engine below the transport**. The specification
    assumes the SDK's retry is the only one. The connection-layer split (Porting Method) invites an enterprise's Polly
    handler chain
    underneath, and two engines multiply sends and re-send bodies the SDK's safety gate never saw (entry 18's
    verification). *Resolved*: one retry layer per call path, the SDK's; nothing the SDK creates adds a retrying
    handler, and composing a resilience handler without its retry strategy is documented (§6.1).
25. **The HTTPS guard has no loopback exemption**. **XCUT-16** and **AUTH-28** are silent on
    `http://localhost`, and a reader could assume one. *Resolved*: no exemption, matching Azure.Core's bearer
    policy; local development uses HTTPS via `dotnet dev-certs` (§6.3).
26. **AUTH-11's "MUST propagate" meets AUTH-37's "non-fatal" refresh**. A synchronous
    refresh in the expiring-but-valid zone — the as-built cache — sits between them. *Resolved*: propagation applies
    when no valid token remains; in the expiring-but-valid zone the valid token is stamped and the refresh runs in
    the background, its failure observed and logged, never cached (§6.3).
27. **CFG-31 rejects date forms RFC 9110 obliges recipients to accept**. "A malformed
    header missing the comma after the weekday MUST fail" excludes RFC 850 and asctime dates, which RFC 9110 §5.6.7
    says recipients MUST accept and which the BCL's `RetryConditionHeaderValue` does accept (verified). *Resolved*:
    follow the specification — a rejected date is "no hint", so the retry falls back to backoff and can never wait a
    wrong amount (§6.1). Recommended to the specification author: state the intent explicitly.
28. **"Tolerant of an informational weekday" has two readings**. **RETRY-15** alone could
    mean "tolerates the weekday's presence"; **CFG-30** settles it — the weekday "MUST NOT be validated against the
    actual date". The BCL's parser validates it (verified: `Mon, 06 Nov 1994` is rejected). *Resolved* by
    **CFG-30**'s reading (§6.1).
29. **The async redirect rule is a MUST NOT with three sanctioned escapes**. **PIPE-32**
    states a reference limitation as normative; **REDIR-25** then lists "(c) let callers install their own async
    redirect step". *Resolved*: an async redirect step is sanctioned; the standard preset installing it is judged,
    §10 entry 14 (§5.3, §6.2).
30. **The context store has no stated reader**. **CTX-7**–**CTX-13** and
    **CTX-17**–**CTX-19** mandate a bounded process-wide store keyed by call key without saying who looks contexts
    up, so a port cannot tell substance from scaffolding (P11). *Resolved*: kept, with a .NET reader — a
    `DelegatingHandler` below the transport resolves the live call from a `CallKey` stamped into
    `HttpRequestMessage.Options` (§5.4).
31. **REDIR-11's marker has a trap the requirement itself flags.** "Only the auth step strips the marker, so a
    pipeline with no auth step … forwards the internal marker to the transport; a robust port should strip the
    signal independently." *Resolved* by removing the marker: redirect and auth both compare against the call's
    immutable seed origin, so there is nothing to strip, §10 entry 15 (§6.2).
32. **XCUT-2's "timeout type is a subtype of the cancellation type" is the .NET case, inverted.** On .NET the
    runtime reports an `HttpClient.Timeout` as the cancellation type (`TaskCanceledException`) carrying the timeout
    type as its *inner* exception (verified), not as a subtype. The requirement's rule — decide by the ambient
    cancellation state, timeout branch first — covers it anyway. *Resolved*: every classification tests the call's
    token, never the exception type (§5.2, §6.1).
33. **RECOV-17 to RECOV-34 are stated only in Appendix C**. Eighteen requirements —
    capability-based retry classification, budgets, pacing, the idempotency-key and client-identity steps, retry
    configuration validation — have no chapter body. *Resolved*: cited by ID against Appendix C only and never
    attributed to `08-execution-pipelines.md`; recommended to the specification author for §8.2.
34. **HTTP-42 is silent on a byte-order mark in a text body**. On .NET the question is live in
    both directions: `new StreamWriter(stream, Encoding.UTF8)` writes a BOM the server did not ask for, and
    `Encoding.UTF8.GetString` does not strip one, so a BOM-prefixed response decodes to a string starting with
    U+FEFF (both verified in §3.1). *Resolved*: core never encodes through a BOM-emitting writer, and decoding strips
    a leading BOM only when it matches the resolved charset's preamble (§3.1).
35. **The projection seam does not say what to do with a path-parameter value of `.` or `..`**
   . **SEAM-27** forbids a value injecting extra `/` segments, but `System.Uri` removes dot
    segments — including percent-encoded `%2E%2E` — so a correctly encoded `..` escapes its segment by removal rather
    than insertion (verified in §3.5). *Resolved*: the projection rejects a value that is exactly `.` or `..` with an
    `ArgumentException` naming the placeholder (§3.5). The same canonicalisation applies to a redirect `Location`,
    where RFC 3986 resolution requires it and no caller-supplied value is involved (§6.2).
36. **HTTP-17's header-name rules are a floor; may a port be stricter?** **HTTP-17** rejects blank names, C0
    controls, DEL and non-ASCII and trims surrounding whitespace, but does not say whether a port may reject more.
    RFC 9110's token grammar also rejects separators such as space, `(` and `:`, and `HttpClient`'s
    `TryAddWithoutValidation` refuses such names anyway (verified: `"a b"` returns `false`, which the as-built adapter
    then drops silently). *Resolved* (§4.1): names are trimmed and then validated against the RFC 9110 token grammar,
    a superset of **HTTP-17**'s rejections, on both the outbound and the inbound path (**HTTP-19** keeps inbound names
    strict), so a name the transport could never emit fails at the model with a clear error instead of disappearing
    at the transport (**HTTP-21**, **TRANSPORT-12**).
37. **XCUT-20's "never throw" meets OBS-20's and OBS-30's unwrapped listener calls.** **XCUT-20** says
    "Observability code paths (redaction, event emission, span/metric recording) MUST NEVER throw into the caller's
    request path"; **OBS-20** says the runtime does not defensively wrap tracer or metrics calls, and **OBS-30** puts
    the burden on callbacks never throwing. .NET does not guard them either: a throwing
    `ActivityListener.ActivityStarted` propagates out of `StartActivity` (verified). *Resolved* (§8.1): the SDK's own
    recording code — tag computation, redaction, rendering — is total and never throws; log emission is wrapped
    (**OBS-20**); listener callbacks (`ActivityListener`, `MeterListener`) are the **OBS-30** contract party and are
    not wrapped. **XCUT-20**'s "span/metric recording" is read as the SDK's code, not third-party listener code.
38. **OBS-32 names its instruments in one sentence and defers to OpenTelemetry in the next.** It names
    "`http.client.request.count` (unit `{request}`) and ... `http.client.request.duration` (unit `ms`)" and says names
    and units "SHOULD follow OpenTelemetry semantic conventions", whose stable HTTP client metrics use seconds and
    define no request-count instrument. *Resolved* (§8.1) by the governing clause: `http.client.request.duration` in
    `s`, no separate count (derivable from the histogram), plus `http.client.active_requests` — matching both
    OpenTelemetry and the .NET runtime's own `System.Net.Http` meter (verified: same names, unit `s`). Because the
    reference transport's meter emits the same names, the documentation tells consumers to enable one meter or the
    other; the SDK keeps the names on its `Dexpace.Sdk` meter because core is transport-agnostic.
39. **HTTP-46 says "body by value", but a single-use stream has no value.** *Added by dated correction, 2026-10-02
    (phase 2a, P2a-1).* **HTTP-46** asks request equality to compare the body "by value", yet a stream body can only
    be read once and reading it destroys it, and the requirement's own rationale forbids blocking work in `Equals`.
    *Resolved* (§4.2): equality is by value where the bytes are a construction-time fact and by identity where they are
    not. The in-memory variant behind `FromBytes`, `FromString`, `FromValue<T>` and `ToReplayableAsync()` compares
    content type and bytes (`SequenceEqual`), and its hash covers only the content type and length, so hashing stays
    O(1); the single-use stream variant and any unknown `RequestBody` subclass keep reference equality, and a body
    equals itself. `RequestBody`'s remarks state the rule so later body variants inherit it. A reading of the clause,
    not a departure from it.
40. **HTTP-48 admits obs-text in an `ETag`, and HTTP-18 forbids it in an outbound header value.** *Added by dated
    correction, 2026-10-02 (phase 2a, P2a-3).* `etagc` includes obs-text (`0x80`–`0xFF`), so `ETag.Strong("é")` is valid,
    but `Headers.Set` enforces **HTTP-18**'s outbound ASCII rule. *Resolved* (§4.4): the outbound rule wins. The tag
    parses and constructs, and `RequestConditions.ApplyTo` throws `ArgumentException` (naming the code point, never the
    value) when asked to send it. The chapters do not address the interaction.

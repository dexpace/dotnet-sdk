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
    `HttpRequestMessage.Options` (§5.4). *Correction 2026-10-07 (4a, P4a-13):* the route is open; 4c carries the key on `RequestOptions` and 8b stamps it.
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
    *Dated correction, 2026-10-03 (phase 3b, P3b-12):* "a leading BOM" is read for every encoding that has a preamble, not UTF-8 alone. `Encoding.GetString` keeps a BOM for
    all of them (`utf-16` `FF FE`, `utf-16be` `FE FF`, UTF-32 `FF FE 00 00` decode to a leading U+FEFF too), so both string readers call one internal routine that resolves the
    encoding (the declared charset, else UTF-8) and skips `encoding.Preamble` when the bytes start with it. A mark that does not match the declared charset is kept, because the
    declared charset wins (**HTTP-42**).
35. **The projection seam does not say what to do with a path-parameter value of `.` or `..`**
   . **SEAM-27** forbids a value injecting extra `/` segments, but `System.Uri` removes dot
    segments — including percent-encoded `%2E%2E` — so a correctly encoded `..` escapes its segment by removal rather
    than insertion (verified in §3.5). *Resolved*: the projection rejects a value that is exactly `.` or `..` with an
    `ArgumentException` naming the placeholder (§3.5). The same canonicalisation applies to a redirect `Location`,
    where RFC 3986 resolution requires it and no caller-supplied value is involved (§6.2).
    *Dated correction, 2026-10-02 (phase 2b, P2b-7):* the same removal applies to the template's literal text, so a
    template such as `/../admin` or `/%2e%2e/admin` would also escape the base path (verified: `https://h/c/%2E%2E/x`
    becomes `https://h/x`). The projection therefore also rejects a literal segment that is a dot-segment (`%2E` read as
    `.`) when the template is set, and rejects a rendered dot-segment at `BuildRequest` (reachable only by concatenating
    literal dots with an empty value, as in `/.{a}.`), the second as `InvalidOperationException`.
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
41. **SEAM-2 says core "never implements" a seam, but core must ship the bridges and `DelegateHttpClient`.** *Added by
    dated correction, 2026-10-02 (phase 2b, P2b-1).* Read to the letter, "never implements" forbids the two bridges
    **SEAM-18** requires, `DelegateHttpClient` (**SEAM-11**) and 4c's `HttpPipeline` (**PIPE-26**). *Resolved* (§3.2): core
    never supplies the external concern behind a seam (the I/O of a transport, the format of a codec) and never names a
    concrete supplier, but it may ship adapters that re-shape a caller-supplied implementation and perform no I/O. An
    architecture test (`SeamImplementationArchitectureTests`) makes the reading mechanical: every core type implementing a
    seam is non-public and on a named allow-list with a reason, and a new implementer needs an entry. A reading of the
    clause, not a departure from it.
42. **SEAM-22 asks for "full generic type capture", which a .NET generic method does not need.** *Added by dated
    correction, 2026-10-02 (phase 2b, P2b-2).* The type a codec decodes into is the generic argument itself, which the CLR
    binds to a closed type at run time, so an unresolved type variable cannot reach the codec through the generic API; the
    only door through which one could, a `Type`-taking overload, does not exist. *Resolved* (§3.4): the requirement is met
    by construction and is marked ✅, not N/A, with `SerdeSeamArchitectureTests.No_seam_member_takes_a_System_Type` as the
    tripwire and the `ContainsGenericParameters` rejection design §3.4 requires of any future `Type` overload.
43. **§3.3 says no SDK path abandons a `Task<Response>`, but §5.3 documents a mode in which the caller does.** *Added by
    dated correction, 2026-10-02 (phase 2b, P2b-4).* "Cancel without interruption" is the caller awaiting
    `task.WaitAsync(token)` while the blocking call runs on, so a response produced later has no receiver, which is
    **SEAM-30**'s orphan. *Resolved* (§3.3, §5.3): `AsAsync` checks the token after `Execute` returns, disposes a response
    produced once it is signalled, and completes the task cancelled, so the response is closed exactly once and never
    surfaces. The disposal is direct, so a throwing `Dispose` faults the task until phase 3b's `DisposeQuietly` replaces it.
    *Dated correction, 2026-10-03 (phase 3b, P3b-16):* the disposal is now quiet. `AsAsync` calls `Disposal.DisposeQuietly`, so a throwing `Dispose` no longer faults the task: it
    completes cancelled and the failure is reported on the current `Activity` (§3.3, §3.7).
44. **IO-10 asks `copyTo(offset, count)` to reject an out-of-range window, while IO-21 asks `slice(offset, count)` to accept an
    overflowing offset lazily.** *Added by dated correction, 2026-10-03 (phase 3a, P3a-9).* In the reference these are two
    operations on two types (a `Buffer` and a view). The port ships neither the `Buffer` (**IO-7**) nor a second windowing
    operation: it has one, `CapturedBytes.Slice`, whose consumers all want "give me what is there, up to this much". *Resolved*
    (§3.1): the single operation follows **IO-21**'s lazy rule, and **IO-10**'s copy and clear clauses retire with the
    `Buffer`. Neither clause's letter has a .NET subject, so this is a reading, not a §10 deviation.
45. **IO-14 says only "decoded as UTF-8", leaving malformed bytes and a byte-order mark open.** *Added by dated correction,
    2026-10-03 (phase 3a, P3a-11).* *Resolved* (§3.1): `Utf8LineReader` replaces a malformed sequence with U+FFFD, which is
    what `Encoding.UTF8.GetString` and WHATWG's decode rule do, rather than throwing, so one bad byte cannot kill an event
    stream; and it keeps a byte-order mark as ordinary content on every line, in both terminator modes. Phase 7b strips the
    leading BOM at stream start.
46. **Section 8.3 says "the recovery layer is synchronous", but the .NET async path is primary.** *Added by dated
    correction, 2026-10-07 (phase 4b, P4b-6).* *Resolved* (§5.2): the sentence describes the reference's primary runtime. The
    port gives every recovery step contract a required `Apply` and `ApplyAsync`, and every chain and the dispatcher one
    `bool async` body; the sync entry point reads the already-completed `ValueTask` through the internal `SyncPath`, the one
    sanctioned `ValueTask<T>.Result` read. The normative part of the paragraph, the two-layer prohibition, is kept: an
    architecture test forbids a `Recovery` to `Pipeline` reference (P4b-5). A reading, not a §10 deviation.
47. **`PIPE-2` and `PIPE-4` name five pillars and one pre-redirect slot, but §5.1's `Operation` stage is a sixth singleton outside
    it.** *Added by dated correction, 2026-10-07 (phase 4c, P4c-6).* *Resolved* (§5.1): `Operation` (the once-per-call deadline, and the
    operation span from phase 5c) sits outside `PerCall`. Both run outside the redirect and retry loops and see one response, so
    every boundary property `PIPE-2` and `PIPE-37` protect holds; making `Operation` a non-pillar would let two deadline
    policies nest, and folding it into `PerCall` would put the deadline inside the error mapping. A reading, not a §10 deviation.
48. **`PIPE-3` asks for configurable slots around the retry boundary, and says nothing about after the auth and logging
    pillars.** *Added by dated correction, 2026-10-07 (phase 4c, P4c-7).* *Resolved* (§5.1): `PerCall`, `PerHop` and `PerAttempt` are
    the user slots; there is none after `Auth`, `Diagnostics` or `Serde`. The keys are sparse (100 apart, with 150 and 250 between),
    so the first consumer that must run after authentication, a request signer, adds one (a `PostAuth = 550`) additively. `PIPE-3`
    is a SHOULD, met in part. A reading, not a §10 deviation.
49. **`CFG-31` is silent on a single-digit day, and `RETRY-15` requires it.** *Added by dated correction, 2026-10-07 (phase 5a, P5a-11).*
    *Resolved* (§8.2): `HttpDate` accepts a one- or two-digit day; `CFG-31` names blank input and a missing comma as its failures and
    does not demand two digits. The zone and month names are case-insensitive, a superset of **CFG-30**. A reading, not a §10 deviation.
50. **`CFG-23`'s globs disagree with curl's leading-dot convention.** *Added by dated correction, 2026-10-07 (phase 5a, P5a-14).*
    *Resolved* (§8.2): `NO_PROXY=.internal.example.com` is a literal glob that matches only that host, where curl and
    `HttpClient.DefaultProxy` read a domain suffix; write `*.internal.example.com`. The lead may add the suffix reading as a superset.
51. **`CFG-12` asks for a builder that is safe to share while unfinished; core has no configuration builder.** *Added by dated
    correction, 2026-10-07 (phase 5a, P5a-25).* *Resolved* (§8.2): an object initializer runs on an instance no other code can reach, so
    the clause is vacuous and the row is N/A.
52. **`RETRY-41` says to clamp a negative retry count to the default; `RECOV-34` and `HTTP-35` say to reject it.** *Added by dated
    correction, 2026-10-08 (phase 6a, P6a-13).* *Resolved* (§6.1): `RetryOptions` rejects a negative `MaxRetryAttempts` where it is set, so the
    clamp clause is vacuous; the row is met on its override and zero clauses.
53. **`RETRY-10` and `RETRY-15` speak of nanoseconds; `TimeSpan` has 100 ns ticks.** *Added by dated correction, 2026-10-08 (phase 6a,
    P6a-14).* *Resolved* (§6.1): a jitter width below one tick returns the base delay and a fractional `Retry-After` is honoured to seven
    decimal places.
54. **`RETRY-21` describes the stage stack's pacing headers as a caller-configurable ordered list.** *Added by dated correction, 2026-10-08
    (phase 6a, P6a-17).* *Resolved* (§6.1): both stacks use one fixed precedence; the configurability is `HonorRetryAfter`, an on/off switch
    for all four headers on the stage stack, ignored by the recovery stack.
55. **`RETRY-16` maps an "out-of-range" value to no hint; `RETRY-18` clamps computed deltas to 365 days.** *Added by dated correction,
    2026-10-08 (phase 6a, P6a-18).* *Resolved* (§6.1): "out of range" is outside the grammar or a date field's range; a well-formed numeral of any
    size saturates and is clamped, so a 30-digit `Retry-After` waits 365 days rather than 200 ms.
56. **`RECOV-19` classifies each RE-SENT attempt's response.** *Added by dated correction, 2026-10-08 (phase 6a, P6a-6).* *Resolved* (§5.2,
    §6.1): the dispatcher composition performs the initial send itself, so it classifies every send, and an initial 503 is retried whether
    or not `ErrorMappingStep` is installed.
57. **`REDIR-3` says "the ORIGINAL request method"; Node judges the current hop.** *Added by dated correction, 2026-10-08 (phase 6b, P6b-4).* *Resolved* (§6.2): the first request
    the policy drives is the original, and a 301 that follows an opted-in 303 is judged as the original `POST` was, so it is returned under the default set.
58. **`REDIR-20` says the predicate "fully overrides the built-in follow decision".** *Added by dated correction, 2026-10-08 (phase 6b, P6b-7).* *Resolved* (§6.2): it replaces
    eligibility (method, status, `FollowSeeOther`) only. Loop detection, the cap, the `Location` screen, the downgrade guard, the replay gate and credential stripping are MUSTs with
    no carve-out for a predicate, so a predicate can make the policy stricter and never looser.
59. **`PIPE-40` lists a "non-replayable body" among the abandon paths that return the in-flight response unclosed; `REDIR-6` and `REDIR-22`(b) fail and close it.** *Added by dated
    correction, 2026-10-08 (phase 6b, P6b-17).* *Resolved* (§5.2, §6.2): the list is read as the union over the three re-driving pillars. Retry declines a non-replayable body by
    surfacing the last outcome (returned open); redirect declines by throwing, so nobody receives the response and it is disposed first. `BODY-4` states the same split.
60. **The reference does not say what several `Location` values mean.** *Added by dated correction, 2026-10-08 (phase 6b, P6b-10).* *Resolved* (§6.2): two or more values are
    malformed and the 3xx is returned unfollowed; taking the first would let an intermediary that appends a field choose the target.
61. **`REDIR-28` says the malformed-`Location` event logs the raw string.** *Added by dated correction, 2026-10-08 (phase 6b, P6b-20).* *Resolved* (§6.2): the value goes through
    `UrlRedactor.RedactHeaderValue`, which is total over unparseable text; stricter than the letter, and the letter's own caveat about credential-bearing values is the reason.

62. **`AUTH-16`, `AUTH-17` and `AUTH-22` cannot all hold for a `-sess` challenge without `qop`.** *Added by dated correction, 2026-10-08 (phase 6c, P6c-31).*
    *Resolved* (§6.3): `AUTH-16` calls it satisfiable and `AUTH-17` folds the cnonce into HA1, but `AUTH-22` forbids sending the cnonce without `qop`, so the server could not
    verify the response (RFC 2617 has the same contradiction; RFC 7616 has no no-`qop` form). The handler declines it, which avoids a guaranteed `401` round trip and lets a
    composite fall through. Node sends the cnonce anyway; Ruby follows `AUTH-22` literally.
63. **`AUTH-28` says "on any request path where a credential will be attached"; a reactive scheme attaches one only after the first `401`.** *Added by dated correction,
    2026-10-08 (phase 6c, P6c-8).* *Resolved* (§6.3, §11 item 25): the guard also runs on the outbound pass of a reactive scheme (Digest, Basic on challenge), so a request
    is never sent in the clear to draw a challenge it may not answer. A Digest-configured call over `http://` to a server that never challenges now fails; that is a
    misconfiguration surfaced early, in keeping with item 25's no-exemption stance.
64. **`AUTH-8` and `AUTH-9` name a named-key credential this port never had.** *Added by dated correction, 2026-10-08 (phase 6c, P6c-19).* *Resolved* (§6.3): the two named-key
    clauses are vacuous. A second key type that stamps identically to `ApiKeyCredential` would duplicate it for no requirement; a consuming SDK that needs a name for signing owns
    that type.
65. **`AUTH-36` surfaces a `401` unchanged when the rejected request carried no `Authorization` header (cross-origin suppression); the same hazard exists for every scheme.**
    *Added by dated correction, 2026-10-08 (phase 6c, P6c-12).* *Resolved* (§6.3, §10 entry 15): a `401` on a cross-origin hop is returned without challenge handling for any scheme,
    so a foreign server cannot draw the caller's credential by answering `401`.
66. **`PAGE-19` says an absolute `Link` target "is used as-is"; each page is a fresh pipeline call whose auth policy trusts that page's own URL.**
    *Added by dated correction, 2026-10-09 (phase 7c, P7c-12).* *Resolved* (§7.1, §10 entry 15): the `Link` strategy ends the walk, quietly, on a target on another origin
    than the walk's **first** request (a scheme downgrade included) unless the caller sets `allowCrossOrigin`, and removes userinfo from a target it follows. Following the
    letter would let a hostile or compromised server harvest the bearer token with one header, because the authorization policies stamp a request that is same-origin with
    its own call's seed (§10 entry 15) and the next page's seed is the server-chosen URL. The origin compared is the first request's, not the response's, so a first page that
    redirected cannot launder an origin. Stronger than the letter, and recorded as a reading rather than a §10 deviation because a server cannot observe it except by not
    being followed (`PaginationLinkOriginTests`). `first-release.md` carries the asymmetry.

**Correction 2026-10-07 (phase 5c, P5c-10).** Item 38 as built: `http.client.request.duration` (histogram, `s`, OpenTelemetry's bucket advice
`0.005`, `0.01`, `0.025`, `0.05`, `0.075`, `0.1`, `0.25`, `0.5`, `0.75`, `1`, `2.5`, `5`, `7.5`, `10`, through `InstrumentAdvice<double>`, which
carries no `[Experimental]` diagnostic on SDK 10.0.401) with `http.request.method` (`_OTHER` for an unknown method), `server.address`, `server.port`,
`url.scheme` and, on completion, `http.response.status_code` and `network.protocol.version`, or `error.type` for an exception or a status of 400
or more; `http.client.active_requests` (up-down counter, `{request}`) with the four start attributes, the same list for `+1` and `-1`. There is no
count instrument and no operation-level instrument.

## 6. Retry, Redirect, and Authentication

All three pillars are built in some form at `d45e64b`, and all three were designed from the platform's idioms
rather than from the specification (the pre-roadmap native-first decision, Porting Method: the siblings inform *what* to
build, not *how*). This
chapter keeps what those idioms got right, reconciles each pillar against the requirement text, and treats the one
structural question the reference never had to answer: this SDK's pipeline sits *above* an `HttpClient` whose own
handler chain can already retry and already follows redirects, so the port must decide which layer owns each
behaviour and make the other one stand down.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

### 6.1 Retry

**Single-sourcing is structural, with one .NET caveat.** **HTTP-9**/**RETRY-6**'s idempotent set `{GET, HEAD,
OPTIONS, PUT, DELETE}`, **RETRY-1**'s classifier (408, 429, and 500–599 except 501 and 505), **XCUT-7**'s default
configurable set `{408, 429, 500, 502, 503, 504}` and **RETRY-13**'s backoff calculator live in one internal static
class, `RetryFacts`, as `FrozenSet<T>` constants and pure functions. A static in one assembly is one copy per load
context; a host that loads `Dexpace.Sdk.Core` into two `AssemblyLoadContext`s gets two copies, but of identical
constants, so nothing can drift. Two as-built facts disagree with the sets. `Method.IsIdempotent` is `IsSafe || PUT
|| DELETE`, and `IsSafe` includes TRACE, so the model's idempotent set has six members where **RETRY-6** says the
set "MUST be single-sourced and equal to" five; RFC 9110 does call TRACE idempotent, but the port follows the
specification's set everywhere, and `Method` keeps no public RFC-meaning accessor beside it (§4.3, §11
item 22). And `RetryPolicy` hardcodes its status set in a
private field, so **XCUT-7**'s "configurable" set and **RETRY-1**'s classifier do not exist as separate things yet.

**XCUT-5 versus XCUT-7 keeps two names.** The baked flag on a protocol error is computed once from the classifier
and is queryable (`HttpResponseException.IsRetryable`, not yet built), while the retry step consults the configured
`RetryOptions.RetryableStatusCodes` alone, authoritative in both directions (**RETRY-37**) — and a test asserts that
a configured set containing 501 retries a 501 whose baked flag is `false`.

**The open capability, and .NET's I/O family (P13).** **XCUT-6** requires a custom error that declares itself
retryable to participate "without editing the classifier — for such errors the classifier queries the capability".
.NET cannot duck-type a property, so the capability is an interface, `IRetryableError { bool IsRetryable { get; } }`,
which `SdkException` implements virtually (`ServiceRequestException` returns `true`, **XCUT-4**) and a third-party
transport's exception can implement without touching core. The non-capability branch is **RETRY-2**'s "an I/O error
or a timeout error" anywhere in the cause chain, walked with §5.2's cycle-safe enumerator — and here the obvious
.NET reading is wrong. Verified on .NET 10.0.401: neither `HttpRequestException` nor `SocketException` derives from
`IOException`. The runtime's own HTTP stack reports connection refused, DNS failure and peer reset as
`HttpRequestException` over a `SocketException`, so a classifier that tested `is IOException` would call the most
common transient failures non-retryable. The port's I/O family is `IOException`, `HttpRequestException` without a
status code, `SocketException` and `TimeoutException`, so every pre-response transport failure **RETRY-4** lists is
retryable at the condition level, and classification runs off the capability first, as **RECOV-17** (stated in
appendix C only, §11 item 33) requires. The same fact bends **XCUT-4**(b)'s "belonging to the
runtime's I/O-error family so existing I/O catch sites keep matching": the transport's wrapper,
`ServiceRequestException`, derives from `SdkException` and cannot also be an `HttpRequestException`, so an existing
`catch (HttpRequestException)` site does not match it. That is recorded as §10
entry 7; the original is preserved as `InnerException`, and the classifier
walks it. The requirement's "runtime's I/O-error family" presumes a runtime whose HTTP failures are I/O errors; §11
item 23 records the reading.

**Cancellation versus timeout, verified end to end (XCUT-1, XCUT-2).** The runtime represents both with one type,
exactly the case **XCUT-2** anticipates: "even when the timeout type is a *subtype* of the cancellation type — the
timeout branch must be checked first". Verified on .NET 10.0.401 against a local server: `HttpClient.Timeout`
raises `TaskCanceledException` with an inner `TimeoutException`; cancelling the caller's token raises
`TaskCanceledException` with no `TimeoutException` in the chain. The as-built SystemNet transport already tells
them apart the way **XCUT-2** requires — by the token's state, not the exception's type — and maps the first to
`ServiceRequestTimeoutException`, which the as-built retry policy retries (verified: a 200 ms `HttpClient.Timeout`
against a 3 s handler produced three server hits for two configured retries, then surfaced
`ServiceRequestTimeoutException`). Cancellation is never caught by the retry filter (**XCUT-1**, **RETRY-23**), and
because a cancelled token cannot be un-cancelled, "restore the cancellation flag" is free (§5.2).
`ThreadInterruptedException` is treated as cancellation. **P13 — the overall deadline masquerades as
cancellation.** `OperationPolicy` implements `OverallTimeout` with a linked `CancellationTokenSource`, so when it
fires the transport sees a cancelled token and rethrows `OperationCanceledException`. Verified: a 300 ms
`OverallTimeout` against a slow server surfaces `TaskCanceledException` to a caller who never cancelled — a timeout
reported as cancellation, the confusion **XCUT-1** forbids. `OperationPolicy` must catch the cancellation when *its*
source fired and the caller's token did not, and throw a non-retryable timeout `SdkException` carrying it as
`InnerException`; nothing retries it, because the policy is outermost. `DexpaceClientOptions.AttemptTimeout` is
declared but read by nothing; it belongs in the retry policy as a per-attempt linked source, classified the same
way but retryable (**XCUT-2**).

**Retry safety (XCUT-10, RETRY-5, RETRY-7).** A request is re-sendable iff it has no body and an idempotent method,
or has a replayable body. The as-built predicate is `(body is null || body.IsReplayable) && (method.IsIdempotent ||
RetryNonIdempotentWhenReplayable)`, which is wrong in both directions: by default a POST with a replayable body is
*not* retried although **RETRY-5** makes it re-sendable, and with the option on a *bodyless* POST *is* retried,
which **XCUT-10**(a) forbids outright ("a bare POST MUST NOT be retried even when the failure is a transport error
that never reached the server"). The port drops the option and implements the rule as written.

**Configuration validates at construction** (**RECOV-34**, appendix C only). .NET `TimeSpan` is a 64-bit tick count
(100 ns), so the reference's ~292-year nanosecond ceiling becomes `TimeSpan.MaxValue` (~29,000 years), and the port
validates against the specification's bound explicitly rather than inheriting a different one from the type. The
multiplier is at least 1.0, the jitter fraction in [0, 1], retries non-negative with 0 disabling (**RETRY-41**), and the
status and method sets are copied into `FrozenSet<T>` when the pipeline is built, so a caller who mutates the
collection they configured cannot change a running client. As built, `RetryOptions` is an unvalidated mutable
class read live on every call (§8.2).

**Backoff follows the specification, not the as-built choice.** The pre-roadmap design left the jitter formula (full
or decorrelated) open, and the policy PR #6 built chose full jitter — uniform over `[0, min(base × 2^attempt, max)]` —
which violates **RETRY-9** (`initialDelay × multiplier^(attempt−1)`) and **RETRY-10** (symmetric jitter over
`[d(1−j/2), d(1+j/2)]`). The defaults also differ from **RETRY-12** (200 ms, 2.0, 8 s, 0.2, three sends): as built,
`MaxDelay` is 30 s and `MaxRetryAttempts` is 3, which is *four* sends, breaking **RETRY-14**'s equivalence of three
sends across the two stacks. The port keeps Polly's option name — in `Polly.RetryStrategyOptions`
`MaxRetryAttempts` counts retries, not sends, and a .NET reader will read it that way (P14) — and sets it to 2.
Overflow saturation (**RETRY-11**) is done in ticks before any `TimeSpan` is constructed, as the as-built code
already does for its own formula. The random source is injectable (`Func<double>`, defaulting to
`Random.Shared.NextDouble`) beside the injectable `TimeProvider`; the pair is what makes a backoff test
deterministic. **P13 — `Random` is not thread-safe, and fails silently.** Verified on .NET 10.0.401: one seeded
`Random` shared across `Parallel.For` returned 0 on 1,995,605 of 2,000,000 calls once its state was corrupted, while
`Random.Shared` returned no zeros in a million concurrent draws. A test that injects `new Random(42)` into a shared
policy gets a flat zero jitter under load rather than an exception, so the injectable is a `Func<double>` and the
test helper wraps a seeded generator in a lock.

**The clock is `TimeProvider` (P2).** **CFG-15**'s time seam retires into the runtime: `TimeProvider` ships in the
shared framework from .NET 8, with `GetUtcNow()` as the wall clock, `GetTimestamp()`/`GetElapsedTime()` as the
monotonic counter (**CFG-16**), and `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` as the non-blocking
wait; tests use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, a test-only package. §8.3 owns
the seam; retry consumes it, as the as-built `RetryPolicy`, `SetDatePolicy` and `AccessTokenCache` already do.

**The inter-attempt wait (RETRY-26, XCUT-3).** On the async path the wait is free (P7): `Task.Delay` over
`TimeProvider` is a timer, pins no thread, and on cancellation disposes its timer and faults with
`TaskCanceledException` — verified: an hour-long delay cancelled after 50 ms completed in 53 ms. **P13 — `Task.Delay`
has a ceiling far below the specification's.** Verified on .NET 10.0.401: `Task.Delay` accepts 49 days and throws
`ArgumentOutOfRangeException` for 50 days, with or without a `TimeProvider`, because the timer's limit is
`uint.MaxValue − 1` milliseconds (~49.7 days). **RETRY-18** clamps pacing deltas to 365 days, so a conforming parser
hands the wait a value the wait rejects. Verified against the as-built pipeline: `Retry-After: 5184000` (60 days)
makes `SendAsync` throw `System.ArgumentOutOfRangeException` — an unclassified exception from a retry decision,
which also breaks **RETRY-22**'s rule that pacing must never mask the real failure. The port's one
`TimeProviderWaits.DelayAsync` (*dated correction, 2026-10-07, phase 5a, P5a-7: it was named `RetryWait.DelayAsync`*) performs any delay above 49 days as successive bounded waits (at most eight for 365 days).
On the sync path the wait blocks the caller, which is what a sync call means: a `TimeProvider.CreateTimer` that
sets a `ManualResetEventSlim`, waited with the call's token, never `Thread.Sleep` and never `Task.Delay(...).Wait()`.

**Pacing headers need a hand-written parser, because the BCL's is both too strict and too lenient (P13).** Verified
on .NET 10.0.401 — the as-built parser, `DateTimeOffset.TryParseExact(value, "r", …)`, is **case-sensitive**
(rejects `sun, 06 nov 1994 08:49:37 gmt`, where Ruby's `Time.httpdate` accepts it), rejects the single-digit day,
and rejects `UTC` and `+0000` zones — all four of which **CFG-30** and **RETRY-15** require be accepted. The
tempting alternative, `System.Net.Http.Headers.RetryConditionHeaderValue.TryParse`, accepts the single-digit day and
both zones but still rejects lower case, rejects a date whose weekday is inconsistent (`Mon, 06 Nov 1994`, where
**CFG-30** says the weekday "MUST NOT be validated against the actual date"), rejects fractional delta-seconds
(`1.5`, which **RETRY-15** requires), and *accepts* the RFC 850 and asctime forms that **CFG-31** requires to fail
because neither starts with the three-letter-plus-comma weekday prefix, although RFC 9110 obliges recipients to
accept them (§11 item 27). Numeric parsing has its own traps: `int.TryParse` with
`NumberStyles.None` correctly rejects `+5`, ` 5`, `1e3` and `0x10` and — unlike Ruby's `Integer` — reads `08` as 8,
not octal; `double.TryParse` with `NumberStyles.Float` accepts `NaN`, `Infinity` and `1e3`, which **RETRY-19**
requires be screened out; and `double.TryParse("1.5", …)` under `de-DE` returns **15**, because the current
culture's decimal separator is a comma. The port therefore ships one span-based RFC 1123 parser shared with
**CFG-29**– **CFG-31**, one strict decimal screen (`digits [ "." digits ]`) applied before any numeric parse, and
parses with `CultureInfo.InvariantCulture` explicitly — never `null`, which is what the as-built call passes and
which means the current culture. The screen also covers `retry-after-ms`, `x-ms-retry-after-ms` and
`X-RateLimit-Reset` (**RETRY-15**), none of which the as-built parser reads. Every delta is clamped to 365 days in
ticks before a `TimeSpan` is built, since `TimeSpan.FromSeconds(1e20)` throws `OverflowException` (verified). The
rest of the pacing contract translates directly: a past date yields zero, distinct from unparseable yielding no hint
(**RETRY-17**); a hint replaces the schedule and gets no symmetric jitter (**RETRY-20**); the stage stack resolves
caller override → headers → fixed delay → exponential (**RETRY-39**); and a parse failure never masks the upstream
failure (**RETRY-22**).

**Two stacks, and why the unification sanction is not invoked.** As in Ruby, the port keeps both retry stacks
because it keeps both layers (§5): the stage `RetryPolicy`, which imposes no total-timeout budget (**RETRY-28**),
and the recovery-chain retry with its deadline-shrinking budget (**RETRY-27**), which is not built. The sanction is
quoted twice with different scopes — **RETRY-28** in `09-retry-and-resilience.md` ("a port that unifies the stacks")
and the pipelines chapter `08-execution-pipelines.md` ("a port unifying retry entry points") — and this port invokes
neither (§11 item 19). `OperationPolicy.OverallTimeout` is not a unification: it is a
whole-call deadline enforced by cancellation, outside both loops, off by default. Both stacks share `RetryFacts`,
the pacing parser and `RetryWait`, and **RETRY-14**'s three-sends equivalence is asserted by test.

**The layer underneath: Microsoft.Extensions.Http.Resilience (the connection-layer split).** The pre-roadmap
connection-layer split (Porting Method) routes connection-level resilience into the transport's `HttpClient`, so that
an enterprise's existing `DelegatingHandler` / Polly chain composes **underneath** the SDK. The specification has no
clause for a second retry engine below the transport, and
composing two unchanged produces two failures (§11 item 24). **Multiplied sends**: the SDK's
three sends each become up to four sends of a standard resilience handler's retry strategy, so one logical call can
reach a flaky server a dozen times, with two independent backoff schedules and pacing honoured twice. **A re-send
the SDK's safety gate never saw**: a handler retry re-serialises the `HttpContent`, which for a single-use body is
the second write **BODY-3** forbids. Verified on .NET 10.0.401 with a minimal `DelegatingHandler` that calls
`base.SendAsync` twice under the as-built SystemNet transport: a `RequestBody.FromStream` body surfaces
`StreamConsumedException` on the second send. The body failed loudly, as designed — but the handler's retry had
already bypassed **XCUT-10**'s rule that a single-use body "MUST NOT be re-sent", and a handler configured to retry
POST would do the same for a non-idempotent request. The rule the port adopts is **one retry layer per call path,
and it is the SDK's**, because only the SDK sees idempotency keys, replayability, typed errors and the configured
status set. The transport cannot inspect a caller-supplied `HttpClient`'s handler chain (`HttpMessageInvoker` keeps
it private), so enforcement is by construction and documentation: the SDK-created client and
`Dexpace.Sdk.Extensions.DependencyInjection` register no retrying handler; the DI package's documentation directs
consumers of `AddStandardResilienceHandler` to either set the SDK's `MaxRetryAttempts` to 0 or build a resilience
handler without a retry strategy, keeping the circuit breaker, rate limiter and attempt timeout, which the SDK does
not replicate. (The package is not in this machine's offline NuGet cache, so its default strategy composition is
cited from its documentation, not verified here.)

The rest of the loop translates without incident. A retryable response is disposed *before* the wait, with pacing
read from it first (**RETRY-35**; built). The failed-attempt trail is attached with §5.2's helper and discarded on
success (**RETRY-34**; not built — the as-built exception path discards prior failures silently). Each attempt
re-drives with the same request the policy received, which §5.1's signature makes structural (**RETRY-44**). The
async loop is a `while` loop inside one `async` method, so N retries reuse one state machine and build no chain of
continuations (**RETRY-30**) — free unless someone writes the retry as recursion, which the house style's
no-recursion rule already forbids. An `async` method faults its task on every terminal path (**RETRY-33**, free),
and the loop checks the call's token before each attempt and disposes any response that arrives after it fired
(**RETRY-32**). `TimeProvider` is never disposed by the SDK (**RETRY-45**).

**As built (d45e64b):** built — diverges: full-jitter formula and non-spec defaults (four sends); hardcoded status
set; no classifier or `IsRetryable`; safety predicate wrong both ways; `Retry-After` parser case-sensitive, integer-
only, unclamped (60-day hint throws); no `-ms`/rate-limit headers; no suppressed trail; overall deadline surfaces
as cancellation; `AttemptTimeout` unused; no recovery-chain stack.

**As built (2026-10-08, phase 6a):** built. `RetryFacts` moved to the internal `Dexpace.Sdk.Core.Resilience` namespace (P6a-4), which holds the
classifier, the one backoff calculator (`RetryBackoff`), the one pacing parser (`RetryPacing`), `RetryBudget` and `RetryEngine`. The text
above stands as written, with six *dated corrections*:

- **One loop under both stacks (P6a-3).** "Both stacks share `RetryFacts`, the pacing parser and `RetryWait`" reads "… and one loop":
  `RetryPolicy` and `RetryRecovery` are adapters over the same `RetryEngine`. This single-sources the loop; it does not unify the stacks,
  so §11 item 19 stands (the budget stays recovery-only).
- **The classifier's capability widens and never vetoes (P6a-8).** An `HttpResponseException` anywhere in the cause chain is decided by
  `RetryOptions.RetryableStatusCodes` alone; otherwise a `true` `IRetryableError` or the I/O family anywhere in the chain is retryable.
- **`OperationTimeoutException` (P6a-24).** An expired `OverallTimeout` throws it (non-retryable, the inner trail copied onto it);
  `OverallTimeout` and `AttemptTimeout` accept `null` or `(0, 49 days]`.
- **`AttemptTimeout` is cooperative, not abandoning (P6a-23).** A per-attempt linked token source on the `TimeProvider`, mapped to a
  retried `ServiceRequestTimeoutException`; no `LateResult`.
- **The recovery stack's position (P6a-5).** `RetryRecovery` is configuration composed by `RecoveryDispatcher` (request chain once;
  transport and response steps per send; recovery steps once), not an `IRecoveryStep`; every send's surviving response in the configured
  set is classified (P6a-6).
- **The random source is injectable through internal constructors only (P6a-27)**, not a public parameter.

### 6.2 Redirect

**P13 — the transport follows redirects by default, and does it wrong.** `HttpClientHandler.AllowAutoRedirect` and
`SocketsHttpHandler.AllowAutoRedirect` both default to `true` (`MaxAutomaticRedirections` 50), and the as-built
`SystemNetHttpClient()` constructor creates `new HttpClient()` with that default. Verified on .NET 10.0.401 against
a local `HttpListener` and a TLS `TcpListener`, the runtime's redirect handler:

| Behaviour | Handler default | Requirement |
|---|---|---|
| 300 with `Location` | followed | **REDIR-2**: MUST NOT be followed |
| 301/302 on POST | re-issued as GET, body dropped | **REDIR-3**: no automatic POST→GET rewrite |
| 303 on PUT | followed as GET by default | **REDIR-5**: not followed unless opted in |
| `Authorization` | stripped on every hop | **REDIR-7**: satisfied |
| `Cookie`, `Proxy-Authorization` cross-origin | **forwarded** | **REDIR-9**: MUST be stripped |
| `https` → `http` | not followed; the 302 is returned silently | **REDIR-15**: fail with a clear error |
| `Location` with `user:pass@` | followed; userinfo not sent as a header but kept in `RequestMessage.RequestUri` | **REDIR-12**: drop before re-issue |
| self-loop | not detected; the handler keeps following and finally returns the 302 | **REDIR-16**: detect the revisit |

And because the handler follows before the SDK sees anything, the SDK's policy is dead code. Verified against the
as-built assemblies: a pipeline holding `RedirectPolicy` over the default `SystemNetHttpClient`, sent a `GET` to a
300, returned the final 200 while a probe below `RedirectPolicy` saw exactly one drive. Two consequences follow.
The SDK-created client must be `new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })`, and the DI
package configures the primary handler the same way. A caller-supplied `HttpClient` cannot be inspected, but a
follow can be *detected*: verified, the handler rewrites `response.RequestMessage.RequestUri` in place to the final
URI. The transport compares it with the URI it sent and, on a mismatch, disposes the response and fails with a
configuration error naming `AllowAutoRedirect` — loudly, because by then a cookie may already have crossed an
origin, and a silent success would hide that.

**PIPE-32 is reversed** (§5.3, §10 entry 14, §11 item 29): one
`RedirectPolicy` serves both paths.

**Codes and methods** (**REDIR-1**–**REDIR-6**) translate literally, and the as-built follower departs from almost all
of them; it copied the handler's browser semantics rather than the requirements. Verified against the as-built
policy with auto-redirect off: a POST to a 302 arrives at the target as a GET with no body (**REDIR-3**). 303 is
always followed (**REDIR-5**), there is no allowed-method set (**REDIR-3**/**REDIR-4**, default `{GET, HEAD}`, held
as a `FrozenSet<Method>` copy per **REDIR-26**), a non-replayable body silently returns the 3xx instead of failing
with an error naming replayability (**REDIR-6**), and a rejected downgrade silently returns the 3xx instead of
failing (**REDIR-15**). `MaxRedirects` defaults to 20 where **REDIR-17** says 3.

**Resolution and wire-exactness (REDIR-13, REDIR-14).** `Uri.TryCreate(current, location, out next)` is the right
tool, and verified on .NET 10.0.401 it resolves per RFC 3986 — dot segments removed, as §5.2.4 of the RFC requires
of resolution — while preserving `%2F` in the path, `%26` in the query, the fragment's escapes, bracketed IPv6
literals and explicit ports. `Uri` does normalise one thing: a percent-encoded *unreserved* character (`%41`) is
decoded to `A`, which RFC 3986 §6.2.2.2 defines as equivalent, so no wire meaning changes. **P13 — the obvious way
to print a `Uri` is the lossy one.** `Uri.ToString()` returns the *unescaped* form — verified, `a%20b` renders as
`a b` — so every wire, log-for-comparison or visited-set use goes through `AbsoluteUri`, never `ToString()`. Userinfo
is dropped with `GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped)`,
verified to preserve every escape above (**REDIR-12**; the as-built follower keeps userinfo). `TryCreate` also
accepts `ftp://x/y` (verified), so an unsupported scheme is rejected explicitly as "malformed" and returned unfollowed
(**REDIR-18**). **HTTP-46**'s no-DNS equality is free — `Uri.Equals` resolves nothing — but its hidden precondition is
that `Uri.Equals` ignores userinfo and fragment (verified: `https://a:b@h/x#1` equals `https://h/x#2`), so the
**REDIR-16** visited set is a `HashSet<string>` of `AbsoluteUri` under ordinal comparison, "compared by their
textual external form" as **HTTP-46** says, not a `HashSet<Uri>`.

**Cross-origin is judged against the seed, and the marker retires.** **REDIR-8** compares scheme, case-insensitive
host and effective port against the *original* request, "not the previous hop". The as-built follower compares
against the previous hop. `Uri.Port` already yields the effective port — verified, `https://h/` and `https://h:443/`
both report 443 — so the triple `(scheme, host, port)` compares correctly; the as-built `AuthorizationPolicy`
comment claiming `Uri.Port` "returns -1 for the default scheme port" is wrong, harmlessly, since both sides are
computed the same way.

**REDIR-11** asks for an out-of-band signal from redirect to auth that a server cannot forge, that can only suppress
stamping, and that is removed before dispatch, and it flags the reference's trap: "only the auth step strips the
marker, so a pipeline with no auth step … forwards the internal marker to the transport." The as-built auth base
class had already found a better mechanism than a marker, and the port adopts and hardens it: **no signal at all**.
`HttpPipeline.SendAsync` fixes the seed request's origin on the call-scoped `PipelineContext` when the call starts,
immutable thereafter; `RedirectPolicy` compares each target against it (**REDIR-8**), and `AuthorizationPolicy`
compares the request it is about to stamp against the same value — a mismatch means "this hop is cross-origin",
whoever caused it. Every clause holds a fortiori: a `Location` value cannot reach an immutable field set before the
first response existed; a comparison can only withhold a credential; nothing is added to the request, so nothing
needs stripping and the no-auth-step trap cannot occur (**REDIR-11**, **AUTH-29**). It is stronger than a marker in
one respect: it works under a custom redirect policy that never learned to set one. The hardening over the as-built
version is where the seed comes from — as built, the auth policy records whatever origin it sees on its *first*
invocation into the public, string-keyed property bag, where any other policy could overwrite
`"dexpace.auth.origin"`. The mechanism change is recorded as §10 entry 15; the stage
order **REDIRECT → RETRY → AUTH** still matters, because auth must run per hop to see each hop's URL.

The rest of the credential hygiene translates directly: `Authorization` is stripped before every re-issue,
same-origin included (**REDIR-7**; as built, only cross-origin); `Cookie` and `Proxy-Authorization` are
additionally stripped cross-origin (**REDIR-9**; as built, `Proxy-Authorization` is not); `Cookie` is kept
same-origin (**REDIR-10**; built). The as-built `StripSensitiveHeadersOnCrossOrigin` switch is removed: **REDIR-9**
is a MUST, and an option that turns it off narrows it (P9).

**Lifecycle** is mechanical and mostly missing: a visited set seeded with the original URI whose revisit returns the
current response unclosed (**REDIR-16**); max hops 3, 0 disabling following (**REDIR-17**); a missing, empty or
malformed `Location` returning the response unfollowed (**REDIR-18**, **REDIR-19**; built for missing and
unparseable); the prior response disposed before each follow-up and the current one disposed if building the
follow-up throws, while every "return current" outcome leaves it open (**REDIR-22**; built for the follow path);
stack safety from a `while` loop (**REDIR-23**; built). The redirect predicate with its read-only snapshot
(**REDIR-20**, **REDIR-21**) and the structured, redacted hop events (**REDIR-28**) are not built.

**As built (d45e64b):** built — diverges: default transport follows redirects itself (policy is dead code);
browser-style POST→GET and 303 handling; no allowed-method set, loop detection, userinfo drop, or
`Proxy-Authorization` strip; silent 3xx on downgrade and non-replayable body; previous-hop origin; 20-hop default.

**As built (2026-10-08, phase 6b):** built. Everything the line above lists as missing is built, `REDIR-27` is deferred (a `LocationHeader` member has no consumer) and
`REDIR-25` stays 🚫 under §10 entry 14. The text above stands as written, with five *dated corrections*:

- **The predicate's scope and `Target` (P6b-7, P6b-8).** `RedirectOptions.Predicate` replaces the eligibility decision (the allowed-method set and `FollowSeeOther`) and nothing
  else; loop detection, the hop cap, the `Location` screen, the downgrade guard, the replay gate and credential stripping stay unconditional. `RedirectCondition` is a public
  sealed class (`Response`, `RedirectsFollowed`, `VisitedUris` as a fresh copy, and `Target`, the policy's own resolution of `Location`, an extension the spec does not ask for).
- **The exception family (P6b-16).** `RedirectException : SdkException` with two sealed leaves, `RedirectSchemeDowngradeException` and `RedirectBodyNotReplayableException`, not a
  `ServiceRequestException` (wrong retry semantics), with the redacted URLs in the message and no URL property (`XCUT-19`).
- **"Held as a `FrozenSet<Method>` copy" is kept as written:** `RedirectOptions.AllowedMethods` is an `IReadOnlySet<Method>` backed by a `FrozenSet` copy made at `init`.
- **The explicit default port is elided (P6b-30).** `Uri.AbsoluteUri` renders `https://h:443/y` as `https://h/y`; the origin and the wire meaning are unchanged, every
  non-default port, IPv6 literal and reserved escape survives. The checklist states the residue and does not assert that `:443` survives.
- **The malformed `Location` is logged redacted, not raw (P6b-20),** through `UrlRedactor.RedactHeaderValue`, which is total over unparseable text.

`AuthorizationPolicy.GetOrigin`'s comment that `Uri.Port` returns -1 for an absent port is wrong on .NET (it returns the scheme's default), as 6b's `HttpOrigin` relies on; 6c may move the policy onto `HttpOrigin`.

### 6.3 Authentication

**The descriptor/resolver model** (**AUTH-1**–**AUTH-7**) is not built and is pure data: an `AuthScheme` enum, an
`AuthRequirement` record, an `AuthDescriptor` of requirements in preference order, and a stateless static resolver.
**P13 — record equality is not value equality over collections.** **AUTH-2** requires "value-based equality over
scheme + scopes + params", and a `record` looks like it provides that. Verified on .NET 10.0.401: two records whose
`ImmutableArray<string>` member holds equal contents compare **unequal**, and so do two with equal `string[]`
members, because the synthesized `Equals` uses each member's default comparer. `AuthRequirement` therefore
overrides `Equals`/`GetHashCode` with sequence comparison, as does every model record carrying a collection (§4).

**Credentials.** **AUTH-8**'s redaction is free for the as-built classes, and fragile (P7). `ApiKeyCredential` and
`BasicCredential` are sealed classes, so `ToString()` prints the type name (verified); they get reference equality
by not overriding `Equals`, which is the variant **AUTH-8** assigns them — writing nothing is the implementation, as
in Ruby. The hidden precondition is that nobody converts them to records: verified, a `record Cred(string User,
string Password)` prints `Cred { User = alice, Password = hunter2 }`. The port pins the rule with a test that
formats every credential type and asserts the secret is absent, and overrides `ToString()` explicitly so the
assertion does not depend on the class keyword. `AccessToken` is a `readonly struct` — value equality over its
fields (**AUTH-8**'s bearer variant), `ToString()` the type name — and implements `IEquatable<AccessToken>` so the
equality is not the reflection-based `ValueType.Equals`, which is slow and opaque to the trimmer. **AUTH-9**'s
non-blank rule is `ArgumentException.ThrowIfNullOrWhiteSpace`; as built, `ApiKeyCredential` rejects empty but accepts
whitespace, and `BasicCredential` checks only for null where **AUTH-14** asks for non-empty with whitespace allowed.
**AUTH-10** makes expiry optional; as built, `ExpiresOn` is a non-nullable `DateTimeOffset`, so a non-expiring token
must be faked with `DateTimeOffset.MaxValue`. The port makes it `DateTimeOffset?`.

**The token cache: the hot path is built and correct.** **AUTH-34** and **XCUT-12** want a lock-free read of a valid
token and single-flight refresh under a lock scoped to one credential. The as-built `AccessTokenCache` does both: an
immutable `TokenHolder` published through a `volatile` field (a multi-field struct cannot be read atomically, so
boxing it behind one reference is what makes the unlocked read tear-free), and a `SemaphoreSlim(1, 1)` per cache key
with a double-checked read after acquisition. `SemaphoreSlim.WaitAsync` blocks no thread, and the lock is per key,
never global. What is missing is the policy around it. **AUTH-34**'s configurable refresh margin (default 30 s) is
absent — as built, a token is used until `RefreshOn` or `ExpiresOn`, so a request can leave with a token that
expires in flight; the port refreshes at the earlier of `RefreshOn` and `ExpiresOn − margin`. **AUTH-35**'s
rejection of a token already expired at fetch time (evaluated with no margin) is absent. A provider exception is
correctly not cached — the holder is written only after the provider returns — so **AUTH-11** holds; its "MUST
propagate" is read as applying when no valid token remains, §11 item 26. **AUTH-37**'s three
zones are not: in the expiring-but-valid zone the as-built cache awaits the refresh on the request path, then serves
the old token if the refresh fails. The port stamps the valid token immediately and starts one owned background
refresh per key — stored on the cache entry and observed, never fire-and-forget
(`docs/styleguide/csharp/09-concurrency.md` §9.6) — launched under `ExecutionContext.SuppressFlow()` so it does not
capture the triggering request's `Activity` and ambient state for its lifetime (§5.4). Finally, the per-key
dictionary is unbounded, and its key includes `TokenRequestContext.Claims`, which in a claims-challenge flow comes
from the server's `WWW-Authenticate` — a server-influenced key space, so **XCUT-14** applies and the cache moves
onto §5.4's `BoundedMap`.

**The HTTPS guard is missing, and it is the most important gap in this chapter.** **AUTH-28**/**XCUT-16** require
the auth step to reject a non-HTTPS URL "BEFORE any token fetch or header stamping". Verified against the as-built
pipeline: `BasicAuthPolicy` stamped `Authorization: Basic dTpw` on both attempts to an `http://` URL. The guard goes
in `AuthorizationPolicy.ProcessAsync`, before `GetCredentialAsync`, failing with a non-retryable `SdkException`
naming the policy type and the scheme; it is skipped on a cross-origin hop, where no credential is attached, so a
permitted downgrade hop proceeds credential-free (**AUTH-29**). There is no loopback exemption, in keeping with
Azure.Core's bearer policy, which refuses non-TLS endpoints outright; local development uses `dotnet dev-certs`
(§11 item 25).

**Challenge parsing is hand-written (P13).** **AUTH-12**/**AUTH-13** need an RFC 7235 parser that splits multiple
challenges, handles quoted strings with commas and `=`, escapes, bare schemes and token68, and never throws. .NET
has a parser that gets halfway. Verified on .NET 10.0.401: `HttpResponseHeaders.WwwAuthenticate` correctly splits
`Basic realm="a, b", Bearer realm="x", error="invalid_token"` into two challenges, but leaves each one's parameters
as a single raw string (`realm="x", error="invalid_token"`) with no parameter map; and
`AuthenticationHeaderValue.TryParse` returns `false` for an unterminated quoted string, where **AUTH-13** requires
recovery with the parameters parsed so far preserved. It also operates on `HttpResponseMessage` headers, which the
SDK's `Response` is not. The port's `AuthenticationChallenge.Parse(ReadOnlySpan<char>)` is a character-level state
machine: the grammar is not regular, and even `RegexOptions.NonBacktracking`, which removes the ReDoS risk, cannot
match balanced quoting with escapes. Composition takes a defensive copy of its handler list and delegates to the
first that can handle (**AUTH-23**); the header written is chosen by an explicit proxy flag (**AUTH-25**); a handler
that cannot answer returns `null`, never an empty header. None of this is built: PR #8 deferred the parser and the
Basic challenge handler to dexpace/dotnet-sdk#7.

**Digest** (**AUTH-15**–**AUTH-22**; deferred as dexpace/dotnet-sdk#2) needs only the BCL. `MD5.HashData` and
`SHA256.HashData` are one-shot static hashes. **P13 — the BCL's hex is upper-case.** RFC 7616 and **AUTH-17**
require lower-case hex; verified, `Convert.ToHexString` returns `900150983CD24FB0D6963F7D28E17F72` for `"abc"`.
`Convert.ToHexStringLower` fixes it but arrived in .NET 9, so on the `net8.0` target the port lower-cases — the
reason `.editorconfig` already dials CA1308 down. The client nonce is `RandomNumberGenerator.GetHexString(32,
lowercase: true)`, a CSPRNG draw of 128 bits (**AUTH-20**, **XCUT-21**; verified to return 32 lower-case hex
characters), never `Random`. **AUTH-21**'s ISO-8859-1 is `Encoding.Latin1` — and it is lossy without warning:
verified, `"é€"` encodes as `E9 3F`, the euro sign silently replaced by `?`. A username outside Latin-1 under a
challenge without `charset=UTF-8` therefore produces a wrong digest and a 401 rather than an error; the handler
checks the credential against Latin-1 first and declines instead. **FIPS is where .NET is worse off than Ruby.**
.NET has no bundled MD5: on Linux `MD5` is OpenSSL's, and on a FIPS-enabled OpenSSL the algorithm is refused inside
the provider — the same failure Ruby avoids by using `Digest::MD5`'s bundled implementation. This could not be
reproduced here (no FIPS OpenSSL; on this machine `MD5.HashData` succeeds), so it rests on published platform
behaviour. The port does not vendor an MD5: it probes availability once, and on a host that refuses it the Digest
handler treats `MD5`/`MD5-sess` challenges as unsupported and declines them, falling through to `SHA-256` if offered
— recorded as §10 entry 16, because **AUTH-15** says the handler "MUST support
exactly" all four. The per-nonce counter (**AUTH-18**) is an `Interlocked.Increment` on a boxed counter per nonce,
correct under concurrent reuse (**AUTH-24**), in a `BoundedMap` capped at 1024 (**AUTH-19**, **XCUT-14**).

**Basic** (**AUTH-14**) is `Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"))`, computed once per
policy — built, and correct. The client never decodes Basic credentials, but the one decoding path the SDK has
(test fixtures and a future server-side helper) should know that `Convert.FromBase64String` ignores embedded
whitespace (verified: `Y W J j` decodes to `abc`) while rejecting other garbage with `FormatException` — stricter
than Ruby's `unpack1("m")`, lenient in exactly one place.

**The AUTH pillar step** (**AUTH-27**–**AUTH-33**) runs inside both loops, as built. The 401 re-challenge — hook
consulted only when `WWW-Authenticate` is present (**AUTH-33**), the replacement driven once through `next` (a
fork, §5.1) with no further challenge handling (**AUTH-30**), the 401 disposed if the hook throws (**AUTH-32**), and
the replay skipped with the original 401 returned *undisposed* when the replacement body is not replayable
(**AUTH-31**) — is not built. When it is, **AUTH-31**'s reference drift cannot recur: sync and async share one
implementation (§5.3). The bearer 401 path (**AUTH-36**) evicts only the token whose stamped header produced the 401,
compared as the header string, so a token another request already refreshed survives. **AUTH-11**/**AUTH-38**'s
"through the asynchronous channel" is free for `async` methods (§5.3).

**As built (d45e64b):** partial: api-key, basic and bearer policies with cross-origin withholding and a correct
wait-free, single-flight cache; missing the HTTPS guard, descriptor/resolver, challenge parsing and handlers, Digest,
401 handling, refresh margin, expired-at-fetch rejection, background refresh, bounded cache, optional expiry and
non-blank validation.

---

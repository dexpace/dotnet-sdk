## 8. Instrumentation and Configuration

This is the chapter where .NET gives the port the most and where accepting the gift costs the most letter. The
runtime and its first-party package ecosystem have already converged on one answer for each observability concern —
`ILogger` for logs, `ActivitySource`/`Activity` for traces, `Meter` for metrics, `IConfiguration`/`IOptions<T>` for
configuration, `TimeProvider` for time — and OpenTelemetry .NET is built *on* those types rather than beside them.
The reference, Ruby and Node ports each had to define a facade and hope the ecosystem duck-typed into it; this port
adopts the ecosystem's types directly (P14) and spends its argument on the MUSTs those types were not designed to
satisfy. Behaviour marked verified was run on .NET SDK 10.0.401 (runtime 10.0.12).

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

### 8.1 The instrumentation seam

**There is no SDK-defined listener, and the P2 test splits the three primitives.** `ActivitySource`, `Activity` and
`Meter` live in `System.Diagnostics.DiagnosticSource`, which ships in the shared framework on both targets (verified:
present in `Microsoft.NETCore.App` 10.0.12 and in the 8.0.31 reference pack), so choosing them is choosing the
platform (P2) and core owes no seam for tracing or metrics. (As built, the logging facade's 9.0.5 closure replaces
that in-box assembly on `net8.0` with a package copy; §2.4 measures it and band-matching removes it.) `ILogger` is
different, and the common framing must be kept honest: `Microsoft.Extensions.Logging.Abstractions` is **installed from
NuGet**, not shipped with the runtime, so it does not get P2's pass. It is justified by P14 — every .NET host,
OpenTelemetry's log bridge and every major sink (Serilog, NLog) consume `ILogger`, and a bespoke SDK logging interface
would need an adapter for each — and it is recorded against the letter of **SEAM-1** ("depends at runtime on nothing
beyond its language's standard library plus a logging facade") and **NFR-1** ("a compile-time-only logging facade"):
the package is a *runtime* dependency, its assembly must be present when core loads. Recorded as §10 entry 1. The
as-built `Dexpace.Sdk.Core.csproj` already references it (pinned at 9.0.5, which supports `net8.0`), so `CLAUDE.md`'s
"BCL-only" rule has been stale since PR #6 added the reference; the pre-roadmap platform decision that core may take
the standard abstraction packages (Porting Method) anticipated the correction.

**The structured log event object does not exist, and that reverses Ruby's central conclusion.** Ruby built
`Dexpace::Instrumentation::Event` because **OBS-1** says "The facade MUST decide enabled/disabled once, at
event-creation time, and return a shared inert event for the disabled case", and seven more MUSTs are stated *about*
that object. .NET's convergence reaches the same guarantee by a different mechanism, and the mechanism has no object
in it: a `[LoggerMessage]`-generated method or a cached `LoggerMessage.Define` delegate performs the
`logger.IsEnabled(level)` check once, at the call, passes its arguments on the stack into a generated state struct,
and never invokes the formatter when disabled (verified: against a disabled logger the formatter ran zero times and
`Log` was never called). **OBS-1**'s *observable* guarantee — a disabled level allocates nothing and emits nothing —
holds; its *conformance step*, "assert the returned event is the shared singleton (reference-identical across
calls)", has no referent because nothing is returned. That is P4 applied honestly: the host has one concept where
the reference has two, and building an event builder over `ILogger` would allocate the very object the generated path
avoids. The consequences for the clauses Ruby hung on the object, each checked:

- **OBS-2**: `ILogger` has six levels; the SDK emits exactly four — `Error`, `Warning`, `Information`, `Debug` —
  with VERBOSE mapped to `Debug`, the mapping the requirement names.
- **OBS-3**: keys are template placeholders, validated *at compile time* by the generator (a placeholder that binds
  to no
  parameter is a `SYSLIB1014` build error; verified), which is stronger than a runtime rejection. A `null` value is
  carried as `null` in the structured state — so a JSON sink writes `null` — but the rendered message text says
  `(null)` (verified), which is not the "literal string `null`" of the letter; the residual is named (P6).
- **OBS-4**/**OBS-40**: the categorisation tag is `EventId.Name`, carried beside the key/value state rather than in it
  (verified: the state list holds only the template keys plus `{OriginalFormat}`), so a duplicate `event` key cannot
  arise and the collision diagnostic has nothing to diagnose.
- **OBS-5**/**OBS-9**: precedence between per-event fields, a global context and a diagnostic context is decided by
  the logging provider, and global context is attached by the host (`BeginScope`, OpenTelemetry resource
  attributes); the SDK offers no global-context channel of its own.
- **OBS-6**/**OBS-20**: SDK-owned events carry only strings and integers, so a value whose `ToString` throws is
  unreachable from them; but a throwing provider or formatter *is* reachable — verified, a throwing `ToString` on a
  logged value propagates out of the log call into the caller. **OBS-20** ("every log-emission site ... MUST catch any
  exception") therefore requires a guard the as-built lacks: each emission is wrapped in a `catch` that excludes
  `OperationCanceledException`, attempts one `http.instrumentation.error` event, and swallows a second failure.
- **OBS-7**: rendered strings (the URL) are truncated at 8 KiB with a marker before they reach a template.
- **OBS-8**: a log call is one call; "emit at most once" is vacuous.

Recorded as §10 entry 22, which lists the clauses whose letter is not met (**OBS-1**'s
singleton, **OBS-3**'s text rendering, **OBS-5**, **OBS-9**) and the ones that are stronger (**OBS-3**'s key
validation).

**Stable names need the delegate form, not the generator** (**OBS-39**), which is the P13 case of this subsection.
**OBS-39** fixes events `http.request`/`http.response` carrying `http.request.method`, `url.full`,
`http.response.status_code`, `http.response.duration_ms` and content-length/header fields. The `[LoggerMessage]`
generator cannot express those keys: a dotted placeholder such as `{http.request.method}` is rejected with
`SYSLIB1014` "Template ... is not provided as argument" (verified), because placeholders must bind to C# parameter
names. `LoggerMessage.Define` accepts them — verified, a `Define<string,string>` over
`"{http.request.method} {url.full}"` with `EventId(1, "http.request")` yields exactly those state keys and that event
name — and it is the same cached-delegate, check-once, allocation-free-when-disabled mechanism, so `CA1848` is
satisfied either way. The as-built `InstrumentationPolicy` uses the generator with `{Method}`/`{Url}`/`{StatusCode}`
and
event names defaulted from method names (`LogSendingRequest`); it moves to `Define` delegates with the semconv keys.
**OBS-2**'s mapping puts request/response events at `Debug` and failures at `Warning`, as built.

**Tracing is `Activity`, and its no-op is `null`** (**OBS-21**–**OBS-27**). OpenTelemetry .NET's tracing API *is*
`System.Diagnostics.Activity`, so the P14 subset Ruby had to define is the runtime type itself. **OBS-25**'s
allocation-free no-op holds in a form the letter did not imagine: with no listener,
`ActivitySource.StartActivity` returns `null` (verified), so the untraced path allocates nothing and there is no
"shared no-op span" because there is no span. **OBS-21**'s recording flag is `Activity.IsAllDataRequested`/`Recorded`,
and `Stop` is idempotent (verified: two `Stop` calls and a `Dispose` produced one stop callback). **OBS-22**'s
scope restoration is `Activity.Current`, an `AsyncLocal` that `Stop` resets to the parent (verified). **OBS-26**'s
reserved sentinels are `default(ActivityTraceId)`/`default(ActivitySpanId)`, which render as 32 and 16 hex zeros
(verified), with `ActivityTraceFlags` and a trace-state string beside them and `ActivityContext.IsRemote` for
remoteness. **OBS-27** is not met: `Activity` generates W3C identifiers only; `Activity.TraceIdGenerator` is a
settable hook (verified present) that can supply ids, but a Datadog flavour — 64-bit, rendered in decimal — is a
propagation format the runtime does not model, and Datadog's own .NET tracer handles it. Recorded as §10
entry 24. **OBS-30**'s contract that callbacks never throw is left as a contract, because .NET
does not guard it either: a throwing `ActivityListener.ActivityStarted` propagates out of `StartActivity` (verified).
That meets **OBS-20**'s "The runtime does NOT defensively wrap tracer ... or metrics ... calls" and conflicts with
**XCUT-20**'s "Observability code paths (redaction, event emission, span/metric recording) MUST NEVER throw into the
caller's request path"; the resolution — the SDK's own
recording code (tag computation, redaction) is total, listener callbacks are the **OBS-30** contract party and are
not wrapped — is §11 item 37.

**OBS-23**/**OBS-10**'s log correlation is host configuration, not SDK code.
`LoggerFactoryOptions.ActivityTrackingOptions`
folds the current `Activity`'s `TraceId`/`SpanId` into every log scope, and the flags enum *is* an allow-list — its
members are `TraceId`, `SpanId`, `ParentId`, `TraceState`, `TraceFlags`, `Tags` and `Baggage` — which is **OBS-10**'s
shape. Two letters differ: the keys are `TraceId`/`SpanId`, not `trace.id`/`span.id`, and the generic host's default
also folds `ParentId`. **OBS-24** and **ASYNC-8**–**ASYNC-12** are nearly free, and verified rather than assumed:
`AsyncLocal<T>` flows through `ExecutionContext` across every `await`, `Task.Run` and thread-pool hop, is
copy-on-write (a child's assignment did not leak back to the parent), and the thread pool runs each work item under
the captured `ExecutionContext` and restores the worker's own afterwards (the documented `ExecutionContext.Run`
contract) — **ASYNC-9**'s save/install/restore is the runtime's behaviour. The precondition (P7) is that
two APIs opt out: `ExecutionContext.SuppressFlow()` and `ThreadPool.UnsafeQueueUserWorkItem` each delivered `null`
for a set `AsyncLocal` (verified), so both are banned in SDK code by §9.1's banned-API gate — with one sanctioned
exception, the internal helper that launches SDK-owned background work (the token cache's refresh), which suppresses
flow on purpose so that work does not pin the triggering call's context (§5.4, §6.3). The span-model
residuals — the `null` no-op, the key names, the host-owned allow-list default — are recorded together as §10
entry 23.

**The HTTP-tracer vocabulary is span structure** (**OBS-28**, **OBS-29**). There is no listener object with
`operationStarted`/`attemptFailed` methods; the vocabulary maps to an *operation* `Activity` opened at the
`Operation` stage, one client-kind *attempt* `Activity` per attempt (as built, `InstrumentationPolicy` at the
`Diagnostics` stage), and `ActivityEvent`s for the in-between milestones. **OBS-29**'s ordering is then the span
lifecycle: one operation start; exactly one operation end whose status is `Ok` or `Error`; attempt spans repeating;
and retries-exhausted recorded as an `ActivityEvent` on the operation span immediately before it ends in `Error` with
the same exception. One operation `Activity` corresponds to one logical operation, the 1:1 the requirement states,
and an `ActivityListener` ordering test asserts it. **OBS-28**'s transport milestones — connection acquired, request
sent, headers received — are emitted by the runtime itself on the reference transport: System.Net.Http has its own
`ActivitySource` (verified), with experimental connection-setup and DNS sources beside it from .NET 9. The operation
span is
not built: the `PipelineStage.Operation` documentation says the stage "opens the operation span", and
`OperationPolicy`
does not.

**The obvious-but-wrong result: the reference transport already instruments itself** (P13). Verified with a loopback
server, an `ActivityListener` and a `MeterListener`: every `HttpClient` send produced a `System.Net.Http` client
span as a child of the SDK's attempt span, and the `System.Net.Http` meter publishes `http.client.request.duration`
(unit `s`) and `http.client.active_requests` — the *same instrument names* the as-built `InstrumentationPolicy`
records on the `Dexpace.Sdk` meter. Two consequences. First, the policy's `traceparent` stamping is wrong on this
transport: the runtime's propagator does not overwrite a header already present, so the wire carried the *SDK*
span's id (`…-47c6df6fbff7357c-01`) while the runtime's child span (`c9d9b0470385a854`) became invisible to the
server; with no pre-set header the wire carried the runtime child's id, which is correct. The fix belongs in the
adapter, not the SPI: `Http.SystemNet` strips a `traceparent`/`tracestate` equal to the current activity's before
dispatch and lets the runtime propagate, while transports that do not propagate keep the policy's stamping.
Second, a consumer that enables both meters sees each attempt measured twice under one instrument name, which an
exporter that flattens meter names (Prometheus) collides. The SDK keeps the semantic-convention names on its own
meter,
because core is transport-agnostic and a non-`HttpClient` transport emits nothing, and the reference transport's
documentation says to enable one meter or the other. Together with the specification's own naming conflict —
**OBS-32**
names "`http.client.request.count` (unit `{request}`) and ... `http.client.request.duration` (unit `ms`)" while
saying names "SHOULD follow OpenTelemetry semantic conventions", whose stable HTTP metrics use seconds and no count
instrument — this is resolved as §11 item 38: seconds, no separate count, matching
the runtime and OpenTelemetry. The as-built's remaining defects are small and listed for the roadmap: `server.port`
is recorded as `-1` for a default port (the convention wants the port number); the duration histogram's status tag is
a boxed nullable; and the redacted URL is computed before the listener or log check, so the disabled path allocates
a `UriBuilder`, a `StringBuilder` and a string on every attempt — against **OBS-1**'s intent though not its
event-object letter.

**Metrics** (**OBS-31**–**OBS-33**) are `Meter` with `Counter<long>` and `Histogram<double>`; with no listener an
instrument's `Record` is a cheap no-op and the static instruments are shared, which is **OBS-31**'s default without
a no-op class. A histogram tolerates any `double` and a counter is documented non-negative (**OBS-33**).

**Redaction** (**OBS-11**–**OBS-19**, **XCUT-19**) is the one place the as-built narrows a MUST-level guarantee, and
P9 does not allow that to stand. `UrlRedactor` is **default-allow**: it replaces the values of eight named
parameters (`access_token`, `token`, `code`, `sig`, `signature`, `api_key`, `apikey`, `password`) and passes every
other value through. **OBS-12** is default-deny: "URL query-parameter values MUST be redacted to `***` unless the
parameter name ... is allow-listed. The default query allow-list MUST be exactly `{api-version}`." A credential in a
parameter the deny-list does not name — `X-Amz-Signature`, `client_secret`, `sv` — reaches the span tag today. The
runtime itself moved the same way: System.Net.Http's own span reports `url.full` as `http://127.0.0.1:…/p?*` —
the whole query redacted by default (verified) — so default-deny is also the ecosystem's convention. The other
divergences are corrected in the same pass: userinfo is *stripped* where **OBS-11** wants the placeholder `***:***@`;
the fragment is dropped where **OBS-13** scrubs `key=value` tokens and keeps a plain fragment; value-less parameters
(`?flag`) are silently removed and a present-but-empty query (`?`) is lost, both against **OBS-14**; and keys and
values are re-encoded rather than preserved. **OBS-15**'s totality holds for the `Uri` overload, whose input is
already
parsed; the string overload **OBS-16** needs for header values returns `[malformed url]` on failure.
**OBS-16**–**OBS-18**
(header-value URLs, `Location`, a default-deny header allow-list) have no call site yet because nothing logs headers,
and bind the moment anything does. **OBS-19**'s dropped-header verbosity is the transport's (§3.2).

**Log granularity and body preview** (**OBS-34**–**OBS-38**) are not built. **OBS-34** separates *what* is logged
(none/headers/body, default none) from `ILogger`'s *whether*, and requires spans and metrics to run at every
granularity. The ecosystem shape to copy is `Microsoft.Extensions.Http.Diagnostics`' `LoggingOptions` (a body flag, a
body size limit, header allow-lists) — copied as a shape in a core `HttpLoggingOptions` record, not taken as a
dependency, since that package brings the compliance and redaction packages with it. The body preview wraps the body
in a tee stream
that copies the first 8 KiB into a pooled buffer while the caller reads everything (**OBS-36**, **XCUT-24**), is
skipped for unknown-length bodies on the async path (**OBS-37**), and decodes with the media type's charset or writes
`[binary N bytes captured]` (**OBS-38**). **OBS-35**'s layered log-level lookup is `IConfiguration` binding (§8.2).

**The correlation bundle is `Activity`.** **CTX-14**'s bundle — trace id, span id, flags, state, flavour, validity,
remoteness, active span, per-operation tracer factory — is `ActivityContext` plus `PipelineContext.Activity` plus the
static `ActivitySource`; **CTX-15**'s shared untraced sentinel is `default(ActivityContext)`. The context chain itself
is §5.4's.

**As built (d45e64b):** partial: `DexpaceDiagnostics` (source and meter), per-attempt spans, metrics and
generator-based logs built; diverges: default-allow redaction (**OBS-12**), non-semconv log keys (**OBS-39**),
unconditional `traceparent` stamping, no emission guard (**OBS-20**); missing: operation span, granularity/body
preview, header redaction.


**Correction 2026-10-07 (phase 4a, P4a-3).** "The correlation bundle is `Activity`" names `InstrumentationContext` as the composing
type, and the shared untraced sentinel's `TraceState` is `null` on `default(ActivityContext)`, normalised to `""` there.

### 8.2 Configuration

**CFG-1** fixes four tiers: explicit override, environment by exact key, a system-property source by normalised key,
then the caller default. The options PR #4 built took a position before this document existed — plain
options types in core, and no bespoke environment/override reader, because the siblings hand-rolled one and
`IConfiguration` covers it — and the position is adopted, but it must be argued against **CFG-1**, not asserted. .NET's
convergence for
layered configuration is `Microsoft.Extensions.Configuration`: an ordered stack of providers where the later one wins,
consumed through `IOptions<T>` binding. It is installed from NuGet, so P2 does not retire the concern — it moves it
out of core, into the `Dexpace.Sdk.Extensions.DependencyInjection` package, as a separately installable unit
(**NFR-2**). Core holds no lookup at all; it holds the options types the lookup binds into.

The tiers map as follows, and the third is a genuinely different source rather than a fabricated one (P11). Tier 1,
the explicit override, is the `Action<DexpaceClientOptions>` passed to `AddDexpaceClient`, registered after binding so
it wins. Tier 2 is `AddEnvironmentVariables()`, which a default host adds above file sources. Tier 3 — the system
property, which .NET has no ambient store for (`AppContext` switches are the nearest analogue, and are process knobs
for the runtime, not application settings) — is `appsettings.json` and its environment-specific overlay, which sit
*below* environment variables in every default host, preserving **CFG-1**'s order: override > environment > third
source > default. Tier 4 is the property initializer on the options record. The one-deviation-applied-twice is the key
normalisation: **CFG-3**'s `MAX_RETRY_ATTEMPTS` → `max.retry.attempts` becomes `Dexpace__Retry__MaxRetryAttempts` →
`Dexpace:Retry:MaxRetryAttempts`, case-insensitive, which is the rule every .NET operator already knows; **CFG-4**'s
raw accessor is vacuous because nothing normalises away casing. **CFG-2**'s empty-is-absent rule is not the
binder's: an environment variable set to the empty string is a present, empty value to `IConfiguration`, and binding
it into a typed option fails `ValidateOnStart` (below) rather than falling through to the next tier. **CFG-11**'s
substitutable seams are `AddInMemoryCollection` in tests, which never touches the real environment. **CFG-13**'s
process-wide global slot is deliberately not provided: the container is the scope, and a static mutable configuration
slot is the service-locator pattern the house guide rejects
(`docs/styleguide/csharp-aspnetcore/02-dependency-injection.md` 2.1). **CFG-14**'s key constants become one
`SectionName = "Dexpace"` constant plus the property names. How the specification left the system-property tier
without porting guidance is §11 item 4; the substitution and the retired slot are §10 entry 25.

**The typed accessors are where the ecosystem and the specification disagree about failure, and the port takes the
ecosystem's side deliberately.** **CFG-5** says typed accessors "MUST NOT throw on missing or unparseable values",
falling back to the default; the house guide says the opposite for bound options —
`docs/styleguide/csharp-aspnetcore/01-host-and-configuration.md` 1.4, "Validate options at startup with
`ValidateOnStart` so misconfiguration fails the boot". The binder's conversions are the BCL `TypeConverter`s, and
they fail loudly: `BooleanConverter` rejects `"1"` with `FormatException` (and accepts `" TRUE "`), `Int32Converter`
parses `"010"` as 10 but *also* accepts `"0x10"` as 16 (all verified). A value that silently falls back to a default
is
a misconfiguration nobody sees until production; a value that fails `ValidateOnStart` fails the deploy. The port
therefore fails fast at startup and records it against **CFG-5**–**CFG-7**'s never-throw letter as §10
entry 26. **CFG-6**'s strict boolean is kept in spirit — only `true`/`false` succeed — and
the hex acceptance is a gotcha the DI package closes with its own integer converter, since **CFG-5**'s base-10 intent
is not negotiable.

**CFG-7**'s duration grammar is the sharpest gotcha in the chapter (P13). The binder converts `TimeSpan` with
`TimeSpanConverter`, which parses `"1000"` as **one thousand days** (verified: `1000.00:00:00`) — **CFG-7** says "a
bare number interpreted as *milliseconds*" — and rejects both `"PT5S"` and `"500ms"` with `FormatException`
(verified). An operator who writes `"AttemptTimeout": "1000"` meaning one second gets a timeout of nearly three years
and no error. The DI package registers a `TimeSpan` converter implementing **CFG-7** exactly: ISO-8601 through
`System.Xml.XmlConvert.ToTimeSpan` (in the BCL; verified `PT5S` → 5 s, and `PT-5S` rejected), `<number><unit>`
shorthand, a bare number as milliseconds, negatives rejected.

**Immutability** (**CFG-8**, **CFG-9**, **CFG-12**). The as-built options are `sealed class`es with public setters,
passed *by reference* into every `PipelineContext`, so a caller who mutates `options.Retry.MaxRetryAttempts` while a
request is in flight changes that request's retry budget mid-operation. Immutable options records and the
house guide (`docs/styleguide/csharp/06-types-and-data-modeling.md` 6.1, `docs/styleguide/csharp/10-api-design.md`
10.2) agree with **CFG-8**: the options become `sealed record`s with `init` accessors, derived with `with`
(**CFG-9**'s copy-on-write), and **CFG-10**'s override removal is vacuous. One precondition: `with` is a *shallow*
copy, so the nested `RetryOptions`/`RedirectOptions` must be records too, or a derived copy shares a mutable child
(P7). Binding into `init` accessors works through the reflection binder; whether the AOT-safe configuration-binding
source generator binds `init` accessors is to be verified when the DI package is built, and if it does not, the
package binds into a private mutable staging type and snapshots it into the record — Ruby's staging-object pattern,
for a different reason. **CFG-37**'s null guards are `ArgumentNullException.ThrowIfNull` under nullable reference
types; **CFG-38** is satisfied by construction, since typed values only ever arrive through the one binding path.
Validation is an `IValidateOptions<DexpaceClientOptions>` with `ValidateOnStart`, in the DI package (roadmap
phase 9).

**The proxy model** (**CFG-22**–**CFG-28**). The options design behind PR #4 ruled proxies out of scope because
`HttpClient`
honours `HTTP(S)_PROXY`/`NO_PROXY` natively. It does, and its semantics diverge from four MUSTs (all verified on
10.0.401). `NO_PROXY=*` does **not** bypass: `HttpClient.DefaultProxy.IsBypassed` returned `false` for every host,
against **CFG-27**. A proxy URL with no port is accepted and defaulted, against **CFG-25**'s "An absent port in a
proxy
URL MUST be treated as invalid". `GetProxy` returns the proxy URI *with its userinfo*, so logging the resolved proxy
leaks the credentials **CFG-22** requires masked. And `WebProxy.BypassList` entries are **regular expressions**, not
globs: `new WebProxy(..., ["*.internal.example.com"])` throws `RegexParseException`, and a bypass entry `a.b` matches
host `axb`, against **CFG-23**'s glob grammar. The runtime default also reads the environment implicitly when the
first handler is built, where **CFG-28** says "the environment MUST be consulted only when a resolver is explicitly
invoked". That ruling is overturned: core owns an immutable `ProxyOptions` record (type, address, ordered glob
bypass list compiled once, credentials masked in `ToString`, explicit bypass-all flag) and an explicit
`ProxyOptions.FromEnvironment()` resolver implementing **CFG-24**–**CFG-27**; an SDK-managed `Http.SystemNet`
transport
calls that resolver explicitly at construction and installs its own glob-matching `IWebProxy`, so .NET users keep the
environment-driven behaviour they expect with the specification's semantics. A caller-supplied `HttpClient` keeps
whatever proxy it has (**TRANSPORT-15**). **CFG-24**'s first layer — `https.proxyHost`-style system properties — has
no
.NET source, and the explicit `ProxyOptions` argument stands in for it; that is the same tier substitution as above.

**Dates, identifiers, equality, identity** (**CFG-29**–**CFG-36**). **CFG-29**'s canonical format is
`DateTimeOffset.ToString("r", CultureInfo.InvariantCulture)` (verified: `Sun, 06 Nov 1994 08:49:37 GMT`). Parsing is
the obvious-tool-is-wrong case: `ParseExact(s, "r", ...)` **validates the weekday** — `Mon, 06 Nov 1994 ...`
throws `FormatException` because 6 November 1994 was a Sunday — and rejects the `UTC` zone token (both verified),
whereas **CFG-30** says "the leading weekday token is informational only and MUST NOT be validated" and requires
`GMT`/`UTC`/`+0000`/`+00:00`. The parser strips the weekday and comma, normalises the zone, and parses the rest with
`ParseExact` over an explicit invariant format (month names already match case-insensitively; verified); §6.1 uses
the same parser for `Retry-After`. **CFG-32** is `Guid.NewGuid()`, a version-4 UUID (verified) drawn from the
operating system's cryptographic generator — stronger than the non-cryptographic generator the requirement permits,
and **XCUT-21**'s CSPRNG path is `RandomNumberGenerator`, kept separate as the specification insists.
**CFG-33**/**CFG-34**:
.NET's defaults are wrong for both clauses — records compare array members by reference (verified), and `double`'s
`Equals`, which `SequenceEqual` uses, treats `0.0` and `-0.0` as equal (verified) where **CFG-34** requires them
distinct; its NaN-equals-NaN clause happens to match. A deep-equality helper comparing doubles through
`BitConverter.DoubleToInt64Bits` ships when a model first carries an array. **CFG-35** is §6.1's classifier.
**CFG-36**'s descriptor is `SdkVersion` plus `RuntimeInformation.FrameworkDescription`; its fallback is `"0.0.0"`
where the requirement says a non-blank `unknown`.

**As built (d45e64b):** diverges: options are mutable classes shared by reference (**CFG-8**); proxy delegated to
`HttpClient` defaults (**CFG-22**–**CFG-28**); version fallback `0.0.0` (**CFG-36**); not built: the DI package's
binding, validation and duration/integer converters, `ProxyOptions`, the date parser as a shared utility.

### 8.3 The clock, the wait, and the prohibition

**CFG-15** requires the time seam to expose a wall clock, a monotonic elapsed counter and a blocking interruptible
sleep; **RETRY-26** requires the inter-attempt wait not to "pin an execution carrier"; **XCUT-3** requires prompt
cancellation. .NET 8 put the seam in the BCL: `TimeProvider`, whose `GetUtcNow()` is the wall clock,
`GetTimestamp()`/`GetElapsedTime()` the monotonic counter (**CFG-16**), and `Task.Delay(TimeSpan, TimeProvider,
CancellationToken)` the cancellable non-blocking wait (**CFG-18**, **ASYNC-18**). The as-built retry policy, set-date
policy and token cache all take a `TimeProvider`, defaulting to `TimeProvider.System`, and tests drive a fake. The
async wait unmounts nothing because nothing is mounted: an awaiting continuation holds no thread, which is
**RETRY-26**'s non-pinning property as the runtime's default. The blocking sleep **CFG-15** also names is the sync
path's wait, and it must still route through `TimeProvider` or a fake clock will not advance it: a
`TimeProvider.CreateTimer` signalling a `ManualResetEventSlim`, waited with the cancellation token, blocks only the
caller's own thread. That resolves the same **CFG-15**-versus-**RETRY-26** tension Ruby recorded, differently:
§11 item 1.

**CFG-17** has a verified trap: `Task.Delay(TimeSpan.FromMilliseconds(-1))` does not reject — `-1` ms is
`Timeout.InfiniteTimeSpan`, and the task never completes (verified: not complete after 300 ms; `-2` ms throws
`ArgumentOutOfRangeException`; the `TimeProvider` overload behaves the same). A negative delay computed from a
`Retry-After` date in the past would therefore hang the call forever rather than retry at once. The as-built retry
policy is safe because it waits only when `delay > TimeSpan.Zero`; the rule is stated so the next caller of
`Task.Delay` keeps it. **CFG-17**'s "re-assert the interrupt/cancellation status before propagating" is free: a
cancelled token cannot be un-cancelled, so the signal is always observable (P7). **CFG-19**'s wrapper unwrapping is
free
on `await`, which rethrows the original exception, and on `GetAwaiter().GetResult()`; only `.Result`/`.Wait()`
produce `AggregateException`, and §9.1 bans them.

**The prohibition is enforced by the runtime, and one residual needs a lint.** Ruby banned `Timeout.timeout` and
`Thread#raise` because an asynchronous interrupt can land inside an `ensure` releasing a pooled connection. .NET Core
removed the equivalent: `Thread.Abort` throws `PlatformNotSupportedException` (verified, surfaced through reflection
as the inner exception of a `TargetInvocationException`). What remains is `Thread.Interrupt`, which injects
`ThreadInterruptedException` into the next blocking wait on the target thread — the same hazard at a narrower set of
landing sites — and it is banned in SDK code by §9.1's banned-API list alongside the `ExecutionContext` flow
suppressors of §8.1. Deadlines are cooperative cancellation tokens throughout, observed at well-defined points. This
is why **CFG-20**'s interruptible-task future (cancel-with-interrupt versus cancel-without) has no .NET counterpart:
cancellation is cooperative everywhere in the ecosystem, and a future that interrupted a pool thread would reintroduce
the hazard. Its **CFG-21** obligation survives in cooperative form, and needs care because the obvious tool drops
it: `Task.WaitAsync(CancellationToken)` abandons the awaited task on cancellation without cancelling or observing
it, so a `Response` that completes afterwards is never disposed (verified: a disposable result delivered 50 ms after
the `WaitAsync` was cancelled was not disposed by anyone). Any SDK code that stops awaiting a task that can still
produce a `Response` attaches a continuation that disposes a late result — the orphaned-result rule of **ASYNC-5**
and **TRANSPORT-9**. The retirement of the interrupt mode is §10 entry 8; the two-mode
cancellation MUSTs it shares a cause with (**ASYNC-3**, **ASYNC-4**) are argued where the async adapter contract is
(§3.3).

**As built (d45e64b):** built (`TimeProvider` throughout, non-negative delay guard); missing: the sync-path wait,
the banned-API gate, the late-`Response` disposal rule as a shared helper.

---

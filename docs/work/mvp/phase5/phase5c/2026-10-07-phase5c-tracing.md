# Phase 5c — Tracing and Metrics: Implementation Plan

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0–4 merged; 5a and 5b are being
designed in parallel and nothing of theirs is in the tree). Design:
[phase 5c tracing design](2026-10-07-phase5c-tracing-design.md), the authority for every decision below. The plan cites its
positions (A–H), tables and rulings (`P5c-1`…`P5c-21`) rather than restating them. Scope authority: the roadmap's Phase 5
card and Phase List row 5. Format precedent: the [4c plan](../../phase4/phase4c/2026-10-05-phase4c-pipeline.md) and its
4a/4b siblings.

**What this document is.** The roadmap's step 3 for sub-phase 5c: numbered TDD tasks in the design's four-PR landing order,
each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff (none, P5c-16), **Breaking** markings,
requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 4.2) and it
writes no production code. The [coverage table](#coverage-one-row-per-owned-id) below is the plan's own row table.

**Scope.** 12 rows: `OBS-21`–`OBS-23`, `OBS-25`–`OBS-33` (10 MUST, 2 SHOULD). 5b owns the other 28 (P5c-1, open for the
lead). `CTX-14`, `CTX-15`, `OBS-20`, `OBS-34`, `OBS-24` (contingent), `XCUT-20`, `RETRY-*`, `REDIR-*` and `SEAM-28` are other
owners' rows on which 5c does work; they appear in [the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-5c).

**Order and gates.** 4a, 4b and 4c are merged (dependencies met). 5a and 5b are **conveniences** (design, Prerequisites):
5c reads no 5a option, and shares one file, `InstrumentationPolicy`, with 5b (P5c-17). Branch `<issue>-phase-5c-tracing`
off `main` (the issue number is the lead's; the planning branch is `62-phase-5-planning`). The four PRs land in order; each is
green on the whole local gate.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off). `System.Diagnostics` and
   `System.Diagnostics.Metrics` are imported per file.
3. **`src/` code**: `ConfigureAwait(false)` on every `await` (`CA2007`), methods at most 70 lines (`MA0051`; 5c removes the
   `InstrumentationPolicy` waiver, task 1.7, and leaves `RetryPolicy`'s for 6a), `///` XML docs on every public member
   (CS1591), no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2; `System.Diagnostics.DiagnosticSource` is in the
   shared framework), no reflection (`IsAotCompatible`, `IsTrimmable`). Every new `catch` is
   `when (!ExceptionFacts.IsFatal(ex))` (RETRY-25, design §10 entry 12). **5c adds no `try`/`catch` around a listener
   callback or a tracer/meter call** (`OBS-20`, `OBS-30`, §11 item 37): a throwing `ActivityStopped`, `ActivityStarted` or
   `MeasurementCallback` propagates, with the single exception of P5c-13's dispose-then-rethrow.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `AotSmoke` for the
   smoke checks; the loopback wire tests are `Integration`, as the existing wire classes. 5c adds **no** `Security` class and
   edits **none** (design, "Security tests that must stay green"). Core tests live under
   `tests/Dexpace.Sdk.Core.Tests/{Diagnostics,Pipeline,Pipeline/Policies,Execution}/`; doubles and recorders under
   `tests/Dexpace.Sdk.TestSupport/Diagnostics/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2).
5. **Every test that installs a listener uses the scoped recorders of task 1.2, and every class that installs an
   `ActivityListener` or `MeterListener` on `Dexpace.Sdk` is in `[Collection("Instrumentation")]`.** Scoping only filters
   what a recorder reports; it does not stop the recorder's listener from creating or sampling spans for other tests
   (`ActivitySource` takes the highest sampling result across all listeners). So a parallel `AllDataAndRecorded` recorder
   would make spans exist and record in tests that assert no listener or a non-recording sampler. Any test that asserts
   "no listener" or non-recording sampling must be in `Instrumentation` or `NoDiagnosticListeners`. An unscoped recorder is
   process-wide and from PR 2 would see other tests' operation spans.
5a. **"Mapped" pipelines are built explicitly.** `DexpacePipeline.CreateDefault` does **not** contain `ErrorMappingPolicy`
   (it adds idempotency, client-identity, set-date and `AddStandardResilience`: operation, redirect, retry,
   instrumentation). Every test that needs a mapped 4xx/5xx builds `CreateDefault`'s composition plus an explicit
   `.Add(new ErrorMappingPolicy())` at `PerCall`, and says so in its name or body.
6. **Security tests are never deleted or loosened.** The close-out check is an **empty** diff:
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   prints nothing. A change that seems to force an edit there is a signal to re-read the design's "Security (phase 1) tests
   that must stay green", not to edit the file.
7. **`PublicAPI.Unshipped.txt`.** No line is added to or removed from **either** file in any 5c task (P5c-16): every 5c type
   is `internal`. Each task that adds a type states "PublicAPI: none". The build's `RS0016`/`RS0017` output is the authority:
   a 5c task that makes either fire has made something public by accident (usually a missing `internal` on a nested type or
   a test helper). `git diff --stat -- src/*/PublicAPI.Unshipped.txt` is empty at every PR gate.
8. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes. The
   behavioural changes of design "Breaking changes" (items 1–7) each get a remarks line on the public member they change
   (`InstrumentationPolicy`, `PipelineContext.Instrumentation`, `SystemNetHttpClient`) and a `CHANGELOG.md` `[Unreleased]`
   line in the PR's close-out task.
9. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*`; 5c writes no `IO.X`. The
   simple name `Activity` is also a member of `PipelineContext`; inside that type use `System.Diagnostics.Activity`
   (as `HttpPipeline` already does).
10. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `test:` for tests only, `docs:` for
    documentation only, `chore:` for refactors. The plan authorises no `git push`, no `gh` command and no remote action. No
    AI attribution in any commit, PR or document (global rule).
11. **Names from 5a and 5b are placeholders.** The design assumes (P5c-17 to P5c-19) 5b's `InstrumentationPolicy` constructor
    parameter, redactor and emission guard. Tasks 1.1 and 2.1 re-derive every call site from the merged code; where this
    plan writes `s_redactor`, `LogSendingRequest` and kin, the merged code wins.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build
is the lint gate. Test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's `--filter`).
**The planning host has no .NET SDK**: nothing in this plan was compiled, and every API-shape claim is re-derived in task
1.1 before it is relied on.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty (convention 6)
git diff --stat -- src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt   # expect empty (convention 7)
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`,
then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and
`scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) runs before the push of PR 2 (it rewrites `HttpPipeline` and
adds the largest new code) and PR 4. No `PackageReference` changes, so no `packages.lock.json` change is expected.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info OBS
scripts/knowledge --gaps OBS                # the roadmap's gap table lists no OBS gap ID; appendix C rows OBS-21 and OBS-29 carry text the chapter lacks
scripts/knowledge --req OBS-29              # per row in scope of the PR
```

---

## Plan-level readings (where the design was ambiguous, inconsistent, or unverifiable on the planning host)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it and
flagged it where it moves work between PRs.

- **R1 — `OperationTelemetry.Stop` takes a `bool settled`, not a `Response?` (open for the lead).** The design lists
  `Stop(Activity?, Response?)`. The `Response` parameter has no use inside `Stop`: P5c-13's disposal is the pipeline's, done
  in `SendCoreAsync`'s catch. What `Stop` does need is whether `Complete` or `Fail` ran, to apply the design's rule "the
  `finally` sets `Error` with no description if neither ran" for a fatal exception. The plan uses
  `Stop(Activity?, bool settled)`; the lead may keep the design's signature if a use appears.
- **R2 — PR 1 keeps an interim fallback for attempt spans (open for the lead).** Design Position F makes the attempt span a
  child of the bundle, and `InstrumentationContext.None.StartActivity` returns `null`. Until PR 2 puts the operation span
  in the bundle, the dispatch bundle is still `FromActivity(Activity.Current)` (4c's interim): with no ambient activity (the
  usual consumer: listening to `Dexpace.Sdk` only) it is `None`, and PR 1 alone would stop producing attempt spans. Task 1.7
  therefore starts the span through the bundle and, **only when the bundle is `None`**, falls back to
  `DexpaceDiagnostics.ActivitySource.StartActivity(name, kind)` (parent `Activity.Current`, as built). Task 2.4 deletes the
  fallback and adds the fact that an attempt span exists only under an operation span (Position B). The alternative, landing
  PRs 1 and 2 together, makes the fallback unnecessary; the plan keeps each PR independently shippable.
- **R3 — The ordinal on `dexpace.attempt.failed` is `CallState.Transmissions - 1`.** `AttemptFailed(PipelineContext, …)` has no
  parameter for the ordinal, and `RetryPolicy`'s own counter resets per redirect hop. The transmission counter is incremented
  by `InstrumentationPolicy`, so in a pipeline **without** that policy the event reports `0`. This matches the attempt span
  (which does not exist there either) and is documented on the user page; a pipeline that drops diagnostics has no
  per-transmission telemetry to correlate with.
- **R4 — The "propagation off" wire clause is covered by pure-function tests, not an in-process wire test.** Design PR 3 lists
  a wire test "with propagation disabled by the `AppContext` switch, the SDK's stamp survives". The runtime reads its
  propagation switch once into a static (to verify, task 1.1 V6), so a test cannot flip it after the process has sent a
  request, and a test run is one process. Task 3.1 tests the decision (`TraceContextStripping.ShouldStrip` and the switch
  resolver) with injected inputs; task 3.2 adds the wire test for the *documented residual* instead (a borrowed client whose
  handler has a no-output propagator sends no `traceparent`). If V6 finds the switch is read live, task 3.2 adds the design's
  wire test as written.
- **R5 — OBS-24 is contingent (P5c-1).** If 5b's census omits `OBS-24`, task 2.6 carries its one test (c) and the coverage
  table gains a row; the plan lists it as contingent and counts 12 rows.
- **R6 — `RedirectHop` ships with no production caller (P5c-7, P5c-20).** Its tests are unit tests over a `PipelineContext`
  built by `TestContexts`; 6b calls it. The plan states this so the checklist marks the row's evidence honestly.
- **R7 — Pre-flight verification is a task, not an assumption.** Seven runtime and tooling facts the design marks "to
  verify at plan time" cannot be verified on the planning host. Task 1.1 lists them with the decision each outcome selects.
  No later task proceeds on an unverified one.

---

## PR 1 — Test isolation, then the attempt span and metrics

**Gate: none (4a, 4b, 4c merged).** Rows: `OBS-31`, `OBS-32`, `OBS-33`, and the attempt-span half of `OBS-21`/`OBS-25`.
Files: `tests/Dexpace.Sdk.TestSupport/Diagnostics/{ActivityRecorder,MetricRecorder,TestHosts}.cs`,
`tests/Dexpace.Sdk.Core.Tests/Support/DiagnosticCollections.cs`, `src/Dexpace.Sdk.Core/Diagnostics/{HttpSemanticConventions,
HttpClientMetrics,AttemptTelemetry}.cs`, `src/Dexpace.Sdk.Core/Pipeline/{CallState}.cs`,
`src/Dexpace.Sdk.Core/Pipeline/Policies/InstrumentationPolicy.cs`, and the three test classes that use recorders.

### Task 1.1 — Pre-flight: verify seven facts on the real toolchain (no commit)

Throwaway scratch tests (kept in the scratchpad, never committed). Record each outcome in the PR description and in the
checklist's "Findings" list. Each fact selects a branch of a later task.

| # | Fact | Probe | If it fails |
|---|---|---|---|
| V1 | `Meter.CreateHistogram<double>(name, unit, description, tags, advice)` with `InstrumentAdvice<double>` compiles on the pinned SDK **without** an `[Experimental]` diagnostic (design Position F; `OBS-32`) | compile a one-line call under the repository's analyzers | task 1.5 drops the advice and its test; `OBS-32` is unaffected (the design says so); note it under §11 item 38 at close-out |
| V2 | `Activity.AddException(Exception, in TagList, DateTimeOffset)` exists and produces an `exception` event with `exception.type`, `exception.message`, `exception.stacktrace` (P5c-5) | start an activity under a listener, add an `SdkException` built through 4b's trail, read `Events` | task 2.2 writes the three tags by hand through `AddEvent(new ActivityEvent("exception", tags: …))` |
| V3 | `AddException`'s `exception.stacktrace` is `ex.ToString()`; does 4b's `ExceptionTrail` render suppressed messages that can carry a URL? (P5c-5) | add an `HttpResponseException` whose trail carries a URL-bearing suppressed exception; read the tag | task 2.2 writes `exception.stacktrace` from `ex.StackTrace` (never `ToString()`), by hand per V2's fallback |
| V4 | A single fatal-excluded `Activity.Stop()` called twice is a no-op the second time, and `Dispose()` after `Stop()` does not re-notify the listener (`OBS-21`'s "Stop is idempotent"; `AttemptTelemetry.End`) | count `ActivityStopped` callbacks | `AttemptTelemetry` and `OperationTelemetry.Stop` guard with their own flag (they do either way; the probe decides whether a test asserts the runtime's or the SDK's idempotency) |
| V5 | `[CollectionDefinition(DisableParallelization = true)]` exists on xUnit v3's `CollectionDefinitionAttribute`, and a class in it never overlaps any other class (P5c-15) | read the pinned `xunit.v3` package's public API; run two sleeping classes | task 1.2's collection is replaced by the xUnit v3 mechanism the probe finds (an assembly-level `[assembly: CollectionBehavior(DisableTestParallelization = true)]` is the fallback, and costs the whole suite its parallelism: stop and take it to the lead) |
| V6 | `System.Net.Http`'s propagation switch is `AppContext` switch `System.Net.Http.EnableActivityPropagation`, falling back to environment variable `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION`, default true; whether it is cached in a static; whether a `DistributedContextPropagator.CreateNoOutputPropagator()` global can be detected without reflection (P5c-11) | read the runtime source at the pinned version (`SocketsHttpHandler`/`DiagnosticsHandler`); send two requests flipping the switch between them | task 3.1 uses the verified names; if the switch is read live, R4's wire test is written as the design says; a no-output global propagator that cannot be detected joins the documented residual |
| V7 | The runtime's `System.Net.Http` span is an `ActivitySource` named `System.Net.Http` and, with no listener on it but an `Activity.Current`, injects `Activity.Current`'s own id (design Position H) | a loopback request under each listener combination, read `traceparent` | task 3.2's three wire tests are written to the observed behaviour; a different source name changes only the listener filter |
| V8 | `Activity.TraceIdGenerator`'s generated ids are never the zero id, i.e. the runtime coerces a zero draw (`OBS-27`'s clause; P5c-14) | read the runtime's `ActivityTraceId.CreateRandom` implementation at the pinned version | task 2.7 keeps the test as a sample check, and the checklist records the clause as admitted (probability 2^-128); a dated §10 entry 24 correction is proposed in task 4.3 |

**Done when:** the table above has an outcome beside each row. No production change.

### Task 1.2 — Scoped recorders, host helper, the `NoDiagnosticListeners` collection (P5c-15 item 3 and 4)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/ScopedRecorderTests.cs`, class `ScopedRecorderTests`
(`[Collection("Instrumentation")]`, `Unit`):

- `A_scoped_recorder_keeps_only_activities_in_its_own_trace` (start a `Scoped` recorder, start a sibling root activity from
  the same source on another trace; `Started` holds the first trace's children only)
- `The_scoped_root_is_the_ambient_activity_in_the_creating_context` (`Activity.Current == recorder.Root` after construction;
  restored to the previous current after `Dispose`)
- `A_scoped_recorder_excludes_its_own_root_from_Started_and_Stopped`
- `A_host_from_TestHosts_Unique_is_a_valid_unique_host` (two calls differ; `new Uri($"https://{host}/")` parses;
  lower-case; ends `.example.test`)
- `A_scoped_metric_recorder_keeps_only_measurements_for_its_server_address` (record two measurements on a private test
  `Meter` with different `server.address` tags; only the matching one is kept)

Red: CS0117 (`ActivityRecorder.Scoped`, `MetricRecorder.ForServer`), CS0103 (`TestHosts`).

**Production (test support only).**

- `ActivityRecorder` gains `public static ActivityRecorder Scoped(params string[] sourceNames)`: constructs the recorder over
  the named sources **and** a private `ActivitySource("Dexpace.Sdk.Tests.Root")`, starts `Root` (name `test-root`) from that
  source, and so sets `Activity.Current = Root` in the creating context. `Root` is exposed (`public Activity Root { get; }`).
  `Started`/`Stopped` for a scoped recorder drop any activity whose `TraceId != Root.TraceId` and `Root` itself. `Dispose`
  stops `Root` (restoring the previous current) then disposes the listener. The existing `public ActivityRecorder(string)`
  constructor and its remarks stay (unscoped mode), with the remarks now saying "prefer `Scoped`".
  **Create a scoped recorder inside the test body**, not in a constructor or field initialiser: `Activity.Current` is an
  `AsyncLocal`, and a value set in the test class's constructor is not guaranteed to flow into the test method (R-note in
  the type's remarks).
- `MetricRecorder` gains `public static MetricRecorder ForServer(string meterName, string serverAddress, params string[]
  instrumentNames)` (constructor overload taking a `Func<RecordedMeasurement, bool>` filter, applied in `Record`). The
  existing `For(name)` and `RecordObservableInstruments` stay.
- New `tests/Dexpace.Sdk.TestSupport/Diagnostics/TestHosts.cs`: `public static class TestHosts { public static string Unique(); }`
  returning `$"{Guid.NewGuid():N}.example.test"` (lower-case).
- New `tests/Dexpace.Sdk.Core.Tests/Support/DiagnosticCollections.cs`:
  `[CollectionDefinition("NoDiagnosticListeners", DisableParallelization = true)] public sealed class NoDiagnosticListenersCollection;`
  (per V5). The existing `"Instrumentation"` collection name has no definition and keeps serialising its members among
  themselves; it is left alone (it does not exclude other collections, which is why scoping, not the collection, is the
  isolation).
- **Migrate the three classes that install recorders** to the scoped form, mechanically, in this task: `InstrumentationPolicyTests`
  (the `_listener` field becomes a `using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");` first line of each test that
  inspects activities; the `Activities` property is replaced by `recorder.Started`; the class loses `IDisposable`),
  `DexpacePipelineTests` (its `Collection("Instrumentation")` span assertions) and `InstrumentationContextTests` (six
  `new ActivityRecorder("Dexpace.Sdk")` sites become `ActivityRecorder.Scoped("Dexpace.Sdk")`). Tests that install a
  `MetricRecorder` use `MetricRecorder.ForServer("Dexpace.Sdk", host, …)` with a request to `TestHosts.Unique()`.
  Behavioural assertions are **not** changed in this task: every test must stay green on the as-built policy (a pin of the
  migration). The one expected behavioural effect is that attempt spans now have the scoped root as parent, which no existing
  assertion reads.

**PublicAPI:** none (tests only). **IDs:** none (support). **Verify:** V-fast `ScopedRecorderTests`,
`InstrumentationPolicyTests`, `DexpacePipelineTests`, `InstrumentationContextTests`; then `dotnet test --project
tests/Dexpace.Sdk.Core.Tests --configuration Release` to see no cross-test leakage when the classes run in parallel.

### Task 1.3 — `HttpSemanticConventions` (P5c-8, P5c-9; design "Internal types")

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/HttpSemanticConventionsTests.cs`, class
`HttpSemanticConventionsTests` (internal access through `InternalsVisibleTo`; `Unit`):

- `MethodValue_is_the_method_for_the_nine_known_methods_and_OTHER_for_anything_else` (`[Theory]`: `GET HEAD POST PUT DELETE
  CONNECT OPTIONS TRACE PATCH` return themselves, `Method.Of("PURGE")` and another vendor token (say `Method.Of("MKCOL2")`) return `_OTHER`; `Method.Of("get")` is
  normalised by `Method.Of` itself to `Method.Get` and yields `"GET"`; the
  comparison is ordinal, as the conventions' known-method set is case-sensitive; P5c-9)
- `SpanName_is_the_method_for_a_known_method_and_HTTP_otherwise`
- `ErrorType_of_a_status_is_the_cached_decimal_string_for_400_to_599` (`ReferenceEquals` on two calls; values outside 400–599
  fall back to a freshly formatted string)
- `ErrorType_of_an_exception_is_its_full_type_name` (and a nested/generic type does not throw)
- `BoxedStatusCode_returns_the_same_boxed_instance_for_100_to_599` (`ReferenceEquals`; outside the range returns a new box)
- `ProtocolVersion_maps_every_Protocol_member` (`Http10` "1.0", `Http11` "1.1", `Http2` and `H2PriorKnowledge` "2", `Quic`
  "3"; an undefined enum value returns `null`, never throws)
- `The_attribute_name_constants_are_the_stable_convention_keys` (a pin of the string values of every constant the other tasks
  use: `http.request.method`, `http.request.method_original`, `http.request.resend_count`, `http.response.status_code`,
  `server.address`, `server.port`, `url.scheme`, `url.full`, `network.protocol.version`, `error.type`, and the `dexpace.*`
  names of Position E)

Red: CS0103 (`HttpSemanticConventions`).

**Production.** New `src/Dexpace.Sdk.Core/Diagnostics/HttpSemanticConventions.cs`:
`internal static class HttpSemanticConventions` with `const string` attribute and event names (Position E's three events and
their attributes among them: `dexpace.attempt.failed`, `dexpace.retry.exhausted`, `dexpace.redirect.hop`,
`dexpace.retry.delay`, `dexpace.retry.attempts`, `dexpace.redirect.hop`, `dexpace.redirect.cross_origin`), and:

- `static string MethodValue(Method method)` (a `FrozenSet<string>` of the nine, `StringComparer.Ordinal`; no allocation);
- `static string SpanName(Method method)`;
- `static string ErrorType(Exception ex)` and `static string ErrorType(int status)` (a `string[200]` cache for 400–599,
  built once);
- `static object BoxedStatusCode(int status)` (an `object[500]` cache for 100–599);
- `static string? ProtocolVersion(Protocol protocol)`.

**PublicAPI:** none. **IDs:** `OBS-21` (vocabulary), `OBS-32` (attribute names). **Verify:** V-fast
`HttpSemanticConventionsTests`.

### Task 1.4 — `CallState.Transmissions` (P5c-8; Position F "The transmission ordinal")

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/CallStateTransmissionTests.cs`:

- `The_first_transmission_is_ordinal_zero_and_each_next_is_one_more` (`CallState` built through `TestContexts.For(…).State`;
  `NextTransmission()` returns 0, 1, 2)
- `Concurrent_increments_are_never_lost_or_duplicated` (64 tasks × 100 increments; the set of returned ordinals is
  `0..6399` exactly)
- `Transmissions_reads_the_count_so_far`
- `A_context_copy_shares_the_counter` (`context.ForAttempt(1).State` is the same `CallState`; ordinals continue)

Red: CS1061 (`NextTransmission`, `Transmissions`).

**Production.** `CallState` (`src/Dexpace.Sdk.Core/Pipeline/CallState.cs`) gains `private int _transmissions;`,
`internal int NextTransmission() => Interlocked.Increment(ref _transmissions) - 1;` and
`internal int Transmissions => Volatile.Read(ref _transmissions);`. (The exhaustion record is task 2.3.)

**PublicAPI:** none. **IDs:** none directly (`resend_count` source). **Verify:** V-fast `CallStateTransmissionTests`.

### Task 1.5 — `HttpClientMetrics` (`OBS-31`, `OBS-32`, `OBS-33`; P5c-10; Position F)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/HttpClientMetricsTests.cs`, class
`HttpClientMetricsTests` (`[Collection("Instrumentation")]`, `Unit`; every test uses `TestHosts.Unique()` and
`MetricRecorder.ForServer`):

- `Duration_has_the_stable_attribute_set` (one measurement carrying `http.request.method`, `server.address`, `server.port`,
  `url.scheme`, `http.response.status_code`, `network.protocol.version`; no `error.type`; `OBS-32`)
- `Duration_is_in_seconds_and_the_unit_is_s` (instrument `Unit == "s"`; value is `< 5` for an instantaneous fake; `OBS-32`)
- `Active_requests_has_the_start_attribute_set_and_the_unit_request` (`Unit == "{request}"`; the `+1` and `-1`
  measurements carry the **identical** tag list: method, `server.address`, `server.port`, `url.scheme`; `OBS-32`)
- `Active_requests_returns_to_zero_after_success_and_after_failure` (sum over the recorder is 0 in both; `OBS-31`, `OBS-33`)
- `Active_requests_does_not_decrement_what_it_did_not_increment` (a recorder created between `RequestStarted` and
  `RequestEnded` sees no `-1`: the pair is decided at start; `OBS-33`)
- `Server_port_is_the_port_number_for_a_default_port` (`https://host/` records `443`, `http://host/` `80`, an explicit port
  its value; never `-1`; P5c-8 — the `server.port` fix)
- `A_4xx_sets_error_type_to_the_status_code_string_on_the_duration` (`404` → `error.type == "404"`, and
  `http.response.status_code == 404` also present; P5c-8)
- `A_failure_sets_error_type_to_the_exception_type_and_no_status_code`
- `An_unknown_method_is_OTHER_on_both_instruments` (`Method.Of("PURGE")`; P5c-9)
- `The_histogram_tolerates_NaN_and_infinity` (call `RequestDuration.Record(double.NaN)`, `PositiveInfinity` and
  `NegativeInfinity` with a listener and with none; nothing throws; `OBS-33`)
- `The_instruments_manufacture_the_three_kinds_with_per_measurement_tags` (over `DexpaceDiagnostics.Meter`: a scratch
  `Counter<long>`, `UpDownCounter<long>` and `Histogram<double>` created on it record with tags; `OBS-31`)
- `The_histogram_carries_the_bucket_advice` (only if V1 passed: `Instrument.Advice?.HistogramBucketBoundaries` equals
  `[0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]`; if V1 failed the test is not written and the
  checklist records "advice dropped")
- `A_throwing_measurement_callback_propagates` (a `MeterListener` whose callback throws; `RequestEnded` throws that
  exception; nothing in `HttpClientMetrics` catches; `OBS-20` evidence)

Red: CS0103 (`HttpClientMetrics`).

**Production.** New `src/Dexpace.Sdk.Core/Diagnostics/HttpClientMetrics.cs`: `internal static class HttpClientMetrics`:

- `internal static readonly Histogram<double> RequestDuration` and `ActiveRequests` (`UpDownCounter<long>`), created on
  `DexpaceDiagnostics.Meter`, same names, units and descriptions as the policy's statics today (moved out of the policy);
  the histogram with `InstrumentAdvice<double>` per V1.
- `internal static bool RequestStarted(Request request)`: returns `false` and does nothing when `!ActiveRequests.Enabled`;
  otherwise builds one `TagList` of the four start attributes (`HttpSemanticConventions.MethodValue`, host, `request.Url.Port`
  as `BoxedStatusCode`-style cached boxes are **not** used for ports: ports box a new `int`, enabled path only, which is
  allowed), calls `ActiveRequests.Add(1, tags)` and returns `true`.
- `internal static void RequestEnded(Request request, long startTimestamp, bool counted, Response? response, Exception? failure)`:
  if `counted`, `ActiveRequests.Add(-1, startTags)`; if `RequestDuration.Enabled`, build the duration `TagList` (start attributes
  plus `http.response.status_code` boxed from `BoxedStatusCode`, `network.protocol.version` from `ProtocolVersion`, and
  `error.type` for a `>= 400` status or a failure) and `RequestDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags)`.
  The `-1` tag list is rebuilt from the same request (identical content), so no tag list is stored across the call.
- `TagList` is a stack struct; both methods are `MA0051`-safe by splitting `AppendStartTags(ref TagList, Request)` and
  `AppendOutcomeTags(ref TagList, Response?, Exception?)`.

No call to `Stopwatch` as an object; `Stopwatch.GetTimestamp()` is the caller's (task 1.6).

**PublicAPI:** none. **IDs:** `OBS-31`, `OBS-32`, `OBS-33`; `OBS-20` (5b's) evidence. **Verify:** V-fast
`HttpClientMetricsTests`.

### Task 1.6 — `AttemptTelemetry` (`OBS-21`, `OBS-25`; P5c-8, P5c-13; Position F)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/AttemptTelemetryTests.cs`, class
`AttemptTelemetryTests` (`[Collection("Instrumentation")]`, `Unit`; scoped recorders):

- `An_untraced_begin_holds_no_span_and_computes_no_url` (bundle `None`: `Begin` returns a telemetry whose `Activity` is null;
  asserted with a counting `UrlRedactor` seam is not possible on a static, so the assertion is on `Activity == null` and
  the zero-allocation test of task 2.8)
- `A_traced_begin_starts_a_client_span_under_the_bundle_with_the_start_tags` (`Kind == Client`; name from
  `HttpSemanticConventions.SpanName`; tags `http.request.method`, `server.address`, `server.port` (443 for
  `https://h/`), `url.scheme`, `url.full` redacted; the span's `ParentId` is the bundle's span id)
- `The_first_transmission_omits_resend_count_and_a_later_one_carries_its_ordinal` (call `Begin` three times against one
  `CallState`: absent, `1`, `2`; P5c-8)
- `An_unknown_method_is_OTHER_with_the_original_and_the_span_is_named_HTTP` (P5c-9)
- `Succeeded_sets_status_code_protocol_version_and_leaves_status_unset_for_a_2xx`
- `Succeeded_with_a_4xx_or_5xx_sets_error_type_to_the_status_string_and_status_Error_without_description` (P5c-8)
- `Failed_sets_error_type_to_the_exception_type_and_status_Error_with_the_message` (the as-built behaviour, pinned)
- `A_non_recording_span_is_not_written_to` (a listener whose `Sample` returns `PropagationData`: `IsAllDataRequested` is
  false; `Succeeded`/`Failed` leave `TagObjects` empty and `Status` unset; the SDK's mutators are guarded; `OBS-21`)
- `End_is_idempotent_and_stops_the_span_once` (`ActivityStopped` count is 1 after two `End()` calls; V4)
- `A_throwing_ActivityStopped_propagates_from_End` (`OBS-20`/`OBS-30` evidence; nothing catches)
- `The_metrics_pair_is_recorded_by_End` (a `ForServer` recorder: one duration measurement and a `-1`, even when `Failed` ran)

Red: CS0103 (`AttemptTelemetry`).

**Production.** New `src/Dexpace.Sdk.Core/Diagnostics/AttemptTelemetry.cs`:
`internal struct AttemptTelemetry` (a `struct`, not `readonly`, with `private bool _ended`), constructed by
`static AttemptTelemetry Begin(Request request, PipelineContext context)`:

1. `Activity? activity = StartSpan(context.Instrumentation, name, ActivityKind.Client)` — the bundle's `StartActivity`, **with
   R2's interim fallback** to the static source when the bundle is `None` (a `// R2: removed in PR 2 task 2.4` comment).
2. `var ordinal = context.State.NextTransmission();` always (an `Interlocked` increment, no allocation).
3. If `activity is { IsAllDataRequested: true }`: set the start tags (`url.full` from `UrlRedactor.Default.Redact(request.Url)`
   here and nowhere else in the attempt path; `server.port` is `request.Url.Port`), `http.request.resend_count` only when
   `ordinal > 0`, and `http.request.method_original` for `_OTHER`.
4. `long start = Stopwatch.GetTimestamp(); bool counted = HttpClientMetrics.RequestStarted(request);`

Members: `Activity? Activity`, `long StartTimestamp` (shared with 5b's `http.response.duration_ms`, P5c-18 (c)),
`Succeeded(Response)`, `Failed(Exception)`, `End()`. `End()` sets `_ended`, then `HttpClientMetrics.RequestEnded(...)`, then
`activity?.Stop()`/`Dispose()` per V4, in that order **only if not already ended**. Every tag write is behind
`activity is { IsAllDataRequested: true }`. No `try`/`catch` in the type.

**PublicAPI:** none. **IDs:** `OBS-21`, `OBS-25` (attempt-side), `OBS-32`; `OBS-20` evidence. **Verify:** V-fast
`AttemptTelemetryTests`.

### Task 1.7 — Restructure `InstrumentationPolicy` (the 5c half; P5c-13, P5c-17; R2) — Breaking 3, 4, 6

**Failing tests first.** Edit `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/InstrumentationPolicyTests.cs`. Existing
assertions that **change** (each in its own commit hunk so the diff is the evidence):

- `ProcessAsync_Activity_HasExpectedOtelTags`: unchanged assertions pass; add `server.port` for a default port in the new
  `Server_port_is_the_port_number_for_a_default_port` (`https://api.example.com/` → `443`; the old value was `-1`). **Breaking 3.**
- `ProcessAsync_AttemptNumber_SetOnResendCountTag` is renamed `Resend_count_is_absent_on_the_first_transmission_and_counts_retries`:
  `Activities[0]` has **no** `http.request.resend_count`, `Activities[1]` carries `1` (was `0` and `1`). **Breaking 3.**
- `ProcessAsync_DurationHistogram_CarriesMethodAndStatusTags` gains the attributes of `OBS-32`'s stable set (the single
  assertion list is replaced by the full set; `server.address` identifies the measurement through `TestHosts.Unique()`).
  **Breaking 4.**
- `ProcessAsync_RecordsDurationHistogram` and `ProcessAsync_ActiveRequestsCounter_IncrementsThenDecrements` move to
  `MetricRecorder.ForServer` (task 1.2 already did the construction; assertions unchanged).

New facts in the same class:

- `A_4xx_response_sets_error_type_and_Error_status_on_the_attempt_span` (a 404 from the transport: `error.type == "404"`,
  `Status == Error`, `StatusDescription` null; **Breaking 3**)
- `A_redirect_hop_continues_the_resend_count` (a `ScriptedTransport` 302 then 200 with `RedirectPolicy` and
  `InstrumentationPolicy`: the second attempt span carries `http.request.resend_count == 1`; P5c-8)
- `An_unknown_method_is_OTHER_and_the_span_is_named_HTTP`
- `The_attempt_span_is_a_child_of_the_ambient_activity_when_the_bundle_is_that_activity` (the interim parenting under R2:
  `ParentId == recorder.Root.Id`)
- `With_no_ambient_activity_the_attempt_span_is_still_started` (**R2's fallback**, removed by task 2.4: no scoped recorder,
  a plain `ActivityRecorder`, no ambient activity; one `Client` span)
- `A_throwing_ActivityStopped_after_a_response_exists_disposes_the_response_then_propagates` (the listener throws from
  `ActivityStopped`; `TrackingResponseBody` records a dispose before the exception reaches the caller; **Breaking 6**, P5c-13)
- `A_throwing_ActivityStarted_propagates_and_no_response_is_leaked` (the transport is never reached; `OBS-30`)
- `Spans_and_metrics_record_at_the_default_log_level` (`NullLogger`, no logging configuration: one attempt span and one
  duration measurement; `OBS-34` is 5b's row and cites this test)
- `Process_sync_records_activity_and_returns_the_response` (existing) and a new
  `The_sync_path_fills_the_same_tags_as_the_async_path`.

Red: the three changed assertions fail on the as-built values (`-1`, `0`, the old tag list), the new facts fail for the missing
behaviour or compile error.

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/InstrumentationPolicy.cs`:

- Delete the two static instruments (they live in `HttpClientMetrics`), `Stopwatch.StartNew()`, the up-front
  `s_redactor.Redact(...)`, and the `MA0051` waiver block (`#pragma warning disable/restore MA0051`).
- `ProcessCoreAsync` becomes an orchestrator under 70 lines:

  1. null checks; `var telemetry = AttemptTelemetry.Begin(request, context);`
  2. stamp `traceparent`/`tracestate` when `telemetry.Activity` is a W3C span (private static `Stamp(request, activity)`, same
     logic as built; P5c-12) and `downstream = context.WithActivity(telemetry.Activity)`;
  3. log "sending" **only under** `_logger.IsEnabled(LogLevel.Debug)` (the redacted URL is computed inside that guard; 5b
     replaces this half, P5c-17);
  4. `Response? response = null; try { response = await/Run(...); telemetry.Succeeded(response); log received (guarded); telemetry.End(); return response; }`
  5. `catch (Exception ex) when (response is not null && !ExceptionFacts.IsFatal(ex))` — a failure after the response exists
     (a throwing listener at `End`): `Disposal.DisposeQuietly(Async)(response, ex)` then `throw;` (P5c-13);
  6. `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` — `telemetry.Failed(ex)`; the failure log (guarded by
     `IsEnabled(Warning)`, redacted URL computed inside it); `throw;`
  7. `finally { telemetry.End(); }` (idempotent, so the fatal path stops the span and balances the counter).

  The `async`/sync fork is one conditional in step 4 and one in step 5; if the method exceeds 70 lines, the forward call
  moves to a private `ForwardAsync`.
- Remarks rewritten per design "Public API surface" (parenting through the bundle; the attribute list; `resend_count`;
  `error.type` for 4xx/5xx; `_OTHER`; metric attributes; the stamping/stripping split) with `<b>Breaking:</b>` paragraphs for
  Breaking 3, 4 and 6.
- Constructor unchanged (5b's to change, P5c-17). If 5b has landed first, rebase: keep 5b's log delegates and guard, replace
  only the span/metric half, and keep 5b's waiver removal if it already happened.

**PublicAPI:** none (`InstrumentationPolicy`'s public shape is untouched). **IDs:** `OBS-21`, `OBS-25`, `OBS-31`–`OBS-33`;
`OBS-20`/`OBS-34` (5b's rows) evidence. **Verify:** V-fast `InstrumentationPolicyTests`, `DexpacePipelineTests`,
`RetryPolicyTests`, `RedirectPolicyTests`.

### Task 1.8 — Close-out (PR 1)

- `CHANGELOG.md` `[Unreleased]`, under `### Changed`, one line per breaking behaviour landed here: attempt-span `server.port`
  is the port number (was `-1`); `http.request.resend_count` counts redirect hops and is absent on the first transmission
  (was `0`); a 4xx/5xx response sets `error.type` and `Error` on the attempt span; an unknown method is `_OTHER` (span named
  `HTTP`, original on `http.request.method_original`); the metrics gain the stable attribute set and bucket advice; a
  throwing `ActivityStopped` after a response exists disposes it (design Breaking 3, 4, 6). Entries are written in the
  imperative, no attribution.
- Run **V-gate**. **Commits:** `test: scoped diagnostics recorders and the NoDiagnosticListeners collection`, then
  `feat!: attempt span and metrics follow the HTTP client conventions (OBS-31..OBS-33)`.

---

## PR 2 — The operation span, the bundle and the events

**Gate: PR 1 merged.** Rows: `OBS-21`–`OBS-23`, `OBS-25`–`OBS-30`. Files:
`src/Dexpace.Sdk.Core/Diagnostics/OperationTelemetry.cs`, `src/Dexpace.Sdk.Core/Pipeline/{HttpPipeline,CallState,PipelineStage,PipelineContext}.cs`,
`Policies/{RetryPolicy,OperationPolicy}.cs`, `src/Dexpace.Sdk.Core/Diagnostics/{AttemptTelemetry,DexpaceDiagnostics}.cs`,
`BannedSymbols.txt`, `tests/Dexpace.Sdk.Core.Tests/{Diagnostics,Pipeline,Execution}/*`, `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.

### Task 2.1 — Pre-flight: re-derive 5a and 5b's surface (no commit)

Read the merged `InstrumentationPolicy`, `DexpaceClientOptions` and `UrlRedactor` at the PR's base. Record, in the PR
description: (a) whether 5b's constructor parameter, redactor instance and emission guard exist and what they are called
(P5c-17, P5c-18 (a)); (b) whether 5a made `DexpaceClientOptions` immutable (P5c-19, no 5c change); (c) whether
`SEAM-28` has a carrier for the operation id (design Position C, P5c-4); (d) whether 5b's census owns `OBS-24` (R5). The plan's
tasks 2.2 to 2.8 read none of these; they decide only the rebase notes in tasks 2.4 and 2.9. If the merged code contradicts a
contract the design assumed (P5c-17 to P5c-21), stop and take it to the lead rather than working around it.

### Task 2.2 — `OperationTelemetry`: start, complete, fail, stop (`OBS-21`, `OBS-29`; P5c-4, P5c-5; R1)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/OperationTelemetryTests.cs`, class
`OperationTelemetryTests` (`[Collection("Instrumentation")]`, `Unit`; scoped recorders; a `CallState` from
`TestContexts.For(…).State`):

- `Start_with_no_listener_returns_null` (no recorder: `OperationTelemetry.Start(request)` is `null`)
- `Start_opens_an_internal_span_named_by_the_normalised_method` (`Kind == Internal`; name `GET`; for `Method.Of("PURGE")` the
  name `HTTP` and tag `http.request.method == "_OTHER"`, `http.request.method_original == "PURGE"`)
- `Start_sets_the_seed_tags_and_the_redacted_url_only_when_recording` (a `Sample` returning `PropagationData`: no tags written;
  full sampling: `server.address`, `server.port` 443, `url.full` with the query value `***`)
- `Complete_records_the_final_status_code_and_leaves_status_unset` (P5c-5; **not** `Ok`)
- `Fail_records_the_exception_event_error_type_and_Error_status` (an `event` named `exception` with
  `exception.type == "…ServiceRequestException"`; `error.type` the full name; `Status == Error` with the message; per V2 and V3)
- `Fail_for_a_cancellation_is_a_failure` (`OperationCanceledException` is `Error`; the deadline case is the same type)
- `Stop_is_idempotent_and_notifies_the_listener_once` (V4)
- `Stop_with_nothing_settled_sets_Error_without_description_and_no_exception_event` (`Stop(activity, settled: false)`, the
  fatal-exception case; `StatusDescription` null; `Events` empty; R1)
- `Stop_after_Complete_does_not_set_Error` (`settled: true`)
- `Every_mutator_is_a_no_op_on_a_null_span` (all four called with `null`; no throw; no allocation is asserted in task 2.8)
- `Every_mutator_skips_a_non_recording_span` (PropagationData sampling; `Complete`/`Fail` leave the span untouched; `OBS-21`)

Red: CS0103 (`OperationTelemetry`).

**Production.** New `src/Dexpace.Sdk.Core/Diagnostics/OperationTelemetry.cs`: `internal static class OperationTelemetry`:

- `static Activity? Start(Request seed)`: `DexpaceDiagnostics.ActivitySource.StartActivity(HttpSemanticConventions.SpanName(seed.Method), ActivityKind.Internal)`;
  tags only `if (activity is { IsAllDataRequested: true })`. The name is the method (the `SEAM-28` carrier does not exist,
  P5c-4; recorded ⏳ in the cross-owner table).
- `static void Complete(Activity? activity, Response response)`: `http.response.status_code` boxed from `BoxedStatusCode`
  under the recording guard. Status untouched.
- `static void Fail(Activity? activity, Exception ex, CallState state)`: recording-guarded; in order, (1) the exhaustion event
  (task 2.3 adds the call; this task leaves a marked no-op hook), (2) `activity.AddException(ex)` per V2/V3 (hand-written
  fallback otherwise), (3) `error.type`, (4) `SetStatus(Error, ex.Message)`.
- `static void Stop(Activity? activity, bool settled)`: `if (!settled && activity is { IsAllDataRequested: true }) SetStatus(Error)`;
  then `activity?.Stop()` and `Dispose()` per V4, guarded so the listener is notified once.

No `try`/`catch` anywhere in the type. `MA0051`: keep `Start` short by extracting `SetSeedTags(Activity, Request)`.

**PublicAPI:** none. **IDs:** `OBS-21`, `OBS-29` (lifecycle), `OBS-28`. **Verify:** V-fast `OperationTelemetryTests`.

### Task 2.3 — The event shape and the exhaustion record (`OBS-28`, `OBS-29`; P5c-6, P5c-7)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/OperationEventTests.cs`, class `OperationEventTests`
(same setup as task 2.2; a `PipelineContext` whose `Instrumentation` is `FromActivity(operation)`):

- `Attempt_failed_carries_resend_count_error_type_status_and_delay_in_seconds` (an exception failure: `error.type` the type
  name, no `http.response.status_code`; a response failure: `error.type == "503"`, `http.response.status_code == 503`;
  `dexpace.retry.delay` is a `double` equal to `TimeSpan.TotalSeconds`; `http.request.resend_count` is
  `Transmissions - 1`, `0` for the first; R3)
- `Attempt_failed_is_emitted_on_the_operation_span_not_the_attempt_span` (the event is in `operation.Events`)
- `Nothing_is_emitted_on_an_untraced_call` (bundle `None`: no event, no exception; zero allocation asserted in task 2.8)
- `Nothing_is_emitted_on_a_non_recording_operation_span`
- `RetriesExhausted_records_on_the_call_state_and_emits_nothing_yet` (`CallState.TryGetExhaustion(out attempts)` is `true`;
  `operation.Events` empty — P5c-6)
- `RetrySequenceStarted_clears_the_exhaustion_record` (the "earlier hop cannot leak" mechanism, P5c-6)
- `Fail_emits_exhausted_immediately_before_the_exception_event` (after `RetriesExhausted(…, 3)`: `Events` is exactly
  `[dexpace.retry.exhausted, exception]`, in that order, no event between; `dexpace.retry.attempts == 3`;
  `error.type` of the first equals `exception.type` of the second; `OBS-29`)
- `Fail_without_an_exhaustion_record_emits_only_the_exception_event`
- `Exhaustion_on_an_earlier_hop_does_not_leak_into_a_later_one` (`RetriesExhausted`, then `RetrySequenceStarted`, then
  `Fail`: no exhausted event)
- `RedirectHop_carries_hop_status_the_redacted_target_and_cross_origin` (`dexpace.redirect.hop == 1`,
  `http.response.status_code == 302`, `url.full` has the query value `***`, `dexpace.redirect.cross_origin` is `bool`)
- `RedirectHop_redacts_the_target` (a target with userinfo and a `sig=secret` query: neither string appears in any event
  attribute; `XCUT-19`, the same redactor as the span)

Red: CS1061/CS0103 (the new `OperationTelemetry` members, `CallState` members).

**Production.**

- `CallState` gains `private bool _exhausted; private int _exhaustedAttempts;` under the existing `_gate`, and
  `internal void RecordExhaustion(int attempts)`, `internal void ClearExhaustion()`,
  `internal bool TryGetExhaustion(out int attempts)`.
- `OperationTelemetry` gains the Position E entry points, each a no-op unless `context.Instrumentation.ActiveSpan is
  { IsAllDataRequested: true } operation`:
  `AttemptFailed(PipelineContext context, Response? response, Exception? failure, TimeSpan nextDelay)`,
  `RetrySequenceStarted(PipelineContext context)` (clears the record; always runs, since clearing is state, not telemetry),
  `RetriesExhausted(PipelineContext context, int attempts)` (records; always runs), and
  `RedirectHop(PipelineContext context, int hop, int statusCode, Uri target, bool crossOrigin)` (the target through
  `UrlRedactor.Default`, or 5b's instance when it exists, P5c-18 (a)).
- `Fail`'s marked hook from task 2.2 now emits `dexpace.retry.exhausted` (`dexpace.retry.attempts`, `error.type =
  HttpSemanticConventions.ErrorType(ex)`) when `state.TryGetExhaustion(out var n)`, immediately before `AddException`.
- Attribute values are boxed from the static caches where they exist; the events are built only under the recording guard
  (no allocation untraced).

**PublicAPI:** none. **IDs:** `OBS-28`, `OBS-29`. **Verify:** V-fast `OperationEventTests`.

### Task 2.4 — `HttpPipeline.SendCoreAsync`: the span, the bundle, the lifecycle (`OBS-25`, `OBS-26`, `OBS-28`, `OBS-29`; P5c-2, P5c-3, P5c-13; R2) — Breaking 1, 2

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/OperationSpanLifecycleTests.cs`, class
`OperationSpanLifecycleTests` (`[Collection("Instrumentation")]`, `Unit`; scoped recorders; pipelines from
`DexpacePipeline.CreateDefault` and `new PipelineBuilder()…Build`):

- `A_succeeding_call_ends_one_operation_span_without_error` (exactly one `Internal` span named `GET` per call; `Status` not
  `Error`; `http.response.status_code` 200; `OBS-29`)
- `A_returned_4xx_without_error_mapping_is_a_successful_operation` (a pipeline without `ErrorMappingPolicy`, a 404: the
  operation span is not `Error`, the attempt span is; Position C)
- `A_returned_4xx_on_the_default_pipeline_is_a_successful_operation` (`CreateDefault` as built, a 404: the response is
  returned, the operation span is not `Error`, the attempt span is; the default pipeline does not map, see finding 11)
- `A_mapped_4xx_fails_the_operation` (`CreateDefault`'s composition plus an explicit `.Add(new ErrorMappingPolicy())` at
  `PerCall`: `HttpResponseException`; the operation span is `Error` with an `exception` event; the attempt span is `Error`
  too)
- `A_retry_exhausted_call_ends_with_exhausted_then_exception_carrying_the_same_type` (default pipeline over a transport that
  fails `ServiceRequestException` every time, `MaxRetryAttempts = 2`: the operation `Events` end with
  `dexpace.retry.exhausted`, `exception`, adjacent; the two events name the same exception type; one operation span stopped
  once; three attempt spans; `OBS-29`) — the `ActivityListener` ordering test driven through `ActivityStarted`/`ActivityStopped`
  callbacks, asserting the stop order attempt, attempt, attempt, operation
- `A_returned_503_without_error_mapping_emits_no_exhausted_event` (a pipeline without `ErrorMappingPolicy`, retry budget
  spent, response returned: the operation succeeds, so **no** `dexpace.retry.exhausted`; P5c-6)
- `A_returned_503_with_error_mapping_emits_exhausted_then_exception` (`CreateDefault`'s composition plus an explicit
  `ErrorMappingPolicy`; `HttpResponseException` is the exception type of both)
- `A_returned_503_on_the_default_pipeline_emits_no_exhausted_event` (`CreateDefault` as built, budget spent: the 503 is
  returned, the operation succeeds, so no `dexpace.retry.exhausted`)
- `A_deadline_cancellation_after_exhaustion_does_not_pair_the_wrong_exception` (budget spent, then an
  `OperationCanceledException` from the deadline: the exhausted event names `OperationCanceledException`; the pairing holds
  by construction)
- `Attempt_spans_are_children_of_the_operation_span` (`ParentId == operation.Id`; same `TraceId`; the operation span is a child
  of `recorder.Root`)
- `An_attempt_span_exists_only_under_an_operation_span` (Position B; replaces R2's task-1.7 fact: with a `Sample` that drops
  the operation span but accepts the source otherwise, **no** attempt span is created for that call)
- `A_fatal_exception_ends_the_operation_span_in_error_without_an_exception_event` (an `OutOfMemoryException` from the
  transport; `Status == Error`, `StatusDescription` null, no `exception` event; the span is stopped once; the exception
  propagates unchanged; RETRY-25)
- `A_throwing_ActivityStopped_on_the_operation_span_disposes_the_response_before_propagating` (P5c-13, Breaking 6)
- `Send_of_T_ends_the_operation_span_before_the_handler_runs` (a handler that reads `Activity.Current` and the recorder's
  `Stopped` list; documented behaviour, P5c-4)
- `The_operation_span_exists_for_every_pipeline_shape` (`CreateEmpty` and a custom pipeline with no `OperationPolicy`; one
  operation span each; PIPE-39 reconciled)
- `A_traced_call_bundle_is_the_operation_span` (a probe policy reads `context.Instrumentation`: `ActiveSpan` is the operation
  span, `TraceId`/`SpanId` equal its ids, `TraceIdFormat == W3C`; `IsValid`; `CallKey.TraceId == operation.TraceId`;
  `CTX-14`, `OBS-26`)
- `An_untraced_call_bundle_is_None_even_under_an_ambient_activity` (no `Dexpace.Sdk` listener but an ambient activity from
  another source: `ReferenceEquals(context.Instrumentation, InstrumentationContext.None)`; `CallKey.TraceId` is zero;
  `Activity.Current` is unchanged afterwards; `CTX-15`, `OBS-25`; P5c-3)
- `A_traced_bundle_has_W3C_ids_and_is_valid` (32 and 16 lower-case hex characters, `OBS-26`)
- `The_dispatch_context_is_not_re_keyed` (the `CallKey` of the first and the last link are equal; the span is opened before
  the key is minted; 4a's coupling obligation)

Existing tests that change: `InstrumentationPolicyTests.With_no_ambient_activity_the_attempt_span_is_still_started` (R2's
fallback fact) is **deleted** and replaced by `An_attempt_span_exists_only_under_an_operation_span`;
`DexpacePipelineTests` span-count assertions now expect `1 operation + n attempt` (read each, list the decision in the commit
message); `InstrumentationPolicyTests` tests that drive `new PipelineBuilder().Add(policy).Build(transport)` now see an
operation span in `recorder.Started`: they filter on `Kind == ActivityKind.Client` through one helper
`ClientSpans(recorder)`. Every other `Security` and non-diagnostic class is untouched.

Red: the new facts fail (no operation span; the bundle is `FromActivity(Activity.Current)`).

**Production.**

- `HttpPipeline.SendCoreAsync` (`src/Dexpace.Sdk.Core/Pipeline/HttpPipeline.cs`) per Position B, in outline:

  ```csharp
  var operation = OperationTelemetry.Start(request);                              // null with no listener
  var dispatch = new DispatchContext(InstrumentationContext.FromActivity(operation));   // None when null
  var context = PipelineContext.Create(request, options, requestOptions, dispatch, cancellationToken);
  var runner = new PipelineRunner(_entries, 0, _terminal);
  Response? response = null; var settled = false;
  try
  {
      response = async ? await runner.RunAsync(request, context).ConfigureAwait(false) : runner.Run(request, context);
      OperationTelemetry.Complete(operation, response); settled = true;
      OperationTelemetry.Stop(operation, settled);               // a throwing listener lands in the catch below
      return response;
  }
  catch (Exception ex) when (response is not null && !ExceptionFacts.IsFatal(ex))
  {   // Complete/Stop threw after the response exists (P5c-13)
      context.State.CloseFurthest(); /* dispose response quietly, async or sync, ex as primary */ throw;
  }
  catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
  {
      context.State.CloseFurthest(); OperationTelemetry.Fail(operation, ex, context.State); settled = true; throw;
  }
  finally { OperationTelemetry.Stop(operation, settled); }
  ```

  Keep the file under 70 lines for the method (`MA0051`), extracting `DisposeSuperseded(Response, Exception, bool async)`
  if needed. `ArgumentNullException.ThrowIfNull` guards stay first. The existing comment about CTX-17 stays.
- **Delete R2's fallback** from `AttemptTelemetry.Begin`: the bundle's `StartActivity` only.
- `CallKey`/`DispatchContext` are untouched: the key is minted from the bundle at construction, after the span exists.

**PublicAPI:** none. **IDs:** `OBS-25`, `OBS-26`, `OBS-28`, `OBS-29`; `CTX-14`, `CTX-15` (4a's) evidence. **Verify:** V-fast
`OperationSpanLifecycleTests`, `InstrumentationPolicyTests`, `DexpacePipelineTests`, `HttpPipelineTests`, `ContextChainWiringTests`,
`SeedOriginTests`, `EmptyPipelineTests`.

### Task 2.5 — Wire today's `RetryPolicy` to the emitter (P5c-6, P5c-7, P5c-20)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/RetryTraceEventsTests.cs`, class
`RetryTraceEventsTests` (`[Collection("Instrumentation")]`, `Unit`; default-shaped pipelines with `InstantTimeProvider`; scoped recorders):

- `A_retried_failure_emits_attempt_failed_with_the_next_delay` (one 503 then 200: one `dexpace.attempt.failed` on the
  operation span, `error.type == "503"`, `http.response.status_code == 503`, `http.request.resend_count == 0`,
  `dexpace.retry.delay` a `double`; no `dexpace.retry.exhausted`, no `exception`)
- `A_retried_exception_emits_attempt_failed_with_the_exception_type`
- `A_spent_budget_on_a_would_retry_failure_records_exhaustion` (interim predicate, P5c-6: `MaxRetryAttempts = 2`, three 503s,
  `CreateDefault`'s composition plus an explicit `ErrorMappingPolicy` at `PerCall` (the default pipeline alone returns the 503
  and emits no exhausted event): events are `dexpace.attempt.failed` ×2, `dexpace.retry.exhausted`
  (`dexpace.retry.attempts == 3`), `exception`)
- `A_non_replayable_request_is_not_exhausted_it_is_not_retried` (a stream body, a 503: no `attempt.failed`, no exhausted
  record, response returned)
- `A_non_idempotent_request_is_not_exhausted` (`POST` without `RetryNonIdempotentWhenReplayable`)
- `A_zero_budget_is_retries_off_not_exhausted` (`MaxRetryAttempts = 0`; `dexpace.retry.exhausted` never emitted; P5c-6)
- `A_non_retryable_status_is_not_exhausted` (a 404)
- `A_new_retry_sequence_clears_an_earlier_exhaustion` (a test-only policy at the `Redirect` stage drives its continuation a
  second time after receiving an exhausted 503 from the first run; the second run succeeds or fails differently, and the
  final failure, if any, is not paired with the stale record: `RetrySequenceStarted` clears it through `RetryPolicy`. With
  today's `RedirectPolicy` (301/302/303/307/308 only; a thrown exception ends the call) no hop ever follows an exhausted
  sequence, so the leak becomes reachable only with 6a/6b; see finding 12)
- `The_retry_policy_still_returns_and_throws_exactly_what_it_did` (a pin over the existing `RetryPolicyTests` outcomes with
  an operation span present; the retry facts of `RetryPolicyTests`, `RetryFactsTests` stay green unedited)

Red: the new facts fail (no events).

**Production.** `RetryPolicy.ProcessCoreAsync` (it already carries its `MA0051` waiver for 6a; **no restructure**, three call
sites):

1. `OperationTelemetry.RetrySequenceStarted(context);` once, before the loop.
2. Before each `SleepAsync`: compute `var delay = DelayFor(...)` first (the same call, same single `Random.Shared` draw, same
   order), then `OperationTelemetry.AttemptFailed(context, response, caughtException, delay);`, then
   `SleepAsync(delay, …)`. (Today `DelayFor(...)` is an argument of the `SleepAsync` call; hoisting it changes no behaviour.)
3. Where the loop gives up on a would-retry failure with `options.MaxRetryAttempts > 0` and `attempt >= options.MaxRetryAttempts`
   and `canRetryRequest` and (exception retryable or status retryable): `OperationTelemetry.RetriesExhausted(context, attempt + 1);`
   before rethrowing/returning. The condition is a small private static `IsExhausted(...)` so the waiver's rewrite by 6a
   replaces one method, not the loop.

`RetryPolicy`'s remarks gain a line stating that the policy reports attempts and exhaustion through the diagnostic events
(Position E) and that 6a keeps the same calls at the same decisions.

**PublicAPI:** none. **IDs:** `OBS-28`, `OBS-29`; `RETRY-*` (6a) evidence. **Verify:** V-fast `RetryTraceEventsTests`,
`RetryPolicyTests`, `RetryFactsTests`, `ReDriveRequestIsolationTests` and `RetryPacingOverflowTests` (both `Security`, run only,
never edited).

### Task 2.6 — Span scope, listener contract and concurrency (`OBS-21`, `OBS-22`, `OBS-23`, `OBS-30`; `OBS-20` evidence; R5)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/ActivityScopeTests.cs`, class `ActivityScopeTests`
(`[Collection("Instrumentation")]`, `Unit`; a plain `Send` and `SendAsync` through the default pipeline unless noted):

- `Activity_Current_is_restored_after_the_call_including_on_throw` (`[Theory]` over sync/async × success/failure: after the
  call `Activity.Current == recorder.Root`; `OBS-22`)
- `The_attempt_span_is_Activity_Current_inside_the_transport` (the fake transport records `Activity.Current`: the attempt
  span; `OBS-22`)
- `The_operation_span_is_Activity_Current_in_a_PerCall_policy` (a probe at `PerCall`: the operation span)
- `The_attempt_span_is_current_at_the_log_site` (a recording `ILogger` that captures `Activity.Current?.SpanId` inside
  `Log`: both the "sending" and "received" entries see the attempt span; with a failure, the failure entry too; `OBS-23`.
  Logs at Debug need `IsEnabled(Debug)` true on the test logger)
- `An_untraced_call_leaves_the_callers_Activity_Current_untouched_and_log_sites_see_it` (no `Dexpace.Sdk` listener, an ambient
  activity from another source; the logger sees that activity; `OBS-23`'s precondition)
- `Activity_Current_survives_a_thread_hop_in_the_transport` (a transport that `await Task.Yield()`s onto another thread:
  still the attempt span; restored after; `ExecutionContext` flow, `ASYNC-9`)
- `Sdk_writes_to_a_non_recording_span_are_skipped` (a `PropagationData` sampler across a full default-pipeline call: neither
  the operation nor the attempt span carries a tag, event or status; the call's behaviour is unchanged; `OBS-21`)
- `Stop_is_idempotent` (a span the SDK stopped can be `Stop`ped again by the listener's own code without a second
  `ActivityStopped`)
- `A_throwing_ActivityStarted_propagates_out_of_SendAsync_and_Send` (the exception is the listener's, unwrapped; the transport
  is not reached; no response leaked; on the async path `Activity.Current` is the caller's afterwards; `OBS-30`. A span whose
  start callback threw is the listener's contract violation: `ActivityStarted` runs inside `ActivitySource.StartActivity`,
  before the SDK holds the span, so stopping it would need a `try`/`catch` around the listener call, which convention 3
  forbids. The test asserts no "span is stopped" clause)
- `A_throwing_ActivityStopped_on_the_attempt_span_disposes_the_response_before_propagating` (P5c-13; task 1.7's fact over the
  full pipeline)
- `A_throwing_metric_callback_propagates_out_of_SendAsync` (a `MeterListener` callback throws; `OBS-20` evidence)
- `Concurrent_calls_keep_their_spans_in_their_own_traces` (64 concurrent `SendAsync`; each call's attempt spans share the
  trace of its own operation span and no other; each operation span has exactly the attempts of its own call; run under a
  recording listener; `OBS-30`)
- **Contingent on P5c-1 (R5)** `The_attempt_span_is_current_across_a_thread_hop_and_the_callers_is_restored_on_throw`
  (`OBS-24`'s ExecutionContext-flow evidence): written only if task 2.1 found 5b's census omits `OBS-24`.

Several of these are **pins** on behaviour tasks 1.7 and 2.4 already produce; each is proven able to fail by temporarily
removing the thing it pins (the `Dispose`/`Stop` call; the `IsAllDataRequested` guard), never committed.

**Production.** None expected. A failing fact here is a defect in task 1.6, 1.7 or 2.4: fix it there (amend that task's
commit) rather than patching around it.

**PublicAPI:** none. **IDs:** `OBS-21`, `OBS-22`, `OBS-23`, `OBS-30`. **Verify:** V-fast `ActivityScopeTests`.

### Task 2.7 — Trace-id flavours and the banned generator setter (`OBS-26`, `OBS-27`; P5c-14)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/TraceIdTests.cs`, class `TraceIdTests`
(`[Collection("Instrumentation")]`, `Unit`):

- `Generated_trace_ids_are_32_lowercase_hex_and_non_zero` (10 000 operation spans under a recording listener; every
  `TraceId.ToHexString()` is 32 characters of `[0-9a-f]` and never all zero; the sample check of V8, which cannot prove
  coercion)
- `Generated_span_ids_are_16_lowercase_hex_and_non_zero`
- `The_W3C_flavour_is_reported_and_the_no_op_flavour_is_the_zero_id` (a traced bundle `TraceIdFormat == W3C`;
  `InstrumentationContext.None.TraceId == default`, `.TraceIdFormat == Unknown`; `OBS-27`)
- `The_sdk_does_not_install_a_trace_id_generator` (read `Activity.TraceIdGenerator` after exercising a call under a listener:
  it is `null`, or exactly the value it had before the test; `OBS-27`)
- `A_legacy_hierarchical_parent_yields_a_valid_W3C_operation_span_or_a_documented_invalid_bundle` (only if the runtime default
  `Activity.DefaultIdFormat` allows constructing one in the test: a hierarchical ambient activity; whichever the runtime
  does, the bundle reports its real format and `IsValid` agrees; pins 4a's `FromActivity` on the new path)

Red: the first four are pins on the as-built runtime (confirm able to fail by asserting a wrong length); the build-gate half
is next.

**Production.** `BannedSymbols.txt` gains, under a new comment block citing `OBS-27` and design §10 entry 24 ("a library must
not set the process-wide trace-id hook the application owns"):

```
# Process-wide trace-id generation belongs to the application (OBS-27, design §10 entry 24, P5c-14).
P:System.Diagnostics.Activity.TraceIdGenerator;A library never sets the process-wide trace-id generator; the application owns it (OBS-27, design §10 entry 24)
```

Prove the ban works: temporarily add `Activity.TraceIdGenerator = null;` to a `src/Dexpace.Sdk.Core` file, see `RS0030` fail
the build with that message, remove it (never committed). If V8 found the runtime does **not** coerce a zero draw, the
checklist row and task 4.3's dated §10 entry 24 correction name the clause as admitted.

**PublicAPI:** none. **IDs:** `OBS-26`, `OBS-27`. **Verify:** V-fast `TraceIdTests`; `dotnet build Dexpace.Sdk.sln --configuration Release`.

### Task 2.8 — The zero-allocation untraced path (`OBS-25`; P5c-15)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/UntracedAllocationTests.cs`, class
`UntracedAllocationTests` (`[Collection("NoDiagnosticListeners")]`, `[Trait("Category", "Unit")]`). The class uses **no**
recorder, and its constructor asserts the precondition `!DexpaceDiagnostics.ActivitySource.HasListeners()` so a leaked
process-wide listener fails the test loudly instead of corrupting the measurement. Helper `AllocatedBytes(Action, int
warmup = 100, int iterations = 1000)` returns the `GC.GetAllocatedBytesForCurrentThread()` delta divided by `iterations`
after warm-up (a `static readonly` request, no per-iteration setup).

- `Untraced_tracing_primitives_allocate_nothing` (a `[Theory]` over each: `OperationTelemetry.Start(request)` is `null`;
  `InstrumentationContext.FromActivity(null)`; `InstrumentationContext.None.StartActivity("GET", ActivityKind.Client)`;
  `OperationTelemetry.Complete(null, response)`, `Fail(null, ex, state)`, `Stop(null, true)`; `AttemptTelemetry.Begin` with a
  `None` bundle then `End()`; `HttpClientMetrics.RequestStarted`/`RequestEnded` with no `MeterListener` (each `0` bytes per
  iteration); and `CallState.NextTransmission()`)
- `An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through` (two pipelines over the same
  synchronous fake transport that returns one pre-built `Response`: A has `new InstrumentationPolicy(NullLogger.Instance)`
  at `Diagnostics`; B has a pass-through `HttpPipelinePolicy` subclass at `Diagnostics` overriding the synchronous `Process`.
  `AllocatedBytes(() => A.Send(request, ct))` equals `AllocatedBytes(() => B.Send(request, ct))`. The sync path completes on
  the calling thread, so the per-thread counter is exact; the async path's state-machine box is the pipeline's, not
  tracing's)
- `Untraced_retry_and_redirect_event_calls_allocate_nothing` (`OperationTelemetry.AttemptFailed`, `RetrySequenceStarted`,
  `RetriesExhausted`, `RedirectHop` with a `None` bundle: `0` bytes; `RetriesExhausted`'s record is a bool and an int under
  the gate)
- `The_untraced_path_does_not_compute_the_redacted_url` (a pin through allocation: a request with a long query string
  allocates the same bytes as one with none)
- `The_precondition_catches_a_leaked_listener` (a meta-test: starts and disposes a recorder, then asserts `HasListeners()` is
  false again; guards the test environment)

If the pass-through comparison is not byte-equal, the delta names the allocation: fix the production path (the usual
suspects are a `TagList` built before an `Enabled` check, a boxed `int`, or the redacted URL) rather than loosening the
assertion. The comparison joins `OBS-1`'s disabled-log half only when 5b's log path is lazy (P5c-18 (e)): whichever of 5b and 5c
lands second makes the joint assertion; with 5c first the pass-through test uses `NullLogger` and `InstrumentationPolicy`'s
log guards (task 1.7) keep it exact.

Red: on a deliberately regressed path (an unguarded `s_redactor.Redact` restored locally) the comparison fails; confirm, then
remove the regression. The first test is red on compile only (the types exist), so it is a **pin** and is shown able to fail by
allocating inside `Begin` locally.

**Production.** None expected beyond fixes the red uncovers.

**PublicAPI:** none. **IDs:** `OBS-25` (and `OBS-1`'s untraced half by cross-reference). **Verify:** V-fast
`UntracedAllocationTests` run **alone** (`--filter-class`) and then inside the full suite, which proves the collection
excludes the parallel classes.

### Task 2.9 — Documentation of the changed members (P5c-2, P5c-16)

No tests (the build's CS1591/doc analyzers and the probe are the gate). Edit XML docs only:

- `PipelineStage.Operation`: "the pipeline opens the operation span around this stage; the stage's policy applies the overall
  deadline" (replacing "opens the operation span"; P5c-2). The type remarks' pillar paragraph is unchanged.
- `OperationPolicy` remarks: the operation span is the pipeline's and encloses this policy (it is not opened here).
- `PipelineContext.Instrumentation`: "the operation span's bundle, or `InstrumentationContext.None` when the SDK's source has
  no listener, even under an ambient activity" with `<b>Breaking:</b> was the caller's ambient activity`; `CallKey` remarks:
  the trace id is zero for an untraced call. `PipelineContext.Activity`: the attempt span, downstream of `Diagnostics` only
  (meaning unchanged, restated).
- `DexpaceDiagnostics` remarks: list the spans (`GET`-style operation span, `Client` attempt spans), the events
  (`dexpace.attempt.failed`, `dexpace.retry.exhausted`, `dexpace.redirect.hop`, `exception`) and the two instruments, with
  their units. No signature changes.
- `HttpPipeline` remarks: the operation span encloses the whole call and ends when the response is returned.

**PublicAPI:** none (doc comments only; `RS0016`/`RS0017` unaffected). **IDs:** none (documentation). **Verify:**
`dotnet build Dexpace.Sdk.sln --configuration Release`.

### Task 2.10 — NativeAOT smoke (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckTracingAndMetricsAsync()`, in the existing style (`Expect`, the in-process
`CannedHandler`), after `CheckPipelineReworkAsync`: an `ActivityListener` on `Dexpace.Sdk` records a default-pipeline
`SendAsync` and `Expect`s one `Internal` operation span with exactly one `Client` child whose `ParentId` is the operation's;
a `MeterListener` enabling `http.client.request.duration` and `http.client.active_requests` `Expect`s one duration
measurement and a balanced counter; a second call with the listeners disposed `Expect`s no span and no measurement. No
reflection; `AotSmoke` gets no `InternalsVisibleTo`. A trim or AOT warning means the source is fixed, not the smoke.
**PublicAPI:** none. **IDs:** `OBS-31`, `OBS-32` (NativeAOT evidence). **Verify:** `dotnet publish tests/Dexpace.Sdk.AotSmoke
--configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints the "all checks
passed" line.

### Task 2.11 — Close-out (PR 2)

- `CHANGELOG.md` `[Unreleased]`, `### Changed` (design Breaking 1, 2, 7): every `HttpPipeline` call opens an `Internal`
  operation span on `Dexpace.Sdk` when listened to, and attempt spans are its children, no longer children of the caller's
  `Activity.Current`; `PipelineContext.Instrumentation` is the operation span's bundle, or `InstrumentationContext.None` when
  the SDK has no listener, even under an ambient activity (it was the ambient activity), so `CallKey.TraceId` is zero for an
  untraced call; new operation-span events `dexpace.attempt.failed`, `dexpace.retry.exhausted` and `exception`. `### Added`:
  `Activity.TraceIdGenerator` is a banned API in `src/` (build gate, not a runtime change).
- Run **V-gate** and the coverage gate with its self-test. **Commits:** `test: untraced allocation, scope and listener
  contract tests`, then `feat!: the operation span, the correlation bundle and the attempt events (OBS-21..OBS-30)`.

---

## PR 3 — The adapter

**Gate: PR 2 merged** (the wire test needs both spans). Rows: none owned beyond design §8.1's stripping commitment (P5c-11,
P5c-12); `OBS-23`'s "traceparent on the wire" half is evidence only. Files:
`src/Dexpace.Sdk.Http.SystemNet/{SystemNetHttpClient,TraceContextStripping}.cs`, `src/Dexpace.Sdk.Http.SystemNet/README.md`,
`tests/Dexpace.Sdk.Http.SystemNet.Tests/{TraceContextStrippingTests,TracePropagationWireTests}.cs`.

### Task 3.1 — The strip rule and the propagation switch (pure functions; R4; P5c-11)

**Failing tests first.** New `tests/Dexpace.Sdk.Http.SystemNet.Tests/TraceContextStrippingTests.cs`, class
`TraceContextStrippingTests` (`[Trait("Category", "Unit")]`; no I/O):

- `A_traceparent_equal_to_the_current_activity_id_is_stripped_when_the_runtime_propagates` (`ShouldStripTraceparent(value,
  currentId, runtimePropagates: true)` is `true`)
- `A_traceparent_is_kept_when_the_runtime_does_not_propagate` (`runtimePropagates: false`; the SDK stamp survives; R4's
  "propagation off")
- `A_caller_traceparent_that_is_not_the_current_id_is_kept` (any other value; `null` current id)
- `A_null_current_activity_never_strips`
- `A_tracestate_is_stripped_only_when_it_equals_the_current_trace_state_and_the_traceparent_was_stripped`
- `The_header_name_comparison_is_case_insensitive_the_value_comparison_is_ordinal`
- `The_switch_resolver_prefers_the_AppContext_switch_then_the_environment_then_true` (`ResolvePropagation(bool? appContext,
  string? environment)`: `(false, *)` false, `(null, "0")`/`"false"` false, `(null, "1")`/`"true"` true, `(null, null)` true,
  an unparsable environment value true; names per V6)
- `The_runtime_value_is_read_once` (two reads return the same cached value; the reader is a `Lazy`/static, not a
  per-request read)

Red: CS0103 (`TraceContextStripping`).

**Production.** New `src/Dexpace.Sdk.Http.SystemNet/TraceContextStripping.cs`: `internal static class TraceContextStripping` with
`ShouldStripTraceparent(string value, string? currentId, bool runtimePropagates)`,
`ShouldStripTracestate(string value, string? currentTraceState, bool traceparentStripped)`,
`ResolvePropagation(bool? appContextSwitch, string? environmentValue)` and `internal static bool RuntimePropagates { get; }`
(a `static readonly` read once through `AppContext.TryGetSwitch` and `Environment.GetEnvironmentVariable`, per V6; no
reflection). If V6 found a no-output global propagator is detectable without reflection (P5c-11), `RuntimePropagates` also
accounts for it; otherwise it joins the residual.

**PublicAPI:** none (`internal`, `InternalsVisibleTo` already grants `Dexpace.Sdk.Http.SystemNet.Tests`). **IDs:** none
(design §8.1). **Verify:** `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-class
"*TraceContextStrippingTests"`.

### Task 3.2 — Strip in `ToHttpRequestMessage`; the wire tests (P5c-11, P5c-12; V7)

**Failing tests first.** New `tests/Dexpace.Sdk.Http.SystemNet.Tests/TracePropagationWireTests.cs`, class
`TracePropagationWireTests` (`[Trait("Category", "Integration")]`; a `LoopbackServer` that records the received request;
scoped recorders from `TestSupport`; proxy off as `RedirectWireTests` does; the SDK pipeline built with
`new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport)`):

- `The_runtime_child_span_is_the_wire_parent` (listeners on `Dexpace.Sdk` **and** `System.Net.Http`: the server's
  `traceparent` parent-id equals the id of the recorded `System.Net.Http` span, **not** the attempt span's; the trace-id is
  the call's; the runtime span's `ParentId` is the attempt span's id)
- `The_attempt_span_is_the_wire_parent_without_a_runtime_listener` (listener on `Dexpace.Sdk` only: the wire parent-id is the
  attempt span's id; the trace is unbroken)
- `The_tracestate_the_sdk_stamped_is_not_duplicated_on_the_wire` (**a pin**: a `tracestate` on the ambient root; exactly one
  `tracestate` header arrives, and it is the one belonging to the runtime child span's context when a runtime listener is
  present. The runtime's propagator does not overwrite a header already on the request (design §8.1, verified there; V7 to
  confirm), so it passes before the change; prove it can fail by temporarily making the adapter add a second `tracestate`,
  never committed)
- `A_caller_traceparent_is_not_stripped` (a request that carries its own `traceparent: 00-<other>-…`, sent with no SDK span
  current: it reaches the server unchanged)
- `A_borrowed_client_gets_the_same_treatment_as_an_owned_one` (`new SystemNetHttpClient(httpClient)` with a default
  `SocketsHttpHandler { AllowAutoRedirect = false }`: the first test's expectation holds)
- `A_borrowed_client_whose_handler_has_a_no_output_propagator_sends_no_traceparent_for_a_traced_call` (the documented residual,
  P5c-11 / R4: `SocketsHttpHandler { ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator() }`;
  the server received **no** `traceparent`)
- `An_untraced_call_sends_no_sdk_traceparent` (no `Dexpace.Sdk` listener: only what the runtime adds, nothing the SDK stamped)
- **Only if V6 found the switch is read live (R4):** `The_sdk_stamp_survives_when_propagation_is_off`.

The three names the design lists in its test table map to: `The_runtime_child_span_is_the_wire_parent`,
`The_sdk_stamp_survives_when_propagation_is_off` (pure-function form in task 3.1, R4), `A_caller_traceparent_is_not_stripped`.

Red: the first fails on the as-built adapter (the wire parent is the attempt span's id; the strip lets the runtime stamp its
child span); the `tracestate` test is a pin and passes before the change; the no-output-propagator test passes before the change (the SDK stamp still arrives) and fails after unless
the residual is as documented, so it is written to assert the **after** behaviour and is red first.

**Production.** `SystemNetHttpClient.ToHttpRequestMessage`:

- Before the header loop, `var current = Activity.Current; var strip = TraceContextStripping.RuntimePropagates && current is not null;`
  (nothing is allocated when there is no current activity).
- In the loop, before `IsWireSafe`: `if (strip && TraceContextStripping.ShouldStripTraceparent…) { skip }`, and the same for
  `tracestate` using the flag set by the traceparent decision (the loop order is not guaranteed, so the decision is computed
  once before the loop from `request.Headers`, not inside it). A `traceparent` that does **not** equal `current.Id` is
  copied as any other header (the caller's).
- The skip is silent (no log: nothing is dropped from the caller's intent, the runtime replaces it), and does not touch
  `s_framingHeaderDropped` / `s_unsafeHeaderDropped`.
- Remarks on the type gain the stripping rule, the two-meters note (a consumer enabling `Dexpace.Sdk` and `System.Net.Http`
  sees each attempt measured twice under one name; enable one or the other, design §8.1) and **Breaking 5**.

**PublicAPI:** none. **IDs:** none owned; evidence for `OBS-23`/design §8.1. **Verify:** `dotnet test --project
tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-class "*TracePropagationWireTests"`, then the whole
project, then the Security category (the four wire `Security` classes go through the changed method: they set no
`traceparent` and no ambient activity, so nothing changes; confirm).

### Task 3.3 — `Dexpace.Sdk.Http.SystemNet` README

`src/Dexpace.Sdk.Http.SystemNet/README.md` "Behaviour" list gains two bullets: **Trace context** (the adapter drops the SDK's
own `traceparent`/`tracestate` when it equals the current activity's and runtime propagation is on, so the wire carries the
runtime's child span id; for a caller-supplied client whose handler chain does not propagate — no
`SocketsHttpHandler`/`HttpClientHandler` at the root, or a no-output propagator — a traced call sends no `traceparent`;
remedies: leave propagation on, or do not listen to `Dexpace.Sdk`) and **Metrics** (enable `Dexpace.Sdk` **or**
`System.Net.Http`'s meter for HTTP client duration; both report each attempt under the same instrument name). No code.
**Verify:** the probe's `links` check; the package README is packed (the `Directory.Build.targets` rule), so
`dotnet pack` still succeeds.

### Task 3.4 — Close-out (PR 3)

- `CHANGELOG.md` `[Unreleased]`, `### Changed` (design Breaking 5): `SystemNetHttpClient` strips the SDK's own
  `traceparent`/`tracestate` when runtime propagation is on, so the wire carries the runtime's child span id; a caller-supplied
  client without runtime propagation sends no `traceparent` for a traced call.
- Run **V-gate**. **Commits:** `test: trace-context wire tests over the loopback server`, then
  `feat!: SystemNetHttpClient lets the runtime own traceparent (design 8.1)`.

---

## PR 4 — Close-out

**Gate: PRs 1–3 merged.** The docs close the sub-phase (roadmap step 7). Rows: all 12 (closing).

### Task 4.1 — User documentation

New `docs/sdk-documentation/tracing-and-metrics.md` (the directory holds `bodies.md`, `execution-context.md`, `http.md`,
`io.md`, `pipelines.md`, `recovery.md`, `seams.md`), opening "As built by phase 5c, written against source on <date>".
Content: enabling the source and meter (`AddSource("Dexpace.Sdk")`, `AddMeter("Dexpace.Sdk")`); the span tree (operation span,
attempt spans, kinds, names, when each ends: at response headers, not at body consumption; `Send<T>(handler)` ends the span
before the handler runs); the attribute tables of Positions C and F; the event table of Position E and the exhaustion
pairing; the status rules (success `Unset`, failure `Error` plus the `exception` event, a returned 4xx/5xx is a success
unless mapped; **the default pipeline does not map**, so on it a 4xx/5xx is a successful operation and status-based retry
exhaustion emits no `dexpace.retry.exhausted` event; only an added `ErrorMappingPolicy`, or a retryable exception, does); the metrics and their attribute sets; **an attempt span exists only under an operation span**; the
`InstrumentationContext.None` bundle for untraced calls and its effect on `CallKey.TraceId`; `traceparent` stamping in core
and stripping in the reference adapter, with the borrowed-client residual; the two-meters note; log correlation is the host's
`ActivityTrackingOptions` (`TraceId`/`SpanId`, §10 entry 23); what is **not** emitted (byte-count milestones, a Datadog
flavour, a count instrument); the migration table for the seven breaking changes. Cite requirement IDs and rulings; do not
copy design §8.1. `docs/README.md`'s ownership table gains the row the probe asks for. **Verify:** the probe's `links` check.

### Task 4.2 — The checklist

Write `docs/work/mvp/phase5/phase5c/<date>-phase5c-tracing-checklist.md` (task 4.6 files the design and this plan beside it)
from what was built: **12 rows**, phase-1 legend, mirroring the coverage table below: `OBS-21`, `OBS-22`, `OBS-26`, `OBS-29`,
`OBS-30`, `OBS-31`, `OBS-32`, `OBS-33` ✅; `OBS-23`, `OBS-25`, `OBS-28` ✅ with the clause carried by design §10 entry 23;
`OBS-27` ✅ for W3C and no-op, 🚫 for Datadog (§10 entry 24), the zero-draw clause per V8. Include cross-reference rows for
`CTX-14`, `CTX-15`, `OBS-20`, `OBS-34` (and `OBS-24` if R5 applied); the "existing assertions changed" table (expected:
`InstrumentationPolicyTests`' `server.port`, `resend_count`, duration-tag and span-count assertions, `DexpacePipelineTests`'
span counts; and **no** `Security` class); the unedited-Security list with the empty-diff evidence of convention 6; the
V1–V8 outcomes; the deviation ledger P5c-1…P5c-21 with each ruling's final state (accepted, reversed, still open) and R1–R7 as
built.

### Task 4.3 — Dated corrections, roadmap note

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/`:

- **Design Position C and D** (this phase's design, a dated correction proposal, finding 11): "With `ErrorMappingPolicy` in the
  chain (the default pipeline)" is wrong; `DexpacePipeline.CreateDefault` does not contain it, so on the default pipeline a
  4xx/5xx is a successful operation and status-based exhaustion never emits `dexpace.retry.exhausted`.
- **§8.1** (`08-…`; re-read the file name): the **As built** line; the operation span opens at call entry (P5c-2); success
  leaves the status `Unset` (P5c-5, correcting "`Ok` or `Error`"); the untraced bundle is `None` (P5c-3); the as-built defect
  list is closed (`server.port`, `resend_count`, `error.type`, `_OTHER`, the lazy URL, the leaked response).
- **§10 entry 23:** `OBS-29`'s evidence is the ordering test; `OBS-21`'s mutators are inert for the SDK's own writes only.
- **§10 entry 24:** name `OBS-27`'s zero-draw clause per V8 (P5c-14).
- **§11 item 38:** the attribute sets and the bucket advice (or its absence, V1).
- **§12:** the `OBS` row notes.
- The 4a and 4c designs' "5c replaces it with the operation span" lines gain a pointer to this plan's task 2.4 (as a dated
  correction line, not a rewrite).

Append to the roadmap: the Phase List row 5's `sdk-design refs` cell gains this design's link (appended, never replacing), and
a dated Phase Status Note that records the 5c dependency edges (4a/4b/4c dependencies met; 5a and 5b conveniences; the shared
`InstrumentationPolicy` file, P5c-17), the four PRs, the rulings the lead accepted or changed (P5c-1, -2, -3, -5, -6, -7, -11,
-14, -16, -17, -18 were open), the hand-offs: 6a (the three `RetryPolicy` calls and the exhaustion predicate, P5c-6/P5c-20),
6b (`OperationTelemetry.RedirectHop`, R6), 5b (the shared evidence of P5c-18), 8b (the sync terminal inherits the strip), and
the `SEAM-28` ⏳ (the operation id has no carrier). `CLAUDE.md`: the `Diagnostics/` layout line (the three new internal files)
and "What is genuinely unbuilt" (drop 5c, keep 5a/5b as the lead has them). `src/Dexpace.Sdk.Core/README.md`: the
Diagnostics row. If the implementation found anything the knowledge corpus should hold, record it as a note under
`docs/knowledge/notes/` (never edit `harvested/`). **Verify:** the probe's `links` and `citations` checks.

### Task 4.4 — Changelog and first-release register

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/tracing-and-metrics.md`; the AOT smoke covers the operation span, attempt
spans and the two metrics." Confirm every one of design Breaking 1–7 has its `### Changed` line from tasks 1.8, 2.11 and 3.4.
`docs/first-release.md`'s "Behavioural asymmetries a consumer must know" gains the two-meters note and the borrowed-client
`traceparent` residual (the one-line form, linking the user page).

### Task 4.5 — Gate

Run **V-gate** and the coverage gate with its self-test. **Commits:** `docs: phase 5c checklist, user page and dated corrections`.

### Task 4.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5c            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5c --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs).
`apply --write` performs `git mv` only; the plan authorises no commit beyond those named and **no push**.

---

## Coverage: one row per owned ID

Exactly 12 rows (`OBS-21`–`OBS-23`, `OBS-25`–`OBS-33`). **Planned exit** uses the roadmap's constraint-3 legend. "Pin" means the
test already passes on built behaviour and is proven able to fail (convention 1).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `OBS-21` | MUST | 1, 2 | 1.3, 1.6, 2.2, 2.6 | ✅ (§10 entry 23: the SDK's own writes are inert; `SetTag` itself is not) | `ActivityScopeTests.Sdk_writes_to_a_non_recording_span_are_skipped`, `OperationTelemetryTests.Every_mutator_skips_a_non_recording_span`, `AttemptTelemetryTests.A_non_recording_span_is_not_written_to` |
| `OBS-22` | MUST | 2 | 2.4, 2.6 | ✅ | `ActivityScopeTests.Activity_Current_is_restored_after_the_call_including_on_throw`, `The_attempt_span_is_Activity_Current_inside_the_transport` |
| `OBS-23` | MUST | 2 | 2.6 | ✅ (§10 entry 23: host `ActivityTrackingOptions`, keys `TraceId`/`SpanId`) | `ActivityScopeTests.The_attempt_span_is_current_at_the_log_site`, `An_untraced_call_leaves_the_callers_Activity_Current_untouched_and_log_sites_see_it` |
| `OBS-25` | MUST | 1, 2 | 1.5, 1.6, 2.4, 2.8 | ✅ (§10 entry 23: no shared no-op span object) | `UntracedAllocationTests.An_untraced_sync_send_through_InstrumentationPolicy_allocates_no_more_than_a_pass_through`, `Untraced_tracing_primitives_allocate_nothing`; `OperationSpanLifecycleTests.An_untraced_call_bundle_is_None_even_under_an_ambient_activity` |
| `OBS-26` | MUST | 2 | 2.4, 2.7 | ✅ | `OperationSpanLifecycleTests.A_traced_call_bundle_is_the_operation_span`, `A_traced_bundle_has_W3C_ids_and_is_valid`; `TraceIdTests.Generated_trace_ids_are_32_lowercase_hex_and_non_zero` |
| `OBS-27` | MUST | 2 | 2.7 | ✅ W3C and no-op; 🚫 Datadog (§10 entry 24); zero-draw clause per V8 (P5c-14) | `TraceIdTests.The_W3C_flavour_is_reported_and_the_no_op_flavour_is_the_zero_id`, `The_sdk_does_not_install_a_trace_id_generator`; the `Activity.TraceIdGenerator` ban in `BannedSymbols.txt` (build gate) |
| `OBS-28` | SHOULD | 2 | 2.3, 2.4, 2.5 | ✅ in part (§10 entry 23: byte-count milestones not emitted; transport milestones are the runtime's own spans) | `OperationEventTests.Attempt_failed_carries_resend_count_error_type_status_and_delay_in_seconds`, `RetryTraceEventsTests.A_retried_failure_emits_attempt_failed_with_the_next_delay`, `OperationSpanLifecycleTests.Attempt_spans_are_children_of_the_operation_span` |
| `OBS-29` | MUST | 2 | 2.2, 2.3, 2.4, 2.5 | ✅ | `OperationSpanLifecycleTests.A_retry_exhausted_call_ends_with_exhausted_then_exception_carrying_the_same_type`, `A_succeeding_call_ends_one_operation_span_without_error`, `A_returned_503_without_error_mapping_emits_no_exhausted_event`, `A_fatal_exception_ends_the_operation_span_in_error_without_an_exception_event` |
| `OBS-30` | MUST | 1, 2 | 1.6, 1.7, 2.6 | ✅ by contract (§11 item 37) | `ActivityScopeTests.A_throwing_ActivityStarted_propagates_out_of_SendAsync_and_Send`, `Concurrent_calls_keep_their_spans_in_their_own_traces` |
| `OBS-31` | MUST | 1, 2 | 1.5, 2.10 | ✅ | `HttpClientMetricsTests.The_instruments_manufacture_the_three_kinds_with_per_measurement_tags`; AOT smoke `CheckTracingAndMetricsAsync` |
| `OBS-32` | SHOULD | 1, 2 | 1.3, 1.5, 1.7, 2.10 | ✅ (§11 item 38) | `HttpClientMetricsTests.Duration_has_the_stable_attribute_set`, `Active_requests_has_the_start_attribute_set_and_the_unit_request`, `The_histogram_carries_the_bucket_advice` (V1) |
| `OBS-33` | MUST | 1 | 1.5 | ✅ | `HttpClientMetricsTests.The_histogram_tolerates_NaN_and_infinity`, `Active_requests_returns_to_zero_after_success_and_after_failure`, `Active_requests_does_not_decrement_what_it_did_not_increment` |

Count: `OBS-21`–`OBS-23` + `OBS-25`–`OBS-33` = **12 rows** (10 MUST: `OBS-21`, `OBS-22`, `OBS-23`, `OBS-25`, `OBS-26`, `OBS-27`,
`OBS-29`, `OBS-30`, `OBS-31`, `OBS-33`; 2 SHOULD: `OBS-28`, `OBS-32`). All 12 mapped, none ⏳, none retired.

### Work on other owners' rows (no checklist row in 5c)

These carry no exit mark in 5c's checklist. 5c's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `CTX-14`, `CTX-15` (4a) | The bundle is the operation span when traced, `None` when untraced | 2.4 | `OperationSpanLifecycleTests.A_traced_call_bundle_is_the_operation_span`, `An_untraced_call_bundle_is_None_even_under_an_ambient_activity` |
| `OBS-20` (5b) | "A throwing tracer/meter is NOT caught" | 1.5, 1.6, 2.6 | `ActivityScopeTests.A_throwing_ActivityStarted_propagates_out_of_SendAsync_and_Send`, `A_throwing_metric_callback_propagates_out_of_SendAsync` |
| `OBS-34` (5b) | Span lifecycle and metrics run at the default log level | 1.7 | `InstrumentationPolicyTests.Spans_and_metrics_record_at_the_default_log_level` |
| `OBS-24` (5b; **contingent**, P5c-1, R5) | The attempt span is current across a thread hop and restored on throw | 2.6 | `ActivityScopeTests.The_attempt_span_is_current_across_a_thread_hop_and_the_callers_is_restored_on_throw` |
| `OBS-1` (5b) | The untraced half of the joint zero-allocation assertion | 2.8 | `UntracedAllocationTests`; the joint assertion lands with whichever of 5b and 5c is second |
| `XCUT-19` (phase 10) | Span `url.full` and event `url.full` are the redacted form | 1.6, 2.3 | `AttemptTelemetryTests.A_traced_begin_starts_a_client_span_under_the_bundle_with_the_start_tags`, `OperationEventTests.RedirectHop_redacts_the_target` |
| `XCUT-20` (phase 10) | §11 item 37's reading: tag computation is total, callbacks are not wrapped | 1.6, 2.6 | as `OBS-20` |
| `RETRY-*` (6a) | The three event calls and the exhaustion record are the interim wiring | 2.5 | `RetryTraceEventsTests` |
| `REDIR-*` (6b) | `RedirectHop` ships unwired | 2.3 | `OperationEventTests.RedirectHop_carries_hop_status_the_redacted_target_and_cross_origin` |
| `SEAM-28` (2b; ⏳) | The operation span is named by the method until a carrier exists | 2.2 | `OperationTelemetryTests.Start_opens_an_internal_span_named_by_the_normalised_method` |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 1 | none | `OBS-31`, `OBS-32`, `OBS-33`; the attempt half of `OBS-21`, `OBS-25`, `OBS-30` | 1.1–1.8 (8) |
| 2 | PR 1 | `OBS-21`–`OBS-23`, `OBS-25`–`OBS-30` (finishing), `OBS-31`/`OBS-32` (AOT) | 2.1–2.11 (11) |
| 3 | PR 2 | none owned (design §8.1; evidence for `OBS-23`) | 3.1–3.4 (4) |
| 4 | PRs 1–3 | all 12 (closing) | 4.1–4.6 (6) |
| **Total** | | | **29 tasks, 4 PRs** |

Rows by the PR that first lands them: PR 1: `OBS-31`, `OBS-32`, `OBS-33` (3); PR 2: `OBS-22`, `OBS-23`, `OBS-26`, `OBS-27`, `OBS-28`,
`OBS-29` (6) plus the completion of `OBS-21`, `OBS-25`, `OBS-30`; the three whose first partial evidence is PR 1 finish in PR 2
(`OBS-21` task 2.6, `OBS-25` tasks 2.4 and 2.8, `OBS-30` task 2.6). The authoritative count is the 12-row table above.

PR 1 is independent of 5a and 5b and may land at any time; PR 2 needs PR 1 (it deletes R2's fallback); PR 3 needs PR 2 for the
two-span wire test; PR 4 follows all three. Every PR stays green in the order these gates allow.

---

## Findings while planning

Items checked against the repository at `4130f7b` on 2026-10-07, each with what the plan did.

1. **F1 — The design's `Stop(Activity?, Response?)` has no use for the `Response`.** The plan takes `settled` instead (R1).
2. **F2 — PR 1 as designed would drop attempt spans for consumers with no ambient activity.** The dispatch bundle is
   `FromActivity(Activity.Current)` until PR 2, and `None.StartActivity` returns `null`. R2's interim fallback keeps PR 1
   shippable; task 2.4 removes it.
3. **F3 — The "Instrumentation" xUnit collection has no `CollectionDefinition`.** xUnit v3 then treats it as a plain named
   collection: it serialises its members among themselves but does not exclude other collections. It is still required
   for every class that listens to `Dexpace.Sdk` (convention 5), because a recorder's listener affects sampling for the
   whole process and scoping only filters what is reported. The plan scopes the recorders (task 1.2), keeps all listening
   classes in `Instrumentation`, and adds the one real `DisableParallelization` collection only for the allocation test.
4. **F4 — `Activity.Current` set in a test constructor is not guaranteed to reach the test method.** `ActivityRecorder.Scoped`
   is therefore created in the test body (task 1.2), and the existing field-initialised `_listener` of
   `InstrumentationPolicyTests` is replaced.
5. **F5 — The runtime's propagation switch is almost certainly cached in a static**, so a wire test cannot flip it in-process
   (R4, V6). The "propagation off" clause is covered by pure-function tests, and a wire test pins the documented residual
   instead.
6. **F6 — `InstrumentationPolicy` already guards nothing about the redacted URL.** The as-built method computes it before any
   listener or log check. Task 1.7 moves both log calls behind `IsEnabled` and `AttemptTelemetry.Begin` computes `url.full`
   itself, so a call that is both traced and debug-logged redacts twice. 5b's rework of the log half may share the string;
   not 5c's to optimise (P5c-17).
7. **F7 — `RetryPolicy` computes `DelayFor` as an argument of `SleepAsync`.** The event needs the delay first; task 2.5 hoists
   it. The random draw order is unchanged, so `RetryPacingOverflowTests` (`Security`) is unaffected.
8. **F8 — `dexpace.attempt.failed`'s ordinal is 0 in a pipeline without `InstrumentationPolicy` (R3).** Documented on the user
   page; not worth a second counter.
9. **F9 — The planning host has no .NET SDK.** No API shape (R7, V1–V8) is verified; task 1.1 is the gate, and each fact has a
   stated fallback so a negative result changes a task, not the design.
10. **F10 — Names from 5a and 5b are unverified (convention 11).** Task 2.1 re-derives them and stops rather than works around
    a contract the merged code does not meet.
11. **F11 — The default pipeline does not contain `ErrorMappingPolicy`.** The design's Position C/D premise is wrong:
    `CreateDefault` adds idempotency, client-identity, set-date and the standard resilience set, and nothing in `src/`
    constructs `ErrorMappingPolicy`. Tests needing mapping add it explicitly (convention 5a); the user page says so; task 4.3
    carries a dated-correction proposal for the design. Consequence: on the default pipeline a retryable status after the
    budget is returned, the operation succeeds, and no `dexpace.retry.exhausted` is emitted.
12. **F12 — No redirect hop can follow an exhausted retry sequence today.** Redirect (stage 200) wraps retry (300), exhaustion
    needs a retryable status or a rethrown request/response exception, and redirect follows only 3xx. The stale-record test
    uses a test-only `Redirect`-stage policy that re-drives the continuation; the leak becomes reachable with 6a/6b.

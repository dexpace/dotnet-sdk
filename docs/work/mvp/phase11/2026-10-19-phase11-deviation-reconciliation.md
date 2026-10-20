# Phase 11 — Deviation Reconciliation: Implementation Plan

**Status:** Draft, for review. Written 2026-10-19 against `main` at `eb9b758` (phases 0 to 10 merged). Design:
[phase 11 design](2026-10-19-phase11-deviation-reconciliation-design.md) (filed by task 6.8 to
`docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-design.md`), the authority for every decision below. The plan cites
its facts (`V1`..`V18`), decisions (`P11-1`..`P11-27`), §10 entries, §11 items and negative scans (`N1`..`N20`) rather than restating them.
Format precedent: the phase 10 plan (`docs/work/mvp/phase10/2026-10-13-phase10-cross-cutting-invariants.md`).

**What this document is.** Roadmap step 3 for phase 11 (one phase, no sub-phases, P11-1): numbered TDD tasks in six stacked pull requests,
each with the failing test (name, project, `[Trait]`), the production change, the `PublicAPI.Unshipped.txt` diff where there is one, the
requirement IDs and the command that proves it green. It is not the checklist (task 6.3 writes it from what was built). The
[traceability table](#traceability) maps every §10 entry, every §11 item and every inbound bullet to a task.

**Scope.** 26 repairs (the design's *Repairs at a glance*), the ledger (`docs/deviations.md`, `LedgerAuditTests`), the §12 generation, the
close-out corrections. Planned outcome: unmet-clause count **3** (§10 entries 16, 18, 32); §10 entries 24, 28, 31, 33 retired; 15 ⏳ MUST rows
resolved, `NFR-17` ⏳.

**Order and gates.** Six stacked pull requests on branches `<issue>-phase-11-<slug>` (the first off `main`, each later one off its predecessor),
each green on its own: **PR 1** gates and small safety repairs (1.1–1.7); **PR 2** error taxonomy and bridges (2.1–2.6); **PR 3** bodies,
headers, URLs and pages (3.1–3.7); **PR 4** the transport's native retry (4.1–4.3); **PR 5** observability, configuration, retry, SSE, auth
(5.1–5.9); **PR 6** the ledger and close-out (6.1–6.9). Task 0.1 is a pre-flight with no commit.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the named test; run it and **see it fail for the stated reason** (a compile error is the expected red only
   for a new public type); make the minimal change; see it pass; run the group's gate. A test that passes before the change is a **pin**
   and says so; a pin is proven able to fail by temporarily breaking the line it pins (never committed; `git checkout -- <file>`). **No commit
   is red**: a task that changes a signature edits every caller and every test that asserted the old behaviour in the same commit, and
   the plan names those tests.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), uses its project's `GlobalUsings.cs` (`ImplicitUsings` off), one
   type per file, file-scoped namespace.
3. **`src/` code:** `ConfigureAwait(false)` on every `await` (`CA2007`); methods ≤ 70 lines (`MA0051`); `///` on every public member
   (CS1591); every broad `catch` filters `when (!ExceptionFacts.IsFatal(ex))` or ends in `throw;` (task 1.4 makes this a gate); no new
   `#pragma` without a why-comment (`AnalyzerWaiverTests`); no `Task.Delay`, wall clock or environment read (`RS0030`); no new
   `PackageReference` (the dependency audit); `IsTrimmable`/`IsAotCompatible` stay clean.
4. **Tests.** `[Trait("Category", …)]` on every class; tokens from `TestContext.Current.CancellationToken`; xUnit `Assert` only; no wall-clock
   sleep. Tests that install a throwing or non-recording `ActivityListener` join the `NoDiagnosticListeners` collection (5c convention).
5. **`Security` tests are never deleted or loosened.** Phase 11 adds three (`HeaderInjectionValidationTests.The_inbound_path_does_not_trim_a_name`,
   `HeaderInjectionValidationTests.A_uri_created_without_canonicalization_is_rejected`, `ServerSentEventLineCapTests.Data_lines_that_each_fit_but_never_end_the_event_fail_once_the_event_cap_is_passed`)
   and edits none. Gate: `git diff --stat main...HEAD -- '**/Security/*.cs'` shows additions only (reviewed by hand for removed lines).
6. **`PublicAPI.Unshipped.txt`.** Every surface change is an `Unshipped` line; the `RS0016`/`RS0017` output is the authority (apply the
   analyzer's code fix, compare to the task's diff). `PublicAPI.Shipped.txt` stays empty (`NFR4ArchitectureTests`).
7. **Breaking changes** get a `CHANGELOG.md` `[Unreleased]` line **in the same commit** (constraint 8), under a "Phase 11" heading.
8. **Commits** follow the repository style (`fix:`, `feat:`, `test:`, `docs:`, `ci:`, `chore:`). No AI attribution. **The plan authorises no
   `git push`, no `gh` write and no remote action**; the one remote read (task 1.1, step 1) happens only if the lead runs or explicitly
   authorises it. The implementer stops before any push and hands the lead the branch names.
9. **Shared files** (`CHANGELOG.md`, `CLAUDE.md`, the roadmap, `docs/first-release.md`, `.github/workflows/ci.yml`, `BannedSymbols.txt`,
   `.editorconfig`, `tests/Dexpace.Sdk.AotSmoke/*`): re-derive each hunk from the merged file; never resolve a conflict by keeping a side whole.
10. **Conformance waivers.** A waiver whose assertion now passes fails the run (8a's rule); a task that fixes a waived clause deletes the
    waiver in the same commit.

### Environment and command shorthands

`dotnet` 10.0.401 (`global.json`), `net10.0`, from the repository root. On the authoring machine the SDK is not on `PATH`:
`export DOTNET_ROOT=/home/mohammad/.dotnet` then, on its own line, `export PATH="$DOTNET_ROOT:$PATH"`. Microsoft.Testing.Platform: build first,
then `dotnet test … --no-build`; never pass MSBuild flags to `dotnet test`; a run reporting zero tests is a failed command.

- **V-build** = `dotnet build Dexpace.Sdk.sln --configuration Release -m:3 -nr:false --disable-build-servers`
- **V-core `<Class>`** = `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --no-build --filter-class "*<Class>"`; **V-sn**, **V-di**, **V-stj**, **V-conf-tests** the same over `tests/Dexpace.Sdk.Http.SystemNet.Tests`, `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests`, `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests`, `tests/Dexpace.Sdk.Conformance.Tests`
- **V-conf** = `--filter-trait "Category=Conformance"` over Core, SystemNet, DI, STJ and Conformance.Tests (each has a Conformance class since phase 10)
- **V-sec** = `--filter-trait "Category=Security"` over Core, SystemNet, DI
- **V-fmt** = `dotnet format Dexpace.Sdk.sln --verify-no-changes`
- **V-gate** = `dotnet restore Dexpace.Sdk.sln --locked-mode`; V-build; V-fmt; `dotnet test --solution Dexpace.Sdk.sln --configuration Release --no-build --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`; `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80`; `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`; `dotnet pack Dexpace.Sdk.sln -c Release --no-build --output artifacts/packages`; `dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages`; `dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages`; `scripts/ci/reproducible-pack.sh artifacts/reproducible` (from a clean tree); `dotnet run scripts/ci/requirement-coverage.cs -- --check` (and, from 6.2, `--check-section12`); the NativeAOT publish and run; the tools solution; `scripts/knowledge verify-structure`; the Security diff check (convention 5)

---

## Task 0.1 — Pre-flight (no commit)

1. **Knowledge queries** (start of the phase and of every numbered task): `scripts/knowledge --origin note --brief`;
   `scripts/knowledge --section conflicts --brief`; `scripts/knowledge --req <ID>` for each ID a task cites.
2. **Baseline** (V1): V-build (0 warnings); the full test run (7356 / 27 skipped / 0 failed at `eb9b758`); V-conf with exactly each driver's
   pinned waivers and `NotExercised` lists; `requirement-coverage.cs --report` tallies (V3).
3. **The Core flake (8b inbound bullet).** Run `V-core` over the whole project ten times
   (`for i in $(seq 10); do dotnet test --project tests/Dexpace.Sdk.Core.Tests -c Release --no-build || echo "run $i failed"; done`), and
   record the result. If `OperationSpanLifecycleTests.An_attempt_span_exists_only_under_an_operation_span` (or any test) fails once, task 1.2
   applies; if not, task 1.2 records "not reproduced in 10 runs at `eb9b758`" in the checklist and does nothing else.
4. **Re-verify the design's facts** as throwaway probes outside the tree (scratch directory): V4 (rejecting scheduler), V6 (the filter, five
   methods), V8 (`TarWriter` classification on this host), V9, V11, V13, V14, V15, V17. A fact that differs is a **stop**: record it and
   amend the affected task before writing code.
5. Create the first branch off `main` (no push).

---

# PR 1 — Gates and small safety repairs

## Task 1.1 — The `reproducible packages` job (`NFR-12`; P11-22)

**Files.** `scripts/ci/reproducible-pack.sh`, `.github/workflows/ci.yml` (the `reproducible` job only), possibly `global.json` or a build
property (outcome-dependent).

1. **Evidence first.** Ask the lead to run, or to authorise in writing for this one call, `gh run view <failing-run-id> --log-failed` for PR #102's
   `reproducible packages` job, and paste the `reproducible-pack:` lines (the f1db46f diagnostics name the entries and the zip metadata that
   differ). Without that authorisation, skip to step 3 on hypothesis (a).
2. **Classify** against the design's hypotheses: (a) the runner's SDK patch differs from `global.json`; (b) a part (`.snupkg`, `.psmdcp`) appears
   only on the runner; (c) a container field ignores `SOURCE_DATE_EPOCH`; (d) something else the log names.
3. **The check first.** Add to the script, before the packs, a **toolchain line**: print `dotnet --version` from inside each worktree and exit 1,
   naming both versions, when it differs from `global.json`'s `sdk.version`. Locally it is green (V2), so the red evidence for this task is the
   failing CI log (step 1), not a local run; the commit carries the check together with the cause's fix, so no commit is red:
   - (a): the job writes a job-local `global.json` with `"rollForward": "disable"` into each worktree before restore, so both packs and the check use
     exactly 10.0.401 (`setup-dotnet` already installs it from `global.json`);
   - (b)/(c): normalise or exclude the part the log names, with the build property it points at (`Directory.Build.props`);
   - (d): the fix the log dictates, recorded in the checklist.
4. **Self-test.** Extend `reproducible-pack.sh` with `--self-test`: pack once, copy the first set, flip one byte in one `.nupkg` of the copy, and
   require the compare loop to exit 1 naming that package (the gate fails closed, as `coverage-gate-selftest.sh` does for coverage). Add the
   self-test as a step of the `reproducible` job **and** a row of `CiWorkflowTests.Gates` (same commit).

**Verify.** `scripts/ci/reproducible-pack.sh artifacts/reproducible` and `--self-test` locally; the lead's CI run green. **IDs.** `NFR-12`.
**Commit.** `ci: fix the reproducible-pack job and add its self-test (NFR-12)`.

## Task 1.2 — The Core flake (8b inbound; conditional on task 0.1)

**Only if 0.1 reproduced a failure.** Failing evidence: the failing run's log. Fix: the class that leaks activities into others joins the
`NoDiagnosticListeners` collection, or the leaking test scopes its listener with `ActivityRecorder.Scoped` (5c convention). **Verify.** Ten
consecutive green runs of `tests/Dexpace.Sdk.Core.Tests`. **Commit.** `test: isolate <class> from process-wide activity listeners`.
**Otherwise** no commit; the checklist records the ten green runs.

## Task 1.3 — Core's dependency allowance is a fixed set (`SEAM-1`, `NFR-1`; §10 entry 1)

**Test first.** `scripts/ci/dependency-audit-selftest.sh` (new): builds a scratch copy of `src/Dexpace.Sdk.Core/obj/project.assets.json`-shaped
input — simplest: run the audit against a copy of `artifacts/packages` and a copy of `src/Dexpace.Sdk.Core/bin/Release/net10.0/Dexpace.Sdk.Core.deps.json`
with one extra library entry `Some.Facade.Dependency/1.0.0` injected under the facade's `dependencies` — and requires exit 1 naming
`Some.Facade.Dependency`. Today it exits 0 (the allowance is `Closure(Facade)`, `dependency-audit.cs:119`): the red.
**Change.** `dependency-audit.cs`: core's policy record allows exactly `{Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions}`
(band-matched), not the facade's closure; the self-test becomes a step after **Dependency audit** in `ci.yml` and a row in `CiWorkflowTests.Gates`.
`PackagingPolicyParityTests` stays green (update the packaging suite's core policy if it mirrors the closure rule).
**Verify.** The self-test (red, then green); the audit over a real pack (green). **Commit.** `ci: core's dependency allowance is a fixed set, with a self-test`.

## Task 1.4 — The fatal filter: two repairs and a gate (`RECOV-2`, `RETRY-25`; §10 entry 12; N20)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Architecture/FatalFilterArchitectureTests.cs` (Unit), a source scan in the style of
`BannedSymbolsSiteTests`: over `src/Dexpace.Sdk.{Core,Http.SystemNet,Serialization.SystemTextJson,Extensions.DependencyInjection}/**/*.cs`,
(1) every unfiltered catch-all (`catch`, `catch (Exception)`, `catch (Exception ex)` with no `when`) must have a handler whose last statement is
`throw;`; (2) every filtered catch-all's `when` must call `ExceptionFacts.IsFatal`, `EmissionGuard.Reportable` or `IsClosedInFlight`, or be a typed
`is` test listing non-fatal types. Plus a self-test theory over three inline snippets (a `when (true)` catch, a swallowing bare catch, a compliant
catch) proving the scanner flags the first two. Red today: `Diagnostics/UrlRedactor.cs:99` and `:166`.
**Change.** `UrlRedactor.cs:99`, `:166`: `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` (the existing `CA1031` pragma and comment stay).
**Verify.** V-core `FatalFilterArchitectureTests`, V-core `UrlRedactor*`. **IDs.** `RECOV-2`, `RETRY-25`, `XCUT-20`. **Commit.** `fix: UrlRedactor no longer swallows OutOfMemoryException; gate every broad catch`.

## Task 1.5 — `Disposal` writes only to a recording activity (`OBS-21`; §10 entry 23; V14)

**Test first.** `DisposalTests.A_suppressed_dispose_failure_is_not_recorded_on_a_non_recording_activity` (Core.Tests, Unit, `NoDiagnosticListeners`
collection): start an `Activity` with an `ActivityListener` whose sampler returns `PropagationData` (so `IsAllDataRequested` is `false`), make
`Disposal.DisposeQuietly` swallow a throwing `Dispose`, assert the activity has no events. Red: `Disposal.cs:105-110` adds one.
**Change.** guard with `Activity.Current is { IsAllDataRequested: true } activity`. **Commit.** `fix: Disposal records a dispose failure only on a recording activity (OBS-21)`.

## Task 1.6 — `CA1062` on for `src/`; the `HttpResponseException` guards (`XCUT-8`, `NFR-7`; §10 entry 28; P11-9, P11-20; V12, V17)

**Tests first** (Core.Tests, `Errors/HttpResponseExceptionTests.cs`, Unit):
- `The_constructor_rejects_a_null_response` — `Assert.Throws<ArgumentNullException>`, `ParamName == "response"`. Red: `NullReferenceException`.
- `The_constructor_rejects_a_non_error_status` — `[Theory]` 100, 200, 204, 302, 304 → `ArgumentException`, `ParamName == "response"`. Red: constructs.
**Change.** `Errors/TransportExceptions.cs:107-113`: a private static `Validated(Response response)` called inside the `base(...)` argument
(`ThrowIfNull`, then `!response.Status.IsError` → `ArgumentException`). `.editorconfig`: a `[src/**/*.cs]` section with
`dotnet_diagnostic.CA1062.severity = error`, and the old `none` block's comment rewritten to "tests and tools only" (`AnalyzerWaiverTests` keeps its
rationale rule). Callers relying on the unguarded constructor, edited in the same commit: `tests/Dexpace.Sdk.Conformance.Tests/Controls/InvariantTaxonomyControlTests.cs:26`
(the control returns `new SdkException("mapped a 200")` instead — still a "successful exception", still red for `xcut-8`), `tests/…/BrokenPipelines.cs:221`.
**Verify.** V-build (the analyzer now reports nothing); V-core `HttpResponseExceptionTests`; V-conf-tests `InvariantTaxonomyControlTests`.
**Breaking.** The constructor's new exceptions (CHANGELOG). **Commit.** `fix: HttpResponseException rejects null and a non-error status; CA1062 on for src (XCUT-8)`.

## Task 1.7 — PR 1 gate

V-gate. The `CiWorkflowTests` rows added by 1.1 and 1.3 are green. Hand the lead the branch name.

---

# PR 2 — Error taxonomy and bridges

## Task 2.1 — `ServiceRequestException : IOException` (`TRANSPORT-20`, `XCUT-4`; §10 entry 7; P11-7)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Errors/TransportExceptionTaxonomyTests.cs` (Unit):
- `ServiceRequestException_is_an_IOException` — `Assert.IsAssignableFrom<IOException>(new ServiceRequestException("x"))`, and for `ServiceRequestTimeoutException`.
- `ServiceRequestException_reports_itself_retryable` — `((IRetryableError)e).IsRetryable` is `true`.
- `A_suppressed_secondary_is_readable_through_the_trail` — `ExceptionTrail.AddSuppressed(e, other)` then `ExceptionTrail.GetSuppressed(e)` contains it, and `e.ToString()` renders it (the type overrides `ToString` the way `SdkException` does).
Red: compile succeeds, first assertion fails.
**Change.** `Errors/TransportExceptions.cs`: `public class ServiceRequestException : IOException, IRetryableError` with the same constructors;
`IsRetryable => true` (sealed); `ToString()` renders `ExceptionTrail.GetSuppressed(this)` (one shared internal renderer extracted from `SdkException.ToString`).
Callers that type-test `SdkException` for a transport failure, found by `grep -rn "SdkException" src | grep -v "class "` and reviewed one by one
(the retry classifier reads `IRetryableError` and the cause chain, `RetryFacts.cs:101-113`, so it needs no change; `ExceptionTrail` already
handles a foreign exception). Kit assertions that assert the old base, edited in the same commit: `src/Dexpace.Sdk.Conformance/Assertions/FailureAssertions.cs:44-45`,
`TaxonomyInvariants.cs:67`, `RedirectAndTimeoutAssertions.cs:59`, `CancellationAssertions.cs:110`, `ResendAssertions.cs:68` (each now requires
`IOException`, which is `TRANSPORT-20`'s letter; their negative controls are re-checked to stay red). `PublicAPI.Unshipped.txt`: the base-type
change shows as removed/added lines for the type and its members (apply the code fix).
**Verify.** V-core `TransportExceptionTaxonomyTests`, `Retry*`; V-conf; V-sec. **Breaking.** "`ServiceRequestException` derives from `IOException`;
`catch (SdkException)` no longer catches a transport failure — catch `ServiceRequestException` or `IOException`." **IDs.** `TRANSPORT-20`, `XCUT-4`.
**Commit.** `feat!: ServiceRequestException is an IOException (TRANSPORT-20, XCUT-4)`.

## Task 2.2 — `ServiceResponseException` removed; the send-failure message (`XCUT-4`; inbound 10)

**Tests first.**
- `TransportExceptionTaxonomyTests.Core_exports_one_transport_failure_family` (Unit): by reflection over `Dexpace.Sdk.Core`'s exported types,
  the types implementing `IRetryableError` that carry no `Response` and are not `HttpResponseException` are exactly
  {`ServiceRequestException`, `ServiceRequestTimeoutException`}. Red: lists `ServiceResponseException`.
- `SendFailuresTests.A_response_that_ended_after_the_request_arrived_is_not_called_unsent` (SystemNet.Tests, Unit): an `HttpRequestException` with
  `HttpRequestError.ResponseEnded` maps to `ServiceRequestException` whose message does not contain "could not be sent". Red: it does.
**Change.** Delete `ServiceResponseException` (`TransportExceptions.cs:66-94`, five `PublicAPI.Unshipped.txt` lines). `SendFailures.cs:75`: the
message chosen by `HttpRequestError` ("no response was received" for `ResponseEnded`/`InvalidResponse`; "could not be sent" for `NameResolutionError`,
`ConnectionError`, `SecureConnectionError`, `ProxyTunnelError`). Fix the remarks at `SdkException.cs:12-16`, the cref at `RetryPolicy.cs:95`, the
summary at `TransportExceptions.cs:10-13` ("no response was received"). Tests constructing it are moved to `ServiceRequestException`:
`RetryClassifierTests.cs:66-71,152,220`, `RetryPolicyTests.cs:259-261`, `RetryPolicyEngineTests.cs:428`, `OperationEventTests.cs:110,127,140`,
`ResponseShapeControlTests.cs:38`; the raw-socket client (`tests/Dexpace.Sdk.Conformance.Tests/RawSocket/RawSocketHttpClient.cs:75`) throws
`ServiceRequestException`, and `RawSocketHttpClientTests.cs:344` follows. Scan N3 becomes live in 6.2.
**Breaking.** "`ServiceResponseException` is removed." **Commit.** `feat!: remove the never-thrown ServiceResponseException; name an ended response truthfully`.

## Task 2.3 — `AsAsync` faults instead of throwing (`ASYNC-2`; F-A2; V4)

**Tests first** (Core.Tests `Client/SyncToAsyncBridgeTests.cs`, Unit; new `tests/Dexpace.Sdk.TestSupport/Threading/RejectingTaskScheduler.cs`, whose
`QueueTask` throws `InvalidOperationException`):
- `A_scheduler_that_rejects_the_work_faults_the_returned_task_instead_of_throwing` — `Record.Exception(() => task = bridge.ExecuteAsync(...))` is
  null; `await Assert.ThrowsAsync<TaskSchedulerException>(() => task)`. Red: synchronous throw.
- `A_pre_cancelled_call_completes_cancelled_without_touching_the_scheduler` — the scheduler records no `QueueTask`; the task is `Canceled`. Red.
**Change.** `Client/HttpClientExtensions.cs:149`: return `Task.FromCanceled<Response>(token)` when signalled; wrap `Task.Factory.StartNew(...)` in
`try … catch (TaskSchedulerException ex) { return Task.FromException<Response>(ex); }`. **IDs.** `ASYNC-2`. **Commit.** `fix: AsAsync delivers scheduler rejection through the task (ASYNC-2)`.

## Task 2.4 — `WaitOrDisposeAsync`: the cancel-without-interruption mode (`SEAM-30`, `ASYNC-3`; §11 item 43; V5)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Client/WaitOrDisposeTests.cs` (Unit): 20 000 iterations of the V5 race (a fake blocking transport that
returns a tracking `Response` after a jittered spin; the token cancelled from another thread around the same instant) through
`task.WaitOrDisposeAsync(token)`; assert zero responses left undisposed and every caller either got the response or `OperationCanceledException`,
never both. Red: compile error (new public method). No test asserts the raw `WaitAsync` leak (a race that "may" leak is a flaky assertion);
V5 is the evidence, and the helper's guarantee is the test.
**Change.** New public `Dexpace.Sdk.Core.Client.ResponseTaskExtensions.WaitOrDisposeAsync(this Task<Response> task, CancellationToken cancellationToken)`
delegating to `Internal/LateResult.WaitOrDisposeAsync` (whose remark loses "the one sanctioned site" — `TypedResponse.cs:92` is the second;
the remark names both). `HttpClientExtensions.AsAsync`'s XML docs name the helper as the way to cancel without interruption.
`PublicAPI.Unshipped.txt`: `+static Dexpace.Sdk.Core.Client.ResponseTaskExtensions.WaitOrDisposeAsync(this System.Threading.Tasks.Task<Dexpace.Sdk.Core.Http.Response.Response!>! task, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.Task<Dexpace.Sdk.Core.Http.Response.Response!>!` (and the class line).
**IDs.** `SEAM-30`, `ASYNC-3`. **Commit.** `feat: WaitOrDisposeAsync closes an orphaned response when the caller stops waiting (SEAM-30)`.

## Task 2.5 — Validating `init` accessors name their field (`SEAM-29`; §10 entry 10)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Configuration/InitAccessorParamNameTests.cs` (Unit): a `[Theory]` over an explicit table of every
null-rejecting `init` accessor (found by `grep -rn "ThrowIfNull(value)" src`; one row per accessor, each row a lambda that assigns `null!` in an object
initializer or a `with`), asserting `ArgumentNullException` whose `ParamName` equals the property name. A companion `[Fact]` re-runs the grep over
`src/` and fails when an accessor exists that the table lacks, so a new one cannot slip in. Red: `ParamName == "value"`.
**Change.** `ArgumentNullException.ThrowIfNull(value, nameof(<Property>))` at each site (`OperationDescriptor` ×6, `DexpaceClientOptions` ×4,
`RequestOptions` ×2, `ProxyOptions` ×2, `AuthRequirement`, `RedirectOptions`, `RetryOptions`, and any the grep adds). **Commit.** `fix: validating init accessors name their field (SEAM-29)`.

## Task 2.6 — PR 2 gate

V-gate. The NativeAOT smoke still passes (`ServiceRequestException`'s base change is exercised by `CheckPhase8*`).

---

# PR 3 — Bodies, headers, URLs and pages

## Task 3.1 — A second open returns the same stream (`BODY-14`, `HTTP-41`; §10 entry 6; P11-8)

**Tests first.**
- `ResponseBodyLifecycleTests.A_second_open_returns_the_same_stream` (Core.Tests, Unit) — `Theory` over the stream and bytes response variants and both
  forms (`OpenRead` then `OpenReadAsync`, and the reverse): `Assert.Same`.
- `ResponseBodyLifecycleTests.An_open_after_dispose_still_throws_StreamClosedException` — pin.
- `HttpResponseMessageBodyTests.A_second_open_returns_the_same_stream` (SystemNet.Tests, Unit).
Red: `StreamConsumedException`.
**Change.** `Http/Response/ResponseBody.cs` (`StreamResponseBody`, `BytesResponseBody` caching the first `Stream`), `LoggingResponseBody` (its tail),
`Http.SystemNet/HttpResponseMessageBody.cs:42-75` (cache the stream it obtained; the latch still flips before `HttpContent` is touched).
The readers (`ReadAsBytes*`, `ReadAsString*`, `ReadValue*`) after an `OpenRead` now read the remaining bytes of the same handle instead of
throwing; the tests that asserted the old throw, edited in the same commit: in `ResponseBodyTests.cs`, `ResponseBodyLifecycleTests.cs`,
`LoggingResponseBodyTests.cs`, `ReadValueTests.cs`, `HttpResponseExceptionGetErrorTests.cs`, `BodyConvenienceTests.cs`, `ReadValueEndToEndTests.cs`
(each assertion re-stated against `BODY-14`'s letter, named in the commit message). `StreamConsumedException` remains for request bodies and the
error body's replay is unchanged. Kit: if a transport assertion asserts a second-open throw, it changes to `Assert.Same` and its control inverts.
**Breaking.** CHANGELOG. **Commit.** `feat!: a response body returns the same stream on a second open (BODY-14, HTTP-41)`.

## Task 3.2 — Inbound header names are not trimmed (`HTTP-19`, `XCUT-18`, `TRANSPORT-14`; §11 item 36; P11-14; V15)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Security/HeaderInjectionValidationTests.cs` gains
`The_inbound_path_does_not_trim_a_name` (`[Theory]` `"\tX-A"`, `"X-A\t"`, `" X-A"`, `"X-A "`; `[Trait("Category","Security")]`):
`Assert.Throws<ArgumentException>(() => new Headers.Builder().AddInbound(name, "v"))`. Red: stored as `X-A`. The outbound trim tests
(`:94-104`) stay green unedited.
**Change.** `Http/Common/HeaderSyntax.cs`: `ValidateInboundName` (no trim; `HttpHeaderSyntax.IsValidName`), used by `Headers.Builder.AddInbound`.
Kit: `src/Dexpace.Sdk.Conformance/Assertions/SecurityInvariants.cs:192` tightens from "refusal or trim" to refusal **inbound** (outbound keeps
both readings, `HTTP-17`), with a new negative control under `tests/Dexpace.Sdk.Conformance.Tests/Controls/` (a model that trims an inbound name)
registered in its `Covered` list. **Commit.** `fix: the inbound header path refuses a name with surrounding whitespace (HTTP-19, XCUT-18)`.

## Task 3.3 — `FromFile` rejects a Unix special file (`BODY-11`, `HTTP-40`; §10 entry 31; P11-13; V8)

**Tests first** (Core.Tests `Http/Request/FileRequestBodyTests.cs`, Unit, skipped on Windows with `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`):
- `A_character_device_is_rejected_at_construction` — `/dev/null` → `ArgumentException` naming the path's kind, not the path's contents.
- `A_fifo_is_rejected_at_construction_without_blocking` — the BCL cannot create a FIFO, so the test runs `mkfifo` (present on the Linux and macOS
  rows) through `Process.Start`, then constructs on a worker and asserts it completes within a 5 s guard and throws `ArgumentException`.
- `An_empty_regular_file_is_accepted` — pin.
Red: no throw.
**Change.** `Http/Request/RequestBody.cs:283-308` (`FromFile`): on non-Windows, when the captured length is 0, classify with an internal
`UnixFileKind.Classify(string path)` that writes a `TarWriter` entry for the path into a `Stream.Null`-backed 512-byte capture, completes the
write, reads the entry's `TarEntryType`; `RegularFile` (and `V7RegularFile`) passes; anything else, or the `IOException` a socket raises, throws
`ArgumentException`. `System.Formats.Tar` is in the shared framework (the dependency audit stays green, verify). NativeAOT smoke: add the
`/dev/null` rejection to `CheckPhase11Async` (new, `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.Phase11.cs`, Linux only).
**Breaking.** CHANGELOG. **Commit.** `fix: FromFile rejects a Unix FIFO or device at construction (BODY-11, HTTP-40)`.

## Task 3.4 — `Request` rejects a non-canonicalizing `Uri` (V9)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Security/HeaderInjectionValidationTests.cs` gains `A_uri_created_without_canonicalization_is_rejected`
(Security): `new Uri("https://h/p?a=%0D%0AX: 1", new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true })` passed to
`Request.Create`/the constructor/`WithUrl` → `ArgumentException`. Red: accepted.
**Change.** `Http/Request/Request.cs:47-67,202-205`: one internal `RequireCanonical(Uri)` check that calls
`uri.GetComponents(UriComponents.Path | UriComponents.Query, UriFormat.UriEscaped)` and maps the `InvalidOperationException` such a `Uri` raises (V9)
to `ArgumentException` (message redacted, as entry 29). Task 0.1 re-verifies that a normally created `Uri` never raises it. **Commit.** `fix: Request refuses a Uri built without canonicalization`.

## Task 3.5 — `Page` copies its values (`PAGE-2`; §10 entry 17)

**Test first.** `PageTests.Values_is_a_snapshot_of_the_list_passed_in` (Unit): build a `Page<int>` from a `List<int>`, mutate the list, assert
`Values` unchanged. Red.
**Change.** `Pagination/Page.cs:44` copies (`[.. values]` into an `ImmutableArray`/read-only list); drop the now-duplicate copy at `PageStep.cs:109`.
**Commit.** `fix: a Page snapshots its values`.

## Task 3.6 — The splice keeps empty segments (`PAGE-21`; §10 entry 18)

**Test first.** `QuerySpliceTests.Stray_empty_segments_are_copied_verbatim` replaces `:182-184`'s pin: `?a=1&&b=2&` with the cursor set yields
`?a=1&&b=2&&cursor=…` (an empty segment is an untargeted parameter and is copied). Red.
**Change.** `Pagination/QuerySplice.cs:111-112`: split without `RemoveEmptyEntries`; an empty segment is never targeted. **Commit.** `fix: the query splice copies empty segments verbatim (PAGE-21)`.

## Task 3.7 — PR 3 gate

V-gate; V-sec (two new Security tests green); the AOT smoke runs `CheckPhase11Async`.

---

# PR 4 — The transport's native retry

## Task 4.1 — The native-retry stop filter (`TRANSPORT-2`; §10 entry 33; P11-12; V6)

**Test first.** `tests/Dexpace.Sdk.Http.SystemNet.Tests/BodylessRetryWireTests.cs`: the residual theory (`:44-86`) becomes
`A_bodyless_request_reaches_the_server_once_on_an_sdk_built_handler` over `GET`, `HEAD`, `OPTIONS`, `DELETE`, `TRACE` × {async, sync} ×
{`new SystemNetHttpClient()`, `SystemNetHttpClient.CreatePrimaryHandler()`-based}: a keep-alive connection warmed by one exchange, then the server
reads the request and resets before any response byte; assert one arrival and a `ServiceRequestException`. Red: four arrivals.
A second theory pins the borrowed case: `A_caller_handler_keeps_its_native_retry` (a caller's `SocketsHttpHandler`, four arrivals) — the documented
scope of item 71.
**Change.** New internal `Http.SystemNet/FirstResponseByteStream.cs` (a `Stream` wrapper) and its `PlaintextStreamFilter` installation in
`CreatePrimaryHandler` (`SystemNetHttpClient.cs:506-516`, which the owned construction and the DI named client both use): it tracks "a request has
been written and no byte of its response has been read"; in that state a read returning 0 throws `IOException("the connection closed before any
response byte")`; every other read, write and dispose passes through unchanged (no byte inspected or altered). ≤ 70-line methods; sync and async
read paths both. The existing zero-length-content substitution for body-less `POST`/`PUT`/`PATCH` (`OutboundHeaderMapper.cs:138-143`) stays.
**Verify.** V-sn `BodylessRetryWireTests`, the whole SystemNet suite; V-conf (task 4.2 handles the waiver). **Commit.** `fix: SDK-built handlers stop SocketsHttpHandler's native re-send (TRANSPORT-2)`.

## Task 4.2 — The `transport-2` waiver follows the scope (convention 10)

**Change.** `tests/Dexpace.Sdk.Http.SystemNet.Tests/Conformance/SystemNetSubject.cs:135` and the DI driver's subject: delete the
`transport-2.bodyless-not-resent` waiver for the owned and DI subjects (it would turn stale and fail the run); keep it for the
handler-taking subject only if that subject is given a **caller-built** handler (`SystemNetSubject.cs:69` builds `new SocketsHttpHandler()`), with
`Owner = null` and the reason "a caller's handler is the caller's native client (design §11 item 71)". The kit gains no assertion.
**Verify.** V-conf over SystemNet and DI: green with the narrowed waiver list; the stale rule proves each deleted waiver was no longer needed.
**Commit.** `test: the transport-2 waiver applies only to a caller's handler`.

## Task 4.3 — PR 4 gate

V-gate; the AOT smoke (the filter is AOT-clean; the publish shows no warning).

---

# PR 5 — Observability, configuration, retry, SSE and auth

## Task 5.1 — `TraceIdGeneration` (`OBS-27`; §10 entry 24; P11-18; V13)

**Tests first** (Core.Tests `Diagnostics/TraceIdGenerationTests.cs`, Unit): 10 000 `W3C()` ids are non-zero 32-hex; 10 000 `Datadog()` ids have a
zero high half, a non-zero low half, and `FormatDatadog` renders a decimal `ulong` in range; an internal overload over a byte source that returns
zeros first proves both generators re-draw; `FormatDatadog(default)` throws `ArgumentException`; `TraceIdTests.cs:72-81` (the SDK installs no
generator) stays green. Red: compile error.
**Change.** New public static `Dexpace.Sdk.Core.Diagnostics.TraceIdGeneration` with `W3C()`, `Datadog()` (both `ActivityTraceId`, drawn from
`RandomNumberGenerator`), `FormatDatadog(ActivityTraceId)`; XML docs show `Activity.TraceIdGenerator = TraceIdGeneration.Datadog;` as the
application's opt-in. `BannedSymbols.txt:130` stays. `PublicAPI.Unshipped.txt`: the class and three members.
**Commit.** `feat: TraceIdGeneration offers the W3C and Datadog flavours with zero coercion (OBS-27)`.

## Task 5.2 — A null log value is the literal `null` (`OBS-3`; §10 entry 22)

**Test first.** `HttpLogRecordTests.A_null_value_is_emitted_as_the_literal_null` (Unit): the structured state's value for a null field is the string
`"null"` and the rendered message contains `null`, not `(null)`. Red. The existing `HttpLogRecordTests.cs:42-48` (structured null) is the assertion
this replaces; edited in the same commit, named in the message.
**Change.** `Diagnostics/HttpLogRecord.cs` (and any `Define` site passing a nullable): map `null` to `"null"` at emission. **Commit.** `fix: a null log field is emitted as "null" (OBS-3)`.

## Task 5.3 — The plain-name `Define` sites use the generator (styleguide 6.2; §10 entry 22)

**Test first.** none can fail for a refactor; this is a **pin** task: the existing tests of `ProxyResolutionLog`, `ProxyWarnings` and `HeaderDropLog`
(event ids, levels, keys) must pass unedited before and after. **Change.** `ProxyResolutionLog.cs:23`, `ProxyWarnings.cs:24`, `HeaderDropLog.cs:42-57`
become `[LoggerMessage]` partial methods with the same ids, names and levels. **Verify.** the pins; `DisabledPathAllocationTests`. **Commit.** `chore: the fixed-key log events with plain names use the LoggerMessage generator`.

## Task 5.4 — An empty value falls through to the lower provider (`CFG-2`; §10 entry 25, §11 item 68; P11-17; V11)

**Tests first** (DI.Tests, Unit):
- `EmptyValueTests.An_empty_value_falls_through_to_the_lower_provider` — a root with an in-memory provider (`Retry:MaxRetryAttempts = 5`) under an
  environment-style provider with an empty value; `AddDexpaceClient("c", root.GetSection("Dexpace:c"))` resolves 5. Red: the record default (it
  replaces `EmptyValueTests.cs:13-25`, edited in the same commit).
- `EmptyValueTests.A_section_from_an_unregistered_configuration_keeps_the_merged_read` — pin of the fallback.
- `EmptyValueTests.A_chained_configuration_is_walked` — a `ChainedConfigurationProvider` holding the lower value.
**Change.** `DexpaceClientStateSetup.cs:14,24-27` resolves `IConfiguration`; when it is an `IConfigurationRoot` and `root.GetSection(section.Path)`
enumerates the same pairs, `ConfigSection` (`ConfigSection.cs:25-35`) gets a provider-aware lookup: providers from the top, `TryGet` the full path,
skip blank values, walk a `ChainedConfigurationProvider.Configuration` root. No `ConfigurationBinder` (banned). `first-release.md`'s `di-empty-shadows`
goes in task 6.5. **Breaking.** CHANGELOG. **Commit.** `fix: an empty configuration value falls through to the next provider (CFG-2)`.

## Task 5.5 — `RetryOptions.PacingHeaders` (`RETRY-21`; §11 item 54)

**Tests first** (Core.Tests, Unit):
- `RetryPolicyTests.The_stage_stack_walks_the_configured_pacing_header_order` — `PacingHeaders = ["Retry-After"]`: a response carrying a delta-seconds
  `X-RateLimit-Reset: 60` and `Retry-After: 2` waits 2 s (a fake `TimeProvider` asserts the timer); with the default order it is unchanged. Red: compile error.
- `RetryOptionsTests.PacingHeaders_rejects_unknown_names_duplicates_and_empty` and `…_is_copied` (`CFG-33`-style defensive copy).
- `RetryRecoveryTests.The_recovery_stack_keeps_the_fixed_precedence` — pin.
**Change.** `Configuration/RetryOptions.cs`: `IReadOnlyList<string> PacingHeaders { get; init; }` (default the four, in the fixed order; validated
subset; copied); `Resilience/RetryPacing.TryGetHint` takes the order; `RetryPolicy.cs:196` passes it (`HonorRetryAfter = false` stays the off switch);
`RecoveryDispatcher.cs:141` keeps the fixed order. DI: `Retry:PacingHeaders` (a replacing list, the reader's list grammar) and a
`DexpaceConfigurationKeys` constant. `PublicAPI.Unshipped.txt`: the property. **Commit.** `feat: the stage stack's pacing headers are a configurable ordered list (RETRY-21)`.

## Task 5.6 — The SSE per-event data cap (`SSE-19`; §10 entry 20 R1; P11-19; V10)

**Test first.** `tests/Dexpace.Sdk.Core.Tests/Security/ServerSentEventLineCapTests.cs` gains
`Data_lines_that_each_fit_but_never_end_the_event_fail_once_the_event_cap_is_passed` (Security; sync and async; a reader with `maxEventBytes: 1024`
over 2 000 `data:x` lines; `ServerSentEventTooLargeException` with `MaxEventBytes == 1024`; the reader stays failed). Red: compile error. The
existing `:144` test stays green. A Unit test pins the default (8 MiB).
**Change.** `ServerSentEvents/EventAccumulator.cs:71` counts data bytes; `ServerSentEventReader` constructors, `ReadAll`/`ReadAllAsync` and
`ServerSentEventStream.FromResponse` gain `maxEventBytes` (overloads, not optional parameters, `RS0026`/`RS0027`); new public sealed
`ServerSentEventTooLargeException : StreamingException` in `Dexpace.Sdk.Core.ServerSentEvents` (not `Errors`, the `SSE-37` stray guard) carrying
`MaxEventBytes`. `PublicAPI.Unshipped.txt`: the overloads and the type. `sse.md` updated in 6.7. **Commit.** `fix: bound an SSE event's accumulated data (SSE-19)`.

## Task 5.7 — The attempt header is set and may not be the idempotency key (`RETRY-38`; §11 item 10)

**Tests first** (Unit): `RetryEngineTests.The_attempt_header_replaces_its_previous_value` (two retries; the third attempt carries exactly one
`X-Attempt: 3`); `RetryOptionsTests.AttemptHeaderName_rejects_the_idempotency_key_name` (`"Idempotency-Key"`, any case). Red: appended; accepted.
**Change.** `RetryEngine.cs:132-133` uses `Headers.Set`; `RetryOptions.cs:192` rejects the name. **Commit.** `fix: the retry attempt header replaces its value and never reuses the idempotency key (RETRY-38)`.

## Task 5.8 — The MD5 probe treats any non-fatal failure as refused (`AUTH-15`; §10 entry 16)

**Test first.** `Md5AvailabilityTests.An_unexpected_probe_failure_means_refused` (Unit): the probe over a delegate that throws
`InvalidOperationException` returns `false` and a later construction of `DigestChallengeHandler` succeeds. Red: the exception escapes (and would be
cached by the `Lazy`). **Change.** `Auth/Md5Availability.cs:26-42`: `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` → `false`, logging
nothing (no logger at that point; the handler's decline is the observable). **Commit.** `fix: an unexpected MD5 probe failure declines MD5 instead of failing construction`.

## Task 5.9 — PR 5 gate

V-gate; the AOT smoke (`CheckPhase11Async` gains a `TraceIdGeneration.Datadog()` draw and an SSE event-cap rejection).

---

# PR 6 — The ledger and close-out

## Task 6.1 — `docs/deviations.md`

Write the file in the design's shape: preamble (count **3**, target 5, the counting rule P11-2, audit commit = the PR 5 tip); *The audit*
(33 rows from the design's §10 table, `Status` and `Disposition` in their closed sets, `Evidence` re-pointed to line numbers **at the PR 5 tip** —
the repairs moved lines; re-derive each cite); *Retained deviations* (one `### N.` block per retained entry, *Verified* and *Why it stays*);
*Readings* (72 rows); *Negative scans* (N1–N20 with patterns, globs, allow-lists and probes); *Amendment set N11* (N11-1 … N11-12); *Deviations
recorded outside a phase* (empty). `docs/README.md`: the row loses "(planned)". **Commit.** `docs: the as-built deviation audit (docs/deviations.md)`.

## Task 6.2 — `LedgerAuditTests` and two bans (P11-5, P11-6)

**Tests first.** `tests/Dexpace.Sdk.Conformance.Tests/Gates/LedgerAuditTests.cs` (Unit) with rules L1–L11, each a `[Fact]`/`[Theory]` over the real
files plus a fixture theory (synthetic markdown in the test) that must fail it: a missing §10 number; `design only` in Status; a `path:line` past EOF;
a `retain` row with no class; an unknown ID; an `unmet-clause` row absent from `first-release.md` and a count of 6; a missing §11 row; a waiver with no
row; a scan matching outside its allow-list; a scan whose probe does not match; a cited test that does not exist. Write the fixtures first and see
each fail against a skeleton gate that returns "pass", then implement. The real-file facts go green only after 6.1 and 6.5 (same PR).
**Change.** `BannedSymbols.txt`: `M:System.Threading.Thread.Abort` and `T:System.Runtime.CompilerServices.ModuleInitializerAttribute` (with the
section comment). `requirement-coverage.cs`: `--section12` (print, per prefix, the satisfied / vacuous / not-satisfied (+ §10 entry) / deferred
(+ owner) sets from the resolved marks) and `--check-section12` (exit 1 when §12's last generated table differs); a ⏳ in a MUST owning row must name
an owner (a `docs/work/…` plan path with a task number, or a `first-release.md` entry), else a finding; `requirement-coverage-selftest.sh` gains a
case for each. `ci.yml`: a **Section 12** step after **Requirement coverage** (Linux), and its `CiWorkflowTests` row and conditional entry.
**Verify.** V-conf-tests `LedgerAuditTests`; the self-test; `--check-section12` green after 6.4. **Commit.** `test: the ledger-audit gate over docs/deviations.md; generate §12`.

## Task 6.3 — The phase 11 checklist

`docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-checklist.md`, written from what was built, `| ID | Level | Status | Task(s) | What was built, where proven |`,
with owning rows for: every ID a repair changed (`NFR-12`, `SEAM-1`, `NFR-1`, `RECOV-2`, `OBS-21`, `XCUT-8`, `TRANSPORT-20`, `XCUT-4`, `ASYNC-2`, `SEAM-30`,
`SEAM-29`, `BODY-14`, `HTTP-41`, `HTTP-19`, `XCUT-18`, `TRANSPORT-14` (✅ value and obs-text; 🚫 name, entry 32), `BODY-11`, `HTTP-40`, `PAGE-2`, `PAGE-21`
(✅ empty segments; 🚫 the unreserved residual, entry 18), `TRANSPORT-2`, `OBS-27`, `OBS-3`, `CFG-2`, `RETRY-21`, `RETRY-38`, `SSE-19`, `AUTH-15` (🚫 on a FIPS
host, entry 16), `XCUT-24` (✅, item 69)); and the **15 hand-off rows** (`RECOV-17` … `-24`, `-27`, `-28`, `-29`, `-34`, `BODY-4`, `BODY-5`, `BODY-10`), each ✅
citing the 6a, 6b, 6c or 3a evidence by file and test, with appendix C's level. `NFR-17` is not re-owned (it stays ⏳ in phase 10's row, which names its
owner). Levels match appendix C (`requirement-coverage.cs --check` is the proof). The styleguide audit groups (public surface, nullability, async,
disposal, analyzers, tests, trim/AOT, styleguide conflicts) with results; the Security-class statement; deviations from this plan.
**Verify.** `requirement-coverage.cs --report`: MUST ⏳ 1 (`NFR-17`). **Commit.** `docs: the phase 11 checklist`.

## Task 6.4 — Design §10, §11, §12 and the as-built refresh (P11-3, P11-21, P11-23)

One dated pass, "Correction 2026-10-<dd> (phase 11)", appended, never replacing text:
- §10: per entry, its verdict and disposition from the design's table (one or two lines each); the preface's count becomes three with the counting rule;
  entries 24, 28, 31, 33 marked **retired**; entry 32's reason replaced (P11-11); entry 26 reclassified (P11-10); the styleguide list gains the
  `LoggerMessage` generator departure (dotted keys only) and loses `CA1062`; entry 12's site count; entry 1's measurement; entry 8's "untouched".
- §11: the 16 doc-correct items; items 69–72.
- §12: the table from `requirement-coverage.cs --section12`, then `--check-section12` green.
- §§2–9: the as-built refresh — one appended dated line per **As built** paragraph. The proposed text per line (from the audit):

| Line | Refreshed status |
|---|---|
| 02:52, 02:62 | built, with §10 entry 2 (the DI cell names one root; the "core only" cell is the kit's) |
| 02:130, 02:183 | built (`latestPatch`; net10.0 everywhere; IVT to own tests; no SourceLink package) |
| 02:290 | built, with §10 entry 1: closure = facade + `DI.Abstractions` 10.0.12 |
| 03:205, 03:246, 03:251, 03:272 | built; second open returns the same stream (task 3.1); `BODY-12` declined, `BODY-36` declined |
| 03:444, 03:450, 03:459 | built, with §10 entry 32; entry 33 retired (task 4.1) |
| 03:605, 03:610 | built; `WaitOrDisposeAsync` is the cancel-without-interruption mode (task 2.4) |
| 03:697, 03:722, 03:727, 03:731, 03:740 | built |
| 03:803, 03:806 | built; `SEAM-28`'s operation id is not attached (MAY, declined) |
| 03:860, 03:863 | built, with §10 entry 9 (`ServiceSlot`, not `TryAdd`) |
| 03:892, 03:931, 03:934, 03:947, 03:953 | built; P3b-3's reporting gap remains for a pipeline without `InstrumentationPolicy` (design-only, `CFG-21` allows it) |
| 03:984 | built: the DI reader, with §10 entries 25 (`CFG-2` repaired) and 26 |
| 04:111 … 04:327 | built, with §10 entries 11, 29, 30, 31 (31 retired) |
| 05:59 … 05:183 | built, with §10 entry 14; `XCUT-24` met (item 69) |
| 05:285, 05:288, 05:311 | built, with §10 entries 12, 13 |
| 05:383, 05:385, 05:388 | built; the credential bridge is `TokenCredential.GetToken` |
| 05:446-449, 05:475, 05:488, 05:490 | built; no `CallKey` stamp or SDK reader (post-release trigger; item 30) |
| 06:89 … 06:435 | built, with §10 entries 15, 16; `RETRY-21` and `RETRY-38` repaired (5.5, 5.7) |
| 07:155, 07:159 | built, with §10 entries 17, 18, 19 |
| 07:296 | built, with §10 entry 20 (R1 repaired, 5.6) |
| 07:408, 07:431, 07:435 | built, with §10 entry 21 |
| 07:470 | not built: webhooks, owner `first-release.md` *Post-v1 packages* (D3) |
| 08:23, 08:31, 08:84, 08:125, 08:200, 08:215-218 | built, with §10 entries 22, 23 (the 9.0.5 sentence is superseded) |
| 08:431-433 | built, with §10 entry 23; `dexpace.redirect.hop` is wired; `OBS-28` byte counts declined; `OBS-27` repaired (5.1) |
| 08:341, 08:345, 08:356 | built, with §10 entries 25, 26 |
| 08:406, 08:409 | built, with §10 entry 8 |
| 09:107, 09:200, 09:254, 09:382, the 09:10-30 table | built; the kit is measured by the coverage gate; not built: package-validation baseline, strong naming, `NFR-16` (phase 12); the table gains an "At phase 11" column |

**Verify.** the housekeeping probe (`links`, `citations`); `--check-section12`. **Commit.** `docs: phase 11 dated corrections to design §2-§12`.

## Task 6.5 — `docs/first-release.md`, the overlay, `CLAUDE.md`, the knowledge notes

Execute the design's *first-release walk* row by row (ticks dated and pointed at evidence; nothing deleted, superseded lines struck through with a
dated note per the register's rule "ticked, dated and pointed at its evidence, never deleted"); the one-time public-surface list rewritten; *Unsatisfied
MUSTs* = the three (L6 checks the IDs); *SHOULD/MAY declined* gains `BODY-12`, `OBS-28`, `SEAM-28`, loses `XCUT-24` (dated); *Asymmetries* loses
`di-empty-shadows` (dated) and gains `borrowed-handler-native-retry`. The styleguide overlay (`docs/styleguide/README.md`, a deliberate human-directed
edit, dated): xUnit/Shouldly row, `MA0051` row, `CA1062` row (conformed), `LoggerMessage` row (dotted keys only). `CLAUDE.md`: the "What is genuinely
unbuilt" paragraph (phase 11 built), "Things That Will Bite You" (transport failures are `IOException`s; a second open returns the same stream; an empty
configuration value falls through; `WaitOrDisposeAsync`). Knowledge notes under `docs/knowledge/notes/`: supersede the configuration empty-value note;
add the transport-taxonomy, HTAB-edge, `TarWriter` and filter facts. **Verify.** `scripts/knowledge verify-structure`; `LedgerAuditTests` L6 green.
**Commit.** `docs: phase 11 register, overlay, CLAUDE.md and knowledge corrections`.

## Task 6.6 — User pages and `CHANGELOG.md`

`transport.md` (the filter, the waiver scope, `IOException`), `retry.md` (`PacingHeaders`, attempt header, the taxonomy), `sse.md` (the event cap),
`configuration.md`/`dependency-injection.md` (the fall-through), `bodies.md`/`http.md` (same-stream open, inbound names, special files, the `Uri` check),
`tracing-and-metrics.md` (`TraceIdGeneration`), `cross-cutting-invariants.md` (XCUT-4, -8, -18, -24), `conformance.md` (waiver list). `CHANGELOG.md`: confirm
every breaking line exists (each was added with its commit) and add the phase summary. Tick *Deviations audited* in `first-release.md`. **Commit.** `docs: phase 11 user pages and changelog`.

## Task 6.7 — The roadmap

Phase List row 11 links the design, plan and checklist; a dated Phase Status Note (counts from the checklist; the three counted clauses; the
inbound dispositions; the lead's open actions: the ruleset (`NFR-17`), the CI log of task 1.1 if not yet read, D3). **Commit.** `docs: phase 11 status note`.

## Task 6.8 — File the documents and probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 11
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 11 --write
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Repoint every citation of the inbox paths to `docs/work/mvp/phase11/`; the last probe reports nothing. **Commit.** `docs: file the phase 11 documents`.

## Task 6.9 — Final gate

V-gate, including `--check-section12`, the reproducible pack and its self-test, the AOT smoke, the tools solution and `verify-structure`; the Security
diff check (additions only); `requirement-coverage.cs --report` (MUST ⏳ 1). Hand the lead the six branch names. No push.

---

## Traceability

### Design §10 entries

| Entry | Disposition | Task(s) |
|---|---|---|
| 1 | retain; +repair allowance; +doc | 1.3, 6.4 |
| 2 | retain | 6.1, 6.4 |
| 3 | retain; +doc (net10.0) | 6.1, 6.2 (N1), 6.4 |
| 4 | retain | 6.1, 6.4 |
| 5 | retain | 6.1, 6.4 |
| 6 | repair | 3.1 |
| 7 | repair | 2.1, 2.2, 6.2 (N3) |
| 8 | retain; +repair F-A2, item 43; +doc | 2.3, 2.4, 6.2 (N4, `Thread.Abort` ban), 6.4 |
| 9 | retain; +doc | 6.2 (N2, `ModuleInitializer` ban), 6.4 |
| 10 | retain; +repair ParamName; +doc | 2.5, 6.2 (N5), 6.4 |
| 11 | retain | 6.1, 6.4 |
| 12 | doc-correct; +repair; gate | 1.4, 6.2 (N20), 6.4 |
| 13 | retain | 6.2 (N6), 6.4 |
| 14 | retain | 6.1, 6.4 |
| 15 | retain | 6.2 (N7), 6.4 |
| 16 | retain (counted); +repair probe | 5.8, 6.2 (N8), 6.4, 6.5 |
| 17 | retain; +repair Page copy | 3.5, 6.4 |
| 18 | retain (counted); +repair segments, `Uri` check | 3.4, 3.6, 6.4, 6.5 |
| 19 | retain; +doc | 6.2 (N9), 6.4 |
| 20 | repair R1 | 5.6 |
| 21 | retain | 6.4 |
| 22 | retain; +repair `OBS-3`, generator | 5.2, 5.3, 6.4 |
| 23 | retain; +repair `Disposal` | 1.5, 6.2 (N11), 6.4 |
| 24 | repair (retired) | 5.1 |
| 25 | repair `CFG-2`; retain rest | 5.4, 6.2 (N10), 6.4 |
| 26 | retain (reclassified) | 6.4, 6.5 |
| 27 | retain | 6.4 |
| 28 | repair (retired) | 1.6 |
| 29 | retain | 6.4 |
| 30 | retain | 6.4 |
| 31 | repair (retired) | 3.3, 6.2 (N17) |
| 32 | retain (counted), reason corrected | 4.2, 6.4, 6.5 |
| 33 | repair (retired) | 4.1, 4.2 |

### Design §11 items

| Items | Disposition | Task(s) |
|---|---|---|
| 1, 3, 5, 6, 7, 8, 9, 11, 13, 16, 19, 22, 23, 25, 26, 27, 28, 29, 31, 33, 35, 37, 38, 39, 40, 42, 45, 46, 48, 49, 50, 52, 53, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66 | retain (reading confirmed) | 6.1 (*Readings*), 6.2 (N11–N18 where scanned) |
| 17, 18, 21, 34 | retain, with a scan or wording note | 6.2 (N12, N13, N14), 6.4 |
| 2, 4, 12, 14, 15, 20, 24, 30, 32, 41, 44, 47, 51, 67 | doc-correct | 6.4 (item 30 also N19) |
| 10 | doc-correct; +repair attempt header | 5.7, 6.4 |
| 36 | repair | 3.2 |
| 43 | repair | 2.4 |
| 54 | repair | 5.5 |
| 68 | repair | 5.4 |
| 69–72 (new) | added | 6.4 |

### Inbound bullets

| Source | Bullet | Task(s) |
|---|---|---|
| 8a | F-A2 (`ASYNC-2`) | 2.3 |
| 8b | seven against five | 3.3, 4.1, 5.1, 6.1, 6.5 |
| 8b | the `TRACE` question | 4.1 (superseded) |
| 8b | the Core flake | 0.1, 1.2 |
| 9 | entries 25 and 26 as-built facts | 5.4, 6.4 |
| 9 | `CFG-2` in the count | 5.4 |
| 9 | item 30's reader | 6.4, 6.2 (N19) |
| 10 | `ServiceResponseException` | 2.2 |
| 10 | `XCUT-24` error snapshot | 6.3, 6.4 (item 69), 6.5 |
| 10 | `XCUT-4` count | 2.1 |
| 10 | `XCUT-8` reading | 1.6 |
| 10 | the HTAB edge | 3.2, 6.4 (item 70) |
| 10 | 16 ⏳ MUST rows, the *Every MUST dispositioned* line | 6.3, 6.5 |
| 10 | the proposed frozen-design corrections (§9, §10 entry 7, three §11 items, §12) | 6.4 |
| 10 (to 12) | one-time surface choices | 1.6, 2.2, 6.5 |
| — (found in this audit) | the reproducible-pack job | 1.1 |

### Exit criteria

| Criterion (design) | Task(s) |
|---|---|
| 1 `docs/deviations.md`, every row as-built | 6.1 |
| 2 ledger gate in CI | 6.2 |
| 3 §12 four sets, checked | 6.2, 6.4 |
| 4 one-time surface list | 6.5 |
| 5 inbound and walk done | the table above, 6.5 |
| 6 count three, held by L6 | 6.1, 6.2, 6.5 |
| 7 every gate green | 1.7, 2.6, 3.7, 4.3, 5.9, 6.9 |

## Plan-level readings

- **R1 — Task 1.1 depends on one human action.** The plan does not invent the CI log; if the lead declines the read, task 1.1 ships the toolchain
  check and the self-test, and the job's status after the lead's CI run decides whether more is needed.
- **R2 — Evidence lines move.** Every `path:line` in the design is at `eb9b758`; `docs/deviations.md` cites lines at the PR 5 tip, re-derived in 6.1,
  and L3 checks them.
- **R3 — Task 2.1's blast radius.** The `IOException` base changes no classifier behaviour (it reads `IRetryableError` and the cause chain); the risk is
  consumer code typed on `SdkException`, which the breaking-change note names. If review rejects P11-7, the alternative is a §10 retain with reason
  "the SDK root is a styleguide-level choice", and `TRANSPORT-20` joins the counted clauses (four); tasks 2.1's tests are dropped, 2.2 stays.
- **R4 — Task 3.1 edits many tests.** Each edited assertion is listed in the commit message with the `BODY-14` sentence it now follows; none is a
  `Security` test.

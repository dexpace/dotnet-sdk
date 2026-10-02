# Phase 3b — Bodies: Implementation Plan

**Status:** Draft, for review. Written 2026-10-02 against `main` at `313188f` (phase 2b merged). Design:
[phase 3b bodies design](2026-10-02-phase3b-bodies-design.md), the authority for every decision below (the plan
cites its row, position or ruling `P3b-n` rather than restating it, and changes none). Scope authority: the
[v1 roadmap](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md), Phase 3 card and Phase List row 3. Precedent for
format and depth: the [2a plan](../../phase2/phase2a/2026-09-30-phase2a-domain-model.md) and the
[2b plan](../../phase2/phase2b/2026-09-30-phase2b-seams.md). Both landed under `docs/work/`; this file is filed
to `docs/work/mvp/phase3/phase3b/` by the housekeeping `apply` step of task 7.6.

**What this document is.** The roadmap's step 3 for sub-phase 3b: numbered TDD tasks, in the design's landing order (seven
pull-request-sized steps), each with its failing tests, its production change, its `PublicAPI.Unshipped.txt` diff, its
**Breaking** markings, its `CHANGELOG.md` entry, its requirement IDs and its verification commands. It is not the
checklist (written from what was built, task 7.3) and it writes no production code.

**Scope.** 49 rows: `BODY-1`–`BODY-37`, `HTTP-36`–`HTTP-45`, `HTTP-51`, `HTTP-52`. `HTTP-44`/`HTTP-45` are dispositioned
(hand-off to 7a), `HTTP-39`/`BODY-10` belong to 3a. Every row maps to a task in the
[traceability table](#traceability-id--pr--task).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them
   green; then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so;
   a pin is proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and the curated
   `GlobalUsings.cs` set only (`ImplicitUsings` is off; Core's set is `System`, `System.Collections.Generic`,
   `System.IO`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`; everything else is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await`, `await using` and `await foreach` (`CA2007`); methods at most
   70 lines (`MA0051`); `///` XML docs on every public member (CS1591); no new `PackageReference` in
   `Dexpace.Sdk.Core` (constraint 2; nothing in 3b needs one: `RandomNumberGenerator`, `Activity`, `Encoding`,
   `SemaphoreSlim` and `ExceptionDispatchInfo` are shared framework). `RS0030`: no `Task<T>.WaitAsync` (the drain uses
   `SemaphoreSlim`).
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it, in every suite). 3b adds no
   `Security` class. Core test classes mirror the source folders: `tests/Dexpace.Sdk.Core.Tests/Http/Request/` (namespace
   `Dexpace.Sdk.Core.Tests.Http.Requests`), `…/Http/Response/` (`…Http.Responses`), and `…/Internal/` for `Disposal`.
   `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only, never a transport (SEAM-2), so every check that needs
   `SystemNetHttpClient` or `HttpResponseMessageBody` lives in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`. Internal types
   are reached through `InternalsVisibleTo` (the library's own test project only).
5. **Security tests are never deleted or loosened.** `EnsureSuccessErrorMappingTests` gets the mechanical `Dispose(bool)`
   rewrite of its private `TrackingBody` fake in task 1.2 and **no assertion changes value**; `MalformedContentTypeWireTests`
   and the four SystemNet `Security` wire classes are **unedited**. Every PR's close-out runs
   `git diff main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` and
   expects: empty in PRs 2 to 7, and in PR 1 only the `TrackingBody` hunk (the diff is read, not just counted). A change
   that forces any other edit is a signal to re-read design §3.7 and the design's Migration table, not to edit the file.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed or removed member is an edit or deletion
   of its line in `Unshipped`, never a `*REMOVED*` marker. The lines listed per task are the expected hand-authored diff;
   the build's `RS0016`/`RS0017` output is the authority. Protected members are listed without a visibility keyword.
   Aliases (expand when pasting): `[Q]` = `Dexpace.Sdk.Core.Http.Request`, `[S]` = `Dexpace.Sdk.Core.Http.Response`,
   `[C]` = `Dexpace.Sdk.Core.Http.Common`, `[T]` = `System.Threading.Tasks`. Internal types appear in no `PublicAPI` file.
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes,
   stating what it was. Each PR's last task adds the matching `CHANGELOG.md` `[Unreleased]` line, prefixed **Breaking:**
   where the design's "Breaking changes" table lists it (constraint 8).
8. **One PR per step, code and tests together.** Every commit inside a PR is green too. PR 1 contains one change that
   cannot be split at the type (the `ResponseBody` dispose pattern), so it is ordered *additive `Disposal` → the pattern
   change with the mechanical adaptation of every subclass → everything that rests on it*. Commit style `feat:` /
   `feat!:` / `fix:` / `chore:` / `test:` / `docs:`; no AI attribution anywhere. The plan commits nothing and pushes
   nothing; the executor is told when to.
9. **Re-deriving on 3a's merged code.** 3a lands first (design Prerequisites). The design's names for 3a's items are
   placeholders; **task 0.1 fixes them** and every later task uses the names it records. Where this plan writes
   `CopyExactly`, `TeeStream`, `Tap`, `WriteTo`, `OpenRead` it means "3a's merged member, as recorded by task 0.1".

### Environment

```bash
export DOTNET_ROOT=<the SDK 10.0.401 install; the 2b plan used one under the session scratchpad>
export PATH=$DOTNET_ROOT:$PATH
```

### Verification blocks

**V-fast** (inner loop, one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
# SystemNet classes: --project tests/Dexpace.Sdk.Http.SystemNet.Tests ; serde: tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests
```

**V-gate** (the last task of every PR; the whole of CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/30, MA0051, CA2007, CA1063/CA1816, CA2000
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # see convention 5
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`
from clean, then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and
`scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) runs before the push of PR 7 and of any PR whose diff removes
a test (PR 1 replaces one). No `PackageReference` changes, so no `packages.lock.json` change is expected; if a locked
restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info BODY
scripts/knowledge --prefix-info HTTP
scripts/knowledge --gaps BODY               # single prefix per call (the multi-prefix form dropped prefixes, #31/#42)
scripts/knowledge --req BODY-22             # per row in scope of the PR
```

---

## Ambiguities resolved by the plan

The design is complete enough that the plan changes no decision. Where it left a detail open, the plan took the reading
most consistent with it; each is restated at the task it touches.

| # | Ambiguity | Reading taken | Task |
|---|---|---|---|
| A1 | Design says `ErrorBodyPreviewTests` (`BODY-33`) as if it existed; no such class is in `tests/` at `313188f` | New class, new file `tests/Dexpace.Sdk.Core.Tests/Http/Response/ErrorBodyPreviewTests.cs`; the reading-twice behaviour is a pin over S8's existing code | 1.6 |
| A2 | `Disposal.DisposeQuietly(resource, primary, logger)`: what a non-null `primary` does before 4b | The report carries the `dexpace.dispose.primary_type` tag and the primary is returned untouched (the method is `void`; it never throws a non-fatal exception and never replaces the primary) | 1.1 |
| A3 | The P3b-16 test needs an `Activity` on the worker thread, and `Disposal` reports on `Activity.Current` | The test starts an `Activity` from a test-local `ActivitySource` (with an `ActivityListener`) on the calling thread before `ExecuteAsync`; `ExecutionContext` flows into the `LongRunning` worker, so `Activity.Current` is set there. If the flow does not hold on the pinned runtime, the test passes the activity through `Disposal`'s logger parameter instead and says so | 1.5 |
| A4 | "Fatal" filter before 4b | Private predicate `ex is OutOfMemoryException` only (which includes `InsufficientMemoryException`), exactly P3b-3 and design §5.2 / §10 entry 12: `StackOverflowException` is uncatchable and `AccessViolationException` is not delivered on .NET, so neither is listed. 4b repoints `Disposal` to `ExceptionFacts.IsFatal`, which has the same set | 1.1 |
| A5 | P3b-11 says in-memory response bodies "keep serving after dispose", but `BytesResponseBody` is single-use | Unchanged: the consumed flag stays; "serving" means a not-yet-read in-memory body is still openable after dispose, and a read one reports `StreamConsumedException` | 1.2 |
| A6 | The seekable variant needs a stream with `CanRead && CanSeek` and `contentLength` in `[0, Array.MaxLength]`; what about `Length < Position + contentLength` | Not checked at construction (the stream may grow); the exact copy fails loud at write time with 3a's `EndOfStreamException` | 2.3 |
| A7 | `FromForm` input `null` entries (null name or value) | `ArgumentNullException` naming the field index only, never the value; an empty name or value is legal | 2.1 |
| A8 | Where `BoundedWriteStream` lives and who disposes it | `Http/Request/BoundedWriteStream.cs`, `internal sealed`; `Dispose(bool)` never touches the primary (the destination belongs to the transport) | 4.2 |
| A9 | `LoggingResponseBody.ContentLength` before and after capture (`BODY-29`) when the drain is in progress | The delegate's declared length until `IsFullyCaptured` is `true`; read without a lock through a `volatile` field | 6.3 |
| A10 | Which 3a surface the logging wrappers' synchronous paths need | Only what the base types force: a synchronous `WriteTo`/`OpenRead` override per 3a's abstract-or-virtual decision; the drain's sync half is the same core with `bool async = false` (§11 item 12) | 5.1, 6.4 |

---

## PR 0 — Pre-flight (no commit)

### Task 0.1 — Record 3a's merged surface

Before PR 2 opens (PR 1 has no 3a edge). Read 3a's merged code and write down, in the PR description (not in a file),
the exact names, namespaces and visibility of: the exact-length copy (sync and async), `TeeStream` and its tap accessors
(clear, snapshot, count), the materialisation-cap setting, `RequestBody.WriteTo` and `ResponseBody.OpenRead` (abstract or
virtual), and any synchronous reader. Confirm the five contracts in the design's "interface assumed from 3a" table hold:
no zero-count read, `EndOfStreamException` naming delivered-of-total, mirror-before-forward, `Flush`/`Dispose` to the
primary only, `leaveOpen` for helpers (`IO-6`). If one does not hold, **stop and reopen the consuming row with the lead**
(the design says so); do not adapt silently.

```bash
git log --oneline main -20 && git grep -n "CopyExactly\|class TeeStream\|WriteTo(\|OpenRead(" -- src
```

If 3a has not merged when PR 1 is ready, PR 1 proceeds alone (it is free now); PRs 2.3 onward wait. Task 2.1/2.2 (the
form body) are also free now and may land first.

---

## PR 1 — Disposal, latches, readers and the BOM

**Gate: free now.** Breaking items 1 to 6. Rows: `HTTP-41`, `HTTP-42`, `HTTP-43`, `HTTP-52` (cite), `BODY-14`,
`BODY-15`, `BODY-16`, `BODY-30` (cite), `BODY-33`, inherited `SEAM-14`. If 3a lands first and edits `ResponseBody.cs`
(sync `OpenRead`, capped `ReadAsBytesAsync`), re-derive each change below on 3a's merged file rather than resolving hunks.

### Task 1.1 — `Disposal` (additive; P3b-3, `BODY-28` groundwork, `SEAM-14` groundwork)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/DisposalTests.cs`, class `DisposalTests`, `Unit`
(a test-local `ActivitySource` plus `ActivityListener` capturing stopped activities and their events; a test-local
`ListLogger : ILogger` recording level and message):

- `A_null_resource_is_a_no_op` (sync and async)
- `A_resource_that_disposes_cleanly_is_disposed_once_and_reports_nothing`
- `A_throwing_dispose_is_swallowed_and_never_propagates` (sync, async)
- `A_throwing_dispose_adds_an_exception_event_tagged_dexpace_dispose_suppressed_on_the_current_activity` (fact 7: event
  named `exception`, tag `dexpace.dispose.suppressed = true`, `exception.type` the thrown type)
- `With_a_primary_in_flight_the_report_carries_the_primary_type_and_the_primary_is_untouched` (A2: tag
  `dexpace.dispose.primary_type`; the primary instance, its message and its `Data` are unchanged)
- `With_a_logger_a_warning_names_the_resource_type_and_the_exception_type_and_never_a_value` (level `Warning`; the message
  contains both type names and does not contain the exception's message text)
- `With_no_listener_and_no_logger_nothing_is_observable_and_nothing_throws` (the documented interim gap, P3b-3)
- `A_fatal_exception_propagates` (A4: `OutOfMemoryException` thrown by `Dispose` escapes `DisposeQuietly`)
- `An_async_only_resource_is_disposed_through_DisposeAsync`

Red: CS0103/CS0246, `Disposal` does not exist.

**Production.** New `src/Dexpace.Sdk.Core/Internal/Disposal.cs`, `internal static class Disposal` (namespace
`Dexpace.Sdk.Core.Internal`, beside `SdkVersion.cs`), exactly the two signatures of the design's "Internal types"
(`ILogger?` from `Microsoft.Extensions.Logging`, already referenced). Private `Report(Exception ex, string resourceType,
Exception? primary, ILogger? logger)`: `Activity.Current?.AddException(ex, tags)` with a `TagList` carrying
`dexpace.dispose.suppressed` and, when `primary` is non-null, `dexpace.dispose.primary_type`; and, when `logger` is
non-null, a `LogWarning` using `LoggerMessage.Define` (source-generated logging is not in use elsewhere in Core; follow
the style of the existing `src/` call sites and keep AOT-clean: no format-string reflection). Private `IsFatal(Exception)`
(A4: `ex is OutOfMemoryException`, nothing wider). The XML remarks state the interim gap in the design's words (no listener and no logger: reported to nobody, until
5b) and name the repoints (4b: `ExceptionTrail.AddSuppressed`, `ExceptionFacts.IsFatal`; 5b: logger plumbing). Both
methods at most 70 lines; `DisposeQuietlyAsync` uses `ConfigureAwait(false)`.

**PublicAPI:** none (internal). **Docs:** none public. **IDs:** groundwork for `BODY-28`, `HTTP-43`, `SEAM-14`.
**Verify:** V-fast `DisposalTests`.

### Task 1.2 — The `ResponseBody` dispose pattern and every subclass (P3b-2, `BODY-15`, `HTTP-41`; **Breaking 1, 5**)

The one change that cannot be split at the type: `ResponseBody.Dispose()` and `DisposeAsync()` stop being virtual, so
every subclass is migrated in the same commit.

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ResponseBodyLifecycleTests.cs`, class
`ResponseBodyLifecycleTests`, `Unit` (a test-local `ReleaseCountingBody : ResponseBody` overriding the two protected
members, plus the built-in `FromStream`/`FromBytes` variants over a `DisposalTrackingStream`, **new in this task**: a
`Stream` wrapper that counts `Dispose(bool)` and `DisposeAsync` calls and exposes `IsDisposed`, created in
`tests/Dexpace.Sdk.TestSupport/Streams/DisposalTrackingStream.cs` (public, `///`-documented, MIT header, curated usings),
with a self-test `tests/Dexpace.Sdk.Core.Tests/Support/DisposalTrackingStreamTests.cs` (`Unit`) in the style of
`DisposalCountingBodyTests`; tasks 2.4, 3.2 and 4.4 reuse it):

- `Dispose_releases_once_across_Dispose_and_DisposeAsync_in_any_order` (sync-sync, async-async, mixed both ways)
- `Concurrent_disposes_release_exactly_once` (`Barrier`-started, N = 16, counts not timings)
- `A_release_that_throws_still_flips_the_latch_and_propagates_once` (first call throws, second is a silent no-op)
- `A_never_read_body_is_released_by_dispose` (`BODY-15`)
- `The_default_DisposeAsyncCore_calls_Dispose_true`
- `A_stream_body_read_after_dispose_throws_StreamClosedException` (P3b-11, **Breaking 5**)
- `A_stream_body_that_was_read_and_then_disposed_reports_consumed_not_closed` (P3b-11 ordering: consumed first)
- `An_unread_in_memory_body_still_serves_after_dispose` (A5, `FromBytes`)
- `A_second_open_throws_and_names_the_buffering_route` (`BODY-14`: `StreamConsumedException`, message mentions the
  buffering wrapper / `FromReplayableBytes` route per design position B; a pin for the type, new for the message; over
  `FromStream` and `FromBytes`, asserting the message equals the one shared constant)
- `The_release_of_the_stream_variant_disposes_the_source_exactly_once` (sync and async paths)

Red: CS0115/CS0506 compile errors in the subclass fakes of the test (no `Dispose(bool)` to override).

**Production.** Edit `src/Dexpace.Sdk.Core/Http/Response/ResponseBody.cs`:

- `private int _disposed;` `public void Dispose()` (non-virtual): `if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
  Dispose(true); GC.SuppressFinalize(this);`.
- `public ValueTask DisposeAsync()` (non-virtual): the same latch, then `await DisposeAsyncCore().ConfigureAwait(false)`;
  `GC.SuppressFinalize(this)`.
- `protected virtual void Dispose(bool disposing) { }` and `protected virtual ValueTask DisposeAsyncCore() { Dispose(true);
  return ValueTask.CompletedTask; }`. No finalizer (styleguide 13.3); no `protected IsDisposed` (10.1). Throwing from
  the release must still leave the latch flipped (it is flipped before the call).
- One shared message for every second-open `StreamConsumedException` (an `internal const string` on `StreamConsumedException`
  or `ResponseBody`, visible to SystemNet through `InternalsVisibleTo` or, if not granted, duplicated verbatim with a test
  asserting equality), naming the buffering route (the explicit wrapper, or `FromReplayableBytes` for error bodies; position B,
  `BODY-14`). `StreamResponseBody`, `BytesResponseBody` and `HttpResponseMessageBody` all throw it, replacing "This response
  body has already been read."
- `StreamResponseBody`: `_consumed` check first, then a `_closed` flag (set in the release) → `StreamClosedException`
  from `OpenReadAsync` (P3b-11). Its `Dispose(bool)` disposes `source`; its `DisposeAsyncCore` awaits
  `source.DisposeAsync().ConfigureAwait(false)`. `BytesResponseBody` and `ReplayableBytesResponseBody` need no override.
- Class remarks: replace the "Always dispose the body" sentence with the latch contract; add the **Breaking** paragraph
  (items 1 and 5: "was `public virtual`; subclasses override `Dispose(bool)` / `DisposeAsyncCore()`").
- Verify with a throwaway build (not committed) that the pattern without a finalizer passes `CA1063`/`CA1816` under
  `latest-recommended`; if an analyzer objects, fix the source, never suppress without a rationale in `.editorconfig`.

**Mechanical migration, same commit** (design Migration table; the executor greps `git grep -n ": ResponseBody"` **and** `git grep -n DisposeCount` for
anything missed):

| File | Rewrite |
|---|---|
| `src/Dexpace.Sdk.Http.SystemNet/HttpResponseMessageBody.cs` | `Dispose(bool)` releases `_message`; no `DisposeAsyncCore` override needed; `OpenReadAsync` throws `StreamClosedException` after dispose, after the consumed check (P3b-11); the second-open `StreamConsumedException` uses the shared buffering-route message (`BODY-14`). Class summary updated |
| `tests/Dexpace.Sdk.TestSupport/Transports/DisposalCountingBody.cs` | `override void Dispose(bool)`; the doc says it now counts **releases** (at most one) |
| `tests/Dexpace.Sdk.Core.Tests/Support/DisposalCountingBodyTests.cs` | Not a subclass, so a `: ResponseBody` grep misses it. `Dispose_and_DisposeAsync_each_increment_the_count` asserted 2 after `Dispose()` then `DisposeAsync()`; under the latch it is 1. Renamed `Dispose_and_DisposeAsync_release_once`, asserting 1 after each call and after both (a reviewed assertion change, listed in task 7.3) |
| `tests/Dexpace.Sdk.Core.Tests/Security/EnsureSuccessErrorMappingTests.cs` (`TrackingBody`) | `override void Dispose(bool)` only; no assertion changes (convention 5) |
| `tests/Dexpace.Sdk.Core.Tests/Http/Response/ResponseTests.cs`, `Pagination/PageableTests.cs`, `Pipeline/Policies/RedirectPolicyTests.cs`, `Client/SyncToAsyncBridgeTests.cs` (`ThrowingDisposeBody`) | the same override rewrite |
| `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`, `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/BodyConvenienceTests.cs` | subclasses that do not override dispose today: compile-check only |

Also in this task: new `tests/Dexpace.Sdk.Http.SystemNet.Tests/HttpResponseMessageBodyTests.cs`, class
`HttpResponseMessageBodyTests`, `Unit` (a message assertion on the second-open `StreamConsumedException`, equal to the shared
buffering-route message, goes in `A_read_then_dispose_then_read_reports_consumed` and in a dedicated
`A_second_open_throws_and_names_the_buffering_route`; an in-process `HttpMessageHandler` returning a `TrackingContent : HttpContent`
that counts `Dispose(bool)`, no socket): `The_message_is_disposed_once_across_Dispose_and_DisposeAsync`,
`A_read_after_dispose_throws_StreamClosedException`, `A_read_then_dispose_then_read_reports_consumed`,
`A_never_read_body_releases_the_message`. These construct the body through `SystemNetHttpClient` or through
`InternalsVisibleTo` (the SystemNet test project already has it for its own types; if not, add the grant for the test
project only).

**PublicAPI.Unshipped.txt** (Core; sorted into place). Existing `virtual …ResponseBody.Dispose() -> void` and
`virtual …ResponseBody.DisposeAsync() -> ValueTask` lines lose `virtual`; new protected lines:

```text
[S].ResponseBody.Dispose() -> void
[S].ResponseBody.DisposeAsync() -> [T].ValueTask
[S].ResponseBody.Dispose(bool disposing) -> void
[S].ResponseBody.DisposeAsyncCore() -> [T].ValueTask
```

(the last two with the `virtual` keyword the analyzer prints for protected virtual members; copy from the `RS0016`
output). **IDs:** `HTTP-41`, `BODY-14`, `BODY-15`. **Verify:** V-fast `ResponseBodyLifecycleTests`,
`HttpResponseMessageBodyTests`; then `dotnet build Dexpace.Sdk.sln --configuration Release` and the full test run (every
fake compiles; every `DisposeCount` assertion holds except the one in `DisposalCountingBodyTests`, rewritten above).

### Task 1.3 — `Response` latch and `SystemNetHttpClient` latch (`HTTP-43`, `SEAM-14`; **Breaking 2**)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ResponseDisposeLatchTests.cs`, class
`ResponseDisposeLatchTests`, `Unit` (using `DisposalCountingBody`):

- `Double_dispose_releases_once` (sync-sync, async-async, mixed)
- `A_throwing_body_propagates_once_and_the_second_call_is_a_no_op`
- `A_never_read_body_is_released_by_response_dispose`
- `Concurrent_response_disposes_release_once` (`Barrier`, N = 16)
- `EnsureSuccessAsync_on_an_error_status_then_a_caller_dispose_releases_once` (the existing `finally { DisposeAsync }` plus a
  second dispose; a pin that the latch absorbs it)

And in `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientDisposeTests.cs` (new, class
`SystemNetHttpClientDisposeTests`, `Unit`): `An_owned_client_disposes_its_HttpClient_once_across_Dispose_and_DisposeAsync`
(**a pin**: `HttpClient` disposal is already idempotent, so a second `Dispose` not throwing and calls still throwing
`ObjectDisposedException` hold before the change; it does not by itself prove the SDK-owned latch. To make the latch
observable, reach the owned client through the internal `SystemNetHttpClient(Action<SocketsHttpHandler>)` constructor via
`InternalsVisibleTo`, or add an internal release-counting seam on the owned client, and assert exactly one release; that
assertion is the one seen red), `A_borrowed_client_is_never_disposed_by_either_form`,
`Concurrent_disposes_are_safe`. Beside it, assertions that `DelegateHttpClient` and both bridges' disposes are idempotent
no-ops (pins; they live in `Core.Tests/Client/` as `Bridge_and_delegate_disposes_are_idempotent`).

Red: `Response` double-dispose reaches the body twice (counts 2 with a fake that does not latch); `SystemNetHttpClient`'s
owned-client release count is 2 through the internal seam. The behavioural tests (`ObjectDisposedException`, no throw) are pins
and the checklist says so, as the 2b checklist did for `SEAM-14`.

**Architecture test, same commit.** `tests/Dexpace.Sdk.Core.Tests/Architecture/ModelImmutabilityArchitectureTests.cs`
(`Every_instance_field_is_readonly_except_the_documented_body_state`) lists `Response` in `ModelTypes` with an empty
allow-list, so the new mutable field fails it. Add `Response._disposed` to the allow-list with the rationale "HTTP-43 and
P3b-2: the idempotent-close latch; a reviewed decision", and update the class comment that says Response's disposal state
lives in its body. Task 1.7's PR notes and task 7.3's changed-assertions table list it.

**Production.** `Response.cs`: `private int _disposed;` in `Dispose()`/`DisposeAsync()` the same `Interlocked.Exchange`
latch, then forward to `Body`; `GC.SuppressFinalize` kept. Remarks: the **Breaking** paragraph (item 2). `SystemNetHttpClient.cs`:
`private int _disposed;` latch in `Dispose()`; `DisposeAsync()` calls it; remarks line "Phase 8b's latch makes both throw"
is reworded to "disposal is idempotent (SEAM-14); 8b adds the throw after dispose (SEAM-15)". **PublicAPI:** none.
**IDs:** `HTTP-43`, `SEAM-14`. **Verify:** V-fast `ResponseDisposeLatchTests`, `SystemNetHttpClientDisposeTests`,
`ModelImmutabilityArchitectureTests`.

### Task 1.4 — Readers dispose the body; `TextDecoding` and the BOM (P3b-12, P3b-13, `BODY-16`, `HTTP-42`; **Breaking 3, 4**)

**Failing tests first.** Extend `ResponseBodyLifecycleTests`: `Readers_dispose_the_body_on_success_and_on_failure`
(`ReadAsBytesAsync` and `ReadAsStringAsync`, over a body whose stream throws mid-read; the release count is 1 in both).
New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ResponseBodyDecodeTests.cs`, class `ResponseBodyDecodeTests`, `Unit`:

- `A_UTF8_BOM_is_stripped_with_no_charset` and `…with_charset_utf-8`
- `A_UTF16_LE_BOM_is_stripped_under_charset_utf-16` (fact 5)
- `A_UTF16_BE_BOM_is_stripped_under_charset_utf-16be`
- `A_UTF32_BOM_is_stripped_under_charset_utf-32`
- `A_BOM_that_does_not_match_the_resolved_charset_is_kept` (UTF-8 BOM bytes under `charset=iso-8859-1` decode to three
  Latin-1 characters; the declared charset wins, `HTTP-42`)
- `Only_a_leading_BOM_is_stripped` (a U+FEFF mid-text survives)
- `No_charset_decodes_as_UTF8` and `An_unknown_charset_falls_back_to_UTF8` (pins; 2a fixed `utf-7`)
- `An_empty_body_decodes_to_the_empty_string`
- `Decoding_is_the_one_routine_the_readers_share` (an internal-facing test of `TextDecoding.Decode(ReadOnlySpan<byte>,
  Encoding?)`, asserting the same output through `ReadAsStringAsync`)

Red: CS0103 (`TextDecoding`); the BOM cases fail by a leading U+FEFF.

**Production.** New `src/Dexpace.Sdk.Core/Internal/TextDecoding.cs`: `internal static class TextDecoding` with
`internal static string Decode(ReadOnlySpan<byte> bytes, Encoding? declared)`: resolve `declared ?? Encoding.UTF8`,
read `encoding.Preamble`, skip it when non-empty and the bytes start with it, `GetString` the rest. 3a's synchronous string
reader (if it exists) is repointed to this routine in this task; otherwise 3a's merged code is left alone and a one-line
note in the PR says the sync reader is 3a's to repoint (P3b-12 requires one shared routine: if 3a shipped its own decode,
**this task replaces it**, because the same file is touched). `ResponseBody.ReadAsBytesAsync` becomes
`try { … } finally { await DisposeAsync().ConfigureAwait(false); }` (the existing inner `await using` over the stream stays);
`ReadAsStringAsync` calls `TextDecoding.Decode`. The remarks say "then closes the stream **and the body**" and carry the
**Breaking** paragraphs (items 3 and 4). `ResponseBodySerdeExtensions.ReadValueAsync` is **not** changed (P3b-13; routed to
7a in the checklist). Grep `git grep -n "ReadAsBytesAsync\|ReadAsStringAsync" -- src` for any call site that uses the body
afterwards; none is known.

**PublicAPI:** none (signatures unchanged; the lines carry no behaviour). **IDs:** `BODY-16`, `HTTP-42`.
**Verify:** V-fast `ResponseBodyLifecycleTests`, `ResponseBodyDecodeTests`; full Core and serde test runs (serde
`BodyConvenienceTests` reads then may reuse; fix tests that assumed an undisposed body, stating each in the PR).

### Task 1.5 — `AsAsync` on `DisposeQuietly`; the pin test replaced (P3b-16; **Breaking 6**)

**Failing test first.** Edit `tests/Dexpace.Sdk.Core.Tests/Client/SyncToAsyncBridgeTests.cs`: rename
`A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` to
`A_throwing_dispose_after_cancellation_cancels_the_task_and_reports_the_failure` and rewrite its body (A3): start an
`Activity` from a test-local source with an `ActivityListener` on the calling thread, run the same scenario, then assert
`task.IsCanceled` and exactly one `exception` event tagged `dexpace.dispose.suppressed` with
`exception.type == typeof(InvalidOperationException).FullName`. Update the comment above it (the "until 3b" note goes).
The other `SEAM-30` tests (`Cancelling_after_delivery…`, the disposal-count test above it) are unchanged. Red: the task is
`Faulted`.

**Production.** `src/Dexpace.Sdk.Core/Client/HttpClientExtensions.cs`, `SyncToAsyncAdapter.Run`:
`response.Dispose()` becomes `Disposal.DisposeQuietly(response)`; update the comment. The `AsAsync` remarks lose
"Until phase 3b's quiet disposal, a Dispose that throws there faults the task with that exception instead." and gain the
**Breaking (behaviour)** paragraph (item 6). **PublicAPI:** none. **IDs:** closes 2b's `SEAM-30` interim; groundwork `BODY-28`.
**Verify:** V-fast `SyncToAsyncBridgeTests`.

### Task 1.6 — Pins and citations (`BODY-33`, `HTTP-52`, `BODY-30`; A1)

New `tests/Dexpace.Sdk.Core.Tests/Http/Response/ErrorBodyPreviewTests.cs`, class `ErrorBodyPreviewTests`, `Unit`: all
**pins** over S8's existing code, proven able to fail per convention 1 (temporarily make the error body single-use):
`Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works`,
`The_error_body_is_still_readable_after_the_response_is_disposed` (P3b-11's in-memory clause),
`An_empty_error_body_reads_as_an_empty_body_never_null` (§10 entry 30), `Disposing_the_exception_response_twice_is_harmless`.
Re-run, unedited beyond task 1.2, `EnsureSuccessErrorMappingTests` (`Security`) over the new latch: the checklist cites
`A_non_error_status_is_returned_with_its_body_intact` for `BODY-31`.

**Production.** None. **IDs:** `BODY-33`, `HTTP-52` and `BODY-30` (cite; ⏳ 4c for the policy form). **Verify:** V-fast
`ErrorBodyPreviewTests`, then the `Security` filter run.

### Task 1.7 — Close-out (PR 1)

`CHANGELOG.md` `[Unreleased]`, under `### Changed` with **Breaking:** lines for items 1 to 6 (one line each, the member
and what it was) and `### Fixed` for the quiet-disposal and BOM behaviour; the 2b hand-off sentence. The PR description lists the reviewed test-assertion changes: the `ModelImmutabilityArchitectureTests`
allow-list entry for `Response._disposed` (1.3) and the `DisposalCountingBodyTests` rewrite (1.2). Run **V-gate** and the
coverage gate (a test was replaced). **Commits:** `feat!: response body and response dispose latches, quiet disposal, BOM strip`
(one commit for 1.2 as it is the atomic change), preceded by `feat: internal Disposal helper` (1.1) and followed by
`fix: response readers dispose the body` (1.4), `fix: AsAsync disposes a post-cancellation response quietly` (1.5),
`test: error-body preview pins` (1.6). **Rows closed:** see traceability.

---

## PR 2 — Form body, `FromStream` validation, the seekable variant

**Gate:** tasks 2.1 and 2.2 (form) are **free now**; 2.3 to 2.5 (stream and seekable) need 3a's exact copy and synchronous
`WriteTo` (task 0.1). The design allows splitting into two PRs (2a form, 2b seekable) if 3a is late; the plan numbers
tasks so the split is a cut after 2.2. Breaking item 7 lands in 2.3. Rows: `HTTP-36`–`HTTP-38`, `BODY-1`, `BODY-3`,
`BODY-6`–`BODY-9`, `BODY-35`.

### Task 2.1 — `FormUrlEncoder` and the vector file (`HTTP-38`; P3b-7)

**Vectors first.** New `tests/vectors/body/form-urlencoded.json` in the `{ "source": "...", "cases": [...] }` shape of
`VectorFile`, each case `{ "name", "fields": [["n","v"],…], "wire": "…" }` or `{ "name", "fields": …, "rejects": true }`.
`"source"` cites the WHATWG URL Standard's `application/x-www-form-urlencoded` serializer and notes the Node (RFC 3986 plus
`%20`→`+`) disagreement on `~` and `*`. Cases (design "Tests, vectors and ports"): fact 6's line (`a b*-._~!'()é` →
`a+b*-._%7E%21%27%28%29%C3%A9`), `~`, `*`, space, a literal `+`, `&`, `=`, `é`, an emoji (4-byte), empty name, empty value,
duplicate names kept in order, and a lone-surrogate rejection. Verify the vectors are copied to the output directory
(check how `tests/vectors/http/*.json` is wired in the test `.csproj`; add `body/` the same way).

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/FormUrlEncoderTests.cs` (`FormUrlEncoderTests`,
`Unit`): `Every_vector_case_encodes_to_its_wire_bytes` (`[Theory]` over `VectorFile.Load<FormCase>("body/form-urlencoded.json")`),
`A_lone_surrogate_throws_ArgumentException_naming_the_field_index_never_the_value` (A7/P3b-7),
`A_null_name_or_value_throws_ArgumentNullException`, `Order_and_duplicates_are_kept`,
`Unreserved_star_dash_dot_underscore_are_literal_and_every_other_byte_is_uppercase_hex`. Red: CS0103 (`FormUrlEncoder`).

**Production.** New `src/Dexpace.Sdk.Core/Http/Request/FormUrlEncoder.cs`, `internal static class FormUrlEncoder`:
`internal static byte[] Encode(IEnumerable<KeyValuePair<string, string>> fields)`. Strict `UTF8Encoding(false, true)`
(catch `EncoderFallbackException` and rethrow `ArgumentException` naming the index); bytes `A-Z a-z 0-9 * - . _` literal,
`0x20` → `+`, else `%XX` uppercase. Writes into an `ArrayBufferWriter<byte>` or `ValueStringBuilder`-free span loop;
helper methods keep each under 70 lines. **PublicAPI:** none. **IDs:** `HTTP-38` (form half).
**Verify:** V-fast `FormUrlEncoderTests`.

### Task 2.2 — `RequestBody.FromForm` (`HTTP-36`, `HTTP-38`, `BODY-1`, `BODY-3`; P3b-7, P3b-14)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/FormBodyTests.cs` (`FormBodyTests`, `Unit`):
`FromForm_is_replayable_with_the_exact_length`, `The_content_type_is_application_x_www_form_urlencoded_with_no_charset`,
`Two_writes_are_byte_identical`, `ToReplayableAsync_returns_the_same_instance`, `Equal_fields_give_equal_bodies_and_hash_in_O1`
(it is the in-memory variant, so 2a's value equality applies), `A_Dictionary_and_a_list_of_pairs_are_both_accepted`,
`A_null_fields_argument_throws_ArgumentNullException`, and the sink-failure ports `A_throwing_destination_propagates_for_FromBytes`,
`…_for_FromString` and `…_for_FromForm`. The file header comment cites `nodejs-sdk@c0ff3fd`
`packages/core/src/body/simple-bodies.test.ts` (form cases and sink-failure cases) and notes the excluded `freeze` cases
(constraint 10). Red: CS0117, no `FromForm`.

**Production.** `RequestBody.cs`: `public static RequestBody FromForm(IEnumerable<KeyValuePair<string, string>> fields)`
`=> new BytesRequestBody(FormUrlEncoder.Encode(fields), CommonMediaTypes.ApplicationFormUrlEncoded)` (confirm the constant
and its lack of a charset parameter in `CommonMediaTypes`; if it carries one, build the type with `MediaType.Of`). XML doc
with an example and the WHATWG note. **PublicAPI:**

```text
static [Q].RequestBody.FromForm(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string!, string!>>! fields) -> [Q].RequestBody!
```

**IDs:** `HTTP-36`/`HTTP-37`/`HTTP-38`/`BODY-1`/`BODY-3` (form half). **Verify:** V-fast `FormBodyTests`.

### Task 2.3 — `FromStream` validation and the seekable variant (`BODY-9`, `BODY-35`; P3b-4; **Breaking 7**)

**Gate:** 3a's exact copy and `WriteTo`. **Failing tests first.** New
`tests/Dexpace.Sdk.Core.Tests/Http/Request/SeekableStreamBodyTests.cs` (`SeekableStreamBodyTests`, `Unit`):

- `A_seekable_stream_with_a_declared_length_is_replayable_with_that_length`
- `Every_write_seeks_to_the_start_position_captured_at_construction` (a stream constructed at position 3, moved by the
  caller to 0 and to the end before the write, still delivers bytes `[3, 3+n)` each time, first write included)
- `Two_writes_are_byte_identical`
- `A_concurrent_second_write_throws_InvalidOperationException` (a gate stream parks the first write; the second throws
  before touching the stream, so at most one rewind happens between two writes)
- `An_unknown_length_stays_single_use` (−1 with a seekable stream; `IsReplayable` false; second write
  `StreamConsumedException`; the length is **not** inferred from `Length − Position`)
- `A_non_seekable_stream_with_a_length_stays_single_use`
- `A_short_seekable_source_fails` (declared 10, source has 4: 3a's `EndOfStreamException`; consumer test for `HTTP-39`/`BODY-10`)
- `The_callers_stream_stays_open_after_write_failure_and_ToReplayableAsync` (`BODY-8` ownership)
- `The_length_must_be_in_0_to_Array_MaxLength` (`long.MaxValue` seekable → single-use rather than replayable)
- `Equality_is_identity` (two bodies over one stream are unequal; a body equals itself)

Extend (new file) `tests/Dexpace.Sdk.Core.Tests/Http/Request/RequestBodyContractTests.cs` (`RequestBodyContractTests`, `Unit`,
`HTTP-36`/`BODY-1`/`BODY-35`): a `[Theory]` over every factory (`FromBytes`, `FromString`, `FromValue`, `FromForm`,
`FromStream` single-use and seekable, and — as their PRs land — `FromFile` and `Multipart`) asserting nullable
`ContentType`, `ContentLength` (exact or −1), `IsReplayable` default and, for replayable ones, byte-identical double writes
and `ToReplayableAsync` returning `this` (`BODY-3`); `FromStream_rejects_contentLength_below_minus_one`
(`ArgumentOutOfRangeException`, `ParamName` `contentLength`) and `FromStream_rejects_a_non_readable_source`
(`ArgumentException`), each skipped (with a one-line note) only if 3a already added it.

Red: the seekable variant does not exist (the stream is single-use); validation tests fail by not throwing.

**Production.** `RequestBody.cs`: `FromStream` validates (`contentLength >= -1`, `source.CanRead`), then dispatches: seekable
variant when `source.CanRead && source.CanSeek && contentLength is >= 0 and <= Array.MaxLength`, else the existing
`StreamRequestBody` (with 3a's exact-copy wiring if 3a added it). New private `SeekableStreamRequestBody(Stream source,
MediaType? contentType, long contentLength)` with `_start = source.Position` captured at construction, an `int _inFlight`
`Interlocked` flag, `IsReplayable => true`, and `WriteToAsync`/`WriteTo` that: check the destination, acquire the flag
(`InvalidOperationException` "a seekable-stream body cannot be written concurrently" on contention), `source.Seek(_start)`,
run 3a's exact copy of `contentLength` bytes, release the flag in `finally`. It never disposes the source (`BODY-8`). A6:
no `Length` check at construction. Remarks and the `FromStream` doc carry the **Breaking** paragraph (item 7) and the
documented fact that the caller's position is moved. **PublicAPI:** none (signature unchanged).
**IDs:** `BODY-9`, `BODY-35`, `HTTP-38` (stream half), `BODY-1`. **Verify:** V-fast `SeekableStreamBodyTests`,
`RequestBodyContractTests`.

### Task 2.4 — `SingleUseBodyTests` and the stream half of `RequestBodyOwnershipTests` (`HTTP-37`, `BODY-3`, `BODY-6`, `BODY-7`, `BODY-8`)

All **pins** (convention 1: each proven able to fail by temporarily removing the `Interlocked` guard or the `leaveOpen`
behaviour). New `tests/Dexpace.Sdk.Core.Tests/Http/Request/SingleUseBodyTests.cs` (`SingleUseBodyTests`, `Unit`):
`A_second_write_throws_StreamConsumedException`, `Concurrent_writes_admit_exactly_one` (`Barrier`-started, N = 16),
`ToReplayableAsync_on_a_single_use_body_leaves_the_original_consumed`, `ToReplayableAsync_on_a_replayable_body_returns_this`.
Add to `SingleUseBodyTests` the five `materialize.test.ts` ports, with a file header citing `nodejs-sdk@c0ff3fd`
`packages/core/src/body/materialize.test.ts` and `stream-body.test.ts` (ownership, single use, concurrency):
`ToReplayableAsync_preserves_the_media_type`, `The_materialised_body_writes_twice_identically`,
`ToReplayableAsync_on_a_replayable_body_returns_this` (already above), `ToReplayableAsync_drains_the_source_once` and
`Under_N_concurrent_ToReplayableAsync_callers_exactly_one_drains_and_the_rest_see_StreamConsumedException` (`Barrier`, N = 16).
New `RequestBodyOwnershipTests.cs` (`RequestBodyOwnershipTests`, `Unit`): `The_callers_stream_is_open_after_a_write_a_failed_write_and_ToReplayableAsync`
(single-use and seekable, over the `DisposalTrackingStream` created in task 1.2). The file half is added in task 3.2 and the multipart half in 4.4.
**Production:** none. **IDs:** `HTTP-37`, `BODY-6`, `BODY-7`, `BODY-8` (stream half). **Verify:** V-fast both classes.

### Task 2.5 — Close-out (PR 2)

`CHANGELOG.md`: `### Added` `RequestBody.FromForm`; `### Changed` **Breaking:** `FromStream` over a readable seekable stream
with a declared length is replayable and seeks before each write; `contentLength < -1` and a non-readable source are rejected.
**V-gate.** **Commits:** `feat: RequestBody.FromForm with the WHATWG serializer` (2.1, 2.2), `feat!: seekable known-length stream bodies are replayable`
(2.3), `test: single-use and ownership pins for request bodies` (2.4).

---

## PR 3 — File body

**Gate:** 3a's exact copy and `WriteTo`. Additive. Rows: `HTTP-40`, `BODY-8` (file half), `BODY-11`–`BODY-13`; consumer
tests for `HTTP-39`/`BODY-10`.

### Task 3.1 — `FileRequestBody` and `FromFile` (`HTTP-40`, `BODY-11`, `BODY-12`, `BODY-13`; P3b-5, P3b-6, P3b-14)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/FileRequestBodyTests.cs` (`FileRequestBodyTests`,
`Unit`; temp files under a per-test directory deleted in `Dispose`; header comment cites `nodejs-sdk@c0ff3fd`,
`packages/body-file/src/file-body.test.ts`; `[SkippableFact]`-style guards are not needed, the symlink test skips itself
with a runtime check where the OS cannot create links). Ported cases: `Is_recognisable_by_type_and_exposes_its_range`
(`BODY-12`: `FilePath` is the resolved full path, `Offset`, `ContentLength`), `Is_replayable_and_two_writes_are_byte_identical`,
`A_missing_path_throws_FileNotFoundException`, `A_directory_throws_ArgumentException`, `A_negative_offset_is_rejected`,
`An_offset_or_count_past_the_size_is_rejected` (`ArgumentOutOfRangeException`), `The_destination_is_not_closed`,
`The_exact_range_is_written`, `Count_minus_one_means_the_rest_of_the_file`, `Count_zero_writes_nothing_and_succeeds`,
`A_fresh_handle_is_opened_per_write`, `A_read_error_propagates`, `A_write_error_propagates`. Added here:
`A_symbolic_link_is_resolved_and_its_target_size_is_used` (fact 3: the link's own length 7 is not used; target length 6),
`A_file_that_shrinks_after_construction_fails_naming_transferred_of_total` (fact 4; `EndOfStreamException`, the consumer
test for `HTTP-39`/`BODY-10`/`BODY-13`), `Count_zero_on_a_zero_size_file_never_opens_it` (P3b-5; the file is replaced by a
locked or missing file after construction and the write still succeeds with zero bytes), `The_handle_is_released_after_each_write_and_after_a_failed_write`
(`BODY-8`: the test reopens the file with `FileShare.None` immediately after), `Growth_past_the_count_is_ignored`
(P3b-6), `The_file_may_be_open_for_writing_elsewhere` (the permissive `FileShare`), `The_content_type_is_never_guessed`
(P3b-14: null when none is given). Red: CS0117 (`FromFile`).

**Production.** New `src/Dexpace.Sdk.Core/Http/Request/FileRequestBody.cs` (`public sealed class FileRequestBody : RequestBody`,
internal constructor, signatures per the design's type shapes). The static factory `RequestBody.FromFile(string path,
MediaType? contentType = null, long offset = 0, long count = -1)` does the fail-fast work: `ArgumentException.ThrowIfNullOrEmpty`,
`Path.GetFullPath`, resolve a symbolic link with `new FileInfo(full).ResolveLinkTarget(returnFinalTarget: true)` (fact 3; the
resolved target's `Length` is the size, but `FilePath` stays the path the caller gave resolved to full, and the open goes
through that path so the OS follows the link), missing → `FileNotFoundException`, `FileAttributes.Directory` or `Device`
→ `ArgumentException` (fact 1), range validation (`offset` in `[0, size]`, `count` in `[-1, size - offset]`; −1 resolves to
`size - offset`) → `ArgumentOutOfRangeException`. Size captured once at construction. The write: if the resolved count is 0,
return without opening (P3b-5); otherwise open
`new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 0, FileOptions.SequentialScan | (async ? FileOptions.Asynchronous : FileOptions.None))`
inside `using`/`await using` (**`CA2000`** must be satisfied by the scope, not suppressed), seek to `Offset`, run 3a's exact
copy of the count, dispose. Async and sync writes share one core with `bool async` (§11 item 12) split under 70 lines.
Equality: identity (the type is `sealed`, no override). Remarks: the "not zero-copy" statement (`BODY-12`), the FIFO/device
residue (P3b-5, **open for the lead**: documented as a known domain limit; the code does **not** try to detect special
files), and the rewrite-between-writes caveat (P3b-6).

**PublicAPI:**

```text
[Q].FileRequestBody
[Q].FileRequestBody.ContentLength.get -> long
[Q].FileRequestBody.ContentType.get -> [C].MediaType?
[Q].FileRequestBody.FilePath.get -> string!
[Q].FileRequestBody.IsReplayable.get -> bool
[Q].FileRequestBody.Offset.get -> long
[Q].FileRequestBody.WriteToAsync(System.IO.Stream! destination, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> [T].Task!
[Q].FileRequestBody.WriteTo(System.IO.Stream! destination, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) -> void
static [Q].RequestBody.FromFile(string! path, [C].MediaType? contentType = null, long offset = 0, long count = -1) -> [Q].FileRequestBody!
```

(the synchronous line is whatever 3a's `WriteTo` signature is). **IDs:** `HTTP-40`, `BODY-11`, `BODY-12`, `BODY-13`.
**Verify:** V-fast `FileRequestBodyTests`.

### Task 3.2 — Ownership (file half) and the contract theory (`BODY-8`, `HTTP-36`, `BODY-1`)

Extend `RequestBodyOwnershipTests` with the file-handle rows already listed in 3.1 (move them here if the 3.1 commit is
large) and add `FromFile` to `RequestBodyContractTests`' theory. **Production:** none. **IDs:** `BODY-8` (file half),
`HTTP-36`. **Verify:** V-fast both.

### Task 3.3 — Close-out (PR 3)

`CHANGELOG.md` `### Added`: `RequestBody.FromFile`, `FileRequestBody`. `docs/first-release.md` is **not** touched here (the
P3b-5 residue is routed in task 7.4 once the lead decides). **V-gate.** **Commit:** `feat: file-backed request bodies`.

---

## PR 4 — Multipart

**Gate:** 3a's synchronous `WriteTo`. Additive. Rows: `HTTP-51`, `BODY-2`, `BODY-6` (composite).

### Task 4.1 — `MultipartPart` and `MultipartFraming` (`HTTP-51`; P3b-8)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/MultipartPartTests.cs` (`MultipartPartTests`, `Unit`):
`Name_and_FileName_reject_CR_LF_and_other_C0_controls_and_DEL_naming_the_code_point_never_the_value`,
`Quote_and_backslash_are_backslash_escaped_in_the_header`, `Non_ASCII_is_written_as_UTF8`, `A_null_name_or_body_throws`,
`An_empty_Name_is_allowed`, `The_part_is_a_sealed_class_with_a_validating_constructor` (reflection: not a record, sealed).
New `MultipartFramingTests.cs` (`MultipartFramingTests`): `A_generated_boundary_is_dexpace_plus_32_alphanumerics`,
`Two_generated_boundaries_differ`, `A_caller_boundary_must_match_RFC_2046_1_to_70_bchars_not_ending_in_space`
(theory: valid and invalid), `A_boundary_with_a_comma_is_sent_as_a_quoted_string` (through `MediaType.Of`),
`The_part_header_bytes_are_computed_once_and_reused` (the framing object exposes stored arrays; the same array instance is
returned for length and write). Red: CS0246.

**Architecture test, same commit.** `ModelConstructionArchitectureTests.Public_constructors_in_Http_namespaces_are_exactly_the_allow_list`
fails on any public constructor under `Dexpace.Sdk.Core.Http.*` that is not on `s_allowedConstructors` (HTTP-2/SEAM-29). Add
`Dexpace.Sdk.Core.Http.Request.MultipartPart(System.String, Dexpace.Sdk.Core.Http.Request.RequestBody, System.String)` with the
P3b-8 rationale (a validating constructor, not a record; the 2a `Request` precedent). Task 7.3's changed-assertions table
and task 7.4's §4.5 dated correction record it as the new construction route.

**Production.** New `src/Dexpace.Sdk.Core/Http/Request/MultipartPart.cs` (public sealed class, design shape; `Name` and
`FileName` validated with `HttpHeaderSyntax`'s code-point formatter) and `MultipartFraming.cs` (`internal static class`):
boundary generation (`RandomNumberGenerator.GetString` over `[A-Za-z0-9]`, fact 8), boundary validation, and
`Frame(IReadOnlyList<MultipartPart>, string boundary)` returning a small internal record of per-part header `byte[]`s and
the trailer `byte[]`. The part `Content-Type` is `part.Body.ContentType?.ToString()` (omitted when null). **PublicAPI:**

```text
[Q].MultipartPart
[Q].MultipartPart.Body.get -> [Q].RequestBody!
[Q].MultipartPart.FileName.get -> string?
[Q].MultipartPart.MultipartPart(string! name, [Q].RequestBody! body, string? fileName = null) -> void
[Q].MultipartPart.Name.get -> string!
```

**IDs:** `HTTP-51`. **Verify:** V-fast the two classes and `ModelConstructionArchitectureTests`.

### Task 4.2 — `BoundedWriteStream` (`HTTP-51`; A8)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/BoundedWriteStreamTests.cs`: `A_chunk_that_would_exceed_the_known_length_is_refused_before_it_is_written`,
`The_total_is_checked_at_the_end_and_a_short_total_fails`, `An_unknown_length_never_refuses`, `Dispose_does_not_dispose_the_primary`,
`Flush_forwards_to_the_primary`, `Seek_and_Read_are_unsupported`. **Production.** New
`src/Dexpace.Sdk.Core/Http/Request/BoundedWriteStream.cs` (`internal sealed class : Stream`, write-only, a `long` count,
sync and async `Write` overrides, `Dispose(bool)` leaves the primary alone). **PublicAPI:** none. **Verify:** V-fast.

### Task 4.3 — `RequestBody.Multipart` and the composite (`HTTP-51`, `BODY-2`, `BODY-6`; P3b-8)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/MultipartBodyTests.cs` (`MultipartBodyTests`, `Unit`;
header comment cites `nodejs-sdk@c0ff3fd` `packages/core/src/body/multipart-body.test.ts`; every case except the builder
cases `newBuilder`/`parts`, §10 entry 10): `The_wire_bytes_match_a_hand_written_frame` (boundary pinned by the caller),
`The_content_type_is_multipart_form_data_with_the_boundary_parameter`, `ContentLength_equals_the_bytes_actually_written`,
`ContentLength_is_minus_one_when_any_part_is_unknown` (`BODY-2`), `IsReplayable_is_the_conjunction_over_the_parts` (`BODY-2`),
`A_replayable_composite_writes_identical_bytes_twice_and_admits_concurrent_writes`,
`A_single_use_composite_refuses_a_second_write_before_any_byte` (`BODY-6`), `A_part_whose_own_length_lies_is_caught_by_the_bounded_writer`
(too long: refused before the chunk; too short: failure at the end), `Part_headers_cannot_break_the_framing` (the CR/LF
case is **inverted** to a rejection, P3b-8; the construction throws), `An_empty_part_list_is_rejected` (added),
`Nothing_the_composite_was_given_is_disposed` (parts' bodies and the destination stay open, `BODY-8`),
`The_destination_is_not_closed`, `A_failure_in_a_part_propagates_and_stops_the_write`, `Equality_is_identity`,
`A_nested_multipart_part_frames_correctly`. Red: CS0117.

**Production.** `RequestBody.cs`: `public static RequestBody Multipart(IEnumerable<MultipartPart> parts, string? boundary = null)`
(copies the list, empty → `ArgumentException`, builds the framing once). New `MultipartRequestBody.cs`
(`internal sealed`, or private nested per the design's "private sealed" note; a separate internal file keeps `RequestBody.cs`
readable): stores the framing arrays, `ContentLength` is `-1` if any part is `-1` else the sum of stored header bytes, part
lengths and the trailer; `IsReplayable` is the conjunction; the write goes through `BoundedWriteStream`, writes each stored
header array then `part.Body.WriteToAsync` (sync path: `WriteTo`) then the CRLF, then the trailer, and checks the total.
A non-replayable composite carries an `Interlocked` consume-once guard (a second write throws `StreamConsumedException` before
any byte); a replayable one has no guard. Split into helpers under 70 lines. **PublicAPI:**

```text
static [Q].RequestBody.Multipart(System.Collections.Generic.IEnumerable<[Q].MultipartPart!>! parts, string? boundary = null) -> [Q].RequestBody!
```

**IDs:** `HTTP-51`, `BODY-2`, `BODY-6`. **Verify:** V-fast `MultipartBodyTests`.

### Task 4.4 — Contract and ownership rows (`HTTP-36`, `BODY-1`, `BODY-8`)

Add `Multipart` to `RequestBodyContractTests`' theory and the multipart half to `RequestBodyOwnershipTests`. **Production:**
none. **Verify:** V-fast both.

### Task 4.5 — Close-out (PR 4)

`CHANGELOG.md` `### Added`: `RequestBody.Multipart`, `MultipartPart`. **V-gate.** **Commit:** `feat: multipart request bodies`.

---

## PR 5 — `LoggingRequestBody`

**Gate:** 3a's `TeeStream` and tap. Internal only. Rows: `BODY-17`–`BODY-21`, `BODY-32` (request half), `BODY-34`
(consumer clause), `BODY-37`.

### Task 5.1 — `LoggingRequestBody` (`BODY-17`–`BODY-21`, `BODY-32`, `BODY-37`; P3b-9)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Request/LoggingRequestBodyTests.cs` (`LoggingRequestBodyTests`,
`Unit`; header comment cites `nodejs-sdk@c0ff3fd` `packages/core/src/body/request-body-logging.test.ts`, excluding the
sink-abort/close cases, a host fact, and "teardown is close() only"): `The_wire_receives_every_byte_and_the_tap_mirrors_up_to_the_cap`
(`BODY-17`), `The_tap_reflects_only_the_latest_attempt` (`BODY-18`), `A_multi_megabyte_write_mirrors_only_the_cap` and
`A_cap_of_zero_mirrors_nothing_and_forwards_everything` (`BODY-19`), `A_primary_failure_leaves_the_failing_chunk_captured`
(`BODY-20`), `IsReplayable_is_the_delegates` and `Materialising_rewraps_with_the_cap_and_a_separate_tap` (`BODY-21`; a
replayable delegate returns `this`), `Snapshots_are_copies` (`BODY-37`), `Snapshot_with_a_smaller_max_returns_a_prefix`,
`Negative_caps_are_rejected` (constructor and `Snapshot(int)`; `ArgumentOutOfRangeException`) and
`Caps_above_the_array_bound_are_clamped` (`BODY-32`), `The_destination_is_left_open` (`leaveOpen`, `IO-6`),
`ContentType_and_ContentLength_forward_to_the_delegate`, `Equality_is_identity`, and the property theory
`For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded` (`BODY-34`, consumer clause; a
`[Theory]` over (cap, length) pairs including 0, 1, cap−1, cap, cap+1). New architecture test
`tests/Dexpace.Sdk.Core.Tests/Architecture/LoggingWrapperSurfaceTests.cs`: `No_member_of_either_wrapper_returns_Stream_Memory_or_byte_array_other_than_Snapshot`
(`BODY-37`; reflection over `NonPublic | Public | Instance` declared members; the response wrapper joins this test in task 6.5).
Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Http/Request/LoggingRequestBody.cs`, `internal sealed class LoggingRequestBody : RequestBody`
(signatures per the design). Cap clamped to `Array.MaxLength`, negative rejected. Each write: clear the tap, wrap the
destination in 3a's `TeeStream` (`leaveOpen: true`) with a tap capped at `tapCap`, call `inner.WriteToAsync` (or `WriteTo`)
into the tee, and leave the tap readable afterwards. `Snapshot()` and `Snapshot(int)` return copies. `ToReplayableAsync`:
`inner.IsReplayable ? this : new LoggingRequestBody(await inner.ToReplayableAsync(ct), tapCap)`. Remarks note that two
concurrent writes of one wrapped replayable body would interleave the preview (P3b-9) and that 4b/5b engage the wrapper.
**PublicAPI:** none. **IDs:** as above. **Verify:** V-fast both classes.

### Task 5.2 — Contract row and close-out (PR 5)

`RequestBodyContractTests` gains the wrapper over each replayable factory (internal access). `CHANGELOG.md`: no line (internal
only; the checklist records it). **V-gate.** **Commit:** `feat: internal request-body logging wrapper`.

---

## PR 6 — `LoggingResponseBody`

**Gate:** 3a's synchronous `OpenRead`; PR 1 (`Disposal`, the latch). Internal only. Rows: `BODY-22`–`BODY-29`, `BODY-32`
(response half), `BODY-34` (consumer clause).

### Task 6.1 — `PrefixedReadStream` (`BODY-24`, `BODY-25`, `BODY-27`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/PrefixedReadStreamTests.cs`:
`Reads_the_prefix_then_the_live_tail`, `Never_issues_a_zero_count_read` (a probe tail stream throwing on `count == 0`; a
caller asking for 0 bytes gets 0 without touching it), `Dispose_routes_to_the_shared_close_once_guard` (a delegate counting
invocations), `Read_after_dispose_throws_ObjectDisposedException`, `Seek_and_Length_are_unsupported`, `Sync_and_async_reads_agree`.
**Production.** New `src/Dexpace.Sdk.Core/Http/Response/PrefixedReadStream.cs` (`internal sealed class : Stream`, constructor
takes the prefix `byte[]`, a count, the tail `Stream` and an `Action`/`Func<ValueTask>` close callback; debug assertions for the
zero-count rule). **PublicAPI:** none. **Verify:** V-fast.

### Task 6.2 — The drain core (`BODY-22`, `BODY-25`, `BODY-26`; P3b-10)

**Failing tests first**, in `tests/Dexpace.Sdk.Core.Tests/Http/Response/LoggingResponseBodyTests.cs` (`LoggingResponseBodyTests`,
`Unit`; header comment cites `nodejs-sdk@c0ff3fd` `packages/core/src/body/response-body-logging.test.ts`, excluding the
`WritableStream` abort cases): `The_first_read_triggers_exactly_one_upstream_drain_under_16_concurrent_readers` (a counting
delegate, `Barrier`-started; `BODY-22`), `A_snapshot_triggers_the_drain`, `The_failure_query_never_starts_a_drain`
(`BODY-26`), `A_drain_failure_is_cached_and_the_partial_bytes_kept` (every later read rethrows the same captured
exception via `ExceptionDispatchInfo`; the snapshot returns the partial bytes without throwing), `Never_issues_a_zero_count_read`
(`BODY-25`: a probe stream throwing on `count == 0`), `The_drain_is_cancelled_by_the_starting_accessors_token_and_the_failure_is_cached`
(P3b-10: the cancellation is cached as `OperationCanceledException`; later reads rethrow it), `Dispose_cancels_a_running_drain`
(the lifetime token), `The_semaphore_wait_honours_the_callers_token` (a second accessor's token cancels its wait, not the
drain). Red: CS0246.

**Production.** New `src/Dexpace.Sdk.Core/Http/Response/LoggingResponseBody.cs`
(`internal sealed class LoggingResponseBody : ResponseBody`, signatures per the design). State: `SemaphoreSlim(1, 1)`, a
`volatile` captured `byte[]`/count, `ExceptionDispatchInfo? _failure`, `bool _fullyCaptured`, a `CancellationTokenSource`
for the lifetime. The drain is **one `bool async` core** (§11 item 12) split into acquire (`Wait`/`WaitAsync` with the
caller's token), drain-or-return-cached, release, each under 70 lines; the drain token is `CancellationTokenSource
.CreateLinkedTokenSource(starterToken, lifetimeToken)` (P3b-10). The drain reads in chunks bounded by `captureCap`, never
issuing a zero-count read, and stops at `captureCap + 1` bytes to learn whether the body overflows (regime decided in 6.3).
This task lands the drain, the failure cache and the concurrency; `OpenReadAsync` is added in 6.3 (the abstract member is
implemented by a stub that throws `NotImplementedException` for the duration of the commit **only if** the commit must be
green on its own; prefer folding 6.2 and 6.3 into one commit and keeping two test classes). **Verify:** V-fast.

### Task 6.3 — Regimes, lifecycle, caps (`BODY-23`, `BODY-24`, `BODY-27`, `BODY-28`, `BODY-29`, `BODY-32`)

**Failing tests first**, same class: `Fits_cap_every_read_is_an_independent_view` (`BODY-23`: each read a fresh read-only
`MemoryStream` over the captured array; the delegate is disposed once), `Over_cap_the_consumer_receives_every_byte_once`
(`BODY-24`, `BODY-34`), `Over_cap_a_second_read_throws_StreamConsumedException`,
`The_delegate_is_closed_at_most_once_across_every_path` (`BODY-27`: wrapper dispose and tail-stream dispose share one
`Interlocked` guard; a throwing delegate dispose still flips it and propagates once), `A_close_failure_after_full_capture_is_reported_not_raised`
(`BODY-28`: through `Disposal.DisposeQuietly`, an `exception` event is observable, the drain error stays empty),
`Snapshot_survives_dispose`, `Content_length_is_the_captured_size_only_after_full_capture` (`BODY-29`, A9: before capture the
delegate's value), `Negative_caps_are_rejected` and `Caps_above_the_array_bound_are_clamped` (`BODY-32`, constructor and
`SnapshotAsync(int, …)`; the capless clause is vacuous, design position F, and is **not** tested), `Snapshot_returns_what_exists_up_to_the_cap`,
`A_snapshot_of_an_over_cap_body_returns_exactly_the_cap`, property theory `For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded`
(`BODY-34`, consumer clause; (cap, length) pairs 0, 1, cap−1, cap, cap+1), `IsFullyCaptured_reflects_the_regime`,
`ContentType_forwards_to_the_delegate`. **Production.** `LoggingResponseBody.OpenReadAsync` (and 3a's sync `OpenRead`):
trigger the drain, rethrow the cached failure, then serve per regime (fits-cap: dispose the delegate quietly via
`Disposal.DisposeQuietly`, return a fresh view; over-cap: single-use `PrefixedReadStream` over the captured prefix and the
live delegate stream, whose dispose goes through the same close-once guard); `Dispose(bool)`/`DisposeAsyncCore` close the
delegate once (the guard), cancel the lifetime token and dispose the semaphore only after any running drain exits.
`ContentLength` per A9. **PublicAPI:** none. **Verify:** V-fast.

### Task 6.4 — Synchronous half (`BODY-22`, A10)

**Failing tests first.** `Sync_OpenRead_drains_once_and_serves_the_same_bytes` and `A_sync_and_an_async_first_reader_race_to_one_drain`
(`Barrier`; one upstream read) in `LoggingResponseBodyTests`. **Production.** The sync entry points call the one core with
`async: false` (the semaphore `Wait`, not `WaitAsync`; no blocking on a task, styleguide 9.1). **Verify:** V-fast.

### Task 6.5 — Surface architecture row (`BODY-37`) and close-out (PR 6)

Extend `LoggingWrapperSurfaceTests` to `LoggingResponseBody` (members returning `Stream`, `Memory<byte>` or `byte[]` are only
the snapshot copies and `OpenReadAsync`/`OpenRead` which the base type requires: the assertion allows the base-type overrides
by name and says why). `CHANGELOG.md`: no line. **V-gate.** **Commit:** `feat: internal response-body logging wrapper`.

---

## PR 7 — Close-out

**Gate:** PRs 1 to 6 merged. The docs close the phase (roadmap step 7). Rows: all 49 (closing).

### Task 7.1 — NativeAOT smoke

Extend `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (`RunAllAsync` calls one more check per item, in its existing style with its
`Expect` helper): `FromFile` over a temp file written in the binary and written to a `MemoryStream`; `FromForm` against the
vector line for fact 6; `Multipart` with two parts and a pinned boundary; a `Response` disposed twice (sync and async) over a
`DisposalCountingBody`-style local body. No reflection. If publish emits a trim/AOT warning, fix the source, not the smoke.
**Verify:** the publish-and-run (expected "aot-smoke: all checks passed"). **IDs:** closing evidence for `HTTP-40`, `HTTP-38`,
`HTTP-51`, `HTTP-43`.

### Task 7.2 — User documentation

New `docs/sdk-documentation/bodies.md`, opening "As built by phase 3b … written against source on <date>" (the roadmap's
exit criterion). Content: the body variants and how each is constructed; replayability and the seekable rule; ownership
(design position A's table); the response-body dispose contract, the single-use read and `StreamConsumedException`/`StreamClosedException`;
the BOM rule; the file body's domain limit; the WHATWG form encoder and its difference from Node; the multipart rules; the
seven-item breaking-change migration table with before/after; the two logging wrappers described as internal. Cite IDs; do not
copy design §3. Update `src/Dexpace.Sdk.Core/README.md`'s bodies row, `docs/architecture.md`'s bodies paragraph,
`CLAUDE.md`'s "Single-use bodies" bullet (seekable known-length streams are replayable) and its "What is genuinely unbuilt"
paragraph (drop file, form-urlencoded and multipart bodies, the logging body wrappers and the dispose latches from item 3's
list; keep the rest of 3), and `docs/README.md`'s ownership table row. **Verify:** the probe's `links` check.

### Task 7.3 — The checklist

Write `docs/work/mvp/phase3/phase3b/2026-10-0X-phase3b-bodies-checklist.md` from what was built (one row per requirement ID,
the 2b checklist's legend and layout: test file, class, category, and the "existing assertions changed" table:
`TrackingBody`'s override only, the `DisposalCountingBody` count doc, the rewritten
`DisposalCountingBodyTests.Dispose_and_DisposeAsync_release_once` (2 became 1, renamed), the
`ModelImmutabilityArchitectureTests` allow-list entry for `Response._disposed` (HTTP-43, P3b-2, a reviewed decision), the
`ModelConstructionArchitectureTests` allow-list entry for the `MultipartPart` constructor (P3b-8), the replaced
`SyncToAsyncBridgeTests` pin, each `BodyConvenienceTests` adjustment if any). Rows per the design's table with these marks: ✅ for the 36 built or already-met
rows; ✅ with a clause for `HTTP-52`, `BODY-30`, `BODY-31`, `BODY-34` (⏳ 5b), `BODY-12` (🚫 kernel path, §3.1) and
`BODY-37` (tee half ⏳ 3a `IO-28`); ⏳ for `HTTP-39`/`BODY-10` (3a), `HTTP-44`/`HTTP-45` (7a), `BODY-4` (6a/6b/6c), `BODY-5` (6a)
and `BODY-36` (`docs/first-release.md`); `BODY-32`'s capless clause named **vacuous**. The totals are **recounted against the
built code**, never copied from the design. Each `Security` class listed as "unedited" except `EnsureSuccessErrorMappingTests`
(mechanical `TrackingBody` rewrite) with the diff output as evidence. Record the red evidence honestly, the way the 2b
checklist did (which tests were seen red, which are pins).

### Task 7.4 — Dated corrections, hand-offs and routed entries

Frozen documents change only by dated correction (design Migration and Deviation Ledger). In `docs/sdk-design-dotnet/`:

- **§3.1** (P3b-4 seekable promotion; P3b-10 linked drain token; the variants' "As built"), **§3.3** and **§11 item 43** (P3b-16,
  "until phase 3b" removed), **§3.7** (`Disposal`'s `ILogger?` parameter, its interim status, the 4b/5b repoints), **§4.5**
  (`FromFile`/`FromForm`/`Multipart` shapes and the new `MultipartPart` public constructor, added to the construction allow-list), **§10 entries 5 and 6** (the verdicts extended to the 3b variants, P3b-1),
  **§11 item 34** (P3b-12), **§12** (`BODY-33` addressed; `BODY-32`'s vacuous clause; `BODY-36` deferred).
- **P3b-5** (the Unix special-file residue): **routed as the lead decides** (a §10 entry, or a §11 item). The plan does not
  decide; until told, the checklist row reads ✅ with the stated clause and the docs mention it. **P3b-3's no-listener gap**
  and **P3b-10's departure** are likewise recorded as the lead's call (the design marks all three open).
- The 2b checklist's `SEAM-14` row gets a dated correction pointing at `SystemNetHttpClientDisposeTests` (⏳ 3b → ✅), and the
  phase-1 checklist's row S9 a dated note that its 3b clause is closed by `ResponseDisposeLatchTests` and
  `HttpResponseMessageBodyTests`.
- `docs/first-release.md`: a "SHOULD- and MAY-level requirements declined for v1" bullet for `BODY-36`.
- If the implementation found anything the knowledge corpus should hold (for example, fact 6's three-encoder disagreement or
  fact 1's FIFO behaviour), record it as a note under `docs/knowledge/notes/`, never editing `harvested/`.
- Append a dated Phase Status Note for 3b to the roadmap: what landed, the PRs, the rulings, the open items, and the
  hand-offs to 4b, 4c, 5b, 6a/6b/6c, 7a, 8b (design "Coupling").

### Task 7.5 — Hand-off notes recorded for later plans

In the status note, list the coupling the design assigns: 4b repoints `Disposal`; 4c moves `EnsureSuccessAsync`'s
`finally` onto `Disposal.DisposeQuietlyAsync(this, primary)`; 5b engages the wrappers, shares one preview size and plumbs
`ILogger`; 6a/6b/6c add replay tests for the now-replayable seekable streams; 7a decides whether `ReadValueAsync` closes the
body; 8b writes the synchronous `SerializeToStream` over `WriteTo` and decides on a `Disposal` equivalent.

### Task 7.6 — Close-out (PR 7)

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/bodies.md`; the AOT smoke covers the body surface." Run **V-gate** and
the coverage gate with its self-test. Move the superpowers inbox files and run the probe until clean:

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 3b            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 3b --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

**Commits:** `test: NativeAOT smoke over the body surface`, then `docs: phase 3b checklist, user page and dated corrections`.

---

## Traceability: ID → PR → task

| IDs | PR | Task(s) | Mark (design) |
|---|---|---|---|
| `HTTP-36` | 2–4 | 2.2, 2.3, 3.2, 4.4, 5.2 (`RequestBodyContractTests`) | ✅ |
| `HTTP-37`, `BODY-7` | 2 | 2.4 | ✅ |
| `HTTP-38` | 2–4 | 2.1, 2.2, 2.3, 3.1, 4.3 | ✅ |
| `HTTP-39`, `BODY-10` | 3a | consumer tests in 2.3, 3.1 | ⏳ 3a |
| `HTTP-40`, `BODY-11`, `BODY-13` | 3 | 3.1 | ✅ (P3b-5 reading) |
| `HTTP-41`, `BODY-15` | 1 | 1.2 | ✅ |
| `HTTP-42` | 1 | 1.4 | ✅ |
| `HTTP-43` | 1 | 1.3 | ✅ |
| `HTTP-44`, `HTTP-45` | — | 7.4, 7.5 (hand-off notes) | ⏳ 7a |
| `HTTP-51`, `BODY-2` | 4 | 4.1–4.3 | ✅ |
| `HTTP-52`, `BODY-30`, `BODY-31` | 1 | 1.6 (cites) | ✅ ⏳ 4c |
| `BODY-1`, `BODY-3` | 2–4 | 2.2, 2.3, 2.4, 3.2, 4.4 | ✅ |
| `BODY-4`, `BODY-5` | — | 7.5 | ⏳ 6a/6b/6c |
| `BODY-6` | 2, 4 | 2.4, 4.3 | ✅ |
| `BODY-8` | 2–4 | 2.4, 3.1, 3.2, 4.4 | ✅ |
| `BODY-9`, `BODY-35` | 2 | 2.3 | ✅ |
| `BODY-12` | 3 | 3.1 | ✅ 🚫 |
| `BODY-14` | 1 | 1.2 | ✅ |
| `BODY-16` | 1 | 1.4 | ✅ |
| `BODY-17`–`BODY-21`, `BODY-37` | 5 | 5.1 | ✅ (tee half ⏳ 3a) |
| `BODY-22`, `BODY-26` | 6 | 6.2, 6.4 | ✅ |
| `BODY-23`, `BODY-24`, `BODY-27`, `BODY-28`, `BODY-29` | 6 | 6.1, 6.3 | ✅ |
| `BODY-25` | 6 | 6.1, 6.2 | ✅ (§10 entry 4) |
| `BODY-32` | 5, 6 | 5.1, 6.3 | ✅ (capless clause vacuous) |
| `BODY-33` | 1 | 1.6 | ✅ |
| `BODY-34` | 5, 6 | 5.1, 6.3 | ✅ ⏳ 5b |
| `BODY-36` | 7 | 7.4 | ⏳ `docs/first-release.md` |
| inherited `SEAM-14` | 1 | 1.3, 7.4 | ✅ |

All 49 rows are present; the checklist recounts the totals.

## Tasks per PR

| PR | Tasks | Commits (approx.) | Gate |
|---|---|---|---|
| 0 | 0.1 | none | reads 3a |
| 1 | 1.1–1.7 | 5 | free now |
| 2 | 2.1–2.5 | 3 (splittable after 2.2) | form free; seekable needs 3a |
| 3 | 3.1–3.3 | 1 | 3a |
| 4 | 4.1–4.5 | 1–2 | 3a |
| 5 | 5.1–5.2 | 1 | 3a tee |
| 6 | 6.1–6.5 | 1–2 | 3a `OpenRead`, PR 1 |
| 7 | 7.1–7.6 | 2 | PRs 1–6 |

## Findings while planning

1. **F1 — `ErrorBodyPreviewTests` does not exist.** The design names it for `BODY-33` as if it did; the plan creates it (A1).
2. **F2 — The `Dispose` pattern change breaks every `ResponseBody` subclass at once.** PR 1's task 1.2 is therefore the one
   atomic commit; it touches one `src/` file outside Core (`HttpResponseMessageBody.cs`) and eight test files. The plan lists
   them from `git grep`; the executor re-greps before committing.
3. **F3 — The replaced `SEAM-30` pin test needs an `Activity` on a worker thread.** The plan relies on `ExecutionContext`
   flow into `TaskCreationOptions.LongRunning` workers (A3) and gives a fallback if that does not hold on the pinned runtime.
4. **F4 — `PrefixedReadStream`/drain ordering inside PR 6.** Task 6.2's tests cannot all be green without 6.3's
   `OpenReadAsync`; the plan folds the two into one commit (two test classes stay separate) rather than commit a throwing stub.
5. **F5 — Open for the lead, not resolved here.** P3b-3 (no-listener reporting gap), P3b-5 (Unix special files upload as an
   empty body: whether §10 entry or §11 item) and P3b-10 (linked drain token departs from a §3.1 sentence). The plan builds the
   design's defaults and routes each in task 7.4; if the lead rules otherwise, the affected tasks are 1.1, 3.1 and 6.2.
6. **F6 — Dependency on 3a's names.** All consumer tasks (2.3, 3.1, 4.3, 5.1, 6.x) read 3a's merged surface through task 0.1.
   If 3a's `WriteTo` is abstract rather than virtual, every existing `RequestBody` variant already in the repo (and the test
   fakes) must implement it; that is 3a's change, not 3b's, but the new 3b variants implement both shapes regardless.

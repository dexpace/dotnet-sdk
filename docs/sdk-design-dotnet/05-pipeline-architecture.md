## 5. Pipeline Architecture

Both pipeline layers survive, and the specification forbids merging them: "A port MUST NOT collapse the two layers
into one: the stage pipeline owns ordering and re-drive-with-fork; the recovery chain owns the sum-type fold and
the uniform-failure guarantee." .NET keeps two execution models only weakly (§3.3): the asynchronous path is primary
and the synchronous path is a real one — `HttpClient.Send`, `Stream.Read` — rather than a blocking wrapper, so the
sync mirror of the stage pipeline and both bridges are kept, but the direction of the mirror is reversed relative to
the reference: here the async runtime is the one every other shape is derived from.

This chapter is the one where the as-built tree and the specification disagree most, and the disagreement has a
single root. The pipeline spine PR #6 built, over PR #4's
`PipelineContext`, made that context the single mutable carrier and the runner re-entrant from a fixed index, so
that no per-attempt state cloning is needed. The second half of that sentence is right and is the best idea in the
as-built pipeline; the first half is what §5.1 overturns, because a mutable carrier shared by every re-drive is
precisely the state a fork is supposed to isolate.

**Correction (2026-09-29): citations of the retired 2026-06 documents are repointed (roadmap decision D2).** The
lead ruled on D2 on 2026-09-29: the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice
designs and two plans) are replaced by the specification, this design and the roadmap rather than filed under
`docs/work/`, and were deleted from the tree; git history keeps them. Where this chapter cited one of them, the
citation was edited in place: it now names the section that owns the decision, or states the decision inline with
the pull request (#3–#9) that built it. No decision recorded here changed.

### 5.1 The stage-based pipeline

**The step is an abstract class, not a delegate — the ecosystem's shape, not its package (P14).** Two shapes
dominate .NET HTTP composition. `DelegatingHandler` (request in, response out) is the runtime's own, but it lives
*below* the transport seam on `HttpRequestMessage`, and the connection-layer split (Porting Method) deliberately leaves
it there: an
enterprise's handler chain composes *underneath* the SDK. Above the seam, the shape .NET client libraries have
converged on is the abstract policy class with a stage, a `Process`/`ProcessAsync` pair and an explicit `next` —
Azure.Core's `HttpPipelinePolicy` and its successor `System.ClientModel.Primitives.PipelinePolicy`. The as-built
`HttpPipelinePolicy` already adopts that shape; the port keeps it and takes neither package (both are installed from
NuGet, so neither earns a P2 pass). Policies are `sealed` unless designed for extension, which is also how
**PIPE-36**'s stage lock is enforced: `AuthorizationPolicy` seals `Stage`, and every shipped pillar class is sealed.

**Stages, reconciled.** The specification fixes `PRE_REDIRECT → REDIRECT → RETRY → AUTH → LOGGING → SERDE → SEND`
(**PIPE-2**) with user slots around each pillar and sparse keys (**PIPE-3**). The as-built enum is sparse and
correctly ordered but misnames one slot and omits two. The port's enum:

| Value | Spec stage | Pillar | Notes |
|---|---|---|---|
| `Operation = 100` | (outside both loops) | yes | .NET addition: once-per-call deadline and operation span (§6.1) |
| `PerCall = 150` | PRE_REDIRECT slot | no | **new**: the error-mapping step's home (**PIPE-37**); runs once, sees the final response |
| `Redirect = 200` | REDIRECT | yes | |
| `PerHop = 250` | post-redirect / pre-retry | no | **renamed** from as-built `PerCall`, which runs once *per redirect hop*, not per call |
| `Retry = 300` | RETRY | yes | |
| `PerAttempt = 400` | post-retry / pre-auth | no | fresh `Date`, per-attempt headers |
| `Auth = 500` | AUTH | yes | |
| `Diagnostics = 600` | LOGGING | yes | per-attempt span, metrics, log events |
| `Serde = 700` | SERDE | yes | **new**, reserved, no shipped policy — the spec reserves it, so this is not a fabricated slot (P11) |

`SEND` is not an enum member at all: the transport is the fixed terminal captured by `Build`, so **PIPE-8**'s "MUST
NOT hold a user step" is enforced by the type system rather than by a check. The rename matters: the as-built
`PerCall` doc comment promises "once per logical call", and a policy author who trusts it and mints something
per-call there gets one per redirect hop.

**Composition rules.** A pillar admits one policy (**PIPE-4**). A distinct second policy on an occupied pillar must
"fail fast naming both step types and pointing at the replace path" (**PIPE-5**); as built, cardinality is checked
only at `Build`, and the message names the stage and a count, not the two types. The port checks at `Add`, where
the stage is already known, and names both runtime types and `Replace<T>`. Re-adding the *same* instance is
idempotent (**PIPE-6**) and is decided with `ReferenceEquals` — stated explicitly because a policy written as a
`record` would otherwise compare by value and swallow a genuine collision; as built, adding one instance twice
throws at `Build`. `Remove<T>` deletes every instance (**PIPE-20**) and a missing anchor names the type
(**PIPE-21**) — both built. What is missing is the rest of the edit surface: `Prepend`, `AddRange`/`PrependRange`
with **PIPE-38**'s documented asymmetry (append-all keeps batch order, prepend-all reverses it), the cross-stage
rejection of **PIPE-18**/**PIPE-19** (as built, `InsertBefore<T>` of a policy from another stage is silently
re-bucketed by the stable sort instead of rejected), the all-or-nothing bulk reload (**PIPE-23**), and the
empty-slots-only resilience preset (**PIPE-24**; `DexpacePipeline.CreateDefault` builds from scratch and has no
"install into this builder" form). `Build` is a stable sort by stage into an array (**PIPE-22**, **PIPE-25**); the
port additionally exposes it as an `IReadOnlyList<HttpPipelinePolicy>` view, which **PIPE-25** requires.

**The runner already forks for free; the context is what does not (P7).** The reference's defect — "reusing the
handle resumes past already-visited steps and MUST be treated as a defect" (**PIPE-15**) — exists because the JVM
cursor is a mutable object whose index advances. The as-built `PipelineRunner` is a `readonly struct` holding the
policy array, an immutable index and the transport; `RunAsync` constructs the next runner rather than advancing
this one. Calling it twice therefore *is* forking: both calls resume from the same position (**PIPE-16**), and there
is no advanced handle to misuse. This is the hidden-precondition case P7 warns about, because **PIPE-16** asks for
three things — same position, "carry the current in-flight request", and "advance independently" — and the struct
gives only the first. The request and response live on the shared `PipelineContext`, whose `Request` has a public
setter, so a re-drive starts from whatever the previous drive's *downstream* policies left there. Verified on
.NET 10.0.401 against the as-built assemblies (a probe policy at `PerAttempt`, a `BasicAuthPolicy` at `Auth`, a
server returning 503 then 200): the probe saw `Authorization` absent on attempt 1 and **present** on attempt 2 —
the second attempt entered the retry loop carrying the first attempt's credential, stamped by a policy *below* the
probe. That is **RETRY-44**'s "upstream steps MUST NOT mutate the shared in-flight request between attempts"
failing in the other direction, and the same leak is why the as-built redirect follower builds hop *n+1* from a
request that already carries hop *n*'s auth header.

The fix is to stop sharing the request, not to clone the context, and it lands on the shape the rest of .NET
already uses for this problem — `DelegatingHandler.SendAsync(request) → response`:

```csharp
public abstract class HttpPipelinePolicy
{
    public abstract PipelineStage Stage { get; }
    public abstract ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner next);
    public virtual Response Process(Request request, PipelineContext context, PipelineRunner next);
}

public readonly struct PipelineRunner
{
    public ValueTask<Response> RunAsync(Request request, PipelineContext context);
    public Response Run(Request request, PipelineContext context);
}
```

`Request` is already an immutable `record` (§4), so passing it as an argument makes every clause of the fork
contract structural. A substituted request "sticks" downstream because it is what the step passes on
(**PIPE-14**); a re-drive carries exactly the request the pillar holds, never a downstream mutation (**PIPE-16**,
**RETRY-44**); a short-circuit is returning a synthetic `Response` (**PIPE-12**); and the as-built
`PipelineAbortedException` for "the chain completed without producing a response" disappears, because a method
returning `ValueTask<Response>` cannot complete without one. `PipelineContext` shrinks to call-scoped, read-mostly
state — options, the call's `CancellationToken`, the seed request (§6.2), the correlation context (§5.4), the active
`Activity`, a call-scoped property bag — plus two per-drive values, the attempt ordinal and hop count, which a
pillar sets by passing `context.ForAttempt(n)`, a copy sharing every call-scoped reference. Options are carried by
reference, unchanged, across every fork (**PIPE-17**); that they are *immutable* is §8.2's obligation, and the
as-built `DexpaceClientOptions` — a class with public setters, shared by reference into in-flight calls — does not
yet meet it. A built pipeline is an immutable array plus a transport, so concurrent calls share nothing mutable
(**PIPE-10**, **PIPE-11**), and the empty pipeline dispatches straight to the transport (**PIPE-9**) with one
context allocation, which the SHOULD half of that requirement tolerates.

**Cursor-scoped state retires with the marker it existed for.** Ruby needed forked-cursor state, inheritable and
writable only by the forking pillar, to carry the cross-origin redirect marker safely. This port removes the marker
(§6.2): the seed request is fixed by the runtime when the call starts, and both the redirect and auth pillars
compare against it. With nothing a pillar needs to hand privately to its own downstream, there is no write-guarded
per-fork map to build (P11). The call-scoped property bag stays, for state that is legitimately per call — the
idempotency key reused across redirect hops.

**Three shipped steps that are not pillars.** Two of the requirements cited here, **RECOV-32** and **RECOV-33**, are
stated only in Appendix C (§11 item 33). As in Ruby, each is written once against the one policy
shape, so the same class serves both layers (§5.2).

- **The idempotency step** (**RECOV-32**) stamps its header only for methods in the configured set, default POST,
  PUT, PATCH; in the default respect-existing mode a request already carrying the header is left alone and the key
  strategy is not invoked, and the strategy is invoked at most once per applicable request. As built:
  `IdempotencyPolicy` defaults to POST only, has no overwrite mode and a fixed `Guid` strategy, and correctly checks
  the header before minting; its key is parked in the call-scoped bag so redirect hops reuse it.
- **The client-identity step** (**RECOV-33**) joins configured tokens into one space-separated line, appends after
  the first existing value by default or replaces in Replace mode, and emits nothing for a blank line. As built,
  `ClientIdentityPolicy` is Replace-only over `DexpaceClientOptions.UserAgent`.
- **The error-mapping step** (**RECOV-15**) maps only 400..599 and returns 1xx, 2xx and 3xx unchanged, so a 304 or
  an unfollowed 3xx keeps its body (**BODY-31**); its factory rejects a non-error status (**XCUT-8**). As built
  there is no step: the core policies of PR #6 chose that `HttpPipeline.SendAsync` **returns** the `Response` for
  any status, with a caller-invoked `Response.EnsureSuccessAsync`. The port keeps that as the default — it is the .NET
  idiom, `HttpResponseMessage.EnsureSuccessStatusCode` — and ships an `ErrorMappingPolicy` at `PerCall` for callers
  who want throw-by-default, the placement **PIPE-37** requires. **P13 — the obvious tool maps the wrong range.**
  `EnsureSuccessStatusCode` throws for anything outside 200–299; verified on .NET 10.0.401, a bare 304 raises
  `HttpRequestException`. The as-built `EnsureSuccessAsync` copies that convention (`if (IsSuccess) return;`, with
  `IsSuccess` meaning 2xx), so it turns a 304 into an exception and violates **RECOV-15** and **BODY-31**. It must
  test `Status.IsClientError || Status.IsServerError` instead.

**The bounded error-body copy, specified once.** **RECOV-16** requires the error body "buffered into a bounded,
replayable in-memory copy capped at a fixed maximum (1 MiB / MAX_BUFFERED_ERROR_BODY_BYTES)", a hard truncation
with no marker, the same bound on every buffering path. The constant exists (`Response.MaxBufferedErrorBytes`) and
`EnsureSuccessAsync` drains to it inside an `await using` over the body stream, which releases the connection
whether or not the drain completes (**BODY-30**). The port moves the drain into one internal
`ErrorBodyBuffer.CaptureAsync(Response, CancellationToken)`, shared with the retry stack's re-classification of a
re-sent error (**RETRY-36**), rents its chunk from `ArrayPool<byte>.Shared` instead of allocating 80 KiB per call
(`docs/styleguide/csharp/13-resource-management.md` §13.6), and disposes the original `Response` rather than only
its stream. The truncation is markerless by specification, so the decoding witness (`HttpResponseException
.GetErrorAsync<T>`) must fail into a typed `DeserializationException` on a structurally incomplete payload, never
assume well-formedness.

**PIPE-40**'s lifecycle rule — close every superseded intermediate response before the next drive, never close the
one handed back, return the in-flight response unclosed on an abandoned re-drive — stays with the re-driving
policy, which is the only code that knows which response is superseded. With responses flowing as return values
rather than through `context.Response`, "superseded" is simply "a local the policy is about to overwrite", and the
`await using` it would be tempting to wrap around it is exactly wrong for the one it returns.

**As built (d45e64b):** built — diverges: shared mutable `PipelineContext.Request` leaks downstream mutations into
re-drives; pillar collision checked late and unnamed; no prepend/bulk/cross-stage rules; stage slots misnamed or
missing; `EnsureSuccessAsync` maps non-2xx instead of 400..599.

**Dated correction (2026-10-07, phase 4b).** *Three shipped steps*: the logic lives once in the recovery steps
(`IdempotencyKeyStep`, `ClientIdentityStep`) and the policies delegate to them (P4b-17, P4b-18); `IdempotencyPolicy`
defaults to POST, PUT and PATCH and takes an `IdempotencyKeyStep`, and `ClientIdentityPolicy` appends by default and takes a
`ClientIdentityMode`. *The bounded error-body copy*: `ErrorBodyBuffer` (sync and async) and `ErrorMappingStep` are built by
4b (P4b-16), and `Response.EnsureSuccessAsync` is re-homed onto the buffer; `ErrorMappingPolicy`, `XCUT-8`'s factory
rejection and the public sync `Response.EnsureSuccess` stay 4c's, over `ErrorBodyBuffer.Capture`.

**As built (2026-10-07, phase 4c):** built. The text above stands as written, with five *dated corrections*:

- **`PipelineContext` as built (P4c-4).** A sealed class with an internal constructor over one call-scoped state object, with per-drive
  copies (`ForAttempt`, `ForHop`, `WithActivity`, `WithCancellationToken`). Its members: `SeedRequest`, `RequestOptions`, `Options`,
  `CallKey`, `Instrumentation`, `CancellationToken`, `Activity`, `AttemptNumber`, `HopNumber`, and a property bag keyed by
  `PipelinePropertyKey<T>` instances (reference identity) in place of the string bag, whose `"dexpace.auth.origin"` key any policy could overwrite.
  There is no `Request` and no `Response`. The policy parameter is named `continuation` (`CA1716` rejects `next` on a virtual member).
- **The builder reads `Stage` once (P4c-8)**, recording a `(policy, stage)` entry at insertion and rejecting a value that is not a
  `PipelineStage` member; a policy whose `Stage` varies cannot pass validation in one slot and run in another. A policy must be a
  plain class: it cannot be a `record`, which may not inherit from the non-record `HttpPipelinePolicy` (CS8864).
- **Cross-stage edits are `ArgumentException` (P4c-10)**; a missing anchor stays `InvalidOperationException`; a pillar collision is an
  `InvalidOperationException` at `Add` naming both types and `Replace<T>`. No new public exception type.
- **`ErrorMappingPolicy` wraps 4b's `ErrorMappingStep` (P4c-17)** through the response fold, at `PerCall`, and is not part of
  `CreateDefault`; the shared error-body buffer is 4b's.
- **"A response with no body" is the empty replayable body (P4c-18)**, which `ErrorMappingPolicy` maps without a drain; the exception
  carries the response as it is (**BODY-30**).

### 5.2 The recovery-chain primitives

**RECOV-1**'s closed two-variant outcome is a record hierarchy closed by a private constructor — the styleguide's
own `Result` pattern (`docs/styleguide/csharp/08-error-handling.md` §8.7):

```csharp
public abstract record Outcome
{
    private Outcome() { }
    public sealed record Success(Response Response) : Outcome;
    public sealed record Failure(Exception Error) : Outcome;
    public T Match<T>(Func<Response, T> onSuccess, Func<Exception, T> onFailure);
}
```

The hierarchy is genuinely closed — no type outside `Outcome` can call the private constructor — so mutual exclusion
and joint exhaustiveness hold by construction. What C# 14 does not give is compiler-proven exhaustiveness at a
`switch`: the compiler does not know the hierarchy is closed and reports CS8509 on a switch expression without a
discard arm, which `TreatWarningsAsErrors` turns into a build break. The port therefore makes `Match` the one fold
every chain uses, and any `switch` expression over `Outcome` carries a `_ => throw new UnreachableException()` arm.
The same `Outcome` is reused, not re-invented, by the SSE typed adapter (§7.2).

**RECOV-2 — "catch EVERY throwable" — meets a runtime where not every throwable can be caught.** .NET has no
`Throwable`/`Error` split; everything derives from `Exception`, and the obvious translation, `catch (Exception)`,
also catches `OutOfMemoryException`, which **RETRY-25** says "MUST NOT be retried, classified retryable, or logged;
they MUST be surfaced unchanged with no suppressed-trail attachment." `StackOverflowException` cannot be caught at
all — the runtime terminates the process — and `AccessViolationException` is not delivered to managed handlers on
.NET Core. The port's rule is one filter, used by every orchestrator and every retry loop:

```csharp
catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))   // IsFatal: OutOfMemoryException (and subtypes)
```

An exception filter runs before the stack unwinds, so a fatal exception that fails the filter propagates with its
original stack and no intermediate frame touches it — "surfaced unchanged" in the most literal sense .NET offers.
This is recorded as §10 entry 12: the letter of **RECOV-2** says *every*, and the port
excludes a named fatal set for the reason **RETRY-25** gives. `ThreadInterruptedException`, the one .NET relic of
JVM-style interruption, is classified as cancellation (§6.1), never as a retryable failure.

**Cancellation needs no re-assertion, but its classification needs care (P7).** **RECOV-11** requires the wrapper
to "re-assert the cancellation signal on the current context before returning". On .NET the signal is a
`CancellationToken`, and a cancelled token cannot be un-cancelled, so converting an `OperationCanceledException`
into a `Failure` loses nothing and the clause is free. The hidden precondition is that "this exception is a
cancellation" and "the call was cancelled" are different facts: `HttpClient.Timeout` surfaces as
`TaskCanceledException` with an inner `TimeoutException` although nobody cancelled anything (verified on .NET
10.0.401; caller-token cancellation surfaces as `TaskCanceledException` with no `TimeoutException` anywhere in the
chain). Every classification therefore tests the *call's* token (`context.CancellationToken.IsCancellationRequested`),
never `ex is OperationCanceledException` — which is **XCUT-2**'s "told apart by the ambient cancellation state,
not by matching a message string", and §6.1 builds the retry classifier on it.

**RECOV-3**–**RECOV-10** are plain folds over arrays copied at construction (`[.. steps]`), which also resolves
**RECOV-14**'s reference asymmetry in the stricter direction — both lists of both chains are copied. Request steps
fold left to right and a throw aborts the rest; response steps run only on `Success`; recovery steps run on every
outcome in declared order; a throwing response step becomes a `Failure` for the recovery steps (**RECOV-7**) and a
throwing recovery step a `Failure` for the *next* one, so the chain's apply never throws (**RECOV-8**). Dispatch
unwraps with `ExceptionDispatchInfo.Capture(error).Throw()` (**RECOV-10**): it rethrows the same instance with its
original stack trace, where `throw error;` would reset the trace and `throw new …` would substitute. The as-built
`RetryPolicy` already rethrows this way. **RECOV-12**/**RECOV-13** — the pipeline closes the response held by a step
that *throws*, while a step that *returns* a different outcome owns the one it dropped — live in one internal helper
so the asymmetry cannot be implemented twice. The four reference drifts the specification pushes onto porters
(**RECOV-14**, **BODY-8**, **RETRY-34**, **AUTH-31**) are resolved the same way Ruby resolved them (§11
item 12), and here each is additionally unable to drift between sync and async, because
§5.3 gives every policy one implementation for both.

**Suppressed exceptions have no host facility.** **RECOV-12**, **RETRY-34**, **PAGE-13**, **SSE-29** and others need
a list of secondary failures on a primary one. `Exception.InnerException` is a single causal parent, set once at
construction; `AggregateException` is a *wrapper*, and wrapping would violate **RECOV-10**'s "rethrow the contained
throwable UNCHANGED". The only per-instance extension point every exception has is `Exception.Data`. The port
therefore has one `ExceptionTrail.AddSuppressed(primary, secondary)` helper: on an `SdkException` it appends to a
`Suppressed` list the type exposes and renders in `ToString()`; on any other exception it stores the list under a
namespaced `Data` key, readable through `ExceptionTrail.GetSuppressed(Exception)`. Verified on .NET 10.0.401:
`Exception.Data` accepts a non-serializable `List<Exception>`, and a foreign exception's `ToString()` does not
render it — so on a non-SDK primary the trail is retrievable but invisible to a naive log line. The helper skips
attaching an exception to itself (**RETRY-34**'s self-suppression guard). This is the one place the port cannot
match the reference's ergonomics, recorded as §10 entry 13.

**Walking the cause chain is cycle-safe, and on .NET the cycle is almost unreachable (P7).** **XCUT-9** requires any
cause walk to "track visited causes by reference identity and terminate on a self-referential or cyclic chain".
`InnerException` is get-only and assigned in the constructor, which must receive an already-constructed exception,
so no public API can close a cycle; verified on .NET 10.0.401 that one *can* be closed by writing the private
`_innerException` field through reflection, which is also what a hostile or buggy deserializer would do. The walk
also has a shape the JVM's does not: `AggregateException.InnerExceptions` makes the "chain" a tree. Core has one
`ExceptionFacts.EnumerateCauses(Exception)` that walks `InnerException` and `AggregateException.InnerExceptions`
breadth-first with a `HashSet<Exception>(ReferenceEqualityComparer.Instance)` — reference identity, because an
exception type may override `Equals` — and every classification in the port goes through it. It is bounded as well
as cycle-safe (a depth cap of 64), because rule 9 of the house style bounds every loop.

**As built (d45e64b):** not built — no `Outcome`, no recovery chain, no suppressed trail, no shared cause walker; the
fatal filter and dispatch-info rethrow appear only inside `RetryPolicy`.

**As built (2026-10-07, phase 4b):** built. The recovery layer lives in `Dexpace.Sdk.Core.Recovery`: `Outcome`, the three step
contracts, `RequestRecoveryChain`, `ResponseRecoveryChain` and `RecoveryDispatcher`, with `ExceptionFacts` and
`ExceptionTrail` in `Dexpace.Sdk.Core.Errors`. The retry engine (the recovery-stack engine, **RECOV-17**–**RECOV-30**,
**RECOV-34**) stays phase 6a's. The text above stands as written, with six *dated corrections*:

- **`Outcome` is an abstract class with nested sealed classes, not a record (P4b-3).** A non-sealed record gets a
  synthesised `protected` copy constructor that any record outside the assembly can call, and C# forbids making it
  private, so `abstract record Outcome` with a private constructor is not closed (verified on 10.0.401). An abstract class
  with a private constructor is. The accessors are the Try-pattern (`IsSuccess`, `IsFailure`, `TryGetResponse`,
  `TryGetError` with `[NotNullWhen(true)]`, P4b-4) and one fold, `Match<T>`.
- **The layer has both forms over one core (P4b-6).** Every step contract declares `Apply` and `ApplyAsync`; each chain and
  the dispatcher run one private `bool async` body, and the sync entry point reads the already-completed `ValueTask`
  through the internal `SyncPath` (P4b-7, §11 item 46).
- **`EnumerateCauses` yields the root first (P4b-13)**, then the causes breadth-first; the name reads the other way.
- **The trail (P4b-14).** `SdkException.Suppressed` and `ExceptionTrail.GetSuppressed` return immutable snapshots; a foreign
  exception whose `Data` is read-only drops the secondary rather than throw (the one deliberate swallow); no attachment
  involves a fatal exception, the primary itself, or the same secondary twice; `SdkException.ToString()` renders
  `(Suppressed Exception #n)` blocks with a cycle guard and an 8-level cap.
- **Conversion boundary (P4b-8).** `when (!ExceptionFacts.IsFatal(ex))` at four sites; `OperationCanceledException` is
  converted like any other (the token stays cancelled, **RECOV-11**), and a `null` from a step or a transport is a throw of
  `InvalidOperationException` naming the type.
- **`ErrorBodyBuffer` is 4b's (P4b-16)**; see §5.1.

**As built (2026-10-08, phase 6a):** the recovery-stack retry is built. It composes the dispatcher's halves (the request chain once;
the transport and the response steps per send; the recovery steps once on the terminal outcome) around the shared `RetryEngine`, and it is
**not** an `IRecoveryStep` (P6a-5): a recovery step sees only an `Outcome`, so it could not re-send. `RetryRecovery` is configuration only;
`RecoveryDispatcher` gains a constructor taking it and a `Retry` property. `RECOV-17`–`RECOV-31` and `RECOV-34` are closed by the
6a checklist's carried-rows table.

### 5.3 The async mirror and the two bridges

**One stage identity, two invocation protocols, one implementation per policy.** **PIPE-28**'s "the two MUST NOT
each re-derive ordering independently" is structural here: one `PipelineStage` enum, one sorted array, and both
`Run` and `RunAsync` index into it. What remains is how a policy provides both protocols without writing its logic
twice — the drift **AUTH-31** and **RETRY-34** record in the reference. The as-built answer is not to: there is no
`Process`, and `HttpPipeline.Send` is `SendAsync(...).AsTask().GetAwaiter().GetResult()` over a transport whose
`Execute` is itself `ExecuteAsync(...).GetAwaiter().GetResult()`. That is sync-over-async twice, which the house
style forbids (`docs/styleguide/csharp/09-concurrency.md` §9.1) and which burns a pool thread per blocked call.
The port adopts the Azure SDK's established idiom instead: each shipped policy implements one private
`ProcessCoreAsync(request, context, next, bool async)` in which every I/O call branches
`async ? await next.RunAsync(...) : next.Run(...)`; `ProcessAsync` calls it with `async: true`, and `Process` calls
it with `async: false` and asserts the returned `ValueTask` is already completed before reading its result. Nothing
blocks on an incomplete task, the retry wait is a genuine blocking wait on the sync path (§6.1), and there is exactly
one body of retry, redirect and auth logic. `Process` has a default implementation for third-party policies, and
*that* default is the documented blocking bridge the pipeline-spine design that preceded PR #6 anticipated (a virtual
`Process` that blocks on `ProcessAsync`; PR #6 built only the pipeline-level `HttpPipeline.Send`); shipped policies
override it.

**PIPE-29**/**PIPE-30** are nearly free on .NET (P7). An `async` method never throws synchronously — any exception,
including an argument check, is captured into the returned task — so a policy written with `async` satisfies
**PIPE-29** without effort. The hidden precondition is a non-`async` override that returns a `ValueTask` directly
and throws before returning, as the as-built `ApiKeyAuthPolicy.GetCredentialAsync` would if it validated anything.
Verified on .NET 10.0.401: calling such a method directly throws synchronously, but awaiting it from inside an
`async` runner produces a faulted task instead. Because `PipelineRunner.RunAsync` is itself `async`, the runtime
normalises every policy's synchronous throw into a faulted task (**PIPE-30**) by construction; fatal exceptions are
captured the same way, which is the one sense in which .NET cannot honour "propagate synchronously" and the reason
§5.2's filter, not this normalisation, is where fatal errors are kept out of retry and logging. **PIPE-31**'s
terminal mapping is `SendAsync<T>(request, Func<Response, CancellationToken, ValueTask<T>> handler, ...)`, which
applies the handler inside `await using` over the response (disposal is idempotent) and unwraps nothing, because
`await` already surfaces the original exception rather than an `AggregateException` — verified: `.Result` on a
faulted task throws `AggregateException`, `await` and `GetAwaiter().GetResult()` throw the original.

**PIPE-32 is reversed.** The specification says "the async standard pipeline MUST NOT follow HTTP redirects at the
pipeline layer (there is no async redirect pillar)", and **REDIR-25** repeats it while listing three porter options,
the third being "(c) let callers install their own async redirect step." Ruby preserved the asymmetry and made it
visible. On .NET the asymmetry would be perverse: the async pipeline is the primary one, the redirect policy is
already async, and a sync/async behavioural split is exactly what §5.3's single-implementation rule exists to
prevent. The port's async and sync standard pipelines both install `RedirectPolicy`. **REDIR-25**(c) sanctions the
step existing on the async path; what it does not sanction is the standard preset installing it, and that is
recorded as §10 entry 14. The invariant the asymmetry protected — exactly one layer follows
redirects — is kept by forcing the transport not to (§6.2), not by making the SDK not to.

**The bridges.** **PIPE-33**'s sync-to-async bridge "MUST require a caller-supplied executor (no default)", and
**SEAM-18** gives the reason: "a shared global fork/join-style pool is explicitly not acceptable because a blocking
call would starve it." The as-built `IHttpClient.AsAsync()` is `Task.Run(() => inner.Execute(request), ct)` — the
shared `ThreadPool`, the exact pool the requirement names. Hill-climbing thread injection softens starvation on .NET
but does not remove it. The port's bridge takes the executor .NET actually has, a `TaskScheduler`, with no overload
that defaults it; a caller wanting dedicated threads passes a scheduler that creates them. **PIPE-33**'s
interruption clause maps onto the one cancellation mechanism .NET has: `Thread.Abort` throws
`PlatformNotSupportedException` on .NET Core and `Thread.Interrupt` only wakes a thread in a managed wait, so
"cancelling with interruption" is passing the call's `CancellationToken` into the worker's `IHttpClient.Execute`,
where `HttpClient.Send` aborts the in-flight socket operation; "cancelling without interruption" is the caller
awaiting `task.WaitAsync(token)`, which completes as cancelled while the worker runs on. The as-built sync seam's
`Execute(Request)` takes no token, so §3.2's addition of one is a precondition of this bridge. **PIPE-34**'s
async-to-sync bridge blocks with `GetAwaiter().GetResult()`, which unwraps (**ASYNC-13**) and honours the token it
threads through. Both mappings — interruption as token cancellation, executor as scheduler — fall under the
cancellation mapping §3.3 argues and §10 entry 8 records, here extended to
**PIPE-33** and **PIPE-34**; the residual (P6) is a sync transport that ignores its token, which no bridge on any
runtime can pre-empt. Both as-built bridges also dispose the client they wrap, which is **XCUT-22** failing for a
caller-supplied client: a bridge creates nothing, so its `Dispose` touches nothing (§3.7).

**PIPE-35**'s flatten-versus-nest choice is two named factories, `PipelineBuilder.Flatten(pipeline)` and
`PipelineBuilder.Nest(pipeline)`, never one overload. **PIPE-26** — the built pipeline "MUST itself implement the
transport SPI" — makes `Nest` trivial: `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient`, threading the
seam's per-call options (§3.2) into the pipeline's context, and its `Dispose` is a no-op toward the transport it did
not create (**PIPE-27**). As built, `HttpPipeline` implements neither interface and cannot stand in for a transport.

**As built (d45e64b):** partial: async runtime only; sync path is double sync-over-async; bridges default to the
shared pool, take no token, and dispose what they wrap; `HttpPipeline` is not a transport; no flatten/nest.

**As built (2026-10-07, phase 4c):** built. `HttpPipeline` implements `IAsyncHttpClient` and `IHttpClient` and is a no-op to dispose
toward the transport (no latch, so a disposed pipeline stays usable, **SEAM-15** being a MAY); `Flatten` and `Nest` are named
factories; every shipped policy has one `ProcessCoreAsync(..., bool async)` body. The text above stands as written, with two *dated
corrections*: *the sync terminal (P4c-13)*: `Build` decides once whether the transport implements `IHttpClient` (its own `Execute`) or
is bridged through `AsBlocking`, so the sync path is real down to the transport and no further until phase 8b; the retry policy's sync
wait is a genuine blocking wait over the `TimeProvider`, and `AuthorizationPolicy.GetCredential` is the documented bridge until phase 6c
gives `AccessTokenCache` a sync path. *The options capture (P4c-11)*: `Build(transport)` captures `new DexpaceClientOptions()` and
`Build(transport, options)` the caller's, so the seam entry points have client options to run with; the `SendAsync`/`Send` overloads
taking a `DexpaceClientOptions` override them for one call and carry no optional token (`RS0026`, `RS0027`). *Dated correction, 2026-10-07,
phase 5a (P5a-6):* the overloads are kept; `DexpaceClientOptions` is now an immutable record, so "this call, these options" is derived with `with`.

### 5.4 The execution context model

**The pre-roadmap design left one question open, and the specification answers most of it.** The question was
ambient `AsyncLocal` context versus an explicit context value threaded through the pipeline, and PR #4 answered it
with an explicit, mutable, per-call `PipelineContext`, no `ContextStore`, and no ambient SDK state. The port keeps the
*explicit* half of that answer, overturns the *mutable*
half for the correlation context, and overturns "no store", because **CTX-7**–**CTX-13** and **CTX-17**–**CTX-19** are
MUSTs and the store turns out to have real .NET substance.

**Why not `AsyncLocal<T>` (P13 — the obvious ambient tool has flow semantics the chain cannot use).** Ambient state
is how .NET correlates a call without threading a parameter, and `Activity.Current` already is ambient state backed
by `AsyncLocal`. But an SDK-owned `AsyncLocal` holding the promotion chain fails in two verified ways (.NET
10.0.401). First, flow is copy-on-write per async method: a value set inside an awaited `async` callee is **not**
visible to its caller afterwards, while a value set inside a synchronous callee **is**. A promotion performed deep
in the chain would be visible to the policies above it or not depending on whether the promoting code happened to
complete synchronously — a timing-dependent correctness property. Second, flow is capture-by-default: a task
started during call A and still running after call B has begun sees call A's value, so anything the SDK starts in
the background — the token cache's refresh (§6.3) — pins the context, and with it the request and response graph
**CTX-19** says must be released, for as long as it runs. The port therefore keeps no SDK `AsyncLocal`; the
correlation context travels explicitly on `PipelineContext`, and `Activity.Current` remains the only ambient value
because it is the runtime's own and every OpenTelemetry collector reads it (P14). Background work the SDK starts
runs under `ExecutionContext.SuppressFlow()` — verified: a task started under suppression observes no `AsyncLocal`
value.

**The promotion chain** (**CTX-1**–**CTX-3**) is three sealed records — `DispatchContext`, `RequestContext`,
`ExchangeContext` — not one type with a stage field, so "the exchange type exposes no method promoting back"
(**CTX-1**) is the absence of a method. Each `Promote` returns a new instance carrying the same key and bundle and
adding exactly one artifact (**CTX-2**); records make "each source is unchanged" free. The operation name is
introduced at dispatch→request, carried forward, and reaches the tracing seam only (**CTX-16**).

**The instrumentation bundle is `ActivityContext`, and the no-op bundle is its default (P14, P7).** **CTX-14** asks
for trace id, span id, flags, state, validity and remoteness flags, W3C-compatible — which is
`System.Diagnostics.ActivityContext` field for field. **CTX-15**'s reserved sentinels are `default(ActivityContext)`:
verified, its trace id is 32 zeros, span id 16 zeros, flags `None`, state empty, `IsRemote` false. **CTX-20**'s
zero-cost no-op tracer factory is `ActivitySource.StartActivity` returning `null` when no listener is registered,
which the as-built `InstrumentationPolicy` already relies on. The call key (**CTX-4**) is a `readonly record struct
CallKey(ActivityTraceId TraceId, ActivitySpanId SpanId, long Sequence)` with the sequence from
`Interlocked.Increment` on a process-wide counter: call-unique under the shared all-zero sentinel, no string
allocated unless rendered, and — because the key participates in the context records' equality — two
default-constructed contexts are unequal unless a caller pins an explicit key (**CTX-5**/**CTX-6**).

**The store, and the .NET reason to have one.** The specification never says who reads the store (§11
item 30). On .NET there is a concrete reader: the enterprise `DelegatingHandler` chain the
connection-layer split places *underneath* the transport. Such a handler sees only an `HttpRequestMessage`; the
SystemNet transport stamps
the `CallKey` into `HttpRequestMessage.Options` under a public `HttpRequestOptionsKey<CallKey>`, and the handler
resolves the live call through `DexpaceCallContexts.TryGet(key, out context)` (**CTX-18**'s explicit-absent lookup).
Carrying the key rather than the context keeps the request/response graph out of `HttpRequestMessage`, which
handlers routinely retain in logs. The store is a process-wide `ConcurrentDictionary<CallKey, ContextSlot>`, with
`TryAdd` as **CTX-8**'s single-winner insert and the indexer as its unconditional overwrite; registration happens at
promotion, never at dispatch construction (**CTX-17**).

**P13 — the obvious conditional remove compares by value.** **CTX-9** requires eviction "CONDITIONALLY ON REFERENCE
IDENTITY … never by value equality", and .NET ships an overload that looks exactly like the answer:
`ConcurrentDictionary.TryRemove(KeyValuePair<TKey, TValue>)` removes only if the current value matches. It matches
with `EqualityComparer<TValue>.Default`, which for a record is value equality. Verified on .NET 10.0.401: with a
live record in the slot, `TryRemove` of a distinct but value-equal stale record returns `true` and evicts the live
one — the precise failure **CTX-9**'s rationale describes. The store therefore holds each context in a `ContextSlot`,
a sealed class with no `Equals` override, so the same overload compares slot *references*, and the overload is added
to the banned-API list for any other value type (§9). Only the furthest-reached link occupies the slot, so closing a
promoted intermediate is a no-op (**CTX-10**). Disposing the `Response` closes its `ExchangeContext`; a failed call's
context is closed by the pipeline before the exception propagates.

**The bound is the backstop, and weak references are banned.** After each insert a drain *loop* evicts arbitrary
victims until the live count is at or under the cap (**CTX-11**, **CTX-12**, **CTX-13**), sharing one
`BoundedMap` helper with the Digest nonce counter (§6.3) and every other caller- or server-keyed map (**XCUT-14**).
The count is an `Interlocked` counter maintained beside the dictionary, because `ConcurrentDictionary.Count`
acquires every bucket lock (per the runtime source) and would serialise the hot path. **CTX-19**'s prohibition has
an obvious .NET temptation — `ConditionalWeakTable` and `WeakReference<T>` look like a way to "help" the collector
with abandoned contexts — and both are banned in core by `Microsoft.CodeAnalysis.BannedApiAnalyzers` (§9), because a
weakly held context could be collected mid-call along with an unread body pinning a connection.

**As built (d45e64b):** not built — no promotion chain, call key or store; correlation is `Activity.Current` plus a
mutable `PipelineContext` with an untyped string-keyed property bag.

---


**Correction 2026-10-07 (phase 4a, `docs/work/mvp/phase4/phase4a/2026-10-05-phase4a-context-design.md`).** The correlation
bundle is `InstrumentationContext`, a sealed class composing `ActivityContext`, the active `Activity?` and the
`ActivitySource` factory (P4a-3). Promotion registers, and each context binds the store it registers into (P4a-6). The
identity-compare slot lives in the internal `BoundedMap`, not a context-specific type (P4a-12). The store's cap is
10 000 (P4a-10). `CallKey` has no public constructor; mint with `CallKey.Next` (P4a-2). The promotions are
`PromoteToRequest` and `PromoteToExchange` (P4a-4). `default(ActivityContext).TraceState` is `null`, not empty, so
`InstrumentationContext` normalises it to `""` for CTX-15 and OBS-26. The route of the `CallKey` to a transport
`DelegatingHandler` is open and lands with 4c and 8b (P4a-13).

**As built (2026-10-07, 4a):** built. `Dexpace.Sdk.Core.Execution` holds the chain, the key, the bundle and
`DexpaceCallContexts.TryGet`; `BoundedMap` backs the store.

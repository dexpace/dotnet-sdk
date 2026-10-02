# cancellation-and-timeouts

## Rules
- Cancellation is distinct from timeout, propagates into the network layer, and never silently disappears.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:24-24` · high · sha:8014d2ec2c9d</sub>
- SEAM-13 (SHOULD) - Blocking transports SHOULD honor cooperative cancellation during blocking I/O, and async transports SHOULD treat cancelling the returned future as a best-effort abort of the in-flight exchange.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:26-26` · high · sha:8014d2ec2c9d</sub>
- Cancellation is terminal and non-retryable, and MUST be told apart from a retryable timeout out-of-band, never by matching an error message (see section 9, RETRY-23, RETRY-24).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:27-27` · high · sha:8014d2ec2c9d</sub>
- XCUT-1 (MUST) A thread or task cancellation MUST be surfaced as a distinct, terminal, NON-retryable signal kept separate from a timeout, propagating cancellation without losing the ambient cancellation flag and never letting a cancelled operation be automatically retried.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:9-9` · high · sha:d6123be82c9e</sub>
- XCUT-2 (MUST) A read, response or connect TIMEOUT MUST be classified as a RETRYABLE transport failure and MUST NOT set the cancellation flag.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:10-10` · high · sha:d6123be82c9e</sub>
- XCUT-2 (MUST) Timeout and cancellation MUST be told apart by the ambient cancellation state rather than by matching a message string, even when the runtime uses the same exception type for both, and when the timeout type is a subtype of the cancellation type the timeout branch must be checked first.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:10-10` · high · sha:d6123be82c9e</sub>
- XCUT-3 (MUST) Inter-attempt retry waits MUST be promptly cancellable: a pending wait MUST abort near-immediately on cancellation, surface the cancellation signal (not a spurious timeout), and cancel any timer or future it armed.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:11-11` · high · sha:d6123be82c9e</sub>
- XCUT-3 states that a port SHOULD preserve the reference's non-pinning wait property (a scheduled timer completing an awaitable future so a virtual-thread carrier can unmount and no shared pool thread is monopolized) where its runtime has an equivalent concern, but the normative requirement is prompt cancellation, not the mechanism.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:11-11` · high · sha:d6123be82c9e</sub>
- A timeout is discriminated from caller cancellation by the caller's token: catch (OperationCanceledException) when (!callerToken.IsCancellationRequested) is a timeout.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:330-331` · high · sha:da6000c93fc5</sub>
- RequestOptions.Timeout is enforced with a separate CancellationTokenSource whose own state is inspected.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:334-335` · high · sha:da6000c93fc5</sub>
- The SDK-managed client sets HttpClient.Timeout to Timeout.InfiniteTimeSpan so deadlines live in per-call options and the shared client is never mutated (TRANSPORT-5).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:335-337` · high · sha:da6000c93fc5</sub>
- A borrowed client's own HttpClient.Timeout remains an outer bound and is documented as such.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:337-337` · high · sha:da6000c93fc5</sub>
- A positive sub-millisecond timeout budget is clamped up to 1 ms so the behaviour is stated rather than accidental.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:339-340` · high · sha:da6000c93fc5</sub>
- A per-call deadline is a CancellationTokenSource created for the call, linked to the caller's token, armed with CancelAfter (or built over the pipeline's TimeProvider so tests control it), and disposed when the call ends (styleguide rule 13.8).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:512-515` · high · sha:da6000c93fc5</sub>
- A timeout is classified as the retryable failure of XCUT-2 rather than the terminal cancellation of XCUT-1 by the deadline source's own IsCancellationRequested checked while the caller's token is not signalled; the ambient token state, not an exception message, is the discriminator.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:515-518` · high · sha:da6000c93fc5</sub>
- Every cancellation classification tests the call's token (context.CancellationToken.IsCancellationRequested) and never `ex is OperationCanceledException`, satisfying XCUT-2's requirement that cancellation be told apart by ambient cancellation state and not by matching a message string; section 6.1 builds the retry classifier on it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:220-222` · high · sha:1608fcd4b329</sub>
- The retry policy, set-date policy and token cache each take a TimeProvider defaulting to TimeProvider.System, and tests drive a fake.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:322-324` · high · sha:ddf8f695ff61</sub>
- The sync path's blocking sleep named by CFG-15 must route through TimeProvider (a TimeProvider.CreateTimer signalling a ManualResetEventSlim, waited with the cancellation token) or a fake clock will not advance it, and it blocks only the caller's own thread; this resolves the CFG-15-versus-RETRY-26 tension as section 11 item 1.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:326-329` · high · sha:ddf8f695ff61</sub>
- Code must wait with Task.Delay only when the delay is greater than TimeSpan.Zero, because a negative delay computed from a past Retry-After date would otherwise hang the call forever instead of retrying at once; the as-built retry policy already guards this.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:333-336` · high · sha:ddf8f695ff61</sub>
- Thread.Interrupt, which injects ThreadInterruptedException into the next blocking wait on the target thread, is banned in SDK code by section 9.1's banned-API list alongside the ExecutionContext flow suppressors.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:346-348` · high · sha:ddf8f695ff61</sub>
- Deadlines are cooperative cancellation tokens throughout, observed at well-defined points.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:348-348` · high · sha:ddf8f695ff61</sub>
- Any SDK code that stops awaiting a task that can still produce a Response must attach a continuation that disposes a late result, per the orphaned-result rule of ASYNC-5 and TRANSPORT-9 (CFG-21's obligation in cooperative form).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:350-356` · high · sha:ddf8f695ff61</sub>

## Constraints
- When HttpClient.Timeout elapses, SendAsync throws a TaskCanceledException whose InnerException is a TimeoutException and which is an OperationCanceledException (verified), matching XCUT-2's edge case that the timeout branch must be checked first even when the timeout type is a subtype of the cancellation type.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:326-330` · high · sha:da6000c93fc5</sub>
- A per-call deadline implemented with CancelAfter on the caller's linked source is indistinguishable from caller cancellation (verified: it surfaces as a TaskCanceledException with no TimeoutException inside).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:332-334` · high · sha:da6000c93fc5</sub>
- Verified on .NET 10.0.401, HttpClient.Timeout surfaces as TaskCanceledException with an inner TimeoutException although nobody cancelled anything, whereas caller-token cancellation surfaces as TaskCanceledException with no TimeoutException anywhere in the chain, so "this exception is a cancellation" and "the call was cancelled" are different facts.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:217-220` · high · sha:1608fcd4b329</sub>
- Task.Delay(TimeSpan.FromMilliseconds(-1)) does not reject because -1 ms is Timeout.InfiniteTimeSpan and the task never completes (verified: not complete after 300 ms; -2 ms throws ArgumentOutOfRangeException; the TimeProvider overload behaves the same).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:331-334` · high · sha:ddf8f695ff61</sub>
- In .NET Core, Thread.Abort throws PlatformNotSupportedException (verified, surfaced as the inner exception of a TargetInvocationException), so the asynchronous-interrupt hazard of Ruby's Timeout.timeout and Thread#raise is removed by the runtime.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:342-346` · high · sha:ddf8f695ff61</sub>
- Task.WaitAsync(CancellationToken) abandons the awaited task on cancellation without cancelling or observing it, so a Response that completes afterwards is never disposed (verified: a disposable result delivered 50 ms after the WaitAsync was cancelled was not disposed by anyone).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:351-355` · high · sha:ddf8f695ff61</sub>

## Conclusions
- The blocking Execute gains a CancellationToken parameter relative to the as-built Execute(Request), because without it the blocking seam has no cancellation vehicle and SEAM-13 (blocking transports SHOULD honor cooperative cancellation during blocking I/O) is unanswerable.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:286-288` · high · sha:da6000c93fc5</sub>
- TRANSPORT-6's truncation hazard does not arise on .NET because CancelAfter treats zero as cancel-now rather than no deadline (verified: a 0.5 ms CancelAfter fired).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:337-340` · high · sha:da6000c93fc5</sub>
- Caller cancellation is rethrown untouched as OperationCanceledException carrying the caller's token rather than wrapped as TRANSPORT-3's non-retryable interrupt-shaped IOException, because the styleguide (docs/styleguide/csharp/08-error-handling.md) says cancellation is not an error to wrap; XCUT-1 sanctions the departure and it is recorded as design section 10 entry 8.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:382-388` · high · sha:da6000c93fc5</sub>
- ASYNC-20 and the second sentence of SEAM-16 (cancelling a completed success future does not close the delivered response) hold for free, because a Task cannot be cancelled after it completes and a token signalled after delivery touches nothing.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:458-460` · high · sha:da6000c93fc5</sub>
- The JVM interrupt-shaped clauses (ASYNC-3 two modes, ASYNC-4 poisoning protocol, SEAM-18 and ASYNC-14 restore the interrupt flag, CFG-20 interruptible task, SEAM-14(3) and XCUT-13 interrupt-safety) describe a mechanism .NET lacks, since nothing in the async world uses Thread.Interrupt and it cannot reach a thread-less await; .NET cancellation is the cooperatively observed CancellationToken surfaced as OperationCanceledException.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:498-506` · high · sha:da6000c93fc5</sub>
- Interrupt-clause intent maps as: aborting an in-flight exchange is cancelling the token the transport passes to SendAsync; not poisoning a pooled thread is vacuous because a token is per call; preserving the ambient cancellation flag is free because a token's state is immutable once signalled; this is recorded once as design section 10 entry 8 (P6).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:506-510` · high · sha:da6000c93fc5</sub>
- TRANSPORT-5's per-call scoping falls out free, since a derived CancellationTokenSource is inherently scoped to one call.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:521-522` · high · sha:da6000c93fc5</sub>
- RECOV-11's requirement to re-assert the cancellation signal before returning is free on .NET because a cancelled CancellationToken cannot be un-cancelled, so converting an OperationCanceledException into a Failure loses nothing.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:213-217` · high · sha:1608fcd4b329</sub>
- The time seam is the BCL TimeProvider: GetUtcNow() is the wall clock, GetTimestamp()/GetElapsedTime() the monotonic counter (CFG-16), and Task.Delay(TimeSpan, TimeProvider, CancellationToken) the cancellable non-blocking wait (CFG-18, ASYNC-18).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:318-322` · high · sha:ddf8f695ff61</sub>
- RETRY-26's non-pinning wait property is the runtime's default on the async path, because an awaiting continuation holds no thread.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:324-326` · high · sha:ddf8f695ff61</sub>
- CFG-17's re-assertion of cancellation status is free because a cancelled token cannot be un-cancelled, and CFG-19's wrapper unwrapping is free on await and GetAwaiter().GetResult() (only .Result/.Wait() produce AggregateException, which section 9.1 bans).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:336-340` · high · sha:ddf8f695ff61</sub>
- CFG-20's interruptible-task future (cancel-with-interrupt versus cancel-without) has no .NET counterpart, because cancellation is cooperative everywhere in the ecosystem and a future that interrupted a pool thread would reintroduce the hazard; the retirement of the interrupt mode is section 10 entry 8.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:348-357` · high · sha:ddf8f695ff61</sub>

## Reference
- SEAM-13 rationale is that cancellation must reach the network layer to free resources promptly; conformance is to interrupt a parked blocking send and assert it unwinds, and cancel an async future and assert the transport's cancel hook fires.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:26-26` · high · sha:8014d2ec2c9d</sub>
- The reference JVM pattern for XCUT-1 is to catch the interrupt, re-assert the interrupt state, and throw the I/O-family cancellation type.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:9-9` · high · sha:d6123be82c9e</sub>
- The two-mode cancellation MUSTs ASYNC-3 and ASYNC-4 share a cause with the retired interrupt mode and are argued in section 3.3 where the async adapter contract is.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:356-358` · high · sha:ddf8f695ff61</sub>
- As built (d45e64b) for the clock: TimeProvider is used throughout with a non-negative delay guard; missing are the sync-path wait, the banned-API gate, and the late-Response disposal rule as a shared helper.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:360-361` · high · sha:ddf8f695ff61</sub>

## Conflicts

## Superseded


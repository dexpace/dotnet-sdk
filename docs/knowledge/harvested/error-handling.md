# error-handling

## Rules
- XCUT-4 (MUST) A transport error MUST report itself always-retryable at the error level.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:15-15` · high · sha:d6123be82c9e</sub>
- XCUT-8 (MUST) The status-to-exception mapping factory MUST reject being asked to map a non-error status (1xx/2xx/3xx) by raising an argument error rather than fabricating a "successful exception".
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:19-19` · high · sha:d6123be82c9e</sub>
- XCUT-9 (MUST) Any classification walking an error's cause chain MUST be cycle-safe, tracking visited causes by reference identity and terminating on a self-referential or cyclic chain.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:20-20` · high · sha:d6123be82c9e</sub>
- The correctness-sensitive decisions are made once in Dexpace.Sdk.Core so that every transport adapter behaves identically.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:35-39` · high · sha:d7cea7b15cf3</sub>
- Error-to-exception mapping applies only to error statuses: a 304, or a 3xx that no redirect step followed, is returned with its body intact (BODY-31); the bounded error-body copy it needs is specified once in design section 5.1.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:236-239` · high · sha:da6000c93fc5</sub>
- Any failure that produced no response maps to a ServiceRequestException subtype that chains the native exception and reports itself retryable (TRANSPORT-20); a timeout maps to ServiceRequestTimeoutException.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:375-377` · high · sha:da6000c93fc5</sub>
- Encode and decode failures surface as SDK-owned exceptions that chain the codec's own exception (JsonException, NotSupportedException) and never leak it, while a genuine stream IOException propagates unwrapped (SEAM-23, SEAM-20, SEAM-21).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:592-595` · high · sha:da6000c93fc5</sub>
- The serde failure hierarchy requires a base serde failure with encode/decode subtypes that is open for codegen/adapters to add more specific subtypes (SEAM-23).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:595-597` · high · sha:da6000c93fc5</sub>
- Every orchestrator and every retry loop uses the single filter `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))`, where IsFatal is OutOfMemoryException and its subtypes.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:201-205` · high · sha:1608fcd4b329</sub>
- ThreadInterruptedException, the one .NET relic of JVM-style interruption, is classified as cancellation (section 6.1) and never as a retryable failure.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:211-212` · high · sha:1608fcd4b329</sub>
- The ExceptionTrail.AddSuppressed helper skips attaching an exception to itself (RETRY-34's self-suppression guard).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:246-247` · high · sha:1608fcd4b329</sub>
- XCUT-9 requires any cause walk to track visited causes by reference identity and terminate on a self-referential or cyclic chain.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:250-252` · high · sha:1608fcd4b329</sub>
- EnumerateCauses is bounded as well as cycle-safe with a depth cap of 64, because rule 9 of the house style bounds every loop.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:258-259` · high · sha:1608fcd4b329</sub>
- Exceptions are reserved for the unexpected, such as a violated caller precondition or a disk that vanished mid-write, and carry a type and a chained cause so the failure is diagnosable from the throw site.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:3-3` · high · sha:75ca031bcddf</sub>
- Routine, expected outcomes such as a parse that misses or a lookup that finds nothing are returned as values the signature admits, not thrown as exceptions for control flow.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:3-3` · high · sha:75ca031bcddf</sub>
- Throw a specific exception type and never throw bare Exception or ApplicationException (rule 8.1).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:36-36` · high · sha:75ca031bcddf</sub>
- The narrowest exception type that names the failure is chosen: ArgumentOutOfRangeException for a bad argument, InvalidOperationException for a bad state, a domain exception type for a domain rule.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:39-40` · high · sha:75ca031bcddf</sub>
- Catch only exception types the code can handle, and gate any broad catch with a when exception filter (rule 8.2).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:49-49` · high · sha:75ca031bcddf</sub>
- A bare catch (Exception) must be avoided because it also catches OutOfMemoryException and OperationCanceledException alongside the intended error.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:52-52` · high · sha:75ca031bcddf</sub>
- When a caught exception must be inspected before deciding, use an exception filter when(...) rather than catch-test-rethrow, because the filter runs before the stack unwinds and a non-match leaves the original stack and context intact.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:53-53` · high · sha:75ca031bcddf</sub>
- Rethrow a caught exception with a bare throw; and never with throw ex;, because throw ex; resets the stack trace to the current frame (rule 8.3).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:65-68` · high · sha:75ca031bcddf</sub>
- When wrapping a low-level failure in a higher-level exception, pass the original exception as the innerException argument so the cause chains; wrap to add context, never to hide the cause.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:69-69` · high · sha:75ca031bcddf</sub>
- Never swallow exceptions: no empty catch block and no catch-and-ignore (rule 8.4).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:81-81` · high · sha:75ca031bcddf</sub>
- Every caught exception must be handled by recovery, translation or surfacing, or not caught at all; swallowing is strictly worse than crashing.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:84-84` · high · sha:75ca031bcddf</sub>
- Catching an exception to log it and then continuing as if nothing failed counts as swallowing unless continuing is genuinely correct.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:85-85` · high · sha:75ca031bcddf</sub>
- The only sanctioned no-op catch is a documented one with a why-comment explaining why the failure is provably irrelevant.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:85-85` · high · sha:75ca031bcddf</sub>
- Validate preconditions with the ThrowIf family (ArgumentNullException.ThrowIfNull and ArgumentException/ArgumentOutOfRangeException.ThrowIf*), not with a defensive try/catch (rule 8.5).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:95-98` · high · sha:75ca031bcddf</sub>
- Wrapping work in try/catch to turn a NullReferenceException into a message is forbidden because it catches your own bug after the fact.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:98-98` · medium · sha:75ca031bcddf</sub>
- Custom exceptions derive directly from Exception, are sealed, carry strongly typed context properties set in the constructor instead of data concatenated into the message, and drop [Serializable] (rule 8.6).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:112-115` · high · sha:75ca031bcddf</sub>
- Custom exceptions provide the standard constructors, including a message overload and a (message, innerException) overload so cause chaining works.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:115-115` · high · sha:75ca031bcddf</sub>
- Custom exceptions must not be marked [Serializable] and must not implement the (SerializationInfo, StreamingContext) constructor, because binary serialization of exceptions is obsolete and BinaryFormatter is removed from .NET.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:116-116` · high · sha:75ca031bcddf</sub>
- A custom exception type is justified only when callers will catch that failure distinctly; otherwise a BCL exception type suffices.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:116-116` · high · sha:75ca031bcddf</sub>
- Return a Result or use the Try-pattern for expected, routine failures, and never throw for control flow (rule 8.7).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:131-131` · high · sha:75ca031bcddf</sub>
- The Result type is an opt-in Result<T, TError> modelled as a closed record hierarchy that the caller must pattern-match.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:134-134` · high · sha:75ca031bcddf</sub>
- The Try-pattern is exposed as bool TryX(out T value) with [NotNullWhen(true)] so the analyzer proves the out value non-null on success.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:134-134` · high · sha:75ca031bcddf</sub>
- Never throw to unwind a loop or signal an ordinary branch; exceptions are expensive to throw and invisible in the type.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:134-135` · high · sha:75ca031bcddf</sub>
- Choose one error dialect per module and hold it: a module that returns Result does not also throw for the same class of failure.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:135-135` · high · sha:75ca031bcddf</sub>
- Treat OperationCanceledException (and its TaskCanceledException subtype) as cooperative cancellation, not an error to log, alarm on or wrap (rule 8.8).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:153-156` · high · sha:75ca031bcddf</sub>
- Let OperationCanceledException propagate to the owner of the token, or catch it specifically only to perform cleanup and then rethrow.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:157-157` · high · sha:75ca031bcddf</sub>
- When a broad catch is necessary, exclude cancellation with a filter such as catch (Exception ex) when (ex is not OperationCanceledException).
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:157-157` · high · sha:75ca031bcddf</sub>
- Use specific exception types per domain, chain the original cause as InnerException, and attach context on rethrow.
  <sub>styleguide · `docs/styleguide/csharp/README.md:62-62` · high · sha:1e6ba36fc337</sub>
- Catch only what can be handled, never catch bare Exception without a when filter, and never write an empty catch.
  <sub>styleguide · `docs/styleguide/csharp/README.md:62-62` · high · sha:1e6ba36fc337</sub>
- Use opt-in Result<T, TError> where a failure is expected and routine, and never mix it with exceptions within a module.
  <sub>styleguide · `docs/styleguide/csharp/README.md:62-62` · high · sha:1e6ba36fc337</sub>

## Constraints
- XCUT-4 (MUST) The error taxonomy MUST have exactly two top-level branches: protocol errors carrying a fully-received response raised as an unchecked/runtime error, and transport errors carrying no response belonging to the runtime's I/O-error family so existing I/O catch sites keep matching.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:15-15` · high · sha:d6123be82c9e</sub>
- System.Runtime.Serialization.SerializationException also exists in the BCL, so a file importing both namespaces gets CS0104; the SDK keeps its specification-given names and qualifies them in its own code.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:598-601` · high · sha:da6000c93fc5</sub>
- .NET has no Throwable/Error split, so catch (Exception) also catches OutOfMemoryException, which RETRY-25 says MUST NOT be retried, classified retryable, or logged and MUST be surfaced unchanged with no suppressed-trail attachment; StackOverflowException cannot be caught (the runtime terminates the process) and AccessViolationException is not delivered to managed handlers on .NET Core.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:196-201` · high · sha:1608fcd4b329</sub>
- .NET has no suppressed-exception facility: Exception.InnerException is a single causal parent set once at construction, and AggregateException is a wrapper that would violate RECOV-10's requirement to rethrow the contained throwable UNCHANGED, so the only per-instance extension point is Exception.Data; this concerns RECOV-12, RETRY-34, PAGE-13, SSE-29 and others.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:238-242` · high · sha:1608fcd4b329</sub>
- Verified on .NET 10.0.401, Exception.Data accepts a non-serializable List<Exception> but a foreign exception's ToString() does not render it, so on a non-SDK primary the suppressed trail is retrievable but invisible to a naive log line.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:244-246` · high · sha:1608fcd4b329</sub>
- InnerException is get-only and assigned in the constructor from an already-constructed exception so no public API can close a cause cycle, though it can be closed by writing the private _innerException field through reflection (as a hostile or buggy deserializer would), verified on .NET 10.0.401.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:252-255` · high · sha:1608fcd4b329</sub>
- A discarded Task is a build error.
  <sub>styleguide · `docs/styleguide/csharp/README.md:62-62` · high · sha:1e6ba36fc337</sub>

## Conclusions
- The port keeps SdkException as the root and chains the native exception as InnerException instead of making the transport failure a subtype of IOException/HttpRequestException (TRANSPORT-20, XCUT-4), because HttpRequestException is not an IOException and C# has single inheritance; recorded as design section 10 entry 7.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:377-382` · high · sha:da6000c93fc5</sub>
- The design adds an unsealed SerdeException : SdkException and unseals SerializationException and DeserializationException beneath it, because as built they are sealed and derive directly from SdkException leaving no base to catch or extend.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:596-598` · high · sha:da6000c93fc5</sub>
- An exception filter runs before the stack unwinds, so a fatal exception failing the filter propagates with its original stack and no intermediate frame touches it, which is surfaced-unchanged in the most literal sense .NET offers.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:207-208` · high · sha:1608fcd4b329</sub>
- Section 10 entry 12 records that the port deviates from the letter of RECOV-2 ("catch EVERY throwable") by excluding a named fatal set for the reason RETRY-25 gives.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:209-211` · high · sha:1608fcd4b329</sub>
- The port has one ExceptionTrail.AddSuppressed(primary, secondary) helper: on an SdkException it appends to a Suppressed list the type exposes and renders in ToString(), and on any other exception it stores the list under a namespaced Data key readable via ExceptionTrail.GetSuppressed(Exception).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:242-246` · high · sha:1608fcd4b329</sub>
- The suppressed-exception limitation is the one place the port cannot match the reference's ergonomics and is recorded as section 10 entry 13.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:247-248` · high · sha:1608fcd4b329</sub>
- Core has one ExceptionFacts.EnumerateCauses(Exception) that walks InnerException and AggregateException.InnerExceptions breadth-first (the chain is a tree because of AggregateException) using HashSet<Exception>(ReferenceEqualityComparer.Instance), since an exception type may override Equals, and every classification in the port goes through it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:255-258` · high · sha:1608fcd4b329</sub>
- SERDE-10 (both serde exceptions share a common root) has a structural gap because SerializationException and DeserializationException both derive directly from SdkException, leaving the root of every SDK failure as their only catchable common root.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:306-309` · high · sha:68af5c6bf0ea</sub>
- An abstract SerdeException : SdkException between SdkException and the two serde exceptions restores SERDE-10 at no cost while the package is pre-1.0.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:306-310` · high · sha:68af5c6bf0ea</sub>
- The tension between OBS-20 ("The runtime does NOT defensively wrap tracer ... or metrics ... calls") and XCUT-20 (observability paths MUST NEVER throw into the caller's request path) is resolved by making the SDK's own recording code (tag computation, redaction) total while listener callbacks are the OBS-30 contract party and are not wrapped; this is section 11 item 37.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:99-104` · high · sha:ddf8f695ff61</sub>
- A precondition violation is treated as a bug in the caller rather than a runtime condition to recover from, so it is asserted at the top of the method with a ThrowIf helper that captures the parameter name via CallerArgumentExpression.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:98-99` · high · sha:75ca031bcddf</sub>
- Custom exceptions derive directly from Exception rather than SystemException or ApplicationException because the framework guidelines retired the three-deep SystemException/ApplicationException split.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:115-115` · high · sha:75ca031bcddf</sub>
- Result suits failures whose error carries structured detail the caller acts on, while the Try-pattern suits a plain present/absent answer on a hot path with no allocation.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:135-135` · high · sha:75ca031bcddf</sub>

## Reference
- A convenience form of the status-to-exception mapper MAY return an absent/null value for non-error statuses instead of raising.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:19-19` · high · sha:d6123be82c9e</sub>
- The decisions made once in core are idempotent-method classification (HTTP-9/RETRY-6), retryable-status classification (RETRY-1/XCUT-5/XCUT-7), body replayability (BODY-3/BODY-4), header-injection defence (HTTP-17 through HTTP-19/XCUT-18) and cancellation-versus-timeout classification (XCUT-1/XCUT-2).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:36-39` · high · sha:d7cea7b15cf3</sub>
- The as-built adapter wraps only exceptions matching the filter catch ... when (ex is JsonException or NotSupportedException), which lets stream IOException propagate.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:593-595` · high · sha:da6000c93fc5</sub>
- ApplicationException is a historical artifact that the framework guidelines advise against deriving from.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:40-40` · high · sha:75ca031bcddf</sub>
- Rule 8.1 is enforced by analyzer CA2201 (do not raise reserved exception types), and review rejects bare Exception and ApplicationException.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:47-47` · high · sha:75ca031bcddf</sub>
- Rule 8.2 is enforced by CA1031 (do not catch general exception types), and review requires a when filter on any broad catch.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:63-63` · high · sha:75ca031bcddf</sub>
- Rule 8.3 is enforced by CA2200 (rethrow to preserve stack details), and review requires innerException on every wrap.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:79-79` · high · sha:75ca031bcddf</sub>
- Rule 8.4 is enforced by CA1031, and review rejects any empty or commentless ignore-catch.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:93-93` · high · sha:75ca031bcddf</sub>
- The ThrowIf helpers run in every build, including release, because they are preconditions rather than Debug.Assert invariants.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:99-99` · high · sha:75ca031bcddf</sub>
- Rule 8.5 is enforced by CA1062 (validate public arguments), and review prefers ThrowIf over a defensive catch.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:110-110` · high · sha:75ca031bcddf</sub>
- Rule 8.6 is enforced by CA1032 (provide standard constructors) and CA1064 (exceptions should be public); CA2229 and CA2237 are no longer required since [Serializable] is dropped.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:129-129` · high · sha:75ca031bcddf</sub>
- Rule 8.7 is enforced by review, which forbids exceptions for expected failures and forbids mixing Result and throwing in one module, with [NotNullWhen] required on every Try method.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:151-151` · high · sha:75ca031bcddf</sub>
- Rule 8.8 is enforced by review, and a CA1031 broad-catch justification must exclude OperationCanceledException.
  <sub>styleguide · `docs/styleguide/csharp/08-error-handling.md:169-169` · high · sha:75ca031bcddf</sub>
- Chapter 08 covers specific exception types, no bare catch (Exception) without a filter, mandatory throw; and inner-exception chaining, ThrowIf helpers, opt-in Result for expected failures, and no control-flow exceptions.
  <sub>styleguide · `docs/styleguide/csharp/README.md:40-40` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded


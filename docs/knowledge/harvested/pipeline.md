# pipeline

## Rules
- SEAM-26 (MUST) - The operation-input projection MUST let generated code declare, per operation, an HTTP method, a path template with named placeholders, and typed projections of inputs onto path, query, header and body, so arguments flow through typed projections rather than URL string surgery.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:29-29` · high · sha:0adae2d6a47f</sub>
- SEAM-27 (MUST) - When assembled against a base URL, path-parameter values MUST be percent-encoded as single path segments so a value cannot inject extra "/", every placeholder MUST have a supplied value, and the query MUST be RFC-3986 rendered.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>
- Base-URL composition MUST follow fixed rules - a trailing slash normalizes to one separator, an empty path leaves the base untouched, an existing base query is preserved with the operation query appended after it, and a base carrying a fragment or resolving to a malformed URL is rejected with a context-bearing error (SEAM-27).
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>
- PIPE-1 (MUST): Steps MUST execute in a single fixed total order derived from stage assignment, where a step in a lower-ordered stage runs before (wraps) a step in a higher-ordered stage on the inbound path and observes the response later on the outbound path, independent of insertion order across stages.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:9-9` · high · sha:33e9443472ce</sub>
- PIPE-1 (MUST): The order of steps within one non-pillar stage follows insertion order (PIPE-7).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:9-9` · high · sha:33e9443472ce</sub>
- PIPE-2 (MUST): The runtime MUST preserve the pillar precedence chain REDIRECT, RETRY, AUTH, LOGGING, SERDE (outer to inner), plus an outermost pre-redirect slot outside both loops and a terminal SEND hop innermost.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:10-10` · high · sha:33e9443472ce</sub>
- PIPE-3 (SHOULD): The stage list SHOULD interleave user-extensible slots (a pre and a post slot) around each pillar and SHOULD use sparse numeric order keys so new stages can be inserted without renumbering.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:11-11` · high · sha:33e9443472ce</sub>
- PIPE-4 (MUST): A pillar stage MUST admit at most one step; the configurable pillars are REDIRECT, RETRY, AUTH, LOGGING, and SERDE.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:12-12` · high · sha:33e9443472ce</sub>
- PIPE-5 (MUST): Installing a DISTINCT second step onto an occupied pillar, via any add or a bulk reload, MUST fail fast naming both step types and pointing at the replace path, rather than silently overwriting.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:12-12` · high · sha:33e9443472ce</sub>
- PIPE-6 (MUST): Re-installing the SAME step onto its pillar MUST be idempotent, with sameness distinguished by reference identity, not value equality.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:12-12` · high · sha:33e9443472ce</sub>
- PIPE-8 (MUST): The terminal SEND stage MUST be reserved for the transport hop, MUST NOT hold a user step, and flattening MUST skip it.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:12-12` · high · sha:33e9443472ce</sub>
- PIPE-9 (MUST): An empty pipeline MUST dispatch directly to the terminal transport, threading the caller's per-call options, and SHOULD do so without allocating per-call cursor state.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:16-16` · high · sha:33e9443472ce</sub>
- PIPE-10 (MUST): The built pipeline runtime MUST be immutable after construction, and each send MUST allocate its own per-call cursor so concurrent calls share no mutable pipeline state.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:16-16` · high · sha:33e9443472ce</sub>
- PIPE-11 (MUST): Steps MUST be safe for concurrent invocation, with per-request mutable state living in the per-call cursor, never on the step.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:16-16` · high · sha:33e9443472ce</sub>
- PIPE-12 (MUST): Each step MUST be bidirectional: it receives the inbound request, MAY invoke the rest of the chain, and MAY inspect or substitute the outbound response, and it MAY short-circuit by returning a synthetic response without invoking the chain.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:17-17` · high · sha:33e9443472ce</sub>
- PIPE-13 (MUST): Invoking the next step MUST advance a monotonic cursor and invoke it; when the steps are exhausted it MUST dispatch the current in-flight request to the terminal transport, threading the caller's per-call options.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:17-17` · high · sha:33e9443472ce</sub>
- PIPE-14 (MUST): A substituted request MUST propagate to every downstream step and the terminal dispatch (it sticks).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:17-17` · high · sha:33e9443472ce</sub>
- PIPE-15 (MUST): A step that drives the downstream chain more than once (retry re-attempting, redirect following a hop, auth retrying after a challenge) MUST fork a fresh cursor for each re-drive rather than reusing the same next handle.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:18-18` · high · sha:33e9443472ce</sub>
- PIPE-15 (MUST): Reusing the next handle resumes past already-visited steps and MUST be treated as a defect; a port MUST provide an equivalent fork primitive and its wrapping pillar steps MUST use it.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:18-18` · high · sha:33e9443472ce</sub>
- PIPE-16 (MUST): A forked cursor MUST resume from the SAME position as its parent, carry the current in-flight request, and share the immutable options, with forks advancing independently.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:18-18` · high · sha:33e9443472ce</sub>
- PIPE-17 (MUST): The caller's per-call options MUST be carried unchanged for the entire call, including across every re-drive fork, readable by any step and threaded into the terminal dispatch.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:19-19` · high · sha:33e9443472ce</sub>
- PIPE-17 (MUST): Per-call options MUST be immutable and shared, not copied-and-diverged per fork.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:19-19` · high · sha:33e9443472ce</sub>
- PIPE-40 (MUST): A wrapping step that re-drives the chain MUST release each superseded intermediate response, closing its body before the next drive, and MUST NOT close the response it ultimately hands back to the caller because close-responsibility passes outward.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:20-20` · high · sha:33e9443472ce</sub>
- PIPE-40 (MUST): On paths that abandon a re-drive (redirect cycle, non-replayable body, budget exhausted) the in-flight response MUST be returned unclosed.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:20-20` · high · sha:33e9443472ce</sub>
- PIPE-7 (MUST): Non-pillar stages MUST hold an ordered sequence in which append adds to the tail and prepend adds to the head, preserving relative order through build and any re-bucketing edit.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:24-24` · high · sha:33e9443472ce</sub>
- PIPE-18 / PIPE-19 (MUST): The surgical insert-after, insert-before and replace edits MUST act relative to the FIRST existing instance of an anchor type, and the inserted or replacing step MUST declare the same stage as the anchor.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:24-24` · high · sha:33e9443472ce</sub>
- PIPE-18 / PIPE-19 (MUST): A cross-stage insert or replace MUST be rejected.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:24-24` · high · sha:33e9443472ce</sub>
- PIPE-20 (MUST): Remove MUST delete EVERY instance of a type, preserving relative order, and MUST be a no-op when the type is absent.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:24-24` · high · sha:33e9443472ce</sub>
- PIPE-21 (MUST): An insert-relative or replace edit whose anchor type is absent MUST fail identifying the missing type.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:24-24` · high · sha:33e9443472ce</sub>
- PIPE-22 (MUST): Every mutation that re-buckets by stage MUST re-derive the flattened order deterministically, so the observable ordering after an edit equals building the same set from scratch.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:25-25` · high · sha:33e9443472ce</sub>
- PIPE-23 (MUST): A bulk reload MUST be all-or-nothing: a pillar collision leaves the existing collection completely unchanged rather than a partial rebuild.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:25-25` · high · sha:33e9443472ce</sub>
- PIPE-24 (MUST): The standard-resilience preset MUST install into EMPTY slots only, validating up front that no target pillar is occupied and rejecting the whole call (installing nothing) if any is.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:25-25` · high · sha:33e9443472ce</sub>
- PIPE-25 (MUST): build() MUST produce the ordered sequence by flattening stages in declaration order (skipping SEND) into an immutable runtime that exposes a read-only, ordered view of its steps.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:26-26` · high · sha:33e9443472ce</sub>
- PIPE-38 (MUST): Append-all MUST preserve the batch's iteration order within a stage, while prepend-all (each element prepended individually) results in the REVERSED batch order, and a port MUST document this asymmetry.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:26-26` · high · sha:33e9443472ce</sub>
- PIPE-36 (SHOULD): The shipped pillar families SHOULD lock their stage assignment so a subclass cannot relocate out of its pillar.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:26-26` · high · sha:33e9443472ce</sub>
- PIPE-37 (MUST): A step whose correctness depends on the SINGLE terminal response (for example status-to-typed-error mapping) MUST occupy the outermost pre-redirect slot so it runs outside both loops, and on a non-error status MUST return the response untouched.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:26-26` · high · sha:33e9443472ce</sub>
- PIPE-26 (MUST): The pipeline runtime MUST itself implement the transport SPI, delegating execute and execute-async to its own send and send-async (with and without options), so a configured pipeline can stand in wherever a transport is expected and options survive the indirection.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:30-30` · high · sha:33e9443472ce</sub>
- PIPE-27 (MUST): Closing the pipeline MUST be a no-op with respect to the underlying transport; the pipeline never owns its transport and MUST NOT close it.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:30-30` · high · sha:33e9443472ce</sub>
- PIPE-39 (SHOULD): The runtime SHOULD offer convenience constructors for a step-less pipeline forwarding directly to a transport and for a standard pipeline installing the default resilience pillars (sync: redirect+retry+instrumentation; async: retry+instrumentation with a caller-supplied scheduler for non-blocking backoff).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:31-31` · high · sha:33e9443472ce</sub>
- PIPE-28 (MUST): The async pipeline runtime MUST reuse the identical stage identities and staging policy as the sync runtime; the two MUST NOT each re-derive ordering independently.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:35-35` · high · sha:33e9443472ce</sub>
- PIPE-29 (MUST): An async step MUST NOT throw synchronously to signal a transport or async failure; it MUST return a future completing exceptionally, and MAY throw synchronously only for caller-bug argument validation.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:35-35` · high · sha:33e9443472ce</sub>
- PIPE-30 (MUST): The async runtime MUST defensively normalize ANY synchronous exception from a step's async entry point (or the empty-pipeline dispatch) into an exceptionally-completed future, while fatal or unrecoverable errors propagate synchronously and MUST NOT be swallowed.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:35-35` · high · sha:33e9443472ce</sub>
- PIPE-31 (MUST): The async terminal response-mapping operator MUST, on success, apply the handler then close the response (tolerating idempotent double-close).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:36-36` · high · sha:33e9443472ce</sub>
- PIPE-31 (MUST): On failure the async terminal response-mapping operator MUST unwrap async-wrapper exceptions to the original cause and MUST close any response accompanying a failure to avoid leaking the body.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:36-36` · high · sha:33e9443472ce</sub>
- PIPE-32 (MUST): The async standard pipeline MUST NOT follow HTTP redirects at the pipeline layer (there is no async redirect pillar), so a 3xx surfaces verbatim unless redirect following is enabled on the transport.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:36-36` · high · sha:33e9443472ce</sub>
- PIPE-32 (MUST): A port MUST document the async-versus-sync standard pipeline redirect asymmetry.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:36-36` · high · sha:33e9443472ce</sub>
- PIPE-33 (MUST): The sync-to-async bridge MUST require a caller-supplied executor (no default), run the wrapped synchronous pipeline as a single opaque unit on that executor (so its steps stay synchronous on the worker and do NOT gain per-step concurrency), and thread per-call options into the wrapped send.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:40-40` · high · sha:33e9443472ce</sub>
- PIPE-33 (MUST): In the sync-to-async bridge, cancelling the future with interruption MUST interrupt the worker running the in-flight send, and cancelling without interruption MUST complete as cancelled without interrupting.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:40-40` · high · sha:33e9443472ce</sub>
- PIPE-34 (MUST): The async-to-sync bridge MUST block on the async result per call while preserving options and MUST honor thread interruption, which on interrupt means restoring the flag, cancelling the in-flight future, and surfacing an interrupted-I/O error.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:40-40` · high · sha:33e9443472ce</sub>
- PIPE-35 (SHOULD): The pipeline builder SHOULD provide two unambiguous ways to seed from an existing pipeline: FLATTEN (copy its steps and transport so they run in the same loops) versus NEST (treat it as an opaque transport so the new steps run once, OUTSIDE the nested loops).
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:41-41` · high · sha:33e9443472ce</sub>
- PIPE-35 (SHOULD): A port MUST make the flatten-versus-nest choice explicit rather than accidental.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:41-41` · high · sha:33e9443472ce</sub>
- A port MUST NOT collapse the stage pipeline and the recovery chain into one layer: the stage pipeline owns ordering and re-drive-with-fork, and the recovery chain owns the sum-type fold and the uniform-failure guarantee.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:59-59` · high · sha:33e9443472ce</sub>
- HttpPipeline implements both IHttpClient and IAsyncHttpClient so a configured pipeline can stand in wherever a transport is expected (PIPE-26).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:263-264` · high · sha:da6000c93fc5</sub>
- An unparseable or absent inbound Content-Type SHOULD be downgraded to 'no media type' (TRANSPORT-27).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:369-370` · high · sha:da6000c93fc5</sub>
- The adapter parses inbound Content-Type with MediaType.TryParse, maps failure to null, and wraps all response adaptation in a guard that disposes the HttpResponseMessage on any exception.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:371-372` · high · sha:da6000c93fc5</sub>
- Inbound header values take HTTP-19's lenient path, and a header that fails even that is dropped individually (TRANSPORT-14).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:372-373` · high · sha:da6000c93fc5</sub>
- The operation-input projection seam requires a per-operation declaration of method, path template with named placeholders, and typed path/query/header/body projections, with the body carried rather than encoded (SEAM-26).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:632-633` · high · sha:da6000c93fc5</sub>
- Path parameters are percent-encoded as single segments so a value cannot inject '/', placeholder values are mandatory, queries render per RFC 3986, and base-URL composition normalises trailing slashes, treats an empty path as a no-op, preserves the base query with the operation query appended, and rejects a base with a fragment or a malformed URL with a context-bearing error (SEAM-27).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:633-636` · high · sha:da6000c93fc5</sub>
- The optional operation id (SEAM-28) rides on the descriptor and is handed to the pipeline context, never to the URL.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:640-641` · high · sha:da6000c93fc5</sub>
- The projection rejects a path-parameter value that is exactly "." or ".." with an ArgumentException naming the placeholder; the specification is silent on this and the resolution is recorded as section 11 item 35.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:668-670` · high · sha:da6000c93fc5</sub>
- SEAM-27's base composition rules (exactly one separator, keep the base query and append the operation's) are hand-coded over the base's components and never delegated to new Uri(base, relative).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:678-680` · high · sha:da6000c93fc5</sub>
- A port MUST NOT collapse the stage pipeline and the recovery chain into one layer, because the stage pipeline owns ordering and re-drive-with-fork while the recovery chain owns the sum-type fold and the uniform-failure guarantee.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:3-5` · high · sha:1608fcd4b329</sub>
- Policies are sealed unless designed for extension, which is how PIPE-36's stage lock is enforced: AuthorizationPolicy seals Stage and every shipped pillar class is sealed.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:34-35` · high · sha:1608fcd4b329</sub>
- A pillar admits exactly one policy (PIPE-4).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:58-58` · high · sha:1608fcd4b329</sub>
- A distinct second policy on an occupied pillar must fail fast naming both step types and pointing at the replace path (PIPE-5); the port checks at Add, where the stage is known, and names both runtime types and Replace<T>, whereas as built the check happens only at Build and names the stage and a count.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:58-61` · high · sha:1608fcd4b329</sub>
- Re-adding the same policy instance is idempotent (PIPE-6) and is decided with ReferenceEquals, because a policy written as a record would otherwise compare by value and swallow a genuine collision.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:61-64` · high · sha:1608fcd4b329</sub>
- Remove<T> deletes every instance of the policy type (PIPE-20) and a missing anchor names the type (PIPE-21); both are already built.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:64-65` · high · sha:1608fcd4b329</sub>
- The builder edit surface requires Prepend and AddRange/PrependRange with PIPE-38's documented asymmetry, where append-all keeps batch order and prepend-all reverses it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:65-67` · high · sha:1608fcd4b329</sub>
- InsertBefore<T> of a policy from another stage must be rejected (PIPE-18/PIPE-19) rather than silently re-bucketed by the stable sort as built.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:66-68` · high · sha:1608fcd4b329</sub>
- The builder needs an all-or-nothing bulk reload (PIPE-23) and an empty-slots-only resilience preset (PIPE-24) that installs into an existing builder, which DexpacePipeline.CreateDefault as built does not offer since it builds from scratch.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:68-70` · high · sha:1608fcd4b329</sub>
- Build is a stable sort by stage into an array (PIPE-22, PIPE-25) and the port also exposes it as an IReadOnlyList<HttpPipelinePolicy> view, which PIPE-25 requires.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:70-71` · high · sha:1608fcd4b329</sub>
- Because Request is an immutable record passed as an argument, a substituted request sticks downstream because it is what the step passes on (PIPE-14), a re-drive carries exactly the request the pillar holds and never a downstream mutation (PIPE-16, RETRY-44), and a short-circuit is returning a synthetic Response (PIPE-12).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:107-111` · high · sha:1608fcd4b329</sub>
- PipelineContext shrinks to call-scoped read-mostly state (options, the call's CancellationToken, the seed request, the correlation context, the active Activity, a call-scoped property bag) plus two per-drive values, the attempt ordinal and hop count, which a pillar sets by passing context.ForAttempt(n), a copy sharing every call-scoped reference.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:112-115` · high · sha:1608fcd4b329</sub>
- Options are carried by reference, unchanged, across every fork (PIPE-17), which obliges the options type to be immutable (section 8.2); the as-built DexpaceClientOptions is a class with public setters shared by reference into in-flight calls and does not yet meet it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:115-118` · high · sha:1608fcd4b329</sub>
- The idempotency step (RECOV-32) stamps its header only for methods in the configured set (default POST, PUT, PATCH); in the default respect-existing mode a request already carrying the header is left alone and the key strategy is not invoked, and the strategy is invoked at most once per applicable request.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:133-136` · high · sha:1608fcd4b329</sub>
- The client-identity step (RECOV-33) joins configured tokens into one space-separated line, appends after the first existing value by default or replaces in Replace mode, and emits nothing for a blank line.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:138-140` · high · sha:1608fcd4b329</sub>
- The error-mapping step (RECOV-15) maps only statuses 400..599 and returns 1xx, 2xx and 3xx unchanged so that a 304 or an unfollowed 3xx keeps its body (BODY-31), and its factory rejects a non-error status (XCUT-8).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:141-143` · high · sha:1608fcd4b329</sub>
- Response.EnsureSuccessAsync must test Status.IsClientError || Status.IsServerError rather than treating only 2xx as success, because the as-built version (if (IsSuccess) return; with IsSuccess meaning 2xx) turns a 304 into an exception and violates RECOV-15 and BODY-31.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:148-150` · high · sha:1608fcd4b329</sub>
- RECOV-16 requires the error body to be buffered into a bounded, replayable in-memory copy capped at a fixed maximum (1 MiB / MAX_BUFFERED_ERROR_BODY_BYTES), a hard truncation with no marker, with the same bound on every buffering path.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:152-154` · high · sha:1608fcd4b329</sub>
- Because error-body truncation is markerless, the decoding witness HttpResponseException.GetErrorAsync<T> must fail into a typed DeserializationException on a structurally incomplete payload and never assume well-formedness.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:160-162` · high · sha:1608fcd4b329</sub>
- PIPE-40's lifecycle rule requires closing every superseded intermediate response before the next drive, never closing the one handed back, and returning the in-flight response unclosed on an abandoned re-drive; this stays with the re-driving policy, the only code that knows which response is superseded.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:164-166` · high · sha:1608fcd4b329</sub>
- With responses flowing as return values, a superseded response is a local the policy is about to overwrite, and an await using wrapped around the response a policy returns is wrong for that returned response.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:166-168` · high · sha:1608fcd4b329</sub>
- There is one PipelineStage enum and one sorted array, and both Run and RunAsync index into it, so the sync and async pipelines do not each re-derive ordering independently (PIPE-28).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:266-268` · high · sha:1608fcd4b329</sub>
- PIPE-31's terminal mapping is SendAsync<T>(request, Func<Response, CancellationToken, ValueTask<T>> handler, ...), which applies the handler inside await using over the response (disposal is idempotent) and unwraps nothing, since await surfaces the original exception instead of an AggregateException.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:291-295` · high · sha:1608fcd4b329</sub>
- The built pipeline MUST itself implement the transport SPI (PIPE-26): HttpPipeline implements IAsyncHttpClient and IHttpClient, threading the seam's per-call options (section 3.2) into the pipeline's context, which makes Nest trivial.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:327-330` · high · sha:1608fcd4b329</sub>
- HttpPipeline's Dispose is a no-op toward the transport it did not create (PIPE-27).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:329-330` · high · sha:1608fcd4b329</sub>

## Constraints
- The stage-based pipeline and the recovery-chain layer share one backoff calculator and one pacing-header parser so their retry behavior cannot drift.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:3-3` · high · sha:33e9443472ce</sub>
- PIPE-13 (MUST): The cursor MUST only move forward within a single un-forked drive.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:17-17` · high · sha:33e9443472ce</sub>
- HttpClient accepts an inbound Content-Type with a parameter lacking '=' (e.g. text/plain; foo) while MediaType.Parse rejects it; with the as-built adapter ExecuteAsync then throws ArgumentException after the HttpResponseMessage is live and nothing disposes it (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:366-369` · high · sha:da6000c93fc5</sub>
- A built pipeline is an immutable array plus a transport so concurrent calls share nothing mutable (PIPE-10, PIPE-11), and the empty pipeline dispatches straight to the transport (PIPE-9) with one context allocation, which the SHOULD half of that requirement tolerates.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:118-120` · high · sha:1608fcd4b329</sub>
- Verified on .NET 10.0.401, EnsureSuccessStatusCode throws for anything outside 200-299 and a bare 304 raises HttpRequestException, so the obvious tool maps the wrong range (P13).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:146-148` · high · sha:1608fcd4b329</sub>
- Verified on .NET 10.0.401, a non-async override returning a ValueTask directly that throws before returning throws synchronously when called directly, but awaiting it from inside an async runner yields a faulted task; because PipelineRunner.RunAsync is async the runtime normalises every policy's synchronous throw into a faulted task (PIPE-30) by construction.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:285-289` · high · sha:1608fcd4b329</sub>
- Fatal exceptions are captured into the faulted task as well, so .NET cannot honour "propagate synchronously" for them, and the section 5.2 filter, not this normalisation, is where fatal errors are kept out of retry and logging.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:289-291` · high · sha:1608fcd4b329</sub>
- The obvious _value ??= await ParseAsync() re-runs the handler on a legitimate null and re-reads an already-consumed single-use body, which is Ruby's @value ||= load gotcha in C#.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:391-393` · high · sha:68af5c6bf0ea</sub>

## Conclusions
- The stage-based pipeline is the composition surface for assembling a client: redirect, retry, auth, logging/instrumentation and serialization concerns are ordered as pillar steps there, per-call cursors and forks drive re-attempts, a configured pipeline becomes a transport others can nest, and it has a real async mirror.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:57-57` · high · sha:33e9443472ce</sub>
- Azure.Core is the closest prior art to this SDK, and its HttpPipelinePolicy/HttpPipeline shape is the one the as-built pipeline deliberately follows.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:8-11` · high · sha:d7cea7b15cf3</sub>
- The pipeline's internal policy chain uses ValueTask where steps often complete synchronously, while the SPI boundary does not.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:439-440` · high · sha:da6000c93fc5</sub>
- The .NET operation projection is a sealed record OperationDescriptor with Method, a template string and four projection lists each defaulting to empty, plus one OperationDescriptor.BuildRequest(Uri baseAddress, ...) in core; no code generation is implied and only the runtime primitive a generator would target is specified.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:638-641` · high · sha:da6000c93fc5</sub>
- In the .NET port the asynchronous path is primary and the synchronous path is a real one (HttpClient.Send, Stream.Read) rather than a blocking wrapper, so the sync mirror of the stage pipeline and both bridges are kept, with the mirror direction reversed relative to the reference so that async is the runtime every other shape derives from.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:5-8` · high · sha:1608fcd4b329</sub>
- The as-built pipeline made PipelineContext a single mutable carrier with a runner re-entrant from a fixed index, so no per-attempt state cloning is needed; the re-entrant runner is kept as the best idea of the as-built pipeline, but the shared mutable carrier is overturned because it is exactly the state a fork is supposed to isolate.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:10-15` · high · sha:1608fcd4b329</sub>
- DelegatingHandler is not used as the policy shape because it lives below the transport seam on HttpRequestMessage, and the connection-layer split deliberately leaves it there so that an enterprise's handler chain composes underneath the SDK.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:26-30` · high · sha:1608fcd4b329</sub>
- A pipeline step is an abstract class (HttpPipelinePolicy with a stage, a Process/ProcessAsync pair and an explicit next), not a delegate, matching the shape of Azure.Core's HttpPipelinePolicy and System.ClientModel.Primitives.PipelinePolicy without taking either package.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:26-34` · high · sha:1608fcd4b329</sub>
- Neither Azure.Core nor System.ClientModel is adopted as a dependency because both are installed from NuGet and so neither earns a P2 pass.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:32-34` · high · sha:1608fcd4b329</sub>
- The as-built stage PerCall was renamed PerHop because it runs once per redirect hop rather than once per call, and its doc comment promising once-per-logical-call misleads policy authors into minting per-call state that is minted once per hop.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:46-56` · high · sha:1608fcd4b329</sub>
- The Serde = 700 stage is reserved with no shipped policy because the specification reserves it, so it is not a fabricated slot (P11).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:51-51` · high · sha:1608fcd4b329</sub>
- SEND is not a PipelineStage member because the transport is the fixed terminal captured by Build, so PIPE-8's requirement that SEND must not hold a user step is enforced by the type system rather than by a check.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:53-54` · high · sha:1608fcd4b329</sub>
- The as-built PipelineRunner is a readonly struct holding the policy array, an immutable index and the transport, and RunAsync constructs the next runner rather than advancing itself, so calling it twice forks naturally and both calls resume from the same position (PIPE-16) with no advanced handle to misuse, unlike the reference's mutable JVM cursor (PIPE-15).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:73-78` · high · sha:1608fcd4b329</sub>
- The fix for the fork defect is to stop sharing the request rather than clone the context: policies take the Request as an argument and return ValueTask<Response> (the DelegatingHandler.SendAsync shape), via HttpPipelinePolicy.Stage, abstract ProcessAsync(Request, PipelineContext, PipelineRunner next), virtual Process, and PipelineRunner.RunAsync/Run(Request, PipelineContext).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:89-105` · high · sha:1608fcd4b329</sub>
- The as-built PipelineAbortedException for a chain that completed without producing a response disappears because a method returning ValueTask<Response> cannot complete without one.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:111-112` · high · sha:1608fcd4b329</sub>
- Cursor-scoped (forked-cursor) state, which Ruby needed to carry the cross-origin redirect marker, is not built in the .NET port because the marker is removed: the seed request is fixed when the call starts and both the redirect and auth pillars compare against it, leaving nothing a pillar must hand privately to its downstream (P11).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:122-127` · high · sha:1608fcd4b329</sub>
- HttpPipeline.SendAsync returns the Response for any status with a caller-invoked Response.EnsureSuccessAsync as the default (the .NET idiom, like HttpResponseMessage.EnsureSuccessStatusCode), and the port additionally ships an ErrorMappingPolicy at PerCall for callers wanting throw-by-default, the placement PIPE-37 requires.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:143-146` · high · sha:1608fcd4b329</sub>
- The port moves the error-body drain into one internal ErrorBodyBuffer.CaptureAsync(Response, CancellationToken), shared with the retry stack's re-classification of a re-sent error (RETRY-36), renting its chunk from ArrayPool<byte>.Shared instead of allocating 80 KiB per call (styleguide 13.6), and disposing the original Response rather than only its stream.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:156-160` · high · sha:1608fcd4b329</sub>
- PIPE-29 and PIPE-30 are nearly free on .NET because an async method never throws synchronously and captures any exception, including an argument check, into the returned task.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:283-285` · high · sha:1608fcd4b329</sub>
- PIPE-32 is reversed in the port: both the async and sync standard pipelines install RedirectPolicy, rather than the specification's rule that the async standard pipeline MUST NOT follow redirects at the pipeline layer, because a sync/async behavioural split would be perverse when the async pipeline is primary and the redirect policy is already async and would contradict the single-implementation rule.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:297-302` · high · sha:1608fcd4b329</sub>
- PIPE-35's flatten-versus-nest choice is two named factories, PipelineBuilder.Flatten(pipeline) and PipelineBuilder.Nest(pipeline), never one overload.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:326-327` · high · sha:1608fcd4b329</sub>
- SERDE-27: ReadValueAsync<T> streams through DeserializeAsync without materializing the body and disposes the body stream on every path.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:385-386` · high · sha:68af5c6bf0ea</sub>
- SERDE-28's status-aware handler is the pipeline's error path: HttpResponseException.GetErrorAsync<T> reads the bounded, buffered error body the pipeline captured before throwing.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:387-389` · high · sha:68af5c6bf0ea</sub>
- The lazy typed-response wrapper of HTTP-44 and HTTP-45 is not built; its .NET shape is Lazy<Task<T>> in ExecutionAndPublication mode, which memoizes a null success and a faulted task alike.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:389-391` · high · sha:68af5c6bf0ea</sub>
- The lazy wrapper's lock is held only while the factory starts the task, so concurrent first callers await the same task rather than block, satisfying HTTP-45's non-pinning clause via the primitive.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:393-396` · high · sha:68af5c6bf0ea</sub>

## Reference
- Only method and path template are required in the operation-input projection; the four projections default to empty, and the body is carried, not encoded, by this seam.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:29-29` · high · sha:0adae2d6a47f</sub>
- SEAM-26 conformance is that a parameterless GET overriding only method and path assembles the right request, and inputs placed via projections appear in the correct request part.
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:29-29` · high · sha:0adae2d6a47f</sub>
- SEAM-27 rationale is that these invariants make typed projection safe against path injection and support signed/SAS bases; conformance examples are that a path value containing "/" is encoded not split, a missing placeholder throws, and "host/c?sig=.." plus "/pets" yields "host/c/pets?sig=..&<opquery>".
  <sub>spec · `docs/product-spec/03-pluggable-seams-and-extension-model.md:30-30` · high · sha:0adae2d6a47f</sub>
- The SDK has two cooperating pipeline layers: a stage-based pipeline that is the user-facing dispatch runtime, and recovery-chain primitives that form the resilience layer beneath resilience steps.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:3-3` · high · sha:33e9443472ce</sub>
- In the stage-based pipeline, cross-cutting concerns become discrete bidirectional steps assigned to a fixed, totally-ordered list of named stages, and a request flows inbound to a terminal transport and back outbound in reverse.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:3-3` · high · sha:33e9443472ce</sub>
- PIPE-1 conformance: one probe step per stage records entry and exit; entry matches the stage list and exit is its exact reverse, regardless of insertion order.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:9-9` · high · sha:33e9443472ce</sub>
- A step's placement relative to the pillar boundaries determines whether it sees per-hop/per-attempt responses or only the single terminal response, and SERDE is a reserved slot with no shipped behavior.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:10-10` · high · sha:33e9443472ce</sub>
- PIPE-2 conformance: with a redirecting and retrying transport, a pre-redirect step is invoked once with the final response, and an auth step re-runs per redirect hop.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:10-10` · high · sha:33e9443472ce</sub>
- PIPE-15/16 conformance: a retry step re-driving twice via the fork visits every downstream step on both attempts, whereas reusing the handle skips downstream steps on the second attempt.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:18-18` · high · sha:33e9443472ce</sub>
- PIPE-40 is the response-lifecycle corollary of PIPE-15 and a frequent porter trap; its conformance check is that a 2-hop redirect closes each intermediate body exactly once while the final response reaches the caller open.
  <sub>spec · `docs/product-spec/08-execution-pipelines.md:20-20` · high · sha:33e9443472ce</sub>
- The as-built HttpPipeline converts a null transport result through PipelineAbortedException; under section 5.1's request-in, response-out signature that exception's other use disappears and only the null check remains.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:448-450` · high · sha:da6000c93fc5</sub>
- As built (d45e64b), the operation-input projection seam is not built; pagination carries its own internal query-pair reader and the URL redactor its own parser, neither being the projection seam.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:687-688` · high · sha:da6000c93fc5</sub>
- The specification fixes the stage order PRE_REDIRECT, REDIRECT, RETRY, AUTH, LOGGING, SERDE, SEND (PIPE-2) with user slots around each pillar and sparse keys (PIPE-3).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:37-38` · high · sha:1608fcd4b329</sub>
- The as-built stage enum is sparse and correctly ordered but misnames one slot and omits two.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:38-39` · high · sha:1608fcd4b329</sub>
- The port's PipelineStage enum has Operation = 100 (outside both loops, a pillar, a .NET addition for the once-per-call deadline and operation span), PerCall = 150 (PRE_REDIRECT slot, non-pillar, new, home of the error-mapping step per PIPE-37, runs once and sees the final response), Redirect = 200 (pillar), PerHop = 250 (post-redirect/pre-retry, non-pillar, renamed from the as-built PerCall), Retry = 300 (pillar), PerAttempt = 400 (post-retry/pre-auth, non-pillar, fresh Date and per-attempt headers), Auth = 500 (pillar), Diagnostics = 600 (LOGGING pillar, per-attempt span, metrics, log events), Serde = 700 (pillar, new, reserved, no shipped policy).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:41-51` · high · sha:1608fcd4b329</sub>
- As built, adding one policy instance twice throws at Build, contrary to PIPE-6.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:62-64` · high · sha:1608fcd4b329</sub>
- PIPE-15 states that reusing the pipeline handle resumes past already-visited steps and MUST be treated as a defect.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:73-75` · high · sha:1608fcd4b329</sub>
- PIPE-16 asks for three things of a fork: the same position, carrying the current in-flight request, and advancing independently; the readonly-struct runner gives only the first.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:78-80` · high · sha:1608fcd4b329</sub>
- Verified on .NET 10.0.401 against the as-built assemblies (PerAttempt probe policy, BasicAuthPolicy at Auth, a server returning 503 then 200), the second attempt entered the retry loop carrying the first attempt's Authorization credential stamped by a downstream policy, because the request lives on the shared PipelineContext whose Request has a public setter.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:80-85` · high · sha:1608fcd4b329</sub>
- RETRY-44 states that upstream steps MUST NOT mutate the shared in-flight request between attempts; the as-built shared PipelineContext.Request violates it, and the same leak makes the as-built redirect follower build hop n+1 from a request already carrying hop n's auth header.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:84-87` · high · sha:1608fcd4b329</sub>
- The call-scoped property bag stays for state that is legitimately per call, such as the idempotency key reused across redirect hops.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:126-127` · high · sha:1608fcd4b329</sub>
- RECOV-32 and RECOV-33 are stated only in Appendix C (section 11 item 33), and each of the three non-pillar shipped steps (idempotency, client identity, error mapping) is written once against the one policy shape so the same class serves both the stage pipeline and the recovery chain (section 5.2).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:129-131` · high · sha:1608fcd4b329</sub>
- As built, IdempotencyPolicy defaults to POST only, has no overwrite mode and a fixed Guid strategy, correctly checks the header before minting, and parks its key in the call-scoped bag so redirect hops reuse it.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:136-137` · high · sha:1608fcd4b329</sub>
- As built, ClientIdentityPolicy is Replace-only over DexpaceClientOptions.UserAgent.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:139-140` · high · sha:1608fcd4b329</sub>
- The cap constant exists as Response.MaxBufferedErrorBytes, and as built EnsureSuccessAsync drains to it inside an await using over the body stream, which releases the connection whether or not the drain completes (BODY-30).
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:154-156` · high · sha:1608fcd4b329</sub>
- As built for section 5.1: built but diverging, since the shared mutable PipelineContext.Request leaks downstream mutations into re-drives, pillar collision is checked late and unnamed, there is no prepend/bulk/cross-stage rule, stage slots are misnamed or missing, and EnsureSuccessAsync maps non-2xx instead of 400..599.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:170-172` · high · sha:1608fcd4b329</sub>
- REDIR-25 repeats the no-async-redirect rule and lists three porter options, the third being "(c) let callers install their own async redirect step"; this sanctions the step existing on the async path but not the standard preset installing it, and the reversal is recorded as section 10 entry 14.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:298-304` · high · sha:1608fcd4b329</sub>
- As built, HttpPipeline implements neither IAsyncHttpClient nor IHttpClient and cannot stand in for a transport.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:330-330` · high · sha:1608fcd4b329</sub>
- A bodyless response currently surfaces as a generic DeserializationException from an empty stream rather than one naming the missing body.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:386-387` · high · sha:68af5c6bf0ea</sub>

## Conflicts

## Superseded


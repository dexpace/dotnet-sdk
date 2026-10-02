# naming-conventions

## Rules
- An implementation of a role-named interface takes a qualified name (SystemClock : Clock, RedisWorkerQueue : WorkerQueue).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:102-102` · high · sha:329377176013</sub>
- Never rename interfaces not owned by the project; IDisposable, IEnumerable<T> and ILogger<T> keep their framework names.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:102-102` · high · sha:329377176013</sub>
- Drop the Async suffix on first-party methods and name a method for what it does (LoadUser, Route, Charge) whether or not it is asynchronous.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:112-115` · high · sha:329377176013</sub>
- An override or interface implementation of a framework member ending in Async keeps the inherited name.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:116-116` · high · sha:329377176013</sub>
- Prefix descriptive generic type parameters with T (TKey, TValue, TSession, TOutput).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:125-128` · high · sha:329377176013</sub>
- Use a bare T only when a single self-evident type parameter says it all.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:127-128` · high · sha:329377176013</sub>
- A type parameter that conveys nothing must be constrained so the name earns its place, or the genericity removed.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:129-129` · medium · sha:329377176013</sub>
- PascalCase every type (classes, structs, records, enums, delegates, interfaces) and every public member (methods, properties, events, public fields, constants, local functions).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:30-33` · high · sha:329377176013</sub>
- Use language keywords for built-in types (int not Int32, string not String, float not Single), including on static calls such as int.Parse.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:34-34` · high · sha:329377176013</sub>
- Local variables, method parameters, lambda parameters and class or struct primary-constructor parameters are camelCase with no prefix.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:43-46` · high · sha:329377176013</sub>
- A positional record parameter is PascalCase because it is a public property.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:46-46` · high · sha:329377176013</sub>
- Reserve single-letter names for loop counters and the syntax-spec type-parameter conventions.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:47-47` · high · sha:329377176013</sub>
- Names carry semantic meaning, not type information (iterations, not intCount; customer, not cust); Hungarian prefixes and abbreviations are rejected in review.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:47-57` · high · sha:329377176013</sub>
- Prefix private or internal instance fields with _, static fields with s_, and [ThreadStatic] fields with t_.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:59-62` · high · sha:329377176013</sub>
- Avoid this. to disambiguate fields from parameters, relying on the _ prefix instead.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:62-62` · high · sha:329377176013</sub>
- Declare fields readonly by default and write static readonly in that order, never readonly static.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:63-63` · high · sha:329377176013</sub>
- Public fields are rare and, when used, are PascalCase with no prefix, but a property is preferred.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:63-63` · high · sha:329377176013</sub>
- PascalCase all constants, const fields and const locals alike, not SCREAMING_CASE.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:73-76` · high · sha:329377176013</sub>
- The only exception to PascalCase constants is interop, where a const mirrors the exact name and value of the native symbol it maps to.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:77-77` · high · sha:329377176013</sub>
- Use nameof instead of string literals for parameter names, property names (such as in change notifications) and logging field names.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:86-90` · high · sha:329377176013</sub>
- Name first-party interfaces for their role with no I prefix (Clock, WorkerQueue, PaymentGateway).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:98-101` · high · sha:329377176013</sub>
- BCL and framework interfaces (IDisposable, IEnumerable<T>, ILogger) keep their I-prefixed names, and dexpace never renames what it does not own.
  <sub>styleguide · `docs/styleguide/csharp/README.md:78-78` · high · sha:1e6ba36fc337</sub>

## Constraints

## Conclusions
- The transport interfaces keep the I prefix and the async member keeps the Async suffix despite the styleguide's naming chapter saying to drop both, because the Framework Design Guidelines are the stronger authority for a public NuGet surface, a prefix-less HttpClient would collide with System.Net.Http.HttpClient in every consumer's scope, and Execute sits next to ExecuteAsync so the suffix carries information.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:255-259` · high · sha:da6000c93fc5</sub>
- The no-I-prefix convention is a deliberate dexpace house deviation from the runtime style, shared with the Go, Kotlin and TypeScript guides, because the I prefix is Hungarian notation that leaks implementation kind.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:101-101` · high · sha:329377176013</sub>
- The Async suffix is dropped because the return type already carries the information and renaming a method to async later would churn call sites; this is a recorded deviation from the TAP guideline.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:115-116` · high · sha:329377176013</sub>
- nameof is preferred over string literals because a literal duplicates the identifier and goes stale on rename, whereas nameof is a compile-time constant that a rename updates.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:89-90` · high · sha:329377176013</sub>
- The I prefix on first-party interfaces is dropped in favour of naming the role (WorkerQueue rather than IWorkerQueue), against the runtime and Learn guidance to prefix with I.
  <sub>styleguide · `docs/styleguide/csharp/README.md:78-78` · high · sha:1e6ba36fc337</sub>
- The I-prefix drop follows house style across dexpace guides (Go, Kotlin, TS) that name the abstraction rather than its kind, with the accepted cost of diverging from near-universal C# convention and IDE completion habits.
  <sub>styleguide · `docs/styleguide/csharp/README.md:78-78` · high · sha:1e6ba36fc337</sub>
- The Async method suffix is dropped, naming methods for what they do (LoadUser, not LoadUserAsync), against the TAP and Learn guidance to suffix Task-returning methods with Async.
  <sub>styleguide · `docs/styleguide/csharp/README.md:79-79` · high · sha:1e6ba36fc337</sub>
- The Async suffix is dropped because house style names behaviour rather than return-type mechanics and Task<T> is visible in the signature, with the accepted cost of losing the at-a-glance must-await cue.
  <sub>styleguide · `docs/styleguide/csharp/README.md:79-79` · high · sha:1e6ba36fc337</sub>

## Reference
- The I-prefix and Async-suffix departure from the styleguide is indexed in the docs/styleguide/README.md overlay and recorded as design section 10 entry 27.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:259-259` · high · sha:da6000c93fc5</sub>
- The default naming analyzer (IDE1006) that requires the I prefix is disabled in .editorconfig for this guide.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:110-110` · high · sha:329377176013</sub>
- The await-safety the Async suffix used to signal is covered by CS4014 (forgotten await) and CA2007/xUnit1031 (sync-over-async and unobserved tasks).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:116-123` · high · sha:329377176013</sub>
- Generic parameter naming is enforced by CA1715 (identifiers have correct prefix) and review of multi-parameter generics.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:136-136` · high · sha:329377176013</sub>
- Chapter 2 of the styleguide states that Clock is a first-party interface with no I and Route returns a Task without an Async suffix as the two recorded deviations from ecosystem convention.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:3-26` · high · sha:329377176013</sub>
- The naming rule for types and members is enforced by dotnet_naming_rule in .editorconfig and IDE0049 (use language keywords).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:41-41` · high · sha:329377176013</sub>
- Field naming is enforced by .editorconfig naming rules for private/internal, static and thread-static fields, plus IDE0044 (make field readonly).
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:71-71` · high · sha:329377176013</sub>
- CA2208 (instantiate argument exceptions correctly) catches common nameof misuse.
  <sub>styleguide · `docs/styleguide/csharp/02-naming-conventions.md:96-96` · high · sha:329377176013</sub>
- Chapter 02 covers PascalCase types and members, _camelCase/s_/t_ fields, camelCase locals and parameters, nameof, language keywords over BCL types, no I prefix and no Async suffix.
  <sub>styleguide · `docs/styleguide/csharp/README.md:34-34` · medium · sha:1e6ba36fc337</sub>
- CS4014 and the CA2007/xUnit1031 analyzers flag unawaited and sync-over-async misuse, mitigating the loss of the Async suffix.
  <sub>styleguide · `docs/styleguide/csharp/README.md:79-79` · high · sha:1e6ba36fc337</sub>

## Conflicts
- **No I prefix on interfaces (2.6) vs public API keeps I (design §10 entry 27)** — The styleguide says to name first-party interfaces for their role with no I prefix (HttpClient, not IHttpClient), but the SDK keeps the I prefix on public interfaces such as IHttpClient, IAsyncHttpClient and ISerde, because the Framework Design Guidelines are the stronger authority for a public NuGet surface and a prefix-less HttpClient would collide with System.Net.Http.HttpClient in every consumer's scope; the port KEEPS this departure and records it in a note.
  <sub>styleguide `docs/styleguide/csharp/02-naming-conventions.md:98-110` · design `docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md:239-244` · unresolved 2026-10-02</sub>
- **No Async suffix (2.7) vs public API keeps Async (design §10 entry 27)** — The styleguide says to drop the Async suffix and name a method for what it does (Execute, not ExecuteAsync), but the SDK keeps the Async suffix on every Task- or ValueTask-returning public member, because the TAP guideline governs public API and the SDK ships a synchronous Execute beside the asynchronous one, so the suffix carries information; the port KEEPS this departure and records it in a note.
  <sub>styleguide `docs/styleguide/csharp/02-naming-conventions.md:112-123` · design `docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md:239-244` · unresolved 2026-10-02</sub>

## Superseded


# sdk-positioning

## Rules
- Correctness-sensitive decisions (idempotency classification, status ranges, body replayability, header-injection defenses, credential hygiene, cancellation semantics) are made once in the core so that every transport behaves identically.
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>
- A faithful port must preserve the seams and their invariants rather than reimplement each concern per adapter.
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>
- SDK-domain concerns (auth, idempotency keys, typed-error- and Retry-After-aware retry, redirect semantics, SDK spans and metrics) live in the SDK pipeline, while connection-level concerns live in the HttpMessageHandler chain underneath it.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:23-27` · high · sha:d7cea7b15cf3</sub>

## Constraints
- The SDK core does not itself open sockets, encode JSON, or read bytes off a stream; the network transport, byte-stream implementation and wire codec each plug in behind a single-purpose interface.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>

## Conclusions
- The dexpace SDK is defined as an HTTP-client toolkit, not an HTTP client, whose core supplies immutable wire models, a staged request/response pipeline, recovery-aware resilience primitives and a small set of narrow abstractions.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- Because the core carries no concrete transport, codec or I/O dependency (SEAM-1), a consumer can adopt it without a transitive-dependency conflict and can swap transports, JSON libraries or async runtimes independently of one another.
  <sub>spec · `docs/product-spec/01-product-overview.md:7-7` · high · sha:4f786c44354d</sub>
- The connection-layer split puts SDK-domain concerns in the SDK pipeline and connection-level concerns in the HttpMessageHandler chain underneath it (§1, §6.1).
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:72-73` · high · sha:8e66b82361d2</sub>
- A faithful port preserves the seams and their invariants but not the reference's module count.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:100-101` · high · sha:d7cea7b15cf3</sub>
- The .NET port ends with fewer surviving seams than the Ruby port because the async pivot collapses into the runtime's Task and discovery collapses into explicit construction and the DI container (§3.6).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:101-103` · high · sha:d7cea7b15cf3</sub>
- The .NET port has fewer packages than the reference's module map because the runtime already ships the pieces the reference had to wrap.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:102-103` · high · sha:d7cea7b15cf3</sub>
- The SDK does not compete with HttpClient on the easiest way to fetch a JSON endpoint and does not replace HttpClient at all, but sits above it.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:13-14` · high · sha:d7cea7b15cf3</sub>
- The primary audience is authors of generated or hand-written service-client SDKs, such as an internal platform team publishing a client for a company's own API or an OpenAPI-codegen backend targeting C#, who need the correctness-sensitive plumbing solved exactly once.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:14-16` · high · sha:d7cea7b15cf3</sub>
- The secondary audience is application teams who want the SDK's guarantees while keeping their own HttpClient configuration (proxy, mTLS, HTTP/2 and HTTP/3, corporate DelegatingHandlers) and their own codec.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:21-23` · high · sha:d7cea7b15cf3</sub>
- The SDK's thesis is "an HTTP-client toolkit, not an HTTP client", restated for .NET because .NET's default reflex is the opposite of the reference audience's, as a dominant client ships with the runtime.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:3-8` · high · sha:d7cea7b15cf3</sub>

## Reference
- The product's own framing is that it is "the machinery an HTTP client is made of," not the client itself.
  <sub>spec · `docs/product-spec/01-product-overview.md:3-3` · high · sha:4f786c44354d</sub>
- The primary consumers are authors of generated or hand-written service clients, and application teams who bring their own transport, JSON library and async runtime and pay only for what they put on the classpath, both served by one codebase and the same seam design.
  <sub>spec · `docs/product-spec/01-product-overview.md:5-5` · high · sha:4f786c44354d</sub>
- Service-client authors need correct, secure, observable HTTP plumbing, namely retries that never double-send a one-shot body, redirects that never leak a bearer token cross-origin, and logging that never prints a secret, without re-solving those problems per service.
  <sub>spec · `docs/product-spec/01-product-overview.md:5-5` · high · sha:4f786c44354d</sub>
- The architectural principles are the durable principles a port must preserve even as individual subsystems evolve, stated once and referenced throughout the specification.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:3-3` · high · sha:8014d2ec2c9d</sub>
- The plumbing solved once for SDK authors is idempotency-aware retry that never double-sends a one-shot body (RETRY-5, XCUT-10).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:16-18` · high · sha:d7cea7b15cf3</sub>
- The plumbing solved once includes redirects that never leak a bearer token or cookie cross-origin (REDIR-7 through REDIR-13, XCUT-17).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:18-18` · high · sha:d7cea7b15cf3</sub>
- The plumbing solved once includes RFC 7235 challenge parsing and RFC 7616 Digest (AUTH-12 through AUTH-22).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:18-19` · high · sha:d7cea7b15cf3</sub>
- The plumbing solved once includes cursor, page-number and Link-header pagination (PAGE-16 through PAGE-24).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:19-19` · high · sha:d7cea7b15cf3</sub>
- The plumbing solved once includes WHATWG SSE parsing (SSE-1 through SSE-19).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:19-20` · high · sha:d7cea7b15cf3</sub>
- The plumbing solved once includes PATCH's absent/null/present three-state semantics (SERDE-14 through SERDE-20), so that no codegen backend re-solves any of it.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:20-21` · high · sha:d7cea7b15cf3</sub>
- The .NET ecosystem's dominant client is System.Net.Http.HttpClient over SocketsHttpHandler, with IHttpClientFactory for lifetime management and Microsoft.Extensions.Http.Resilience (Polly v8) for retries, hedging and circuit breaking.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:5-8` · high · sha:d7cea7b15cf3</sub>
- Refit, RestSharp and Flurl are complete opinionated .NET client layers, and Microsoft's Kiota abstractions and Azure.Core are two generated-client runtimes.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:7-9` · high · sha:d7cea7b15cf3</sub>

## Conflicts

## Superseded


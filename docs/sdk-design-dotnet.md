# dexpace SDK — .NET Port Design

**Status:** Design proposal. This document is not normative in the sense `product-spec.md` is — it mints no
requirement IDs — but every architectural decision below is justified against, or deliberately deviates from, a
specific requirement in that specification. Read `product-spec.md` first; this document assumes its vocabulary
(`SEAM-*`, `HTTP-*`, `IO-*`, `BODY-*`, `CTX-*`, `PIPE-*`, `RECOV-*`, `RETRY-*`, `REDIR-*`, `AUTH-*`, `PAGE-*`,
`SSE-*`, `SERDE-*`, `OBS-*`, `CFG-*`, `TRANSPORT-*`, `ASYNC-*`, `XCUT-*`, `NFR-*` — 645 requirements across 19
prefixes) and cites IDs inline rather than re-deriving them. Where an argument turns on exact wording, the
requirement is quoted verbatim.

**Written after the code, and reconciled against it.** Unlike the Ruby port's design, which preceded its code, this
one was written against a partial as-built tree at `d45e64b`: HTTP models, bodies, the transport SPI, errors, the
`System.Net.Http` transport, the `System.Text.Json` codec, options, diagnostics, the pipeline and its policies,
authentication and pagination already exist. Each design section therefore ends with a one-line **As built
(d45e64b):** status — `built`, `built — diverges: …`, `partial: …` or `not built` — so a reader can see, section by
section, where the tree already matches the design, where it diverges, and what remains. The earlier .NET design
notes in `docs/superpowers/specs/2026-06-14-*.md` (platform decisions D1–D4 and the slice designs) are treated as the
existing decisions: adopted unless the specification or the porting method argues otherwise, and overturned
explicitly where they are. .NET behaviour asserted as verified was checked on .NET SDK 10.0.401 (runtime 10.0.12);
claims about the `net8.0` floor were read from the 8.0.31 reference pack.

**Scope.** This is a package-and-seam-level architecture for a .NET implementation of the same product: an
HTTP-client toolkit, not an HTTP client. It covers the solution and package layout, the idiomatic .NET mapping of
each of the specification's seams and its async pivot, domain-model construction with records, both pipeline layers,
resilience (retry, redirect, authentication), pagination, SSE and serialization, instrumentation and configuration,
and the toolchain that enforces the quality bar mechanically. It records every deliberate deviation from the
specification in one catalogue (§10), every ambiguity it had to resolve (§11), and which requirements each section
addresses (§12). It does **not** contain C# beyond the signatures that carry an argument, `.csproj` files, CI YAML
or analyzer configuration — those are downstream of this document. It does not specify a code-generation layer, only
the runtime primitives a generator would target (§3.5), and it does not design the service-client surface a
generator would emit.

---

## Table of Contents

- [Porting Method](./sdk-design-dotnet/00-porting-method.md)
- [1. Overview](./sdk-design-dotnet/01-overview.md)
- [2. Project and Solution Layout](./sdk-design-dotnet/02-project-and-solution-layout.md)
- [3. Seam-by-Seam Idiomatic Mapping](./sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md)
- [4. Domain Model Construction](./sdk-design-dotnet/04-domain-model-construction.md)
- [5. Pipeline Architecture](./sdk-design-dotnet/05-pipeline-architecture.md)
- [6. Retry, Redirect, and Authentication](./sdk-design-dotnet/06-retry-redirect-and-authentication.md)
- [7. Pagination, SSE, and Serialization](./sdk-design-dotnet/07-pagination-sse-and-serialization.md)
- [8. Instrumentation and Configuration](./sdk-design-dotnet/08-instrumentation-and-configuration.md)
- [9. Toolchain and Quality Gates](./sdk-design-dotnet/09-toolchain-and-quality-gates.md)
- [10. Deliberate Deviations from the Reference Contract](./sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md)
- [11. Appendix: Reference-Spec Ambiguities and How This Port Resolves Them](./sdk-design-dotnet/11-appendix-reference-spec-ambiguities-and-how-this-port-resolves-them.md)
- [12. Appendix: Requirement Coverage Index](./sdk-design-dotnet/12-appendix-requirement-coverage-index.md)

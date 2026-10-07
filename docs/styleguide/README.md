# Styleguide (vendored)

The binding code-style rules for this repository. This directory is a **vendored copy** of the C#
subset of [`dexpace/styleguide`](https://github.com/dexpace/styleguide), taken at commit `591e524`
(2026-07-27), so that the design document, the knowledge corpus, and reviewers can cite a rule by a
repository-relative path (`docs/styleguide/csharp/09-concurrency.md`) instead of a path on one
machine. **Do not edit the vendored chapters here.** A rule change goes upstream first and is then
re-vendored whole; a place where this SDK deliberately departs from a rule is recorded in the
[SDK overlay](#sdk-overlay--where-this-repository-departs) below and in
[`docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md`](../sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md).

| Path | Upstream | What it is |
|---|---|---|
| [`csharp/`](./csharp/) | `csharp/` | The platform-agnostic C# guide, chapters 01–15. **Primary authority for every `.cs` file here.** |
| [`csharp-aspnetcore/`](./csharp-aspnetcore/) | `csharp-aspnetcore/` | The hosting companion. Only its DI (02), options/configuration (01), serialization (05), logging/observability (06) and build (08) chapters bear on this SDK — and only on `Dexpace.Sdk.Extensions.DependencyInjection` and the instrumentation surface. The endpoint and EF Core chapters do not apply to a client library. |
| [`security.md`](./security.md), [`performance.md`](./performance.md), [`git-and-code-review.md`](./git-and-code-review.md) | root | The cross-cutting, language-agnostic rules the C# guide adapts. |
| [`checklists/csharp.md`](./checklists/csharp.md), [`checklists/csharp-aspnetcore.md`](./checklists/csharp-aspnetcore.md) | `skills/*/reference/checklist.md` | One-line-per-rule audit checklists — walk top to bottom for a full review. |

Re-vendoring: copy the four paths above from a fresh `dexpace/styleguide` checkout, update the commit
in the first paragraph, and re-run the housekeeping probe (`links` check) — the chapters link to each
other with relative paths that this layout preserves.

## Priority

**Correctness > performance > developer experience.** When they conflict, this ordering decides.
Within developer experience: clarity, simplicity, concision, maintainability, consistency — in that
order; consistency is the lowest-priority tiebreaker.

## The twelve rules

These apply to every dexpace project; [`csharp/README.md`](./csharp/README.md#the-12-rules-in-c)
restates each in C# vocabulary.

1. **Data and functions, not objects.** Plain records for state; functions and small interfaces for
   behaviour. No inheritance for code reuse.
2. **Explicit over implicit.** No hidden control flow, no framework magic, every dependency visible in
   the signature. Library options follow their documented defaults.
3. **Immutable by default.** Return new values; read-only collections in public APIs.
4. **Errors are values, handled explicitly.** Every error path covered, nothing silently swallowed,
   errors wrapped with context.
5. **Composition over inheritance.** Pipelines over template methods; delegation over "is-a".
6. **Transform, don't mutate.** Input in, new output out; state changes explicit and localized.
7. **Always say why.** Comments explain reasoning, not mechanics.
8. **Assert aggressively.** Two assertions per function on average; preconditions, postconditions,
   positive and negative space.
9. **Limits on everything.** Every loop, queue, retry, buffer and timeout bounded; no recursion in
   library code; C# methods cap at 70 lines.
10. **Small functions, breathing room.** Aim for 10–30 lines at one level of abstraction.
11. **Performance from the outset.** Network > disk > memory > CPU.
12. **Zero technical debt.** Perfection over technical debt — debt never gets paid.

## Chapter index

| # | Chapter | Where it bites in this SDK |
|---|---|---|
| 01 | [Formatting & Tooling](./csharp/01-formatting-and-tooling.md) | `Directory.Build.props`, `.editorconfig`, `dotnet format --verify-no-changes`, the 70-line cap |
| 02 | [Naming Conventions](./csharp/02-naming-conventions.md) | `_camelCase` / `s_` fields; the `I`-prefix and `Async`-suffix rules (see overlay) |
| 03 | [Nullability & the Type System](./csharp/03-nullability-and-the-type-system.md) | NRT as law, `!` banned — every model and SPI signature |
| 04 | [Variables & Declarations](./csharp/04-variables-and-declarations.md) | `var` discipline, `readonly` by default |
| 05 | [Methods & Functions](./csharp/05-methods-and-functions.md) | Guard clauses, `ThrowIfNull`, options records at ≥3 parameters |
| 06 | [Types & Data Modeling](./csharp/06-types-and-data-modeling.md) | `record` / `readonly record struct` HTTP models, `sealed` by default, closed hierarchies |
| 07 | [C# Idioms](./csharp/07-csharp-idioms.md) | `switch` expressions, collection expressions, raw strings |
| 08 | [Error Handling](./csharp/08-error-handling.md) | The `SdkException` hierarchy, `when` filters, cancellation is not an error |
| 09 | [Concurrency & Async](./csharp/09-concurrency.md) | `CancellationToken` everywhere, `ConfigureAwait(false)`, no sync-over-async (the `IHttpClient` bridge) |
| 10 | [API Design](./csharp/10-api-design.md) | Minimal public surface, `internal` by default, public-API analyzer, `[Obsolete]` + semver |
| 11 | [Testing](./csharp/11-testing.md) | xUnit, property tests, fakes over mocks, `TimeProvider` for retry/token-cache time |
| 12 | [Project & Assembly Organization](./csharp/12-project-organization.md) | Namespaces match folders, one top-level type per file, acyclic project references |
| 13 | [Resource Management](./csharp/13-resource-management.md) | `Response` / `ResponseBody` / transport disposal, `ArrayPool`, bounded buffers |
| 14 | [Documentation](./csharp/14-documentation.md) | `///` on every public member (CS1591 is a build error here), `<inheritdoc/>` |
| 15 | [Performance](./csharp/15-performance.md) | `Span<T>` parsing in `MediaType` / headers / SSE, `ValueTask`, allocation-free hot paths |

## SDK overlay — where this repository departs

The upstream guide is written for dexpace applications and services. This repository is a **public
NuGet library** consumed by code the dexpace team does not control, and a handful of rules collide
with the .NET ecosystem conventions every consumer's IDE, analyzer set and muscle memory assume. The
C# guide itself subordinates to the .NET Runtime coding style and the aspnetcore companion names the
[Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
as an authority; for a public library surface those guidelines are the stronger authority. Each
departure below is argued in full in the design document (§3 and §10) — this table is the index.

| Rule (chapter) | Upstream says | This SDK does | Status |
|---|---|---|---|
| No `I` prefix (02) | Name the role: `HttpClient`, not `IHttpClient` | Keeps `I` on public interfaces (`IAsyncHttpClient`, `ISerde`) | Framework Design Guidelines for public API; a prefix-less `HttpClient` would also collide with `System.Net.Http.HttpClient` in every consumer's scope. Design §10. |
| No `Async` suffix (02) | `Execute`, not `ExecuteAsync` | Keeps `Async` on `Task`/`ValueTask`-returning public members | TAP guideline for public API; the SDK ships a sync `Execute` next to the async one, so the suffix is load-bearing, not decoration. Design §10. |
| .NET 10 / C# 14 (01) | Target .NET 10 LTS | Libraries and tests target `net10.0` only, set once in `Directory.Build.props`; C# `latest` | **Conformed (corrected 2026-09-28).** Roadmap decision D1 was approved on 2026-09-28: the floor rose to `net10.0` in phase 0, before any release. Until then this row read: libraries multi-target `net8.0;net10.0` because .NET 8 is in support until 2026-11-10, `Dexpace.Sdk.Core` targets `net8.0` only, and `System.IO.Pipelines` and friends are NuGet packages on `net8.0`. Design §2.3 and §9.2 carry the matching corrections. C# `latest` rather than `14.0` is unchanged. |
| xUnit v3 + Shouldly (11) | xUnit v3 on Microsoft.Testing.Platform | xUnit v2 today | Migration is a roadmap item, not a deviation. |
| `ConfigureAwait(false)` (09) | Required in library code | `CA2007` at warning (an error under `TreatWarningsAsErrors`) for everything under `src/`; relaxed for tests, repository tools and the AOT smoke consumer | **Conformed (corrected 2026-09-28).** Library code awaits with `ConfigureAwait(false)`, including `await using` (`await using var x = y.ConfigureAwait(false)`). The relaxation for application-shaped code is the one rule 9.4 itself makes ("suppressed in app/host projects"). Until then this row recorded `CA2007` dialled down, with an `.editorconfig` rationale that is **factually wrong**: it said `CA2007` cannot see `await using` / `await foreach`, but on .NET 10.0.401 the rule does fire there (design §9.4). |
| `ImplicitUsings` (01, 12) | Curated, explicit global usings | `<ImplicitUsings>disable</ImplicitUsings>` in `Directory.Build.props`, plus a committed `GlobalUsings.cs` per project | **Conformed (corrected 2026-09-28).** Until then `ImplicitUsings` was enabled as an undocumented departure (design §9.4). |
| Public preconditions asserted (05) | `ThrowIfNull` on public entry points | `CA1062` dialled down in `.editorconfig` | Guards applied by hand; design §9 records the dial-down. |
| 70-line method cap (01) | Analyzer-enforced | Meziantou.Analyzer `MA0051` at 70 lines, as a build error | **Conformed (corrected 2026-09-28).** Two existing methods carry scoped waivers, each naming the roadmap phase that restructures it: `InstrumentationPolicy.ProcessAsync` (phase 5) and `RedirectPolicy.ProcessAsync` (phase 6b). Until then the cap was a review rule. |
| Closed choices as abstract records (06, 6.3) | `abstract record` closed by a private constructor, sealed record cases | `Dexpace.Sdk.Core.Recovery.Outcome` is an abstract **class** with a private constructor and nested sealed classes | **Departure (dated 2026-10-07, phase 4b, P4b-3, open for the lead).** A non-sealed record's synthesised `protected` copy constructor lets any record outside the assembly derive from it, so the set is not closed (verified on SDK 10.0.401); an abstract class with a private constructor is. Design §5.2 carries the dated correction; `docs/knowledge/notes/data-modeling.md` supersedes the harvested rule for this type. |

Everything not in this table applies as written.

# Phase 7c — Pagination: Implementation Plan

**Status:** Draft, for review. Written 2026-10-09 against `main` at `8dc8ee4` (phases 0 to 6c merged). Design:
[phase 7c pagination design](2026-10-09-phase7c-pagination-design.md), the authority for every decision below. The plan
cites its census rows (`PAGE-1`–`PAGE-36`), facts (1–13) and rulings (`P7c-1`…`P7c-20`) rather than restating them. Scope
authority: the roadmap's Phase 7 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the phase 6
plans (`docs/work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect.md` and its 6a and 6c siblings), read first. 7a (serde) and 7b
(SSE) are planned concurrently by other authors in this working tree; this plan touches only 7c's own files and lists the
shared files it will later edit (design, "Cross-sub-phase interfaces").

**What this document is.** The roadmap's step 3 for sub-phase 7c: numbered TDD tasks in the design's three groups (P7c-20), each
with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** marking, requirement IDs and
verification commands. It is not the checklist (step 4, written from what was built in task 3.5) and it writes no production
code itself. The [checklist section](#checklist-one-row-per-owned-id) below is the plan's own row table: exactly one row per
ID 7c owns.

**Scope.** 36 rows, `PAGE-1`–`PAGE-36` (32 MUST, 4 SHOULD). Planned exits: 33 ✅, 3 🚫 (`PAGE-3` under §10 entry 17; `PAGE-29`,
`PAGE-30` under entry 19), 0 ⏳. `PIPE-26` and `SERDE-13` are other owners' rows 7c adds tests to (see
[the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-7c)).

**Order and gates.** One `feat!` pull request (P7c-20), implemented as three groups of tasks that are green commits in
order. **Group 1** (tasks 1.1–1.5): pure internals, no public surface. **Group 2** (2.1–2.9): the contract, the engine, the
bases, the factories and the blocking pager; it follows group 1. **Group 3** (3.1–3.7): fetchers, wire and AOT evidence,
documentation and corrections; it follows group 2. The design's segmentation is not changed.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the group's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed). **No commit is red:** a task that changes
   a signature edits every caller in the same commit.
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code:** `ConfigureAwait(false)` on every `await` including `await using` and `await foreach` (`CA2007`); methods at
   most 70 lines (`MA0051`); `///` XML docs on every public member (CS1591), each citing its `PAGE-n` IDs; no new
   `PackageReference` in `Dexpace.Sdk.Core` (constraint 2); no reflection, so `IsAotCompatible` and `IsTrimmable` hold
   unannotated; `Uri.ToString()` never reaches a log, message or exception text (a cursor or `Link` target can carry a
   session token; use `UrlRedactor` if a URL must be named). One type per file, named for the type.
4. **Tests:** `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `Security` for exactly the
   one class added in task 2.8 (P7c-12); `Integration` for the wire class (3.2). The AOT smoke check is a plain method on
   `SmokeChecks` and carries no trait. Core tests live under `tests/Dexpace.Sdk.Core.Tests/Pagination/` (namespace
   `Dexpace.Sdk.Core.Tests.Pagination`); doubles under `tests/Dexpace.Sdk.TestSupport/`. `Dexpace.Sdk.Core.Tests` references
   Core and `TestSupport` only (SEAM-2) and reaches internals through `InternalsVisibleTo`. Assertions are xUnit `Assert` only.
   Tokens come from `TestContext.Current.CancellationToken` (xUnit v3); no test sleeps on the wall clock.
5. **Security tests are never deleted or loosened.** 7c edits **no** existing `Security` class and adds exactly one
   (`PaginationLinkOriginTests`, core). The V-gate's diff check is therefore `git diff --stat main...HEAD --
   tests/Dexpace.Sdk.Core.Tests/Security tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` listing that one added file and nothing
   else.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped` and a
   removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority
   (apply the analyzer's code fix, then compare against the design's "The public surface" list). Only core's file changes; the
   SystemNet and STJ files must not (the V-gate checks). 7c's lines are all `Dexpace.Sdk.Core.Pagination.*` (or `static` /
   `abstract` prefixed of the same), so a rebase against 7a/7b is mechanical: re-sort.
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating what
   it was. Task 3.6 adds the `CHANGELOG.md` `[Unreleased]` line for each (design, "Breaking changes", items 1–9).
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1): write
   `System.IO.…` fully qualified in core files if needed.
9. **Commits** follow the repository style: `feat!:` for the breaking PR, `test:` for tests only, `docs:` for documentation only,
   `chore:` for refactors. **No AI attribution** in a commit message, changelog line or document. **The plan authorises no `git
   push`, no `gh` command and no remote action** (global hard rule). Branch: `<issue>-phase-7c-pagination` off `main`; 7c has no
   issue, so the implementer names it `phase-7c-pagination`.
10. **Siblings own shared files too.** `PublicAPI.Unshipped.txt`, `CHANGELOG.md`, `CLAUDE.md`, the roadmap status notes,
    `src/Dexpace.Sdk.Core/README.md` and `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` are shared with 7a and 7b: re-derive each hunk
    from the merged file and never resolve a conflict by keeping either side whole. 7c adds no package, so
    `Directory.Packages.props` and the lock files do not move (P7c-18).
11. **Strategies are pure.** A built-in strategy does no I/O, reads no clock and holds only its configuration (`PAGE-5`). Strategy,
    splice and `Link` tests therefore need no pipeline; only the engine suites and the one `Security` class build clients.
12. **Test doubles.** `ScriptedTransport` accepts `Func<Request, Response>` entries, so a response can carry the request the
    engine actually sent (`response.Request`, which `PAGE-17` and `PAGE-19` read). Tests that need that use it; they never build
    a response with `TestResponses.Create`'s default `https://example.test/` request when the strategy reads the URL.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build is
the lint gate. The test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's `--filter`).
Facts 1–6 of the design were verified on this SDK in a scratch app on 2026-10-09; task 0.1 re-verifies them on the implementer's
host before any code is written.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-sec** (the one pagination `Security` class; after task 2.8 and in every later V-gate):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*PaginationLinkOriginTests"
```

**V-gate** (the last task of every group; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # convention 5
git diff --stat main...HEAD -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt   # expect empty (main...HEAD: a bare `git diff` is empty after a commit; add a plain `git diff --stat -- <same paths>` for uncommitted work)
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`, then
`dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh
artifacts/test-results`) runs before the PR is offered for push. No `PackageReference` changes, so no `packages.lock.json` change
is expected; if a locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each group)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info PAGE
scripts/knowledge --gaps PAGE                 # 0 of 36: no PAGE ID has to be read out of appendix C alone
scripts/knowledge --req PAGE-19               # per row in scope of the task (also PIPE-26, SERDE-13 for cited work)
```

---

## Plan-level readings (where the design was silent or the tree differs)

The design's rulings are not changed. **R1–R4 are corrections of fact against the tree; R5–R9 are additions.** Additions that
add behaviour are flagged for the lead.

- **R1 — `SyncPath.GetCompletedResult` and `Disposal` have the shape the design assumes.** `SyncPath.GetCompletedResult<T>(ValueTask<T>,
  string owner)` throws `InvalidOperationException` ("…suspended; a synchronous drive must complete without awaiting (PIPE-28)")
  when the task is not complete; the blocking shell passes `nameof(Pageable)` as `owner`. `Disposal.DisposeQuietly(IDisposable?,
  Exception? primary, ILogger?)` and `Disposal.DisposeQuietlyAsync(IAsyncDisposable?, Exception? primary, ILogger?)` both exist;
  the step calls the async twin when `async` is true and the sync one otherwise. `Response` implements both
  `IDisposable` and `IAsyncDisposable`.
- **R2 — `ScriptedSerde<T>.Deserialize` (sync) returns `default`.** The blocking engine reads `ReadAsBytes` then calls
  `ISerde.Deserialize<T>(ReadOnlySpan<byte>)`, so the existing scripted serde cannot serve the blocking tests. Task 2.1 adds
  `EnvelopeSerde` to the **test project** (`tests/Dexpace.Sdk.Core.Tests/Pagination/`), a small deterministic codec for
  `Envelope` (`"1,2,3|next-cursor"`, `"null"` for a null envelope) that implements both the sync and the async members, is
  stateless (so two concurrent walks are safe, `PAGE-5`) and drains the stream like a real codec. `ScriptedSerde` is left alone
  (other suites use it). Nothing in `TestSupport` changes.
- **R3 — The old `PaginationStrategiesTests` and `PageableTests` both call the old `Pageable.Create(pipeline, …)`.** They cannot
  survive the cutover, so task 2.2 writes the new strategy tests in a **temporary** file `PaginationStrategyParseTests.cs`, and
  task 2.5 deletes the old `PaginationStrategiesTests.cs` and `git mv`s the temporary file onto that name (design: "rewritten").
- **R4 — `Page<T>` is constructed in the old `Pageable.cs` (line 130) and in three `PageableTests` facts (lines ~334, 344, 350).**
  Task 2.1 changes the constructor and, in the same commit, passes `current` at the one src site and moves the three facts into
  the new `PageTests.cs` with the fourth argument, so no commit is red.
- **R5 — `PageWalk<TPage,T>` (internal) bundles the walk's constants.** The design's `PageStep.FetchAsync` takes client, request,
  template, serde, strategy, options, flag and token (eight parameters). The plan bundles the six constants into an internal sealed
  `PageWalk<TPage,T>` (async client or blocking client, `First`, `Serde`, `Strategy`, `Options`, `MaxPages`), leaving
  `FetchAsync(walk, current, async, ct)`. Internal only; the behaviour is the design's. (Flagged: not in the design.)
- **R6 — `ModelImmutabilityArchitectureTests` lists open types by `typeof`.** Its `Models()` data is an explicit list of
  non-generic types and its collection check reads `IsGenericType` for field types only, so `Page<>` and `PageInfo<>` do not join
  it. The design's contingency resolves to: the new `PaginationArchitectureTests` (task 2.8) carries the immutability assertion
  for `Page<T>`, `PageInfo<T>` and `FetchedPage<T>` itself (no public setter, no mutable collection property), and `PagingOptions`
  is excluded by name with the `PAGE-35` rationale in that test's comment.
- **R7 — The security test uses the real 6c stack.** `BearerTokenAuthPolicy(TokenCredential, string scope)` and
  `PipelineBuilder.Add(...).Build(transport)` are used exactly as `RedirectCredentialLeakTests` does (private `FixedTokenCredential`
  copied into the new class; the class is private there). No new `TestSupport` type.
- **R8 — `Link` vectors.** `tests/vectors/pagination/link-header.json` is read through `VectorFile.Load<T>` (TestSupport) and its
  `source` string must match `@<sha>` (enforced by `VectorFileTests.Every_vector_file_names_its_source_path_and_sha`): the plan
  writes `nodejs-sdk@c0ff3fd packages/core/src/pagination/link-header.test.ts`. The csproj already copies `..\vectors\**\*.json`.
- **R9 — Ruby sources are absent (design R3).** Task 0.1 step 4 records whether any row lacks a conformance case after the Node
  ports; only then are the Ruby files fetched (a read-only GitHub fetch the lead must authorise, per the global rule, so the plan
  assumes not).

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no commit)

**Why first.** The design's facts 1, 3, 4, 5 and 6 are `System.Uri` behaviours on which tasks 1.1 and 1.4 depend; a wrong one
reopens a ruling (P7c-9, P7c-12) rather than a test.

1. Run the phase-start queries above. Record any difference from the design's table (36 IDs, 0 gaps, no PAGE note or conflict).
2. Baseline: `dotnet restore Dexpace.Sdk.sln --locked-mode`, `dotnet build Dexpace.Sdk.sln --configuration Release`, and
   `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*Pageable*"` and
   `"*PaginationStrategies*"` must be green (24 + 25 = 49 tests; use the runner's reported count, theory rows included) before any change. Note the 49-test baseline.
3. In the scratchpad directory (not the repo), write a file-based app and run it with `dotnet run`, printing: the facts-1 and -2
   `GetComponents` / `Uri.Query` results; fact 3 (`Uri.TryCreate(base, "not a url")` succeeds, `"https://evil/x\ty"` succeeds);
   fact 4 (`"?page=2"` keeps the path, `"//evil.example/x"` changes the origin); fact 5 (`EscapeDataString("a+b/c=")`,
   `UnescapeDataString("a+b%20c")`); fact 6 (`int.TryParse("٣", NumberStyles.None, InvariantCulture)` and `(" 3", …)` are `false`);
   and `new Uri("https://h:443/p").GetComponents(SchemeAndServer, UriEscaped)`. Paste the observed values into the task 1.1/1.4
   test expectations if they differ; a difference that contradicts a ruling reopens it.
4. `ls ../ruby-sdk` is not needed: the design already records that the Ruby test sources are absent. Re-confirm by
   `ls /home/mohammad/Projects/dexpace/ruby-sdk/gems 2>&1` if the sibling checkout exists; otherwise skip.
5. `grep -rn "Pageable\.\|PaginationStrategies\.\|Page<" src tests --include='*.cs' | grep -v '/obj/\|/bin/'` and confirm the only
   call sites are `src/Dexpace.Sdk.Core/Pagination/*`, `tests/Dexpace.Sdk.Core.Tests/Pagination/*` and (comment only)
   `tests/Dexpace.Sdk.Core.Tests/Pipeline/PipelineAsTransportTests.cs`. Docs references: `CLAUDE.md`, `README.md`,
   `src/Dexpace.Sdk.Core/README.md` (line 18).

**IDs:** none (pre-flight).

---

# Group 1 — Splice, `Link` parser and `Link` target (internal; no public surface)

## Task 1.1 — `QuerySplice` (`PAGE-21`, `PAGE-22`, `PAGE-23`, `PAGE-24`; P7c-9, P7c-10; facts 1, 2, 5, 11)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/QuerySplice.cs`; `tests/Dexpace.Sdk.Core.Tests/Pagination/QuerySpliceTests.cs`.

**Node port (spec, tests table).** Port every case of `../nodejs-sdk/packages/core/src/pagination/query-splice.test.ts` at `c0ff3fd` (22 cases) into `QuerySpliceTests`, in addition to the cases below. Drop only cases that assert JavaScript host facts (`URLSearchParams` / WHATWG `URL` behaviour) and list each dropped case, with the reason, in the task's commit message; where .NET diverges (case-sensitive match, duplicates dropped, `Uri` canonicalisation, the entry 18 residual) keep the .NET expectation and name the Node case it diverges from in a comment. The checklist evidence (3.5) cites the port.

**Failing tests first** (`[Trait("Category","Unit")]`, `public sealed class QuerySpliceTests`; the first fails with a compile error
because `QuerySplice` does not exist):

```csharp
[Fact]
public void Set_replaces_the_first_match_in_place_and_keeps_every_other_segment_verbatim()
{
    var result = QuerySplice.Set(new Uri("https://h/p?flag&x=1&q=a+b&r=%2F"), "x", "2");
    Assert.Equal("https://h/p?flag&x=2&q=a+b&r=%2F", result.AbsoluteUri);
}
```

The rest of the class (each a `[Fact]` unless noted; expected strings are `result.AbsoluteUri` or `Get`'s return):

- `Set` appends when absent (`?a=1` + `b` → `?a=1&b=v`); appends with no `?` when the URL has no query; `null` value removes the first
  match and every later duplicate; `null` on an absent name is a no-op returning an equal URI.
- **Duplicates:** `?x=1&y=2&x=3` + `Set("x","9")` → `?x=9&y=2` (first replaced in place, later dropped; Breaking 5).
- **Case-sensitivity regression (design §7.1 bug):** `?Page=1` + `Set("page","2")` → `?Page=1&page=2`; `Get(?Page=1,"page")` is `null`.
- **Decoded-name match (P7c-9):** `?my%20key=1` and `?my key=1` (the `Uri` form) both match `Get(…, "my key")`; `Set` on `my key`
  replaces it and writes the encoded name `my%20key`.
- **Encoding (`PAGE-22`):** `Set(…, "q", "a b+c/d=")` writes `q=a%20b%2Bc%2Fd%3D`; a non-ASCII value writes UTF-8 percent
  escapes. `Get` decodes `%20` and `%2B` but leaves a literal `+` as `+` (`?q=a+b` → `"a+b"`).
- **`Get` shapes:** a value-less flag (`?flag`) reads `""`; empty `&`-segments are skipped (`?&&a=1`); first match wins; absent is
  `null`; `?a=` reads `""`; a name containing `=` in the value (`?a=b=c`) reads `"b=c"`.
- **Rebuild (`PAGE-24`, fact 1):** `https://u:p@h:8443/a%2Fb/c?x=1#f` + `Set("x","2")` → `https://u:p@h:8443/a%2Fb/c?x=2#f`
  (userinfo, port, escaped path and fragment survive); `https://h:443/p?x=1` + `Set("x","2")` → `https://h/p?x=2` and the
  result's text contains no `:443` (pinning that `UriBuilder` is gone); `http://h:80/p` likewise.
- **The residual (§10 entry 18), pinned:** `?x=%7E%41&page=1` + `Set("page","2")` → the query is `x=~A&page=2` (`Uri` canonicalised
  unreserved escapes before the splice). The test's comment cites entry 18 and says the assertion documents the residual, not a goal.
- **Lone surrogate (P7c-10, fact 11):** `Set(url, "c", "\uD800")` throws `ArgumentException` with `ParamName == "value"`; the same
  for a lone low surrogate and for a lone surrogate in `name` (`ParamName == "name"`); the message does **not** contain the
  offending text (use a marker value such as `"secret\uD800"` and `Assert.DoesNotContain("secret", ex.Message)`). A *paired*
  surrogate (an emoji) is accepted and round-trips through `Get`.
- **Argument checks:** null `url` → `ArgumentNullException`; null or empty `name` → `ArgumentException`/`ArgumentNullException`.

**Production change.** `QuerySplice.cs` (`internal static class`, namespace `Dexpace.Sdk.Core.Pagination`; imports
`Dexpace.Sdk.Core.Http.Request` for `Rfc3986`, which is `internal` in that namespace):

```csharp
internal static class QuerySplice
{
    internal static string? Get(Uri url, string name)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(name);
        RejectLoneSurrogate(name, nameof(name));
        foreach (var segment in Segments(url))
        {
            var (rawName, rawValue) = SplitSegment(segment);
            if (string.Equals(Rfc3986.DecodeComponent(rawName), name, StringComparison.Ordinal))
            {
                return rawValue is null ? string.Empty : Rfc3986.DecodeComponent(rawValue);
            }
        }

        return null;
    }

    internal static Uri Set(Uri url, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(name);
        RejectLoneSurrogate(name, nameof(name));
        if (value is not null) { RejectLoneSurrogate(value, nameof(value)); }

        var kept = new List<string>();
        var matched = false;
        foreach (var segment in Segments(url))
        {
            var rawName = SplitSegment(segment).RawName;
            if (!string.Equals(Rfc3986.DecodeComponent(rawName), name, StringComparison.Ordinal)) { kept.Add(segment); continue; }
            if (matched || value is null) { continue; }   // later duplicates (and every match on removal) are dropped
            matched = true;
            kept.Add($"{Rfc3986.EncodeComponent(name)}={Rfc3986.EncodeComponent(value)}");
        }

        if (!matched && value is not null) { kept.Add($"{Rfc3986.EncodeComponent(name)}={Rfc3986.EncodeComponent(value)}"); }
        return Rebuild(url, string.Join('&', kept));
    }

    // private helpers: Segments (url.Query without '?', split on '&', skipping empties), SplitSegment (text before / after the
    // first '='; a segment with no '=' has a null value), RejectLoneSurrogate (char.IsHighSurrogate must be followed by a low and
    // a low must follow a high; throws new ArgumentException($"The {paramName} contains an unpaired surrogate and cannot be
    // percent-encoded.", paramName) and never the value), Rebuild.
}
```

`Rebuild(Uri url, string query)`: `var head = url.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo |
UriComponents.Path, UriFormat.UriEscaped);` then `+ (query.Length > 0 ? "?" + query : "")` then, when `url.Fragment.Length > 0`,
`+ "#" + url.GetComponents(UriComponents.Fragment, UriFormat.UriEscaped)`; return `new Uri(text, UriKind.Absolute)`. Each
private helper is well under 70 lines. XML docs on the internal members cite `PAGE-21`–`PAGE-24`; the class remark states why
`Query` (which re-renders the whole query, `PAGE-21`) and `UriBuilder` (explicit default port, fact 1) are not used.

**PublicAPI:** none (internal). **Breaking:** none (internal; Breaking 5 lands with the strategies in 2.2).

**Verify:** `V-fast` with `QuerySpliceTests`, then `dotnet build Dexpace.Sdk.sln --configuration Release`.

**IDs:** `PAGE-21`, `PAGE-22`, `PAGE-23`, `PAGE-24`.

## Task 1.2 — `QuerySplice` property tests (`PAGE-21`, `PAGE-22`; P7c-18)

**Files.** New: `tests/Dexpace.Sdk.Core.Tests/Pagination/QuerySplicePropertyTests.cs`.

**Node port.** Port `../nodejs-sdk/packages/core/src/pagination/query-splice.property.test.ts` at `c0ff3fd`: every property it states becomes a seeded property here (the two below are the minimum; add any Node property they do not cover, dropping only JavaScript-host-fact properties and listing them in the commit message).

**Tests first** (they pass once 1.1 exists, so these are **pins**; prove each can fail by temporarily changing the splice to
re-encode untargeted segments, then revert). No package is added: a seeded `System.Random` generator, 500 cases per property,
the seed in the test name and in the failure message so a failure is reproducible:

```csharp
private const int Seed = 20261009;

[Fact]
public void Untargeted_segments_are_byte_identical_after_a_set_seed_20261009()
{
    var rng = new Random(Seed);
    for (var i = 0; i < 500; i++)
    {
        var segments = RandomSegments(rng);                       // 0-6 segments over an alphabet of letters, digits, "%41", "+", ":", "~", "-", "_", "="
        var url = new Uri("https://h/p" + (segments.Count == 0 ? "" : "?" + string.Join('&', segments)));
        var result = QuerySplice.Set(url, "zz-target", "v");      // name outside the alphabet: never matches
        Assert.True(result.Query.StartsWith(url.Query, StringComparison.Ordinal) || url.Query.Length == 0,
            $"seed {Seed}, case {i}: {url.Query} -> {result.Query}");
    }
}
```

Plus: **write-then-read identity** (for any well-formed string value from a generator that excludes lone surrogates,
`Get(Set(url, n, v), n) == v`, and `Get(Set(url, n, null), n) == null`); **no exception other than `ArgumentException`** for any
server-shaped string (a generator that includes lone surrogates, `%`-garbage such as `%G1` and `%`, empty and very long
strings): `Set`/`Get` either return or throw `ArgumentException`, never `UriFormatException`, `FormatException` or an
`IndexOutOfRange`.

**Production change:** none expected. If a property fails, the failure is a defect in 1.1: fix it there (a `%` followed by
non-hex must not make `Rfc3986.DecodeComponent` throw; `Uri.UnescapeDataString` leaves invalid escapes as is, verified in 0.1).

**Verify:** `V-fast` with `QuerySplicePropertyTests`.

**IDs:** `PAGE-21`, `PAGE-22`.

## Task 1.3 — `LinkHeaderParser` and the `link-header.json` vector (`PAGE-18`, `PAGE-20`; fact 3)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/LinkHeaderParser.cs`; `tests/vectors/pagination/link-header.json`;
`tests/Dexpace.Sdk.Core.Tests/Pagination/LinkHeaderParserTests.cs`.

**Vector first.** `link-header.json` follows the shape of `tests/vectors/retry/pacing.json`: `"source": "nodejs-sdk@c0ff3fd
packages/core/src/pagination/link-header.test.ts"`, a `"note"` (the `FindNext` contract; cases Node asserts about JavaScript
host behaviour are not ported; the `<not a url>` divergence is in `LinkTargetTests`, not here), and `"cases"`: `{ "name",
"header", "expected" }` with `expected` the raw URI-reference or `null`. Port the 15 Node cases of `link-header.test.ts` and add
the .NET-specific ones below (each named `PAGE-18: …` or `PAGE-20: …`):

- quoted and unquoted `rel` (`rel=next`, `rel="next"`); case-insensitive (`REL=NEXT`, `Rel="Next"`); a `rel` token list separated
  by SP and by HTAB (`rel="prev next"`, `rel="prev\tnext"`); `rel="nextish"` and `rel="prev"` do not match; first matching
  link-value wins when two have `next`; decoys (`<a>; title="rel=next"; rel=prev` → `null`); commas inside `<…>`
  (`<https://h/a,b?x=1>; rel=next`); commas and semicolons inside a quoted value (`<https://h/n>; title="a, b; c"; rel=next`);
  quoted-pair escapes (`<https://h/n>; title="say \"hi\""; rel=next`, and `rel="ne\xt"` unescapes to `next`); an unterminated
  `<` or quote yields `null` without throwing; a link-value with no `<` is skipped to the next top-level comma and the next
  link-value is still read; leading, trailing and doubled OWS and commas (`" , <https://h/n>;rel=next , "`); empty and
  whitespace-only input → `null`; only the first `rel` parameter of a link-value counts (RFC 8288 §3.3: `<u>; rel=prev; rel=next`
  → `null`); `PAGE-20`: the *joined* form of split header instances (`"<https://h/a>; rel=prev, <https://h/b>; rel=next"`).

**Failing tests first** (compile error: `LinkHeaderParser` missing):

```csharp
[Trait("Category", "Unit")]
public sealed class LinkHeaderParserTests
{
    private sealed record LinkCase(string Name, string Header, string? Expected);

    public static TheoryData<string> Names() => [.. VectorFile.Load<LinkCase>("pagination/link-header.json").Select(c => c.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void FindNext_matches_the_vector(string name)
    {
        var vector = VectorFile.Load<LinkCase>("pagination/link-header.json").Single(c => c.Name == name);
        Assert.Equal(vector.Expected, LinkHeaderParser.FindNext(vector.Header));
    }

    [Fact]
    public void FindNext_never_throws_for_arbitrary_input() { /* seeded Random, 500 strings over `<>";,= \t\\a` of length 0-60: no exception, result null or a substring of the input */ }
}
```

(Read `VectorFile.Load`'s generic signature in `tests/Dexpace.Sdk.TestSupport/Vectors/` first and adapt the record casing to
its JSON options; `VectorFileTests` uses the same pattern.)

**Production change.** `LinkHeaderParser` (`internal static class`; `internal static string? FindNext(string? value)`): a
single-pass scanner over the already-joined value, as the design describes, split into four private methods each under 70 lines:

1. `FindNext` loops: `SkipSeparators` (SP, HTAB, commas); at end → `null`; if the char is not `<` → `SkipToTopLevelComma` and
   continue; else `close = value.IndexOf('>', i + 1)`; `close < 0` → return `null` (nothing after an unterminated `<` can be
   trusted); `target = value[(i + 1)..close]`; `i = close + 1`; `ReadRel(value, ref i)` returns the first `rel` parameter's value
   (or `null`); if `HasNextToken(rel)` return `target.Trim(' ', '\t')`.
2. `ReadRel` loops over `; name [= (token | quoted-string)]` parameters until a top-level comma or the end; a name compares
   `OrdinalIgnoreCase` with `rel`; only the first `rel` is recorded; an unterminated quoted-string ends the value (set `i` to the end)
   and returns what was gathered so far as `null` (malformed → no match).
3. `ReadQuoted(string s, ref int i)` honours `\` quoted-pair (the next char is taken literally) and returns the unescaped text.
4. `HasNextToken(string? rel)`: split on SP and HTAB (`rel.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)`), any token
   `OrdinalIgnoreCase`-equal to `next`.

`SkipToTopLevelComma` is quote- and angle-aware (a `,` inside `"…"` or `<…>` is data). `FindNext` never throws.

**PublicAPI:** none. **Verify:** `V-fast` with `LinkHeaderParserTests`, then `VectorFileTests` (the new file must name its source and
sha).

**IDs:** `PAGE-18`, `PAGE-20`.

## Task 1.4 — `LinkTarget.TryResolve` (`PAGE-19`; P7c-12; facts 3, 4, 9)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/LinkTarget.cs`; `tests/Dexpace.Sdk.Core.Tests/Pagination/LinkTargetTests.cs`.

**Node port.** Port the PAGE-19 cases of `../nodejs-sdk/packages/core/src/pagination/strategies.test.ts` at `c0ff3fd` (Link target resolution, cross-origin and scheme cases) into `LinkTargetTests`; dropped JavaScript-host cases are listed in the commit message, and the divergences (`not a url` ends the stream, P7c-12's template-origin comparison, userinfo strip) are kept as the .NET expectation with a comment naming the Node case.

**Failing tests first** (compile error: `LinkTarget` missing). Template URL for every row is `https://api.example/repo/issues?page=1`
unless stated; the response URL (the base, post-redirect) is the same unless stated; `allowCrossOrigin` false unless stated.

```csharp
[Fact]
public void A_query_only_reference_keeps_the_path()
{
    Assert.True(LinkTarget.TryResolve(Base, "?page=2", Template, allowCrossOrigin: false, out var target));
    Assert.Equal("https://api.example/repo/issues?page=2", target!.AbsoluteUri);
}
```

- **Resolution (`PAGE-19`):** absolute same-origin (`https://api.example/repo/issues?page=2`); path-relative (`other?page=2` →
  `/repo/other?page=2`); root-relative (`/x?y=1`); the base is the **response** URL, not the template: with response URL
  `https://api.example/repo/redirected/` and target `next?page=2` the result is under `/repo/redirected/`.
- **Rejected before resolving (fact 3):** `not a url` (the spec fixture: the stream ends, diverging from Node), a target with a
  TAB, a control character, `<`, `>` or `"`, an empty and a whitespace-only target, and `null` → `false`, `target` null.
- **Scheme and host guard:** `ftp://api.example/x`, `javascript:alert(1)`, `mailto:a@b`, `file:///etc/passwd` → `false`; `https://`
  with an empty host → `false` (the call must not throw).
- **Userinfo stripped:** `https://user:pw@api.example/repo/issues?page=2` → `true`, `target.UserInfo == ""`, the text contains neither
  `user` nor `pw`.
- **Origin guard (P7c-12):** `https://evil.example/p2` → `false`; the network-path reference `//evil.example/x` → `false`; a different
  port (`https://api.example:8443/x`) → `false`; `http://api.example/x` (scheme downgrade is a different origin) → `false`; with
  `allowCrossOrigin: true` the first, second and third resolve to their targets (userinfo still stripped); the downgrade also
  resolves when allowed. The comparison is against the **template's** origin, not the response URL's: response URL
  `https://other.example/` (a redirected first page) with template `https://api.example/…` and target `https://other.example/n` →
  `false` without the opt-in (P7c-12, "launders a redirect").
- **Total:** `TryResolve` never throws for any of a seeded 500-string set (controls, `%`-garbage, very long strings, `\uD800`).

**Production change.** `LinkTarget` (`internal static class`; imports `Dexpace.Sdk.Core.Http.Common` for `HttpOrigin`,
`System.Diagnostics.CodeAnalysis` for `[NotNullWhen(true)]`):

```csharp
internal static bool TryResolve(Uri responseUrl, string? raw, Uri templateUrl, bool allowCrossOrigin, [NotNullWhen(true)] out Uri? target)
{
    target = null;
    var text = raw?.Trim(' ', '\t');
    if (string.IsNullOrEmpty(text) || HasForbiddenCharacter(text) || !TryCombine(responseUrl, text, out var created)) { return false; }
    if (!IsHttpWithHost(created)) { return false; }
    var stripped = created.UserInfo.Length == 0
        ? created
        : new Uri(created.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped), UriKind.Absolute);
    if (!allowCrossOrigin && HttpOrigin.From(stripped) != HttpOrigin.From(templateUrl)) { return false; }
    target = stripped;
    return true;
}
```

`HasForbiddenCharacter`: any `char <= ' '` or `== '\u007F'`, or `<`, `>`, `"`. `TryCombine`: `Uri.TryCreate(responseUrl, text, out created)`
inside a `catch (Exception ex) when (ex is UriFormatException or ArgumentException or InvalidOperationException)` that returns
`false`. `IsHttpWithHost`: `created.IsAbsoluteUri`, scheme `http`/`https` (ordinal ignore case), non-empty `IdnHost`.

**PublicAPI:** none. **Verify:** `V-fast` with `LinkTargetTests`.

**IDs:** `PAGE-19`; the P7c-12 guard (its end-to-end proof is task 2.8).

## Task 1.5 — Close-out (group 1)

Run `V-gate`. Expected: the PublicAPI file diff is empty; test count rises by the new classes only; no existing test changed.
Commit: `test:`/`chore:` as the implementer splits it (`chore: internal query splice, Link parser and Link target for phase 7c`).

---

# Group 2 — Contract, engine, bases, factories and the blocking pager

## Task 2.1 — Fixtures, `Page<T>.Request`, `PageInfo<T>`, `IPageStrategy` (`PAGE-2`, `PAGE-4`; P7c-2, P7c-7, P7c-19; R2, R4)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/PageInfo.cs`, `src/Dexpace.Sdk.Core/Pagination/IPageStrategy.cs`;
`tests/Dexpace.Sdk.Core.Tests/Pagination/PageFixtures.cs`, `EnvelopeSerde.cs`, `PageInfoTests.cs`, `PageTests.cs`.
Edited: `src/Dexpace.Sdk.Core/Pagination/Page.cs`, `src/Dexpace.Sdk.Core/Pagination/Pageable.cs` (the one `new Page<T>` call, line
130: pass `current` as the fourth argument), `tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs` (delete the three `Page<int>`
construction facts, lines ~330–352, moved), `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt`.

**Test fixtures (not tests).** `PageFixtures` is an `internal static class` in the test project: `Envelope` is a `sealed record
Envelope(IReadOnlyList<int> Items, string? Next)`; `PageFixtures.Body(IReadOnlyList<int> items, string? next)` renders
`"1,2,3|next"` UTF-8; `PageFixtures.Respond(IReadOnlyList<int> items, string? next = null, Headers? headers = null)` returns a
`Func<Request, Response>` (a `ScriptedTransport` entry) that builds `TestResponses.Create(Status.Ok, request, headers,
ResponseBody.FromBytes(bytes))`, so `response.Request` is the request the engine sent. `EnvelopeSerde : ISerde` parses that text
in both `Deserialize<T>(ReadOnlySpan<byte>)` and `DeserializeAsync<T>(Stream, CancellationToken)` (it drains the stream with
`CopyToAsync`), returns `default` for the body `null`, **returns an empty envelope (`Items` empty, `Next` null) for an empty body**, throws a distinct
`MalformedEnvelopeException` (test-project type, `FormatException`-derived) for any other text that is not `"a,b,c|next"` form, throws
`InvalidOperationException` if `T != typeof(Envelope)`, and has no state. Tests that need valid bytes use `PageFixtures.Body`; the
`TrackingResponseBody` default bytes `[1,2,3,4]` are **not** an envelope, so dispose-count tests over it must either supply a valid
envelope body (a `TrackingResponseBody`/`DisposalCountingBody` wrapper in the test project that serves `PageFixtures.Body` and counts or
fails on dispose) or use the empty-body result. Strategy-throws tests use a private sentinel exception the serde can never throw, so
`ThrowsAsync<Sentinel>` proves the strategy path, not a serde failure. `Serialize*` are no-ops. Test-project classes with no `[Fact]` need no trait.

**Failing tests first:**

```csharp
[Trait("Category", "Unit")]
public sealed class PageInfoTests
{
    [Fact]
    public void A_null_item_list_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => new PageInfo<int>(null!, nextRequest: null));

    [Fact]
    public void A_null_next_request_is_the_end_signal_and_is_kept_as_null()
    {
        var info = new PageInfo<int>([1, 2], null);
        Assert.Null(info.NextRequest);
        Assert.Equal([1, 2], info.Items);
    }
}
```

`PageTests`: the three moved facts, now with the fourth argument — the constructor stores `Values`, `Status`, `Headers`, `Request`;
null `values`, `headers` or `request` → `ArgumentNullException`; `Page<T>` is not `IDisposable`/`IAsyncDisposable` (architecture
test in 2.8 asserts it too).

**Production change.**

- `Page<T>`: constructor `Page(IReadOnlyList<T> values, Status status, Headers headers, Request request)` (null-checks all
  reference arguments) and `public Request Request { get; }` documented as "the request the walk sent for this page, before the
  pipeline; it carries no credential the auth policy stamped (P7c-7, fact 10)". Class remark gains `PAGE-2`, `PAGE-3` (a page owns
  no response, §10 entry 17). **Breaking:** the constructor required three arguments.
- `PageInfo<T>` (`public sealed class`): `PageInfo(IReadOnlyList<T> items, Request? nextRequest)`, `Items`, `NextRequest`
  ("`null` is the only end signal, `PAGE-4`"). Not a record: value equality over a list would be reference equality (P7c-19).
- `IPageStrategy<in TPage, T>` (`public interface`): `PageInfo<T> Parse(TPage page, Response response, Request template);` with
  remarks stating that `response`'s body is already read and closed-or-closing, that a strategy reads only its status, headers and
  `response.Request.Url`, builds the next request from `template`, holds no per-walk state (`PAGE-5`), and that a `null` return is a
  contract violation (`PAGE-4`).

**PublicAPI diff.** Edit the `Page<T>` ctor line to the four-argument form; add `Dexpace.Sdk.Core.Pagination.Page<T>.Request.get ->
Dexpace.Sdk.Core.Http.Request.Request!`, `Dexpace.Sdk.Core.Pagination.PageInfo<T>` (type, constructor, `Items.get`, `NextRequest.get ->
Dexpace.Sdk.Core.Http.Request.Request?`) and `Dexpace.Sdk.Core.Pagination.IPageStrategy<TPage, T>` with `.Parse(TPage page,
Dexpace.Sdk.Core.Http.Response.Response! response, Dexpace.Sdk.Core.Http.Request.Request! template) ->
Dexpace.Sdk.Core.Pagination.PageInfo<T>!`. Apply the analyzer's code fix and diff.

**Verify:** `V-fast` with `PageInfoTests`, `PageTests`, `PageableTests`; full `dotnet build`.

**IDs:** `PAGE-2`, `PAGE-4`.

## Task 2.2 — The built-in strategies and the new `PaginationStrategies` factories (`PAGE-4`, `PAGE-5`, `PAGE-16`, `PAGE-17`, `PAGE-18`–`PAGE-20`, `PAGE-23`; P7c-2, P7c-8, P7c-11, P7c-12; R3)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/DelegateStrategy.cs`, `CursorStrategy.cs`, `PageNumberStrategy.cs`,
`LinkHeaderStrategy.cs` (each `internal sealed class … : IPageStrategy<TPage, T>`);
`tests/Dexpace.Sdk.Core.Tests/Pagination/PaginationStrategyParseTests.cs` (**temporary name**, R3). Edited:
`src/Dexpace.Sdk.Core/Pagination/PaginationStrategies.cs` (the new overloads are **added**; the old ones stay until 2.5). **RS0026:** the old
`LinkHeader<TPage>(string rel = "next")` and the new `LinkHeader<TPage,T>(items, headerName = "Link", allowCrossOrigin = false)` both have optional
parameters, and PublicApiAnalyzers RS0026 ("Do not add multiple overloads with optional parameters") fires across generic arities, which fails the
build under warnings-as-errors. So in 2.2 the new link factory is added under the **temporary name `LinkHeaderNext<TPage,T>`** (tests and PublicAPI
line use that name) and 2.5 renames it to `LinkHeader` in the same commit that deletes the old overload. `Create`, `Cursor` and `PageNumber` keep
their final names (the old `Cursor`/`PageNumber` have no optional parameter and were verified to build clean beside the new pair), `PublicAPI.Unshipped.txt`.

**Node port.** Port every case of `../nodejs-sdk/packages/core/src/pagination/strategies.test.ts` (18 cases; the `Link` target cases already live in `LinkTargetTests`, 1.4) and `strategy.test.ts` at `c0ff3fd` into `PaginationStrategyParseTests`, in addition to the cases below; list dropped JavaScript-host cases and name divergences in comments. The checklist evidence cites the ports.

**Failing tests first** (compile error: the new `PaginationStrategies.Cursor<TPage,T>` etc. do not exist). Tests call `Parse`
directly with `TestResponses.Create(Status.Ok, executedRequest, headers)`; `template` is the walk's `first`.

```csharp
[Fact]
public void Cursor_sets_the_parameter_on_the_template_and_keeps_its_method_headers_and_body()
{
    var template = Request.Post("https://api.example/search?q=a", RequestBody.FromString("{}")).WithHeader("X-Trace", "1");
    var strategy = PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next);
    var info = strategy.Parse(new Envelope([1], "abc"), TestResponses.Create(Status.Ok, template), template);
    var next = Assert.IsType<Request>(info.NextRequest);
    Assert.Equal(Method.Post, next.Method);
    Assert.Equal("https://api.example/search?q=a&cursor=abc", next.Url.AbsoluteUri);
    Assert.Equal("1", next.Headers.Get("X-Trace"));
    Assert.Same(template.Body, next.Body);
}
```

Remaining cases:

- **Cursor (`PAGE-16`):** default parameter name is `cursor`; a custom name; `null` and `""` cursor → `NextRequest == null` and items
  still returned; an existing `cursor=old` is replaced in place (not appended); the next request is built from the **template**,
  not `response.Request` (give the response a different request URL and `Authorization` header and assert neither leaks into
  `next`, P7c-8).
- **PageNumber (`PAGE-17`):** empty items → end (no next, even when the URL says `page=1`); current page read from the **executed**
  request (`response.Request.Url` `?page=4` → next template `page=5`, though the template has `page=1`); absent/empty/garbage
  (`page=abc`, `page=-1`, `page=+3`, `page= 3`, Arabic-Indic digit `٣`, `page=99999999999`) → `startPage` (default 1) → next `page=2`;
  `startPage: 0` with no parameter → next `page=1`; `current == int.MaxValue` (`page=2147483647`) → end (P7c-11); `startPage`
  negative → `ArgumentOutOfRangeException` at construction; culture independence (run the parse under `CultureInfo` `ar-SA` via
  `CultureInfo.CurrentCulture` set/restore inside the test; the result is unchanged).
- **LinkHeader (`PAGE-18`–`PAGE-20`; written against `LinkHeaderNext` until 2.5 renames it):** reads `Link`; default header name `Link`; a custom `headerName` (`X-Next`); **every instance** is
  read (two `Link` header lines, the `next` on the second); no header → end; no `next` rel → end; a `next` that fails
  `LinkTarget` (cross-origin, `not a url`) → end, items still returned; a resolvable `next` → `template.WithUrl(target)` with
  method/headers/body kept; `allowCrossOrigin: true` follows a cross-origin target; the base is `response.Request.Url` (a response
  from a redirected URL resolves a relative `next` against it).
- **Create (delegate adapter):** `Create(items, nextRequest)` returns `PageInfo<T>(items(page), nextRequest(page, response, template))`;
  null delegates → `ArgumentNullException`.
- **Contract (`PAGE-4`, `PAGE-5`):** an empty item list with a non-null next is allowed (`PageInfo` carries both); the same strategy
  instance parsed by two threads concurrently (`Parallel.For` over 200 envelopes) yields independent, correct results.
- **Argument validation:** null `items`/`nextCursor`; null/empty `queryParameter`; a `queryParameter` containing a lone surrogate →
  `ArgumentException` at construction (so a bad name fails early, P7c-10); the same three checks for `LinkHeader`'s `headerName` (null, empty,
  lone surrogate).

**Production change.**

- `DelegateStrategy<TPage,T>(Func<TPage,IReadOnlyList<T>> items, Func<TPage,Response,Request,Request?> nextRequest)`.
- `CursorStrategy<TPage,T>`: `cursor = nextCursor(page)`; `string.IsNullOrEmpty(cursor)` → `new PageInfo<T>(items(page), null)`; else
  `template.WithUrl(QuerySplice.Set(template.Url, queryParameter, cursor))`.
- `PageNumberStrategy<TPage,T>`: `var list = items(page)`; `ArgumentNullException.ThrowIfNull(list)`; empty → end; `raw =
  QuerySplice.Get(response.Request.Url, queryParameter)`; `current = raw is { Length: > 0 } && int.TryParse(raw,
  NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : startPage`; `current == int.MaxValue` → end; else
  `template.WithUrl(QuerySplice.Set(template.Url, queryParameter, (current + 1).ToString(CultureInfo.InvariantCulture)))`.
- `LinkHeaderStrategy<TPage,T>`: `values = response.Headers.GetAll(headerName)`; none → end; `raw = LinkHeaderParser.FindNext(string.Join(", ",
  values))`; `LinkTarget.TryResolve(response.Request.Url, raw, template.Url, allowCrossOrigin, out var target)` →
  `template.WithUrl(target)` else end.
- `PaginationStrategies` (public, new overloads, each validating and returning `IPageStrategy<TPage,T>`): `Create<TPage,T>(items,
  nextRequest)`, `Cursor<TPage,T>(items, nextCursor, queryParameter = "cursor")`, `PageNumber<TPage,T>(items, queryParameter = "page",
  startPage = 1)`, `LinkHeaderNext<TPage,T>(items, headerName = "Link", allowCrossOrigin = false)` (temporary name, RS0026; renamed `LinkHeader` in 2.5). `LinkHeader` validates `headerName`: `ArgumentException.ThrowIfNullOrEmpty` plus the lone-surrogate rejection, as for `queryParameter`. XML docs cite the IDs; the
  `LinkHeader` remark explains the cross-origin guard and that per-call auth stamps credentials for any origin it follows
  (`allowCrossOrigin: true` is the opt-in). The `PageNumber` remark states Breaking 3 (an extra empty exchange where a `hasMore`
  predicate used to stop) and points at `Create` for a predicate. **Breaking:** the class remark lists Breaking 2–5 (the old
  overloads are removed in 2.5; mark here, once).

**PublicAPI diff.** Add the four `static Dexpace.Sdk.Core.Pagination.PaginationStrategies.*<TPage, T>` lines (the link one as `LinkHeaderNext`), each
`-> Dexpace.Sdk.Core.Pagination.IPageStrategy<TPage, T>!`. The old three lines stay for now.

**Verify:** `V-fast` with `PaginationStrategyParseTests`; build.

**IDs:** `PAGE-4`, `PAGE-5`, `PAGE-16`, `PAGE-17`, `PAGE-18`, `PAGE-19`, `PAGE-20`, `PAGE-23`.

## Task 2.3 — The single-use enumerables (`PAGE-14`; fact 8)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/SingleUseAsyncEnumerable.cs`, `SingleUseEnumerable.cs`;
`tests/Dexpace.Sdk.Core.Tests/Pagination/SingleUseEnumerableTests.cs`.

**Failing tests first:**

```csharp
[Fact]
public async Task The_second_GetAsyncEnumerator_throws_and_the_first_still_works()
{
    var view = new SingleUseAsyncEnumerable<int>(_ => Items());
    await using var first = view.GetAsyncEnumerator(TestContext.Current.CancellationToken);
    Assert.True(await first.MoveNextAsync());
    var ex = Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator());
    Assert.Contains("single-use", ex.Message, StringComparison.Ordinal);
    static async IAsyncEnumerable<int> Items() { yield return 1; await Task.CompletedTask; }
}
```

Also: the factory is not invoked until the first `GetAsyncEnumerator` (a counter proves laziness, `PAGE-6`); the latch is race-safe
(`Parallel.For` of 64 callers: exactly one succeeds, the rest throw); the token passed to `GetAsyncEnumerator` reaches the iterator's
`[EnumeratorCancellation]` parameter (an iterator that yields until its token is cancelled stops); a subclass of `AsyncPageable<int>` whose `WalkPagesAsync` has **no** `[EnumeratorCancellation]` (and one that is a non-iterator method) is cancelled through `AsPages().WithCancellation(token)` and observes the token; the blocking twin has the same
three facts over `IEnumerable<T>`.

**Production change.** `SingleUseAsyncEnumerable<T>(Func<CancellationToken, IAsyncEnumerable<T>> factory) : IAsyncEnumerable<T>` with
`private int _used;` and `GetAsyncEnumerator(CancellationToken ct)`: `if (Interlocked.Exchange(ref _used, 1) != 0) throw new
InvalidOperationException("This page view is single-use; call AsPages() again for a new walk.");` then `return
factory(ct).GetAsyncEnumerator(ct);` (passing `ct` to both means a subclass whose `WalkPagesAsync` lacks `[EnumeratorCancellation]`, or is not an iterator, still sees AsPages cancellation; a compiler iterator that does have the attribute combines identical tokens harmlessly). `SingleUseEnumerable<T>(Func<IEnumerable<T>>)` is the blocking twin. Both `internal sealed`.

**PublicAPI:** none. **Verify:** `V-fast` with `SingleUseEnumerableTests`.

**IDs:** `PAGE-14`, `PAGE-6`.

## Task 2.4 — `PageWalk` and `PageStep`: the one fetch-and-parse step (`PAGE-4`, `PAGE-11`, `PAGE-13`, `PAGE-15`, `PAGE-27`, `PAGE-28`, `PAGE-32`, `PAGE-33`; P7c-4, P7c-14, P7c-17; R1, R5)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/PageWalk.cs`, `PageStep.cs`, `FetchedStep.cs`;
`tests/Dexpace.Sdk.Core.Tests/Pagination/PageStepTests.cs`.

**Failing tests first** (compile error). All drive `PageStep.FetchAsync` directly with a `ScriptedTransport` / `SyncFirstTransport`,
`EnvelopeSerde`, and a delegate strategy; `TrackingResponseBody` / `DisposalCountingBody` from `TestSupport` count disposes.

```csharp
[Fact]
public async Task A_parse_failure_closes_the_response_once_and_the_parse_error_is_primary()
{
    var log = new List<string>();
    var body = new TrackingResponseBody(log, disposeFailure: new IOException("close failed"));   // serves a valid envelope (see 2.1 fixtures), not the default [1,2,3,4]
    var transport = new ScriptedTransport((Request r) => TestResponses.Create(Status.Ok, r, body: body));
    var walk = Walk(transport, strategy: Throwing(new StrategySentinelException("parse failed")));   // private sentinel; EnvelopeSerde never throws it
    var ex = await Assert.ThrowsAsync<StrategySentinelException>(async () => await PageStep.FetchAsync(walk, walk.First, async: true, Ct));
    Assert.Equal(1, body.DisposeCount);
    Assert.Contains(ExceptionTrail.GetSuppressed(ex), s => s is IOException { Message: "close failed" });
}
```

(Read `ExceptionTrail`'s public/internal accessor name first and use it; the design says "dispose failure lands on `ExceptionTrail`".)

Remaining cases:

- **Success:** a page with the snapshot of the strategy's items (mutating the strategy's list afterwards does not change
  `Page.Values`), `Status`, `Headers`, and `Page.Request` equal to the **sent** request (not `response.Request`); `Next` is the
  strategy's; the response disposed exactly once **before** `FetchAsync` returns (`PAGE-11`, entry 17).
- **Success-path dispose failure (`PAGE-15`, `PAGE-32`):** a body whose dispose throws → `FetchAsync` throws that exception unwrapped.
- **Null response (`PAGE-28`):** a transport returning `null` (a `Func<Request,Response>` returning `null!`) → `InvalidOperationException`
  whose message names the client type and `SEAM-16`.
- **Null envelope (P7c-17, `SERDE-13`):** body `null` → `DeserializationException` naming `Envelope`; the response is closed once.
- **Null `PageInfo` (`PAGE-4`):** a strategy returning `null!` → `InvalidOperationException` naming the strategy type; closed once.
- **Late response (`PAGE-33`, P7c-14):** a transport that cancels the token **then** returns a response → the response is disposed
  once, `OperationCanceledException` is thrown, the serde is never called (a counting serde proves it).
- **Fatal exceptions are not intercepted** by the close path: an `OutOfMemoryException` from the strategy propagates and the
  test does not assert a dispose (mirror how other suites treat `ExceptionFacts.IsFatal`; if a fatal exception is awkward to
  raise, assert on `IsFatal`-gated behaviour with a custom exception the tests cannot make fatal and skip this row, recording the
  gap in the checklist).
- **Sync flag (P7c-4):** with `async: false` over `SyncFirstTransport` (whose `ExecuteAsync` throws) the step never calls
  `ExecuteAsync`, and the returned `ValueTask` is already complete (`IsCompletedSuccessfully`); `SyncPath.GetCompletedResult` on it
  returns the result. Every path (success, parse failure, null envelope, null `PageInfo`, null response, dispose failure) with `async: false`
  returns an already-completed `ValueTask` (faulted counts as completed): assert `IsCompleted` for each. (No suspending-read case: no `await` runs on
  the sync path, so it cannot be built against `PageStep`; the `PIPE-28` throw is `SyncPath`'s own test.)
- **Options pass-through (`PAGE-36`):** the `RequestOptions` instance on the transport's `RecordedCall` is the walk's (reference
  equality).

**Production change.**

- `PageWalk<TPage,T>` (`internal sealed class`): `IAsyncHttpClient? AsyncClient`, `IHttpClient? BlockingClient`, `Request First`,
  `ISerde Serde`, `IPageStrategy<TPage,T> Strategy`, `RequestOptions Options`, `int? MaxPages`; two internal static factories
  `ForAsync(...)`/`ForBlocking(...)` do the guards (R5).
- `FetchedStep<T>` (`internal readonly record struct (Page<T> Page, Request? Next)`).
- `PageStep.FetchAsync<TPage,T>(PageWalk<TPage,T> walk, Request current, bool async, CancellationToken ct)` per the design's
  pseudocode, split so each method is under 70 lines: `FetchAsync` (send, null check, late-cancel discard) and
  `ParseAsync` (read, deserialize, parse, snapshot, `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` →
  `CloseAsync(response, ex, async)` then `throw;`), then the unguarded success-path dispose
  (`if (async) await response.DisposeAsync().ConfigureAwait(false); else response.Dispose();`). `ReadEnvelopeAsync<TPage>`:
  `async` → `OpenReadAsync` + `await using` + `serde.DeserializeAsync<TPage>(stream, ct)`; sync → `serde.Deserialize<TPage>(
  response.Body.ReadAsBytes(ct))`. `CloseAsync(response, primary, async)` calls `Disposal.DisposeQuietlyAsync` or
  `Disposal.DisposeQuietly` (R1). **No `await` runs when `async` is false**: every await expression is under the flag, so the state
  machine completes synchronously (the test above proves it). The null-envelope exception is `new DeserializationException(
  $"The serde returned null for page type '{typeof(TPage).FullName}'; a page envelope must be non-null.")`.

**PublicAPI:** none. **Verify:** `V-fast` with `PageStepTests`.

**IDs:** `PAGE-4`, `PAGE-11`, `PAGE-13`, `PAGE-15`, `PAGE-27`, `PAGE-28`, `PAGE-32`, `PAGE-33`, `PAGE-36`; `SERDE-13` (the null
envelope; other owner's row).

## Task 2.5 — The async cutover: bases, `StrategyPageable`, `Pageable.Create`, old surface removed (`PAGE-1`, `PAGE-6`–`PAGE-10`, `PAGE-14`, `PAGE-25`, `PAGE-26`, `PAGE-31`, `PAGE-36`; P7c-3, P7c-5, P7c-6, P7c-13; R3) — **Breaking 1, 2, 6, 8, 9**

One commit: this is the task where signatures change, so every caller moves with it.

**Files.** Edited: `src/Dexpace.Sdk.Core/Pagination/AsyncPageable.cs`, `Pageable.cs`, `PaginationStrategies.cs` (delete the three old
overloads, rename `LinkHeaderNext` to `LinkHeader` and update the 2.2 tests, and delete the old private helpers `SetQueryParameter`/`GetQueryParameter`/`LinkHeader` parsing), `PublicAPI.Unshipped.txt`,
`tests/Dexpace.Sdk.Core.Tests/Pagination/PageableTests.cs` (rewritten), `tests/Dexpace.Sdk.Core.Tests/Pipeline/PipelineAsTransportTests.cs`
(line 5 comment now says `Pageable.Create` takes the transport seam, and a pipeline passes as one). New:
`src/Dexpace.Sdk.Core/Pagination/StrategyPageable.cs`. Deleted: `tests/Dexpace.Sdk.Core.Tests/Pagination/PaginationStrategiesTests.cs`
(its coverage moved to `PaginationStrategyParseTests`, task 2.2), then `git mv PaginationStrategyParseTests.cs
PaginationStrategiesTests.cs` and rename the class (R3).

**Failing tests first.** Rewrite `PageableTests` against the new factory (compile error until the production change). Helper:

```csharp
private static AsyncPageable<int> Make(IAsyncHttpClient client, int? maxPages = null, RequestOptions? options = null) =>
    Pageable.Create<Envelope, int>(client, Request.Get("https://api.example/items"), new EnvelopeSerde(),
        PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next), options, maxPages);

[Fact]
public async Task The_item_view_flattens_pages_in_server_order()
{
    var transport = new ScriptedTransport(PageFixtures.Respond([1, 2], "b"), PageFixtures.Respond([3], "c"), PageFixtures.Respond([4, 5]));
    var items = new List<int>();
    await foreach (var item in Make(transport).WithCancellation(Ct)) { items.Add(item); }
    Assert.Equal([1, 2, 3, 4, 5], items);
    Assert.Equal(3, transport.CallCount);
}
```

Keep the 24 existing facts' *intent* (port each to the new factory; the old names that remain true are kept). Also port every case of `../nodejs-sdk/packages/core/src/pagination/paginator.test.ts` (9 cases) and `cancellation.test.ts` (4 cases) at `c0ff3fd`, dropping only JavaScript-host cases (listed in the commit message), and add:

- **`PAGE-1`:** a three-page fixture through `AsPages()` asserts per-page `Status`, `Headers` (a distinct `X-Page` header per page)
  and `Page.Request.Url` (the sent URLs, in order, with the cursor); the item view and page view see identical items (compare the
  flattened `AsPages()` to the item view over a fresh walk).
- **`PAGE-6`:** `Pageable.Create`, `AsPages()`, and `GetAsyncEnumerator()` each issue zero exchanges (`CallCount == 0`); the first
  `MoveNextAsync` issues one.
- **`PAGE-7`:** after the last page, three further `MoveNextAsync` return `false` with `CallCount` unchanged; an empty page with a
  non-null next costs exactly one more exchange and still continues.
- **`PAGE-8`:** two sequential `await foreach` walks restart from `first` (CallCount doubles; the first request of the second walk
  has no cursor); two concurrent walks over a `RecordingTransport` responder that echoes by cursor are independent.
- **`PAGE-9`/`PAGE-10`:** `maxPages: 2` stops after two exchanges though page 2 has a next; `maxPages` of `0` and `-1` throw
  `ArgumentOutOfRangeException` **at `Create`**, with `ParamName == "maxPages"`; `maxPages: null` walks 1,000 synchronously
  completed pages.
- **`PAGE-5`:** one built-in strategy instance (built once, outside `Make`) is passed to two pageables, or one pageable is walked twice concurrently
  (`Task.WhenAll`), over a `RecordingTransport` responder that echoes by cursor; both walks yield their own correct items and sent URLs.
- **`PAGE-14`:** `AsPages()` view: second `GetAsyncEnumerator` throws `InvalidOperationException`; two `AsPages()` calls are two
  independent single-use views; the item view's `GetAsyncEnumerator` may be called repeatedly.
- **`PAGE-25`/`PAGE-26`:** a transport that blocks on its token (a `TaskCompletionSource` completed by `ct.Register`) observes
  cancellation when the consumer's `WithCancellation` token is cancelled mid-walk, and no further exchange happens; cancelling
  **between items of a fetched page** does not drop that page's remaining items (the token is checked at the next page).
- **`PAGE-31`:** 10,000 synchronously completed pages through `await foreach` finish without a stack overflow (a `Func<Request,
  Response>` transport generating pages from the cursor).
- **`PAGE-36`:** a custom `RequestOptions` (`WithTag`) reaches every `RecordedCall` as the **same instance**; omitted → `RequestOptions.Empty`
  is the instance seen.
- **`PIPE-26`:** a fact named `A_pipeline_backing_a_paginator_works_as_the_client` builds `new PipelineBuilder().Build(transport)` and
  passes it as the `IAsyncHttpClient` (the conversion the design relies on) and walks two pages; `PipelineAsTransportTests` keeps its
  pointer.
- **Construction guards:** null `client`/`first`/`serde`/`strategy` → `ArgumentNullException`; a `first` with a non-replayable stream body
  (`RequestBody.FromStream(new NonSeekableStream(), …)`; use a stream type `CanSeek == false`) → `ArgumentException` with `ParamName == "first"`
  and a message containing `ToReplayableAsync`; a `RequestBody.FromString` template passes and is re-sent on every page (a
  `RecordedCall` check that each page's request body bytes are intact).
- **Subclassing (`Q1`):** a test double `class FakePageable : AsyncPageable<int>` overriding `WalkPagesAsync` gets `AsPages()`
  single-use and the item view for free.

**Production change.**

- `AsyncPageable<T>`: add the protected parameterless constructor (public-API line), `public IAsyncEnumerable<Page<T>> AsPages()` →
  `new SingleUseAsyncEnumerable<Page<T>>(WalkPagesAsync)`; `public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken
  cancellationToken = default)` → a private `async IAsyncEnumerable<T> ItemsAsync([EnumeratorCancellation] CancellationToken ct)`
  that `await foreach`es `WalkPagesAsync(ct).WithCancellation(ct).ConfigureAwait(false)` and yields `page.Values`; `protected
  abstract IAsyncEnumerable<Page<T>> WalkPagesAsync(CancellationToken cancellationToken);`. The remarks state: lazy, one exchange
  per page, nothing live at a `yield` (entry 17), the `PAGE-10` advice to set a finite `maxPages` in production, `PAGE-33`'s race
  (a response the transport builds after a cancel is never delivered and is the transport's to release, `TRANSPORT-9`; one delivered
  after the cancel is disposed and discarded), and the one subclass obligation, plus the `PAGE-12` scoping note ("tell consumers to scope it": enumerate with `await foreach`, or `await using` a hand-driven enumerator, so the walk is disposed even on early exit). **Breaking:** `AsPages(int? pageSizeHint = null)` and
  `GetAsyncEnumerator` were abstract; `pageSizeHint` is gone; `AsPages` returns a single-use view.
- `StrategyPageable<TPage,T>(PageWalk<TPage,T> walk) : AsyncPageable<T>` with `WalkPagesAsync` the design's loop: `ct.ThrowIfCancellationRequested()`
  at the top of each page (`PAGE-26`); `maxPages` check; `await PageStep.FetchAsync(walk, current, async: true, ct).ConfigureAwait(false)`;
  `fetched++`; `yield return step.Page`; `step.Next is null → yield break`.
- `Pageable.Create<TPage,T>(IAsyncHttpClient client, Request first, ISerde serde, IPageStrategy<TPage,T> strategy, RequestOptions? options =
  null, int? maxPages = null)`: guards (`ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIfNegativeOrZero` when `maxPages` has a value,
  the replayable-body check), `options ?? RequestOptions.Empty` captured once, `PageWalk.ForAsync(...)`. The `HttpPipeline`-typed
  overload and `DexpaceClientOptions` import are deleted. **Breaking** remarks on `Create` list Breaking 1, 8 and 9.
- `PaginationStrategies`: delete the old overloads (**Breaking 2**) and the private query helpers.

**PublicAPI diff.** Delete the old `abstract AsPages(int? …)`, `abstract GetAsyncEnumerator(…)`, the old `Pageable.Create<TPage, T>(HttpPipeline!
…)` and the three old `PaginationStrategies` lines; rename the `LinkHeaderNext` line to `LinkHeader`; add `AsyncPageable<T>.AsPages() -> IAsyncEnumerable<Page<T>!>!`, `GetAsyncEnumerator(…) ->
IAsyncEnumerator<T>!`, `abstract WalkPagesAsync(CancellationToken) -> IAsyncEnumerable<Page<T>!>!` (protected; the file's prefix is
`abstract`) and the new `static Pageable.Create<TPage, T>(IAsyncHttpClient! client, Request! first, ISerde! serde, IPageStrategy<TPage, T>!
strategy, RequestOptions? options = null, int? maxPages = null) -> AsyncPageable<T>!`. Let the analyzer's code fix produce the exact text.

**Verify:** `V-fast` with `PageableTests`, `PaginationStrategiesTests`, `PipelineAsTransportTests`; then `dotnet build Dexpace.Sdk.sln --configuration
Release` (any leftover caller of the removed surface is a compile error; there should be none after the steps above).

**IDs:** `PAGE-1`, `PAGE-5` (shared-instance walk), `PAGE-6`, `PAGE-7`, `PAGE-8`, `PAGE-9`, `PAGE-10`, `PAGE-14`, `PAGE-25`, `PAGE-26`, `PAGE-31`, `PAGE-36`; `PIPE-26` (other owner).

## Task 2.6 — The blocking pager: `Pageable<T>`, `StrategyBlockingPageable`, `CreateBlocking` (`PAGE-1`, `PAGE-6`, `PAGE-7`, `PAGE-9`, `PAGE-14`, `PAGE-31`; P7c-4, P7c-5; R1)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/PageableOfT.cs` (the type is `Pageable<T>`; the file is named so it does not clash with
`Pageable.cs`, the arity-0 factory class — the repository's styleguide names files for the type, so note the exception in the file
header comment), `StrategyBlockingPageable.cs`; `tests/Dexpace.Sdk.Core.Tests/Pagination/BlockingPageableTests.cs`. Edited:
`Pageable.cs` (add `CreateBlocking`), `PublicAPI.Unshipped.txt`. The `Pageable<T>` remarks carry the matching `PAGE-12` note (`foreach` / `using` scoping of the enumerator).

**Failing tests first** (compile error):

```csharp
[Fact]
public void The_blocking_walk_uses_Execute_never_ExecuteAsync_and_flattens_in_order()
{
    var transport = new SyncFirstTransport(r => PageFixtures.Respond([1, 2], r.Url.Query.Contains("cursor") ? null : "b")(r));
    var pageable = Pageable.CreateBlocking<Envelope, int>(transport, Request.Get("https://api.example/items"), new EnvelopeSerde(),
        PaginationStrategies.Cursor<Envelope, int>(e => e.Items, e => e.Next));
    Assert.Equal([1, 2, 1, 2], pageable.ToArray());
    Assert.Equal(2, transport.CallCount);
}
```

(`SyncFirstTransport.ExecuteAsync` throws, so a pass proves the blocking path never takes the async member.) Cases:

- **`PAGE-6`:** `CreateBlocking`, `AsPages()` and `GetEnumerator()` each issue zero exchanges; the first `MoveNext` issues one.
- **`PAGE-1`/`PAGE-7`/`PAGE-9`:** page view with per-page status/headers/request; three `MoveNext` after the end change nothing;
  `maxPages` stops at the cap, `0`/`-1` throw at construction.
- **`PAGE-14`:** the second `GetEnumerator` on one `AsPages()` view throws; the item view re-enumerates a fresh walk.
- **`PAGE-31`:** 10,000 synchronous pages, no stack overflow.
- **The token captured by `CreateBlocking`:** a pre-cancelled token → `OperationCanceledException` from the first `MoveNext`, zero
  exchanges; a token cancelled mid-walk stops before the next page; the `RecordedCall`'s token is the factory's.
- **Options (`PAGE-36`):** the same `RequestOptions` instance on each `RecordedCall` (use `RecordingSyncTransport`).
- **Close/failure parity:** parse failure closes once with the parse error primary (reuse the 2.4 matrix over `Execute`); a success-path
  dispose failure surfaces from `MoveNext` unwrapped.
- **A suspending read is a defect, not a hang:** a response whose body `ReadAsBytes` is fine but a transport that returns a not-yet-complete
  task cannot occur on the sync interface, so this row is the 2.4 `GetCompletedResult` pin; do not duplicate it.

**Production change.**

- `Pageable<T>` (`public abstract class Pageable<T> : IEnumerable<T>`): protected ctor; `public IEnumerable<Page<T>> AsPages()` →
  `new SingleUseEnumerable<Page<T>>(WalkPages)`; `public IEnumerator<T> GetEnumerator()` → a private iterator flattening `WalkPages()`;
  explicit `IEnumerable.GetEnumerator`; `protected abstract IEnumerable<Page<T>> WalkPages();`. XML remarks: blocking twin of
  `AsyncPageable<T>`; thread-blocking until 8b (`PIPE-28`, the inherited ⏳); each page is buffered through `ReadAsBytes`, so a page
  envelope over the 64 MiB materialisation cap fails (R6 of the design).
- `StrategyBlockingPageable<TPage,T>(PageWalk<TPage,T> walk, CancellationToken token)`: the design's loop, reading each step through
  `SyncPath.GetCompletedResult(PageStep.FetchAsync(walk, current, async: false, token), nameof(Pageable))`.
- `Pageable.CreateBlocking<TPage,T>(IHttpClient client, Request first, ISerde serde, IPageStrategy<TPage,T> strategy, RequestOptions? options =
  null, int? maxPages = null, CancellationToken cancellationToken = default)` with the same guards as `Create`; XML docs explain why the
  name differs from `Create` (a pipeline converts to both seams; overloads would be `CS0121`).

**PublicAPI diff.** Add `Pageable<T>` (type, protected ctor, `AsPages`, `GetEnumerator`, `abstract WalkPages`) and `static
Pageable.CreateBlocking<TPage, T>(IHttpClient! …, System.Threading.CancellationToken cancellationToken = default) -> Pageable<T>!`.

**Verify:** `V-fast` with `BlockingPageableTests`, `PageableTests`; build.

**IDs:** `PAGE-1`, `PAGE-6`, `PAGE-7`, `PAGE-9`, `PAGE-14`, `PAGE-31`, `PAGE-36`; `PIPE-28` (inherited, not owned).

## Task 2.7 — The lifecycle suite (`PAGE-4`, `PAGE-11`, `PAGE-12`, `PAGE-13`, `PAGE-15`, `PAGE-27`, `PAGE-28`, `PAGE-32`, `PAGE-33`; entry 17)

**Files.** New: `tests/Dexpace.Sdk.Core.Tests/Pagination/PaginationLifecycleTests.cs`. These are **pins** over the public API for rules the
step (2.4) and the shells (2.5, 2.6) already implement; prove each can fail by temporarily removing the matching close/guard
(never committed). Ported from Node `lifecycle.test.ts` (13 cases), adapted to entry 17: "closes the held page" becomes "no
response is open at any yield".

Cases (async and, where noted, blocking via a `[Theory]` over a `bool blocking` or two facts):

- **`PAGE-11`:** `EarlyBreak_FirstBodyDisposed_AndTransportCalledOnce` (the existing fact, kept) plus: when the first item of page 1 is
  delivered, the page-1 body's `DisposeCount` is already 1 (stronger than the letter); taking one item and stopping costs one exchange.
- **`PAGE-12`:** a hand-driven enumerator (`var e = pageable.GetAsyncEnumerator(); await e.MoveNextAsync();` then dropped without
  `DisposeAsync`) leaves `disposed count == fetched count` for every body.
- **`PAGE-13`:** the parse-throws case with the dispose also throwing — the thrown exception is the parse error and the dispose failure
  is on its `ExceptionTrail` suppressed list; a parse failure on page 2 does not re-dispose page 1.
- **`PAGE-15`/`PAGE-32`:** a success-path dispose failure ends the walk with that exception from `MoveNextAsync`/`MoveNext`, does not hang, and
  a further `MoveNextAsync` does not issue another exchange.
- **`PAGE-27`** (bodies serve a valid envelope or the empty-body result per 2.1; the strategy-throws path uses the sentinel exception): exactly-once close per path — success, parse failure, null envelope, null `PageInfo`, late response after cancel — with
  `DisposalCountingBody` per page; the matrix totals are asserted, not just "not zero".
- **`PAGE-28`:** the cause matrix, each surfacing as the original type: transport fault (`IOException` from the script), eager throw (the
  transport throws synchronously), parse throw, consumer throw inside the `await foreach` body (the response is already closed), null
  response (`InvalidOperationException`).
- **`PAGE-4`:** a null `PageInfo` closes the response and throws `InvalidOperationException` naming the strategy type.
- **`PAGE-33`:** a late response after cancel is discarded and disposed (the 2.4 case, now through the public walk: `OperationCanceledException`
  surfaces from `MoveNextAsync`, one dispose, no item delivered).

**Production change:** none expected; a failing pin is a defect in 2.4/2.5/2.6 and is fixed there.

**Verify:** `V-fast` with `PaginationLifecycleTests`.

**IDs:** `PAGE-4`, `PAGE-11`, `PAGE-12`, `PAGE-13`, `PAGE-15`, `PAGE-27`, `PAGE-28`, `PAGE-32`, `PAGE-33`.

## Task 2.8 — Architecture tests and the `Link` origin `Security` class (`PAGE-3`, `PAGE-11`, `PAGE-14`, `PAGE-19`; P7c-12; R6, R7; facts 4, 9)

**Files.** New: `tests/Dexpace.Sdk.Core.Tests/Architecture/PaginationArchitectureTests.cs` (`Unit`);
`tests/Dexpace.Sdk.Core.Tests/Security/PaginationLinkOriginTests.cs` (**`Security`**).

**Failing tests first.** `PaginationLinkOriginTests` is the permanent proof that a server cannot take a credential with one header:

```csharp
[Trait("Category", "Security")]
public sealed class PaginationLinkOriginTests
{
    [Theory]
    [InlineData("<https://evil.example/p2>; rel=next")]
    [InlineData("<//evil.example/p2>; rel=next")]                          // network-path reference (fact 4)
    [InlineData("<http://api.example/p2>; rel=next")]                      // scheme downgrade is another origin
    [InlineData("<https://api.example:8443/p2>; rel=next")]                // port change
    public async Task A_cross_origin_next_link_ends_the_walk_and_no_request_reaches_the_other_origin(string link)
    {
        var transport = new ScriptedTransport(
            (Request r) => PageFixtures.Respond([1], null, new Headers.Builder().Set("Link", link).Build())(r),
            (Request r) => PageFixtures.Respond([2])(r));
        var pipeline = new PipelineBuilder().Add(new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope")).Build(transport);
        var items = new List<int>();
        await foreach (var item in Pageable.Create<Envelope, int>(pipeline, Request.Get("https://api.example/items"), new EnvelopeSerde(),
            PaginationStrategies.LinkHeader<Envelope, int>(e => e.Items)).WithCancellation(Ct)) { items.Add(item); }
        Assert.Equal([1], items);
        Assert.Equal(1, transport.CallCount);
        Assert.All(transport.Requests, r => Assert.Equal("api.example", r.Url.Host));
    }
}
```

Also: with `allowCrossOrigin: true` the walk **does** follow (and the test asserts the second request is stamped, documenting the
warning in the XML doc — this row is a `Unit` fact in `PaginationStrategiesTests`, not a `Security` fact, so it can be reordered
without touching the Security class); a userinfo-bearing same-origin target is followed with the userinfo removed (the
second `RecordedCall`'s `Url.UserInfo` is empty and the `Authorization` header is the policy's stamp); a first page that
**redirected** to another origin does not license following links there (build with `RedirectPolicy`; script a 302 to `https://other.example/items`, then a 200 from `other.example` whose **`Link` header** is
`<https://other.example/p2>; rel=next` — template origin is `api.example`, so the walk ends; assert exactly one follow-up exchange (the redirect hop) and **no request
to `/p2`**. This case sits in the `Security` class (`PaginationLinkOriginTests`), unlike the `allowCrossOrigin: true` fact). Copy `FixedTokenCredential` into the class (R7; `private sealed class`).

`PaginationArchitectureTests` (reflection over `typeof(Page<>).Assembly`'s `Dexpace.Sdk.Core.Pagination` namespace): `Page<>`, `PageInfo<>`,
`FetchedPage<>` implement neither `IDisposable` nor `IAsyncDisposable` (entry 17); `AsPages` / `GetAsyncEnumerator` / `GetEnumerator`
are non-virtual on `AsyncPageable<>` and `Pageable<>`; `WalkPagesAsync`/`WalkPages` are `protected abstract`; no public type in the
namespace has a public property or method parameter of type `Response` **except** `IPageStrategy<,>.Parse` and the `Func` the
`Create` adapter takes (assert the allow-list by name, so a new leak is a red test); `Page<>`, `PageInfo<>`, `FetchedPage<>` have no
public setter that is not `init` and no property of type `List<>`/`Dictionary<,>`/array (R6; `PagingOptions` is excluded by name,
with the `PAGE-35` rationale in the comment). `FetchedPage<>` assertions skip (not fail) until task 3.1 adds the type; **3.1
removes the skip** and the test list carries a `// 3.1` marker.

**Production change:** none expected (these assert what 2.1–2.6 built). If the `redirected first page` case ends differently, the
defect is in `LinkHeaderStrategy` passing the wrong origin (it must be `template.Url`, P7c-12).

**Verify:** `V-fast` with `PaginationArchitectureTests`, then **V-sec**.

**IDs:** `PAGE-3`, `PAGE-14`, `PAGE-19`; the P7c-12 guard (`Security`).

## Task 2.9 — Close-out (group 2)

1. `V-gate`, **V-sec first**. The `Security` diff check lists exactly `PaginationLinkOriginTests.cs`.
2. Confirm the PublicAPI file's pagination block equals the design's "Added" list minus group 3's `FetchedPage`/`PagingOptions`/`FromFetchers`.
3. Confirm no leftover reference to `HttpPipeline` or `DexpaceClientOptions` in `src/Dexpace.Sdk.Core/Pagination/` (`grep`).

---

# Group 3 — Fetchers, wire and AOT evidence, documentation and corrections

## Task 3.1 — `FetchedPage<T>`, `PagingOptions`, `Pageable.FromFetchers` (`PAGE-8`, `PAGE-9`, `PAGE-14`, `PAGE-34`, `PAGE-35`; P7c-15, P7c-16, Q2)

**Files.** New: `src/Dexpace.Sdk.Core/Pagination/FetchedPage.cs`, `PagingOptions.cs`, `FetcherPageable.cs`;
`tests/Dexpace.Sdk.Core.Tests/Pagination/FetcherPageableTests.cs`. Edited: `Pageable.cs` (add `FromFetchers`),
`PaginationArchitectureTests.cs` (remove the 2.8 skip), `PublicAPI.Unshipped.txt`.

**Failing tests first** (compile error), ported from Node `fetchers.test.ts` (12 cases):

```csharp
[Fact]
public async Task The_next_link_wins_over_the_continuation_token_and_both_are_written_to_the_options()
{
    var seen = new List<(string Key, string? Link, string? Token)>();
    var pageable = Pageable.FromFetchers<int>(
        (o, ct) => new ValueTask<FetchedPage<int>?>(Page([1], "https://api.example/p2", "tok")),
        (key, o, ct) => { seen.Add((key, o.NextLink, o.ContinuationToken)); return new ValueTask<FetchedPage<int>?>(Page([2])); });
    var items = new List<int>();
    await foreach (var i in pageable.WithCancellation(Ct)) { items.Add(i); }
    Assert.Equal([1, 2], items);
    Assert.Equal([("https://api.example/p2", "https://api.example/p2", "tok")], seen);
}
```

with a local `Page(...)` helper building `new FetchedPage<int>(new Page<int>(values, Status.Ok, Headers.Empty, Request.Get("https://api.example/")),
nextLink, token)`. Cases:

- **`PAGE-34`:** the first fetcher runs exactly once per walk; the next key is `NextLink` when not blank, else `ContinuationToken` when non-empty,
  else the walk ends; a blank link with no token ends; a `null` page from `nextPage` ends; a `null` **first** page is an empty stream; a
  `FetchedPage` with a `null` `Page` throws `ArgumentNullException` at construction and via `with { Page = null! }`.
- **`PAGE-35`:** one `PagingOptions` instance is passed to **every** fetcher call of a walk (reference-equal across the first and next
  calls); a second walk gets a **different** instance (fresh state, `PAGE-8`); `State` is per-walk scratch (written by the first
  fetcher, read by the next).
- **`PAGE-9`:** `maxPages: 2` stops after two fetches even with a next key (the `nextPage` call count is 1); `0`/`-1` throw at
  construction; null fetchers → `ArgumentNullException`.
- **`PAGE-14`:** `AsPages()` single-use; the item view re-enumerates.
- **`PAGE-6`/laziness:** `FromFetchers` and `AsPages()` call no fetcher; the first `MoveNextAsync` calls the first.
- **Cancellation:** the token reaches both fetchers; cancelled between pages → `OperationCanceledException` before the next fetch.
- **Ownership (P7c-15):** a fetcher that disposes its own response with `await using` and returns a materialized page leaves a
  `DisposalCountingBody` at exactly one dispose; a fetcher that throws propagates unwrapped (the engine owns nothing).

**Production change.**

- `FetchedPage<T>` (`public sealed record FetchedPage<T>(Page<T> Page, string? NextLink = null, string? ContinuationToken = null)`, the spec's positional
  shape, **kept**): `Page` is redeclared in the record body as an explicit `init` property over a backing field (`public Page<T> Page { get => _page; init =>
  _page = value ?? throw new ArgumentNullException(nameof(value)); }` with `private readonly Page<T> _page = Page ?? throw new ArgumentNullException(nameof(Page));`
  initialised in the primary-constructor scope), so the positional constructor, `Deconstruct`, equality members and `<Clone>$` stay as in the spec while
  construction and `with { Page = null! }` both throw. If the compiler rejects redeclaring the positional parameter that way on the host, fall back to a
  non-positional record with an explicit `Deconstruct(out Page<T>, out string?, out string?)` — never drop `Deconstruct` — and record the fallback beside R5/R6. The remark states ownership (P7c-15: the fetcher closes its own response; the page owns none, so `PAGE-34`'s "ownership
  transfers" clause is inverted, a dated amendment to entry 17).
- `PagingOptions` (`public sealed class`): `NextLink`, `ContinuationToken` get/set, `IDictionary<string, object?> State { get; } = new
  Dictionary<string, object?>()`. The remark: mutable on purpose (`PAGE-35`), single-consumer, not thread-safe, one per walk.
- `FetcherPageable<T>` per the design's walk, with the cap checked **after** the yield so `maxPages: n` never starts fetch `n + 1`:
  `count++; yield return fetched.Page; if (maxPages is { } m && count >= m) yield break; key = NextKey(fetched); if (key is null) yield break;
  options.NextLink = fetched.NextLink; options.ContinuationToken = fetched.ContinuationToken; ct.ThrowIfCancellationRequested(); fetched =
  await next(key, options, ct).ConfigureAwait(false);`.
- `Pageable.FromFetchers<T>(Func<PagingOptions, CancellationToken, ValueTask<FetchedPage<T>?>> firstPage, Func<string, PagingOptions,
  CancellationToken, ValueTask<FetchedPage<T>?>> nextPage, int? maxPages = null)`. XML remark: async fetchers only; a blocking fetcher
  front-end is not built (Q2).

**PublicAPI diff.** Add `FetchedPage<T>` (the analyzer lists the record's generated members: constructor, `Page`/`NextLink`/`ContinuationToken`
`get`/`init`, `Deconstruct`, `Equals`, `GetHashCode`, `ToString`, `<Clone>$`, `==`/`!=`), `PagingOptions`
(type, ctor, four accessors, `State.get`) and `static Pageable.FromFetchers<T>(…) -> AsyncPageable<T>!`. Take the exact text from the code fix.

**Verify:** `V-fast` with `FetcherPageableTests`, `PaginationArchitectureTests`; build.

**IDs:** `PAGE-8`, `PAGE-9`, `PAGE-14`, `PAGE-34`, `PAGE-35`.

## Task 3.2 — Wire tests over the loopback server (`PAGE-18`, `PAGE-19`, `PAGE-20`, `PAGE-36`; `TRANSPORT-1`)

**Files.** New: `tests/Dexpace.Sdk.Http.SystemNet.Tests/PaginationWireTests.cs` (`[Trait("Category", "Integration")]`, `public sealed class`).
The SystemNet test project references Core and SystemNet; it must serialize the envelope itself, so the class holds a
private `sealed record Page(...)` and a tiny private `ISerde` (UTF-8 `"1,2|next"`) — do **not** reference TestSupport's `EnvelopeSerde` (it is in the
Core test project). Pattern: `ReasonPhraseWireTests` (`LoopbackServer.Start(script)`, `new SystemNetHttpClient(client)` over a
`SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }`).

**Failing tests first** (compile error until the class exists; green with no production change, so these are **pins**, proven by
temporarily making `LinkHeaderStrategy` read only the first header instance):

1. **A three-page `Link` walk** with the links split across **two header lines** on page 2 (`LoopbackResponse.Raw` with
   `Link: <…?page=3>; rel=next` after an earlier `Link: <…>; rel=prev` line) and a **query-only** reference on page 1
   (`Link: <?page=2>; rel=next`); the server's recorded request lines are `/items`, `/items?page=2`, `/items?page=3`; items arrive in order.
   Pins that `SystemNetHttpClient` surfaces each `Link` line as its own value and that `PAGE-20` joins them. Targets are built from
   `server.Url("/items")` so the origin is the loopback origin; the template origin equals it, so the guard passes.
2. **A cursor walk whose `RequestOptions.Timeout` governs every page** (`new RequestOptions { Timeout = TimeSpan.FromSeconds(30) }`): three
   pages succeed; the walk uses a short `Timeout` and the responder (`LoopbackServer.Start(Func<RecordedRequest, LoopbackResponse>)`) **blocks** on the page-2 request (a
   `ManualResetEventSlim` / `Thread.Sleep`-free wait released at test end) so the transport's own timeout fires; the walk throws
   `ServiceRequestTimeoutException` (the transport's `RequestOptions.Timeout` path, `SystemNetHttpClient.cs`; **not** `AttemptTimeoutWireTests`, which exercises
   `DexpaceClientOptions.AttemptTimeout` through `RetryPolicy`). Keep the row; drop it only if the check actually fails on the host, and then record the drop as
   a deviation from the spec's test table in the checklist.
3. **A cross-origin `Link` to a second loopback server** ends the walk (the second server's `Requests` is empty).

**Production change:** none. **Verify:** `dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-class "*PaginationWireTests"`.

**IDs:** `PAGE-18`, `PAGE-19`, `PAGE-20`, `PAGE-36` (wire evidence).

## Task 3.3 — NativeAOT smoke (`tests/Dexpace.Sdk.AotSmoke`; 7c's share of the exit's paged round trip)

**Files.** Edited: `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (one new method, one line in `RunAllAsync`),
`tests/Dexpace.Sdk.AotSmoke/SmokeModels.cs` (the envelope record and its own context). Read the file's check-registration shape first:
`SmokeChecks` is an `internal static class` of plain `CheckXxx` methods, throwing `SmokeFailureException` through `Expect(bool, string)`, with
no attribute or trait; the project references Core, `Http.SystemNet` and `Serialization.SystemTextJson` only, so `ScriptedTransport` is
**not** available: build the client with `DelegateHttpClient.Create` / `CreateBlocking`.

**Change.**

- `SmokeModels.cs`: `internal sealed record WidgetPage(List<Widget> Items, string? Next);` and `[JsonSerializable(typeof(WidgetPage))] internal
  sealed partial class PaginationSmokeContext : JsonSerializerContext;` — **its own context**, so 7a's context is not edited.
- `SmokeChecks.cs`: `await CheckPhase7cPaginationAsync();` after `CheckPhase6cAuthAsync()` in `RunAllAsync` (resolve the one-line conflict
  with 7a/7b at rebase), and `private static async Task CheckPhase7cPaginationAsync()`: (1) a three-page cursor walk through
  `DexpacePipeline` (use the creation call the existing checks use) over an in-memory `DelegateHttpClient` returning JSON bodies, deserialized by
  the STJ serde built over `PaginationSmokeContext` (read how `CheckPipelineJsonRoundTripAsync` builds its serde), asserting item order,
  `Page<T>.Request` URLs, and per-page status; (2) a `Link`-header walk over the same style of client; (3) `AsPages()` second
  `GetAsyncEnumerator` throws `InvalidOperationException`; (4) the same cursor walk through `Pageable.CreateBlocking` over `DelegateHttpClient.CreateBlocking`.
  The method is split into private helpers so none exceeds 70 lines (`MA0051` applies to tests/tools only if the rule is on there; follow the
  existing `CheckPhase6aRetryAsync` → `CheckPhase6aTimeoutAndCapabilityAsync` split).

**Verify:**

```bash
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
```

Expected: `aot-smoke: all checks passed`, with no new trim/AOT warnings (the project treats them as errors).

**IDs:** `PAGE-1`, `PAGE-14`, `PAGE-18`, `PAGE-19`, `PAGE-6` (AOT evidence).

## Task 3.4 — User documentation (`docs/sdk-documentation/pagination.md`)

New page, opening "As built by phase 7c … written against source on 2026-10-09" (read `docs/sdk-documentation/redirect.md` and `retry.md` for tone
and the section pattern; **reading and porting `../nodejs-sdk/docs/sdk-documentation/write-a-paging-strategy.md` is mandatory** (it is in the local sibling
checkout; spec exit criterion 4), as the structure of the "writing a custom strategy" section. Only the Ruby `sdk-documentation/pagination.md` is conditional: it is
absent locally, so it is never fetched without the lead's go-ahead, and its absence is recorded in the checklist and the roadmap status note). Sections: the two views (`AsPages()` single-use, item view re-enumerates) and why pages
are materialized (entry 17); the factories (`Create`, `CreateBlocking`, `FromFetchers`) and why the blocking one has a different name; the
strategies and the `Create` adapter; writing a custom strategy (`IPageStrategy`, build the next request from the template, read only
`response.Request.Url`); the cap advice (`PAGE-10`: set a finite `maxPages` in production); `PAGE-33`'s race; the `Link` cross-origin guard,
the userinfo strip and `allowCrossOrigin`, with the credential-stamping warning; the silent end on a rejected target (R8 of the design);
the fetcher ownership rule (the fetcher disposes its own response; `PagingOptions` is per walk); the blocking pager's 64 MiB per-page note and
`PIPE-28`'s inherited ⏳; the `PageNumber` extra-exchange note and the `Create` recipe for a `hasMore` predicate; the migration table (design,
"Breaking changes" 1–9). Also: `docs/sdk-documentation/pipelines.md` gains one sentence linking `pagination.md` where it discusses a pipeline as a
transport; `src/Dexpace.Sdk.Core/README.md` line 18 becomes `AsyncPageable<T>`, `Pageable<T>`, `Page<T>`, `PageInfo<T>`, `IPageStrategy<TPage,T>`,
`Pageable` (`Create`, `CreateBlocking`, `FromFetchers`), `PaginationStrategies`, `PagingOptions`, `FetchedPage<T>`.

**Verify:** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`.

**IDs:** `PAGE-10`, `PAGE-33` (documented clauses), `PAGE-12` (the `await using` note).

## Task 3.5 — The checklist

Write `docs/work/mvp/phase7/phase7c/…-phase7c-pagination-checklist.md` (path per the housekeeping filing in 3.7) from what was built: one row per
ID using this plan's table as the starting point, with the statuses and the tests actually observed. State: `PAGE-3` 🚫 citing §10 entry 17;
`PAGE-29`/`PAGE-30` 🚫 citing entry 19; `PAGE-21` ✅ with the entry 18 residual and the `PAGE-24` clause retired; the blocking rows ✅ with
`PIPE-28`'s inherited ⏳ against 8b stated in a line (not a 7c deferral); `PAGE-19` ✅ with the cross-origin reading (§11 item). Cites the
cross-owner rows' tests (`PIPE-26`, `SERDE-13`). Totals: 33 ✅, 3 🚫, 0 ⏳.

## Task 3.6 — Dated corrections, `CHANGELOG.md`, roadmap note, `CLAUDE.md` (design, "Corrections owed at close-out")

Each a dated correction (2026-10-09 or the merge date) naming its ruling:

1. **Design §7.1 (`docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md`):** the strategy contract, the seam-taking factories and
   `RequestOptions`, `FetchedPage<T>`, the `Link` cross-origin guard and userinfo strip (P7c-2, 5, 6, 12, 15); an "As built (phase 7c)" verdict.
   Do not edit the "As built (d45e64b)" line; append.
2. **§10 entry 17:** dated amendment (P7c-15; fetchers dispose their own responses). **§10 entry 18:** retire the `PAGE-24` clause (the splice no
   longer uses `UriBuilder`); the `PAGE-21` residual stands. **§11:** new item at the next free number on the merged file (P7c-12, the
   `PAGE-19` reading). **§12:** the PAGE row's "As built" counts.
3. **`docs/first-release.md`:** a "Behavioural asymmetries" entry for the `Link` cross-origin guard and `allowCrossOrigin`; the public-splice
   question (P7c-9) as a post-1.0 candidate.
4. **`CHANGELOG.md` `[Unreleased]`:** one entry under the file's existing structure (read how 6c's entry is laid out and match it) listing the nine
   breaking changes (strategy interface and seam-taking factories; the `PaginationStrategies` shapes; `PageNumber`'s extra empty exchange; `LinkHeader`'s
   guard, header-instances and base; case-sensitive splice and dropped duplicates; `AsPages()` single-use, `pageSizeHint` removed, non-virtual members;
   `Page<T>`'s fourth constructor argument; `maxPages <= 0` and non-replayable templates throwing at construction; `DeserializationException` for a
   null envelope), plus the additions (`CreateBlocking`, `FromFetchers`, `PagingOptions`, `FetchedPage<T>`, `Pageable<T>`, `PageInfo<T>`). IDs
   in the entry's heading: `PAGE-1..PAGE-36`. Re-derive the hunk from the merged file if 7a/7b landed first.
5. **`CLAUDE.md`:** the `Pagination/` layout line (design correction 6), and "What is genuinely unbuilt" drops "the remaining pagination surface (7)"
   (keeping tri-state PATCH and SSE if 7a/7b have not landed; edit only 7c's words) and gains a pointer to `docs/sdk-documentation/pagination.md`
   in the built-phases list.
6. **Roadmap** (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`): a dated Phase Status Note ("7c pagination built: 33 ✅ / 3 🚫 / 0 ⏳; the AOT smoke
   pages; the `Security` class; deviations P7c-3, P7c-6, P7c-12, P7c-16 were open for the lead and are recorded as taken-by-default"), and the Phase List
   row 7 gains the 7c design, plan and checklist links. Append-only; never rewrite an earlier note.

## Task 3.7 — File the phase documents, run the probe, final gate

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 7c            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 7c --write    # git mv the design, plan and checklist
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix what the last probe reports (misattributed requirement IDs, broken links, stale counts in `CLAUDE.md`/`README.md`). Then run the full
`V-gate` once more, **V-sec first**, and the coverage gate commands above. No push and no PR: those are the lead's actions (global rule); the PR
description would name the checklist, and 7c has no issue to close.

---

## Checklist: one row per owned ID

Exactly 36 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 3.5 records what was built. "Pin" means the
test passes on the as-built behaviour and is proven able to fail (convention 1). Level counts: 32 MUST, 4 SHOULD (`PAGE-10`, `PAGE-20`, `PAGE-31`,
`PAGE-35`).

| ID | Level | Group | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `PAGE-1` | MUST | 2, 3 | 2.5, 2.6, 3.3 | ✅ | `PageableTests.The_item_view_flattens_pages_…`, three-page status/headers/`Request`; `BlockingPageableTests`; AOT smoke |
| `PAGE-2` | MUST | 2 | 2.1 | ✅ | `PageInfoTests`, `PageTests` (null guards, `Request`); `PaginationArchitectureTests` immutability |
| `PAGE-3` | MUST | 2 | 2.1, 2.8 | 🚫 (§10 entry 17) | `PaginationArchitectureTests` (no `IDisposable` on pages); checklist cites entry 17 |
| `PAGE-4` | MUST | 2 | 2.1, 2.2, 2.4, 2.7 | ✅ | `PageInfoTests`; strategy `Parse` tests; null `PageInfo` closes and throws (`PageStepTests`, lifecycle) |
| `PAGE-5` | MUST | 2 | 2.2, 2.5 | ✅ | one strategy instance across two concurrent walks / `Parallel.For` parse |
| `PAGE-6` | MUST | 2 | 2.3, 2.5, 2.6 | ✅ (pin + build) | zero-exchange probes (async, blocking, fetchers); `SingleUseEnumerableTests` laziness |
| `PAGE-7` | MUST | 2 | 2.5, 2.6 | ✅ | three `MoveNext` after the end; empty page with a next costs one exchange |
| `PAGE-8` | MUST | 2, 3 | 2.5, 3.1 | ✅ | `ReEnumeration_EachEnumerationRestartsFromFirst`; fresh `PagingOptions` per walk |
| `PAGE-9` | MUST | 2, 3 | 2.5, 2.6, 3.1 | ✅ | `maxPages` stops with a non-null next; `0`/`-1` throw at construction (all three factories) |
| `PAGE-10` | SHOULD | 2 | 2.5, 3.4 | ✅ | 1,000-page uncapped walk; user page cap advice |
| `PAGE-11` | MUST | 2 | 2.4, 2.7 | ✅ | `EarlyBreak_FirstBodyDisposed_AndTransportCalledOnce`; page-1 body disposed before its first item |
| `PAGE-12` | MUST | 2 | 2.7, 3.4 | ✅ (by construction, entry 17) | abandoned hand-driven enumerator: disposed count equals fetched count |
| `PAGE-13` | MUST | 2 | 2.4, 2.7 | ✅ | parse error primary, dispose failure on `ExceptionTrail` (async and blocking) |
| `PAGE-14` | MUST | 2, 3 | 2.3, 2.5, 2.6, 3.1, 3.3 | ✅ | second `GetAsyncEnumerator`/`GetEnumerator` on one `AsPages()` view throws; race test; AOT smoke |
| `PAGE-15` | MUST | 2 | 2.4, 2.7 | ✅ | success-path dispose failure propagates unwrapped |
| `PAGE-16` | MUST | 2 | 2.2 | ✅ | `Cursor` default name, null/empty ends, splice on the template |
| `PAGE-17` | MUST | 2 | 2.2 | ✅ | `PageNumber` rows: executed request, garbage → `startPage`, 0-based, `int.MaxValue`, invariant digits, culture |
| `PAGE-18` | MUST | 1, 2, 3 | 1.3, 2.2, 3.2, 3.3 | ✅ | `LinkHeaderParserTests` over `link-header.json`; `PaginationWireTests`; AOT smoke |
| `PAGE-19` | MUST | 1, 2 | 1.4, 2.2, 2.8, 3.2 | ✅ (cross-origin reading, §11 item) | `LinkTargetTests`; `PaginationLinkOriginTests` (Security); wire cross-origin row |
| `PAGE-20` | SHOULD | 1, 2, 3 | 1.3, 2.2, 3.2 | ✅ | joined split instances (vector + strategy + wire) |
| `PAGE-21` | MUST | 1 | 1.1, 1.2 | ✅ (residual: §10 entry 18) | `QuerySpliceTests` (case-sensitive regression, untargeted verbatim); property tests; the residual pin |
| `PAGE-22` | MUST | 1 | 1.1, 1.2 | ✅ | encode/decode rows, lone surrogate naming the parameter only; write-then-read property |
| `PAGE-23` | MUST | 1, 2 | 1.1, 2.2 | ✅ | replace-in-place, drop duplicates, append, remove; template method/headers/body kept |
| `PAGE-24` | MUST | 1 | 1.1 | ✅ (entry 18's `PAGE-24` clause retired) | userinfo/port/path/fragment survive; no `:443` |
| `PAGE-25` | MUST | 2 | 2.5 | ✅ | cancel mid-walk observed by the in-flight transport call |
| `PAGE-26` | MUST | 2 | 2.5 | ✅ | cancel between items keeps the fetched page's items |
| `PAGE-27` | MUST | 2 | 2.4, 2.7 | ✅ | dispose-count matrix per path |
| `PAGE-28` | MUST | 2 | 2.4, 2.7 | ✅ | cause matrix; null response → `InvalidOperationException` naming the client |
| `PAGE-29` | MUST | — | 3.5 | 🚫 (§10 entry 19) | checklist; serial/ordered delivery covered by the flatten tests |
| `PAGE-30` | MUST | — | 3.5 | 🚫 (§10 entry 19) | checklist |
| `PAGE-31` | SHOULD | 2 | 2.5, 2.6 | ✅ | 10,000 synchronous pages, async and blocking |
| `PAGE-32` | MUST | 2 | 2.4, 2.7 | ✅ | success-path dispose failure ends the walk, no hang, no further exchange |
| `PAGE-33` | MUST | 2 | 2.4, 2.5, 2.7, 3.4 | ✅ | late response discarded and disposed once; documented race |
| `PAGE-34` | MUST | 3 | 3.1 | ✅ (ownership per P7c-15) | `FetcherPageableTests` (key precedence, null ends, first once, fetcher-owned dispose) |
| `PAGE-35` | SHOULD | 3 | 3.1 | ✅ | one `PagingOptions` per walk, shared by every call, fresh per walk |
| `PAGE-36` | MUST | 2, 3 | 2.4, 2.5, 2.6, 3.2 | ✅ | the same `RequestOptions` instance on every `RecordedCall`; wire timeout row |

Count: **36 rows**, all mapped (32 MUST, 4 SHOULD). By exit: 33 ✅ (`PAGE-1`, `PAGE-2`, `PAGE-4`–`PAGE-28`, `PAGE-31`–`PAGE-36`), 3 🚫 (`PAGE-3`,
`PAGE-29`, `PAGE-30`), 0 ⏳, 0 N/A. By level: 29 MUSTs ✅ and 3 MUSTs 🚫 (`PAGE-3`, `PAGE-29`, `PAGE-30`); the four SHOULDs ✅.

### Work on other owners' rows (no checklist row in 7c)

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `PIPE-26` (4c) | A pipeline backing a paginator | 2.5 | `PageableTests.A_pipeline_backing_a_paginator_works_as_the_client`; `PipelineAsTransportTests` pointer |
| `SERDE-13` (7a) | A null envelope is a typed failure, not a `NullReferenceException` | 2.4 | `PageStepTests` null-envelope row (`DeserializationException` naming `TPage`) |
| `PIPE-28` (4c, inherited ⏳ against 8b) | The blocking pager is honest above the transport; `SystemNetHttpClient.Execute` is sync-over-async until 8b | 2.6 | `BlockingPageableTests` over `SyncFirstTransport`; checklist line |

---

## Traceability: group → rows → tasks

| Group | Tasks | Rows (first owner) |
|---|---|---|
| 1 — splice, `Link` parser, `Link` target | 1.1–1.5 | `PAGE-18`, `PAGE-19`, `PAGE-20`, `PAGE-21`, `PAGE-22`, `PAGE-23`, `PAGE-24` |
| 2 — contract, engine, bases, factories, blocking | 2.1–2.9 | `PAGE-1`, `PAGE-2`, `PAGE-3`, `PAGE-4`–`PAGE-17`, `PAGE-25`–`PAGE-28`, `PAGE-31`–`PAGE-33`, `PAGE-36` |
| 3 — fetchers, wire, AOT, docs, corrections | 3.1–3.7 | `PAGE-34`, `PAGE-35`, `PAGE-29`, `PAGE-30` (checklist), documentation clauses of `PAGE-10`, `PAGE-33` |

Task count: 0.1 (pre-flight) + 5 (group 1) + 9 (group 2) + 7 (group 3) = **22 tasks**.

## Findings while planning

1. **`PaginationStrategies` old and new overloads coexist between 2.2 and 2.5, except `LinkHeader`.** `Cursor`, `PageNumber` and `Create` differ in generic arity and
   build clean. The two `LinkHeader` overloads both have optional parameters, so PublicApiAnalyzers **RS0026** ("Do not add multiple overloads with optional
   parameters", verified on SDK 10.0.401 with PublicApiAnalyzers 5.6.0) fails the build even across generic arities; the new one is therefore added as
   `LinkHeaderNext` in 2.2 and renamed in 2.5. If the compiler reports a CS0121 ambiguity for another pair on the host, rename that pair the same way — flag, do not guess.
2. **`ScriptedSerde<T>.Deserialize` (sync) is a no-op** (R2), so any blocking-engine test built on it would silently return `null` envelopes and fail with
   `DeserializationException`; the plan adds a test-project codec instead of changing a shared double.
3. **`FetchedPage<T>` as a positional record with a validated `Page`** needs an explicit backing field in the `init` accessor (`field` keyword avoided so the
   file compiles on any C# the gates accept); the analyzer's PublicAPI text for the record is taken from the code fix, not hand-written.
4. **Holding a loopback response** is done by a blocking responder (`LoopbackServer.Start(Func<…>)` exists); task 3.2 row 2 is kept and only dropped, with a recorded spec deviation, if the check fails on the host.
5. **Fatal-exception pin (task 2.4)** is awkward to exercise (`OutOfMemoryException` cannot be thrown safely in a test); the plan allows recording the gap
   rather than faking it.
6. **Three of the design's four "open for the lead" rulings (P7c-3, P7c-6, P7c-16) remove or add public surface**; the plan implements them as taken so the
   branch is reviewable, and task 3.6's roadmap note records them as taken-by-default. P7c-12 (the cross-origin guard) is implemented as specified; if the
   lead rejects it, reverting means deleting the origin comparison in `LinkTarget`, the `allowCrossOrigin` parameter and `PaginationLinkOriginTests` — which is a
   **Security** class, and constraint 5 forbids loosening one, so the lead decides that before group 2 merges, not after.

## Findings while implementing

Added at close-out (2026-10-09); the [checklist](2026-10-09-phase7c-pagination-checklist.md) holds the full deviation list. Three readings above proved wrong against the tree:

1. **`ResponseBody.ReadAsBytes` closes the body** (BODY-16), so task 2.4's blocking read through it would surface a release failure from the read, before the strategy ran, and could never attach it to a parse failure's
   trail. The step reads the stream through `BodyMaterializer.ReadAll` under the same 64 MiB cap instead (checklist deviation 4).
2. **`SystemNetHttpClient` ignores `RequestOptions.Timeout` until 8b**, so task 3.2's row 2 cannot fire a `ServiceRequestTimeoutException`; it became an options-instance row (checklist deviation 5).
3. **In Microsoft.Testing.Platform mode `dotnet test` forwards MSBuild flags to the test application**; the resource-limited form is `dotnet build … -m:3 -nr:false --disable-build-servers` then `dotnet test --no-build`
   (checklist deviation 2). CA1716 also forced the `first` parameter name (deviation 3).

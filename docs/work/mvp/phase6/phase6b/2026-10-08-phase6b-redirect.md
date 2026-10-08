# Phase 6b — Redirect: Implementation Plan

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Design: [phase 6b redirect design](2026-10-08-phase6b-redirect-design.md), the
authority for every decision below. The plan cites its census rows, positions (A–K), facts (1–17) and rulings
(`P6b-1`…`P6b-30`) rather than restating them. Scope authority: the roadmap's Phase 6 card and Phase List row 6
(`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the phase 5 plans
(`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration.md` and its 5b and 5c siblings), read first. 6a (retry) and 6c
(auth) are planned concurrently by other authors in this working tree; this plan touches only 6b's own files (the design and
this plan) and lists the shared files it will later edit.

**What this document is.** The roadmap's step 3 for sub-phase 6b: numbered TDD tasks in the design's three-PR segmentation,
each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** markings, `CHANGELOG.md` entry,
requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 3.5) and it
writes no production code. The [checklist section](#checklist-one-row-per-owned-id) below is the plan's own row table:
exactly one row per ID 6b owns.

**Scope.** 28 rows: `REDIR-1`–`REDIR-28` (23 MUST, 4 SHOULD, 1 MAY). Planned exits: 26 ✅, 1 🚫 (`REDIR-25`), 1 ⏳ (`REDIR-27`),
0 N/A. `BODY-4`, `BODY-5`, `PIPE-15`, `PIPE-16`, `PIPE-40`, `XCUT-17`, `HTTP-46`, `AUTH-29`, `XCUT-16`, `OBS-20`, `OBS-39` and
`TRANSPORT-1` are other owners' rows on which 6b works; they appear in
[the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-6b) and carry no exit mark in 6b's checklist.

**Order and gates.** PR 1 has no gate. **PR 2 follows PR 1** (`RedirectPolicy` must exist in its new form to call the
emitters). **PR 3 follows PRs 1 and 2** and lands as late as practical against 6a and 6c (P6b-28). The
`RedirectCredentialHygieneTests` and `RedirectWireTests` `Security` classes run **first** after PR 1's tasks 1.3 and 1.9. The
segmentation is the design's; the plan does not change it.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`; the rewrite **removes** `RedirectPolicy`'s waiver and adds none); `///` XML docs on every public member
   (CS1591); no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2; `FrozenSet`/`ToFrozenSet` are in
   `System.Collections.Frozen`, in the shared framework; `ILogger`/`LoggerMessage` come from the permitted
   `Microsoft.Extensions.Logging.Abstractions`). No reflection in `src/`, so `IsAotCompatible` and `IsTrimmable` hold
   unannotated. `Uri.ToString()` is lossy and banned on log paths: every URL reaching a log, a message or a span goes through
   `UrlRedactor`.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); the AOT smoke check is a plain method on `SmokeChecks` and carries no trait; `Security` for exactly the two leak classes added in PR 3 (P6b-25). Core tests live under
   `tests/Dexpace.Sdk.Core.Tests/<area>/` (namespace `Dexpace.Sdk.Core.Tests.<Area>`); doubles under
   `tests/Dexpace.Sdk.TestSupport/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2); it reaches
   internals through `InternalsVisibleTo`.
5. **Security tests are never deleted or loosened.** Unlike 5a, 6b **does** edit one `Security` class
   (`RedirectCredentialHygieneTests`, P6b-24). The V-gate's diff check is therefore the exact allowed diff: only
   `RedirectCredentialHygieneTests.cs` may appear under `tests/Dexpace.Sdk.Core.Tests/Security` other than the two **added**
   files of PR 3, and nothing under `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` other than the one added file of PR 3.
   Task 1.3 states the three hunks; a reviewer compares the diff against them.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`
   and a removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority
   (apply the analyzer's code fix and compare against the lines this plan and the design's "The public surface" list). Only
   core's file changes; the SystemNet and STJ files must not (the V-gate checks). A hand-written `Equals(RedirectOptions?)`
   on a sealed record keeps the same `PublicAPI` lines as the synthesised one (fact 12).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes,
   stating what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes",
   items 1–9). 6a and 6c edit the same changelog; whichever PR lands second re-derives its hunks on the merged file.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1): write
   `System.IO.…` fully qualified in core files if needed. The `Pipeline/Policies/Redirect/` folder keeps the namespace
   `Dexpace.Sdk.Core.Pipeline.Policies` (design, "Internal types"); task 1.5 flattens the folder if `IDE0130` is on.
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `feat:` for an additive one, `test:` for
   tests only, `docs:` for documentation only, `chore:` for refactors. **No AI attribution** of any kind in a commit message,
   changelog line or document (global hard rule). The plan authorises no `git push`, no `gh` command and no remote action.
   Branches follow `<issue>-phase-6b-<slug>` off `main` (`70-phase-6b-pr1-redirect`, …).
10. **Siblings own shared files too.** `PublicAPI.Unshipped.txt`, `CHANGELOG.md`, `DexpaceLogEvents.cs` (6a adds 140–149, 6c
    160–169), `DexpaceLogKeys.cs` and the roadmap are shared with 6a and 6c: each PR re-derives its hunks from the merged
    file and never resolves a conflict by keeping either side whole. `RedirectOptions.cs` and `RetryOptions.cs` are separate
    files and do not collide.
11. **Test timing.** A test that must wait uses `InstantTimeProvider` (TestSupport) or `FakeTimeProvider`, or a
    `TaskCompletionSource`; none sleeps on the wall clock. No 6b test reads the process environment.
12. **The decider is pure.** `RedirectDecider.Decide` does no I/O and reads no clock; matrix rows therefore need no pipeline.
    Only the policy suite (task 1.9) and the leak tests (PR 3) build pipelines.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build
is the lint gate. The test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's
`--filter`). **The design was written on a host with no .NET SDK, and so was this plan**; every command below is untested
here and runs for the first time on the implementer's host. Facts 7–11 are therefore verified in task 0.1 before any code is
written.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-sec** (the two redirect `Security` classes; first after tasks 1.3 and 1.9, and after PR 3's tests exist):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*RedirectCredentialHygieneTests"
dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-class "*RedirectWireTests"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect only the files convention 5 allows
git diff --stat -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt src/Dexpace.Sdk.Serialization.SystemTextJson/PublicAPI.Unshipped.txt   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/ci/reproducible-pack.sh
dotnet build   tools/Dexpace.Tools.sln --configuration Release && dotnet test --solution tools/Dexpace.Tools.sln --configuration Release
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The coverage gate (`dotnet test … --coverlet --coverlet-output-format cobertura --results-directory artifacts/test-results`,
then `dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80` and `scripts/ci/coverage-gate-selftest.sh
artifacts/test-results`) runs before the push of every PR. No `PackageReference` changes, so no `packages.lock.json` change is
expected; if a locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info REDIR
scripts/knowledge --gaps REDIR              # 0 of 28: no REDIR ID is a gap ID (design, "Governing documents")
scripts/knowledge --req REDIR-20            # per row in scope of the PR (also BODY-4, PIPE-40, XCUT-17 for cited work)
```

Task 0.1 records any difference from the design's corpus-file reading. A difference that contradicts a ruling reopens that
ruling.

---

## Plan-level readings (where the design was silent, ambiguous or wrong about the tree)

The design's decisions are not changed. **R1–R4 are corrections of fact; R5–R9 are additions.** Additions that add behaviour
are flagged for the lead.

- **R1 — `Disposal` has an async twin.** The design names `Disposal.DisposeQuietly(resource, primary, logger)`. The tree
  also has `Disposal.DisposeQuietlyAsync(IAsyncDisposable?, Exception? primary = null, ILogger? logger = null)`, which the
  as-built policy already uses on its async path. The rewritten loop calls the async form when `async` is true and the sync
  form otherwise, for all three disposal sites (superseded response, failing decision, throwing predicate).
- **R2 — Call sites that name `StripSensitiveHeadersOnCrossOrigin`.** Removing the property breaks compilation of
  `tests/Dexpace.Sdk.Core.Tests/Security/RedirectCredentialHygieneTests.cs` (line ~172),
  `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/RedirectPolicyTests.cs` (the `MakeOptions` parameter and its two named-argument
  call sites at lines ~242 and ~273) and `tests/Dexpace.Sdk.Core.Tests/Configuration/DexpaceClientOptionsTests.cs` (the assertion
  at ~line 66 and the member-name list at ~142). All three are edited in task 1.3, in
  the same commit as the removal, so no commit is red. Task 0.1 step 3 re-greps for others
  (`grep -rni StripSensitiveHeadersOnCrossOrigin src tests docs README.md`).
- **R3 — `RedirectPolicyTests` is rewritten in task 1.9, but its `MakeOptions` helper must compile at 1.3.** Task 1.3 drops the
  parameter only; task 1.9 deletes the class body per the design's list.
- **R4 — `ReDriveLifecycleTests.Options(maxRedirects: 20)`** passes an explicit value, so the new default of 3 changes none of
  its outcomes. The design's "the default becomes the record's own" is done by changing the helper's parameter to
  `int? maxRedirects = null` and building `new RedirectOptions()` when null (task 1.9).
- **R5 — A recursion-free `RedirectChain.Snapshot`.** The snapshot copies the visited list with `[.. _visited]` (a collection
  expression over `List<Uri>`), not `AsReadOnly()`, so a predicate that casts `VisitedUris` to `List<Uri>` or an array and
  writes cannot reach the chain (design position E; test in task 1.9). The chain stores keys (`string`) in a `HashSet<string>`
  and the matching `Uri` objects in a `List<Uri>`, both appended by `Advance`.
- **R6 — `RedirectDecision` is a `readonly struct` with static factories** (`Follow`, `ReturnCurrent`, `Fail`) and public
  members only inside the assembly (`internal`). Its `Follow` payload is `(Request Next, Uri Target, bool CrossOrigin, bool
  Downgraded)`. A `Fail` carries the exception plus a `RedirectFailureKind` (internal enum, explicit values: `None = 0`,
  `SchemeDowngrade = 1`, `BodyNotReplayable = 2`) so the policy can emit `scheme_downgrade_rejected` without a type test.
  Not in the design; internal only (flagged).
- **R7 — The `RedirectStopReason` enum** has explicit values (styleguide 6.8): `NotARedirect = 0`, `NotEligible = 1`,
  `MalformedLocation = 2`, `LoopDetected = 3`, `HopCap = 4`.
- **R8 — Events are emitted by the policy, not by the decider.** The decider stays pure, so the five emissions of position J
  (PR 2) are called from the loop: the decision value carries what the event needs (the raw malformed value on
  `ReturnCurrent(MalformedLocation)` via `RedirectDecision.MalformedRaw`, `string?`; the target on the others). Internal.
- **R9 — The matrix is keyed by name, as 5a's `ProxyResolutionCase` is.** `RedirectCase` is an `internal sealed record`
  in the test project (`RedirectCases.cs`) that carries its options and, for the predicate rows, its lambda, built in code. A
  public test method cannot take an internal type (CS0051) and a public `TheoryData<RedirectCase>` member cannot exist
  (CS0050/CS0053), so the theory is keyed by the string display name:
  `public static TheoryData<string> Cases()` returns the `REDIR-n: description` names from `RedirectCases.Names()`, and
  `public void Decide_matches_the_table(string name)` looks the row up with `RedirectCases.Named(name)` (the
  `ProxyResolutionCase.Names`/`Named` pattern, `tests/Dexpace.Sdk.Core.Tests/Configuration/ProxyResolutionCase.cs`). Rows may
  therefore hold a `RedirectOptions` and lambdas. Any other theory over an internal type (`RedirectStopReason`, task 1.9) passes
  an `int` or a `string` and casts or parses inside the method. Task 0.1 step 4 confirms the pattern on the pinned `xunit.v3`.

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no PR; no commit)

**Why first.** The design's facts 7–11 were never run (no SDK on the design host). Tasks 1.1, 1.2, 1.3 and 3.3 depend on the
answers.

**Do.**

1. Run the five phase-start queries above and compare with the design's table ("The knowledge queries could not be run on this
   host"). Record every difference in a scratch note (`$SCRATCH/phase6b-preflight.md`, in the session scratchpad); a
   difference that contradicts a ruling goes to the lead before PR 1 starts.
2. Create a throwaway console project **outside the repository** (scratchpad directory, `net10.0`, no package references) and
   print, for each fact:
   - **7** — `new Uri("HTTPS://EXAMPLE.COM:443/a").AbsoluteUri` against `new Uri("https://example.com/a").AbsoluteUri` (expect
     identical, `https://example.com/a`).
   - **8** — `Uri.TryCreate(new Uri("https://a.example/x"), v, out var u)` for `v` in `"http:///p"`, `"https://"`, `"http:foo"`,
     `"//"`, `"http://"`: print the result, `u?.IdnHost`, `u?.Scheme`. Every dispatchable-looking success must have a non-empty
     `IdnHost`, or task 1.2's empty-host screen must also reject the case printed.
   - **9** — `v` = `"/a b"`, `"/é"`, `"/a\tb"`: print `TryCreate`'s result and `AbsoluteUri`. Expect accepted and
     percent-encoded, with no exception.
   - **10** — `new Uri("https://bücher.example/").IdnHost` against `new Uri("https://xn--bcher-kva.example/").IdnHost` (expect
     equal) and `.Host` for both (may differ).
   - **11** — `FrozenSet<Method>` over `Method.Get`, `Method.Head` with the default comparer: `Contains(Method.Of("get"))`
     (print; the design's rule is that `Method` has value equality, so the answer decides whether `AllowedMethods` is
     case-sensitive; record it) and a NativeAOT publish (`PublishAot=true`) of the same snippet, run.
   - **Extra (plan-level)** — `new Uri("https://h/y").GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo,
     UriFormat.UriEscaped)` for `https://u:p@h:443/y%2Fz?a=%26#f%41` (expect userinfo gone, `%2F` and `%26` kept, `:443`
     elided, `%41` decoded to `A` only if fact 1's note holds); `Uri.TryCreate(new Uri("https://a/x"), "//other.example/p",
     out var u)` (expect `https://other.example/p`, not `file:`, fact 6); `Uri.TryCreate(new Uri("https://a/x/y"),
     "../z?q=1#f", …)`.
3. Run `grep -rni "StripSensitiveHeadersOnCrossOrigin" src tests docs README.md` (case-insensitive: the named argument `stripSensitiveHeadersOnCrossOrigin:` is lower camel) and record every hit (R2). Run
   `grep -rln "new RedirectOptions\|MaxRedirects" src tests` and record the files whose behaviour the new default of 3 could
   change (design fact 17 found none beyond `ReDriveLifecycleTests`).
4. Confirm the name-keyed `TheoryData<string>` pattern (R9) by reading `tests/Dexpace.Sdk.Core.Tests/Configuration/ProxyResolutionCase.cs`
   and `ProxyUrlParserTests` (an internal case record, a public `TheoryData<string>` of names, a lookup by `Named(name)`).
5. Read, and record the line numbers of, the files this plan edits: `RedirectPolicy.cs`, `RedirectOptions.cs`,
   `RedirectCredentialHygieneTests.cs`, `RedirectPolicyTests.cs`, `ReDriveLifecycleTests.cs`, `DexpaceClientOptionsTests.cs`,
   `LogCatalogueTests.cs`, `LogVocabularyTests.cs`, `DexpaceLogEvents.cs`, `DexpaceLogKeys.cs`, `SmokeChecks.cs`.

**Outcomes feed.** A fact that comes out the other way reopens the ruling that cites it (design "Facts the design rests on"):
fact 8 → P6b-10 (add the missing screen to task 1.2), fact 10 → P6b-12 (fall back to `Host` lower-cased and record), fact 11
→ P6b-3 (document case sensitivity; add a case-insensitivity row to the matrix if `Method` folds case).
**Exit:** the scratch note exists; nothing is committed.

---

## PR 1 — The decision function, the options, the exceptions and the rewritten policy

**Gate: none.** Rows: `REDIR-1`–`REDIR-24`, `REDIR-26`, with `REDIR-25` argued (🚫) and `REDIR-27` deferred in the checklist.
Files: `src/Dexpace.Sdk.Core/Http/Common/HttpOrigin.cs`, `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/*.cs`,
`src/Dexpace.Sdk.Core/Pipeline/Policies/{RedirectPolicy,RedirectCondition}.cs`, `src/Dexpace.Sdk.Core/Errors/RedirectExceptions.cs`,
`src/Dexpace.Sdk.Core/Configuration/RedirectOptions.cs`, `PublicAPI.Unshipped.txt`, and the tests named below.

### Task 1.1 — `HttpOrigin` (`REDIR-8`, `REDIR-11`; P6b-12, facts 5 and 10)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Http/Common/HttpOriginTests.cs`, class `HttpOriginTests` (Unit):

- `From_lowercases_the_scheme_and_host` (`HTTPS://EXAMPLE.COM/a` → `("https", "example.com", 443)`)
- `An_explicit_default_port_equals_an_absent_one` (`https://h:443/` equals `https://h/`; `http://h:80/` equals `http://h/`, fact 5)
- `A_different_port_is_a_different_origin` (`https://h:8443/` ≠ `https://h/`)
- `A_different_scheme_is_a_different_origin` (`http://h/` ≠ `https://h/`)
- `An_IDN_host_and_its_punycode_spelling_are_one_origin` (`https://bücher.example/` equals `https://xn--bcher-kva.example/`, fact 10)
- `Userinfo_path_query_and_fragment_are_ignored` (`https://u:p@h/a?b#c` equals `https://h/`)
- `An_IPv6_literal_keeps_its_brackets_out_of_the_host_comparison` (`https://[::1]:8443/` equals `https://[0:0:0:0:0:0:0:1]:8443/`)
- `From_rejects_a_relative_uri` (`ArgumentException`)

Red: compile error (the type does not exist).

**Production.** `src/Dexpace.Sdk.Core/Http/Common/HttpOrigin.cs`:

```csharp
internal readonly record struct HttpOrigin(string Scheme, string Host, int Port)
{
    internal static HttpOrigin From(Uri uri)   // ArgumentException when !uri.IsAbsoluteUri
        => new(uri.Scheme.ToLowerInvariant(), uri.IdnHost.ToLowerInvariant(), uri.Port);
}
```

Documented as the RFC 6454 triple, internal, offered to 6c (P6b-12). `IdnHost` is used rather than `Host` (fact 10); if task 0.1
disproved fact 10, the fallback is `Host` and the IDN test is replaced by a recorded `Skip`-free note.

**IDs:** `REDIR-8`, `REDIR-11` (the seed-origin comparison). **Verify:** V-fast `HttpOriginTests`.

### Task 1.2 — The total `Location` resolver (`REDIR-12`, `REDIR-13`, `REDIR-14`, `REDIR-18`, `REDIR-19`; P6b-10, P6b-30)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/Redirect/RedirectLocationTests.cs` (Unit). All cases call
`RedirectLocation.TryResolve(Uri current, Headers headers, out Uri? target, out string? malformedRaw)` which returns a
`LocationOutcome` enum (`Resolved`, `Absent`, `Malformed`; explicit values; internal):

- `A_missing_header_is_absent` / `An_empty_header_is_absent` / `An_all_whitespace_header_is_absent` (`REDIR-19`; `malformedRaw` null)
- `A_relative_location_resolves_against_the_current_hop_not_the_seed` (`REDIR-14`; current `https://h/a/b`, `c` → `https://h/a/c`;
  `/x?y=1` → `https://h/x?y=1`; `../z` → `https://h/z`)
- `A_protocol_relative_location_keeps_the_current_scheme` (`//other.example/p`, fact 6)
- `An_absolute_location_is_used_as_is` (`REDIR-14`)
- `Userinfo_is_dropped` (`REDIR-12`; `https://u:p@h/y` → `https://h/y`; also for a relative-looking value that carries none)
- `Reserved_escapes_survive_resolution_and_stripping` (`REDIR-13`; `%2F`, `%26`, a fragment escape, a bracketed IPv6 literal,
  a non-default port `:8443`)
- `An_explicit_default_port_is_elided` (P6b-30; `https://h:443/y` → `AbsoluteUri` `https://h/y`; asserts the residue, not a bug)
- `A_non_http_scheme_is_malformed` (`ftp://x/y`, `mailto:a@b`, `javascript:1`, `file:///etc/passwd`, `data:,x`; fact 2; mixed
  case `FTP://x`)
- `An_empty_host_is_malformed` (`http:///p`, `https://`, `http:foo`; fact 8, extended by whatever task 0.1 printed)
- `Two_location_values_are_malformed` (P6b-10; `malformedRaw` is non-null)
- `A_value_with_spaces_or_non_ASCII_is_followed_percent_encoded` (fact 9)
- `A_fragment_only_or_fragment_bearing_location_resolves` (Ruby round 2; `#frag` → same URL plus fragment)
- `The_resolver_never_throws_for_hostile_values` (a dozen: `"\0"`, 70 000 characters, `"http://[::1"`, `"http://a b/"`,
  `"//"`, `"\\\\host\\share"`, `"http://a:99999/"`, `"%"`, `"http://%41/"`, a lone high surrogate, `"   x   "`)

Red: compile error.

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectLocation.cs`, `internal static class`:

```csharp
internal static LocationOutcome TryResolve(Uri current, Headers headers, out Uri? target, out string? malformedRaw)
```

- `values = headers.GetAll(HttpHeaderName.WellKnown.Location)`. Zero values or a single value that is whitespace → `Absent`.
  More than one value → `Malformed` with `malformedRaw = string.Join(", ", values)`.
- One value: `raw = values[0].Trim()`; `Uri.TryCreate(current, raw, out var created)`; failure → `Malformed`.
- Screen: scheme is `http` or `https` (`OrdinalIgnoreCase`) and `created.IdnHost.Length > 0`; else `Malformed`.
- `target = StripUserInfo(created)` via `GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped)`
  passed to `new Uri(text, UriKind.Absolute)` guarded by `TryCreate` (total).
- Every method under 70 lines; the private helpers are `StripUserInfo` and `Screen`. The code never calls `new Request`.

**IDs:** `REDIR-12`, `REDIR-13`, `REDIR-14`, `REDIR-18`, `REDIR-19`. **Verify:** V-fast `RedirectLocationTests`.

### Task 1.3 — `RedirectOptions` reshaped; the three call sites of the removed property (`REDIR-3`, `REDIR-4`, `REDIR-5`, `REDIR-15`, `REDIR-17`, `REDIR-20`, `REDIR-26`; P6b-3, P6b-24, R2) — Breaking 1, 2

**Failing tests.**

New `tests/Dexpace.Sdk.Core.Tests/Configuration/RedirectOptionsTests.cs` (Unit):

- `Defaults_are_three_hops_no_downgrade_no_303_and_GET_HEAD` (`REDIR-17`; `MaxRedirects == 3`; `AllowedMethods` equals `{GET, HEAD}`;
  `FollowSeeOther` false; `Predicate` null; `AllowHttpsToHttpDowngrade` false)
- `A_negative_MaxRedirects_throws_at_init` (`ArgumentOutOfRangeException`, `ParamName` `value`); `Zero_is_accepted`
- `AllowedMethods_is_copied_at_init` (`REDIR-26`; build with a `HashSet<Method> { Post }`, add `Put` afterwards, assert the
  option's set still has only `Post`; the getter's object is not the caller's set)
- `A_null_set_or_a_null_element_is_rejected` (`ArgumentNullException` / `ArgumentException`)
- `An_empty_set_is_accepted`
- `The_default_set_is_one_shared_frozen_instance` (two `new RedirectOptions()` return the same `AllowedMethods` reference)
- `Equality_compares_the_set_by_content_and_the_predicate_by_delegate` (two options built with differently-ordered equal sets are
  equal with equal hash codes; different sets are unequal; the same delegate equal, two distinct lambdas unequal)
- `ToString_lists_every_member_by_name`
- `RedirectOptions_has_no_LocationHeader_member` (a reflection fact: no public member named `LocationHeader`; the `REDIR-27` evidence, P6b-21)
- `StripSensitiveHeadersOnCrossOrigin_no_longer_exists` (a reflection pin: the property is absent; mirrors the design's
  "options-shape test" and is the reason the property's absence needs no other test)

Edited in the same task (R2; the **only** edits to a `Security` class in 6b, P6b-24):

- `tests/Dexpace.Sdk.Core.Tests/Security/RedirectCredentialHygieneTests.cs`:
  1. `Authorization_is_stripped_on_every_hop_even_same_origin`: the `303` row runs with `FollowSeeOther = true` in its options.
  2. `Stripping_is_on_by_default_regardless_of_the_legacy_switch` is **replaced** by `Stripping_holds_under_every_redirect_option`,
     which builds options with every permissive value (`AllowHttpsToHttpDowngrade = true`, `FollowSeeOther = true`,
     `AllowedMethods` = every well-known `Method`, `MaxRedirects = 10`, a predicate that always returns `true`) and asserts the
     same three headers (`Authorization`, `Cookie`, `Proxy-Authorization`) absent on the cross-origin request exactly as the
     replaced test did.
  3. The class header comment drops "the rest of the matrix … is phase 6b's" and names `RedirectDecisionMatrixTests` and
     `RedirectCredentialLeakTests`.
- `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/RedirectPolicyTests.cs`: the `stripSensitiveHeadersOnCrossOrigin` parameter and
  assignment are deleted from `MakeOptions`, **and** the named argument `stripSensitiveHeadersOnCrossOrigin: true` is removed
  at both call sites (lines ~242 in `ProcessAsync_CrossOriginRedirect_StripsAuthorizationAndCookieHeaders` and ~273 in
  `ProcessAsync_SameOriginRedirect_StripsAuthorizationHeader`; deleting the parameter alone is CS1739). The class is replaced
  wholesale in task 1.9, which gives those two facts a disposition.
- `tests/Dexpace.Sdk.Core.Tests/Configuration/DexpaceClientOptionsTests.cs`: line ~64 `Assert.Equal(20, …)` becomes `3`; line ~66
  `Assert.True(redirect.StripSensitiveHeadersOnCrossOrigin)` is deleted; the member
  list at ~142 becomes `MaxRedirects`, `AllowHttpsToHttpDowngrade`, `FollowSeeOther`, `AllowedMethods`, `Predicate`.

Red: compile errors (`FollowSeeOther`, `AllowedMethods`, `Predicate` do not exist).

**Production.** `src/Dexpace.Sdk.Core/Configuration/RedirectOptions.cs` (design position A):

- `MaxRedirects { get; init => field = ...; } = 3` with `ArgumentOutOfRangeException.ThrowIfNegative(value)`.
- `AllowHttpsToHttpDowngrade { get; init; }` unchanged.
- `FollowSeeOther { get; init; }`.
- `AllowedMethods { get; init => field = Freeze(value); } = s_defaultMethods` where `s_defaultMethods` is a private static
  `FrozenSet<Method>` of `Get`, `Head`, and `Freeze` throws on a null set or a null element then returns `value.ToFrozenSet()`.
  The property type is `IReadOnlySet<Method>`.
- `Func<RedirectCondition, bool>? Predicate { get; init; }`.
- `StripSensitiveHeadersOnCrossOrigin` removed; the "Roadmap phase 6b removes the property" remark goes.
- `public bool Equals(RedirectOptions? other)` and `GetHashCode()` hand-written, following `ProxyOptions.cs`: `MaxRedirects`,
  `AllowHttpsToHttpDowngrade`, `FollowSeeOther` and `Predicate` (delegate equality) compared directly; `AllowedMethods` compared
  with `SetEquals`; the hash combines the scalars, the count and the XOR of the elements' hashes (order independent).
  `PrintMembers` renders every member (the set as its sorted tokens).
- XML docs: **Breaking** on `MaxRedirects` (was 20; negative now throws), on the class (`StripSensitiveHeadersOnCrossOrigin`
  removed; it had no effect since phase 1), and a remark on `AllowedMethods` that eligibility is judged on the original method
  (P6b-4). `RedirectCondition` is referenced by a `cref`, so this task adds the type's skeleton (properties only, internal
  constructor, full docs) in `Pipeline/Policies/RedirectCondition.cs`; its behaviour is tested in task 1.5.

**PublicAPI.** Delete the two `StripSensitiveHeadersOnCrossOrigin` lines; add the `AllowedMethods`, `FollowSeeOther` and
`Predicate` lines and the five `RedirectCondition` lines (the type plus its four properties) listed in the design's "The public surface".

**IDs:** `REDIR-17`, `REDIR-26` (and the option halves of `REDIR-3`, `REDIR-5`, `REDIR-15`, `REDIR-20`).
**Verify:** V-fast `RedirectOptionsTests`, `DexpaceClientOptionsTests`, `RedirectCredentialHygieneTests`; V-sec. The
hygiene class still passes against the as-built policy (the policy follows everything); it is a pin until task 1.9.

### Task 1.4 — The `RedirectException` family (`REDIR-6`, `REDIR-15`; P6b-16)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Errors/RedirectExceptionTests.cs` (Unit):

- `The_family_derives_from_SdkException_and_not_from_ServiceRequestException`
- `Both_leaves_are_sealed_and_derive_from_RedirectException`
- `Each_type_has_the_three_standard_constructors` (message, inner exception round trip)
- `The_downgrade_message_names_both_urls_redacted_and_the_opt_in` (built through the factory the decider uses, task 1.8; here
  through the internal `RedirectMessages.Downgrade(int status, Uri from, Uri to)`: a `?sig=secret` query and `u:p@` userinfo in
  the inputs do not appear in the text; the text names `RedirectOptions.AllowHttpsToHttpDowngrade`)
- `The_replay_message_names_replayability_and_ToReplayableAsync` (`REDIR-6`: contains `"not replayable"` and
  `"ToReplayableAsync"`; the target is redacted)
- `No_leaf_exposes_a_Uri_or_string_url_property` (reflection: the declared public properties of the family are those of
  `Exception`/`SdkException` only; `XCUT-19`)
- `A_malformed_url_input_renders_the_sentinel_instead_of_throwing` (`UrlRedactor.Default` is total)

Red: compile error.

**Production.** `src/Dexpace.Sdk.Core/Errors/RedirectExceptions.cs`: `public class RedirectException : SdkException` with
the three constructors, and `public sealed class RedirectSchemeDowngradeException : RedirectException` and
`public sealed class RedirectBodyNotReplayableException : RedirectException`, each with the same three (the
`StreamingException` precedent in `LifecycleExceptions.cs`), each documented, each carrying the retry-semantics remark
("not a `ServiceRequestException`: raised after a response arrived"). The internal static `RedirectMessages` (same folder
as the decider, task 1.5) builds the two messages with `UrlRedactor.Default.Redact(Uri)` for every URL.

**PublicAPI.** The design's twelve `E.` lines (three types, each a type line plus three constructor lines).

**IDs:** `REDIR-6` (the error half), `REDIR-15` (the error half). **Verify:** V-fast `RedirectExceptionTests`.

### Task 1.5 — The internal vocabulary: `RedirectChain`, `RedirectDecision`, `RedirectStopReason`, `RedirectCondition` (`REDIR-16`, `REDIR-20`, `REDIR-21`; P6b-8, P6b-11, R5–R7)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/Redirect/RedirectChainTests.cs` (Unit):

- `A_new_chain_is_seeded_with_the_first_requests_userinfo_free_key` (`REDIR-16`; seed `https://u:p@h/a` → key `https://h/a`)
- `The_seed_origin_is_the_context_seed_requests_not_the_first_drive` (`REDIR-8`, `REDIR-11`: a chain built from a context whose
  seed differs from the policy's request uses the seed's origin; the original method is the **policy's** request's, P6b-4)
- `Advance_appends_the_target_key_and_counts_the_hop`
- `Keys_compare_ordinally_and_keep_the_fragment` (`https://h/a#x` and `https://h/a#y` are two keys; `HTTP-46`'s textual form, facts 4 and 7)
- `Re_spelled_equivalents_collide` (`HTTPS://H:443/a` and `https://h/a`: fact 7)
- `Snapshot_copies_the_visited_list` (R5: cast `VisitedUris` to `IList<Uri>`/array, attempt `Add`/index-set, assert the chain's next
  `Contains` result is unchanged; the cast either fails or writes to the copy)
- `Snapshot_carries_the_response_the_count_and_the_target` (`RedirectCondition` properties; `Target` null when unusable)

New `RedirectDecisionTests` in the same folder (Unit): `ReturnCurrent_carries_its_reason`, `Follow_carries_the_built_request`,
`Fail_carries_the_exception_and_its_kind`, `RedirectStopReason_values_are_explicit` (R7: `(int)` of each is pinned).

Red: compile error.

**Production.** Under `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/` (all `internal`, each type in its own file, headers
and docs):

- `RedirectStopReason.cs` (R7).
- `RedirectDecision.cs`: `readonly struct`, static `Follow(Request next, Uri target, bool crossOrigin, bool downgraded)`,
  `ReturnCurrent(RedirectStopReason reason, Uri? target = null, string? malformedRaw = null)`, `Fail(Exception exception,
  RedirectFailureKind kind)` (R6, R8); `Kind` enum `Follow = 0`, `ReturnCurrent = 1`, `Fail = 2` (explicit).
- `RedirectChain.cs`: `sealed class`; ctor `(Request first, PipelineContext context)`; members `SeedOrigin`, `OriginalMethod`
  (`first.Method`), `Current` (`Request`), `Followed` (`int`), `Contains(string key)`, `Advance(Request next, Uri target)`,
  `Snapshot(Response response, Uri? target)` returning `RedirectCondition`, and `static string KeyOf(Uri)` (the userinfo-free
  `AbsoluteUri`).
- `RedirectCondition` (task 1.3's skeleton) gains its internal constructor body; `VisitedUris` is `[.. visited]`.

If `IDE0130` (namespace does not match folder) is on and fails the build, flatten the folder into `Pipeline/Policies/` and update
this plan's later paths accordingly (design "Internal types").

**IDs:** `REDIR-16`, `REDIR-20`, `REDIR-21` (the snapshot half). **Verify:** V-fast `RedirectChainTests`, `RedirectDecisionTests`.

### Task 1.6 — Header stripping and the 303 rebuild: `RedirectReissue` (`REDIR-5`, `REDIR-7`, `REDIR-9`, `REDIR-10`, `REDIR-11`; P6b-5, P6b-13, P6b-23)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/Redirect/RedirectReissueTests.cs` (Unit), over
`RedirectReissue.Build(Request current, Uri target, int status, bool crossOrigin) -> Request`:

- `Authorization_is_removed_every_value_on_a_same_origin_hop` (two `Authorization` values; `REDIR-7`)
- `Cookie_and_Proxy_Authorization_survive_a_same_origin_hop` (`REDIR-10`)
- `Cookie_and_Proxy_Authorization_are_removed_every_value_cross_origin` (`REDIR-9`)
- `A_303_becomes_a_GET_from_any_method_with_no_body` (POST, PUT, DELETE, PATCH, HEAD; `REDIR-5`)
- `A_303_removes_every_Content_header_by_case_insensitive_prefix` (`content-type`, `CONTENT-LENGTH`, `cOnTeNt-Language`,
  `Content-MD5`, `Content-Disposition`, `Content-Encoding`; a non-content header such as `Accept` survives; Ruby round 3)
- `A_cross_origin_303_also_loses_Cookie_and_Proxy_Authorization` (Ruby round 1)
- `A_non_303_keeps_method_and_the_same_body_instance` (307, 308, 301, 302: `ReferenceEquals(next.Body, current.Body)`)
- `A_non_303_keeps_Content_headers`
- `Nothing_is_added_to_the_request` (header names after are a subset of those before; no marker, no `Referer`)
- `A_marker_shaped_header_is_an_ordinary_header` (P6b-23: `x-dexpace-internal-redirect-cross-origin: 1` survives a same-origin hop
  and is not interpreted; also survives a cross-origin hop, since only the three named headers are stripped)
- `The_rebuild_uses_one_constructor_call` (a 303 on a POST with a body does not throw: fact 14, `HTTP-7`)

Red: compile error.

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectReissue.cs`, `internal static class`:

```csharp
internal static Request Build(Request current, Uri target, int status, bool crossOrigin)
{
    var headers = current.Headers.Without(HttpHeaderName.WellKnown.Authorization);
    if (crossOrigin) headers = headers.Without("Cookie").Without("Proxy-Authorization");
    if (status != 303) return new Request(current.Method, target, headers, current.Body);
    foreach (var name in headers.Names.Where(IsContentHeader).ToArray()) headers = headers.Without(name);
    return new Request(Method.Get, target, headers, body: null);
}
```

(`Headers.Names` is materialised first because `Without` returns a new instance; the prefix test is
`name.StartsWith("content-", StringComparison.OrdinalIgnoreCase)`.)

**IDs:** `REDIR-5`, `REDIR-7`, `REDIR-9`, `REDIR-10`, `REDIR-11`. **Verify:** V-fast `RedirectReissueTests`.

### Task 1.7 — Failing tests: the decision matrix (`REDIR-1`–`REDIR-21`, `REDIR-26`; P6b-26, R9)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/Redirect/RedirectDecisionMatrixTests.cs` and the data class
`RedirectCases.cs` (the card's "one `[Theory]` table"). One `[Theory] public void Decide_matches_the_table(string name)` over
`public static TheoryData<string> Cases()` (the `REDIR-n: description` names; R9), which looks up `RedirectCases.Named(name)` and calls
`RedirectDecider.Decide(Response, RedirectChain, RedirectOptions)` and asserting the row's expectation. The row type (internal, never a parameter
or member type of a public API; options and predicate lambdas are built in code):

```csharp
internal sealed record RedirectCase(
    string Name, string Method, string Url, BodyKind Body, string[] RequestHeaders,
    int Status, string[] LocationValues, string[] ResponseHeaders,
    RedirectOptions Options, int Followed, string[] VisitedKeys, string? SeedUrl,
    Expect Expect);   // Kind, StopReason?, ExceptionType?, NextMethod, NextUrl, HeadersPresent, HeadersAbsent, SameBody, CrossOrigin, Downgraded
```

Rows are ported from `nodejs-sdk@54aeed4 packages/core/src/redirect/decide.test.ts`, one per `test(…)`, **in its section
order**, each citing its source line range in a comment above the group. The groups, with the changes the design lists:

| Node section (lines) | Rows | Ported as |
|---|---|---|
| fast path (129–162) | 200, 204, 300, 304, 305, 306, 399, 100 each with a `Location`; predicate never called (`REDIR-1`, `REDIR-2`, `REDIR-21`) | direct |
| predicate override (163–205) | predicate true on a non-default method follows; predicate false on GET stops; predicate sees snapshot; predicate called with no `Location`, with an unusable `Location` and at the cap (`REDIR-20`, `REDIR-21`) | direct |
| predicate: snapshot and safety mechanics (206–242) | predicate cannot defeat loop, cap, malformed, downgrade or replay gate (`REDIR-20` / P6b-7) | kept, **extended** to the loop and the cap |
| Location resolution (243–308), unfollowed paths (309–358), totality and configuration (359–406) | relative / absolute / protocol-relative / userinfo / `%2F` / IPv6 / non-default port; empty, whitespace, missing, `ftp:`, `mailto:`, empty host; a dozen hostile values instead of the fast-check property | direct, one row per `test(…)`, **except** L376 'the location header is configurable (REDIR-27)', which is **dropped** (`REDIR-27` ⏳, P6b-21: no `LocationHeader` member); the property becomes fixed examples |
| loop detection (407–438), survives normalization (803–836) | A→B→A; seed revisit including userinfo seed; case and default-port re-spellings | direct |
| hop cap (439–490) | counts 0..5 against `MaxRedirects` 0, 1, 3 (`REDIR-17`) | the property becomes fixed examples |
| scheme-downgrade guard (491–554) | https→http fails; opt-in follows with `Downgraded`; http→https follows; **https→http→https flags only the middle hop** (added) | direct plus one added |
| body replayability gate (555–611) | single-use body on 307/308/301/302 fails; 303 exempt; no body follows; replayable follows; **seekable `FromStream` follows, non-seekable fails** (added, 3b hand-off); **downgrade error wins over replay error** (design B) | direct plus added |
| header construction (612–667) | `Authorization` always removed; `Cookie`/`Proxy-Authorization` cross-origin only; port change only is cross-origin (`REDIR-8`) | direct |
| cross-origin marker (668–704), and the marker rows inside 'credential and marker hygiene against multi-valued headers' (L902 'a multi-valued forged marker collapses…', L922 'the 303 rebuild clears an inbound marker too') | **one P6b-23 row**: a marker-shaped header is ordinary | all four REDIR-11 marker rows (L669, L684, L902, L922) are **dropped** and replaced by the one row (design §10 entry 15 retires the marker) |
| 303 rebuild and method preservation (705–757) | GET from every method; no body; `Content-*` removed; method preserved on 307/308 with `AllowedMethods` containing it | direct; **plus** `content-type` / `CONTENT-LENGTH` / `cOnTeNt-Language` row and cross-origin-303-drops-`Cookie` row (added) |
| "REDIR-3 measures eligibility against the CURRENT hop" (758–802) | **inverted** to assert the literal reading: a 301 on a GET that an opted-in 303 produced from a POST, default set, is **not eligible** (P6b-4) | inverted |
| Location forms RFC 3986 (837–884) | dot segments, query-only, fragment-only, empty path | direct; **plus** a fragment-only and a fragment-bearing `Location` (added, Ruby round 2) |
| multi-valued headers (885–943) | two `Authorization` values removed (L886) | direct; L902 and L922 are the dropped marker rows above |
| carries the body instance (944–965) | `ReferenceEquals(next.Body, current.Body)` | direct |
| "the shared return-current value" (118–128) | — | **dropped** (JavaScript object identity; constraint 10) |

Added rows with no Node source: two `Location` values → `MalformedLocation` (P6b-10; the 'multi-valued headers' section has no such test); an IDN host spelled two ways (same origin; fact 10); `AllowedMethods` containing `POST` follows a
body-less POST (`BODY-5`); `AllowedMethods` empty follows only an opted-in 303; a 301 on `OPTIONS`/`TRACE`/`CONNECT` under the
default set is `NotEligible`; the status 301 on `HEAD` follows. Every row asserts `Kind`, the stop reason or exception type, and
on a follow the next method, exact `AbsoluteUri`, headers present and absent, body identity and `CrossOrigin`.

Red: compile error (`RedirectDecider` does not exist).

**IDs:** `REDIR-1`–`REDIR-21`, `REDIR-26`. **Verify:** the file fails to compile; commit nothing alone (the red state ends in 1.8).

### Task 1.8 — `RedirectDecider` (`REDIR-1`–`REDIR-21`; P6b-6, P6b-7, P6b-14, P6b-15)

**Production.** `src/Dexpace.Sdk.Core/Pipeline/Policies/Redirect/RedirectDecider.cs`:

```csharp
internal static RedirectDecision Decide(Response response, RedirectChain chain, RedirectOptions options)
{
    if (!IsRecognised(response.Status.Code)) return RedirectDecision.ReturnCurrent(NotARedirect);        // gate 1
    var outcome = RedirectLocation.TryResolve(chain.Current.Url, response.Headers, out var target, out var raw);   // gate 2
    var condition = chain.Snapshot(response, target);                                                    // gate 3: always allocated
    if (!IsEligible(response.Status.Code, chain, options, condition)) return ReturnCurrent(NotEligible);
    if (outcome != LocationOutcome.Resolved) return outcome == Absent ? ReturnCurrent(MalformedLocation without event) : ...;   // gate 4
    ...
}
```

Gates, in this fixed order, one private method each so every method stays under 70 lines (`MA0051`):

1. `IsRecognised`: `301, 302, 303, 307, 308` only.
2. Resolve (task 1.2); never throws.
3. Eligibility: `options.Predicate is { } p ? p(condition) : status == 303 ? options.FollowSeeOther :
   options.AllowedMethods.Contains(chain.OriginalMethod)`. A throwing predicate propagates (the policy disposes, task 1.9).
4. Unusable target: `Absent` → `ReturnCurrent(MalformedLocation)` marked as *absent* (no event; `REDIR-19`), `Malformed` →
   `ReturnCurrent(MalformedLocation, malformedRaw: raw)` (event in PR 2). The absent/malformed distinction is carried by
   `malformedRaw == null` (R8).
5. Loop: `chain.Contains(RedirectChain.KeyOf(target))` → `ReturnCurrent(LoopDetected, target)`.
6. Cap: `chain.Followed >= options.MaxRedirects` → `ReturnCurrent(HopCap)`.
7. Downgrade: current URL scheme `https` and target `http` → `Fail(RedirectSchemeDowngradeException, SchemeDowngrade)` unless
   `options.AllowHttpsToHttpDowngrade`, then `downgraded = true`.
8. Replay gate: status ≠ 303 and `chain.Current.Body is { IsReplayable: false }` → `Fail(RedirectBodyNotReplayableException,
   BodyNotReplayable)`. No idempotency is consulted (`BODY-5`).
9. Build: `crossOrigin = HttpOrigin.From(target) != chain.SeedOrigin`; `next = RedirectReissue.Build(...)`; `Follow(...)`.

**Verify:** V-fast `RedirectDecisionMatrixTests` (green), `RedirectLocationTests`, `RedirectReissueTests`,
`RedirectChainTests`. Add a pin test `The_decider_makes_no_call_outside_the_predicate` only if reflection-free (no); instead keep
purity by review.

**IDs:** `REDIR-1`–`REDIR-4`, `REDIR-6`, `REDIR-8`, `REDIR-15`–`REDIR-21`, `REDIR-26`.

### Task 1.9 — The rewritten policy; `RedirectPolicyTests` replaced; `ReDriveLifecycleTests` split (`REDIR-22`, `REDIR-23`, `REDIR-24`, `REDIR-25`; `PIPE-15`, `PIPE-16`, `PIPE-40`; P6b-9, P6b-17, P6b-18, P6b-22, P6b-27) — Breaking 3–8

**Failing tests.**

Replace `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/RedirectPolicyTests.cs` entirely (the card's exit: **replaced, not kept
alongside**). The new class (Unit), through `PipelineBuilder` over `ScriptedTransport` with `TrackingResponseBody`, each case
also run on the sync `Send` path (`Theory` over `bool sync` or a paired method):

- `Stage_is_Redirect` (kept)
- `The_superseded_response_is_disposed_before_the_next_send` (`REDIR-22`(a); log order `send:1`, `dispose:redirect`, `send:2`)
- `A_downgrade_failure_disposes_the_response_once_and_throws_the_exception_unchanged` (`REDIR-22`(b))
- `A_replay_failure_disposes_the_response_once_and_throws_the_exception_unchanged` (`REDIR-22`(b), `AllowedMethods = {POST}`)
- `A_throwing_predicate_disposes_the_response_and_propagates_unchanged` (P6b-9; the same exception instance)
- `A_dispose_failure_on_a_failing_hop_rides_the_primary_exceptions_suppressed_trail` (R1; `TrackingResponseBody(disposeFailure:)`)
- `Every_stop_reason_returns_the_response_undisposed` (`REDIR-22`(c); `[Theory]` over the five reasons passed as `int` and cast to the internal `RedirectStopReason` inside the method: `NotARedirect`, `NotEligible`,
  `MalformedLocation`, `LoopDetected`, `HopCap`)
- `A_to_B_to_A_returns_Bs_3xx_open` (`REDIR-16`, end to end)
- `The_cap_at_0_1_and_3_returns_the_last_3xx_without_throwing` (`REDIR-17`; `MaxRedirects = 0` follows nothing)
- `A_thousand_hop_chain_completes_under_a_raised_cap` (`REDIR-23`; scripted chain from `RedirectFixtures`, `MaxRedirects = 1000`)
- `A_predicate_cannot_reach_the_live_visited_set` (R5, `REDIR-20`)
- `The_predicate_is_called_once_per_recognised_3xx_and_never_otherwise` (`REDIR-21`; zero on 200/300/304/305; one on a
  3xx with no `Location`, with a malformed one, and at the cap)
- `A_cancelled_token_between_hops_throws_after_disposing_and_before_the_next_send` (P6b-18; transport call count stays 1)
- `An_auth_stage_probe_runs_once_per_hop` (`REDIR-24`; a `ProbePolicy` at `PipelineStage.Auth` counts calls)
- `Each_hop_is_driven_with_the_policys_own_request` (`PIPE-15`, `PIPE-16`; a downstream stamp does not reach hop 2)
- the conforming facts of today's class, moved unchanged in substance: the relative `Location`, no `Location`, malformed and
  non-http(s) scheme cases, the 200 pass-through, the chained multi-hop, `ProcessAsync_301OnGet_KeepsGet`
- the ported Ruby cases of `step_test.rb`: `LifecycleTest` (L776), `PredicateTest` (L202) and `RebuildTest` (L680) where they
  assert shared behaviour, each citing its line range (see the Ruby port table below for every class and file)

**Ruby port table** (`ruby-sdk/gems/dexpace-core/test/dexpace/redirect/`; every ported test cites its source line range in a
comment above it, as the 5a vectors did; a case that asserts a Ruby host fact or a primitive this port lacks is skipped and the
skip is noted in the target file's header comment):

| Ruby source (lines) | Target | Disposition |
|---|---|---|
| `step_test.rb` `ConstructionTest` (L58) | `RedirectOptionsTests` (task 1.3) | ported where it asserts option validation and defaults; cursor-construction cases skipped |
| `DecisionTest` (L140) | the matrix (task 1.7) | ported as rows where Node's matrix does not already hold them |
| `PredicateTest` (L202) | `RedirectPolicyTests` and the matrix predicate rows | ported |
| `CredentialHygieneTest` (L319) | `RedirectReissueTests` (task 1.6) | ported |
| `MarkerTest` (L407) | none | **not ported**: tests a cursor-state marker this port retires (§10 entry 15); replaced by the one P6b-23 row |
| `LocationTest` (L466) | `RedirectLocationTests` (task 1.2) | ported |
| `RefusedTargetTest` (L542) | `RedirectLocationTests` | ported (empty host, non-http(s) scheme) |
| `ReissueTest` (L608) | `RedirectReissueTests` | ported |
| `RebuildTest` (L680) | `RedirectPolicyTests` and `RedirectReissueTests` | ported |
| `LifecycleTest` (L776) | `RedirectPolicyTests` | ported |
| `EmissionTest` (L843) | `RedirectLogTests` (task 2.1) | ported where it asserts the five events |
| `ForkingTest` (L956) | none | **not ported**: tests a fork primitive this port does not have (4c's `ForHop`) |
| `condition_snapshot_test.rb` | `RedirectChainTests` (task 1.5) | ported (snapshot immutability, `REDIR-20`) |
| `events_test.rb` | `RedirectLogTests` (task 2.1) | ported where it asserts event names, levels and keys |
| `scheme_downgrade_error_test.rb` | `RedirectExceptionTests` (task 1.4) | ported (message names both redacted URLs and the opt-in) |
| `matrix_facts_test.rb` | none | skipped: asserts Ruby host facts (design, "Tests, vectors and ports") |

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/Redirect/RedirectFixtures.cs` (internal static): `MultiLocation(params string[])`,
`Chain(int hops, string host = "api.example.com")` (a scripted array of N distinct 302 responses then a 200), and a
`TrackedRedirect(string location, List<string> log, string name)` helper. TestSupport's `ScriptedTransport`,
`TestResponses.Redirect` and `TrackingResponseBody` cover the rest.

**Deleted, not kept** (the ten non-conforming facts, design "Tests, vectors and ports"): `ProcessAsync_302OnPost_BecomesGetWithNoBody`,
`A_303_on_a_POST_with_a_body_follows_as_a_bodiless_GET`, `ProcessAsync_303OnPut_BecomesGetWithNoBody`,
`ProcessAsync_301OnPost_BecomesGet`, `ProcessAsync_307OnPost_PreservesMethodAndBody`, `ProcessAsync_308OnPost_PreservesMethodAndBody`
(the preserved-POST case already lives in the matrix under `AllowedMethods = {POST}`), `ProcessAsync_HttpsToHttpDowngrade_NotFollowed_WhenFlagFalse`,
`ProcessAsync_307OnPost_NonReplayableBody_NotFollowed`, `ProcessAsync_MaxRedirects_StopsAndReturnsLast3xxResponse` as written
(restated at the new default above), and the `MakeOptions` helper's removed parameter.
`ProcessAsync_HttpsToHttpDowngrade_Followed_WhenFlagTrue` stays (restated through `AllowHttpsToHttpDowngrade = true`).
The two strip facts that used `MakeOptions(stripSensitiveHeadersOnCrossOrigin: true)`,
`ProcessAsync_CrossOriginRedirect_StripsAuthorizationAndCookieHeaders` and `ProcessAsync_SameOriginRedirect_StripsAuthorizationHeader`,
are **moved** into `RedirectReissueTests` (task 1.6: cross-origin strips `Authorization`, `Cookie`, `Proxy-Authorization`;
same-origin strips `Authorization` only) with the option argument gone, and are covered end to end by the matrix's header rows
and the hygiene class; they are not kept in `RedirectPolicyTests`.

Edit `tests/Dexpace.Sdk.Core.Tests/Pipeline/ReDriveLifecycleTests.cs`:

- `Options(int? maxRedirects = null, int maxRetries = 3)` builds `new RedirectOptions()` (R4) or `{ MaxRedirects = maxRedirects }`.
- `A_redirect_over_a_non_replayable_body_returns_the_in_flight_response_undisposed` is split into
  `A_redirect_on_a_method_outside_the_allowed_set_returns_the_in_flight_response_undisposed` (the reason it already passes for;
  `NotEligible`) and `A_redirect_over_a_non_replayable_body_throws_and_disposes_the_in_flight_response_once` (`AllowedMethods =
  {POST}`; `RedirectBodyNotReplayableException`; dispose count 1). All other cases untouched.

Red: the new class fails on the as-built behaviour (POST→GET, silent downgrade return, no loop detection, default 20, no
cancellation check); compile is green.

**Production.** Rewrite `src/Dexpace.Sdk.Core/Pipeline/Policies/RedirectPolicy.cs` (design position I). The loop, one
`while (true)` with no recursion:

```csharp
private static async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
{
    ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(context);
    var options = context.Options.Redirect;
    var chain = new RedirectChain(request, context);
    while (true)
    {
        var response = async ? await continuation.RunAsync(chain.Current, context.ForHop(chain.Followed)).ConfigureAwait(false)
                             : continuation.Run(chain.Current, context.ForHop(chain.Followed));
        var decision = await DecideOrDisposeAsync(response, chain, options, context, async).ConfigureAwait(false);
        switch (decision.Kind)
        {
            case ReturnCurrent: return response;                                   // REDIR-22(c), undisposed
            case Fail: await FailAsync(response, decision, context, async); throw decision.Exception;   // REDIR-22(b)
            default:
                await DisposeSupersededAsync(response, context, async);            // REDIR-22(a): before the next drive
                context.CancellationToken.ThrowIfCancellationRequested();          // P6b-18
                chain.Advance(decision.Next, decision.Target);
                break;
        }
    }
}
```

- `DecideOrDisposeAsync` wraps `RedirectDecider.Decide` in `try`/`catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))`:
  disposes `response` with `primary: ex` (R1) and rethrows with `throw;` (the exception is unchanged, P6b-9).
- The `throw decision.Exception` uses the exception instance from the decider; `Disposal.DisposeQuietly*(response, primary: ex,
  logger: context.State.Logger)` records a dispose failure on it.
- Remove the `MA0051` pragma pair and the private `WithoutUserInfo`/`IsCrossOrigin` helpers (their roles moved to tasks 1.2
  and 1.1). Remove `s_redirectStatuses`.
- Rewrite the class remarks (design "Migration plan"): the 301/302-on-POST and 303 bullets, the "Non-replayable body guard"
  paragraph and the `StripSensitiveHeadersOnCrossOrigin` sentence go; new text describes the allowed-method set, the opt-in
  303, the two errors, loop detection, the 3-hop cap, the predicate's scope (P6b-7), and carries the **Breaking** paragraphs
  (items 3–8 of the design's table) with what each was.
- `Process` keeps `SyncPath.GetCompletedResult(ProcessCoreAsync(…, async: false), nameof(RedirectPolicy))`.
- `REDIR-25`: the class remarks and `DexpacePipeline.CreateDefault`'s remarks state that the async standard pipeline follows
  redirects (§10 entry 14) and that `MaxRedirects = 0` returns the 3xx verbatim; if a remark names 20 hops, restate as 3.

**IDs:** `REDIR-22`, `REDIR-23`, `REDIR-24`, `REDIR-25` (documentation clause; 🚫 by entry 14). **Verify:** V-fast
`RedirectPolicyTests`, `ReDriveLifecycleTests`; then V-sec (both redirect `Security` classes **first**, before anything else);
then `StageOrderTests`, `PipelineBuilderTests`, `ResiliencePresetTests`, `DexpacePipelineTests`.

### Task 1.10 — Close-out (PR 1)

1. Run the PR 1 V-gate, **V-sec first**. Confirm `git diff --stat main...HEAD -- tests/*/Security` lists exactly
   `RedirectCredentialHygieneTests.cs` (convention 5).
2. `PublicAPI.Unshipped.txt`: apply the analyzer's code fixes; compare against the design's lists (removed 2, added
   `RedirectOptions` 6 lines, `RedirectCondition` 5, `RedirectException` family 12; the event and key constants wait for PR 2).
3. `CHANGELOG.md` `[Unreleased]`: one line per Breaking item 1–8, each saying what it was and how to restore a follow (add the
   method to `AllowedMethods`, or set a `Predicate`; neither restores the POST→GET rewrite, which `REDIR-3` forbids).
4. Grep for remaining crefs or remarks naming 20 hops or the removed property:
   `grep -rn "StripSensitiveHeadersOnCrossOrigin\|MaxRedirects.*20\|20 redirect" src docs/sdk-documentation README.md`.
5. Coverage gate (see Verification blocks).

---

## PR 2 — Events

**Gate: PR 1.** Rows: `REDIR-28`. Files: `Diagnostics/DexpaceLogEvents.cs`, `Diagnostics/DexpaceLogKeys.cs`,
`Diagnostics/RedirectLog.cs`, `Pipeline/Policies/RedirectPolicy.cs`, `PublicAPI.Unshipped.txt`, tests.

### Task 2.1 — Failing tests: the vocabulary and the emitters (`REDIR-28`, `OBS-20`, `OBS-39`; P6b-19, P6b-20)

Read first: `tests/Dexpace.Sdk.Core.Tests/Diagnostics/LogCatalogueTests.cs` and `LogVocabularyTests.cs` (they enumerate the
constants; they gain the new ones. Two assertions in `LogVocabularyTests` must change, named below), and TestSupport's `RecordingLogger`, `ThrowingLogger`,
`DisabledLogger`, `ActivityRecorder` (all in TestSupport) and `TracingFixtures.cs` (in `tests/Dexpace.Sdk.Core.Tests/Diagnostics/`, not TestSupport).

New `tests/Dexpace.Sdk.Core.Tests/Diagnostics/RedirectLogTests.cs` (Unit), over `RedirectLog` called directly and through the
policy:

- `Each_event_has_its_id_name_level_and_keys` (`[Theory]`: `http.redirect.hop`/150/Information with `url.full`,
  `dexpace.redirect.target`, `http.response.status_code`, `dexpace.redirect.hop`, `dexpace.redirect.cross_origin`;
  `loop_detected`/151/Warning; `scheme_downgrade_rejected`/152/Warning; `scheme_downgrade_permitted`/153/Warning;
  `location_malformed`/154/Warning with `dexpace.redirect.location`)
- `The_hop_number_is_one_based_and_the_cross_origin_flag_is_a_bool`
- `A_userinfo_and_token_bearing_target_is_redacted` (`?sig=secret`, `u:p@`)
- `A_malformed_location_carrying_userinfo_is_redacted_by_RedactHeaderValue` (P6b-20; `http://user:pw@/x` or `//u:pw@h` raw
  strings never appear)
- `A_throwing_logger_leaves_the_decision_and_the_response_unchanged` (`OBS-20`; `log_failed` reported, not thrown)
- `A_disabled_logger_builds_nothing` (the formatting delegates are never invoked; `DisabledLogger`/`IsEnabled(false)`)
- `NotEligible_and_HopCap_emit_no_event`; `An_absent_location_emits_no_event` (`REDIR-19`)
- `A_rejected_downgrade_emits_the_rejected_event_before_the_throw`
- `A_permitted_downgrade_emits_the_permitted_event_and_then_the_hop_event` (`XCUT-17`(d))
- `The_events_are_not_gated_by_HttpLoggingOptions_Level` (`Level = None` still emits; P6b-19)
- `A_followed_hop_adds_a_dexpace_redirect_hop_span_event_on_a_recording_operation_span` (5c's listener fixture; the attributes
  `dexpace.redirect.hop`, `http.response.status_code`, `url.full` redacted, `dexpace.redirect.cross_origin`; no other span event)
- `LogVocabularyTests` edits (the **one permitted narrowing**; every other assertion stays as is): `Id_ranges_are_inside_the_reserved_blocks`
  currently asserts no id in `110–119` or `140–169` (L89–90), which ids 150–154 break. Narrow the exclusion to `110–119`,
  `140–149` and `155–169`, and add `Assert.InRange(id, 150, 159)` for the five redirect ids. `Keys_are_the_OpenTelemetry_names`
  pins exactly 16 keys (L67) and an exact expected list: grow both to 20 (the four new `dexpace.redirect.*` keys; `url.full`
  and `http.response.status_code` already exist). 6a (`140–149`) and 6c (`160–169`) edit the same assertions, so each PR
  re-derives them on the merged file.
- `The_public_constants_have_the_published_values` (`DexpaceLogEvents.RedirectHop == "http.redirect.hop"`, ids 150–154, the four
  `DexpaceLogKeys` values)

Red: compile error (`RedirectLog` and the constants do not exist).

**IDs:** `REDIR-28`. **Verify:** compile red; `LogCatalogueTests`/`LogVocabularyTests` edited here fail their count/contents.

### Task 2.2 — Constants and `RedirectLog` (`REDIR-28`; `OBS-39`, `OBS-20`; P6b-19, P6b-20)

**Production.**

- `DexpaceLogEvents.cs`: add the five `const string` names and five `const int` ids from the design's "The public surface"
  (150–154), each documented (OBS-39), with the doc comment's id-range remark unchanged.
- `DexpaceLogKeys.cs`: `RedirectHop = "dexpace.redirect.hop"`, `RedirectTarget`, `RedirectCrossOrigin`, `RedirectLocation`.
- `src/Dexpace.Sdk.Core/Diagnostics/RedirectLog.cs`, `internal static partial class`? No source generator: a plain `internal
  static class` with five cached `LoggerMessage.Define` delegates (the `notes/observability.md` ruling), under the same scoped
  `#pragma warning disable CA1727` as `HttpLogEmitter` with the same explanatory comment, and five `internal static void`
  emitters:

  ```csharp
  internal static void Hop(PipelineContext context, Uri current, Uri target, int status, int hop, bool crossOrigin)
  internal static void LoopDetected(PipelineContext context, Uri current, Uri target, int followed)
  internal static void DowngradeRejected(PipelineContext context, Uri current, Uri target)
  internal static void DowngradePermitted(PipelineContext context, Uri current, Uri target)
  internal static void LocationMalformed(PipelineContext context, Uri current, string raw)
  ```

  Each: `var logger = context.State.Logger; try { if (!logger.IsEnabled(level)) return; var redactor =
  s_redaction.Get(context.Options.Logging).Redactor; … } catch (Exception ex) when (Reportable(ex, token))
  { ReportFailure(logger, <event name>, ex); }`, copying `HttpLogEmitter`'s `Reportable` / `ReportFailure` shape (if those are
  private there, extract them to an internal `EmissionGuard` helper shared by both, with `HttpLogEmitterTests` and
  `EmissionGuardTests` re-run unchanged). `s_redaction` is a `RedactionCache` instance following `OperationTelemetry`'s
  pattern (one allow-list per call). The malformed value goes through `redactor.RedactHeaderValue(raw)`.
- `PublicAPI.Unshipped.txt`: the 10 event lines and 4 key lines.

**IDs:** `REDIR-28`, `OBS-39`. **Verify:** V-fast `RedirectLogTests` (emitter cases), `LogCatalogueTests`, `LogVocabularyTests`.

### Task 2.3 — The policy calls them; the span event (`REDIR-28`; P6b-19, 5c)

**Production.** In `RedirectPolicy.ProcessCoreAsync` (the loop stays one method under 70 lines; extract `EmitFollow`,
`EmitReturn` and `EmitFail` helpers):

- `Follow`: `if (decision.Downgraded) RedirectLog.DowngradePermitted(...)`; `RedirectLog.Hop(...)` with
  `hop = chain.Followed + 1`; `OperationTelemetry.RedirectHop(context, chain.Followed + 1, status, decision.Target,
  decision.CrossOrigin)`; **all before** the superseded response is disposed (position J: "before the superseded response is
  disposed").
- `ReturnCurrent(LoopDetected)` → `RedirectLog.LoopDetected(...)`; `ReturnCurrent(MalformedLocation)` with a non-null raw →
  `RedirectLog.LocationMalformed(...)`; others none.
- `Fail` with `SchemeDowngrade` → `RedirectLog.DowngradeRejected(...)` before the dispose and throw.
- Emission never changes the decision or the lifecycle: the emitters swallow their own failures (`OBS-20`).

**IDs:** `REDIR-28`. **Verify:** V-fast `RedirectLogTests` (all), `RedirectPolicyTests`, `OperationEventTests`,
`OperationTelemetryTests`, `AuthHttpsGuardTests` (it now also emits the permitted event, unobserved); V-sec.

### Task 2.4 — Close-out (PR 2)

V-gate; `CHANGELOG.md` line for Breaking 9 (the policy writes `http.redirect.*` events to the pipeline's logger, the hop at
`Information`); coverage gate; `grep -rn "150\|151\|152\|153\|154" src/Dexpace.Sdk.Core/Diagnostics/DexpaceLogEvents.cs` confirms
no clash with 6a's 140–149 or 6c's 160–169 on the merged file.

---

## PR 3 — The convergence test and the close-out

**Gate: PRs 1 and 2**, and as late as practical against 6a and 6c (P6b-28). Rows: all 28 (closing). Files: the two leak
classes, the AOT smoke, docs, the checklist, the corrections.

### Task 3.1 — `RedirectCredentialLeakTests` (core; `REDIR-7`–`REDIR-11`, `XCUT-17`, `AUTH-29`, `XCUT-16`; P6b-25)

New `tests/Dexpace.Sdk.Core.Tests/Security/RedirectCredentialLeakTests.cs`, `[Trait("Category", "Security")]`. The pipeline is
the real stack: `RedirectPolicy` → `RetryPolicy(new InstantTimeProvider())` → a `PerHop` `ProbePolicy` → a real
`AuthorizationPolicy` subclass → `ScriptedTransport`. The constructors used are the ones that exist at `3a1db00`
(`RetryPolicy(TimeProvider? = null)`, `BasicAuthPolicy(BasicCredential)`, `BearerTokenAuthPolicy(TokenCredential, params string[])`);
if 6a or 6c have changed them when this task runs, adapt the construction without weakening an assertion (constraint 5) and
record it in the checklist. URLs are https throughout (the auth guard refuses plaintext). The seed carries a caller-set
`Authorization`, `Cookie` and `Proxy-Authorization`.

One `[Theory]` over `(Scenario, AuthKind)` where `AuthKind` is `Basic` or `Bearer`; the assertions run on **every** request the
transport recorded, never only the last (Ruby's testing strategy). Scenarios (the design's table, verbatim):

| Scenario | Script | Expected on each request after the seed |
|---|---|---|
| same-origin hop | 302 → same origin → 200 | `Authorization` = the auth policy's stamp, `Cookie`/`Proxy-Authorization` kept |
| cross-origin hop | 302 → other host → 200 | none of the three |
| cross-origin hop, then a retry | 302 → other host → 503 → 200 | none of the three on **both** foreign attempts |
| same-origin hop, then a retry | 307 → same origin → 503 → 200 | stamped on both attempts; cookie kept |
| a retry before the hop | 503 → 302 → other host → 200 | the seed's two attempts stamped; the foreign hop bare |
| foreign then back to the seed origin, with a retry | 302 → other → 302 → seed origin → 503 → 200 | foreign bare; back at the seed origin `Authorization` re-stamped by auth, caller's `Cookie`/`Proxy-Authorization` stay gone |
| a permitted downgrade, then a retry | 302 → `http://other` → 503 → 200 (`AllowHttpsToHttpDowngrade`) | none of the three; no `SdkException` from the HTTPS guard (`AUTH-29`) |
| a port change only | 302 → `https://seed:8443/` → 200 | none of the three (`REDIR-8`'s effective port) |

Extra facts in the same class: `A_return_to_the_seed_origin_does_not_restore_a_stripped_cookie` (phase 1's pin, restated end to
end), `A_redirect_exception_is_never_retried` (a downgrade rejection throws once; transport count 1; `Retry` never sees it).
Because the test must hold whichever retry/auth shape lands, it asserts on headers only, never on policy internals.

Red/green: this is a **pin** if PR 1 is correct (the behaviour exists); prove it able to fail by temporarily making
`RedirectReissue` keep `Cookie` cross-origin (never committed) and seeing the cross-origin rows fail.

**IDs:** `REDIR-7`, `REDIR-8`, `REDIR-9`, `REDIR-10`, `REDIR-11`, `REDIR-24`. **Verify:** V-fast `RedirectCredentialLeakTests`; V-sec.

### Task 3.2 — `RedirectCredentialLeakWireTests` (SystemNet; `TRANSPORT-1`, `REDIR-7`–`REDIR-9`; P6b-25)

New `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/RedirectCredentialLeakWireTests.cs`, `[Trait("Category", "Security")]`, in the style
of `RedirectWireTests` (read it first): two `LoopbackServer`s on two ports (two origins), an SDK-managed `SystemNetHttpClient`,
the pipeline `RedirectPolicy` → `RetryPolicy` → transport. The scenario is "cross-origin hop, then a retry": the first server
answers 302 to the second, the second answers 503 then 200, and the second server's recorded raw requests (both attempts)
carry **none** of `authorization`, `cookie`, `proxy-authorization`. A second case, `A_same_origin_hop_keeps_cookie`, pins
`REDIR-10` on the wire. The loopback is plain http, so no auth policy runs (`AUTH-28` would refuse it): the wire test proves the
caller-set half, the core test the stamped half (header comment says so).

**IDs:** `REDIR-7`, `REDIR-9`, `REDIR-10`, `TRANSPORT-1` (cited). **Verify:** V-fast (SystemNet project, `--filter-class
"*RedirectCredentialLeakWireTests"`); V-sec.

### Task 3.3 — NativeAOT smoke (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`; fact 11)

Add a `CheckRedirectAsync()` call to the run list in `Program.cs`/`SmokeChecks.cs` (read the file's check-registration shape
first). The AotSmoke project is a console program: `SmokeChecks` is an `internal static class` of plain `CheckXxx()` methods with
no attribute or trait, and the project references only Core, `Http.SystemNet` and `Serialization.SystemTextJson`, so TestSupport's
`ScriptedTransport` is **not** available. Build the transport as `CheckBridgesAndDelegateClientsAsync` does, with `DelegateHttpClient`
(or an in-file fake) returning a 307 and then a 200. The check covers: that scripted 307 followed under `AllowedMethods = {Get, Post}`
and a predicate (a `Func<RedirectCondition, bool>` lambda, no reflection); a `FrozenSet`-backed `AllowedMethods` assertion; and an
https→http hop rejected with `RedirectSchemeDowngradeException`, the message asserted to contain the redacted URLs. The check
throws on any mismatch, as its siblings do. Run:

```bash
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
```

**IDs:** `REDIR-20`, `REDIR-26` (AOT evidence), `REDIR-15`.

### Task 3.4 — User documentation

1. New `docs/sdk-documentation/redirect.md` (read `pipelines.md` and `retry`/`configuration.md` for tone): the defaults (3 hops, GET
   and HEAD only, 303 opt-in), the allowed-method set, why POST is never rewritten to GET, the predicate and its snapshot
   (including `Target`, and the scope in P6b-7), the two exceptions and how to buffer a body with `ToReplayableAsync()`, the
   downgrade opt-in, credential hygiene with the residual-risk paragraph (a secret in a custom header crosses origins; use
   `ApiKeyAuthPolicy`), the `MaxRedirects = 0` recipe for "give me the 3xx" (`REDIR-25`), the five log events and the span
   event, and the explicit-default-port residue (P6b-30). Risks 1 and 2 of the design are stated plainly.
2. `docs/sdk-documentation/pipelines.md`: the `MaxRedirects` sentence links `redirect.md`.
3. `src/Dexpace.Sdk.Core/README.md`: if the options sample shows redirect, restate it with the new members.

### Task 3.5 — The checklist

Write `docs/work/mvp/phase6/phase6b/…-phase6b-redirect-checklist.md` (path per the housekeeping filing in 3.7) from what was
built, one row per ID, using the plan's table as the starting point, with the statuses and evidence actually observed. States
`REDIR-13`'s residue (the elided default port) without asserting that `:443` survives, `REDIR-25` 🚫 citing entry 14, `REDIR-11`
✅ citing entry 15, and `REDIR-27` ⏳ with the design's reason. Cites the cross-owner rows' tests (below) so 3b's `BODY-4` /
`BODY-5` and 4c's `PIPE-40` checklists can cite them.

### Task 3.6 — Dated corrections, roadmap note, hand-offs

Per the design's "Corrections owed at close-out", each a dated correction naming its ruling:

- design §6.2 (As built line; predicate scope and `Target`; the exception family; the default-port residue), §11 new items at the
  next free numbers on the merged file (P6b-4, P6b-7, P6b-17, P6b-10, P6b-20), §12 `REDIR` row notes, §3.4 replay-gate sentence;
- roadmap: append this design's link to the phase 6 row's `sdk-design refs`, and a dated status note recording the end-to-end
  test as the convergence exit and naming which of 6a/6c it ran against;
- 3b checklist (`BODY-4`, `BODY-5`) and 4c checklist (`PIPE-40`) cite PR 1's tests;
- `CLAUDE.md`: "What is genuinely unbuilt" drops the redirect rewrite; the `Pipeline/Policies/` layout line names the redirect
  folder; `Errors/` gains the `RedirectException` family; "Things That Will Bite You" gains the one line (redirects follow only
  GET and HEAD by default, 303 is opt-in, a downgrade or a non-replayable body throws);
- `docs/first-release.md`: `REDIR-27` ⏳ under "What v1 ships without" with no named trigger;
- `CHANGELOG.md`: confirm every Breaking line from PR 1 and PR 2 is present.

### Task 3.7 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6b            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6b --write    # git mv the design, plan and checklist
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix what the last probe reports. Then run the full V-gate once more, **V-sec first**.

---

## Keeping the `Security` classes green

Constraint 5; the design's table (P6b-24) is the authority and the plan adds the *when*:

| Class | Edit | When it is run |
|---|---|---|
| `RedirectCredentialHygieneTests` | **three hunks** in task 1.3: the `303` row gains `FollowSeeOther = true`; `Stripping_is_on_by_default_regardless_of_the_legacy_switch` becomes `Stripping_holds_under_every_redirect_option` (equal or larger claim); the header comment is updated | first, after task 1.3 and after task 1.9; then every V-gate |
| `RedirectWireTests` | none | after task 1.9 (V-sec) and in every V-gate; every case seeds a GET over a same-scheme loopback with at most two hops |
| `AuthHttpsGuardTests` | none | task 1.9 (it sets `AllowHttpsToHttpDowngrade = true`, whose name is kept, and seeds a GET); task 2.3 (its downgrade case now also emits the permitted event, unobserved) |
| `ReDriveRequestIsolationTests` | none | task 1.9 (a GET with a 307) |
| `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests`, `RetryPacingOverflowTests`; SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | none | V-gate |
| **Added:** `RedirectCredentialLeakTests` (core), `RedirectCredentialLeakWireTests` (SystemNet) | new | tasks 3.1, 3.2 |

**The check is the exact allowed diff**: after PR 1, `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Core.Tests/Security
tests/Dexpace.Sdk.Http.SystemNet.Tests/Security` lists `RedirectCredentialHygieneTests.cs` only; after PR 3 it additionally lists
the two added files and nothing else. A change that seems to force another edit there is a signal to re-read the design's
"Keeping the `Security` classes green", not to edit the file.

---

## Checklist: one row per owned ID

Exactly 28 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 3.5 records what was
built. "Pin" means the test already passes on the as-built behaviour and is proven able to fail (convention 1). Level counts:
23 MUST, 4 SHOULD (`REDIR-10`, `REDIR-21`, `REDIR-23`, `REDIR-28`), 1 MAY (`REDIR-27`).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `REDIR-1` | MUST | 1 | 1.7, 1.8 | ✅ | `RedirectDecisionMatrixTests` fast-path rows |
| `REDIR-2` | MUST | 1 | 1.7, 1.8 | ✅ | matrix rows 300/304/305 with a `Location`; predicate never called |
| `REDIR-3` | MUST | 1 | 1.3, 1.7, 1.8, 1.9 | ✅ | matrix rows (301/302 preserve method and body, original-method eligibility, inverted Node row); `RedirectOptionsTests.Defaults_…` |
| `REDIR-4` | MUST | 1 | 1.7, 1.8 | ✅ | matrix rows 307/308 |
| `REDIR-5` | MUST | 1 | 1.6, 1.7, 1.8 | ✅ | `RedirectReissueTests.A_303_becomes_a_GET_…`, `…removes_every_Content_header_…`; matrix 303 rows |
| `REDIR-6` | MUST | 1 | 1.4, 1.7, 1.8, 1.9 | ✅ | `RedirectExceptionTests.The_replay_message_names_replayability_…`; `RedirectPolicyTests.A_replay_failure_disposes_…`; matrix replay-gate rows |
| `REDIR-7` | MUST | 1, 3 | 1.6, 3.1, 3.2 | ✅ | `RedirectReissueTests.Authorization_is_removed_every_value_…`; `RedirectCredentialLeakTests`; `RedirectCredentialLeakWireTests` |
| `REDIR-8` | MUST | 1, 3 | 1.1, 1.5, 3.1 | ✅ | `HttpOriginTests` (all); `RedirectChainTests.The_seed_origin_is_…`; leak-test port-change row |
| `REDIR-9` | MUST | 1, 3 | 1.6, 3.1, 3.2 | ✅ | `RedirectReissueTests.Cookie_and_Proxy_Authorization_are_removed_…`; leak tests |
| `REDIR-10` | SHOULD | 1, 3 | 1.6, 3.1, 3.2 | ✅ | `RedirectReissueTests.Cookie_and_Proxy_Authorization_survive_a_same_origin_hop`; leak-test same-origin rows |
| `REDIR-11` | MUST | 1, 3 | 1.6, 3.1 | ✅ (no marker; §10 entry 15) | `RedirectReissueTests.A_marker_shaped_header_is_an_ordinary_header`; the matrix P6b-23 row |
| `REDIR-12` | MUST | 1 | 1.2 | ✅ | `RedirectLocationTests.Userinfo_is_dropped`; `RedirectCredentialHygieneTests` (kept) |
| `REDIR-13` | MUST | 1 | 1.2 | ✅ (residue: explicit default port elided, P6b-30) | `RedirectLocationTests.Reserved_escapes_survive_…`, `An_explicit_default_port_is_elided` |
| `REDIR-14` | MUST | 1 | 1.2 | ✅ | `RedirectLocationTests.A_relative_location_resolves_against_the_current_hop_…`, `…protocol_relative…` |
| `REDIR-15` | MUST | 1, 2 | 1.4, 1.8, 1.9, 2.3 | ✅ | `RedirectPolicyTests.A_downgrade_failure_…`; matrix downgrade rows; `RedirectLogTests.A_permitted_downgrade_…` |
| `REDIR-16` | MUST | 1 | 1.5, 1.8, 1.9 | ✅ | `RedirectChainTests` (key rows); `RedirectPolicyTests.A_to_B_to_A_returns_Bs_3xx_open`; matrix loop rows |
| `REDIR-17` | MUST | 1 | 1.3, 1.8, 1.9 | ✅ | `RedirectOptionsTests.Defaults_…`, `A_negative_MaxRedirects_…`; `RedirectPolicyTests.The_cap_at_0_1_and_3_…` |
| `REDIR-18` | MUST | 1, 2 | 1.2, 1.8 | ✅ | `RedirectLocationTests.A_non_http_scheme_is_malformed`, `An_empty_host_…`, `Two_location_values_…`, `The_resolver_never_throws_…`; `RedirectLogTests` malformed event |
| `REDIR-19` | MUST | 1 | 1.2, 1.8 | ✅ | `RedirectLocationTests.A_missing_header_is_absent` (and the empty / whitespace twins) |
| `REDIR-20` | MUST | 1 | 1.3, 1.5, 1.7, 1.9 | ✅ | matrix predicate rows; `RedirectPolicyTests.A_predicate_cannot_reach_the_live_visited_set`, `A_throwing_predicate_…` |
| `REDIR-21` | SHOULD | 1 | 1.5, 1.7, 1.9 | ✅ | `RedirectPolicyTests.The_predicate_is_called_once_per_recognised_3xx_and_never_otherwise` |
| `REDIR-22` | MUST | 1 | 1.9 | ✅ | `RedirectPolicyTests` (a), (b) ×3, (c) × 5 reasons; `ReDriveLifecycleTests` (split) |
| `REDIR-23` | SHOULD | 1 | 1.9 | ✅ | `RedirectPolicyTests.A_thousand_hop_chain_completes_under_a_raised_cap` |
| `REDIR-24` | MUST | 1, 3 | 1.9, 3.1 | ✅ (pin on stage order, new behavioural test) | `StageOrderTests` (existing); `RedirectPolicyTests.An_auth_stage_probe_runs_once_per_hop` |
| `REDIR-25` | MUST | 1 | 1.9 (docs) | 🚫 (design §10 entry 14, `async-redirect-pillar`; P6b-22) | the remarks on `RedirectPolicy` and `DexpacePipeline`; `MaxRedirects = 0` case in `RedirectPolicyTests` |
| `REDIR-26` | MUST | 1, 3 | 1.3, 3.3 | ✅ | `RedirectOptionsTests.AllowedMethods_is_copied_at_init`; AOT smoke |
| `REDIR-27` | MAY | — | — | ⏳ (declined for v1; P6b-21; `docs/first-release.md`) | the checklist's argument; no `LocationHeader` member (`RedirectOptionsTests.RedirectOptions_has_no_LocationHeader_member`) |
| `REDIR-28` | SHOULD | 2 | 2.1, 2.2, 2.3 | ✅ | `RedirectLogTests` (all); span-event case |

Count: **28 rows**, all mapped (23 MUST, 4 SHOULD, 1 MAY). By exit: 26 ✅ (`REDIR-1`–`REDIR-24`, `REDIR-26`, `REDIR-28`), 1 🚫
(`REDIR-25`), 1 ⏳ (`REDIR-27`), 0 N/A. By level: 22 of 23 MUSTs ✅ and `REDIR-25` 🚫; the four SHOULDs ✅; the MAY ⏳.

### Work on other owners' rows (no checklist row in 6b)

These carry no exit mark in 6b's checklist (the 4c precedent). 6b's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `BODY-4` (3b; the gates travel to 6a/6b/6c) | The redirect path's gate: consult `IsReplayable` and decline **loudly**; seekable `FromStream` bodies follow | 1.7, 1.8, 1.9 | matrix seekable/non-seekable rows; `ReDriveLifecycleTests.A_redirect_over_a_non_replayable_body_throws_…` |
| `BODY-5` (3b) | A body-less POST under `AllowedMethods = {POST}` is followed; idempotency is not consulted | 1.7 | matrix row |
| `PIPE-15`, `PIPE-16` (4c) | Each hop is a fresh drive with the policy's own request | 1.9 | `RedirectPolicyTests.Each_hop_is_driven_with_the_policys_own_request`; `ReDriveRequestIsolationTests` |
| `PIPE-40` (4c) | Intermediate-dispose order; "return current" left open; the non-replayable abandon path read as retry's (P6b-17) | 1.9 | `RedirectPolicyTests` lifecycle cases; `ReDriveLifecycleTests` |
| `XCUT-17` (10) | (a)–(d) end to end, including (d)'s rejection error and "logging the deviation" | 1.8, 2.3, 3.1 | matrix downgrade rows; `RedirectLogTests`; `RedirectCredentialLeakTests` permitted-downgrade row |
| `AUTH-29`, `XCUT-16` (6c, 10) | The credential-free downgrade hop passes the HTTPS guard | 3.1 | `RedirectCredentialLeakTests` permitted-downgrade row; `AuthHttpsGuardTests` (kept) |
| `HTTP-46` (2a) | Visited keys compare textual forms ordinally; `Uri.Equals` is never used | 1.5 | `RedirectChainTests.Keys_compare_ordinally_and_keep_the_fragment` |
| `OBS-20`, `OBS-39` (5b) | Every redirect emission is guarded; the five names/ids and four keys are public constants | 2.1, 2.2 | `RedirectLogTests.A_throwing_logger_…`, `The_public_constants_…` |
| `TRANSPORT-1` (8b) | Unchanged; `RedirectPolicy` is the single authority | 3.2 | `RedirectCredentialLeakWireTests`; `RedirectWireTests` |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 1 | none; V-sec first | `REDIR-1`–`REDIR-24`, `REDIR-26` (`REDIR-25` argued) | 1.1–1.10 (10) |
| 2 | PR 1 | `REDIR-28` | 2.1–2.4 (4) |
| 3 | PRs 1, 2 | all 28 (closing); the convergence exit | 3.1–3.7 (7) |
| pre-flight | — | — | 0.1 (1) |
| **Total** | | | **22 tasks** (21 in three PRs, plus the pre-flight) |

Rows by the PR that first lands them: PR 1: 25 (`REDIR-1`–`REDIR-24` and `REDIR-26`; `REDIR-25` closes in the same PR as a documentation
clause; `REDIR-27` has no code); PR 2: `REDIR-28`; PR 3: no new row, closing evidence for `REDIR-7`–`REDIR-11` and `REDIR-24`.
24 + 1 (`REDIR-26`) + 1 (`REDIR-28`) + 1 🚫 (`REDIR-25`) + 1 ⏳ (`REDIR-27`) = 28. PR 2 needs PR 1 (the policy); PR 3 needs both.
Every PR stays green in the order the gates allow.

Every ID in the design's scope is covered by at least one task: `REDIR-1`/`2` (1.7, 1.8), `3`/`4` (1.3, 1.7, 1.8), `5` (1.6),
`6` (1.4, 1.8, 1.9), `7`/`9`/`10`/`11` (1.6, 3.1, 3.2), `8` (1.1, 1.5), `12`/`13`/`14`/`18`/`19` (1.2), `15` (1.4, 1.8, 2.3),
`16` (1.5, 1.9), `17` (1.3, 1.9), `20`/`21` (1.5, 1.7, 1.9), `22`/`23`/`24` (1.9), `25` (1.9 documentation, 🚫), `26` (1.3),
`27` (3.5, ⏳ argued), `28` (2.1–2.3).

---

## Findings while planning

Items checked against the repository at `3a1db00` on 2026-10-08, each with what the plan did. R1–R9 above are the readings
these produced.

1. **F1 — `Disposal.DisposeQuietlyAsync` exists** and the as-built policy already uses it; the design names only the sync form
   (R1). The plan uses both.
2. **F2 — Three call sites name the property being removed** (R2): the hygiene `Security` class, the old policy tests' helper and
   the options-shape test's name list. They are edited in task 1.3, in the same commit as the removal.
3. **F3 — The hygiene class's edit is forced, not elective.** Removing the property makes the replaced test uncompilable; the
   design's P6b-24 argues the replacement is a strictly larger claim, and the plan states the three hunks so the review diff is
   checkable (convention 5; contrast 5a's empty-diff rule).
4. **F4 — The design is written without an SDK; the plan is too.** Facts 7–11 are verified in task 0.1 before code. Fact 8
   (the empty-host screen) and fact 10 (IDN) each reopen one ruling if they come out the other way; fact 11 decides whether
   `AllowedMethods` is case-sensitive and whether a matrix row is needed.
5. **F5 — `ReDriveLifecycleTests.Options(maxRedirects: 20)` passes an explicit value** (R4): the new default changes none of its
   outcomes; only the helper's default parameter changes.
6. **F6 — `LogCatalogueTests` and `LogVocabularyTests` enumerate the event constants.** They are read and extended in task 2.1,
   with the single narrowing named in task 2.1 (the reserved-range exclusion, 5 ids) and the key count 16 → 20.
7. **F7 — `OperationTelemetry.RedirectHop` takes the target as a `Uri` and redacts it itself** (fact 16); the policy passes the
   resolved `Uri`, never a string, and the hop log event passes the same object.
8. **F8 — The leak test depends on constructors owned by 6a and 6c** (design Risk 3). PR 3 is last for that reason; task 3.1
   authorises adapting construction (never an assertion) and requires the adaptation be recorded in the checklist.
9. **F9 — A thousand-hop chain needs 1 000 distinct targets** (a repeated target is a loop, `REDIR-16`); `RedirectFixtures.Chain`
   builds `…/h0`, `…/h1`, … and the cap is raised to 1000.
10. **F10 — Node's `decide.test.ts` marker rows, its fast-check properties and its frozen-object row do not port as written** (design
    "Tests, vectors and ports"): the four marker rows (L669, L684, L902, L922) become one P6b-23 row; L376 (configurable `Location`, `REDIR-27`) is dropped, properties become fixed examples, the frozen-object row is
    dropped; the "CURRENT hop" row is inverted (P6b-4).
11. **F11 — `REDIR-25` and `REDIR-27` have no code.** `REDIR-25` is a documentation clause under §10 entry 14; `REDIR-27` is a
    declined MAY with a reflection fact (`RedirectOptions_has_no_LocationHeader_member`, task 1.3) that no `LocationHeader` member exists. Both appear in the checklist with their
    arguments, so the 28-row count is exact.
12. **F12 — `IDE0130` (namespace/folder match) is unverified.** Task 1.5 flattens `Pipeline/Policies/Redirect/` if the analyzer
    rejects the organisational folder.

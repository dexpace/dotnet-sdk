# Phase 6c — Authentication: Implementation Plan

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Design: [phase 6c authentication design](2026-10-08-phase6c-auth-design.md), the
authority for every decision below. The plan cites its census rows, argued positions (A–G), facts (1–17) and rulings
(`P6c-1`…`P6c-41`) rather than restating them. Scope authority: the roadmap's Phase 6 card and Phase List row 6. Format
precedent: the [5a plan](../../phase5/phase5a/2026-10-07-phase5a-configuration.md) and its
[5b](../../phase5/phase5b/2026-10-07-phase5b-logging.md) and [5c](../../phase5/phase5c/2026-10-07-phase5c-tracing.md)
siblings (read first).

**What this document is.** The roadmap's step 3 for sub-phase 6c: numbered TDD tasks in the design's eight-PR segmentation,
each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** markings, `CHANGELOG.md` entry,
requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 8.3) and it
writes no production code. The [checklist section](#checklist-one-row-per-owned-id) below is the plan's own row table:
exactly one row per ID 6c owns.

**Scope.** 38 rows: `AUTH-1`–`AUTH-38` (36 MUST, 2 SHOULD: `AUTH-19`, `AUTH-38`). Planned exits: 38 ✅, 0 N/A, 0 🚫, 0 ⏳.
`XCUT-12`, `XCUT-14`, `XCUT-16`, `XCUT-19`(d), `XCUT-21`, `BODY-4` and `PIPE-28` are other owners' rows on which 6c does
work; they appear in [the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-6c) and carry no exit mark in
6c's checklist. Issues: **#7** is closed by PR 5 (parser, Basic, composite and the `401` hook land by PR 5; see task 5.7),
**#2** by PR 6.

**Order and gates.** PR 1 has no gate. **PR 2 follows PR 1** (`AuthCredentials.Available` is a list of `AuthScheme`). **PR 3 follows
PR 2** (`BasicChallengeHandler` reads `BasicCredential.HeaderValue`). **PR 4 follows 1, 2 and 3** (it needs the descriptor, the
credentials and `IChallengeHandler`). **PR 5 follows PR 4** (the cache's hook rides the base's `401` lifecycle). **PR 6
follows PRs 2 and 3** (`DigestCredential`, the handler SPI); its end-to-end replay test (task 6.6) needs PR 4, so the plan lands PR 6 after PR 4 and
may start tasks 6.1–6.5 beside PR 4 and PR 5. **PR 7 follows 1, 4, 5 and 6.** **PR 8 follows 1–7.** `AuthHttpsGuardTests`
(`Security`) runs first after PR 4 (P6c-8). The segmentation is the design's; the plan does not change it.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`; the parser, the Digest computation and the cache each name their helper split below and add no waiver); `///`
   XML docs on every public member (CS1591); no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2; `MD5`, `SHA256`,
   `RandomNumberGenerator`, `Convert.ToHexStringLower`, `SemaphoreSlim`, `ExceptionDispatchInfo`, `ImmutableArray` and
   `LoggerMessage` are all in the shared framework or the already permitted `Microsoft.Extensions.Logging.Abstractions`). No
   reflection in `src/`, so `IsAotCompatible` and `IsTrimmable` hold unannotated. No regular expression in the parser (§6.3).
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `Security` on
   `CredentialRedactionTests`, `Integration` on `DigestWireTests`, `AotSmoke` on the smoke checks. Core tests live under
   `tests/Dexpace.Sdk.Core.Tests/<area>/` (namespace `Dexpace.Sdk.Core.Tests.<Area>`); doubles under
   `tests/Dexpace.Sdk.TestSupport/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2), so the one
   wire test is in `Dexpace.Sdk.Http.SystemNet.Tests`.
5. **Security tests are never deleted or loosened.** 6c edits exactly one (`AuthHttpsGuardTests`, strengthened, task 4.5) and
   adds one (`CredentialRedactionTests`, task 2.4). The V-gate's diff check is the **exact allowed diff** (see
   [Keeping the `Security` classes green](#keeping-the-security-classes-green)).
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`
   and a removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the
   authority (apply the analyzer's code fix and compare against the design's list, "The public surface"). Only core's file
   changes; the SystemNet and STJ files must not (the V-gate checks). A sealed record's synthesised members appear as the
   `RequestOptions` block shows (`<Clone>$`, `Equals(T?)`, the three overrides, the two operators).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes,
   stating what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes",
   items 1–9). 6a and 6b edit the same changelog; whichever PR lands second re-derives its hunks on the merged file.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1): write
   `System.IO.…` fully qualified where a core file needs it. Digest and the parser touch no `System.IO` type.
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `feat:` for an additive one, `test:` for
   tests only, `docs:` for documentation only, `chore:` for refactors. **No AI attribution** of any kind in a commit message,
   changelog line, PR body or document. The plan authorises no `git push`, no `gh` command that writes, and no remote
   action; the closing comments on #7 and #2 are drafted in tasks 5.7 and 6.7 for the lead to post. Branches follow
   `<issue>-phase-6c-<slug>` off `main` (`70-phase-6c-pr1-descriptor`, …).
10. **Names from siblings are placeholders where 6a or 6b touch the same file** (`PublicAPI.Unshipped.txt`, `CHANGELOG.md`,
    `DexpaceLogEvents.cs` (6a, 6b add ids 140–159), `HttpHeaderName`, `RequestOptions`, `BannedSymbols.txt`): each PR
    re-derives its hunks from the merged file and never resolves a conflict by keeping either side whole.
11. **Test timing.** A test that must wait uses `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`, pinned in
    `Directory.Packages.props`) or a `TaskCompletionSource` gate; none sleeps on the wall clock to prove a negative. No test
    reads a real clock, the process environment or the network (the wire test is loopback only).
12. **Secrets in tests.** A test that formats a credential asserts the secret is absent (`Assert.DoesNotContain`) and uses a
    distinctive literal (`"s3cr3t-Zx9"`), so a leak cannot match by accident.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build
is the lint gate. The test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's
`--filter`). **The design was written on a host with no .NET SDK**; every command below is untested on that host and runs
for the first time on the implementer's. Facts 4, 7, 8, 9 and 13 are therefore verified in task 0.1 before any code is
written.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # the exact allowed diff, per PR (see Security section)
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
artifacts/test-results`) runs before the push of PR 4, PR 5, PR 6 and PR 8. No `PackageReference` changes, so no
`packages.lock.json` change is expected; if a locked restore complains, run `dotnet restore` on both solutions and commit
every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info AUTH
scripts/knowledge --gaps AUTH                # 0 of 38: no AUTH ID is a gap ID (design, "Governing documents")
scripts/knowledge --req AUTH-31              # per row in scope of the PR (also XCUT-14, XCUT-16, BODY-4 for cited work)
```

Task 0.1 records any difference from the design's corpus-file reading (the host had no SDK, so the CLI never ran). A
difference that contradicts a ruling reopens that ruling.

---

## Plan-level readings (where the design was silent, ambiguous or wrong about the tree)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it, and
flagged it for the lead where the reading adds behaviour. **R1–R6 are corrections of fact against the tree at `3a1db00`;
R7–R14 are additions.**

- **R1 — `BoundedMap` has no `GetOrAdd`.** The as-built map offers `Set`, `TryAdd`, `TryGetValue`, `TryRemove` and
  `TryRemoveIfSame`, and its `TValue` must be a reference type. The cache and the nonce store therefore share one internal
  helper, `BoundedMapExtensions.GetOrAdd<TKey, TValue>(this BoundedMap<TKey, TValue>, TKey, Func<TValue>)` (`TryGetValue`,
  else `TryAdd` a fresh value, else `TryGetValue` again), added in task 5.3 under `Internal/`. A value evicted while a request
  still holds it keeps working (the holder owns the reference); the next request creates a new entry, which is the cap's
  documented price (design F, `XCUT-14`).
- **R2 — `TokenCredential` already has `GetToken` (a blocking bridge with its own `RS0030` pragma).** P6c-26's "sync path"
  therefore needs no new credential member; `AccessTokenCache.Get` calls `TokenCredential.GetToken`, and a credential that is
  synchronous overrides it. The bridge pragma in `TokenCredential` stays (it is the SPI's documented default, not the
  policy's). What retires is `AuthorizationPolicy.GetCredential`'s bridge.
- **R3 — PR 4 needs a temporary bearer sync bridge.** The base's two `GetCredential` members become abstract in PR 4, but
  `AccessTokenCache.Get` lands in PR 5. Task 4.4 gives `BearerTokenAuthPolicy.GetCredential` the as-built blocking bridge
  (one `RS0030` pragma, moved from the base, commented "removed by task 5.5") so PR 4 stays green and
  `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_documented_bridge` still passes; task 5.5 deletes the pragma and
  rewrites that test. Net `RS0030` pragma count in `AuthorizationPolicy`: 1 → 0 at PR 4; in `BearerTokenAuthPolicy`: 0 → 1 →
  0.
- **R4 — `AccessToken.ExpiresOn` becoming nullable touches the as-built cache in PR 2.** `AccessTokenCache.IsValid` reads
  `now < token.ExpiresOn`. Task 2.2 changes that one line to `token.ExpiresOn is not { } e || now < e` so PR 2 compiles and
  stays green; PR 5 replaces the whole method. `grep -rn "ExpiresOn" src tests` lists every reader (task 2.2 migrates each).
- **R5 — `Response.Status` is a `Status` value type with `.Code`, and `Headers.GetAll(string)` already returns every field
  line.** The `401` test is `response.Status.Code == 401`; the challenge fields are `response.Headers.GetAll(
  HttpHeaderName.WellKnown.WwwAuthenticate)` (P6c-14). No new header-reading helper is added.
- **R6 — `TrackingResponseBody` and `TestResponses` already exist in `TestSupport`** (`tests/Dexpace.Sdk.TestSupport/
  Transports/`), with `DisposeCount`. The design's "added to `TestSupport` if absent" resolves to *absent: nothing added*.
  Task 4.2 adds only `TestResponses.Unauthorized(params string[] wwwAuthenticate)` if no equivalent exists (`TestResponses.
  Create` is read first; the helper is a thin wrapper).
- **R7 — `ChallengeFixtures`** (the eight Ruby strings) lives in `tests/Dexpace.Sdk.Core.Tests/Auth/ChallengeFixtures.cs` as
  `internal const string`s; the JSON vectors carry the cases that need an expected structure. Fixtures are test code, not
  `TestSupport`, because only the parser tests use them.
- **R8 — `Authorize` takes a `Request` and returns a `Request?`; it never mutates.** Handlers build the replacement with
  `request.WithHeaders(request.Headers.Set(name, value))` (the pattern `AuthorizationPolicy` already uses). A handler that
  cannot answer returns `null` and consumes no Digest count (P6c-35).
- **R9 — The event constants follow the as-built `DexpaceLogEvents` shape.** The file holds `public const string` names and
  `public const int …Id` ids (`HttpRequestId = 100`). Task 5.1 adds `TokenRefreshFailed = "dexpace.auth.token_refresh_failed"`
  and `TokenRefreshFailedId = 160` beside them, and the `EventId` is `new EventId(DexpaceLogEvents.TokenRefreshFailedId,
  DexpaceLogEvents.TokenRefreshFailed)`, as the 5b events are built (read before writing).
- **R10 — `params IEnumerable<T>` constructors** (`AuthDescriptor`, `CompositeChallengeHandler`) use C# 13's params
  collections, as `ScriptedTransport(params IEnumerable<object>)` already does in `TestSupport`. A call with an array or a
  list still compiles.
- **R15 — `AccessTokenCache` has one optional-parameter constructor (a dated correction to the design's surface block).** The design
  lists `(TokenCredential, TimeProvider? = null)` and `(TokenCredential, TimeSpan, TimeProvider? = null)`. With
  Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0 two public overloads with optional parameters trip `RS0026`, and the shorter one
  also trips `RS0027`; both are errors under `TreatWarningsAsErrors` (phase 4c design line 452 verified this; 3a and 5a avoided
  overloads for the same reason). The plan keeps `(TokenCredential credential, TimeProvider? timeProvider = null)` and turns the
  margin into `RefreshMargin { get; init; }`, validated non-negative. `GetAsync` keeps the as-built `ct` parameter name. The design
  file carries the same correction (section F and the PublicAPI block).
- **R11 — `AuthChallengeContext` is constructed by the base through an `internal` constructor** and is a sealed class (not
  a record: it holds a disposable `Response`, and record equality over one would be a trap). Its tests are in
  `AuthorizationPolicyChallengeTests` through a probe policy.
- **R12 — `MultiSchemeAuthPolicy` is gated on the lead (P6c-36, open).** PR 7 is written in full; if the lead drops the
  policy, PR 7 and its rows' end-to-end share (`AUTH-4`/`AUTH-5` through a policy that holds several credentials) are
  deleted from the plan and from the checklist; `AUTH-4`/`AUTH-5` stay ✅ through the single-scheme policies and
  `AuthResolverTests`.
- **R13 — `Md5Availability` is `internal static` with an `internal static bool IsAvailable` backed by `Lazy<bool>` and an
  `internal static bool Probe(Func<byte[]> hash)`** the tests call with a throwing delegate; the handler's internal
  constructor takes the availability as a `bool`. If task 0.1 finds `MD5.IsSupported` on `net10.0`, `Probe`'s default
  delegate reads it first (fact 7); no public surface depends on the outcome.
- **R14 — `username*` encoding is a private helper of `DigestComputation`** (`Rfc8187Encode(string, Encoding)`): percent-
  encode every byte outside `ALPHA / DIGIT / "!#$&+-.^_`|~"` (RFC 8187 `attr-char`), upper-case hex, prefix
  `UTF-8''` or `ISO-8859-1''`. It is tested through the handler's output and by two vector rows.

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no PR; no commit)

**Why first.** The design's facts 4, 7, 8, 9 and 13 were never run (no SDK on the design host). Several tasks depend on the
answers.

**Do.**

1. Run the five phase-start queries above and compare with the design's table ("The knowledge queries could not be run on
   this host"). Record every difference in a scratch note (`$SCRATCH/phase6c-preflight.md`); a difference that contradicts a
   ruling goes to the lead before PR 1 starts.
2. Create a throwaway console project **outside the repository** (scratchpad directory, `net10.0`, no package references)
   and print, for each fact:
   - **4** — `Convert.ToHexStringLower(new byte[] { 0xAB, 0x0F })` (expect `ab0f`).
   - **7** — `MD5.HashData([])` succeeds; whether a static `MD5.IsSupported` member exists (compile attempt); record both.
     If a FIPS-mode OpenSSL is available (`OPENSSL_CONF` pointing at a FIPS-only config), run it there and record the
     exception type.
   - **8** — start a task inside `using (ExecutionContext.SuppressFlow())` while `Activity.Current` is non-null (an
     `ActivitySource` with a listener); the task prints `Activity.Current is null` (expect `True`).
   - **9** — start a `HttpListener` or `TcpListener` on loopback, send via `SocketsHttpHandler` a request to
     `http://127.0.0.1:PORT/a%2Fb?x=%26y` and to `http://127.0.0.1:PORT`, read the raw request line, and print it beside
     `uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)` for the same `Uri` (expect equal, and `/` for the
     empty path). Record the result: it decides whether task 6.6 pins `GetComponents` or falls back to `PathAndQuery`.
   - **13** — compute, with `MD5`/`SHA256` and the design's formulas, the RFC 2617 §3.5 response for `Mufasa`/`Circle Of
     Life`, realm `testrealm@host.com`, nonce `dcd98b7102dd2f0e8b11d0f600bfb0c093`, uri `/dir/index.html`, `GET`, qop
     `auth`, `nc=00000001`, cnonce `0a4f113b` (expect `6629fae49393a05397450978507c4ef1`); the legacy no-qop form (expect
     `670fd8c2df070c60b045671b8b24ff02`); and RFC 7616 §3.9.1's inputs under SHA-256 and SHA-256-sess (record both full
     64-digit values for `digest.json`; the RFC's printed SHA-256 response has 63 digits, an erratum, so it is not a vector).
   - **Extra (plan-level)** — `new SemaphoreSlim(1, 1).Wait(0)` twice (true, false); `ExceptionDispatchInfo.Capture(ex)
     .Throw()` appends rather than resets a stack trace (R-risk 3); `Encoding.Latin1.GetBytes("é€")` is `E9 3F` (fact 6);
     `RandomNumberGenerator.GetHexString(32, lowercase: true).Length == 32` (fact 5).
3. Run `grep -rn "new AccessToken(\|\.ExpiresOn" src tests` and `grep -rn "WithheldHeaderName\|GetCredentialAsync\|GetCredential(" src
   tests docs README.md` and record every site (design fact 16/17 list four; a fifth is a PR 2 or PR 4 task).
4. Re-read `src/Dexpace.Sdk.Core/Diagnostics/DexpaceLogEvents.cs`, `Internal/BoundedMap.cs` and `Pipeline/Policies/
   RedirectPolicy.cs` (it strips `Authorization` per hop; 6b rewrites it, 6c must not break its tests) and record their
   current member names for R1 and R9.

**Outcomes feed.** A fact that comes out the other way reopens the ruling that cites it (design "Facts the design rests on")
and stops the PR that depends on it. **Exit:** the scratch note exists; nothing is committed.

---

## PR 1 — The descriptor and the resolver

**Gate: none.** Rows: `AUTH-1`–`AUTH-7`. Files: `src/Dexpace.Sdk.Core/Auth/{AuthScheme,AuthRequirement,AuthDescriptor,
AuthResolver}.cs`, `Errors/AuthResolutionException.cs`, `Http/Request/RequestOptions.cs`, `PublicAPI.Unshipped.txt`, and the
tests named below.

### Task 1.1 — Failing tests: scheme, requirement, descriptor, resolver (`AUTH-1`–`AUTH-7`; P6c-3 to P6c-5)

New `tests/Dexpace.Sdk.Core.Tests/Auth/AuthDescriptorTests.cs`, class `AuthDescriptorTests`:

- `AuthScheme_values_are_explicit_and_exactly_five` (`AUTH-1`; `OAuth2` 0, `ApiKey` 1, `Basic` 2, `Digest` 3, `NoAuth` 4;
  `Enum.GetValues<AuthScheme>().Length == 5`; `default(AuthScheme) == AuthScheme.OAuth2`, not `NoAuth`: P6c-3)
- `A_requirement_rejects_an_undefined_scheme` (`(AuthScheme)99` throws `ArgumentOutOfRangeException`)
- `NoAuth_is_the_anonymous_sentinel` (`AuthRequirement.NoAuth.Scheme == NoAuth`; the static returns an equal instance each
  time)
- `Scopes_and_parameters_are_copied_at_init` (`AUTH-2`; mutate the source list/dictionary after `init`; the requirement
  is unchanged; `Scopes` default `[]`, `Parameters` default empty; a `null` scope element throws `ArgumentException`)
- `Scopes_and_parameters_are_preserved_and_never_interpreted` (`AUTH-2`; a requirement with `Scopes = ["a", "b"]` and
  `Parameters = {["k"] = "v"}` reads them back in order)
- `Requirements_with_equal_content_are_equal_and_hash_alike` (`AUTH-2`, fact 1: two requirements built from different
  `List<string>` instances with equal contents are `Equal`; differing scope order is unequal; differing parameter value is
  unequal; `GetHashCode` agrees on equal ones)
- `A_descriptor_copies_its_requirements_and_exposes_them_read_only` (`AUTH-3`; mutating the source array after
  construction changes nothing; `Requirements` is an `IReadOnlyList<AuthRequirement>` that is not castable to
  `IList<AuthRequirement>`)
- `An_empty_descriptor_or_a_null_element_throws_ArgumentException` (`AUTH-3`)
- `AllowsAnonymous_is_true_iff_any_requirement_is_NoAuth` (`AUTH-3`; three descriptors)
- `Descriptors_with_the_same_ordered_requirements_are_equal` (order matters: `[a, b]` ≠ `[b, a]`)
- `A_descriptor_has_no_init_member_so_with_cannot_empty_it` (reflection: `AuthDescriptor` declares no property with an
  `IsExternalInit` setter; P6c-4)

New `tests/Dexpace.Sdk.Core.Tests/Auth/AuthResolverTests.cs`, class `AuthResolverTests`:

- `Per_call_wins_over_operation_and_client` / `Operation_wins_over_client` / `Client_is_used_when_the_others_are_absent`
  (`AUTH-4`; each asserts the returned requirement's scheme)
- `A_higher_tier_that_cannot_be_satisfied_does_not_fall_through` (`AUTH-4`; per-call `[Digest]`, client `[OAuth2]`,
  available `{OAuth2}` → `AuthResolutionException`, never the client's `OAuth2`)
- `The_first_satisfiable_requirement_in_preference_order_wins` (`AUTH-5`; `[Digest, OAuth2, Basic]` over `{OAuth2, Basic}`
  → `OAuth2`)
- `NoAuth_is_always_satisfiable` (`AUTH-5`; `[NoAuth]` over the empty set; `[Digest, NoAuth]` over `{}` → `NoAuth`)
- `No_credential_is_inspected` (`AUTH-5`; the resolver's signature takes `IReadOnlyCollection<AuthScheme>`, so the
  test is a compile-time pin plus a reflection check that `AuthResolver` declares no parameter of a credential type)
- `All_tiers_absent_throws_ArgumentException` (`AUTH-6`)
- `Nothing_satisfiable_throws_AuthResolutionException_with_required_and_available` (`AUTH-6`; `Required` in preference order,
  `Available` in enum order whatever the caller's collection order; message contains both lists and never a scope value)
- `Resolve_is_safe_under_concurrent_calls_on_one_descriptor` (`AUTH-7`; 64 parallel `Task.Run` calls on one shared
  descriptor, every result equal; reflection pin: `AuthResolver` is `static`, has no instance or static field)

Tests for the carrier are task 1.4's.

Red: the types do not exist (compile error, expected). **IDs:** `AUTH-1`–`AUTH-7`. **Verify:** V-fast `AuthDescriptorTests`,
`AuthResolverTests` (red).

### Task 1.2 — The data types and the exception (`AUTH-1`–`AUTH-3`; P6c-3, P6c-4)

**Production** (all under `Dexpace.Sdk.Core.Auth` unless stated):

- `AuthScheme.cs`: `public enum AuthScheme { OAuth2 = 0, ApiKey = 1, Basic = 2, Digest = 3, NoAuth = 4 }`, every member
  documented, remarks stating that `NoAuth` is never written to the wire and that `0` is deliberately not the permissive
  value (P6c-3).
- `AuthRequirement.cs`: `public sealed record AuthRequirement` with the constructor `AuthRequirement(AuthScheme scheme)`
  (`Enum.IsDefined` check → `ArgumentOutOfRangeException`), `Scheme { get; }`, and `init` members `Scopes`
  (`IReadOnlyList<string>`; backing `ImmutableArray<string>`; `ThrowIfNull(value)`, a null element → `ArgumentException`)
  and `Parameters` (`IReadOnlyDictionary<string, string>`; backing `ImmutableDictionary<string, string>` with
  `StringComparer.Ordinal`, built as `RequestOptions.Tags` is), `static AuthRequirement NoAuth { get; } = new(AuthScheme.NoAuth)`.
  **Hand-written** `Equals(AuthRequirement?)` (scheme, `SequenceEqual` over scopes, ordinal content comparison of
  parameters) and `GetHashCode` (fact 1: synthesised equality would compare the immutable collections by reference).
  `PrintMembers` renders scheme and scope list only (a parameter value may be a tenant identifier; it is not printed).
  Use the `field` keyword for `init` members as `RequestOptions` does.
- `AuthDescriptor.cs`: `public sealed record AuthDescriptor` with `AuthDescriptor(params IEnumerable<AuthRequirement>
  requirements)` (R10; copy to `ImmutableArray<AuthRequirement>`; empty → `ArgumentException`; a null element →
  `ArgumentException`), `Requirements { get; }` (no `init`), `AllowsAnonymous { get; }` computed once, hand-written
  `Equals`/`GetHashCode` by sequence.
- `Errors/AuthResolutionException.cs` (`Dexpace.Sdk.Core.Errors`): `public sealed class AuthResolutionException : SdkException`
  with `Required`, `Available` (both `IReadOnlyList<AuthScheme>`, copied) and a constructor
  `(IReadOnlyList<AuthScheme> required, IReadOnlyList<AuthScheme> available)` that builds the deterministic message
  (`"No configured authentication scheme satisfies the operation: required [Digest, OAuth2], available [Basic]."`). Not a
  `ServiceRequestException` and implements no retry marker (P6c-41). Follow `HttpResponseException`'s precedent of a
  domain constructor (read it first; add the analyzer-requested `[SuppressMessage]`-free shape only if CA1032 asks, with a
  documented rationale in `.editorconfig` territory avoided: prefer the three standard constructors as `internal`-delegating
  overloads if the analyzer insists).
- XML docs: remarks on `AuthRequirement` state "Scopes and parameters are preserved and never interpreted by the resolver
  (`AUTH-2`)".

**PublicAPI:** the `AuthScheme`, `AuthRequirement`, `AuthDescriptor` and `AuthResolutionException` lines from the design's
"Added" block, from the analyzer's code fix. **IDs:** `AUTH-1`, `AUTH-2`, `AUTH-3`. **Verify:** V-fast `AuthDescriptorTests`.

### Task 1.3 — `AuthResolver` (`AUTH-4`–`AUTH-7`; P6c-5)

**Production.** `Auth/AuthResolver.cs`: `public static class AuthResolver` with `Resolve(AuthDescriptor? perCall,
AuthDescriptor? operation, AuthDescriptor? client, IReadOnlyCollection<AuthScheme> availableSchemes)`. Body (about 25
lines): `ThrowIfNull(availableSchemes)`; `var selected = perCall ?? operation ?? client ?? throw new ArgumentException(
"At least one authentication tier must be present.")`; loop `selected.Requirements` returning the first whose scheme is
`NoAuth` or `availableSchemes.Contains`; else throw `AuthResolutionException` with `Required` the selected schemes in
order (duplicates collapsed, first occurrence kept) and `Available` the available schemes sorted by enum value and
de-duplicated. No field, no cache (`AUTH-7`). Docs state the tier order and "no fall-through".

**IDs:** `AUTH-4`–`AUTH-7`. **Verify:** V-fast `AuthResolverTests` (green).

### Task 1.4 — The carrier: `RequestOptions.Auth` and `OperationAuth` (`AUTH-4`; P6c-6) — Breaking 9

**Test first.** In the existing `tests/Dexpace.Sdk.Core.Tests/Http/Request/RequestOptionsTests.cs` (read first for its name):
`Auth_and_OperationAuth_default_to_null`; `Two_options_with_equal_descriptors_are_equal` (separately built, equal-content
descriptors); `Options_differing_only_in_Auth_are_unequal` and the same for `OperationAuth`; `GetHashCode_agrees_for_equal_options`;
`With_sets_each_tier_without_touching_the_other`. Red: members do not exist.

**Production.** Add to `RequestOptions` two `AuthDescriptor?` members, `Auth` and `OperationAuth`, `{ get; init; }`, documented
as the per-call and operation tiers of `AUTH-4` (design A, P6c-6), extend the hand-written `Equals` and `GetHashCode`
with both (an `AuthDescriptor` compares by value). `<para><b>Breaking:</b> equality now includes the two descriptors</para>`.
Check every `RequestOptions` equality-sensitive caller still compiles (`grep -rn "RequestOptions" src | grep -i equal`).

**PublicAPI:** `RequestOptions.Auth.get/init`, `RequestOptions.OperationAuth.get/init`. **IDs:** `AUTH-4`. **Verify:** V-fast
`RequestOptionsTests`, then the whole `Dexpace.Sdk.Core.Tests` project.

### Task 1.5 — Close-out (PR 1)

`CHANGELOG.md` `### Added`: the descriptor types, the resolver and `AuthResolutionException`, `RequestOptions.Auth`/
`OperationAuth`; `### Changed`: `RequestOptions` equality includes the two descriptors (**Breaking**, behaviour). Run
**V-gate** (Security diff: empty for PR 1). **Commit:** `feat: phase 6c descriptor and resolver (AUTH-1..AUTH-7)`.

---

## PR 2 — Credentials

**Gate: PR 1 merged** (`AuthScheme`). Rows: `AUTH-8`–`AUTH-10`, the credential halves of `AUTH-14` and `AUTH-26`, `XCUT-19`(d). Files:
`src/Dexpace.Sdk.Core/Auth/{AccessToken,ApiKeyCredential,BasicCredential,DigestCredential,TokenRequestContext,
AuthCredentials,AccessTokenCache(one line, R4)}.cs`, `PublicAPI.Unshipped.txt`, and the tests below.

### Task 2.1 — Failing tests: credentials (`AUTH-8`–`AUTH-10`, `AUTH-14`, `AUTH-26`; P6c-17 to P6c-22)

Extend `tests/Dexpace.Sdk.Core.Tests/Auth/CredentialTypesTests.cs` (read it first). The file holds the classes `AccessTokenTests`,
`TokenRequestContextTests`, `TokenCredentialTests`, `ApiKeyCredentialTests` and `BasicCredentialTests`; no class is named
`CredentialTypesTests`. Put the `AccessToken_*` and `IsExpired_*` cases in `AccessTokenTests`, the `ApiKeyCredential_*` cases in
`ApiKeyCredentialTests`, the `BasicCredential_*` cases in `BasicCredentialTests`, `TokenRequestContext_copies_its_scopes` in
`TokenRequestContextTests`, and add two new classes in the same file, `DigestCredentialTests` and `AuthCredentialsTests`. The
as-built `ApiKeyCredentialTests` has only null-key and empty-key cases (no whitespace case exists), so the whitespace case below is
a new test; `new AccessToken(...)` uses are migrated in task 2.5. Every V-fast filter for this file is
`--filter-class "*AccessTokenTests" --filter-class "*TokenRequestContextTests" --filter-class "*ApiKeyCredentialTests" --filter-class "*BasicCredentialTests" --filter-class "*DigestCredentialTests" --filter-class "*AuthCredentialsTests"` (abbreviated below as **the credential classes**):

- `AccessToken_rejects_null_empty_and_whitespace_tokens` (`AUTH-9`; `[Theory]` null, `""`, `"  "`)
- `AccessToken_without_expiry_never_expires` (`AUTH-10`; `new AccessToken("t")` has `ExpiresOn == null` and `IsExpired(
  DateTimeOffset.MaxValue, TimeSpan.Zero)` is false)
- `IsExpired_is_strictly_after` (`AUTH-10`; at `now == ExpiresOn` not expired; `now = ExpiresOn + 1 tick` expired; with a
  30 s margin, `now = ExpiresOn - 29 s` expired, `ExpiresOn - 31 s` not)
- `AccessToken_equality_is_by_value_over_token_expiry_and_refresh` (`AUTH-8`, P6c-21: equal triple equal; differing only in
  `RefreshOn` unequal; `==`/`!=` agree with `Equals`; hash agrees)
- `AccessToken_ToString_redacts_the_token` (`AUTH-8`; contains `***`, not the token; renders `ExpiresOn`/`RefreshOn`)
- `ApiKeyCredential_rejects_a_whitespace_only_key` (`AUTH-9`; breaking 2)
- `ApiKeyCredential_scheme_must_be_null_or_non_blank_without_whitespace` (`AUTH-26`; `"SharedAccessKey"` ok, `""`, `" "`,
  `"Shared Key"` throw `ArgumentException`)
- `ApiKeyCredential_rejects_a_value_the_outbound_header_grammar_refuses` (`AUTH-26`, `XCUT-18`, fact 12; a key containing
  `\r\n` or a non-ASCII character throws `ArgumentException` at construction, message never contains the key)
- `ApiKeyCredential_exposes_the_stamped_value_computed_once` (internal `HeaderValue` equals `"SharedAccessKey k"` /
  `"k"`; the test project sees it through `InternalsVisibleTo`)
- `ApiKeyCredential_ToString_redacts_the_key` (`AUTH-8`)
- `BasicCredential_rejects_empty_username_or_password_but_allows_whitespace` (`AUTH-14`; `("u", "")` throws, `("u", " ")`
  and `(" ", "p")` are accepted: P6c-17, breaking 3)
- `BasicCredential_rejects_a_colon_in_the_username` (RFC 7617 §2; the password may contain `:`)
- `BasicCredential_ToString_redacts_the_password` (`AUTH-8`)
- `DigestCredential_rejects_empty_username_or_password_and_redacts` (P6c-18)
- `TokenRequestContext_copies_its_scopes` (P6c-22; mutate the source list after construction; `Scopes` and `CacheKey` are
  unchanged and consistent)

Red: members missing or behaviour absent. **IDs:** `AUTH-8`–`AUTH-10`, `AUTH-14`, `AUTH-26`. **Verify:** V-fast
the credential classes (red).

### Task 2.2 — `AccessToken` (`AUTH-8`–`AUTH-10`; P6c-21, R4) — Breaking 1

**Production.** `Auth/AccessToken.cs`: stays a `readonly struct`, now `IEquatable<AccessToken>`. Constructors `(string token)`,
`(string token, DateTimeOffset? expiresOn)`, `(string token, DateTimeOffset? expiresOn, DateTimeOffset? refreshOn)`, each
`ArgumentException.ThrowIfNullOrWhiteSpace(token)`. `ExpiresOn` is `DateTimeOffset?`. `IsExpired(DateTimeOffset now,
TimeSpan margin) => ExpiresOn is { } e && now + margin > e`. `Equals(AccessToken)`, `Equals(object?)`, `GetHashCode`
(`HashCode.Combine(Token, ExpiresOn, RefreshOn)`), `==`, `!=`, `ToString()` =
`AccessToken { Token = ***, ExpiresOn = …, RefreshOn = … }` (`default(AccessToken)` has a null token: `ToString` must not
dereference it). Docs carry the **Breaking** note (was: `ExpiresOn` non-nullable, no equality, `ToString` the type name).

**R4:** change `AccessTokenCache.IsValid` to `token.ExpiresOn is not { } e || now < e`. Migrate every reader found by task
0.1 step 3 (`new AccessToken(...)` calls compile unchanged; code reading `ExpiresOn` as non-nullable gets `.Value` or a
pattern). **PublicAPI:** the design's removed and added `AccessToken` lines. **IDs:** `AUTH-8`–`AUTH-10`. **Verify:** V-fast
the credential classes, `AccessTokenCacheTests`.

### Task 2.3 — The key, password and digest credentials; `TokenRequestContext` (`AUTH-8`, `AUTH-9`, `AUTH-14`, `AUTH-26`; P6c-17 to P6c-22) — Breaking 2, 3, 4

**Production.**

- `ApiKeyCredential`: `ThrowIfNullOrWhiteSpace(key)`; `scheme` null or non-blank with no whitespace; compute
  `internal string HeaderValue` once (`scheme is null ? key : scheme + " " + key`) and validate it with the outbound-header
  grammar through the same internal check `Headers.Set` uses (`HeaderSyntax.ValidateOutboundValue(value, name, paramName)` in
  `Http/Common/HeaderSyntax.cs`, not the public predicate class `HttpHeaderSyntax`; its message echoes one offending character and
  index, never the value, and the exception is an `ArgumentException` naming the parameter). The
  `ApiKeyAuthPolicy` reads `HeaderValue` in task 4.4 (it stops concatenating per request). `ToString()` =
  `ApiKeyCredential { Key = ***, HeaderName = X-Api-Key, Scheme = … }`.
- `BasicCredential`: `ThrowIfNull` then empty checks for both members (whitespace allowed); `username.Contains(':')` →
  `ArgumentException`. `ToString()` redacts the password and prints the username (it is an identifier, not a secret).
  `ToBase64()` stays public; add `internal string HeaderValue { get; }`, a get-only property assigned once in the constructor (`"Basic " + ToBase64()`); an expression-bodied property would recompute the base64 on every read.
- `DigestCredential` (new, `sealed class`): non-empty username and password; no encoding check (P6c-32); `ToString()`
  redacts.
- `TokenRequestContext`: copy `scopes` into an `ImmutableArray<string>`; `Scopes` returns it as `IReadOnlyList<string>`;
  `CacheKey` is built from the copy.
- Docs: each type's remarks carry its **Breaking** note (design "Breaking changes" 2, 3, 4).

**PublicAPI:** `DigestCredential`, `ToString` overrides, nothing else new. **IDs:** `AUTH-8`, `AUTH-9`, `AUTH-14`, `AUTH-26`.
**Verify:** V-fast the credential classes (green).

### Task 2.4 — `AuthCredentials` and the redaction class (`AUTH-8`; `XCUT-19`(d); P6c-20)

**Test first.** New `tests/Dexpace.Sdk.Core.Tests/Security/CredentialRedactionTests.cs`, `[Trait("Category", "Security")]`, header
comment citing phase 1 S5, `XCUT-19`(d), design §6.3 fact 2 and "permanent under constraint 5":

- `Every_credential_type_formats_without_its_secret` (`[Theory]` over a member-data list of `(object value, string secret)`
  for `AccessToken`, `ApiKeyCredential`, `BasicCredential`, `DigestCredential`: `ToString()`, `$"{value}"`,
  `string.Format("{0}", value)` and `value.ToString()` through `object` each contain `***` and not the secret)
- `AuthCredentials_with_every_member_set_lists_schemes_and_leaks_nothing` (the text names the configured schemes, contains
  none of the four secrets nor a member's own `ToString` output)
- `A_record_that_wraps_a_credential_does_not_reintroduce_the_secret_unsuspected` (a private nested `record Holder(
  BasicCredential C)`: `Holder.ToString()` prints `C`'s redacted form; pins that the redaction is the credential's own)
- `Every_public_type_in_the_Auth_namespace_that_holds_a_secret_overrides_ToString` (reflection over the namespace: any
  public type with a property named `Token`, `Key`, `Password` or `Secret` declares `ToString` itself; a scanner self-test
  with two private fixture types, one compliant, one not, proves it can fail)

New `AuthCredentials` tests in `AuthCredentialsTests` (in `Auth/CredentialTypesTests.cs`): `Defaults_have_no_credentials_and_a_30_second_refresh_margin`,
`Margin_must_be_non_negative`.

**Production.** `Auth/AuthCredentials.cs`: `public sealed record AuthCredentials` with `TokenCredential? Token`, `ApiKeyCredential?
ApiKey`, `BasicCredential? Basic`, `DigestCredential? Digest`, `TimeSpan TokenRefreshMargin` (`init`, negative →
`ArgumentOutOfRangeException`, default `AccessTokenCache.DefaultRefreshMargin`, which task 5.3 defines; this task defines a
private 30 s constant and 5.3 replaces it with the cache's), a computed internal `IReadOnlyList<AuthScheme> Available`
(OAuth2 if `Token`, ApiKey, Basic, Digest in enum order) and a hand-written `PrintMembers` that prints
`Schemes = [OAuth2, Basic]` only. **PublicAPI:** `AuthCredentials` lines. **IDs:** `AUTH-8`; `XCUT-19`(d) (cited).
**Verify:** V-fast `CredentialRedactionTests` and the credential classes.

### Task 2.5 — Migrate the as-built tests that assert changed behaviour (breaking 2, 3)

Rewrite, never delete without a replacement: `BasicAuthPolicyTests.ProcessAsync_EmptyPassword_StampsCorrectly` becomes
`BasicCredential_with_an_empty_password_is_rejected_at_construction` (asserts the constructor throws; the stamping case it
covered is now unreachable), and the as-built `ApiKeyCredentialTests` null/empty-key cases stay as they are (the whitespace-key case is new, task 2.1, not a migration). Any
`AccessToken` assertion that compared by reference or relied on `ToString` is updated. Record the changes in the
"existing assertions changed" table the checklist keeps (task 8.3). **Verify:** the whole core test project.

### Task 2.6 — Close-out (PR 2)

`CHANGELOG.md` `### Changed`: breaking items 1–4 (token non-blank/nullable expiry/equality/redaction; key credential
validation; Basic credential validation incl. the colon rule; `TokenRequestContext.Scopes` is a copy); `### Added`:
`DigestCredential`, `AuthCredentials`, `CredentialRedactionTests` mention. Run **V-gate**; the Security diff lists exactly
`tests/Dexpace.Sdk.Core.Tests/Security/CredentialRedactionTests.cs` (added). **Commit:** `feat!: phase 6c credentials
(AUTH-8..AUTH-10)`.

---

## PR 3 — The challenge parser and the Basic and composite handlers

**Gate: PR 2 merged** (`BasicCredential.HeaderValue`). Rows: `AUTH-12`, `AUTH-13`, `AUTH-14` (handler), `AUTH-23`, `AUTH-25`. Files:
`src/Dexpace.Sdk.Core/Auth/{AuthenticationChallenge,ChallengeParser(internal),IChallengeHandler,BasicChallengeHandler,
CompositeChallengeHandler}.cs`, `Http/Common/HttpHeaderName.cs`, `PublicAPI.Unshipped.txt`, `tests/vectors/auth/
challenges.json`, and the tests below.

### Task 3.1 — Vectors and fixtures (`AUTH-12`, `AUTH-13`; P6c-14, P6c-15, R7)

Create `tests/vectors/auth/challenges.json` (copied to the test output by the existing `..\vectors\**\*.json` glob; check
`Dexpace.Sdk.Core.Tests.csproj`), an array of `{ "name", "input", "expected": [ { "scheme", "parameters": {…},
"token68": null|"…" } ], "source", "note" }`. Port every case of `ruby-sdk/gems/dexpace-core/test/dexpace/auth/
challenge_test.rb` and `challenges_test.rb` and `test/support/challenge_fixtures.rb`'s eight strings, and the cases of
`nodejs-sdk@54aeed4 packages/core/src/auth/challenge.test.ts` Ruby lacks, each with `source` naming the file and case. Rows
that must exist (names are the vector `name`s): `single_bearer_realm`; `multiple_challenges_one_field`; `basic_charset_utf8`;
`quoted_comma_in_value`; `quoted_equals_in_value`; `escaped_quote_and_backslash`; `scheme_and_param_names_fold_to_lower`;
`values_are_verbatim_case`; `bare_scheme`; `token68_after_scheme`; `token68_with_trailing_equals`; `digest_full_challenge`
(realm, nonce, qop, algorithm, opaque, charset); `bearer_error_params`; `duplicate_parameter_first_wins` (P6c-15; note
cites it); `blank_input`; `whitespace_only_input`; `malformed_parameter_skips_to_next_comma_keeps_earlier_params`;
`malformed_scheme_skips_to_next_challenge`; `unterminated_quote_ends_at_end_of_input`; `trailing_backslash_dropped`;
`parameter_without_equals_abandons_element`; `value_neither_token_nor_quoted`; `commas_only`; `lookahead_scheme_vs_param`
(`Basic realm="a", Digest realm="b"` is two challenges; `Digest realm="b", nonce="n"` is one). A row where a sibling
differs from a ruling (Node duplicate-last-wins) records the .NET expectation and a `note` naming the ruling.

Create `tests/Dexpace.Sdk.Core.Tests/Auth/ChallengeFixtures.cs` (R7): `internal static class ChallengeFixtures` with the eight
Ruby strings as `const string`. **Verify:** the file loads through `VectorFile.Load<ChallengeCase>("auth/challenges.json")`
in task 3.3's first test.

### Task 3.2 — The three header names

**Test first** in `tests/Dexpace.Sdk.Core.Tests/Http/Common/HttpHeaderNameTests.cs` (read for its name): `WellKnown_has_WwwAuthenticate_
ProxyAuthenticate_and_ProxyAuthorization` (original spelling `WWW-Authenticate`, `Proxy-Authenticate`, `Proxy-Authorization`;
case-insensitive equality with `HttpHeaderName.Of("www-authenticate")`). **Production:** add the three to
`HttpHeaderName.WellKnown`, alphabetical position as the file orders them. If 6b has already added
`ProxyAuthorization`, re-derive and add only the missing ones (convention 10). **PublicAPI:** three lines. **Verify:** V-fast
`HttpHeaderNameTests`.

### Task 3.3 — Failing tests: the parser (`AUTH-12`, `AUTH-13`; P6c-15)

New `tests/Dexpace.Sdk.Core.Tests/Auth/AuthenticationChallengeTests.cs`:

- `Parse_matches_every_vector` (`[Theory]` over `challenges.json`; each row asserts scheme, parameter dictionary content
  (keys lower-case), `Token68`, and the challenge count; for `Parse(string?)` **and** `Parse(ReadOnlySpan<char>)`)
- `Scheme_and_parameter_names_are_lower_cased_with_ASCII_folding_only` (a Turkish dotted `İ` scheme is not folded to `i̇`
  by the current culture; run under `CultureInfo.InvariantCulture` and `tr-TR`)
- `Values_are_never_folded`
- `The_constructor_folds_parameter_names_and_copies_them` (`new AuthenticationChallenge("Bearer", {["Realm"] = "x"})` →
  scheme `bearer`, key `realm`; mutating the source later changes nothing; a blank scheme throws `ArgumentException`)
- `Token68_is_Parameters_token68` (`Token68Key` const equals `"token68"`)
- `Parse_never_throws_for_arbitrary_text` (property-style: 5 000 inputs from a seeded `Random(6)` over the alphabet
  `Bearer Basic realm="\,=;\t ` plus control and non-ASCII characters; never throws; output challenges all have non-empty
  lower-case schemes and `Parameters` keys non-empty)
- `Parse_is_linear_in_the_input` (a 1 MiB string of `a=b,` repeated parses under a generous bound asserted by counting
  steps through an internal counter, not wall time; the parser exposes no counter, so the test instead asserts a 4× longer
  input allocates at most 5× the bytes via `GC.GetAllocatedBytesForCurrentThread`)
- `A_quoted_comma_is_not_a_challenge_separator` / `An_escaped_quote_does_not_end_the_value` (pins, from vector rows, kept
  as named tests so a failure names the rule)
- `Each_header_field_is_parsed_on_its_own` (P6c-14; lives with the step in task 4.2, listed here for traceability only)

Red: the type does not exist. **IDs:** `AUTH-12`, `AUTH-13`. **Verify:** V-fast `AuthenticationChallengeTests` (red).

### Task 3.4 — `AuthenticationChallenge` and the parser (`AUTH-12`, `AUTH-13`; P6c-15)

**Production.**

- `Auth/AuthenticationChallenge.cs`: `public sealed class AuthenticationChallenge` as the design's block. The constructor
  folds names with an internal ASCII lower-casing (`ChallengeParser.FoldAscii`), copies into an `ImmutableDictionary<string,
  string>` (ordinal), keeps **first** value on a folded duplicate, rejects a blank scheme. `Parse(string?)` forwards to
  `Parse(ReadOnlySpan<char>)`. `ToString()` prints scheme and parameter *names* only (values may carry nonces).
- `Auth/ChallengeParser.cs` (`internal static class`): one entry `Parse(ReadOnlySpan<char>) -> List<AuthenticationChallenge>`
  and a `ref struct Reader` holding the span and position. Helper split to hold `MA0051`'s 70 lines (each ≤ 40):
  `SkipWhitespace`, `TryReadToken(out ReadOnlySpan<char>)` (token = `1*tchar`), `ReadQuoted(StringBuilder)` (handles `\x`,
  an unterminated quote ends at end of input with content kept, a trailing backslash is dropped), `SkipToTopLevelComma`
  (respects quotes), `ReadChallenge`, `TryReadParameter(out name, out value)`, `LooksLikeParameter()` (the one-token
  lookahead of design C: token, `BWS`, `=`, then a non-`=` character, restoring position), `TryReadToken68`. Loop: skip
  whitespace and commas; read a token as scheme (failure → skip to next top-level comma); skip `1*SP`; if the next is a
  token68 candidate (token68 chars then `=*` then comma/end) take it as `token68`; else read parameters separated by commas
  while `LooksLikeParameter()`; on a malformed element keep what was parsed and `SkipToTopLevelComma`. Never recurse; one
  pass; names folded as parsed; duplicate name keeps the first.
- Docs state the grammar handled, the leniency, and the first-wins rule (P6c-15).

No `Regex`, no `string.Split`, no exceptions for control flow. **PublicAPI:** `AuthenticationChallenge` lines. **IDs:**
`AUTH-12`, `AUTH-13`. **Verify:** V-fast `AuthenticationChallengeTests` (green).

### Task 3.5 — `IChallengeHandler`, `BasicChallengeHandler`, `CompositeChallengeHandler` (`AUTH-14`, `AUTH-23`, `AUTH-25`; P6c-16, R8)

**Tests first.** New `tests/Dexpace.Sdk.Core.Tests/Auth/BasicChallengeHandlerTests.cs`:
`Answers_a_basic_challenge_with_any_realm_by_setting_Authorization`; `Value_is_Basic_plus_base64_of_UTF8_user_colon_password`
(`("Aladdin", "open sesame")` → `Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==`; a non-ASCII password round-trips UTF-8; an empty-looking
`" "` password is accepted); `Returns_null_when_no_basic_challenge_is_present`; `Sets_Proxy_Authorization_when_proxy_is_true`
(`AUTH-25`; `Authorization` untouched); `Replaces_an_existing_header_rather_than_appending`; `Returns_a_new_request_and_leaves_the_input_untouched`
(R8); `The_value_equals_the_one_BasicAuthPolicy_stamps_preemptively` (`AUTH-14`, one computation); `Rejects_null_arguments`.

New `CompositeChallengeHandlerTests`: `The_first_non_null_answer_wins`; `Order_is_the_constructor_order` (stronger first);
`Returns_null_when_every_handler_declines`; `Copies_its_handlers` (mutating the source array afterwards changes nothing);
`An_empty_list_or_a_null_element_throws_ArgumentException`; `Passes_the_proxy_flag_through`; `A_handler_after_the_winner_is_not_called`
(a counting handler); `Is_stateless_and_safe_under_parallel_use` (`AUTH-24`'s composite half; 32 parallel calls).

New `ChallengeHandlerContractTests` (a `[Theory]` over the two shipped handlers, Digest added in task 6.4): the return is
either `null` or a request whose only changed header is the one the `proxy` flag selects (`AUTH-25`).

**Production.** `IChallengeHandler.cs`, `BasicChallengeHandler.cs` (reads `credential.HeaderValue` once at construction),
`CompositeChallengeHandler.cs` (`ImmutableArray<IChallengeHandler>`). Docs on `IChallengeHandler`: "`null` means the
handler cannot answer; the result is exactly what a policy's hook returns (`AUTH-30`)" and the `proxy` semantics (P6c-13).
**PublicAPI:** the three types. **IDs:** `AUTH-14`, `AUTH-23`, `AUTH-25`. **Verify:** V-fast the three new classes.

### Task 3.6 — Close-out (PR 3)

`CHANGELOG.md` `### Added`: the challenge model and parser, `IChallengeHandler`, `BasicChallengeHandler`,
`CompositeChallengeHandler`, the three well-known header names. Run **V-gate** (Security diff: empty). **Commit:**
`feat: phase 6c challenge parser and Basic and composite handlers (AUTH-12, AUTH-13, AUTH-23, AUTH-25)`.

---

## PR 4 — The `401` lifecycle in the authorization base

**Gate: PRs 1, 2, 3 merged.** Rows: `AUTH-26` (policy), `AUTH-27`–`AUTH-33`, `AUTH-38`, and the descriptor wiring of `AUTH-4`
through the policies. Files: `src/Dexpace.Sdk.Core/Pipeline/Policies/{AuthorizationPolicy,AuthChallengeContext,
ApiKeyAuthPolicy,BasicAuthPolicy,BearerTokenAuthPolicy,ChallengeAuthPolicy}.cs`, `Internal/AuthOrigin.cs`, `Errors/
HttpsRequiredException.cs`, `PublicAPI.Unshipped.txt`, and the tests below.

### Task 4.1 — `AuthOrigin` and `HttpsRequiredException` (`AUTH-28`, `AUTH-29`; P6c-8, P6c-41)

**Tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/AuthOriginTests.cs`: `Same_scheme_host_and_port_are_the_same_origin`;
`Default_ports_are_equal_to_explicit_ones` (`https://a/` vs `https://a:443/`); `Host_and_scheme_compare_case_insensitively`;
`A_different_port_is_a_different_origin`. New `tests/Dexpace.Sdk.Core.Tests/Errors/HttpsRequiredExceptionTests.cs`:
`Carries_policy_name_and_scheme`; `Is_an_SdkException_but_not_a_ServiceRequestException` (non-retryable, P6c-41; also asserts it
implements no retry marker interface found by `grep -rn "interface IRetry" src`); `The_message_is_the_as_built_text`
(`"BasicAuthPolicy refused to attach a credential to a request over the 'http' scheme; credentials are only sent over https."`).
Add the same non-retryable assertion for `AuthResolutionException` (task 1.2) and `TokenProviderException` (task 5.1) in one
`AuthExceptionsAreNotRetryableTests` once the third exists (task 5.1 extends it).

**Production.** Move the as-built private `GetOrigin` into `Internal/AuthOrigin.cs` as `internal static string Of(Uri)` and
`internal static bool Same(Uri a, Uri b)` (the formula is unchanged); `Errors/HttpsRequiredException.cs`:
`public sealed class HttpsRequiredException : SdkException` with `PolicyName`, `Scheme` and the constructor
`(string policyName, string scheme)`. **PublicAPI:** the exception. **IDs:** `AUTH-28`, `AUTH-29`. **Verify:** V-fast the two
classes.

### Task 4.2 — Failing tests: the lifecycle (`AUTH-27`–`AUTH-33`, `AUTH-38`; P6c-9 to P6c-12)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/AuthorizationPolicyChallengeTests.cs`, driven by a private
`ProbeAuthPolicy : AuthorizationPolicy` (constructor `(AuthDescriptor, IEnumerable<AuthScheme>)`, an `Action`/`Func` per
virtual, counters for `GetCredential*` and `OnChallenge*` calls) over `ScriptedTransport` and `TestResponses`. Each fact below
is one test; the sync/async pairs are `[Theory]`s over `bool async` (Ruby's pairs, design "Tests"): the sync arm calls
`pipeline.Send`, the async arm `SendAsync`.

- Stage and order (`AUTH-27`): `Stage_is_Auth_and_sealed` (reflection: `Stage` getter `IsFinal`); `The_stage_order_is_Redirect_
  then_Retry_then_Auth` (a pin over `PipelineStage` values 200 < 300 < 500; proven able to fail by swapping two in a scratch
  copy); `A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder` (`PIPE-5` pin, using the builder's existing error).
- Resolution wiring (`AUTH-4`–`AUTH-6`): `A_per_call_NoAuth_sends_anonymously_and_returns_a_401_unchanged` (no stamp, no guard, hook
  not called, `GetCredential*` count 0); `A_per_call_scheme_the_policy_cannot_serve_throws_AuthResolutionException_before_sending`
  (transport count 0); `The_operation_tier_is_used_when_no_per_call_tier_is_set`.
- Cross-origin (`AUTH-29`): `A_cross_origin_hop_has_its_credential_header_removed_and_is_not_guarded` (seed `https://a`, request
  `http://b`; header stripped; no `HttpsRequiredException`); `A_cross_origin_401_is_returned_without_challenge_handling` (the
  hook count is 0, P6c-12, whatever `WWW-Authenticate` says); `No_marker_property_is_set_on_the_context` (the context's
  property bag stays empty after the call).
- Guard (`AUTH-28`): `A_non_https_url_throws_HttpsRequiredException_before_any_credential_is_resolved` (`GetCredential*` count
  0, transport 0); `The_guard_also_covers_a_reactive_schemes_outbound_pass` (a probe whose `GetCredential` returns `null`;
  still refused: P6c-8); `Case_insensitive_scheme_compare` (`HTTPS` passes).
- Stamp: `A_non_null_credential_replaces_an_existing_header_value`; `A_null_credential_stamps_nothing`.
- Hook, `401` (`AUTH-30`): `A_401_with_WWW_Authenticate_calls_the_hook_with_the_parsed_challenges` (the context's
  `Challenges` equals the parse of the field; `Requirement`, `Request` (as stamped) and `Response` set);
  `A_non_null_replacement_is_driven_once_through_the_continuation` (second transport call carries the replacement; the third
  never happens even if the replay also returns `401`: `No_further_challenge_handling_after_the_replay`);
  `The_default_hook_yields_null_and_the_401_is_returned`; `The_401_is_disposed_before_the_replay` (a `TrackingResponseBody`
  on the first response, `DisposeCount == 1` before the transport sees the second request: record order in a shared log);
  `The_returned_response_is_the_replays` ; `Each_WWW_Authenticate_field_is_parsed_on_its_own` (P6c-14: two fields, the first
  with an unterminated quote; the second's `Bearer` challenge is still seen).
- `AUTH-33`: `A_401_without_WWW_Authenticate_is_returned_unchanged_and_the_hook_is_not_called`; `A_non_401_with_the_header_is_
  returned_unchanged` (e.g. 403 with `WWW-Authenticate`); `A_200_is_untouched`.
- `AUTH-32`: `A_hook_that_throws_disposes_the_401_and_propagates_the_exception` (three arms: sync `OnChallenge` throw, async
  `OnChallengeAsync` throw, a *synchronous* throw from a non-`async` `OnChallengeAsync` override; the original exception type
  surfaces, `DisposeCount == 1`, and a failing dispose is suppressed onto the trail with the hook exception as primary
  (`ExceptionTrail` / `Suppressed`, as 4b's disposal tests assert)).
- `AUTH-31`: `A_replacement_with_a_non_replayable_body_is_not_sent_and_the_original_401_is_returned_undisposed` (a
  `RequestBody.FromStream` over a non-seekable stream; transport count 1; `DisposeCount == 0` on the returned response; both
  paths); `A_replacement_with_a_replayable_body_is_sent`; `A_replacement_without_a_body_is_sent`; `The_gate_applies_to_every_replacement_regardless_of_which_hook_built_it` (a second probe policy).
- Replacement checks (P6c-10): `A_cross_origin_replacement_throws_InvalidOperationException_after_disposing_the_401`.
- `AUTH-38`: `ProcessAsync_returns_a_faulted_task_instead_of_throwing_for_an_http_url` (call `policy.ProcessAsync(...)` directly
  on a probe; no exception at the call, `IsFaulted` after completion, exception is `HttpsRequiredException`; also for a
  resolver failure and for a `GetCredentialAsync` that throws synchronously); `Process_throws_the_original_exception_on_the_sync_path`.
- Sync/async parity: `The_sync_and_async_paths_produce_identical_requests` (record the request sequence under both).

Red: the base does not have the new members (compile error, expected). **IDs:** `AUTH-27`–`AUTH-33`, `AUTH-38`. **Verify:**
V-fast `AuthorizationPolicyChallengeTests` (red).

### Task 4.3 — The base class (`AUTH-27`–`AUTH-33`, `AUTH-38`; P6c-8 to P6c-12) — Breaking 6, 7, 8

**Production.** Rewrite `Pipeline/Policies/AuthorizationPolicy.cs` to the design's section E surface and the nine-step body.

- Members: the protected constructor `(AuthDescriptor clientDescriptor, IEnumerable<AuthScheme> availableSchemes)` (null
  checks, copy to `ImmutableArray<AuthScheme>`), `WithheldHeaderNames` (abstract), the two abstract `GetCredential*` taking
  `(AuthRequirement, Request, PipelineContext)` and returning `(string, string)?` / `ValueTask<(string, string)?>`, the two
  virtual hooks returning `null`. `Stage`, `ProcessAsync` and `Process` stay sealed. `AuthChallengeContext` (R11) in its own
  file: `sealed class`, internal constructor, the five properties.
- Body: keep `ProcessCoreAsync(request, context, continuation, bool async)` for the `async` idiom (§5.3) and split it into
  helpers so no method exceeds 70 lines: `DriveWithoutCredentialAsync` (steps 1/3), `GuardAndStampAsync` (steps 4/5),
  `HandleChallengeAsync` (steps 7–9), `TryGetReplacementAsync`, `ReadChallenges(Response)` (R5). Order exactly as design E:
  cross-origin first (`AuthOrigin.Same(context.SeedRequest.Url, request.Url)`; strip all `WithheldHeaderNames`; drive; no
  challenge handling); `AuthResolver.Resolve(context.RequestOptions.Auth, context.RequestOptions.OperationAuth, clientDescriptor,
  availableSchemes)`; `NoAuth` → drive and return; `EnsureHttps` → `HttpsRequiredException`; stamp; drive; on `Status.Code
  == 401` with at least one `WWW-Authenticate` field parse each field and call the hook; hook failure → `Disposal.DisposeQuietly(
  response, primary: ex)` then rethrow with `ExceptionDispatchInfo` so the stack is kept; `null` replacement → return the
  `401`; replacement checks (same origin else dispose + `InvalidOperationException`; `Body is { IsReplayable: false }` → return the
  `401` undisposed); dispose the `401`; drive the replacement once through the same `continuation`; return.
- Sync path: the identical body with `async: false` and `continuation.Run`, `GetCredential`, `OnChallenge`; `Process` keeps
  `SyncPath.GetCompletedResult` (PIPE-28). The `RS0030` pragma and the bridge `GetCredential` are **deleted** (P4c-13 closed).
- Docs: remarks rewritten (HTTPS paragraph names `HttpsRequiredException`; lifecycle paragraph lists the order; **Breaking**
  paragraph for the protected surface and the exception type; the `Remarks` of `GetCredential*` say a `null` result means
  "nothing to attach on this pass").

**PublicAPI:** the design's removed and added `AuthorizationPolicy`/`AuthChallengeContext` lines. **IDs:** `AUTH-27`–`AUTH-33`,
`AUTH-38`, `AUTH-4`–`AUTH-6` (wiring). **Verify:** V-fast `AuthorizationPolicyChallengeTests`; the project will not compile
until task 4.4 migrates the three policies, so 4.3 and 4.4 are developed together and green together (commit once).

### Task 4.4 — The three single-scheme policies and `ChallengeAuthPolicy` (`AUTH-26`, `AUTH-14`, `AUTH-4`; P6c-7, P6c-9, P6c-36, R3)

**Production.**

- `ApiKeyAuthPolicy`: `base(new AuthDescriptor(new AuthRequirement(AuthScheme.ApiKey)), [AuthScheme.ApiKey])`;
  `WithheldHeaderNames => [_credential.HeaderName]`; `GetCredential*` return `(_credential.HeaderName.Original,
  _credential.HeaderValue)` (the value computed once at construction, task 2.3). `GetCredentialAsync` wraps `GetCredential`.
- `BasicAuthPolicy`: descriptor `[Basic]`; withheld `[Authorization]`; value `credential.HeaderValue` (`AUTH-14`: one
  computation shared with the handler).
- `BearerTokenAuthPolicy`: descriptor `[OAuth2 { Scopes = scopes }]`; withheld `[Authorization]`; `GetCredentialAsync` builds the
  `TokenRequestContext` from the **resolved requirement's** scopes (falling back to the policy's when the requirement has
  none: design F) and awaits the cache; **R3:** `GetCredential` is the as-built blocking bridge with one `RS0030` pragma
  and the comment "removed by task 5.5 (P6c-26)". The remarks' "401 re-acquisition is deferred" paragraph is rewritten in
  task 5.5, not here.
- `ChallengeAuthPolicy` (new, sealed): constructor `(IChallengeHandler handler)`; descriptor `[Digest, Basic]`, available
  `{Digest, Basic}`; withheld `[Authorization]`; `GetCredential*` return `null` (nothing preemptive); `OnChallenge*` call
  `handler.Authorize(context.Challenges, context.Request, proxy: false)`. Docs: "`new ChallengeAuthPolicy(new
  DigestChallengeHandler(credential))` is issue #2's shape".

**Tests.** Extend `ApiKeyAuthPolicyTests` (`The_header_value_is_stamped_from_the_credential_computed_once`, `A_custom_header_is_
withheld_cross_origin`), `BasicAuthPolicyTests` (`Preemptive_Basic_is_stamped_on_the_first_request`), `BearerTokenAuthPolicyTests`
(per-call OAuth2 scopes fetch a token for those scopes: a credential recording the `TokenRequestContext` it was asked
for), and new `ChallengeAuthPolicyTests`: `Nothing_is_stamped_on_the_first_request`; `A_basic_401_is_answered_once_via_the_handler`
(`ScriptedTransport` script: `401 WWW-Authenticate: Basic realm="r"` then `200`; the second request carries the expected
`Authorization`; exactly two calls); `A_401_the_handler_cannot_answer_is_returned`; `A_still_401_after_the_replay_is_returned_
without_a_third_call`; `A_composite_falls_back_from_a_declined_handler`; `Both_paths_behave_alike` (sync and async); `The_guard_
refuses_http_before_the_first_request` (P6c-8). **IDs:** `AUTH-26`, `AUTH-14`, `AUTH-30`. **Verify:** V-fast the four policy
test classes, then the whole core test project.

### Task 4.5 — Edit `AuthHttpsGuardTests`, strengthened (`AUTH-28`; P6c-8, fact 16)

This is the one `Security` class 6c edits. Read it in full first; the allowed diff is **exactly**:

1. `CountingAuthPolicy` is re-signatured to the new base (the `(AuthDescriptor, IEnumerable<AuthScheme>)` constructor
   call, `WithheldHeaderNames`, the requirement-taking `GetCredential(Async)`), still counting calls and still returning a
   fixed header.
2. The first case's `Assert.Equal(typeof(SdkException), ex.GetType())` becomes `Assert.Equal(typeof(HttpsRequiredException),
   ex.GetType())`, and `ThrowsAsync<SdkException>` becomes `ThrowsAsync<HttpsRequiredException>` in that case and the
   others; it gains `Assert.Equal("http", ex.Scheme)`.
3. Nothing else: no assertion removed, no data row removed, no exception type widened.

Run this class first (convention 5 / Security section). **Verify:** V-fast `AuthHttpsGuardTests`; then `git diff
tests/Dexpace.Sdk.Core.Tests/Security/AuthHttpsGuardTests.cs` read line by line against the list.

### Task 4.6 — Close-out (PR 4)

`CHANGELOG.md` `### Changed`: breaking items 6 (the base's protected surface), 7 (typed guard), 8 (every policy honours the
tiers; a `401` is answered per its hook); `### Added`: `ChallengeAuthPolicy`, `AuthChallengeContext`, `HttpsRequiredException`.
Update `BearerTokenAuthPolicy`/`RedirectPolicy` remarks only where they cite `WithheldHeaderName` (grep). Run **V-gate**, the
coverage gate with its self-test, and confirm the Security diff is `AuthHttpsGuardTests.cs` plus (from PR 2)
`CredentialRedactionTests.cs` only. Confirm `ReDriveRequestIsolationTests`, `AsyncErrorModelTests`, `RedirectCredentialHygieneTests` and `RedirectPolicyTests` are green unedited
(6b's end-to-end test depends on the shipped policies). **Commit:** `feat!: phase 6c 401 lifecycle in the authorization base
(AUTH-27..AUTH-33)`.

---

## PR 5 — The token cache, the background refresh and the bearer hook

**Gate: PR 4 merged.** Rows: `AUTH-11`, `AUTH-34`–`AUTH-37`, `XCUT-14` and `XCUT-12` (cited). **Closes #7** (with PRs 3 and 4).
Files: `src/Dexpace.Sdk.Core/Auth/AccessTokenCache.cs`, `Errors/TokenProviderException.cs`, `Internal/{BackgroundWork,AuthLog,
BoundedMapExtensions}.cs`, `Diagnostics/DexpaceLogEvents.cs`, `Pipeline/Policies/BearerTokenAuthPolicy.cs`,
`BannedSymbols.txt` (comment line), `PublicAPI.Unshipped.txt`, and the tests below.

### Task 5.1 — `TokenProviderException`, the event, `AuthLog` and `BackgroundWork` (`AUTH-35`, `AUTH-37`; P6c-23, P6c-25, P6c-37, R9)

**Tests first.**

- `tests/Dexpace.Sdk.Core.Tests/Errors/TokenProviderExceptionTests.cs`: `Is_an_SdkException_and_not_retryable`;
  `The_message_never_contains_a_token`; extend `AuthExceptionsAreNotRetryableTests` (task 4.1) with it.
- `tests/Dexpace.Sdk.Core.Tests/Diagnostics/AuthLogTests.cs`: `TokenRefreshFailed_is_id_160_named_dexpace_auth_token_refresh_failed_at_Warning`
  (a `RecordingLogger` from `TestSupport`; asserts id, name, level, the `error.type` field and the attached exception);
  `A_throwing_logger_does_not_throw` (`ThrowingLogger`; OBS-20's rule, as `ProxyResolutionLog` does).
- `tests/Dexpace.Sdk.Core.Tests/Internal/BackgroundWorkTests.cs`: `The_work_observes_no_Activity_current` (fact 8; start an
  activity with a listener, call `BackgroundWork.Run`, the delegate records `Activity.Current is null`);
  `The_caller_flow_is_restored_after_Run_returns` (the caller's `AsyncLocal` is still visible afterwards);
  `A_throwing_delegate_faults_the_returned_task_not_the_caller`; `Run_returns_a_task_the_caller_can_await`.
- A source-scan test in `tests/Dexpace.Sdk.Core.Tests/Architecture/BannedSymbolsSiteTests.cs`:
  `SuppressFlow_appears_only_in_BackgroundWork` (reads the `src/` tree text, asserting the token `SuppressFlow` occurs in
  exactly one file; the scanner self-test proves it can fail).

**Production.**

- `Errors/TokenProviderException.cs`: `public sealed class TokenProviderException : SdkException` with `(string message)`;
  docs "the provider returned a default or already-expired token; never contains the token".
- `Diagnostics/DexpaceLogEvents.cs` (R9): `TokenRefreshFailed`, `TokenRefreshFailedId = 160` with docs. **PublicAPI:** two
  `const` lines.
- `Internal/AuthLog.cs`: written like `ProxyResolutionLog` — `LoggerMessage.Define<string>(LogLevel.Warning, new EventId(DexpaceLogEvents.TokenRefreshFailedId,
  DexpaceLogEvents.TokenRefreshFailed), "Background token refresh failed with {error.type}.")` (the placeholder names the structured
  field, as 5b's templates do, e.g. `HttpLogEmitter`'s `{error.type}`) inside a non-fatal `try`;
  `internal static void TokenRefreshFailed(ILogger logger, Exception ex)`. Honour 5b's emission-guard pattern (read
  `HttpLogEmitter`'s guard helper and reuse it if it is internal and fits; otherwise the same wrapper `ProxyResolutionLog`
  uses). The `error.type` value comes from `HttpSemanticConventions.ErrorType(exception)`, as 5b and 5c do, never the exception's message.
- `Internal/BackgroundWork.cs`: `internal static Task Run(Func<Task> work)`: the one site of `ExecutionContext.SuppressFlow()`
  (a `using` around `Task.Run`), under a documented `#pragma warning disable RS0030`, with remarks citing design §5.4/§8.1 and
  `CTX-19`. `BannedSymbols.txt`: the comment line "which does not exist yet" becomes "`Internal/BackgroundWork.cs`"; no
  banned entry removed (a diff check: `git diff BannedSymbols.txt` shows only comment lines).

**IDs:** `AUTH-37` (infrastructure), `AUTH-35`. **Verify:** V-fast the four new classes.

### Task 5.2 — Failing tests: the cache (`AUTH-11`, `AUTH-34`–`AUTH-37`, `XCUT-14`; P6c-23, P6c-24, P6c-28)

Rewrite `tests/Dexpace.Sdk.Core.Tests/Auth/AccessTokenCacheTests.cs` around a `GatedCredential : TokenCredential` (a
`TaskCompletionSource` gate per call, a call counter, an injectable failure, records the `TokenRequestContext`) and
`FakeTimeProvider`; no sleeps. Cases (the Ruby `bearer_stamper_test`, `async_bearer_stamper_test`, `bearer_provider_test` and
Node `bearer-cache.test.ts` ports are each named in a comment):

- Zones (`AUTH-34`, `AUTH-37`): `A_fresh_token_is_returned_without_calling_the_provider_again`; `The_hot_path_takes_no_lock` (a
  gated provider holds a refresh; a second `GetAsync` on a *fresh* key returns synchronously — `IsCompletedSuccessfully`);
  `A_token_inside_the_margin_is_stamped_now_and_refreshed_in_the_background` (the call returns the old token at once, one
  provider call is observed to start, the gate then completes and the next call returns the new token);
  `The_default_margin_is_30_seconds_and_configurable`; `A_negative_margin_throws`; `RefreshOn_before_ExpiresOn_minus_margin_wins`
  (refresh at the earlier of the two, P6c-23); `A_token_at_exactly_its_expiry_is_still_valid_but_expiring` (`now == ExpiresOn`);
  `An_expired_token_is_fetched_in_the_foreground`; `A_token_without_expiry_is_never_refreshed`.
- Coalescing (`AUTH-34`, `AUTH-37`, P6c-24): `N_concurrent_requests_at_expiry_cost_one_fetch` (N = 16; gate; one provider call;
  all 16 get the same token); `With_a_failing_provider_N_waiters_fail_together_after_one_attempt`; `A_request_arriving_after_a_
  failure_fetches_again` (no failure is cached: `AUTH-35`); `A_waiters_own_cancellation_leaves_the_fetch_running`;
  `A_fetchers_own_cancellation_is_not_adopted_by_the_waiters` (the next waiter fetches); `Different_keys_do_not_block_each_other`
  (`XCUT-12`: key A gated, key B completes); `A_per_key_semaphore_not_a_global_one`.
- Background refresh (`AUTH-37`): `Only_one_background_refresh_runs_per_key_however_many_requests_arrive`; `The_background_
  refresh_sees_no_ambient_Activity`; `A_failing_background_refresh_logs_event_160_once_and_the_triggering_call_is_unaffected`
  (via `RecordingLogger` on the internal logger-taking overload); `A_background_failure_leaves_the_old_token_valid_until_it_
  expires`; `After_a_background_failure_the_next_request_inside_the_margin_starts_another_refresh`; `The_refresh_task_is_
  observed_not_fire_and_forget` (the entry exposes the task to an internal test hook, `entry.RefreshTask`, and it completes
  faulted-free).
- Validation (`AUTH-35`): `A_default_token_from_the_provider_throws_TokenProviderException`; `A_token_already_expired_at_fetch_
  throws_TokenProviderException`; `A_provider_exception_propagates_unwrapped_and_nothing_is_cached` (`AUTH-11`, `AUTH-35`; a
  sync throw from a non-`async` `GetTokenAsync` override observed through the returned task: fact 14; and through `Get`);
  `The_exception_message_never_contains_the_token`.
- Sync path (P6c-26): `Get_returns_the_same_tokens_as_GetAsync`; `Get_honours_cancellation_while_waiting`; `Get_uses_TokenCredential_
  GetToken_not_GetTokenAsync` (a credential overriding `GetToken` only counts its calls; `GetTokenAsync` throws if called);
  `A_sync_and_an_async_waiter_coalesce_onto_one_fetch`.
- Bound (`XCUT-14`, P6c-28): `The_cache_holds_at_most_1024_keys_once_quiescent` (insert 3 000 distinct claims keys, drain via
  the map's loop with an internal test hook `entry count <= 1024 + slack`; assert against `BoundedMap.Capacity` rather than a
  literal); `An_evicted_live_entry_costs_one_extra_fetch_and_no_failure`; `The_cache_key_includes_claims`.
- Rejection path (`AUTH-36`, `AUTH-37`; internal `GetAfterRejectionAsync(context, rejectedHeaderValue, ct)`):
  `A_rejection_of_the_current_token_evicts_it_and_fetches_fresh` (provider call count +1; returns the new token);
  `A_rejection_of_a_stale_header_keeps_a_token_another_request_already_refreshed` (cached `Bearer new`; rejected `Bearer old` →
  no eviction, returns `new`, no provider call); `Eviction_and_fetch_happen_under_one_critical_section` (two concurrent
  rejections of the same header → one provider call); `A_rejection_returns_a_genuinely_fresh_token_not_the_background_
  refresh_in_flight` (post-eviction path).

Red: the cache has none of this. **IDs:** `AUTH-11`, `AUTH-34`–`AUTH-37`, `XCUT-12`, `XCUT-14`. **Verify:** V-fast
`AccessTokenCacheTests` (red).

### Task 5.3 — The cache (`AUTH-11`, `AUTH-34`–`AUTH-37`; P6c-23 to P6c-28, R1, R2)

**Production.** Rewrite `Auth/AccessTokenCache.cs` to the design's surface (section F) and add `Internal/
BoundedMapExtensions.cs` (R1, with a unit test `BoundedMapExtensionsTests.GetOrAdd_returns_the_existing_value_and_creates_one_
under_a_race`).

- Surface (R15): `DefaultRefreshMargin` (30 s), the one constructor `(TokenCredential credential, TimeProvider? timeProvider = null)`,
  `RefreshMargin { get; init; }` (default `DefaultRefreshMargin`; a negative value is `ArgumentOutOfRangeException`), `GetAsync`, `Get`,
  plus `internal` overloads
  `GetAsync(TokenRequestContext, ILogger, CancellationToken)` / `Get(…, ILogger, …)` (the public ones pass `NullLogger.Instance`)
  and `internal GetAfterRejectionAsync` / `GetAfterRejection`. Entries: `BoundedMap<string, CacheEntry>` capacity 1024.
  `AuthCredentials` (task 2.4) switches to `AccessTokenCache.DefaultRefreshMargin`.
- `CacheEntry` (sealed): `volatile TokenHolder? Holder`, `SemaphoreSlim Gate = new(1, 1)`, `int Generation`, a
  `FetchOutcome? Last` (generation, `AccessToken?`, `ExceptionDispatchInfo?`, `bool Canceled`), and `Task? RefreshTask`.
- Hot path (no lock): read `Holder` once; if not expiring/expired by `now`, return. Expiring zone: stamp `t`, try
  `Gate.Wait(0)`; if acquired, `RefreshTask = BackgroundWork.Run(() => RefreshInBackgroundAsync(entry, context, logger))`
  (the delegate always calls `GetTokenAsync` with `CancellationToken.None`, validates the fetched token as in `AUTH-35`,
  publishes the holder, advances `Generation`, releases the gate in `finally`, and on a non-fatal failure writes
  `AuthLog.TokenRefreshFailed` and nothing else — `ExceptionFacts.IsFatal` guards the catch). Expired/missing zone: one shared
  private `FetchAsync(entry, context, ct, bool async, ILogger)` records `Generation` before waiting, `Gate.WaitAsync(ct)` or
  `Gate.Wait(ct)`, and after acquiring: if `Generation` moved, adopt `Last` (rethrow with `ExceptionDispatchInfo` unless
  `Canceled`); else fetch (`GetTokenAsync(...).ConfigureAwait(false)` or `GetToken`), validate (`default` token or
  `IsExpired(now, TimeSpan.Zero)` → `TokenProviderException`), publish, record outcome, advance `Generation`, release in
  `finally`. The sync method calls the same method with `async: false` and `SyncPath.GetCompletedResult` (PIPE-28).
  Helper split: `Classify(token, now)` → fresh/expiring/expired; `ValidateFetched(token, now)`; `Record(entry, outcome)`;
  `RefreshInBackgroundAsync`; `FetchAsync` — each under 70 lines.
- `GetAfterRejection(Async)`: take the gate, clear `Holder` only if `"Bearer " + holder.Token.Token` ordinal-equals the
  rejected header (`AUTH-36`), then if `Holder` is non-null (another request replaced it) return it, else fetch (post-
  eviction fetch is a genuinely fresh one: `AUTH-37`). Never adopts a previous outcome.
- No `Task.Delay`, no `DateTime.*` (the clock bans); time from `_time.GetUtcNow()` only.
- The parameter name stays `ct` on `GetAsync` (as built, matching `TokenCredential.GetTokenAsync`); the new `Get` uses `ct` too. The
  design's `cancellationToken` spelling is not adopted (R15), so no PublicAPI removal line and no named-argument break for `GetAsync`.
- Docs: **Breaking** paragraph (design breaking item 5); a remark that coalesced failures share one exception instance
  (risk 3) and that the background refresh cannot be cancelled (risk 4).

**PublicAPI:** the design's `AccessTokenCache` lines as corrected by R15 (one constructor, `RefreshMargin.get`/`.init`, `Get`; the as-built
`GetAsync` line is unchanged). **IDs:** `AUTH-11`, `AUTH-34`–`AUTH-37`, `XCUT-12`, `XCUT-14`.
**Verify:** V-fast `AccessTokenCacheTests`, `BoundedMapExtensionsTests` (green); then run each concurrency test 200 times in
a loop (`for i in $(seq 200); do dotnet test … --filter-class "*AccessTokenCacheTests"; done`) once, locally, and record the
result in the checklist (flake guard).

### Task 5.4 — Migrate the as-built cache tests (breaking 5)

The tests named in the design's migration table are rewritten with their new expectations, never deleted without a
replacement: `GetAsync_RefreshThrows_WhileStillValid_ReturnsCachedToken` becomes `A_failed_background_refresh_while_valid_stamps_
the_valid_token_and_logs`; `GetAsync_PastRefreshOn_RefreshesOnce` becomes `Past_RefreshOn_the_valid_token_is_stamped_and_one_
background_refresh_runs`. Record both in the "existing assertions changed" table. **Verify:** the core project.

### Task 5.5 — `BearerTokenAuthPolicy`: real sync path and the `401` hook (`AUTH-36`, `AUTH-37`; P6c-26, P6c-27, R3) — Breaking 8

**Tests first** in `BearerTokenAuthPolicyTests`:

- `Process_sync_stamps_through_the_real_sync_path` (replaces `Process_sync_stamps_through_the_documented_bridge`: a credential
  whose `GetTokenAsync` throws and whose `GetToken` returns a token still stamps on `Send`);
  `The_sync_and_async_paths_share_one_cache`.
- `A_401_with_a_Bearer_challenge_evicts_fetches_fresh_and_retries_once` (script: `401 WWW-Authenticate: Bearer realm="r"` → `200`;
  the second request carries the second token; the credential was asked twice; the retry is a second transport call only);
  `A_401_with_a_non_bearer_challenge_is_returned` (`Basic realm=…` → hook returns `null`, no retry, no eviction);
  `A_401_without_a_stamped_Authorization_header_is_returned` (cross-origin hop is already covered by the base; here a probe
  request that arrived without the header);
  `The_retry_is_method_agnostic` (`POST` with a replayable body retried; with a stream body not retried and the `401`
  returned undisposed: `AUTH-31` through the bearer hook, both paths);
  `A_provider_that_hands_back_the_rejected_token_yields_no_retry` (P6c-27: returns the same token twice → the `401` surfaces,
  transport count 1);
  `A_second_401_after_the_retry_is_returned_without_another_retry` (`AUTH-30`);
  `Per_call_OAuth2_scopes_get_their_own_token_on_retry`;
  `Retry_uses_the_logger_of_the_call` (event 160 from a background refresh in a pipeline with a `RecordingLogger` reaches it);
  `Bearer_hook_works_on_both_paths` (`[Theory]` over `bool async`).

**Production.** Delete `BearerTokenAuthPolicy.GetCredential`'s bridge pragma (R3); `GetCredential` calls
`_cache.Get(ctx, context.State.Logger, context.CancellationToken)` (the policy lives in the same assembly; `CallState.Logger` is
`internal`). Add the second constructor `(AccessTokenCache cache, params string[] scopes)`. `OnChallenge*`: return `null` unless
`context.Request.Headers.Get("Authorization")` is non-null and some challenge's scheme is `bearer`; else call the cache's
`GetAfterRejection(Async)(tokenContext, rejectedHeaderValue)`; build `"Bearer " + token.Token`; if equal to the rejected header
→ `null`; else return `context.Request.WithHeaders(Headers.Set("Authorization", value))`. Rewrite the remarks (the "deferred
to challenge-handling work" paragraph becomes the as-built `401` paragraph; **Breaking** note for retry on `401`).

**PublicAPI:** the `BearerTokenAuthPolicy(AccessTokenCache, params string[])` line. **IDs:** `AUTH-36`, `AUTH-37`, `AUTH-31`
(bearer), `AUTH-30`. **Verify:** V-fast `BearerTokenAuthPolicyTests`, `AccessTokenCacheTests`.

### Task 5.6 — End-to-end bearer pipeline pin (`AUTH-30`, `AUTH-36`)

New `tests/Dexpace.Sdk.Core.Tests/Pipeline/BearerPipelineTests.cs` using `DexpacePipeline.CreateDefault` (read how its
tests build one): `A_challenged_bearer_call_through_the_default_pipeline_succeeds_with_one_resend` (the retry stage does not
treat the `401` as retryable: `RETRY-1`, `CFG-35`; the `Diagnostics` stage records `http.request.resend_count == 1` on the
second attempt span via `ActivityRecorder`: fact 15); `A_pipeline_with_redirect_forwards_no_bearer_cross_origin` (the
`Redirect` > `Retry` > `Auth` order, `AUTH-27`/`AUTH-29`, with a `ScriptedTransport` redirect then a foreign-origin `401`:
no token fetched for the foreign hop, the hook never called). **IDs:** `AUTH-27`, `AUTH-29`. **Verify:** V-fast
`BearerPipelineTests`.

### Task 5.7 — Close-out (PR 5) and the draft closing comment for #7

`CHANGELOG.md` `### Changed`: breaking item 5 (cache: margin, background refresh, bounded, coalesced, validation) and the
bearer retry (**Breaking**, behaviour); `### Added`: `AccessTokenCache.Get`, `DefaultRefreshMargin`, `TokenProviderException`,
log event 160. Run **V-gate**, the coverage gate and its self-test; Security diff unchanged from PR 4. Draft (do not post)
the comment for #7 in `$SCRATCH/issue-7-comment.md`: the shipped types (`AuthenticationChallenge`, `IChallengeHandler` as an
interface per the SPI rule, `BasicChallengeHandler`, `CompositeChallengeHandler`, the `401` hook, the bearer retry), the
checklist rows (`AUTH-12`, `AUTH-13`, `AUTH-14`, `AUTH-23`, `AUTH-25`, `AUTH-30`–`AUTH-33`, `AUTH-36`), and that BCL crypto
only and trim/AOT safety held. The PR body carries `Closes #7`. **Commit:** `feat!: phase 6c token cache and bearer challenge
(AUTH-11, AUTH-34..AUTH-37)`.

---

## PR 6 — Digest

**Gate: PRs 2 and 3 merged (`DigestCredential`, the handler SPI); task 6.6 needs PR 4.** Rows: `AUTH-15`–`AUTH-22`, `AUTH-24`. **Closes #2.** Files:
`src/Dexpace.Sdk.Core/Auth/{DigestAlgorithm,DigestChallengeHandler,DigestComputation,Md5Availability,NonceCounter}.cs`,
`PublicAPI.Unshipped.txt`, `tests/vectors/auth/digest.json`, `tests/Dexpace.Sdk.Http.SystemNet.Tests/DigestWireTests.cs`, and the
tests below.

### Task 6.1 — Vectors (`AUTH-15`–`AUTH-22`; P6c-29 to P6c-35)

Create `tests/vectors/auth/digest.json`: rows `{ "name", "challenges": [header strings], "credential": {user, password},
"method", "url", "cnonce", "preference"?, "md5Available"?, "expected": null | { "authorization": "…" } , "source", "note" }`.
Fixed `cnonce` values come from the case (the handler's internal constructor takes a cnonce factory for tests). Rows (values
re-derived in task 0.1 fact 13, never copied unverified):

- `rfc2617_mufasa_md5_qop_auth` (the `6629fae4…` vector, `source: RFC 2617 §3.5`), `legacy_no_qop_md5` (`670fd8c2…`,
  Ruby), `rfc7616_sha256_qop_auth` and `rfc7616_sha256_sess_qop_auth` (RFC 7616 §3.9.1 inputs, the re-derived 64-digit
  responses; `note`: the RFC's printed value has 63 digits), `md5_sess_qop_auth`, `sha256_default_preference_chosen_over_md5`
  (a server offering both: SHA-256 wins by default), `md5_preferred_when_preference_says_so`, `unsupported_algorithm_only`
  (`SHA-512-256` → `null`), `auth_int_only_declined`, `qop_list_auth_int_comma_auth` (`auth` is picked), `missing_realm_
  or_nonce_declined`, `non_digest_scheme_declined`, `sess_without_qop_declined` (P6c-31; `note` names Node/Ruby differences),
  `opaque_echoed_when_sent_and_absent_otherwise`, `quoting_escapes_quote_and_backslash`, `utf8_charset_non_ascii_password`,
  `latin1_default_charset_latin1_password`, `latin1_credential_not_representable_declined` (fact 6, P6c-32),
  `non_ascii_username_goes_out_as_username_star` (P6c-33; UTF-8 and Latin-1 forms), `nonce_not_echoable_declined`,
  `digest_uri_is_escaped_request_target` (`/a%2Fb?x=%26y`), `empty_path_digest_uri_is_slash`, `fips_md5_unavailable_
  falls_through_to_sha256` (`md5Available: false`), `fips_md5_only_challenge_declined`, `algorithm_case_insensitive_in_
  challenge_spelled_in_full_in_response`, `userhash_never_sent`, and `nc_increments_on_nonce_reuse` as a two-step row
  (second call `nc=00000002`).

**Verify:** loads through `VectorFile.Load<DigestCase>("auth/digest.json")`.

### Task 6.2 — `Md5Availability` and `DigestAlgorithm` (`AUTH-15`; P6c-30, P6c-29, R13)

**Tests first.** `Md5AvailabilityTests`: `Probe_succeeds_when_the_hash_succeeds`; `Probe_is_false_on_CryptographicException_and_
PlatformNotSupportedException`; `Probe_does_not_swallow_other_exceptions` (an `InvalidOperationException` propagates);
`IsAvailable_is_computed_once` (`Lazy` pin via a counter in the internal probe overload). `DigestAlgorithmTests`:
`Values_are_explicit` (`Md5` 0, `Md5Sess` 1, `Sha256` 2, `Sha256Sess` 3) and `Wire_names_are_MD5_MD5_sess_SHA_256_SHA_256_sess`.

**Production.** `DigestAlgorithm.cs` (public enum, explicit values); `Md5Availability.cs` (R13); an internal
`DigestAlgorithmNames` mapping (parse case-insensitively, format in full spelling). **PublicAPI:** `DigestAlgorithm`.
**IDs:** `AUTH-15`. **Verify:** V-fast the two classes.

### Task 6.3 — The computation (`AUTH-17`, `AUTH-20`–`AUTH-22`; P6c-32 to P6c-34, P6c-38, R14)

**Tests first.** `DigestComputationTests` (internal, through `InternalsVisibleTo`): every vector row's `response` value via
`Compute(algorithm, user, password, realm, nonce, method, digestUri, qop, nc, cnonce, charset)`; `HA1_sess_folds_nonce_and_cnonce`;
`Hex_is_lower_case` (`AUTH-17`, fact 4); `Encoding_is_UTF8_under_charset_UTF8_case_insensitive_else_Latin1` (`AUTH-21`);
`A_Latin1_unrepresentable_credential_is_reported_as_not_encodable_not_hashed_with_question_marks` (the method returns a
failure result; asserts no `E9 3F` substitution, fact 6); `Rfc8187_encoding_of_a_username` (R14: `"Jäsøn Doe"` → `UTF-8''J%C3%A4s%C3%B8n%20Doe`);
`Quoted_string_escaping` (`\"` and `\\`); `Digest_uri_is_the_escaped_PathAndQuery_or_slash` (P6c-34, the fact-9 decision from
task 0.1; a pin per URL shape); `The_cnonce_is_32_lower_case_hex_characters` (`AUTH-20`; shape via the factory default) and
`Two_cnonces_differ` ; `The_cnonce_source_is_RandomNumberGenerator` (source scan: `src/…/DigestChallengeHandler.cs` contains
`RandomNumberGenerator.GetHexString` and not `Random.Shared` — `XCUT-21`).

**Production.** `Auth/DigestComputation.cs` (`internal static class`): `Compute` returning the lower-case hex response,
`ComputeHa1`, `ComputeHa2` (method:uri, `auth` only), `Hash(algorithm, bytes)` (MD5 or SHA-256 via the static `HashData`),
`EncodeCredential(...)` returning a result struct, `Quote(string)`, `Rfc8187Encode`, `DigestUri(Uri)`. Hex only via
`Convert.ToHexStringLower` (P6c-38). Each method under 70 lines; no state. **IDs:** `AUTH-17`, `AUTH-20`–`AUTH-22`.
**Verify:** V-fast `DigestComputationTests` (green on the RFC vectors first).

### Task 6.4 — `DigestChallengeHandler` (`AUTH-15`, `AUTH-16`, `AUTH-22`; P6c-29 to P6c-33)

**Tests first.** New `DigestChallengeHandlerTests`:

- `Every_vector_row_produces_its_expected_Authorization_or_null` (`[Theory]` over `digest.json`; the handler is built through
  the internal constructor `(credential, preference, md5Available, cnonceFactory)`).
- Selection (`AUTH-16`): `Satisfiable_requires_digest_realm_nonce`; `qop_absent_or_a_list_containing_auth_is_accepted`;
  `Algorithm_absent_means_MD5`; `Chosen_by_configured_preference_not_wire_order`; `Default_preference_is_SHA256_SHA256sess_MD5_
  MD5sess` (`DefaultPreference` property); `A_caller_preference_is_any_non_empty_duplicate_free_subset` (empty → `ArgumentException`,
  duplicate → `ArgumentException`); `A_challenge_among_several_is_picked_by_scheme` (a `Bearer` first, a `Digest` second);
  `Sess_without_qop_is_declined` (P6c-31).
- Wire form (`AUTH-22`): `username_realm_nonce_uri_response_cnonce_opaque_are_quoted_qop_nc_algorithm_are_bare`;
  `algorithm_is_always_emitted_in_full_spelling`; `qop_nc_cnonce_only_when_qop_is_negotiated`; `opaque_only_when_sent`; `a_username_
  with_non_ascii_goes_out_as_username_star`; `an_unechoable_realm_nonce_or_opaque_is_declined` (a value with a control or non-ASCII
  character; `XCUT-18`, fact 12); `the_header_value_passes_the_outbound_grammar` (set through `Headers.Set` without throwing).
- Proxy: `proxy_true_sets_Proxy_Authorization_only`.
- FIPS (`AUTH-15`, P6c-30): `With_MD5_unavailable_an_MD5_only_challenge_is_declined`; `With_MD5_unavailable_a_challenge_offering_both_
  falls_through_to_SHA256`; `Construction_never_fails_on_a_host_without_MD5_even_with_an_MD5_only_preference`; `Md5_sess_is_dropped_too`.
- Not implemented on purpose: `No_rspauth_verification` (`AUTH-15`: a pin that the handler exposes no member reading
  `Authentication-Info`; reflection on the public surface).
- Declines never consume a count: `A_declined_attempt_does_not_advance_nc` (P6c-35).

`ChallengeHandlerContractTests` (task 3.5) gains the Digest handler.

**Production.** `Auth/DigestChallengeHandler.cs`: constructors `(DigestCredential)` and `(DigestCredential,
IEnumerable<DigestAlgorithm> preference)`, `DefaultPreference`, and the `internal` test constructor. `Authorize`: find the
first `digest` challenge (loop candidates: each Digest challenge in wire order is evaluated, preference picks the
algorithm across all satisfiable ones: `RFC 7616 §3.7`); `TrySelect(...)`; `EncodeCredential`; `nc`; `Compute`; build the
header string with a `StringBuilder` (helpers `AppendQuoted`, `AppendBare`); `request.WithHeaders(request.Headers.Set(
proxy ? ProxyAuthorization : Authorization, value))`. Effective preference = configured ∩ available (`Md5Availability`).
**PublicAPI:** `DigestChallengeHandler`, `DefaultPreference`. **IDs:** `AUTH-15`, `AUTH-16`, `AUTH-22`. **Verify:** V-fast
`DigestChallengeHandlerTests`, `ChallengeHandlerContractTests`.

### Task 6.5 — The nonce counter (`AUTH-18`, `AUTH-19`, `AUTH-24`; P6c-35, `XCUT-14`)

**Tests first.** `NonceCounterTests` (through the handler): `First_use_of_a_nonce_is_nc_00000001`;
`Reuse_of_the_same_nonce_increments`; `A_different_nonce_starts_at_one`; `nc_is_eight_lower_case_hex_digits_of_the_low_32_bits`
(an internal test hook sets the counter near `uint.MaxValue`, expects wrap to `00000000` rendering from `(uint)n`); `The_store_is_
a_BoundedMap_of_1024` (reflection on the private field type is brittle; assert via an internal `NonceStoreCapacity` hook equal
to `BoundedMap.Capacity` and to the literal 1024); `64_parallel_Authorize_calls_on_one_nonce_yield_nc_1_to_64_without_duplicates`
(`AUTH-24`; parse each response's `nc`, sort, compare with `1..64`); `Handlers_hold_no_state_except_the_counters` (reflection:
the handler's instance fields are the credential, the preference array, the availability flag, the cnonce factory and the
bounded map only). **Production.** `NonceCounter` (internal sealed class with an `int _value`, `Next() => Interlocked.
Increment`), the handler's `BoundedMap<string, NonceCounter>` through `GetOrAdd` (R1). **IDs:** `AUTH-18`, `AUTH-19`,
`AUTH-24`. **Verify:** V-fast `NonceCounterTests`.

### Task 6.6 — End-to-end replay and the wire test (`AUTH-16`, `AUTH-22`, `AUTH-30`; fact 9)

In `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/ChallengeAuthPolicyTests.cs` add `A_digest_401_is_answered_once_end_to_end`
(`ScriptedTransport`: `401 WWW-Authenticate: Digest …` then `200`; second request's `Authorization` is the RFC vector's
header when the cnonce factory is fixed; exactly two transport calls; a second `401` after the replay is returned) and
`A_composite_digest_then_basic_falls_back_to_basic_when_digest_declines`.

New `tests/Dexpace.Sdk.Http.SystemNet.Tests/DigestWireTests.cs`, `[Trait("Category", "Integration")]`, using the existing
`Loopback/LoopbackServer`: the server answers the first request `401` with a Digest challenge and records the second
request's raw request-target and `Authorization`; cases: a path with `%2F`, a query with `%26`, and an empty path; each asserts
the `uri="…"` parameter equals the request-target the server received (fact 9). The `LoopbackServer` listens on plain
`http://127.0.0.1:{port}/` and P6c-8's guard has no loopback exemption (design §11 item 25), so the test does **not** go through an
auth policy (it would throw `HttpsRequiredException` before the first request). Instead it builds the loopback `Request`, calls
`DigestChallengeHandler.Authorize` (with a fixed cnonce factory) on a parsed challenge to get the stamped request, sends that
request straight through `SystemNetHttpClient.ExecuteAsync`, and compares the `uri="…"` parameter with the request-target the
server recorded. If task 0.1 found `GetComponents(PathAndQuery, UriEscaped)` differs from the wire,
this test drives the fallback decision (P6c-34) before PR 6 merges. **IDs:** `AUTH-16`, `AUTH-22`, `AUTH-30`. **Verify:**
`dotnet test --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-class "*DigestWireTests"`.

### Task 6.7 — Close-out (PR 6) and the draft closing comment for #2

`CHANGELOG.md` `### Added`: RFC 7616 Digest (MD5, MD5-sess, SHA-256, SHA-256-sess, `qop=auth`), `DigestChallengeHandler`,
`DigestAlgorithm`, the FIPS probe (a host that refuses MD5 declines MD5 challenges). No **Breaking** (issue #2: additive).
Run **V-gate**, coverage gate and self-test; confirm the SystemNet `PublicAPI.Unshipped.txt` is untouched. Draft (do not
post) `$SCRATCH/issue-2-comment.md`: shipped surface, checklist rows (`AUTH-15`–`AUTH-22`, `AUTH-24`), the FIPS behaviour
(design §6.3 probe), the `-sess`-without-qop decline and its §11 item, the default preference. The PR body carries `Closes #2`.
**Commit:** `feat: phase 6c Digest authentication (AUTH-15..AUTH-22, AUTH-24)`.

---

## PR 7 — `MultiSchemeAuthPolicy`

**Gate: PRs 1, 4, 5, 6 merged; the lead's decision on P6c-36 (R12).** Rows: `AUTH-4`/`AUTH-5` end to end. Files:
`src/Dexpace.Sdk.Core/Pipeline/Policies/{CredentialStampers(internal),MultiSchemeAuthPolicy}.cs`, the three single-scheme
policies (refactored onto the stampers), `PublicAPI.Unshipped.txt`.

### Task 7.1 — Shared internal stampers, no behaviour change (P6c-36)

**Pin first.** Run the whole `Pipeline/Policies/*AuthPolicyTests` set green before touching anything. **Production
(refactor).** `Pipeline/Policies/CredentialStampers.cs`: `internal static class` (or three small `internal sealed` classes
if state is needed — the bearer stamper holds a cache): `ApiKeyStamper`, `BasicStamper`, `BearerStamper` (cache, scopes, the
`OnChallenge` logic of task 5.5), `DigestStamper` (a `DigestChallengeHandler`, reactive). `ApiKeyAuthPolicy`,
`BasicAuthPolicy`, `BearerTokenAuthPolicy` delegate to them. `chore:` only; every existing test unchanged and green.
**Verify:** the whole policies test folder.

### Task 7.2 — Failing tests and the policy (`AUTH-4`, `AUTH-5`; P6c-7, P6c-36)

New `MultiSchemeAuthPolicyTests`: `Available_schemes_are_the_configured_credentials` (an `AuthCredentials` with only `Basic`
→ resolution against `[Digest, Basic]` picks `Basic`); `The_client_descriptor_order_decides` (`[OAuth2, Basic]` with both set
→ OAuth2); `A_per_call_descriptor_overrides_the_client_one`; `An_operation_descriptor_is_used_when_no_per_call_one_is_set`;
`A_per_call_NoAuth_sends_anonymously`; `A_descriptor_naming_only_unconfigured_schemes_throws_AuthResolutionException_before_sending`;
`ApiKey_is_stamped_in_its_own_header_and_withheld_cross_origin` (`WithheldHeaderNames` includes the key's header and
`Authorization`); `Basic_is_preemptive`; `Digest_is_reactive_and_answers_one_401`; `OAuth2_is_stamped_and_retried_once_on_a_
Bearer_401` (the same cache and logic as the single-scheme policy: assert via a shared scripted run); `Two_calls_with_
different_tiers_use_different_schemes_through_one_policy_instance`; `The_policy_is_safe_under_concurrent_calls`;
`Constructor_rejects_null_and_an_empty_credentials_set` (a descriptor whose every requirement is unservable by the
`AuthCredentials` is a construction-time `ArgumentException` only if `AllowsAnonymous` is false and the client tier has no
servable requirement: documented; the check is a pin of the chosen reading, flagged for the lead).

**Production.** `MultiSchemeAuthPolicy`: constructor `(AuthDescriptor clientDescriptor, AuthCredentials credentials,
TimeProvider? timeProvider = null)`; builds only the stampers for non-null credentials (the bearer cache is `new AccessTokenCache(credentials.Token, timeProvider) { RefreshMargin =
credentials.TokenRefreshMargin }`, R15); `GetCredential*` switch on `requirement.Scheme`; `OnChallenge*` switch
likewise (`OAuth2` → bearer logic, `Digest` → handler, others `null`). **PublicAPI:** the policy. **IDs:** `AUTH-4`, `AUTH-5`.
**Verify:** V-fast `MultiSchemeAuthPolicyTests` and the policies folder.

### Task 7.3 — Close-out (PR 7)

`CHANGELOG.md` `### Added`: `MultiSchemeAuthPolicy` and its OpenAPI mapping table in `docs/sdk-documentation/auth.md`
(written in task 8.2). Run **V-gate**. **Commit:** `feat: phase 6c multi-scheme authentication policy`.

---

## PR 8 — Close-out

**Gate: PRs 1–7 merged.** The docs close the sub-phase (roadmap step 7). Rows: all 38 (closing).

### Task 8.1 — NativeAOT smoke over the new surface (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckPhase6cAuthAsync()`, in the existing style with its `Expect` helper (no reflection;
`AotSmoke` gets no `InternalsVisibleTo`; `using` additions are alphabetical in the file's block):

- `AuthenticationChallenge.Parse("Basic realm=\"a\", Digest realm=\"b\", nonce=\"n\", qop=\"auth\"")` yields two challenges with
  the expected parameters; a malformed input returns without throwing;
- `AuthResolver.Resolve(null, new AuthDescriptor(new AuthRequirement(AuthScheme.Digest), AuthRequirement.NoAuth), null, [])`
  returns `NoAuth`;
- a `DigestChallengeHandler` built with a SHA-256-only preference answers a SHA-256 challenge (`algorithm=SHA-256` and a
  64-hex `response=` in the header) — the BCL hash and the CSPRNG cnonce under AOT;
- a `BearerTokenAuthPolicy` over a fixed `TokenCredential` stamps `Bearer <token>` on a scripted `https://` call (the cache,
  `BoundedMap`, `SemaphoreSlim` and `BackgroundWork` under AOT: include a token inside the margin so a background refresh runs
  and completes);
- `ToString()` of a `BasicCredential`, an `AccessToken` and an `AuthCredentials` contains `***`/no secret.

A trim/AOT warning means the source is fixed, not the smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke
--configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "all checks passed".
**Commit:** `test: NativeAOT smoke over the authentication surface`.

### Task 8.2 — User documentation

New `docs/sdk-documentation/auth.md`, opening "As built by phase 6c, written against source on <date>". Content, citing
requirement IDs and never copying the design: the descriptor and tiers and where they ride (`RequestOptions.Auth`/
`OperationAuth`, the policy's client descriptor); the credential types and their redaction; the five shipped policies and the
OpenAPI-scheme mapping table (design G); the `401` lifecycle in the order of design E, the replayability gate and the
cross-origin rule; the cache (zones, margin, coalescing, background refresh, event 160, bound); Digest (algorithms,
preference, the FIPS probe, `-sess` without qop, encodings, `username*`); the challenge parser's leniency; what is not built
(claims challenges, query/cookie API keys, preemptive Digest, `NamedKeyCredential`, `407` handling: P6c-13, 19, 35, 40); the
migration table for the nine breaking changes. Notes that Ruby's `gems/` tree and Node's `auth.md` were read from
`ruby-sdk` and `nodejs-sdk@54aeed4`. `docs/README.md`'s ownership table gains the row the probe asks for. `src/Dexpace.Sdk.Core/
README.md`: the auth samples. **Verify:** the probe's `links` check.

### Task 8.3 — The checklist

Write `docs/work/mvp/phase6/phase6c/<date>-phase6c-auth-checklist.md` (the housekeeping `apply` step files the design and this
plan beside it, task 8.6) from what was built: **38 rows**, the constraint-3 legend, mirroring the design's census and this
plan's checklist section: 38 ✅ with evidence, each recorded departure (`AUTH-15`/`AUTH-16` MD5 platform dependency, `AUTH-16`
`-sess` decline, `AUTH-8`/`AUTH-9` named-key clauses vacuous, `AUTH-28` reactive-scheme guard, `AUTH-14` colon rule). Include the
"existing assertions changed" table (expected: `BasicAuthPolicyTests.ProcessAsync_EmptyPassword_StampsCorrectly`, the
`AccessTokenCacheTests.GetAsync_RefreshThrows_WhileStillValid_ReturnsCachedToken`
and `GetAsync_PastRefreshOn_RefreshesOnce`, `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_documented_bridge`,
`AuthHttpsGuardTests` per task 4.5), the Security evidence of the exact allowed diff, the vector provenance (Ruby, Node
`@54aeed4`; cases not ported and why), the fact 7/9/13 outcomes from task 0.1, the flake-loop result of task 5.3, and the
deviation ledger as built (P6c-2, 6, 7, 8, 12, 17, 19, 21, 25, 29, 30, 31, 33, 36, 38) plus the plan's readings R3, R12
with their lead decisions.

### Task 8.4 — Dated corrections, roadmap note, hand-offs

Frozen documents change only by dated correction (the design's "Corrections owed" list; re-read each file immediately before
writing, as 6a and 6b may have added items). In `docs/sdk-design-dotnet/`: **§6.3** (the **As built** line; `net8.0` hex text →
`Convert.ToHexStringLower`, P6c-38; the default preference, P6c-29); **§5.4 / §8.1** (the helper is `Internal/BackgroundWork.cs`);
**§10 entry 16** ("probed once per process"); **§11** new items at the next free numbers (`-sess` without qop; the reactive-
scheme guard; the vacuous named-key clauses; the cross-origin `401`); **§12** (`AUTH` row 38 of 38, `AUTH-26` addressed). Earlier
checklists: phase 1's S4 and S5 rows, 3b's `BODY-4`, 4a's `AUTH-19` hand-off, 4c's P4c-13 (the bridge retired). **Roadmap:** the
Phase List row 6's `sdk-design refs` cell gains this design's link (appended, never replacing), and a dated Phase Status Note
recording the eight PRs, the rulings the lead accepted or changed (P6c-2, 6, 7, 8, 17, 19, 21, 25, 29, 31, 33, 36), the nine
breaking changes, #7 and #2 closed with their checklist rows, and the hand-offs (8b: the `407`/`ICredentials` adaptation; 9:
DI for auth policies; 10: `XCUT-12`, `XCUT-14`, `XCUT-16`, `XCUT-19`(d), `XCUT-21` cite 6c's tests; 12: claims challenges and
query/cookie API keys in `docs/first-release.md`). **`CLAUDE.md`:** the `Auth/` and `Pipeline/Policies/` layout lines; "What is
genuinely unbuilt" drops "the auth resolver with RFC 7235 challenges and Digest (6c)". **`BannedSymbols.txt`:** verify the
comment line from task 5.1. If the implementation found anything the knowledge corpus should hold (the fact 7/9 outcomes), record
a note under `docs/knowledge/notes/` (never edit `harvested/`). **Verify:** the probe's `links` and `citations` checks.

### Task 8.5 — Close-out

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/auth.md`; the AOT smoke covers the challenge parser, the resolver, Digest
and the bearer policy." Run **V-gate**, the coverage gate with its self-test, and the Security diff. Post nothing: the closing
comments for #7 and #2 (drafted in tasks 5.7 and 6.7, updated with the final checklist row links) are handed to the lead.
**Commits:** `docs: phase 6c checklist, user page and dated corrections` (after 8.1's `test:` commit).

### Task 8.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6c            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 6c --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs).
`apply --write` performs `git mv` only; the plan authorises no commit beyond those in 8.1 and 8.5 and **no push**.

---

## Keeping the `Security` classes green

Constraint 5; the design's table is the authority and the plan adds the *when* and the exact allowed diff (contrast 5a's empty
diff: 6c edits one class and adds one).

| Class | Edit | When it is run |
|---|---|---|
| `AuthHttpsGuardTests` (S4) | the re-signature and the strengthened exception-type assertions of task 4.5, nothing else | **first** after task 4.4, then every V-gate from PR 4 |
| `CredentialRedactionTests` (new, S5's `XCUT-19`(d) remainder) | added in task 2.4 | every V-gate from PR 2 |
| `UrlRedactionDefaultDenyTests` (S5) | none | V-gate |
| `ReDriveRequestIsolationTests` (three cases build pipelines with `BasicAuthPolicy(new BasicCredential("u", "p"))` through the reworked base) | none | tasks 4.4 and 4.6, then V-gate (run `AsyncErrorModelTests`, non-Security, beside it) |
| `RedirectCredentialHygieneTests`, SystemNet `RedirectWireTests` (S3) | none (neither uses an auth policy) | V-gate |
| `RetryPacingOverflowTests`, `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | none | V-gate |

**The check** is `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
and the allowed output by PR: PR 1, 3: empty; PR 2: `CredentialRedactionTests.cs` added; PR 4–8: `CredentialRedactionTests.cs`
added and `AuthHttpsGuardTests.cs` modified, with `git diff` of the latter matching task 4.5's list line for line. Any other
line is a signal to re-read the design's "Keeping the `Security` classes green", not to edit the file.

---

## Checklist: one row per owned ID

Exactly 38 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 8.3 records what was
built. "Pin" means the test already passes on the as-built behaviour and is proven able to fail (convention 1). Level: 36
MUST, 2 SHOULD (`AUTH-19`, `AUTH-38`).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `AUTH-1` | MUST | 1 | 1.1, 1.2 | ✅ | `AuthDescriptorTests.AuthScheme_values_are_explicit_and_exactly_five` |
| `AUTH-2` | MUST | 1 | 1.1, 1.2 | ✅ | `AuthDescriptorTests.Scopes_and_parameters_are_copied_at_init`, `Requirements_with_equal_content_are_equal_and_hash_alike` |
| `AUTH-3` | MUST | 1 | 1.1, 1.2 | ✅ | `AuthDescriptorTests.A_descriptor_copies_its_requirements_…`, `An_empty_descriptor_or_a_null_element_throws_ArgumentException`, `AllowsAnonymous_is_true_iff_…` |
| `AUTH-4` | MUST | 1, 4, 7 | 1.3, 1.4, 4.3, 4.4, 7.2 | ✅ | `AuthResolverTests.Per_call_wins_…`, `A_higher_tier_that_cannot_be_satisfied_does_not_fall_through`; `AuthorizationPolicyChallengeTests` tier cases; `MultiSchemeAuthPolicyTests` |
| `AUTH-5` | MUST | 1, 7 | 1.3, 7.2 | ✅ | `AuthResolverTests.The_first_satisfiable_requirement_in_preference_order_wins`, `No_credential_is_inspected` |
| `AUTH-6` | MUST | 1, 4 | 1.3, 4.3 | ✅ | `AuthResolverTests.All_tiers_absent_…`, `Nothing_satisfiable_throws_AuthResolutionException_…`; `A_per_call_scheme_the_policy_cannot_serve_throws_…` |
| `AUTH-7` | MUST | 1 | 1.3 | ✅ | `AuthResolverTests.Resolve_is_safe_under_concurrent_calls_on_one_descriptor` (static class, no field) |
| `AUTH-8` | MUST | 2 | 2.1–2.4 | ✅ (named-key clause vacuous, P6c-19) | `CredentialRedactionTests` (all), `AccessTokenTests.AccessToken_equality_is_by_value_…` |
| `AUTH-9` | MUST | 2 | 2.1–2.3 | ✅ (named-key clause vacuous) | `AccessTokenTests.AccessToken_rejects_null_empty_and_whitespace_tokens`, `ApiKeyCredentialTests.ApiKeyCredential_rejects_a_whitespace_only_key` |
| `AUTH-10` | MUST | 2 | 2.1, 2.2 | ✅ | `AccessTokenTests.AccessToken_without_expiry_never_expires`, `IsExpired_is_strictly_after` |
| `AUTH-11` | MUST | 5 | 5.2, 5.3 | ✅ (met → pin + sync-throw arm) | `AccessTokenCacheTests.A_provider_exception_propagates_unwrapped_and_nothing_is_cached` |
| `AUTH-12` | MUST | 3 | 3.1, 3.3, 3.4 | ✅ | `AuthenticationChallengeTests.Parse_matches_every_vector`, `Scheme_and_parameter_names_are_lower_cased_…` |
| `AUTH-13` | MUST | 3 | 3.1, 3.3, 3.4 | ✅ | `AuthenticationChallengeTests.Parse_never_throws_for_arbitrary_text`, the leniency vector rows |
| `AUTH-14` | MUST | 2, 3, 4 | 2.3, 3.5, 4.4 | ✅ (colon rule stricter, P6c-17) | `BasicChallengeHandlerTests.Value_is_Basic_plus_base64_of_UTF8_…`, `The_value_equals_the_one_BasicAuthPolicy_stamps_preemptively`; `BasicCredentialTests.BasicCredential_…` |
| `AUTH-15` | MUST | 6 | 6.2, 6.4 | ✅ (MD5 platform dependency, §10 entry 16) | `DigestChallengeHandlerTests` FIPS cases, `Every_vector_row_…`, `No_rspauth_verification` |
| `AUTH-16` | MUST | 6 | 6.4 | ✅ (`-sess` without qop declined, P6c-31) | `DigestChallengeHandlerTests.Chosen_by_configured_preference_not_wire_order`, `Sess_without_qop_is_declined`, `Satisfiable_requires_digest_realm_nonce` |
| `AUTH-17` | MUST | 6 | 6.3 | ✅ | `DigestComputationTests` (RFC 2617/7616 vectors), `Hex_is_lower_case` |
| `AUTH-18` | MUST | 6 | 6.5 | ✅ | `NonceCounterTests.First_use_…`, `Reuse_of_the_same_nonce_increments`, `nc_is_eight_lower_case_hex_digits_…` |
| `AUTH-19` | SHOULD | 6 | 6.5 | ✅ | `NonceCounterTests.The_store_is_a_BoundedMap_of_1024` |
| `AUTH-20` | MUST | 6 | 6.3 | ✅ | `DigestComputationTests.The_cnonce_is_32_lower_case_hex_characters`, `The_cnonce_source_is_RandomNumberGenerator` |
| `AUTH-21` | MUST | 6 | 6.3, 6.4 | ✅ | `DigestComputationTests.Encoding_is_UTF8_under_charset_…`, `A_Latin1_unrepresentable_credential_is_…` |
| `AUTH-22` | MUST | 6 | 6.3, 6.4, 6.6 | ✅ | `DigestChallengeHandlerTests` wire-form cases; `DigestWireTests` |
| `AUTH-23` | MUST | 3 | 3.5 | ✅ | `CompositeChallengeHandlerTests` (all) |
| `AUTH-24` | MUST | 3, 6 | 3.5, 6.5 | ✅ | `NonceCounterTests.64_parallel_Authorize_calls_…`, `Handlers_hold_no_state_except_the_counters`, `CompositeChallengeHandlerTests.Is_stateless_and_safe_under_parallel_use` |
| `AUTH-25` | MUST | 3 | 3.5 | ✅ | `ChallengeHandlerContractTests`, `BasicChallengeHandlerTests.Sets_Proxy_Authorization_when_proxy_is_true` |
| `AUTH-26` | MUST | 2, 4 | 2.3, 4.4 | ✅ (met, unreconciled → addressed, P6c-19) | `ApiKeyCredentialTests.ApiKeyCredential_…`, `ApiKeyAuthPolicyTests.The_header_value_is_stamped_from_the_credential_computed_once` |
| `AUTH-27` | MUST | 4 | 4.2, 5.6 | ✅ (pin) | `AuthorizationPolicyChallengeTests.Stage_is_Auth_and_sealed`, `The_stage_order_is_Redirect_then_Retry_then_Auth`, `BearerPipelineTests` |
| `AUTH-28` | MUST | 4 | 4.1–4.5 | ✅ (reactive-scheme reading, P6c-8) | `AuthHttpsGuardTests` (strengthened), `AuthorizationPolicyChallengeTests.A_non_https_url_throws_HttpsRequiredException_…`, `The_guard_also_covers_a_reactive_schemes_outbound_pass` |
| `AUTH-29` | MUST | 4 | 4.2, 5.6 | ✅ (pin; no marker, §10 entry 15) | `AuthorizationPolicyChallengeTests.A_cross_origin_hop_has_its_credential_header_removed_…`, `A_cross_origin_401_is_returned_without_challenge_handling`, `BearerPipelineTests.A_pipeline_with_redirect_forwards_no_bearer_cross_origin` |
| `AUTH-30` | MUST | 4, 5, 6 | 4.2–4.4, 5.5, 6.6 | ✅ | `AuthorizationPolicyChallengeTests.A_non_null_replacement_is_driven_once_…`, `No_further_challenge_handling_after_the_replay`, `The_default_hook_yields_null_…`; `ChallengeAuthPolicyTests` |
| `AUTH-31` | MUST | 4, 5 | 4.2, 5.5 | ✅ | `AuthorizationPolicyChallengeTests.A_replacement_with_a_non_replayable_body_is_not_sent_…` (both paths); `BearerTokenAuthPolicyTests.The_retry_is_method_agnostic` |
| `AUTH-32` | MUST | 4 | 4.2 | ✅ | `AuthorizationPolicyChallengeTests.A_hook_that_throws_disposes_the_401_and_propagates_the_exception` (three arms) |
| `AUTH-33` | MUST | 4 | 4.2 | ✅ | `AuthorizationPolicyChallengeTests.A_401_without_WWW_Authenticate_is_returned_unchanged_and_the_hook_is_not_called` |
| `AUTH-34` | MUST | 5 | 5.2, 5.3 | ✅ | `AccessTokenCacheTests.The_default_margin_is_30_seconds_and_configurable`, `The_hot_path_takes_no_lock`, `N_concurrent_requests_at_expiry_cost_one_fetch` |
| `AUTH-35` | MUST | 5 | 5.1–5.3 | ✅ | `AccessTokenCacheTests.A_default_token_from_the_provider_throws_TokenProviderException`, `A_token_already_expired_at_fetch_…`, `A_request_arriving_after_a_failure_fetches_again` |
| `AUTH-36` | MUST | 5 | 5.2, 5.5 | ✅ | `AccessTokenCacheTests.A_rejection_of_a_stale_header_keeps_…`; `BearerTokenAuthPolicyTests.A_401_with_a_Bearer_challenge_evicts_fetches_fresh_and_retries_once`, `A_401_with_a_non_bearer_challenge_is_returned` |
| `AUTH-37` | MUST | 5 | 5.1–5.3, 5.5 | ✅ | `AccessTokenCacheTests` background and coalescing cases; `AuthLogTests`; `BackgroundWorkTests` |
| `AUTH-38` | SHOULD | 4 | 4.2 | ✅ (by construction, pinned) | `AuthorizationPolicyChallengeTests.ProcessAsync_returns_a_faulted_task_instead_of_throwing_for_an_http_url` |

Count: **38 rows**, all mapped (36 MUST, 2 SHOULD). By exit: 38 ✅, 0 N/A, 0 🚫, 0 ⏳.

### Work on other owners' rows (no checklist row in 6c)

These carry no exit mark in 6c's checklist. 6c's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `XCUT-12` (10) | Per-key semaphore, never global | 5.3 | `AccessTokenCacheTests.Different_keys_do_not_block_each_other` |
| `XCUT-14` (10) | The token cache and the Digest nonce store are `BoundedMap`s | 5.3, 6.5 | `AccessTokenCacheTests.The_cache_holds_at_most_1024_keys_…`, `NonceCounterTests.The_store_is_a_BoundedMap_of_1024` |
| `XCUT-16` (10) | The typed guard; the reactive reading | 4.1–4.5 | `AuthHttpsGuardTests`, `HttpsRequiredExceptionTests` |
| `XCUT-18` (10) | The echoed Digest values and the key value pass the outbound grammar | 2.3, 6.4 | `ApiKeyCredentialTests.ApiKeyCredential_rejects_a_value_the_outbound_…`, `DigestChallengeHandlerTests.an_unechoable_…` |
| `XCUT-19`(d) (10; phase 1 S5 remainder) | Credentials never reveal secrets in string form | 2.4 | `CredentialRedactionTests` |
| `XCUT-21` (10) | The cnonce is from `RandomNumberGenerator` | 6.3 | `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator` |
| `BODY-4` (3b, ⏳ 6a/6b/6c) | The `AUTH-31` gate, auth's share | 4.2, 5.5 | the `AUTH-31` tests |
| `PIPE-5` (4c) | One policy per pillar | 4.2 | `A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder` |
| `PIPE-28` (4c, ✅; ⏳ 8b) | One fewer documented bridge in core | 4.3, 5.5 | `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_real_sync_path`, `The_sync_and_async_paths_produce_identical_requests` |
| `CTX-19` (4a) | Background work does not pin a call | 5.1 | `BackgroundWorkTests.The_work_observes_no_Activity_current` |
| `OBS-39` (5b, event-id scheme) | Event id 160 inside the reserved auth range | 5.1 | `AuthLogTests.TokenRefreshFailed_is_id_160_…` |
| `REDIR-7`, `REDIR-11`, `REDIR-24` (6b/4c) | The seed origin the policy compares against; no marker | 4.2 | `AuthorizationPolicyChallengeTests` cross-origin cases |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| pre-flight | — | — | 0.1 (1) |
| 1 | none | `AUTH-1`–`AUTH-7` | 1.1–1.5 (5) |
| 2 | 1 | `AUTH-8`–`AUTH-10`; `AUTH-14`, `AUTH-26` (credential halves) | 2.1–2.6 (6) |
| 3 | 2 | `AUTH-12`, `AUTH-13`, `AUTH-14` (handler), `AUTH-23`, `AUTH-25` | 3.1–3.6 (6) |
| 4 | 1, 2, 3 | `AUTH-26` (policy), `AUTH-27`–`AUTH-33`, `AUTH-38` | 4.1–4.6 (6) |
| 5 | 4 | `AUTH-11`, `AUTH-34`–`AUTH-37` | 5.1–5.7 (7) |
| 6 | 2, 3 (4 for 6.6) | `AUTH-15`–`AUTH-22`, `AUTH-24` | 6.1–6.7 (7) |
| 7 | 1, 4, 5, 6; lead on P6c-36 | `AUTH-4`/`AUTH-5` end to end | 7.1–7.3 (3) |
| 8 | 1–7 | all 38 (closing) | 8.1–8.6 (6) |
| **Total** | | | **47 tasks** (46 in eight PRs, plus the pre-flight) |

Rows by the PR that first lands them: PR 1: 7; PR 2: 5 (`AUTH-8`–`AUTH-10`, `AUTH-14`, `AUTH-26`, the credential halves); PR 3: 5 (`AUTH-12`, `AUTH-13`, `AUTH-23`,
`AUTH-24`, `AUTH-25`); PR 4: 8 (`AUTH-27`–`AUTH-33`, `AUTH-38`); PR 5: 5 (`AUTH-11`, `AUTH-34`–`AUTH-37`); PR 6: 8
(`AUTH-15`–`AUTH-22`). 7 + 5 + 5 + 8 + 5 + 8 = 38. The first-landing PR counts the row's first appearance in the checklist's PR
column; a row that spans PRs (`AUTH-14`, `AUTH-24`, `AUTH-26`) is counted once, where it first lands. The gates are
linear for PRs 1 to 4 (1, then 2, then 3, then 4); PR 6 may be developed beside PR 4 and PR 5 once PRs 2 and 3 are in; every
PR stays green in the order these gates allow. Issue closure: PR 5 closes #7 (its parser and handlers landed in PR 3, its `401`
hook in PR 4, the bearer retry in PR 5); PR 6 closes #2.

---

## Findings while planning

Items checked against the repository at `3a1db00` on 2026-10-08, each with what the plan did. R1–R14 above are the readings
these produced.

1. **F1 — `BoundedMap` has no `GetOrAdd` and requires reference-type values** (R1); the plan adds one internal extension used by
   the cache and the nonce store.
2. **F2 — `TokenCredential.GetToken` already exists with its own blocking bridge** (R2); the policy's sync path needs no new
   credential member, only the cache's `Get`.
3. **F3 — Making the base's `GetCredential` abstract in PR 4 would leave the bearer policy without a sync path until PR 5** (R3);
   the plan moves the bridge pragma into the bearer policy for exactly one PR and deletes it in task 5.5.
4. **F4 — `AccessToken.ExpiresOn` becoming nullable breaks the as-built cache's `IsValid` in PR 2** (R4); one-line fix there.
5. **F5 — `TrackingResponseBody` and `TestResponses` already exist** (R6); nothing is added to `TestSupport` beyond a possible
   thin `Unauthorized` helper.
6. **F6 — `DexpaceLogEvents` uses `const string` names and `const int …Id` ids** (R9); the design's two constants follow it.
7. **F7 — `AuthHttpsGuardTests` asserts the exact exception type** (design fact 16); task 4.5 lists the allowed diff line for line
   and the V-gate's expected `git diff --stat` output per PR.
8. **F8 — The design's `AuthCredentials.TokenRefreshMargin` default depends on a constant PR 5 defines** (PR 2 precedes it);
   task 2.4 uses a private 30 s constant and task 5.3 repoints it.
9. **F9 — Six files are shared with 6a and 6b** (design "PR segmentation"); convention 10 and the per-PR tasks say to re-derive
   hunks, and task 3.2 checks whether 6b already added `ProxyAuthorization`.
10. **F10 — `Dexpace.Sdk.Core.Tests` cannot see `Dexpace.Sdk.Http.SystemNet` (SEAM-2)**, so the digest-uri wire test lives in the
    transport's test project (task 6.6) and the `Security` diff command covers both Security folders.
11. **F11 — A `-sess` challenge without qop cannot satisfy `AUTH-16`, `AUTH-17` and `AUTH-22` together** (P6c-31); task 6.1
    carries the row and task 8.4 files the §11 item.
12. **F12 — The design host had no .NET SDK;** every command in this plan, every `PublicAPI` line and the analyzer's view of
    the records' synthesised members are verified on the implementer's host in task 0.1 and against the analyzer's output in
    each task. Design facts 7, 9 and 13 can reopen P6c-30, P6c-34 and the digest vectors respectively.
13. **F13 — Hash and RNG calls are all static BCL APIs** (`MD5.HashData`, `SHA256.HashData`, `RandomNumberGenerator.GetHexString`),
    so the AOT smoke (task 8.1) exercises them without reflection; no `System.Security.Cryptography` package is added.
14. **F14 — `params IEnumerable<T>` constructors** (R10) are a C# 13 feature the repository's `TestSupport` already uses; no
    language-version change is needed.

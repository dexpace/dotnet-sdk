# Phase 5a — Configuration: Implementation Plan

**Status:** Draft, for review. Written 2026-10-07 against `main` at `4130f7b` (phases 0 to 4c merged), on branch
`62-phase-5-planning` (issue #62). Design: [phase 5a configuration design](2026-10-07-phase5a-configuration-design.md), the
authority for every decision below. The plan cites its census rows, positions (A–F), facts (1–15) and rulings
(`P5a-1`…`P5a-28`) rather than restating them. Scope authority: the roadmap's Phase 5 card and Phase List row 5. Format
precedent: the [4c plan](../../phase4/phase4c/2026-10-05-phase4c-pipeline.md) and its [4a](../../phase4/phase4a/2026-10-05-phase4a-context.md)
and [4b](../../phase4/phase4b/2026-10-05-phase4b-recovery.md) siblings (all read first).

**What this document is.** The roadmap's step 3 for sub-phase 5a: numbered TDD tasks in the design's six-PR segmentation,
each with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** markings, `CHANGELOG.md`
entry, requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 6.3)
and it writes no production code. The [checklist section](#checklist-one-row-per-owned-id) below is the plan's own row
table: exactly one row per ID 5a owns.

**Scope.** 26 rows: `CFG-8`, `CFG-9`, `CFG-12`, `CFG-13`, `CFG-15`–`CFG-36` (18 MUST, 7 SHOULD, 1 MAY). Planned exits: 23 ✅,
1 N/A (`CFG-12`), 2 🚫 (`CFG-13`, `CFG-20`), 0 ⏳. The other twelve `CFG` IDs are phase 9's; `OBS-1`–`OBS-40` divide between
5b and 5c. `PIPE-17`, `RETRY-15`, `RETRY-26`, `XCUT-3`, `XCUT-5`, `XCUT-6`, `ASYNC-5`, `TRANSPORT-9`, `TRANSPORT-15`,
`TRANSPORT-30` and `RECOV-33` are other owners' rows on which 5a does work; they appear in
[the cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-5a) and carry no exit mark in 5a's checklist.

**Order and gates.** PRs 1, 2, 3 and 5 are independent of each other; **PR 4 follows PR 2** (the `Environment` ban and the
new `RS0030` machinery land there; if the lead reorders, task 4.6 adds the ban lines itself). PR 6 follows PRs 1–5.
`RetryPacingOverflowTests` (`Security`) runs first after PR 2 and after PR 3 (P5a-26). The segmentation is the design's; the
plan does not change it.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace is imported in the file). A new
   `git mv`ed file keeps its header.
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`; the plan names the helper split wherever a parser or resolver approaches the cap, and adds no waiver); `///` XML
   docs on every public member (CS1591); no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2; `Regex`,
   `ManualResetEventSlim`, `FrozenSet`, `TimeProvider`, `System.Net.ICredentials`, `System.Net.Sockets.SocketException` and
   `System.Net.Http.HttpRequestException` are all in the shared framework; `ILogger`/`LoggerMessage` come from the already
   permitted `Microsoft.Extensions.Logging.Abstractions`). No reflection in `src/`, so `IsAotCompatible` and `IsTrimmable`
   hold unannotated. `Regex` is constructed with `RegexOptions.NonBacktracking` only (no `Compiled`, no source generator).
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `AotSmoke` for the
   smoke checks. 5a adds **no** `Security` class and edits **none** (P5a-26): see
   [Keeping the `Security` classes green](#keeping-the-security-classes-green). Core tests live under
   `tests/Dexpace.Sdk.Core.Tests/<area>/` (namespace `Dexpace.Sdk.Core.Tests.<Area>`); doubles under
   `tests/Dexpace.Sdk.TestSupport/`. `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2).
5. **Security tests are never deleted or loosened.** The V-gate's diff check is the exact allowed diff: **empty**.
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   lists nothing, in every PR. A change that seems to force an edit there is a signal to re-read the design's "Keeping the
   `Security` classes green", not to edit the file.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`
   and a removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority
   (apply the analyzer's code fix and compare against the lines this plan lists). Only core's file changes; the SystemNet and
   STJ files must not (the V-gate checks). A sealed record's synthesised members appear as the `RequestOptions` block shows
   (`<Clone>$`, `Equals(T?)`, the three overrides, the two operators) and its private copy constructor and
   `EqualityContract` do not (design fact 9; confirmed in task 0.1).
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes,
   stating what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes",
   items 1–4). 5b and 5c edit the same changelog; whichever PR lands second re-derives its hunks on the merged file.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1); 5a
   writes `System.IO.IOException` fully qualified wherever a core file needs it (`RetryFacts`).
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `feat:` for an additive one, `test:` for
   tests only, `docs:` for documentation only, `chore:` for refactors. **No AI attribution** of any kind in a commit message,
   changelog line or document. The plan authorises no `git push`, no `gh` command and no remote action. Branches follow
   `<issue>-phase-5a-<slug>` off `main` (`62-phase-5a-pr1-options`, …).
10. **Names from siblings are placeholders where 5b or 5c touch the same file.** `DexpaceClientOptions` gains 5b's logging
    property, `BannedSymbols.txt`, `PublicAPI.Unshipped.txt` and `CHANGELOG.md` are shared (design "PR segmentation"): each PR
    re-derives its hunks from the merged file and never resolves a conflict by keeping either side whole.
11. **Test timing.** A test that must wait uses `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`, pinned in
    `Directory.Packages.props`) or a `TaskCompletionSource`; none sleeps on the wall clock to prove a negative, and none reads
    the process environment (every `FromEnvironment` test passes a dictionary lookup).

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green
build is the lint gate. The test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's
`--filter`). **The design was written on a host with no .NET SDK**; every command below is untested on that host and runs for
the first time on the implementer's. Facts 7–11 are therefore verified in task 0.1 before any code is written.

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
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty (convention 5)
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
artifacts/test-results`) runs before the push of PR 2, PR 4 and PR 6. No `PackageReference` changes, so no
`packages.lock.json` change is expected; if a locked restore complains, run `dotnet restore` on both solutions and commit
every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info CFG
scripts/knowledge --gaps CFG                # 0 of 26: no CFG ID is a gap ID (design, "Governing documents")
scripts/knowledge --req CFG-24              # per row in scope of the PR (also XCUT-5, XCUT-6, RETRY-15 for cited work)
```

Task 0.1 records any difference from the design's corpus-file reading. A difference that contradicts a ruling reopens that
ruling.

---

## Plan-level readings (where the design was silent, ambiguous or wrong about the tree)

The design's decisions are not changed. Where it left a point open the plan took the reading most consistent with it, and
flagged it for the lead where the reading adds behaviour. **R1–R4 are corrections of fact; R5–R12 are additions.**

- **R1 — The options overload of `BuildRequest` has no scheme check of its own.** The design's migration row says
  `BuildRequest` "drops its duplicate scheme check" and that "`OperationDescriptorTests`' scheme cases move". As built, the
  check is `OperationUrlComposer.RequireUsableBase`, reached through `BuildRequest(Uri)`, and it **stays** (the `Uri`
  overload is public and still needs it). The options overload only delegates, after a null check. The scheme cases that
  must move are in `OperationBuildRequestTests.BuildRequest_from_options_applies_the_same_base_rules` (not
  `OperationDescriptorTests`): once `BaseAddress` validates at `init`, that test can no longer reach `BuildRequest`, because
  the options value cannot be constructed. Task 1.2 moves the same three inputs (`/relative`, `https://host/c#frag`,
  `ftp://host/`) to `DexpaceClientOptionsTests`, asserting the throw at construction, and rewrites the doc remark on
  `BuildRequest(DexpaceClientOptions)`.
- **R2 — `BlockingWaitTests` has a case P5a-7 reverses.** `A_zero_or_negative_wait_returns_at_once` carries an `[InlineData(-5)]`
  row: the as-built wait treats a negative as "done". `TimeProviderWaits.Sleep` rejects it (`CFG-17`, fact 3). Task 2.1 splits
  the theory: zero returns at once, negative throws `ArgumentOutOfRangeException`. `RetryPolicy` never passes a negative
  (fact 12, confirmed by reading `DelayFor`'s clamp), so no policy test changes value.
- **R3 — `LateResult` is an async method, not a `ContinueWith`.** The design says one continuation with
  `ExecuteSynchronously | OnlyOnRanToCompletion` that observes a faulted task. `Task<T>.Result` is itself banned
  (`RS0030`), so a continuation reading `t.Result` would need a second `#pragma`. An `async Task` helper that
  `await`s the abandoned task with `ConfigureAwait(false)`, disposes on success through `Disposal.DisposeQuietly`, and
  swallows a non-fatal failure is the same behaviour (one awaiter per abandonment, run inline on completion, a fault
  observed) with the single sanctioned pragma the design promises (`WaitAsync`). The "exactly once" test is unchanged.
- **R4 — The default `User-Agent` field is a property initializer on a `field`-backed property.** `RequestOptions` already
  uses C#'s `field` keyword with an `init` accessor and an initializer (`Tags`); the three records follow that shape, not
  a hand-written backing field, except `ProxyOptions.NonProxyHosts`, which needs a second private field (the compiled
  patterns) and so declares both explicitly.
- **R5 — A recursion bound on `DeepValue` (open for the lead).** Node's `deepEqual` "overflows the stack on a
  self-referential array rather than terminating" (its test says so on purpose). In .NET a stack overflow ends the process
  and cannot be caught. `DeepValue.Equals`/`GetHashCode` therefore carry a depth counter and throw
  `InvalidOperationException` (message names the bound, never the content) past depth 128. The design is silent; the
  alternative is the Node behaviour, which .NET cannot express safely.
- **R6 — IPv6 proxy hosts (open for the lead).** Node's tests pin that `http://[2001:db8::1]:8080` resolves to the bare
  address `2001:db8::1` and renders re-bracketed. `ProxyOptions.Host` stores the bare address; `ToString` re-brackets any host
  containing `:`; the resolver reads the host out of the brackets; `Host` validation rejects a bracket character. The design
  says only "non-blank".
- **R7 — Percent-decoding and empty user names (open for the lead).** Node resolves a malformed `%` in credentials to `null`
  (its `decodeURIComponent` throws) and treats an empty user name as "no credentials". `Uri.UnescapeDataString` leaves a
  lone `%` in place silently, which would alter a password without a trace. The resolver therefore pre-scans the userinfo
  and treats a `%` not followed by two hex digits as invalid (reason `credentials`), and an empty user name yields no
  credentials at all (`UserName` and `Password` both `null`). The design says only "percent-decoded with
  `Uri.UnescapeDataString`".
- **R8 — Surrounding whitespace on an environment value.** The value is trimmed of ASCII whitespace before the "present and
  non-empty" test (a whitespace-only value is absent); nothing inside the value is altered. The design treats "empty is
  absent" (P5a-15 item 1) and is silent on whitespace.
- **R9 — Host validity is one internal predicate.** `ProxyOptions.Host`'s `init` accessor throws `ArgumentException` on a blank
  host or one with whitespace, a control character, a `/`, `@`, `[` or `]`; the resolver calls the same
  `ProxyOptions.IsValidHost` (internal) before constructing, so `FromEnvironment` never relies on catching the constructor's
  exception and never throws.
- **R10 — The proxy warning's `EventId` is `new EventId(20, "ProxyConfigurationIgnored")`.** Ids 1–3 are taken by
  `Disposal` and `SystemNetHttpClient` (separate assemblies, separate scopes); 20 leaves room and is the "provisional" id
  P5a-16 asks for, renumbered by 5b's event scheme.
- **R11 — Vectors that disagree with a ruling.** Node's `http-date.test.ts` pins three behaviours the design rules the other
  way: surrounding whitespace tolerated (design: no trimming), a leap second `:60` rolled into the next minute (design: `60`
  rejected), and year `0000` (`DateTimeOffset` has no year 0). The vector file records the .NET expectation and a `note`
  naming the ruling (design "Tests, vectors and ports").
- **R12 — Test-class and file names.** Core tests: `Configuration/{DexpaceClientOptionsTests (existing), OptionsImmutabilityTests,
  TimeProviderWaitsTests (renamed from Internal/BlockingWaitTests), ProxyOptionsTests, ProxyGlobTests, ProxyUrlParserTests,
  NoProxyListTests, ProxyFromEnvironmentTests, NoImplicitProxyReadTests, BuildInfoTests}`; `Http/Common/HttpDateTests`;
  `Internal/{LateResultTests, DeepValueTests}`; `Pipeline/Policies/{RetryFactsTests (existing, extended), RetryPolicyTests
  (existing, extended)}`; `Pipeline/OriginalExceptionSurfaceTests`; `Recovery/IdempotencyKeyStepTests` (existing,
  extended); `Architecture/OptionsRecordArchitectureTests`. Vectors under `tests/vectors/config/` are copied to the test
  output by the existing `..\vectors\**\*.json` glob in `Dexpace.Sdk.Core.Tests.csproj`.

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no PR; no commit)

**Why first.** The design's facts 7–11 and the half of fact 9 not visible in the tree were never run (no SDK on the design
host). Several tasks below depend on the answers.

**Do.**

1. Run the five phase-start queries above and compare with the design's table ("The knowledge queries could not be run on this
   host"). Record every difference in a scratch note (`$SCRATCH/phase5a-preflight.md`); a difference that contradicts a ruling
   goes to the lead before PR 1 starts.
2. Create a throwaway console project **outside the repository** (scratchpad directory, `net10.0`, no package references) and
   print, for each fact:
   - **7** — `new Uri("http://proxy").Port`, `new Uri("http://proxy:80").IsDefaultPort`, `new Uri("http://proxy:80").Port`
     (expect `80`, `true`, `80`: neither can tell an explicit port from an absent one).
   - **8** — a sealed record with a `string` member whose `init` accessor also fills a second private field; `with` on an
     unrelated member must keep the second field, `with` on the member must recompute it.
   - **9** — a sealed record declaring `public override string ToString()`, a `private bool PrintMembers(StringBuilder)` and a
     `public bool Equals(T?)`; confirm it compiles with the analyzers' defaults and (by building core with a scratch
     `PublicAPI.Unshipped.txt` line removed) that `RS0016` lists exactly `<Clone>$`, `Equals(T?)`, the three overrides and the
     two operators, as `RequestOptions` shows.
   - **10** — `new Regex(@"\A" + Regex.Escape("*a*a*a*a*a*a*a*a*a*b").Replace(@"\*", ".*") + @"\z", NonBacktracking | IgnoreCase |
     CultureInvariant)` matched against 60 `a` characters (must return quickly); `\z` against `"host\n"` (no match);
     `.` against `"\n"` (no match). Then publish the same snippet NativeAOT (`PublishAot=true`) and run it: this decides whether
     task 4.2 keeps `Regex` or takes P5a-14's stated alternative (see 4.2).
   - **11** — `Environment.Version` and `RuntimeInformation.FrameworkDescription` under the AOT publish (expect `10.0.x` and
     `.NET 10.0.x`; the latter contains a space).
   - **Extra (plan-level)** — `DateTimeOffset.MinValue.UtcDateTime`/`MaxValue` round-trip through `ToString("r",
     InvariantCulture)` (used by task 3.1's boundary vectors); `Uri.UnescapeDataString("pa%ss")` (R7: expect `pa%ss`);
     `Task.Delay(TimeSpan.FromMilliseconds(-1), TimeProvider.System)` never completing and `-2 ms` throwing (fact 3).
3. Re-read, and record the line numbers of, the three as-built date sites (`SetDatePolicy.cs:52`, `RequestConditions.cs:138`,
   `RetryPolicy.cs` `ParseRetryAfter`), the two test sites that mutate options (`ClientIdentityPolicyTests.cs` around line
   103, `RetryPolicyTests.cs` around lines 588–590) and `grep -rn "\.Retry = \|\.Redirect = \|\.UserAgent = \|\.BaseAddress = "
   src tests docs README.md` for any other assignment (design fact 15 found only the two test sites; a third is a PR 1 task).

**Outcomes feed.** A fact that comes out the other way reopens the ruling that cites it (design "Facts the design rests on")
and stops the PR that depends on it. **Exit:** the scratch note exists; nothing is committed.

---

## PR 1 — The options records

**Gate: none.** Rows: `CFG-8`, `CFG-9`, `CFG-12` (N/A argued in the checklist), `PIPE-17`'s immutability citation. Files:
`src/Dexpace.Sdk.Core/Configuration/{DexpaceClientOptions,RetryOptions,RedirectOptions}.cs` (the as-built single file splits;
`Http/Request/` is the one-type-per-file precedent), `Operations/OperationDescriptor.cs` (docs only),
`Pipeline/HttpPipeline.cs` (docs only), `PublicAPI.Unshipped.txt`, and the tests named below.

### Task 1.1 — Failing tests: record shape, validation, derivation, redaction (`CFG-8`, `CFG-9`; P5a-3, P5a-4, P5a-5)

New `tests/Dexpace.Sdk.Core.Tests/Configuration/OptionsImmutabilityTests.cs`, class `OptionsImmutabilityTests`:

- `With_derives_a_copy_and_leaves_the_source_unchanged` (`CFG-9`; `options with { Retry = options.Retry with {
  MaxRetryAttempts = 5 } }`: the source's `Retry.MaxRetryAttempts` stays `3`, the copy's is `5`, the two `Retry` instances are
  distinct, the untouched `Redirect` is the same instance in both (shallow), `CFG-9`'s "copy-on-write over nested records")
- `Two_equal_options_are_equal_and_hash_alike` (value equality over every member, nested records included; was reference
  equality, Breaking 1)
- `A_differing_nested_member_makes_options_unequal`
- `Records_are_sealed_and_expose_init_only_properties` (reflection: `DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`
  are `IsSealed`; every public property's setter carries the `IsExternalInit` required modifier; the generic rule test is
  task 1.3, this one pins the three)
- `Nested_options_cannot_be_null` (`Retry = null!` and `Redirect = null!` throw `ArgumentNullException`, `ParamName` `value`)
- `UserAgent_rejects_null_and_accepts_blank` (null throws; `""` and `"  "` are accepted, because
  `ClientIdentityPolicyTests.A_blank_UserAgent_option_emits_no_header` depends on a blank meaning "emit nothing")

New in the existing `tests/Dexpace.Sdk.Core.Tests/Configuration/DexpaceClientOptionsTests.cs` (migrated in task 1.2; written
now):

- `BaseAddress_accepts_an_absolute_http_or_https_uri_without_a_fragment` (`[Theory]`: `http://h`, `https://h/v1`,
  `https://h:8443/a/b?x=1`; `null` is accepted and means "not set")
- `BaseAddress_rejects_what_BuildRequest_used_to_reject` (`[Theory]`, the three inputs moved from
  `OperationBuildRequestTests`: `/relative` (`UriKind.RelativeOrAbsolute`), `https://host/c#frag`, `ftp://host/`, plus the
  empty fragment `https://host/#`; each throws `ArgumentException` **from the object initializer**, and the message names
  the redacted address (assert it does not contain a `?sig=secret` query value when the input has one: use
  `https://host/c?sig=secret#f` as a fifth input)) (`P5a-4`, 2b's hand-off)
- `ToString_renders_the_members_and_redacts_the_base_address` (a base address `https://h/v1?sig=abc123` renders without
  `abc123`; the text contains `UserAgent`, `Retry` and `Redirect`; `BaseAddress` is rendered through `UrlRedactor`, never
  `Uri.ToString()`, which is banned on log paths, design §3.5) (`P5a-5`)
- `ToString_of_the_nested_records_renders_every_member` (`RetryOptions` and `RedirectOptions` use the synthesised
  `ToString`; both contain every property name)

Red: `with` does not compile against a class (CS8858), which is the expected compile-error red for the whole file; once the
`with` cases are stubbed out the rest fail on behaviour (equality is by reference, validation does not throw, `ToString`
calls the default).

**IDs:** `CFG-8`, `CFG-9`. **Verify:** V-fast `OptionsImmutabilityTests` and `DexpaceClientOptionsTests` (red).

### Task 1.2 — The records, the validation, the migrations (`CFG-8`, `CFG-9`; P5a-3, P5a-4, P5a-5) — Breaking 1, 2

**Production.**

- Split `Configuration/DexpaceClientOptions.cs` into three files (one record per file, styleguide 12):
  `DexpaceClientOptions.cs`, `RetryOptions.cs`, `RedirectOptions.cs`, each `public sealed record`, each property `{ get; init; }`,
  defaults unchanged (`MaxRetryAttempts = 3`, 200 ms, 30 s, `HonorRetryAfter = true`, `RetryNonIdempotentWhenReplayable =
  false`, `MaxRedirects = 20`, `AllowHttpsToHttpDowngrade = false`, `StripSensitiveHeadersOnCrossOrigin = true`). 6a and 6b
  own every value change; 5a changes the type kind, not the knobs (design position A).
- `DexpaceClientOptions` (R4: `field`-backed `init`, as `RequestOptions.Tags`):
  - `BaseAddress { get; init => field = RequireUsable(value); }`: `null` allowed; otherwise `IsAbsoluteUri`, scheme
    `http`/`https`, `Fragment.Length == 0` (an empty fragment `#` counts, design fact 5 of 2b), else `ArgumentException`
    whose message ends with `UrlRedactor.Default.Redact(value)` (the helper shape of `OperationUrlComposer.RequireUsableBase`;
    do not call it, it throws with `nameof(baseAddress)` as the parameter, and a property setter's parameter is `value`).
    Extract nothing public; if the two checks end up identical, the shared predicate is a private static in
    `DexpaceClientOptions` that `OperationUrlComposer` does **not** adopt (its text and parameter name differ).
  - `UserAgent` (`ThrowIfNull`, default the as-built `$"dexpace-dotnet/{SdkVersion.Value}"`; task 5.2 changes it),
    `Retry`, `Redirect` (`ThrowIfNull`, default `new()`), `OverallTimeout`, `AttemptTimeout` (unchecked: P5a-4 leaves numeric
    rules to 6a, 6b and phase 9).
  - `private bool PrintMembers(StringBuilder builder)` rendering `BaseAddress = <redacted or (none)>`, `UserAgent`, the two
    timeouts, then `Retry` and `Redirect` through their own `ToString()`; the synthesised `ToString` calls it.
- **`BuildRequest(DexpaceClientOptions)`** keeps its null check and its "BaseAddress is not set" `ArgumentException`; its
  `<exception>` doc drops "or fails the rules of `BuildRequest(Uri)`" for the options overload (R1) and says the address was
  validated when the options were built. `OperationUrlComposer.RequireUsableBase` is unchanged.
- **Migrate every as-built mutation site** (design fact 15, task 0.1 step 3):
  - `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/ClientIdentityPolicyTests.cs`, `The_option_is_read_per_call`: becomes
    `A_per_call_options_value_wins_over_the_captured_one` — the pipeline is built over `first/1`, the second call passes
    `options with { UserAgent = "second/2" }`; same two assertions on `transport.Requests[0|1]`. The behaviour pinned
    (the policy reads the call's options, not a snapshot of one instance) is unchanged; the mutation that proved it is
    no longer possible.
  - `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/RetryPolicyTests.cs`, `Sync_send_honours_cancellation_during_the_wait`:
    the three assignments fold into the initializer (`MakeOptions()` returns the record; the test builds
    `MakeOptions() with { Retry = new RetryOptions { BaseDelay = …, MaxDelay = …, HonorRetryAfter = false } }`, keeping
    `MakeOptions`' other retry fields: read it first and carry them over with `with { Retry = options.Retry with { … } }`).
    No assertion changes value.
  - `OperationBuildRequestTests.BuildRequest_from_options_applies_the_same_base_rules` is **deleted** and its three inputs live in
    `DexpaceClientOptionsTests` (task 1.1; R1). `BuildRequest_from_options_reads_BaseAddress` and
    `…throws_ArgumentException_when_BaseAddress_is_null` stay.
  - `DexpaceClientOptionsTests.DexpaceClientOptions_RetryAndRedirect_AreNonNullOnFreshInstance`: the comment "so callers can do
    `opts.Retry.MaxRetryAttempts = 5`" is replaced with the `with` form.
- `src/Dexpace.Sdk.Core/README.md` line ~40 (`new DexpaceClientOptions { OverallTimeout = … }`) already compiles; the options
  sample gets its `with` example in task 6.2, not here.
- XML docs: every record's `<remarks>` states the rule (sealed record, `init`, derive with `with`), the three **Breaking**
  notes (setters became `init`; equality by value; `BaseAddress` validated when set, `ArgumentException` there not at
  `BuildRequest`; `UserAgent`/`Retry`/`Redirect` reject null), and `BaseAddress`'s old "checked at `BuildRequest`" sentence
  is replaced.

**`PublicAPI.Unshipped.txt`** (the analyzer's code fix produces these; compare). **Removed (14 lines):** every `.set -> void`
under `DexpaceClientOptions` (`AttemptTimeout`, `BaseAddress`, `OverallTimeout`, `Redirect`, `Retry`, `UserAgent`),
`RetryOptions` (`BaseDelay`, `HonorRetryAfter`, `MaxDelay`, `MaxRetryAttempts`, `RetryNonIdempotentWhenReplayable`) and
`RedirectOptions` (`AllowHttpsToHttpDowngrade`, `MaxRedirects`, `StripSensitiveHeadersOnCrossOrigin`). **Added**, for each
`T` of the three (`C.` = `Dexpace.Sdk.Core.Configuration.`):

```text
C.T.<each property>.init -> void
C.T.<Clone>$() -> C.T!
C.T.Equals(C.T? other) -> bool
override C.T.Equals(object? obj) -> bool
override C.T.GetHashCode() -> int
override C.T.ToString() -> string!
static C.T.operator !=(C.T? left, C.T? right) -> bool
static C.T.operator ==(C.T? left, C.T? right) -> bool
```

The `.get` lines and the three default constructors are unchanged.

**IDs:** `CFG-8`, `CFG-9`. **Verify:** V-fast `OptionsImmutabilityTests`, `DexpaceClientOptionsTests`,
`OperationBuildRequestTests`, `ClientIdentityPolicyTests`, `RetryPolicyTests`; then the whole `Dexpace.Sdk.Core.Tests`
project (every object initializer compiles against `init`; the `Security` classes build unchanged, design fact 15).

### Task 1.3 — The rule, enforced by architecture test (`CFG-8`; P5a-3, P5a-28)

New `tests/Dexpace.Sdk.Core.Tests/Architecture/OptionsRecordArchitectureTests.cs`, class `OptionsRecordArchitectureTests`
(header comment cites the design's rule A.1–A.5 and that 5b's `HttpLoggingOptions` is bound by it):

- `Every_record_in_the_Configuration_namespace_is_sealed` (reflection over `typeof(DexpaceClientOptions).Assembly`, namespace
  `Dexpace.Sdk.Core.Configuration`; a record is recognised by its compiler-generated `<Clone>$` method)
- `No_public_property_in_the_Configuration_namespace_has_a_public_setter` (every public instance property of every public
  type in the namespace: a setter is allowed only if its return parameter carries the `IsExternalInit` required
  modifier; static properties (`BuildInfo.*`, later) have no setter at all)
- `No_public_collection_member_is_a_mutable_collection_type` (every public property type in the namespace is not
  `List<>`, `Dictionary<,>`, `HashSet<>`, an array, `ICollection<>`, `IList<>` or `IDictionary<,>`; `IReadOnlyList<>` and
  `IReadOnlyCollection<>` pass; this is rule A.3 for `ProxyOptions.NonProxyHosts` and 5b's list)
- `The_scanner_recognises_a_public_setter_a_List_and_an_array` (three private nested fixture types in the test class, one per
  violation, each flagged by the same predicate methods: proves the scanner can fail)

Before PR 4 and PR 5 land, the namespace holds only the three options records, so the tests pass on the first run for the
right reason (a pin, proven by the scanner test). **IDs:** `CFG-8`. **Verify:** V-fast `OptionsRecordArchitectureTests`.

### Task 1.4 — The per-call overloads: remarks and a pin (`PIPE-17`; P5a-6)

**Test first (a pin).** In `tests/Dexpace.Sdk.Core.Tests/Pipeline/HttpPipelineTests.cs`:
`A_per_call_DexpaceClientOptions_overrides_the_captured_options_for_that_call_only` (a pipeline built over
`new DexpaceClientOptions { UserAgent = "built/1" }` with `ClientIdentityPolicy`; `SendAsync(request, options with {
UserAgent = "call/2" }, token)` sends `call/2`; the next plain `SendAsync` sends `built/1`; the sync `Send` twin likewise).
Passes before the change; proven able to fail by making `HttpPipeline` ignore the per-call options (never committed).

**Production (docs only).** `Pipeline/HttpPipeline.cs` remarks, "Two kinds of options": replace "phase 5a, which makes that
record immutable, decides their future" with the rule: *the overloads taking a `DexpaceClientOptions` run one call with that
immutable value; derive it with `with`. `RequestOptions` remains the seam's per-call carrier (timeout, retry cap, tags).*
Add the dated line "(phase 5a, P5a-6)". No `PublicAPI` change.

**IDs:** `PIPE-17` (cited). **Verify:** V-fast `HttpPipelineTests`, `OptionsFlowTests`.

### Task 1.5 — Close-out (PR 1)

`CHANGELOG.md` `### Changed`: the options are sealed records (**Breaking**, source and behaviour: post-construction assignment
no longer compiles, use `with`; equality and `GetHashCode` by value; `ToString` renders the members with the base address
redacted) and `BaseAddress` is validated when set (**Breaking**, behaviour: `ArgumentException` at the assignment, not at
`BuildRequest`; `UserAgent`, `Retry` and `Redirect` reject `null`). Run **V-gate**. **Commit:** `feat!: phase 5a options records
(CFG-8, CFG-9)`.

---

## PR 2 — The clock, the waits and the late result

**Gate: none; `RetryPacingOverflowTests` runs first** (task 2.1). Rows: `CFG-15`–`CFG-21` (`CFG-20` 🚫 in the checklist),
`XCUT-3`, `RETRY-26`, `ASYNC-5`, `TRANSPORT-9` (cited). Files: `src/Dexpace.Sdk.Core/Configuration/TimeProviderWaits.cs` (new;
`Internal/BlockingWait.cs` is `git mv`ed into it), `Internal/LateResult.cs` (new), `Pipeline/Policies/RetryPolicy.cs`,
`/home/mohammad/Projects/dotnet-sdk/BannedSymbols.txt`, `PublicAPI.Unshipped.txt`, tests under `Configuration/`, `Internal/`
and `Pipeline/`.

### Task 2.1 — `TimeProviderWaits.Sleep`, taking over `BlockingWait` (`CFG-15`, `CFG-16`, `CFG-17`; P5a-7, R2)

**Baseline first.** Run `dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class
"*RetryPacingOverflowTests"` and the `BlockingWaitTests` class; both are green at this point (the baseline the later runs
compare to).

**Failing tests first.** `git mv tests/Dexpace.Sdk.Core.Tests/Internal/BlockingWaitTests.cs
tests/Dexpace.Sdk.Core.Tests/Configuration/TimeProviderWaitsTests.cs`, class `TimeProviderWaitsTests` (namespace
`Dexpace.Sdk.Core.Tests.Configuration`), every `BlockingWait.Wait(d, clock, token)` call becoming `clock.Sleep(d, token)`.
Kept cases (renamed to the new shape, no assertion changed): `Waits_the_requested_time_on_a_fake_clock`,
`Cancels_when_the_token_is_cancelled`, `Chunks_a_wait_longer_than_49_days` (the 365-day, ≥ 8 timers case; the recording
`RecordingClock` double stays). Changed and new:

- `A_zero_wait_returns_at_once_without_arming_a_timer` (`[Fact]`, replacing the zero row; a `RecordingClock` sees no
  `CreateTimer`; `CFG-17`'s "zero returns")
- `A_negative_delay_is_rejected` (`[Theory]`: `-1 ms`, `-5 s`, `Timeout.InfiniteTimeSpan`; `ArgumentOutOfRangeException`,
  `ParamName` `delay`; this is the row R2 reverses; fact 3: `-1 ms` is "infinite" to the BCL)
- `A_null_time_provider_is_rejected` (`ArgumentNullException`, `ParamName` `timeProvider`; called as
  `TimeProviderWaits.Sleep(null!, …)` because an extension on a null receiver)
- `An_already_cancelled_token_throws_before_any_timer_even_for_a_zero_delay` (`OperationCanceledException` whose
  `CancellationToken` equals the caller's; no timer armed; matches Node's "honors an already-aborted signal ahead of the
  zero-duration path")
- `Cancellation_surfaces_with_the_callers_token_and_leaves_it_signalled` (`CFG-17`'s "re-assert": the caught exception's
  `CancellationToken == cts.Token` and `cts.Token.IsCancellationRequested` is still `true`)
- `A_sub_millisecond_delay_is_passed_to_the_timer_unrounded` (a `RecordingClock` records `TimeSpan.FromTicks(5)` as the due
  time; `CFG-17`'s SHOULD, nothing rounds)

Red: CS0103/CS1061 (`Sleep` does not exist), then the negative and null cases fail on behaviour.

**Production.**

- `git mv src/Dexpace.Sdk.Core/Internal/BlockingWait.cs src/Dexpace.Sdk.Core/Configuration/TimeProviderWaits.cs`; namespace
  `Dexpace.Sdk.Core.Configuration`; `public static class TimeProviderWaits` with the two extension methods of the design's
  position B. The class doc cites `CFG-15`, `CFG-17`, `CFG-18`, design §8.3, and says it replaces the internal wait.
  `MaxSingleWait` stays `internal`.
- `Sleep(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken)`:

  ```csharp
  ArgumentNullException.ThrowIfNull(timeProvider);
  RequireNonNegative(delay);                 // AOORE for < 0, including Timeout.InfiniteTimeSpan
  cancellationToken.ThrowIfCancellationRequested();   // before any timer, even at zero
  while (delay > TimeSpan.Zero) { …the as-built chunk loop and WaitOnce, unchanged… }
  ```

  Two private helpers keep each method under the `MA0051` cap (`RequireNonNegative`, `WaitOnce`); `WaitOnce` is the as-built
  body verbatim, including the `ObjectDisposedException` comment.
- XML docs on both extension methods (per the design: parameters, `<exception>` lists, the chunking remark, "blocks only the
  caller's thread, which is what a sync call asked for (§11 item 1)"). `DelayAsync` is added in task 2.2; the class compiles
  with `Sleep` alone in the meantime. Because `git mv` removes `BlockingWait`, this task also repoints every remaining
  `BlockingWait` reference in `RetryPolicy.cs` (see Verify), so the commit builds green.

**`PublicAPI.Unshipped.txt`:**

```text
Dexpace.Sdk.Core.Configuration.TimeProviderWaits
static Dexpace.Sdk.Core.Configuration.TimeProviderWaits.Sleep(this System.TimeProvider! timeProvider, System.TimeSpan delay, System.Threading.CancellationToken cancellationToken) -> void
```

**IDs:** `CFG-15`, `CFG-16` (the seam half), `CFG-17`. **Verify:** V-fast `TimeProviderWaitsTests`, then the `RetryPolicy`
call site still compiles because `RetryPolicy.SleepAsync` is switched in task 2.3 (this task temporarily updates its one
sync call, `BlockingWait.Wait(delay, _timeProvider, token)` → `_timeProvider.Sleep(delay, token)`, and repoints the async
branch's two `BlockingWait.MaxSingleWait` reads (RetryPolicy.cs ~line 267) to `TimeProviderWaits.MaxSingleWait`, leaving the
async chunk loop otherwise alone until task 2.3 replaces it; `git grep -n BlockingWait src tests` is empty at the end of this
task, so the commit is green).

### Task 2.2 — `TimeProviderWaits.DelayAsync` (`CFG-18`; P5a-7)

**Failing tests first.** In `TimeProviderWaitsTests`:

- `DelayAsync_completes_when_the_fake_clock_advances` (`FakeTimeProvider`; the task is pending before `Advance`, complete
  after)
- `DelayAsync_of_zero_completes_synchronously_without_a_timer` (`Task.IsCompletedSuccessfully` on return; a `RecordingClock`
  saw no `CreateTimer`)
- `DelayAsync_rejects_a_negative_delay_and_infinite_before_returning_a_task` (`[Theory]` as `Sleep`; the exception is thrown
  by the call itself, not stored in a faulted task; closes the `-1 ms` = infinite trap, fact 3)
- `DelayAsync_rejects_a_null_provider`
- `DelayAsync_with_an_already_cancelled_token_returns_a_cancelled_task_without_a_timer`
- `DelayAsync_cancelled_mid_wait_disposes_the_timer` (a `RecordingClock` double whose timers count disposals; after the
  cancel, one timer was created and one disposed; `CFG-18`'s "cancellation disposes the timer")
- `DelayAsync_chunks_a_delay_above_49_days` (365 days, ≥ 8 timers, each ≤ 49 days, sum 365 days; the async twin of the
  `Sleep` chunk test)

Red: CS1061 (`DelayAsync` does not exist).

**Production.** `DelayAsync` validates synchronously (null provider, negative), returns `Task.CompletedTask` for zero when
the token is not signalled, `Task.FromCanceled(token)` when it is, and otherwise delegates to a private
`static async Task DelayChunksAsync(…)` that loops `await Task.Delay(chunk, timeProvider, cancellationToken)
.ConfigureAwait(false)` over `MaxSingleWait` slices. **The one `#pragma warning disable RS0030` for `Task.Delay`** surrounds
that single call, with the comment *"CFG-17, CFG-18, design §8.3: the one sanctioned Task.Delay site; the guard above rejects
the -1 ms infinite trap and this loop chunks past ~49.7 days"* (the ban entries land in task 2.5; the pragma is harmless
before it). XML docs as `Sleep`'s, plus `<returns>`.

**`PublicAPI.Unshipped.txt`:**

```text
static Dexpace.Sdk.Core.Configuration.TimeProviderWaits.DelayAsync(this System.TimeProvider! timeProvider, System.TimeSpan delay, System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.Task!
```

**IDs:** `CFG-18`. **Verify:** V-fast `TimeProviderWaitsTests`.

### Task 2.3 — `RetryPolicy` on `TimeProviderWaits` (`CFG-15`, `CFG-18`; `RETRY-26`, `XCUT-3`; P5a-7)

**Pin first.** `RetryPacingOverflowTests` (`Security`, unedited), the `RetryPolicyTests` class and `TimeProviderWaitsTests`
all pass before the edit. Add to `RetryPolicyTests`: `The_sync_and_async_waits_use_the_policys_TimeProvider_and_the_same_delay`
(a `FakeTimeProvider` plus a recording wrapper: with `HonorRetryAfter = true` and `Retry-After: 7`, both
`pipeline.Send` and `pipeline.SendAsync` arm exactly one timer of `7 s`; a pin, fact 13).

**Production.** In `RetryPolicy.cs`:

- `SleepAsync(TimeSpan delay, bool async, CancellationToken token)` becomes: `if (delay <= TimeSpan.Zero) { return; }`
  then `async ? _timeProvider.DelayAsync(delay, token).ConfigureAwait(false) : _timeProvider.Sleep(delay, token)`; the
  private chunk loop and the `BlockingWait.MaxSingleWait` reads are deleted (`delay <= 0` stays a skip so a hinted zero does
  not arm a timer, behaviour unchanged, fact 12).
- The class remarks and the constructor's `<param>` stop saying "`Task.Delay(TimeSpan, TimeProvider, CancellationToken)`"
  (crefs to a symbol the next task bans) and name `TimeProviderWaits` instead. The same cref edit in
  `Pipeline/DexpacePipeline.cs` line ~22 ("The async backoff is …"). Doing it here keeps task 2.5 a one-file change.
- `ParseRetryAfter` is **not** touched here (PR 3).

**IDs:** `CFG-15`, `CFG-18`; cites `RETRY-26`, `XCUT-3`. **Verify:** V-fast `RetryPolicyTests`, then **the Security
category** (`RetryPacingOverflowTests` first), then `DexpacePipelineTests`.

### Task 2.4 — `LateResult` (`CFG-21`; `ASYNC-5`, `TRANSPORT-9`; P5a-9, R3)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/LateResultTests.cs`, class `LateResultTests` (a
`TrackingDisposable` private nested class counting `Dispose` calls, optionally throwing from it):

- `A_result_delivered_after_cancellation_is_disposed_exactly_once` (`TaskCompletionSource<TrackingDisposable>`; `WaitOrDisposeAsync`
  throws `OperationCanceledException` when the token fires first; completing the source afterwards disposes the result once;
  completing it a second time is impossible, so the count is `1` after a short `await` of a completion marker; `CFG-21`)
- `A_result_delivered_before_cancellation_is_returned_and_never_disposed`
- `A_late_result_whose_dispose_throws_is_swallowed` (the non-fatal exception does not escape, does not become an unobserved task
  exception, and does not mask the caller's `OperationCanceledException`)
- `A_faulted_late_task_is_observed_and_raises_no_UnobservedTaskException` (subscribe to `TaskScheduler.UnobservedTaskException`
  in a scoped handler; fault the source after cancellation; run `GC.Collect()` / `GC.WaitForPendingFinalizers()` twice;
  the handler count stays `0`; proven able to fail by removing the catch in the helper)
- `A_cancelled_late_task_is_ignored` (the source ends `TrySetCanceled`; nothing is disposed, nothing is thrown)
- `DisposeWhenCompleted_disposes_a_late_result_without_waiting` (the second entry point; also on an already-completed task:
  disposed once)
- `Cancellation_before_the_call_throws_and_still_disposes_a_later_result` (an already-cancelled token)
- `A_result_completed_between_the_token_firing_and_the_catch_is_still_disposed` (the F1 race, made deterministic: the source
  is completed from inside a `token.Register` callback, so the task is already successfully completed when the filter runs;
  the result is disposed once and the `OperationCanceledException` still reaches the caller)
- `A_late_null_result_is_ignored_without_throwing` (`CFG-21`'s "null-safe": the source completes with `null` after cancellation;
  nothing is thrown, nothing is observed unhandled; `Disposal.DisposeQuietly(null)` is the null-safe path)

Red: CS0103 (`LateResult` does not exist).

**Production.** New `src/Dexpace.Sdk.Core/Internal/LateResult.cs`, `internal static class LateResult` with the two signatures
of design position B:

```csharp
internal static async Task<T> WaitOrDisposeAsync<T>(Task<T> task, CancellationToken cancellationToken) where T : class, IDisposable
{
    ArgumentNullException.ThrowIfNull(task);
    try
    {
#pragma warning disable RS0030 // SEAM-30, design §8.3: the one sanctioned Task<T>.WaitAsync site; a late result is disposed below.
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0030
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        DisposeWhenCompleted(task);
        throw;
    }
}

internal static void DisposeWhenCompleted<T>(Task<T> task) where T : class, IDisposable
{
    ArgumentNullException.ThrowIfNull(task);
    _ = DisposeLateAsync(task);
}
```

`DisposeLateAsync` is a private `async Task` that `await`s the task with `ConfigureAwait(false)`, passes the result to
`Disposal.DisposeQuietly(result)`, and swallows `catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))` (a faulted or
cancelled late task is observed here; R3). The `catch` filter on `WaitOrDisposeAsync` tests **only** the caller's token, never `task.IsCompleted`: the
abandoned task may complete between `WaitAsync` throwing and the filter running, and a filter that skipped disposal then would
leak the `Response` (the race ASYNC-5 assigns to whoever loses it). `DisposeWhenCompleted` already copes with a task that is
pending, completed successfully, faulted or cancelled, so it runs on every cancellation exit; the `OperationCanceledException`
reaches the caller either way, and a task that itself ended cancelled with the token *not* signalled is not caught at all
(the filter is false) and surfaces unchanged. XML
docs state "exactly once: one awaiter per abandonment, and `Response`'s dispose is latched (3b)" and that no as-built caller
needs it today (`AsAsync` already disposes a response produced after cancellation inside its worker, 2b); the consumers are
6a and 8b. **Internal: no `PublicAPI` change.**

`BannedSymbols.txt` reason lines for the `WaitAsync` and `TaskCompletionSource<T>.SetResult`/`TrySetResult` entries gain "or go
through `LateResult`" (task 2.5 edits the file once for everything; this task writes the text, 2.5 lands it).

**IDs:** `CFG-21`; cites `ASYNC-5`, `TRANSPORT-9`. **Verify:** V-fast `LateResultTests`; `git grep -n "WaitAsync" src` lists
`LateResult.cs` (the pragma) and the two doc mentions only.

### Task 2.5 — The `RS0030` entries (`CFG-15`, `CFG-16`, `CFG-28`; P5a-8)

**Mechanism, not a test.** Edit `/home/mohammad/Projects/dotnet-sdk/BannedSymbols.txt` (additions only; no line is deleted;
only the seven `WaitAsync`/`SetResult` reason lines gain "or go through `LateResult`"). Under a new comment block "Clock,
delay and environment discipline (CFG-15, CFG-16, CFG-17, CFG-18, CFG-28; design §8.3, P5a-8)":

```text
M:System.Threading.Thread.Sleep(System.Int32);Not cancellable and no fake clock; use TimeProviderWaits.Sleep (CFG-15, design §8.3)
M:System.Threading.Thread.Sleep(System.TimeSpan);Not cancellable and no fake clock; use TimeProviderWaits.Sleep (CFG-15, design §8.3)
M:System.Threading.Tasks.Task.Delay(System.Int32);-1 ms waits forever and no overload chunks past ~49.7 days; use TimeProviderWaits.DelayAsync (CFG-17, CFG-18, design §8.3)
M:System.Threading.Tasks.Task.Delay(System.Int32,System.Threading.CancellationToken);<same reason>
M:System.Threading.Tasks.Task.Delay(System.TimeSpan);<same reason>
M:System.Threading.Tasks.Task.Delay(System.TimeSpan,System.Threading.CancellationToken);<same reason>
M:System.Threading.Tasks.Task.Delay(System.TimeSpan,System.TimeProvider);<same reason>
M:System.Threading.Tasks.Task.Delay(System.TimeSpan,System.TimeProvider,System.Threading.CancellationToken);<same reason>
P:System.DateTime.Now;Wall clock outside the seam; use TimeProvider.GetUtcNow or GetTimestamp (CFG-15, CFG-16)
P:System.DateTime.UtcNow;<same>
P:System.DateTime.Today;<same>
P:System.DateTimeOffset.Now;<same>
P:System.DateTimeOffset.UtcNow;<same>
M:System.Environment.GetEnvironmentVariable(System.String);Nothing reads the environment implicitly; ProxyOptions.FromEnvironment is the one site (CFG-28)
M:System.Environment.GetEnvironmentVariable(System.String,System.EnvironmentVariableTarget);<same>
M:System.Environment.GetEnvironmentVariables;<same>
M:System.Environment.GetEnvironmentVariables(System.EnvironmentVariableTarget);<same>
```

(The `<same>` markers stand for the full reason text; write it out in the file.) `Stopwatch` is **not** banned (P5a-8: monotonic;
`InstrumentationPolicy` is 5c's file).

**Sanctioned sites.** `TimeProviderWaits.DelayAsync` (task 2.2's pragma); none for the clock bans (no site today, task 0.1
`grep` found none); the `Environment` site arrives in PR 4 (task 4.6). Any `cref` to a newly banned symbol that the analyzer
flags is rewritten to `<c>` text in the same commit (task 2.3 already did the known ones).

**Negative checks (run, never committed).** One at a time, add to a scratch `src/Dexpace.Sdk.Core/Internal/Scratch.cs`:
`Thread.Sleep(1);`, `_ = Task.Delay(1);`, `_ = DateTime.UtcNow;`, `_ = DateTimeOffset.Now;`,
`_ = Environment.GetEnvironmentVariable("X");` and see `RS0030` fail the build with the reason line; delete the file.

**IDs:** `CFG-15`, `CFG-16`, `CFG-28` (prohibitive half). **Verify:** `dotnet build Dexpace.Sdk.sln --configuration Release`
(green with the pragmas; the five negative checks red).

### Task 2.6 — `CFG-19`: the original exception surfaces (`CFG-19`; P5a-10)

**Test (a pin).** New `tests/Dexpace.Sdk.Core.Tests/Pipeline/OriginalExceptionSurfaceTests.cs`, class
`OriginalExceptionSurfaceTests`: `A_transport_failure_surfaces_as_the_original_exception_through_Send_and_SendAsync` (`[Theory]`
over `IOException`, `InvalidOperationException`, `TimeoutException`; the transport is
`DelegateHttpClient.Create`/`CreateBlocking` throwing it; a bare `new PipelineBuilder().Build(transport)` pipeline; asserts
`Assert.Throws<T>` on `Send` and `Assert.ThrowsAsync<T>` on `SendAsync` (exact type, so an `AggregateException` wrapper
fails), and that the caught exception is the thrown instance, `Assert.Same`). No production change: `await` and a completed
`ValueTask`'s `GetAwaiter().GetResult()` rethrow the original (§8.3), and `.Result`/`.Wait()` are banned. Proven able to fail by
temporarily wrapping the transport's exception in `AggregateException` inside the double (never committed).

**IDs:** `CFG-19`; `CFG-20` has no code (🚫, `cooperative-cancellation`, design §10 entry 8: the checklist cites it).
**Verify:** V-fast `OriginalExceptionSurfaceTests`.

### Task 2.7 — Close-out (PR 2)

`CHANGELOG.md` `### Added`: `TimeProviderWaits.Sleep` and `DelayAsync` (the sync and async waits over `TimeProvider`; negative
delays rejected); `### Changed`: the new banned APIs for `src/` (internal gate, no consumer-visible change). Run **V-gate**
and the coverage gate with its self-test; run `RetryPacingOverflowTests` explicitly and compare to task 2.1's baseline.
**Commit:** `feat: phase 5a time provider waits and the late-result helper (CFG-15..CFG-21)`.

---

## PR 3 — RFC 1123 dates

**Gate: none; `RetryPacingOverflowTests` runs first** (task 3.3). Rows: `CFG-29`, `CFG-30`, `CFG-31`; `RETRY-15` (date clause,
cited). Files: `src/Dexpace.Sdk.Core/Http/Common/HttpDate.cs` (new), `Pipeline/Policies/{SetDatePolicy,RetryPolicy}.cs`,
`Http/Request/RequestConditions.cs`, `tests/vectors/config/http-date.json` (new), `tests/Dexpace.Sdk.Core.Tests/Http/Common/HttpDateTests.cs`
(new), `PublicAPI.Unshipped.txt`.

### Task 3.1 — Vectors and `Format` (`CFG-29`; P5a-11)

**Vectors.** New `tests/vectors/config/http-date.json`, `{ "source": "nodejs-sdk@54aeed4
packages/core/src/config/http-date.test.ts; ruby-sdk phase 5a design (RFC 1123 case list); design §8.2 and P5a-11",
"cases": [ … ] }`, each case `{ "name", "input", "expected" (UTC ISO-8601 or null), "note"? }`. Format cases are asserted in
code (they are a handful), parse cases live in the file:

| Case | Input | Expected | Note |
|---|---|---|---|
| canonical | `Thu, 01 Jan 2026 00:00:10 GMT` | `2026-01-01T00:00:10Z` | |
| the RFC example | `Sun, 06 Nov 1994 08:49:37 GMT` | `1994-11-06T08:49:37Z` | |
| upper-case month | `Thu, 01 JAN 2026 00:00:10 GMT` | same | `CFG-30` |
| lower-case month | `Thu, 01 jan 2026 00:00:10 GMT` | same | `CFG-30` |
| lower-case zone | `Thu, 01 Jan 2026 00:00:10 gmt` | same | superset of `CFG-30` (P5a-11) |
| `UTC` zone | `… 00:00:10 UTC` | same | `CFG-30` |
| `+0000` zone | `… 00:00:10 +0000` | same | `CFG-30` |
| `+00:00` zone | `… 00:00:10 +00:00` | same | `CFG-30` |
| contradicting weekday | `Mon, 01 Jan 2026 00:00:10 GMT` | same | weekday informational (`CFG-30`) |
| single-digit day | `Thu, 1 Jan 2026 00:00:10 GMT` | same | `RETRY-15`; Ruby rejects (note: P5a-11) |
| blank | `` | null | `CFG-31` |
| whitespace only | `   ` | null | `CFG-31` |
| missing comma | `Mon 01 Jan 2024 00:00:00 GMT` | null | `CFG-31` |
| surrounding whitespace | `  Thu, 01 Jan 2026 00:00:10 GMT  ` | null | note: Node tolerates; design: no trimming (R11) |
| leap second | `Thu, 01 Jan 2026 00:00:60 GMT` | null | note: Node rolls over; design rejects `60` (R11) |
| day 32 | `Thu, 32 Jan 2026 00:00:10 GMT` | null | |
| 31 February | `Thu, 31 Feb 2026 00:00:10 GMT` | null | |
| hour 24 | `Thu, 01 Jan 2026 24:00:10 GMT` | null | |
| unknown month | `Thu, 01 Foo 2026 00:00:10 GMT` | null | |
| non-zero offset | `Thu, 01 Jan 2026 00:00:10 +0100` | null | |
| two-digit year | `Thu, 01 Jan 26 00:00:10 GMT` | null | four digits required |
| year 0026 | `Sun, 01 Jan 0026 00:00:00 GMT` | `0026-01-01T00:00:00Z` | read literally |
| year 0000 | `Sat, 01 Jan 0000 00:00:00 GMT` | null | note: Node accepts; `DateTimeOffset` has no year 0 (R11) |
| RFC 850 | `Sunday, 06-Nov-94 08:49:37 GMT` | null | §11 item 27 |
| asctime | `Sun Nov  6 08:49:37 1994` | null | §11 item 27 |
| double space | `Thu,  01 Jan 2026 00:00:10 GMT` | null | one space only |
| trailing junk | `Thu, 01 Jan 2026 00:00:10 GMT x` | null | end of input |
| embedded CR/LF | `Thu, 01 Jan 2026 00:00:10 GMT\r\n` | null | |
| non-ASCII digit | `Thu, 0١ Jan 2026 00:00:10 GMT` | null | culture/digit safety |
| the 9999 boundary | `Fri, 31 Dec 9999 23:59:59 GMT` | `9999-12-31T23:59:59Z` | the S7 case |

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Http/Common/HttpDateTests.cs`, class `HttpDateTests`, with a
private `DateCase(string Name, string Input, string? Expected, string? Note)` record for `VectorFile.Load<DateCase>("config/http-date.json")`.
This task writes the `Format` half:

- `Format_renders_the_canonical_form_in_UTC` (`1994-11-06T08:49:37Z` → `Sun, 06 Nov 1994 08:49:37 GMT`)
- `Format_zero_pads_a_single_digit_day` (`2026-01-01` → `Thu, 01 Jan 2026 00:00:00 GMT`; `CFG-29`)
- `Format_converts_a_non_UTC_offset_to_the_UTC_instant` (`2026-01-01T02:00:00+02:00` → `… 00:00:00 GMT`)
- `Format_ignores_the_current_culture` (a `[Theory]` over `ar-SA`, `th-TH`, `tr-TR`, `fa-IR`: the output is identical, run
  under `CultureInfo.CurrentCulture` swapped in a `try`/`finally` inside the test; `CFG-29`'s literal `GMT` and Latin digits)
- `Format_renders_the_outermost_instants` (year 0001 and `9999-12-31T23:59:59Z`; includes a `+14:00`/`-12:00` offset instant
  at each boundary: confirms `UtcDateTime` never throws, task 0.1 extra)

Red: CS0103 (`HttpDate` does not exist).

**Production.** New `src/Dexpace.Sdk.Core/Http/Common/HttpDate.cs`, `public static class HttpDate`;
`Format(DateTimeOffset instant) => instant.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)`. The class remarks carry
the grammar summary, cite `CFG-29`–`CFG-31`, `RETRY-15`, design §8.2 and §11 item 27, and say why neither BCL route works
(fact 2). `Parse`/`TryParse` land in task 3.2; until then the class has `Format` alone (a green commit).

**`PublicAPI.Unshipped.txt`:** `Dexpace.Sdk.Core.Http.Common.HttpDate` and `static Dexpace.Sdk.Core.Http.Common.HttpDate.Format(System.DateTimeOffset instant) -> string!`.

**IDs:** `CFG-29`. **Verify:** V-fast `HttpDateTests`.

### Task 3.2 — `Parse` and `TryParse` (`CFG-30`, `CFG-31`; P5a-11)

**Failing tests first.** In `HttpDateTests`:

- `TryParse_matches_every_vector` (`[Theory]` over `VectorFile`; `expected` null → `false` and `default`; otherwise the instant
  equals `DateTimeOffset.Parse(expected, InvariantCulture, AssumeUniversal)` with `Offset == TimeSpan.Zero`)
- `Parse_throws_FormatException_for_every_rejected_vector` and `Parse_returns_the_instant_for_every_accepted_vector`
- `Parse_error_never_echoes_the_input` (the message of the `FormatException` for `"secret-token-123"` does not contain it; the
  message contains the example form; `HTTP-20`'s rule applied to a header value)
- `Parse_rejects_null_with_ArgumentNullException` and `TryParse_accepts_null_returning_false`
- `TryParse_never_throws_for_arbitrary_text` (a seeded `Random(5140)` generating 20 000 strings from an alphabet of letters,
  digits, `,: +-/\r\n\t`, month and zone fragments, lengths 0–40; none throws; the seed is a constant so a failure replays)
- `Format_then_TryParse_round_trips_every_second_precision_instant` (a seeded sample of 20 000 instants from `0001-01-01` to
  `9999-12-31T23:59:59Z` plus the two boundaries; `TryParse(Format(x))` returns `x`)
- `Parse_is_independent_of_the_current_culture` (the accepted vectors parse identically under `ar-SA` and `th-TH`; digits are
  read digit by digit, not through `int.Parse`)
- `A_single_digit_day_is_accepted_and_a_three_digit_day_is_not` (`RETRY-15`; `Thu, 100 Jan …` null)

Red: CS0117 (`HttpDate.Parse`/`TryParse` do not exist), then the vector cases.

**Production.** `TryParse(string? value, out DateTimeOffset instant)` returns `false` for null and otherwise calls a private
`TryParseCore(ReadOnlySpan<char>, out DateTimeOffset)`; `Parse(string value)` throws `ArgumentNullException` for null and
`FormatException("The value is not an HTTP-date; expected the RFC 1123 form, for example 'Sun, 06 Nov 1994 08:49:37 GMT'.")`
otherwise. The grammar is the design's position C, applied with **no trimming or normalisation**:

```text
weekday  = 3 ASCII letters then ", "          (matched and discarded, never compared)
day      = 1*2 DIGIT ; SP ; month = 3 letters, ASCII case-insensitive against Jan..Dec ; SP ; year = 4 DIGIT ; SP
time     = 2DIGIT ":" 2DIGIT ":" 2DIGIT       (00-23, 00-59, 00-59; 60 rejected)
zone     = SP ("GMT" / "UTC" ASCII case-insensitive / "+0000" / "+00:00") ; then end of input
```

Structure (each method ≤ 70 lines, `MA0051`): a private `ref struct` cursor over the span (`TryTakeLetters(int count)`,
`TryTakeDigits(int min, int max, out int value)`, `TryTake(char)`, `AtEnd`), then `TryReadWeekday`, `TryReadDay`,
`TryReadMonth`, `TryReadYear`, `TryReadTime`, `TryReadZone` and `TryAssemble(year, month, day, hour, minute, second, out
DateTimeOffset)` that checks `year >= 1`, `day <= DateTime.DaysInMonth(year, month)` and constructs with
`new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)`. A digit is `c is >= '0' and <= '9'` (never
`char.IsDigit`, which accepts other scripts); a letter is `c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')`; the month and
zone folds use `(c | 0x20)` on ASCII letters (the internal `AsciiFold` helper works on `string`, so it is not reused on a
span). Every failure path is `return false`; no exception is used for control flow, so `TryParse` cannot throw.

**`PublicAPI.Unshipped.txt`:**

```text
static Dexpace.Sdk.Core.Http.Common.HttpDate.Parse(string! value) -> System.DateTimeOffset
static Dexpace.Sdk.Core.Http.Common.HttpDate.TryParse(string? value, out System.DateTimeOffset instant) -> bool
```

**IDs:** `CFG-30`, `CFG-31`. **Verify:** V-fast `HttpDateTests`.

### Task 3.3 — The three as-built date sites move onto it (`CFG-29`, `CFG-30`; `RETRY-15`; P5a-12) — Breaking 4

**Pins first.** Run, green and unedited, `SetDatePolicyTests`, the `RequestConditions` tests under
`tests/Dexpace.Sdk.Core.Tests/Http/Request/`, `RetryPolicyTests` and the **Security category**, `RetryPacingOverflowTests`
first (its HTTP-date case `Fri, 31 Dec 9999 23:59:59 GMT` parses under the new parser, the vector above).

**Failing tests first (new behaviour).** In `RetryPolicyTests`, a `[Theory]` `A_retry_after_http_date_the_BCL_parser_rejected_is_now_honoured`
over a fixed "now" `2026-01-01T00:00:00Z` (`FakeTimeProvider`) and a `Retry-After` 30 seconds later written as
`thu, 01 jan 2026 00:00:30 gmt`, `Mon, 01 Jan 2026 00:00:30 GMT` (inconsistent weekday), `Thu, 01 Jan 2026 00:00:30 UTC`,
`Thu, 01 Jan 2026 00:00:30 +0000`, `Thu, 1 Jan 2026 00:00:30 GMT` (single-digit day): each arms one timer of exactly 30 s,
through both `Send` and `SendAsync`; and `An_rfc850_or_asctime_retry_after_is_ignored_and_the_computed_delay_is_used`
(`Sunday, 06-Nov-94 08:49:37 GMT` and `Sun Nov  6 08:49:37 1994`: the armed delay is within `[0, MaxDelay]` of the computed
back-off, not a date delta; a rejected date is "no hint", never a wrong wait, §11 item 27).

Red: the new behaviours (lower case, `UTC`, …) fail on the as-built `"r"` parse.

**Production.**

- `SetDatePolicy.cs:52`: `HttpDate.Format(_timeProvider.GetUtcNow())`.
- `RequestConditions.cs` `SetDate`: `HttpDate.Format(instant)` (drops the `CultureInfo` import if unused; byte-identical
  output, fact 14).
- `RetryPolicy.ParseRetryAfter`'s HTTP-date branch: `HttpDate.TryParse(headerValue, out var httpDate)` replaces the
  `TryParseExact(…, "r", …)`; the delta and the floor at zero are unchanged. The `<b>Breaking (behaviour):</b>` paragraph in
  the class remarks lists what is now honoured (lower case, an inconsistent weekday, `UTC`/`+0000`/`+00:00`, a single-digit
  day) and what is still ignored (RFC 850, asctime). The rest of the pacing parser (fractional seconds, `retry-after-ms`, the
  rate-limit headers) stays 6a's.

**IDs:** `CFG-29`, `CFG-30`; cites `RETRY-15`. **Verify:** V-fast `RetryPolicyTests`, `SetDatePolicyTests`, the
`RequestConditions` class, then **the Security category**.

### Task 3.4 — Close-out (PR 3)

`CHANGELOG.md` `### Added`: `HttpDate` (`Format`, `Parse`, `TryParse`); `### Changed`: **Breaking (behaviour)** — `RetryPolicy`
honours HTTP-date `Retry-After` values it used to ignore (lower case, a weekday inconsistent with the date, the
`UTC`/`+0000`/`+00:00` zones, a single-digit day); `SetDatePolicy` and `RequestConditions` format through `HttpDate`
(output unchanged). Run **V-gate**. **Commit:** `feat!: phase 5a HTTP-date format and parse (CFG-29..CFG-31)`.

---

## PR 4 — The proxy model and resolver

**Gate: PR 2** (the `Environment` ban and the pragma convention). Rows: `CFG-22`–`CFG-28`; `TRANSPORT-15`, `TRANSPORT-30`
(cited). Files (all under `src/Dexpace.Sdk.Core/`): `Configuration/{ProxyType,ProxyOptions}.cs` (new), `Internal/{ProxyGlob,
ProxyUrlParser,NoProxyList,ProxyResolution,ProxyResolutionLog}.cs` (new), `PublicAPI.Unshipped.txt`; vectors
`tests/vectors/config/{proxy-globs,proxy-resolution,no-proxy-split}.json` (new); tests under
`tests/Dexpace.Sdk.Core.Tests/Configuration/`. **Nothing in `Dexpace.Sdk.Http.SystemNet` changes** (P5a-18; the V-gate checks
its `PublicAPI.Unshipped.txt` and, additionally, `git diff --stat main...HEAD -- src/Dexpace.Sdk.Http.SystemNet` is empty).

### Task 4.1 — `ProxyType` and the `ProxyOptions` model (`CFG-22`; P5a-13, R6, R9)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Configuration/ProxyOptionsTests.cs`, class `ProxyOptionsTests`
(`Host` and `Port` are `required`, so every construction names them):

- `Defaults_are_Http_no_credentials_no_bypass` (`Type == Http`, `NonProxyHosts` empty, `UserName`/`Password`/
  `ChallengeCredentials` null, `BypassAll` false)
- `Host_and_Port_are_required_members` (reflection: both properties carry `RequiredMemberAttribute`; no other does; P5a-3's
  "only `ProxyOptions.Host`/`Port` qualify")
- `Host_rejects_blank_whitespace_and_delimiters` (`[Theory]`: ``, `" "`, `"a b"`, `"a/b"`, `"a@b"`, `"[::1]"`, `"a\u0007b"`;
  `ArgumentException`; the message never echoes the value; null throws `ArgumentNullException`) (R9)
- `Host_accepts_a_name_an_ipv4_and_a_bare_ipv6_address` (`proxy.internal`, `10.0.0.1`, `2001:db8::1`)
- `Port_must_be_in_0_to_65535` (`[Theory]`: `-1`, `65536`, `int.MinValue` → `ArgumentOutOfRangeException`; `0` and `65535`
  accepted; `CFG-25`'s range on one member)
- `NonProxyHosts_is_copied_at_init` (`CFG-8`'s defensive copy: the caller's `List<string>` is mutated after construction and the
  record is unchanged; the property type is `IReadOnlyList<string>`; the instance is not the caller's list)
- `NonProxyHosts_rejects_null_and_null_entries` (`ArgumentNullException`)
- `ToString_masks_credentials` (exact text: `ProxyOptions { Type = Http, Host = proxy.internal, Port = 3128, NonProxyHosts =
  [*.internal, localhost], UserName = ***, Password = ***, ChallengeCredentials = set, BypassAll = False }`; absent values
  print `(none)`; a different user name and password produce the same text (the masking law, Node's property test as a
  `[Theory]` over six pairs incl. empty strings and strings containing the host's letters); an IPv6 host renders re-bracketed
  `[2001:db8::1]` (R6); no secret substring appears)
- `Equality_compares_the_pattern_list_by_content_and_credentials_by_value` (two instances built from separate equal lists are
  equal and hash alike; list order matters; a different `Password` is unequal; `ChallengeCredentials` compare by reference;
  fact 8's reason: the synthesised equality would compare the compiled-pattern array by reference)
- `With_on_an_unrelated_member_keeps_the_compiled_patterns_and_with_on_NonProxyHosts_recompiles` (fact 8; observable through
  `IsBypassed`: derive with a new list and the old pattern no longer matches; derive with `Port = 1` and the old pattern
  still does; the compiled-array identity is internal and asserted through `InternalsVisibleTo` as `ReferenceEquals` on the
  internal `CompiledPatterns` accessor for the unrelated-member case)

Red: CS0246 (`ProxyOptions`, `ProxyType` do not exist).

**Production.**

- `Configuration/ProxyType.cs`: `public enum ProxyType { Http = 0, Socks4 = 1, Socks5 = 2 }` with explicit values
  (styleguide 6.8) and per-member docs ("`socks4a` maps to `Socks4` and `socks5h` to `Socks5` at resolution").
- `Configuration/ProxyOptions.cs`, `public sealed record ProxyOptions`, the member list of the design's position D.
  `Type` (default `Http`, `Enum.IsDefined`-free: reject an undefined value with `ArgumentOutOfRangeException` through a
  `switch` over the three members), `Host` (`required`, `ThrowIfNull`, then `if (!IsValidHost(value)) throw new
  ArgumentException("The proxy host is blank or contains a character that is not allowed in a host.", nameof(value))`),
  `Port` (`required`, range), `NonProxyHosts` (explicit backing: `init` copies to `ImmutableArray<string>`, checks entries
  non-null, and compiles each through `Internal.ProxyGlob.Compile` into a private `Regex[]`; default empty arrays), `UserName`,
  `Password`, `ChallengeCredentials` (`ICredentials?`), `BypassAll`. `internal static bool IsValidHost(string?)` (R9).
  `Equals(ProxyOptions?)`, `GetHashCode`, and `ToString` hand-written (the design's masked form); `private bool PrintMembers`
  is not declared (the hand-written `ToString` makes it unreachable). `IsBypassed` lands in task 4.2 (the class compiles
  with a stub that throws `NotImplementedException` only inside the failing-test window: replace it in 4.2; **do not
  commit the stub**).
- XML docs: the class remarks cite `CFG-22`, `TRANSPORT-30` ("MUST NOT be logged"), design §8.2, P5a-13; state "not a property
  of `DexpaceClientOptions`: it configures the transport"; the `ChallengeCredentials` doc explains the native 407 hook
  (the slot is typed `ICredentials?`, P5a-13) and that `UserName`/`Password` are the Basic fallback.

**`PublicAPI.Unshipped.txt`** (`C.` = `Dexpace.Sdk.Core.Configuration.`; `ILogger` lines are task 4.5's):

```text
C.ProxyType
C.ProxyType.Http = 0 -> C.ProxyType
C.ProxyType.Socks4 = 1 -> C.ProxyType
C.ProxyType.Socks5 = 2 -> C.ProxyType
C.ProxyOptions
C.ProxyOptions.<Clone>$() -> C.ProxyOptions!
C.ProxyOptions.BypassAll.get -> bool
C.ProxyOptions.BypassAll.init -> void
C.ProxyOptions.ChallengeCredentials.get -> System.Net.ICredentials?
C.ProxyOptions.ChallengeCredentials.init -> void
C.ProxyOptions.Equals(C.ProxyOptions? other) -> bool
C.ProxyOptions.Host.get -> string!
C.ProxyOptions.Host.init -> void
C.ProxyOptions.NonProxyHosts.get -> System.Collections.Generic.IReadOnlyList<string!>!
C.ProxyOptions.NonProxyHosts.init -> void
C.ProxyOptions.Password.get -> string?
C.ProxyOptions.Password.init -> void
C.ProxyOptions.Port.get -> int
C.ProxyOptions.Port.init -> void
C.ProxyOptions.ProxyOptions() -> void
C.ProxyOptions.Type.get -> C.ProxyType
C.ProxyOptions.Type.init -> void
C.ProxyOptions.UserName.get -> string?
C.ProxyOptions.UserName.init -> void
override C.ProxyOptions.Equals(object? obj) -> bool
override C.ProxyOptions.GetHashCode() -> int
override C.ProxyOptions.ToString() -> string!
static C.ProxyOptions.operator !=(C.ProxyOptions? left, C.ProxyOptions? right) -> bool
static C.ProxyOptions.operator ==(C.ProxyOptions? left, C.ProxyOptions? right) -> bool
```

(`IsBypassed` is added in 4.2.) The architecture test of task 1.3 now covers `ProxyOptions` (sealed; init-only; `IReadOnlyList`).

**IDs:** `CFG-22`, `CFG-8` (collections rule, second subject). **Verify:** V-fast `ProxyOptionsTests`,
`OptionsRecordArchitectureTests`.

### Task 4.2 — Globs and `IsBypassed` (`CFG-23`; P5a-14)

**Vectors.** New `tests/vectors/config/proxy-globs.json`: cases `{ name, patterns[], host, expected, note? }`, source
`nodejs-sdk@54aeed4 packages/core/src/config/proxy.test.ts` plus the Ruby 5a design's glob list:

| Patterns | Host | Expected | Note |
|---|---|---|---|
| `*.internal.example.com` | `API.internal.example.com` | true | case-insensitive |
| `*.internal.example.com` | `internal.example.com` | false | the apex is not a subdomain |
| `*.internal.example.com` | `a.internal.example.com.evil.test` | false | full-string |
| `a.b` | `axb` | false | a dot is literal |
| `ho?t.example.com` | `host.example.com` | true | `?` is one character |
| `ho?t.example.com` | `hoost.example.com` | false | |
| `a+b` | `aab` | false | metacharacters escaped |
| `a+b` | `a+b` | true | |
| `a\*b` | `a\*b` | true | a backslash is an ordinary literal (no escape in the dialect) |
| `a\*b` | `aXXXb` | false | |
| `*` | `anything.test` | true | a lone star in a *list* (≥ 2 entries) is a glob |
| `localhost` | `LOCALHOST` | true | |
| `localhost` | `localhost\n` | false | `\z`, not `$` (fact 10) |
| `*.example.com` | `evil.com\n.example.com` | false | `.` does not cross a newline |
| (empty list) | `example.com` | false | |
| `` (one empty pattern) | `` | true | an empty pattern matches only the empty host |
| `` | `a` | false | |
| `.internal.example.com` | `a.internal.example.com` | false | curl treats this as a domain suffix; the spec's letter does not (note: P5a-14, open for the lead) |
| `.internal.example.com` | `.internal.example.com` | true | literal glob |
| `10.0.0.*` | `10.0.0.7` | true | |

**Failing tests first.** New `ProxyGlobTests` (`Configuration/`):

- `IsBypassed_matches_every_vector` (`[Theory]` over the file; each case builds a `ProxyOptions { Host = "p", Port = 1,
  NonProxyHosts = patterns }`)
- `BypassAll_short_circuits_regardless_of_the_list` (`BypassAll = true`, empty list, any host true; `CFG-22`/`CFG-27`)
- `A_null_host_throws_ArgumentNullException`
- `A_star_dense_pattern_matches_in_linear_time` (`*a*a*a*a*a*a*a*a*a*b` against 60 `a`; asserts `false` within a 2 s
  `Stopwatch` ceiling, a regression guard rather than the Node 50 ms figure; the guard is that the call returns at all under
  `NonBacktracking`; proven able to fail by constructing the regex without `NonBacktracking`, never committed)
- `Patterns_are_compiled_once_at_init_not_per_call` (the internal `CompiledPatterns` array is the same instance across two
  `IsBypassed` calls; `CFG-23`'s "compiled once"; the Node `compiledGlobs.has` assertion's analogue)
- `Glob_metacharacters_other_than_star_and_question_are_literals` (`[Theory]` over `[`, `]`, `(`, `)`, `{`, `}`, `^`, `$`, `|`,
  `\`, `+`: each, written as a pattern, matches itself and not a regex-expanded host)

Red: the `IsBypassed` stub throws.

**Production.** `Internal/ProxyGlob.cs`, `internal static class ProxyGlob`:
`Regex Compile(string glob)` builds `@"\A" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + @"\z"` with
`RegexOptions.NonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant` (no `Singleline`, so `.` does not
match `\n`); `bool Matches(Regex[] patterns, string host)`. `ProxyOptions.IsBypassed(string host)`:
`ArgumentNullException.ThrowIfNull(host); return BypassAll || ProxyGlob.Matches(_patterns, host);`. Remove the 4.1 stub.
XML docs on `IsBypassed` cite `CFG-23`, state "write `*.internal.example.com`, not `.internal.example.com`: a leading dot is a
literal here, unlike curl and `HttpClient.DefaultProxy`", and name P5a-14.

**AOT fallback (decided by task 0.1 fact 10).** If the NativeAOT publish of the throwaway program fails or warns on
`NonBacktracking`, replace `ProxyGlob.Compile`/`Matches` with a hand-written two-pointer wildcard matcher (P5a-14's stated
equivalent alternative: `*` any run, `?` one char, `OrdinalIgnoreCase`-style ASCII folding, `\n` not matched by `*`/`?`
for the newline vectors, full-string); `ProxyOptions` then holds `string[]` patterns instead of `Regex[]`, equality is
unchanged, and **every vector and test above passes unchanged**. The branch is recorded in the checklist.

**`PublicAPI.Unshipped.txt`:** `C.ProxyOptions.IsBypassed(string! host) -> bool`.

**IDs:** `CFG-23`. **Verify:** V-fast `ProxyGlobTests`, `ProxyOptionsTests`.

### Task 4.3 — The proxy URL grammar (`CFG-24`, `CFG-25`; P5a-15 item 3, R6, R7, R9)

**Vectors.** New `tests/vectors/config/proxy-resolution.json`, cases `{ name, env{}, expected (object|null), warning
(null|{variable, reason}), note? }`; this task covers the URL half (single-variable cases), 4.5 the selection half. Source:
the Node `proxy.test.ts` URL-layer cases and the Ruby 5a design's list. The URL grammar rows (`env` is `{"HTTPS_PROXY": …}`
unless noted):

| Input | Expected | Warning reason |
|---|---|---|
| `https://secure.example.com:9090` | **null**: `https` (TLS to the proxy) is invalid, not silently downgraded (P5a-15 item 3; Node's vector accepts it, note) | `scheme` |
| `http://proxy.example.com:8080` | Http, `proxy.example.com`, 8080 | — |
| `HTTP://Proxy.Example.com:8080` | Http, host kept as written | — (scheme case-insensitive) |
| `socks5://proxy.example.com:1080` | Socks5 | — |
| `socks5h://p:1080`, `socks4a://p:1080`, `socks4://p:1080` | Socks5, Socks4, Socks4 | — |
| `ftp://proxy.example.com:21` | null | `scheme` |
| `proxy.example.com:8080` (no scheme) | null (the platform accepts it as `http`; `CFG-24` names `scheme://`) | `scheme` |
| `http://proxy.example.com` | null; never guess | `port` |
| `http://proxy.example.com:` | null | `port` |
| `http://p.example.com:80` | port **80** (fact 7: read from the raw text, not `Uri.Port`) | — |
| `http://p.example.com:443` | port 443 | — |
| `http://p:70000`, `http://p:65536`, `http://p:-1`, `http://p:+80`, `http://p:0x50`, `http://p:8_0`, `http://p:80.0`, `http://p:١٢` | null | `port` |
| `http://p:0` / `http://p:65535` | ports 0 / 65535 | — |
| `http://[2001:db8::1]:8080` | host `2001:db8::1` (bare), port 8080 (R6) | — |
| `http://[2001:db8::1]` | null | `port` |
| `http://[2001:db8::1:8080` (unclosed) | null | `unparseable` |
| `http://us%40er:p%3Ass@proxy.example.com:8080` | user `us@er`, password `p:ss` | — |
| `http://u:p+q@proxy:8080` | password `p+q` (a `+` stays a `+`) | — |
| `http://:secret@p.example.com:8080` | no credentials (R7) | — |
| `http://user@p.example.com:8080` | user `user`, password `null` | — |
| `http://u:pa%ss@h.example.com:8080` | null (R7: a lone `%` is invalid) | `credentials` |
| `http://u:p%2@h:8080` | null | `credentials` |
| `http://a@b:c@proxy:8080` | null: the authority is split at the last `@`, and a second `@` left in the userinfo is invalid (a literal `@` must be written `%40`) | `unparseable` |
| `http://p:8080/` | accepted (a bare `/` path) | — |
| `http://p:8080/x`, `http://p:8080?x=1`, `http://p:8080#f` | null | `path` |
| `not a url at all`, `http://`, `://p:80`, `http:///x:80` | null | `unparseable` |
| `` or whitespace only | not a URL case: an empty value is *absent* (task 4.5), so the parser is never asked | — |
| `http://p q:80` (space in host) | null | `host` |
| the same with `  http://proxy.example.com:8080  ` | accepted after trim (R8) | — |

**Failing tests first.** New `ProxyUrlParserTests` (`Configuration/`, internal access): `Parses_every_vector` (`[Theory]` over the
URL-half rows; asserts the parsed `ProxyType`/host/port/user/password or the rejection reason, `ProxyUrlError` enum
`None, Unparseable, Scheme, Host, Port, Path, Credentials`); `Never_throws_for_arbitrary_text` (seeded `Random(5141)`, 20 000
strings built from `{http, socks5, ftp, zz}://` + userinfo/host/port pieces from the Node generator's alphabet
`[\w%.:+~@-]`, ports `''`, `:0`, `:80`, `:443`, `:8080`, `:65535`, `:70000`, `:x`; none throws); `The_port_is_read_from_the_raw_text_not_Uri_Port`
(a direct pin on fact 7's trap: `http://p:80` parses to 80, `http://p` to a `Port` rejection).

Red: CS0103 (`ProxyUrlParser` does not exist).

**Production.** `Internal/ProxyUrlParser.cs`: `internal static bool TryParse(string value, out ParsedProxy parsed, out
ProxyUrlError error)` over a span; the steps (each its own private method, `MA0051`): scheme (`://` required; closed set,
ASCII case-insensitive; `https` and anything else → `Scheme`), authority end (first `/`, `?` or `#` after `://`; a non-empty,
non-`/` remainder → `Path`), userinfo split at the **last** `@` of the authority (a second `@` in the userinfo →
`Unparseable`), user/password split at the first `:` and decoded by a private percent-decoder (valid `%XX` only, UTF-8 bytes,
a `+` untouched; invalid → `Credentials`; R7), host (a `[…]` bracket run → the bare inside, else up to the last `:`) checked with
`ProxyOptions.IsValidHost` (→ `Host`), port (after the host's `:`; all ASCII digits, 1–5 of them, value ≤ 65535, else `Port`;
absent or empty → `Port`). It never calls `Uri`/`Uri.Port`. `ParsedProxy` is an internal `readonly record struct`.

**IDs:** `CFG-24`, `CFG-25`. **Verify:** V-fast `ProxyUrlParserTests`.

### Task 4.4 — The `NO_PROXY` list (`CFG-26`, `CFG-27`; P5a-15 item 4)

**Vectors.** New `tests/vectors/config/no-proxy-split.json`, cases `{ name, value, expected (string[]|"bypass-all") }`:

| Value | Expected |
|---|---|
| `a.test,b.test` | `["a.test","b.test"]` |
| ` a.test , b.test ` | `["a.test","b.test"]` (trim) |
| `a\,b,c` | `["a,b","c"]` (an escaped comma is a literal comma) |
| `a,, ,c` | `["a","","c"]` (empty dropped, a whitespace-only fragment survives as an empty token: split → drop empty → unescape → trim, in that order) |
| `*` | `"bypass-all"` |
| ` * ` | `"bypass-all"` |
| `*,x.test` | `["*","x.test"]` (a star in a longer list is a glob) |
| `*.internal` | `["*.internal"]` |
| `` | `[]` (and absent: handled by selection, 4.5) |
| `\,` | `[","]` |
| `a\\,b` | `["a\\,b"]` (one token; a backslash is not an escape of a backslash, so the comma after `a\\` is still preceded by a backslash and is not a split point, and `\,` is the only escape: the first backslash stays, the second plus the comma becomes `,`; the cells in this table are raw text, the JSON file escapes each backslash once more) |
| `,` | `[]` |

**Failing tests first.** New `NoProxyListTests`: `Splits_every_vector` (`[Theory]`); `Each_token_round_trips_through_the_escape`
(Node's escape-law property as a seeded loop, 5 000 cases: ≥ 2 tokens from `[a-z,.*-]{1,10}`, encoded with `,` → `\,`, joined with
`,`, split back equal); `A_lone_star_is_reported_as_bypass_all_not_as_a_token`.

Red: CS0103 (`NoProxyList`).

**Production.** `Internal/NoProxyList.cs`: `internal static NoProxyResult Parse(string value)` where the result is the token list
and a `BypassAll` flag (the list is exactly one token `*` **after** the pipeline). A single-pass scanner that finds commas not
preceded by a backslash (so `\,` stays inside the fragment), drops empty fragments **before** unescaping and trimming
(the observable order of `CFG-26`), then replaces `\,` with `,` and trims. The pipe-separated system-property form has no .NET
source and is documented as absent.

**IDs:** `CFG-26`, `CFG-27`. **Verify:** V-fast `NoProxyListTests`.

### Task 4.5 — `FromEnvironment`: selection, the CGI guard, warnings (`CFG-24`, `CFG-27`; P5a-15, P5a-16, R8, R10)

**Vector additions** (`proxy-resolution.json`, selection half):

| Environment | Expected | Warning |
|---|---|---|
| `HTTPS_PROXY=http://s:9090`, `HTTP_PROXY=http://p:8080` | the `HTTPS_PROXY` one (preferred for every target) | — |
| only `HTTP_PROXY=http://p:8080` | it | — |
| `https_proxy` only / `http_proxy` only | lower case is a fallback | — |
| `HTTPS_PROXY=http://u:80`, `https_proxy=http://l:80` | the upper-case one | — |
| `HTTPS_PROXY=` (empty), `HTTP_PROXY=http://p:8080` | `HTTP_PROXY` (an empty value is absent) | — |
| `HTTPS_PROXY=   `, `HTTP_PROXY=http://p:8080` | `HTTP_PROXY` (R8) | — |
| `HTTPS_PROXY=ftp://x:21`, `HTTP_PROXY=http://p:8080` | **null** (no fallthrough on a malformed chosen value) | `{HTTPS_PROXY, scheme}` |
| `HTTP_PROXY=http://p:8080`, `GATEWAY_INTERFACE=CGI/1.1` | null (httpoxy guard: upper-case `HTTP_PROXY` skipped) | `{HTTP_PROXY, cgi}` |
| `http_proxy=http://p:8080`, `GATEWAY_INTERFACE=CGI/1.1` | the lower-case value (the guard skips only upper-case `HTTP_PROXY`) | — |
| `HTTPS_PROXY=http://p:80`, `NO_PROXY=a.test,b.test` | list `["a.test","b.test"]` | — |
| the same with `no_proxy=…` only | the lower-case fallback | — |
| `NO_PROXY` and `no_proxy` both set | `NO_PROXY` wins | — |
| `HTTPS_PROXY=http://p:80`, `NO_PROXY=*` | **null** (`CFG-27`) | — (not a failure: no warning) |
| `HTTPS_PROXY=http://p:80`, `NO_PROXY=*,x.test` | list `["*","x.test"]` | — |
| nothing set | null | — (nothing configured is not a warning) |
| `NO_PROXY=a.test` and no proxy variable | null | — |

**Failing tests first.** New `ProxyFromEnvironmentTests` (`Configuration/`; every case through the
`Func<string, string?>` overload over a dictionary, never `Environment`): `Resolves_every_vector` (`[Theory]`; a recording
`ILogger` double captures level, `EventId` and the formatted message); and:

- `A_rejection_warns_once_naming_the_variable_and_the_rule_and_never_the_value` (a value containing `hunter2` as a password
  is rejected for the port; the single `Warning`-level event carries `EventId` 20 / `ProxyConfigurationIgnored`, the variable
  name and the reason keyword, and **no** event text contains `hunter2`, the host or any part of the URL; `CFG-24`'s warning)
- `A_well_formed_value_warns_about_nothing`
- `A_NullLogger_still_returns_the_options` (`FromEnvironment(Func<…>, NullLogger.Instance)` returns the same options as a
  recording logger over the same dictionary; the two overloads that read the process environment are task 4.6)
- `A_throwing_logger_does_not_make_FromEnvironment_throw` (`ILogger.Log` throws `InvalidOperationException`; the result is still
  `null`; a fatal `OutOfMemoryException` is **not** swallowed: it propagates; `catch (Exception ex) when
  (!ExceptionFacts.IsFatal(ex))`)
- `A_throwing_lookup_propagates` (the caller's seam; the never-throw promise covers the input, not a broken seam)
- `Null_arguments_throw_ArgumentNullException` (`environment`, `logger`)
- `The_resolver_never_throws_for_arbitrary_values` (seeded loop over `HTTPS_PROXY` values, as 4.3, through the full selection
  path)
- `FromEnvironment_reads_only_the_variables_it_documents` (the lookup records the keys asked: exactly
  `HTTPS_PROXY, https_proxy, HTTP_PROXY, http_proxy, GATEWAY_INTERFACE?, NO_PROXY, no_proxy` in a stable order; `GATEWAY_INTERFACE`
  is read only when `HTTP_PROXY` is the candidate; `CFG-28`: nothing is read until the method is called, asserted by a
  lookup counter that is `0` before the call)

Red: CS0117 (`FromEnvironment` does not exist).

**Production.**

- `Internal/ProxyResolutionLog.cs`: `internal static partial class` with one `LoggerMessage.Define<string, string>(LogLevel.Warning,
  new EventId(20, "ProxyConfigurationIgnored"), "Proxy configuration in {Variable} was ignored: {Reason}.")` and an
  `internal static void Ignored(ILogger logger, string variable, string reason)` that wraps the call in `try { … } catch
  (Exception ex) when (!ExceptionFacts.IsFatal(ex)) { }` (a throwing `ILogger` must not make the resolver throw, `OBS-20`'s rule
  ahead of 5b's guard; 5b replaces the wrapper with its emission guard, P5a-28). The provisional id is noted in the doc.
- `Internal/ProxyResolution.cs`: `internal static ProxyOptions? Resolve(Func<string, string?> environment, ILogger logger)`
  (`ThrowIfNull` both): `ChooseVariable` (upper then lower for `HTTPS_PROXY`, then `HTTP_PROXY`, `http_proxy`; first present
  non-empty after trim; the CGI guard reads `GATEWAY_INTERFACE` only when upper-case `HTTP_PROXY` is the candidate and skips it
  with a `cgi` warning), `ProxyUrlParser.TryParse` (failure → warn with the `ProxyUrlError` keyword → `null`, no fallthrough),
  `ReadBypassList` (`NO_PROXY` then `no_proxy`, `NoProxyList.Parse`; `BypassAll` flag → return `null`, no warning),
  construction through the record's initializer (the values are pre-validated, so no constructor exception is possible; a
  defensive `catch (ArgumentException)` is **not** added: if it could throw it would be a bug the tests find).
- `ProxyOptions.FromEnvironment(Func<string, string?> environment, ILogger logger)` (public; it calls
  `ProxyResolution.Resolve` directly), documented with the rules list of the design (variable order, one proxy for every target, no fallthrough, closed
  scheme set, raw-authority port, `NO_PROXY` split order, `*`, never throws), the divergence from `HttpClient.DefaultProxy`
  (it does not bypass on `NO_PROXY=*`, defaults an absent port and returns credentials in `GetProxy`'s URI, fact 6), and the
  note that `socks4a`/`socks5h` map to `Socks4`/`Socks5`. The `FromEnvironment(ILogger)` and parameterless overloads are task 4.6,
  because they need `DefaultLookup`, the one sanctioned `RS0030` site.

**`PublicAPI.Unshipped.txt`:**

```text
static C.ProxyOptions.FromEnvironment(System.Func<string!, string?>! environment, Microsoft.Extensions.Logging.ILogger! logger) -> C.ProxyOptions?
```

**IDs:** `CFG-24`, `CFG-25`, `CFG-26`, `CFG-27`. **Verify:** V-fast `ProxyFromEnvironmentTests`, `ProxyUrlParserTests`,
`NoProxyListTests`.

### Task 4.6 — The one environment read, and `CFG-28` held two ways (`CFG-28`; P5a-16, P5a-17)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Configuration/NoImplicitProxyReadTests.cs`, class `NoImplicitProxyReadTests`:

- `The_default_lookup_is_Environment_GetEnvironmentVariable` (internal `ProxyResolution.DefaultLookup` is declared as the
  method group `Environment.GetEnvironmentVariable`, so `DefaultLookup.Method` equals
  `typeof(Environment).GetMethod("GetEnvironmentVariable", [typeof(string)])`; no process variable is set, so the parallel suite
  is undisturbed; proves the parameterless overload routes through `Environment` without touching it)
- `Both_process_environment_overloads_route_through_ProxyResolution_ResolveFromProcess` (hermetic, no environment read: IL
  scan of the bodies of `ProxyOptions.FromEnvironment()` and `FromEnvironment(ILogger)` for a `call` to
  `ProxyResolution.ResolveFromProcess`, and of `ResolveFromProcess` for an `ldsfld` of `DefaultLookup`, so the logger argument
  and the one sanctioned lookup are the only things wired; the logging behaviour itself is proven over the `Func` overload in
  4.5, so no test reads the process environment, design "Hermetic")
- `Null_logger_is_rejected_by_FromEnvironment_ILogger` (`ArgumentNullException`, `ParamName` `logger`; thrown before any lookup)
- `No_method_in_the_Core_assembly_calls_FromEnvironment` (the second line of defence, P5a-17; reflection over every method body
  of every type in the Core assembly, `MethodBody.GetILAsByteArray()`, scanning for `call`/`callvirt` opcodes whose operand
  token resolves (`Module.ResolveMethod(token, typeArgs, methodArgs)`, `ArgumentException` caught per token) to a method named
  `FromEnvironment` on `ProxyOptions`; the only permitted caller is none, which holds because all three public overloads call
  `ProxyResolution.Resolve` / `ResolveFromProcess` rather than one another. The test file states that `Dexpace.Sdk.Http.SystemNet`
  is not reachable from `Dexpace.Sdk.Core.Tests` (SEAM-2) and that 8b adds the same scan over the transport assembly with the
  one sanctioned constructor call.) A scanner self-test builds a nested method that does call it and proves the scan sees it.

Red: CS0117 (`DefaultLookup`, `ResolveFromProcess`, the `FromEnvironment(ILogger)` and parameterless overloads).

**Production.** `ProxyResolution.DefaultLookup` is the one place the process environment is touched:

```csharp
#pragma warning disable RS0030 // CFG-28, design §8.2, P5a-17: the one sanctioned environment read; reached only by the explicit FromEnvironment().
internal static readonly Func<string, string?> DefaultLookup = Environment.GetEnvironmentVariable;
#pragma warning restore RS0030
```

`ProxyResolution.ResolveFromProcess(ILogger logger)` is `Resolve(DefaultLookup, logger)` (`ThrowIfNull(logger)` first).
`ProxyOptions.FromEnvironment(ILogger logger)` is `ProxyResolution.ResolveFromProcess(logger)` and `ProxyOptions.FromEnvironment()`
is `ProxyResolution.ResolveFromProcess(NullLogger.Instance)`; neither calls another `FromEnvironment` overload (so the IL scan
below has no permitted caller to exempt). The parameterless form logs nothing; a warning with no logger to receive it is the as-built `NullLogger` default, which 5b may revisit). The static
initializer runs on first use of `ProxyResolution`, never at assembly load. No call site anywhere invokes `FromEnvironment`.
(If PR 4 landed before PR 2, this task also adds the four `Environment` lines of task 2.5.)

**`PublicAPI.Unshipped.txt`:**

```text
static C.ProxyOptions.FromEnvironment(Microsoft.Extensions.Logging.ILogger! logger) -> C.ProxyOptions?
static C.ProxyOptions.FromEnvironment() -> C.ProxyOptions?
```

**IDs:** `CFG-28`. **Verify:** V-fast `NoImplicitProxyReadTests`; `git grep -n "GetEnvironmentVariable" src` lists exactly the
one pragma'd line.

### Task 4.7 — Close-out (PR 4)

`CHANGELOG.md` `### Added`: `ProxyOptions`, `ProxyType` and `ProxyOptions.FromEnvironment` (an immutable proxy model with
credential-masking `ToString`, glob bypass list, and an environment resolver that never throws; installing it in the transport is
phase 8b's). Run **V-gate**, the coverage gate and its self-test, and (explicitly) the AOT publish, because this is the first PR
that puts `Regex` in core. **Commit:** `feat: phase 5a proxy options and environment resolver (CFG-22..CFG-28)`.

---

## PR 5 — Identity, the classifier, deep equality, identifiers

**Gate: none.** Rows: `CFG-32`–`CFG-36`; `RECOV-33`, `XCUT-5`, `XCUT-6`, `XCUT-21` (cited). Files:
`src/Dexpace.Sdk.Core/Configuration/BuildInfo.cs` (new), `Internal/{SdkVersion,DeepValue}.cs`,
`Configuration/DexpaceClientOptions.cs` (the default line), `Pipeline/Policies/RetryFacts.cs`, `PublicAPI.Unshipped.txt`,
tests under `Configuration/`, `Internal/`, `Pipeline/Policies/`, `Recovery/`.

### Task 5.1 — `BuildInfo` (`CFG-36`; P5a-22)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Configuration/BuildInfoTests.cs`, class `BuildInfoTests`:

- `Every_field_is_non_blank_and_header_safe` (`SdkVersion`, `RuntimeVersion`, `RuntimeDescription`, `OSName`: non-blank;
  `SdkVersion`, `RuntimeVersion` and `OSName` pass `HttpHeaderSyntax.IsValidName`; `RuntimeDescription` is free text and is only
  non-blank; Node's "no blank token" law)
- `IdentityTokens_are_the_two_ordered_product_tokens` (exactly `[ "dexpace-dotnet/" + SdkVersion, "dotnet/" + RuntimeVersion ]`;
  every token is `name/version` with both halves token-valid; read-only list; the same instance on every read: "resolved once")
- `The_sdk_version_carries_no_build_metadata` (no `+`)
- `The_runtime_version_is_Environment_Version` (equal to `Environment.Version.ToString()`; not `FrameworkDescription`, which has a
  space; fact 11)
- `ToToken_rejects_what_would_break_a_User_Agent` (internal `BuildInfo.ToToken(string?)` via `InternalsVisibleTo`; `[Theory]`:
  `null`, ``, `"  "`, `"1.0 beta"`, `"1.\r\n0"` (an embedded control), `"ünï"`, `"a/b"`, `"a;b"` → `Unknown`; `"1.2.3"`, `"0.0.1-alpha.1"`,
  `"10.0.401"` pass through, and `" 1.2.3 "` and `"1.0\r\n"` come back trimmed (`string.Trim()` strips surrounding
  whitespace including CR/LF, so surrounding control whitespace is stripped, never an injection; only an embedded control
  fails the token check); `CFG-36`'s "never a blank token"; the header-safety defect Node recorded for `navigator.userAgent`)
- `A_throwing_probe_yields_unknown_for_that_field_only` (internal `BuildInfo.Probe(Func<string?>)` wraps a non-fatal `try`; a
  probe throwing `InvalidOperationException` gives `Unknown`; a fatal `OutOfMemoryException` propagates)
- `OSName_maps_the_platform` (internal `BuildInfo.OsNameFor(Func<OSPlatform, bool>)`: windows/linux/macos/freebsd/`Unknown`)
- `SdkVersion_internal_forwarder_agrees` (`Internal.SdkVersion.Value == BuildInfo.SdkVersion`; 5c's `ActivitySource`/`Meter`
  versions read the same value)

Red: CS0246 (`BuildInfo` does not exist).

**Production.** `Configuration/BuildInfo.cs`, `public static class BuildInfo`, the design's members; `Unknown = "unknown"`
const; static fields initialised in declaration order each through `Probe(() => …)`: `SdkVersion` from
`AssemblyInformationalVersionAttribute` with the `+build` suffix stripped (as the as-built `SdkVersion.BuildVersion`),
falling back to `Assembly.GetName().Version`, then `ToToken`; `RuntimeVersion` from `Environment.Version`; `RuntimeDescription`
from `RuntimeInformation.FrameworkDescription` (non-blank check only); `OSName` from `RuntimeInformation.IsOSPlatform`;
`IdentityTokens` an `IReadOnlyList<string>` over an array (`Array.AsReadOnly`). `ToToken` = trim, then
`HttpHeaderSyntax.IsValidName` else `Unknown`. `Internal/SdkVersion.cs` shrinks to `internal static string Value =>
BuildInfo.SdkVersion;` (kept as a forwarder, the as-built name 5c may keep using). The fallback changes from `0.0.0` to `unknown`
(**Breaking 3**, behaviour; noted on `BuildInfo.SdkVersion`).

**`PublicAPI.Unshipped.txt`:**

```text
Dexpace.Sdk.Core.Configuration.BuildInfo
const Dexpace.Sdk.Core.Configuration.BuildInfo.Unknown = "unknown" -> string!
static Dexpace.Sdk.Core.Configuration.BuildInfo.IdentityTokens.get -> System.Collections.Generic.IReadOnlyList<string!>!
static Dexpace.Sdk.Core.Configuration.BuildInfo.OSName.get -> string!
static Dexpace.Sdk.Core.Configuration.BuildInfo.RuntimeDescription.get -> string!
static Dexpace.Sdk.Core.Configuration.BuildInfo.RuntimeVersion.get -> string!
static Dexpace.Sdk.Core.Configuration.BuildInfo.SdkVersion.get -> string!
```

**IDs:** `CFG-36`. **Verify:** V-fast `BuildInfoTests`; `DexpaceDiagnosticsTests` (the `ActivitySource` version still resolves).

### Task 5.2 — The default `User-Agent` (`CFG-36`; `RECOV-33`; P5a-22) — Breaking 3

**Failing tests first.** `DexpaceClientOptionsTests.The_default_user_agent_is_the_identity_tokens_joined` (equals `string.Join(' ',
BuildInfo.IdentityTokens)`; contains `" dotnet/"`; no `0.0.0`); in `ClientIdentityPolicyTests`,
`ProcessAsync_UsesDefaultUserAgent_WhenOptionsIsDefault` gains the assertion that the sent header equals the joined tokens
(the `StartsWith("dexpace-dotnet/")` assertion stays); `ClientIdentityStepTests` and `ClientIdentityPolicyTests`' explicit
`UserAgent` cases stay unchanged. Read `HeaderInjectionWireTests` (`Security`, SystemNet) before the change: it asserts on the
injected header, not on the agent line (design "Keeping the `Security` classes green`); if it asserts the line, stop and ask.

**Production.** `DexpaceClientOptions`' default `UserAgent` is `string.Join(' ', BuildInfo.IdentityTokens)`, computed once in a
`private static readonly string` (the as-built `s_defaultUserAgent` pattern). XML doc on `UserAgent` states the new default
and the **Breaking** note (was `dexpace-dotnet/<assembly-version>`; a consuming SDK that composes its own line through
`ClientIdentityPolicy` is unaffected). `README.md` mentions of the old default, if any (`grep -rn "dexpace-dotnet/" docs
src/*/README.md`), are updated in task 6.2.

**IDs:** `CFG-36`; cites `RECOV-33`. **Verify:** V-fast `DexpaceClientOptionsTests`, `ClientIdentityPolicyTests`,
`ClientIdentityStepTests`; the SystemNet `HeaderInjectionWireTests` still green.

### Task 5.3 — The `CFG-35` classifier (`CFG-35`; `XCUT-5`, `XCUT-6`; P5a-21)

**Failing tests first.** In `RetryFactsTests` (existing; namespace `…Tests.Pipeline.Policies`):

- `IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505` (every code 100–599: the true set has exactly 100 members,
  `{408, 429} ∪ 500..599 \ {501, 505}`; Node's "holds exactly 100 codes")
- `IsRetryableStatus_is_false_outside_the_documented_range` (0, -1, 99, 600, `int.MaxValue`)
- `The_configured_RetryPolicy_set_is_not_the_classifier` (a pin on `RetryPolicyTests`' side of the line: a scripted `507` and a
  scripted `511` are returned untouched by `RetryPolicy` (one transport call each) although `RetryFacts.IsRetryableStatus` is
  `true` for both, while a scripted `500` is retried; `XCUT-5`'s closing note keeps the two sets separate and 6a rebuilds
  `RetryPolicy`'s from `XCUT-7`'s data)
- `IsRetryableCause_accepts_the_IO_family` (`[Theory]`: `System.IO.IOException`, `System.Net.Sockets.SocketException`,
  `TimeoutException`, `HttpRequestException` with a null `StatusCode`; §11 item 23)
- `IsRetryableCause_rejects_everything_else` (`InvalidOperationException`, `ArgumentException`, `OperationCanceledException`,
  a `TaskCanceledException` with no inner, an `HttpRequestException` whose `StatusCode` is `404`)
- `IsRetryableCause_walks_the_cause_chain` (`new InvalidOperationException("x", new IOException())` true; an `HttpClient` timeout
  shape `new TaskCanceledException("t", new TimeoutException())` true: its `InnerException` is a `TimeoutException`; an
  `AggregateException` whose second inner is an `IOException` true; a chain of 100 wrappers with an `IOException` at the bottom
  false, because `ExceptionFacts.EnumerateCauses` stops below depth 64; an `IOException` at depth 10 true)
- `IsRetryableCause_terminates_on_a_cycle` (a private exception whose `InnerException` getter returns its partner and back; the
  call returns `false` rather than looping, `XCUT-9`)
- `IsRetryableCause_rejects_null` (`ArgumentNullException`)

Red: CS0117 (`RetryFacts.IsRetryableStatus`/`IsRetryableCause` do not exist).

**Production.** Two `internal static` members on `RetryFacts`: `IsRetryableStatus(int code) => code is 408 or 429 or (>= 500 and <=
599 and not 501 and not 505)`; `IsRetryableCause(Exception exception)`: `ThrowIfNull`, then `foreach (var e in
ExceptionFacts.EnumerateCauses(exception))` returns `true` for `System.IO.IOException` (written fully qualified, convention 8),
`SocketException`, `TimeoutException` or `HttpRequestException { StatusCode: null }`. The class remarks add: "the two predicates
are `CFG-35`'s single classifier; 6a wires `XCUT-5` (`HttpResponseException.IsRetryable` baked from the status half) and
`XCUT-6` (the `IRetryableError` capability, a widening of the cause half). No as-built behaviour changes in 5a." The
`RetryPolicy` is **not** edited.

**IDs:** `CFG-35`; cites `XCUT-5`, `XCUT-6`. **Verify:** V-fast `RetryFactsTests`, `RetryPolicyTests`.

### Task 5.4 — `DeepValue` (`CFG-33`, `CFG-34`; P5a-20, R5)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Internal/DeepValueTests.cs`, class `DeepValueTests` (ported from Node's
`equality.test.ts`; header comment cites it; the Node cases that assert a JavaScript fact (`undefined`, `DataView`, typed arrays,
`bigint`) are not ported, constraint 10):

- `Two_nulls_are_equal_and_null_is_not_equal_to_a_value` and `Null_hashes_to_zero`
- `Primitives_compare_with_Equals` (`1` vs `1`, `"a"` vs `"a"`, `1` vs `1L` unequal)
- `Arrays_compare_element_by_element` and `Arrays_of_different_lengths_are_unequal` and `Two_empty_arrays_are_equal`
- `Nested_object_arrays_recurse` (`new object[] { 1, new object[] { "a", 2.0 } }`)
- `Multi_dimensional_arrays_compare_structurally` (`int[,]` equal and unequal; rank and each dimension length checked)
- `Arrays_of_different_runtime_types_are_unequal` (`object[] { 1.0 }` vs `double[] { 1.0 }`; `object[]` vs `string[]` with the same
  strings; `int[]` vs `long[]` with the same values; design position E)
- `A_non_array_falls_back_to_object_Equals` (including a top-level boxed `0.0` vs `-0.0`: equal, because `0.0.Equals(-0.0)` is
  `true` and `CFG-33` sends a non-array to ordinary equality; the bit rule is **not** applied at the top level)
- `NaN_equals_NaN_inside_an_array_and_as_boxed_elements` (`CFG-34`; `double[] { double.NaN }` vs another NaN with a different
  payload (`BitConverter.Int64BitsToDouble(0x7FF8000000000001)`): equal; `float[]` likewise; `object[] { (object)double.NaN }`
  likewise, "boxed" meaning an element of an `object[]`)
- `Positive_zero_differs_from_negative_zero_inside_an_array_and_as_boxed_elements` (`CFG-34`; `double[] { 0.0 }` vs `{ -0.0 }`
  and `object[] { (object)0.0 }` vs `{ (object)-0.0 }` unequal, although `0.0.Equals(-0.0)` is `true`, fact 5)
- `Hashing_mirrors_equality` (equal pairs hash alike for every rule above, NaN payloads included; `+0.0` and `-0.0` array
  elements hash differently, because the element hash uses the canonicalised bit pattern; a top-level non-array hashes with
  `GetHashCode()`, consistent with `object.Equals`)
- `Hash_distinguishes_element_order_and_booleans_and_lengths`
- `Equality_is_reflexive_symmetric_and_hash_consistent_over_random_nested_arrays` (a seeded `Random(5142)`, 2 000 generated
  values of depth ≤ 4 over ints, strings, doubles incl. NaN/±0, bools, nulls and nested `object[]`)
- `A_self_referential_array_throws_InvalidOperationException_rather_than_overflowing_the_stack` (R5; `Equals` and `GetHashCode`;
  the message names the bound and contains none of the content)
- `Nesting_past_the_bound_throws_and_nesting_within_it_does_not` (depth 100 fine, depth 200 throws)

Red: CS0103 (`DeepValue` does not exist).

**Production.** `Internal/DeepValue.cs`, `internal static class DeepValue` with `Equals(object? a, object? b)` and
`GetHashCode(object? value)`, each delegating to a private depth-carrying core (`const int MaxDepth = 128`). Rules per design
position E: reference-equal or both null → equal; one null → unequal; both `Array` → same runtime `GetType()`, same `Rank`, each
`GetLength(d)` equal, then the row-major elements compared recursively (`Array.GetValue` via a flattened `IEnumerable` walk, no
`dynamic`, no reflection beyond `Array`'s own API: AOT-safe). The bit-pattern rule belongs to the **element** comparison only,
i.e. to values reached through the array walk (elements of `double[]`/`float[]`, or boxed doubles/floats that are elements of
an `object[]`): there a `double`/`float` compares by `BitConverter.DoubleToInt64Bits`/`SingleToInt32Bits` after canonicalising
any NaN to `double.NaN`/`float.NaN`. A non-array **top-level** value (including a boxed `double`) is not an element and falls
back to `object.Equals(a, b)` as the design and `CFG-33` say; any other element also uses `object.Equals`. The hash combines
rank, lengths and element hashes in order with `HashCode`. **Internal: no `PublicAPI` change.** The class remarks note "ships
now because `CFG-33`/`CFG-34` are 5a's MUSTs; no as-built model carries a floating-point array; a public promotion is 7a's call
(P5a-20)".

**IDs:** `CFG-33`, `CFG-34`. **Verify:** V-fast `DeepValueTests`.

### Task 5.5 — `CFG-32`: UUIDs are version 4 from the OS CSPRNG (`CFG-32`; P5a-19)

**Tests (pins; no production change).** In `tests/Dexpace.Sdk.Core.Tests/Recovery/IdempotencyKeyStepTests.cs`:

- `The_default_key_strategy_mints_a_version_4_IETF_variant_uuid` (the key is 36 characters, hyphens at offsets 8, 13, 18, 23,
  lower-case hex elsewhere; character at index 14 is `4`; character at index 19 is one of `8 9 a b`)
- `The_default_key_strategy_yields_no_collision_across_parallel_minting` (100 000 keys from `Parallel.For` over 8 degrees,
  collected in a `ConcurrentBag`; a `HashSet` of them has 100 000 members; thread-safety, `CFG-32`)

Proven able to fail by temporarily changing the default strategy to `Guid.CreateVersion7().ToString("D")` (index 14 is `7`; never
committed). `XCUT-21`'s security path stays `RandomNumberGenerator` (6c's cnonce) and is not touched. No type is added; the
checklist cites design §8.2 ("stronger than required"). **IDs:** `CFG-32`. **Verify:** V-fast `IdempotencyKeyStepTests`.

### Task 5.6 — Close-out (PR 5)

`CHANGELOG.md` `### Added`: `BuildInfo` (the SDK version and runtime identity, resolved once, each field falling back to
`unknown`); `### Changed`: **Breaking (behaviour)** — the default `User-Agent` is `dexpace-dotnet/<version> dotnet/<runtime>`
(was one token), and an undeterminable SDK version reads `unknown` (was `0.0.0`), which also changes the `ActivitySource` and
`Meter` version in that case. Internal: `RetryFacts.IsRetryableStatus`/`IsRetryableCause` and `DeepValue` (no consumer-visible
change). Run **V-gate**. **Commit:** `feat!: phase 5a build info, default user agent and classifier (CFG-32..CFG-36)`.

---

## PR 6 — Close-out

**Gate: PRs 1–5 merged.** The docs close the sub-phase (roadmap step 7). Rows: all 26 (closing).

### Task 6.1 — NativeAOT smoke over the new surface (`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`)

`RunAllAsync` calls one more check, `CheckPhase5aConfigurationAsync()`, in the existing style with its `Expect` helper (no
reflection; `AotSmoke` gets no `InternalsVisibleTo`; `using` additions are alphabetical in the file's block):

- a `DexpaceClientOptions` derived with `with` (`Retry = options.Retry with { MaxRetryAttempts = 5 }`): the source is unchanged,
  `ToString()` does not contain a query secret placed in `BaseAddress`, and a relative base address throws `ArgumentException`;
- `HttpDate.TryParse(HttpDate.Format(instant), out var back)` round trip, a lower-case `utc`-zone date accepted, an RFC 850 date
  rejected;
- `ProxyOptions` built with `NonProxyHosts = ["*.internal.example.com"]`: `IsBypassed("a.internal.example.com")` true and
  `IsBypassed("internal.example.com")` false **under NativeAOT** (the `NonBacktracking` `Regex` path; task 4.2's AOT fallback
  applies if this fails), `ToString()` free of a configured password, and `ProxyOptions.FromEnvironment(key => key ==
  "HTTPS_PROXY" ? "http://p.example.test:3128" : null, NullLogger.Instance)` resolving host and port;
- `BuildInfo.IdentityTokens` non-blank and `BuildInfo.RuntimeVersion` non-`unknown` (fact 11 under AOT);
- `TimeProvider.System.Sleep(TimeSpan.Zero, CancellationToken.None)` returns, and `await TimeProvider.System.DelayAsync(
  TimeSpan.Zero, CancellationToken.None)` completes.

A trim/AOT warning means the source is fixed, not the smoke. **Check:** `dotnet publish tests/Dexpace.Sdk.AotSmoke
--configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` prints "all checks passed".
**Commit:** `test: NativeAOT smoke over the configuration surface`.

### Task 6.2 — User documentation

New `docs/sdk-documentation/configuration.md` (the directory holds `bodies.md`, `execution-context.md`, `http.md`, `io.md`,
`pipelines.md`, `recovery.md`, `seams.md`), opening "As built by phase 5a, written against source on <date>". Content, citing
requirement IDs and never copying the design: the options records (sealed record + `init`, derive with `with`, nesting, the
validation at `init` and what is deliberately not validated, redacted `ToString`, the per-call `DexpaceClientOptions` overloads
and `RequestOptions`); the clock (`TimeProvider`, `TimeProviderWaits.Sleep`/`DelayAsync`, the negative-delay rule and the `-1 ms`
trap, the banned APIs); `HttpDate`; `ProxyOptions` and `FromEnvironment` (variable order, no fallthrough, the CGI guard, the
`NO_PROXY` rules, the glob dialect and the leading-dot divergence from curl, "installing it is the transport's job, phase 8b");
`BuildInfo` and the default `User-Agent`; what is not configurable and why (the materialisation cap → 7a, the SSE line cap →
7b, the context-store capacity declined, no global slot); the migration table for the four breaking changes. Notes that Ruby's
counterpart (`ruby-sdk/gems/` tree) is absent locally, as 2a–4c found, and that Node's `config/` tests were read from
`nodejs-sdk@54aeed4`. `docs/README.md`'s ownership table gains the row the probe asks for.
`src/Dexpace.Sdk.Core/README.md`: the options sample uses `with`. **Verify:** the probe's `links` check.

### Task 6.3 — The checklist

Write `docs/work/mvp/phase5/phase5a/<date>-phase5a-configuration-checklist.md` (the housekeeping `apply` step files the design
and this plan beside it, task 6.6) from what was built: **26 rows**, the constraint-3 legend, mirroring the design's census and
this plan's checklist section: 23 ✅ (`CFG-19` by `await` semantics with its pin, `CFG-32` by `Guid.NewGuid()` and its pins,
`CFG-28` by absence plus the ban and the IL scan), `CFG-12` N/A (no configuration builder type, P5a-25), `CFG-13` 🚫
(`configuration-via-iconfiguration`, design §10 entry 25) and `CFG-20` 🚫 (`cooperative-cancellation`, entry 8). Include the
"existing assertions changed" table (expected: `ClientIdentityPolicyTests.The_option_is_read_per_call`, the three
`RetryPolicyTests` assignment lines, `OperationBuildRequestTests`' base-rules theory moved, `BlockingWaitTests`' `-5` row),
the unedited-`Security` list with the empty-diff evidence of convention 5, the cross-owner table's cited tests, the vector
provenance (Node `@54aeed4`, Ruby design case lists; the cases not ported and why), the AOT-fallback decision of task 4.2, and
the deviation ledger as built (P5a-2, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 17, 19, 20, 21, 22, 23, 24, 25, 28) plus the plan's
readings R5–R9 with their lead decisions.

### Task 6.4 — Dated corrections, roadmap note, hand-offs

Frozen documents change only by dated correction (the design's "Corrections owed" list; re-read each file immediately before
writing, as 5b and 5c may have added items). In `docs/sdk-design-dotnet/`:

- **§8.2** (`08-instrumentation-and-configuration.md`): the **As built** line (records built; `ProxyOptions` and `FromEnvironment`
  built in core, installation 8b's; the date parser shared; the version fallback `unknown`); "**CFG-36**'s descriptor is
  `SdkVersion` plus `RuntimeInformation.FrameworkDescription`" → `BuildInfo`, whose runtime token is `Environment.Version`
  (P5a-22); the challenge slot typed `ICredentials?` (P5a-13); `DeepValue` internal and recursion-bounded (P5a-20, R5).
- **§8.3:** the **As built** line (the sync wait built as `TimeProviderWaits.Sleep`; the late-result helper `LateResult`; the
  banned-API gate extended, P5a-7 to P5a-9).
- **§5.3:** a dated note that the per-call overloads (`HttpPipeline`'s per-call `DexpaceClientOptions` overloads, sync and async) are kept, with the
  per-call options layered over the client's (P5a-6; the design ledger routes this note to PR 6).
- **§9.1:** a dated note that the banned-API list gains the clock, delay and environment groups, plus the one sanctioned pragma
  per group (P5a-8; the design ledger routes this note to PR 6).
- **§6.1:** "The port's one `RetryWait.DelayAsync`" → `TimeProviderWaits.DelayAsync` (P5a-7).
- **§3.1:** the configurable materialisation cap is 7a's per-read limit, not an options member (P5a-23).
- **§10 entry 25:** `FromEnvironment` reads the upper- and lower-case names with the CGI guard, one proxy for every target
  (P5a-15). No new §10 entry is opened (design "Deviation Ledger").
- **§11, new items** (next free numbers): the single-digit day accepted under `CFG-31` because `RETRY-15` requires it (P5a-11);
  `CFG-23`'s globs versus curl's leading-dot suffix (P5a-14); `CFG-12` read as vacuous without a builder type (P5a-25).
- **§12:** the `CFG` row's notes (`CFG-12` N/A argued; `CFG-21` built as `LateResult`).
- **3a checklist** (`P3a-12` row, hand-off): the materialisation-cap hand-off names 7a (P5a-23). **4a checklist:** the capacity
  hand-off is closed as declined (P5a-24). **4c checklist:** `PIPE-17` cites PR 1's `OptionsImmutabilityTests` (and the
  `P4c-15` hand-off is closed: the per-call overloads kept, P5a-6).
- **Roadmap** (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`): the Phase List row 5's `sdk-design refs` cell gains
  this design's link (appended, never replacing), and a dated Phase Status Note recording the six PRs, the rulings the lead
  accepted or changed (P5a-2, 4, 6, 7, 11, 13, 14, 15, 20, 21, 22, 23, and 28 once 5b and 5c confirm), the four breaking
  changes, and the hand-offs: 6a (`HttpDate`, `TimeProviderWaits`, `LateResult`, the `CFG-35` predicates, `RetryOptions`
  defaults), 6b (`RedirectOptions`), 6c (no `Task.Delay`), 7a (the per-read cap, `DeepValue`), 7b (the SSE line cap), 8b
  (`ProxyOptions` installation, `CFG-28`'s transport reading, `LateResult` for `TRANSPORT-9`), 9 (records, `IValidateOptions`,
  `FromEnvironment(key => configuration[key], logger)`).
- **`CLAUDE.md`:** the layout lines for `Configuration/` (`DexpaceClientOptions`, `RetryOptions`, `RedirectOptions`,
  `ProxyOptions`, `TimeProviderWaits`, `BuildInfo`) and `Http/Common/` (`HttpDate`); "What is genuinely unbuilt" drops "layered
  configuration" for core and keeps the binding tier under phase 9; the build-and-test counts if they changed.
- `docs/first-release.md`: if the register lists the `User-Agent` or the options shape as a consumer-visible asymmetry, add the
  entries; otherwise record none.

If the implementation found anything the knowledge corpus should hold (for example the `NonBacktracking`-under-AOT outcome or
the `UnescapeDataString` leniency), record it as a note under `docs/knowledge/notes/` (never edit `harvested/`).
**Verify:** the probe's `links` and `citations` checks.

### Task 6.5 — Close-out

`CHANGELOG.md` `### Added`: "`docs/sdk-documentation/configuration.md`; the AOT smoke covers the options records, `HttpDate`,
`ProxyOptions` and `BuildInfo`." Run **V-gate**, the coverage gate with its self-test, and
`git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
(expect empty). **Commits:** `docs: phase 5a checklist, user page and dated corrections` (after 6.1's `test:` commit).

### Task 6.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5a            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 5a --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Fix whatever the final probe reports (counts in `CLAUDE.md`/`README.md`, package READMEs, broken links, misattributed IDs).
`apply --write` performs `git mv` only; the plan authorises no commit beyond those in 6.1 and 6.5 and **no push**.

---

## Keeping the `Security` classes green

Constraint 5; the design's table (P5a-26) is the authority and the plan adds the *when*:

| Class | Edit | When it is run |
|---|---|---|
| `RetryPacingOverflowTests` (S7) | none | **first** in tasks 2.1, 2.3 and 3.3, then in every V-gate; its `RecordingTimeProvider` observes timers through `TimeProvider.CreateTimer`, which `Task.Delay(TimeSpan, TimeProvider, …)`, `Sleep` and `DelayAsync` all call (fact 13) |
| `AuthHttpsGuardTests`, `ReDriveRequestIsolationTests`, `RedirectCredentialHygieneTests` | none | task 1.2 (they build options with object initializers and call the kept per-call overloads; fact 15); `RedirectCredentialHygieneTests` still sets `StripSensitiveHeadersOnCrossOrigin = false` in an initializer, which compiles until 6b removes the property |
| `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests` | none | V-gate; `UrlRedactor` gains a caller (the options' `PrintMembers`), not a change |
| SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests`, `RedirectWireTests` | none | task 5.2 reads `HeaderInjectionWireTests` first (the new default `User-Agent` reaches the wire); V-gate |

**The check is an empty diff**, not "only these hunks" (contrast 4c's convention 5): every V-gate runs
`git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
and expects no output. No `Security` class is added: 5a fixes no phase-1 defect.

---

## Checklist: one row per owned ID

Exactly 26 rows. **Planned exit** uses the roadmap's constraint-3 legend; the checklist written in task 6.3 records what was
built. "Pin" means the test already passes on the as-built behaviour and is proven able to fail (convention 1). The census
level counts: 18 MUST, 7 SHOULD (`CFG-12`, `CFG-13`, `CFG-18`, `CFG-19`, `CFG-20`, `CFG-35`, `CFG-36`), 1 MAY (`CFG-28`).

| ID | Level | PR | Task(s) | Planned exit | Primary evidence |
|---|---|---|---|---|---|
| `CFG-8` | MUST | 1, 4 | 1.1, 1.2, 1.3, 4.1 | ✅ | `OptionsImmutabilityTests.Records_are_sealed_and_expose_init_only_properties`; `OptionsRecordArchitectureTests`; `ProxyOptionsTests.NonProxyHosts_is_copied_at_init` |
| `CFG-9` | MUST | 1 | 1.1, 1.2 | ✅ (source-seam clause vacuous in core) | `OptionsImmutabilityTests.With_derives_a_copy_and_leaves_the_source_unchanged` |
| `CFG-12` | SHOULD | 1 | 1.3 (argument) | N/A (no configuration builder type, P5a-25) | the checklist's argument; `OptionsRecordArchitectureTests` shows no builder in the namespace |
| `CFG-13` | SHOULD | — | 4.6 (absence) | 🚫 (design §10 entry 25, `configuration-via-iconfiguration`) | no static slot: `NoAmbientStateArchitectureTests`; `NoImplicitProxyReadTests.No_method_in_the_Core_assembly_calls_FromEnvironment` |
| `CFG-15` | MUST | 2 | 2.1, 2.3, 2.5 | ✅ | `TimeProviderWaitsTests` (all); the `RS0030` bans; `RetryPolicyTests.The_sync_and_async_waits_use_the_policys_TimeProvider_and_the_same_delay` |
| `CFG-16` | MUST | 2 | 2.1, 2.5 | ✅ | the `DateTime(Offset).Now/UtcNow/Today` bans (negative checks in 2.5); `TimeProvider.GetTimestamp` is the only elapsed-time source (`Stopwatch` is monotonic and 5c's) |
| `CFG-17` | MUST | 2 | 2.1 | ✅ | `TimeProviderWaitsTests.A_negative_delay_is_rejected`, `A_zero_wait_returns_at_once_without_arming_a_timer`, `Cancellation_surfaces_with_the_callers_token_and_leaves_it_signalled`, `A_sub_millisecond_delay_is_passed_to_the_timer_unrounded` |
| `CFG-18` | SHOULD | 2 | 2.2, 2.3 | ✅ | `TimeProviderWaitsTests.DelayAsync_*` (seven cases) |
| `CFG-19` | SHOULD | 2 | 2.6 | ✅ (pin) | `OriginalExceptionSurfaceTests.A_transport_failure_surfaces_as_the_original_exception_through_Send_and_SendAsync` |
| `CFG-20` | SHOULD | — | — | 🚫 (design §10 entry 8, `cooperative-cancellation`; P5a-10) | `Thread.Interrupt` banned (as built); no interruptible future |
| `CFG-21` | MUST | 2 | 2.4 | ✅ | `LateResultTests.A_result_delivered_after_cancellation_is_disposed_exactly_once`, `A_result_completed_between_the_token_firing_and_the_catch_is_still_disposed`, `A_late_null_result_is_ignored_without_throwing` |
| `CFG-22` | MUST | 4 | 4.1 | ✅ | `ProxyOptionsTests.ToString_masks_credentials`, `Defaults_are_Http_no_credentials_no_bypass`, `Equality_…` |
| `CFG-23` | MUST | 4 | 4.2 | ✅ | `ProxyGlobTests.IsBypassed_matches_every_vector`, `A_star_dense_pattern_matches_in_linear_time`, `Patterns_are_compiled_once_at_init_not_per_call` |
| `CFG-24` | MUST | 4 | 4.3, 4.5, 4.6 | ✅ (system-property layer substituted by the explicit `ProxyOptions`, §10 entry 25) | `ProxyFromEnvironmentTests.Resolves_every_vector`, `A_rejection_warns_once_naming_the_variable_and_the_rule_and_never_the_value`, `The_resolver_never_throws_for_arbitrary_values` |
| `CFG-25` | MUST | 4 | 4.1, 4.3 | ✅ | `ProxyUrlParserTests.Parses_every_vector` (port rows), `The_port_is_read_from_the_raw_text_not_Uri_Port`, `ProxyOptionsTests.Port_must_be_in_0_to_65535` |
| `CFG-26` | MUST | 4 | 4.4, 4.5 | ✅ | `NoProxyListTests.Splits_every_vector`, `Each_token_round_trips_through_the_escape` |
| `CFG-27` | MUST | 4 | 4.2, 4.4, 4.5 | ✅ | `NoProxyListTests.A_lone_star_is_reported_as_bypass_all…`; the `NO_PROXY=*` vector; `ProxyGlobTests.BypassAll_short_circuits_regardless_of_the_list` |
| `CFG-28` | MAY | 2, 4 | 2.5, 4.6 | ✅ (prohibitive half; the convenience half has no slot, `CFG-13`) | the `Environment` bans; `NoImplicitProxyReadTests`; `ProxyFromEnvironmentTests.FromEnvironment_reads_only_the_variables_it_documents` |
| `CFG-29` | MUST | 3 | 3.1, 3.3 | ✅ | `HttpDateTests.Format_*`; `SetDatePolicyTests` and the `RequestConditions` tests (byte-identical pin) |
| `CFG-30` | MUST | 3 | 3.2 | ✅ | `HttpDateTests.TryParse_matches_every_vector` (the tolerance rows) |
| `CFG-31` | MUST | 3 | 3.2 | ✅ | `HttpDateTests.TryParse_matches_every_vector` (the rejection rows), `Parse_error_never_echoes_the_input`, `TryParse_never_throws_for_arbitrary_text` |
| `CFG-32` | MUST | 5 | 5.5 | ✅ (pin; §8.2: stronger than required) | `IdempotencyKeyStepTests.The_default_key_strategy_mints_a_version_4_IETF_variant_uuid`, `…yields_no_collision_across_parallel_minting` |
| `CFG-33` | MUST | 5 | 5.4 | ✅ | `DeepValueTests` (arrays, nesting, null, hashing, properties) |
| `CFG-34` | MUST | 5 | 5.4 | ✅ | `DeepValueTests.NaN_equals_NaN_…`, `Positive_zero_differs_from_negative_zero_…`, `Arrays_of_different_runtime_types_are_unequal` |
| `CFG-35` | SHOULD | 5 | 5.3 | ✅ (both halves internal; 6a wires `XCUT-5`/`XCUT-6`) | `RetryFactsTests.IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505`, `IsRetryableCause_*` |
| `CFG-36` | SHOULD | 5 | 5.1, 5.2 | ✅ | `BuildInfoTests` (all); `DexpaceClientOptionsTests.The_default_user_agent_is_the_identity_tokens_joined` |

Count: **26 rows**, all mapped (18 MUST, 7 SHOULD, 1 MAY). By exit: 23 ✅ (`CFG-8`, `CFG-9`, `CFG-15`–`CFG-19`, `CFG-21`–`CFG-36`),
1 N/A (`CFG-12`), 2 🚫 (`CFG-13`, `CFG-20`), 0 ⏳.

### Work on other owners' rows (no checklist row in 5a)

These carry no exit mark in 5a's checklist (the 4c precedent). 5a's tests are cited by the owner's row.

| ID (owner) | Work | Task | Evidence the owner cites |
|---|---|---|---|
| `PIPE-17` (4c, ✅) | The "options immutable/shared" half becomes structural | 1.2, 1.4 | `OptionsImmutabilityTests`; `HttpPipelineTests.A_per_call_DexpaceClientOptions_overrides_…` |
| `RETRY-15` (6a) | The HTTP-date clause (tolerant weekday, single-digit day) met by `HttpDate.TryParse`; fractional, `-ms` and rate-limit clauses stay 6a's | 3.3 | `RetryPolicyTests.A_retry_after_http_date_the_BCL_parser_rejected_is_now_honoured` |
| `RETRY-26`, `XCUT-3` (6a, 10) | The wait both rows describe is `TimeProviderWaits` | 2.3 | `RetryPolicyTests.The_sync_and_async_waits_use_…` |
| `ASYNC-5`, `TRANSPORT-9` (8a/8b) | The orphaned-result rule has its shared helper | 2.4 | `LateResultTests` |
| `XCUT-5`, `XCUT-6` (10, built in 6a) | The single classifier exists; 6a bakes `HttpResponseException.IsRetryable` and adds the capability check | 5.3 | `RetryFactsTests` |
| `XCUT-21` (10) | The non-security UUID path is `Guid.NewGuid()`; the security path stays `RandomNumberGenerator` (6c) | 5.5 | `IdempotencyKeyStepTests` |
| `TRANSPORT-15`, `TRANSPORT-30` (8b) | `ProxyOptions` is the model 8b installs; the masked `ToString` is `TRANSPORT-30`'s "MUST NOT log credentials" at the source | 4.1 | `ProxyOptionsTests.ToString_masks_credentials` |
| `RECOV-33` (4b) | The default token line becomes `BuildInfo.IdentityTokens` joined; the step's behaviour is unchanged | 5.2 | `ClientIdentityPolicyTests`, `ClientIdentityStepTests` |

---

## Traceability: PR → rows → tasks

| PR | Gate | Rows | Tasks |
|---|---|---|---|
| 1 | none | `CFG-8`, `CFG-9`, `CFG-12` (N/A) | 1.1–1.5 (5) |
| 2 | none; S7 first | `CFG-15`–`CFG-19`, `CFG-21`, `CFG-28` (the ban half) | 2.1–2.7 (7) |
| 3 | none; S7 first | `CFG-29`, `CFG-30`, `CFG-31` | 3.1–3.4 (4) |
| 4 | PR 2 | `CFG-22`–`CFG-28` (and `CFG-8`'s collection rule) | 4.1–4.7 (7) |
| 5 | none | `CFG-32`–`CFG-36` | 5.1–5.6 (6) |
| 6 | PRs 1–5 | all 26 (closing) | 6.1–6.6 (6) |
| pre-flight | — | — | 0.1 (1) |
| **Total** | | | **36 tasks** (35 in six PRs, plus the pre-flight) |

Rows by the PR that first lands them: PR 1: 3 (`CFG-8`, `CFG-9`, `CFG-12`); PR 2: 6 (`CFG-15`–`CFG-19`, `CFG-21`) with `CFG-28`'s ban
half; PR 3: 3; PR 4: 7 (`CFG-22`–`CFG-28`); PR 5: 5; the two 🚫 rows (`CFG-13`, `CFG-20`) have no code. 3 + 6 + 3 + 7 + 5 + 2 = 26
(`CFG-28` counted once, in PR 4 where it completes). PRs 1, 2, 3 and 5 may land in any order; PR 4 needs PR 2; every PR stays
green in any order these gates allow.

---

## Findings while planning

Items checked against the repository at `4130f7b` on 2026-10-07, each with what the plan did. R1–R12 above are the readings
these produced.

1. **F1 — The scheme check the design says to remove is not in `BuildRequest(DexpaceClientOptions)`** (R1). It is
   `OperationUrlComposer.RequireUsableBase`, shared with the public `Uri` overload, and stays. The test to move is in
   `OperationBuildRequestTests`, not `OperationDescriptorTests`.
2. **F2 — `BlockingWaitTests` contradicts P5a-7 on a negative delay** (R2); the plan splits the theory rather than keeping both.
3. **F3 — `Task<T>.Result` is banned, so `LateResult` as a continuation needs a second pragma** (R3); the plan uses an async
   helper so the `WaitAsync` pragma is the only one.
4. **F4 — `DeepValue` on a cyclic array would stack-overflow the process in .NET** (R5); a depth bound is added and flagged.
5. **F5 — Node's proxy tests pin four behaviours the design does not state:** bare IPv6 hosts (R6), a lone `%` in credentials
   resolving to `null` and an empty user name meaning no credentials (R7), and `https` as a *proxy* scheme (Node accepts it;
   the design rejects it, P5a-15 item 3 — the vector records the divergence).
6. **F6 — Node's `http-date` tests pin three behaviours the design rules the other way** (R11); the vector file records them
   as `null` with a `note`.
7. **F7 — The design's fact 8 (the `with` recompile of derived state) and fact 10 (`NonBacktracking` + `\z` + AOT) decide
   task 4.1 and 4.2**; task 0.1 runs them, and task 4.2 carries the stated fallback (a hand-written matcher) with every
   vector unchanged.
8. **F8 — `Dexpace.Sdk.Core.Tests` cannot see `Dexpace.Sdk.Http.SystemNet` (SEAM-2)**, so the `CFG-28` IL scan covers Core only;
   8b adds the same scan over the transport (task 4.6 says so in the test header).
9. **F9 — The as-built `RetryOptions.MaxRetryAttempts` is `3`;** the design's text mentioning `2` is 6a's future default, not
   5a's. 5a keeps every value (position A).
10. **F10 — `RetryPolicy`'s retried set is not `CFG-35`'s classifier** (`408, 429, 500, 502, 503, 504` vs `408, 429, 5xx \ {501,
    505}`); task 5.3 pins that they stay separate, as `XCUT-5`'s closing note requires.
11. **F11 — Crefs to newly banned symbols** (`Task.Delay` in `RetryPolicy` and `DexpacePipeline` remarks) are rewritten in task
    2.3, before the ban lands in 2.5, so the ban is a one-file change and a cref the analyzer flags is never the cause of a red
    build.
12. **F12 — `UserAgent` may be blank by design** (`ClientIdentityPolicyTests.A_blank_UserAgent_option_emits_no_header`): the
    `init` guard rejects `null` only (task 1.1).
13. **F13 — No `Security` class edit is needed, and the check is an empty diff**, unlike 4c (which edited one). The plan states
    it as the exact allowed diff so a reviewer can run one command.
14. **F14 — The design host had no .NET SDK;** every command in this plan, every `PublicAPI` line and the exact record-synthesised
    member list are verified on the implementer's host in task 0.1 and against the analyzer's output in each task.
15. **F15 — `HttpDate` boundary:** `DateTimeOffset` has no year 0, so Node's year-0000 vector is `null` here (R11), and `Format`
    uses `UtcDateTime` (never throws) rather than `ToUniversalTime()`; task 3.1 tests the two boundary instants at the extreme
    offsets.

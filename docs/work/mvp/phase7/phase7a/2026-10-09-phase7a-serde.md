# Phase 7a — Serde: Implementation Plan

**Status:** Draft, for review. Written 2026-10-09 against `main` at `8dc8ee4` (phases 0 to 6c merged). Design:
[phase 7a serde design](2026-10-09-phase7a-serde-design.md), the authority for every decision below. The plan cites
its census rows, positions (A–H), facts (1–12) and rulings (`P7a-1`…`P7a-25`) rather than restating them. Scope authority:
the roadmap's Phase 7 card (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). Format precedent: the phase 6 plans
(`docs/work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect.md` and its siblings), read first. 7b (SSE) and 7c (pagination) are
planned concurrently by other authors in this working tree; this plan touches only 7a's own files (the plan itself) and
lists the shared files its tasks will later edit.

**What this document is.** The roadmap's step 3 for sub-phase 7a: numbered TDD tasks in the design's five-PR segmentation, each
with its failing tests, production change, `PublicAPI.Unshipped.txt` diff, **Breaking** markings, `CHANGELOG.md` entry,
requirement IDs and verification commands. It is not the checklist (step 4, written from what was built in task 5.3) and it
holds no production code beyond the snippets whose exact text is load-bearing.

**Scope.** 32 rows: `SERDE-1`–`SERDE-30` plus `HTTP-44` and `HTTP-45` (carried from 3b, P7a-25). Planned exits: 32 ✅ (the
design's disposition table: 15 already met, 15 built, 2 carried), 0 🚫, 0 ⏳, 0 N/A. `SERDE-14`'s covariance SHOULD is unmet by
§10 entry 21 and recorded as ✅ with that citation (the MUST part is met). Rows owned elsewhere on which 7a works
(`BODY-16` extended to the typed reader, `RECOV-15` reused, `XCUT-19` reused, `NFR-9`) are in the
[cross-owner table](#work-on-other-owners-rows-no-checklist-row-in-7a).

**Order and gates.** PR 1 has no gate. **PR 2 follows PR 1** (the converter needs `Tristate<T>`, `ITristate`, `ITristateVisitor`).
**PR 3 follows PR 2** only for the shared `ReadValueEndToEndTests` (it needs the Tristate-wired serde); its Core halves are
independent. **PR 4 follows PR 3** (the handlers call the reshaped `ReadValueAsync`). **PR 5 follows PRs 1 to 4.** The
segmentation is the design's; the plan does not change it. Task 0.1 gates everything: facts 8 to 11 and fact 3's AOT behaviour
were never run.

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a compile
   error counts and is the expected red for a new type or a changed signature); make the production change; run them green;
   then run the wider gate of the PR's last task. A test that passes before the change is a **pin** and says so; a pin is
   proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and relies on the
   project's curated `GlobalUsings.cs` only (`ImplicitUsings` is off; every other namespace, `System.Buffers`,
   `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.Text.Json*`, is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` including `await using` (`CA2007`); methods at most 70 lines
   (`MA0051`); `///` XML docs on every public member (CS1591); no new `PackageReference` anywhere (constraint 2; the dependency
   audit runs in each PR's gate). `Task.Result`, `Task.Wait`, `ValueTask.Result` and the awaiter `GetResult` are banned
   (`BannedSymbols.txt`, RS0030): `TypedResponse` never reads a task synchronously. `TaskCompletionSource<T>.TrySetResult` and
   `Task<T>.WaitAsync(CancellationToken)` are banned too (SEAM-30); `LateResult` cannot serve `TypedResponse` (it needs
   `T : class, IDisposable`), so task 4.3 uses a scoped `#pragma warning disable RS0030` with a why-comment citing SEAM-30. `Uri.ToString()` is lossy and banned on
   log and message paths: every URL reaching a message goes through `UrlRedactor`.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it per suite); `Security` for exactly
   the one class added in task 4.2; the AOT smoke check is a plain method on `SmokeChecks` and carries no trait. Core tests
   live under `tests/Dexpace.Sdk.Core.Tests/<area>/`, doubles under `tests/Dexpace.Sdk.TestSupport/`.
   `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only (SEAM-2): **no test in it may use `SystemTextJsonSerde`**.
5. **Security tests are never deleted or loosened.** 7a edits no existing `Security` class and adds one
   (`StatusAwareHandlerLocationRedactionTests`, task 4.2). The V-gate's diff check is therefore exact: only that one added file
   may appear under `tests/Dexpace.Sdk.Core.Tests/Security`, and nothing under `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security`.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed member is an edit of its line in `Unshipped`
   and a removed one is a deleted line; the file is sorted ordinally and the build's `RS0016`/`RS0017` output is the authority
   (apply the analyzer's code fix, `dotnet format analyzers --diagnostics RS0016 RS0017 --severity info`, and compare against
   the design's "The public surface" list). Core's **and** the STJ adapter's files change in 7a; the SystemNet file must not
   (the V-gate checks). `RS0026`/`RS0027` (optional-parameter overload rules) are checked in every task that adds an overload:
   none of 7a's new members add a second overload with optional parameters.
7. **XML docs mark a breaking change** with `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it changes, stating
   what it was. The PR's last task adds the `CHANGELOG.md` `[Unreleased]` line for each (design "Breaking changes", items 1–5),
   under a "Phase 7a" sub-heading of its own so 7b and 7c merge line-locally.
8. **Namespaces.** `Dexpace.Sdk.Core.IO` shadows the simple name `IO` inside `Dexpace.Sdk.Core.*` (3a finding F1): write
   `System.IO.…` fully qualified in core files if needed. `Request.Request` is spelled as in `Response.cs`.
9. **Commits** follow the repository style: `feat!:` for a breaking feature PR, `feat:` for an additive one, `test:` for tests
   only, `docs:` for documentation only, `chore:` for refactors. **No AI attribution** of any kind in a commit message,
   changelog line or document (global hard rule). **The plan authorises no `git push`, no `gh` command and no remote
   action**; branches follow `<issue>-phase-7a-<slug>` off `main` (issue #1 is the Tristate issue; the phase issue number is
   the lead's to allocate).
10. **Siblings own shared files too.** `src/Dexpace.Sdk.Core/PublicAPI.Unshipped.txt`, `CHANGELOG.md`,
    `tests/Dexpace.Sdk.AotSmoke/{SmokeChecks,SmokeModels}.cs`, `Pageable.cs` (7c's), `CLAUDE.md` and the roadmap's status
    notes are shared with 7b and 7c: each PR re-derives its hunks from the merged file and never resolves a conflict by keeping
    either side whole. 7a never edits `Pageable.cs`.
11. **Test timing.** A test that must wait uses a `TaskCompletionSource` gate or `FakeTimeProvider`; none sleeps on the wall
    clock. No 7a test reads the process environment.
12. **`T?` on an unconstrained or `notnull` type parameter.** `Tristate<T>` declares `where T : notnull`; `ReadValue<T>` and the
    handlers declare **no** constraint (design P7a-9). The two files use `T?` as the annotation for "possibly null"; every
    `where T : notnull` signature that appears below is exactly the design's.

### Environment

`dotnet` 10.0.401 (`global.json`), `net10.0`. Run every command from the repository root. Warnings are errors: a green build
is the lint gate. The test runner is Microsoft.Testing.Platform (`--filter-class`, `--filter-trait`, not VSTest's `--filter`).
**The design was written on a host with no .NET SDK, and so was this plan**; every command below is untested here and runs for
the first time on the implementer's host. Facts 8 to 11 are therefore verified in task 0.1 before any code is written.

### Verification blocks

**V-fast** (per task; one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
dotnet test --project tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests --configuration Release --filter-class "*<ClassName>"
```

**V-sec** (the new `Security` class; after task 4.2 and again after each later PR):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*StatusAwareHandlerLocationRedactionTests"
```

**V-gate** (the last task of every PR; everything in CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/26/27/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # only the file convention 5 allows
git diff --stat -- src/Dexpace.Sdk.Http.SystemNet/PublicAPI.Unshipped.txt                                              # expect empty
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
scripts/knowledge --prefix-info SERDE
scripts/knowledge --gaps SERDE              # design: 0 of 30
scripts/knowledge --req SERDE-13            # per row in scope of the PR (HTTP-44, HTTP-45 for PR 4)
```

Task 0.1 records any difference from the design's corpus-file reading. A difference that contradicts a ruling reopens that
ruling.

---

## Plan-level readings (where the design was silent, ambiguous or wrong about the tree)

The design's decisions are not changed. **R1–R6 are corrections of fact; R7–R12 are additions.** Additions that add behaviour
are flagged for the lead.

- **R1 — `Disposal` has an async twin.** `Disposal.DisposeQuietly(IDisposable?, Exception? primary = null, ILogger? logger =
  null)` and `Disposal.DisposeQuietlyAsync(IAsyncDisposable?, Exception? primary = null, ILogger? logger = null)` are both
  `internal static` in `Dexpace.Sdk.Core.Internal`. The async readers and handlers call the async form, the sync readers the
  sync form. `ResponseBody` and `Response` are both `IDisposable` and `IAsyncDisposable`, so both overloads bind.
- **R2 — Where the code the design names lives.** `HttpResponseException` is in `src/Dexpace.Sdk.Core/Errors/TransportExceptions.cs`
  (there is no `HttpResponseException.cs`); `ErrorBodyBuffer` is `internal static` in `Http/Response/ErrorBodyBuffer.cs`
  (`Capture`, `CaptureAsync(Response, CancellationToken, ILogger?)`, returning a `Response` over a replayable buffered body and
  disposing the live one); `ErrorMapping.ToException(Response)` is `internal static` in `Errors/ErrorMapping.cs` and throws
  `ArgumentException` for a non-error status; `UrlRedactor` is a **public sealed class** with an `internal static UrlRedactor
  Default`, `Redact(Uri)` and `Redact(string)`. `PrefixedReadStream`'s constructor is `(byte[] prefix, int prefixLength, byte[]?
  staged, Stream tail, Action closeDelegate, Func<ValueTask> closeDelegateAsync)`: the peek wrapper (task 3.4) passes close-once
  wrappers around `tail.Dispose` and `tail.DisposeAsync` (the class calls the delegate at EOF and again on `Dispose`, so the raw
  delegates would double-dispose the tail).
- **R3 — The bounded reader the DIM default uses is `BodyMaterializer.ReadAll`.** `ISerde.Deserialize<T>(Stream)`'s default (P7a-13)
  calls `BodyMaterializer.ReadAll(source, declaredLength: source.CanSeek ? source.Length - source.Position : -1, limit: ResponseBody.DefaultMaxMaterializedBytes, default)`
  (`IO/BodyMaterializer.cs`; it does **not** dispose the source and throws `BodyTooLargeException`), then
  `Deserialize<T>(ReadOnlySpan<byte>)`. A DIM in a public interface may call an `internal static` helper of the same assembly.
- **R4 — `ReadValueAsync_is_single_use` may change exception type.** After P7a-11 the first read disposes the body; a second read
  of a disposed `FromBytes` body may surface `StreamClosedException` (as `LoggingResponseBody` does for "disposed before it
  was read") instead of `StreamConsumedException`. The design says "the plan checks". **Task 3.5 runs the test before
  changing it.** If the type differs, the test is edited to the as-built type with a comment naming P7a-11, and the difference
  is added to the Breaking list (design item 2); it is never silently loosened to `Exception`.
- **R5 — The existing fakes cannot serve the Core handler tests.** `ScriptedSerde<T>` ignores the bytes and returns a scripted
  value; `BytesSerde` encodes only. Task 3.2 adds `Utf8LiteralSerde` to `tests/Dexpace.Sdk.TestSupport/Serialization/`: a
  grammar of `null` (decodes to `null`), `ok:<text>` (decodes to the text, for `T = string`), `bad` (throws
  `DeserializationException` with an inner `FormatException`), `io` (throws `IOException` after reading one byte), anything
  else (`DeserializationException`). It implements the new stream member explicitly so the sync reader tests do not depend on
  the DIM, and a `DefaultImplementationSerde` sibling that does **not** override it, to exercise the DIM (task 3.2).
- **R6 — `SeamImplementationArchitectureTests` and `SerdeSeamArchitectureTests`.** The first lists `ISerde` as a seam whose
  implementers outside the allow-list must be public; 7a adds no `ISerde` implementer in `src/`. The second enumerates
  `s_seams = [ISerde, IStringSerde, IHttpClient, IAsyncHttpClient, SerdeExtensions]`; task 3.1 adds
  `ResponseBodySerdeExtensions` and `ResponseHandlers` to it (neither takes a `Type`), which is stricter than the design asks
  and costs nothing.
- **R7 — Two new test fakes in `TestSupport`, both flagged.** `Utf8LiteralSerde` and `DefaultImplementationSerde` (R5), plus a
  `GateResponseHandler<T>` (a handler that counts calls and blocks on a `TaskCompletionSource`) for `TypedResponseTests`, and
  a new `CountingPayloadBody` (task 3.2), and reuse of the existing `TestResponses.Create`. (`DisposalCountingBody` and
  `TrackingResponseBody` are `sealed` and fixed-content, 0 bytes and `[1,2,3,4]`, with no sync `OpenRead`, so they cannot serve the
  reader and handler tests.) All carry the header and
  XML docs (`TestSupport` is a normal project under the same gates).
- **R8 — `RespectNullableAnnotations` and the STJ test context.** The test models are positional records and classes in a
  nullable-enabled project, so annotations drive `RespectNullableAnnotations` correctly. A test context in **default
  generation mode** (no `GenerationMode` argument on `[JsonSourceGenerationOptions]`, fact 8) is a separate file,
  `TristateTestContext.cs`, so the existing `TestJsonContext` is untouched.
- **R9 — `JsonSerializerOptions(options)` on a context's `Options`.** The context-constructor copies `context.Options` through
  the options constructor path (`this(context.Options)`); the spec's fact 6 says the copy is mutable. Task 0.1 prints the copy's
  `IsReadOnly` and `TypeInfoResolver` type to confirm before task 2.4 builds on it.
- **R10 — `AddTristateSupport` on an already read-only `JsonSerializerOptions`.** Setting `Converters.Add` or `TypeInfoResolver`
  on frozen options throws `InvalidOperationException`; the extension lets it propagate and its docs say "call before the
  options are used". The serde never hits this (it always wires its private copy before `MakeReadOnly`).
- **R11 — The `Tristate<T>` equality operators.** A struct with `==` needs `Equals(object?)`/`GetHashCode` overrides
  (`CS0660`/`CS0661`, `CA1815`); all are in task 1.2. `CA1000` fires on the four static members and is suppressed per task 1.2; `CA2225` (operator named alternates) is satisfied by `FromNullable`
  for `implicit operator Tristate<T>(T?)` and by `Tristate.Absent`/`Tristate.Null` for the sentinel conversion; if the rule
  still fires, the `.editorconfig` severity is not touched: the sentinel gets a `ToTristate<T>()` method instead.
- **R13 — Branch scheme.** The design names one phase branch, `<issue>-phase-7a-serde`, for the five PRs. A git ref cannot be both
  that name and a prefix of it, so the plan reads each PR as its own topic branch off `main`
  (`<issue>-phase-7a-pr1-tristate` … `-pr5-closeout`), all belonging to the one phase named `<issue>-phase-7a-serde`; the PR
  bodies and the checklist header name the phase. Recorded as a plan-level reading; no behaviour changes.
- **R12 — Rows the plan reads differently from the design's table: none** (R13 is a branch-naming reading only). The two open items (Q1, P7a-21) are taken as the
  design takes them; the plan records them in the checklist as "ruled, open for the lead" and implements nothing else.

---

## Task 0.1 — Pre-flight: queries and the to-verify facts (no PR; no commit)

**Why first.** Facts 3 (AOT), 8, 9, 10 and 11 were never run. Tasks 2.3, 2.5, 3.4, 4.3 and 5.1 depend on the answers.

**Do.**

1. Run the five phase-start queries above and compare with the design's table. Record every difference in a scratch note
   (`$SCRATCH/phase7a-preflight.md`, in the session scratchpad); a difference that contradicts a ruling goes to the lead before
   PR 1 starts.
2. Create a throwaway console project **outside the repository** (scratchpad directory, `net10.0`, no package references,
   `<Nullable>enable</Nullable>`) with a source-generated `JsonSerializerContext` (default generation mode) over
   `record Patch(Wrapper<string> Name, Wrapper<int> Size)` where `Wrapper<T>` is a small `readonly struct` with a private state
   and `JsonConverter` factory registered on `JsonSerializerOptions`. Print, for each fact:
   - **8** — with a `TypeInfoResolver.WithAddedModifier` that sets `ShouldSerialize` to omit an "absent" `Wrapper<int>`:
     does `JsonSerializer.Serialize(new Patch(default, default), ctx.Patch)` omit the key? Run it **twice**: once with the context's
     default mode, once with `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]` (the
     fast-path mode). Record both. Expect the default-mode run to omit; a fast-path context may not (R1 in the design).
   - **9** — does the converter's `Read` receive a JSON `null` for a **value-type** converter at the root (`"null"`), in an
     array element, and as a property value, with `HandleNull` left at its default and then overridden to `true`? Print the
     token types seen.
   - **10** — `object? x = default(int?) ?? throw new Exception();` is not the shape; compile instead
     `static T Req<T>(T? v) => v ?? throw new Exception();` with an unconstrained `T` and print that it builds, and that
     `await ReadValueAsync<TPage>(...) ?? throw …` over a `ValueTask<T>` (not `T?`) builds with no warning (`CS8600` family) in
     a `Nullable=enable` project with `TreatWarningsAsErrors`. This decides whether `Pageable` still compiles untouched.
   - **11** — a `Lazy<Task<int>>` whose factory runs `Thread.Sleep(500)` then returns a completed task: 8 threads call `.Value`;
     print how long the 7 losers blocked (expect about 500 ms). Then run the CAS shape of P7a-15 with the same body and print
     that no loser blocks. This is the numeric justification for P7a-15 recorded in the checklist.
   - **3 (AOT)** — `PublishAot=true` of the throwaway project with a **value-type** `Wrapper<int>` reached only through the
     `RuntimeHelpers.GetUninitializedObject` + interface-visitor factory shape (design position B). Record the `IL2067` warning
     text (it must name `GetUninitializedObject`) and that the published binary round-trips `Wrapper<int>`. If it fails at run
     time, STOP: P7a-6 is reopened and goes to the lead before PR 2.
   - **Extra (plan-level, R9)** — `new JsonSerializerOptions(ctx.Options)`: print `IsReadOnly` (expect `false`),
     `TypeInfoResolver?.GetType()`, and whether `copy.GetTypeInfo(typeof(Patch))` still resolves after
     `copy.TypeInfoResolver = copy.TypeInfoResolver!.WithAddedModifier(_ => {})`.
   - **Extra (plan-level)** — `Tristate`-shaped `readonly struct` with `implicit operator S<T>(T? value)` for `T : notnull`, and a
     second `implicit operator S<T>(Sentinel)`: confirm `S<string> s = null;` binds to the `T?` operator (not ambiguous with the
     sentinel's), and that `S<int> i = 3;` binds.
3. Read, and record the line numbers of, the files the plan edits: `ISerde.cs`, `ResponseBodySerdeExtensions.cs`,
   `TransportExceptions.cs` (`HttpResponseException`), `SystemTextJsonSerde.cs`, both `PublicAPI.Unshipped.txt`, `BodyConvenienceTests.cs`,
   `SerdeSeamArchitectureTests.cs`, `SmokeChecks.cs` (`RunAllAsync`, `CheckSerdeProfiles`), `SmokeModels.cs`, the STJ package
   `README.md`, `CHANGELOG.md` `[Unreleased]`, the roadmap's Phase 7 card and last status note.
4. Run `grep -rn "ReadValueAsync\|GetErrorAsync" src tests docs/sdk-documentation README.md` and record every call site; each
   in `src/` must still compile after task 3.5 (only `Pageable.cs`, line ~112, is expected).
5. Confirm the analyzer position on `_ = RunAsync(tcs);` (task 4.3) by adding the line to a scratch copy of a core file and
   building: record whether `CA2012`, `CS4014`, `IDE0058` or `RS0030` fire. If one does, task 4.3 uses the discard form that
   passes (`Task unused = RunAsync(tcs); GC.KeepAlive(unused);` is the last resort and is flagged). In the same scratch build
   confirm that the scoped `#pragma warning disable RS0030 // SEAM-30` / `restore` form compiles clean around
   `tcs.TrySetResult(...)` and `task.WaitAsync(cancellationToken)` (both banned in `BannedSymbols.txt`); record the result.

**Outcomes feed.** Fact 8 false in fast-path mode → add test `TristateJsonTests.Absent_is_omitted_under_a_fast_path_context`
as an *expected-documented* limitation (R1 mitigation: the doc tells callers to use the default or `Metadata` mode) and add a
sentence to `serde.md` (task 5.2); the default-mode test stays the contract. Fact 9 false → `TristateConverter<T>.Read` handles
the null case by checking `reader.TokenType` before anything else (it already does) and the root-null test is rewritten to
the observed behaviour with the ruling note. Fact 10 false → `Pageable` is a compile break and task 3.5 adds the one-line
deletion (flag to 7c). Fact 11 mismatch → P7a-15's rationale is rewritten in the checklist, the CAS shape stays.
**Exit:** the scratch note exists; nothing is committed.

---

## PR 1 — `Tristate<T>` and its companions (core)

**Gate: none.** Rows: `SERDE-14`, `SERDE-17`, `SERDE-18`, `SERDE-30` (and the no-STJ-attribute architecture rule of P3). Files:
`src/Dexpace.Sdk.Core/Serialization/{TristateState,ITristate,ITristateVisitor,TristateSentinel,Tristate,TristateOfT}.cs`,
`PublicAPI.Unshipped.txt`, and the tests named below. Branch `<issue>-phase-7a-pr1-tristate`.

### Task 1.1 — `TristateState`, `ITristate`, `ITristateVisitor<TResult>`, `TristateSentinel` (`SERDE-14`, `SERDE-30`; P7a-6, P7a-7)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/TristateSentinelTests.cs`, class `TristateSentinelTests` (Unit):

- `TristateState_has_the_stable_values` (`Absent == 0`, `Null == 1`, `Present == 2`: a numeric pin, styleguide 6.8)
- `ToString_is_Absent_and_Null` (string equality, `SERDE-30`)
- `Equality_is_by_kind` (`TristateSentinel.Absent == TristateSentinel.Absent`, `!= TristateSentinel.Null`, hash codes equal for equal kinds; reached through `InternalsVisibleTo`; the `Tristate.*` form moves to task 1.3)
- `The_sentinel_default_is_Absent` (`default(TristateSentinel).ToString() == "Absent"`)

(the public `Tristate.Absent`/`Tristate.Null` accessors arrive in task 1.3, and task 1.1's tests do not use them; this class starts with
`TristateSentinel.Absent`/`TristateSentinel.Null` as `internal static` readonly instances that task 1.3 exposes.) Red: compile error.

**Production.** In `src/Dexpace.Sdk.Core/Serialization/`:

- `TristateState.cs` — `public enum TristateState { Absent = 0, Null = 1, Present = 2 }` with a doc line per member.
- `ITristate.cs` and `ITristateVisitor.cs`:

```csharp
public interface ITristate
{
    TristateState State { get; }
    TResult Accept<TResult>(ITristateVisitor<TResult> visitor);
}

public interface ITristateVisitor<out TResult>
{
    TResult Visit<T>(Tristate<T> value) where T : notnull;
}
```

  Documented as "the codec-adapter hook": an adapter in another assembly reaches the closed `Tristate<T>` without
  `MakeGenericType` (P7a-6). `ITristateVisitor<out TResult>`: C# permits `out` on a result type used only as a return.
- `TristateSentinel.cs` — `public readonly struct TristateSentinel : IEquatable<TristateSentinel>`: one private `TristateState`
  field (default `Absent`), an `internal` constructor taking `TristateState` that throws `ArgumentOutOfRangeException` for
  `Present`, `internal TristateState Kind`, `internal static TristateSentinel Absent`/`Null`, `Equals` ×2, `GetHashCode`,
  `==`, `!=`, `ToString()` returning `"Absent"` or `"Null"`.

**IDs:** `SERDE-30` (sentinel half), `SERDE-14` (support). **Verify:** V-fast `TristateSentinelTests`.

### Task 1.2 — `Tristate<T>` (`SERDE-14`, `SERDE-17`, `SERDE-18`, `SERDE-30`; P7a-2, P7a-3)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/TristateTests.cs`, class `TristateTests` (Unit). Every test is
named for the requirement it pins:

- `Default_is_Absent` (`SERDE-17`: `default(Tristate<string>)`, a field in a `record` that sets no initializer, and a
  `new Dto()` property all report `IsAbsent`; `State == TristateState.Absent`)
- `Present_of_null_throws_ArgumentNullException` (`SERDE-14`: the fourth state cannot be built; `ParamName` is `value`)
- `Present_holds_the_value` / `Null_has_no_value` / `Absent_has_no_value` (the three `Is*` predicates are mutually exclusive:
  exactly one is true per state, asserted by a loop over the three states)
- `Value_throws_InvalidOperationException_naming_the_state` (message contains `Absent` / `Null`; one assertion per state)
- `TryGetValue_is_true_only_for_Present` (the out parameter is the value / `default`)
- `GetValueOrDefault_returns_default_for_Absent_and_Null_and_the_value_for_Present`; `GetValueOrDefault_with_a_fallback`
- `Match_invokes_exactly_one_arm_per_state` (three counters; also the returned value)
- `Implicit_from_a_value_is_Present` (`Tristate<string> s = "a"` → Present `"a"`; `Tristate<int> i = 3` → Present 3)
- `Implicit_from_null_is_Null_and_never_throws` (P7a-2: `string? n = null; Tristate<string> s = n;` → `IsNull`)
- `FromNullable_never_returns_Absent` (reference `T`: `null` → Null, value → Present)
- `Implicit_from_the_sentinels_converts_to_any_T` (`Tristate<string>`, `Tristate<int>`, `Tristate<Dto>` from `TristateSentinel.Absent`
  and `.Null`, the covariance's use, §10 entry 21)
- `Equality_and_hash_are_by_state_and_value` (Absent==Absent; Null==Null; Present(1)==Present(1); Present(1)!=Present(2);
  Absent!=Null; `Equals(object)` with a boxed other `Tristate<string>` is false; equal values hash equal; operators `==`/`!=`)
- `ToString_is_Absent_Null_or_Present_of_the_value` (string equality: `"Absent"`, `"Null"`, `"Present(5)"`, `"Present(a)"`; `SERDE-30`)
- `Switch_on_State_is_exhaustive_by_pattern` (a `switch` expression over `t.State` with no discard compiles and returns per state)
- `ITristate_Accept_dispatches_to_the_closed_type` (a visitor that returns `typeof(T)` for `Tristate<int>` boxed as `ITristate`
  returns `typeof(int)`; for a default boxed value, State Absent)
- `A_present_value_type_is_copied_not_boxed_into_state` (pin: `Tristate<int>` has size `sizeof(int) + sizeof(int)` padded; assert
  `Unsafe.SizeOf<Tristate<int>>() <= 8`, documenting the allocation-free argument of design §3.4; if the runtime pads
  differently, relax to `<= 16` with the observed value in the message)

Red: compile error (the type does not exist). `DExpace.Sdk.Core.Tests.Serdes` is the existing namespace of `SerdeSeamTests`;
the new classes use `Dexpace.Sdk.Core.Tests.Serialization` (folder-matching, `IDE0130`).

**Production.** `src/Dexpace.Sdk.Core/Serialization/TristateOfT.cs` (the file name avoids colliding with the static class's
`Tristate.cs`), the shape of the design's position A, with these load-bearing details:

```csharp
public readonly struct Tristate<T> : IEquatable<Tristate<T>>, ITristate where T : notnull
{
    private readonly T? _value;
    private readonly TristateState _state;       // default(TristateState) == Absent: SERDE-17 for free

    private Tristate(T? value, TristateState state) { _value = value; _state = state; }

    public static Tristate<T> Absent => default;
    public static Tristate<T> Null => new(default, TristateState.Null);
    public static Tristate<T> Present(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, TristateState.Present);
    }

    public static Tristate<T> FromNullable(T? value) => value is null ? Null : new(value, TristateState.Present);

    public static implicit operator Tristate<T>(T? value) => FromNullable(value);
    public static implicit operator Tristate<T>(TristateSentinel sentinel) =>
        sentinel.Kind == TristateState.Null ? Null : Absent;

    TResult ITristate.Accept<TResult>(ITristateVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return visitor.Visit(this);
    }
    // State, IsAbsent/IsNull/IsPresent, Value, TryGetValue([MaybeNullWhen(false)] out T), GetValueOrDefault ×2,
    // Match, Equals ×2 (EqualityComparer<T>.Default for Present), GetHashCode (HashCode.Combine(_state, _value) only for
    // Present, else (int)_state), ==, !=, ToString.
}
```

`ToString` is hand-written (`"Absent"`, `"Null"`, `$"Present({_value})"` through `string.Create(CultureInfo.InvariantCulture, …)`),
and the struct is **not** a record struct (P7a-3). Every public member carries `///` docs; `Present`/`Null`/`Absent` docs
name the state they build.

**PublicAPI.** The analyzer's code fix lists every member; add them to `Unshipped` (`Dexpace.Sdk.Core.Serialization.Tristate<T>`
and `…Tristate<T>.Absent.get`, … `ITristate.Accept<TResult>`). Review against the design's list.

**`CA1000` (do not declare static members on generic types)** is a warning at `latest-recommended`, hence a build error here,
and fires on `Absent`, `Null`, `Present` and `FromNullable` (confirmed with a scratch build on SDK 10.0.401; `CA2225` does not
fire). The spec's public shape (design position A, SERDE-18) requires these generic-typed members, so each of the four carries a
scoped `[SuppressMessage("Design", "CA1000", Justification = "SERDE-18 / design position A: the generic-typed factories are the
specified API; the non-generic Tristate.* is the inference-friendly route.")]`. `.editorconfig` severity is **not** changed. The
four suppressions are recorded in the checklist's deviations (task 5.3).

**IDs:** `SERDE-14`, `SERDE-17`, `SERDE-18` (the `Tristate<T>` half), `SERDE-30`. **Verify:** V-fast `TristateTests`.

### Task 1.3 — The static `Tristate` class (`SERDE-14`, `SERDE-18`; P7a-7)

**Failing tests.** Appended to `TristateTests` (Unit) unless noted:

- `Tristate_Absent_and_Null_convert_in_assignment_to_a_Tristate_of_T` (`Tristate<Dto> d = Tristate.Null;` and a `with` expression
  `patch with { Name = Tristate.Null }` on a `record` patch)
- `Tristate_Present_infers_T` (`Tristate.Present("a")` → `Tristate<string>`; `Tristate.Present(5)` → `Tristate<int>`;
  `Present<string>(null!)` throws `ArgumentNullException`)
- `Tristate_FromNullable_for_a_nullable_value_type_maps_null_to_Null` (`int? n = null;` → Null; `int? m = 3;` → Present 3)
- `Tristate_FromNullable_for_a_reference_type_maps_null_to_Null`
- `GetValueOrNull_returns_the_value_or_null_for_value_types` (`Tristate<int>.Present(3).GetValueOrNull() == 3`; Absent/Null → `null`)
- `The_sentinel_property_types_are_TristateSentinel` (reflection pin: `typeof(Tristate).GetProperty("Absent")!.PropertyType`)

Red: compile error.

**Production.** `src/Dexpace.Sdk.Core/Serialization/Tristate.cs`, `public static class Tristate`:
`Absent`/`Null` return `TristateSentinel` (makes the task 1.1 sentinels' accessors the public route; the sentinel's constructor
stays `internal`), `Present<T>(T) where T : notnull`, `FromNullable<T>(T?) where T : class`, `FromNullable<T>(T?) where T : struct`
(the `Nullable<T>` overload: a distinct signature, design §A), and `static T? GetValueOrNull<T>(this Tristate<T> value) where
T : struct`. `CA1000`/`CA1034` do not apply to this non-generic static class (the `CA1000` suppression lives on `Tristate<T>`, task 1.2). All members documented.

**PublicAPI.** Add `Tristate` (static), `TristateSentinel` (struct) and every member the analyzer lists.

**IDs:** `SERDE-14`, `SERDE-18`. **Verify:** V-fast `TristateTests`, `TristateSentinelTests`.

### Task 1.4 — The no-STJ-attribute architecture rule (P3; design position B)

**Failing test (a pin; it passes on creation and is proven by mutation).** New
`tests/Dexpace.Sdk.Core.Tests/Architecture/TristateArchitectureTests.cs`, class `TristateArchitectureTests` (Unit):

- `Tristate_types_carry_no_System_Text_Json_attribute` — for `typeof(Tristate<>)`, `typeof(Tristate)`,
  `typeof(TristateSentinel)`, `typeof(ITristate)`, `typeof(ITristateVisitor<>)`, every custom attribute's type namespace is not
  `System.Text.Json*`; also no member (property, ctor) carries one
- `Core_references_no_System_Text_Json_assembly` — `typeof(Tristate<>).Assembly.GetReferencedAssemblies()` has no name starting
  `System.Text.Json` (SEAM-1's cousin; if SEAM-1 already asserts it, this test names that class in its message and stays as the
  Tristate-specific evidence)
- `Tristate_of_T_is_a_readonly_struct_and_not_a_record` — `IsValueType`, `IsReadOnly` (`IsReadOnlyAttribute`), and no
  `EqualityContract`/`<Clone>$` member (P7a-3)

**Mutation proof.** Temporarily put `[JsonConverter(typeof(object))]` on `Tristate<T>`; the first test fails; revert.

**IDs:** `SERDE-14` (support), P3. **Verify:** V-fast `TristateArchitectureTests`.

### Task 1.5 — Close-out (PR 1)

1. `PublicAPI.Unshipped.txt` diff reviewed against the design list (core only; `git diff --stat` shows no other `PublicAPI` file).
2. `CHANGELOG.md` `[Unreleased]` → "Phase 7a" sub-heading → `Added` line: `Tristate<T>`, `Tristate`, `TristateSentinel`,
   `TristateState`, `ITristate`, `ITristateVisitor<TResult>` (`SERDE-14`, `-17`, `-18`, `-30`; closes #1 with PR 2).
3. V-gate. Expected outcome: green; nothing is `Breaking` in PR 1.

---

## PR 2 — System.Text.Json wiring, the copying constructor and the default options

**Gate: PR 1.** Rows: `SERDE-15`, `SERDE-16`, `SERDE-19`–`SERDE-26`, `SERDE-29`. Files:
`src/Dexpace.Sdk.Serialization.SystemTextJson/{TristateConverter,TristateConverterFactory,TristateModifier,TristateJsonSerializerOptionsExtensions,SystemTextJsonSerde}.cs`,
`PublicAPI.Unshipped.txt`, `README.md`, and the tests named below. Branch `<issue>-phase-7a-pr2-stj-tristate`.

### Task 2.1 — Test models and a default-mode context (supports `SERDE-15`, `SERDE-16`, `SERDE-17`; fact 8)

**No failing test of its own** (fixtures). New `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/TristateModels.cs`:

```csharp
public sealed record WidgetPatch(Tristate<string> Name, Tristate<int> Size, Tristate<string> Note);
public sealed class NestedPatch { public Tristate<Widget> Inner { get; init; } public Tristate<List<Widget>> Many { get; init; } }
public sealed record Holder(WidgetPatch Patch, Tristate<WidgetPatch> NestedTristate);
public sealed record Shaped(Tristate<string> A, Tristate<string> B, Tristate<string> C);
public sealed record CallerFiltered { /* a property with a caller predicate set in a modifier, see 2.3 */ }
public sealed record DictHolder(Dictionary<string, Tristate<string>> Map);
public sealed record ArrayHolder(Tristate<string>[] Items);
public sealed record NonNullable(string Required);
```

and `TristateTestContext.cs`: `[JsonSerializable]` for each plus `Tristate`-instantiating types (`typeof(Tristate<string>)`,
`typeof(Tristate<int>)`, `typeof(Tristate<Widget>)`, `typeof(Tristate<List<Widget>>)`, `List<Widget>`, `Tristate<string>[]`,
`Dictionary<string, Tristate<string>>`, `Tristate<string>` as a root), declared with **no `GenerationMode`** (fact 8's contract),
`internal sealed partial class TristateTestContext : JsonSerializerContext;`. A second context,
`TristateMetadataContext`, carries `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]` for the
R1 fallback test. The project's analyzers treat the generated warnings as errors: if the generator reports `SYSLIB1220` or
unsupported-member diagnostics for `Tristate<T>`'s public `Value` property, suppress **nothing** — record the diagnostic in the
scratch note and ask the lead (it would reopen position B).

**Verify:** `dotnet build tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests --configuration Release` is green.

### Task 2.2 — Failing tests: the wire behaviour (`SERDE-15`, `SERDE-16`, `SERDE-17`, `SERDE-19`, `SERDE-20`; P7a-4, P7a-5)

New `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/TristateJsonTests.cs`, class `TristateJsonTests` (Unit), over
`new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default))` unless noted. The helper
`Encode(value)` serializes through `ISerde.Serialize(IBufferWriter<byte>)` and returns the UTF-8 string; `Decode<T>(json)`
goes through `ISerde.Deserialize<T>(ReadOnlySpan<byte>)`.

Encode (`SERDE-15`, `SERDE-19`):

- `Absent_omits_the_key` (`WidgetPatch(Absent, Absent, Absent)` → `{}`)
- `Null_emits_json_null` (`Name = Tristate.Null` → `{"name":null}`)
- `Present_emits_the_value` (`{"name":"a","size":3}`)
- `The_three_states_in_one_document` (`{"name":null,"size":3}`, the AOT smoke's expected bytes, camelCase from the defaults)
- `A_present_object_encodes_the_object_not_the_wrapper` (`Tristate<Widget>` → `{"inner":{"name":"g","size":1}}`, no `value`/`state` keys)
- `A_present_list_encodes_as_an_array`
- `A_nested_Tristate_inside_a_present_dto_is_still_rewritten` (`Holder(…, Tristate.Present(patch))`)
- `A_property_modifier_from_the_caller_is_composed_not_replaced` (a caller resolver wrapped with a modifier that sets
  `ShouldSerialize` to reject `Size == 7`; the serde keeps both: Absent omitted and `Present(7)` omitted; P7a-4 composition)
- `Tristate_wiring_is_on_by_default_for_a_bare_options_constructor` (`new SystemTextJsonSerde(new JsonSerializerOptions
  { TypeInfoResolver = TristateTestContext.Default })` already omits Absent: `SERDE-19`)
- `A_caller_registered_Tristate_converter_wins` (P7a-5: a caller converter factory for `Tristate<>` added to the options before
  construction; its output shows in the bytes; Absent omission still applies)
- `A_property_named_with_an_empty_string_is_handled` (`[JsonPropertyName("")]` on a `Tristate<string>`; Node's case)

Decode (`SERDE-16`, `SERDE-17`):

- `Missing_null_and_value_decode_to_Absent_Null_Present` for `string`, `int` (value type, the AOT hazard), `Widget`, and
  `List<Widget>` (`{}` / `{"x":null}` / `{"x":v}`; the element type is preserved: `Assert.IsType<Widget>`)
- `Mixed_document_decodes_each_property_independently` (`{"B":null,"C":"v"}` → `A=Absent B=Null C=Present(v)`, fact 7)
- `A_positional_record_and_a_class_with_init_properties_both_default_to_Absent`
- `A_value_of_the_wrong_type_is_a_DeserializationException` (`{"size":"x"}` → `DeserializationException` with a `JsonException`
  inner, the Tristate converter's failures are wrapped like any other, `SERDE-9`)
- `A_non_null_token_whose_inner_result_is_null_is_a_JsonException` (a `Tristate<Widget>` whose inner converter returns `null`:
  produced with a custom resolver; the converter rejects it instead of building a Present(null))

Positions where the wire has no key (`SERDE-20`):

- `Top_level_Absent_and_Null_both_write_null` (`Encode<Tristate<string>>(default)` and `(Null)` → `null`)
- `A_top_level_null_decodes_to_Null` (fact 9)
- `An_array_element_Absent_writes_null_and_indices_do_not_shift` (`ArrayHolder`)
- `A_dictionary_value_Absent_writes_null` (the documented compromise)

`AddTristateSupport` (P7a-4):

- `AddTristateSupport_without_a_resolver_throws_ArgumentException`
- `AddTristateSupport_is_idempotent_in_effect` (called twice, the output is unchanged; the converter list contains one factory)
- `AddTristateSupport_makes_plain_JsonSerializer_omit_Absent` (the direct-`JsonSerializer` caller of the design: serialize a
  `WidgetPatch` through `JsonSerializer.Serialize(value, options)` after wiring a mutable options object)
- `AddTristateSupport_on_read_only_options_throws_InvalidOperationException` (R10)

Fast-path (fact 8, R1 in the design): `Absent_is_omitted_under_a_metadata_mode_context` (uses `TristateMetadataContext`) and
`Absent_is_omitted_under_the_default_mode_context` (the contract; the headline test of the PR).

Round-trip property test (P7a-24):

- `Seeded_round_trip_of_10000_generated_models` — `new Random(seed)` (seed `20261009`, written to the test output with
  `TestContext.Current.SendDiagnosticMessage`); each iteration builds a `WidgetPatch` with each field Absent/Null/Present
  (Present values random strings including quotes, braces and non-ASCII; random `int`s including `int.MinValue`), encodes,
  decodes and asserts equality field by field; Absent stays Absent, Null stays Null.

Red: compile error for `CreateDefaultOptions`, `AddTristateSupport`. **IDs:** `SERDE-15`, `-16`, `-17`, `-19`, `-20`.

### Task 2.3 — The converter, the factory, the modifier and `AddTristateSupport` (`SERDE-15`, `SERDE-16`, `SERDE-19`, `SERDE-20`; P7a-4, P7a-5, P7a-6)

**Production.** In `src/Dexpace.Sdk.Serialization.SystemTextJson/`, all `internal sealed`/`internal static` except the extension:

1. `TristateConverter.cs` — `internal sealed class TristateConverter<T> : JsonConverter<Tristate<T>> where T : notnull`:
   `HandleNull => true` (fact 9, explicit). `Read`: `reader.TokenType == JsonTokenType.Null` → `Tristate<T>.Null`; else
   `var info = _info ??= (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));` (a lazily cached field; a benign race writes the same
   reference), `JsonSerializer.Deserialize(ref reader, info)`; a `null` result from a non-null token throws `JsonException`
   naming `typeof(T)`; otherwise `Tristate<T>.Present(value)`. `Write`: Absent or Null → `writer.WriteNullValue()` (`SERDE-20`'s
   degradation); Present → `JsonSerializer.Serialize(writer, value.Value, info)`. A source-generated context lacking
   `JsonTypeInfo<T>` makes `GetTypeInfo` throw `NotSupportedException`; the serde's existing `catch` already wraps it
   (task 2.4 widens the filter to the sync member).
2. `TristateConverterFactory.cs` — `internal sealed class TristateConverterFactory : JsonConverterFactory`. `CanConvert`:
   `typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Tristate<>)`. `CreateConverter`:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2067",
    Justification = "typeToConvert is a closed Tristate<> constructed by the caller's source-generated metadata; "
                  + "the AOT smoke consumer's value-type Tristate<int> proves it (NFR-9).")]
public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
{
    var boxed = (ITristate)RuntimeHelpers.GetUninitializedObject(typeToConvert);   // default (Absent) instance
    return boxed.Accept(ConverterVisitor.Instance);
}

private sealed class ConverterVisitor : ITristateVisitor<JsonConverter>
{
    internal static readonly ConverterVisitor Instance = new();
    public JsonConverter Visit<T>(Tristate<T> value) where T : notnull => new TristateConverter<T>();
}
```

   No `MakeGenericType`, no `Activator`. Task 0.1 decided whether `IL2067` or another ID is what the toolchain reports; the
   suppression uses the reported ID.
3. `TristateModifier.cs` — `internal static void Apply(JsonTypeInfo typeInfo)`: if `typeInfo.Kind != JsonTypeInfoKind.Object`
   return; for each `property` whose `PropertyType` is a closed `Tristate<>` (`IsGenericType && GetGenericTypeDefinition() ==
   typeof(Tristate<>)`), set `ShouldSerialize` to `(_, v) => v is ITristate { State: not TristateState.Absent }`, **AND**-composed
   with any prior predicate (`var prior = property.ShouldSerialize; … prior is null || prior(o, v)`). Reflection on `Type` identity
   only; no trim hazard.
4. `TristateJsonSerializerOptionsExtensions.cs` — `public static class TristateJsonSerializerOptionsExtensions`, one method
   `public static void AddTristateSupport(this JsonSerializerOptions options)`: `ArgumentNullException.ThrowIfNull`;
   `TypeInfoResolver is null` → `ArgumentException` (message names `AddTristateSupport` and "TypeInfoResolver");
   if `!options.Converters.Any(c => c is TristateConverterFactory)` add the factory; then
   `options.TypeInfoResolver = options.TypeInfoResolver.WithAddedModifier(TristateModifier.Apply)`. Docs: mutating,
   "call after setting the resolver" (R6 in the design), throws `InvalidOperationException` on read-only options (R10).
   Idempotence in effect: the second wrap composes the same predicate twice; `TristateModifier` detects its own predicate by a
   static marker delegate to avoid stacking (a `static readonly Func<object, object?, bool> s_omitAbsent` compared by reference,
   so composition skips a property whose `ShouldSerialize` already `== s_omitAbsent`).

**PublicAPI (adapter).** `TristateJsonSerializerOptionsExtensions` and `AddTristateSupport`. Everything else is `internal`.

**IDs:** `SERDE-15`, `SERDE-16`, `SERDE-19`, `SERDE-20`. **Verify:** V-fast `TristateJsonTests` (most tests need task 2.5's
`CreateDefaultOptions`; they are green at the end of 2.5, the earlier ones as marked there). **If the headline default-mode
test is red here, STOP and follow R1**: do not paper over with a metadata-mode-only test.

### Task 2.4 — The copying constructors (`SERDE-26`, `SERDE-19`, `SERDE-9`; P7a-5) — Breaking 4

**Failing tests.** New `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/SerdeOptionsTests.cs`, class `SerdeOptionsTests` (Unit):

- `The_callers_options_are_not_frozen_by_construction` (`SERDE-26`; after `new SystemTextJsonSerde(options)`, `options.IsReadOnly`
  is `false` and `options.WriteIndented = true` does not throw)
- `The_callers_options_keep_their_converter_count_and_resolver_reference` (`Converters.Count` before == after;
  `ReferenceEquals(options.TypeInfoResolver, resolverBefore)`)
- `Serializing_a_Tristate_model_through_the_callers_options_shows_no_omission` (no SDK-injected change: through the caller's own
  `JsonSerializer.Serialize(…, options)`, an Absent field is not omitted by the serde's wiring; it follows the caller's own options)
- `A_later_mutation_of_the_callers_options_does_not_affect_the_serde` (set `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
  after construction; the serde's output keeps the old naming)
- `The_context_constructor_copies_the_context_options` (`SystemTextJsonSerde(TristateTestContext.Default)`; the context's own
  `Options.IsReadOnly` is whatever it was, and the serde still omits Absent: wiring is on the copy; `TristateTestContext.Default.Options`
  has no `TristateConverterFactory` afterwards)
- `A_null_options_argument_throws_and_a_resolverless_one_still_throws_ArgumentException` (the existing guard)
- `Constructing_twice_from_one_options_object_works` (the pre-fix code threw nothing but froze; the pin is that both serdes work)

Red: `The_callers_options_are_not_frozen_by_construction` fails against the as-built code (`MakeReadOnly()` on the caller's
instance), and `The_callers_options_keep_…` fails when the converter is added to the caller's object.

**Production.** `SystemTextJsonSerde.cs`:

```csharp
public SystemTextJsonSerde(JsonSerializerOptions options)
{
    ArgumentNullException.ThrowIfNull(options);
    if (options.TypeInfoResolver is null) { throw new ArgumentException(/* as today */, nameof(options)); }

    var copy = new JsonSerializerOptions(options);   // an independent mutable copy (fact 6)
    copy.AddTristateSupport();
    copy.MakeReadOnly();
    _options = copy;
}
```

The context constructor still chains through `this(context.Options)`. Replace the XML doc sentence "The supplied options are made
read-only by this constructor" with the copy statement, and add `<para><b>Breaking:</b> …</para>` (was: the caller's instance
was made read-only; now it is copied and left untouched). `Serialize`, `SerializeAsync`, `DeserializeAsync`, `Deserialize` keep
their `catch` filters; extend the sync `Deserialize` filter and the `Serialize` one to include `InvalidOperationException` only
where already present (no widening beyond the design's `SERDE-9` text).

**IDs:** `SERDE-26`, `SERDE-19`. **Verify:** V-fast `SerdeOptionsTests`, then the whole STJ suite (the existing
`SystemTextJsonSerdeTests` must stay green).

### Task 2.5 — `CreateDefaultOptions` (`SERDE-21`, `SERDE-25`; P7a-8)

**Failing tests.** `SerdeOptionsTests` (continued):

- `CreateDefaultOptions_returns_a_fresh_mutable_instance_per_call` (`SERDE-25`: two calls are `!ReferenceEquals`; `IsReadOnly`
  false; mutating one does not affect the other)
- `CreateDefaultOptions_uses_Web_naming_and_strict_numbers` (`PropertyNamingPolicy == JsonNamingPolicy.CamelCase`,
  `PropertyNameCaseInsensitive`, `NumberHandling == JsonNumberHandling.Strict`, `RespectNullableAnnotations`)
- `CreateDefaultOptions_requires_a_resolver` (`null` → `ArgumentNullException`)
- `CreateDefaultOptions_is_Tristate_wired` (a `TristateConverterFactory` is in `Converters`; Absent omission works through
  `JsonSerializer` directly)
- `RespectNullableAnnotations_rejects_a_member_null_under_the_defaults` (`{"required":null}` into `NonNullable` →
  `DeserializationException`, `SERDE-21`'s adjacent rule, §7.3)
- `A_root_null_is_still_returned_as_null_under_RespectNullableAnnotations` (fact 5; `Deserialize<NonNullable>("null"u8)` returns
  null, the reader layer rejects it in PR 3)

**Production.** `public static JsonSerializerOptions CreateDefaultOptions(IJsonTypeInfoResolver typeInfoResolver)` on
`SystemTextJsonSerde`: `ArgumentNullException.ThrowIfNull`; `new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling
= JsonNumberHandling.Strict, RespectNullableAnnotations = true, TypeInfoResolver = typeInfoResolver }`; `AddTristateSupport()`;
return. Documented with the usage line `new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default))`
and the three things it chooses and why. **PublicAPI (adapter).** `CreateDefaultOptions`.

**IDs:** `SERDE-21` (configuration), `SERDE-25`. **Verify:** V-fast `SerdeOptionsTests`, `TristateJsonTests` (now fully green).

### Task 2.6 — Strict coercion, one test each (`SERDE-21`, `SERDE-22`)

**Failing tests (pins: they pass if 2.5 is right; each is proven by mutation).** New
`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/StrictCoercionTests.cs`, class `StrictCoercionTests` (Unit). A `Target`
record `(int I, double D, bool B, string S)` and a context; two serde factories: `Default()` (`CreateDefaultOptions`) and
`General()` (the context's own `General` options). The nine coercions as **nine explicit `[Fact]`s each running both serdes**
(a loop that drops one is invisible; the test body is a call to a shared `AssertRejected(json)` helper, one fact per row):

1. `String_five_does_not_bind_to_int` (`{"i":"5"}`)
2. `String_1_5_does_not_bind_to_double` (`{"d":"1.5"}`)
3. `String_true_does_not_bind_to_bool` (`{"b":"true"}`)
4. `Empty_string_does_not_bind_to_int` (`{"i":""}`)
5. `Float_1_5_does_not_bind_to_int` (`{"i":1.5}`)
6. `Bool_true_does_not_bind_to_int` (`{"i":true}`)
7. `Int_1_does_not_bind_to_bool` (`{"b":1}`)
8. `Bool_true_does_not_bind_to_double` (`{"d":true}`)
9. `Int_5_does_not_bind_to_string` (`{"s":5}`)

Each asserts `DeserializationException` with a non-null inner `JsonException`. Then `SERDE-22`'s two permissions as their
own tests: `Integer_widens_to_double` (`{"d":5}` → `5.0`) and `Empty_string_binds_to_string` (`{"s":""}` → `""`), plus
`A_well_typed_document_binds` (control). **Mutation proof.** Temporarily drop `NumberHandling = Strict` from
`CreateDefaultOptions`; rows 1 and 2 fail under `Default()`; revert.

**IDs:** `SERDE-21`, `SERDE-22`. **Verify:** V-fast `StrictCoercionTests`.

### Task 2.7 — Unmapped members, ISO-8601, concurrency (`SERDE-23`, `SERDE-24`, `SERDE-29`)

**Failing tests (pins).** `SerdeOptionsTests` (continued):

- `An_unmapped_member_is_skipped` (`SERDE-23`: `{"name":"a","size":1,"extra":{"x":[1,2]}}` → `Widget("a",1)`)
- `DateTimeOffset_round_trips_as_ISO_8601_and_the_same_instant` (`SERDE-24`: offset `+05:30`; the encoded string matches
  `yyyy-MM-ddTHH:mm:ss…+05:30`; decoded `==` the same `UtcDateTime`); also `DateTime` (UTC kind)
- `Many_workers_share_one_serde_without_cross_talk` (`SERDE-29`: 32 `Task.Run` workers × 500 iterations, each encoding then
  decoding a worker-and-iteration-unique `Widget`; all equal; no exception; runs both the first-use metadata initialisation —
  a **fresh** serde per test, so the cache is cold — and the warm path)

**IDs:** `SERDE-23`, `SERDE-24`, `SERDE-29`. **Verify:** V-fast `SerdeOptionsTests`.

### Task 2.8 — README and close-out (PR 2)

1. `src/Dexpace.Sdk.Serialization.SystemTextJson/README.md`: replace the "made read-only" statement with the copy statement; add
   a short Tristate and `CreateDefaultOptions` section (usage line from task 2.5).
2. `PublicAPI.Unshipped.txt` (adapter) diff against the design's list: `CreateDefaultOptions`, `TristateJsonSerializerOptionsExtensions`,
   `AddTristateSupport`. (`Deserialize<T>(Stream)` arrives in PR 3.)
3. `CHANGELOG.md`: "Phase 7a" `Added` (STJ Tristate wiring, `CreateDefaultOptions`, `AddTristateSupport`) and `Changed`/**Breaking**
   (the options constructor copies; design item 4).
4. V-gate. Expected outcome: green; `Category=Security` unchanged.

---

## PR 3 — The typed readers, the sync stream decode and `GetError<T>`

**Gate: PR 2** (for `ReadValueEndToEndTests` only). Rows: `SERDE-3`, `SERDE-5`–`SERDE-13`. Files:
`src/Dexpace.Sdk.Core/Serialization/{ISerde,ResponseBodySerdeExtensions,SerdeValues}.cs`, `src/Dexpace.Sdk.Core/Errors/TransportExceptions.cs`,
`src/Dexpace.Sdk.Serialization.SystemTextJson/SystemTextJsonSerde.cs`, `PublicAPI.Unshipped.txt` (both),
`tests/Dexpace.Sdk.TestSupport/Serialization/`, and the tests named below. Branch `<issue>-phase-7a-pr3-readers`.

### Task 3.1 — `ISerde.Deserialize<T>(Stream)` as a default interface member (`SERDE-3`, `SERDE-9`, `SERDE-12`; P7a-13) — Breaking 5

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeStreamDefaultTests.cs`, class `SerdeStreamDefaultTests` (Unit),
over `DefaultImplementationSerde` (task 3.2 creates it; write the tests first, they fail to compile):

- `The_default_reads_the_stream_to_the_end_and_decodes_the_span` (the fake's span decode records the bytes it received)
- `The_default_leaves_the_stream_open` (`SERDE-3`: a `DisposeCountingStream` reports 0 closes)
- `The_default_refuses_a_stream_over_the_cap` (`BodyTooLargeException` from a **seekable** synthetic stream whose `Length`
  exceeds `ResponseBody.DefaultMaxMaterializedBytes`; the DIM passes `source.CanSeek ? source.Length - source.Position : -1` as
  the declared length, so `RefuseDeclaredLength` refuses it before any read and nothing near the cap is allocated; the stream is a
  `RepeatingStream` in the test file reporting a large `Length`; the exception names the limit. A non-seekable over-cap source
  still drains up to the cap in memory before the probe byte; that path is not tested for speed)
- `An_IOException_from_the_stream_propagates_unwrapped` (`SERDE-12`)
- `Argument_validation` (`null` source → `ArgumentNullException`)
- `The_seam_still_takes_no_System_Type` (re-runs `SerdeSeamArchitectureTests`' logic is *not* duplicated; the architecture class
  is extended in this task: its `s_seams` gains `ResponseBodySerdeExtensions` and `ResponseHandlers` (R6; `ResponseHandlers`
  does not exist until 4.1, so only `ResponseBodySerdeExtensions` is added here and `ResponseHandlers` in 4.1)).

**Production.** `ISerde.cs`: add the member with its default body (R3) and update the type-level `<remarks>` to say the seam has
"two encode primitives and **three** decode primitives" (a stream, a UTF-8 span, and a synchronous stream with a bounded default):

```csharp
T? Deserialize<T>(Stream source)
{
    ArgumentNullException.ThrowIfNull(source);
    var declared = source.CanSeek ? source.Length - source.Position : -1;
    var bytes = BodyMaterializer.ReadAll(source, declared, ResponseBody.DefaultMaxMaterializedBytes, CancellationToken.None);
    return Deserialize<T>(bytes);
}
```

with `<remarks>` stating the default **materialises** (bounded, unwrapped `IOException`, `BodyTooLargeException`) and that a codec
with a streaming sync decoder should override it. No `CancellationToken` parameter (`RS0027`, design P7a-13). **PublicAPI
(core).** `Dexpace.Sdk.Core.Serialization.ISerde.Deserialize<T>(System.IO.Stream! source) -> T?`. Adding the file-level
`using Dexpace.Sdk.Core.IO;`/`Http.Response` for the helper is needed (R3; mind the `IO` shadowing in convention 8).
`SerdeSeamArchitectureTests`: add `typeof(ResponseBodySerdeExtensions)` to `s_seams`.

**IDs:** `SERDE-3`, `SERDE-12`. **Verify:** V-fast `SerdeStreamDefaultTests`, `SerdeSeamArchitectureTests`, `SeamImplementationArchitectureTests`.

### Task 3.2 — Test fakes (`TestSupport`; R5, R7)

**No failing test of its own** (fixtures, exercised by 3.1 and 3.3). New files, each with header, XML docs and the project's
namespace `Dexpace.Sdk.TestSupport.Serialization`:

- `DefaultImplementationSerde.cs` — implements `ISerde` and **omits** `Deserialize<T>(Stream)` so the DIM is exercised; the span
  decode records `ReceivedBytes` and returns `default`/a scripted value; `SerializeAsync`/`Serialize` write nothing.
- `Utf8LiteralSerde.cs` — the grammar of R5, implemented over all three decode primitives (the stream member explicitly, with
  a `StreamReads` counter, `LastStream` reference and a `ReadsMaterialised` flag set if the source's `Length` is touched), so
  Core's reader tests assert that the reader hands a *stream*, never a buffered copy.
- `tests/Dexpace.Sdk.TestSupport/Transports/CountingPayloadBody.cs` — `public sealed class CountingPayloadBody(byte[] payload, long
  contentLength = -1, Exception? disposeFailure = null) : ResponseBody`: overrides `OpenReadAsync` **and** `OpenRead` (a fresh
  `MemoryStream` over `payload`, single-use semantics not needed), exposes `DisposeCount` (incremented in `Dispose(bool)`, which
  throws `disposeFailure` when set), and **throws** from `ReadAsBytes*`/`ReadAsString*` (`ReadsNeverMaterialise`). Used by tasks 3.3
  and 4.1 for the `ok:abc`, `null`, `bad` and `io` payloads.
- `tests/Dexpace.Sdk.TestSupport/Transports/GateResponseHandler.cs` is added in task 4.3, not here.

**Verify:** `dotnet build tests/Dexpace.Sdk.TestSupport --configuration Release` green; 3.1's tests now run and pass.

### Task 3.3 — Failing tests: the readers (`SERDE-3`, `SERDE-9`, `SERDE-12`, `SERDE-13`; P7a-9 to P7a-12)

New `tests/Dexpace.Sdk.Core.Tests/Serialization/ReadValueTests.cs`, class `ReadValueTests` (Unit), each fact in an async form
and a sync twin (the twin uses `ReadValue`/`OpenRead`; names end `_sync`). All over `Utf8LiteralSerde` and `CountingPayloadBody` (task 3.2; its `ReadAsBytes*` throw, `ReadsNeverMaterialise`):

- `A_value_decodes_through_a_stream` (`ok:abc` → `"abc"`; the stream was read, the body never materialised)
- `The_body_is_disposed_exactly_once_on_success`, `…_on_a_codec_failure`, `…_on_an_IOException`, `…_on_a_root_null` (`DisposeCount == 1`
  in each; P7a-11)
- `A_dispose_failure_after_a_decode_failure_is_attached_not_substituted` (a body whose dispose throws `InvalidOperationException`
  after the codec failed: the thrown exception is the codec's; the dispose failure is in its suppressed trail,
  `ExceptionTrail`; RECOV-16's pattern)
- `A_root_null_for_a_reference_type_is_a_DeserializationException_naming_T` (message contains `String`/the type's `typeof(T)`
  text, and `ReadValueOrDefault`; no inner exception: P7a-12)
- `A_nullable_value_type_admits_a_root_null` (`ReadValueAsync<int?>` → `null`: `Nullable<>` targets legitimately decode `null`)
- `ReadValueOrDefault_admits_a_root_null` (`null` literal → `null`)
- `An_empty_body_with_ContentLength_zero_is_a_missing_body_naming_T` (and for `ReadValueOrDefault`: also fails, Q3)
- `A_zero_byte_unknown_length_body_is_a_missing_body_naming_T` (a stream body of unknown length: `ResponseBody.FromStream(empty)`)
- `A_peeked_byte_is_replayed_to_the_codec` (the codec sees all bytes of `ok:abc`, not `k:abc`)
- `An_IOException_mid_stream_propagates_unwrapped` (`SERDE-12`; `Assert.IsType<IOException>` and `Assert.Null(ex.InnerException)`
  not required but `ex is not SerdeException`)
- `A_codec_DeserializationException_is_the_same_instance` (`SERDE-9`: `Assert.Same`, not re-wrapped)
- `A_second_read_surfaces_the_as_built_exception` (R4; asserts the type that task 3.5's pre-run found; initially a placeholder
  commented `// fixed in 3.5`, the test is written *after* the pre-run)
- `The_cancellation_token_reaches_the_codec` (the fake records the token)
- `Argument_validation` (null body / serde)

Red: compile error (`ReadValueOrDefault*`, `ReadValue`), and `A_root_null_…`, `An_empty_body_…` fail against the as-built reader.

### Task 3.4 — `SerdeValues` and the missing-body peek (P7a-9, P7a-10)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeValuesTests.cs` (Unit), `internal` access through
`InternalsVisibleTo`:

- `RequireNonNull_returns_a_non_null_reference` / `…_throws_DeserializationException_naming_T_for_a_null_reference`
- `RequireNonNull_returns_default_for_a_non_nullable_value_type_default` (a boxed `0` is not null; `T = int` never reaches the throw)
- `RequireNonNull_admits_a_null_Nullable` (`T = int?`: returns `null`; the helper's rule is `typeof(T).IsValueType` →
  accept, else throw on `null`)
- `The_message_names_ReadValueOrDefaultAsync` (the guidance string of P7a-9)
- `Peek_returns_null_for_an_empty_stream` / `Peek_returns_a_stream_replaying_the_first_byte` / `Peek_disposal_disposes_the_underlying_stream_once`
  (sync and async twins; the underlying `DisposeCountingStream`; the test reads the wrapper to EOF **before** disposing it and
  asserts a count of 1)

**Production.** `src/Dexpace.Sdk.Core/Serialization/SerdeValues.cs`, `internal static class SerdeValues`:

- `internal static T RequireNonNull<T>(T? value)` — throws `DeserializationException("The response body is the JSON literal null,
  but '" + typeof(T) + "' is not nullable; use ReadValueOrDefaultAsync to accept null.")` when `value is null && !typeof(T).IsValueType`;
  returns `value!` otherwise. Offered to 7b (design, cross-sub-phase interfaces). **Convergence step (design: "If 7b lands first with its own check, 7a's PR 3
  converges both on the helper"):** before opening PR 3, grep the merged tree (`grep -rn "DeserializationException" src/Dexpace.Sdk.Core/Sse`
  and any typed SSE null check); if 7b has landed its own root-null check, route it through `SerdeValues.RequireNonNull` with the
  same message and note the change in the PR body and the checklist. If 7b has not landed, record "offered, not yet consumed".
- `internal static Stream? PeekFirstByte(Stream stream)` and `internal static async ValueTask<Stream?> PeekFirstByteAsync(Stream
  stream, CancellationToken ct)` — read one byte; zero → return `null` (the caller disposes `stream`); else wrap in a
  `PrefixedReadStream(new[] { b }, 1, null, stream, closeOnce, closeOnceAsync)` (R2), where the two delegates are wrapped in a
  local close-once latch (an `Interlocked.Exchange` flag shared by both) around `stream.Dispose`/`stream.DisposeAsync`:
  `PrefixedReadStream` calls its close delegate when the tail reaches EOF **and** again on `Dispose` (its `_disposed` latch guards
  only Dispose against Dispose), so the raw delegates would dispose the real stream twice. The wrapper is *returned*; the real
  stream is closed exactly once, at EOF or at wrapper disposal, whichever comes first.
- `internal static DeserializationException NoBody<T>()` — `"The response has no body to deserialize as '{typeof(T)}'."`.

**IDs:** `SERDE-13`, `SERDE-27` (body half). **Verify:** V-fast `SerdeValuesTests`.

### Task 3.5 — The reshaped readers (`SERDE-3`, `SERDE-9`, `SERDE-12`, `SERDE-13`; P7a-9 to P7a-12) — Breaking 1, 2, 3

**Pre-run (R4).** Before changing anything run `dotnet test --project tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests
--configuration Release --filter-class "*BodyConvenienceTests"` to see green; then after the change below, if
`ReadValueAsync_is_single_use` goes red, read the actual exception type, fix the assertion to it with a comment naming P7a-11,
record the difference in the Breaking list and fill in `A_second_read_surfaces_the_as_built_exception` (3.3).

**Production.** `src/Dexpace.Sdk.Core/Serialization/ResponseBodySerdeExtensions.cs`:

```csharp
public static async ValueTask<T> ReadValueAsync<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default)
{
    var value = await ReadCoreAsync<T>(body, serde, cancellationToken).ConfigureAwait(false);
    return SerdeValues.RequireNonNull(value);
}

public static ValueTask<T?> ReadValueOrDefaultAsync<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default) =>
    ReadCoreAsync<T>(body, serde, cancellationToken);

public static T ReadValue<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default) { … ReadCore<T>, RequireNonNull … }
public static T? ReadValueOrDefault<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default) => ReadCore<T>(…);
```

`ReadCoreAsync<T>` (private, each helper under 70 lines, `ConfigureAwait(false)` everywhere): validate arguments **before** any
disposal (an argument error must not dispose a body the caller still owns: the body is only disposed once it has been opened or
the validation passed); then

```csharp
Exception? primary = null;
try
{
    if (body.ContentLength == 0) { throw SerdeValues.NoBody<T>(); }
    var stream = await body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
    var peeked = await SerdeValues.PeekFirstByteAsync(stream, cancellationToken).ConfigureAwait(false);
    if (peeked is null) { await stream.DisposeAsync().ConfigureAwait(false); throw SerdeValues.NoBody<T>(); }
    await using var scope = peeked.ConfigureAwait(false);
    return await serde.DeserializeAsync<T>(peeked, cancellationToken).ConfigureAwait(false);
}
catch (Exception ex) { primary = ex; throw; }
finally { await Disposal.DisposeQuietlyAsync(body, primary).ConfigureAwait(false); }
```

(an `IOException` or any non-fatal exception stays the primary; `StreamConsumedException` from `OpenReadAsync` propagates; a
fatal exception is not swallowed by `Disposal`). `ReadCore<T>` mirrors it with `OpenRead`, `PeekFirstByte`, `serde.Deserialize<T>(Stream)`
and `Disposal.DisposeQuietly`. Docs on each overload: `<exception>` for `DeserializationException`, `StreamConsumedException`,
`IOException`; `<remarks>` with the three **Breaking** paragraphs (items 1 to 3), and the cross-reference to `ReadValueOrDefault*`.
`Pageable.cs` is **not** edited: its `?? throw` compiles (fact 10, task 0.1); if task 0.1 found otherwise, the one-line
deletion goes in this task and is flagged to 7c.

**PublicAPI (core).** Edit the `ReadValueAsync<T>` line (`ValueTask<T>`), add `ReadValueOrDefaultAsync<T>`, `ReadValue<T>`,
`ReadValueOrDefault<T>`.

**IDs:** `SERDE-3`, `-9`, `-12`, `-13`, `-27` (body half). **Verify:** V-fast `ReadValueTests`, `SerdeValuesTests`, then the Core
suite for `Pageable` (`dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*Pageable*"`).

### Task 3.6 — `HttpResponseException.GetError<T>` (3a hand-off; P7a-13)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Errors/HttpResponseExceptionGetErrorTests.cs` (Unit), mirroring the async
tests of `BodyConvenienceTests` over `Utf8LiteralSerde`:

- `GetError_decodes_the_buffered_error_body` / `GetError_returns_null_for_a_null_literal` (keeps `T?`, documented "possibly null")
- `GetError_throws_ResponseNotReadException_on_a_consumed_body` (the inner exception is the `StreamConsumedException`)
- `GetError_does_not_dispose_the_exception_response` (the buffered response stays readable by `GetErrorAsync` *only if* the body is
  replayable; the test uses `FromReplayableBytes` through `ErrorBodyBuffer.Capture` and asserts both twins return the same value)
- `GetError_argument_validation`

**Production.** `TransportExceptions.cs`: `public T? GetError<T>(ISerde serde)` — `Response.Body.OpenRead()` + `serde.Deserialize<T>(Stream)`
in a `using`, `catch (StreamConsumedException ex)` → `ResponseNotReadException` (same message as the async twin). No
`CancellationToken` (the sync stream member takes none; P7a-13). **PublicAPI.** `HttpResponseException.GetError<T>`.

**IDs:** none new (3a hand-off); supports `SERDE-3`. **Verify:** V-fast `HttpResponseExceptionGetErrorTests`;
`EnsureSuccessGetErrorRoundTripTests` (STJ, a `Unit` regression class) stays green in the full-suite run, and the Core error-mapping `Security` classes are unchanged and green.

### Task 3.7 — The STJ override and the adapter's tests (`SERDE-3`, `SERDE-5`, `SERDE-6`, `SERDE-9`, `SERDE-12`)

**Failing tests.** `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/SystemTextJsonSerdeTests.cs` additions (Unit):

- `Sync_Deserialize_from_a_stream_decodes_and_leaves_it_open` (`SERDE-3`)
- `Sync_Deserialize_from_a_stream_propagates_an_IOException_unwrapped` (`SERDE-12`)
- `Sync_Deserialize_from_a_stream_wraps_malformed_input` (`SERDE-9`: `DeserializationException`, inner `JsonException`)
- `A_close_counting_stream_sees_zero_closes_across_all_three_stream_members` (`SERDE-3`: `SerializeAsync`, `DeserializeAsync`,
  sync `Deserialize(Stream)`; the existing `DisposeCountingStream`)
- `A_list_of_dto_decodes_typed_and_an_unregistered_parametric_target_throws_naming_it` (`SERDE-6`: `List<Widget>` decodes
  element-typed; `List<ApiError>` is unregistered in `TestJsonContext`→ `DeserializationException` whose message contains
  `ApiError`, on this host a missing `JsonTypeInfo`, §7.3)
- `A_dto_field_access_returns_typed_values` (`SERDE-5`)
- `A_generic_helper_decodes_into_the_closed_type` already exists: the new test is its sibling for the Tristate type

New `ReadValueEndToEndTests.cs` (Unit, STJ suite), over the real adapter and `CreateDefaultOptions`:

- `ReadValueAsync_of_a_root_null_names_T_through_every_overload` (`ReadValueAsync`, `ReadValue`, and, after PR 4, the two handlers;
  here the first two plus `ReadValueOrDefault*` returning null; task 4.2 extends it to both handlers over the real adapter)
- `A_Tristate_PATCH_model_decodes_from_a_response_body` (`{"name":null,"size":3}` → Null/Present/Absent)
- `A_204_style_empty_body_names_T` (`SERDE-27`'s missing-body rule through the real adapter, replacing STJ's generic "no JSON
  tokens")

`BodyConvenienceTests` is updated per R4/3.5 only where an assertion's type changed.

**Production.** `SystemTextJsonSerde.cs`:

```csharp
public T? Deserialize<T>(Stream source)
{
    ArgumentNullException.ThrowIfNull(source);
    var info = GetTypeInfo<T>(forSerialize: false);
    try { return JsonSerializer.Deserialize(source, info); }
    catch (Exception ex) when (ex is JsonException or NotSupportedException)
    { throw new DeserializationException($"Failed to deserialize JSON to '{typeof(T)}'.", ex); }
}
```

STJ's stream overload leaves the stream open (`SERDE-3`) and `IOException` is not in the filter (`SERDE-12`). **PublicAPI (adapter).**
`SystemTextJsonSerde.Deserialize<T>(System.IO.Stream! source) -> T?`.

**IDs:** `SERDE-3`, `-5`, `-6`, `-9`, `-12`, `-13`. **Verify:** V-fast the three classes above.

### Task 3.8 — Close-out (PR 3)

1. `CHANGELOG.md`: "Phase 7a" `Changed`/**Breaking** items 1 to 3 and 5 (design list), `Added` for the new readers, the sync stream
   member and `GetError<T>`. Items are written exactly as the design's "Breaking changes".
2. Re-run `grep -rn "ReadValueAsync" src tests docs/sdk-documentation README.md` and update any doc sentence that says the reader
   "returns null" (`docs/sdk-documentation/bodies.md`, `http.md`, `seams.md` as found in task 0.1 step 4), noting the doc edits in
   the PR body.
3. V-gate. Expected: green.

---

## PR 4 — Response handlers and `TypedResponse<T>`

**Gate: PR 3.** Rows: `SERDE-27`, `SERDE-28`, `HTTP-44`, `HTTP-45`. Files:
`src/Dexpace.Sdk.Core/Http/Response/{IResponseHandler,TypedResponse}.cs`, `src/Dexpace.Sdk.Core/Serialization/{ResponseHandlers,DeserializingHandler,SuccessDeserializingHandler}.cs`,
`PublicAPI.Unshipped.txt`, `tests/Dexpace.Sdk.TestSupport/Transports/GateResponseHandler.cs`,
`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/ReadValueEndToEndTests.cs` (extended), and the tests named below.
Branch `<issue>-phase-7a-pr4-handlers`.

### Task 4.1 — `IResponseHandler<T>` and `ResponseHandlers.Deserialize<T>` (`SERDE-27`; P7a-16, P7a-17)

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/ResponseHandlersTests.cs`, class `ResponseHandlersTests` (Unit), over
`Utf8LiteralSerde` and a `TestResponses.Create(…)` response over a `CountingPayloadBody` and a response-level dispose counter
(the existing `DisposeCountingTransport`/`TestResponses` pattern; if `Response` has no observable counter, the body's counter is the
evidence and the response's latch is asserted by a second `DisposeAsync` being a no-op):

`SERDE-27`'s five-case matrix as five tests:

1. `Valid_body_decodes_and_disposes_the_response_once`
2. `A_204_response_fails_naming_T_and_disposes` (`ContentLength 0`)
3. `A_malformed_body_is_a_SerdeException_with_an_inner_exception_and_disposes` (`bad`)
4. `A_mid_stream_IOException_propagates_unwrapped_and_disposes` (`io`)
5. `The_response_is_disposed_in_every_case` (a parametrised `[Theory]` over the four above: `DisposeCount == 1`, and the thrown
   failure is never replaced by a dispose failure)

and: `A_root_null_names_T` (handler rejects as the reader does), `The_cancellation_token_reaches_the_codec`,
`SERDE-3_and_the_handler_dispose_bind_different_objects` (the codec leaves the *stream* open - the counting stream sits under the peek wrapper, so
the codec boundary is the wrapper, and the underlying stream may legitimately close at EOF through the close-once latch (task 3.4);
the test reads only up to, not to, EOF and asserts 0 closes from the codec, then the handler's response dispose closes it exactly once: both behaviours in one
test, as Ruby's design warns), `Handler_validates_arguments` (null serde at factory time → `ArgumentNullException`; null response at
`HandleAsync`).

Red: compile error.

**Production.**

- `src/Dexpace.Sdk.Core/Http/Response/IResponseHandler.cs` — `public interface IResponseHandler<T>` with
  `ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken);` documented "owns and disposes `response`".
- `src/Dexpace.Sdk.Core/Serialization/ResponseHandlers.cs` — `public static class ResponseHandlers` with `Deserialize<T>(ISerde)`
  and `DeserializeOnSuccess<T>(ISerde)` (the second lands in 4.2; this task adds the first and a `throw new NotImplementedException()`
  is **not** committed: add the second member in task 4.2's commit).
- `DeserializingHandler.cs` — `internal sealed class DeserializingHandler<T>(ISerde serde) : IResponseHandler<T>`:

```csharp
public async ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(response);
    Exception? primary = null;
    try { return await response.Body.ReadValueAsync<T>(serde, cancellationToken).ConfigureAwait(false); }
    catch (Exception ex) { primary = ex; throw; }
    finally { await Disposal.DisposeQuietlyAsync(response, primary).ConfigureAwait(false); }
}
```

  Add `typeof(ResponseHandlers)` to `SerdeSeamArchitectureTests.s_seams` (R6). **PublicAPI.** `IResponseHandler<T>`,
  `ResponseHandlers`, `Deserialize<T>`.

**IDs:** `SERDE-27`. **Verify:** V-fast `ResponseHandlersTests`.

### Task 4.2 — `DeserializeOnSuccess<T>` and the `Security` redaction class (`SERDE-28`; P7a-18, P7a-19, P7a-23)

**Failing tests.** `ResponseHandlersTests` (continued) (Unit):

- `A_200_decodes` and `A_204_is_a_missing_body_naming_T` (2xx delegates to the one decode path)
- `A_200_root_null_names_T` (`SERDE-13` through `DeserializeOnSuccess`: a 200 with body `null` over `Utf8LiteralSerde` throws
  `DeserializationException` naming `T`, one response dispose)
- `A_500_throws_HttpResponseException_with_the_status_and_a_buffered_body_readable_after_the_live_response_is_disposed`
- `A_599_non_canonical_status_is_still_an_error_response` (400-599 range)
- `A_400_buffers_at_most_MaxBufferedErrorBytes` (a body of `MaxBufferedErrorBytes + 10`: the exception's body is truncated at the
  existing bound; the test asserts only that it is readable and not longer than the bound, the same behaviour as `ErrorBodyBuffer`)
- `The_live_response_is_disposed_exactly_once_on_the_error_branch` (the capture releases it; this branch adds no second dispose)
- `A_304_fails_with_a_DeserializationException_leading_with_the_code_and_one_dispose` (message starts `304`)
- `A_103_informational_fails_the_same_way`
- `The_third_branch_message_carries_the_raw_ETag` (`ETag: "abc"` verbatim, including weak `W/"x"`)
- `The_third_branch_message_omits_absent_parts` (no `ETag`/`Location` header → neither label present)
- `A_relative_Location_is_resolved_against_the_request_uri_then_redacted`
- `An_unparseable_Location_is_replaced_by_the_placeholder` (`<unparseable>`; no exception from the message builder)

**`ReadValueEndToEndTests` (STJ suite, listed among PR 4's touched files).** Task 4.2 extends
`ReadValueAsync_of_a_root_null_names_T_through_every_overload` with `ResponseHandlers.Deserialize<T>` and
`ResponseHandlers.DeserializeOnSuccess<T>` (a 200) over the real adapter, so the test covers all four non-null overloads plus the
handlers.

New `Security` class `tests/Dexpace.Sdk.Core.Tests/Security/StatusAwareHandlerLocationRedactionTests.cs`,
`[Trait("Category", "Security")]` (never deleted or loosened). The class holds four tests:

- `A_302_with_a_secret_in_the_Location_query_does_not_leak_it` (`Location: https://h/p?access_token=s3cr3t`; the exception message
  does not contain `s3cr3t`, and does contain the redacted form from `UrlRedactor.Default`)
- `A_relative_Location_secret_does_not_leak_either` (`/p?access_token=s3cr3t`)
- `The_message_does_not_contain_userinfo` (`https://u:p4ss@h/p`)
- `A_Location_with_obs_text_or_HTAB_is_rendered_without_splitting_the_message` (a `Location` and `ETag` carrying HTAB and obs-text
  bytes, the most `Headers.Builder.AddInbound` accepts; `AddInbound` rejects CR, LF and every other control character other than
  HTAB, so a CR/LF fixture cannot be built and no such Security test exists; the message is a single line)

The `?` replacement of control characters stays as defensive code in the message builder, covered by a **Unit** test in
`ResponseHandlersTests` that calls the private builder through an `internal` seam (`SuccessDeserializingHandler<T>.BuildMessage`,
`InternalsVisibleTo`) with a raw string holding `\r\n`; it is not Security because `Headers` can never deliver such a value.

Red: compile error for `DeserializeOnSuccess`, then red per assertion.

**Production.** `ResponseHandlers.DeserializeOnSuccess<T>(ISerde)` and `SuccessDeserializingHandler.cs` — `internal sealed class
SuccessDeserializingHandler<T>(ISerde serde) : IResponseHandler<T>`:

```csharp
private readonly DeserializingHandler<T> _decode = new(serde);   // instance field: a static cannot capture the primary-ctor parameter

public async ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(response);
    if (response.IsSuccess) { return await _decode.HandleAsync(response, cancellationToken).ConfigureAwait(false); }
    if (response.IsError)
    {
        var captured = await ErrorBodyBuffer.CaptureAsync(response, cancellationToken).ConfigureAwait(false);
        throw ErrorMapping.ToException(captured);
    }

    await response.DisposeAsync().ConfigureAwait(false);
    throw new DeserializationException(NotDecodableMessage(response));
}
```

(`IsSuccess` is 200–299; `IsError` is 400–599, via `Status`; a non-canonical 599 passes `IsError`, task 4.2's test pins it). The third-branch message builder
is a private static helper: `$"{code} {reason}: expected a 2xx response to deserialize as '{typeof(T)}'."` then optionally
` ETag: {raw}.` and ` Location: {redacted}.`, where `redacted` is `UrlRedactor.Default.Redact(uri)` for a `Location` resolved
with `Uri.TryCreate(response.Request.Uri, raw, out var u)` and `<unparseable>` otherwise; header values are read **raw** through
`response.Headers.GetAll(…)` and never through the validating helpers (P7a-23), and control characters are replaced by `?` before
interpolation. The reason phrase comes from `Response.ReasonPhrase ?? Status` text. Both helpers stay under 70 lines.

**Mutation proof (Security).** Temporarily interpolate the raw `Location`; the first three Security tests (secret in query, relative
secret, userinfo) fail; the obs-text/HTAB test is a pin on single-line rendering and is not expected to fail; revert.

**IDs:** `SERDE-28`. **Verify:** V-fast `ResponseHandlersTests`; V-sec.

### Task 4.3 — `TypedResponse<T>` (`HTTP-44`, `HTTP-45`; P7a-14, P7a-15)

**Test support.** `tests/Dexpace.Sdk.TestSupport/Transports/GateResponseHandler.cs`: `GateResponseHandler<T>` implementing
`IResponseHandler<T>`: `Calls` (int, `Interlocked`), `Started` (a `TaskCompletionSource`), `Gate` (a `TaskCompletionSource<T>`
the handler awaits), `RecordedToken`, optional `ThrowOnCall`.

**Failing tests.** New `tests/Dexpace.Sdk.Core.Tests/Http/Response/TypedResponseTests.cs`, class `TypedResponseTests` (Unit):

- `Metadata_is_readable_with_the_body_never_opened` (`HTTP-44`: a body whose `OpenReadAsync` throws; `Request`, `Status`,
  `Headers`, `Protocol`, `ReasonPhrase` all readable; handler `Calls == 0`)
- `One_handler_call_across_three_sequential_GetValueAsync` (same value each time, `Assert.Same` for a reference value)
- `Sixty_four_concurrent_first_callers_run_the_handler_once` (`HTTP-45`: callers released by a start gate; one `Calls`, all results
  `Assert.Same`)
- `A_null_success_is_memoized` (a handler returning `null!` for a nullable `T`: second call returns null without re-running)
- `A_failure_is_memoized_as_the_same_exception_object` (`Assert.Same(first, second)` on caught exceptions; `Calls == 1`)
- `A_blocked_handler_does_not_block_a_second_caller` (the observable form of `HTTP-45`: while the handler waits on its gate,
  `GetValueAsync` called from a second thread returns an **incomplete** `ValueTask` synchronously; `IsCompleted == false`,
  measured without awaiting; then completing the gate completes both)
- `A_callers_cancelled_wait_leaves_the_parse_running` (caller 1 cancels its token: its wait throws `OperationCanceledException`;
  the handler is not cancelled; caller 2 gets the value; `RecordedToken` is the construction token, not caller 1's)
- `A_cancelled_construction_token_is_a_memoized_failure` (the handler throws `OperationCanceledException` under the construction
  token; both later calls re-throw the same instance; the handler is not re-run)
- `Dispose_forwards_to_the_response_once` (`DisposeCount == 1` after `Dispose()` then `DisposeAsync()`)
- `Disposing_during_a_parse_memoizes_the_resulting_failure` (a handler reading the body after dispose fails; that failure sticks)
- `A_fatal_exception_is_recorded_and_rethrown` (an `OutOfMemoryException`-like fatal per `ExceptionFacts.IsFatal`: recorded in the
  source, then the exception still surfaces to the awaiting caller; the test asserts the awaiting caller sees it, and the fault is
  not silently swallowed)
- `Argument_validation` (null response / handler)
- `The_wrapped_response_is_not_exposed` (reflection pin: no public member of type `Response` other than `Request`-typed metadata
  properties; no `Body` property)

Red: compile error.

**Production.** `src/Dexpace.Sdk.Core/Http/Response/TypedResponse.cs`, the design's shape (position F):

```csharp
public sealed class TypedResponse<T> : IAsyncDisposable, IDisposable
{
    private readonly Response _response;
    private readonly IResponseHandler<T> _handler;
    private readonly CancellationToken _cancellationToken;
    private Task<T>? _value;

    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        var task = Volatile.Read(ref _value) ?? Start();
        if (!cancellationToken.CanBeCanceled) { return new ValueTask<T>(task); }
#pragma warning disable RS0030 // SEAM-30: the memoized task keeps the result, so an abandoned wait orphans nothing; the handler owns and disposes the Response
        return new ValueTask<T>(task.WaitAsync(cancellationToken));
#pragma warning restore RS0030
    }

    private Task<T> Start()
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _value, tcs.Task, null) is { } winner) { return winner; }
        _ = RunAsync(tcs);              // not a background launch: runs inline on the winner's thread until its first await
        return tcs.Task;
    }

    private async Task RunAsync(TaskCompletionSource<T> tcs)
    {
        try
        {
            var result = await _handler.HandleAsync(_response, _cancellationToken).ConfigureAwait(false);
#pragma warning disable RS0030 // SEAM-30: the result is memoized in the task itself; nothing is orphaned if no caller awaits it
            tcs.TrySetResult(result);
#pragma warning restore RS0030
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);                       // every exception, cancellation included: same object on every access
            if (ExceptionFacts.IsFatal(ex)) { throw; }     // recorded, then not swallowed
        }
    }
}
```

The metadata properties are copied from `response` in the constructor; `Dispose`/`DisposeAsync` forward to the response's latched
dispose (`HTTP-43`). No lock, no `Lazy`, no `Task.Result` anywhere (banned). The two RS0030 suppressions (`TrySetResult`, `WaitAsync`) are
scoped pragmas citing SEAM-30 and are recorded in the checklist's deviations (task 5.3). The `_ = RunAsync(...)` line follows the form task 0.1
step 5 found accepted. The `<remarks>` explain the construction-token rule and the "caller token cancels only its wait" rule, and
that the handler owns and disposes the response on its own paths, so a caller who never calls `GetValueAsync` must dispose the
wrapper.

**PublicAPI.** `TypedResponse<T>` and every member (ctor with optional token, `Request`, `Status`, `Headers`, `Protocol`,
`ReasonPhrase`, `GetValueAsync`, `Dispose`, `DisposeAsync`).

**IDs:** `HTTP-44`, `HTTP-45`. **Verify:** V-fast `TypedResponseTests`; repeat the concurrency test 200 times in a loop locally
(`for i in $(seq 200); do dotnet test … --filter-class "*TypedResponseTests" --filter-method "*Sixty_four*" || break; done`, not
committed) to rule out a scheduling-dependent pass.

### Task 4.4 — Close-out (PR 4)

1. `CHANGELOG.md`: "Phase 7a" `Added` (`IResponseHandler<T>`, `ResponseHandlers`, `TypedResponse<T>`; `SERDE-27`, `SERDE-28`,
   `HTTP-44`, `HTTP-45`).
2. V-gate and V-sec. Expected: green; exactly one new file under `tests/Dexpace.Sdk.Core.Tests/Security`.

---

## PR 5 — AOT smoke, documentation and close-out

**Gate: PRs 1 to 4.** Files: `tests/Dexpace.Sdk.AotSmoke/{SmokeChecks,SmokeModels}.cs`, `docs/sdk-documentation/serde.md`,
`docs/work/mvp/phase7/phase7a/…` (filed), `CHANGELOG.md`, `CLAUDE.md`, the roadmap, the design chapters. Branch
`<issue>-phase-7a-pr5-closeout`.

### Task 5.1 — NativeAOT smoke `CheckPhase7aSerdeAsync` (`NFR-9`; exit criterion 3; fact 3)

**Failing check.** The smoke executable is the test: add the check, build, publish and run; the first run fails if a model is
missing from the context.

`SmokeModels.cs` (append only; 7b and 7c append too): `internal sealed record WidgetPatch(Tristate<string> Name, Tristate<int>
Size, Tristate<string> Note);` and `[JsonSerializable(typeof(WidgetPatch))]` plus `[JsonSerializable(typeof(Tristate<string>))]`,
`[JsonSerializable(typeof(Tristate<int>))]` on `SmokeJsonContext` (a second attribute line each; no restructuring of the file).

`SmokeChecks.cs`: one new method and **one** call line in `RunAllAsync` (convention 10):

```csharp
private static async Task CheckPhase7aSerdeAsync()
{
    var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(SmokeJsonContext.Default));
    var patch = new WidgetPatch(Tristate.Null, Tristate.Present(3), default);
    // 1. the wire bytes: Absent note omitted, Null name written, camelCase from CreateDefaultOptions
    // 2. send as a PATCH body through RequestBody.FromValue over the in-process transport; echo the bytes back
    // 3. decode via TypedResponse<WidgetPatch> + ResponseHandlers.DeserializeOnSuccess; Null/Present/Absent hold
    // 4. a second GetValueAsync returns the same instance
    // 5. ReadValueAsync<WidgetPatch> of a body containing `null` throws DeserializationException
    …
}
```

with `Expect(…, "phase 7a: …")` for each of the five points (`{"name":null,"size":3}` byte-for-byte). The value-type
`Tristate<int>` is what proves P7a-6's suppression. If the published binary throws on `Tristate<int>`, STOP (task 0.1's AOT
result was wrong): reopen P7a-6.

**Verify:** `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke`
exits 0 and prints the new check's name; publish emits **no new** IL warnings beyond the single justified `IL2067` suppression
(there should be zero published warnings: the suppression removes it).

### Task 5.2 — User documentation (`docs/sdk-documentation/serde.md`; exit criterion 6)

Create `docs/sdk-documentation/serde.md`, opening "As built by phase 7a … written against source on 2026-10-09", shaped by Ruby
`serde.md` and Node `write-a-serde.md`/`write-a-response-handler.md` (design, ports): the seam (six members), the four profiles
(link to `seams.md`, not restated), `Tristate<T>` (the three states, the constructors, `Match`, pattern matching, the PATCH
recipe `patch with { Name = Tristate.Null }`), `SystemTextJsonSerde` (copy-not-freeze, `CreateDefaultOptions`, strict coercion
table with the nine rows, `RespectNullableAnnotations`'s consequence R4 in the design, `AddTristateSupport` for direct
`JsonSerializer` use, generation-mode note from task 0.1), the readers (`ReadValue*`, root `null`, missing body, disposal), the
handlers and a worked custom `IResponseHandler<T>`, `TypedResponse<T>` (laziness, once, memoized failure, cancellation), the
dictionary-value compromise, and "what is not built" (per-read cap, P7a-21; the Newtonsoft adapter). Update the cross-links in
`docs/sdk-documentation/{seams,bodies,http}.md` where they say the reader returns `null` (task 3.8 item 2). Append
`[serde.md](./sdk-documentation/serde.md) (phase 7a)` to `docs/README.md`'s `sdk-documentation` ownership row (the row the probe
asks for, as phase 6c did), re-deriving the line against the merged file since 7b and 7c add their own pages.

**Verify:** `dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations`. Expected: no new findings
(including the ownership-table row for `serde.md`).

### Task 5.3 — The checklist (step 4 of the phase workflow)

Write `docs/superpowers/plans/…`-adjacent checklist at its filing location (task 5.6 moves it):
`docs/work/mvp/phase7/phase7a/2026-10-09-phase7a-serde-checklist.md`, following the 6b checklist's legend and structure
(`docs/work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect-checklist.md`): a header with the date and branch, the legend, a
"Red evidence" paragraph naming each mutation proof (tasks 1.4, 2.6, 4.2) and the task-0.1 results (facts 3, 8, 9, 10, 11 with
the printed numbers), then **one row per ID: 32 rows, no blank cells**, each naming the test(s), the `[Trait]` category and the file.
Planned content (the [checklist section](#checklist-one-row-per-owned-id) below is the skeleton). Add the sections: "Deviations
from the plan", "The Security classes" (one added, none edited), "Work on other owners' rows", the ruled-open items (Q1/P7a-9
and P7a-21) and the issue #1 closing comment's text (the lead posts it; no agent posts to GitHub).

### Task 5.4 — `CHANGELOG.md`

Consolidate the "Phase 7a" entries from tasks 1.5, 2.8, 3.8 and 4.4 under one sub-heading in `[Unreleased]`: **Added**,
**Changed (Breaking)** items 1 to 5 verbatim from the design, **Fixed** (`SERDE-26`). Re-derive hunks against the merged
file (7b and 7c edit it too).

### Task 5.5 — Dated corrections, roadmap note, `CLAUDE.md`, hand-offs

Each correction is a dated entry, never a rewrite (the docs are frozen to routine work):

- Design §7.3: the lazy wrapper is a CAS on a `TaskCompletionSource<T>` not `Lazy<Task<T>>` (P7a-15, fact 11 with task 0.1's
  numbers); the root-`null` route is `ReadValueOrDefault(Async)`; "only on the `net10.0` target" is moot (D1); As-built line → built.
- Design §3.4: `ISerde` has a sixth member; the cap stays a constant (P7a-21, correcting the 2026-10-07 sentence).
- Design §12: the SERDE row cites the 7a checklist.
- 3b checklist: `HTTP-44`/`HTTP-45` ⏳ 7a → ✅ pointing at the 7a checklist's carried rows. 3a checklist / 5a design: the
  P5a-23 row names its closure (P7a-21); `P5a-20` names P7a-22.
- Roadmap: a dated entry appended to `## Phase Status Notes` for 7a (what was built, the 5 PRs, the open rulings, the hand-offs
  of the design's "Hand-offs to later phases": 8a, 8b, 9, 10, 12), and the Phase 7 card's 7a bullets get the status note; **no
  earlier entry is rewritten**.
- `CLAUDE.md`: "What is genuinely unbuilt" drops "tri-state PATCH" and gains phase 7a's sentence pointing at
  `docs/sdk-documentation/serde.md`; the layout's `Serialization/` line lists `Tristate` and `ResponseHandlers`; `Http/Response/`
  lists `TypedResponse` and `IResponseHandler`. Merge against 7b's and 7c's edits to the same paragraph.
- `docs/knowledge/notes/`: a note recording what the implementation found (fact 8's fast-path result, fact 9, fact 11). A note,
  never an edit to `harvested/`.
- Issue #1: the closing-comment text from the design is carried in the checklist; **the plan posts nothing**.

### Task 5.6 — File the phase documents and run the probe

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 7a            # dry run
dotnet run --project .claude/skills/housekeeping/src -- apply --delivery mvp --phase 7a --write    # git mv
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

Expected: the spec, this plan and the checklist land under `docs/work/mvp/phase7/phase7a/` with the dates and slugs the
probe prints, links resolve, cited requirement IDs are attributed correctly, `docs/superpowers/` is left empty of 7a files.
Fix whatever the last probe reports; run the full V-gate one last time. No commit, push or `gh` command is part of this task.

---

## Keeping the `Security` classes green

No phase-1 `Security` class is edited. `EnsureSuccessGetErrorRoundTripTests` (STJ, `Category=Unit`, not Security) and the Core error-mapping classes read
`GetErrorAsync`, whose contract does not change; the first stays green in every PR's full-suite run. `Category=Security` is run on
`Dexpace.Sdk.Core.Tests` and `Dexpace.Sdk.Http.SystemNet.Tests` at every PR's V-gate (the STJ suite has no Security tests, and a
zero-match filter would exit 8 under Microsoft.Testing.Platform).
7a adds exactly one `Security` class (task 4.2) and the V-gate's `git diff --stat` enforces "one file, added".

---

## Checklist: one row per owned ID

Skeleton for task 5.3 (disposition per the design; the evidence column is filled with the as-built test names).

| ID | Level | Planned exit | Task | Primary evidence |
|---|---|---|---|---|
| `SERDE-1` | MUST | ✅ already met | — | `SystemTextJsonSerdeTests.SerializeAsync_then_DeserializeAsync_round_trips` |
| `SERDE-2` | MUST | ✅ already met (2b) | — | `SerdeSeamTests.DefaultMediaType_has_no_default_implementation` |
| `SERDE-3` | MUST | ✅ test widened | 3.1, 3.7 | close-counting tracker over all three stream members |
| `SERDE-4` | MUST | ✅ already met (2b) | — | `SerdeProfileTests.Fixed_buffer_*` |
| `SERDE-5` | MUST | ✅ by construction + test | 3.7 | typed-field-access DTO test |
| `SERDE-6` | MUST | ✅ by construction + test | 3.7 | `List<Dto>` / unregistered target test |
| `SERDE-7` | MUST | ✅ by construction | 3.5 | `ReadValue*` helpers |
| `SERDE-8` | MUST | ✅ by construction | 3.1 | `SerdeSeamArchitectureTests` over the widened seam |
| `SERDE-9` | MUST | ✅ extended | 2.2, 3.5, 3.7 | wrapping on the sync stream member and the Tristate converter |
| `SERDE-10` | MUST | ✅ already met (2b) | — | `SerdeExceptionHierarchyTests` |
| `SERDE-11` | SHOULD | ✅ by language | — | no checked exceptions in C# (§11 item 15) |
| `SERDE-12` | MUST | ✅ extended | 3.1, 3.3, 3.7, 4.1 | unwrapped `IOException` at every layer |
| `SERDE-13` | MUST | ✅ built, ruled (P7a-9, open) | 3.4, 3.5, 4.1 | root-`null` names `T` through every overload |
| `SERDE-14` | MUST | ✅ built (covariance SHOULD unmet, §10 entry 21) | 1.1–1.4 | `TristateTests`, `TristateArchitectureTests` |
| `SERDE-15` | MUST | ✅ built | 2.2, 2.3 | `Absent_omits_the_key` (default-mode context) |
| `SERDE-16` | MUST | ✅ built | 2.2, 2.3 | decode matrix incl. `Tristate<int>` |
| `SERDE-17` | MUST | ✅ built | 1.2, 2.2 | `Default_is_Absent`, mixed-document decode |
| `SERDE-18` | SHOULD | ✅ built | 1.2, 1.3 | `Match`, `FromNullable`, `TryGetValue`, `GetValueOrNull` |
| `SERDE-19` | MUST | ✅ built (P7a-5) | 2.3, 2.4 | wiring on by default from a bare options constructor |
| `SERDE-20` | SHOULD | ✅ built | 2.2 | top-level, array, dictionary positions |
| `SERDE-21` | MUST | ✅ built | 2.5, 2.6 | nine named coercion facts |
| `SERDE-22` | MUST | ✅ test | 2.6 | widening and `""`→`string` |
| `SERDE-23` | SHOULD | ✅ STJ default + test | 2.7 | unmapped member skipped |
| `SERDE-24` | SHOULD | ✅ STJ default + test | 2.7 | ISO-8601 round trip |
| `SERDE-25` | SHOULD | ✅ built | 2.5 | fresh instance per call |
| `SERDE-26` | MUST | ✅ built (fix), Breaking | 2.4 | caller's options never frozen or wired |
| `SERDE-27` | MUST | ✅ built | 3.4, 3.5, 4.1 | five-case handler matrix |
| `SERDE-28` | MUST | ✅ built | 4.2 | 2xx / 4xx-5xx / other, Security redaction |
| `SERDE-29` | SHOULD | ✅ test | 2.7 | 32 workers × 500 on one serde |
| `SERDE-30` | MAY | ✅ built | 1.1, 1.2 | `ToString` string-equality |
| `HTTP-44` | MUST | ✅ built (carried from 3b) | 4.3 | metadata without the body; one parse; memoized |
| `HTTP-45` | MUST | ✅ built (carried from 3b), P7a-15 | 4.3 | 64 concurrent callers; blocked handler does not block |

### Work on other owners' rows (no checklist row in 7a)

| ID | Owner | What 7a does |
|---|---|---|
| `BODY-16` | 3b | extended to the typed reader (P7a-11): the body is disposed on every path |
| `RECOV-15` / `BODY-30` | 4b/4c | `SERDE-28`'s error branch reuses `ErrorBodyBuffer` + `ErrorMapping` (the one capture site, the 1 MiB bound) |
| `XCUT-19` | 5b | `Location` in the third-branch message goes through `UrlRedactor` |
| `NFR-9` | phase 0 | one justified `IL2067` suppression, exercised by the AOT smoke's `Tristate<int>` |
| `HTTP-43` | 3b | `TypedResponse` disposal forwards to the response's latched dispose |
| `SEAM-22` | 2b | `SerdeSeamArchitectureTests` widened over the new seams |

---

## Traceability: PR → rows → tasks

| PR | Rows | Tasks |
|---|---|---|
| 0 (no PR) | facts 3, 8, 9, 10, 11 | 0.1 |
| 1 | `SERDE-14`, `-17`, `-18`, `-30` | 1.1–1.5 |
| 2 | `SERDE-15`, `-16`, `-19`–`-26`, `-29` | 2.1–2.8 |
| 3 | `SERDE-3`, `-5`–`-13` | 3.1–3.8 |
| 4 | `SERDE-27`, `-28`, `HTTP-44`, `HTTP-45` | 4.1–4.4 |
| 5 | exit criteria 3, 4, 6 | 5.1–5.6 |

**Task count: 31** (Task 0.1; 1.1–1.5; 2.1–2.8; 3.1–3.8; 4.1–4.4; 5.1–5.6).

---

## Findings while planning

1. **Fact 8 is the PR 2 risk.** If the default-mode context ignores `ShouldSerialize`, `Absent` would serialize as `null` and a
   PATCH silently clears fields. Task 0.1 measures it first; task 2.2's headline test runs against the default-mode context
   precisely to catch it.
2. **`ReadValueAsync_is_single_use` may change type after P7a-11** (R4): disposing the body before the second read can turn
   `StreamConsumedException` into `StreamClosedException`. The plan runs the test before editing it and records any difference
   as an additional Breaking line rather than loosening the assertion.
3. **`ISerde` default member needs an internal helper.** The DIM calls `BodyMaterializer.ReadAll` (R3); a future move of that
   helper must keep the cap semantics. Noted for the 8a conformance kit.
4. **Fatal exceptions in `TypedResponse.RunAsync`** are rethrown inside a discarded task and surface only as an unobserved task
   exception; the awaiting caller still sees the same exception through the memoized source. Accepted (design P7a-15: "recorded and
   then rethrown"); recorded in the checklist as a known limit.
5. **Tests in `Dexpace.Sdk.Core.Tests` cannot use `SystemTextJsonSerde`** (SEAM-2): all Core-side reader and handler tests
   run on the `TestSupport` fakes of R5, and the real-adapter evidence sits in the STJ suite's `ReadValueEndToEndTests`.
6. **Open for the lead, implemented as the design rules:** Q1/P7a-9 (`SERDE-13` does not bind `ISerde`'s own `T?`
   members) and P7a-21 (the per-read cap hand-off is declined). Reversing either after PR 3 costs one task each: for Q1, a
   `RequireNonNull` call in `SystemTextJsonSerde` plus its `PublicAPI` return annotation; for P7a-21, the twelve overloads of
   the design's argument.

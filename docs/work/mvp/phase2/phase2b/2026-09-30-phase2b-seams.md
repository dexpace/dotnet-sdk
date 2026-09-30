# Phase 2b — Seams: Implementation Plan

**Status:** Draft, for review. Written 2026-09-30 against `main` at `bf8f4ae`, on branch
`36-phase-2a-domain-model-design`. GitHub issue [#36](https://github.com/dexpace/dotnet-sdk/issues/36).
Design: [phase 2b seams design](2026-09-30-phase2b-seams-design.md), the authority for every decision below (the plan
cites its row, position or ledger entry rather than restating it). Scope authority: the
[phase 2 segmentation design](../2026-09-29-phase2-segmentation-design.md). Roadmap:
[v1 roadmap](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md), phase 2 card. Precedent for format and depth: the
[2a plan](../phase2a/2026-09-30-phase2a-domain-model.md).

**What this document is.** The roadmap's step 3 for sub-phase 2b: the numbered TDD tasks, in the design's landing
order (six pull-request-sized steps), each with its failing tests, its production change, its
`PublicAPI.Unshipped.txt` diff, its **Breaking** markings, its `CHANGELOG.md` entry, its requirement IDs and its
verification commands. It is not the checklist (step 4, written from what was built, task 6.3) and it writes no
production code.

**Scope.** 29 rows: `SEAM-1`–`SEAM-28` and `SEAM-30`. Every row maps to a task in the
[traceability table](#traceability-id--pr--task).

---

## Conventions that hold for every task

1. **TDD, in this order.** Write the tests named in the task; run them and see them fail for the stated reason (a
   compile error counts, and is the expected red for a new type or a changed signature); make the production change;
   run them green; then run the wider gate of the PR's last task. A test that passes before the change is a **pin**
   and says so; a pin is then proven able to fail by temporarily breaking the thing it pins (never committed).
2. **Every new `.cs` file** carries the two-line MIT header (`IDE0073`), in `src/` and `tests/` alike, and a curated
   `GlobalUsings.cs` set only (`ImplicitUsings` is off; every other namespace is imported in the file).
3. **`src/` code**: `ConfigureAwait(false)` on every `await` (`CA2007`), methods at most 70 lines (`MA0051`), `///` XML
   docs on every public member (CS1591), no new `PackageReference` in `Dexpace.Sdk.Core` (constraint 2). Nothing in 2b
   needs one: `Task.Factory`, `ArrayBufferWriter<byte>`, `Encoding` and `System.Collections.Immutable` are in the shared
   framework.
4. **Tests**: `[Trait("Category", "Unit")]` on every class (`TestCategoryTests` enforces it, in every suite). 2b adds
   no `Security` class (design "Tests, vectors and ports"). Core test classes mirror the source folders:
   `tests/Dexpace.Sdk.Core.Tests/{Client,Errors,Serialization,Operations,Pipeline,Architecture,Support}/`.
   `Dexpace.Sdk.Core.Tests` references Core and `TestSupport` only, never a transport (SEAM-2, an architecture test), so
   every check that needs `SystemNetHttpClient` lives in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`.
5. **Security tests are never deleted or loosened, and 2b edits none.** The four wire classes
   (`FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `RedirectWireTests`, `MalformedContentTypeWireTests`)
   and the core `Security` classes stay **byte-identical** in every PR. Each PR's close-out runs the check
   `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
   and expects empty output. A change that forces an edit to one of them is a signal to re-read design §3.2 and
   [position I](2026-09-30-phase2b-seams-design.md#i-the-option-less-convenience-and-the-wire-tests), not to edit the file.
6. **`PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` is empty, so a changed or removed member is an *edit or
   deletion of its line in `Unshipped`*, never a `*REMOVED*` marker. The lines listed per task are the expected
   hand-authored diff; the build's `RS0016`/`RS0017` output is the authority, and it also lists the members the
   compiler synthesises for a record (`<Clone>$`, `operator ==`/`!=`, `Equals(T?)`, `GetHashCode`, `ToString`), which
   the task does not spell out. Protected members are listed without a visibility keyword. Namespace aliases in the
   diff blocks (expand them when pasting): `[K]` = `Dexpace.Sdk.Core.Client`, `[Q]` = `Dexpace.Sdk.Core.Http.Request`,
   `[S]` = `Dexpace.Sdk.Core.Http.Response`, `[C]` = `Dexpace.Sdk.Core.Http.Common`, `[E]` = `Dexpace.Sdk.Core.Errors`,
   `[Z]` = `Dexpace.Sdk.Core.Serialization`, `[O]` = `Dexpace.Sdk.Core.Operations`, `[N]` =
   `Dexpace.Sdk.Http.SystemNet`, `[T]` = `System.Threading.Tasks`, `[H]` = `System.Threading`.
7. **XML docs mark a breaking change** with a `<para><b>Breaking:</b> …</para>` in `<remarks>` of the member it
   changes, stating what it was. Each PR's last task adds the matching `CHANGELOG.md` `[Unreleased]` line, prefixed
   **Breaking:** where design "Breaking changes" lists it (constraint 8).
8. **One PR per step, code and tests together** (roadmap step 5's one-PR allowance; design "Landing order"). Every
   commit inside a PR is green too. PR 3 is the one change that cannot be split at the interface, so it is ordered as
   *additive test infrastructure → the signature change with the mechanical adaptation of every implementer and caller
   → the two bridge rewrites, which have no caller and so stand alone*. Commit style `feat:` / `feat!:` / `fix:` /
   `chore:` / `test:` / `docs:`; no AI attribution anywhere.

### Environment

```bash
export DOTNET_ROOT=/tmp/claude-1000/-home-mohammad-Projects-dotnet-sdk/036600bc-088e-406d-b30d-fae2f01ee087/scratchpad/dotnet
export PATH=$DOTNET_ROOT:$PATH
```

### Verification blocks

**V-fast** (inner loop, one class):

```bash
dotnet test --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-class "*<ClassName>"
# SystemNet or serde classes: --project tests/Dexpace.Sdk.Http.SystemNet.Tests | tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests
```

**V-gate** (the last task of every PR; the whole of CI that can run locally):

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release                  # warnings-as-errors: analyzers, CS1591, RS0016/17/30, MA0051, CA2007
dotnet format  Dexpace.Sdk.sln --verify-no-changes
dotnet test    --solution Dexpace.Sdk.sln --configuration Release
dotnet test    --project tests/Dexpace.Sdk.Core.Tests --configuration Release --filter-trait "Category=Security"
dotnet test    --project tests/Dexpace.Sdk.Http.SystemNet.Tests --configuration Release --filter-trait "Category=Security"
git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security   # expect empty
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke
dotnet pack    Dexpace.Sdk.sln --configuration Release --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages
scripts/knowledge verify-structure
dotnet run --project .claude/skills/housekeeping/src -- probe --only links,citations
```

The AOT smoke runs in every PR (the gate is cheap; PRs 1, 3, 4 and 5 change public surface, and PR 6 extends the
smoke). The coverage gate (`dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80`, after a clean
coverlet run, then `scripts/ci/coverage-gate-selftest.sh artifacts/test-results`) is run before the push of PR 6 and of
any PR whose diff removes tests. No `PackageReference` changes, so no `packages.lock.json` change is expected; if a
locked restore complains, run `dotnet restore` on both solutions and commit every changed lock file.

### Phase-start queries (re-run at the start of each PR)

```bash
scripts/knowledge --origin note --brief
scripts/knowledge --section conflicts --brief
scripts/knowledge --prefix-info SEAM
scripts/knowledge --gaps SEAM               # single prefix; the space form drops prefixes when several are given (#31)
scripts/knowledge --req SEAM-18             # per row in scope of the PR
```

---

## Rulings

The design left six judgement calls open. The lead weighed them on 2026-09-30: five design defaults stand, and ruling
4 takes design §3.4 as written. Each is marked `RULED` at the task it affects.

| Ruling | Outcome | Task |
|---|---|---|
| **1. `SerdeException` abstract** (position F) | `public abstract class SerdeException` with `protected` constructors | 1.1 |
| **2. `AsAsync` accepts `TaskScheduler.Default`** (position E) | Accepted, documented as one dedicated thread per call through `LongRunning` | 3.4 |
| **3. `AsAsync`'s check after return** (position E, P2b-4) | Adopted: a response produced after the token is signalled is disposed and the task completes `Canceled`. Accepted consequence until 3b's `DisposeQuietly` (review F11): a `Dispose` that throws in the check faults the task with that exception, unobserved under `WaitAsync`; `A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` pins it and 3b replaces that test | 3.4 |
| **4. The string profile** (position G) | Design §3.4 as written: `SerializeToString` returns `IStringSerde.SerializeToString` when the codec implements that new optional interface, and otherwise decodes the buffer as UTF-8. The declared-charset default is withdrawn (P2b-5), so review F3's two consequences (the `utf-7` edge on 2a PR 3, and widening `ISerde`'s UTF-8 contract) no longer arise | 1.2, 1.5 |
| **5. Typed-value `OperationDescriptor`** (position H) | Node's shape: typed `PathParameters`, `Query`, `Headers`, `Body` | 5.1, 5.2 |
| **6. Build-time assembly failure** | `InvalidOperationException` naming the placeholder (also for a path parameter naming no placeholder and a rendered dot-segment, position K) | 5.2 |

---

## PR 1 — The serde seam

**Gate: free now.** No edge into 2a (segmentation "What 2b is gated on"); PR 1 may open, review and merge before
2a's PR 1. Breaking item 1. Rows: `SEAM-19`–`SEAM-23`.

### Task 1.1 — `SerdeException` and the re-parented subtypes (`SEAM-23`) — RULED 1

**Failing tests first.** New file `tests/Dexpace.Sdk.Core.Tests/Errors/SerdeExceptionHierarchyTests.cs`, class
`SerdeExceptionHierarchyTests`, `[Trait("Category", "Unit")]`:

- `SerdeException_is_abstract_and_derives_from_SdkException` (**RULED 1**)
- `SerdeException_constructors_are_all_protected` (**RULED 1**: reflection over `GetConstructors(NonPublic |
  Instance)`; no public constructor)
- `SerializationException_and_DeserializationException_derive_from_SerdeException`
- `Both_subtypes_are_not_sealed` (`IsSealed` is `false`; the reason is `SEAM-23`)
- `A_test_local_sealed_subtype_of_DeserializationException_is_caught_as_SerdeException` (a
  `sealed class WidgetDecodeException : DeserializationException` compiles, and `catch (SerdeException)` and
  `catch (SdkException)` both catch it)
- `Encode_and_decode_failures_are_distinguishable_under_one_handler` (`catch (SerdeException e) when (e is
  SerializationException)` selects only the encode failure)
- `The_chained_cause_survives` (`InnerException` is the exact instance passed to each subtype's two-argument
  constructor)
- `Every_public_constructor_of_the_subtypes_is_unchanged` (parameter lists `()`, `(string)`, `(string, Exception?)`;
  pins that the re-parenting is source-compatible at every throw site)

Red: CS0246, `SerdeException` does not exist.

**Production.** New `src/Dexpace.Sdk.Core/Errors/SerdeException.cs`: `public abstract class SerdeException :
SdkException` with three `protected` constructors (`()`, `(string)`, `(string, Exception?)`), each XML-documented.
Edit `src/Dexpace.Sdk.Core/Errors/SerializationExceptions.cs`: drop `sealed` from both classes and change the base to
`SerdeException`; constructors unchanged. Verified at design time (fact 6): an abstract exception with protected
standard constructors passes `CA1032`/`CA1012` under warnings-as-errors; if a different analyzer objects, fix the
source, do not suppress. The `<exception cref>` tags in `ISerde.cs` already name the two subtypes and stay; add one sentence there that both
derive from `SerdeException`. No throw site in
`SystemTextJsonSerde.cs` (six) changes.

**`PublicAPI.Unshipped.txt`** (`src/Dexpace.Sdk.Core`; sorted into place). The file records neither sealing nor base
types, so **the diff shows only `SerdeException`'s new lines and nothing for the re-parenting**. The evidence for
Breaking item 1 is therefore `SerdeExceptionHierarchyTests` and the `CHANGELOG.md` line (task 1.5):

```text
[E].SerdeException
[E].SerdeException.SerdeException() -> void
[E].SerdeException.SerdeException(string! message) -> void
[E].SerdeException.SerdeException(string! message, System.Exception? innerException) -> void
```

**Docs.** Both subtype classes carry `<para><b>Breaking:</b> was <c>sealed</c> and derived directly from
<c>SdkException</c>; unsealed and re-parented under <c>SerdeException</c> so codegen and adapters can add more specific
subtypes (SEAM-23). This departs from styleguide 8.6 and 6.2 on purpose.</para>` in `<remarks>`.
**IDs:** `SEAM-23`. **Verify:** V-fast `SerdeExceptionHierarchyTests`; `dotnet build Dexpace.Sdk.sln
--configuration Release` (every existing catch and throw site compiles unchanged).

### Task 1.2 — `SerdeExtensions`, `IStringSerde`, the derived profiles (`SEAM-20`) — RULED 4

**Test infrastructure first (additive).** `ScriptedSerde` writes nothing, so it cannot drive a profile test. New
`tests/Dexpace.Sdk.TestSupport/Serialization/BytesSerde.cs`: `public sealed class BytesSerde(MediaType mediaType,
byte[] payload, Exception? failure = null) : ISerde` — the two encode primitives write `payload` (or throw `failure`
when given), the two decode primitives return `default`. Class is documented like its siblings (design "Type shapes", the fakes).
Beside it, `tests/Dexpace.Sdk.TestSupport/Serialization/StringOverrideSerde.cs`: `public sealed class
StringOverrideSerde(MediaType mediaType, byte[] payload, string text) : IStringSerde`, a `BytesSerde` whose
`SerializeToString` returns `text` (it is written in this task, once `IStringSerde` exists, so the task stays green).

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeProfileTests.cs`, class
`SerdeProfileTests`, `Unit`:

- `SerializeToUtf8Bytes_returns_the_payload_bytes`
- `Two_SerializeToUtf8Bytes_calls_return_distinct_arrays` (`NotSame`, equal content: a fresh array per call)
- `SerializeToString_decodes_the_buffer_as_UTF8` (**RULED 4**: payload `0xC3 0xA9` gives `"é"`)
- `SerializeToString_ignores_the_declared_charset` (**RULED 4**: a `BytesSerde` declaring `text/plain;
  charset=iso-8859-1` with payload `0xC3 0xA9` still gives `"é"`, and a codec declaring `utf-7` does not throw, because
  the profile never reads `MediaType.Charset`)
- `An_IStringSerde_override_wins_over_the_UTF8_decode` (**RULED 4**: `StringOverrideSerde` returns its `text`, not the
  decoded payload)
- `Fixed_buffer_serialize_returns_the_count_and_writes_at_the_offset` (`buffer.AsSpan(2)`; the two leading bytes stay as
  they were)
- `Fixed_buffer_serialize_of_an_exact_fit_succeeds`
- `Fixed_buffer_overflow_throws_ArgumentOutOfRangeException_and_leaves_every_byte_untouched` (bytes before the offset,
  inside the span and after it; `ex.InnerException` is `null`; the type is not an `SdkException`)
- `A_SerializationException_from_the_primitive_surfaces_as_the_same_instance` (all three profiles; not re-wrapped)
- `An_IOException_from_the_primitive_propagates_unwrapped`
- `A_null_serde_is_rejected_with_ArgumentNullException` (`ParamName` `serde`, all three profiles)

Red: CS1061, no `SerializeToUtf8Bytes` on `ISerde`.

**Production.** New `src/Dexpace.Sdk.Core/Serialization/SerdeExtensions.cs`: `public static class SerdeExtensions`
with three extension methods on `ISerde`, and new `src/Dexpace.Sdk.Core/Serialization/IStringSerde.cs`:
`public interface IStringSerde : ISerde { string SerializeToString<T>(T value); }` (design "Type shapes"). The extensions are each guarding `ArgumentNullException.ThrowIfNull(serde)`:
`SerializeToUtf8Bytes<T>` (an `ArrayBufferWriter<byte>` then `WrittenSpan.ToArray()`); `SerializeToString<T>` (`serde is IStringSerde s ?
s.SerializeToString(value) :` the same buffer decoded with `Encoding.UTF8.GetString(WrittenSpan)`); `Serialize<T>(this
ISerde, Span<byte> destination, T value)` (scratch `ArrayBufferWriter<byte>` first, copy only when
`WrittenCount <= destination.Length`, else `throw new ArgumentOutOfRangeException(nameof(destination), …)` with a
message that names the two sizes and never the payload). The extension named `Serialize` does not collide with the
interface's `Serialize(IBufferWriter<byte>, T)` (a `Span<byte>` is not convertible to `IBufferWriter<byte>`; design
"Type shapes"). No allocation beyond the scratch buffer on the fixed-buffer path.

**`PublicAPI.Unshipped.txt`:**

```text
[Z].IStringSerde
[Z].IStringSerde.SerializeToString<T>(T value) -> string!
[Z].SerdeExtensions
static [Z].SerdeExtensions.Serialize<T>(this [Z].ISerde! serde, System.Span<byte> destination, T value) -> int
static [Z].SerdeExtensions.SerializeToString<T>(this [Z].ISerde! serde, T value) -> string!
static [Z].SerdeExtensions.SerializeToUtf8Bytes<T>(this [Z].ISerde! serde, T value) -> byte[]!
```

**Docs.** Additive, no Breaking marker. `SerializeToString`'s remarks state the UTF-8 rule and the `IStringSerde` override;
`IStringSerde`'s remarks say when a codec implements it (a wire format that is not UTF-8 text) and that a binary codec
has no meaningful string form. **IDs:** `SEAM-20`. **Verify:** V-fast `SerdeProfileTests`.

### Task 1.3 — STJ adapter pins (`SEAM-20`, `SEAM-21`, `SEAM-22`)

**Failing or pinning tests**, `tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/SystemTextJsonSerdeTests.cs`
(existing class, `Unit`); all but the cycle assertions are **pins** (expected green on first run) and each is proven
able to fail per convention 1:

- `SerializeAsync_leaves_the_destination_open` (`MemoryStream.CanWrite` still true after the call)
- `DeserializeAsync_leaves_the_source_open`
- `An_IOException_from_the_destination_propagates_unwrapped` (a test-local `ThrowingStream` whose `WriteAsync` throws
  `IOException`; the caught exception is that instance, not a `SerializationException`)
- `An_IOException_from_the_source_propagates_unwrapped` (`ReadAsync` throws)
- `A_generic_helper_decodes_into_the_closed_type` (`SEAM-22`: `static async Task<List<T>?> Decode<T>(ISerde s, Stream
  x) => await s.DeserializeAsync<List<T>>(x)` called with `Widget` yields a `List<Widget>` whose items are `Widget`)
- extend the two existing cycle tests and `Deserialize_malformed_json_throws_DeserializationException` with
  `Assert.IsType<JsonException>(ex.InnerException)` (the chained-cause clause of `SEAM-20`/`SEAM-21`; an additive
  assertion in a `Unit` class, listed in the checklist's "existing assertions changed" table)

**Production.** None expected. `tests/…/TestModels.cs` gains `[JsonSerializable(typeof(List<Widget>))]` on
`TestJsonContext` (the helper test needs the closed generic registered). If a pin fails, the fix is in
`SystemTextJsonSerde.cs` and is stated in the PR. **PublicAPI:** none. **IDs:** `SEAM-20` (adapter half), `SEAM-21`,
`SEAM-22` (helper test). **Verify:** V-fast the class, project `Dexpace.Sdk.Serialization.SystemTextJson.Tests`.

### Task 1.4 — Seam pins in core (`SEAM-19`, `SEAM-22`)

**Pinning tests** (green first, then proven able to fail):

- `tests/Dexpace.Sdk.Core.Tests/Serialization/SerdeSeamTests.cs`, class `SerdeSeamTests`, `Unit`:
  `DefaultMediaType_has_no_default_implementation` (reflection: `typeof(ISerde).GetProperty("DefaultMediaType")!
  .GetMethod!.IsAbstract`), `RequestBody_FromValue_stamps_the_codecs_own_media_type` (a `BytesSerde` declaring
  `application/xml`), `FromValue_lets_an_explicit_media_type_win`.
- `tests/Dexpace.Sdk.Core.Tests/Architecture/SerdeSeamArchitectureTests.cs`, class `SerdeSeamArchitectureTests`,
  `Unit`: `No_seam_member_takes_a_System_Type` (every method parameter and return type of `ISerde`, `IHttpClient`,
  `IAsyncHttpClient` and `SerdeExtensions` is not `typeof(Type)`; the failure message cites `SEAM-22` and the
  `ContainsGenericParameters` rejection design §3.4 requires of any future `Type` overload).

**Production.** None. **PublicAPI:** none. **IDs:** `SEAM-19`, `SEAM-22` (tripwire). **Verify:** V-fast both classes.

### Task 1.5 — Close-out

- `ISerde` `<remarks>` gain the `SEAM-19`–`SEAM-22` contract (the media-type declaration is required; the codec never
  captures an open generic; the two primitives and the derived profiles; failure model), and (**RULED 4**) that the buffer primitives are UTF-8, with `IStringSerde` as the
  optional string override for a codec whose wire format is not UTF-8 text (design position G).
- `CHANGELOG.md` `[Unreleased]`:
  - `### Added`: "`SerdeException` (abstract), the optional `IStringSerde`, and `SerdeExtensions` — `SerializeToUtf8Bytes`, `SerializeToString` and a
    fixed-buffer `Serialize(Span<byte>, T)` over any `ISerde` (`SEAM-20`, `SEAM-23`)."
  - `### Changed`: "**Breaking:** `SerializationException` and `DeserializationException` are unsealed and derive from
    the new abstract `SerdeException` (was: sealed, deriving from `SdkException`). Source-compatible at every throw and
    catch site; binary-incompatible for a caller compiled against the sealed types (`SEAM-23`)."
- Run **V-gate**. **Commit:** `feat!: SerdeException hierarchy and the serde profiles (SEAM-19..SEAM-23)`.

---

## PR 2 — Seam-surface guards

**Gate: free now.** Tests and a build rule only; no public surface, no changelog `Added`/`Breaking`. Rows: `SEAM-1`,
`SEAM-2`, `SEAM-5`, `SEAM-14` (ownership), `SEAM-30` (the bans).

### Task 2.1 — `Seam1ArchitectureTests` (`SEAM-1`)

**Pinning test.** New `tests/Dexpace.Sdk.Core.Tests/Architecture/Seam1ArchitectureTests.cs`, class
`Seam1ArchitectureTests`, `Unit`: `Core_references_no_transport_or_codec_assembly` — `typeof(Request).Assembly
.GetReferencedAssemblies()` contains none of `System.Net.Http`, `System.Net.Sockets`, `System.Net.Security`,
`System.Text.Json`. Verified 2026-09-30 on the Release assembly: core references `System.Runtime`, `System.Memory`,
`Microsoft.Extensions.Logging.Abstractions`, `System.Collections`, `System.Diagnostics.DiagnosticSource`,
`System.Collections.Immutable`, `System.Collections.Concurrent`, `System.Threading` and `System.Linq`. Cite
`scripts/ci/dependency-audit.cs` (the `deps.json`/nuspec gate) in the class remark. Proven able to fail by adding a
throwaway `System.Net.Http.HttpMethod` reference in a scratch core file (never committed).
**IDs:** `SEAM-1`. **Verify:** V-fast `Seam1ArchitectureTests`.

### Task 2.2 — `SeamImplementationArchitectureTests` and the `SEAM-5` pins (`SEAM-2`, `SEAM-5`)

**Pinning tests.**

- `tests/…/Architecture/SeamImplementationArchitectureTests.cs`, class `SeamImplementationArchitectureTests`, `Unit`:
  `Core_implements_a_seam_only_through_the_allow_listed_adapters` — every type in core's assembly (nested included)
  assignable to `IHttpClient`, `IAsyncHttpClient` or `ISerde` is non-public (`IsNotPublic` / `IsNestedPrivate`) **and**
  on the allow-list, which PR 2 fills with exactly `HttpClientExtensions+SyncToAsyncAdapter` and
  `HttpClientExtensions+AsyncToSyncAdapter` (the as-built nested names; PR 3 keeps them, and PR 4 adds
  `DelegateHttpClient+AsyncAdapter` and `DelegateHttpClient+BlockingAdapter`). Each allow-list entry carries a reason
  string (position A, P2b-1); the failure message tells a future author that a new implementer needs an entry with a
  reason, and that 4c owns the `HttpPipeline` entry (`PIPE-26`). A second test,
  `The_allow_list_names_only_types_that_exist`, fails on a stale entry.
- `tests/…/Client/SeamParameterTests.cs`, class `SeamParameterTests`, `Unit` (`SEAM-5`, DI-less half):
  `CreateDefault_rejects_a_null_transport_naming_the_parameter` (`ArgumentNullException`, `ParamName` `transport`;
  `DexpacePipeline.cs:49` is the guard), `RequestBody_FromValue_rejects_a_null_serde_naming_the_parameter`
  (`ParamName` `serde`, `RequestBody.cs:101`).

**Production.** None. **IDs:** `SEAM-2`, `SEAM-5` (the DI half is ⏳ phase 9; the checklist says so). **Verify:** V-fast
both classes.

### Task 2.3 — `SystemNetHttpClientOwnershipTests` (`SEAM-14`, ownership)

**Pinning tests.** New `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientOwnershipTests.cs`, class
`SystemNetHttpClientOwnershipTests`, `Unit` (it uses an in-process `HttpMessageHandler`, no socket). A test-local
`StubHandler : HttpMessageHandler` answers `200` and records `IsDisposed`. Calls use the one-argument
`transport.ExecuteAsync(request)`, which binds to the instance member today and to the option-less extension after PR 3
(position I), so this file survives PR 3 unedited.

- `A_borrowed_client_still_sends_after_the_transport_is_disposed` (`new HttpClient(handler)` passed in; `Dispose` the
  transport; `client.SendAsync` still succeeds; `handler.IsDisposed` is `false`)
- `DisposeAsync_leaves_a_borrowed_client_alone_too`
- `An_owned_client_is_disposed_with_the_transport` (`new SystemNetHttpClient()`; after `Dispose`, `ExecuteAsync`
  faults with `ObjectDisposedException`: `HttpClient` checks disposal before any I/O, so no socket is opened)
- `Dispose_twice_does_not_throw` and `DisposeAsync_twice_does_not_throw` (both forms; relies on `HttpClient`'s own
  idempotence until 3b's latch)

**Production.** None. The class remark states which half is ⏳ (3b's SDK-owned idempotence latch). **IDs:** `SEAM-14`.
**Verify:** V-fast, project `Dexpace.Sdk.Http.SystemNet.Tests`.

### Task 2.4 — `BannedSymbols.txt`: the `SEAM-30` entries

**No failing unit test can observe an analyzer rule, so the proof is a throwaway probe** (never committed):

1. Add a `// Cancellation-completion race (SEAM-30, design §3.3, §3.7): …` comment block and these entries to
   `BannedSymbols.txt`, each `<doc-ID>;<reason citing design §3.3 and SEAM-30>`:
   `M:System.Threading.Tasks.TaskCompletionSource`1.SetResult(`0)`, `…TrySetResult(`0)`; and every `WaitAsync`
   overload of `Task`1`, each its own doc ID (design row `SEAM-30`): `(System.Threading.CancellationToken)`,
   `(System.TimeSpan)`, `(System.TimeSpan,System.Threading.CancellationToken)`, `(System.TimeSpan,System.TimeProvider)`,
   `(System.TimeSpan,System.TimeProvider,System.Threading.CancellationToken)`. The non-generic `TaskCompletionSource`
   and `Task.WaitAsync` are **not** added: neither carries a `Response`, so neither can orphan one (design row
   `SEAM-30`).
2. Temporarily add to a scratch `src/Dexpace.Sdk.Core` file one call to each symbol, `dotnet build
   src/Dexpace.Sdk.Core --configuration Release`, and confirm exactly one `RS0030` per call, with the reason text.
3. Confirm `AccessTokenCache.cs:84` (`SemaphoreSlim.WaitAsync`, a different symbol) and a scratch call to the
   non-generic `Task.WaitAsync` do **not** fire, and that the real
   build is green after the scratch file is removed.
4. Record the observed count in the PR description.

**Docs.** The file's header comment already explains the escape hatch (a scoped `#pragma warning disable RS0030` citing
the design section); the new block says reintroducing either symbol "needs a `#pragma` citing `SEAM-30`".
**IDs:** `SEAM-30` (the ban half). **Verify:** the probe above, then `dotnet build Dexpace.Sdk.sln --configuration
Release`.

### Task 2.5 — Close-out

`CHANGELOG.md` `[Unreleased]` → `### Changed`: "Build: `BannedSymbols.txt` bans `TaskCompletionSource<T>.SetResult` /
`TrySetResult` and `Task<T>.WaitAsync` in `src/` (`SEAM-30`); no public surface changes." Run **V-gate**. **Commit:**
`test: seam-surface architecture guards, ownership pins and SEAM-30 bans (SEAM-1, SEAM-2, SEAM-5, SEAM-14, SEAM-30)`.

---

## PR 3 — The SPI change

**Gate: 2a PR 1 merged** (`RequestOptions` with its `Empty` instance; `HTTP-34`, `HTTP-35`). This is 2b's only hard
gate: the signatures do not compile without the type. **Before opening**, run `git log main --oneline -- 
src/Dexpace.Sdk.Core/Http/Request/RequestOptions.cs` and confirm the merge; then apply the interleaving rule against
2a PR 7 (design "Landing order"):

1. If 2a task 7.1 (`TestResponses.Create`, behaviour-free) has merged, build the fakes' edits on it, so they are
   signature lines only.
2. Otherwise whichever of this PR and 2a PR 7 is ready first lands first and the other rebases, **re-deriving** its
   change on the new base and re-running the whole gate rather than resolving hunks textually. The overlap is
   `SystemNetHttpClient.cs`, the three `TestSupport` fakes and `AotSmoke/SmokeChecks.cs`.

Breaking items 2–7. Rows: `SEAM-11`–`SEAM-18`, `SEAM-24`, `SEAM-25`, `SEAM-30` (the bridge).

### Task 3.1 — Additive test infrastructure (green against the old SPI)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Support/`, `Unit`:

- `RecordingTaskSchedulerTests`: `It_runs_each_task_on_its_own_non_pool_thread`, `It_counts_every_QueueTask`,
  `It_never_inlines_a_task_on_the_calling_thread` (`TryExecuteTaskInline` is `false`), `Dispose_is_recorded_and_not_
  triggered_by_running_tasks`.
- `DisposalCountingBodyTests`: `Dispose_and_DisposeAsync_each_increment_the_count`, `The_body_is_empty_and_readable`,
  `It_carries_the_media_type_it_was_given`.

Red: CS0246.

**Production (test infrastructure; `TestSupport` is not packed and not AOT-gated).**

- `tests/Dexpace.Sdk.TestSupport/Threading/RecordingTaskScheduler.cs` — `public sealed class RecordingTaskScheduler :
  TaskScheduler, IDisposable`: starts every queued task on a new background `Thread` (so `ExecutionContext` flows as it
  does for any scheduler), `int QueueCount`, `bool IsDisposed`, `IReadOnlyList<int> ThreadIds`, `TryExecuteTaskInline`
  returns `false`, `GetScheduledTasks` returns an empty sequence (documented).
- `tests/Dexpace.Sdk.TestSupport/Transports/DisposalCountingBody.cs` — `public sealed class
  DisposalCountingBody(MediaType? contentType = null) : ResponseBody`; `int DisposeCount` bumped with
  `Interlocked.Increment` by both `Dispose()` and `DisposeAsync()`; `OpenReadAsync` returns an empty stream.

**PublicAPI:** none (`TestSupport` is not a shipped surface). **IDs:** infrastructure for `SEAM-18`, `SEAM-24`,
`SEAM-25`, `SEAM-30`. **Verify:** V-fast both classes; the whole solution still builds.

### Task 3.2 — The signature change and the mechanical adaptation of every implementer and caller (`SEAM-11`, `SEAM-13`, `SEAM-15`, `SEAM-16`, `SEAM-17`)

One commit: the change is atomic at the interface. Tests are written first and seen red (CS7036/CS1061 on the new
shape); the commit contains tests and production together.

**Failing tests first.**

- `tests/Dexpace.Sdk.Core.Tests/Client/HttpClientExtensionsTests.cs`, class `HttpClientExtensionsTests`, `Unit`:
  `The_option_less_ExecuteAsync_passes_RequestOptions_Empty_and_the_token` (a `RecordingTransport`; `LastCall.Options` is
  `Same(RequestOptions.Empty)`, `LastCall.CancellationToken` is the token passed), `The_option_less_Execute_passes_
  RequestOptions_Empty_and_the_token` (a `RecordingSyncTransport`), `The_option_less_calls_default_the_token_to_none`,
  `The_option_less_calls_reject_a_null_client_and_a_null_request`. (Design row `SEAM-11`: in PR 3 the recording fakes carry
  the capture, because `DelegateHttpClient` lands in PR 4; task 4.2 adds that variant.)
- `tests/…/Client/SeamSurfaceTests.cs`, class `SeamSurfaceTests`, `Unit`: `The_async_seam_returns_Task_of_Response`
  (`SEAM-17`: reflection, `Task<Response>`, not `ValueTask`), `The_sync_seam_takes_a_token_and_options` (`SEAM-13`),
  `No_SPI_member_declares_a_default_on_any_parameter` (`ParameterInfo.HasDefaultValue` is `false` on the three-parameter
  members of both interfaces, and on every implementation in core and in `TestSupport`; design fact 2).
- `tests/…/Pipeline/PipelineRunnerTests.cs` (existing class) additions:
  `The_transport_receives_RequestOptions_Empty` (position D; the design says 4c replaces this test),
  `The_transport_receives_the_contexts_token`,
  `A_null_response_from_the_transport_fails_at_the_runner_before_any_policy_sees_it` (`SEAM-16`; a test-local
  `NullTransport` returning `Task.FromResult<Response>(null!)`, an outer policy asserting that after
  `continuation.RunAsync` it is never reached with a `null` `context.Response`, and the exception is
  `PipelineAbortedException`).
- `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientTests.cs` (existing class) additions:
  `Options_are_accepted_and_ignored_until_8b` (a `RequestOptions { Timeout = … }` gives the same response as `Empty`;
  a pin for `SEAM-11`'s conformance clause on the real adapter), `A_null_options_argument_faults_the_task_with_
  ArgumentNullException`, `The_sync_Execute_passes_the_token_through` (a pre-cancelled token throws
  `OperationCanceledException`), and in a new class
  `tests/Dexpace.Sdk.Http.SystemNet.Tests/SystemNetHttpClientSurfaceTests.cs` `The_implementation_declares_no_defaults_on_the_SPI_members` (Core.Tests cannot
  see the adapter, so this half of the reflection test lives here).

**Production.**

- `src/Dexpace.Sdk.Core/Client/IHttpClient.cs`: `Response Execute(Request request, RequestOptions options,
  CancellationToken cancellationToken);` — no defaults. `IAsyncHttpClient.cs`: `Task<Response> ExecuteAsync(Request
  request, RequestOptions options, CancellationToken cancellationToken);` — no defaults. XML remarks state:
  `SEAM-11` (one request, one response, body never pre-buffered, options optional and ignorable, a transport that
  ignores them behaves identically), `SEAM-12` (concurrent calls safe; per-call state confined to the call),
  `SEAM-13` (the token; sync honours it inside blocking I/O only once 8b lands), `SEAM-15` (post-dispose mode is
  implementation-defined and each SDK type documents its own), `SEAM-16` (never `null`, and `null` arguments are
  rejected with `ArgumentNullException`, delivered through the returned task on the async seam, `ASYNC-2`); and
  `<para><b>Breaking:</b> was <c>Execute(Request)</c></para>` / `<c>ExecuteAsync(Request, CancellationToken = default)</c>`.
- `HttpClientExtensions.cs`: add `public static Response Execute(this IHttpClient client, Request request,
  CancellationToken cancellationToken = default)` and `public static Task<Response> ExecuteAsync(this IAsyncHttpClient
  client, Request request, CancellationToken cancellationToken = default)`, each guarding null and passing
  `RequestOptions.Empty`. The two existing bridges are adapted **mechanically only** in this task (their rewrite is 3.3
  and 3.4): the sync adapter calls `inner.Execute(request, options, cancellationToken)`, and `AsyncToSyncAdapter`
  forwards `(request, options, cancellationToken)`.
- `PipelineRunner.cs`: `_transport.ExecuteAsync(context.Request, RequestOptions.Empty, context.CancellationToken)`, and
  `context.Response = result ?? throw new PipelineAbortedException("The transport returned no response (SEAM-16).")`
  (position D; the null guard fires before any policy sees the value). `RunAsync`'s `<remarks>` gain
  `<para><b>Breaking (behaviour):</b> a transport returning <c>null</c> now fails here with
  <c>PipelineAbortedException</c>, before any policy sees it; policies used to see a <c>null</c> response and
  <c>HttpPipeline</c> threw at the end.</para>` (Breaking item 7, `SEAM-16`).
- `SystemNetHttpClient.cs`: `ExecuteAsync(Request, RequestOptions, CancellationToken)` (`ThrowIfNull` on both
  reference arguments; the body is otherwise unchanged; options are read by nothing until 8b), and `Execute(Request,
  RequestOptions, CancellationToken)` keeping its sync-over-async body under the existing `RS0030, CA2000` pragma,
  now `ExecuteAsync(request, options, cancellationToken).GetAwaiter().GetResult()`. No default on either
  (design fact 2). `<inheritdoc/>` stays; class remark gains "**Breaking:**" text and the `SEAM-15` sentence (owned
  client throws `ObjectDisposedException` after dispose, a borrowed one keeps working, until 8b's latch).
- `tests/Dexpace.Sdk.Http.SystemNet.Tests/GlobalUsings.cs`: add `global using Dexpace.Sdk.Core.Client;` (position I).
  **No other file in that project is edited**: 22 `transport.ExecuteAsync(request, ct)` calls (13 in the four `Security`
  classes, 3 in `SystemNetHttpClientTests`, 6 in `LoopbackServerTests`, recount with
  `grep -rn "ExecuteAsync(" tests/Dexpace.Sdk.Http.SystemNet.Tests --include='*.cs'`) bind to the extension (design
  fact 1).
- `tests/Dexpace.Sdk.TestSupport/Transports/`: new `RecordedCall.cs` — `public sealed record RecordedCall(Request
  Request, RequestOptions Options, CancellationToken CancellationToken)`; `RequestLog` (internal) stores
  `RecordedCall`s and `Requests` projects them, so no existing test changes; `RecordingTransport`,
  `RecordingSyncTransport` and `ScriptedTransport` take the new signatures (no defaults) and gain `Calls` and
  `LastCall`; their responders stay `Func<Request, Response>`.
- `tests/Dexpace.Sdk.Core.Tests/Pipeline/Policies/OperationPolicyTests.cs`: `HangingTransport.ExecuteAsync` takes the
  new signature (a signature line only). `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`: `EchoTransport.ExecuteAsync`
  likewise; its one-argument `transport.ExecuteAsync(request)` (line ~82) already imports `Dexpace.Sdk.Core.Client` and
  binds to the extension.

**`PublicAPI.Unshipped.txt`.** Core (edit the two SPI lines in place, add the extensions):

```text
[K].IAsyncHttpClient.ExecuteAsync([Q].Request! request, [Q].RequestOptions! options, [H].CancellationToken cancellationToken) -> [T].Task<[S].Response!>!
[K].IHttpClient.Execute([Q].Request! request, [Q].RequestOptions! options, [H].CancellationToken cancellationToken) -> [S].Response!
static [K].HttpClientExtensions.Execute(this [K].IHttpClient! client, [Q].Request! request, [H].CancellationToken cancellationToken = default([H].CancellationToken)) -> [S].Response!
static [K].HttpClientExtensions.ExecuteAsync(this [K].IAsyncHttpClient! client, [Q].Request! request, [H].CancellationToken cancellationToken = default([H].CancellationToken)) -> [T].Task<[S].Response!>!
```

`SystemNet` (edit lines 5 and 6 in place):

```text
[N].SystemNetHttpClient.Execute([Q].Request! request, [Q].RequestOptions! options, [H].CancellationToken cancellationToken) -> [S].Response!
[N].SystemNetHttpClient.ExecuteAsync([Q].Request! request, [Q].RequestOptions! options, [H].CancellationToken cancellationToken) -> [T].Task<[S].Response!>!
```

**IDs:** `SEAM-11` (contract), `SEAM-13` (the sync token), `SEAM-15` (docs), `SEAM-16` (runner), `SEAM-17`.
**Verify:** V-fast the four touched classes; then `dotnet build Dexpace.Sdk.sln --configuration Release` and both
Security filters (they must pass with every `Security` file unedited).

### Task 3.3 — `AsBlocking` rewritten (`SEAM-13`, `SEAM-14`, `SEAM-18`, `SEAM-16`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Client/AsyncToSyncBridgeTests.cs`, class
`AsyncToSyncBridgeTests`, `Unit`:

- `A_faulted_InvalidOperationException_surfaces_as_itself_not_as_an_AggregateException`
- `Options_and_token_reach_ExecuteAsync_by_reference` (`RecordingTransport.LastCall`)
- `Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException` (`SEAM-13`: a test-local
  transport awaiting `Task.Delay(Timeout.Infinite, ct)`; a second thread cancels; the blocked `Execute` throws
  `OperationCanceledException`, not a hang)
- `Disposing_the_bridge_never_disposes_the_wrapped_client` (`SEAM-14`, Breaking item 5: was `DisposeAsync()` of the
  inner; `RecordingTransport.IsDisposed` stays `false`; also after a second dispose)
- `A_null_Task_or_a_null_Response_from_the_wrapped_client_fails_with_InvalidOperationException` (`SEAM-16`; design
  row `SEAM-16` and the `AsBlocking` type shape)
- `A_null_request_or_null_options_throws_ArgumentNullException`
- `The_bridge_is_a_last_resort_and_says_so` is **not** written (docs are not testable); the remarks carry it.

Red: `Disposing_…` and the null-handling tests fail on the as-built adapter; the rest are pins.

**Production.** `HttpClientExtensions.AsyncToSyncAdapter`: `Execute(request, options, ct)` null-guards, calls
`inner.ExecuteAsync(request, options, ct)`, rejects a `null` task or `null` result with `InvalidOperationException`
("The wrapped IAsyncHttpClient returned null (SEAM-16)."), blocks with `.GetAwaiter().GetResult()` (unwraps) under the
existing scoped `RS0030` waiver, now with the comment "design §3.3, §5.3 — last resort; `SynchronizationContext`
deadlock hazard documented". `Dispose()` is empty. The remarks document the hazard of design §1 when the wrapped
transport does not use `ConfigureAwait(false)`, and state that it does not hop to the pool (position E). Signature of
`AsBlocking(this IAsyncHttpClient)` is unchanged. **PublicAPI:** none. **Docs:** `AsBlocking` `<remarks>`
**Breaking (behaviour):** no longer disposes the wrapped client; a caller who wrapped a transport they own disposes it
themselves. **IDs:** `SEAM-13`, `SEAM-14`, `SEAM-16`, `SEAM-18`. **Verify:** V-fast `AsyncToSyncBridgeTests`.

### Task 3.4 — `AsAsync` rewritten (`SEAM-18`, `SEAM-24`, `SEAM-25`, `SEAM-30`) — RULINGS 2 and 3

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Client/SyncToAsyncBridgeTests.cs`, class
`SyncToAsyncBridgeTests`, `Unit`, over `RecordingTaskScheduler`, `RecordingSyncTransport` and `DisposalCountingBody`
(3.1):

- `No_AsAsync_overload_lacks_a_TaskScheduler` (reflection over `HttpClientExtensions`)
- `A_null_scheduler_throws_ArgumentNullException` and `A_null_client_throws_ArgumentNullException`
- `The_call_runs_on_the_given_scheduler` (`QueueCount` is 1; the delegate's thread id is in `ThreadIds`)
- `TaskScheduler_Default_is_accepted` (**RULED 2**: the call succeeds, and runs on a non-pool thread because
  of `LongRunning`, design fact 4)
- `The_exact_options_and_token_instances_reach_Execute` (`SEAM-18`)
- `The_callers_AsyncLocal_and_Activity_reach_the_worker_thread` (`SEAM-24`: an `AsyncLocal<string>` and
  `Activity.Current` set by the caller are visible inside the delegate, on a `RecordingTaskScheduler` that starts its
  own thread)
- `A_transport_exception_faults_the_task_with_the_original_exception`
- `A_null_result_from_the_wrapped_client_faults_with_InvalidOperationException` (`SEAM-16`)
- `Argument_errors_are_delivered_through_the_returned_task_not_thrown_synchronously` (`ASYNC-2`: a null request or null
  options gives a faulted `Task`, and the call itself does not throw)
- `A_token_cancelled_before_the_send_never_runs_it` (`SEAM-30`; the delegate is never invoked, the task is `Canceled`)
- `A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces` (**RULED 3**; the
  conformance clause of `SEAM-30`: the delegate starts, the test cancels, the delegate returns a response over a
  `DisposalCountingBody`, the task is `Canceled`, `DisposeCount` is 1; the same test also awaits through
  `task.WaitAsync(token)` — allowed in tests — to model design §5.3's mode)
- `A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` (**RULED 3**, a pin of design
  position E's stated consequence until 3b: a body whose `Dispose` throws `InvalidOperationException`; the task is
  `Faulted` with that exception, not `Canceled`. 3b's `DisposeQuietly` replaces this test)
- `Cancelling_after_delivery_leaves_the_response_readable` (`SEAM-16`; the token is signalled only after the task has
  completed: the response is intact and not disposed)
- `Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client` (`SEAM-25`, `SEAM-14`: the
  `RecordingTaskScheduler` is not disposed and `RecordingSyncTransport.IsDisposed` stays `false`, also after two disposes)
- `An_options_ignoring_client_behaves_identically_with_and_without_options` (`SEAM-11`'s conformance clause, over the
  bridge)

Red: CS1501 (`AsAsync` takes no argument today), then the behaviour tests.

**Production.** `HttpClientExtensions.AsAsync(this IHttpClient client, TaskScheduler scheduler)` — no zero-scheduler
overload; `ThrowIfNull(client)` and `ThrowIfNull(scheduler)` are synchronous (a construction error, not a call error).
The adapter's `ExecuteAsync` is **not** `async`: it returns `Task.FromException<Response>(…)` for a null argument, and
otherwise `Task.Factory.StartNew(() => Run(inner, request, options, cancellationToken), cancellationToken,
TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, scheduler)`, where the static `Run` is (design
"Type shapes"):

```csharp
var response = inner.Execute(request, options, cancellationToken)
    ?? throw new InvalidOperationException("The wrapped IHttpClient returned null (SEAM-16).");
if (cancellationToken.IsCancellationRequested)             // RULED 3
{
    response.Dispose();                                    // SEAM-30; Disposal.DisposeQuietly once 3b lands
    cancellationToken.ThrowIfCancellationRequested();      // StartNew completes the task Canceled (fact 4)
}
return response;
```

`DisposeAsync` is `ValueTask.CompletedTask` and touches nothing (`SEAM-25`, `XCUT-22`, styleguide 13.4). Confirm the
build accepts `Task.Factory.StartNew` under the repository analyzers (`CA2008` is satisfied: a scheduler is passed); do
not suppress. XML remarks state: the caller's scheduler and why there is no default (`SEAM-18`: blocking calls starve a
shared pool); that `TaskScheduler.Default` is accepted and costs a dedicated thread per call through `LongRunning`
(**RULED 2**); that a custom scheduler receives `LongRunning` as a hint it may ignore; the cancellation
mapping both ways through the one token (`SEAM-24`); that a blocking call cannot be interrupted (`SEAM-18`'s interrupt
clause maps to the token, design §10 `cooperative-cancellation`); and that the "cancel without interruption" mode of
design §5.3 cannot orphan a response (**RULED 3**).

**`PublicAPI.Unshipped.txt`** (edit line ~324 in place):

```text
static [K].HttpClientExtensions.AsAsync(this [K].IHttpClient! client, [T].TaskScheduler! scheduler) -> [K].IAsyncHttpClient!
```

**Docs.** `AsAsync` `<remarks>`: `<para><b>Breaking:</b> was <c>AsAsync(this IHttpClient)</c>, which offloaded to the
thread pool; it takes the caller's scheduler now, and no longer disposes the wrapped client.</para>` and
`<para><b>Breaking (behaviour):</b> a response produced after the call's token is signalled is disposed and the call
completes cancelled.</para>` (Breaking items 5 and 6). **IDs:** `SEAM-11`, `SEAM-13`, `SEAM-14`, `SEAM-16`, `SEAM-18`,
`SEAM-24`, `SEAM-25`, `SEAM-30`. **Verify:** V-fast `SyncToAsyncBridgeTests`; run the class 20 times in a loop
(`for i in $(seq 20); do …; done`) to shake out scheduling flakiness before declaring it done.

### Task 3.5 — Cross-bridge concurrency (`SEAM-12`)

**Failing test first.** New `tests/Dexpace.Sdk.Core.Tests/Client/ConcurrencyTests.cs`, class `ConcurrencyTests`, `Unit`:
`No_cross_talk_through_the_bridges` — 64 concurrent calls with distinct requests and distinct `RequestOptions` through
`AsAsync` (over a `RecordingTaskScheduler`) and `AsBlocking` (each call on its own task), each response paired with its
own request and options (the responder echoes the request path in a header; `Calls` proves the option instance), no
exception, no shared mutable state. A `RecordingTransport` and `RecordingSyncTransport` are enough; the
`DelegateHttpClient` forms join in task 4.2. Pin (expected green first; proven able to fail by making a bridge adapter
cache the last request in a field).
**Production.** None. **IDs:** `SEAM-12` (core's implementations; the per-transport proof is ⏳ against 8a's kit).
**Verify:** V-fast `ConcurrencyTests`.

### Task 3.6 — Close-out

- **Docs that state the old SPI** (design "Migration plan"): `docs/architecture.md` lines 45–47 (Transport SPI
  paragraph: the new signatures, the option-less extensions, `AsAsync(TaskScheduler)`), and
  `src/Dexpace.Sdk.Http.SystemNet/README.md` (packed into the NuGet package): the usage sample calls
  `transport.ExecuteAsync(Request.Get(…))`, which now needs `using Dexpace.Sdk.Core.Client;` — add the line to the
  sample and one sentence saying `ExecuteAsync(request, options, token)` is the interface member and the two-argument
  form an extension. `src/Dexpace.Sdk.Core/README.md`'s `Client` row stays accurate until PR 4 (task 4.3).
- `CHANGELOG.md` `[Unreleased]`:
  - `### Added`: "`HttpClientExtensions.Execute` / `ExecuteAsync` — option-less calls that pass `RequestOptions.Empty`
    (`SEAM-11`)."
  - `### Changed`: "**Breaking:** `IHttpClient.Execute(Request)` is now `Execute(Request, RequestOptions,
    CancellationToken)` and `IAsyncHttpClient.ExecuteAsync(Request, CancellationToken = default)` is now
    `ExecuteAsync(Request, RequestOptions, CancellationToken)`, with no parameter defaults; `SystemNetHttpClient`
    takes the same signatures. Callers that import `Dexpace.Sdk.Core.Client` keep calling `ExecuteAsync(request, ct)`
    through the new extension; every implementer changes (`SEAM-11`, `SEAM-13`)."
  - "**Breaking:** `AsAsync(this IHttpClient)` is now `AsAsync(this IHttpClient, TaskScheduler)`; neither bridge
    disposes the client it wraps any more; a response produced by `AsAsync` after the call's token is signalled is
    disposed and the call completes cancelled (`SEAM-14`, `SEAM-18`, `SEAM-30`)."
  - "**Breaking (behaviour):** a transport returning `null` now fails at the pipeline runner with
    `PipelineAbortedException`, before any policy sees a `null` response (`SEAM-16`)."
- Run **V-gate**, including the empty `Security` diff check. **Commit:** `feat!: transport SPI takes RequestOptions and a
  token; rewrite the sync/async bridges (SEAM-11..SEAM-18, SEAM-24, SEAM-25, SEAM-30)`.

---

## PR 4 — `DelegateHttpClient`

**Gate: PR 3 merged.** Additive. Split from PR 3 to keep the one change that cannot be split as small as it can be.
Rows: `SEAM-2` (allow-list), `SEAM-11`, `SEAM-12`, `SEAM-15`, `SEAM-16`.

### Task 4.1 — `DelegateHttpClient` (`SEAM-11`, `SEAM-15`, `SEAM-16`, `SEAM-2`)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Client/DelegateHttpClientTests.cs`, class
`DelegateHttpClientTests`, `Unit`:

- `A_bare_send_lambda_works_as_a_transport` (`SEAM-11`: an `async` lambda, a `Task.FromResult` lambda and a method
  group each bind to `Create`)
- `A_throwing_lambda_binds_without_ambiguity_and_faults_the_task` (design fact 3, position J: `Create((r, o, ct) => throw
  new InvalidOperationException())` compiles; the exception arrives through the returned task, not synchronously)
- `A_bare_blocking_lambda_works_as_a_blocking_transport` (`CreateBlocking`); `A_throwing_blocking_lambda_propagates`
- `The_delegate_receives_the_exact_request_options_and_token`
- `An_options_ignoring_transport_returns_the_same_response_with_and_without_options` (`SEAM-11` conformance clause)
- `A_null_result_faults_the_task_with_InvalidOperationException` and `A_null_task_faults_with_InvalidOperationException`
  (`SEAM-16`); `CreateBlocking`'s null result throws `InvalidOperationException`
- `Argument_errors_are_delivered_through_the_task` (null request, null options)
- `Create_and_CreateBlocking_reject_a_null_delegate_eagerly` (`ArgumentNullException`, `ParamName` `send`)
- `It_keeps_working_after_dispose` (`SEAM-15`: `DisposeAsync` and `Dispose` are no-ops, the transport still answers)
- `Cancelling_after_delivery_leaves_the_response_readable` (`SEAM-16`)

Red: CS0103, `DelegateHttpClient` does not exist.

**Production.** New `src/Dexpace.Sdk.Core/Client/DelegateHttpClient.cs`: `public static class DelegateHttpClient` with
`Create(Func<Request, RequestOptions, CancellationToken, Task<Response>> send)` and `CreateBlocking(Func<Request,
RequestOptions, CancellationToken, Response> send)`, returning two `private sealed` nested adapters
(`AsyncAdapter : IAsyncHttpClient`, `BlockingAdapter : IHttpClient`); a static class returning interfaces rather than
one public class implementing both (design "Type shapes": an object implementing both would make one direction a
bridge in disguise). The async adapter's `ExecuteAsync` is a non-`async` shell that null-checks and hands to a private
`async` helper (`await … .ConfigureAwait(false)`), so a synchronous throw from the delegate, a `null` task and a `null`
result all surface through the task. Both `Dispose` forms are no-ops. Each method ≤70 lines.

**`PublicAPI.Unshipped.txt`:**

```text
[K].DelegateHttpClient
static [K].DelegateHttpClient.Create(System.Func<[Q].Request!, [Q].RequestOptions!, [H].CancellationToken, [T].Task<[S].Response!>!>! send) -> [K].IAsyncHttpClient!
static [K].DelegateHttpClient.CreateBlocking(System.Func<[Q].Request!, [Q].RequestOptions!, [H].CancellationToken, [S].Response!>! send) -> [K].IHttpClient!
```

**Allow-list.** `SeamImplementationArchitectureTests` (2.2) gains `DelegateHttpClient+AsyncAdapter` and
`DelegateHttpClient+BlockingAdapter`, each with a reason (P2b-1). **Docs.** Additive; the class remark explains why the
factory is `Create`/`CreateBlocking` (two `Create` overloads are `CS0121` for a throwing lambda, verified; P2b-6) and that
Dispose is a no-op and the transport keeps working after it (`SEAM-15`). **IDs:** `SEAM-2`, `SEAM-11`, `SEAM-15`,
`SEAM-16`. **Verify:** V-fast `DelegateHttpClientTests`, `SeamImplementationArchitectureTests`.

### Task 4.2 — Move the PR 3 tests onto `DelegateHttpClient` (`SEAM-12`, `SEAM-11`)

Where PR 3's bridge tests used test-local delegate transports (the blocking-cancellation transport in
`AsyncToSyncBridgeTests`, any hand-written send function), replace them with `DelegateHttpClient.Create` /
`CreateBlocking` (`Unit` tests, edited additively; listed in the checklist's "existing assertions changed" table).
`ConcurrencyTests` gains `No_cross_talk_through_both_DelegateHttpClient_forms` (64 concurrent calls, each response paired
with its own request and options). `HttpClientExtensionsTests` gains
`The_option_less_Execute_works_over_a_DelegateHttpClient`. **Production:** none. **IDs:** `SEAM-11`, `SEAM-12`.
**Verify:** V-fast `ConcurrencyTests`, `AsyncToSyncBridgeTests`, `HttpClientExtensionsTests`.

### Task 4.3 — Close-out

`CLAUDE.md` repository-layout tree: `Client/` line gains `DelegateHttpClient`; `src/Dexpace.Sdk.Core/README.md`'s
`Client` row gains `DelegateHttpClient` (design "Migration plan"). `CHANGELOG.md` `### Added`:
"`DelegateHttpClient.Create` / `CreateBlocking` — a bare send function as a transport (`SEAM-11`)." Run **V-gate**.
**Commit:** `feat: DelegateHttpClient (SEAM-11, SEAM-12, SEAM-15, SEAM-16)`.

---

## PR 5 — `OperationDescriptor` and `BuildRequest`

**Gate: 2a PR 2 merged** (`Query`, `Query.Encode`, the internal `Rfc3986` encoder, `VectorFile` and the `tests/vectors/`
csproj wiring). The specification does not need it (design §3.5 never names `Query`); **this is a code edge by the
design's choice** (position H: one RFC 3986 renderer instead of two that could drift). Independent of PRs 3 and 4, so
it may be reviewed in parallel with them. **Convenience edges** (design "Prerequisites"), each with its fallback: 2a PR
4 (`Method` as a reference type; before it, the `Method` null check and its test are left out and added by the first 2b
PR after 2a PR 4 merges, PR 6 at the latest), 2a PR 5 (`Headers` value equality; before it, tests compare header
enumerations and `Equals` compares `Headers` by reference), 2a PR 6 (validating `Request` constructor, `RequestBody`
equality and `UrlRedactor.Default`; `BuildRequest` calls `new Request(method, url, headers, body)`, which compiles
against both).
Additive. Rows: `SEAM-26`–`SEAM-28`.

**Before opening:** confirm 2a's `tests/Dexpace.Sdk.TestSupport/Vectors/VectorFile.cs` and the csproj `<None
Include="..\vectors\**\*.json" …>` glob exist (it covers `vectors/seam/`), and read 2a's `VectorFileTests`, whose
`Every_vector_file_names_its_source_path_and_sha` will police the new file too.

### Task 5.1 — `OperationDescriptor`: shape, template and parameter validation (`SEAM-26`) — RULED 5

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Operations/OperationDescriptorTests.cs`, class
`OperationDescriptorTests`, `Unit`:

- `Method_and_PathTemplate_are_required_members` (reflection: `RequiredMemberAttribute` on both; a missing one is
  `CS9035` at compile time, design §10 `no-builder-objects`)
- `A_parameterless_GET_needs_only_Method_and_PathTemplate` (the defaults: `PathParameters` empty and non-null, `Query`
  is `Query.Empty`, `Headers` is `Headers.Empty`, `Body` and `OperationId` are `null`)
- `A_null_Method_or_PathTemplate_throws_ArgumentNullException` (the `Method` half only once 2a PR 4 has made `Method`
  a reference type; see the gate above)
- `An_empty_PathTemplate_is_allowed`
- `PathTemplate_validation_throws_ArgumentException_naming_the_template` (`[Theory]`: unbalanced `{` and `}`, an empty
  placeholder `{}`, `?`, `#`, a space, a bare `%`, a lone `%2`, and a literal dot-segment `/../x`, `/%2e%2e/x`,
  `/%2E%2E/x`, `/.%2e/x`, `/./x`, `/c/..` (design position K); each through the object initializer **and** through
  `with`; `ex.Message` contains the template, `ParamName` is `PathTemplate`)
- `Dots_inside_a_segment_are_not_dot_segments` (`/a..b/{x}`, `/.well-known/{x}`, `/v1.2/{x}` are accepted)
- `Placeholder_names_are_any_brace_free_text_compared_ordinally` (`{id}` and `{Id}` are two placeholders; `{ id }` is a
  third, whose name keeps its spaces; a repeated `{id}` is one placeholder taking one value; design row `SEAM-26`)
- `A_valid_template_with_percent_escapes_and_several_placeholders_is_accepted` (`/a%20b/{x}/c/{y}`)
- `PathParameters_reject_dot_and_dot_dot_and_a_lone_surrogate_naming_the_key` (initializer and `with`; the message names
  the key and never the value)
- `PathParameters_are_re_based_on_ordinal_keys` (assign a dictionary made with `StringComparer.OrdinalIgnoreCase`; `Id`
  and `id` stay distinct; the same technique 2a uses for `RequestOptions.Tags`)
- `WithPathParameter_adds_and_replaces_without_mutating_the_original`
- `Equality_compares_PathParameters_by_content_and_delegates_the_rest` (two separately built descriptors with equal
  `PathParameters` and `Query` are equal and their hashes agree; `Headers` and `Body` compare as their own `Equals`
  does: by reference before 2a PR 5 and PR 6 respectively, so before those land the test shares one `Headers` and one
  `Body` instance, and after them it builds equal ones separately; design "Type shapes")
- `ToString_shows_method_and_template_never_values` (`"GET /pets/{id}"`; a secret in a path value, the query or a header
  never appears)

Red: CS0246, `OperationDescriptor` does not exist.

**Production.** New folder `src/Dexpace.Sdk.Core/Operations/`. `OperationDescriptor.cs`: `public sealed record
OperationDescriptor` exactly as design "Type shapes" (`required Method Method`, `required string PathTemplate`,
`ImmutableDictionary<string, string> PathParameters`, `Query Query`, `Headers Headers`, `RequestBody? Body`, `string?
OperationId`), validation in `field`-backed `init` accessors so `with` cannot bypass it (design §4); explicit
`Equals(OperationDescriptor?)` and `GetHashCode` (a record's generated equality compares `ImmutableDictionary` by
reference), overridden `ToString`. A new `internal static class PathTemplateSyntax` (`Operations/PathTemplateSyntax.cs`)
parses the template into literal and placeholder parts and validates it (outside placeholders an RFC 3986 path: `pchar`,
`/`, `%HH`; placeholder names non-empty and brace-free; no literal segment that is `.` or `..` once `%2E`/`%2e` is read
as `.`, design position K); the placeholder list is computed once in `PathTemplate`'s `init` and kept `internal`. The
dot-segment predicate is one `internal static bool IsDotSegment(ReadOnlySpan<char>)` on `PathTemplateSyntax`, reused by
task 5.2's rendered check. Keep each method ≤70 lines by keeping the parser in its own class.

**`PublicAPI.Unshipped.txt`** (the members below plus the record-synthesised ones the build lists):

```text
[O].OperationDescriptor
[O].OperationDescriptor.Body.get -> [Q].RequestBody?
[O].OperationDescriptor.Body.init -> void
[O].OperationDescriptor.Equals([O].OperationDescriptor? other) -> bool
[O].OperationDescriptor.Headers.get -> [C].Headers!
[O].OperationDescriptor.Headers.init -> void
[O].OperationDescriptor.Method.get -> [C].Method!
[O].OperationDescriptor.Method.init -> void
[O].OperationDescriptor.OperationDescriptor() -> void
[O].OperationDescriptor.OperationId.get -> string?
[O].OperationDescriptor.OperationId.init -> void
[O].OperationDescriptor.PathParameters.get -> System.Collections.Immutable.ImmutableDictionary<string!, string!>!
[O].OperationDescriptor.PathParameters.init -> void
[O].OperationDescriptor.PathTemplate.get -> string!
[O].OperationDescriptor.PathTemplate.init -> void
[O].OperationDescriptor.Query.get -> [Q].Query!
[O].OperationDescriptor.Query.init -> void
[O].OperationDescriptor.WithPathParameter(string! name, string! value) -> [O].OperationDescriptor!
```

(`Method` is `[C].Method`, a value type until 2a PR 4 makes it a reference type; the build's annotation is the
authority: the `!` on the `Method` lines appears only after 2a PR 4.)

**Docs.** Additive; the type remark states the generator's duty to format non-string path values with the invariant
culture (`CA1305`, position H) and that `Body` is carried by reference and never encoded. **IDs:** `SEAM-26`.
**Verify:** V-fast `OperationDescriptorTests`.

### Task 5.2 — `BuildRequest(Uri)` and the composition vectors (`SEAM-27`) — RULINGS 5 and 6

**Vector file first.** New `tests/vectors/seam/operation-compose.json`, shape `{ "source": "ruby-sdk@5b17395
gems/dexpace-core/test/dexpace/operation_build_request_test.rb; nodejs-sdk@54aeed4 packages/core/src/seams/
operation.test.ts", "cases": [ … ] }`, each case `{ "name", "base", "method", "template", "pathParameters", "query"
(pairs), "expected" (an `AbsoluteUri`) | "error" (`ArgumentException` | `InvalidOperationException`), "errorAt" (`init`
| `build`) }`. The implementer copies expected values from the two cited sibling files at those shas where they exist,
and **verifies every expected value with a throwaway probe** (as 2a does) before committing it. The cases this plan
requires, at minimum:

- the specification's `https://host/c?sig=abc` + `/pets` → `https://host/c/pets?sig=abc`; `/pets/{id}` with `id=a/b` →
  `…/pets/a%2Fb`; root base `https://h` and trailing-slash base `https://h/c/` (exactly one separator); a template
  without a leading slash; an empty template (base path untouched: `https://h/c/` stays, `https://h/c` stays)
- base query forms: `?` only → no query; `a=1&` (dangling `&` dropped) plus operation query `x=2` → `?a=1&x=2`;
  operation query only; `q=a b` → `q=a%20b`; a placeholder used twice
- value encoding: a space → `%20`, `+` → `%2B`, non-ASCII `é` → `%C3%A9`, a literal `%20` in the *template* kept
- userinfo and port kept: `https://u:p@h:8443/c` + `/pets`
- errors: a placeholder with no value → `InvalidOperationException` (`build`, **RULED 6**); a path parameter
  naming no placeholder → `InvalidOperationException` (`build`); values `.` and `..` → `ArgumentException` (`init`);
  a lone surrogate value → `ArgumentException` (`init`); a base with a fragment `https://h/c#f` and with an empty
  fragment `https://h/c#` (design fact 5) → `ArgumentException` (`build`); a relative base and a non-http(s) base
  `ftp://h/` → `ArgumentException` (`build`); a rendered dot-segment, `/.{a}.` with `a` = `""` →
  `InvalidOperationException` (`build`, design position K, **RULED 6**); an encoded value `%2e%2e` → `%252e%252e`
  (inert, accepted)

**Failing tests first.** New `tests/Dexpace.Sdk.Core.Tests/Operations/OperationBuildRequestTests.cs`, class
`OperationBuildRequestTests`, `Unit`:

- `BuildRequest_matches_the_composition_vectors` (`[Theory]` over `VectorFile.Load<ComposeCase>("seam/operation-
  compose.json")`; a test-local `ComposeCase` record; asserts `request.Url.AbsoluteUri` for a success case and the
  exception type, its `errorAt` phase and the message rules for an error case)
- `The_built_request_carries_method_headers_and_the_same_body_instance`
- `Each_projection_lands_in_its_request_part` (the conformance example: a parameterless GET setting only `Method` and
  `PathTemplate` assembles `GET <base>/pets`)
- `A_missing_placeholder_message_names_the_placeholder_and_never_a_value`
- `A_rejected_base_message_carries_the_base_through_UrlRedactor` (the 2a ruling that URL errors carry the redacted
  input: `https://user:secret@h/c#f` never shows `secret`; the expected text is computed from `UrlRedactor`, not
  hard-coded, as 2a's test does)
- `Composition_never_uses_Uri_ToString` is covered by the existing `M:System.Uri.ToString` ban (RS0030) and needs no test
- `BuildRequest_rejects_a_null_baseAddress`

Red: CS1061, no `BuildRequest`.

**Production.** `OperationDescriptor.BuildRequest(Uri baseAddress)` delegates to a new `internal static class
OperationUrlComposer` (`Operations/OperationUrlComposer.cs`), keeping every method ≤70 lines, composing over the base's
components and **never** `new Uri(base, relative)` (design §3.5): validate the base (absolute, `http`/`https`, no
fragment — `Fragment.Length > 0` catches the empty `#` too, fact 5; a base failure is `ArgumentException` whose message
carries the base through `UrlRedactor.Default`); authority from `GetLeftPart(UriPartial.Authority)` (keeps userinfo and
port, fact 5); base path from `GetComponents(UriComponents.Path, UriFormat.UriEscaped)` (no leading slash, fact 5);
rendered template = literals as written, each placeholder replaced by `Rfc3986.EncodeComponent(value)` (one segment
each); path = base path trimmed of trailing `/`, then `/`, then the rendered template trimmed of a leading `/`, an
empty template leaving the base path untouched; query = `GetComponents(UriComponents.Query, UriFormat.UriEscaped)`
with dangling `&` dropped, joined by `&` to `Query.Encode()`, no `?` when both are empty; the result is
`new Uri(text, UriKind.Absolute)`, then `new Request(Method, url, Headers, Body)`. A placeholder with no value and a
path parameter naming no placeholder throw `InvalidOperationException` naming the placeholder or parameter and never
its value (**RULED 6**). After substitution, each segment of the rendered template is checked with
`PathTemplateSyntax.IsDotSegment`, and a match throws the same type naming the template and never a value (design
position K). If `UrlRedactor.Default` (an `internal static UrlRedactor Default` on `UrlRedactor`)
does not exist yet because 2a PR 6 has not landed, this task adds it, an internal change with no public-API line, and
2a's PR 6 reuses it (design "Prerequisites").

**`PublicAPI.Unshipped.txt`:** `[O].OperationDescriptor.BuildRequest(System.Uri! baseAddress) -> [Q].Request!`.
**IDs:** `SEAM-27`. **Verify:** V-fast `OperationBuildRequestTests`, `VectorFileTests` (the new file's `source` field
is policed by it).

### Task 5.3 — `BuildRequest(DexpaceClientOptions)` and the `BaseAddress` read (`SEAM-27`)

**Failing tests first.** `OperationBuildRequestTests` additions: `BuildRequest_from_options_reads_BaseAddress`,
`BuildRequest_from_options_throws_ArgumentException_when_BaseAddress_is_null` (`ParamName` `options`),
`BuildRequest_from_options_applies_the_same_base_rules` (a relative or fragment-carrying `BaseAddress` fails as
`BuildRequest(Uri)` does), `BuildRequest_from_options_rejects_null_options`. In
`tests/Dexpace.Sdk.Core.Tests/Configuration/DexpaceClientOptionsTests.cs` (existing class):
`BaseAddress_is_read_by_BuildRequest`.

**Production.** `OperationDescriptor.BuildRequest(DexpaceClientOptions options)` reads `options.BaseAddress` and
delegates. `src/Dexpace.Sdk.Core/Configuration/DexpaceClientOptions.cs`: replace the **"Not yet read by anything"**
remark on `BaseAddress` with "read by `OperationDescriptor.BuildRequest(DexpaceClientOptions)`; must be an absolute
http(s) URI with no fragment, checked at `BuildRequest` (5a may check at construction when it makes the options a
record, `CFG-8`/`CFG-9`)". The property's shape is unchanged.

**`PublicAPI.Unshipped.txt`:** `[O].OperationDescriptor.BuildRequest(Dexpace.Sdk.Core.Configuration.DexpaceClientOptions!
options) -> [Q].Request!`. **IDs:** `SEAM-27`. **Verify:** V-fast both classes.

### Task 5.4 — `OperationId` (`SEAM-28`)

**Failing tests first**, `OperationDescriptorTests` additions: `OperationId_null_is_accepted_and_blank_is_rejected`
(`""` and `"  "` throw `ArgumentException`, initializer and `with`), `The_operation_id_never_reaches_the_url_headers_or_
body` (two descriptors differing only in `OperationId` build requests with equal `Url.AbsoluteUri`, equal headers
(enumerations, or `Headers` equality once 2a PR 5 lands) and the same body instance, and no header value or URL
contains the id).

**Production.** The `OperationId` `init` accessor's validation (declared in 5.1; the test is what fails first if 5.1
omitted the blank check). **IDs:** `SEAM-28` (the MUST NOT; the attachment to the context chain is ⏳ against 4a).
**Verify:** V-fast `OperationDescriptorTests`.

### Task 5.5 — Close-out

`CLAUDE.md` repository-layout tree gains `Operations/` (line: "`OperationDescriptor`, the operation-input projection").
If 2a PR 9 (`ModelImmutabilityArchitectureTests`) has already landed, add `OperationDescriptor` to its type list;
otherwise 2a's PR 9 does, and 2b's PR 5 notes the hand-off in its description (design "Coupling"). `CHANGELOG.md`
`### Added`: "`OperationDescriptor` and `BuildRequest` — the operation-input projection, RFC 3986 composed over the base
address (`SEAM-26`–`SEAM-28`); `DexpaceClientOptions.BaseAddress` is now read." Run **V-gate**. **Commit:** `feat:
OperationDescriptor and BuildRequest (SEAM-26, SEAM-27, SEAM-28)`.

---

## PR 6 — Close-out

**Gate: PRs 1–5 merged.** The docs close the phase (roadmap step 7). Rows: all 29 (closing).

### Task 6.1 — NativeAOT smoke over the new surface

Extend `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (`RunAllAsync` calls one more check per item, in its existing style
and with its `Expect` helper): `AsAsync(TaskScheduler.Default)` and `AsBlocking` round trips over an in-process
transport; both `DelegateHttpClient` forms; `SerializeToUtf8Bytes`, `SerializeToString` and the fixed-buffer
`Serialize` over `SystemTextJsonSerde` and the existing source-generated `SmokeJsonContext`; `SerdeException` caught
from a `DeserializationException`; and `OperationDescriptor.BuildRequest` with a path parameter, a query and a base
address. No reflection. If the publish emits a trim/AOT warning, fix the source, not the smoke. The check for this task
*is* the publish-and-run: `dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output
artifacts/aot-smoke && ./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke` (expected output "aot-smoke: all checks passed").
**IDs:** closing evidence for `SEAM-11`, `SEAM-18`, `SEAM-20`, `SEAM-23`, `SEAM-26`, `SEAM-27`.

### Task 6.2 — User documentation

New `docs/sdk-documentation/seams.md`. The directory may not exist yet: 2a's task 9.4 creates it with `http.md`; if 2b
lands first this task creates it, and `docs/README.md`'s ownership table gains the row the probe asks for. It opens
"As built by phase 2b … written against source on <date>", as the roadmap's universal exit criteria require. Content:
the transport SPI and its option-less calls; `AsAsync(TaskScheduler)` and `AsBlocking` with their caveats;
`DelegateHttpClient`; the serde seam, `SerdeExtensions` and the `SerdeException` hierarchy; `OperationDescriptor` and
`BuildRequest`; the breaking-change migration table (the seven items of design "Breaking changes", each with a
before/after). Cite requirement IDs; do not copy design §3. **Verify:** the probe's `links` check.

### Task 6.3 — The checklist

Write `docs/work/mvp/phase2/phase2b/<date>-phase2b-seams-checklist.md` from what was built (one row per requirement
ID, phase-1 legend), including: the **N/A and vacuous rows** — `SEAM-3`, `SEAM-4`, `SEAM-7`, `SEAM-9` N/A citing §3.1 or
§3.6 and the §10 topic; `SEAM-5` and `SEAM-6` N/A for the DI-less half and ⏳ phase 9 for the DI half; `SEAM-8` and
`SEAM-10` N/A vacuous (§3.6, §12); the vacuous clauses of `SEAM-17`, `SEAM-21`, `SEAM-24` and `SEAM-25` named inside
their ✅ rows (P2b-3); `SEAM-22` ✅ by construction (P2b-2); the ⏳ half of `SEAM-11`/`SEAM-13`/`SEAM-15` (8b),
`SEAM-12`/`SEAM-13` (8a), `SEAM-14` (3b), `SEAM-28` (4a) — the "existing assertions changed" table (the additive
`InnerException` assertions of task 1.3, the signature-only edits to `HangingTransport` and `EchoTransport`, the
bridge tests moved onto `DelegateHttpClient` in 4.2) — **each `Security` class listed as "unedited"** with the empty
diff check as evidence — the deviation ledger P2b-1 to P2b-7 as built, and the design gaps in this plan's "Findings"
that the lead resolved.

### Task 6.4 — Dated corrections, the ambiguity appendix, the roadmap note

Frozen documents change only by dated correction. In `docs/sdk-design-dotnet/`:

- §3.2 wording (`DelegateHttpClient.Create` and `CreateBlocking`, P2b-6) and its "As built" line;
- §3.4 (names the optional override interface `IStringSerde`, ruling 4; and "abstract `SerdeException`", ruling 1) and its
  "As built" line;
- `11-appendix-reference-spec-ambiguities-and-how-this-port-resolves-them.md`: numbered items for P2b-1 (`SEAM-2`'s
  "never implements"), P2b-2 (`SEAM-22`) and P2b-4 (the check after return), and a dated correction to item 35 for
  P2b-7 (dot-segments rejected in the template's literal text and in rendered segments too);
- `12-…` (the coverage index): `SEAM-17`'s second sentence, `SEAM-21`'s no-codec clause, `SEAM-24`'s adapter clause and
  `SEAM-25`'s first sentence added to the vacuous list (P2b-3).

Append a dated Phase Status Note for 2b to the roadmap (what landed, the six PRs, the six rulings, the hand-offs to
3b, 4a, 4c, 5a, 7a, 8a, 8b and 9 from design "Coupling"). `CLAUDE.md`'s "What is genuinely unbuilt" paragraph drops
"the transport SPI taking `RequestOptions`" and `BaseAddress` as unbuilt (they are as built), and its layout tree already
carries the PR 4 and PR 5 lines. If the implementation found anything the knowledge corpus should hold, record it as a
note under `docs/knowledge/notes/` (never edit `harvested/`). **Verify:** the probe's `links` and `citations` checks.

### Task 6.5 — Close-out

`CHANGELOG.md`: `### Added` "`docs/sdk-documentation/seams.md`; the AOT smoke covers the seam surface." Run **V-gate**
and the coverage gate (with its self-test). Run the housekeeping probe once more and fix what it reports. **Commits:**
`test: NativeAOT smoke over the seam surface`, then `docs: phase 2b checklist, user page and dated corrections`.

---

## Traceability: ID → PR → task

| ID | PR | Task(s) | ID | PR | Task(s) |
|---|---|---|---|---|---|
| `SEAM-1` | 2 | 2.1 | `SEAM-16` | 3, 4 | 3.2, 3.3, 3.4, 4.1 |
| `SEAM-2` | 2, 4 | 2.2, 4.1 | `SEAM-17` | 3 | 3.2 |
| `SEAM-3` | 6 | 6.3 (N/A row) | `SEAM-18` | 3 | 3.3, 3.4 |
| `SEAM-4` | 6 | 6.3 (N/A row) | `SEAM-19` | 1 | 1.4 |
| `SEAM-5` | 2, 6 | 2.2, 6.3 (N/A ⏳ row) | `SEAM-20` | 1 | 1.2, 1.3 |
| `SEAM-6` | 6 | 6.3 (N/A ⏳ row) | `SEAM-21` | 1 | 1.3 |
| `SEAM-7` | 6 | 6.3 (N/A row) | `SEAM-22` | 1 | 1.3, 1.4 |
| `SEAM-8` | 6 | 6.3 (N/A vacuous row) | `SEAM-23` | 1 | 1.1 |
| `SEAM-9` | 6 | 6.3 (N/A row) | `SEAM-24` | 3 | 3.4 |
| `SEAM-10` | 6 | 6.3 (N/A vacuous row) | `SEAM-25` | 3 | 3.4 |
| `SEAM-11` | 3, 4 | 3.2, 3.4, 4.1, 4.2 | `SEAM-26` | 5 | 5.1 |
| `SEAM-12` | 3, 4 | 3.5, 4.2 | `SEAM-27` | 5 | 5.2, 5.3 |
| `SEAM-13` | 3 | 3.2, 3.3, 3.4 | `SEAM-28` | 5 | 5.4 |
| `SEAM-14` | 2, 3 | 2.3, 3.3, 3.4 | `SEAM-30` | 2, 3 | 2.4, 3.4 |
| `SEAM-15` | 3, 4 | 3.2, 4.1 | | | |

Count: `SEAM-1`–`SEAM-28` (28) + `SEAM-30` (1) = 29 rows, all mapped. `SEAM-29` is 2a's (task 6.1 of its plan).

---

## Tasks per PR

| PR | Gate | Tasks |
|---|---|---|
| 1 | free now | 1.1–1.5 (5) |
| 2 | free now | 2.1–2.5 (5) |
| 3 | 2a PR 1 | 3.1–3.6 (6) |
| 4 | PR 3 | 4.1–4.3 (3) |
| 5 | 2a PR 2 | 5.1–5.5 (5) |
| 6 | PRs 1–5 | 6.1–6.5 (5) |
| **Total** | | **29 tasks** |

---

## Findings while planning

Design gaps and contradictions, each checked against the repository at `bf8f4ae` on 2026-09-30. Each item names what
the plan did about it. All twelve were then reviewed on 2026-09-30, and each ends with its **review** disposition:
confirmed items are fixed in the design and the tasks above, and the one rejected item is kept for the record.

1. **F1 — Documentation that states the old SPI is not in the design's migration table.**
   `docs/architecture.md:45-47` says "`IAsyncHttpClient.ExecuteAsync(Request, CancellationToken)`" and
   "`IHttpClient.Execute(Request)`", and `src/Dexpace.Sdk.Http.SystemNet/README.md:33` (packed into the NuGet package)
   calls `transport.ExecuteAsync(Request.Get(…))` with no `using Dexpace.Sdk.Core.Client;`, which fails with `CS7036`
   after PR 3. Plan: both edited in task 3.6. **Review:** confirmed; the design's migration table and PR 3 row now list both, and PR 4 updates the Core README's `Client` row (task 4.3).
2. **F2 — Design counts six `RS0030` pragmas in `src/`; there are five.** `TokenCredential.cs:46`,
   `HttpClientExtensions.cs:52`, `HttpPipeline.cs:79`, `SystemNetHttpClient.cs:212` and `:234`. None waives the
   `Uri.ToString()` ban, so the design's conclusion holds; only the count is off. **Review:** confirmed; the design's
   Prerequisites now says five and names them.
3. **F3 — The charset-based string profile (position G) leans on 2a's `HTTP-24` and on an unstated assumption.**
   `MediaType.Charset` throws `NotSupportedException` for `utf-7` today (`MediaType.cs:46`; 2a task 3.4 fixes it), so
   `SerializeToString` over a codec declaring it would throw until 2a PR 3 merges, which the design's "2a
   convenience" table does not list. Separately, the seam's own decode primitive is named `Deserialize(ReadOnlySpan<byte>
   utf8)`, so a codec declaring a non-UTF-8 charset contradicts the seam's stated wire encoding; the design's argument
   ("text codec in another charset gets a correct string") assumes the primitive writes that charset. Plan: the
   `utf-7` fallback test is held until 2a PR 3, and the assumption is left for ruling 4. **Review:** confirmed
   (`Encoding.GetEncoding("utf-7")` throws `NotSupportedException`, which `Charset` does not catch); the design's
   Prerequisites gained the 2a PR 3 edge, position G stated both consequences, and task 1.5 widened `ISerde`'s remarks
   under the default. *Superseded by ruling 4 (2026-09-30):* the lead took design §3.4's optional interface
   (`IStringSerde`) and withdrew the declared-charset rule, so neither consequence arises; the 2a PR 3 edge and the
   widened remarks are removed.
4. **F4 — `SEAM-16`'s null rule is specified for `AsAsync` and `DelegateHttpClient` only.** `AsBlocking` over a
   transport returning a `null` task or `null` result would give a `NullReferenceException` from `GetAwaiter()` or hand
   a `null` response to the caller (as-built `AsyncToSyncAdapter.Execute`). Plan: task 3.3 applies the same
   `InvalidOperationException` rule and tests it. **Review:** confirmed; the design's `SEAM-16` row and the `AsBlocking`
   type shape now carry the rule.
5. **F5 — The design's `TestSupport` additions omit a codec that writes bytes.** `ScriptedSerde` writes nothing
   (`Serialize` and `SerializeAsync` are empty), yet `SEAM-19`'s "codec declaring `application/xml`" test and every
   `SEAM-20` profile test need one. Plan: `BytesSerde` in `TestSupport` (task 1.2). **Review:** confirmed; the design's fakes, its `SEAM-19`/`SEAM-20` rows
   and its PR 1 row now name `BytesSerde`.
6. **F6 — The `BannedSymbols.txt` list is incomplete.** The design bans `TaskCompletionSource<T>.SetResult`/
   `TrySetResult` and `Task<T>.WaitAsync`; but the non-generic `TaskCompletionSource` (.NET 5+) has its own doc IDs,
   `Task.WaitAsync` and `Task`1.WaitAsync` are distinct doc IDs, and .NET 8 added `TimeProvider` overloads. The as-built
   `AccessTokenCache.cs:84` uses `SemaphoreSlim.WaitAsync`, a different symbol that must keep compiling. Plan: task 2.4
   lists every overload and probes them. **Review:** confirmed in part. The five `Task<T>.WaitAsync` overloads are
   distinct doc IDs and are now listed in the design's `SEAM-30` row. The non-generic `TaskCompletionSource` and
   `Task.WaitAsync` are rejected: neither carries a `Response`, so `SEAM-30` does not reach them, and task 2.4 no
   longer bans them (its probe confirms they stay unbanned).
7. **F7 — The `SEAM-11` test approach cites a PR 4 type from a PR 3 test.** The row's
   `HttpClientExtensionsTests.The_option_less_Execute_passes_…` says "a `DelegateHttpClient` captures both by
   reference", but `DelegateHttpClient` lands in PR 4. Plan: PR 3 uses `RecordingTransport.LastCall` (which the design
   adds), and 4.2 adds the `DelegateHttpClient` variant. **Review:** confirmed; the design's `SEAM-11` row now says so.
8. **F8 — A template literal can traverse the base path.** The design validates `PathTemplate` as an RFC 3986 path
   (`pchar`, `/`, `%HH`) and rejects `.`/`..` only as *placeholder values*. A literal template `/../x` or `/%2e%2e/x`
   passes that validation, and `new Uri(...)` then collapses it: verified 2026-09-30 on the pinned SDK,
   `new Uri("https://h/c/../x").AbsoluteUri` is `https://h/x` and so is `…/c/%2e%2e/x` (whereas `%252e%252e` from an
   *encoded value* stays inert). A generator emitting such a template escapes the base path. Suggested ruling: reject a
   dot-segment (literal or `%2e`-encoded) in the template too, with a test in task 5.1. The plan does **not** add it,
   because it is not in the design. **Review:** confirmed (re-verified, plus `.%2e`, `%2e.` and `%2E%2E`), and wider: an
   empty value between literal dots (`/.{a}.`, `a` = `""`) renders `/..`, which a literal check alone misses. The
   design now rejects a literal dot-segment at construction and a rendered one at `BuildRequest` (position K, P2b-7,
   a dated correction to §11 item 35), and tasks 5.1 and 5.2 test both.
9. **F9 — `OperationDescriptor.Equals` says "Body per `RequestBody.Equals`", which does not exist until 2a PR 6.**
   As built, `RequestBody` has no equality members, so before 2a's task 6.3 a body compares by reference. Plan: the
   test pins whichever holds at the time and the PR states it. **Review:** confirmed, and the same holds for `Headers`,
   which has no `Equals` until 2a PR 5. The design's `Equals` now delegates to each member's own equality, its
   Prerequisites lists both edges, and task 5.1's equality test says what it compares before and after them.
10. **F10 — Placeholder-name syntax is unspecified.** The design says placeholder names must be non-empty and the
    braces balanced, and nothing about whitespace, control characters or a repeated name (`{ id }` is a different
    parameter from `{id}`). The plan permits any brace-free non-empty name and allows a repeated placeholder (a vector
    case). A stricter rule (an RFC token, or `[A-Za-z0-9_.-]`) is cheap to add before the API ships. **Review:** confirmed as a
    specification gap; the design's `SEAM-26` row and type shape now state the plan's rule, which is Ruby's
    (`\{([^{}]*)\}`), and task 5.1 tests it.
11. **F11 — A disposal failure in `AsAsync`'s check after return faults the task instead of cancelling it.** Design
    position E disposes with a direct `Dispose()` "so a failure propagates" (styleguide 8.4), but the conformance clause
    says the response "never surfaces" and the caller is `WaitAsync`-abandoned, so a throwing `Dispose` becomes an
    unobserved task exception. Low severity, and 3b's `Disposal.DisposeQuietly` removes it; recorded so the lead can
    decide whether to swallow-and-log now. **Review:** confirmed (verified: the task completes `Faulted`); design
    position E and ruling 3 now state the consequence, the rulings table records it against ruling 3,
    and task 3.4 pins it.
12. **F12 — `SEAM-1`'s "references only the shared framework" test is evidence for the wrong half of the sentence.**
    Verified: core references `System.Diagnostics.DiagnosticSource`, `System.Collections.Concurrent` and the logging
    facade, none of which the design's banned list mentions, so the test is an allow-nothing/deny-four check, not an
    allow-list. That matches the design row; a stricter allow-list would duplicate `scripts/ci/dependency-audit.cs` and
    was not planned. **Review:** rejected. The design row pins only the "no concrete capability" half on purpose and cites
    `dependency-audit.cs` for the allow-list half, which already enforces it on every pack. The three extra references
    are shared-framework or sanctioned (§10 `logging-abstractions-dependency`), so there is nothing to fix.
13. **F13 (found in review) — `OperationDescriptor`'s `Method` null check depends on 2a PR 4.** PR 5 is gated on 2a
    PR 2 only, but until 2a PR 4 `Method` is a `readonly record struct`, so the type shape's "null →
    `ArgumentNullException`" has nothing to reject and `A_null_Method_or_PathTemplate_throws_ArgumentNullException`
    cannot pass a `null` `Method`. **Review:** confirmed; the design's Prerequisites and Coupling gain the 2a PR 4
    convenience edge (the check and its test follow 2a PR 4, in PR 6 at the latest), and PR 5's gate and task 5.1 say so.
14. **F14 (found in review) — Breaking item 7 had no XML-doc marking.** Convention 7 and design "Breaking changes"
    require each item to be marked in the docs of the member it changes, but task 3.2 marked none for the
    `PipelineRunner` null guard. **Review:** confirmed; task 3.2 now adds the `<b>Breaking (behaviour):</b>` paragraph to
    `PipelineRunner.RunAsync`'s remarks.

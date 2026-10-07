# Phase 3a — I/O: Checklist

The execution-time checklist for sub-phase 3a of roadmap phase 3
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `49-phase-3a-io`, following the
[design](2026-10-02-phase3a-io-design.md) and the [plan](2026-10-02-phase3a-io.md) (GitHub issue #49).

The scope is 43 rows by the lead's ruling of 2026-10-02 on P3a-1: `IO-1`–`IO-42` and `HTTP-39`. `HTTP-36` and `HTTP-52` are
3b's rows (see [Cross-references](#cross-references-to-3bs-rows)). Every test below is `[Trait("Category", "Unit")]` and lives in
`tests/Dexpace.Sdk.Core.Tests/` unless a project is named; phase 3a adds no `Security` class. **Red evidence:** for a new type or a
changed signature the red was the compile error (the plan's convention 1). The production types of each step were written in
the same sitting as their tests, and the full suite was run green after each step; the behavioural reds were not each captured
separately, and no pin was proven able to fail by a temporary break. A test marked "pin" holds behaviour that was already
correct.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `IO/{StreamCopyTests,PooledChunkTests,CapturedBytesTests,TeeStreamTests,Utf8LineReaderTests,BodyMaterializerTests,BoundedBufferStreamTests,TextRoundTripTests,ZeroCountReadRuleTests,TestDoubleTests}.cs`,
`Http/Request/{RequestBodyStreamTests,RequestBodyContractTests}.cs`, `Http/Response/ResponseBodyTests.cs`,
`Exceptions/BodyTooLargeExceptionTests.cs`, vectors `tests/vectors/io/utf8-lines.json`; in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`:
`ExactLengthBodyWireTests.cs` (`Integration`); the AOT smoke check is `CheckBodiesAndIoAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.

| ID | Level | Status | What was built | Proven by |
|---|---|---|---|---|
| `IO-1` | MUST | N/A; ✅ | N/A for the letter (`Source.read(Buffer)` and the −1 sentinel do not exist). ✅ for the invariant: every core accumulator appends in order and no read returns more than requested. Design §3.1, §10 `eof-is-zero-read`. | `StreamCopyTests.Draining_into_a_non_empty_accumulator_appends_after_its_existing_bytes`, `A_chunked_source_is_reassembled_in_order` (1, 2, 3, 7 bytes per read, both forms) |
| `IO-2` | MUST | N/A; ✅ (P3a-7) | N/A for the letter; ✅ for the caller-side rule: core never issues a zero-count read, and `CopyExactly(…, 0)` performs no read. | `StreamCopyTests.No_helper_issues_a_zero_count_read`, `CopyExactly_of_zero_performs_no_read`; `Utf8LineReaderTests.No_read_is_ever_zero_count`; `BodyMaterializerTests.No_read_is_ever_zero_count`; `RequestBodyContractTests.Sync_members_add_no_zero_count_read`; `ResponseBodyTests.ReadAsBytes_never_issues_a_zero_count_read`; the union in `ZeroCountReadRuleTests` (five theories over source lengths 0, 1, 7, a chunk, a chunk plus one) |
| `IO-3` | MUST | ✅ | Every size-taking parameter core adds throws `ArgumentOutOfRangeException` before any I/O: `CopyExactly`'s count, `DrainUpTo`'s cap, `CapturedBytes.Slice`, `TeeStream`'s tap limit, `Utf8LineReader`'s cap, `BoundedBufferStream` and `BodyMaterializer`'s limit, and `FromStream`'s `contentLength` (below −1) on both bodies. | `StreamCopyTests.A_negative_argument_throws_before_any_io`; `CapturedBytesTests.A_negative_offset_or_count_is_rejected_eagerly`; `TeeStreamTests.A_negative_tap_limit_throws_ArgumentOutOfRangeException`; `Utf8LineReaderTests.A_non_positive_cap_throws_ArgumentOutOfRangeException`; `BoundedBufferStreamTests.A_negative_limit_throws_ArgumentOutOfRangeException`; `RequestBodyStreamTests.FromStream_rejects_a_content_length_below_minus_one`; `ResponseBodyTests.ResponseBody_FromStream_rejects_a_content_length_below_minus_one` |
| `IO-4` | MUST | ✅ | `TeeStream` forwards the full span to the primary in one call, in every write form. | `TeeStreamTests.The_primary_receives_every_byte_written_in_order` |
| `IO-5` | MUST | ✅ | `TeeStream` forwards `Flush`/`FlushAsync`; every core wrapper is `IDisposable`/`IAsyncDisposable`. | `TeeStreamTests.Flush_and_FlushAsync_reach_the_primary`; the `IO-41` dispose tests |
| `IO-6` | MUST | ✅ (helper half; body half is 3b's `BODY-8`) | Wrapping takes ownership unless `leaveOpen: true`, for `TeeStream` and `Utf8LineReader`; `CapturedBytes` owns no external resource. Design §10 `body-stream-ownership`, P3a-4. | `TeeStreamTests.Disposing_the_wrapper_disposes_the_stream_it_owns`, `With_leaveOpen_the_primary_survives_the_wrapper`; `Utf8LineReaderTests.Disposing_the_reader_disposes_the_stream_it_owns`, `With_leaveOpen_the_stream_survives_the_reader` |
| `IO-7` | MUST | N/A (P3a-9) | No `Buffer` type ships: `ArrayBufferWriter<byte>` is the write side and `CapturedBytes` the read side. Design §3.1, §10 `byte-stream-provider-retired`, position C; §12's "not argued by ID" note is closed by dated correction. | none of its own; `IO-1`'s ordering tests exercise the pair |
| `IO-8` | MUST | ✅ | `CapturedBytes.ToArray()` and `TeeStream.SnapshotTap()` each return a fresh copy and consume nothing. | `CapturedBytesTests.ToArray_returns_a_fresh_copy_each_call`; `TeeStreamTests.A_snapshot_is_unchanged_by_later_writes` |
| `IO-9` | SHOULD | ✅ | `BodyTooLargeException` (public, sealed, under `StreamingException`) names the limit and points at `OpenRead`/`OpenReadAsync`. A declared length above the limit is refused before any read or write. Response readers cap at `DefaultMaxMaterializedBytes` (64 MiB, P3a-12); `ToReplayable(Async)` is bounded by `Array.MaxLength`. The slice clause is vacuous (a capture is an array). Breaking 3 and 4. | `BodyMaterializerTests.A_declared_length_above_the_limit_is_refused_before_any_read`, `An_unknown_length_body_that_exceeds_the_limit_is_refused`, `An_unknown_length_body_of_exactly_the_limit_succeeds`, `A_body_one_byte_over_the_limit_is_refused_by_the_probe`; `ResponseBodyTests.ReadAsBytes_over_a_declared_length_above_the_cap_…`, `ReadAsString_over_…`, `The_public_readers_refuse_a_declared_length_above_the_default_cap_without_allocating_it`; `RequestBodyContractTests.ToReplayable_refuses_a_declared_length_above_Array_MaxLength_without_consuming_the_body`; `BoundedBufferStreamTests`; `BodyTooLargeExceptionTests`; AOT smoke `BodyTooLargeException is raised through the public path` |
| `IO-10` | MUST | N/A (P3a-9) | `Buffer.copyTo` and `clear` retire with the `Buffer`; the one windowing operation, `CapturedBytes.Slice`, follows `IO-21`'s lazy rule. Design §11 item 44 (dated correction). | `Slice(…).ToArray()` is `IO-20`/`IO-21`'s test |
| `IO-11` | MUST | N/A; ✅ | N/A for `exhausted()` and `readByte()`. ✅ for the count-less drain: `ReadAsBytes(Async)` returns every remaining byte and an empty array for an empty body. | `ResponseBodyTests.ReadAsBytes_of_an_empty_body_is_an_empty_array` (pin, both forms, over `FromBytes([])` and `FromStream(Stream.Null)`), `ReadAsBytes_returns_every_byte_of_a_chunked_source`; `BodyMaterializerTests.ReadAll_returns_every_byte_of_a_chunked_source`, `ReadAll_of_an_empty_source_is_an_empty_array` |
| `IO-12` | MUST | ✅ | `StreamCopy.CopyExactly(Async)` never delivers a short result; a premature end throws `EndOfStreamException` naming delivered-of-total. | the `HTTP-39` tests |
| `IO-13` | MUST | ✅ | Free on `Encoding`; both string readers decode through one private routine on `ResponseBody` (3b's PR 1 had not landed, so the plan's R3 branch was taken; 3b's `TextDecoding` then converges both). The line reader decodes UTF-8 only, replacing malformed sequences (§11 item 45). | `TextRoundTripTests.Non_ASCII_text_round_trips_through_UTF_8_and_ISO_8859_1` (sync and async); `ResponseBodyTests.ReadAsString_decodes_with_the_declared_charset_and_defaults_to_UTF8`; `Utf8LineReaderTests.A_multi_byte_character_split_across_reads_decodes_once`, `A_malformed_sequence_decodes_to_U_FFFD` |
| `IO-14` | MUST | ✅ | Internal `Utf8LineReader`, default mode: `\n` and `\r\n` terminate, a lone `\r` is content (one byte of lookahead across a refill), `null` when exhausted, a final unterminated line as is, an empty line is `""`. `StreamReader.ReadLine` and `TextReader.ReadLine*` are banned in `src/`. | `Utf8LineReaderTests.Vectors_produce_the_expected_lines_at_every_read_size` (22 vectors at 1, 2, 3, 7 and 4,096 bytes per read, both forms), `A_lone_CR_before_EOF_is_content`, `A_lone_CR_is_decided_by_one_byte_of_lookahead_across_a_refill`, `A_line_longer_than_the_cap_…` (fails and stays failed), `A_line_of_exactly_the_cap_is_accepted`, `Reading_a_long_line_byte_by_byte_scans_each_byte_once`, `Whatwg_mode_terminates_on_CR_immediately_…` |
| `IO-15` | MUST | N/A | No `skip`; `Stream.Seek` or a discard read is the BCL's. Design §10 `eof-is-zero-read`. | none |
| `IO-16` | SHOULD | N/A | The SDK's byte surface already is the host-native `Stream`. Design §3.1, §10 `eof-is-zero-read`. | none |
| `IO-17` | MUST | ✅; N/A | ✅ for the pump: `StreamCopy.CopyToEnd(Async)` pumps to end of stream, returns the total, and `RequestBody.FromStream` of unknown length uses it. N/A for the zero-read-is-a-violation clause (0 is EOF on .NET). | `StreamCopyTests.CopyToEnd_returns_the_total_and_stops_on_end_of_stream`; `RequestBodyStreamTests.An_unknown_length_stream_body_copies_to_the_end` (pin) |
| `IO-18` | SHOULD | N/A | `Stream` has no emit tier. Design §10 `eof-is-zero-read`. | none |
| `IO-19` | MUST | ✅ | `CapturedBytes.OpenRead()` returns a fresh read-only view with its own cursor. | `CapturedBytesTests.Reading_one_view_moves_no_other_view`, `A_view_is_read_only_and_not_publicly_visible` |
| `IO-20` | MUST | ✅ | `CapturedBytes.Slice(offset, count)` is a window of at most `count` bytes; reading past it returns 0; the parent is untouched. | `CapturedBytesTests.A_slice_exposes_its_window_and_then_ends`, `Slicing_leaves_the_parent_unchanged` |
| `IO-21` | MUST | ✅ | `Slice` with an offset past the end constructs and reads empty (needs a clamp, design fact 3); a negative offset or count throws at construction. | `CapturedBytesTests.An_overflowing_slice_constructs_and_reads_empty` (`Read` returns 0, `ReadExactly` throws `EndOfStreamException`), `A_negative_offset_or_count_is_rejected_eagerly`; `Utf8LineReaderTests.An_overflowing_CapturedBytes_slice_reads_as_null` (the line-reader form) |
| `IO-22` | MUST | ✅ (P3a-8) | Closing a view closes neither the capture nor another view; "closing the parent invalidates every slice" holds by construction, since captured bytes are never pooled, and the rule is mechanised by the `RS0030` ban on `ArrayPool<T>.Rent` / `MemoryPool<T>.Rent` (verified to fire with a throwaway probe). | `CapturedBytesTests.Disposing_a_view_leaves_the_capture_and_other_views_readable`; `PooledChunkTests`; the build (`BannedSymbols.txt`) |
| `IO-23` | MUST | ✅ | Views and slices have independent cursors; a slice of a slice adds offsets and is capped by the outer window. | `CapturedBytesTests.A_slice_of_a_slice_composes_additively_and_is_capped_by_the_outer_window`, `Slices_and_views_have_independent_cursors` |
| `IO-24` | MUST | ✅ | Reading a disposed view throws `ObjectDisposedException`, not an EOF, in every read form (design fact 6). | `CapturedBytesTests.Reading_a_disposed_view_throws_in_every_read_form` |
| `IO-25` | MUST | ✅ | `TeeStream` mirrors each write into a private tap and forwards the full payload to the primary. | `TeeStreamTests.The_primary_receives_the_full_payload_whatever_the_tap_limit` |
| `IO-26` | MUST | ✅ | A tap limit; at the limit mirroring stops and forwarding continues; default unbounded (clamped to `Array.MaxLength`); 0 mirrors nothing. | `TeeStreamTests.The_tap_stops_at_its_limit_while_forwarding_continues`, `A_zero_limit_mirrors_nothing`, `The_default_limit_mirrors_everything` |
| `IO-27` | MUST | ✅ | Mirror then forward, with no staging buffer, so a failed primary write leaves its attempted bytes in the tap. | `TeeStreamTests.A_failed_primary_write_still_leaves_the_attempted_bytes_in_the_tap`, `A_write_after_a_failed_one_adds_only_its_own_bytes` |
| `IO-28` | MUST | ✅ (by construction) | `TeeStream` does not derive from `MemoryStream`, and no member hands out the tap; `SnapshotTap()` returns a fresh copy. | `TeeStreamTests.Exposes_no_handle_on_its_tap` (reflection, test-only) |
| `IO-29` | MUST | ✅ | `Flush`, `FlushAsync`, `Dispose` and `DisposeAsync` reach the primary only; the tap survives disposal. | `TeeStreamTests.Dispose_reaches_the_primary_once_and_the_tap_survives`, `Flush_never_touches_the_tap` |
| `IO-30` | MUST | N/A | No provider seam, so no factories. The byte-array-source clause is held by `FromBytes` (`XCUT-15`). Design §10 `byte-stream-provider-retired`, §3.6. | none |
| `IO-31` | MUST | N/A | No registry, no discovery. Design §10 `byte-stream-provider-retired`, `no-provider-registry`. | none |
| `IO-32` | MUST | N/A | Gap ID, read from appendix C (install idempotence, different-provider rejection): there is no install call. | none |
| `IO-33` | MUST | N/A | Gap ID (explicit install wins after auto-resolution): neither exists. | none |
| `IO-34` | SHOULD | N/A | Gap ID (cache a successful auto-resolution): no resolution to cache. | none |
| `IO-35` | SHOULD | N/A | Gap ID (warn on late replacement of a handed-out provider): nothing can be replaced. | none |
| `IO-36` | MAY | N/A | One default load context; no hierarchical discovery. | none |
| `IO-37` | MUST | ✅ | Each helper's remarks state the single-threaded contract; the one concurrency guarantee, independent views read from different threads, is tested. | `CapturedBytesTests.Independent_views_can_be_read_on_different_threads`; the remarks on `StreamCopy`, `PooledChunk`, `CapturedBytes`, `TeeStream`, `Utf8LineReader`, `BodyMaterializer`, `BoundedBufferStream` |
| `IO-38` | MUST | ✅ (P3a-8) | No capture is pooled, so there is no parent close whose visibility could tear; the `RS0030` entry forces any later phase that pools one to face the `Volatile`-read closed flag. | the build (`BannedSymbols.txt`) |
| `IO-39` | SHOULD | N/A | No registry. Design §10 `byte-stream-provider-retired`. | none |
| `IO-40` | MUST | ✅ | Core never sets `ReadTimeout`/`WriteTimeout` (banned in `src/`); `TeeStream` forwards the caller's token unchanged and lets the primary's `OperationCanceledException` propagate as the same instance; the sync copy helpers check the token between chunks. | `TeeStreamTests.The_callers_token_reaches_the_primary_unchanged`, `An_OperationCanceledException_from_the_primary_propagates_as_the_same_instance`; `StreamCopyTests.A_cancelled_token_stops_the_sync_copy_between_chunks`, `Cancellation_surfaces_the_same_OperationCanceledException_from_the_async_copy`; `RequestBodyStreamTests.A_cancelled_token_stops_the_exact_copy`; `RequestBodyContractTests.A_cancelled_token_aborts_ToReplayable`; `ResponseBodyTests.A_cancelled_token_aborts_ReadAsBytes` |
| `IO-41` | MUST | ✅ | `TeeStream` and `Utf8LineReader` latch their dispose with `Interlocked.Exchange`, so the owned stream is disposed at most once across any mix of `Dispose` and `DisposeAsync` (design fact 11). | `TeeStreamTests.Disposing_twice_in_any_mix_disposes_the_owned_stream_once`; `Utf8LineReaderTests.Disposing_twice_in_any_mix_disposes_the_owned_stream_once` |
| `IO-42` | MUST | ✅ | After dispose `TeeStream.Write*`/`Flush*` and `Utf8LineReader.ReadLine*` throw `ObjectDisposedException`; `SnapshotTap()` still works. | `TeeStreamTests.Writing_after_dispose_throws_ObjectDisposedException`, `Flush_after_dispose_throws_ObjectDisposedException`, `The_tap_can_be_snapshotted_after_dispose`; `Utf8LineReaderTests.Reading_after_dispose_throws_ObjectDisposedException` |
| `HTTP-39` | MUST | ✅ | `StreamCopy.CopyExactly(Async)`: exactly `count` bytes, `EndOfStreamException` "The source ended after {delivered} of {count} bytes." on a short source, no read for 0, never reads past `count`. `RequestBody.FromStream` with a known length uses it in both forms. Breaking 1. | `StreamCopyTests.CopyExactly_writes_exactly_count_bytes_when_the_source_is_equal_or_longer`, `CopyExactly_leaves_the_longer_sources_remainder_unread`, `CopyExactly_on_a_short_source_throws_EndOfStreamException_naming_both_numbers`, `ShortTransferMessage_has_one_form`; `RequestBodyStreamTests.A_known_length_stream_body_writes_exactly_that_many_bytes_from_a_longer_source`, `…over_a_short_source_throws_EndOfStreamException_naming_both_numbers`, `A_zero_length_stream_body_issues_no_read`; `RequestBodyContractTests.WriteTo_of_a_known_length_stream_body_writes_exactly_the_declared_bytes`; `ExactLengthBodyWireTests` (`Integration`: `A_body_shorter_than_its_declared_length_fails_the_send`, `A_source_longer_than_its_declared_length_sends_exactly_the_declared_bytes`, `A_body_of_exactly_its_declared_length_round_trips`); AOT smoke `known-length FromStream writes exactly its length in both forms` |

## Cross-references to 3b's rows

`HTTP-36` and `HTTP-52` are 3b's rows by the lead's ruling on P3a-1 and carry no mark here. 3b's checklist cites this work:

- **`HTTP-36` (the sync twin).** `RequestBody.WriteTo` and `ToReplayable` and `ResponseBody.OpenRead`, `ReadAsBytes` and
  `ReadAsString`, virtual with a `NotSupportedException` default (P3a-5), every SDK variant overriding both forms over one
  consume latch. Tests: `RequestBodyContractTests` (the test-local subclass reporting −1 and `false` whose `WriteTo` throws naming
  the subclass; `Each_SDK_variant_writes_the_same_bytes_through_WriteTo_and_WriteToAsync`; the shared-latch test;
  `ToReplayable_and_ToReplayableAsync_return_equal_bodies`), `ResponseBodyTests` (`OpenRead_after_OpenReadAsync_throws_StreamConsumedException`
  and the reverse, `OpenRead_of_an_unoverridden_subclass_throws_NotSupportedException_naming_the_subclass`).
- **`HTTP-52` (the drain re-home).** `Response.EnsureSuccessAsync` now drains through `StreamCopy.DrainUpToAsync` into an
  `ArrayBufferWriter<byte>` with a pooled chunk; the private `DrainCappedAsync` is deleted and the `try`/`finally` dispose scope is
  kept verbatim. `EnsureSuccessErrorMappingTests` (`Security`, all six methods) and `EnsureSuccessTests` passed unedited before and
  after; the pin is `StreamCopyTests.DrainUpTo_stops_at_the_cap_and_reports_whether_the_end_was_seen` (and
  `DrainUpTo_keeps_the_bytes_read_before_a_failure`). `ErrorBodyBuffer`, `ErrorMappingPolicy` and the no-body clause stay ⏳ 4c.

## Exit criteria

- [x] All 43 rows carry a mark: 26 ✅ (`IO-3`–`IO-6`, `IO-8`, `IO-9`, `IO-12`–`IO-14`, `IO-19`–`IO-29`, `IO-37`, `IO-38`, `IO-40`–`IO-42`,
  `HTTP-39`; `IO-6` is the helper half), four split `N/A; ✅` or `✅; N/A` (`IO-1`, `IO-2`, `IO-11`, `IO-17`), and 13 N/A (`IO-7`, `IO-10`,
  `IO-15`, `IO-16`, `IO-18`, `IO-30`–`IO-36`, `IO-39`). No ⏳ half and no 🚫.
- [x] No `Security` class was edited (below); `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` is empty.
- [x] `PublicAPI.Unshipped.txt` changed in PR 5 only (ten added lines: `BodyTooLargeException` and its three constructors, `RequestBody.WriteTo` and
  `ToReplayable`, `ResponseBody.OpenRead`, `ReadAsBytes`, `ReadAsString` and `DefaultMaxMaterializedBytes`); `PublicAPI.Shipped.txt` is still empty.
- [x] `CHANGELOG.md` `[Unreleased]` carries a line per change, prefixed **Breaking:** where design "Breaking changes" lists it (items 1 to 5).
- [x] User page `docs/sdk-documentation/io.md` is written ("As built by phase 3a … written against source on 2026-10-03").
- [x] Dated corrections to design §3.1, §10 entries 4 and 6, §11 (items 44 and 45) and §12; roadmap Phase Status Note appended; `CLAUDE.md`, `docs/README.md`,
  `docs/architecture.md` and `src/Dexpace.Sdk.Core/README.md` updated; the housekeeping probe reports no drift.

## Existing assertions changed

### `Security` classes (constraint 5)

| File | Diff |
|---|---|
| `Http.SystemNet.Tests/Security/*` (all wire classes) | **unedited** |
| `Core.Tests/Security/*` (all classes), including `EnsureSuccessErrorMappingTests` | **unedited.** The drain re-home is behaviour-preserving; its `TrackingBody` needed no new override because the sync members are virtual |

Evidence: `git diff --stat main...HEAD -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` is empty.

### Other tests

None. Every pre-existing test compiles and passes unedited; the new test classes are additive. The only existing test-project file edited is
`tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs` (one new check appended). `StreamRequestBody`'s second-write message gained "or ToReplayable()" (plan reading R8); no test asserts the string.

## Deviation ledger, as built

| ID | As built | Route |
|---|---|---|
| P3a-3 | A second open of a response body throws `StreamConsumedException` in either form; one latch per variant, shared by `OpenRead` and `OpenReadAsync`. | design §10 entry 6, dated correction |
| P3a-5 | The sync body members are virtual; `WriteTo` and `OpenRead` throw `NotSupportedException` naming the subclass unless overridden. | design §3.1, dated correction |
| P3a-8 | No capture is pooled; `ArrayPool<T>.Rent` and `MemoryPool<T>.Rent` are banned in `src/`, the sole rent being the internal `PooledChunk` behind a scoped pragma. | design §3.1, dated correction |
| P3a-9 | `IO-7` and `IO-10` are N/A; `CapturedBytes.Slice` follows `IO-21`'s lazy rule. | design §11 item 44 |
| P3a-11 | `Utf8LineReader` decodes malformed UTF-8 to U+FFFD and keeps a BOM as content. | design §11 item 45 |
| P3a-12 | The 64 MiB cap applies to the response readers only; `ToReplayable(Async)` is bounded by `Array.MaxLength`; the cap is a constant until 5a. *Dated correction, 2026-10-07 (phase 5a, P5a-23):* 5a does not make it an options member; the configurable form is 7a's per-read limit (the SSE line cap is 7b's). | design §3.1, dated correction |

## Findings while building

1. **The stream-timeout ban is a property ban.** The plan's `M:System.IO.Stream.set_ReadTimeout(System.Int32)` and `set_WriteTimeout` entries did not fire in the
   throwaway probe (RS0030 reports a property reference against its `P:` symbol). `BannedSymbols.txt` carries `P:System.IO.Stream.ReadTimeout` and `WriteTimeout`
   instead, which also bans reading the property, a use core has no need of. Every other entry (both pool rents, `TextReader`/`StreamReader`/`StringReader`
   `ReadLine` and `ReadLineAsync`) fired as written.
2. **`EnsureSuccessAsync` inlines the drain.** Following task 1.5, `DrainCappedAsync` is deleted rather than kept as a thin wrapper; the stream is opened and
   disposed in the existing try scope.
3. **The pre-size test measures allocation.** `BoundedBufferStreamTests.A_known_expected_length_pre_sizes_the_buffer` compares the bytes allocated on the current
   thread by a pre-sized and a growing buffer for the same payload (capacity is not observable). It is the plan's F6 risk: delete it if it ever flakes.
4. **`PooledChunk` keeps the private `Lease`.** Disposing two copies of the struct returns the array once, and reading `Array` after dispose throws
   `ObjectDisposedException` (plan F5; the simpler plain struct was not taken).
5. **The `IO` namespace hazard did not bite.** Code uses the global `using System.IO;` and never `IO.X`; the new TestSupport doubles are in
   `Dexpace.Sdk.TestSupport.Streams` (folder `IO/`), and the core test classes are in `Dexpace.Sdk.Core.Tests.IO`, as planned.
6. **V2 entry steps: none.** At the start of PR 5 there were no 3b `RequestBody` or `ResponseBody` subclasses on the branch beyond the as-built private variants and
   the SystemNet `HttpResponseMessageBody`, and `TextDecoding` did not exist, so the decode routine is `ResponseBody`'s private `Decode`, which 3b's PR 1 converges.
7. **Test analyzers.** The test projects build under `TreatWarningsAsErrors` with xUnit's analyzers: calls taking a `CancellationToken` use
   `TestContext.Current.CancellationToken` (`xUnit1051`), and the one fixture that deliberately calls the `byte[]` `WriteAsync` overload (design fact 1) disables
   `CA1835` for the file with a stated reason.
8. **A pre-existing flaky test.** `RecordingTaskSchedulerTests.It_runs_each_task_on_its_own_non_pool_thread` failed once in a coverage run (it compares managed thread
   ids, which the runtime may reuse) and passed on three reruns; phase 3a does not touch it.

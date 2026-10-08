# Phase 3b — Bodies: Checklist

The execution-time checklist for sub-phase 3b of roadmap phase 3
([phase card](../../2026-09-27-dotnet-sdk-v1-roadmap-design.md)): one row per requirement ID, in the legend of the roadmap's
cross-cutting constraint 3. Written from what was built on branch `50-phase-3b-bodies` (stacked on `49-phase-3a-io`), following the
[design](2026-10-02-phase3b-bodies-design.md) and the [plan](2026-10-02-phase3b-bodies.md) (GitHub issue #50).

The scope is 48 rows: `BODY-1`–`BODY-37` and `HTTP-36`–`HTTP-38`, `HTTP-40`–`HTTP-45`, `HTTP-51`, `HTTP-52` (`HTTP-39` is 3a's row). Every test
below is `[Trait("Category", "Unit")]` and lives in `tests/Dexpace.Sdk.Core.Tests/` unless a project is named; phase 3b adds no `Security`
class. **Red evidence:** for a new type or a changed signature the red was the compile error (plan convention 1). The production
types of each step were written in the same sitting as their tests and the full suite was run green after each step; the
behavioural reds were not each captured separately. Pin proofs (temporary breaks, never committed) were run afterwards: making the request-body
`ClaimOnce` guard never trip failed 4 of 8 `SingleUseBodyTests`; making the buffered error body single-use (`BytesResponseBody` in place of the
replayable one) failed 2 of 4 `ErrorBodyPreviewTests`. The other pins (HTTP-37/BODY-7, SEAM-14) were not shown able to fail. A test marked "pin"
holds behaviour that was already correct.

| Mark | Meaning |
|---|---|
| ✅ | Implemented and tested. The row names the test, the `[Trait]` category and the file. |
| 🚫 | Not built: a permanent simplification, with the reason and the design §10 topic or §11 resolution it rests on. |
| ⏳ | Deferred or declined. The row names the plan task that will do it, or the `docs/first-release.md` entry that owns it. |
| N/A | Not applicable in this port. The row cites the design section that retires it. |

## Requirement rows

Test files: `Internal/DisposalTests.cs`; `Http/Request/{RequestBodyContractTests,SingleUseBodyTests,RequestBodyOwnershipTests,SeekableStreamBodyTests,FormUrlEncoderTests,FormBodyTests,FileRequestBodyTests,MultipartPartTests,MultipartFramingTests,BoundedWriteStreamTests,MultipartBodyTests,LoggingRequestBodyTests}.cs`;
`Http/Response/{ResponseBodyLifecycleTests,ResponseBodyDecodeTests,ResponseDisposeLatchTests,ErrorBodyPreviewTests,PrefixedReadStreamTests,LoggingResponseBodyTests}.cs`;
`Client/{SyncToAsyncBridgeTests,ClientDisposeIdempotenceTests}.cs`; `Architecture/LoggingWrapperSurfaceTests.cs`; vectors `tests/vectors/body/form-urlencoded.json`;
in `tests/Dexpace.Sdk.Http.SystemNet.Tests/`: `HttpResponseMessageBodyTests.cs`, `SystemNetHttpClientDisposeTests.cs`; the AOT smoke check is
`CheckPhase3bBodiesAsync` in `tests/Dexpace.Sdk.AotSmoke/SmokeChecks.cs`.

| ID | Level | Status | What was built | Proven by |
|---|---|---|---|---|
| `HTTP-36` | MUST | ✅ | Every 3b variant (form, file, multipart, seekable stream, both wrappers) reports a nullable `ContentType`, the exact `ContentLength` or −1, `IsReplayable`, and overrides both `WriteToAsync` and `WriteTo`. The sync twin itself is 3a's (P3a-5; see Cross-references). | `RequestBodyContractTests.Every_replayable_factory_reports_its_exact_length_and_writes_identical_bytes_twice` (`bytes`, `string`, `form`, `seekable-stream`, `file`, `multipart`, both forms), `The_logging_wrapper_over_each_replayable_factory_keeps_the_contract`; `FormBodyTests.The_sync_twin_writes_the_same_bytes` |
| `HTTP-37` | MUST | ✅ (pin) | The `Interlocked.Exchange` consume guard; base `ToReplayableAsync` drains once into a bytes body. | `SingleUseBodyTests.A_second_write_throws_StreamConsumedException`, `Concurrent_writes_admit_exactly_one`, `ToReplayableAsync_on_a_single_use_body_leaves_the_original_consumed` |
| `HTTP-38` | MUST | ✅ | Classification by source: bytes, string, value, form and file are replayable; multipart is the conjunction; a stream is single-use unless seekable with a known length. `FromForm` is the WHATWG serializer (`FormUrlEncoder`). | `FormUrlEncoderTests.Every_vector_case_encodes_to_its_wire_bytes_or_is_rejected` (17 vectors incl. design fact 6's line, as a theory over the file), `A_lone_surrogate_throws_ArgumentException_naming_the_field_index_never_the_value`, `A_null_name_or_value_throws_ArgumentNullException`, `Order_and_duplicates_are_kept`, `Unreserved_star_dash_dot_underscore_are_literal_and_every_other_byte_is_uppercase_hex`; `FormBodyTests` (all); AOT smoke `FromForm encodes with the WHATWG serializer` |
| `HTTP-40` | MUST | ✅, reading P3b-5 | `FileRequestBody`/`RequestBody.FromFile`: fail-fast validation (exists, not a directory, symlink resolved, range inside the size captured at construction), a fresh handle per write, `ContentLength` the exact count. A Unix FIFO or device uploads as an empty body (design §10 entry 31). | `FileRequestBodyTests` (26 cases), AOT smoke `FromFile writes its exact range in both forms` |
| `HTTP-41` | MUST | ✅, with §10 entry 6 | The idempotent close (the `ResponseBody` latch, P3b-2) and the readers' `finally` close (`BODY-16`); single use already held. The "same handle" clause keeps §10 entry 6 (3a's P3a-3). | `ResponseBodyLifecycleTests.Dispose_releases_once_across_Dispose_and_DisposeAsync_in_any_order`, `Concurrent_disposes_release_exactly_once`, `A_release_that_throws_still_flips_the_latch_and_propagates_once` |
| `HTTP-42` | MUST | ✅ | The charset fallback held since 2a; the BOM strip is new, in the one routine `TextDecoding.Decode` that both string readers call (3a's private `Decode` was deleted: the "3a landed" branch of plan task 1.4). A BOM that does not match the declared charset is kept. | `ResponseBodyDecodeTests` (23 cases, most in both the async and the sync form: UTF-8 with and without a charset, UTF-16 LE and BE, UTF-32, a mismatched BOM kept, only a leading BOM stripped, no charset and unknown charset as UTF-8 pins, empty and BOM-only bodies, `Decoding_is_the_one_routine_the_readers_share`); 3a's `ResponseBodyTests.ReadAsString_decodes_with_the_declared_charset_and_defaults_to_UTF8` and `TextRoundTripTests` pass unedited over it |
| `HTTP-43` | MUST | ✅ | The `Response` latch: `Dispose`/`DisposeAsync` share one `Interlocked` flag and forward to the (also latched) body. | `ResponseDisposeLatchTests` (6: double dispose in all four orders, a throwing body propagates once from either form, a never-read body is released, `Concurrent_response_disposes_release_once`, `EnsureSuccessAsync_…_then_a_caller_dispose_releases_once`); AOT smoke `a Response disposed three times releases its body once` |
| `HTTP-44` | MUST | ⏳ 7a | Hand-off: the lazy typed-response wrapper. | none in 3b |
| `HTTP-45` | MUST | ⏳ 7a | Hand-off, as `HTTP-44`. | none in 3b |
| `HTTP-51` | SHOULD (embedded MUST) | ✅ | `RequestBody.Multipart`/`MultipartPart`: one framing routine computed once at construction and used for the length and the write; a random spec-valid boundary or a caller boundary validated against RFC 2046; a part name that cannot break the framing; a per-part length guard (`BoundedWriteStream`). | `MultipartBodyTests` (22 cases), `MultipartPartTests` (13), `MultipartFramingTests` (22), `BoundedWriteStreamTests` (6); AOT smoke `Multipart frames its parts and reports the exact length` |
| `HTTP-52` | MUST | ✅ | Already met by phase 1 S8; 3a re-homed the drain onto `StreamCopy.DrainUpToAsync` (see Cross-references). 3b re-ran the `Security` tests over the new latch. The policy form is 4c's (`ErrorMappingPolicy`); `ErrorBodyBuffer` is 4b's (dated correction 2026-10-07, P4b-16). | `EnsureSuccessErrorMappingTests` (`Security`, all six; the one mechanical `TrackingBody` rewrite changes no assertion); 3a's `StreamCopyTests.DrainUpTo_stops_at_the_cap_and_reports_whether_the_end_was_seen` **Dated correction (2026-10-07, phase 4c):** the ⏳ 4c half is closed: `ErrorMappingPolicyTests` (the policy form, `BODY-30`'s empty-body clause, `BODY-31`'s untouched non-error status) and 4b's `ErrorBodyBufferTests`. |
| `BODY-1` | MUST | ✅ | As `HTTP-36`. | `RequestBodyContractTests` (above) |
| `BODY-2` | MUST | ✅ | Multipart `IsReplayable` is the conjunction over parts; `ContentLength` is −1 if any part's is. | `MultipartBodyTests.IsReplayable_is_the_conjunction_over_the_parts`, `ContentLength_is_minus_one_when_any_part_is_unknown`, `ContentLength_equals_the_bytes_actually_written`, `Declared_length_equals_bytes_written_for_a_spread_of_part_sets` |
| `BODY-3` | MUST | ✅ | Base `ToReplayableAsync` held; every new replayable variant returns `this`; the logging wrapper re-wraps (`BODY-21`). | `SingleUseBodyTests.ToReplayableAsync_on_a_replayable_body_returns_this`; `RequestBodyContractTests.Every_replayable_factory_…`; `FormBodyTests.ToReplayableAsync_returns_the_same_instance` |
| `BODY-4` | MUST | ⏳ 6a, 6b, 6c | Hand-off to the three gates' owners (retry, redirect's loud failure, auth replay). 3b's share is that every variant reports `IsReplayable` truthfully. | `RequestBodyContractTests` proves the property |
| `BODY-5` | MUST | ⏳ 6a | Hand-off: the retry-only idempotency gate. | none in 3b |
| `BODY-6` | MUST | ✅ (pin; composite built) | `FromStream` throws on a second write; a non-replayable multipart throws `StreamConsumedException` before writing a byte. The seekable variant is replayable, so the clause does not reach it. | `SingleUseBodyTests.A_second_write_throws_StreamConsumedException`; `MultipartBodyTests.A_single_use_composite_refuses_a_second_write_before_any_byte` |
| `BODY-7` | MUST | ✅ (pin) | The consume guard is `Interlocked`, proven under real concurrency. | `SingleUseBodyTests.Concurrent_writes_admit_exactly_one`, `Under_N_concurrent_ToReplayableAsync_callers_exactly_one_drains_…` (`Barrier`, N = 16) |
| `BODY-8` | MUST | ✅, with §10 entry 5 | A request body closes exactly what it opened: `FromStream` (single-use and seekable) never disposes the caller's stream; `FromFile` opens and disposes its own handle per write; multipart disposes nothing it was given. | `RequestBodyOwnershipTests` (stream, file, multipart halves); `SeekableStreamBodyTests.The_callers_stream_stays_open_after_write_failure_and_ToReplayableAsync`; `FileRequestBodyTests.The_handle_is_released_after_each_write_and_after_a_failed_write`; `MultipartBodyTests.Nothing_the_composite_was_given_is_disposed` |
| `BODY-9` | SHOULD | ✅ | The seekable variant: `FromStream` over a readable, seekable stream with a length in `[0, Array.MaxLength]` is replayable; every write seeks to the captured start under an in-flight latch; −1 or non-seekable stays single-use. Breaking 7. | `SeekableStreamBodyTests` (11 cases); AOT smoke `a seekable known-length stream is replayable` |
| `BODY-10` | MUST | ⏳ 3a | The exact-length copy is 3a's row `HTTP-39`; 3b consumes it in the file and seekable bodies and proves the consumption. | consumers: `FileRequestBodyTests.A_file_that_shrinks_after_construction_fails_naming_transferred_of_total`, `SeekableStreamBodyTests.A_short_seekable_source_fails`; 3a's `StreamCopyTests` and `ExactLengthBodyWireTests` |
| `BODY-11` | MUST | ✅, reading P3b-5 | As `HTTP-40`, plus count's rest-of-file sentinel (−1). | `FileRequestBodyTests.Count_minus_one_means_the_rest_of_the_file`, `The_exact_range_is_written`, `An_offset_or_count_past_the_size_is_rejected` |
| `BODY-12` | SHOULD | ✅ 🚫 | Recognisable by type: `FileRequestBody` is public sealed and exposes `FilePath`, `Offset`, `ContentLength`. 🚫 The kernel zero-copy path is not built (`SocketsHttpHandler` has none for request content, design §3.1); the transfer is a large-buffer copy from an unbuffered `FileStream` and is never claimed as zero-copy. | `FileRequestBodyTests.Is_recognisable_by_type_and_exposes_its_range` |
| `BODY-13` | MUST | ✅ | The file write goes through 3a's `StreamCopy.CopyExactly(Async)`, so a shrunk file fails with the shared "ended after X of Y bytes" message. | `FileRequestBodyTests.A_file_that_shrinks_after_construction_fails_naming_transferred_of_total` |
| `BODY-14` | MUST | ✅, with §10 entry 6 | 3a argued the verdict (P3a-3); 3b extends it to its new variants: a second open throws `StreamConsumedException` whose message names the buffering route (one shared constant, repeated verbatim by the transport body), and the over-cap wrapper stream is single-use. | `ResponseBodyLifecycleTests.A_second_open_throws_and_names_the_buffering_route`; `HttpResponseMessageBodyTests.A_second_open_throws_and_names_the_buffering_route`; `LoggingResponseBodyTests.Over_cap_a_second_read_throws_StreamConsumedException` |
| `BODY-15` | MUST | ✅ | The latch: close releases the transport resource whether or not the body was read, at most once, and a release that throws still flips the latch. | `ResponseBodyLifecycleTests.A_never_read_body_is_released_by_dispose`; `HttpResponseMessageBodyTests.The_message_is_disposed_once_across_Dispose_and_DisposeAsync`, `A_never_read_body_releases_the_message` |
| `BODY-16` | MUST | ✅ | `ReadAsBytes(Async)` and `ReadAsString(Async)` dispose the **body** in `finally`, not only the stream. | `ResponseBodyLifecycleTests.Readers_dispose_the_body_on_success_and_on_failure` (bytes and string, sync and async, with a stream that throws mid-read) |
| `BODY-17` | MUST | ✅ | `LoggingRequestBody` (internal) delegates one write into a `TeeStream`; the wire receives every byte. | `LoggingRequestBodyTests.The_wire_receives_every_byte_and_the_tap_mirrors_up_to_the_cap`, `The_sync_form_mirrors_and_forwards_too`, `The_destination_is_left_open` |
| `BODY-18` | MUST | ✅ | A fresh tee per write clears the tap. | `LoggingRequestBodyTests.The_tap_reflects_only_the_latest_attempt` |
| `BODY-19` | MUST | ✅ | The cap is a required constructor argument; 0 mirrors nothing and forwards everything. | `LoggingRequestBodyTests.A_multi_megabyte_write_mirrors_only_the_cap`, `A_cap_of_zero_mirrors_nothing_and_forwards_everything` |
| `BODY-20` | SHOULD | ✅ | Mirror-before-forward is 3a's tee contract; the snapshot after a primary failure contains the failing chunk. | `LoggingRequestBodyTests.A_primary_failure_leaves_the_failing_chunk_captured` |
| `BODY-21` | MUST | ✅ | `IsReplayable` is the delegate's; `ToReplayable(Async)` returns `this` for a replayable delegate, else a new wrapper with its own tap and the same cap. | `LoggingRequestBodyTests.IsReplayable_is_the_delegates_and_a_replayable_delegate_materialises_to_this`, `Materialising_rewraps_with_the_cap_and_a_separate_tap`, `The_sync_ToReplayable_rewraps_too` |
| `BODY-22` | MUST | ✅ | `LoggingResponseBody` (internal): the lazy, drain-once capture; concurrent first accessors serialise on a `SemaphoreSlim`; the drain token is linked from the starting accessor and the lifetime (P3b-10). The sync and async drains are twins over the same capture state (see Deviations). | `LoggingResponseBodyTests.The_first_read_triggers_exactly_one_upstream_drain_under_16_concurrent_readers`, `A_snapshot_triggers_the_drain`, `Nothing_is_read_until_the_first_access`, `Sync_OpenRead_drains_once_and_serves_the_same_bytes`, `A_sync_and_an_async_first_reader_race_to_one_drain`, `The_drain_is_cancelled_by_the_starting_accessors_token_and_the_failure_is_cached`, `Dispose_cancels_a_running_drain`, `The_semaphore_wait_honours_the_callers_token` |
| `BODY-23` | MUST | ✅ | Fits-cap: capture all, dispose the delegate quietly, serve each read as a fresh read-only `MemoryStream` view. | `LoggingResponseBodyTests.Fits_cap_every_read_is_an_independent_view` |
| `BODY-24` | MUST | ✅ | Over-cap: the delegate stays open; the next read is a single-use `PrefixedReadStream` (prefix, the staged overflow byte, the live tail); a second read throws. | `LoggingResponseBodyTests.Over_cap_the_consumer_receives_every_byte_once`, `Over_cap_a_second_read_throws_StreamConsumedException`; `PrefixedReadStreamTests.Reads_the_prefix_then_the_live_tail`, `Sync_and_async_reads_agree` |
| `BODY-25` | MUST | ✅, with §10 entry 4 | `Stream.Read` returning 0 is end of stream; the drain asks for at most one byte past the cap, so every request is positive, and the tail never issues a zero-count read. No declared-length cross-check (a `HEAD` response carries a length and no body; position E). | `LoggingResponseBodyTests.Never_issues_a_zero_count_read` (a probe stream that throws on a zero-count read; caps 0, 5, 1000); `PrefixedReadStreamTests.Never_issues_a_zero_count_read` |
| `BODY-26` | MUST | ✅ | Partial bytes kept; the failure cached as an `ExceptionDispatchInfo`; every read rethrows the same instance; the snapshot returns the partial bytes without throwing; `DrainFailure` reports it without starting a drain. | `LoggingResponseBodyTests.A_drain_failure_is_cached_and_the_partial_bytes_kept`, `A_failure_to_open_the_delegate_is_cached_too`, `The_failure_query_never_starts_a_drain` |
| `BODY-27` | MUST | ✅ | One `Interlocked` close-once guard shared by the wrapper's dispose and the tail stream's dispose; a throwing delegate dispose still flips it and propagates once. | `LoggingResponseBodyTests.The_delegate_is_closed_at_most_once_across_every_path`, `A_throwing_delegate_dispose_still_flips_the_guard_and_propagates_once`, `The_wrapper_dispose_after_a_tail_read_to_the_end_does_not_release_twice`; `PrefixedReadStreamTests.Dispose_routes_to_the_shared_close_once_guard` |
| `BODY-28` | MUST | ✅ | On the fits-cap path a delegate-dispose failure goes through `Disposal.DisposeQuietly` (no primary), never into the drain error; the captured array outlives the wrapper's dispose. | `LoggingResponseBodyTests.A_close_failure_after_full_capture_is_reported_not_raised` (an `exception` event tagged `dexpace.dispose.suppressed`), `Snapshot_survives_dispose`, `Dispose_before_any_read_leaves_the_snapshot_empty_and_starts_no_drain`, `Dispose_then_read_in_the_over_cap_regime_throws_StreamClosedException`; `DisposalTests` (9) |
| `BODY-29` | SHOULD | ✅ | `ContentLength` is the delegate's until a complete capture, then the captured length. | `LoggingResponseBodyTests.Content_length_is_the_captured_size_only_after_full_capture`, `IsFullyCaptured_reflects_the_regime` |
| `BODY-30` | MUST | ✅ | Already met by phase 1 S8, as `HTTP-52`; "no body returns unchanged" is the pipeline step's; `ErrorBodyBuffer` is 4b's (dated correction 2026-10-07, P4b-16). | `EnsureSuccessErrorMappingTests` (`Security`) **Dated correction (2026-10-07, phase 4c):** the ⏳ 4c half is closed: `ErrorMappingPolicyTests` (the policy form, `BODY-30`'s empty-body clause, `BODY-31`'s untouched non-error status) and 4b's `ErrorBodyBufferTests`. |
| `BODY-31` | MUST | ✅ | Already met by phase 1 S8: only 400–599 map. | `EnsureSuccessErrorMappingTests.A_non_error_status_is_returned_with_its_body_intact` (`Security`) **Dated correction (2026-10-07, phase 4c):** the ⏳ 4c half is closed: `ErrorMappingPolicyTests` (the policy form, `BODY-30`'s empty-body clause, `BODY-31`'s untouched non-error status) and 4b's `ErrorBodyBufferTests`. |
| `BODY-32` | MUST | ✅ (capless clause vacuous) | On both wrappers a negative cap (constructor or `Snapshot`) throws `ArgumentOutOfRangeException`; a cap above `Array.MaxLength` is clamped silently; a snapshot returns what exists up to the cap. The capless snapshot's "fail above `Array.MaxLength`" clause is **vacuous** (the capture is bounded by a cap that is itself clamped), reported as vacuous, never as passing. | `LoggingRequestBodyTests.Negative_caps_are_rejected`, `Caps_above_the_array_bound_are_clamped`, `Snapshot_with_a_smaller_max_returns_a_prefix`; `LoggingResponseBodyTests.Negative_caps_are_rejected`, `Caps_above_the_array_bound_are_clamped`, `Snapshot_returns_what_exists_up_to_the_cap`, `A_snapshot_of_an_over_cap_body_returns_exactly_the_cap` |
| `BODY-33` | SHOULD | ✅ | Met by construction (resolving §12's "unaddressed"): the exception's error body is the replayable bytes body of S8, so every read is a fresh, non-consuming view; "null when there is no body" reads as an empty body (§10 entry 30). Pins. | `ErrorBodyPreviewTests` (4): `Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works`, `The_error_body_is_still_readable_after_the_response_is_disposed`, `An_empty_error_body_reads_as_an_empty_body_never_null`, `Disposing_the_exception_response_twice_is_harmless` |
| `BODY-34` | MUST | ✅, ⏳ 5b | The consumer-receives-every-byte clause is built and proven; engagement "only when body-level logging is enabled" and the one shared preview-size setting are 5b's (`OBS-34`, `OBS-36`–`OBS-38`). | `LoggingRequestBodyTests.For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded` (10 pairs); `LoggingResponseBodyTests.For_cap_and_body_pairs_the_consumer_receives_every_byte_and_the_capture_stays_bounded` (11 pairs, incl. 10 over 200,000) |
| `BODY-35` | MUST | ✅ | Every new variant reports the exact length or −1; `FromStream` rejects `contentLength < -1` and a non-readable source (already added by 3a, so not re-tested here). | `RequestBodyContractTests` (the factory theory); 3a's `RequestBodyStreamTests.FromStream_rejects_a_content_length_below_minus_one`, `FromStream_rejects_a_non_readable_source` |
| `BODY-36` | MAY | ⏳ `docs/first-release.md` | Declined for v1: no memory-mapped view; offered later only if a signing use case asks. | bullet in `docs/first-release.md`, "SHOULD- and MAY-level requirements declined for v1" |
| `BODY-37` | MUST | ✅ (tee half ⏳ 3a `IO-28`) | The wrappers expose no tap or capture handle, only copying snapshots; the tee's own refusal is 3a's `IO-28`. | `LoggingRequestBodyTests.Snapshots_are_copies`; `LoggingResponseBodyTests.Snapshots_are_copies`; `LoggingWrapperSurfaceTests` (reflection over both wrappers: no non-private member returns `Stream`, `Memory<byte>`, `Span<byte>` or `byte[]` other than the snapshots and the two opens the base type requires; the snapshots return `byte[]` copies) |

### Inherited row, closed here (not one of the 48)

`SEAM-14` (2b checklist, ⏳ 3b): `SystemNetHttpClient.Dispose` is now latched, so the owned `HttpClient` is released at most once across
`Dispose`/`DisposeAsync`; `DelegateHttpClient`'s and both bridges' disposes hold nothing and are idempotent. Proven by
`SystemNetHttpClientDisposeTests.An_owned_client_disposes_its_HttpClient_once_across_Dispose_and_DisposeAsync` (through the internal
`OwnedClientReleases` count, since `HttpClient`'s own `Dispose` hides the latch), `A_borrowed_client_is_never_disposed_by_either_form`,
`Concurrent_disposes_are_safe`, and `ClientDisposeIdempotenceTests.Bridge_and_delegate_disposes_are_idempotent` (a pin). The
behavioural `ObjectDisposedException` and no-throw assertions are pins that held before the change. `SEAM-15` stays 8b's.

## Cross-references to 3a's rows

- **`HTTP-36` (the sync twin).** Built by 3a (P3a-5): `RequestBody.WriteTo`/`ToReplayable` and `ResponseBody.OpenRead`/`ReadAsBytes`/`ReadAsString`, virtual
  with a `NotSupportedException` default. Tests: 3a's `RequestBodyContractTests` (the un-overridden `WriteTo` default, sync/async byte parity, the shared
  consume latch), which the 3b factory theory extends to every new variant.
- **`HTTP-52` (the drain re-home).** 3a's `Response.EnsureSuccessAsync` drains through `StreamCopy.DrainUpToAsync`; `EnsureSuccessErrorMappingTests` passed
  unedited over it and, in 3b, over the new latch (below).
- **`BODY-14`.** §10 entry 6's dated correction is 3a's alone (P3a-3); 3b writes none.

## Exit criteria

- [x] All 48 rows carry a mark, **recounted against the built code**: 36 ✅ outright (`HTTP-36`, `HTTP-37`, `HTTP-38`, `HTTP-40`, `HTTP-41`, `HTTP-42`, `HTTP-43`, `HTTP-51`;
  `BODY-1`–`BODY-3`, `BODY-6`–`BODY-9`, `BODY-11`, `BODY-13`–`BODY-29`, `BODY-32`, `BODY-33`, `BODY-35`); six ✅ with a clause another owner holds (`HTTP-52`,
  `BODY-30`, `BODY-31`, `BODY-34` with ⏳; `BODY-12` with 🚫; `BODY-37` with the tee half ⏳ 3a); six ⏳ wholly (`BODY-10` to 3a; `HTTP-44`, `HTTP-45` to 7a; `BODY-4`
  to 6a/6b/6c; `BODY-5` to 6a; `BODY-36` to `docs/first-release.md`). `BODY-32`'s capless clause is named vacuous. These equal the design's totals.
- [x] No `Security` class was edited beyond the mechanical `TrackingBody` rewrite (below); `git diff --stat ffd6d7d -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security`
  shows `EnsureSuccessErrorMappingTests.cs | 4 ++--` and nothing else.
- [x] `PublicAPI.Unshipped.txt` changed: the two public dispose members lost `virtual`, the two protected hooks were added, and `FromForm`, `FromFile`, `Multipart`,
  `FileRequestBody` (its five members) and `MultipartPart` (its four members) were added; `PublicAPI.Shipped.txt` is still empty.
- [x] `CHANGELOG.md` `[Unreleased]` carries a **Breaking:** line for design items 1 to 7 (the first six in one block, 7 beside the stream change) and an `### Added` line per new body.
- [x] User page `docs/sdk-documentation/bodies.md` is written ("As built by phase 3b … written against source on 2026-10-03").
- [x] Dated corrections to design §3.1, §3.3, §3.7, §4.5, §10 (entry 5, and the new entry 31), §11 (items 34 and 43) and §12; the 2b checklist's `SEAM-14` row and phase 1's S9 row;
  `docs/first-release.md`; roadmap Phase Status Note appended; `CLAUDE.md`, `docs/README.md`, `docs/architecture.md` and `src/Dexpace.Sdk.Core/README.md` updated.

## Existing assertions changed

### `Security` classes (constraint 5)

| File | Diff |
|---|---|
| `Http.SystemNet.Tests/Security/*` (all wire classes) | **unedited** |
| `Core.Tests/Security/EnsureSuccessErrorMappingTests.cs` | **mechanical**: the private `TrackingBody` fake's `public override void Dispose()` became `protected override void Dispose(bool disposing)` (the dispose pattern changed, P3b-2). No assertion changes value; all six methods pass. |
| every other `Core.Tests/Security/*` class | **unedited** |

Evidence: `git diff --stat ffd6d7d -- tests/Dexpace.Sdk.Http.SystemNet.Tests/Security tests/Dexpace.Sdk.Core.Tests/Security` lists the one file above (2 insertions, 2 deletions).

### Other tests

| File | Change |
|---|---|
| `Core.Tests/Support/DisposalCountingBodyTests.cs` | `Dispose_and_DisposeAsync_each_increment_the_count` is `Dispose_and_DisposeAsync_release_once`: the count after `Dispose()` then `DisposeAsync()` is **1**, not 2 (the latch). `TestSupport/Transports/DisposalCountingBody.cs` overrides `Dispose(bool)` and its docs now say it counts releases. |
| `Core.Tests/Http/Response/ResponseBodyTests.cs` | Two assertions `Assert.Equal(1, counting.DisposeCount)` became `Assert.True(counting.DisposeCount >= 1)` (`ReadAsBytes_over_a_declared_length_above_the_cap_…`, `ReadAsBytesAsync_disposes_the_stream_it_opened_on_failure`): the reader now disposes the stream and then the body, whose release reaches the same source, so the source is disposed twice; BCL streams tolerate it. |
| `Core.Tests/Client/SyncToAsyncBridgeTests.cs` | `A_throwing_dispose_after_cancellation_faults_the_task_with_that_exception` is replaced by `A_throwing_dispose_after_cancellation_cancels_the_task_and_reports_the_failure` (P3b-16): the task is `Canceled` and the scope activity carries one `exception` event tagged `dexpace.dispose.suppressed`. `ThrowingDisposeBody` overrides `Dispose(bool)`. |
| `Core.Tests/Http/Response/ResponseTests.cs`, `Pagination/PageableTests.cs`, `Pipeline/Policies/RedirectPolicyTests.cs` | The `TrackingBody` fakes' `Dispose()`/`DisposeAsync()` overrides became one `Dispose(bool)` override; every assertion unchanged. |
| `Core.Tests/Architecture/ModelImmutabilityArchitectureTests.cs` | The allow-list gained `Response._disposed` (HTTP-43, P3b-2): the idempotent-close latch, a reviewed decision. |
| `Core.Tests/Architecture/ModelConstructionArchitectureTests.cs` | The construction allow-list gained `MultipartPart(String, RequestBody, String)` (P3b-8): a sealed class with a validating constructor, the `Request` precedent. |
| `Core.Tests/Http/Request/RequestBodyContractTests.cs` | Additive only: the factory theory and the wrapper theory. |
| `AotSmoke/SmokeChecks.cs` | One new check, `CheckPhase3bBodiesAsync`, and a local `ReleaseCountingBody`. |

`BodyConvenienceTests` (serde) needed no adjustment: `GetErrorAsync` reads the replayable error body, which keeps serving after dispose.

## Deviation ledger, as built

| ID | As built | Route |
|---|---|---|
| P3b-1 | §10 entry 5 stands, extended to the 3b variants; entry 6 is 3a's. | §10 entry 5, dated correction |
| P3b-3 | `Disposal` is internal, takes an optional `ILogger?`, reports through `Activity.Current` and a `Warning`; silent with neither (accepted interim gap, closes in 5b). | §3.7, dated correction |
| P3b-4 | A seekable known-length `FromStream` is replayable; every write seeks to the captured start; the length is never inferred. | §3.1, dated correction |
| P3b-5 | "Regular file" is enforced only where the platform can see it; a Unix FIFO or device uploads as an empty body. | §10 entry 31 (new) |
| P3b-10 | The response drain's token is linked from the starting accessor and the lifetime. | §3.1, dated correction |
| P3b-12 | The BOM strip covers every preamble-bearing encoding. | §11 item 34, dated correction |
| P3b-7, P3b-8 | WHATWG form serializer; multipart rejects controls instead of stripping them (Node). | `bodies.md` and the vector file; no §10 entry |
| P3b-16 | `AsAsync` disposes quietly; "until phase 3b" is removed. | §3.3 and §11 item 43, dated correction |

## Deviations from the plan, and findings while building

1. **Commit grouping.** The plan's PR 1 asked for four commits after `Disposal`; the dispose pattern, the `Response` latch, the readers, the BOM, the `AsAsync`
   change and the error-body pins were interdependent in the working tree (`SyncToAsyncBridgeTests` already overrides `Dispose(bool)`), so they landed as the one
   `feat!:` commit. Every commit on the branch builds and passes.
2. **`DisposalTrackingStream` was not created.** 3a's `DisposeCountingStream` (`TestSupport/IO`, namespace `…TestSupport.Streams`) already counts `Dispose(bool)` and
   `DisposeAsync`, so the tests reuse it.
3. **The response drain is not one `bool async` core.** Core bans `ValueTaskAwaiter.GetResult` (`BannedSymbols.txt`), so a sync entry cannot block on an async core. The
   sync and async drains are two short twins (`Drain`, `DrainAsync`; `EnsureDrained`, `EnsureDrainedAsync`) over the same capture state (`NextReadSize`, `Absorb`,
   `MarkFits`, `Serve`, the close guard), each under the 70-line cap. Plan reading A10 and §11 item 12's pattern are met in spirit, not in letter.
4. **A per-part length guard, not a composite one.** `BoundedWriteStream` wraps each part's destination with that part's own declared length, so a lying part is caught
   too long (refused before the chunk is written, an `IOException`) or too short (`EndOfStreamException` naming delivered-of-total) while the framing bytes, which are
   stored, cannot drift. The plan's composite-total check is subsumed.
5. **`MediaType.ToString()` has no space after `;`.** The rendered types are `multipart/form-data;boundary=…` and `text/plain;charset=utf-8`; the tests assert that form.
6. **The over-cap drain never buffers a chunk past the cap.** It asks for at most `cap − captured + 1` bytes, so the overflow is one staged byte, not Node's whole
   pending chunk; every read count is positive.
7. **`SystemNetHttpClient.OwnedClientReleases`** is a new `internal` counter (the plan allowed "an internal release-counting seam"); `HttpClient`'s idempotent `Dispose`
   would otherwise hide the SDK latch.
8. **`FromStream` validation was already 3a's.** `contentLength < -1` and a non-readable source were added by 3a (`RequestBodyStreamTests`); 3b adds no duplicate.
9. **The lone-surrogate vector** is written as the marker `<U+D800>` in the shared JSON file, which the test substitutes, because JSON cannot carry a lone surrogate portably.
10. **The `LoggingResponseBody` semaphore and lifetime source.** The `SemaphoreSlim` is never disposed (it holds no unmanaged resource until `AvailableWaitHandle` is read, and
    disposing it under a running drain would make the drain's `Release` throw); the lifetime `CancellationTokenSource` is cancelled and disposed once, guarded so
    `DisposeAsyncCore` and `Dispose(true)` do not both run it.
11. **Knowledge corpus.** Nothing found contradicts a harvested entry, so no note was added under `docs/knowledge/notes/`.

## Correction 2026-10-08 (phase 6a)

`BODY-5` and the retry third of `BODY-4` are no longer ⏳: `RetryFacts.IsResendable` is the retry-only gate, and `RetryResendGateTests.IsResendable_matrix` covers every
`RequestBody` variant (see the [carried-rows table of the 6a checklist](../../phase6/phase6a/2026-10-08-phase6a-retry-checklist.md#carried-rows)). The redirect and auth
thirds of `BODY-4` stay ⏳ 6b and 6c. The rows above stand as written at 3b's exit.

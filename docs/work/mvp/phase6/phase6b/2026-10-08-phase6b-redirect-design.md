# Phase 6b — Redirect: Design

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Brainstormed without a human in the loop. Every judgement call the brainstorming skill
would have put to the lead is taken here as a numbered ruling (`P6b-n`), with the options and the rationale. The calls
the lead may still reverse are marked **open for the lead**. 6a (retry) and 6c (auth) are being designed in parallel by
other authors in this working tree. The interfaces this design assumes of them are stated in
[Cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-6a-and-6c) and in P6b-29. The scope authority is the
roadmap's Phase 6 card and Phase List row 6 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). The format
follows the phase 5 designs (`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration-design.md` and its 5b and 5c
siblings).

**What this document is.** The sub-phase design for 6b. It holds:

- one disposition per requirement row (28 rows);
- the rewritten `RedirectPolicy` and the pure decision function behind it;
- the public surface 6b adds, changes or removes, and the internal types behind it;
- the breaking changes;
- the end-to-end credential-leak test that 6b owns as phase 6's convergence point;
- the replacement of `RedirectPolicyTests`;
- the `Security` classes that must stay green, and the one that is edited and why;
- the pull-request segmentation;
- the rulings, which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §6.2's argument that the
transport must not follow redirects (P13), nor §10 entries 14 (`async-redirect-pillar`) and 15 (no cross-origin marker), nor
§11 items 16, 29 and 31 (roadmap constraint 9). It cites them and records a decision against each row. It does not design
6a's retry engine, 6c's auth resolver or challenge handling, or the transport's borrowed-client detection (8b, phase 1 S3).
It edits no design chapter, roadmap cell or `CLAUDE.md` line. The corrections it owes are listed as proposals in
[Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor or sibling | Kind | State at `3a1db00` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. Everything below is written under them. The rewrite removes `RedirectPolicy`'s `MA0051` waiver (P6b-27). |
| Phase 1, S3 (`RedirectCredentialHygieneTests`, `RedirectWireTests`) and every other `Security` class | **dependency** (constraint 5) | Met. The roadmap's phase-1 table gives 6b "the redirect rewrite (the `REDIR-15` error, `REDIR-16`, `REDIR-17`, removing `StripSensitiveHeadersOnCrossOrigin`)" with those two classes to keep green. [Keeping the `Security` classes green](#keeping-the-security-classes-green) names the one class edited, and argues that the edit is not a weakening. |
| 2a (`Headers.Names`, `Headers.Without`, `Request`'s validated constructor, `HttpHeaderName.WellKnown.Location`) | **dependency** | Met. `Request` rejects a body on GET/HEAD (HTTP-7), so the 303 rebuild passes a null body (fact 14). |
| 3b (seekable `RequestBody.FromStream` is replayable; the dispose latches) | **dependency** | Met. 3b's hand-off "6a/6b/6c own `BODY-4`'s three gates and `BODY-5`, and should test the now-replayable seekable streams" is taken for the redirect gate (P6b-15). |
| 4b (`Disposal.DisposeQuietly(resource, primary, logger)`, `ExceptionTrail`, `ExceptionFacts.IsFatal`) | **dependency** | Met. A dispose failure on a failing hop rides the primary exception's suppressed trail (P6b-17). |
| 4c (`PipelineContext.SeedRequest`, `ForHop`, `HopNumber`, `CallState.Logger`, `SyncPath`, `ProcessCoreAsync(…, async)`, `PipelineStage.Redirect = 200` outside `Retry` outside `Auth`) | **dependency** | Met. 4c's hand-off "6b rewrites `RedirectPolicy` over `context.SeedRequest` and removes `StripSensitiveHeadersOnCrossOrigin`" is taken. Coupling obligation 2 (the seed origin) is read, not redefined. |
| 5a (`RedirectOptions` is a sealed record with `init` accessors; the options-record rule A.1 to A.5; `ProxyOptions`' content-equality precedent) | **dependency** | Met. 5a's hand-off "6b removes `StripSensitiveHeadersOnCrossOrigin` and rules on `MaxRedirects`' validation" is taken (P6b-3). |
| 5b (`DexpaceLogEvents` ids 150–159 reserved for redirect, `LoggerMessage.Define` with dotted keys, the `OBS-20` guard pattern, `RedactionCache`, `UrlRedactor.Redact`/`RedactHeaderValue`) | **dependency** | Met. 5b's hand-off "6a/6b/6c emit through `CallState.Logger` and the guard pattern in `HttpLogEmitter`; ids 140–169 are reserved" is taken (P6b-19). |
| 5c (`OperationTelemetry.RedirectHop(context, hop, status, target, crossOrigin)`, the `dexpace.redirect.hop` span event shape, "6a and 6b must not add events of their own name without a dated correction") | **dependency** | Met. The roadmap's phase-6 entry ("5c's per-attempt event shape is fixed") holds. 6b calls the emitter and adds no span event (P6b-19). |
| A phase 6 segmentation design | **convenience**, with a stated substitute | Absent. The card names each sub-phase's build list, and the census below plus P6b-1 and P6b-2 stand in for the row split, as P5a-2 did for phase 5. **Open for the lead** (P6b-2). |
| **6a** (retry) | **convenience**, both ways | Designed in parallel. Retry runs inside redirect. 6b reads nothing 6a builds except through the end-to-end test, which drives whichever `RetryPolicy` is in the tree when that PR lands (P6b-25, P6b-29). |
| **6c** (auth) | **convenience**, both ways | Designed in parallel. Auth runs inside both loops. 6c's stamping reads the same seed origin. 6b offers its internal `HttpOrigin` helper but does not require it. The end-to-end test drives whichever `AuthorizationPolicy` subclasses are in the tree (P6b-12, P6b-25, P6b-29). |
| **Phase 8b enters on 6b** (roadmap coupling obligation 4: "`TRANSPORT` needs … `REDIR` (6b) … fixed") | 6b is a **dependency of 8b** | 8b's `TRANSPORT-1` and borrowed-client detection rest on `RedirectPolicy` being the single redirect authority, which this design completes. |

**No dependency edge inverts the roadmap's order.** 6b reads nothing that 6a or 6c builds. The phase 6 boundaries stay
conveniences, and no dated status note is owed for an inverted edge.

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/10-redirect-handling.md` §10.1–§10.4 and appendix C rows `REDIR-1`–`REDIR-28`. Appendix C
  carries text the chapter abbreviates:
  - `REDIR-3`'s "If the method is not allowed, the 3xx response is returned verbatim";
  - `REDIR-5`'s "(follow303=true)" and "The original request method is irrelevant";
  - `REDIR-21`'s porter NOTE;
  - `REDIR-28`'s "a redaction error is swallowed to a placeholder".

  Rows cited because a 6b type touches them:
  - `BODY-4` and `BODY-5` (the replay gate);
  - `PIPE-15`, `PIPE-16` and `PIPE-40` (re-drive and lifecycle);
  - `PIPE-2` and `AUTH-27` (stage order);
  - `AUTH-28`, `AUTH-29` and `XCUT-16` (the credential-free downgrade hop);
  - `XCUT-17` and `XCUT-19` (hygiene and redaction);
  - `HTTP-46` and `HTTP-47` (URL equality and construction);
  - `OBS-20` and `OBS-39` (the guard and stable names);
  - `TRANSPORT-1` (the single authority);
  - `RETRY-44` (per-attempt isolation).
- **Design.** §6.2 (the whole section), §5.1 (the stages; `PerHop` runs once per hop), §5.3 (`PIPE-32` reversed), §3.4's
  replay gate (`BODY-4`'s three decline behaviours), §8.1 (log events, redaction), §10 entries 14 and 15, §11 items 16, 29
  and 31, §12's `REDIR` row (26 of 28 cited; `REDIR-27` deferred; `REDIR-11`'s marker retired).
- **Styleguide.** `csharp/06-types-and-data-modeling.md` 6.1, 6.2, 6.5 and 6.8 (records, sealed, `init`, explicit enum
  values); `csharp/08-error-handling.md` (exception families, the standard constructors); `csharp/10-api-design.md` 10.1
  (internal by default) and 10.2 (immutable records); `csharp-aspnetcore/06-logging-and-observability.md` 6.2 as
  overridden by the `notes/observability.md` conflict note (`LoggerMessage.Define` with dotted keys, P5b-3). The overlay's
  `I`-prefix and `Async`-suffix departures stand.
- **Siblings.**
  - `nodejs-sdk@54aeed4` `packages/core/src/redirect/{codes,cross-origin,decide,errors,redirect-step,settings}{,.test}.ts`,
    with `decide.test.ts` (965 lines) as the matrix the card names.
  - `ruby-sdk@5b17395` is now local in full, which no earlier .NET phase found:
    - `gems/dexpace-core/lib/dexpace/redirect/*.rb`;
    - `gems/dexpace-core/test/dexpace/redirect/{step,condition_snapshot,events,scheme_downgrade_error}_test.rb`;
    - `gems/dexpace-core/test/support/redirect_fixtures.rb`;
    - `docs/work/mvp/phase6/phase6b/2026-09-09-phase6b-redirect-design.md`, read for its rulings R7–R9 and its as-built
      ledger P6-91 to P6-100.

    `matrix_facts_test.rb` is skipped, as the card says: it asserts Ruby host facts.

**The knowledge queries could not be run on this host.** `scripts/knowledge` is a .NET program, and the host has only the
`dotnet` muxer with no SDK. The CLI reports "A compatible .NET SDK was not found" (2026-10-08), as it did for phase 5. The
step-1 reading was done on the corpus files directly, which are what the CLI prints from:

| Query (intended) | Done instead | Result |
|---|---|---|
| `--origin note --brief` | read `docs/knowledge/notes/*.md` | Seven files. Two bind 6b. `observability.md`: log events use `LoggerMessage.Define` with dotted keys, not the source generator. `retry-and-resilience.md`: `Outcome` is an abstract class, which 6b does not touch. `naming-conventions.md`'s `Async` suffix binds nothing new. |
| `--section conflicts --brief` | read every harvested file's `## Conflicts` | None open for redirect (`redirect-handling.md`'s section is empty). |
| `--prefix-info REDIR` | appendix C rows 308–335 and `harvested/redirect-handling.md` | 28 IDs: 23 MUST, 4 SHOULD (`REDIR-10`, `REDIR-21`, `REDIR-23`, `REDIR-28`), 1 MAY (`REDIR-27`). The harvested Constraints and Conclusions restate design §6.2 and agree with it. |
| `--gaps REDIR` | the roadmap's gap table | No `REDIR` ID is a gap ID. Every in-scope ID was nevertheless read from appendix C. |

The plan re-runs the four queries on a host with the pinned SDK and records any difference. A difference that contradicts a
ruling below reopens that ruling.

---

## Scope and the 28-row census

**6b owns 28 rows: `REDIR-1`–`REDIR-28`** (23 MUST, 4 SHOULD, 1 MAY), the card's list exactly. The card's "REDIR (23 MUSTs):
8 met, 6 partial, 8 missing" counts MUSTs only. The one MUST it does not classify is `REDIR-25`, which is 🚫 under §10
entry 14.

Planned status, one line per ID. ✅ means built and tested in 6b; ⏳ and 🚫 follow the legend in the roadmap's constraint 3.

| ID | Level | Planned | One line |
|---|---|---|---|
| `REDIR-1` | MUST | ✅ | Only 301, 302, 303, 307 and 308 are recognised. Any other status returns from the loop's first test, before the snapshot or the predicate (P6b-6). |
| `REDIR-2` | MUST | ✅ | 300, 304 and 305 fall outside the recognised set, so a `Location` on them is never read. Matrix rows carry a `Location` on each. |
| `REDIR-3` | MUST | ✅ | 301 and 302 are eligible only when the **original** method is in `AllowedMethods` (default `{GET, HEAD}`). Method and body are preserved, with no POST→GET rewrite (P6b-4). |
| `REDIR-4` | MUST | ✅ | 307 and 308 follow the same eligibility rule and preserve method and body. |
| `REDIR-5` | MUST | ✅ | 303 is not followed unless `FollowSeeOther` is set. When followed it becomes a GET, whatever the original method, with no body and every `Content-*` header removed by case-insensitive prefix (P6b-5). |
| `REDIR-6` | MUST | ✅ | A followed method-preserving hop over a present, non-replayable body throws `RedirectBodyNotReplayableException`, whose message names replayability. 303 is exempt (P6b-15, P6b-16). |
| `REDIR-7` | MUST | ✅ | `Authorization` is removed (every value) before every re-issue: same-origin, cross-origin and the 303 rebuild (P6b-13). |
| `REDIR-8` | MUST | ✅ | Cross-origin means the internal `HttpOrigin` of the target (lower-case scheme, case-insensitive IDN host, effective port) differs from `context.SeedRequest`'s origin, never the previous hop's (P6b-12). |
| `REDIR-9` | MUST | ✅ | `Cookie` and `Proxy-Authorization` are also removed on a cross-origin hop, the 303 rebuild included. |
| `REDIR-10` | SHOULD | ✅ | `Cookie` (and `Proxy-Authorization`) survive a same-origin hop. The conservative MAY ("strip all cookies") is not taken (P6b-13). |
| `REDIR-11` | MUST | ✅ | No signal at all: §10 entry 15. Redirect and auth both compare against the immutable seed origin. Clauses (a) to (c) hold *a fortiori*: nothing is added to the request, so nothing can be forged or needs stripping (P6b-23). |
| `REDIR-12` | MUST | ✅ | Userinfo in the target is dropped with `GetComponents(AbsoluteUri & ~UserInfo, UriEscaped)` before re-issue, and before the visited check (P6b-10). |
| `REDIR-13` | MUST | ✅ | Resolution and stripping preserve `%2F`, `%26`, fragment escapes, bracketed IPv6 and explicit non-default ports. An explicit *default* port is elided by `Uri`, with the origin and wire meaning unchanged (P6b-30). |
| `REDIR-14` | MUST | ✅ | A relative `Location` resolves against the **current** hop's URL (`Uri.TryCreate(current, raw, out target)`, RFC 3986). An absolute one is used as-is after userinfo stripping. |
| `REDIR-15` | MUST | ✅ | An https→http transition on a single hop throws `RedirectSchemeDowngradeException` unless `AllowHttpsToHttpDowngrade`. The opt-in emits a `Warning` event. Stripping applies either way (P6b-14). |
| `REDIR-16` | MUST | ✅ | Visited set: the userinfo-free `AbsoluteUri`, compared ordinally, seeded with the first request the policy drives. A revisit returns the current 3xx open, with a `Warning` event (P6b-11). |
| `REDIR-17` | MUST | ✅ | `MaxRedirects` defaults to 3. Reaching the cap returns the last response as-is, even a 3xx, without throwing. `0` disables following through the same gate. A negative value throws at `init` (P6b-3). |
| `REDIR-18` | MUST | ✅ | A `Location` that will not resolve, names a scheme other than http(s), has an empty host, or appears more than once returns the 3xx unfollowed with a `Warning` event. The policy never throws on the wire value (P6b-10). |
| `REDIR-19` | MUST | ✅ | A missing, empty or all-whitespace `Location` returns the 3xx unfollowed. |
| `REDIR-20` | MUST | ✅ | `RedirectOptions.Predicate` (`Func<RedirectCondition, bool>?`) replaces the built-in eligibility. It receives a fresh `RedirectCondition` per decision, whose visited list is a copy (P6b-7, P6b-8). |
| `REDIR-21` | SHOULD | ✅ | A non-recognised status allocates no snapshot and never calls the predicate. A recognised 3xx always does, even with no usable `Location` and even at the hop cap (P6b-6). |
| `REDIR-22` | MUST | ✅ | (a) The superseded response is disposed **before** the next drive. (b) A throw while deciding or building, a throwing predicate included, disposes the current response first. (c) Every "return current" outcome returns it undisposed (P6b-17). |
| `REDIR-23` | SHOULD | ✅ | One `while` loop with no recursion. A 1 000-hop scripted chain under `MaxRedirects = 1000` completes. |
| `REDIR-24` | MUST | ✅ | `PipelineStage.Redirect` (200) is outside `Retry` (300) and `Auth` (400), and `StageOrderTests` already pins this. 6b adds a behavioural test: an auth probe runs once per hop. |
| `REDIR-25` | MUST | 🚫 | The async standard pipeline follows redirects: §10 entry 14 (`async-redirect-pillar`), §11 item 29. The documentation clause is ✅ by that entry, as 4c recorded for `PIPE-32`. A caller who wants the 3xx verbatim sets `MaxRedirects = 0` (P6b-22). |
| `REDIR-26` | MUST | ✅ | `AllowedMethods` is copied into a `FrozenSet<Method>` in its `init` accessor. Mutating the caller's set afterwards changes nothing (P6b-3). |
| `REDIR-27` | MAY | ⏳ | Declined for v1: the target header stays `Location`, as design §12 lists it ("Deferred"), the same disposition Ruby took (P6b-21). |
| `REDIR-28` | SHOULD | ✅ | Five structured log events (ids 150–154) with URLs through the call's `UrlRedactor`, which is total. The malformed-`Location` event logs the value through `RedactHeaderValue`, which is stricter than the spec's raw carve-out. Each followed hop also emits 5c's `dexpace.redirect.hop` span event (P6b-19, P6b-20). |

**Totals: 28 rows. 26 ✅, 1 🚫 (`REDIR-25`), 1 ⏳ (`REDIR-27`), 0 N/A.** By level:

- 23 MUST: 22 ✅ and `REDIR-25` 🚫.
- 4 SHOULD: all ✅.
- 1 MAY: `REDIR-27` ⏳.

**Rows other phases own on which 6b works.** Their checklist rows cite 6b's tests at close-out, and 6b adds no row for them:

| Row (owner) | 6b's contribution |
|---|---|
| `BODY-4` (3b; the gates travel to 6a/6b/6c) | The redirect path's gate: consult `IsReplayable` and decline **loudly** by throwing. The test drives a seekable `FromStream` body (replayable, followed) and a non-seekable one (throws). |
| `BODY-5` (3b) | The redirect path re-sends a body-less request without consulting idempotency. A body-less `POST` under `AllowedMethods = {POST}` is followed. |
| `PIPE-15`, `PIPE-16` (4c) | Each hop is a fresh `continuation.RunAsync(request, context.ForHop(n))` drive with the policy's own request. This is unchanged and re-pinned by the policy suite. |
| `PIPE-40` (4c) | The intermediate-dispose order, and the "return current" paths left open. The "non-replayable body" abandon path is read as retry's, not redirect's (P6b-17). |
| `XCUT-17` (10; phase 1 built a), b), c)) | (a) to (d) end to end, including (d)'s rejection error and "logging the deviation" on the opt-in. |
| `AUTH-29`, `XCUT-16` (6c, 10) | The end-to-end credential-leak test covers the credential-free downgrade hop (P6b-25). |
| `HTTP-46` (2a) | The visited set compares textual forms ordinally, so no DNS runs and `Uri.Equals` is never used. |
| `OBS-20`, `OBS-39` (5b) | Every redirect emission is guarded. The five event names and ids, and the four keys, are public constants. |
| `TRANSPORT-1` (8b) | Unchanged. `RedirectPolicy` is the single authority that `TRANSPORT-1` presupposes. |

---

## As built at `3a1db00`

`src/Dexpace.Sdk.Core/Pipeline/Policies/RedirectPolicy.cs` is 4c's request-in/response-out form of phase 1's S3 fix. It
holds `Authorization` stripped on every hop, the seed origin from `context.SeedRequest`, `Cookie` and
`Proxy-Authorization` stripped cross-origin, userinfo dropped, non-http(s) targets returned, and `PIPE-40` disposal.
Against the requirement text it diverges in eleven places:

| # | Defect | Rows |
|---|---|---|
| 1 | A 301 or 302 on POST is rewritten to a body-less GET | `REDIR-3` |
| 2 | 303 is always followed, as GET | `REDIR-5` |
| 3 | There is no allowed-method set: any method is followed on 301/302/307/308 | `REDIR-3`, `REDIR-4`, `REDIR-26` |
| 4 | A non-replayable body returns the 3xx silently, instead of failing with an error naming replayability | `REDIR-6`, `BODY-4` |
| 5 | A rejected downgrade returns the 3xx silently, instead of failing | `REDIR-15`, `XCUT-17`(d) |
| 6 | A permitted downgrade is not surfaced observably | `REDIR-15` |
| 7 | There is no loop detection | `REDIR-16` |
| 8 | `MaxRedirects` defaults to 20 and a negative value is not rejected | `REDIR-17` |
| 9 | There is no predicate and no snapshot | `REDIR-20`, `REDIR-21` |
| 10 | No events are emitted, and 5c's `RedirectHop` emitter has no caller | `REDIR-28` |
| 11 | A 303 rebuild keeps `Content-*` headers; the 303 and 301/302-on-POST rebuilds drop only the body | `REDIR-5` |

`RedirectOptions` carries `MaxRedirects = 20`, `AllowHttpsToHttpDowngrade` and the inert
`StripSensitiveHeadersOnCrossOrigin`. The method is one 120-line `ProcessCoreAsync` under an `MA0051` waiver that cites
"phase 6b rewrites the redirect policy".

---

## Facts the design rests on

The host has no .NET SDK, so nothing was run for this design. Each fact is one of three kinds:

- **verified by the design**: cited, and run on 10.0.401 by design §6.2's authors;
- **read**: from the tree;
- **to verify in the plan's first task**: a throwaway program in the scratchpad, as 4c and 5a did.

A to-verify fact that comes out the other way reopens the ruling that cites it.

| # | Fact | Status | Used by |
|---|---|---|---|
| 1 | `Uri.TryCreate(Uri baseUri, string relative, out Uri result)` resolves per RFC 3986 (dot segments removed) and preserves `%2F` in the path, `%26` in the query, fragment escapes, bracketed IPv6 literals and explicit ports. It decodes a percent-encoded unreserved character (`%41` → `A`). | verified, §6.2 | P6b-10, P6b-30 |
| 2 | `Uri.TryCreate` accepts `ftp://x/y`, so an unsupported scheme must be screened explicitly. | verified, §6.2 | P6b-10 |
| 3 | `GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped)` drops userinfo and preserves every escape in fact 1. | verified, §6.2 | P6b-10 |
| 4 | `Uri.Equals` ignores userinfo and fragment; `Uri.ToString()` is the unescaped, lossy form. | verified, §6.2 | P6b-11 (why a string key, never `Uri.Equals` or `ToString`) |
| 5 | `Uri.Port` is the effective port: `https://h/` and `https://h:443/` both report 443. | verified, §6.2 | P6b-12 |
| 6 | A relative `"/b?x=1"` and a protocol-relative `"//other.example/x"` resolve against an https base on Linux, not as `file:`. | read: `RedirectCredentialHygieneTests` passes both through today's `Uri.TryCreate(request.Url, location, …)` | P6b-10 |
| 7 | `AbsoluteUri` lower-cases the scheme and a registered-name host, and elides a scheme-default port. So `HTTPS://EXAMPLE.COM:443/a` and `https://example.com/a` render identically. | to verify | P6b-11 (loop detection survives normalisation), P6b-30 |
| 8 | `Uri.TryCreate(base, "http:///p")`, `"https://"` and `"http:foo"` either fail or yield an empty `Host`. None yields a dispatchable target. | to verify | P6b-10 (the empty-host screen) |
| 9 | A `Location` holding spaces or non-ASCII is accepted by `Uri.TryCreate` as a relative reference and percent-encoded, without an exception. | to verify | P6b-10 (followed, as Node does) |
| 10 | `Uri.IdnHost` renders an IDN host and its punycode spelling identically (`bücher.example` / `xn--bcher-kva.example`), while `Uri.Host` may not. | to verify | P6b-12 |
| 11 | `FrozenSet<Method>` with the default comparer (`Method` is a sealed record with value equality) constructs and looks up under trimming and NativeAOT. | to verify (the AOT smoke covers it) | P6b-3 |
| 12 | On a sealed record, a hand-written `Equals(RedirectOptions?)`/`GetHashCode` pair replaces the synthesised ones, and the `PublicAPI.Unshipped.txt` lines are unchanged. `ProxyOptions` shows the precedent. | read, `ProxyOptions.cs` | P6b-3 |
| 13 | `Headers.Names` yields each name's first-insertion spelling, and `Headers.Without(string)` removes every value under an ASCII-fold lookup. | read, `Headers.cs` | P6b-5, P6b-13 |
| 14 | `new Request(Method.Get, url, headers, body)` throws for a non-null body (HTTP-7), and for a non-absolute or non-http(s) URL. | read, `Request.cs` | P6b-5, P6b-10 |
| 15 | `Disposal.DisposeQuietly(resource, primary, logger)` adds a dispose failure to `primary`'s suppressed trail and never replaces it. Without a primary, it logs `dexpace.dispose.suppressed`. | read, `Disposal.cs` | P6b-17 |
| 16 | `OperationTelemetry.RedirectHop` redacts the target through the call's `RedactionCache` entry and emits only on a recording operation span. | read, `OperationTelemetry.cs` | P6b-19 |
| 17 | Every existing `Security` class that drives `RedirectPolicy` seeds a `GET`. `RedirectCredentialHygieneTests.Authorization_is_stripped_on_every_hop_even_same_origin` includes a `303` row and asserts a final 200. | read (`grep`) | P6b-24 |

---

## Argued positions

### A. The options (`REDIR-3`–`REDIR-5`, `REDIR-15`, `REDIR-17`, `REDIR-20`, `REDIR-26`; P6b-3)

`RedirectOptions` stays the sealed record 5a made it and follows rule A.1 to A.5. After 6b it holds:

| Member | Type | Default | `init` rule |
|---|---|---|---|
| `MaxRedirects` | `int` | **3** (was 20) | `ArgumentOutOfRangeException.ThrowIfNegative` |
| `AllowHttpsToHttpDowngrade` | `bool` | `false` | — |
| `FollowSeeOther` | `bool` | `false` | new (`REDIR-5`'s `follow303`) |
| `AllowedMethods` | `IReadOnlySet<Method>` | `{GET, HEAD}`, one shared static `FrozenSet` | new. A `null` set, or a `null` element, throws `ArgumentNullException`/`ArgumentException`. The value is copied with `ToFrozenSet()` (`REDIR-26`). |
| `Predicate` | `Func<RedirectCondition, bool>?` | `null` | new (`REDIR-20`) |
| ~~`StripSensitiveHeadersOnCrossOrigin`~~ | | | **removed** (design §6.2, P9) |

**Equality** is hand-written, as on `ProxyOptions`. `AllowedMethods` compares by content (`SetEquals`) and contributes
an order-independent hash (the count, then the XOR of each element's hash). `Predicate` compares by delegate equality.

**Names.** `MaxRedirects` and `AllowHttpsToHttpDowngrade` keep their names. They are already public, read naturally
in .NET (`HttpClientHandler.MaxAutomaticRedirections`), and `AuthHttpsGuardTests` (a `Security` class) sets the second, so a
rename would edit a `Security` test for no requirement. `FollowSeeOther` follows `Status.SeeOther` rather than the
reference's `follow303`. **Open for the lead** on the two new names.

**`REDIR-26`.** A `FrozenSet` copy, not a wrapper. A `ReadOnlySet<T>` view over the caller's `HashSet` would still change
when that set changes. The getter returns the frozen copy typed as `IReadOnlySet<Method>`, which is what design §6.2 names.
An empty set is allowed: it means "follow only an opted-in 303", and a predicate may still follow anything.

**What is not an option.** The stripped header set is not configurable: `REDIR-7` and `REDIR-9` are MUSTs, and §6.2 removes the
one switch that narrowed them. There is no `LocationHeader` (`REDIR-27`, P6b-21). There is no per-hop delay: the spec has none,
and a hop is not a retry.

### B. The decision: one pure function, in a fixed gate order (`REDIR-1`, `REDIR-2`, `REDIR-6`, `REDIR-14`–`REDIR-21`; P6b-6)

The loop delegates every per-hop judgement to one internal static function. It does no I/O, reads no clock and has no
side effect beyond calling the caller's predicate:

```text
RedirectDecision RedirectDecider.Decide(Response response, RedirectChain chain, RedirectOptions options)
```

It returns one of three outcomes. `Follow(nextRequest, target, crossOrigin, downgraded)`. `ReturnCurrent(reason)`, where the
reason is one of `NotARedirect`, `NotEligible`, `MalformedLocation`, `LoopDetected` and `HopCap`. `Fail(exception)`. The
`RedirectChain` is the per-call state, held on the stack of one `ProcessCoreAsync`. It holds:

- the seed `HttpOrigin`;
- the original method;
- the current request;
- the visited keys, as an ordered list plus a `HashSet<string>` (ordinal);
- the followed count.

The gates, in order:

1. **Fast path** (`REDIR-1`, `REDIR-2`, `REDIR-21`). A status outside `{301, 302, 303, 307, 308}` returns
   `ReturnCurrent(NotARedirect)`. Nothing is allocated, and the predicate is not called.
2. **Resolve the target, totally** (`REDIR-12`, `REDIR-14`, `REDIR-18`, `REDIR-19`; P6b-10). The result is a userinfo-free,
   dispatchable `Uri`, or `null`. This runs before the predicate so that the snapshot can carry it (P6b-8). It cannot throw.
3. **Snapshot and eligibility** (`REDIR-20`, `REDIR-21`). The snapshot is always allocated. If a predicate is configured, its
   answer is the eligibility decision. Otherwise eligibility is `status == 303 ? FollowSeeOther :
   AllowedMethods.Contains(originalMethod)`. Not eligible → `NotEligible`.
4. **Unusable target** → `MalformedLocation`. Placed after eligibility, so a predicate is heard even with no usable
   `Location`, per `REDIR-21`'s NOTE, but it can never force one (P6b-7).
5. **Loop** (`REDIR-16`). The target's key is already visited → `LoopDetected`.
6. **Hop cap** (`REDIR-17`). `chain.Followed >= MaxRedirects` → `HopCap`. With `MaxRedirects = 0` this fails at hop 0, so
   "disables following entirely" needs no branch of its own. Placed after the predicate, so the predicate is consulted at
   the capped hop too, as both siblings do (Ruby P6-91).
7. **Downgrade** (`REDIR-15`; P6b-14). The current hop is https and the target is http. Without the opt-in this gives
   `Fail(RedirectSchemeDowngradeException)`. With it, `downgraded = true` on the follow.
8. **Replay gate** (`REDIR-6`, `BODY-4`; P6b-15). Unless the status is 303, a present body with `IsReplayable == false` gives
   `Fail(RedirectBodyNotReplayableException)`.
9. **Build** (`REDIR-3`–`REDIR-5`, `REDIR-7`–`REDIR-10`; P6b-5, P6b-13). Cross-origin is judged against the seed. The headers
   are stripped. A 303 is rebuilt as a GET, and any other status keeps its method and body. The result is `Follow`.

**Why a pure function.** Three reasons: the card's port target is Node's `decide.test.ts` matrix, which tests exactly this
function; a `[Theory]` over a pure function needs no transport, no pipeline and no disposal bookkeeping per row; and the loop
that remains in `RedirectPolicy` is small enough to read as the lifecycle alone, which is what `REDIR-22` and `PIPE-40` are
about. Ruby splits the same way (`Reissue`, `Location`, `Chain` beside `Step`).

**Gate order between the two failures.** If both fail, the downgrade error is reported, not the replay error. Node orders
them the same way. A downgrade is a policy refusal that holds whatever the body, so it is the more informative error.

### C. Eligibility is judged on the original method (`REDIR-3`, `REDIR-4`; P6b-4)

`REDIR-3` says "the ORIGINAL request method", in capitals. The literal reading and Node's current-hop reading agree on every
chain except one. An opted-in 303 rewrites POST to GET. Under the literal reading, a 301 arriving on that GET hop under the
default set is **not** followed, because the chain began as a POST. Node follows it and records the choice as a deviation.
Ruby follows the letter.

This port follows the letter. The letter costs one follow on a chain that already took an opt-in, and that chain is
returned to the caller as a 301, not lost. Node's argument (the rewritten GET is safe) is sound but makes the port depart
from a MUST's capitalised text for a convenience nobody asked for. The literal reading is also the more conservative one,
and that is the side a redirect policy should err on.

"Original" means the method of the first request the policy drives. That is normally `context.SeedRequest.Method`. It
differs only if a `PerCall` policy above `Redirect` rewrote the method, and then the policy's own first request is what the
chain actually began with. **Open for the lead.**

### D. The 303 rebuild (`REDIR-5`; P6b-5)

A followed 303 always becomes `GET`, from any method, `HEAD` included. RFC 9110 permits `HEAD` to stay `HEAD`, but `REDIR-5`
says "regardless of the original method". The body is dropped, so the rebuild is one `new Request(Method.Get, target,
headers, body: null)`, never a `With*` chain (fact 14).

Every header whose name starts with `content-` (ordinal, ignore case) is removed by iterating `Headers.Names`. A prefix, not a
fixed list: otherwise `Content-MD5`, `Content-Language` and `Content-Disposition` would survive a body that does not (Ruby's
review round 3 found this). The `REDIR-7`/`REDIR-9` stripping runs first, so a cross-origin 303 loses `Cookie` and
`Proxy-Authorization` too (Ruby's round 1). A 303 is exempt from the replay gate because it drops the body. A predicate
that answers "follow" on a 303 gets the same rebuild, even with `FollowSeeOther` off.

### E. The predicate and its snapshot (`REDIR-20`, `REDIR-21`; P6b-7, P6b-8, P6b-9)

**Scope.** `REDIR-20` says a predicate "fully overrides the built-in follow decision". The two siblings read the phrase
differently:

- **Node:** the code and method eligibility only.
- **Ruby:** eligibility and the loop check, with the cap as a ceiling over the answer.

This port takes Node's reading. The predicate replaces gate 3 and nothing else. Loop detection (`REDIR-16`), the cap
(`REDIR-17`), the `Location` screen (`REDIR-18`, `REDIR-19`), the downgrade guard (`REDIR-15`), the replay gate (`REDIR-6`)
and credential hygiene (`REDIR-7`–`REDIR-12`) are each MUSTs with no carve-out for a predicate. The visited list and the count
are handed to the predicate so that it can be *more* conservative, for example by stopping on a revisited host. They are
not a licence to be less so. This reading of an ambiguous MUST is recorded as a §11 item. **Open for the lead.** Ruby's
reading, which lets a predicate re-follow a loop, is the alternative.

**Shape.** `RedirectCondition` is a public sealed class in `Dexpace.Sdk.Core.Pipeline.Policies` with an internal
constructor and four read-only properties:

| Property | Type | Meaning |
|---|---|---|
| `Response` | `Response` | The 3xx being judged, open. The predicate reads its status and headers and must neither dispose it nor read its body. |
| `RedirectsFollowed` | `int` | Hops already followed: 0 on the first decision. |
| `VisitedUris` | `IReadOnlyList<Uri>` | Every URI visited on this call, in insertion order and distinct by key, the current request's included. A fresh array per snapshot, so the predicate cannot reach the live set. |
| `Target` | `Uri?` | The resolved, userinfo-free target this hop would go to, or `null` when the `Location` is missing or unusable. |

It is a class, not a record. A record would synthesise `with` and value equality over a live `Response`, and neither has a
use here. `VisitedUris` is a list documented as a set: .NET has no insertion-ordered read-only set interface, and the
snapshot is built per decision from the chain's list.

**`Target` is an extension the spec does not ask for.** A predicate that wanted to judge the destination would otherwise
parse `Location` itself, and could parse it differently from the policy. That disagreement is a security bug: the
predicate approves `https://good/` while the policy follows something else. Handing it the policy's own resolution removes
the class of bug. **Open for the lead** (both siblings omit it).

**A throwing predicate** (P6b-9). The exception propagates unchanged: no wrapping, unlike retry's `RETRY-40`, because
`REDIR-20` states no conversion. The current response is disposed first, with any dispose failure on the exception's
suppressed trail. `REDIR-22`(b) names the two build failures, but its reason ("the caller never receives the response")
applies to a third throw from the same place. Ruby P6-99 found the same.

### F. Resolution, userinfo and the visited set (`REDIR-12`–`REDIR-14`, `REDIR-16`, `REDIR-18`, `REDIR-19`; P6b-10, P6b-11, P6b-30)

**Reading the header.** `response.Headers.GetAll(HttpHeaderName.WellKnown.Location)` gives three cases:

- **No value**, or one that is empty or all whitespace: `REDIR-19`, returned unfollowed, with no malformed event (it is
  not malformed, it is absent).
- **More than one value:** treated as malformed (`REDIR-18`). `Location` is a singleton field (RFC 9110 §10.2.2). Two values
  are an ambiguous instruction, and picking the first would let an intermediary that appends a field choose the target.
  **Open for the lead**: Ruby takes the first value, and Node reads whatever `get` joins.
- **Exactly one value:** resolved.

**Resolving.** `Uri.TryCreate(currentHopUrl, raw, out var target)` handles both relative and absolute references
(`REDIR-14`). The result is rejected as malformed when any of these holds:

- `TryCreate` fails;
- the scheme is not `http`/`https`, case-insensitively (fact 2);
- `IdnHost` is empty (fact 8).

Userinfo is then dropped (fact 3). The resulting `Uri` is what the visited check, the downgrade check, the cross-origin check,
the snapshot and the next `Request` all see: one object, resolved once, which Ruby's R7 also required. A value that `Uri`
accepts as a relative reference, with spaces or non-ASCII, is followed percent-encoded, because the server said so (fact
9; Node does the same). The function is total. Its only throwing calls are guarded by `TryCreate` and the screen, and
`new Request` is reached only with a screened http(s) URL.

**The visited key** is the userinfo-free `AbsoluteUri`, compared ordinally (`HTTP-46`'s "textual external form"; facts 4 and
7). Never `Uri.Equals`, which would merge URIs differing in fragment or userinfo, and never `ToString()`, which is lossy. The
set is seeded with the first request's key, also userinfo-free, so that a `Location` pointing back at a userinfo-bearing seed
is still a revisit. The fragment stays in the key: it is part of the textual form, and the cap bounds any fragment-varying
loop. `AbsoluteUri`'s normalisation (case, default port; fact 7) means a server cannot evade the check by re-spelling the
same URI (Node's "loop detection survives URL normalization" rows).

**`REDIR-13`'s residue** (P6b-30). `AbsoluteUri` elides an explicit scheme-default port, so `Location:
https://h:443/y` reaches the wire as `https://h/y`. The origin, the request target and the wire meaning are unchanged. Every
non-default port and every IPv6 literal survives, as does every reserved escape (fact 1). `%41` → `A` is RFC 3986's own
equivalence (design §6.2). The checklist states the residue and does not assert that `:443` survives (Ruby P6-96 is the same
finding).

### G. Credential hygiene and cross-origin (`REDIR-7`–`REDIR-12`, `REDIR-24`; P6b-12, P6b-13, P6b-23)

**Origin.** An internal `readonly record struct HttpOrigin(string Scheme, string Host, int Port)` in `Http/Common/`, built
by `HttpOrigin.From(Uri)` from the lower-case `Scheme`, the lower-case `IdnHost` (fact 10) and `Port` (fact 5). The seed
origin is taken once per call from `context.SeedRequest.Url`: design §6.2 and §10 entry 15, 4c's coupling obligation 2. A
target is cross-origin when `HttpOrigin.From(target) != seed`. This replaces today's private `IsCrossOrigin`, which compared
`Host` rather than `IdnHost`. `AuthorizationPolicy.GetOrigin` builds its own string, and 6c may move it onto `HttpOrigin`
(P6b-29). Nothing in 6b requires it, and nothing in 6c is required to adopt it.

**Stripping**, on the request being built, from the policy's own copy of the current hop:

- `Authorization`, every value, always (`REDIR-7`).
- `Cookie` and `Proxy-Authorization`, every value, when cross-origin (`REDIR-9`).
- Nothing else, and nothing re-added.

A same-origin hop keeps `Cookie` and `Proxy-Authorization` (`REDIR-10`'s SHOULD). The conservative MAY is declined, because
dropping a same-origin cookie breaks a login redirect and protects nothing. Once a header has been stripped by a foreign
hop, it is never restored by a return to the seed origin, because each hop is built from the previous hop's already-stripped
request. Phase 1's `Returning_to_the_seed_origin_does_not_restore_what_a_foreign_hop_stripped` pins this.

**Why the auth credential needs no stripping here.** The policy drives each hop with, and builds the next hop from, its own
request, never the request a downstream policy stamped (`PIPE-16`, `RETRY-44`, 4c). A credential stamped by an
`AuthorizationPolicy` subclass is never in the redirect policy's request. The strip of `REDIR-7` exists for an
`Authorization` the **caller** set on the seed. Re-stamping is the auth policy's own job. It runs per hop and per attempt,
and withholds whenever the request it would stamp is not same-origin with the seed (§10 entry 15, `AUTH-29`).

**`REDIR-11` with no marker** (P6b-23). There is nothing to clear on re-issue (clause a), nothing that could cause a send
(clause b) and nothing to strip before dispatch (clause c). A matrix row pins the absence: a seed carrying a header spelled
like the reference's marker (`x-dexpace-internal-redirect-cross-origin: 1`) is treated as an ordinary header. It changes
neither the cross-origin judgement nor auth stamping.

**A residual risk, stated.** A secret the caller put in a non-standard header on the seed, such as `X-Api-Key`, crosses a
cross-origin hop. The spec names only the three headers, and an SDK cannot know which custom headers are secret.
`ApiKeyAuthPolicy`, which stamps per hop and withholds cross-origin, is the supported way to send such a key. The user page
says so.

### H. Downgrade and replayability fail loudly (`REDIR-6`, `REDIR-15`; P6b-14, P6b-15, P6b-16)

**The exceptions.** A small family in `Dexpace.Sdk.Core.Errors`, following the `StreamingException` precedent:

```text
public class RedirectException : SdkException                         // the three standard constructors
public sealed class RedirectSchemeDowngradeException : RedirectException
public sealed class RedirectBodyNotReplayableException : RedirectException
```

A caller catches `RedirectException` for both, or a leaf for one. The family carries:

- **No `ServiceRequestException` base.** That base means "never sent, retry-safe", and these are refusals made after a
  response arrived. `Redirect` sits outside `Retry` anyway, so no retry policy ever sees them.
- **No raw-URL properties.** A structured logger that destructures exception properties would publish a raw query token,
  which `XCUT-19`(a) and (b) forbid on a log path. The message carries both URLs through `UrlRedactor.Default` (total, so
  a malformed input renders the sentinel). Node puts the raw URL on a field, and this port declines to.

The two messages:

- `"Refused to follow the 302 redirect from '{from}' to '{to}': it downgrades https to http. Set
  RedirectOptions.AllowHttpsToHttpDowngrade to permit it."`
- `"Refused to follow the 307 redirect to '{to}': the request body is not replayable, and a redirect re-sends it. Buffer the
  body with RequestBody.ToReplayableAsync() before sending."`

The second names replayability, as `REDIR-6` requires. **Open for the lead** on the family against one type with a reason
enum.

**Downgrade** (P6b-14). Judged per hop: the current hop's request URL against its target (`REDIR-15`'s "on each individual
hop transition"). An https→http→https chain flags only the hop that downgraded. With the opt-in, the hop is followed, a
`http.redirect.scheme_downgrade_permitted` warning is emitted (the "surface it observably" clause, and `XCUT-17`(d)'s
"logging the deviation"), and stripping applies as usual. The target is cross-origin from an https seed by scheme, so
`Authorization`, `Cookie` and `Proxy-Authorization` are gone, and the auth policy forwards the hop credential-free without its
HTTPS guard firing (`AUTH-29`, `XCUT-16`; `AuthHttpsGuardTests.A_credential_free_cross_origin_downgrade_hop_skips_the_guard`).
Without the opt-in, a `http.redirect.scheme_downgrade_rejected` warning is emitted, then the response is disposed and the
exception thrown (Ruby P6-97: a defined rejection name that nothing emits is a trap).

**Replay gate** (P6b-15). This is `BODY-4`'s redirect branch, which declines **loudly**, as the requirement assigns.
`RequestBody.IsReplayable` is the only input: a bytes, string, file or form body is replayable, and so is a seekable,
length-known `FromStream` body (3b). No idempotency is consulted (`BODY-5`), so a body-less `POST` is followed when `POST` is
allowed. With the default `{GET, HEAD}` set the gate cannot fire, because neither method carries a body (fact 14). It
matters only when a caller widens the set or a predicate approves a body-bearing hop. That is why the as-built
`ReDriveLifecycleTests.A_redirect_over_a_non_replayable_body_returns_the_in_flight_response_undisposed` still passes after the
rewrite, but for the wrong reason (`NotEligible`), and is split in two (see [Tests](#tests-vectors-and-ports)).

### I. Lifecycle and the loop (`REDIR-22`, `REDIR-23`, `PIPE-40`; P6b-17, P6b-18)

```text
chain = new RedirectChain(request, context)                      // seed origin, original method, visited = [key(request)]
while (true)
    response = drive(chain.Current, context.ForHop(chain.Followed))
    decision = DecideOrDispose(response, chain, options)          // a throw (predicate) disposes `response` first (P6b-9)
    switch decision
        ReturnCurrent(reason): emit the reason's event (loop, malformed) if any; return response      // open (REDIR-22c)
        Fail(ex):              emit downgrade-rejected if that is the reason;
                               DisposeQuietly(response, primary: ex); throw ex                       // REDIR-22b
        Follow(next, …):       emit downgrade-permitted if downgraded; emit hop (log and span);
                               DisposeQuietly(response, logger)   // REDIR-22a: BEFORE the next drive
                               context.CancellationToken.ThrowIfCancellationRequested()              // P6b-18
                               chain.Advance(next)                // visited += key(target); Followed++
```

- **Order is the requirement** (`REDIR-22`(a)). The superseded response is disposed before the next drive, not after it
  returns. A one-connection pool would otherwise deadlock. The test records the dispose against the transport's call count, as
  4c's `ReDriveLifecycleTests` and Ruby's testing strategy both do.
- **`PIPE-40` and `REDIR-22`(b).** `PIPE-40` lists "non-replayable body" among the abandon paths that return the in-flight
  response *unclosed*. `REDIR-6` and `REDIR-22`(b) say the redirect path *fails*, and closes the response first. The two
  agree once `PIPE-40`'s list is read as the union over its three re-driving pillars: retry declines a non-replayable body by
  surfacing the last outcome, so that response is returned open; redirect declines by throwing, so nobody receives the
  response and it must be closed. `BODY-4` states exactly this split. The redirect-specific MUSTs govern, and a §11 item
  records the reading.
- **Cancellation between hops** (P6b-18). After the superseded response is disposed, a cancelled call token throws
  `OperationCanceledException` before the next drive. Nothing is leaked, because nothing is held. The alternative, letting the
  transport throw on the next drive, costs one wasted dispatch attempt in a fake transport and an unbounded one in a real
  transport that ignores the token. Node returns the current response on abort. That is impossible here, because it has
  already been disposed.
- **Stack safety** (`REDIR-23`). One `while` loop. A 1 000-hop scripted chain with `MaxRedirects = 1000` and distinct
  targets completes on both paths.
- **Sync parity.** `Process` keeps 4c's `SyncPath.GetCompletedResult(ProcessCoreAsync(…, async: false))`. The decision is
  synchronous anyway, and the predicate is a synchronous delegate.

### J. Observability (`REDIR-28`; P6b-19, P6b-20)

**Log events**, ids 150–154 from 5b's reserved range. Each is a public `const` pair on `DexpaceLogEvents` (`OBS-39`
stability), written with a cached `LoggerMessage.Define` (the `notes/observability.md` conflict, with a scoped `CA1727`
suppression for the dotted keys) to `CallState.Logger`:

| Event (id) | Level | When | Keys |
|---|---|---|---|
| `http.redirect.hop` (150) | `Information` | a hop is followed, before the superseded response is disposed | `url.full` (the current hop, redacted), `dexpace.redirect.target` (redacted), `http.response.status_code`, `dexpace.redirect.hop` (1-based number of the new hop), `dexpace.redirect.cross_origin` |
| `http.redirect.loop_detected` (151) | `Warning` | `ReturnCurrent(LoopDetected)` | `url.full`, `dexpace.redirect.target`, `dexpace.redirect.hop` (followed so far) |
| `http.redirect.scheme_downgrade_rejected` (152) | `Warning` | `Fail` with a downgrade | `url.full`, `dexpace.redirect.target` |
| `http.redirect.scheme_downgrade_permitted` (153) | `Warning` | a downgrade is followed under the opt-in | `url.full`, `dexpace.redirect.target` |
| `http.redirect.location_malformed` (154) | `Warning` | `ReturnCurrent(MalformedLocation)` on an eligible hop | `url.full`, `dexpace.redirect.location` (the value through `RedactHeaderValue`) |

- **Names.** The `http.redirect.*` prefix follows 5b's `http.request`/`http.response`/`http.instrumentation.*` event
  vocabulary. The keys reuse 5b's `url.full` and `http.response.status_code`. The new keys take the `dexpace.redirect.*`
  prefix of 5c's span attributes, so the log key and the span attribute for the hop number and the cross-origin flag are one
  name each. The four new keys are public `const`s on `DexpaceLogKeys`.
- **Levels.** A followed hop is `Information`: there are at most `MaxRedirects` per call, they are not gated by
  `HttpLoggingOptions.Level` (they carry a redacted URL and no header or body), and siblings agree. The four anomalies are
  `Warning`. No event for `NotEligible` or `HopCap`: `REDIR-28` names four kinds, and a refused 3xx is the caller's own
  configuration speaking. **Open for the lead** on the hop level (`Debug` is the alternative).
- **Redaction.** URLs go through the call's `RedactionCache.Get(context.Options.Logging).Redactor`, the same instance 5b's
  `http.request` uses, so one allow-list applies per call. `UrlRedactor.Redact(Uri)` is total and degrades to `[malformed
  url]` (`REDIR-28`'s "redaction failures … swallowed to a placeholder").
- **The guard** (`OBS-20`). One emission method per event. Each checks `IsEnabled(level)` first, builds nothing when
  disabled, and holds its own `try`/`catch` reporting `http.instrumentation.log_failed`, 5b's pattern. A logging failure
  never changes the decision or the response's lifecycle.
- **The malformed `Location` is redacted, not raw** (P6b-20). `REDIR-28` says the malformed-`Location` event "logs the raw
  `Location` string as received (it failed to parse, so cannot be redacted)", and warns porters about credential-bearing
  values. This port *can* redact it: 5b's `UrlRedactor.RedactHeaderValue` is total over unparseable text (it masks every
  `//authority` userinfo and cuts the query). So the event carries the redacted value. That is stricter than the reference,
  and the reference's own caveat is the reason. It is logged as a "stronger" ledger row, not a §10 deviation.
- **The span event.** On each followed hop the policy calls `OperationTelemetry.RedirectHop(context, chain.Followed + 1,
  status, target, crossOrigin)`, the shape 5c fixed. No other span event is added: 5c's rule is "no events of their own name
  without a dated correction". `http.request.resend_count` already counts hops (5c), and `InstrumentationPolicy` reads
  `HopNumber` from `context.ForHop`.

### K. The end-to-end credential-leak test (the phase 6 convergence point; P6b-25)

The card's exit criterion: "an `Authorization`, `Cookie` and `Proxy-Authorization` header each survives neither a
cross-origin hop nor a retry across a hop". The roadmap gives this test to 6b ("the convergence point is an end-to-end
credential-leak test, and 6b owns it").

**Core, `tests/Dexpace.Sdk.Core.Tests/Security/RedirectCredentialLeakTests.cs`.** The pipeline is the real stack:
`RedirectPolicy` → `RetryPolicy` (instant `TimeProvider`) → a `PerHop` probe → a real `AuthorizationPolicy` subclass
(`BasicAuthPolicy`, plus `BearerTokenAuthPolicy` in a second theory column) → `ScriptedTransport`. URLs are https throughout,
because the auth guard refuses plaintext. The seed carries a caller-set `Authorization`, `Cookie` and `Proxy-Authorization`.
The assertions run on **every** request the transport saw, never only the last (Ruby's testing strategy). Scenarios, as one
`[Theory]`:

| Scenario | Script | Expected on each request after the seed |
|---|---|---|
| same-origin hop | 302 → same origin → 200 | `Authorization` = the auth policy's stamp (not the caller's), and `Cookie`/`Proxy-Authorization` kept |
| cross-origin hop | 302 → other host → 200 | none of the three |
| cross-origin hop, then a retry | 302 → other host → 503 → 200 | none of the three on **both** attempts at the foreign host |
| same-origin hop, then a retry | 307 → same origin → 503 → 200 | stamped on both attempts; the cookie kept |
| a retry before the hop | 503 → 302 → other host → 200 | the seed's two attempts stamped; the foreign hop bare |
| foreign then back to the seed origin, with a retry | 302 → other → 302 → seed origin → 503 → 200 | foreign bare; back at the seed origin, `Authorization` re-stamped by auth (the credential's own origin) while the caller's `Cookie`/`Proxy-Authorization` stay gone |
| a permitted downgrade, then a retry | 302 → `http://other` → 503 → 200 (`AllowHttpsToHttpDowngrade`) | none of the three; no `SdkException` from the HTTPS guard (`AUTH-29`) |
| a port change only | 302 → `https://seed:8443/` → 200 | none of the three (`REDIR-8`'s effective port) |

**Wire, `tests/Dexpace.Sdk.Http.SystemNet.Tests/Security/RedirectCredentialLeakWireTests.cs`.** Two loopback servers (two
ports, so two origins) and the SDK-managed `SystemNetHttpClient`. The scenario is "cross-origin hop, then a retry": the
foreign server answers 503, then 200, and its recorded requests carry none of the three headers. The loopback is plain
http, so no auth policy runs here (`AUTH-28` would refuse it). The wire test proves the caller-set half, and the core test
proves the stamped half.

**Category.** Both are tagged `Security`, although they are not phase-1 regressions. They are the permanent exit proof
of `XCUT-17` and of phase 6's convergence point, and a test that guards a credential leak should carry the constraint-5
protection ("deleting or loosening a `Security` test is a review-blocking change"). `TestCategoryTests` accepts the tag.
**Open for the lead** (`Integration` is the alternative, and loses that protection).

**Against the moving siblings.** The test uses only public constructors that exist today: `RetryPolicy(TimeProvider)`,
`BasicAuthPolicy(BasicCredential)`, `BearerTokenAuthPolicy(TokenCredential…)`. 6a and 6c may change those constructors.
Whichever of 6a/6c lands after 6b's PR 3 keeps the class green, or moves it without weakening it (constraint 5). PR 3 is
scheduled last so that it sees as much of 6a and 6c as possible (P6b-28).

---

## The public surface (`PublicAPI.Unshipped.txt`)

Core only. `Dexpace.Sdk.Http.SystemNet` and `Dexpace.Sdk.Serialization.SystemTextJson` are untouched. The plan confirms the
exact lines from the analyzer's code fix. Abbreviations: `C.` is `Dexpace.Sdk.Core.Configuration.`, `P.` is
`Dexpace.Sdk.Core.Pipeline.Policies.`, `E.` is `Dexpace.Sdk.Core.Errors.`, `D.` is `Dexpace.Sdk.Core.Diagnostics.`.

**Removed:**

```text
C.RedirectOptions.StripSensitiveHeadersOnCrossOrigin.get -> bool
C.RedirectOptions.StripSensitiveHeadersOnCrossOrigin.init -> void
```

**Added:**

```text
C.RedirectOptions.AllowedMethods.get -> System.Collections.Generic.IReadOnlySet<Dexpace.Sdk.Core.Http.Common.Method!>!
C.RedirectOptions.AllowedMethods.init -> void
C.RedirectOptions.FollowSeeOther.get -> bool
C.RedirectOptions.FollowSeeOther.init -> void
C.RedirectOptions.Predicate.get -> System.Func<P.RedirectCondition!, bool>?
C.RedirectOptions.Predicate.init -> void
P.RedirectCondition
P.RedirectCondition.Response.get -> Dexpace.Sdk.Core.Http.Response.Response!
P.RedirectCondition.RedirectsFollowed.get -> int
P.RedirectCondition.VisitedUris.get -> System.Collections.Generic.IReadOnlyList<System.Uri!>!
P.RedirectCondition.Target.get -> System.Uri?
E.RedirectException
E.RedirectException.RedirectException() -> void
E.RedirectException.RedirectException(string! message) -> void
E.RedirectException.RedirectException(string! message, System.Exception? innerException) -> void
E.RedirectSchemeDowngradeException            (sealed; the same three constructors)
E.RedirectBodyNotReplayableException          (sealed; the same three constructors)
const D.DexpaceLogEvents.RedirectHop = "http.redirect.hop" -> string!
const D.DexpaceLogEvents.RedirectHopId = 150 -> int
const D.DexpaceLogEvents.RedirectLoopDetected = "http.redirect.loop_detected" -> string!
const D.DexpaceLogEvents.RedirectLoopDetectedId = 151 -> int
const D.DexpaceLogEvents.RedirectSchemeDowngradeRejected = "http.redirect.scheme_downgrade_rejected" -> string!
const D.DexpaceLogEvents.RedirectSchemeDowngradeRejectedId = 152 -> int
const D.DexpaceLogEvents.RedirectSchemeDowngradePermitted = "http.redirect.scheme_downgrade_permitted" -> string!
const D.DexpaceLogEvents.RedirectSchemeDowngradePermittedId = 153 -> int
const D.DexpaceLogEvents.RedirectLocationMalformed = "http.redirect.location_malformed" -> string!
const D.DexpaceLogEvents.RedirectLocationMalformedId = 154 -> int
const D.DexpaceLogKeys.RedirectHop = "dexpace.redirect.hop" -> string!
const D.DexpaceLogKeys.RedirectTarget = "dexpace.redirect.target" -> string!
const D.DexpaceLogKeys.RedirectCrossOrigin = "dexpace.redirect.cross_origin" -> string!
const D.DexpaceLogKeys.RedirectLocation = "dexpace.redirect.location" -> string!
```

**Unchanged lines whose meaning changes:**

- `C.RedirectOptions.MaxRedirects` (default 3; negative throws);
- `C.RedirectOptions.Equals`/`GetHashCode` (now hand-written);
- `P.RedirectPolicy` (behaviour; see Breaking changes).

### Internal types

All in `Dexpace.Sdk.Core`, visible to `Dexpace.Sdk.Core.Tests` through the existing `InternalsVisibleTo`:

| Type | Location | Role |
|---|---|---|
| `HttpOrigin` (`readonly record struct`) | `Http/Common/HttpOrigin.cs` | The RFC 6454 triple; `From(Uri)`. Offered to 6c (P6b-12). |
| `RedirectLocation` (static) | `Pipeline/Policies/Redirect/` | `TryResolve(Uri current, Headers headers, out Uri? target, out string? malformedRaw)`: the total resolver (P6b-10). |
| `RedirectChain` (sealed class) | the same folder | The per-call state: seed origin, original method, current request, visited list and set, followed count; `Advance`, `Snapshot`. |
| `RedirectDecision` (`readonly struct`) and `RedirectStopReason` (enum, explicit values) | the same folder | The decision's three outcomes. |
| `RedirectDecider` (static) | the same folder | `Decide` and its gates, each a method under 70 lines (P6b-27). |
| `RedirectReissue` (static) | the same folder | Header stripping and the 303 rebuild (P6b-5, P6b-13). |
| `RedirectLog` (static partial) | `Diagnostics/RedirectLog.cs` | The five guarded `LoggerMessage.Define` emitters. |

A `Pipeline/Policies/Redirect/` subfolder keeps `Pipeline/Policies/` a flat list of policies. The namespace stays
`Dexpace.Sdk.Core.Pipeline.Policies` (the folder is organisational, as `Diagnostics/HttpLogEmitter.*.cs` is). If the
plan finds the analyzers' folder-to-namespace rule (IDE0130) on, it flattens the folder instead.

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes, and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `RedirectOptions.StripSensitiveHeadersOnCrossOrigin` is removed (it has had no effect since phase 1) | source | 1 |
| 2 | `MaxRedirects` defaults to 3 (was 20), and a negative value throws `ArgumentOutOfRangeException` at `init` | behaviour | 1 |
| 3 | A 301 or 302 on POST is no longer rewritten to a body-less GET. A 301/302/307/308 is followed only for a method in `AllowedMethods` (default `{GET, HEAD}`), with the method and body preserved; otherwise the 3xx is returned | behaviour | 1 |
| 4 | A 303 is no longer followed by default (`FollowSeeOther`). When followed, every `Content-*` header is removed too | behaviour | 1 |
| 5 | An https→http hop without the opt-in throws `RedirectSchemeDowngradeException` (was: the 3xx returned) | behaviour | 1 |
| 6 | A followed method-preserving hop over a non-replayable body throws `RedirectBodyNotReplayableException` (was: the 3xx returned) | behaviour | 1 |
| 7 | A redirect that revisits a URI already visited on the call returns that 3xx | behaviour | 1 |
| 8 | A response with more than one `Location` value is returned unfollowed | behaviour | 1 |
| 9 | The redirect policy writes `http.redirect.*` log events (150–154) to the pipeline's logger, the hop at `Information` | behaviour | 2 |

Additive, with no **Breaking** marker: `AllowedMethods`, `FollowSeeOther`, `Predicate`, `RedirectCondition`, the
`RedirectException` family, and the log-event and log-key constants.

---

## Keeping the `Security` classes green

Constraint 5 says green, or moved without weakening. **One `Security` class is edited, and none is loosened** (P6b-24).

| Class | Edit | Why it is not a weakening |
|---|---|---|
| `RedirectCredentialHygieneTests` | (1) `Authorization_is_stripped_on_every_hop_even_same_origin`'s `303` row runs with `FollowSeeOther = true`. (2) `Stripping_is_on_by_default_regardless_of_the_legacy_switch` is replaced by `Stripping_holds_under_every_redirect_option`, which sets every option to its most permissive value (`AllowHttpsToHttpDowngrade`, `FollowSeeOther`, `AllowedMethods` = every well-known method, a predicate that always answers "follow") and asserts the same three headers absent cross-origin. (3) The header comment drops "the rest of the matrix … is phase 6b's" and names the new matrix class. | (1) The row still asserts the strip on a *followed* 303. Without the opt-in the 303 would not be followed at all and the assertion would be vacuous, because `FollowAsync` asserts a final 200. (2) The old test showed that one switch could not turn stripping off. The switch no longer exists, so the replacement shows that **no** option can, which is a strictly larger claim. The property's absence is pinned by the options-shape test in `DexpaceClientOptionsTests` (Unit). |
| `RedirectWireTests` | None | Every case seeds a GET over a same-scheme loopback with at most two hops. The new defaults change none of its outcomes. |
| `AuthHttpsGuardTests` | None | It sets `AllowHttpsToHttpDowngrade = true`, whose name is kept (P6b-3), and seeds a GET. Its downgrade case now also emits `http.redirect.scheme_downgrade_permitted`, which it does not observe. |
| `ReDriveRequestIsolationTests` | None | A GET with a 307. |
| `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, `UrlRedactionDefaultDenyTests`, `RetryPacingOverflowTests`; SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | None | 6b touches none of their subjects. |

**Added:** `RedirectCredentialLeakTests` (core) and `RedirectCredentialLeakWireTests` (SystemNet), both `Security` (P6b-25).
The plan runs `RedirectCredentialHygieneTests` and `RedirectWireTests` after PR 1, before anything else.

---

## Migration plan from the as-built code

| Change | Sites | Rewrite |
|---|---|---|
| The policy | `Pipeline/Policies/RedirectPolicy.cs` | the loop of position I over `RedirectDecider`; the class remarks rewritten (the 301/302-on-POST bullet, the 303 bullet, the "Non-replayable body guard: … not followed" paragraph and the `StripSensitiveHeadersOnCrossOrigin` sentence all go); the `MA0051` waiver removed |
| The options | `Configuration/RedirectOptions.cs` | the members of position A; hand-written equality; the "Roadmap phase 6b removes the property" remark goes |
| Option-shape tests | `Configuration/DexpaceClientOptionsTests.cs` (lines 64–66 and the member-name list at 142) | the new defaults and the new member list |
| `ReDriveLifecycleTests` | `Options(maxRedirects: 20)`; `A_redirect_over_a_non_replayable_body_returns_the_in_flight_response_undisposed` | the default becomes the record's own; the non-replayable test is split (see Tests) |
| `RedirectPolicyTests` | the whole class | replaced (see Tests); no non-conforming fact is kept |
| `DexpacePipeline` / `PipelineBuilder.AddStandardResilience` remarks | `MaxRedirects` "to zero" sentence | unchanged in meaning; the hop default is restated as 3 where any remark names 20 |
| Docs | `src/Dexpace.Sdk.Core/README.md` (the options sample, if it shows redirect), the new user page `docs/sdk-documentation/redirect.md`, the `pipelines.md` sentence on `MaxRedirects` | close-out PR |

---

## PR segmentation

Each step is one pull request carrying its code **and** its tests, the one-PR allowance 2a through 5c used, with its
`PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` lines. A code-only PR would land red: the option removal stops a `Security`
test compiling, and the new defaults change existing assertions.

| PR | Content | Rows | Gate |
|---|---|---|---|
| **1** | `HttpOrigin`; the `Redirect/` internals; `RedirectCondition`; the `RedirectException` family; `RedirectOptions` reshaped; `RedirectPolicy` rewritten (no events yet); `RedirectDecisionMatrixTests`; `RedirectPolicyTests` replaced; the `Security` edits of P6b-24; the `ReDriveLifecycleTests` split; the options-shape tests | `REDIR-1`–`REDIR-26` (except `REDIR-25` 🚫, argued in the checklist) | none; the two redirect `Security` classes run first |
| **2** | `RedirectLog` and the five events; the `DexpaceLogEvents`/`DexpaceLogKeys` constants; the `OperationTelemetry.RedirectHop` call; the redaction and guard tests | `REDIR-28` | 1 |
| **3** | `RedirectCredentialLeakTests` and `RedirectCredentialLeakWireTests`. Close-out: the AOT smoke extended (a predicate, a `FrozenSet` `AllowedMethods`, a followed 307 and a thrown downgrade under NativeAOT); `docs/sdk-documentation/redirect.md`; the 6b checklist; the dated design corrections; `CHANGELOG.md`; `CLAUDE.md` and README drift; the roadmap status note | all 28 (closing); the convergence exit | 1, 2; and as late as practical against 6a/6c (P6b-28) |

The likely conflicts with 6a and 6c are in five shared files: `PublicAPI.Unshipped.txt`, `CHANGELOG.md`, `DexpaceLogEvents.cs`
(6a adds 140–149, 6c 160–169), `DexpaceLogKeys.cs` and the roadmap. Whichever PR lands second re-derives its hunks on the
merged file rather than resolving them (the 5a/5b precedent). `RedirectOptions.cs` and `RetryOptions.cs` are separate files,
so 6a's record changes do not collide with 6b's.

---

## Tests, vectors and ports

- **`RedirectDecisionMatrixTests` (Unit), the card's "one `[Theory]` table".** A `TheoryData<string>` of row names over
  `RedirectDecider.Decide`, each name looked up in an internal `RedirectCases` table (a public theory cannot take an internal
  record, CS0051; the 5a `ProxyResolutionCase` pattern). Each row:
  - is a named record with a `REDIR-n: description` display name;
  - sets the request (method, URL, headers, body kind: none, replayable, single-use);
  - sets the response (status, `Location` values, extra headers);
  - sets the options and the chain state (followed count, visited keys, seed URL when it differs);
  - states the expected outcome: the kind, the stop reason or exception type, and for a follow the next method, its exact
    `AbsoluteUri`, the headers present and absent, whether the body is the *same instance*, and `crossOrigin`.

  The rows are ported from `nodejs-sdk@54aeed4` `decide.test.ts`, one per `test(…)`, in its section order, with five changes:
  - its fast-check properties become fixed examples ("never throws for arbitrary garbage": a dozen hostile values;
    "the hop cap bounds every chain": counts 0 to 5);
  - its four marker rows (L669, L684, L902, L922) become the P6b-23 "no marker" row;
  - its "eligibility against the CURRENT hop" row is **inverted** to assert the literal reading (P6b-4);
  - its "the predicate does NOT bypass the safety mechanics" row is kept and extended to the loop and the cap (P6b-7);
  - its "the frozen shared return-current value" row is dropped (a JavaScript object-identity fact; constraint 10).

  Added rows:
  - two `Location` values (P6b-10);
  - an IDN host spelled two ways (fact 10);
  - an https→http→https chain flagging only the middle hop;
  - a 303 whose `content-type`, `CONTENT-LENGTH` and `cOnTeNt-Language` are all removed (Ruby round 3);
  - a cross-origin 303 dropping `Cookie` (Ruby round 1);
  - a fragment-only and a fragment-bearing `Location` (Ruby round 2);
  - a seekable `FromStream` body followed and a non-seekable one failing (3b's hand-off).
- **`RedirectPolicyTests` (Unit), replaced.** The loop, through `PipelineBuilder` over `ScriptedTransport` with
  `TrackingResponseBody`:
  - `REDIR-22`(a)'s order (dispose recorded before the next send);
  - (b) on both failures and on a throwing predicate, with exactly one dispose and the exception unchanged;
  - (c) on each of the five stop reasons, with the response undisposed;
  - loop end to end: A→B→A returns B's 3xx open;
  - the cap at 0, 1 and 3;
  - the 1 000-hop stack-safety chain;
  - the snapshot copy (a predicate that casts `VisitedUris` and attempts a write cannot change the next decision);
  - `REDIR-21`'s predicate call counts (zero on 200/300/304/305; one per recognised 3xx, with no `Location` and at the cap);
  - cancellation between hops (P6b-18);
  - `REDIR-24` (an auth-stage probe invoked once per hop);
  - sync parity: every lifecycle case on `Process` too.

  The ten non-conforming facts of today's class are **deleted, not kept alongside** (the card's exit):
  - `ProcessAsync_302OnPost_BecomesGetWithNoBody`;
  - `A_303_on_a_POST_with_a_body_follows_as_a_bodiless_GET`;
  - `ProcessAsync_303OnPut_BecomesGetWithNoBody`;
  - `ProcessAsync_301OnPost_BecomesGet`;
  - `ProcessAsync_307OnPost_PreservesMethodAndBody` and `ProcessAsync_308OnPost_PreservesMethodAndBody` (POST is not
    followed by default; the preserved case moves to the matrix under `AllowedMethods = {POST}`);
  - `ProcessAsync_HttpsToHttpDowngrade_NotFollowed_WhenFlagFalse` (now throws);
  - `ProcessAsync_307OnPost_NonReplayableBody_NotFollowed` (now throws when eligible);
  - `ProcessAsync_MaxRedirects_StopsAndReturnsLast3xxResponse` as written (it relies on `MaxRedirects = 10` and is restated
    at the new default);
  - the `MakeOptions` helper's `stripSensitiveHeadersOnCrossOrigin` parameter.

  The conforming facts (`Stage_IsRedirect`, the relative-`Location`, no-`Location`, malformed and non-http(s)-scheme cases,
  the 200 pass-through, the chained multi-hop) move into the matrix or the new class unchanged in substance.
- **`ReDriveLifecycleTests` (Unit).** `A_redirect_over_a_non_replayable_body_returns_the_in_flight_response_undisposed` becomes
  `A_redirect_on_a_method_outside_the_allowed_set_returns_the_in_flight_response_undisposed` (the reason it already passes
  for) plus `A_redirect_over_a_non_replayable_body_throws_and_disposes_the_in_flight_response_once` (`AllowedMethods =
  {POST}`). The other cases are untouched.
- **`RedirectLogTests` (Unit, PR 2).** TestSupport's `RecordingLogger`, `ThrowingLogger`, `DisabledLogger` and `ActivityRecorder` assert:
  - each event's id, name, level and keys;
  - a userinfo- and token-bearing target redacted;
  - a malformed `Location` carrying `user:pw@` redacted by `RedactHeaderValue` (P6b-20);
  - a throwing logger leaving the response and the decision unchanged (`OBS-20`);
  - nothing built when the level is disabled;
  - the `dexpace.redirect.hop` span event on a recording operation span, through 5c's listener fixture.
- **Ruby ports.** The plan's Ruby port table gives every `step_test.rb` class and the three sibling files (`condition_snapshot_test.rb`, `events_test.rb`, `scheme_downgrade_error_test.rb`) a target or a stated skip. `step_test.rb`'s `LifecycleTest`, `PredicateTest`, `RebuildTest` and `EmissionTest` cases are ported where
  they assert behaviour this design shares. Its `MarkerTest` and `ForkingTest` are not ported: they test a cursor-state marker
  and a fork primitive this port does not have (§10 entry 15; 4c's `ForHop`). `redirect_fixtures.rb`'s helpers are already
  covered by TestSupport's `ScriptedTransport`, `TestResponses.Redirect` and `TrackingResponseBody`. A small internal
  `RedirectFixtures` class in the core test project adds the multi-value `Location` response and the scripted chain builder.
  Each ported file cites its source line range, as the 5a vectors did.
- **AOT smoke (PR 3).** `SmokeChecks` gains a redirect check: a scripted 307 followed under a custom `AllowedMethods` and a
  predicate, and a downgrade rejected with `RedirectSchemeDowngradeException`, under NativeAOT (fact 11).
- **Every test** carries `[Trait("Category", "Unit")]` except the two leak classes (`Security`) and the AOT smoke check
  (`AotSmoke`).

---

## Cross-sub-phase interfaces assumed of 6a and 6c

Stated so each sibling's design can confirm or reject them. A rejection reopens the ruling named.

| With | 6b assumes | 6b provides | Ruling |
|---|---|---|---|
| **6a** | `RetryPolicy` stays at `PipelineStage.Retry`, inside `Redirect`. It re-sends the request it received, never one a downstream policy stamped (`RETRY-44`), and so never restores a header the redirect policy stripped. It keeps a public constructor taking a `TimeProvider` (or the leak test moves with it, without weakening). | `RedirectException` is never a retry candidate (it is thrown outside `Retry`). The hop's request is a stripped, fully-built `Request` that retry treats like any other. | P6b-25, P6b-29 |
| **6a** | 6a's events use ids 140–149 and keys of their own. 6a does not add a `dexpace.redirect.*` key. | ids 150–154 and four `dexpace.redirect.*` keys. | P6b-19 |
| **6c** | `AuthorizationPolicy` (and any replacement resolver) keeps judging "same origin as `context.SeedRequest`" before stamping, withholds otherwise, and skips its HTTPS guard on that withheld path (`AUTH-29`, §10 entry 15). It runs per hop and per attempt. A 401 challenge re-drive stays inside the hop. | `HttpOrigin` for 6c to adopt (optional). The end-to-end leak test, whose rows 6c's `AUTH-29` checklist row may cite. | P6b-12, P6b-25, P6b-29 |
| **6c** | 6c's events use ids 160–169. | — | P6b-19 |
| **6a, 6c** | Neither strips or adds `Cookie`, `Proxy-Authorization` or a caller-set `Authorization` on a request outside its own stage's copy. | — | P6b-13 |

---

## Hand-offs to later phases

- **6c:** may move `AuthorizationPolicy.GetOrigin` onto `HttpOrigin`, and corrects its "`Uri.Port` returns -1" comment
  (design §6.2 notes the error).
- **8b:** `TRANSPORT-1`'s conformance case and the borrowed-client follow detection (phase 1 S3) are unchanged.
  `RedirectWireTests` stays green there.
- **9:** binding `RedirectOptions` from configuration needs a string → `Method` conversion for `AllowedMethods` (`Method.Of`).
  `Predicate` is code-only, set with `PostConfigure`, never bound. `IValidateOptions` may add cross-property rules (none are
  known).
- **10/12 (release):** `REDIR-27`'s ⏳ goes to `docs/first-release.md`'s "What v1 ships without" with no named trigger, as
  Ruby recorded it.

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

These are proposals only: this design edits none of these files. Each becomes a dated correction in PR 3, naming the ruling
that causes it.

- **§6.2:**
  - the **As built** line: everything §6.2 lists as missing is built, `REDIR-27` is deferred, and the malformed `Location` is
    logged redacted;
  - the predicate's scope and `RedirectCondition.Target` (P6b-7, P6b-8);
  - the exception family (P6b-16);
  - "held as a `FrozenSet<Method>` copy" is kept as written;
  - the explicit default port elided by `AbsoluteUri` (P6b-30).
- **§11, new items**, numbered at the next free number when they land, since 6a and 6c may add items concurrently:
  - `REDIR-3`'s "original method" taken literally against Node's current-hop reading (P6b-4);
  - `REDIR-20`'s "fully overrides the built-in decision" read as eligibility only (P6b-7);
  - `PIPE-40`'s "non-replayable body" abandon path read as retry's, with redirect's governed by `REDIR-6`/`REDIR-22`(b)
    (P6b-17);
  - two `Location` values read as malformed (P6b-10);
  - `REDIR-28`'s raw malformed-`Location` replaced by a redacted value (P6b-20).
- **§12:** the `REDIR` row's notes. `REDIR-25` is 🚫 by entry 14, `REDIR-27` stays deferred, and the coverage count is
  unchanged.
- **§3.4 (the replay gate):** `BODY-4`'s redirect branch is built as `RedirectBodyNotReplayableException`.
- **Roadmap:** the phase 6 row's `sdk-design refs` cell gains this design's link (appended); a dated status note at close,
  recording the end-to-end test as the convergence exit and naming which of 6a/6c it ran against.
- **3b checklist:** `BODY-4`'s redirect gate and `BODY-5`'s redirect clause cite PR 1's tests.
- **4c checklist:** `PIPE-40` cites the new lifecycle tests and the §11 reading.
- **`CLAUDE.md`:**
  - "What is genuinely unbuilt" drops the redirect rewrite;
  - the `Pipeline/Policies/` layout line names the redirect folder;
  - `Errors/` gains the `RedirectException` family;
  - "Things That Will Bite You" gains one line: redirects follow only GET and HEAD by default, 303 is opt-in, and a downgrade
    or a non-replayable body throws.
- **`docs/sdk-documentation/pipelines.md`:** the `MaxRedirects` sentence links the new `redirect.md`.

---

## Risks and open questions

1. **The default change is loud.** A consumer that relied on POST→GET on a 302 (a form-login flow) now gets the 302. The
   CHANGELOG line and the user page say how to restore a follow: add the method, or set a predicate. Neither restores the
   rewrite itself, which `REDIR-3` forbids.
2. **A caller-set secret in a custom header crosses origins** (position G). The spec names three headers. The user page
   points at `ApiKeyAuthPolicy`.
3. **6a/6c concurrency.** The leak test is written against today's retry and auth constructors. Mitigation: PR 3 lands
   last, and constraint 5 binds whoever changes them afterwards.
4. **Facts 7 to 10 are unverified on this host.** Fact 8 (the empty-host screen) and fact 10 (IDN) each reopen one ruling
   if they come out the other way. The plan's first task runs them.
5. **The knowledge CLI was not run.** The corpus files were read directly, and the plan re-runs the queries (Governing
   documents).

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered.

**Open for the lead:** P6b-2, P6b-3 (the two new names), P6b-4, P6b-7, P6b-8, P6b-10 (two `Location` values), P6b-16,
P6b-19 (the hop level), P6b-21, P6b-25 (the `Security` tag). The rest are taken.

1. **P6b-1 — Row ownership.** 6b owns `REDIR-1`–`REDIR-28` (28) and nothing else. It contributes tests to `BODY-4`, `BODY-5`,
   `PIPE-15`, `PIPE-16`, `PIPE-40`, `XCUT-17`, `HTTP-46`, `AUTH-29`, `OBS-20` and `OBS-39`, whose rows stay with their
   owners. Options: taking `XCUT-17` as a 6b row because 6b completes it (rejected: Phase List row 10 places it).
2. **P6b-2 — No phase 6 segmentation design; the 6a/6b/6c boundaries are conveniences. Open for the lead.** The card names
   the build lists. This census, P6b-1 and the interface table state the split, and no 6b type consumes a 6a or 6c type.
   Options: write one first (rejected: it would restate the card; the lead may still require it).
3. **P6b-3 — `RedirectOptions`: keep `MaxRedirects` (default 3, negative throws) and `AllowHttpsToHttpDowngrade`; add
   `AllowedMethods` (`IReadOnlySet<Method>`, `FrozenSet` copy, default `{GET, HEAD}`), `FollowSeeOther` and `Predicate`;
   remove `StripSensitiveHeadersOnCrossOrigin`; hand-written content equality. Open for the lead on the names
   `FollowSeeOther` and `AllowedMethods`.** Options:
   - the reference's names `MaxHops`, `AllowSchemeDowngrade` and `Follow303` (rejected: a rename edits `AuthHttpsGuardTests`
     for no requirement, and `Follow303` names a number, not a meaning);
   - an `IReadOnlyCollection<Method>` like 5b's lists (rejected: the semantics are a set, and design §6.2 names the frozen
     set).
4. **P6b-4 — Eligibility on the original method, literally. Open for the lead.** Options: Node's current-hop reading
   (rejected: it departs from a capitalised MUST for one convenience follow). "Original" is the first request the policy
   drives.
5. **P6b-5 — The 303 rebuild:** GET from any method, `HEAD` included; no body; `Content-*` removed by case-insensitive prefix
   after the credential strip; exempt from the replay gate; the same rebuild under a predicate's "follow". Options: keep
   `HEAD` as `HEAD`, as RFC 9110 permits (rejected: `REDIR-5` says "regardless"); a fixed `Content-*` list (rejected: Ruby's
   round 3).
6. **P6b-6 — One pure internal `RedirectDecider.Decide`, gates in the order of position B.** Options: keep the logic inline
   in the policy (rejected: `MA0051`, and the matrix would need a pipeline per row).
7. **P6b-7 — The predicate replaces code/method/303 eligibility only. Loop, cap, `Location` screen, downgrade, replay gate
   and hygiene stay unconditional. Open for the lead.** Options: Ruby's reading, where the predicate also overrides loop
   detection (rejected: `REDIR-16` is a MUST with no carve-out, and the snapshot's visited list serves a stricter predicate,
   not a looser one).
8. **P6b-8 — `RedirectCondition`: a public sealed class with an internal constructor; `Response`, `RedirectsFollowed`,
   `VisitedUris` (a fresh list) and `Target` (the policy's own resolution, an extension). Open for the lead on `Target`.**
   Options: a record (rejected: synthesised equality over a live response); omit `Target` (rejected: it invites a second,
   disagreeing parser into a security decision).
9. **P6b-9 — A throwing predicate disposes the current response and propagates unchanged.** Options: wrap it in a typed error
   as `RETRY-40` does for retry (rejected: `REDIR-20` states no conversion); let it leak the response (rejected: `REDIR-22`'s
   reason applies).
10. **P6b-10 — The resolver:**
    - missing, empty or whitespace `Location` is absent (`REDIR-19`);
    - **two or more values are malformed**;
    - otherwise `Uri.TryCreate(current, raw)`, an http(s) scheme, a non-empty `IdnHost` and userinfo dropped, resolved
      once and total;
    - anything `Uri` accepts as relative is followed percent-encoded.

    **Open for the lead on the multi-value rule.** Options: the first value, as Ruby does (rejected: an appended field
    could choose the target); parsing the joined value (rejected: a comma is legal in a URI).
11. **P6b-11 — The visited set:** the userinfo-free `AbsoluteUri`, ordinal, fragment included, seeded with the first request.
    Options: `HashSet<Uri>` (rejected: fact 4); dropping the fragment (rejected: `HTTP-46`'s textual form; the cap bounds
    it).
12. **P6b-12 — An internal `HttpOrigin` (lower scheme, lower `IdnHost`, `Port`) from `context.SeedRequest`, offered to 6c.**
    Options: keep the private string comparison (rejected: `Host` against `IdnHost`, fact 10); make it public (rejected: no
    consumer outside core).
13. **P6b-13 — Strip exactly `Authorization` always, and `Cookie` and `Proxy-Authorization` cross-origin; keep both
    same-origin (`REDIR-10`).** Options: strip cookies always (the MAY; rejected: it breaks same-origin login redirects and
    protects nothing); a configurable strip list (rejected: the MUSTs are not configurable).
14. **P6b-14 — Downgrade judged per hop; rejected with an event and an exception; permitted with a `Warning` event; stripping
    unchanged.** Options: a silent opt-in (rejected: `REDIR-15`'s "surface it observably").
15. **P6b-15 — The replay gate on `IsReplayable` alone, no idempotency; seekable stream bodies follow.** Options: buffer
    automatically (rejected: `REDIR-6` says fail, and `ToReplayableAsync` is the caller's documented step).
16. **P6b-16 — A `RedirectException : SdkException` family with two sealed leaves; redacted URLs in the message, no raw-URL
    properties. Open for the lead.** Options:
    - one type with a reason enum (narrower, but a two-way catch needs a filter);
    - deriving from `ServiceRequestException` (rejected: wrong retry semantics);
    - raw URLs as properties, as Node does (rejected: `XCUT-19` on a destructuring logger).
17. **P6b-17 — Lifecycle per position I; `PIPE-40`'s non-replayable abandon path read as retry's.** Options: return the 3xx
    open on a non-replayable body to satisfy `PIPE-40` literally (rejected: it contradicts `REDIR-6` and `REDIR-22`(b), the
    pillar-specific MUSTs).
18. **P6b-18 — A cancelled token is checked after the superseded response is disposed and before the next drive.** Options:
    leave it to the transport (rejected: one avoidable dispatch).
19. **P6b-19 — Five `http.redirect.*` log events (150–154) through `LoggerMessage.Define` and the `OBS-20` guard; four
    `dexpace.redirect.*` keys; the hop at `Information`; 5c's span event per hop and no new span event. Open for the lead on
    the hop level.** Options: hop at `Debug`; gating by `HttpLoggingOptions.Level` (rejected: the events carry no headers or
    bodies, and `OBS-34` is about bodies).
20. **P6b-20 — The malformed-`Location` event carries the value through `RedactHeaderValue`, not raw.** Stronger than
    `REDIR-28`'s letter, and the letter's own caveat is the reason. Options: raw, as the reference does (rejected: the port
    can redact unparseable text).
21. **P6b-21 — `REDIR-27` ⏳, declined for v1; the header stays `Location`. Open for the lead.** Options: a validated
    `LocationHeader` member, as Node has (cheap, but a MAY with no consumer, and design §12 already lists it as deferred).
22. **P6b-22 — `REDIR-25` 🚫 under §10 entry 14; its documentation clause ✅ by that entry.**
23. **P6b-23 — `REDIR-11` ✅ under §10 entry 15; one matrix row pins that a marker-shaped header has no effect.**
24. **P6b-24 — `RedirectCredentialHygieneTests` is edited in two places, each to an equal or larger claim. No other
    `Security` class is edited.** Options: leave the 303 row failing behind a skip (rejected: constraint 5).
25. **P6b-25 — The end-to-end leak test: a core `[Theory]` over the real redirect, retry and auth policies, plus a loopback
    wire case; both tagged `Security`. Open for the lead on the tag.** Options: `Integration` (rejected: it loses constraint
    5's protection); core only (rejected: the wire proves the transport adds nothing back).
26. **P6b-26 — The matrix is one `[Theory]` over the pure decider, ported row by row from `decide.test.ts` with the five changes
    and the additions of [Tests](#tests-vectors-and-ports). `RedirectPolicyTests` is replaced, with no non-conforming fact
    kept.**
27. **P6b-27 — The `MA0051` waiver is removed. Every method in the rewrite is under 70 lines.**
28. **P6b-28 — Three PRs, each code and tests; PR 3 is the leak test and the close-out, landed as late as practical against
    6a/6c.** Options: one PR (rejected: events and the convergence test review better apart); four, splitting the internals
    from the policy (rejected: dead internal code for one PR).
29. **P6b-29 — The interfaces assumed of 6a and 6c** are the table in
    [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-6a-and-6c).
30. **P6b-30 — `REDIR-13`'s residue: an explicit scheme-default port is elided by `AbsoluteUri`, with origin and wire meaning
    unchanged; stated in the checklist, not asserted.** Options: rebuild the target text by hand to keep `:443` (rejected:
    `Request` holds a `Uri`, whose `AbsoluteUri` elides it again at the transport).

---

## Deviation Ledger

Every entry is a `P6b-n` ruling above that departs from a requirement's letter, reads an ambiguous clause, or decides a
judgement call the lead may reverse. **Open** means the lead has not ruled. **Taken** means the design argues it from an
existing design section, and it lands as built unless reversed.

| ID | Decision | Touches | Kind | State | Route |
|---|---|---|---|---|---|
| P6b-2 | No phase 6 segmentation design | roadmap segmentation rule | process | **open** | roadmap status note (PR 3) |
| P6b-3 | Option names kept and added; `MaxRedirects` 3 with `init` validation | `REDIR-5`, `REDIR-15`, `REDIR-17`, `REDIR-26` | surface judgement | **open** (names) | §6.2 dated correction (PR 3) |
| P6b-4 | Eligibility on the original method, literally | `REDIR-3`, `REDIR-4` | reading (letter kept, against Node) | **open** | new §11 item (PR 3) |
| P6b-7 | The predicate overrides eligibility only | `REDIR-20`, `REDIR-16`, `REDIR-17` | reading of an ambiguous MUST | **open** | new §11 item (PR 3) |
| P6b-8 | `RedirectCondition.Target` added | `REDIR-20` | extension | **open** | §6.2 dated correction (PR 3) |
| P6b-10 | Two `Location` values are malformed | `REDIR-18`, `REDIR-19` | reading | **open** | new §11 item (PR 3) |
| P6b-16 | The exception family; redacted messages; no raw-URL properties | `REDIR-6`, `REDIR-15`, `XCUT-19` | surface judgement | **open** | §6.2 dated correction (PR 3) |
| P6b-17 | `PIPE-40`'s non-replayable abandon path is retry's | `PIPE-40`, `REDIR-6`, `REDIR-22`, `BODY-4` | reading of two MUSTs | taken (`BODY-4` states the split) | new §11 item (PR 3); 4c checklist |
| P6b-19 | Five events, hop at `Information`, ungated by `HttpLoggingOptions.Level` | `REDIR-28`, `OBS-39` | behaviour judgement | **open** (level) | user page; §8.1 note (PR 3) |
| P6b-20 | Malformed `Location` logged redacted | `REDIR-28`, `XCUT-19` | stronger than the letter | taken | new §11 item (PR 3) |
| P6b-21 | `REDIR-27` declined for v1 | `REDIR-27` | MAY not taken | **open** | §12 (already deferred); `first-release.md` |
| P6b-22 | Async follows redirects | `REDIR-25`, `PIPE-32` | existing §10 entry 14 | taken | checklist cites entry 14 |
| P6b-23 | No marker | `REDIR-11`, `AUTH-29` | existing §10 entry 15 | taken | checklist cites entry 15 |
| P6b-25 | The leak tests are tagged `Security` | constraint 4's category list | process | **open** | roadmap status note (PR 3) |
| P6b-30 | An explicit default port is elided | `REDIR-13` | platform residue | taken | §6.2 dated correction (PR 3) |

No ruling leaves a MUST's letter unmet beyond what §10 entries 14 and 15 already record, so no new §10 entry is opened.
P6b-4, P6b-7, P6b-10, P6b-17 and P6b-20 propose §11 items.

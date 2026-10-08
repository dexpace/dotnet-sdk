# Phase 6c — Authentication: Design

**Status:** Draft, for review. Written 2026-10-08 against `main` at `3a1db00` (phases 0 to 5c merged), on branch
`70-phase-6-planning` (issue #70). Brainstormed without a human in the loop: every judgement call the brainstorming skill
would have put to the lead is taken here as a numbered ruling (`P6c-n`) with the options and the rationale, and the
judgement calls are marked **open for the lead**. 6a (retry) and 6b (redirect) are designed in parallel by other authors in
the same working tree; the interfaces this design assumes of them are stated in
[Cross-sub-phase interfaces assumed of 6a and 6b](#cross-sub-phase-interfaces-assumed-of-6a-and-6b). The scope authority is the
roadmap's Phase 6 card and Phase List row 6 (`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`). The format follows
the phase 5 designs (`docs/work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration-design.md` and its 5b and 5c siblings).

**What this document is.** The sub-phase design for 6c: one disposition per requirement row (38 rows, `AUTH-1`–`AUTH-38`),
the public surface 6c adds or changes, the internal types behind it, the breaking changes, the hand-offs from phases 1, 3b,
4a, 4c and 5a that 6c takes or declines, the pull-request segmentation, the `Security` classes that must stay green, the
issues it closes (#7 and #2), and the rulings, which double as the deviation-ledger IDs (roadmap constraint 7).

**What this document is not.** It is not the plan and not the checklist. It does not restate design §6.3's mapping, §6.2's
seed-origin argument or §10 entries 15 and 16 (roadmap constraint 9); it cites them and records a decision against each row.
It does not design 6b's redirect rewrite or the end-to-end credential-leak test (6b owns it), 6a's retry engine, the
transport's handling of a proxy's `407` (8b), or DI registration of an auth policy (phase 9). It edits no design chapter,
roadmap cell or `CLAUDE.md` line: the corrections it owes are listed as proposals in
[Corrections owed at close-out](#design-roadmap-and-claudemd-corrections-owed-at-close-out).

---

## Prerequisites

Each ordering is stated as a **dependency** or a **convenience** (roadmap "How Phases Get Executed", step 2).

| Predecessor or sibling | Kind | State at `3a1db00` |
|---|---|---|
| Phase 0 gates (warnings as errors, `RS0016`/`RS0017`, `RS0030` over `BannedSymbols.txt`, `MA0051`, `CA2007`, `IsAotCompatible`, the AOT smoke consumer, `TestCategoryTests`) | **dependency** | Met. The one new `RS0030` pragma (the background-launch helper, P6c-25) is the site `BannedSymbols.txt` already reserves ("the one background-launch helper of design §5.4, which does not exist yet"). |
| Phase 1, S4 (`AuthHttpsGuardTests`) and S5 (`UrlRedactionDefaultDenyTests`, `XCUT-19`(d) left to 6c) | **dependency** (constraint 5) | Met. S4's guard is in `AuthorizationPolicy`; 6c types its exception and edits the class without weakening it (P6c-8). S5's credential-formatting clause is 6c's new `Security` class (P6c-20). |
| 2a (`Headers`, `HttpHeaderName`, `HttpHeaderSyntax`) | **dependency** | Met. Outbound header grammar (`XCUT-18`) is what the Digest echo check and the key-credential check call (P6c-19, P6c-33). |
| 3b (`RequestBody.IsReplayable` reported truthfully, `BODY-4` ⏳ 6a/6b/6c) | **dependency** | Met. The `AUTH-31` gate reads `IsReplayable`; 6c closes `BODY-4`'s auth-replay share. |
| 4a (`BoundedMap`, `XCUT-14`) | **dependency** | Met. 4a's hand-off "6c builds the `AUTH-19` nonce counter and moves `AccessTokenCache` onto `BoundedMap`" is taken (P6c-28, P6c-35). |
| 4b (`Disposal`, `ExceptionFacts`, the suppressed trail) | **dependency** | Met. A hook failure disposes the `401` through `Disposal` with the hook's exception as primary (`AUTH-32`). |
| 4c (`PipelineContext.SeedRequest`, `PipelineRunner` as the fork primitive, the single `Auth` pillar, `SyncPath`) | **dependency** | Met. The seed origin (§10 entry 15) is what `AUTH-29` compares against; the replay is a second `continuation.RunAsync` (`AUTH-30`); 4c's hand-off "6c gives `AccessTokenCache` a sync path, retiring `AuthorizationPolicy.GetCredential`'s bridge" (P4c-13) is taken (P6c-9, P6c-26). |
| 5a (`TimeProvider` discipline, the clock bans, `RequestOptions` as a sealed record, the options-record rule) | **dependency** | Met. The cache reads only `TimeProvider.GetUtcNow` (`CFG-16`); no `Task.Delay` (5a's hand-off). `RequestOptions` gains two members under 5a's rule (P6c-6). |
| 5b (`CallState.Logger`, the event-id range 160–169 reserved for auth, the emission-guard pattern) | **dependency** | Met. The one auth event is id 160 (P6c-37). |
| 5c (the per-call transmission counter behind `http.request.resend_count`) | **convenience** | Met. A challenge replay goes through the `Diagnostics` stage below `Auth`, so it is a new attempt span and counts as a resend with no 6c code (fact 15). |
| A phase 6 segmentation design | **convenience**, with a stated substitute | Absent. The card names each sub-phase's build list; this census and P6c-1/P6c-2 stand in, as P5a-1/P5a-2 did. **Open for the lead** (P6c-2). |
| **6a** (retry) and **6b** (redirect) | **convenience**, both ways | Designed in parallel. 6c reads `SeedRequest` (4c) and nothing 6a or 6b builds; 6b's end-to-end leak test consumes 6c's shipped policies. [Cross-sub-phase interfaces](#cross-sub-phase-interfaces-assumed-of-6a-and-6b). |

**No dependency edge inverts the roadmap's order.** 6c reads nothing 6a or 6b builds; the phase 6 boundaries stay
conveniences, as the roadmap's card says ("independent segments over contracts that 4c has already fixed").

---

## Governing documents, and the phase-start queries

- **Normative.** `docs/product-spec/11-authentication.md` §11.1–§11.4 and appendix C rows `AUTH-1`–`AUTH-38`. Cited because a
  6c type touches them: `XCUT-12` (per-credential lock), `XCUT-14` (bounded maps), `XCUT-16` (HTTPS-only credentials),
  `XCUT-18` (outbound header grammar), `XCUT-19`(d) (credentials never reveal secrets in string form), `XCUT-21` (CSPRNG for
  security identifiers), `BODY-4` (replayability gates), `REDIR-7`, `REDIR-11`, `REDIR-24` (the seed origin and stage order),
  `PIPE-5` (one policy per pillar), `PIPE-28` (the real sync path), `CTX-19` (background work must not pin a call).
- **Design.** §6.3 (the whole of authentication), §6.2 (the seed origin), §5.1 and §5.3 (fork, one implementation for both
  paths), §5.4 (`BoundedMap`, the background-launch helper and flow suppression), §8.1 (the banned flow suppressors),
  §8.3 (the clock), §10 entries 15 (`no cross-origin marker`) and 16 (`digest-md5-platform-dependent`), §11 items 25 (no
  loopback exemption) and 26 (`AUTH-11` versus `AUTH-37`), §12's `AUTH` row (37 of 38 cited; `AUTH-26` unaddressed).
- **Issues.** [#7](https://github.com/dexpace/dotnet-sdk/issues/7) (RFC 7235 challenge parsing, `ChallengeHandler`,
  `BasicChallengeHandler`, `CompositeChallengeHandler`, "wire `BearerTokenAuthPolicy` to re-acquire once on a `401` carrying a
  challenge"; BCL crypto only, trim/AOT-safe) and [#2](https://github.com/dexpace/dotnet-sdk/issues/2) (RFC 7616 Digest with
  MD5, MD5-sess, SHA-256, SHA-256-sess, `qop=auth`, `nc`/`cnonce`, one re-auth round trip; "additive — no public API changes to
  the auth surface"). Both read with `gh issue view` on 2026-10-08; neither has comments.
- **Styleguide.** `csharp/06-types-and-data-modeling.md` 6.1 (records), 6.2 (sealed), 6.8 (explicit enum values);
  `csharp/08-error-handling.md` (BCL argument exceptions, SDK exceptions for SDK failures); `csharp/09-concurrency.md` 9.1 (no
  blocking on async), 9.6 (no fire-and-forget); `csharp/10-api-design.md` 10.1 (internal by default), 10.5 (token last). The
  overlay's `I`-prefix and `Async`-suffix departures stand (`IChallengeHandler`, `GetAsync`).
- **Siblings.** `ruby-sdk` is local with its `gems/` tree (earlier .NET phases found the tree absent; it is present now):
  `gems/dexpace-core/lib/dexpace/auth/*.rb`, the 25 test files under `gems/dexpace-core/test/dexpace/auth/`,
  `test/support/{auth,challenge}_fixtures.rb`, `docs/sdk-documentation/auth.md`, and its 6c design
  (`docs/work/mvp/phase6/phase6c/2026-09-09-phase6c-authentication-design.md`, rulings R10–R12 and P6-*). `nodejs-sdk@54aeed4`:
  `packages/core/src/auth/*.{ts,test.ts}` (14 modules), `docs/sdk-documentation/auth.md`, and its auth design
  (`docs/work/mvp/phase5/phase5c/2026-07-26-phase5c-auth-design.md`).

**The knowledge queries could not be run on this host.** `scripts/knowledge` is a `dotnet run` wrapper and the host has only
the muxer (`/usr/bin/dotnet` reports "No .NET SDKs were found", 2026-10-08). The step-1 reading was done on the corpus files
directly, as 5a did:

| Query (intended) | Done instead | Result |
|---|---|---|
| `--origin note --brief` | read `docs/knowledge/notes/*.md` (seven files) | None about authentication. The `Outcome` notes (closed class hierarchies) do not bind 6c: no closed choice is modelled. The `Async`-suffix note binds `AccessTokenCache.GetAsync`, `OnChallengeAsync`; the `I`-prefix note binds `IChallengeHandler`. |
| `--section conflicts --brief` | every harvested file's `## Conflicts` | `authentication.md`'s section is empty; no styleguide conflict bears on 6c. |
| `--prefix-info AUTH` | appendix C rows 336–373 and `harvested/authentication.md` (77 `AUTH` citations) | 38 IDs (36 MUST, 2 SHOULD: `AUTH-19`, `AUTH-38`); owning chapter `11-authentication.md`. One harvested conclusion is stale: "lower-cases hex manually because … the port targets net8.0" (roadmap D1 made the target `net10.0`; P6c-38). |
| `--gaps AUTH` | the roadmap's gap table, then appendix C itself | No `AUTH` ID is among the gap IDs; every in-scope ID was nevertheless read from appendix C, which carries clauses chapter 11 abbreviates (`AUTH-8`'s "MAY remain visible", `AUTH-11`'s "normalizes a synchronous throw", `AUTH-29`'s "a caller-supplied marker is cleared", `AUTH-31`'s reference note, `AUTH-37`'s "post-eviction challenge path"). |
| `--prefix-info XCUT` (cited only) | appendix C rows `XCUT-12`, `XCUT-14`, `XCUT-16`, `XCUT-18`, `XCUT-19`, `XCUT-21` | read; 6c owns none of them (phase 10's rows). |

The plan re-runs the five queries on a host with the pinned SDK and records any difference; a difference that contradicts a
ruling below reopens that ruling.

---

## Scope and the 38-row census

**6c owns 38 rows: `AUTH-1`–`AUTH-38`** (36 MUST, 2 SHOULD), Phase List row 6's ch.11 cell exactly. The card's "AUTH (36
MUSTs): 5 met, 6 partial, 25 missing" counts MUSTs only. No `RETRY`, `REDIR` or `RECOV` row is 6c's; phase 6's exit is
45 + 28 + 38 = 111 rows plus the 15 carried `RECOV` rows, which are 6a's.

Planned status, one line per ID. ✅ = built and tested in 6c; N/A and 🚫 as the roadmap's constraint 3 legend. "As built"
is the state at `3a1db00`.

| ID | Level | As built | Planned | One line |
|---|---|---|---|---|
| `AUTH-1` | MUST | missing | ✅ | `AuthScheme` enum, exactly `OAuth2`, `ApiKey`, `Basic`, `Digest`, `NoAuth`, explicit values; `NoAuth` is the anonymous sentinel, never written to the wire (P6c-3). |
| `AUTH-2` | MUST | missing | ✅ | `AuthRequirement`: one scheme, scopes and parameters copied to immutable storage at `init`, preserved and never read by the resolver; equality and hash by content (§6.3's P13 on record equality; P6c-4). |
| `AUTH-3` | MUST | missing | ✅ | `AuthDescriptor`: non-empty ordered requirements, copied in, read-only out, `ArgumentException` on empty or a null element, `AllowsAnonymous` iff any `NoAuth` (P6c-4). |
| `AUTH-4` | MUST | missing | ✅ | `AuthResolver.Resolve`: the first present tier of per-call > operation > client is the only one consulted, no fall-through; the tiers are carried on `RequestOptions` and the policy (P6c-5, P6c-6, P6c-7). |
| `AUTH-5` | MUST | missing | ✅ | Within the selected descriptor, the first requirement whose scheme is `NoAuth` or in the available set; no credential inspected — the available set is the policy's declared schemes (P6c-5). |
| `AUTH-6` | MUST | missing | ✅ | All tiers absent → `ArgumentException`; nothing satisfiable → `AuthResolutionException` carrying `Required` (preference order) and `Available` (P6c-5). |
| `AUTH-7` | MUST | missing | ✅ | A `static` pure function over immutable inputs; no field, no cache; concurrency test over one shared descriptor (P6c-5). |
| `AUTH-8` | MUST | partial | ✅ | Every credential type overrides `ToString` with the secret redacted; `AccessToken` keeps value equality (`IEquatable`), the key credentials keep reference equality; the named-key clause is vacuous (P6c-19, P6c-20, P6c-21). |
| `AUTH-9` | MUST | partial | ✅ | `AccessToken.Token` and `ApiKeyCredential.Key` reject null, empty and whitespace (`ThrowIfNullOrWhiteSpace`); the named-key clause is vacuous (P6c-19, P6c-21). |
| `AUTH-10` | MUST | partial | ✅ | `AccessToken.ExpiresOn` becomes `DateTimeOffset?`; `IsExpired(now, margin)` is `ExpiresOn is { } e && now + margin > e` (P6c-21). |
| `AUTH-11` | MUST | met | ✅ | A provider exception propagates when no valid token remains and is never cached (§11 item 26); the async path observes it through the task, a synchronous throw from an `async`-less override included (P6c-23, fact 14). |
| `AUTH-12` | MUST | missing | ✅ | `AuthenticationChallenge.Parse`: hand-written state machine; multiple challenges, quoted commas and `=`, escapes, lower-cased scheme and parameter names, verbatim values, bare scheme, `token68` key (P6c-15). |
| `AUTH-13` | MUST | missing | ✅ | The same parser never throws: blank → empty, malformed → skip to the next top-level comma, unterminated quote → end of input, parameters before a malformed tail kept (P6c-15). |
| `AUTH-14` | MUST | partial | ✅ | `Basic ` + base64(UTF-8 `user:password`) computed once, in `BasicAuthPolicy` (preemptive) and `BasicChallengeHandler` (on a `basic` challenge); `BasicCredential` rejects empty, allows whitespace (P6c-17). |
| `AUTH-15` | MUST | missing | ✅ | `DigestChallengeHandler`: exactly MD5, MD5-sess, SHA-256, SHA-256-sess with `qop=auth` or absent; declines `auth-int`-only and any other algorithm; no `rspauth` check. MD5 rows hold only where the host provides MD5 (§10 entry 16; P6c-30). |
| `AUTH-16` | MUST | missing | ✅ | Satisfiable iff `digest`, realm and nonce present, `auth` token in qop or qop absent, algorithm supported or absent (MD5); chosen by the configured preference, not wire order. A `-sess` challenge without qop is declined (§11 new item; P6c-29, P6c-31). |
| `AUTH-17` | MUST | missing | ✅ | HA1/HA2/response per RFC 7616/2069, lower-case hex via `Convert.ToHexStringLower` (P6c-38). |
| `AUTH-18` | MUST | missing | ✅ | `nc` per server nonce, 1 on first use, `Interlocked.Increment` on reuse, rendered `x8` of the low 32 bits (P6c-35). |
| `AUTH-19` | SHOULD | missing | ✅ | The nonce store is a `BoundedMap` per handler, capacity 1024, drained by the map's loop (P6c-35). |
| `AUTH-20` | MUST | missing | ✅ | `cnonce` = `RandomNumberGenerator.GetHexString(32, lowercase: true)`, 128 bits from the OS CSPRNG (§6.3, `XCUT-21`). |
| `AUTH-21` | MUST | missing | ✅ | UTF-8 under `charset=UTF-8` (case-insensitive), ISO-8859-1 otherwise; a credential Latin-1 cannot represent is declined, not hashed lossily (§6.3; P6c-32). |
| `AUTH-22` | MUST | missing | ✅ | Quoted username, realm, nonce, uri, response, cnonce, opaque with `\`-escaping; bare `qop`, `nc`, `algorithm` in full spelling; digest-uri is the escaped request-target; the qop trio only when qop is negotiated. A non-ASCII username goes out as `username*` (P6c-33, P6c-34). |
| `AUTH-23` | MUST | missing | ✅ | `CompositeChallengeHandler`: an immutable copy of the handlers, first non-null answer wins, order documented as "stronger first" (P6c-16). |
| `AUTH-24` | MUST | missing | ✅ | Handlers are stateless except Digest's counters, which are `Interlocked`; a parallel test over one nonce yields 1..N with no duplicate (P6c-35). |
| `AUTH-25` | MUST | missing | ✅ | `IChallengeHandler.Authorize(challenges, request, proxy)` returns the request with `Authorization` (or `Proxy-Authorization` when `proxy`) set, or `null` when it cannot answer (P6c-13, P6c-16). |
| `AUTH-26` | MUST | met, unreconciled (§12) | ✅ | `ApiKeyAuthPolicy` writes the key into the configured header (default `Authorization`), with `Scheme` as the prefix plus one space; the value is computed and validated once at construction (P6c-19). |
| `AUTH-27` | MUST | met | ✅ | One `Auth` pillar (`PIPE-5`); stage order `Redirect` (200) > `Retry` (300) > `Auth` (500). Pinned by a test, not changed. |
| `AUTH-28` | MUST | met (phase 1, S4) | ✅ | The guard stays first on every credentialed path, now a typed `HttpsRequiredException` naming the policy and the scheme; it also covers a reactive scheme's outbound pass (P6c-8). |
| `AUTH-29` | MUST | met (4c, §10 entry 15) | ✅ | Seed-origin comparison, no marker; a cross-origin hop is forwarded credential-free, unguarded, and its `401` is returned without challenge handling (P6c-12). |
| `AUTH-30` | MUST | missing | ✅ | On `401` with `WWW-Authenticate`, the policy's `OnChallengeAsync`/`OnChallenge` hook is consulted; a replacement is driven once through `continuation` with no further challenge handling; the default hook yields `null` (P6c-9, P6c-10). |
| `AUTH-31` | MUST | missing | ✅ | A replacement whose body is not replayable is not sent; the original `401` is returned undisposed. Same gate on both paths and on the bearer retry (P6c-11). |
| `AUTH-32` | MUST | missing | ✅ | A throwing hook (sync, async, or synchronous throw from the async hook) disposes the `401` before propagating (P6c-10). |
| `AUTH-33` | MUST | missing | ✅ | A `401` without `WWW-Authenticate` is returned unchanged and the hook is not called (P6c-10). |
| `AUTH-34` | MUST | partial | ✅ | `Bearer <token>` from `AccessTokenCache`: refresh margin default 30 s, configurable; lock-free hot read; per-key single-flight (P6c-23, P6c-24). |
| `AUTH-35` | MUST | partial | ✅ | A `default(AccessToken)` (null token) or a token expired at fetch time with no margin is a `TokenProviderException`; a throwing provider propagates and caches nothing (P6c-23). |
| `AUTH-36` | MUST | missing | ✅ | `BearerTokenAuthPolicy.OnChallengeAsync`: only on a `bearer` challenge and a stamped `Authorization`; evict-if-matches on the header string, fetch fresh, one retry, any method (P6c-27). |
| `AUTH-37` | MUST | partial | ✅ | Three zones; the expiring zone stamps the valid token and starts one owned background refresh per key under flow suppression; waiters coalesce; a background failure is logged, never fatal; the post-eviction path fetches genuinely fresh (P6c-23 to P6c-25, P6c-27). |
| `AUTH-38` | SHOULD | met by construction | ✅ | `ProcessAsync` is an `async` method, so the guard, the resolver, the provider and the hook all fail the returned `ValueTask`; pinned by a test that `ProcessAsync` returns before faulting (fact 14). |

**Totals: 38 rows — 38 ✅, 0 N/A, 0 🚫, 0 ⏳.** By level: 36 MUST (all ✅), 2 SHOULD (`AUTH-19`, `AUTH-38`, both ✅). Two ✅ rows
carry a recorded departure: `AUTH-15`/`AUTH-16` (§10 entry 16, the MD5 platform dependency, unchanged from the design) and
`AUTH-16`'s `-sess`-without-qop decline (a new §11 item, P6c-31).

**Rows other phases own on which 6c works** (their checklist rows cite 6c's tests; 6c adds no row for them):

| Row (owner) | 6c's contribution |
|---|---|
| `XCUT-14` (10) | The token cache and the Digest nonce store move onto `BoundedMap` (the card names `XCUT-14` in 6c's build list). |
| `XCUT-16` (10) | The typed guard; the reactive-scheme reading (P6c-8). |
| `XCUT-19`(d) (10; phase 1 S5's 6c remainder) | `CredentialRedactionTests`, a new permanent `Security` class (P6c-20). |
| `XCUT-12`, `XCUT-21` (10) | Per-key semaphore, never global; the cnonce from `RandomNumberGenerator`. |
| `BODY-4` (3b, ⏳ 6a/6b/6c) | The `AUTH-31` gate, auth's third of the hand-off. |
| `PIPE-28` (4c, ✅; ⏳ 8b) | The bearer policy's sync path is real (no bridge); one fewer documented bridge in core (P6c-26). |

---

## As built at `3a1db00`

Read for this design: `src/Dexpace.Sdk.Core/Auth/*.cs` (six files), `Pipeline/Policies/{AuthorizationPolicy,ApiKeyAuthPolicy,
BasicAuthPolicy,BearerTokenAuthPolicy,RedirectPolicy}.cs`, `Pipeline/{PipelineContext,PipelineRunner,PipelineStage,
DexpacePipeline,CallState}.cs`, `Internal/{BoundedMap,Disposal,SyncPath,ProxyResolutionLog}.cs`, `Http/Request/RequestOptions.cs`,
`Diagnostics/{DexpaceLogEvents,HttpLogEmitter,AttemptTelemetry}.cs`, `BannedSymbols.txt`, and the tests
`Auth/{AccessTokenCacheTests,CredentialTypesTests}.cs`, `Pipeline/Policies/{ApiKey,Basic,BearerToken}AuthPolicyTests.cs`,
`Security/{AuthHttpsGuardTests,UrlRedactionDefaultDenyTests,RedirectCredentialHygieneTests}.cs`.

- **`AuthorizationPolicy`** (abstract, `Stage` sealed to `Auth`): compares `GetOrigin(context.SeedRequest.Url)` with the
  request's; cross-origin strips `WithheldHeaderName` and drives; same-origin runs `EnsureHttps` (plain `SdkException` naming
  `GetType().Name` and `'<scheme>'`), then `GetCredentialAsync(PipelineContext)` / `GetCredential` (the latter a
  documented sync-over-async bridge with one `RS0030` pragma, P4c-13), sets the header, drives. One shared
  `ProcessCoreAsync(…, bool async)` serves both paths. No `401` handling.
- **`ApiKeyAuthPolicy`**, **`BasicAuthPolicy`**: stateless after construction; Basic's value precomputed.
- **`BearerTokenAuthPolicy`**: one `AccessTokenCache` per policy over the credential, a fixed `TokenRequestContext(scopes)`;
  async only (the sync path is the base bridge); the remarks defer `401` re-acquisition to "challenge-handling work".
- **`AccessTokenCache`**: a `ConcurrentDictionary<string, CacheEntry>` (unbounded), `volatile TokenHolder`, a
  `SemaphoreSlim(1, 1)` per key, double-checked; valid while `now < ExpiresOn` and before `RefreshOn`; no margin; in the
  "refresh due but still valid" case it awaits the refresh on the request path and, if the refresh throws, returns the old
  token silently; no sync method; no expired-at-fetch check.
- **`AccessToken`**: `readonly struct`, `ExpiresOn` non-nullable, `RefreshOn` nullable, no `IEquatable`, `ToString` = type
  name, `ArgumentNullException` on null token only.
- **`ApiKeyCredential`**: rejects null and empty, accepts whitespace; `Scheme` is the prefix; `ToString` = type name.
- **`BasicCredential`**: null checks only; `ToBase64()` public.
- **`TokenRequestContext`**: keeps the caller's `IReadOnlyList<string>` by reference; the cache key is computed at
  construction, so a caller mutating the list afterwards desynchronises `Scopes` from `CacheKey`.
- **`RequestOptions`**: `Timeout`, `MaxRetries`, `Tags`; content equality.
- **`RedirectPolicy`** already strips `Authorization` on every hop and `Cookie`/`Proxy-Authorization` cross-origin against the
  seed (phase 1 S3 + 4c); 6b rewrites the rest.
- **`BannedSymbols.txt`** bans `ExecutionContext.SuppressFlow` and the `UnsafeQueueUserWorkItem` overloads "except inside the
  one background-launch helper of design §5.4, which does not exist yet".

---

## Facts the design rests on

The host has no .NET SDK, so nothing was run for this design. Each fact is **verified by the design** (design §6.3/§5.4,
run on 10.0.401 by its authors), **read** (in the tree), or **to verify in the plan's first task** (a scratchpad program, as
4c and 5a did). A to-verify fact that comes out the other way reopens the ruling that cites it.

| # | Fact | Status | Used by |
|---|---|---|---|
| 1 | A record whose `ImmutableArray<string>` or `string[]` member holds equal contents compares unequal: synthesised equality uses each member's default comparer. | verified, §6.3 | P6c-4 |
| 2 | A `record Cred(string User, string Password)` prints `Cred { User = alice, Password = hunter2 }`. | verified, §6.3 | P6c-20 |
| 3 | `HttpResponseHeaders.WwwAuthenticate` splits challenges but leaves parameters as one raw string; `AuthenticationHeaderValue.TryParse` fails on an unterminated quote. | verified, §6.3 | P6c-15 (why hand-written) |
| 4 | `Convert.ToHexString` is upper-case; `Convert.ToHexStringLower` exists on `net9.0`+, so on `net10.0` (D1). | verified (upper) / to verify (lower, trivially) | P6c-38 |
| 5 | `RandomNumberGenerator.GetHexString(32, lowercase: true)` returns 32 lower-case hex characters from the CSPRNG. | verified, §6.3 | `AUTH-20` |
| 6 | `Encoding.Latin1.GetBytes("é€")` is `E9 3F`: unrepresentable characters become `?` silently. | verified, §6.3 | P6c-32 |
| 7 | On a FIPS-enabled OpenSSL, `MD5.HashData` throws (`CryptographicException`); on the authoring machine it succeeds. Whether `net10.0` exposes a static `MD5.IsSupported` is to verify; if it does, the probe reads it before trying a hash. | published, not reproducible / to verify | P6c-30 |
| 8 | A task started inside `using (ExecutionContext.SuppressFlow())` observes no `AsyncLocal` value; `Activity.Current` (an `AsyncLocal`) is therefore `null` inside it. | verified (first half), §5.4 / to verify (second) | P6c-25 |
| 9 | `Uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)` equals the request-target `SocketsHttpHandler` writes on the wire for the same `Uri` (escapes kept, `/` for an empty path). | to verify (a loopback wire test in `Dexpace.Sdk.Http.SystemNet.Tests`) | P6c-34 |
| 10 | `default(AccessToken).Token` is `null` (a struct's reference field), so a provider returning `default` is `AUTH-35`'s "null token". | read (struct semantics) | P6c-23 |
| 11 | `SemaphoreSlim.Wait(0)` never blocks and returns whether it acquired; `Wait(CancellationToken)` is a blocking wait that is not on the `RS0030` list. | known API / read (`BannedSymbols.txt`) | P6c-24, P6c-25 |
| 12 | `Headers.Set` validates an outbound value against `XCUT-18` (non-ASCII and controls throw `ArgumentException`). | read (2a) | P6c-19, P6c-33 |
| 13 | RFC 2617 §3.5's `Mufasa`/`Circle Of Life` vector with cnonce `0a4f113b` gives response `6629fae49393a05397450978507c4ef1`; Ruby's derived values for the legacy no-qop form (`670fd8c2df070c60b045671b8b24ff02`) and RFC 7616 §3.9.1's inputs under SHA-256 (`9fbf3e22…346e1`) and SHA-256-sess (`a0316f89…51a5c`) are re-derived on .NET (the RFC's printed SHA-256 response has 63 hex digits, an erratum, so it is not a vector). | RFC (genuine) / to re-derive | Tests |
| 14 | An exception thrown synchronously by a callee awaited inside an `async` method faults the method's returned task, so `ProcessAsync` (async) never throws synchronously. | language semantics | `AUTH-11`, `AUTH-38` |
| 15 | `AttemptTelemetry.Begin` takes `CallState.NextTransmission()`, a per-call counter, so a second drive below `Auth` reports `http.request.resend_count` one higher with no change. | read | Prerequisites (5c) |
| 16 | Every `AuthHttpsGuardTests` case builds options with object initializers; `CountingAuthPolicy` overrides `WithheldHeaderName` and `GetCredentialAsync(PipelineContext)`; the first case asserts `ex.GetType() == typeof(SdkException)`. | read | P6c-8, Keeping the `Security` classes green |
| 17 | `BasicAuthPolicyTests.ProcessAsync_EmptyPassword_StampsCorrectly` and `AccessTokenCacheTests.GetAsync_RefreshThrows_WhileStillValid_ReturnsCachedToken` assert behaviour this design changes. | read | Breaking changes 3, 5 |

---

## Argued positions

### A. The descriptor and the resolver (`AUTH-1`–`AUTH-7`; P6c-3 to P6c-7)

Pure data and one pure function, as §6.3 says, in `Dexpace.Sdk.Core.Auth`:

```csharp
public enum AuthScheme { OAuth2 = 0, ApiKey = 1, Basic = 2, Digest = 3, NoAuth = 4 }

public sealed record AuthRequirement
{
    public AuthRequirement(AuthScheme scheme);              // ArgumentOutOfRangeException when !Enum.IsDefined
    public AuthScheme Scheme { get; }
    public IReadOnlyList<string> Scopes { get; init; }      // ImmutableArray copy; [] default; null element rejected
    public IReadOnlyDictionary<string, string> Parameters { get; init; }  // ordinal ImmutableDictionary copy; empty default
    public static AuthRequirement NoAuth { get; }
    // Equals/GetHashCode by Scheme + Scopes (sequence) + Parameters (content)
}

public sealed record AuthDescriptor
{
    public AuthDescriptor(params IEnumerable<AuthRequirement> requirements);  // copied; empty or null element → ArgumentException
    public IReadOnlyList<AuthRequirement> Requirements { get; }               // no init: `with` cannot empty it
    public bool AllowsAnonymous { get; }
    // Equals/GetHashCode by sequence
}

public static class AuthResolver
{
    public static AuthRequirement Resolve(
        AuthDescriptor? perCall, AuthDescriptor? operation, AuthDescriptor? client,
        IReadOnlyCollection<AuthScheme> availableSchemes);
}
```

`Resolve` selects `perCall ?? operation ?? client`; all three null is `ArgumentException` (`AUTH-6`'s first half). Within the
selection the first requirement whose scheme is `NoAuth` or in `availableSchemes` wins (`AUTH-5`). Otherwise it throws
`AuthResolutionException` (in `Dexpace.Sdk.Core.Errors`, an `SdkException`) whose `Required` lists the selected
descriptor's schemes in preference order and whose `Available` lists the available schemes in enum order, so the message is
deterministic (`AUTH-6`). It never consults a lower tier once a higher one is present (`AUTH-4`). No field, no cache:
`AUTH-7` is structural, and a parallel test over one shared descriptor pins it.

**`OAuth2 = 0`, not `NoAuth = 0` (P6c-3).** `default(AuthScheme)` should not be the permissive sentinel: an uninitialised
scheme that meant "anonymous" would silently skip stamping. `0` is `OAuth2`, the first of the set, and every constructor
rejects an undefined value.

**Where the tiers ride (P6c-6, open).** No carrier for operation metadata reaches the pipeline (`SEAM-28`'s operation-id
carrier is still ⏳; `OperationDescriptor.BuildRequest` returns a bare `Request`). The design therefore puts both caller-side
tiers on the record that already travels with every call: `RequestOptions.Auth` (per-call) and `RequestOptions.OperationAuth`
(operation, set by generated code from the operation's security list), Node's shape (`auth-step.ts`: "both per-call slots ride
on the same `RequestOptions`"). The client tier is the auth policy's own descriptor, fixed at construction. `RequestOptions`'
content equality extends to both members (an `AuthDescriptor` compares by value). When `SEAM-28` gains a carrier, the operation
tier may move onto it; that is additive.

**Every auth policy resolves (P6c-7, open).** The base `AuthorizationPolicy` takes the client descriptor and its available
schemes in a protected constructor, resolves per drive, and hands the resolved `AuthRequirement` to its subclass:

| Policy | Client descriptor | Available |
|---|---|---|
| `ApiKeyAuthPolicy` | `[ApiKey]` | `{ApiKey}` |
| `BasicAuthPolicy` | `[Basic]` | `{Basic}` |
| `BearerTokenAuthPolicy` | `[OAuth2 { Scopes = the policy's scopes }]` | `{OAuth2}` |
| `ChallengeAuthPolicy` (new) | `[Digest, Basic]` | `{Digest, Basic}` |
| `MultiSchemeAuthPolicy` (new) | the constructor's descriptor | the non-null members of `AuthCredentials` |

So a per-call `RequestOptions { Auth = new AuthDescriptor(AuthRequirement.NoAuth) }` sends an anonymous call through any
policy, a per-call OAuth2 requirement with other scopes fetches a token for those scopes, and a per-call descriptor naming a
scheme the policy cannot serve fails with `AuthResolutionException` before anything is sent — never silently ignored. A
`NoAuth` resolution stamps nothing, skips the HTTPS guard (`AUTH-28` applies only "where a credential will be attached") and
returns any `401` unchanged (no credential may be attached to an anonymous call). Options considered: the resolver as a
library function only, with no carrier (Ruby: "a release decision no phase has made"; rejected — `AUTH-4`'s tiers would be
unreachable from a pipeline, and the per-call opt-out to anonymous is the commonest real use); resolution only in
`MultiSchemeAuthPolicy` (rejected: a per-call descriptor would be silently ignored by the single-scheme policies).

### B. Credentials (`AUTH-8`–`AUTH-11`, `AUTH-14`, `AUTH-26`; P6c-17 to P6c-22)

| Type | Change | Equality (`AUTH-8`) | `ToString` |
|---|---|---|---|
| `AccessToken` (struct) | `ExpiresOn` → `DateTimeOffset?`; new `AccessToken(string token)` (never expires); `ThrowIfNullOrWhiteSpace(token)`; `IsExpired(DateTimeOffset now, TimeSpan margin)`; `IEquatable<AccessToken>`, `==`/`!=` | value over `Token`, `ExpiresOn`, `RefreshOn` (P6c-21) | `AccessToken { Token = ***, ExpiresOn = …, RefreshOn = … }` |
| `ApiKeyCredential` (sealed class) | `ThrowIfNullOrWhiteSpace(key)`; `Scheme` (the `AUTH-26` prefix) null or non-blank with no whitespace; the stamped value checked against `XCUT-18` once | reference (none written) | `ApiKeyCredential { Key = ***, HeaderName = X-Api-Key, Scheme = … }` |
| `BasicCredential` (sealed class) | non-empty username and password (whitespace allowed, `AUTH-14`); a `:` in the username rejected (RFC 7617 §2) (P6c-17) | reference | `BasicCredential { Username = alice, Password = *** }` |
| `DigestCredential` (new, sealed class) | non-empty username and password; no encoding check at construction (it depends on the challenge's `charset`, P6c-32) | reference | `DigestCredential { Username = alice, Password = *** }` |
| `TokenRequestContext` (struct) | `Scopes` copied to an `ImmutableArray<string>` so `CacheKey` cannot drift (P6c-22) | unchanged | unchanged (scopes and claims are not secrets) |
| `AuthCredentials` (new, sealed record) | `Token`, `ApiKey`, `Basic`, `Digest` (each nullable), `TokenRefreshMargin` (30 s) | record value | hand-written: lists which schemes are configured, never a member's own `ToString` (P6c-20) |

**Redaction is explicit, not inherited (P6c-20).** Design §6.3 found that the class keyword is all that stands between a
credential and a printed secret (fact 2). Every credential type overrides `ToString` by hand, so the redaction holds whatever
the type becomes, and the new `Security` class `CredentialRedactionTests` formats each type — `ToString()`, string
interpolation, and `AuthCredentials` with every member set — and asserts the secret is absent and `***` present. It is the
phase 1 S5 row's `XCUT-19`(d) remainder, permanent under constraint 5.

**No `NamedKeyCredential` (P6c-19, open).** `AUTH-8` and `AUTH-9` name a reference type the .NET port never had. Its one
stamping use, `AUTH-26`'s `SharedAccessKey <key>` example, is `new ApiKeyCredential(key, scheme: "SharedAccessKey")`, and a
second key type that stamps identically would duplicate `ApiKeyCredential` for no requirement. The two named-key clauses are
vacuous in this port and the checklist says so. A consuming SDK that needs a name for signing (Azure's SAS pattern) owns that
type.

**`AUTH-14` versus `AUTH-9` (P6c-17).** `AUTH-9`'s non-blank rule names the bearer token and the key credentials, not the
Basic credential; `AUTH-14` is deliberately laxer (non-empty, whitespace allowed). `BasicCredential` follows `AUTH-14`, which
changes one as-built test (empty password, breaking change 3). Rejecting a `:` in the user-id is RFC 7617 §2's MUST, which no
`AUTH` ID states; it is **open for the lead** because it is stricter than both siblings.

**`AccessToken` equality includes `RefreshOn` (P6c-21, open).** `AUTH-8` says "token+expiry"; `RefreshOn` is the port's
extra field (an Azure-style proactive-refresh hint) and a value type's equality over a subset of its fields would make
`a == b` with observably different values. Two tokens differing only in `RefreshOn` therefore compare unequal.

### C. Challenge parsing (`AUTH-12`, `AUTH-13`; P6c-14, P6c-15)

```csharp
public sealed class AuthenticationChallenge
{
    public AuthenticationChallenge(string scheme, IReadOnlyDictionary<string, string>? parameters = null); // folds names
    public string Scheme { get; }                                        // ASCII lower case
    public IReadOnlyDictionary<string, string> Parameters { get; }       // lower-case keys, verbatim unquoted values
    public string? Token68 { get; }                                      // Parameters["token68"]
    public const string Token68Key = "token68";
    public static IReadOnlyList<AuthenticationChallenge> Parse(string? value);
    public static IReadOnlyList<AuthenticationChallenge> Parse(ReadOnlySpan<char> value);
}
```

A character-level state machine over the RFC 7235 §2.1 grammar (`#challenge`, `auth-scheme [ 1*SP ( token68 / #auth-param ) ]`,
`auth-param = token BWS "=" BWS ( token / quoted-string )`), linear in the input, no regular expression (§6.3), no allocation
beyond the output. The one ambiguity of the grammar — after a comma, is the next token a parameter name or a new scheme? — is
decided by one-token lookahead: a token followed by `BWS "="` and then a non-`=` character is a parameter; otherwise it starts a
new challenge. After the scheme, a run of token68 characters ending in zero or more `=` and then a comma or the end is a
token68; the same run followed by `=` and a value is the first parameter.

Leniency (`AUTH-13`): blank or `null` input returns an empty list; a scheme that is not a token, or a parameter without `=`, or a
value that is neither token nor quoted string, abandons the current element and resumes after the next top-level (unquoted)
comma, keeping the challenge and the parameters parsed so far; an unterminated quoted string ends at the end of input with its
content kept; a trailing backslash is dropped. A duplicate parameter name keeps the **first** value (P6c-15): RFC 7235 says
each name occurs once per challenge, and first-wins means a hostile later duplicate cannot overwrite a realm the handler
already compared. Names fold with an ASCII-only lower-casing (tokens are ASCII by grammar); values are never folded.

**Every `WWW-Authenticate` field is parsed separately (P6c-14).** A server may send one field with several challenges or one
field per challenge (RFC 9110 §11.6.1; RFC 7616 §3.7 recommends the latter for Digest algorithm discovery). The step reads
`response.Headers.GetAll("WWW-Authenticate")` and concatenates the per-field parse results in order. Ruby joins the values with
`", "` first; parsing separately is equivalent on well-formed input and stronger on malformed input, where an unterminated quote
in one field cannot swallow the next field's challenges.

### D. Handlers (`AUTH-14`–`AUTH-25`; P6c-13, P6c-16, P6c-29 to P6c-35)

```csharp
public interface IChallengeHandler
{
    // The request with the answer set under Authorization (or Proxy-Authorization when proxy), or null.
    Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy);
}
public sealed class BasicChallengeHandler : IChallengeHandler { public BasicChallengeHandler(BasicCredential credential); }
public sealed class DigestChallengeHandler : IChallengeHandler
{
    public DigestChallengeHandler(DigestCredential credential);
    public DigestChallengeHandler(DigestCredential credential, IEnumerable<DigestAlgorithm> preference);
    public static IReadOnlyList<DigestAlgorithm> DefaultPreference { get; }   // SHA-256, SHA-256-sess, MD5, MD5-sess
}
public enum DigestAlgorithm { Md5 = 0, Md5Sess = 1, Sha256 = 2, Sha256Sess = 3 }
public sealed class CompositeChallengeHandler : IChallengeHandler
{
    public CompositeChallengeHandler(params IEnumerable<IChallengeHandler> handlers);  // copied; empty or null element rejected
}
```

**One method that returns the stamped request (P6c-16).** `AUTH-23` speaks of a "can-handle check" and `AUTH-25` of emitting
a header name. A `CanHandle` + `Build` pair would select twice (Digest's selection is not free and its counter must advance
once); Ruby collapsed both into one value-returning method. This design goes one step further and returns the replacement
request itself: the handler sets the header whose name its explicit `proxy` flag selects (`AUTH-25` literally), `null` is
"cannot satisfy", and the result is exactly what the step's hook returns (`AUTH-30`). The interface (not an abstract class as
issue #7 sketched) follows the repository's "interfaces for SPIs" rule; the closing comment on #7 says so.

**Basic** answers a `basic` challenge (any realm) with the value precomputed at construction, the same string
`BasicAuthPolicy` stamps preemptively (`AUTH-14`).

**Digest** selects per `AUTH-16` and computes per `AUTH-17`; the decisions beyond the requirement text:

- **Preference (P6c-29, open).** The default walks SHA-256, SHA-256-sess, MD5, MD5-sess — strongest first, RFC 7616 §3.7's
  advice; the spec fixes no default (Ruby's default is the spec's listing order, MD5 first). A caller passes any non-empty
  subset in any order; duplicates are rejected.
- **The MD5 probe (P6c-30; §10 entry 16).** One process-wide `Lazy<bool>`: `MD5.HashData([])` succeeds → available; a
  `CryptographicException` or `PlatformNotSupportedException` → unavailable (fact 7). When unavailable, `Md5`/`Md5Sess` are
  dropped from every handler's effective preference, so an MD5-only challenge is declined and a server offering both falls
  through to SHA-256. Construction never fails on a FIPS host, even with an MD5-only preference: the handler declines
  everything, which the `401` makes visible. An internal constructor takes the availability for tests.
- **`-sess` without qop is declined (P6c-31, open; new §11 item).** `AUTH-16` calls it satisfiable, `AUTH-17` folds the cnonce
  into HA1, and `AUTH-22` forbids sending the cnonce without qop — so the server cannot verify the response. RFC 2617 has the
  same contradiction (cnonce "MUST NOT be specified if the server did not send a qop directive"), and RFC 7616 has no no-qop
  form. Declining avoids a guaranteed `401` round trip and lets the composite fall through. Options: Node sends the cnonce
  anyway (a deviation from `AUTH-22`); Ruby follows `AUTH-22` literally (an unverifiable answer).
- **Encoding (P6c-32).** `charset=UTF-8` (case-insensitive) → UTF-8; otherwise ISO-8859-1, and a credential with a character
  above U+00FF is declined rather than hashed with `?` substituted (fact 6) — design §6.3's choice; Ruby raises a typed error
  instead. The decline is silent at the handler; the caller sees the `401`.
- **Wire form (P6c-33, open).** A username that is not printable ASCII is sent as RFC 7616 §3.4.4's
  `username*=UTF-8''<percent-encoded>` (under `charset=UTF-8`; under Latin-1 it is percent-encoded from its Latin-1 bytes with
  the `ISO-8859-1''` prefix), because the outbound grammar refuses non-ASCII header text (fact 12; Ruby's P6-76). A challenge
  whose realm, nonce or opaque cannot be echoed as outbound header text is declined.
- **digest-uri (P6c-34).** `request.Url.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)`, `/` when the path is
  empty: the request-target the transport writes (fact 9, pinned by a SystemNet loopback test). Never `Uri.ToString()`
  (banned, §3.5).
- **Counter (P6c-35).** Per handler, a `BoundedMap<string, NonceCounter>` of capacity 1024 (`AUTH-19`, `XCUT-14`); `nc` is
  `Interlocked.Increment` on the counter (first use → 1), rendered `((uint)n).ToString("x8")` (`AUTH-18`). A declined attempt
  consumes no count. There is no preemptive Digest: the handler answers a challenge and remembers nothing but counts, so `nc`
  advances only when a server re-issues the same nonce in a later challenge.
- Quoting per `AUTH-22`: `username`, `realm`, `nonce`, `uri`, `response`, `cnonce`, `opaque` quoted with `\` before `"` and
  `\`; `qop=auth`, `nc`, `algorithm` (full spelling: `MD5`, `MD5-sess`, `SHA-256`, `SHA-256-sess`) bare; `algorithm` always
  emitted; `opaque` only when the challenge sent one; no `userhash` (RFC 7616 lets the client choose).

**Composite** (`AUTH-23`): an `ImmutableArray` copy; the first non-null answer wins; callers order stronger first
(`new CompositeChallengeHandler(digest, basic)`).

**Proxy challenges (P6c-13).** The step handles a `401` and `WWW-Authenticate` only (`AUTH-30`'s subject). A forward proxy's
`407` never reaches the pipeline: `SocketsHttpHandler` answers it from `ProxyOptions.ChallengeCredentials` (5a's slot), which 8b
installs. The handlers keep the `proxy` flag so a caller composing its own `407` handling (a CONNECT-less proxy, a test) gets
`Proxy-Authorization` (`AUTH-25`).

### E. The auth step (`AUTH-27`–`AUTH-33`, `AUTH-38`; P6c-8 to P6c-12)

`AuthorizationPolicy` stays the one base class every auth policy derives from, and stays the only thing that can sit at
`PipelineStage.Auth` with `Stage` sealed (`AUTH-27`, `PIPE-5`). Its protected surface changes (P6c-9):

```csharp
public abstract class AuthorizationPolicy : HttpPipelinePolicy
{
    protected AuthorizationPolicy(AuthDescriptor clientDescriptor, IEnumerable<AuthScheme> availableSchemes);
    protected abstract IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; }
    protected abstract ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement, Request request, PipelineContext context);
    protected abstract (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement, Request request, PipelineContext context);
    protected virtual ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge);   // default: null
    protected virtual Request? OnChallenge(AuthChallengeContext challenge);                   // default: null
}

public sealed class AuthChallengeContext   // what the hook sees; constructed only by the base
{
    public AuthRequirement Requirement { get; }
    public Request Request { get; }                          // the request that drew the 401, as stamped
    public Response Response { get; }                        // the 401; the hook must not dispose it
    public IReadOnlyList<AuthenticationChallenge> Challenges { get; }
    public PipelineContext Context { get; }
}
```

The two `GetCredential` members are **both abstract** (P6c-9): the base's sync-over-async default and its `RS0030` pragma
retire, which closes 4c's P4c-13 hand-off; every shipped policy has a real sync path. A `null` credential means "nothing to
attach on this pass" (a reactive scheme). `WithheldHeaderNames` replaces the single `WithheldHeaderName` because
`MultiSchemeAuthPolicy` can write either `Authorization` or an API key's custom header.

The sealed `ProcessCoreAsync(request, context, continuation, async)`, one body for both paths (§5.3), in this order:

1. **Cross-origin first** (`AUTH-29`, §10 entry 15). If the request's origin differs from `context.SeedRequest`'s, remove every
   `WithheldHeaderNames` header, drive once, return the response **without challenge handling** (P6c-12). No guard, no
   credential, no hook: the suppression covers the whole hop, so a server on a foreign origin cannot draw the caller's credential
   by answering `401` — Node's ruling, and `AUTH-36`'s "surface the 401 unchanged when the rejected request carried no
   Authorization header (cross-origin suppression)" generalised to every scheme.
2. **Resolve** (`AUTH-4`–`AUTH-6`): `AuthResolver.Resolve(context.RequestOptions.Auth, context.RequestOptions.OperationAuth,
   clientDescriptor, availableSchemes)`. A failure throws before anything is sent.
3. **`NoAuth` → drive and return** the response unchanged (no guard, no stamp, no hook).
4. **HTTPS guard** (`AUTH-28`, `XCUT-16`): a non-`https` URL (case-insensitive) throws `HttpsRequiredException` before any
   credential is resolved — for a reactive scheme too (P6c-8).
5. **Stamp**: `GetCredential(Async)(requirement, request, context)`; a non-null result is set (replacing any value).
6. **Drive** once through `continuation`.
7. **`401` handling.** Not a `401`, or no `WWW-Authenticate` field → return the response, hook not called (`AUTH-33`).
   Otherwise parse every field (P6c-14) and call `OnChallenge(Async)`. If the hook throws — synchronously, asynchronously, or
   synchronously from the async override — dispose the `401` with `Disposal.DisposeQuietly(response, primary: ex)` and rethrow
   (`AUTH-32`). A `null` replacement returns the `401`.
8. **Replacement checks.** It must be same-origin with the seed (an `InvalidOperationException` after disposing the `401`
   otherwise: a hook that redirects a credential is a programming error, and the HTTPS guard of step 4 then still covers it,
   since same origin means same scheme). Its body, when present, must be replayable; if not, return the original `401`
   **undisposed** — the caller owns it (`AUTH-31`, P6c-11).
9. **Replay**: dispose the `401`, drive the replacement once through the same `continuation` (the fork primitive, §5.1), return
   whatever comes back — no second hook call, whatever its status (`AUTH-30`).

**`AUTH-31` is uniform (P6c-11).** The reference gates only its sync step; the spec asks a port to gate both (a SHOULD inside a
MUST). One body serves both paths here, so the gate cannot drift, and it applies to every replacement, the bearer evict-retry
included — a property of the body, not of which hook built the request (Ruby's reading, adopted).

**`AUTH-38` (P6c-8 and fact 14).** `ProcessAsync` returns `ProcessCoreAsync(…)`, an `async` method, so the resolver's
exception, the guard, a provider failure and a hook failure all fault the returned `ValueTask`. One test calls
`ProcessAsync` on an `http://` URL and asserts it returns a faulted task without throwing.

**The typed guard (P6c-8, open).** `HttpsRequiredException : SdkException` (in `Dexpace.Sdk.Core.Errors`) with
`PolicyName` and `Scheme` properties, and the as-built message. It is not a `ServiceRequestException` and implements no retry
marker, so no retry policy re-drives it (the as-built rule). The guard also runs on the outbound pass of a reactive scheme
(Digest, Basic-on-challenge): `AUTH-28` says "on any request path where a credential will be attached", and a call configured
for Digest will attach one on the first `401`; refusing before the request leaves avoids sending it plaintext to draw a
challenge it may not answer. The cost — a Digest-configured call over `http://` to a server that never challenges now fails —
is a misconfiguration surfaced early, in keeping with §11 item 25's no-exemption stance. Options: guard only before the hook
(rejected: the request has already gone out plaintext and the guard would have to throw from inside the response path with a
`401` to dispose).

### F. The bearer path (`AUTH-34`–`AUTH-37`; P6c-23 to P6c-28)

**The zones.** For a cached token `t`, `now = timeProvider.GetUtcNow()`, margin `M` (default 30 s, `AUTH-34`):

| Zone | Condition | Behaviour |
|---|---|---|
| fresh | `!t.IsExpired(now, M)` and (`t.RefreshOn` is null or `now < t.RefreshOn`) | stamp; no lock taken, no refresh |
| expiring | `!t.IsExpired(now, 0)` but not fresh | stamp `t` **now** and start one background refresh for the key if none is running (`AUTH-37`) |
| expired / missing | `t` absent or `t.IsExpired(now, 0)` | await a foreground fetch, coalesced (`AUTH-34`, `AUTH-37`) |

The cached token's own expiry is evaluated with `AUTH-10`'s "strictly after": at `now == ExpiresOn` the token is still valid.

**Validation of a fetched token (`AUTH-35`; P6c-23).** `default(AccessToken)` (a null `Token`, fact 10) or a token with
`IsExpired(now, TimeSpan.Zero)` at fetch time is a `TokenProviderException` (`Dexpace.Sdk.Core.Errors`, an `SdkException`
whose message never contains the token); nothing is cached. A provider exception propagates unwrapped and is not cached
(`AUTH-11`), so the next request fetches again. The as-built "refresh failed while still valid → return the old token
silently" is replaced by the expiring zone's background refresh, whose failure is logged (P6c-25) — §11 item 26's resolution,
now built as written.

**Coalescing without sharing a cancellation (P6c-24).** The per-key `SemaphoreSlim(1, 1)` stays (fact 11; `XCUT-12`), with a
**fetch generation** on the entry: a waiter records the generation before waiting; after acquiring, if the generation moved, a
fetch finished while it waited and it adopts that outcome — the new token, or the same failure rethrown through
`ExceptionDispatchInfo` — instead of fetching again. So a burst of N requests at expiry costs one fetch, and during a provider
outage N waiters fail together after one attempt rather than serially after N (`AUTH-37`'s "coalesce onto one in-flight
fetch"). A failure is adopted only by waiters that were already waiting: a request arriving after the generation moved fetches
anew, so no failure is cached (`AUTH-35`). One carve-out: a fetch that ended with the fetching caller's own
`OperationCanceledException` is not adopted — the waiters were not cancelled, so the next one fetches. The same code serves the
sync path with `Semaphore.Wait(ct)` and `TokenCredential.GetToken` (P6c-26), so no sync-over-async remains in the policy.
Options: a shared in-flight `Task<AccessToken>` (Node, Ruby; rejected: a sync waiter would have to block on a task, which the
`RS0030` list bans, and the as-built semaphore is already correct and per key).

**The background refresh (P6c-25, open on cancellation).** In the expiring zone the request calls `Semaphore.Wait(0)`: if it
acquires, it launches the refresh and returns the valid token; if not, a fetch is already running and it returns the valid
token. The refresh is started by a new internal `BackgroundWork.Run(Func<Task>)`, the one sanctioned site for
`ExecutionContext.SuppressFlow()` that `BannedSymbols.txt` and design §5.4/§8.1 reserve, so the refresh captures neither the
triggering call's `Activity.Current` nor any other ambient state (`CTX-19`; fact 8). The task is stored on the cache entry
(observed, never fire-and-forget, styleguide 9.6); it always calls `GetTokenAsync` (a background thread has nothing to
gain from the sync method), runs under `CancellationToken.None` (the triggering request may finish first; a provider bounds its
own call), releases the semaphore in a `finally`, advances the generation, and on failure writes one `Warning` event and fails
nothing (`AUTH-37`). Options for cancellation: a cache-lifetime `CancellationTokenSource` (rejected: neither the cache nor the
policy nor the pipeline is disposable today, so nothing would ever cancel it).

**The event (P6c-37).** `DexpaceLogEvents.TokenRefreshFailed = "dexpace.auth.token_refresh_failed"`, id **160** (5b's reserved
auth range), `Warning`, field `error.type`, the exception attached; emitted through an internal `AuthLog` written like 5a's
`ProxyResolutionLog` (a `LoggerMessage.Define` delegate in a non-fatal `try`), to the logger of the call that launched the
refresh (`CallState.Logger`, which lives as long as the pipeline). `AccessTokenCache`'s public methods, used outside a pipeline,
log to `NullLogger`; the policy calls an internal overload that passes the call's logger. No other auth event is added: a
challenge replay is already visible as a second attempt span and a higher `http.request.resend_count` (fact 15).

**`AccessTokenCache` surface.**

```csharp
public sealed class AccessTokenCache
{
    public static TimeSpan DefaultRefreshMargin { get; }                    // 30 s
    public AccessTokenCache(TokenCredential credential, TimeProvider? timeProvider = null);
    public TimeSpan RefreshMargin { get; init; }                            // default DefaultRefreshMargin
    public ValueTask<AccessToken> GetAsync(TokenRequestContext context, CancellationToken ct = default);
    public AccessToken Get(TokenRequestContext context, CancellationToken ct = default);
}
```

*Dated correction, 2026-10-08 (plan R15):* the first draft of this block had a second constructor taking a `TimeSpan refreshMargin`.
Two public overloads with optional parameters trip `RS0026` (and the shorter one `RS0027`) under PublicApiAnalyzers 5.6.0, an
error with `TreatWarningsAsErrors`, so the margin is an `init` property and there is one constructor. The `GetAsync` parameter keeps
its as-built name `ct`.

A negative margin is `ArgumentOutOfRangeException`. Entries live in a `BoundedMap<string, CacheEntry>` keyed by
`TokenRequestContext.CacheKey`, capacity 1024 (P6c-28): the key includes `Claims`, which in a claims-challenge flow comes from
the server, so `XCUT-14` applies (§6.3). Evicting a live entry costs at most one extra fetch, which is the cap's documented
price (`XCUT-14`: "MUST NOT be relied on as the primary cleanup mechanism").

**The bearer `401` (`AUTH-36`; P6c-27).** `BearerTokenAuthPolicy.OnChallenge(Async)` returns `null` unless the stamped request
carries `Authorization` and some challenge's scheme is `bearer`. Otherwise it calls the cache's internal
`GetAfterRejectionAsync(context, rejectedHeaderValue)`, which, under the key's semaphore in one critical section, clears the
cached token only if `"Bearer " + cached.Token` equals the rejected header (ordinal), then returns the cached token if another
request already replaced it (it survives, `AUTH-36`) or fetches a genuinely fresh one (`AUTH-37`'s "post-eviction path"). If the
token obtained would stamp the very header that was rejected (the provider handed back its own cached token), the hook returns
`null` and the `401` surfaces: the retry never re-sends the rejected token. The retry is method-agnostic; the base's `AUTH-31`
gate is what protects a non-replayable body. The `TokenRequestContext` is built from the resolved requirement's scopes (so a
per-call OAuth2 requirement with other scopes gets its own token), falling back to the policy's scopes when the requirement
carries none.

**`BearerTokenAuthPolicy` constructors.** The as-built `(TokenCredential credential, params string[] scopes)` stays; a new
`(AccessTokenCache cache, params string[] scopes)` lets a caller set the margin and the `TimeProvider`, or share one cache
between policies.

### G. Composed policies (P6c-36)

```csharp
public sealed class ChallengeAuthPolicy : AuthorizationPolicy        // Basic/Digest answered on challenge, never preemptive
{
    public ChallengeAuthPolicy(IChallengeHandler handler);
}
public sealed class MultiSchemeAuthPolicy : AuthorizationPolicy      // the descriptor-driven, multi-credential step
{
    public MultiSchemeAuthPolicy(AuthDescriptor clientDescriptor, AuthCredentials credentials, TimeProvider? timeProvider = null);
}
```

`ChallengeAuthPolicy` stamps nothing on the outbound pass and answers a `401` through its handler with `proxy: false`
(`new ChallengeAuthPolicy(new DigestChallengeHandler(credential))` is issue #2's shape; `new CompositeChallengeHandler(digest,
basic)` its fallback).

`MultiSchemeAuthPolicy` is the step a generated SDK installs when its API declares several security schemes: per drive it
resolves the tiers against the schemes `AuthCredentials` configures, then stamps `OAuth2` through an internal bearer stamper (the
same cache and `401` logic as `BearerTokenAuthPolicy`), `ApiKey` and `Basic` preemptively, and `Digest` reactively through a
`DigestChallengeHandler` with the default preference. The single-scheme policies and it share internal stampers, so no logic is
written twice. **Open for the lead:** its alternative is to ship the resolver and the per-call carrier but no multi-credential
policy (a caller composes one by subclassing `AuthorizationPolicy`), which drops PR 7 below.

**What a generated SDK installs, per OpenAPI security scheme:**

| OpenAPI scheme | Policy |
|---|---|
| `http`/`bearer`, `oauth2`, `openIdConnect` | `new BearerTokenAuthPolicy(credential, scopes)` |
| `http`/`basic` | `new BasicAuthPolicy(new BasicCredential(user, password))` — preemptive, no `401` round trip |
| `http`/`digest` | `new ChallengeAuthPolicy(new DigestChallengeHandler(new DigestCredential(user, password)))` |
| `apiKey` in `header` | `new ApiKeyAuthPolicy(new ApiKeyCredential(key, HttpHeaderName.Of("X-Api-Key")))` |
| several of the above | `new MultiSchemeAuthPolicy(clientDescriptor, new AuthCredentials { … })`, with `RequestOptions.OperationAuth` per operation |
| `apiKey` in `query` or `cookie` | not expressible; `AUTH-26` is header-only — recorded for `docs/first-release.md` (P6c-40) |

---

## The public surface (`PublicAPI.Unshipped.txt`)

Core only. The plan confirms the exact lines from the analyzer's code fix. Abbreviations: `A.` = `Dexpace.Sdk.Core.Auth.`,
`E.` = `Dexpace.Sdk.Core.Errors.`, `P.` = `Dexpace.Sdk.Core.Pipeline.Policies.`.

**Removed:**

```text
A.AccessToken.ExpiresOn.get -> System.DateTimeOffset                          (re-added as DateTimeOffset?)
A.AccessToken.AccessToken(string! token, System.DateTimeOffset expiresOn)     (re-added with DateTimeOffset?)
A.AccessToken.AccessToken(string! token, System.DateTimeOffset expiresOn, System.DateTimeOffset? refreshOn)  (likewise)
abstract P.AuthorizationPolicy.WithheldHeaderName.get -> Dexpace.Sdk.Core.Http.Common.HttpHeaderName!
abstract P.AuthorizationPolicy.GetCredentialAsync(Dexpace.Sdk.Core.Pipeline.PipelineContext! context) -> …
virtual P.AuthorizationPolicy.GetCredential(Dexpace.Sdk.Core.Pipeline.PipelineContext! context) -> …
P.AuthorizationPolicy.AuthorizationPolicy() -> void
```

**Added:**

```text
A.AuthScheme (OAuth2 = 0, ApiKey = 1, Basic = 2, Digest = 3, NoAuth = 4)
A.AuthRequirement (sealed record: ctor(AuthScheme); Scheme; Scopes, Parameters .get/.init; static NoAuth; Equals; GetHashCode; the record members)
A.AuthDescriptor (sealed record: ctor(params IEnumerable<AuthRequirement>); Requirements; AllowsAnonymous; Equals; GetHashCode; the record members)
A.AuthResolver.Resolve(AuthDescriptor? perCall, AuthDescriptor? operation, AuthDescriptor? client, IReadOnlyCollection<AuthScheme>! availableSchemes) -> A.AuthRequirement!
A.AuthCredentials (sealed record: Token, ApiKey, Basic, Digest, TokenRefreshMargin .get/.init; ToString)
A.AuthenticationChallenge (ctor; Scheme; Parameters; Token68; const Token68Key; static Parse(string?); static Parse(ReadOnlySpan<char>))
A.IChallengeHandler.Authorize(IReadOnlyList<A.AuthenticationChallenge!>! challenges, Request! request, bool proxy) -> Request?
A.BasicChallengeHandler, A.DigestChallengeHandler (+ DefaultPreference), A.DigestAlgorithm, A.CompositeChallengeHandler
A.DigestCredential (ctor(string! username, string! password); Username; Password; ToString)
A.AccessToken: ctor(string!), ctor(string!, DateTimeOffset?), ctor(string!, DateTimeOffset?, DateTimeOffset?); ExpiresOn -> DateTimeOffset?;
               IsExpired(DateTimeOffset now, TimeSpan margin) -> bool; Equals(AccessToken); Equals(object?); GetHashCode; ToString; ==; !=
A.AccessTokenCache: DefaultRefreshMargin; RefreshMargin.get/.init; Get(TokenRequestContext, CancellationToken) (the as-built ctor(TokenCredential!, TimeProvider?) and GetAsync lines are unchanged)
override A.ApiKeyCredential.ToString(), override A.BasicCredential.ToString()
E.AuthResolutionException (Required, Available; ctor(IReadOnlyList<AuthScheme>, IReadOnlyList<AuthScheme>))
E.HttpsRequiredException (PolicyName, Scheme; ctor(string! policyName, string! scheme))
E.TokenProviderException (ctor(string! message))
P.AuthorizationPolicy: protected ctor(A.AuthDescriptor!, IEnumerable<A.AuthScheme>!); WithheldHeaderNames; GetCredentialAsync(AuthRequirement!, Request!, PipelineContext!);
                       GetCredential(…); OnChallengeAsync(P.AuthChallengeContext!); OnChallenge(P.AuthChallengeContext!)
P.AuthChallengeContext (Requirement, Request, Response, Challenges, Context)
P.ChallengeAuthPolicy (ctor(A.IChallengeHandler!)), P.MultiSchemeAuthPolicy (ctor(A.AuthDescriptor!, A.AuthCredentials!, TimeProvider?))
P.BearerTokenAuthPolicy.BearerTokenAuthPolicy(A.AccessTokenCache! cache, params string![]! scopes)
Dexpace.Sdk.Core.Http.Request.RequestOptions.Auth, .OperationAuth (.get/.init, A.AuthDescriptor?)
Dexpace.Sdk.Core.Http.Common.HttpHeaderName.WellKnown.WwwAuthenticate, .ProxyAuthenticate, .ProxyAuthorization
const Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TokenRefreshFailed, .TokenRefreshFailedId (= 160)
```

The exception types follow `HttpResponseException`'s precedent of a domain constructor rather than the three standard ones;
the plan confirms what the analyzers accept. **Internal:** `AuthLog`, `BackgroundWork`, `ChallengeParser` (the state machine
behind `Parse`), `DigestComputation`, `Md5Availability`, the `NonceCounter` slot, the shared stampers, the cache's
`GetAfterRejectionAsync` and logger-taking overloads, and `AuthOrigin` (the as-built `GetOrigin`, moved so the step and the
replacement check share it).

---

## Breaking changes

Each is marked **Breaking** in the XML docs of the member it changes and gets a `CHANGELOG.md` `[Unreleased]` line in the PR
that makes it (constraint 8).

| # | Change | Kind | PR |
|---|---|---|---|
| 1 | `AccessToken.ExpiresOn` is `DateTimeOffset?` (a token may never expire); the token must be non-blank; equality is by value and `==` is defined; `ToString` redacts | source + behaviour | 2 |
| 2 | `ApiKeyCredential` rejects a whitespace-only key and a blank or whitespace-containing `Scheme`, and a key/scheme the outbound header grammar refuses, at construction (was: per request, or never); `ToString` redacts | behaviour | 2 |
| 3 | `BasicCredential` rejects an empty username or password and a `:` in the username (was: null only); `ToString` redacts | behaviour | 2 |
| 4 | `TokenRequestContext.Scopes` is a copy (was: the caller's list) | behaviour | 2 |
| 5 | `AccessTokenCache`: refreshes 30 s before expiry by default (was: at expiry); stamps a still-valid token and refreshes in the background (was: awaited the refresh on the request path); a failed refresh while valid is logged (was: silent); rejects a default or already-expired provider token; waiters share one fetch's outcome; bounded | behaviour | 5 |
| 6 | `AuthorizationPolicy`'s protected surface: a constructor taking the client descriptor and available schemes; `WithheldHeaderNames`; `GetCredential(Async)` take the resolved requirement and the request and may return `null`, and both are abstract (the sync bridge is gone); `OnChallenge(Async)` hooks | source | 4 |
| 7 | The HTTPS refusal is `HttpsRequiredException` (was: `SdkException`; still assignable to it) | behaviour | 4 |
| 8 | Every auth policy honours `RequestOptions.Auth`/`OperationAuth` (a per-call `NoAuth` sends anonymously; an unservable scheme throws `AuthResolutionException`) and answers a `401` per its hook; `BearerTokenAuthPolicy` retries once on a `401` with a `Bearer` challenge | behaviour | 4, 5 |
| 9 | `RequestOptions` equality includes the two descriptors | behaviour | 1 |

Additive, with no **Breaking** marker: the descriptor types, the resolver, the challenge model and handlers, `DigestCredential`,
`AuthCredentials`, `ChallengeAuthPolicy`, `MultiSchemeAuthPolicy`, the three well-known header names, the log event. Issue #2's
"additive — no public API changes to the auth surface" holds for Digest itself (PR 6 adds types only); the auth surface's
changes are #7's and the spec's.

---

## Keeping the `Security` classes green

Constraint 5: green, or moved without weakening.

| Class | Edit | Why |
|---|---|---|
| `AuthHttpsGuardTests` (S4) | `CountingAuthPolicy` re-signatured (the new constructor, `WithheldHeaderNames`, the requirement-taking `GetCredential(Async)`, still counting calls); the first case's `Assert.Equal(typeof(SdkException), ex.GetType())` becomes `typeof(HttpsRequiredException)`, and it gains `Assert.Equal("http", ex.Scheme)`. Every other assertion unchanged. | Fact 16. The exact-type assertion is strengthened, not loosened; the other `ThrowsAsync<SdkException>` cases become `ThrowsAsync<HttpsRequiredException>` (also stronger). Recorded in the checklist's S4 row (P6c-8). |
| `UrlRedactionDefaultDenyTests` (S5) | None | 6c touches no URL redaction. S5's `XCUT-19`(d) remainder is the new `CredentialRedactionTests`. |
| `RedirectCredentialHygieneTests`, SystemNet `RedirectWireTests` (S3) | None from 6c | Neither uses an auth policy; 6b owns their rewrite. (*Dated correction, 2026-10-08:* the class that does build pipelines with `BasicAuthPolicy(new BasicCredential("u", "p"))`, valid under P6c-17, is `ReDriveRequestIsolationTests`, also edit-free.) |
| `RetryPacingOverflowTests`, `ReDriveRequestIsolationTests`, `EnsureSuccessErrorMappingTests`, `HeaderInjectionValidationTests`, `MediaTypeTryParseTests`, SystemNet `FramingHeaderDropWireTests`, `HeaderInjectionWireTests`, `MalformedContentTypeWireTests` | None | 6c touches none of their subjects. |

**Added:** `CredentialRedactionTests` (`Security`, core), the phase 1 S5 row's `XCUT-19`(d) clause.

---

## Migration plan from the as-built code

| Change | Sites | Rewrite |
|---|---|---|
| Base template | `AuthorizationPolicy.cs` | constructor, resolution, typed guard, `401` lifecycle; `GetOrigin` → internal `AuthOrigin`; the bridge pragma deleted |
| Single-scheme policies | `ApiKeyAuthPolicy`, `BasicAuthPolicy`, `BearerTokenAuthPolicy` | pass their descriptor and scheme to the base; move to the requirement-taking signatures; bearer gains its sync path and `OnChallenge(Async)` |
| Cache | `AccessTokenCache.cs` | zones, margin, generation, background refresh, `BoundedMap`, `Get`, internal rejection and logger overloads |
| Credentials | `AccessToken`, `ApiKeyCredential`, `BasicCredential`, `TokenRequestContext` | as section B |
| Tests that assert changed behaviour | `BasicAuthPolicyTests.ProcessAsync_EmptyPassword_StampsCorrectly` (→ the constructor throws), `AccessTokenCacheTests.GetAsync_RefreshThrows_WhileStillValid_ReturnsCachedToken` (→ the expiring zone stamps the valid token, the background failure is logged), `GetAsync_PastRefreshOn_RefreshesOnce` (→ background), every `new AccessToken(...)` reading `ExpiresOn` as non-nullable, `BearerTokenAuthPolicyTests.Process_sync_stamps_through_the_documented_bridge` (→ the real sync path) | rewritten with the new expectation, never deleted without a replacement |
| `BannedSymbols.txt` | the flow-suppression comment line | "which does not exist yet" → "`Internal/BackgroundWork.cs`"; no line removed |
| `HttpHeaderName.WellKnown` | three names | additions |
| Docs | `src/Dexpace.Sdk.Core/README.md` (auth samples), user page `docs/sdk-documentation/auth.md` | close-out PR |

---

## PR segmentation

Each step is one pull request carrying its code **and** its tests (the one-PR allowance every phase since 2a used), with its
`PublicAPI.Unshipped.txt` diff and `CHANGELOG.md` line.

| PR | Content | Rows | Gate |
|---|---|---|---|
| **1** | `AuthScheme`, `AuthRequirement`, `AuthDescriptor`, `AuthResolver`, `AuthResolutionException`; `RequestOptions.Auth`/`OperationAuth` | `AUTH-1`–`AUTH-7` | none |
| **2** | Credentials (section B), `DigestCredential`, `AuthCredentials`; `CredentialRedactionTests` | `AUTH-8`–`AUTH-10`; `AUTH-14`/`AUTH-26` credential halves; `XCUT-19`(d) | none |
| **3** | `AuthenticationChallenge` and its parser; `IChallengeHandler`, `BasicChallengeHandler`, `CompositeChallengeHandler`; the three header names; `tests/vectors/auth/challenges.json` | `AUTH-12`, `AUTH-13`, `AUTH-14` (handler), `AUTH-23`, `AUTH-25` | none |
| **4** | The `AuthorizationPolicy` template and `401` lifecycle, `AuthChallengeContext`, `HttpsRequiredException`, `ChallengeAuthPolicy`; the three single-scheme policies moved onto it; `AuthHttpsGuardTests` edited | `AUTH-26` (policy), `AUTH-27`–`AUTH-33`, `AUTH-38` | 1, 2, 3 |
| **5** | `AccessTokenCache` rework, `TokenProviderException`, `BackgroundWork`, `AuthLog` and event 160, `BearerTokenAuthPolicy`'s sync path and `401` hook; **closes #7** | `AUTH-11`, `AUTH-34`–`AUTH-37` | 4 |
| **6** | `DigestAlgorithm`, `DigestChallengeHandler`, the MD5 probe, the nonce store, `tests/vectors/auth/digest.json`, the SystemNet digest-uri wire test; **closes #2** | `AUTH-15`–`AUTH-22`, `AUTH-24` | 3 (handler SPI); 4 for the end-to-end replay test |
| **7** | `MultiSchemeAuthPolicy` over the shared stampers (P6c-36) | `AUTH-4`/`AUTH-5` end to end | 1, 4, 5, 6 |
| **8** | Close-out: AOT smoke (a challenge parse, a Digest answer with SHA-256, a bearer stamp under NativeAOT), `docs/sdk-documentation/auth.md`, the 6c checklist, the dated design corrections, `CHANGELOG.md`, `CLAUDE.md` and README drift, the roadmap status note, the closing comments on #7 and #2 naming their checklist rows | all 38 (closing) | 1–7 |

PRs 1, 2 and 3 are independent of each other; 6 can proceed beside 4 and 5. The likely conflicts with 6a and 6b are in
`PublicAPI.Unshipped.txt`, `CHANGELOG.md`, `DexpaceLogEvents.cs` (6a and 6b add ids 140–159), `HttpHeaderName` (6b may add
`ProxyAuthorization` or `Cookie`), `RequestOptions` (if 6a adds a per-call retry member) and `BannedSymbols.txt`; whichever
lands second re-derives its hunks on the merged file rather than resolving them (the 2a/2b precedent).

---

## Tests, vectors and ports

- **Ruby ports** (`ruby-sdk/gems/dexpace-core/test/dexpace/auth/`, read 2026-10-08), each to the .NET class that owns the
  subject: `scheme_test`, `requirement_test`, `descriptor_test`, `resolver_test` → `AuthDescriptorTests`, `AuthResolverTests`;
  `bearer_token_test`, `key_credential_test`, `password_credential_test`, `provider_error_test`,
  `unencodable_credential_error_test` → `CredentialTypesTests` and `CredentialRedactionTests` (the unencodable cases become
  Digest declines, P6c-32); `challenge_test`, `challenges_test` → `AuthenticationChallengeTests` over the JSON vectors;
  `basic_handler_test`, `digest_handler_test`, `challenge_handler_chain_test` → the three handler test classes;
  `key_stamper_test` → `ApiKeyAuthPolicyTests`; `bearer_stamper_test`, `async_bearer_stamper_test`, `bearer_provider_test` →
  `AccessTokenCacheTests`; `step_test`, `async_step_test`, `step_bearer_challenge_test`, `pillar_integration_test`,
  `https_required_error_test` → `AuthorizationPolicyChallengeTests`, `BearerTokenAuthPolicyTests`, `AuthHttpsGuardTests` (a
  Ruby case already covered by S4 is not duplicated). Ruby's sync/async step pairs become one `[Theory]` over both paths.
  `cross_origin_convergence_test` is 6b's (the end-to-end leak test). `matrix_facts_test.rb` is skipped (the card).
- **Fixtures.** `test/support/challenge_fixtures.rb`'s eight strings become `ChallengeFixtures` constants in the core test
  project; `auth_fixtures.rb`'s requests, closable responses and scripted pipelines map onto `TestSupport`'s existing
  `ScriptedTransport`, `TestResponses` and a `TrackingResponseBody` that counts disposals (added to `TestSupport` if absent).
- **Node ports** (`nodejs-sdk@54aeed4 packages/core/src/auth/`): `challenge.test.ts` and `digest.test.ts` cases merge into the
  JSON vectors (`tests/vectors/auth/challenges.json`: input, expected challenges; `digest.json`: challenge, credential,
  method, URL, fixed cnonce, expected header), each case citing its source; `bearer-cache.test.ts`, `auth-step.test.ts`,
  `composing-handler.test.ts`, `resolve.test.ts` for cases Ruby lacks. `md5.test.ts` is not ported (Node's own MD5; .NET uses
  the BCL's, constraint 10). Where a sibling's expectation differs from a ruling here (Ruby's Latin-1 raise, Node's `-sess`
  cnonce, Ruby's default preference), the vector records the .NET expectation and a `note` naming the ruling.
- **Vectors** are loaded through `TestSupport`'s `VectorFile`, as 5a's are.
- **Concurrency.** `AUTH-24`: 64 parallel `Authorize` calls on one nonce yield `nc` 1..64 with no duplicate. `AUTH-34`/`AUTH-37`:
  N concurrent `GetAsync` at expiry over a gated credential → one fetch; the same with a failing credential → one fetch, N
  failures; a waiter whose own token is cancelled leaves the fetch running. Driven by `FakeTimeProvider` and
  `TaskCompletionSource` gates, never by sleeps.
- **Background refresh.** A test asserts the refresh task sees `Activity.Current == null` while the triggering call ran under
  an activity (fact 8), and that a failing refresh emits event 160 once and the triggering response is unaffected.
- **Wire.** `DigestWireTests` in `Dexpace.Sdk.Http.SystemNet.Tests` (the core tests may not reference a transport, SEAM-2):
  a loopback server answers `401` with a Digest challenge, then checks the `uri` parameter equals the request-target it
  received, for a path with `%2F`, a query with `%26`, and an empty path (fact 9).
- **AOT.** PR 8 extends the smoke consumer.
- **Every test** carries a `Category` trait: `Unit`, except `CredentialRedactionTests` (`Security`), `DigestWireTests`
  (`Integration`) and the smoke checks (`AotSmoke`).

---

## Cross-sub-phase interfaces assumed of 6a and 6b

Stated so each sibling's design can confirm or reject them; a rejection reopens the 6c ruling named.

| With | 6c assumes | 6c provides | Ruling |
|---|---|---|---|
| **6a** | The retry classifier (`XCUT-6`, `IRetryableError`) treats `HttpsRequiredException`, `AuthResolutionException` and `TokenProviderException` as non-retryable (they are not `ServiceRequestException`s and implement no marker), and a `401` stays a non-retryable status (`RETRY-1`, `CFG-35`). | Three non-retryable exception types; a challenge replay that happens inside one retry attempt. | P6c-8, P6c-23, P6c-41 |
| **6a** | The `Retry` and `Auth` stage numbers and order are unchanged. | — | `AUTH-27` |
| **6b** | `RedirectPolicy` keeps judging cross-origin against `PipelineContext.SeedRequest` and keeps stripping `Authorization` before every re-issue (`REDIR-7`); it sets no marker. | `AuthorizationPolicy` re-stamps a same-origin hop and withholds, unguarded and unchallenged, on a cross-origin one (P6c-12). | §10 entry 15, P6c-12 |
| **6b** | The end-to-end credential-leak test builds its pipelines from the shipped auth policies, not from an `AuthorizationPolicy` subclass, so 6c's protected-surface change does not break it. | Shipped policies with an unchanged public construction for Basic, API key and bearer. | P6c-9 |
| **6b** | If 6b adds `HttpHeaderName.WellKnown.ProxyAuthorization` or `Cookie`, the second to land re-derives. | The same names, if 6c lands first. | — |

---

## Hand-offs to later phases

- **8b:** the transport answers a proxy's `407` from `ProxyOptions.ChallengeCredentials`; 6c's handlers can be adapted to
  `ICredentials` if a consumer needs Digest towards a proxy (P6c-13).
- **9:** DI registration of an auth policy (an `AuthCredentials` bound from configuration is the natural shape; a secret in
  `appsettings.json` is the host's concern).
- **10:** the `XCUT-12`, `XCUT-14`, `XCUT-16`, `XCUT-19`(d), `XCUT-21` rows cite 6c's tests.
- **12 / `docs/first-release.md`:** claims challenges (`WWW-Authenticate: Bearer … claims="…"`, continuous access evaluation)
  are not built: the bearer hook re-fetches with the same `TokenRequestContext`; and `apiKey` in a query string or a cookie has
  no policy (P6c-40).

---

## Design, roadmap and `CLAUDE.md` corrections owed at close-out

Proposals only: this design edits none of these files. Each is a dated correction in PR 8, naming the ruling that causes it.

- **§6.3:** the **As built** line (descriptor and resolver built and carried on `RequestOptions`; credentials redacted
  explicitly; parser, Basic, Digest and composite handlers built; the `401` hook in the base; bearer zones, coalescing,
  background refresh, bounded cache; typed guard). "on the `net8.0` target the port lower-cases" → `Convert.ToHexStringLower`
  on `net10.0` (D1; P6c-38). The Latin-1 decline stands as written (P6c-32). "`MD5`/`MD5-sess` … falling through to `SHA-256`"
  gains the default preference (P6c-29).
- **§5.4 / §8.1:** the background-launch helper is `Internal/BackgroundWork.cs` (P6c-25).
- **§10 entry 16:** unchanged in substance; it gains "probed once per process" (P6c-30).
- **§11, new items** (numbered at the next free number when they land; 6a and 6b may add items concurrently): `AUTH-16`/`AUTH-17`/
  `AUTH-22` cannot all hold for a `-sess` challenge without qop, resolved by declining (P6c-31); the guard on a reactive
  scheme's outbound pass (P6c-8); `AUTH-8`/`AUTH-9`'s named-key credential is vacuous in a port with one key type (P6c-19); the
  cross-origin `401` is not challenge-handled for any scheme (P6c-12).
- **§12:** the `AUTH` row becomes 38 of 38 cited (`AUTH-26` addressed by P6c-19); its notes add P6c-31.
- **Roadmap:** the phase 6 row's `sdk-design refs` cell gains this design's link (appended); a dated status note at close,
  recording #7 and #2 closed with their checklist rows.
- **Earlier checklists:** phase 1's S4 row (the ⏳ "typed exception if 6c wants one" and the seed-origin note closed) and S5 row
  (`XCUT-19`(d) closed); 3b's `BODY-4` (auth's third closed); 4a's hand-off (`AUTH-19` store and token cache on `BoundedMap`,
  closed); 4c's P4c-13 (the auth bridge retired).
- **`BannedSymbols.txt`:** the comment line naming the helper (an edit to a comment, no entry removed).
- **`CLAUDE.md`:** the layout line for `Auth/` (`AuthScheme`, `AuthRequirement`, `AuthDescriptor`, `AuthResolver`,
  `AuthCredentials`, `AuthenticationChallenge`, the handlers, `AccessTokenCache`, the credentials) and `Pipeline/Policies/`
  (`ChallengeAuthPolicy`, `MultiSchemeAuthPolicy`); "What is genuinely unbuilt" drops "the auth resolver with RFC 7235
  challenges and Digest (6c)". `src/Dexpace.Sdk.Core/README.md`: the auth samples.

---

## Risks and open questions

1. **The guard on reactive schemes** (P6c-8) can surface as a new failure for a Digest-configured client over `http://`.
   Mitigation: the message names the policy and the scheme; the lead may narrow the guard to the replay path.
2. **The FIPS behaviour is unverified on this host** (fact 7). The probe's test injects availability; a FIPS CI leg is out of
   scope. The decline path is the same code as an unsupported algorithm's, which is tested.
3. **Coalesced failures share one exception instance** across waiters (P6c-24); its stack trace is appended to by each rethrow,
   as with a faulted `Task` awaited twice. Acceptable; noted in the XML docs.
4. **The background refresh cannot be cancelled** (P6c-25). A hung provider pins one thread-pool continuation per key until it
   returns; the semaphore it holds makes later expired-zone callers wait on it, under their own tokens.
5. **6a/6b concurrency.** Six shared files (PR segmentation). Mitigation: re-derive, never resolve hunks.
6. **The knowledge CLI was not run** (no SDK); the corpus files were read directly and the plan re-runs the queries.

---

## Rulings

Taken by this design in the absence of a human reviewer. Each lists the options considered. **Open for the lead:** P6c-2,
P6c-6, P6c-7, P6c-8, P6c-17, P6c-19, P6c-21, P6c-25, P6c-29, P6c-31, P6c-33, P6c-36. The rest are taken.

1. **P6c-1 — Row ownership.** 6c owns `AUTH-1`–`AUTH-38` (38) and nothing else; `XCUT-12`, `XCUT-14`, `XCUT-16`, `XCUT-19`(d),
   `XCUT-21`, `BODY-4` and `PIPE-28` are contributions cited by their owners' rows. Options: taking `XCUT-14` as a 6c row because
   the card names it (rejected: Phase List row 6 lists ch.11 only and the exit is 111 rows).
2. **P6c-2 — No phase 6 segmentation design; the 6a/6b/6c boundaries are conveniences. Open for the lead.** The card names the
   build lists, this census and P6c-1 state the split, and no 6c type consumes a 6a or 6b type. Options: write one first
   (Ruby did, `2026-09-09-phase6-segmentation-design.md`; rejected here as restating the card).
3. **P6c-3 — `AuthScheme` is a public enum with explicit values, `OAuth2 = 0` … `NoAuth = 4`**, undefined values rejected.
   Options: `NoAuth = 0` (rejected: a permissive default); a closed class hierarchy (rejected: the scheme has no behaviour).
4. **P6c-4 — `AuthRequirement` and `AuthDescriptor` are sealed records with hand-written content equality**; collections copied
   to immutable storage; the descriptor has no `init` member so `with` cannot empty it. Options: classes (rejected: §6.3 and
   styleguide 6.1 name records); synthesised equality (rejected: fact 1).
5. **P6c-5 — `AuthResolver.Resolve` is a static pure function; `AuthResolutionException` carries `Required` (preference order)
   and `Available` (enum order).** Options: an instance resolver (rejected: `AUTH-7`'s statelessness is structural only for a
   static); `Available` in caller order (rejected: a non-deterministic message for a set).
6. **P6c-6 — The per-call and operation tiers ride on `RequestOptions.Auth`/`OperationAuth`; the client tier is the policy's
   constructor descriptor. Open for the lead.** Options: `OperationDescriptor.Auth` (rejected for now: no carrier reaches the
   pipeline until `SEAM-28`'s); `DexpaceClientOptions.Auth` for the client tier (rejected: the policy is the client's auth
   configuration, and a per-call options override would silently replace it); no carrier, as Ruby (rejected: `AUTH-4`'s tiers
   unreachable).
7. **P6c-7 — Every `AuthorizationPolicy` resolves the tiers against its own declared schemes**, so a per-call `NoAuth` is honoured
   by any policy and an unservable per-call scheme fails before sending. **Open for the lead.** Options: resolution only in
   `MultiSchemeAuthPolicy` (rejected: silently ignored descriptors).
8. **P6c-8 — The guard is typed (`HttpsRequiredException`) and also covers a reactive scheme's outbound pass. Open for the
   lead.** `AuthHttpsGuardTests` is edited, strengthened. Options: keep `SdkException` (rejected: S4's own ⏳ invites a type,
   and Ruby ships one); guard only before the hook (rejected: plaintext already sent).
9. **P6c-9 — The base's protected surface is reshaped**: a constructor with the client descriptor and schemes,
   `WithheldHeaderNames`, requirement-taking nullable `GetCredential(Async)` both abstract (retiring P4c-13's bridge and its
   pragma), `OnChallenge(Async)` virtual with a `null` default. Options: a separate `ChallengeHook` delegate (rejected: two
   extension mechanisms; the template method is Azure.Core's `AuthorizeRequestOnChallengeAsync` precedent); keeping the bridge
   as a default (rejected: P4c-13 scheduled its retirement here).
10. **P6c-10 — The `401` lifecycle lives in the sealed base, in the order of section E**, the replay driven once through the
    same `continuation`, a cross-origin replacement rejected, the `401` disposed before a replay and on a hook failure, kept on a
    `null` replacement and on the replayability gate. Options: per-policy handling (rejected: `AUTH-27`'s one step, and drift).
11. **P6c-11 — `AUTH-31`'s gate on both paths and on every replacement, the bearer retry included.** Options: the reference's
    sync-only gate (rejected: the spec's SHOULD and one shared body).
12. **P6c-12 — No challenge handling on a cross-origin hop**, for every scheme. Options: answering a foreign `401` (rejected: it
    would send the credential the seed comparison just withheld).
13. **P6c-13 — The step handles `401`/`WWW-Authenticate` only; a proxy's `407` is the transport's (8b).** Handlers keep the
    `proxy` flag. Options: a `407` branch in the step (rejected: `SocketsHttpHandler` consumes the `407` before the pipeline).
14. **P6c-14 — Each `WWW-Authenticate` field is parsed on its own and the lists concatenated.** Options: join with `", "`
    (Ruby; rejected: a malformed field could swallow the next).
15. **P6c-15 — The hand-written parser** of section C: one-token lookahead, first duplicate wins, ASCII name folding, linear,
    never throws. Options: `AuthenticationHeaderValue` (rejected, fact 3); a regular expression (rejected, §6.3); last
    duplicate wins (rejected: a later value could overwrite a compared realm).
16. **P6c-16 — `IChallengeHandler.Authorize(challenges, request, proxy) -> Request?`**, one method returning the stamped
    replacement. Options: `CanHandle` + `Build` (rejected: double selection, double counting); an abstract class as #7 sketched
    (rejected: the SPI rule); returning the header value (Ruby; rejected: `AUTH-25` asks the handler to choose the name).
17. **P6c-17 — `BasicCredential` is non-empty with whitespace allowed (`AUTH-14`), and rejects `:` in the username (RFC 7617).
    Open for the lead on the colon.** `BasicAuthPolicy` stays preemptive; `BasicChallengeHandler` answers a challenge.
    Options: Basic only on challenge (Node; rejected: OpenAPI `http`/`basic` clients expect the first request authenticated, and
    the as-built policy is preemptive).
18. **P6c-18 — `DigestCredential` is its own type.** Options: reuse `BasicCredential` (rejected: its `AUTH-14` rule and its name
    are Basic's); a shared `PasswordCredential` (Ruby; rejected: renaming a type every Basic caller uses, for no requirement).
19. **P6c-19 — No `NamedKeyCredential`; `ApiKeyCredential.Scheme` is `AUTH-26`'s prefix, validated, and the stamped value is
    computed and grammar-checked once. Open for the lead.** Options: add the type (rejected: it stamps exactly like
    `ApiKeyCredential`); rename `Scheme` to `Prefix` (rejected: churn for a synonym).
20. **P6c-20 — Every credential type overrides `ToString`; `AuthCredentials` prints only which schemes it holds;
    `CredentialRedactionTests` is a new permanent `Security` class.** Options: rely on the class default (rejected, fact 2).
21. **P6c-21 — `AccessToken`: nullable expiry, non-blank token, `IEquatable` over all three fields, `IsExpired` strictly after.
    Open for the lead on `RefreshOn` in equality.** Options: equality over token and expiry only (rejected: unequal values
    comparing equal).
22. **P6c-22 — `TokenRequestContext` copies its scopes.** Options: leave it (rejected: the cache key can drift from `Scopes`).
23. **P6c-23 — The cache's zones, a 30 s configurable margin, refresh at the earlier of `RefreshOn` and `ExpiresOn − margin`,
    `TokenProviderException` for a default or already-expired token, nothing cached on failure**, and the as-built silent
    fallback replaced by the background refresh. Options: keep the silent fallback (rejected: `AUTH-37` and §11 item 26).
24. **P6c-24 — Coalescing by semaphore plus fetch generation**, sync and async alike; a failure is adopted only by waiters already
    waiting; a fetcher's own cancellation is not adopted. Options: a shared in-flight task (rejected: sync waiters would block
    on a task).
25. **P6c-25 — One owned background refresh per key, launched by the internal `BackgroundWork` under `SuppressFlow` (the
    reserved pragma site), on `CancellationToken.None`, failure logged as event 160. Open for the lead on cancellation.**
    Options: refresh on the request path (as built; rejected, `AUTH-37`); fire-and-forget (rejected, styleguide 9.6).
26. **P6c-26 — `AccessTokenCache.Get` is a real sync path**, used by the bearer policy's `GetCredential` and `OnChallenge`.
    Options: keep the bridge (rejected: P4c-13).
27. **P6c-27 — The bearer `401`: evict-if-matches on the header string and fetch in one critical section; a result equal to the
    rejected header yields no retry; any method.** Options: unconditional eviction (rejected: `AUTH-36` preserves another
    request's refresh).
28. **P6c-28 — The token cache is a `BoundedMap` of capacity 1024.** Options: unbounded (rejected, `XCUT-14`); a smaller cap
    (no evidence for one; 1024 matches `AUTH-19`'s default).
29. **P6c-29 — `DigestAlgorithm` enum; default preference SHA-256, SHA-256-sess, MD5, MD5-sess; any non-empty duplicate-free
    subset accepted. Open for the lead.** Options: the spec's listing order as default (Ruby; rejected: RFC 7616 §3.7).
30. **P6c-30 — The MD5 probe runs once per process; on refusal MD5 and MD5-sess are dropped from every effective preference**
    (§10 entry 16). Options: vendor an MD5 (rejected by §6.3); fail construction (rejected: a FIPS host could not build a
    SHA-256-capable handler).
31. **P6c-31 — A `-sess` challenge without qop is declined (new §11 item). Open for the lead.** Options: Node's cnonce anyway;
    Ruby's literal reading.
32. **P6c-32 — A credential Latin-1 cannot represent is declined under a non-UTF-8 challenge** (design §6.3). Options: Ruby's
    typed raise (rejected: the design decided).
33. **P6c-33 — A non-ASCII username goes out as `username*`; a challenge value that cannot be echoed is declined. Open for the
    lead.** Options: decline any non-ASCII username (rejected: RFC 7616 defines the extended form).
34. **P6c-34 — The digest-uri is `GetComponents(PathAndQuery, UriEscaped)`**, pinned by a wire test. Options:
    `PathAndQuery` (rejected: its escaping is not stated against the wire).
35. **P6c-35 — Per-handler `nc` counters in a `BoundedMap` (1024) with `Interlocked.Increment`; no preemptive Digest.**
    Options: a challenge cache for preemptive Digest (rejected: no requirement; both siblings omit it).
36. **P6c-36 — `ChallengeAuthPolicy` and `MultiSchemeAuthPolicy` are added. Open for the lead on `MultiSchemeAuthPolicy`.**
    Options: resolver only (drops PR 7).
37. **P6c-37 — One auth event, id 160 `dexpace.auth.token_refresh_failed`**, through the triggering call's logger. Options: an
    event per challenge replay (rejected: the attempt span and resend count already show it).
38. **P6c-38 — Lower-case hex through `Convert.ToHexStringLower`** (D1 made `net10.0` the only target). Options: lower-case by
    hand (the design's `net8.0`-era text; rejected as drift).
39. **P6c-39 — Eight pull requests**, as [PR segmentation](#pr-segmentation). Options: one PR (rejected: too large to review).
40. **P6c-40 — Claims challenges and query/cookie API keys are not built**, recorded for `docs/first-release.md`. Options: build
    claims (rejected: no `AUTH` ID; it changes the cache key flow).
41. **P6c-41 — The three new exceptions are non-retryable `SdkException`s.** Options: deriving from `ServiceRequestException`
    (rejected: a retry policy would re-drive a refused or misconfigured call).

---

## Deviation Ledger

Every ruling above is a ledger entry under its `P6c-n` ID (roadmap constraint 7). The ones that leave the specification's letter
or the design's text, and where each lands:

| Ruling | Departs from | Lands as |
|---|---|---|
| P6c-30 | `AUTH-15`'s "MUST support exactly" on a host without MD5 | §10 entry 16 (existing, unchanged in substance) |
| P6c-31 | `AUTH-16`'s satisfiability for `-sess` without qop | new §11 item (a contradiction between `AUTH-16`, `AUTH-17` and `AUTH-22`) |
| P6c-19 | `AUTH-8`/`AUTH-9`'s named-key clauses | new §11 item (vacuous with one key type) |
| P6c-8 | a stricter reading of `AUTH-28` | new §11 item |
| P6c-12 | `AUTH-30`'s "MUST consult" on a cross-origin hop, read under `AUTH-29` | new §11 item |
| P6c-38 | design §6.3's `net8.0` hex text | dated correction to §6.3 |
| P6c-17 (colon) | stricter than `AUTH-14` | checklist note on `AUTH-14` |

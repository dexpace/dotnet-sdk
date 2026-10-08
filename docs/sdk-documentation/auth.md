# Authentication

**As built by phase 6c, written against source on 2026-10-08** (branch `74-phase-6c-auth`). This page describes how
`Dexpace.Sdk.Core` authenticates a call after phase 6c: the descriptor and its three tiers, the credential types, the
five shipped policies, the `401` lifecycle in the authorization base, the token cache, Digest, and the lenient challenge
parser. The requirement IDs it cites (`AUTH-1` to `AUTH-38`) are the normative text
(`docs/product-spec/`, appendix C); the decisions behind each type are in the
[phase 6c design](../work/mvp/phase6/phase6c/2026-10-08-phase6c-auth-design.md) and in
[design §6.3](../sdk-design-dotnet/06-retry-redirect-and-authentication.md). Neither is restated here.

Ruby's `gems/dexpace-core/lib/dexpace/auth/` and Node's `packages/core/src/auth/` (`nodejs-sdk@54aeed4`) were read for the
vectors; this page is written from this repository's source.

## Which scheme a call uses

`AuthScheme` is `OAuth2`, `ApiKey`, `Basic`, `Digest` or `NoAuth` (`AUTH-1`). An `AuthRequirement` is a scheme with optional
scopes and parameters; an `AuthDescriptor` is an ordered, non-empty list of alternatives, strongest preference first
(`AUTH-2`, `AUTH-3`). `AuthResolver.Resolve` picks the first requirement that is `NoAuth` or whose scheme the policy can
serve, from the first tier that is present (`AUTH-4` to `AUTH-7`):

| Tier | Where it rides | Set by |
|---|---|---|
| per call | `RequestOptions.Auth` | the caller of one operation |
| operation | `RequestOptions.OperationAuth` | generated code, from the operation's security list |
| client | the policy's own descriptor | the policy's constructor |

A tier that is present and cannot be satisfied does **not** fall through to a lower one: it throws
`AuthResolutionException` (required and available schemes, no scopes) before anything is sent. `default(AuthScheme)` is
`OAuth2`, never the permissive `NoAuth`. A per-call `new AuthDescriptor(AuthRequirement.NoAuth)` sends the call anonymously
through any auth policy, skips the HTTPS guard, and returns a `401` unchanged.

```csharp
var options = new RequestOptions { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2) { Scopes = ["files.read"] }) };
using var response = await pipeline.SendAsync(request, options, ct);
```

## The credentials

| Type | Rules | `ToString` |
|---|---|---|
| `AccessToken` (struct) | token non-blank; `ExpiresOn` is `DateTimeOffset?` (`null` never expires, `AUTH-10`); equality over token, expiry and refresh hint | `AccessToken { Token = ***, … }` |
| `ApiKeyCredential` | key non-blank; `Scheme` null or non-blank without whitespace; the stamped value is checked against the outbound header grammar once, at construction (`AUTH-26`, `XCUT-18`) | key redacted |
| `BasicCredential` | username and password non-empty (whitespace allowed, `AUTH-14`); no `:` in the username (RFC 7617 section 2) | password redacted |
| `DigestCredential` | username and password non-empty; no encoding check until a challenge names a charset | password redacted |
| `AuthCredentials` (record) | optional `Token`, `ApiKey`, `Basic`, `Digest`, and a `TokenRefreshMargin` (30 s) | lists the configured schemes only |

Every credential overrides `ToString` by hand, so a log line or an interpolated string never carries a secret (`AUTH-8`,
`XCUT-19`(d)). The `Security` class `CredentialRedactionTests` pins it for all of them. There is no `NamedKeyCredential`: a
prefixed key is `new ApiKeyCredential(key, scheme: "SharedAccessKey")` (P6c-19), so the named-key clauses of `AUTH-8` and
`AUTH-9` are vacuous in this port.

## The policies

All derive from `AuthorizationPolicy`, which owns the lifecycle and is the only thing that can sit at `PipelineStage.Auth`
(`AUTH-27`). The stage order is Redirect, Retry, Auth.

| Policy | Client descriptor | Behaviour |
|---|---|---|
| `ApiKeyAuthPolicy` | `[ApiKey]` | stamps the key in its own header, preemptively |
| `BasicAuthPolicy` | `[Basic]` | stamps `Authorization: Basic …` preemptively, no `401` round trip |
| `BearerTokenAuthPolicy` | `[OAuth2 { Scopes }]` | stamps `Bearer <token>` from an `AccessTokenCache`; retries once on a `Bearer` `401` |
| `ChallengeAuthPolicy` | `[Digest, Basic]` | sends nothing first; answers a `401` through an `IChallengeHandler` |
| `MultiSchemeAuthPolicy` | the constructor's | several credentials at once, chosen per call by the descriptor tiers |

What a generated SDK installs, per OpenAPI security scheme (design G):

| OpenAPI scheme | Policy |
|---|---|
| `http`/`bearer`, `oauth2`, `openIdConnect` | `new BearerTokenAuthPolicy(credential, scopes)` |
| `http`/`basic` | `new BasicAuthPolicy(new BasicCredential(user, password))` |
| `http`/`digest` | `new ChallengeAuthPolicy(new DigestChallengeHandler(new DigestCredential(user, password)))` |
| `apiKey` in `header` | `new ApiKeyAuthPolicy(new ApiKeyCredential(key, HttpHeaderName.Of("X-Api-Key")))` |
| several of the above | `new MultiSchemeAuthPolicy(clientDescriptor, new AuthCredentials { … })`, with `RequestOptions.OperationAuth` per operation |
| `apiKey` in `query` or `cookie` | not expressible; `AUTH-26` is header-only (recorded in `docs/first-release.md`) |

`MultiSchemeAuthPolicy` rejects, at construction, a client descriptor that does not allow anonymous calls and names no scheme
its `AuthCredentials` configures (a pinned reading; a descriptor that is unservable only at call time throws
`AuthResolutionException` instead).

## The `401` lifecycle

For each drive the base class does this, in order, on both the asynchronous and the synchronous path from one body:

1. **Cross-origin first** (`AUTH-29`). If a redirect moved the request off the origin of the seed request, every withheld
   credential header is removed, the request is driven once and the response returned **without** challenge handling, so a
   foreign server cannot draw your credential by answering `401`. No guard and no hook run, and no marker is set on the
   context.
2. **Resolve** the requirement (above). 3. **`NoAuth`**: drive and return. 4. **HTTPS guard** (`AUTH-28`): a non-`https` URL
   (case-insensitive) throws `HttpsRequiredException` before any credential is resolved, also for a reactive scheme (Digest)
   whose credential would be attached on the first `401`. There is no loopback exemption. 5. **Stamp** the credential, if the
   policy has one for this pass. 6. **Drive**.
7. **A `401` with a `WWW-Authenticate` field** (`AUTH-33`; any other status or a `401` without the field returns as it is):
   every field is parsed on its own and the policy's hook (`OnChallenge` / `OnChallengeAsync`) is called with an
   `AuthChallengeContext` (`AUTH-30`). If the hook throws, the `401` is disposed first and the exception propagates; a failing
   dispose is attached to it (`AUTH-32`). A `null` replacement returns the `401`.
8. **Checks.** The replacement must be same-origin (otherwise `InvalidOperationException` after disposing the `401`) and its
   body, when present, replayable; a non-replayable body is **not sent** and the original `401` is returned undisposed
   (`AUTH-31`).
9. **Replay.** The `401` is disposed and the replacement driven once through the same continuation; whatever comes back is
   returned, with no second hook call.

`ProcessAsync` reports every failure (resolver, guard, provider, hook) through the returned task, never by throwing
(`AUTH-38`). `HttpsRequiredException`, `AuthResolutionException` and `TokenProviderException` are `SdkException`s that are
never retryable. A subclass overrides `WithheldHeaderNames`, `GetCredential(Async)` (return `null` for "nothing to attach on
this pass") and, optionally, the hooks.

## The token cache

`AccessTokenCache` fronts a `TokenCredential` per `TokenRequestContext.CacheKey` (scopes plus claims), in a bounded map of
1024 keys (`XCUT-14`). For a token and the margin (30 s, `RefreshMargin`):

- **fresh** (not within the margin of its expiry, not past its `RefreshOn`): returned at once, no lock.
- **expiring** (still valid): returned at once, and one background refresh per key is started. The refresh captures no
  ambient state (`BackgroundWork`, the one `ExecutionContext.SuppressFlow` site), runs under no cancellation token, and a failure
  is logged as event 160 `dexpace.auth.token_refresh_failed` at `Warning` with `error.type` and the exception, failing nothing
  (`AUTH-37`).
- **expired or missing**: a foreground fetch, coalesced per key with a semaphore and a fetch generation. N requests at expiry
  cost one fetch; during an outage the N waiters fail together after one attempt (they share one exception instance); a
  request that arrives after the failure fetches again, so no failure is cached; a fetcher's own cancellation is never
  adopted (`AUTH-34`).

A default or already-expired token from the provider is a `TokenProviderException` (never containing the token, `AUTH-35`); a
provider exception propagates unwrapped (`AUTH-11`). `Get` is the real synchronous path (it calls `TokenCredential.GetToken`),
so no thread blocks on a task. The bearer `401` evicts the rejected token only if it is still the cached one, fetches a fresh
one under the same lock, and retries once; if the provider hands back the rejected token, the `401` surfaces (`AUTH-36`).
Claims challenges (continuous access evaluation) are not built: the retry re-fetches with the same `TokenRequestContext`.

## Digest

`DigestChallengeHandler` answers `Digest` challenges (RFC 7616): `MD5`, `MD5-sess`, `SHA-256`, `SHA-256-sess`, with `qop=auth`
or the legacy no-`qop` form (`AUTH-15` to `AUTH-17`).

- **Preference.** The handler walks its preference, not wire order. The default is `SHA-256, SHA-256-sess, MD5, MD5-sess`
  (`DigestChallengeHandler.DefaultPreference`); pass any non-empty, duplicate-free subset.
- **FIPS.** MD5 availability is probed once per process (`net10.0` has no `MD5.IsSupported`, so it tries a hash). On a host that
  refuses MD5 the MD5 algorithms are dropped: an MD5-only challenge is declined, a server offering both gets SHA-256, and
  construction never fails.
- **Declined, never answered wrongly:** no `realm` or `nonce`; only `auth-int`; an unsupported algorithm; a `-sess` algorithm
  without `qop` (its response could not be verified, P6c-31); a realm, nonce or opaque that is not valid outbound header text;
  a credential character the challenge's charset cannot represent. A declined attempt consumes no `nc`.
- **Encodings.** `charset=UTF-8` (any case) hashes UTF-8, otherwise ISO-8859-1. A username that is not printable ASCII goes out
  as `username*=UTF-8''…` (or `ISO-8859-1''…`, RFC 8187). `digest-uri` is the escaped request-target the transport writes
  (verified against the wire). `userhash` is never sent; `rspauth` is not verified.
- **Counters.** `nc` is the atomic count of responses for one nonce, 8 lower-case hex digits, in a bounded 1024-entry map
  (`AUTH-18`, `AUTH-19`, `AUTH-24`); `cnonce` is 128 bits from `RandomNumberGenerator` (`AUTH-20`).

```csharp
var policy = new ChallengeAuthPolicy(new CompositeChallengeHandler(
    new DigestChallengeHandler(new DigestCredential(user, password)),
    new BasicChallengeHandler(new BasicCredential(user, password))));
```

A forward proxy's `407` never reaches the pipeline (the transport answers it, phase 8b); the handlers keep a `proxy` flag so a
caller composing its own proxy handling gets `Proxy-Authorization` (`AUTH-25`).

## The challenge parser

`AuthenticationChallenge.Parse(string?)` (or a `ReadOnlySpan<char>`) reads the RFC 7235 grammar in one linear pass with no
regular expression and never throws (`AUTH-12`, `AUTH-13`). Scheme and parameter names fold to ASCII lower case (never by the
current culture), values are verbatim with quotes stripped and escapes resolved, a `token68` is under the key `token68`, and a
duplicate parameter keeps the **first** value (so a hostile later duplicate cannot overwrite a realm). Malformed text abandons
only the current element: it resumes after the next unquoted comma and keeps what was parsed; an unterminated quote ends at the
end of the input; a trailing backslash is dropped. After a comma, a token followed by `=` and then a character that is not `=`
is a parameter; anything else starts a new challenge.

## What is not built

Claims challenges (continuous access evaluation), `apiKey` in a query string or a cookie, preemptive Digest, a
`NamedKeyCredential`, and `407` handling in the pipeline (P6c-13, 19, 35, 40). They are recorded in
[`first-release.md`](../first-release.md).

## Migrating from the pre-6c surface

| # | Change | Rewrite |
|---|---|---|
| 1 | `AccessToken.ExpiresOn` is `DateTimeOffset?`; the token must be non-blank; equality and `==`; `ToString` redacts | read `ExpiresOn` with `.Value` or a pattern; pass `null` for a token that never expires |
| 2 | `ApiKeyCredential` rejects a whitespace-only key and a bad scheme or value at construction | fix the key; the failure is now at startup |
| 3 | `BasicCredential` rejects an empty username or password and a `:` in the username | encode such a credential differently |
| 4 | `TokenRequestContext.Scopes` is a copy | none |
| 5 | `AccessTokenCache` refreshes 30 s early in the background, logs failures, rejects bad tokens, is bounded | set `RefreshMargin` for the old behaviour (`TimeSpan.Zero` refreshes at expiry) |
| 6 | `AuthorizationPolicy`'s protected surface | derive with the new constructor, `WithheldHeaderNames` and the requirement-taking `GetCredential(Async)` |
| 7 | the HTTPS refusal is `HttpsRequiredException` | catch it (still an `SdkException`) |
| 8 | every policy honours the tiers and answers a `401`; the bearer policy retries once | pass `NoAuth` per call to opt out |
| 9 | `RequestOptions` equality includes the two descriptors | none |

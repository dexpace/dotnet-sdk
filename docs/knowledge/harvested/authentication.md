# authentication

## Rules
- AUTH-1 (MUST) The recognized scheme set is exactly {OAUTH2, API_KEY, BASIC, DIGEST, NO_AUTH}, where NO_AUTH is a distinct sentinel meaning may run anonymously / skip credential stamping rather than a wire scheme.
  <sub>spec · `docs/product-spec/11-authentication.md:7-7` · high · sha:efba58233dd1</sub>
- AUTH-2 (MUST) A requirement binds exactly one scheme to its own OAuth scopes and params (meaningful only for OAUTH2, never inspected by resolution but preserved), is immutable against later mutation of input collections, and has value-based equality over scheme + scopes + params.
  <sub>spec · `docs/product-spec/11-authentication.md:7-7` · high · sha:efba58233dd1</sub>
- AUTH-3 (MUST) A descriptor is a non-empty ordered list of requirements in preference order, rejects an empty list at construction, is immutable, and reports allows-anonymous true iff any requirement's scheme is NO_AUTH.
  <sub>spec · `docs/product-spec/11-authentication.md:7-7` · high · sha:efba58233dd1</sub>
- AUTH-4 (MUST) Tier resolution selects the single most-specific descriptor present in strict order per-call > operation > client and resolves only against it, and a higher tier that is present but unsatisfiable fails rather than falling through to a lower tier because the caller asked for that override explicitly.
  <sub>spec · `docs/product-spec/11-authentication.md:8-8` · high · sha:efba58233dd1</sub>
- AUTH-5 (MUST) Within the selected descriptor resolution returns the first requirement in declared order whose scheme is satisfiable, where satisfiable means NO_AUTH (always) or membership in the supplied set of available schemes, without inspecting any concrete credential.
  <sub>spec · `docs/product-spec/11-authentication.md:8-8` · high · sha:efba58233dd1</sub>
- AUTH-6 (MUST) Resolution fails with an argument error when all tiers are absent, and with a distinct auth-resolution error carrying the required schemes in preference order and the available schemes when the selected descriptor lists no satisfiable scheme.
  <sub>spec · `docs/product-spec/11-authentication.md:8-8` · high · sha:efba58233dd1</sub>
- AUTH-7 (MUST) The auth resolver is stateless, concurrency-safe, and a deterministic pure function of its inputs.
  <sub>spec · `docs/product-spec/11-authentication.md:8-8` · high · sha:efba58233dd1</sub>
- AUTH-8 (MUST) Every credential type redacts its secret in any string/diagnostic representation without mutating or corrupting the real fields, MAY leave non-secret fields visible, and preserves its variant-specific equality.
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- AUTH-8 The bearer token credential has value-based equality over its real token and expiry (the redacted string form does not affect it), while the API-key and name-key credentials use reference identity so two instances with identical fields are not equal.
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- AUTH-9 (MUST) Credential construction validates secret and identity fields as non-blank and rejects blanks (bearer token, API key, name-key name and key).
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- AUTH-10 (MUST) Bearer-token expiry is optional (null means never locally expires) and is evaluated additively with a grace margin: expired at reference time now with margin M iff expiry is non-null and now + M is strictly after expiry.
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- AUTH-11 (MUST) A token provider's fetch errors propagate and are not cached so a subsequent request retries, and async callers observe a provider error through the asynchronous channel (a failed future), never a synchronous throw.
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- AUTH-12 (MUST) The challenge parser parses RFC 7235 WWW-Authenticate/Proxy-Authenticate values into an ordered list of challenges, honoring multiple comma-separated challenges, quoted-string values containing commas and =, backslash escapes, scheme/param names normalized to lower case, values stored verbatim after unquoting, a bare scheme emitted with an empty parameter map, and a token68 value recorded under a synthetic key.
  <sub>spec · `docs/product-spec/11-authentication.md:16-16` · high · sha:efba58233dd1</sub>
- AUTH-13 (MUST) The challenge parser is lenient and never throws: blank input yields an empty list, a malformed challenge recovers to the next top-level comma, an unterminated quoted string terminates at end-of-input, and parameters parsed before a malformed tail are preserved.
  <sub>spec · `docs/product-spec/11-authentication.md:16-16` · high · sha:efba58233dd1</sub>
- AUTH-14 (MUST) Basic stamping produces "Basic " + base64(UTF-8 of username:password) computed once, accepts a Basic challenge case-insensitively, emits Authorization (or Proxy-Authorization for a proxy challenge), and validates credentials as non-empty while permitting whitespace-only per RFC 7617, laxer than the non-blank rule elsewhere.
  <sub>spec · `docs/product-spec/11-authentication.md:17-17` · high · sha:efba58233dd1</sub>
- AUTH-15 (MUST) Digest stamping supports exactly {MD5, MD5-sess, SHA-256, SHA-256-sess} with qop auth (or absent), declining auth-int-only challenges, unsupported algorithms, and mutual-auth verification.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-16 (MUST) A Digest challenge is satisfiable iff the scheme is Digest (case-insensitive), it carries realm and nonce, qop contains auth or is absent, and the algorithm is supported or absent (defaulting to MD5), preferring the algorithm earliest in the configured preference list regardless of wire order.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-17 (MUST) Digest HA1, HA2 and response are computed per RFC 7616/2069 with lower-case hex of the selected algorithm.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-18 (MUST) The Digest nonce count is tracked per server nonce starting at 00000001 and incrementing only on reuse, rendered as exactly 8 lower-case hex digits using the low 32 bits on overflow.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-20 (MUST) The Digest client nonce is drawn from a cryptographically strong source with at least 128 bits of entropy.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-21 (MUST) Digest uses UTF-8 hash-input encoding when the challenge advertises charset=UTF-8 and ISO-8859-1 otherwise.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-22 (MUST) Digest header rendering quotes/escapes the appropriate fields, leaves qop/nc/algorithm unquoted with the full algorithm spelling, uses the request-target as digest-uri, and emits cnonce/nc/qop only when qop is negotiated.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-19 (SHOULD) The per-nonce Digest counter store SHOULD be bounded (default 1024) and drained under the cap, since evicting a live nonce is harmless because its nc restarts at 1, which is spec-legal for a fresh nonce.
  <sub>spec · `docs/product-spec/11-authentication.md:18-18` · high · sha:efba58233dd1</sub>
- AUTH-23 (MUST) Composing handlers delegate to the first handler in declaration order whose can-handle check passes and defensively copy the handler list, and callers order stronger schemes first.
  <sub>spec · `docs/product-spec/11-authentication.md:19-19` · high · sha:efba58233dd1</sub>
- AUTH-24 (MUST) Auth handlers are safe for concurrent invocation, with per-handler mutable counters (e.g. Digest nc) using thread-safe primitives so concurrent reuse of one nonce still yields correct, non-duplicated counts.
  <sub>spec · `docs/product-spec/11-authentication.md:19-19` · high · sha:efba58233dd1</sub>
- AUTH-25 (MUST) A handler emits Authorization for WWW-Authenticate challenges and Proxy-Authorization for Proxy-Authenticate challenges, selected by an explicit proxy flag, and returns no header when it cannot satisfy any offered challenge.
  <sub>spec · `docs/product-spec/11-authentication.md:19-19` · high · sha:efba58233dd1</sub>
- AUTH-26 (MUST) Static key-credential stamping writes the key into the configured header (default Authorization) and, when a prefix is configured, prepends it followed by a single space, with the stamping step stateless after construction.
  <sub>spec · `docs/product-spec/11-authentication.md:19-19` · high · sha:efba58233dd1</sub>
- AUTH-27 (MUST) There is exactly one auth step occupying the single AUTH pillar stage, nested inside both the redirect loop and the retry loop, so auth executes per redirect hop and per retry attempt (redirect wraps retry wraps auth).
  <sub>spec · `docs/product-spec/11-authentication.md:23-23` · high · sha:efba58233dd1</sub>
- AUTH-28 (MUST) On any path where a credential will be attached, the auth step rejects a non-HTTPS request URL (case-insensitive) before any token fetch or header stamping, failing with an error naming the concrete step and the offending scheme, and credentials are never stamped over plaintext.
  <sub>spec · `docs/product-spec/11-authentication.md:23-23` · high · sha:efba58233dd1</sub>
- AUTH-29 (MUST) On a cross-origin redirect re-issue (differing scheme, host or effective port under the RFC 6454 tuple, marked by the redirect step) the auth step does not stamp the caller's credential, strips the internal cross-origin marker so it never reaches the wire, and skips the HTTPS guard so a deliberately-allowed downgrade hop is forwarded credential-free rather than hard-failing.
  <sub>spec · `docs/product-spec/11-authentication.md:24-24` · high · sha:efba58233dd1</sub>
- AUTH-29 (MUST) A same-origin re-issue is re-stamped normally and remains subject to the HTTPS guard, and the suppression mechanism can only suppress stamping, never force a credential to be sent.
  <sub>spec · `docs/product-spec/11-authentication.md:24-24` · high · sha:efba58233dd1</sub>
- AUTH-30 (MUST) On a 401 carrying a WWW-Authenticate header the auth step consults its challenge hook, and if the hook yields a non-null replacement request the step closes the original 401 and drives the replacement through a fresh copy of the downstream chain exactly once with no further challenge handling on the replacement.
  <sub>spec · `docs/product-spec/11-authentication.md:25-25` · high · sha:efba58233dd1</sub>
- AUTH-33 (MUST) A 401 without a WWW-Authenticate header is returned unchanged without consulting the challenge hook.
  <sub>spec · `docs/product-spec/11-authentication.md:25-25` · high · sha:efba58233dd1</sub>
- AUTH-32 (MUST) If the challenge hook throws (or its async future completes exceptionally, or the async hook throws synchronously) the step closes the open 401 response body before propagating.
  <sub>spec · `docs/product-spec/11-authentication.md:25-25` · high · sha:efba58233dd1</sub>
- AUTH-31 (MUST) The 401 re-challenge replay is gated on request-body replayability: if the replacement carries a non-replayable body the step skips the replay, surfaces the original 401 unchanged, and does not close that original response because the caller owns it.
  <sub>spec · `docs/product-spec/11-authentication.md:26-26` · high · sha:efba58233dd1</sub>
- AUTH-34 (MUST) The bearer auth step stamps Authorization Bearer token using a token cached until a configurable refresh margin before expiry (default 30 s), with concurrent requests racing on a missing or expiring token resulting in at most one provider fetch (single-flight) and a non-blocking hot-path read of a valid cached token.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-35 (MUST) The bearer step rejects a null token and a token already expired at fetch time (evaluated with no margin), and does not cache a thrown provider error.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-36 (MUST) On a 401 advertising a Bearer challenge the bearer step evicts only the exact cached token that produced the 401 (matched by the stamped header value) and re-stamps a single retry with a freshly fetched token, preserving a token another request already refreshed.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-36 (MUST) The bearer step surfaces the 401 unchanged when the rejected request carried no Authorization header or the response advertises no Bearer challenge, and fires the eviction-driven retry regardless of HTTP method.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-37 (MUST) The async bearer step implements a three-zone expiry policy without blocking the dispatching thread: fresh (stamp, no refresh), expiring-but-valid (stamp the still-valid token immediately and kick off an off-thread background refresh), expired or missing (await a fresh single-flight fetch).
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-37 (MUST) The async bearer step coalesces concurrent expiring/missing requests onto one fetch, does not cache a failed fetch, and treats a failed background refresh as non-fatal since a valid token was already stamped.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- AUTH-38 (SHOULD) In the async path the HTTPS-guard failure and any hook error SHOULD be delivered through the asynchronous channel (a failed future) rather than synchronously thrown.
  <sub>spec · `docs/product-spec/11-authentication.md:27-27` · high · sha:efba58233dd1</sub>
- XCUT-12 (SHOULD) Hot-path reads of a credential/token cache SHOULD be wait-free (for example a volatile-published read taking no lock while valid).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:29-29` · high · sha:d6123be82c9e</sub>
- XCUT-12 (SHOULD) Token refresh SHOULD be single-flight, so only one concurrent caller fetches an expiring token while the others reuse the result.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:29-29` · high · sha:d6123be82c9e</sub>
- XCUT-12 (MUST) Any lock guarding token refresh MUST be scoped to that cache (per-credential or per-step) so it never serializes unrelated in-flight requests or the global scheduler, and holding that scoped lock across the possibly blocking fetch to enforce single-flight is acceptable and intended.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:29-29` · high · sha:d6123be82c9e</sub>
- A test formats every credential type and asserts the secret is absent, and credential types override ToString() explicitly so the redaction assertion does not depend on the class keyword.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:306-308` · high · sha:27a0a46f35a8</sub>
- The token refresh margin (AUTH-34) is configurable with a default of 30 seconds, and the port refreshes at the earlier of RefreshOn and ExpiresOn minus the margin.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:321-323` · high · sha:27a0a46f35a8</sub>
- The background token refresh is launched under ExecutionContext.SuppressFlow() so it does not capture the triggering request's Activity and ambient state for its lifetime.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:330-331` · high · sha:27a0a46f35a8</sub>
- AUTH-28 and XCUT-16 require the auth step to reject a non-HTTPS URL before any token fetch or header stamping.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:336-338` · high · sha:27a0a46f35a8</sub>
- The HTTPS guard is skipped on a cross-origin hop, where no credential is attached, so a permitted downgrade hop proceeds credential-free (AUTH-29).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:340-341` · high · sha:27a0a46f35a8</sub>
- Challenge-handler composition takes a defensive copy of its handler list and delegates to the first handler that can handle (AUTH-23).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:354-355` · high · sha:27a0a46f35a8</sub>
- The header a challenge handler writes is chosen by an explicit proxy flag (AUTH-25).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:355-355` · high · sha:27a0a46f35a8</sub>
- A challenge handler that cannot answer returns null, never an empty header.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:355-356` · high · sha:27a0a46f35a8</sub>
- The Digest client nonce is RandomNumberGenerator.GetHexString(32, lowercase: true), a CSPRNG draw of 128 bits yielding 32 lower-case hex characters (AUTH-20, XCUT-21), and never Random.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:363-365` · high · sha:27a0a46f35a8</sub>
- The 401 re-challenge hook is consulted only when WWW-Authenticate is present (AUTH-33).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:384-385` · high · sha:27a0a46f35a8</sub>
- The replacement request after a 401 re-challenge is driven once through next (a fork, section 5.1) with no further challenge handling (AUTH-30).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:385-386` · high · sha:27a0a46f35a8</sub>
- The 401 response is disposed if the re-challenge hook throws (AUTH-32).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:386-386` · high · sha:27a0a46f35a8</sub>
- The 401 replay is skipped and the original 401 is returned undisposed when the replacement body is not replayable (AUTH-31).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:386-388` · high · sha:27a0a46f35a8</sub>
- The bearer 401 path (AUTH-36) evicts only the token whose stamped header produced the 401, compared as the header string, so a token another request already refreshed survives.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:389-390` · high · sha:27a0a46f35a8</sub>

## Constraints
- A port must preserve the security invariants (never leak credentials over plaintext or cross-origin, unpredictable Digest cnonce, secret redaction), the deterministic resolution semantics, and the exact challenge/retry lifecycle.
  <sub>spec · `docs/product-spec/11-authentication.md:3-3` · high · sha:efba58233dd1</sub>
- A C# record whose ImmutableArray<string> or string[] member holds equal contents compares unequal to another such record (verified on .NET 10.0.401), because the synthesized Equals uses each member's default comparer, so records do not give value equality over collections.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:296-300` · high · sha:27a0a46f35a8</sub>
- Converting a credential class to a record would leak secrets, because a record such as Cred(string User, string Password) prints as "Cred { User = alice, Password = hunter2 }" (verified), so AUTH-8 redaction is fragile and depends on credentials not being records.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:302-306` · high · sha:27a0a46f35a8</sub>
- HttpResponseHeaders.WwwAuthenticate splits multiple challenges correctly but leaves each challenge's parameters as one raw string with no parameter map (verified on .NET 10.0.401).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:347-350` · high · sha:27a0a46f35a8</sub>
- AuthenticationHeaderValue.TryParse returns false for an unterminated quoted string, whereas AUTH-13 requires recovery with the parameters parsed so far preserved, and it operates on HttpResponseMessage headers, which the SDK's Response is not.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:350-352` · high · sha:27a0a46f35a8</sub>
- Convert.ToHexString returns upper-case hex (verified, "900150983CD24FB0D6963F7D28E17F72" for "abc"), while RFC 7616 and AUTH-17 require lower-case hex.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:360-362` · high · sha:27a0a46f35a8</sub>
- AUTH-21's ISO-8859-1 is Encoding.Latin1, which is lossy without warning (verified: "é€" encodes as E9 3F, the euro sign silently replaced by "?").
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:365-366` · high · sha:27a0a46f35a8</sub>
- .NET has no bundled MD5, so on Linux MD5 is OpenSSL's and on a FIPS-enabled OpenSSL the algorithm is refused inside the provider, whereas Ruby avoids this with Digest::MD5's bundled implementation.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:368-371` · medium · sha:27a0a46f35a8</sub>
- Convert.FromBase64String ignores embedded whitespace (verified: "Y W J j" decodes to "abc") while rejecting other garbage with FormatException, making it stricter than Ruby's unpack1("m") and lenient in exactly one place.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:379-382` · high · sha:27a0a46f35a8</sub>

## Conclusions
- AuthRequirement overrides Equals and GetHashCode with sequence comparison to satisfy AUTH-2's value-based equality over scheme, scopes and params, as does every model record carrying a collection.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:296-300` · high · sha:27a0a46f35a8</sub>
- ApiKeyCredential and BasicCredential are sealed classes, so ToString() prints the type name and they get reference equality by not overriding Equals, which is the variant AUTH-8 assigns them.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:302-305` · high · sha:27a0a46f35a8</sub>
- AccessToken is a readonly struct with value equality over its fields (AUTH-8's bearer variant) and ToString() returning the type name, and it implements IEquatable<AccessToken> to avoid the reflection-based, slow, trimmer-opaque ValueType.Equals.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:308-310` · high · sha:27a0a46f35a8</sub>
- AUTH-9's non-blank rule is implemented with ArgumentException.ThrowIfNullOrWhiteSpace, whereas as built ApiKeyCredential rejects empty but accepts whitespace.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:310-312` · high · sha:27a0a46f35a8</sub>
- AUTH-10 makes token expiry optional, so the port changes AccessToken.ExpiresOn from a non-nullable DateTimeOffset (which forced non-expiring tokens to use DateTimeOffset.MaxValue) to DateTimeOffset?.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:313-314` · high · sha:27a0a46f35a8</sub>
- The as-built AccessTokenCache publishes an immutable TokenHolder through a volatile field, because a multi-field struct cannot be read atomically and boxing it behind one reference makes the unlocked read tear-free.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:317-320` · high · sha:27a0a46f35a8</sub>
- The as-built AccessTokenCache uses a SemaphoreSlim(1, 1) per cache key with a double-checked read after acquisition, which blocks no thread via WaitAsync and is never a global lock.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:319-321` · high · sha:27a0a46f35a8</sub>
- A token provider exception is not cached because the cache holder is written only after the provider returns, so AUTH-11 holds, and its "MUST propagate" is read as applying when no valid token remains (section 11 item 26).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:324-326` · high · sha:27a0a46f35a8</sub>
- The port serves the valid token immediately in the expiring-but-valid zone and starts one owned background refresh per key, stored on the cache entry and observed rather than fire-and-forget.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:328-330` · high · sha:27a0a46f35a8</sub>
- The token cache's per-key dictionary moves onto the BoundedMap of section 5.4 because it is unbounded and its key includes TokenRequestContext.Claims, which comes from the server's WWW-Authenticate in a claims-challenge flow, making XCUT-14 apply.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:331-334` · high · sha:27a0a46f35a8</sub>
- The HTTPS guard goes in AuthorizationPolicy.ProcessAsync before GetCredentialAsync and fails with a non-retryable SdkException naming the policy type and the scheme.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:338-340` · high · sha:27a0a46f35a8</sub>
- The HTTPS guard has no loopback exemption, in keeping with Azure.Core's bearer policy which refuses non-TLS endpoints outright, and local development uses dotnet dev-certs (section 11 item 25).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:341-343` · high · sha:27a0a46f35a8</sub>
- The challenge parser is hand-written as AuthenticationChallenge.Parse(ReadOnlySpan<char>), a character-level state machine, because the grammar is not regular and even RegexOptions.NonBacktracking, which removes ReDoS risk, cannot match balanced quoting with escapes.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:352-354` · high · sha:27a0a46f35a8</sub>
- The port lower-cases hex output manually because Convert.ToHexStringLower arrived in .NET 9 and the port targets net8.0, which is why .editorconfig already dials CA1308 down.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:362-363` · medium · sha:27a0a46f35a8</sub>
- The Digest handler checks the credential against Latin-1 first and declines instead of answering when a username falls outside Latin-1 under a challenge without charset=UTF-8, since otherwise it would produce a wrong digest and a 401 rather than an error.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:366-368` · high · sha:27a0a46f35a8</sub>
- The FIPS MD5 refusal could not be reproduced (no FIPS OpenSSL available, MD5.HashData succeeds locally), so it rests on published platform behaviour.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:370-372` · high · sha:27a0a46f35a8</sub>
- The port does not vendor an MD5 but probes MD5 availability once, and on a host that refuses it the Digest handler treats MD5 and MD5-sess challenges as unsupported, declines them and falls through to SHA-256 if offered, recorded as section 10 entry 16 because AUTH-15 says the handler MUST support exactly all four algorithms.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:372-375` · high · sha:27a0a46f35a8</sub>
- The Digest per-nonce counter (AUTH-18) is an Interlocked.Increment on a boxed counter per nonce, correct under concurrent reuse (AUTH-24), held in a BoundedMap capped at 1024 (AUTH-19, XCUT-14).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:375-376` · high · sha:27a0a46f35a8</sub>
- Sync and async paths share one implementation (section 5.3), so AUTH-31's reference drift between them cannot recur once the 401 handling is built.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:388-389` · high · sha:27a0a46f35a8</sub>

## Reference
- Authentication has two largely independent halves: a scheme-agnostic descriptor/resolver model that decides which auth alternative an operation requires, and a stamping/challenge half that puts credentials on the wire and reacts to server challenges.
  <sub>spec · `docs/product-spec/11-authentication.md:3-3` · high · sha:efba58233dd1</sub>
- 401 eviction and refresh matching is done on the stamped header string rather than credential equality, so value equality is not required for the key credentials.
  <sub>spec · `docs/product-spec/11-authentication.md:12-12` · high · sha:efba58233dd1</sub>
- The default challenge hook yields no replacement request.
  <sub>spec · `docs/product-spec/11-authentication.md:25-25` · high · sha:efba58233dd1</sub>
- The reference implementation enforces the replayability gate only on the synchronous auth step; the async auth step applies no gate and closes the original 401 before re-driving unconditionally, and a faithful port SHOULD apply the same gate on both paths.
  <sub>spec · `docs/product-spec/11-authentication.md:26-26` · high · sha:efba58233dd1</sub>
- The synchronization primitive used for token-cache reads and refresh is non-normative.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:29-29` · high · sha:d6123be82c9e</sub>
- The AUTH-1 to AUTH-7 descriptor/resolver model is not built and is pure data, consisting of an AuthScheme enum, an AuthRequirement record, an AuthDescriptor of requirements in preference order, and a stateless static resolver.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:294-295` · high · sha:27a0a46f35a8</sub>
- As built, BasicCredential checks only for null, whereas AUTH-14 asks for non-empty with whitespace allowed.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:311-312` · high · sha:27a0a46f35a8</sub>
- AUTH-34 and XCUT-12 require a lock-free read of a valid token and single-flight refresh under a lock scoped to one credential.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:316-317` · high · sha:27a0a46f35a8</sub>
- As built, a token is used until RefreshOn or ExpiresOn with no refresh margin, so a request can leave with a token that expires in flight.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:321-323` · high · sha:27a0a46f35a8</sub>
- AUTH-35's rejection of a token already expired at fetch time, evaluated with no margin, is absent from the as-built cache.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:323-324` · high · sha:27a0a46f35a8</sub>
- As built, in AUTH-37's expiring-but-valid zone the cache awaits the refresh on the request path and then serves the old token if the refresh fails, so AUTH-37's three zones are not implemented.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:326-328` · high · sha:27a0a46f35a8</sub>
- The HTTPS guard is missing as built and is called the most important gap in the chapter, since BasicAuthPolicy stamped "Authorization: Basic dTpw" on both attempts to an http:// URL (verified).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:336-338` · high · sha:27a0a46f35a8</sub>
- AUTH-12 and AUTH-13 need an RFC 7235 challenge parser that splits multiple challenges, handles quoted strings with commas and "=", escapes, bare schemes and token68, and never throws.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:345-347` · high · sha:27a0a46f35a8</sub>
- PR #8 deferred the challenge parser and the Basic challenge handler to dexpace/dotnet-sdk#7, so none of the challenge parsing is built.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:356-357` · high · sha:27a0a46f35a8</sub>
- Digest authentication (AUTH-15 to AUTH-22) is deferred as dexpace/dotnet-sdk#2 and needs only the BCL, using the one-shot static hashes MD5.HashData and SHA256.HashData.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:359-360` · high · sha:27a0a46f35a8</sub>
- Basic authentication (AUTH-14) is Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")), computed once per policy, and is built and correct.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:378-379` · high · sha:27a0a46f35a8</sub>
- The client never decodes Basic credentials, and the only decoding paths are test fixtures and a future server-side helper.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:379-381` · high · sha:27a0a46f35a8</sub>
- The AUTH pillar step (AUTH-27 to AUTH-33) runs inside both the retry and redirect loops, as built.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:384-384` · high · sha:27a0a46f35a8</sub>
- The 401 re-challenge handling (AUTH-30 to AUTH-33) is not built.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:384-388` · high · sha:27a0a46f35a8</sub>
- AUTH-11 and AUTH-38's requirement to go "through the asynchronous channel" is satisfied for async methods without extra work (section 5.3).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:390-391` · high · sha:27a0a46f35a8</sub>
- As built (d45e64b), authentication is partial, with api-key, basic and bearer policies, cross-origin credential withholding and a correct wait-free single-flight cache, but missing the HTTPS guard, descriptor/resolver, challenge parsing and handlers, Digest, 401 handling, refresh margin, expired-at-fetch rejection, background refresh, bounded cache, optional expiry and non-blank validation.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:393-396` · high · sha:27a0a46f35a8</sub>

## Conflicts

## Superseded


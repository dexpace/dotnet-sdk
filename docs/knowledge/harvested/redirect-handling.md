# redirect-handling

## Rules
- A redirect is attempted only for HTTP statuses 301, 302, 303, 307, and 308; any other status, including 2xx, 4xx, 5xx, and non-redirect 3xx, is returned verbatim without consulting redirect logic. (REDIR-1, REDIR-2)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:7-7` · high · sha:f2a0d207be56</sub>
- HTTP statuses 300, 304, and 305 must not be auto-followed even when a Location header is present, and 305 in particular must never redirect to a server-chosen proxy. (REDIR-1, REDIR-2)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:7-7` · high · sha:f2a0d207be56</sub>
- For 301 and 302 responses, a redirect is followed only if the original request method is in the configured allowed-method set, which defaults to {GET, HEAD}, and when followed the original method and body are preserved with deliberately no automatic POST-to-GET rewrite. (REDIR-3, REDIR-4, REDIR-5)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- HTTP 307 and 308 redirects preserve method and body and are followed only if the method is in the allowed-method set. (REDIR-3, REDIR-4, REDIR-5)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- HTTP 303 redirects are not followed by default; when opted in, a 303 is re-issued as a GET with the body dropped and every Content-* request header removed case-insensitively, regardless of the original method. (REDIR-3, REDIR-4, REDIR-5)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- Any followed method-preserving redirect (301/302/307/308) re-sends the original request body, so the body must be replayable; if present and not replayable, the operation must fail with a clear error naming replayability rather than corrupting or truncating the re-send, and the redirect is not attempted, since 303 is exempt because it drops the body. (REDIR-6)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:9-9` · high · sha:f2a0d207be56</sub>
- The Authorization header must be stripped before every redirect re-issue, including same-origin redirects and the 303 GET rebuild, because re-attaching a credential for a known origin is the auth layer's job. (REDIR-7, REDIR-8, REDIR-9, REDIR-10)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- A redirect is cross-origin if and only if the resolved target differs from the original (seed) request's origin in scheme, host (case-insensitive), or effective port (using the scheme default when omitted); the comparison must be against the seed origin, not the previous hop, so a same-origin sub-redirect on a foreign host cannot re-expose the credential. (REDIR-7, REDIR-8, REDIR-9, REDIR-10)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- On a cross-origin redirect, whether method-preserving or a 303 GET rebuild, the origin-scoped Cookie and Proxy-Authorization headers must also be stripped. (REDIR-7, REDIR-8, REDIR-9, REDIR-10)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- On a same-origin redirect the Cookie header should be retained, with only Authorization stripped; a more conservative port may strip all cookies. (REDIR-7, REDIR-8, REDIR-9, REDIR-10)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- Because the auth layer runs inside the redirect loop, a cross-origin re-issue must carry an out-of-band signal instructing the auth layer to skip credential stamping; this signal must be impossible for a server-supplied Location to forge into a leak, must only suppress stamping and never cause a credential to be sent, and must be removed by the credential-attaching layer before dispatch, while a same-origin re-issue is not signaled and is re-stamped normally. (REDIR-11, REDIR-24, REDIR-7)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- The redirect follower must wrap the auth layer, with redirect outer and auth inside on every hop, which is what necessitates stripping the Authorization header before re-issue and using the cross-origin suppression signal. (REDIR-11, REDIR-24, REDIR-7)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- Userinfo in the Location target (user:pass@) must be dropped before re-issue, and server-supplied embedded credentials must never be used. (REDIR-12, REDIR-13)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:15-15` · high · sha:f2a0d207be56</sub>
- Stripping userinfo and resolving the Location generally must preserve the wire-exact, already-percent-encoded path, query, and fragment, and must preserve bracketed IPv6 literal hosts and explicit ports; re-encoding that would decode %2F to / or %26 to & is forbidden. (REDIR-12, REDIR-13)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:15-15` · high · sha:f2a0d207be56</sub>
- A relative Location header must be resolved against the current hop's request URL per RFC 3986; absolute Location values are used as-is after userinfo stripping. (REDIR-14, REDIR-15)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:19-19` · high · sha:f2a0d207be56</sub>
- An HTTPS-to-HTTP scheme downgrade across a single redirect hop must be rejected by default with a clear error, permitted only via an opt-in that surfaces the downgrade observably; credential stripping still applies regardless, and the downgrade check is evaluated per hop. (REDIR-14, REDIR-15)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:19-19` · high · sha:f2a0d207be56</sub>
- The redirect step must detect redirect loops by recording every visited absolute URI, seeded with the original request URI, and when a redirect would revisit a seen URI, must stop and return the current redirect response without throwing, leaving its body open for the caller. (REDIR-16, REDIR-17)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:20-20` · high · sha:f2a0d207be56</sub>
- A malformed or unresolvable Location header (invalid URI, illegal characters, unsupported scheme) must not throw; the redirect step logs it and returns the current redirect response unfollowed. (REDIR-18, REDIR-19)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:21-21` · high · sha:f2a0d207be56</sub>
- A redirect response with a missing or empty Location header must be returned unfollowed. (REDIR-18, REDIR-19)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:21-21` · high · sha:f2a0d207be56</sub>
- The redirect step must manage response-body lifecycle deterministically: before issuing a follow-up request the prior redirect response's body must be closed, and if building the follow-up throws due to a non-replayable body or downgrade rejection, the current response must be closed before the error propagates. (REDIR-22, REDIR-23)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- On any "return current" outcome — not-a-redirect, opted-out, malformed/missing Location, loop detected, or max hops reached — the returned redirect response is left open for the caller. (REDIR-22, REDIR-23)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- Redirect following should be implemented as an iterative loop rather than unbounded recursion, so it is stack-safe. (REDIR-22, REDIR-23)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- A configured redirect predicate must fully override the built-in redirect decision and receive a read-only, defensively-copied condition snapshot — the current response, the count of redirects already followed, and an insertion-ordered set of visited URIs including the current request's — so it cannot mutate the live cycle-detection state. (REDIR-20, REDIR-21)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:26-26` · high · sha:f2a0d207be56</sub>
- On the non-redirect fast path, where the status is not a recognized redirect code, the implementation should short-circuit before allocating a condition snapshot and must not consult the predicate; a recognized 3xx always allocates the snapshot and consults the predicate, even with no usable Location. (REDIR-20, REDIR-21)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:26-26` · high · sha:f2a0d207be56</sub>
- The configured allowed-method set must be stored as an immutable defensive copy so post-construction mutation of the caller's collection cannot change redirect policy. (REDIR-26, REDIR-27, REDIR-28)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- Each followed redirect hop, loop detection, and scheme-downgrade event should be emitted as structured log records with URLs passed through a redactor, with redaction failures degrading to a placeholder rather than crashing logging; the malformed-Location event is an exception that logs the raw Location string as received because it failed to parse and so cannot be redacted. (REDIR-26, REDIR-27, REDIR-28)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- Redirect handling MUST strip the Authorization header before every redirect re-issue, even a same-origin one. (XCUT-17)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45` · high · sha:d6123be82c9e</sub>
- On a cross-origin redirect, judged against the original seed origin rather than the previous hop, redirect handling MUST additionally strip origin-scoped credentials (Cookie, Proxy-Authorization) and ensure the caller's credential is not re-applied to the foreign host. (XCUT-17)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45` · high · sha:d6123be82c9e</sub>
- Redirect handling MUST drop any userinfo present in the Location header before re-issuing the request. (XCUT-17)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45` · high · sha:d6123be82c9e</sub>
- Redirect handling MUST reject an HTTPS-to-HTTP scheme downgrade by default, permitting it only via explicit opt-in with the deviation logged. (XCUT-17)
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45` · high · sha:d6123be82c9e</sub>

## Constraints

## Conclusions

## Reference
- Redirect following is implemented as a synchronous pillar step that coordinates with the auth pillar via an internal cross-origin marker, and the async pipeline follows no redirects. (PIPE-32, REDIR-25)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:3-3` · high · sha:f2a0d207be56</sub>
- In the reference implementation only the auth step strips the cross-origin marker, so a pipeline with no auth step, including the sync standard-resilience preset, forwards the internal marker to the transport; a robust port should strip the signal independently of whether a credential layer runs. (REDIR-11, REDIR-24, REDIR-7)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- The number of followed redirects is capped by a max-hops setting defaulting to 3; on reaching the cap the last response is returned as-is even if itself a 3xx, without throwing, and a max-hops of 0 disables redirect following entirely. (REDIR-16, REDIR-17)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:20-20` · high · sha:f2a0d207be56</sub>
- The header from which the redirect target is read may be configurable, defaulting to Location. (REDIR-26, REDIR-27, REDIR-28)
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- The cross-origin redirect marker is an internal, transport-invisible sentinel the redirect step sets on a cross-origin re-issue so the auth step suppresses credential stamping onto a server-chosen foreign host; it is stripped before the wire and unforgeable.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:19` · high · sha:f0b3d2058626</sub>
- An origin tuple (RFC 6454) is the (scheme, host, effective-port) triple, and two URLs share an origin iff all three match, with case-insensitive host and scheme-default port; cross-origin is judged against the original seed request, not the previous hop.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:35` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded

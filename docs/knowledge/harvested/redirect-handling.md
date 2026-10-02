# redirect-handling

## Rules
- REDIR-7 (MUST) The Authorization header is stripped before every redirect re-issue, including same-origin and the 303 GET rebuild, because re-attaching a credential for a known origin is the auth layer's job.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- REDIR-8 (MUST) A redirect is cross-origin iff the resolved target differs from the original (seed) request origin in scheme, host (case-insensitive) or effective port (scheme default when omitted), and the comparison is against the seed origin rather than the previous hop so a same-origin sub-redirect on a foreign host cannot re-expose the credential.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- REDIR-9 (MUST) On a cross-origin redirect (method-preserving or 303 GET rebuild) the origin-scoped Cookie and Proxy-Authorization headers are also stripped.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- REDIR-10 (SHOULD) On a same-origin redirect the Cookie header SHOULD be retained with only Authorization stripped, and a more conservative port MAY strip all cookies.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:13-13` · high · sha:f2a0d207be56</sub>
- REDIR-11 (MUST) Because the auth layer runs inside the redirect loop, a cross-origin re-issue carries an out-of-band signal instructing the auth layer to skip credential stamping, and a same-origin re-issue is not signaled and is re-stamped normally.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- REDIR-11(a) The cross-origin signal must be impossible for a server-supplied Location to forge into a leak, so the redirect layer clears any inbound copy on every re-issue before conditionally setting its own on a cross-origin hop.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- REDIR-11(b,c) The cross-origin signal may only suppress stamping and never cause a credential to be sent, and it is removed by the credential-attaching layer before dispatch.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- REDIR-24 (MUST) The redirect follower wraps the auth layer (redirect outer, auth inside, per hop), which is what necessitates REDIR-7 and REDIR-11.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- REDIR-12 (MUST) Userinfo in the Location target (user:pass@) is dropped before re-issue, and server-supplied embedded credentials are never used.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:15-15` · high · sha:f2a0d207be56</sub>
- REDIR-13 (MUST) Stripping userinfo and resolving the Location preserves the wire-exact already-percent-encoded path, query and fragment plus bracketed IPv6 literal hosts and explicit ports, and re-encoding that would decode %2F to / or %26 to & is forbidden.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:15-15` · high · sha:f2a0d207be56</sub>
- REDIR-14 (MUST) A relative Location is resolved against the current hop's request URL per RFC 3986, and absolute values are used as-is after userinfo stripping.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:19-19` · high · sha:f2a0d207be56</sub>
- REDIR-15 (MUST) An HTTPS-to-HTTP scheme downgrade across a single hop is rejected by default with a clear error, permitted only via an opt-in that surfaces the downgrade observably, with credential stripping applying regardless and the check evaluated per hop.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:19-19` · high · sha:f2a0d207be56</sub>
- REDIR-16 (MUST) The redirect step detects loops by recording every visited absolute URI (seeded with the original request URI) and, when a redirect would revisit a seen URI, stops and returns the current redirect response without throwing, leaving its body open for the caller.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:20-20` · high · sha:f2a0d207be56</sub>
- REDIR-17 (MUST) The number of followed redirects is capped by max-hops (default 3); on reaching the cap the last response is returned as-is even if itself a 3xx without throwing, and max-hops 0 disables redirect following entirely.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:20-20` · high · sha:f2a0d207be56</sub>
- REDIR-18 (MUST) A malformed or unresolvable Location (invalid URI, illegal characters, unsupported scheme) does not throw; the step logs it and returns the current redirect response unfollowed.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:21-21` · high · sha:f2a0d207be56</sub>
- REDIR-19 (MUST) A redirect response with a missing or empty Location is returned unfollowed.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:21-21` · high · sha:f2a0d207be56</sub>
- REDIR-22 (MUST) Before issuing a follow-up the prior redirect response's body is closed, and if building the follow-up throws (non-replayable body, downgrade rejection) the current response is closed before the error propagates.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- REDIR-22 (MUST) On any return-current outcome (not-a-redirect, opted-out, malformed or missing Location, loop detected, max hops) the returned response is left open for the caller.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- REDIR-23 (SHOULD) Redirect following SHOULD be an iterative loop rather than unbounded recursion so it is stack-safe.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:22-22` · high · sha:f2a0d207be56</sub>
- REDIR-20 (MUST) A configured redirect predicate fully overrides the built-in decision and receives a read-only, defensively-copied condition snapshot (current response, count of redirects already followed, and an insertion-ordered set of visited URIs including the current request's) so it cannot mutate live cycle-detection state.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:26-26` · high · sha:f2a0d207be56</sub>
- REDIR-21 (SHOULD) On the non-redirect fast path (status not a recognized redirect code) the implementation SHOULD short-circuit before allocating a condition snapshot and MUST NOT consult the predicate.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:26-26` · high · sha:f2a0d207be56</sub>
- REDIR-21 A recognized 3xx always allocates the condition snapshot and consults the predicate, even with no usable Location.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:26-26` · high · sha:f2a0d207be56</sub>
- REDIR-26 (MUST) The configured allowed-method set is stored as an immutable defensive copy so post-construction mutation of the caller's collection cannot change policy.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- REDIR-27 (MAY) The header from which the redirect target is read MAY be configurable, with default Location.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- REDIR-28 (SHOULD) Each followed hop, loop detection and scheme-downgrade event SHOULD be emitted as structured records with URLs passed through a redactor, with redaction failures degrading to a placeholder rather than crashing logging.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- The async pipeline follows no redirects (PIPE-32, REDIR-25).
  <sub>spec · `docs/product-spec/10-redirect-handling.md:3-3` · high · sha:f2a0d207be56</sub>
- REDIR-1 (MUST) A redirect is attempted only for status 301, 302, 303, 307 and 308; any other status (2xx, 4xx, 5xx, non-redirect 3xx) is returned verbatim without consulting redirect logic.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:7-7` · high · sha:f2a0d207be56</sub>
- REDIR-2 (MUST) Status 300, 304 and 305 MUST NOT be auto-followed even with a Location header, and 305 in particular must never redirect to a server-chosen proxy.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:7-7` · high · sha:f2a0d207be56</sub>
- REDIR-3 (MUST) For 301 and 302 a redirect is followed only if the original request method is in the configured allowed-method set (default {GET, HEAD}), and when followed the original method and body are preserved with deliberately no automatic POST-to-GET rewrite.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- REDIR-4 (MUST) 307 and 308 redirects preserve method and body and are followed only if the method is allowed.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- REDIR-5 (MUST) A 303 is not followed by default; when opted in it is re-issued as a GET with the body dropped and every Content-* request header removed case-insensitively, regardless of the original method.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:8-8` · high · sha:f2a0d207be56</sub>
- REDIR-6 (MUST) Any followed method-preserving redirect (301/302/307/308) re-sends the original body so it must be replayable; if a body is present and not replayable the operation fails with a clear error naming replayability and the redirect is not attempted, while 303 is exempt because it drops the body.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:9-9` · high · sha:f2a0d207be56</sub>
- The SDK-managed SystemNetHttpClient constructor builds its own SocketsHttpHandler with AllowAutoRedirect = false.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:315-316` · high · sha:da6000c93fc5</sub>
- The adapter's HttpMessageHandler-taking constructor rejects a SocketsHttpHandler or HttpClientHandler that has redirects enabled, because a borrowed HttpClient does not expose its handler and cannot be inspected.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:316-318` · high · sha:da6000c93fc5</sub>
- The DI package configures named clients' primary handler with redirects off.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:318-319` · high · sha:da6000c93fc5</sub>
- As a last line of defence, the adapter compares HttpResponseMessage.RequestMessage.RequestUri with the URI it sent (after an automatic redirect that property holds the final URI, verified) and fails loudly, disposing the response, when they differ.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:319-321` · high · sha:da6000c93fc5</sub>
- Exactly one layer follows redirects, an invariant kept by forcing the transport not to follow redirects (section 6.2) rather than by making the SDK not to.
  <sub>design · `docs/sdk-design-dotnet/05-pipeline-architecture.md:304-305` · high · sha:1608fcd4b329</sub>
- The SDK-created client must be new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), and the DI package configures the primary handler the same way.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:218-219` · high · sha:27a0a46f35a8</sub>
- Redirect codes and methods translate the specification literally (REDIR-1 to REDIR-6): 303 is not always followed, the allowed-method set defaults to {GET, HEAD} held as a FrozenSet<Method> copy (REDIR-3/REDIR-4, REDIR-26), a non-replayable body fails with an error naming replayability (REDIR-6), and a rejected downgrade fails instead of silently returning the 3xx (REDIR-15).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:228-234` · medium · sha:27a0a46f35a8</sub>
- Wire, log-for-comparison and visited-set uses of a Uri go through AbsoluteUri, never ToString(), because Uri.ToString() returns the unescaped form (a%20b renders as a b).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:240-242` · high · sha:27a0a46f35a8</sub>
- Userinfo is dropped from a redirect target with GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped), verified to preserve every escape (REDIR-12); the as-built follower keeps userinfo.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:242-244` · high · sha:27a0a46f35a8</sub>
- An unsupported scheme in a redirect Location is rejected explicitly as malformed and the response returned unfollowed (REDIR-18), because Uri.TryCreate accepts ftp://x/y.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:244-246` · high · sha:27a0a46f35a8</sub>
- Cross-origin is judged by comparing scheme, case-insensitive host and effective port against the original (seed) request, not the previous hop (REDIR-8); the as-built follower compares against the previous hop.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:251-253` · high · sha:27a0a46f35a8</sub>
- The stage order REDIRECT, RETRY, AUTH still matters because auth must run per hop to see each hop's URL.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:271-272` · high · sha:27a0a46f35a8</sub>
- Authorization is stripped before every redirect re-issue, same-origin included (REDIR-7; as built, only cross-origin).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:274-275` · high · sha:27a0a46f35a8</sub>
- Cookie and Proxy-Authorization are additionally stripped on cross-origin redirects (REDIR-9; as built, Proxy-Authorization is not), and Cookie is kept same-origin (REDIR-10; built).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:275-277` · high · sha:27a0a46f35a8</sub>
- The redirect visited set is seeded with the original URI, and a revisit returns the current response unclosed (REDIR-16).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:280-281` · high · sha:27a0a46f35a8</sub>
- Redirect max hops defaults to 3, with 0 disabling following (REDIR-17).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:281-282` · high · sha:27a0a46f35a8</sub>
- A missing, empty or malformed Location returns the response unfollowed (REDIR-18, REDIR-19; built for missing and unparseable).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:282-283` · high · sha:27a0a46f35a8</sub>
- The prior response is disposed before each redirect follow-up and the current one is disposed if building the follow-up throws, while every return-current outcome leaves it open (REDIR-22; built for the follow path).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:283-285` · high · sha:27a0a46f35a8</sub>
- The redirect loop is a while loop for stack safety (REDIR-23; built).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:285-285` · high · sha:27a0a46f35a8</sub>

## Constraints
- Redirect following is a synchronous pillar step that coordinates with the auth pillar via an internal cross-origin marker.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:3-3` · high · sha:f2a0d207be56</sub>
- The HttpMessageHandler chain underneath the SDK pipeline follows redirects by default, which silently removes the SDK's redirect authority unless the transport turns it off (§3.2).
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:27-29` · high · sha:d7cea7b15cf3</sub>
- HttpClientHandler.AllowAutoRedirect and SocketsHttpHandler.AllowAutoRedirect both default to true (MaxAutomaticRedirections 50), so HttpClient follows redirects by default and silently removes the SDK's redirect authority (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:308-310` · high · sha:da6000c93fc5</sub>
- Automatic redirect following by the handler directly violates TRANSPORT-1 and XCUT-17(b), which requires stripping origin-scoped credentials (Cookie, Proxy-Authorization) on a cross-origin redirect.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:313-315` · high · sha:da6000c93fc5</sub>
- HttpClientHandler.AllowAutoRedirect and SocketsHttpHandler.AllowAutoRedirect both default to true (MaxAutomaticRedirections 50), and the as-built SystemNetHttpClient() constructor creates new HttpClient() with that default.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:199-202` · high · sha:27a0a46f35a8</sub>
- Because the HttpClient handler follows redirects before the SDK sees anything, the SDK's RedirectPolicy is dead code over the default SystemNetHttpClient (a GET to a 300 returned the final 200 while a probe below RedirectPolicy saw exactly one drive).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:215-217` · high · sha:27a0a46f35a8</sub>

## Conclusions
- For a caller-supplied HttpClient, the transport detects a redirect follow by comparing response.RequestMessage.RequestUri (which the handler rewrites in place to the final URI) with the URI it sent, and on a mismatch disposes the response and fails with a configuration error naming AllowAutoRedirect, loudly because a cookie may already have crossed an origin.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:219-223` · high · sha:27a0a46f35a8</sub>
- PIPE-32 is reversed: one RedirectPolicy serves both the sync and async paths (section 5.3, section 10 entry 14, section 11 item 29).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:225-226` · high · sha:27a0a46f35a8</sub>
- Redirect Location resolution uses Uri.TryCreate(current, location, out next), which on .NET 10.0.401 resolves per RFC 3986 with dot segments removed while preserving %2F in the path, %26 in the query, fragment escapes, bracketed IPv6 literals and explicit ports (REDIR-13, REDIR-14).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:236-239` · high · sha:27a0a46f35a8</sub>
- The REDIR-16 visited set is a HashSet<string> of AbsoluteUri under ordinal comparison rather than a HashSet<Uri>, because Uri.Equals ignores userinfo and fragment (https://a:b@h/x#1 equals https://h/x#2) while HTTP-46 requires comparison by textual external form; HTTP-46's no-DNS equality is free since Uri.Equals resolves nothing.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:246-249` · high · sha:27a0a46f35a8</sub>
- REDIR-11 asks for an unforgeable out-of-band redirect-to-auth signal, but the port adopts and hardens the no-signal mechanism: HttpPipeline.SendAsync fixes the seed request's origin on the call-scoped PipelineContext at call start, immutable thereafter, and RedirectPolicy and AuthorizationPolicy both compare against it, so a mismatch means this hop is cross-origin.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:258-265` · high · sha:27a0a46f35a8</sub>
- The seed-origin comparison satisfies REDIR-11 and AUTH-29 a fortiori: a Location value cannot reach an immutable field set before the first response, a comparison can only withhold a credential, and nothing is added to the request so no marker needs stripping and the no-auth-step trap cannot occur.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:265-267` · high · sha:27a0a46f35a8</sub>
- The seed-origin mechanism works under a custom redirect policy that never learned to set a marker, which makes it stronger than a marker.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:267-268` · high · sha:27a0a46f35a8</sub>
- The as-built StripSensitiveHeadersOnCrossOrigin switch is removed because REDIR-9 is a MUST and an option turning it off narrows it (P9).
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:277-278` · high · sha:27a0a46f35a8</sub>

## Reference
- In the reference implementation only the auth step strips the cross-origin marker, so a pipeline with no auth step (including the sync standard-resilience preset) forwards the internal marker to the transport, and a robust port should strip the signal independently of whether a credential layer runs.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:14-14` · high · sha:f2a0d207be56</sub>
- The malformed-Location log event is the exception to URL redaction and logs the raw Location string as received because it failed to parse, which a port receiving credential-bearing malformed values should account for.
  <sub>spec · `docs/product-spec/10-redirect-handling.md:27-27` · high · sha:f2a0d207be56</sub>
- Verified through the as-built new SystemNetHttpClient(): a 302 turned a POST into a GET, a 307 re-POSTed the 7-byte body to a different origin, the handler stripped Authorization on both hops but forwarded a caller-set Cookie and a custom header to the second origin, and the SDK redirect policy never saw a 3xx.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:310-315` · high · sha:da6000c93fc5</sub>
- The runtime redirect handler follows a 300 with Location, contrary to REDIR-2, which says it MUST NOT be followed.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:204-206` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler re-issues a 301/302 POST as a GET with the body dropped, contrary to REDIR-3's ban on automatic POST-to-GET rewrite.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:207-207` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler follows a 303 on PUT as GET by default, contrary to REDIR-5, which says it is not followed unless opted in.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:208-208` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler strips Authorization on every hop, which satisfies REDIR-7.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:209-209` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler forwards Cookie and Proxy-Authorization cross-origin, contrary to REDIR-9, which says they MUST be stripped.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:210-210` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler does not follow an https to http redirect and silently returns the 302, contrary to REDIR-15, which requires failing with a clear error.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:211-211` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler follows a Location with user:pass@, not sending the userinfo as a header but keeping it in RequestMessage.RequestUri, contrary to REDIR-12, which requires dropping it before re-issue.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:212-212` · high · sha:27a0a46f35a8</sub>
- The runtime redirect handler does not detect a self-loop, keeps following and finally returns the 302, contrary to REDIR-16, which requires detecting the revisit.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:213-213` · high · sha:27a0a46f35a8</sub>
- The as-built follower copied the handler's browser semantics: a POST to a 302 arrives as a GET with no body, 303 is always followed, there is no allowed-method set, and a non-replayable body or rejected downgrade silently returns the 3xx.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:228-233` · high · sha:27a0a46f35a8</sub>
- REDIR-17 says the maximum redirects default is 3, whereas as built MaxRedirects defaults to 20.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:233-234` · high · sha:27a0a46f35a8</sub>
- Uri normalises a percent-encoded unreserved character such as %41 to A, which RFC 3986 section 6.2.2.2 defines as equivalent so no wire meaning changes.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:239-240` · high · sha:27a0a46f35a8</sub>
- Uri.Port already yields the effective port (https://h/ and https://h:443/ both report 443), so the (scheme, host, port) triple compares correctly; the as-built AuthorizationPolicy comment claiming Uri.Port returns -1 for the default port is wrong, harmlessly.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:253-256` · high · sha:27a0a46f35a8</sub>
- The as-built auth policy records the origin it sees on its first invocation into the public string-keyed property bag under "dexpace.auth.origin", where any other policy could overwrite it; the mechanism change is recorded as section 10 entry 15.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:268-271` · high · sha:27a0a46f35a8</sub>
- The redirect predicate with its read-only snapshot (REDIR-20, REDIR-21) and the structured, redacted hop events (REDIR-28) are not built.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:285-286` · high · sha:27a0a46f35a8</sub>
- As built at d45e64b, redirect diverges: the default transport follows redirects itself so the policy is dead code, POST-to-GET and 303 follow browser-style, there is no allowed-method set, loop detection, userinfo drop or Proxy-Authorization strip, a downgrade and a non-replayable body silently return the 3xx, origin is compared to the previous hop, and the default is 20 hops.
  <sub>design · `docs/sdk-design-dotnet/06-retry-redirect-and-authentication.md:288-290` · high · sha:27a0a46f35a8</sub>

## Conflicts

## Superseded


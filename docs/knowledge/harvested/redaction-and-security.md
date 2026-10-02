# redaction-and-security

## Rules
- Safe behavior is the default, and exposing a secret or lowering a guarantee must be an explicit opt-in.
  <sub>spec · `docs/product-spec/02-architectural-principles.md:20-20` · high · sha:8014d2ec2c9d</sub>
- Credentials are transport-scoped and never stamped over plaintext (AUTH-28).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Redirects strip credentials and never launder them cross-origin (REDIR-7, REDIR-9, REDIR-8).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Header names and values are validated against request-splitting before any transport sees them (HTTP-17 to HTTP-19).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Digest client nonces come from a cryptographically strong source (AUTH-20).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- Log-preview and error-body buffers are bounded (HTTP-52, BODY-30).
  <sub>spec · `docs/product-spec/02-architectural-principles.md:22-22` · high · sha:8014d2ec2c9d</sub>
- OBS-11 (MUST) URL userinfo (`user:password@`) MUST always be redacted to the fixed placeholder `***:***@`, unconditionally and independent of any allow-list.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:24-24` · high · sha:1b678eca176d</sub>
- OBS-12 (MUST) URL query-parameter values MUST be redacted to `***` unless the parameter name (decoded, compared case-insensitively) is allow-listed, the default query allow-list MUST be exactly `{api-version}`, an empty allow-list MUST redact every value, multi-value keys MUST be treated atomically, and names and the `=` separator MUST be preserved.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:25-25` · high · sha:1b678eca176d</sub>
- OBS-13 (MUST) A URL fragment MUST be scrubbed under the same allow-list as query parameters, with `key=value` tokens redacted like query values and a plain fragment with no `=` preserved verbatim, because OAuth implicit-flow access tokens ride in the fragment.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:26-26` · high · sha:1b678eca176d</sub>
- OBS-14 (MUST) URL redaction MUST NOT alter scheme, host, port or path, MUST preserve a present-but-empty query (trailing `?`), MUST NOT treat a `?` inside the fragment as a query delimiter, and MAY drop a trailing `&` (empty final pair).
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:27-27` · high · sha:1b678eca176d</sub>
- OBS-15 (MUST) URL redaction MUST be total, returning the fixed sentinel `[malformed url]` on any parse/rebuild failure rather than throwing.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:28-28` · high · sha:1b678eca176d</sub>
- OBS-16 (MUST) A URL arriving as a header value MUST be redacted, with a parseable absolute value redacted like a request URL, a relative/unparseable value keeping the path and dropping everything after it with a fixed `?***` marker appended whenever it carried a query or a fragment, and a value with neither returned verbatim.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:29-29` · high · sha:1b678eca176d</sub>
- OBS-17 (MUST) When logging header values, values of URL-valued response headers (at minimum Location and Content-Location) MUST be redacted through the URL-value redactor, other header values pass through unchanged, and the redaction policy MUST be shared by the sync and async logging paths so it cannot drift.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:30-30` · high · sha:1b678eca176d</sub>
- OBS-18 (MUST) Header logging MUST gate which header names are logged by an allow-list, a non-allow-listed header MUST NOT have its value logged and per a boolean policy is either emitted with a fixed `REDACTED` marker or omitted entirely, and the default allow-list MUST contain only diagnostic, non-credential headers.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:31-31` · high · sha:1b678eca176d</sub>
- OBS-19 (SHOULD) A transport dropping a caller-set request header it cannot encode SHOULD surface the drop with a configurable verbosity policy offering at least WARN every occurrence, WARN the first drop per header name then verbose, and verbose only, with the default SHOULD being once-per-header-name.
  <sub>spec · `docs/product-spec/15-instrumentation-and-observability.md:32-32` · high · sha:1b678eca176d</sub>
- XCUT-16 (MUST) A credential MUST NOT be stamped over a non-secure (non-HTTPS) transport, and the auth layer MUST fail loudly before any token fetch or header write when about to attach a credential and the scheme is not https (case-insensitive).
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:44-44` · high · sha:d6123be82c9e</sub>
- XCUT-17 (MUST) Redirect handling MUST strip Authorization before every redirect re-issue, even same-origin.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45-45` · high · sha:d6123be82c9e</sub>
- XCUT-17 (MUST) On a cross-origin redirect, judged against the original seed origin and not the previous hop, redirect handling MUST additionally strip origin-scoped credentials (Cookie, Proxy-Authorization) and ensure the caller's credential is not re-applied to the foreign host.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45-45` · high · sha:d6123be82c9e</sub>
- XCUT-17 (MUST) Redirect handling MUST drop any userinfo in the Location before re-issue.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45-45` · high · sha:d6123be82c9e</sub>
- XCUT-17 (MUST) Redirect handling MUST reject an HTTPS-to-HTTP scheme downgrade by default, permitting it only via explicit opt-in and logging the deviation.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:45-45` · high · sha:d6123be82c9e</sub>
- XCUT-19 (MUST) Logging and telemetry MUST redact URL userinfo always, and userinfo is never allow-listed.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47-47` · high · sha:d6123be82c9e</sub>
- XCUT-19 (MUST) Logging and telemetry MUST redact URL query-parameter values and key=value fragment tokens unless the parameter name (case-insensitive) is allow-listed.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47-47` · high · sha:d6123be82c9e</sub>
- XCUT-19 (MUST) Header logging MUST be default-deny, emitting verbatim only an explicit allow-list of non-credential headers.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47-47` · high · sha:d6123be82c9e</sub>
- XCUT-19 (MUST) Credential objects MUST NOT reveal their secret in string or serialized form.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47-47` · high · sha:d6123be82c9e</sub>
- XCUT-19 (MUST) Full request/response body logging MUST be OFF by default.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:47-47` · high · sha:d6123be82c9e</sub>
- XCUT-20 (MUST) Observability code paths (redaction, event emission, span/metric recording) MUST NEVER throw into the caller's request path.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:48-48` · high · sha:d6123be82c9e</sub>
- XCUT-20 (MUST) A failure to redact, render or emit MUST degrade gracefully (a safe placeholder such as a malformed-URL marker, or a self-describing instrumentation-error event) and let the request proceed.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:48-48` · high · sha:d6123be82c9e</sub>
- XCUT-21 (MUST) Any security-relevant random value (auth client nonces/cnonce and similar unpredictability-dependent tokens) MUST be drawn from a cryptographically-strong PRNG with sufficient entropy and never a non-cryptographic RNG.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:49-49` · high · sha:d6123be82c9e</sub>
- Newtonsoft.Json TypeNameHandling other than None instantiates attacker-named types and BinaryFormatter is obsolete and removed; both are banned in core and adapters through the banned-API analyzer rather than by convention, and the future Newtonsoft adapter pins TypeNameHandling.None.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:612-615` · high · sha:da6000c93fc5</sub>
- OBS-12 is default-deny: URL query-parameter values MUST be redacted to *** unless the parameter name is allow-listed, and the default query allow-list MUST be exactly {api-version}.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:170-172` · high · sha:ddf8f695ff61</sub>
- URL redaction must replace userinfo with the placeholder ***:***@ (OBS-11) rather than stripping it, as the as-built does.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:175-176` · high · sha:ddf8f695ff61</sub>
- URL redaction must scrub key=value tokens in the fragment and keep a plain fragment (OBS-13) rather than dropping the fragment, as the as-built does.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:176-177` · high · sha:ddf8f695ff61</sub>
- URL redaction must preserve value-less parameters (?flag) and a present-but-empty query (?) per OBS-14, and must preserve keys and values rather than re-encoding them; the as-built silently removes them and re-encodes.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:177-178` · high · sha:ddf8f695ff61</sub>

## Constraints

## Conclusions
- The default-allow redactor is corrected to default-deny because credentials in unnamed parameters (X-Amz-Signature, client_secret, sv) would reach the span tag, and because System.Net.Http's own span reports url.full with the whole query redacted by default (verified), making default-deny the ecosystem's convention; P9 does not allow narrowing a MUST-level guarantee.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:167-175` · high · sha:ddf8f695ff61</sub>

## Reference
- The HTTPS-only credential guard applies only on the credential-attaching path, so a deliberately credential-free re-issue (for example a marker-suppressed cross-origin redirect) MAY proceed over any scheme.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:44-44` · high · sha:d6123be82c9e</sub>
- The reference implementation uses at least 128 bits of entropy for the Digest cnonce.
  <sub>spec · `docs/product-spec/19-cross-cutting-invariants-and-policies.md:49-49` · high · sha:d6123be82c9e</sub>
- The as-built UrlRedactor is default-allow: it replaces the values of eight named parameters (access_token, token, code, sig, signature, api_key, apikey, password) and passes every other value through.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:167-170` · high · sha:ddf8f695ff61</sub>
- OBS-15's totality holds for the Uri overload of the redactor, whose input is already parsed, while the string overload that OBS-16 needs for header values returns "[malformed url]" on failure.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:178-180` · high · sha:ddf8f695ff61</sub>
- OBS-16 to OBS-18 (header-value URLs, Location, a default-deny header allow-list) have no call site yet because nothing logs headers, and bind the moment anything does; OBS-19's dropped-header verbosity is the transport's (section 3.2).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:181-183` · high · sha:ddf8f695ff61</sub>

## Conflicts

## Superseded


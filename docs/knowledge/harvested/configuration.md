# configuration

## Rules
- CFG-1 (MUST): A configuration value lookup resolves in strict order: an explicit override for the exact key, then the environment source queried by the exact key name, then the system-property source queried by the normalized key name, then the caller-supplied default, which MAY be absent.
  <sub>spec · `docs/product-spec/16-configuration.md:7-7` · high · sha:367e27ec6481</sub>
- CFG-2 (MUST): An environment value that is present but empty is treated as absent, so the lookup falls through to the property layer.
  <sub>spec · `docs/product-spec/16-configuration.md:8-8` · high · sha:367e27ec6481</sub>
- CFG-3 (MUST): The property layer is queried under a normalized key derived by lowercasing and replacing every underscore with a dot (MAX_RETRY_ATTEMPTS becomes max.retry.attempts), while the override and environment layers use the original name.
  <sub>spec · `docs/product-spec/16-configuration.md:9-9` · high · sha:367e27ec6481</sub>
- CFG-4 (MUST): A separate raw property accessor looks up by the exact name without the env-to-property normalization, so camelCase property-only keys such as https.proxyHost resolve with casing preserved.
  <sub>spec · `docs/product-spec/16-configuration.md:10-10` · high · sha:367e27ec6481</sub>
- CFG-38 (MUST): The typed accessors (integer, boolean, duration) resolve the raw value through the same layered lookup as the string accessor before parsing, and never read only the override map or skip the env/property layers; the typed default applies only when the layered lookup yields no value.
  <sub>spec · `docs/product-spec/16-configuration.md:11-11` · high · sha:367e27ec6481</sub>
- CFG-5 (MUST): Typed configuration accessors never throw on missing or unparseable values: the integer accessor returns the default when the value is absent or not a valid integer, the duration accessor returns the default on any parse failure, and negative integers are valid and returned as-is.
  <sub>spec · `docs/product-spec/16-configuration.md:15-15` · high · sha:367e27ec6481</sub>
- CFG-6 (MUST): The boolean configuration accessor is strict: only case-insensitive true and false are recognized, and anything else (1, 0, yes, no, on, off) falls through to the default.
  <sub>spec · `docs/product-spec/16-configuration.md:16-16` · high · sha:367e27ec6481</sub>
- CFG-7 (MUST): The duration accessor accepts ISO-8601 durations (leading P or p), shorthand <number><unit> with units ms, s, m, h, d (case-insensitive), and a bare number interpreted as milliseconds; a negative duration is rejected and returns the default, and an unknown unit returns the default.
  <sub>spec · `docs/product-spec/16-configuration.md:17-17` · high · sha:367e27ec6481</sub>
- CFG-8 (MUST): A built configuration is immutable and safe to share without external synchronization, and its override map is defensively copied at build time so later builder mutation cannot alter a built instance.
  <sub>spec · `docs/product-spec/16-configuration.md:21-21` · high · sha:367e27ec6481</sub>
- CFG-9 (MUST): Deriving a reconfigured configuration is copy-on-write: it produces a new instance with the mutator applied, leaves the receiver unchanged, copies the override map before the mutator runs, and inherits the environment and property source seams by reference unless the mutator replaces them.
  <sub>spec · `docs/product-spec/16-configuration.md:22-22` · high · sha:367e27ec6481</sub>
- CFG-10 (MUST): Removing an override drops only the override layer, so a later lookup falls through to env, property and default as if the override never existed; removal does not force the key to resolve to null, and removing a key with no override is a no-op.
  <sub>spec · `docs/product-spec/16-configuration.md:23-23` · high · sha:367e27ec6481</sub>
- CFG-11 (MUST): The environment and property sources are substitutable seams (injectable functions from key name to optional string) so tests supply hermetic lookups without touching the real environment, and production defaults delegate to the platform environment and system properties.
  <sub>spec · `docs/product-spec/16-configuration.md:24-24` · high · sha:367e27ec6481</sub>
- CFG-12 (SHOULD): Configuration builders are usable single-threaded only; the immutability guarantee applies to the built configuration, not to an in-progress builder.
  <sub>spec · `docs/product-spec/16-configuration.md:25-25` · high · sha:367e27ec6481</sub>
- CFG-13 (SHOULD): A process-wide global configuration slot is provided with last-write-wins replacement and safe publication, defaulting to an empty configuration.
  <sub>spec · `docs/product-spec/16-configuration.md:26-26` · high · sha:367e27ec6481</sub>
- CFG-14 (SHOULD): The configuration subsystem exposes stable well-known key constants for the retry-attempt cap, the log level, and the standard proxy variables HTTP_PROXY, HTTPS_PROXY and NO_PROXY.
  <sub>spec · `docs/product-spec/16-configuration.md:27-27` · high · sha:367e27ec6481</sub>
- CFG-37 (MUST): Passing a null or absent required argument (override key or value, source function, derive mutator, global-config setter) to a mutating configuration operation fails fast rather than storing a null, while documented-nullable optional slots (proxy credentials, challenge handler, lookup default) MAY be null; a language without null-safety must add explicit validation.
  <sub>spec · `docs/product-spec/16-configuration.md:28-28` · high · sha:367e27ec6481</sub>
- CFG-22 (MUST): The proxy model is immutable and carries the proxy type (HTTP, SOCKS4, SOCKS5), socket address, an ordered list of non-proxy host glob patterns, optional credentials, an optional challenge-handler slot, and an explicit bypass-all flag, and its string rendering masks credentials.
  <sub>spec · `docs/product-spec/16-configuration.md:42-42` · high · sha:367e27ec6481</sub>
- CFG-23 (MUST): The proxy host-bypass decision short-circuits to true when bypass-all is set and otherwise returns true iff the host matches any configured glob; glob conversion treats * as any run, ? as one character, escapes regex metacharacters, requires a full-string match and matches case-insensitively, and patterns SHOULD be compiled once at construction.
  <sub>spec · `docs/product-spec/16-configuration.md:43-43` · high · sha:367e27ec6481</sub>
- CFG-24 (MUST): Resolving proxy options from configuration never throws on malformed input (invalid config yields null plus a warning) and follows this precedence: system properties first, with host https.proxyHost preferred over http.proxyHost, the port taken from the same layer as the chosen host, and credentials read only from https.proxyUser and https.proxyPassword with no http.* fallback; otherwise the env URL HTTPS_PROXY preferred over HTTP_PROXY parsed as scheme://user:pass@host:port; with no proxy configured or invalid config resolution returns null.
  <sub>spec · `docs/product-spec/16-configuration.md:44-44` · high · sha:367e27ec6481</sub>
- CFG-25 (MUST): The proxy port must be explicit and within 0..65535, a missing, non-numeric or out-of-range port yields null with no default-port guessing, and an absent port in a proxy URL is invalid.
  <sub>spec · `docs/product-spec/16-configuration.md:45-45` · high · sha:367e27ec6481</sub>
- CFG-26 (MUST): The non-proxy host list is resolved with the system property (pipe-separated) winning over the environment variable (comma-separated), both honor a backslash escape preceding the literal separator and trim tokens, and the observable order is split, drop empty, unescape, trim, so a whitespace-only fragment is retained as an empty token.
  <sub>spec · `docs/product-spec/16-configuration.md:46-46` · high · sha:367e27ec6481</sub>
- CFG-27 (MUST): When the resolved non-proxy configuration is exactly a single bare *, it is interpreted as bypass-all (resolution returns null so the caller routes directly) and represented by the explicit bypass-all flag rather than a literal * entry, while a * inside a multi-entry list is a normal any-host glob.
  <sub>spec · `docs/product-spec/16-configuration.md:47-47` · high · sha:367e27ec6481</sub>
- CFG-28 (MAY): A convenience resolver MAY read proxy options from the global configuration, but the environment is consulted only when a resolver is explicitly invoked and nothing reads proxy configuration implicitly at startup.
  <sub>spec · `docs/product-spec/16-configuration.md:48-48` · high · sha:367e27ec6481</sub>
- CFG-36 (SHOULD): A static build/runtime descriptor exposes the SDK version and host runtime identity resolved once at load time, each falling back to a non-blank unknown when unavailable, and provides a default ordered identity-token list (SDK token then runtime token) in which every token is non-blank.
  <sub>spec · `docs/product-spec/16-configuration.md:59-59` · high · sha:367e27ec6481</sub>
- CFG-1: configuration precedence is override, then environment, then normalized property, then default.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:50-50` · high · sha:0451cc7f3bb4</sub>
- CFG-2: an empty environment value is treated as absent.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:50-50` · high · sha:0451cc7f3bb4</sub>
- CFG-3: property keys are normalized.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:50-50` · high · sha:0451cc7f3bb4</sub>
- CFG-4: a raw property accessor exists without normalization.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:50-50` · high · sha:0451cc7f3bb4</sub>
- CFG-38: typed accessors resolve through the full layered lookup.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:50-50` · high · sha:0451cc7f3bb4</sub>
- CFG-5: int and duration accessors never throw and negatives are valid.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:51-51` · high · sha:0451cc7f3bb4</sub>
- CFG-6: the boolean accessor is strict, accepting only true or false.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:51-51` · high · sha:0451cc7f3bb4</sub>
- CFG-7: durations parse ISO, shorthand and bare-number-milliseconds forms and reject negatives.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:51-51` · high · sha:0451cc7f3bb4</sub>
- CFG-8: built configuration is immutable and the override map is copied at build.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-9: derivation is copy-on-write with shared source seams.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-10: remove drops only the override layer.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-11: environment and property seams are substitutable.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-12: the builder is single-threaded.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-13: the global slot is last-write-wins.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-14: well-known key constants exist.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-37: a null required argument is rejected.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:52-52` · high · sha:0451cc7f3bb4</sub>
- CFG-15: a clock seam provides now, monotonic and interruptible sleep with a shared default.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-16: the monotonic clock is non-decreasing and the wall clock is not used for elapsed time.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-17: sleep rejects negative durations and honors cancellation, re-asserting the interrupt flag.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-18: a non-blocking scheduled delay cancels its task on cancel.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-19: async-wrapper unwrapping is cycle-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-20: an interruptible task future leaves a clean interrupt state.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-21: an orphaned closeable result is closed on discard.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:53-53` · high · sha:0451cc7f3bb4</sub>
- CFG-22: the proxy model is immutable and masks credentials.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-23: glob bypass matches the full string case-insensitively.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-24: proxy resolution has defined precedence, never throws, takes host and port from the same layer and uses credentials only for https.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-25: a proxy port is an explicit in-range value or null.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-26: the non-proxy list has defined precedence and escape handling.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-27: a single bare `*` means bypass-all.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-28: proxy environment resolution is opt-in.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:54-54` · high · sha:0451cc7f3bb4</sub>
- CFG-29: HTTP dates are formatted in canonical RFC 1123.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-30: HTTP date parsing is tolerant, with informational weekday and zone aliases.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-31: HTTP date parsing is strict on all other aspects.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-32: a non-blocking, non-cryptographic type-4 UUID generator exists.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-33: deep value equality is content-based and null-safe.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-34: deep equality has NaN, signed-zero and kind-distinct array semantics.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-35: a shared retryability classifier exists where implemented.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-36: a build/runtime descriptor falls back to a non-blank `unknown`.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:55-55` · high · sha:0451cc7f3bb4</sub>
- CFG-1 fixes four configuration tiers: explicit override, environment by exact key, a system-property source by normalised key, then the caller default.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:207-208` · high · sha:ddf8f695ff61</sub>
- CFG-3's key normalisation (MAX_RETRY_ATTEMPTS to max.retry.attempts) becomes Dexpace__Retry__MaxRetryAttempts to Dexpace:Retry:MaxRetryAttempts, case-insensitive, and CFG-4's raw accessor is vacuous because nothing normalises away casing.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:223-226` · high · sha:ddf8f695ff61</sub>
- CFG-6's strict boolean is kept in spirit so that only true/false succeed, and the DI package closes the hex-acceptance gotcha with its own integer converter because CFG-5's base-10 intent is not negotiable.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:246-248` · high · sha:ddf8f695ff61</sub>
- The DI package registers a TimeSpan converter implementing CFG-7 exactly: ISO-8601 through System.Xml.XmlConvert.ToTimeSpan (verified PT5S gives 5 s and PT-5S is rejected), <number><unit> shorthand, a bare number as milliseconds, and negatives rejected.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:254-256` · high · sha:ddf8f695ff61</sub>
- Options become sealed records with init accessors derived with `with` (CFG-8, CFG-9 copy-on-write), consistent with house guide 6.1 and 10.2, which makes CFG-10's override removal vacuous.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:261-263` · high · sha:ddf8f695ff61</sub>
- Nested RetryOptions and RedirectOptions must be records too, because `with` is a shallow copy and otherwise a derived copy shares a mutable child (P7).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:263-265` · high · sha:ddf8f695ff61</sub>
- CFG-37's null guards are ArgumentNullException.ThrowIfNull under nullable reference types, and CFG-38 is satisfied by construction since typed values only arrive through the one binding path.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:268-270` · high · sha:ddf8f695ff61</sub>
- Options validation is an IValidateOptions<DexpaceClientOptions> with ValidateOnStart, placed in the DI package (roadmap phase 9).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:270-271` · high · sha:ddf8f695ff61</sub>
- An SDK-managed Http.SystemNet transport calls ProxyOptions.FromEnvironment() explicitly at construction and installs its own glob-matching IWebProxy, while a caller-supplied HttpClient keeps whatever proxy it has (TRANSPORT-15).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:286-290` · high · sha:ddf8f695ff61</sub>

## Constraints
- CFG-2's empty-is-absent rule is not the binder's behaviour: an environment variable set to the empty string is a present, empty value to IConfiguration, and binding it into a typed option fails ValidateOnStart rather than falling through to the next tier.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:226-228` · high · sha:ddf8f695ff61</sub>
- The binder's TimeSpanConverter parses "1000" as one thousand days (verified: 1000.00:00:00) where CFG-7 requires a bare number to be interpreted as milliseconds, and rejects both "PT5S" and "500ms" with FormatException (verified), so "AttemptTimeout": "1000" would silently yield a timeout of nearly three years.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:250-254` · high · sha:ddf8f695ff61</sub>
- The as-built options are sealed classes with public setters passed by reference into every PipelineContext, so mutating options.Retry.MaxRetryAttempts while a request is in flight changes that request's retry budget mid-operation.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:258-261` · high · sha:ddf8f695ff61</sub>
- HttpClient's native HTTP(S)_PROXY/NO_PROXY handling diverges from the spec: NO_PROXY=* does not bypass (HttpClient.DefaultProxy.IsBypassed returned false for every host), against CFG-27.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:273-278` · high · sha:ddf8f695ff61</sub>
- HttpClient accepts and defaults a proxy URL with no port, against CFG-25's "An absent port in a proxy URL MUST be treated as invalid".
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:277-279` · high · sha:ddf8f695ff61</sub>
- GetProxy returns the proxy URI with its userinfo, so logging the resolved proxy leaks the credentials that CFG-22 requires masked.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:279-280` · high · sha:ddf8f695ff61</sub>
- WebProxy.BypassList entries are regular expressions, not globs (new WebProxy(..., ["*.internal.example.com"]) throws RegexParseException and an entry a.b matches host axb), against CFG-23's glob grammar.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:280-282` · high · sha:ddf8f695ff61</sub>
- The runtime default proxy reads the environment implicitly when the first handler is built, where CFG-28 says the environment MUST be consulted only when a resolver is explicitly invoked.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:282-284` · high · sha:ddf8f695ff61</sub>

## Conclusions
- The configuration subsystem's porting goal is behavioral parity: same precedence, same never-throw lookup, same immutability, and the same substitutable env/property/time seams so conformance tests run hermetically.
  <sub>spec · `docs/product-spec/16-configuration.md:3-3` · high · sha:367e27ec6481</sub>
- A separate configurable materialisation cap, defaulting to 64 MiB (the Ruby port's figure: an order of magnitude above a sane payload and an order below a typical worker's heap), applies before Array.MaxLength and is part of the options surface (design section 8.2), because 2 GiB is far above what a client SDK should hold in one array.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:83-86` · high · sha:da6000c93fc5</sub>
- Core uses plain options types and no bespoke environment/override reader (the position taken by PR #4), because the siblings hand-rolled one and IConfiguration covers it; core holds no lookup at all, only the options types the lookup binds into.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:208-215` · high · sha:ddf8f695ff61</sub>
- Layered configuration via Microsoft.Extensions.Configuration (an ordered provider stack, later wins, consumed through IOptions<T>) is installed from NuGet, so P2 does not retire the concern; it moves it out of core into the separately installable Dexpace.Sdk.Extensions.DependencyInjection package (NFR-2).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:211-215` · high · sha:ddf8f695ff61</sub>
- The system-property tier is replaced by appsettings.json because .NET has no ambient store for it (AppContext switches are the nearest analogue but are process knobs for the runtime, not application settings) (P11).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:217-222` · high · sha:ddf8f695ff61</sub>
- Configuration tier mapping: tier 1 (explicit override) is the Action<DexpaceClientOptions> passed to AddDexpaceClient registered after binding so it wins; tier 2 is AddEnvironmentVariables(); tier 3 is appsettings.json and its environment overlay, below environment variables, preserving CFG-1's order of override > environment > third source > default; tier 4 is the property initializer on the options record.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:217-223` · high · sha:ddf8f695ff61</sub>
- CFG-11's substitutable seams are provided by AddInMemoryCollection in tests, which never touches the real environment.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:228-229` · high · sha:ddf8f695ff61</sub>
- CFG-13's process-wide global configuration slot is deliberately not provided: the container is the scope, and a static mutable configuration slot is the service-locator pattern that the house guide (docs/styleguide/csharp-aspnetcore/02-dependency-injection.md 2.1) rejects.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:229-232` · high · sha:ddf8f695ff61</sub>
- CFG-14's key constants become one SectionName = "Dexpace" constant plus the property names.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:232-233` · high · sha:ddf8f695ff61</sub>
- The port deliberately fails fast on bad typed configuration values at startup (ValidateOnStart, per house guide 1.4), taking the ecosystem's side against CFG-5's "MUST NOT throw on missing or unparseable values" fallback to defaults, because a silently defaulted misconfiguration goes unseen until production while a ValidateOnStart failure fails the deploy; recorded as section 10 entry 26 against CFG-5 to CFG-7.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:236-246` · high · sha:ddf8f695ff61</sub>
- If the AOT-safe configuration-binding source generator does not bind init accessors (to be verified when the DI package is built), the package binds into a private mutable staging type and snapshots it into the record, Ruby's staging-object pattern for a different reason; the reflection binder does bind init accessors.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:265-268` · high · sha:ddf8f695ff61</sub>
- The earlier ruling that proxies are out of scope is overturned: core owns an immutable ProxyOptions record (type, address, ordered glob bypass list compiled once, credentials masked in ToString, explicit bypass-all flag) and an explicit ProxyOptions.FromEnvironment() resolver implementing CFG-24 to CFG-27.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:273-286` · high · sha:ddf8f695ff61</sub>
- CFG-24's first layer (https.proxyHost-style system properties) has no .NET source, and the explicit ProxyOptions argument stands in for it, the same tier substitution as in configuration.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:290-292` · high · sha:ddf8f695ff61</sub>

## Reference
- The configuration subsystem comprises layered string-keyed lookup with copy-on-write derivation plus utility primitives: injectable clock, non-blocking delay/cancellation helpers, environment-driven proxy model, RFC 1123 dates, non-blocking UUID generator, shared retryability classifier, build/runtime identity descriptor, and deep value equality.
  <sub>spec · `docs/product-spec/16-configuration.md:3-3` · high · sha:367e27ec6481</sub>
- Conformance for layered lookup: register an override, distinct env and distinct property values and assert override wins, then env, then property, then default as each higher layer is removed.
  <sub>spec · `docs/product-spec/16-configuration.md:7-7` · high · sha:367e27ec6481</sub>
- Conformance for never-throw accessors: getInt on "not-a-number" returns the default and on "-5" returns -5.
  <sub>spec · `docs/product-spec/16-configuration.md:15-15` · high · sha:367e27ec6481</sub>
- Conformance for the strict boolean accessor: getBoolean("TRUE") parses, while getBoolean("1"), "yes" and "on" return the supplied default.
  <sub>spec · `docs/product-spec/16-configuration.md:16-16` · high · sha:367e27ec6481</sub>
- Duration accessor conformance examples: PT5S yields 5s, 500ms yields 500ms, 1000 yields 1s, PT-5S yields the default, and 5x yields the default.
  <sub>spec · `docs/product-spec/16-configuration.md:17-17` · high · sha:367e27ec6481</sub>
- Glob conformance example: *.internal.example.com matches subdomains case-insensitively but not the apex, and a . in a pattern matches literally.
  <sub>spec · `docs/product-spec/16-configuration.md:43-43` · high · sha:367e27ec6481</sub>
- Non-proxy list conformance examples: a\|b|c yields [a|b, c], and a\,b,c yields [a,b, c].
  <sub>spec · `docs/product-spec/16-configuration.md:46-46` · high · sha:367e27ec6481</sub>
- The binder's BCL TypeConverters fail loudly: BooleanConverter rejects "1" with FormatException (and accepts " TRUE "), and Int32Converter parses "010" as 10 but also accepts "0x10" as 16 (all verified).
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:240-243` · high · sha:ddf8f695ff61</sub>
- As built (d45e64b) for configuration: options are mutable classes shared by reference (CFG-8), proxy is delegated to HttpClient defaults (CFG-22 to CFG-28), the version fallback is 0.0.0 (CFG-36); not built are the DI package's binding, validation and duration/integer converters, ProxyOptions, and the date parser as a shared utility.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:312-314` · high · sha:ddf8f695ff61</sub>

## Conflicts

## Superseded


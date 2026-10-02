# url-and-query-encoding

## Rules
- The form encoder for application/x-www-form-urlencoded bodies (HTTP-38, BODY-35) is hand-written to the WHATWG serializer because WebUtility.UrlEncode disagrees on ~*!(); the two functions have different names and tests and are never interchanged.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:656-658` · high · sha:da6000c93fc5</sub>
- Core renders URLs for the wire, for logs and for equality through AbsoluteUri (or GetComponents), and Uri.ToString() joins the banned-API list.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:674-675` · high · sha:da6000c93fc5</sub>
- Query names are case-sensitive (ordinal), a name whose value list is empty is dropped at build time (HTTP-30), and equality is order-sensitive so two instances are equal iff they encode identically.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:259-261` · high · sha:3aa554d9287d</sub>
- Query encoding uses Uri.EscapeDataString per component, which is RFC 3986 exact (section 3.5).
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:261-261` · high · sha:3aa554d9287d</sub>
- Query parsing (HTTP-31) is lenient and total through Uri.UnescapeDataString, which leaves a malformed escape as raw text and `+` as `+`, the opposite of WebUtility.UrlDecode's form behaviour.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:261-263` · high · sha:3aa554d9287d</sub>

## Constraints
- The EscapeDataString behavior holds for .NET Core only; pre-4.5 .NET Framework escaped a different set, so code copied from Framework-era sources would inherit the wrong set.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:654-656` · high · sha:da6000c93fc5</sub>
- System.Uri removes dot segments including percent-encoded ones (https://h/pets/../admin becomes https://h/admin; %2E%2E and %2e%2e collapse too), and Uri.EscapeDataString("..") returns ".." because dots are unreserved, so a path parameter of ".." escapes its segment even after correct encoding.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:662-667` · high · sha:da6000c93fc5</sub>
- Uri.ToString() is a display form, not a wire form (new Uri("https://h/a%20b?x=%26&y=a%20b").ToString() is https://h/a b?x=%26&y=a b), whereas AbsoluteUri keeps %20.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:671-673` · high · sha:da6000c93fc5</sub>
- new Uri(baseUri, relative) follows RFC 3986 reference resolution, so new Uri(new Uri("https://h/c?sig=1"), "pets") is https://h/pets (last base segment and base query dropped) while a trailing slash on the base yields https://h/c/pets.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:676-679` · high · sha:da6000c93fc5</sub>
- On Linux Uri.TryCreate("/rel", UriKind.Absolute, ...) succeeds with scheme file, so an absoluteness check must also require scheme http or https (as the as-built Request constructor does), and a backslash in a path is silently converted to '/'; both behaviours are pinned by tests.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:682-685` · high · sha:da6000c93fc5</sub>

## Conclusions
- Uri.EscapeDataString is the right tool for HTTP-29, HTTP-32 and path segments because it implements RFC 3986 component encoding exactly (unreserved set A-Za-z0-9-._~, UTF-8 for non-ASCII); on input a b*~+/!()' it yields a%20b%2A~%2B%2F%21%28%29%27 (verified on 10.0.401).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:643-648` · high · sha:da6000c93fc5</sub>
- Uri.UnescapeDataString is HTTP-31's lenient decode: "a+b%2" round-trips as a+b%2, with + staying + and a malformed escape staying raw (verified).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:652-654` · high · sha:da6000c93fc5</sub>
- Query (HTTP-28 to HTTP-32) is an insertion-ordered immutable list of name/value pairs with a nested Builder, not a dictionary, because HTTP-28 requires multi-value support with order preserved and a value-less parameter modelled as a single empty-string value distinct from an absent name.
  <sub>design · `docs/sdk-design-dotnet/04-domain-model-construction.md:256-258` · high · sha:3aa554d9287d</sub>

## Reference
- WebUtility.UrlEncode produces form-style output (+ for space), encodes ~ and leaves *!() unescaped, so it is neither RFC 3986 nor the WHATWG form serializer (a%20b input yields a+b*%7E%2B%2F!()%27).
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:649-649` · high · sha:da6000c93fc5</sub>
- Uri.EscapeUriString is obsolete (SYSLIB0013) and escapes only outside the broad URI set, so '/' and '+' survive and a path value could inject a segment.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:650-650` · high · sha:da6000c93fc5</sub>
- UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true } preserves dot-segment forms but disables canonicalisation for the whole URL including the caller's base address.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:666-668` · high · sha:da6000c93fc5</sub>
- Encoded reserved characters (%2F, %26) survive Uri canonicalisation, which REDIR-13 needs, while unreserved escapes are normalised (%41 to A, %7E to ~), which RFC 3986 section 6.2.2.2 declares equivalent.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:673-675` · high · sha:da6000c93fc5</sub>

## Conflicts

## Superseded


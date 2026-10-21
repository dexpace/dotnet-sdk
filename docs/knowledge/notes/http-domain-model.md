# http-domain-model — notes

Hand-written. `../harvested/http-domain-model.md` is what the documents say; this is what the implementation found, and it wins.
Facts measured on .NET SDK 10.0.401 / runtime 10.0.12, Linux, so a later reader does not have to re-measure them.

## Conclusions
- **`HTTP-17`'s trim of surrounding whitespace is outbound-only; a header name received from the wire is validated against the token grammar without trimming, so a name with a space or HTAB at either end is refused.** The shared name rule trimmed SP and HTAB at both ends before validating, so a received `" X-A"` or `"X-A\t"` was stored as `X-A`, against `HTTP-19` (inbound names stay strictly validated) and `XCUT-18` (a name with HTAB is rejected). The reference transport never reaches the path, because `HttpHeaders.TryAddWithoutValidation` refuses such names and the runtime's parser rejects them first (V15); third-party transports and direct callers do. `Headers.Builder.AddInbound` therefore validates with `HeaderSyntax.ValidateInboundName` (design §11 items 36 and 70, amendment N11-2, P11-14; `HeaderInjectionValidationTests.The_inbound_path_does_not_trim_a_name`).
  <sub>review · `docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-design.md` · high · sha:manual-p11-htab-edge</sub>

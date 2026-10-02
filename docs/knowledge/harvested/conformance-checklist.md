# conformance-checklist

## Rules
- The conformance kit calls every transport with a null request and asserts a faulted task, not a thrown exception.
  <sub>design · `docs/sdk-design-dotnet/03-seam-by-seam-idiomatic-mapping.md:454-455` · high · sha:da6000c93fc5</sub>
- A failing conformance item the port has decided not to satisfy is reported as a failure by the kit and suppressed in this repository's build by a named waiver listing the requirement ID, so the gap stays visible.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:274-275` · high · sha:27bc3ba15ac4</sub>

## Constraints

## Conclusions
- The specification's Appendix B conformance checklist covers only PAGE, SSE, SERDE, OBS, CFG, TRANSPORT, ASYNC, XCUT and NFR, so Appendix B conformance is a strictly weaker claim than full conformance, and the port says so (§11 item 9).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:262-265` · high · sha:27bc3ba15ac4</sub>
- B.4's shared-inert-event identity item (OBS-1) is restated as the zero-allocation assertion because there is no event object to be identical (§10 entry 22).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:267-268` · high · sha:27bc3ba15ac4</sub>
- B.7 is restated because .NET has no async-runtime adapters to test: the pivot is the runtime's Task (§3.3), so ASYNC-1, ASYNC-2, ASYNC-13, ASYNC-14, ASYNC-19, ASYNC-20 and ASYNC-22 run against the transport SPI and the sync bridge, and ASYNC-8 through ASYNC-12 run against AsyncLocal flow.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:269-272` · high · sha:27bc3ba15ac4</sub>
- Adapter-scoped items (ASYNC-21) are reported as vacuous-by-antecedent (§11 item 21).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:272-273` · high · sha:27bc3ba15ac4</sub>

## Reference
- A porter runs the Appendix B conformance checklist to prove a reimplementation conforms to the product spec.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:3-3` · high · sha:0451cc7f3bb4</sub>
- Each Appendix B checklist item references the requirement IDs it exercises, and items are organized by subsystem.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:3-3` · high · sha:0451cc7f3bb4</sub>
- A conformance check passes only when the observable behavior matches the requirement.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:3-3` · high · sha:0451cc7f3bb4</sub>
- Appendix B items B.1-B.3 are exercised as written except the items §10 records, namely the live-page and executor-mode items of B.1 (§10 entries 17 and 19).
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:265-267` · high · sha:27bc3ba15ac4</sub>
- B.5 is exercised against IConfiguration binding with the four-tier order of §8.2, and B.6 is exercised per transport by the kit.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:268-269` · high · sha:27bc3ba15ac4</sub>
- B.9 is the chapter's gate table, with NFR-8/NFR-9 applicable and exercised.
  <sub>design · `docs/sdk-design-dotnet/09-toolchain-and-quality-gates.md:273-274` · high · sha:27bc3ba15ac4</sub>

## Conflicts

## Superseded


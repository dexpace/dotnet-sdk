# non-functional-requirements

## Rules
- Specific numbers and tool names (80% coverage, the shrinker, the exact static analyzers) are the JVM instantiation, and the requirement is that the equivalent gate exists and blocks.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:3-3` · high · sha:5f4684bf7123</sub>
- NFR-1: core has zero concrete runtime dependencies beyond the standard library plus a compile-only logging facade.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:85-85` · high · sha:0451cc7f3bb4</sub>
- NFR-2: adapters depend on core plus at most one third-party library.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:85-85` · high · sha:0451cc7f3bb4</sub>
- NFR-3: the public surface is explicit and minimal.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:86-86` · high · sha:0451cc7f3bb4</sub>
- NFR-4: a machine-comparable API snapshot gates drift and regeneration is deliberate.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:86-86` · high · sha:0451cc7f3bb4</sub>
- NFR-5: an aggregate coverage floor is enforced by the default build.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:87-87` · high · sha:0451cc7f3bb4</sub>
- NFR-6: warnings are errors.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:87-87` · high · sha:0451cc7f3bb4</sub>
- NFR-7: lint and static analysis are fatal with documented scoped waivers.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:87-87` · high · sha:0451cc7f3bb4</sub>
- NFR-17: all quality gates are automatic and blocking.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:87-87` · high · sha:0451cc7f3bb4</sub>
- NFR-8: a shrinker keep-configuration is shipped for the reflective and SPI surface.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:88-88` · high · sha:0451cc7f3bb4</sub>
- NFR-9: a shrink-and-run regression guard runs in the default build.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:88-88` · high · sha:0451cc7f3bb4</sub>
- NFR-10: a runtime floor is declared with higher-floor features isolated, and the emitted target and visible API agree.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:89-89` · high · sha:0451cc7f3bb4</sub>
- NFR-11: core is concurrency-model agnostic with no async-framework types in the public surface.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:90-90` · high · sha:0451cc7f3bb4</sub>
- NFR-12: artifacts are reproducible and byte-identical.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:91-91` · high · sha:0451cc7f3bb4</sub>
- NFR-13: every file carries a license header.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:91-91` · high · sha:0451cc7f3bb4</sub>
- NFR-14: versions and coordinates have a single source of truth.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:91-91` · high · sha:0451cc7f3bb4</sub>
- NFR-15: version metadata is resolvable at runtime and the placeholder never appears in packaged artifacts.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:91-91` · high · sha:0451cc7f3bb4</sub>
- NFR-16: artifacts are signed on the release/CI path and signing is optional locally.
  <sub>spec · `docs/product-spec/appendix-b-conformance-test-checklist.md:91-91` · high · sha:0451cc7f3bb4</sub>

## Constraints

## Conclusions
- The non-functional invariants are language-neutral cross-cutting guarantees that a faithful port must ship with the same footprint, stability and safety promises, enforced by its own ecosystem's equivalent tooling.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:3-3` · high · sha:5f4684bf7123</sub>

## Reference

## Conflicts

## Superseded


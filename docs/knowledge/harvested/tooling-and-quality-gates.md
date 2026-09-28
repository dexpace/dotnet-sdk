# tooling-and-quality-gates

## Rules
- The build SHOULD enforce a minimum aggregate line-coverage floor, currently 80%, across the library units wired into the default build lifecycle, excluding sample/example code, test-only guards, and test fixtures. (NFR-5)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:17` · high · sha:5f4684bf7123</sub>
- Compiler warnings SHOULD be treated as errors across every unit, including deprecation warnings. (NFR-6)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:18` · high · sha:5f4684bf7123</sub>
- The build SHOULD run automated style/lint and static-analysis checks with findings treated as fatal, and where an analyzer cannot run on a given unit's toolchain, disabling it SHOULD be a narrowly-scoped, documented exception with explicit re-enable conditions rather than a silent global relaxation. (NFR-7)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:19` · high · sha:5f4684bf7123</sub>
- The quality gates backing the compatibility snapshot, coverage floor, warnings-as-errors, lint/static-analysis, shrink-survival, and runtime-floor checks MUST be enforced automatically and be blocking, failing the standard build/CI rather than being advisory. (NFR-17)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:20` · high · sha:5f4684bf7123</sub>
- In target ecosystems that support whole-program dead-code elimination/tree-shaking/minification, the SDK MUST ship the keep/retain configuration a downstream shrinker needs so its reflectively-reached and runtime-wired surface survives shrinking, covering the runtime-wired SPI seams, immutable models, and reflectively-bound types. (NFR-8)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:24` · high · sha:5f4684bf7123</sub>
- The shipped shrinker keep-configuration SHOULD be guarded by an automated regression check, wired into the default build, that shrinks a real consumer using only the shipped rules and runs it end-to-end against a live round-trip, failing the build if any runtime-wired or reflectively-reached surface is stripped. (NFR-9)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:25` · high · sha:5f4684bf7123</sub>
- Build artifacts SHOULD be reproducible: identical source inputs SHOULD yield byte-for-byte identical output artifacts, with embedded timestamps normalized/stripped and entry ordering deterministic. (NFR-12)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:37` · high · sha:5f4684bf7123</sub>
- Every source file SHOULD carry the project's license/SPDX header block, enforced in the reference as a review convention rather than a mechanical gate. (NFR-13)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:38` · high · sha:5f4684bf7123</sub>
- Dependency versions, plugin/tool versions, and project coordinates SHOULD live in a single source of truth rather than being restated per unit, so a bump is ideally a one-line edit applying uniformly. (NFR-14)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:39` · high · sha:5f4684bf7123</sub>
- Published artifacts SHOULD embed self-identifying version metadata the SDK can resolve at runtime, so runtime-emitted identifiers such as a User-Agent report the real version rather than an unknown placeholder. (NFR-15)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:40` · high · sha:5f4684bf7123</sub>
- Published artifacts SHOULD be cryptographically signed for provenance, with signing enforced on the release/CI path and made gracefully optional in local builds lacking signing keys. (NFR-16)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:41` · high · sha:5f4684bf7123</sub>

## Constraints

## Conclusions
- Specific numbers and tool names in the non-functional requirements, such as 80% coverage, the shrinker, and the exact static analyzers, are the JVM reference's particular instantiation; a faithful port need only ensure the equivalent gate exists and blocks.
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:3` · high · sha:5f4684bf7123</sub>

## Reference
- The aggregate coverage floor is a minimum line-coverage percentage computed across all library units combined, not per-unit, excluding samples and test-support code, enforced by the default build.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:5` · high · sha:f0b3d2058626</sub>
- A quality gate is an automated, build-blocking check that fails the standard build when its condition is not met, such as coverage floor, API-snapshot drift, warnings, lint/static-analysis, shrink-survival, or runtime-floor.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:51` · high · sha:f0b3d2058626</sub>
- Shrink-survival keep-configuration is the retain/keep rules the SDK ships so a downstream whole-program shrinker does not eliminate reflectively-reached or runtime-wired surface.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:63` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded

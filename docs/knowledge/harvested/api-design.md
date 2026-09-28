# api-design

## Rules
- The public API surface SHOULD be explicit and minimal, with every exported declaration deliberately public and typed, implementation details kept non-exported, and each adapter's public surface as small as its capability allows. (NFR-3)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:12` · high · sha:5f4684bf7123</sub>
- The public API of every published unit SHOULD be captured in a checked-in, machine-comparable snapshot with the build failing on drift, and an intentional API change is landed by regenerating and committing the snapshot in the same change rather than using the regeneration tool to silence an unintentional break. (NFR-4)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:13` · high · sha:5f4684bf7123</sub>

## Constraints

## Conclusions

## Reference

## Conflicts

## Superseded

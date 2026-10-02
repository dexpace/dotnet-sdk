# tooling-and-quality-gates — notes

Hand-written. `../harvested/tooling-and-quality-gates.md` is what the documents say; this is what the port decided, and it wins.

## Conflicts
- **`LangVersion` stays `latest`.** Resolves `tooling-and-quality-gates/db6f60f0` in favour of the SDK and overrides `tooling-and-quality-gates/a31d5855`: `Directory.Build.props` sets the C# language version to `latest` rather than the named `14.0`, the unchanged last clause of the SDK overlay's `.NET 10 / C# 14` row. The `net10.0` half of that row is conformed (`tooling-and-quality-gates/4fbbe369`) and owes nothing. The SDK is pinned to SDK `10.0.401` by `global.json`, so the language version already tracks a reviewed diff; roadmap constraint 6 (phase 0 decision: the target framework conformed, the language version kept).
  <sub>review · `docs/styleguide/README.md` · high · sha:manual-p0-langversion</sub>

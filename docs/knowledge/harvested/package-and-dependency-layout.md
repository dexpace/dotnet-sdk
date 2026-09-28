# package-and-dependency-layout

## Rules
- The core module MUST depend only on the language standard library, the runtime, and a compile-time-only logging facade, and MUST NOT carry a runtime dependency on any concrete HTTP transport, serialization library, I/O implementation, or async framework. (NFR-1)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:7` · high · sha:5f4684bf7123</sub>
- Each optional capability (transport, serialization format, I/O backend, async bridge) SHOULD be a separately installable unit depending on the core plus at most one third-party library, so a consumer composes only the units it uses. (NFR-2)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:8` · high · sha:5f4684bf7123</sub>
- The SDK MUST declare a lowest-supported-runtime floor and target it for all general-purpose units; a capability requiring a newer runtime MUST be isolated into its own unit declaring the higher floor explicitly and MUST NOT be a hard dependency of the general-purpose core, and no produced artifact may reference runtime/stdlib APIs absent on the floor it declares. (NFR-10)
  <sub>spec · `docs/product-spec/20-non-functional-requirements-and-quality-bar.md:29` · high · sha:5f4684bf7123</sub>

## Constraints

## Conclusions

## Reference
- An adapter unit is a separately installable module supplying one concrete capability by depending on the core plus at most one third-party library, keeping its public surface minimal, so consumers compose only the units they need.
  <sub>spec · `docs/product-spec/appendix-a-glossary.md:3` · high · sha:f0b3d2058626</sub>

## Conflicts

## Superseded

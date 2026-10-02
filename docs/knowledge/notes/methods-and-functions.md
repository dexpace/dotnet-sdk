# methods-and-functions — notes

Hand-written. `../harvested/methods-and-functions.md` is what the documents say; this is what the port decided, and it wins.

## Conflicts
- **`CA1062` stays dialled down to `none`, with its rationale.** Resolves `methods-and-functions/1fe02512` in favour of the SDK and overrides `methods-and-functions/fb3de2c4`, `api-design/9a903cbb`, `error-handling/c236117b`, `type-system/7d4cf514` and `tooling-and-quality-gates/86513cc4` as far as each names `CA1062` as the enforcement of public-precondition assertions (the last also lists `CA2007` and `IDE0005`, which the port does enforce): nullable reference types make a non-nullable parameter a compile-time contract, and `ArgumentNullException.ThrowIfNull` still guards the public entry points by hand. The reasoning is design section 9.4 (`tooling-and-quality-gates/9e589c3f`), design section 10 entry 28 (whose `CA2007` half was retired when `CA2007` was re-enabled for `src/`), the SDK overlay's recorded row, and roadmap constraint 6 (phase 0 decision: kept). The guard-clause and assertion rules themselves are not relaxed.
  <sub>review · `docs/styleguide/README.md` · high · sha:manual-p0-ca1062</sub>

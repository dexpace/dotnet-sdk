# data-modeling — notes

Hand-written. `../harvested/data-modeling.md` is what the documents say; this is what the port decided, and it wins.

## Superseded
- **A closed choice is an abstract class with nested sealed classes where a record would leave it open.** Supersedes `data-modeling/cee5cdd6` for `Dexpace.Sdk.Core.Recovery.Outcome`: styleguide 6.3 models a choice as an abstract record closed by a private constructor with sealed record cases, but a non-sealed record carries a synthesised `protected` copy constructor that any record outside the assembly can call, so the set is not closed (verified on SDK 10.0.401; see `retry-and-resilience/302d143d`'s note). The rest of 6.3 holds: one abstract base, every case sealed, consumed by a `switch` whose discard arm is `throw new UnreachableException()` because the compiler still reports `CS8509`. A departure recorded in the styleguide SDK overlay; phase 4b ruling P4b-3, open for the lead.
  <sub>review · `docs/styleguide/README.md` · high · sha:manual-p4b-closed-class</sub>

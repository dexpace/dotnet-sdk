# retry-and-resilience — notes

Hand-written. `../harvested/retry-and-resilience.md` is what the documents say; this is what the port decided, and it wins.

## Superseded
- **`Outcome` is an abstract class with nested sealed classes, because an abstract record closed by a private constructor is not closed.** Supersedes `retry-and-resilience/302d143d`: design §5.2's "record hierarchy closed by a private constructor" is false on .NET. The compiler synthesises a `protected` copy constructor `Outcome(Outcome original)` on a non-sealed record, C# requires it to be `public` or `protected`, and an outside `public sealed record Evil : Outcome` compiled and ran (verified on SDK 10.0.401 in phase 4b, and re-confirmed by the implementation's own build: `OutcomeTests.The_assembly_holds_exactly_two_types_deriving_from_Outcome` and `Outcome_has_no_non_private_constructor`). An abstract class with a private constructor and two nested sealed classes is closed (an outside subclass fails with `CS0122`), and a `switch` over the two still reports `CS8509`, so the discard arm stays. `RECOV-1`'s "jointly exhaustive" holds; record value equality and `with` are given up, which `Outcome` does not want. Phase 4b ruling P4b-3, open for the lead.
  <sub>review · `docs/sdk-design-dotnet/05-pipeline-architecture.md` · high · sha:manual-p4b-outcome-class</sub>

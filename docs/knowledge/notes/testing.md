# testing — notes

Hand-written. `../harvested/testing.md` is what the documents say; this is what the port decided, and it wins.

## Conflicts
- **Tests assert with xUnit's `Assert` alone; Shouldly is not adopted.** Resolves `testing/48e77518` in favour of the SDK and overrides `testing/c54172a7` and `testing/12cfc26a`: no test project references Shouldly and `Directory.Packages.props` pins no such package. The runner half of the overlay's xUnit row is conformed (`testing/b1ecd34a` was narrowed to it): xUnit v3 on Microsoft.Testing.Platform is in use. This note records the assertion style the repository actually has; adopting Shouldly later is a reviewed change that retires it. FluentAssertions v8+ stays banned. This is a phase 0 judgement not named in roadmap constraint 6's table, which the lead can reverse.
  <sub>review · `docs/styleguide/csharp/11-testing.md` · high · sha:manual-p0-shouldly</sub>

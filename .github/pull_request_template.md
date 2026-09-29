## What and why

<!-- One concern per pull request. What changes, and why. Link the issue: "Closes #…". -->

## Requirements

<!-- The docs/product-spec/ requirement IDs this satisfies, changes or touches (e.g. HTTP-7, RETRY-13),
     and where each is cited: the test file's header comment, the branch it forced. "None" for a
     change no requirement governs (tooling, docs). -->

## Verification

<!-- What you ran, on which OS, with which .NET SDK. Tick what applies. CONTRIBUTING.md lists every gate. -->

- [ ] `dotnet build` (Release, 0 warnings), `dotnet format --verify-no-changes` and `dotnet test --solution Dexpace.Sdk.sln` green on ____
- [ ] Coverage floor, `dotnet pack`, the dependency audit and the reproducible pack green (for a change under `src/` or to a dependency)
- [ ] The NativeAOT smoke consumer publishes and runs (for a change under `src/`)
- [ ] A test that failed before this change and passes after it (for a fix or a behaviour change), with its `[Trait("Category", …)]`
- [ ] `PublicAPI.Unshipped.txt` updated, and every changed `packages.lock.json` committed
- [ ] `dotnet run --project .claude/skills/housekeeping/src -- probe` is clean (for a change under `docs/`, `CLAUDE.md` or a README)

## Records

- [ ] `CHANGELOG.md` has an `[Unreleased]` line, with **Breaking** marked where it applies, or the change is not user-visible
- [ ] A deviation from the reference contract is recorded in the owning Deviation Ledger and design §10, or there is none
- [ ] Release work this uncovers or closes is recorded in `docs/first-release.md`, or there is none
- [ ] The checklist, the package README and any `docs/sdk-documentation/` page still describe the code
- [ ] Nothing under `docs/product-spec/`, `docs/sdk-design-dotnet/`, `docs/styleguide/` or `docs/knowledge/harvested/` was edited, except as a dated, stated correction

# Contributing

Thanks for your interest in the dexpace .NET SDK. The work follows a v1 roadmap of thirteen phases
([`docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md`](docs/work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md));
**nothing is published yet**: every package under `src/` is at `0.0.1-alpha.1`, and what remains
before the first release is tracked in [`docs/first-release.md`](docs/first-release.md). Pull
requests are welcome; this page is the flow they go through.

## Setup

The repository is one solution, `Dexpace.Sdk.sln`: three packages under `src/`, their test projects
and a NativeAOT smoke consumer under `tests/`. A second solution, `tools/Dexpace.Tools.sln`, builds
the repository's own tools (the knowledge CLI and the housekeeping skill). Every project targets
**`net10.0`**, and the .NET SDK is pinned in `global.json` (`10.0.401`, rolling forward to the latest
patch only). Nothing else is needed; NuGet packages come from nuget.org (`nuget.config`).

```bash
git clone https://github.com/dexpace/dotnet-sdk.git
cd dotnet-sdk
dotnet restore Dexpace.Sdk.sln --locked-mode
```

Every project commits a `packages.lock.json`, and restores are locked. If you change
`Directory.Packages.props` or a reference, run a plain `dotnet restore` on both solutions and commit
every lock file that moved.

## Quality gates

The build is the lint gate: `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended` and
`EnforceCodeStyleInBuild` turn every analyzer finding into an error, including the public-API files
(RS0016/RS0017), the banned-API list (RS0030), the 70-line method cap (MA0051), the license header
(IDE0073), `ConfigureAwait(false)` in library code (CA2007), and a missing `///` doc comment on a
public member (CS1591). CI (`.github/workflows/ci.yml`) runs these steps, and every one runs locally:

```bash
dotnet restore Dexpace.Sdk.sln --locked-mode
dotnet build   Dexpace.Sdk.sln --configuration Release --no-restore
dotnet format  Dexpace.Sdk.sln --verify-no-changes --no-restore
rm -rf artifacts/test-results                                              # a stale report would count too
dotnet test    --solution Dexpace.Sdk.sln --configuration Release --no-build \
               --results-directory artifacts/test-results --report-trx --coverlet --coverlet-output-format cobertura
dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80         # 80% line coverage over src/
scripts/ci/coverage-gate-selftest.sh artifacts/test-results                # the gate fails closed
dotnet pack    Dexpace.Sdk.sln --configuration Release --no-build --output artifacts/packages
dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages      # core's dependency rule
scripts/ci/reproducible-pack.sh                                            # two packs, byte-identical
dotnet publish tests/Dexpace.Sdk.AotSmoke --configuration Release --output artifacts/aot-smoke
./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke                                 # NativeAOT smoke (Linux)
dotnet restore tools/Dexpace.Tools.sln --locked-mode
dotnet build   tools/Dexpace.Tools.sln --configuration Release --no-restore
dotnet test    --solution tools/Dexpace.Tools.sln --configuration Release --no-build
scripts/knowledge verify-structure
```

Tests are xUnit v3 on Microsoft.Testing.Platform, so `dotnet test` takes the platform's options:
`--project tests/Dexpace.Sdk.Core.Tests`, `--filter-class "*RetryPolicyTests"`,
`--filter-trait "Category=Security"`. CI runs the build job on Linux, Windows and macOS.
Run the suites through `dotnet test`, not `dotnet run`: `Directory.Build.targets` supplies
`--coverlet-file-prefix <TestProject>` to every project that references coverlet.MTP, which xUnit's own runner
rejects and which you must not pass again yourself.

If your change touches documentation (`docs/`, `CLAUDE.md`, a README), also run the drift probe and
fix what it reports:

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
```

## How a change is made

The work here is **spec-driven**. `docs/product-spec/` holds the numbered requirements — `HTTP-7`,
`SEAM-1`, `RETRY-13`, … — and `docs/product-spec/appendix-c-consolidated-normative-requirement-index.md`
is the index; `docs/sdk-design-dotnet/` says how each maps onto .NET. Before changing behaviour, find
the requirement IDs the change must satisfy (`scripts/knowledge --req RETRY-13`), and cite them: in
the test's header comment, beside a non-obvious branch, and in the pull request.

- **Bug fixes** go with a test that fails before the fix and passes after it (TDD), naming the
  requirement the bug violated.
- **Every test** carries a `[Trait("Category", …)]`: `Unit`, `Integration`, `Conformance`,
  `AotSmoke` or `Security`. A `Security` test is a permanent regression test; it is never deleted
  or loosened.
- **A change to the public surface** is a reviewed `PublicAPI.Unshipped.txt` diff plus a
  `CHANGELOG.md` `[Unreleased]` line, with **Breaking** marked where it is.
- **Work that belongs to the release** — a blocker, something v1 ships without, a post-release
  trigger — is recorded in `docs/first-release.md` in the same change.
- **A deliberate difference from the reference contract** is a deviation, not a quiet choice: it
  goes into the phase design's Deviation Ledger and design §10.
- **Larger work** — a roadmap phase — starts with a design, a plan and a checklist mapping every
  requirement ID in scope to a task, under `docs/work/mvp/phaseN[/phaseNx]/`. The roadmap's
  "How Phases Get Executed" section is the procedure.

[`CLAUDE.md`](CLAUDE.md) is the working summary of the conventions, including the constraints that
most often bite. The ones every change meets:

- **Branch off `main`, and target `main`.** A roadmap phase's branch is `<issue>-phase-<N[x]>-<slug>`;
  a stacked pull request targets the branch below it until that one merges.
- **`docs/product-spec/`, `docs/sdk-design-dotnet/`, `docs/styleguide/` and `docs/knowledge/` are
  frozen to routine work.** They are the yardstick, the binding design, the vendored styleguide and
  the harvested corpus; a change to the design is a dated correction. See `docs/README.md`.
- **Core's dependency rule is exact.** `Dexpace.Sdk.Core` references the shared framework,
  `Microsoft.Extensions.Logging.Abstractions`, and build-only packages, and nothing else
  (**SEAM-1**, design §2.4); an adapter package depends on core plus at most one third-party
  library (**NFR-2**).
- **Versions are central.** A package version lives in `Directory.Packages.props`; a
  `PackageReference` carries no `Version`.
- **The two-line MIT header** opens every `.cs` file, source and tests alike.

## Commit messages

Use the prefixes the history already follows, with a subject under 72 characters
([`docs/styleguide/git-and-code-review.md`](docs/styleguide/git-and-code-review.md)) and a body wrapped
at 72:

| Prefix   | Use for                          |
|----------|----------------------------------|
| `feat:`  | new features                     |
| `fix:`   | bug fixes                        |
| `chore:` | refactors and cleanup            |
| `docs:`  | documentation-only changes       |
| `test:`  | tests only                       |
| `ci:`    | CI configuration                 |

## Pull requests

The pull request template asks for the requirement IDs the change touches, the gates you ran and
the records you updated. Keep a pull request to one concern; CI must be green on every operating
system before it is merged.

## Reporting issues

Open one at [github.com/dexpace/dotnet-sdk/issues](https://github.com/dexpace/dotnet-sdk/issues)
using the bug-report or feature-request template. For security vulnerabilities, follow
[`SECURITY.md`](SECURITY.md) instead of opening a public issue. Everyone taking part is expected to
follow the [Code of Conduct](CODE_OF_CONDUCT.md).

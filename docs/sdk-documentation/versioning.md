# Versioning

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). This page says what a version number of the dexpace .NET SDK promises, what
counts as the public API it protects, what a `0.y` minor may break, and how to depend on the packages so a bump does not surprise you. The policy is the phase 12 design's (P12-6, over
[design §2.3](../sdk-design-dotnet/02-project-and-solution-layout.md)); the maintainer's side of the same train (the release commit, the tag, the next-train commit) is in
[`docs/first-release.md`](../first-release.md), and the gates that hold it are in [`quality-gates.md`](./quality-gates.md).

## Where the SDK is today

Nothing is published. Every package is built at `0.1.0-alpha.1` (`VersionPrefix` `0.1.0`, `VersionSuffix` `alpha.1` in `Directory.Build.props`), the first publish is held
([`docs/first-release.md`](../first-release.md)), and until it happens nothing here is a promise to anyone. The sections below describe the policy the first release starts under.

## One version for five packages

The packages are versioned in **lockstep**: `Dexpace.Sdk.Core`, `Dexpace.Sdk.Http.SystemNet`, `Dexpace.Sdk.Serialization.SystemTextJson`, `Dexpace.Sdk.Extensions.DependencyInjection` and
`Dexpace.Sdk.Conformance` always carry the same version, even when one of them did not change. One `VersionPrefix` and one `VersionSuffix` stamp:

- the five packages;
- their dependencies on one another (`Dexpace.Sdk.Http.SystemNet` depends on `Dexpace.Sdk.Core` at its own version, and so on);
- every assembly's `AssemblyInformationalVersion`, which the SDK reads for its `User-Agent` and which a test pins against the package version (`NFR-14`, `NFR-15`).

The reason is NuGet's: a `ProjectReference` becomes a floor-only dependency (`>= x.y.z`), so independently versioned adapters would admit a core and adapter pair that was never built together.
**Install all the `Dexpace.Sdk.*` packages you use at one version.** Skew shows up as a downgrade warning from NuGet (`NU1605`) or, worse, a `MissingMethodException` or `TypeLoadException` at the first
call into a changed member.

## What the public API is

The version number protects four things:

1. **The public API**: what the five `PublicAPI.Shipped.txt` files list, and their documented behaviour (the `///` comments and these pages). The build checks the lists against the code
   (`RS0016`, `RS0017`), so a public member that is not listed does not compile.
2. **The conformance kit's contract**: the assertion IDs and their statuses in `Dexpace.Sdk.Conformance` ([`conformance.md`](./conformance.md)). A transport or codec you wrote against the kit stays green or
   red for the same reasons.
3. **The configuration keys** in `DexpaceConfigurationKeys` ([`dependency-injection.md`](./dependency-injection.md)).
4. **The telemetry names**: the log event names and ids, and the span and metric names and their attribute sets (`OBS-*`, [`logging-and-redaction.md`](./logging-and-redaction.md),
   [`tracing-and-metrics.md`](./tracing-and-metrics.md)).

An `internal` member is not part of it, however it is reachable. The friend grant (`InternalsVisibleTo`) goes to each library's own test project only (`NFR-3`).

## Before 1.0 (`0.y.z`)

| A release | May contain | Never contains |
|---|---|---|
| **patch** (`0.y.z` to `0.y.z+1`) | fixes; an addition only when it fixes a defect | a break of the public API or of documented behaviour |
| **minor** (`0.y` to `0.y+1`) | everything a patch does, new API, and breaks of the public API or of documented behaviour | an unannounced break |

Every break in a minor is:

- a `CHANGELOG.md` line marked **Breaking**, under the version that carries it, in the same change that made it;
- a reviewed `PublicAPI` diff, where a removal is a `*REMOVED*` line in `PublicAPI.Unshipped.txt`;
- from the first publish on, a reviewed `CompatibilitySuppressions.xml` entry for package validation against the baseline (`PackageValidationBaselineVersion`), so a binary break is a decision somebody wrote down.

Where it costs nothing, a removal is preceded by one minor in which the member is `[Obsolete]` (styleguide 10.6): you get a compiler warning and a pointer before you get a compile error.

**Adding a conformance assertion is at least a minor**, because it can turn an adapter's green run red: an assertion you did not have to pass last month is a requirement this month
([`conformance.md`](./conformance.md)). The same goes for tightening a rule the kit already checks.

## From 1.0

Strict SemVer 2.0. A breaking change is a major version, and it goes through `[Obsolete]` first. A minor adds; a patch fixes. The public API is the same four things.

## Pre-release labels, and the `0.1.0` train

`main` always carries the next version with a pre-release label, so a build from `main` is never mistaken for a release. The first train:

| Step | Version | Who does it |
|---|---|---|
| `main` today | `0.1.0-alpha.1` | `VersionPrefix` `0.1.0`, `VersionSuffix` `alpha.1` |
| the release commit | `0.1.0` | empties `VersionSuffix`, dates the `CHANGELOG.md` section (`## [0.1.0] - <date>`) |
| the tag | `v0.1.0` | the release workflow, once it is allowed to run, publishes from the tag |
| the commit after it | `0.2.0-alpha.1` | opens the next train |

A tag that carries a suffix, such as `v0.1.0-alpha.1`, is also accepted by the release preflight, so a pre-release can be published first to exercise the release path; it costs one more publish. A
pre-release version sorts before its release (`0.1.0-alpha.1 < 0.1.0`), and NuGet resolves a pre-release only when you ask for one (`--prerelease`, or a pre-release version in the reference).

## How to depend on it

Pin the minor while the major is `0`, because a minor may break:

```xml
<ItemGroup>
  <PackageReference Include="Dexpace.Sdk.Core" Version="[0.1.0,0.2.0)" />
  <PackageReference Include="Dexpace.Sdk.Http.SystemNet" Version="[0.1.0,0.2.0)" />
  <PackageReference Include="Dexpace.Sdk.Serialization.SystemTextJson" Version="[0.1.0,0.2.0)" />
</ItemGroup>
```

A patch inside that range is a fix you want. From 1.0 a floating minor (`1.*`) is what SemVer is for. If a library of yours exposes a `Dexpace.Sdk` type in its own public API, treat its version as part of
yours: a minor of the SDK that breaks you is a minor of yours (before 1.0) or a major (after).

## Reading the changelog

[`CHANGELOG.md`](../../CHANGELOG.md) follows Keep a Changelog. The head of `[Unreleased]` carries a draft of the release notes, which becomes the `## [0.1.0]` section at the release commit: a summary, the breaking changes to read first, and every requirement the release
ships without or meets only in part (a named list, because the register says the release notes must state each). A line marked **Breaking** is the one to grep for when you upgrade a minor. The XML doc of the member
a change affects carries the same **Breaking** note, so the IDE shows it.

## What does not follow this policy

The repository's own tools (`tools/`, `.claude/skills/`), the test projects and the NativeAOT smoke consumer are not packed and have no version promise. A document under `docs/work/` is a record of what was
decided at the time, not a description of the current API.

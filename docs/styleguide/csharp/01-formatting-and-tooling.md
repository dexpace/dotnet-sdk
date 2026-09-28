# 01 — Formatting & Tooling

Formatting is not a matter of taste here; it is a build artifact. One `.editorconfig`, one `dotnet format`, one analyzer baseline, applied identically on every machine and in CI. This chapter fixes the project file, the language version, and the gates so that style review never spends a word on whitespace and every warning is an error before it merges.

## What good looks like

```xml
<!-- Directory.Build.props — one source of truth for every project in the solution -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>                          <!-- 1.4 — NRT is law -->
    <ImplicitUsings>disable</ImplicitUsings>             <!-- 1.5 — usings are explicit -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>  <!-- 1.2 — no warning survives -->
    <AnalysisLevel>latest-Recommended</AnalysisLevel>    <!-- 1.3 — analyzers on -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

One `Directory.Build.props` at the repository root sets the framework and the gates for every project beneath it (1.1), so no `.csproj` re-states them and none can drift. `Nullable` is `enable`, not `annotations` (1.4); warnings are errors (1.2); analyzers run at build with style enforced (1.3); and `ImplicitUsings` is off so every dependency is visible at the top of the file (1.5, chapter [12](./12-project-organization.md)). Formatting itself is delegated entirely to `.editorconfig` and `dotnet format` (1.6).

## Rules

### 1.1 — Centralize build configuration in `Directory.Build.props`; keep `.csproj` files thin.

**Reasoning, step by step:**
1. A property repeated in every `.csproj` is a property that will drift: one project lands on an older `LangVersion`, another forgets `TreatWarningsAsErrors`, and the gate is silently uneven across the solution. Configuration that must be identical everywhere belongs in exactly one file.
2. MSBuild imports `Directory.Build.props` into every project under its directory automatically. Put the framework, language version, nullable setting, and analyzer gates there; an individual `.csproj` then carries only its own `PackageReference`s and `ProjectReference`s. The result is reviewable in one place and impossible to forget.

**Worked example:**
```xml
<!-- A leaf .csproj — no framework, no gates, just this project's dependencies -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
</Project>
```
**Enforcement:** review; a `<TargetFramework>` or `<Nullable>` inside a leaf `.csproj` is a finding.

### 1.2 — Treat every warning as an error.

**Reasoning, step by step:**
1. A warning that does not break the build is a warning nobody reads. Compiler and analyzer warnings flag exactly the holes this guide cares about — a possible null deref (CS8602), an unawaited task (CS4014), an unobserved `using` — and a yellow squiggle in one developer's IDE is invisible to the next.
2. `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` makes the gate binary: it compiles clean or it does not merge. The escape hatch is a *narrow, justified* `#pragma warning disable CSxxxx` with a why-comment around the single offending line, never a project-wide `<NoWarn>` list, which silences the diagnostic for code not yet written.

**Worked example:**
```csharp
#pragma warning disable CA2007 // top-level statements have no SynchronizationContext to capture
await host.RunAsync();
#pragma warning restore CA2007
```
**Enforcement:** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; `<NoWarn>` entries reviewed and rejected absent a recorded reason.

### 1.3 — Run the Roslyn analyzers at build with code style enforced.

**Reasoning, step by step:**
1. The .NET SDK ships the code-quality (CA) and code-style (IDE) analyzers; they are the mechanical enforcement substrate this guide leans on, and most rules below name the CA or IDE diagnostic that backs them. Off by default at build, they catch nothing in CI.
2. `<AnalysisLevel>latest-Recommended</AnalysisLevel>` opts into the current recommended rule set, and `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` makes `.editorconfig` style rules (naming, `var`, `this.`) fail the build rather than merely decorate the editor. Severities are tuned in `.editorconfig`, not by weakening the level.

**Worked example:**
```ini
# .editorconfig — promote the rules this guide treats as law
dotnet_diagnostic.CA2007.severity = error   # ConfigureAwait in libraries (chapter 09)
dotnet_diagnostic.CA1062.severity = error   # validate public arguments (chapter 05)
dotnet_diagnostic.IDE0005.severity = error  # remove unnecessary usings
```
**Enforcement:** `<AnalysisLevel>` and `<EnforceCodeStyleInBuild>` in `Directory.Build.props`; per-rule severity in `.editorconfig`.

### 1.4 — Set `<Nullable>enable</Nullable>` solution-wide and never weaken it per file.

**Reasoning, step by step:**
1. Nullable reference types are this language's defense against the null deref, and they only work end to end. A single `#nullable disable` file is a hole through which a `null` flows untyped into code that trusts the annotations, and the failure surfaces far from the disabled file.
2. `enable` turns on both the annotation context (`string?` means nullable) and the warning context (flow analysis complains when you ignore it). `annotations` alone is a half-measure that records intent without enforcing it. The full discipline — and the ban on `!` that papers over it — is chapter [03](./03-nullability-and-the-type-system.md); this rule just fixes the switch on, globally.

**Worked example:**
```csharp
#nullable disable // banned — a typed hole; migrate the file instead (chapter 03)
```
**Enforcement:** `<Nullable>enable</Nullable>` in `Directory.Build.props`; a `#nullable disable` directive is a review finding.

### 1.5 — Format with `.editorconfig` + `dotnet format`: Allman braces, four spaces, file-scoped namespaces.

**Reasoning, step by step:**
1. The runtime's rule 1 is "use Visual Studio defaults": Allman braces (every brace on its own line), four spaces and no tabs, one statement and one declaration per line, no more than one blank line between members. These are not preferences to litigate per project; they are the canonical layout, encoded once in `.editorconfig` and applied by `dotnet format`.
2. File-scoped namespaces (`namespace X;`) are the modern default — one namespace per file, no wasted indentation level. `using` directives sit *outside* the namespace, `System.*` first then the rest alphabetically, so a name's origin is unambiguous (MS Learn, chapter [12](./12-project-organization.md)). A pre-commit `dotnet format --verify-no-changes` makes the layout a gate, not a suggestion.

**Worked example:**
```csharp
using System;                       // System.* first
using Microsoft.Extensions.Logging; // then alphabetical

namespace Dexpace.Billing;          // file-scoped — no indentation tax

public sealed class InvoiceTotals
{
    public decimal Net { get; init; }
}
```
**Enforcement:** `.editorconfig` (`csharp_style_namespace_declarations = file_scoped`, `dotnet_sort_system_directives_first = true`); `dotnet format --verify-no-changes` in pre-commit and CI.

### 1.6 — Pin the SDK; keep the toolchain reproducible.

**Reasoning, step by step:**
1. A build that uses whatever SDK happens to be installed is a build that differs between laptops and CI, which makes "works on my machine" a real defect rather than a joke. The language version and analyzer behaviour both track the SDK.
2. A `global.json` pins the SDK band, `<Deterministic>true</Deterministic>` makes the output byte-stable, and `dotnet restore --locked-mode` against a committed `packages.lock.json` freezes the dependency graph. The version is named explicitly (`14.0`), never `latest`, so an upgrade is a reviewed diff and not a surprise on the next machine to build.

**Worked example:**
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestPatch" } }
```
**Enforcement:** committed `global.json` and `packages.lock.json`; CI restores with `--locked-mode`.

### 1.7 — Cap method length at 70 lines via the analyzer.

**Reasoning, step by step:**
1. A method that runs past 70 lines is doing more than one thing, which makes it hard to name, hard to test, and hard to assert over (chapter [05](./05-methods-and-functions.md)). The cap is a forcing function: it converts "this is getting long" from a vague feeling into a build failure, so the extraction happens now rather than never.
2. The number is the deliberate dexpace value, set at Go's level and not scaled down for C#; it is an addition the runtime style does not make, recorded in the [README](./README.md) ledger. Enforce it with a Roslyn analyzer (`Roslynator RCS1213`-class method-length rule, or `csharp-extensions` method-length) rather than trusting review to count lines.

**Worked example:**
```ini
# .editorconfig — fail the build past the cap, do not merely warn
dotnet_diagnostic.MA0051.severity = error  # method is too long
dotnet_code_quality.MA0051.maximum_lines = 70
```
**Enforcement:** Meziantou.Analyzer `MA0051` (or equivalent) configured to 70 lines, severity `error`.

## Cross-references

- Nullable discipline and the `!` ban this chapter switches on: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). The 70-line cap in practice: [05-methods-and-functions.md](./05-methods-and-functions.md).
- `using` placement, file-scoped namespaces, and curated global usings: [12-project-organization.md](./12-project-organization.md). `ConfigureAwait` and the CA2007 gate: [09-concurrency.md](./09-concurrency.md).
- Naming rules the `.editorconfig` enforces: [02-naming-conventions.md](./02-naming-conventions.md). Doc-file generation and CS1591: [14-documentation.md](./14-documentation.md).

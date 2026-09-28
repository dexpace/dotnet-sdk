# 12 — Project & Assembly Organization

A solution is read before it is run, and its shape is the first documentation a newcomer meets. This chapter fixes how files, namespaces, and projects are arranged so that location is predictable, dependencies flow one way, and a type's place in the system is obvious from its path. The build gates that enforce it are switched on in chapter [01](./01-formatting-and-tooling.md); here is the layout they police.

## What good looks like

```
src/
  Dexpace.Billing/                 # one bounded context, one assembly
    Directory.Build.props          # gates inherited from the root props
    GlobalUsings.cs                # curated, committed, small (12.7)
    Invoices/                      # a feature owns its types together (12.4)
      Invoice.cs                   #   namespace Dexpace.Billing.Invoices (12.1)
      InvoiceTotals.cs             #   one top-level type per file (12.2)
      InvoiceStore.cs              #   internal by default (12.5)
    Payments/
      Payment.cs
  Dexpace.Host/                    # entry assembly: composition only (12.8)
    Program.cs
Directory.Packages.props           # one version per package, centrally (12.3)
```

The folder path and the namespace are the same words in the same order — `Billing/Invoices` is `namespace Dexpace.Billing.Invoices` (12.1) — and each file holds exactly one top-level type named for the file (12.2). Types cluster by feature, not by technical kind, so `Invoices/` owns its store and totals together rather than scattering them across `Services/` and `Models/` buckets (12.4). Every type is `internal` until an assembly boundary needs it public (12.5), package versions are declared once in `Directory.Packages.props` (12.3), and the host assembly wires the contexts together without owning any business logic of its own (12.8).

## Rules

### 12.1 — Use file-scoped namespaces that mirror the folder path exactly.

**Reasoning, step by step:**
1. When the namespace tracks the folder, a name's location is derivable from its using and the reverse: see `Dexpace.Billing.Invoices.Invoice` and you know the file sits at `Billing/Invoices/Invoice.cs` without a search. Let the two drift and every navigation becomes a guess, because the compiler does not require them to agree.
2. The root namespace is the assembly name (`Dexpace.Billing`), and each folder beneath it appends one segment, so the path and the namespace are the same words in the same order. File-scoped form (`namespace X;`) states this once at the top of the file and spends no indentation level on it, which is the runtime default this guide already adopts (chapter [01](./01-formatting-and-tooling.md)).

**Worked example:**
```csharp
// file: Billing/Invoices/Invoice.cs
namespace Dexpace.Billing.Invoices;   // folder Billing/Invoices → these exact segments

public sealed record Invoice(InvoiceId Id, IReadOnlyList<InvoiceLine> Lines);
```
**Enforcement:** `IDE0161` (use file-scoped namespace) promoted to error; review that the namespace matches the path.

### 12.2 — Put one top-level type per file and name the file after the type.

**Reasoning, step by step:**
1. One type per file makes the filename a reliable index: `InvoiceStore` lives in `InvoiceStore.cs` and nowhere else, so the editor's file list is a type list and a rename touches one obvious file. Two top-level types in one file hide the second from that index and split its history across unrelated edits.
2. The exception the runtime grants is mechanical, not stylistic: a generic's arity may appear in the filename (`Result`1.cs` for `Result<T>`) so two arities coexist on disk, and tightly coupled nested types stay nested inside their owner rather than becoming separate top-level files. Otherwise the rule is absolute — the file is named for its single top-level type.

**Worked example:**
```csharp
// file: Billing/Invoices/InvoiceStore.cs — exactly this one top-level type
namespace Dexpace.Billing.Invoices;

internal sealed class InvoiceStore   // not also a top-level InvoiceQuery in the same file
{
    public Task<Invoice> Load(InvoiceId id, CancellationToken cancellationToken) => /* ... */;
}
```
**Enforcement:** review; a second top-level type in a file is a finding, generic-arity filenames excepted.

### 12.3 — Centralize package versions with Central Package Management; declare a version once.

**Reasoning, step by step:**
1. A version repeated in every `.csproj` is a version that will diverge: one project references `System.Text.Json` at 9.0.0, another at 9.0.2, and the runtime loads whichever wins, producing bugs that reproduce in one assembly and not another. The version of a package across a solution must be a single number, declared in a single place.
2. Central Package Management puts every version in `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`; a leaf `.csproj` then names the package with no `Version` attribute and inherits the central one. This pairs with the thin-`.csproj` rule of chapter [01](./01-formatting-and-tooling.md): the leaf says *what* it depends on, the central file says *which version*.

**Worked example:**
```xml
<!-- Directory.Packages.props at the repo root — one version per package -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="System.Text.Json" Version="10.0.0" />
  </ItemGroup>
</Project>
```
**Enforcement:** `Directory.Packages.props` with central management on; a `Version` attribute on a leaf `PackageReference` is a review finding.

### 12.4 — Organize by feature folder, not by technical layer.

**Reasoning, step by step:**
1. A `Services/`, `Models/`, `Controllers/` layout groups files by what they *are* and scatters a single feature across all three, so adding a field to invoicing means edits in three distant folders and a change is never local. Grouping by feature (`Invoices/`, `Payments/`) puts everything one capability needs in one folder, so the change is one folder deep and the blast radius is visible.
2. Cohesion should follow the domain, not the framework. A feature folder owns its record, its store, its validation, and its endpoints together; the folder name is a domain word a stakeholder would recognize, not a technical noun. This keeps the public surface of each feature small and lets a whole capability move or be deleted as a unit (chapter [10](./10-api-design.md)).

**Worked example:**
```
Billing/
  Invoices/   Invoice.cs  InvoiceStore.cs  InvoiceValidator.cs   # one feature, all its parts
  Payments/   Payment.cs  PaymentStore.cs
# not: Services/  Models/  Validators/   — a feature smeared across technical buckets
```
**Enforcement:** review of folder layout; top-level technical buckets (`Services`, `Models`, `Controllers`) are a finding.

### 12.5 — Make `internal` the default visibility; widen to `public` only across an intended API boundary.

**Reasoning, step by step:**
1. A `public` type is a promise to every other assembly that its shape will not break, so each one widens the surface you must keep stable and analyze (chapter [10](./10-api-design.md)). Most types exist to serve their own assembly; marking them `public` by reflex exports implementation detail and invites coupling you never intended to support.
2. Default every type to `internal` and promote to `public` only when another assembly must call it as a deliberate API. Within the assembly `internal` costs nothing — the whole context still sees the type — so the narrower default loses no convenience while keeping the cross-assembly contract intentional. `InternalsVisibleTo` opens internals to a test assembly without going public.

**Worked example:**
```csharp
namespace Dexpace.Billing.Invoices;

internal sealed class InvoiceStore { /* serves this assembly only */ }   // default
public sealed record Invoice(InvoiceId Id);                             // crosses the boundary on purpose
```
**Enforcement:** review of `public` on new types; the public-API analyzer of chapter [10](./10-api-design.md) tracks the exported surface.

### 12.6 — Keep project references a directed acyclic graph pointing inward toward the domain.

**Reasoning, step by step:**
1. A cycle between two projects is two projects that must compile, version, and be reasoned about as one, which defeats the point of splitting them; .NET forbids a literal `ProjectReference` cycle, but a logical cycle smuggled through a shared "common" project is just as corrosive. The reference graph must be acyclic, so the build order is total and each assembly has a clear set of things it may know about.
2. Direction is not arbitrary: dependencies point inward toward the domain, never outward toward infrastructure or the host. The domain assembly references nothing of yours; an application assembly references the domain; the host references both. Inverting any of these — a domain that references the database project — couples the stable core to a volatile edge and is the defect this rule exists to catch.

**Worked example:**
```
Dexpace.Host  →  Dexpace.Billing.Application  →  Dexpace.Billing.Domain
                                                  (references none of ours)
# arrows point inward; no arrow ever points back out, and no cycle closes
```
**Enforcement:** project-reference review; an architecture test (12 below) or `dotnet` build-order inspection rejects an outward or cyclic edge.

### 12.7 — Curate global usings in one committed `GlobalUsings.cs`; never auto-generate them.

**Reasoning, step by step:**
1. `<ImplicitUsings>enable</ImplicitUsings>` injects an SDK-chosen, version-dependent set of namespaces that appear in no file, so a reader cannot tell from the source what a file depends on and the set silently shifts when the SDK does. A dependency that is invisible is a dependency that cannot be reviewed; chapter [01](./01-formatting-and-tooling.md) turns implicit usings off for exactly this reason.
2. Instead, list the genuinely ubiquitous namespaces by hand in a single committed `GlobalUsings.cs` with `global using` directives. The set is small, obvious, and diffable — adding one is a reviewed line — and a per-file `using` still appears for anything not in it, so a file's real dependencies stay legible at its top. Keep the global set to namespaces nearly every file uses; a namespace two files need belongs in those two files.

**Worked example:**
```csharp
// GlobalUsings.cs — the whole curated set for this assembly, committed and reviewed
global using System;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;
```
**Enforcement:** `<ImplicitUsings>disable</ImplicitUsings>` (chapter [01](./01-formatting-and-tooling.md)); `IDE0005` removes unused usings; review keeps the global set small.

### 12.8 — Split assemblies along bounded-context seams; keep the host free of business logic.

**Reasoning, step by step:**
1. An assembly is the unit of deployment, versioning, and the `internal` boundary, so its edges should fall where the domain's edges fall — one assembly per bounded context, named for that context (`Dexpace.Billing`). Splitting on technical lines instead (a `DataAccess` assembly spanning every context) cuts across cohesion and forces unrelated changes to ship together.
2. The entry assembly — the host or composition root — exists to wire those contexts together: it reads configuration, builds the dependency graph, and starts the process. It must hold no business rule, because a rule that lives in the host cannot be tested without booting the host and cannot be reused by another entry point. Push every rule down into a context assembly and leave the host as plumbing (chapter [10](./10-api-design.md)).

**Worked example:**
```csharp
// Dexpace.Host/Program.cs — composition only, zero domain logic
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBilling(builder.Configuration);   // each context exposes one registration
builder.Services.AddPayments(builder.Configuration);
await builder.Build().RunAsync();
```
**Enforcement:** review of assembly seams and host contents; a NetArchTest rule asserts the host references contexts and contains no domain types.

### 12.9 — Enforce the dependency direction with an architecture test.

**Reasoning, step by step:**
1. The acyclic, inward-pointing graph of 12.6 and the layered seams of 12.8 are invariants, and an invariant guarded only by review erodes the first time a hurried reference points the wrong way. Architecture is too important to leave to vigilance; make the rule executable so a violating reference fails CI like any other broken test (chapter [11](./11-testing.md)).
2. NetArchTest (or an equivalent) expresses the constraint as an assertion over the loaded assemblies: the domain depends on nothing of ours, no context references a sibling it should not, the host owns no domain types. The test is cheap, runs with the suite, and turns "we agreed dependencies point inward" into a fact the build verifies rather than a convention people remember.

**Worked example:**
```csharp
[Fact]
public void Domain_depends_on_no_other_dexpace_assembly()
{
    var result = Types.InAssembly(typeof(Invoice).Assembly)
        .Should().NotHaveDependencyOn("Dexpace.Billing.Application")
        .GetResult();
    result.IsSuccessful.ShouldBeTrue();   // Shouldly, chapter 11
}
```
**Enforcement:** NetArchTest assertions in the test suite, run in CI alongside chapter [11](./11-testing.md).

## Cross-references

- The build gates this layout relies on — thin `.csproj`, implicit usings off, file-scoped namespaces: [01-formatting-and-tooling.md](./01-formatting-and-tooling.md). The branded types features cluster around: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md).
- The public-API surface that `internal`-by-default protects: [10-api-design.md](./10-api-design.md). The architecture tests that enforce dependency direction: [11-testing.md](./11-testing.md).

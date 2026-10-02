# module-organization

## Rules
- Use file-scoped namespaces (`namespace X;`) whose segments mirror the folder path exactly, with the assembly name as the root namespace and each folder appending one segment (styleguide 12.1).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:27-40` · high · sha:a44b6f9eaba9</sub>
- Each file holds exactly one top-level type and is named after that type; the only exceptions are a generic arity in the filename (such as `Result`1.cs` for `Result<T>`) and tightly coupled nested types, which stay nested in their owner (styleguide 12.2).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:42-58` · high · sha:a44b6f9eaba9</sub>
- Organize code by feature folder (such as Invoices/, Payments/) rather than by technical layer (Services/, Models/, Controllers/, Validators/), so a feature owns its record, store, validation and endpoints together (styleguide 12.4).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:80-93` · high · sha:a44b6f9eaba9</sub>
- Keep project references a directed acyclic graph pointing inward toward the domain, never outward toward infrastructure or the host (styleguide 12.6).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:110-122` · high · sha:a44b6f9eaba9</sub>
- The domain assembly references no other assembly of the solution, an application assembly references the domain, and the host references both; a domain referencing a database project is the defect the rule exists to catch.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:114-114` · high · sha:a44b6f9eaba9</sub>
- Curate global usings by hand in a single committed GlobalUsings.cs file and never auto-generate them (ImplicitUsings disabled) (styleguide 12.7).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:124-138` · high · sha:a44b6f9eaba9</sub>
- Keep the global-using set to namespaces nearly every file uses; a namespace needed by only two files belongs in a per-file using in those two files.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:128-128` · high · sha:a44b6f9eaba9</sub>
- Split assemblies along bounded-context seams, one assembly per bounded context named for that context (for example Dexpace.Billing), rather than along technical lines such as a DataAccess assembly spanning all contexts (styleguide 12.8).
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:140-143` · high · sha:a44b6f9eaba9</sub>
- The entry (host or composition-root) assembly only wires contexts together by reading configuration, building the dependency graph and starting the process, and must hold no business rule, with each context exposing one registration.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:144-153` · high · sha:a44b6f9eaba9</sub>

## Constraints
- .NET forbids a literal ProjectReference cycle, but a logical cycle smuggled through a shared "common" project is equally corrosive and must also be avoided.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:113-113` · high · sha:a44b6f9eaba9</sub>

## Conclusions
- Feature-folder organization is preferred over technical-layer buckets because layered buckets scatter one feature across distant folders, while feature folders keep a change local, keep each feature's public surface small, let a capability move or be deleted as a unit, and name folders with domain words a stakeholder would recognize.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:82-84` · high · sha:a44b6f9eaba9</sub>
- Implicit usings are rejected because they inject an SDK-chosen, version-dependent set of namespaces that appear in no file, making dependencies invisible to review and silently shifting with the SDK.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:127-127` · high · sha:a44b6f9eaba9</sub>
- Business rules are kept out of the host because a rule in the host cannot be tested without booting the host and cannot be reused by another entry point; technical-line assembly splits are rejected because they cut across cohesion and force unrelated changes to ship together.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:143-144` · high · sha:a44b6f9eaba9</sub>

## Reference
- The reference layout places each bounded context as one assembly under src/ with its own Directory.Build.props inheriting root gates, a committed GlobalUsings.cs, and feature folders whose files each hold one internal-by-default type, plus a Dexpace.Host entry assembly containing only Program.cs and a root Directory.Packages.props.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:7-23` · high · sha:a44b6f9eaba9</sub>
- Rule 12.1 is enforced by IDE0161 (use file-scoped namespace) promoted to error, plus review that the namespace matches the path.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:40-40` · high · sha:a44b6f9eaba9</sub>
- Rule 12.2 is enforced by review only; a second top-level type in a file is a finding, with generic-arity filenames excepted.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:58-58` · high · sha:a44b6f9eaba9</sub>
- Rule 12.4 is enforced by review of folder layout; top-level technical buckets named Services, Models or Controllers are a finding.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:93-93` · high · sha:a44b6f9eaba9</sub>
- Rule 12.6 is enforced by project-reference review and by an architecture test or build-order inspection that rejects an outward or cyclic edge.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:122-122` · high · sha:a44b6f9eaba9</sub>
- Rule 12.7 is enforced by ImplicitUsings set to disable, IDE0005 removing unused usings, and review keeping the global set small.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:138-138` · high · sha:a44b6f9eaba9</sub>
- Rule 12.8 is enforced by review of assembly seams and host contents, and by a NetArchTest rule asserting the host references contexts and contains no domain types.
  <sub>styleguide · `docs/styleguide/csharp/12-project-organization.md:154-154` · high · sha:a44b6f9eaba9</sub>
- Chapter 12 covers file-scoped namespaces matching folders, one top-level type per file, Directory.Build.props, curated global usings, acyclic project references, and internal as default.
  <sub>styleguide · `docs/styleguide/csharp/README.md:44-44` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded


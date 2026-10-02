# documentation

## Rules
- Put a worked `<example>` containing a `<code>` block on non-obvious public API, such as builders assembled in order, Result values matched rather than dereferenced, or tokens threaded from a CancellationTokenSource (rule 14.5).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:102-105` · high · sha:250a9c84218b</sub>
- Reserve `<example>` for non-obvious usage, not trivial getters; code inside an `<example>` is real code held to the styleguide's own rules, short and correct.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:106-106` · high · sha:250a9c84218b</sub>
- Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in published library projects (or Directory.Build.props) so the compiler emits the XML doc file and raises CS1591 for public members lacking a `<summary>`, and treat CS1591 as an error via warnings-as-errors (rule 14.6).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:122-125` · high · sha:250a9c84218b</sub>
- Scope the CS1591 documentation gate to assemblies whose surface is consumed by others (published NuGet packages, shared libraries); an application leaf assembly with no public API need not carry the flag.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:126-126` · high · sha:250a9c84218b</sub>
- Link other members with compiler-checked `<see cref="..."/>` and parameters with `<paramref name="..."/>`, never bare prose names such as "see the Resolve method", so a rename breaks the build rather than silently rotting the doc (rule 14.7).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:137-140` · high · sha:250a9c84218b</sub>
- Use `<paramref>` for parameters and type parameters within a member's own doc, and `<see langword="null"/>` or `<c>` for keywords and literals, so every named thing in the doc is either compiler-checked or marked as code.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:141-141` · high · sha:250a9c84218b</sub>
- Delete commented-out code; version control remembers deleted lines, so old code belongs in history rather than in `//` blocks in the working file (rule 14.8).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:151-154` · high · sha:250a9c84218b</sub>
- A `// TODO` or `// HACK` ships only with a reference to a tracked issue, for example `// TODO(DEX-1234): remove once the v2 endpoint lands`.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:155-155` · high · sha:250a9c84218b</sub>
- A `// HACK` comment additionally states what the right fix is.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:155-155` · high · sha:250a9c84218b</sub>
- Every public type and member (and protected members on the one closed-hierarchy base) carries a `<summary>` stating what it is for, plus `<param>` for each parameter, `<returns>` for a non-void result, and `<typeparam>` for each generic parameter (rule 14.1).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:33-36` · high · sha:250a9c84218b</sub>
- Document the `<exception>`s a caller is expected to catch or avoid (precondition exceptions, domain exceptions), and omit exceptions the caller can do nothing about such as OutOfMemoryException.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:37-37` · high · sha:250a9c84218b</sub>
- Document a contract once on the interface and use `<inheritdoc/>` on overrides and interface implementations so the doc is not written and maintained twice (rule 14.2).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:49-52` · high · sha:250a9c84218b</sub>
- Beyond a bare `<inheritdoc/>`, add only new information (a `<remarks>` on this implementation's specific behaviour, or `<inheritdoc cref="..."/>` when the tool cannot infer the source), never restate the base doc.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:53-53` · high · sha:250a9c84218b</sub>
- An override that needs a different `<summary>` than its base is a design smell; an override must honour the base contract rather than redefine it.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:53-53` · high · sha:250a9c84218b</sub>
- Never restate the type or the obvious in XML doc; a summary such as "Gets the name" on `string Name` is noise and worse than no doc (rule 14.3).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:71-74` · high · sha:250a9c84218b</sub>
- Spend the summary on what the signature cannot express: the unit, the range or invariant, ownership (such as the caller disposing a returned stream), the threading guarantee, and boundary edge cases.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:75-75` · high · sha:250a9c84218b</sub>
- When nothing beyond the signature can be said, write a one-line summary stating the member's purpose, or improve the name and move on.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:75-75` · high · sha:250a9c84218b</sub>
- `//` comments explain why, not what; a comment that restates the code (such as "increment the counter" above `count++`) is dead weight and must not ship, and a comment with no statable why should not ship (rule 14.4).
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:87-90` · high · sha:250a9c84218b</sub>
- A `//` comment begins with a capital letter, ends with a period, and sits on its own line above the code it explains rather than trailing it.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:91-91` · high · sha:250a9c84218b</sub>
- Use `//` for all comments including multi-line blocks and avoid `/* */`, which does not nest and swallows code when a closing marker is forgotten.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:91-91` · high · sha:250a9c84218b</sub>
- XML doc and // comments explain reasoning rather than mechanics, and non-obvious public API carries <example>.
  <sub>styleguide · `docs/styleguide/csharp/README.md:65-65` · high · sha:1e6ba36fc337</sub>
- Enforcement notes in the guides name their rule, and a line whose reason cannot be stated should be questioned.
  <sub>styleguide · `docs/styleguide/csharp/README.md:65-65` · high · sha:1e6ba36fc337</sub>

## Constraints

## Conclusions
- Roadmap decision D2, ruled by the lead on 2026-09-29, replaced the thirteen pre-roadmap documents of 2026-06-14/15 (the platform design, ten slice designs and two plans) with the specification, the design and the roadmap, and those documents were deleted from the tree with git history keeping them.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:88-93` · high · sha:8e66b82361d2</sub>
- The overview chapter's citations of the retired 2026-06 documents were likewise repointed in place under roadmap decision D2 (ruled 2026-09-29), with no recorded decision changed.
  <sub>design · `docs/sdk-design-dotnet/01-overview.md:105-110` · high · sha:d7cea7b15cf3</sub>
- Roadmap decision D2 (ruled 2026-09-29) replaced the thirteen pre-roadmap documents of 2026-06-14/15 (platform design, ten slice designs, two plans) with the specification, the design and the roadmap, deleted them from the tree (git history keeps them), and repointed chapter 7's citations of them without changing any recorded decision.
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:10-15` · high · sha:68af5c6bf0ea</sub>
- Decision D2 (ruled 2026-09-29) replaced the thirteen pre-roadmap documents of 2026-06-14/15 with the specification, this design and the roadmap, deleted them from the tree (git history keeps them), and repointed citations to the owning section or to the pull request (#3-#9) that built the decision, without changing any recorded decision.
  <sub>design · `docs/sdk-design-dotnet/08-instrumentation-and-configuration.md:11-16` · high · sha:ddf8f695ff61</sub>
- The styleguide holds XML `///` doc and `//` comments to one standard: both explain why (constraints, units, ownership, hidden edge cases) and never restate what the compiler already proves.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:3-3` · high · sha:250a9c84218b</sub>

## Reference
- Citations of the retired 2026-06 documents in the porting-method chapter were edited in place to name the section that owns the decision or to state the decision inline with the building pull request (#3-#9), and no recorded decision changed.
  <sub>design · `docs/sdk-design-dotnet/00-porting-method.md:88-93` · high · sha:8e66b82361d2</sub>
- Rule 14.4 is enforced by review of comment content and form; `/* */` is flagged and comments restating mechanics are deleted.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:100-100` · high · sha:250a9c84218b</sub>
- Rule 14.5 is enforced by review requiring an `<example>` on non-obvious public API, which must obey the guide's own rules.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:120-120` · high · sha:250a9c84218b</sub>
- The generated XML documentation file feeds IntelliSense and doc-site tooling.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:126-126` · high · sha:250a9c84218b</sub>
- Rule 14.6 is enforced by `<GenerateDocumentationFile>` set for library projects and CS1591 promoted to error by `<TreatWarningsAsErrors>`.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:135-135` · high · sha:250a9c84218b</sub>
- Rule 14.7 is enforced by CA1200 (avoid cref tags with a prefix) and the cref-resolution warning; review rejects bare prose member names.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:149-149` · high · sha:250a9c84218b</sub>
- Rule 14.8 is enforced by IDE0005 and review deleting commented-out code, and review requiring an issue reference on every TODO/HACK.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:163-163` · high · sha:250a9c84218b</sub>
- Rule 14.1 is enforced by CS1591 (missing XML comment on public member) promoted to error for library assemblies, plus review of the param/returns/exception set.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:47-47` · high · sha:250a9c84218b</sub>
- Rule 14.2 is enforced by review requiring `<inheritdoc/>` on implementations and overrides; CS1591 is satisfied by the inherited doc.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:69-69` · high · sha:250a9c84218b</sub>
- Rule 14.3 is enforced by review rejecting signature-restating doc, favouring fewer higher-value comments over coverage for its own sake.
  <sub>styleguide · `docs/styleguide/csharp/14-documentation.md:85-85` · high · sha:250a9c84218b</sub>
- Chapter 14 covers XML /// doc on public API, <inheritdoc/>, never restating types, why-comments, <example> on non-obvious publics, GenerateDocumentationFile, and no commented-out code.
  <sub>styleguide · `docs/styleguide/csharp/README.md:46-46` · medium · sha:1e6ba36fc337</sub>

## Conflicts

## Superseded


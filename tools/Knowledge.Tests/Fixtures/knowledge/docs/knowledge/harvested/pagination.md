# pagination

## Rules
- A Page MUST be a closeable resource owning exactly one underlying response, and whoever pulls a page owns closing it (PAGE-1).
  <sub>spec · `docs/product-spec/12-pagination.md:20` · high · sha:5555eeee6666</sub>
- The item-level view MUST eager-close each page before yielding any of that page's items (PAGE-2).
  <sub>spec · `docs/product-spec/12-pagination.md:38` · high · sha:5555eeee6666</sub>

## Reference
- The design chapter's item-view snippet yields inside a try block with Dispose in the finally, which orders the dispose after the first item (PAGE-2).
  <sub>design · `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:12-18` · medium · sha:7777aaaa8888</sub>

## Conflicts
- **design vs styleguide: immutable page objects** — the design describes a Page as a mutable object disposed in place, while the styleguide requires value objects to be immutable records. Which wins for a closeable page needs settling (PAGE-3).
  <sub>design `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md:22` · styleguide `docs/styleguide/csharp/06-types-and-data-modeling.md:168-183` · unresolved 2026-01-01</sub>

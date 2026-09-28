# pagination — notes

Hand-written. `../harvested/pagination.md` is what the documents say; this file is what the
implementation found, and it wins. Each entry names the harvested entry it answers by that
entry's stable key.

## Superseded
- **Item-view close ordering: `PAGE-2` governs, and the design chapter's snippet does not.** Supersedes `pagination/199b2444`, the Reference entry describing that snippet. The item view copies the page's items, closes the page, and only then yields. Following the snippet would ship a MUST violation the conformance checklist cannot catch, because an early break drives the finally anyway.
  <sub>review · `docs/work/mvp/phase1/phase1a/2026-01-01-phase1a-http.md` · high · sha:manual-fixture-erratum</sub>

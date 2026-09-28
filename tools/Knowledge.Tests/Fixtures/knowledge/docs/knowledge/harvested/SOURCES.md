# Harvested Sources

Every source `harvested/` is derived from, with the sha256 of the whole file at harvest time — per
**file**, never per entry, which is why an entry here cannot be hand-corrected.

Two rows are load-bearing for the tests: `04-core-http-domain-model.md` records the stub's true
digest, and `12-pagination.md` records a wrong one so the inline stale-source warning has
something to fire on. The two `docs/styleguide/csharp/` rows name files the fixture deliberately does not
contain, which is the NOT VERIFIABLE state.

| source | role | sha256 | last harvest |
| --- | --- | --- | --- |
| `docs/product-spec/04-core-http-domain-model.md` | spec | `54bd23b9fb86` | 2026-01-01 |
| `docs/product-spec/12-pagination.md` | spec | `000000000000` | 2026-01-01 |
| `docs/product-spec/appendix-b-conformance-test-checklist.md` | spec | `3a33206d21f8` | 2026-01-01 |
| `docs/sdk-design-dotnet/07-pagination-sse-and-serialization.md` | design | `3c7a20669b75` | 2026-01-01 |
| `docs/styleguide/csharp/06-types-and-data-modeling.md` | styleguide | `aaaaaaaaaaaa` | 2026-01-01 |
| `docs/styleguide/csharp/11-testing.md` | styleguide | `bbbbbbbbbbbb` | 2026-01-01 |

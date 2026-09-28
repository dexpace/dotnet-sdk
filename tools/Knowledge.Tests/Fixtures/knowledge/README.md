# knowledge fixture

A miniature repository root for `tools/Knowledge.Tests`: a nine-row appendix C, three harvested
topic files, one note, two phase documents and a harvest manifest. Hand-written and small on
purpose — a test that pins live corpus counts fails on every harvest. Adapted from the Ruby port's
`scripts/test/fixtures/knowledge/`; the tests copy this tree to a temporary directory per test, so
a test that writes into it never disturbs another.

Two digests in `docs/knowledge/harvested/SOURCES.md` are load-bearing:
`docs/product-spec/04-core-http-domain-model.md` records the true sha of the stub beside it (so a
query over `http-domain-model` is drift-free) and `docs/product-spec/12-pagination.md` records a
deliberately wrong one (so a query over `pagination` raises the inline stale-source warning).
Editing either stub without re-recording its digest will move those two tests. The two
`docs/styleguide/csharp/` sources are deliberately absent (the NOT VERIFIABLE state).

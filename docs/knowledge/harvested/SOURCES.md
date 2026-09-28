# Harvested Sources

**Provenance: seeded, not harvested.** This corpus was seeded mechanically from `ruby-sdk@5b17395`
(`docs/knowledge/harvested/`), keeping only the entries whose every `<sub>` source has role `spec`.
That is sound because `docs/product-spec/` here is a byte-identical copy of the Ruby repository's:
every sha256 below was recomputed against this checkout's file (the first 12 hex digits of the
whole-file digest, the width `knowledge drift` compares at) and matches the digest the Ruby harvest
recorded, so each entry cites the same bytes it was harvested from. An entry's key digests its own
text, which was not changed, so keys are identical to the Ruby corpus's. Design- and
styleguide-role entries, Ruby-only topics and Conflicts entries citing a Ruby design or styleguide
source were dropped (the spec-role subset contained no Conflicts entry). The first real harvest of
this repository — `docs/product-spec/`, `docs/sdk-design-dotnet/` 00–09 and `docs/styleguide/csharp/`
— replaces this file and every topic file beside it.

| source | role | sha256 | last harvest |
| --- | --- | --- | --- |
| `docs/product-spec/01-product-overview.md` | spec | `4f786c44354d` | 2026-09-05 |
| `docs/product-spec/02-architectural-principles.md` | spec | `8014d2ec2c9d` | 2026-09-05 |
| `docs/product-spec/03-pluggable-seams-and-extension-model.md` | spec | `0adae2d6a47f` | 2026-09-05 |
| `docs/product-spec/04-core-http-domain-model.md` | spec | `22d100d5bc94` | 2026-09-05 |
| `docs/product-spec/05-i-o-contracts.md` | spec | `33e67b0b29cd` | 2026-09-05 |
| `docs/product-spec/06-request-and-response-body-lifecycle.md` | spec | `c2bf15dc8a06` | 2026-09-05 |
| `docs/product-spec/07-execution-context-model.md` | spec | `5a9eacfb1c53` | 2026-09-05 |
| `docs/product-spec/08-execution-pipelines.md` | spec | `33e9443472ce` | 2026-09-05 |
| `docs/product-spec/09-retry-and-resilience.md` | spec | `9efbe276001e` | 2026-09-05 |
| `docs/product-spec/10-redirect-handling.md` | spec | `f2a0d207be56` | 2026-09-05 |
| `docs/product-spec/11-authentication.md` | spec | `efba58233dd1` | 2026-09-05 |
| `docs/product-spec/12-pagination.md` | spec | `ba759edd34ec` | 2026-09-05 |
| `docs/product-spec/13-server-sent-events-and-streaming.md` | spec | `dd401a407f5d` | 2026-09-05 |
| `docs/product-spec/14-serialization-serde.md` | spec | `c6bc7789c3a9` | 2026-09-05 |
| `docs/product-spec/15-instrumentation-and-observability.md` | spec | `1b678eca176d` | 2026-09-05 |
| `docs/product-spec/16-configuration.md` | spec | `367e27ec6481` | 2026-09-05 |
| `docs/product-spec/17-transport-adapter-conformance-contract.md` | spec | `2d5843c58993` | 2026-09-05 |
| `docs/product-spec/18-asynchronous-runtime-adapter-contract.md` | spec | `f1bf00174456` | 2026-09-05 |
| `docs/product-spec/19-cross-cutting-invariants-and-policies.md` | spec | `d6123be82c9e` | 2026-09-05 |
| `docs/product-spec/20-non-functional-requirements-and-quality-bar.md` | spec | `5f4684bf7123` | 2026-09-05 |
| `docs/product-spec/appendix-a-glossary.md` | spec | `f0b3d2058626` | 2026-09-05 |
| `docs/product-spec/appendix-b-conformance-test-checklist.md` | spec | `0451cc7f3bb4` | 2026-09-05 |

# `docs/`

One owner per entry, one job per entry, and nothing written by two things. This file is the
index and the contract; the rule is that nothing in `docs/` is unowned.

An entry is listed here before it exists, marked **(planned)**, because an ownership table that
only describes what already exists is a table that gets edited after the fact.

| Entry | Owns | Written by | Housekeeping may write? |
|---|---|---|---|
| [`product-spec/`](./product-spec/) + [`product-spec.md`](./product-spec.md) | **Normative.** The numbered requirements — `HTTP-7`, `SEAM-1`, `RETRY-13`, `NFR-5`, … — that the code exists to satisfy. Language-agnostic; byte-identical to the Ruby and Node siblings' copies (imported from `ruby-sdk@5b17395`, whose appendix C carries two "Amended 2026-09-25, C11" notes) | A human, deliberately | **No — frozen** |
| [`sdk-design-dotnet/`](./sdk-design-dotnet/) + [`sdk-design-dotnet.md`](./sdk-design-dotnet.md) | How each spec area maps to idiomatic .NET, retrofitted from `sdk-design-ruby`. Non-normative but binding by convention. §10 is the **deviation ledger**, §11 the reference-ambiguity appendix, §12 the requirement-coverage index | A human, deliberately | **No — frozen** |
| [`styleguide/`](./styleguide/README.md) | The vendored C# styleguide (`dexpace/styleguide@591e524`) plus the **SDK overlay** recording where this library departs from it | Re-vendored whole from upstream; the overlay by a human | **No — frozen** |
| [`knowledge/harvested/`](./knowledge/harvested/) | Harvested spec / design / styleguide knowledge, topic-indexed. Generated; **never hand-edited**. Today only the `spec` role is seeded | The `knowledge-harvest` skill | **No — frozen** |
| [`knowledge/notes/`](./knowledge/notes/) | What the implementation found, overriding a harvested entry. Hand-written, role `review` | A human | **No — frozen** |
| [`work/`](./work/) | Process records: the [v1 roadmap](./work/mvp/2026-09-27-dotnet-sdk-v1-roadmap-design.md) and per-(sub)phase design, plan and checklist under a unit of delivery | The phase that produced them; **collected** here by the `housekeeping` skill | Yes — `git mv` only |
| [`superpowers/`](./superpowers/README.md) | Nothing, for long. The **inbox** the Superpowers skills write into | `brainstorming`, `writing-plans` | Yes — it drains it |
| [`first-release.md`](./first-release.md) | Release-readiness register: package versions, blockers before first publish, the release path, what v1 ships without, post-release triggers | A human, updated as blockers close | No |
| `deviations.md` **(planned)** | As-built audit of `sdk-design-dotnet/10`'s deviation ledger, plus deviations found outside a phase. Created by roadmap phase 11 | A human, following a phase or review | No — judgment, not a mechanical append |
| [`sdk-documentation/`](./sdk-documentation/http.md) | **As-built** user documentation — how the packages compose, worked examples — one page per phase that ships a layer, mirroring `ruby-sdk/docs/sdk-documentation/`. Today: [`http.md`](./sdk-documentation/http.md) (phase 2a) | A human, or a skill on request | Yes |
| [`architecture.md`](./architecture.md) | The original foundation-slice architecture note, marked superseded by `sdk-design-dotnet/01-overview.md`; kept until roadmap phase 12 writes `sdk-documentation/architecture.md` | A human | Yes |
| [`README.md`](./README.md) | This index | A human | Yes |

## Frozen means frozen

`product-spec/`, `sdk-design-dotnet/`, `styleguide/`, `knowledge/` and the two sibling tables of
contents are **read-only to routine maintenance**. The `housekeeping` skill refuses to write to
them, and that refusal is a tested guard rather than a paragraph of good intent
([`Guard.cs`](../.claude/skills/housekeeping/src/Guard.cs),
[`GuardTests.cs`](../.claude/skills/housekeeping/tests/GuardTests.cs)).

- **`product-spec/`** is what the code is measured against. A tool editing the yardstick is a
  category error.
- **`sdk-design-dotnet/`** carries §10, the deviation ledger. Amending it is a deliberate act, not
  a maintenance pass.
- **`styleguide/`** is a copy of an upstream repository. A local edit forks the rules silently;
  change them upstream and re-vendor.
- **`knowledge/harvested/`** *cannot* absorb a hand edit. Its `<sub>` shas digest the whole source
  file rather than the entry, so an edit inside an entry changes no sha and the next harvest
  regenerates or duplicates it. Record the finding in `knowledge/notes/` instead.

## `work/` and the inbox

`docs/work/<delivery>/phaseN[/phaseNx]/` is the archive. `mvp/` is the first delivery; a later
effort becomes a sibling of it. (The pre-roadmap slice documents were retired rather than filed
here, per roadmap decision D2.) A phase directory is `phaseN`, no hyphen; a sub-phase nests one level deeper as `phaseN/phaseNx`. Every
file keeps its `YYYY-MM-DD-` prefix, and a (sub)phase has three files: `…-design.md`, the plain
plan `….md`, and `…-checklist.md`.

New documents do **not** land there directly: the `brainstorming` and `writing-plans` skills
hard-code `docs/superpowers/{specs,plans}/`, so that directory is an inbox and the `housekeeping`
skill collects from it. See [`superpowers/README.md`](./superpowers/README.md).

## Findings are routed, not registered

A finding belongs with its owner: a numbered task in the plan of the phase whose scope it falls
in, a [`first-release.md`](./first-release.md) entry, or — when it is in material you may write —
simply the fix. An aggregate `## Open Findings` / `## Deferred Items` / `## Open Items` section
inside a spec, design or plan document is drift, and the probe reports it.

## Keeping this file true

```bash
dotnet run --project .claude/skills/housekeeping/src -- probe
scripts/knowledge --coverage
```

The [`housekeeping`](../.claude/skills/housekeeping/SKILL.md) skill probes every claim here against
the repository — project counts, phase-directory counts, harvested-topic counts, a README on every
package, broken relative links, register text in the wrong document, and a requirement ID
attributed to a specification chapter that does not carry it. The
[`knowledge-lookup`](../.claude/skills/knowledge-lookup/SKILL.md) skill is the query surface over
the spec, the design and the styleguide. Both are hand-run tools; their test suites build from
`tools/Dexpace.Tools.sln`.

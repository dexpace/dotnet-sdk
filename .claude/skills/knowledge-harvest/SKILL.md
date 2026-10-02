---
name: knowledge-harvest
description: Use ONLY when the user explicitly asks to harvest, gather, or extract knowledge from files or directories into a reusable corpus - e.g. "harvest the styleguide in X and the spec in Y", "/knowledge-harvest ...", "gather what the design docs say about retries". Reads local file trees with parallel subagents and produces cited, structured knowledge entries, optionally merged into a persistent docs/knowledge/ corpus. Never invoke this on your own initiative.
---

# knowledge-harvest

Read a set of local paths once. Extract discrete, cited knowledge entries with parallel
subagents. Either synthesize them into the conversation or merge them into a persistent,
provenance-tracked corpus that later sessions read instead of the raw sources.

**This skill runs only when the user explicitly asks for it.** Never invoke it because a
harvest seems useful.

## Division of labor

You are the researcher. You decide; the scripts measure and apply; the extractors read.

- **You**: parse the request, choose routing from measured statistics, get plan
  confirmation, spawn extractors, normalize topics and types, detect contradictions,
  resolve conflicts with the user, synthesize.
- **`stats` / `pack`**: walks, hashes, measures heading offsets, applies your routing.
  No judgment.
- **`collect`**: parses the extractors' output, checks every citation against the manifest and the files,
  attaches role and whole-file sha, drops duplicates. It never rewrites a statement and never invents an entry.
- **`merge`**: applies the merge decisions you record. Never decides whether
  two statements contradict.
- **`knowledge-extractor` subagents**: read one chunk, emit entries. No writes, no advice.

**You never read the harvested source files yourself.** That is the whole point — the
extractors carry that cost in parallel, in their own contexts.

## Step 1: Parse the request

The user writes prose. There is no flag syntax. Extract:

| Signal | Meaning |
|---|---|
| Filesystem paths | The roots to harvest. |
| Descriptive phrase attached to a path | That root's **role**. "the styleguide in X" → `styleguide`, "design details in X" → `design`, "product spec in X" → `spec`. |
| "skip X", "ignore X", "except X" | Exclusion glob. |
| "store it", "save this", "keep it", "add to the corpus" | Persist. Absent → ephemeral run. |
| "just the plan", "dry run", "what would it do" | Stop after Step 4. |
| "focus on X", "I care about Y" | Topic hint, passed to extractors. |
| "globally", "outside the repo" | Corpus goes to `~/.claude/knowledge/<project-slug>/` (not for this repository). |

Roles are free-form strings. `spec`, `design`, and `styleguide` get extraction bias in the
extractor prompt; anything else is carried through provenance with no bias. Normalize an
inferred role to one of those three when the phrasing clearly matches, otherwise pass the
user's own word through.

Default persistence is **off**. Default corpus location in THIS repository is `docs/knowledge/harvested/`: `scripts/knowledge` and `verify-structure` read only that directory, and the structure gate rejects a topic file stranded one level up (`docs/knowledge/README.md`).

If no path is given at all, ask which paths. Do not default to the current directory —
harvesting a whole repo by accident is expensive.

## Step 2: Measure

Pick a working directory for intermediates: the session scratchpad if you have one, else
`mktemp -d`. Call it `$WORK`.

```bash
dotnet run --project .claude/skills/knowledge-harvest/src -- stats \
  --path <root1> --as <role1> \
  --path <root2> --as <role2> \
  --exclude '<glob>' \
  -o "$WORK/stats.json"
```

Exit 2 means nothing eligible was found or a path is missing. Report the reason and stop.

Read `stats.json`. It gives you, per root: file count, total bytes, median and max file
size, per-directory totals, the five largest files, and for every markdown file the `##`
section offsets with byte sizes.

## Step 3: Route

Decide, per root, from the measured distribution — not from a constant. Read
`references/routing.md` for the heuristics and the reasoning behind them.

Write `$WORK/routing.json`:

```json
{
  "roots": [
    {"path": "docs/product-spec", "budget": 100000, "tier_threshold": 15000, "split": true}
  ]
}
```

Every root present in `stats.json` must appear, or `pack` exits 2. `path` must match the
root path string in `stats.json` exactly.

Then:

```bash
dotnet run --project .claude/skills/knowledge-harvest/src -- pack \
  --stats "$WORK/stats.json" --routing "$WORK/routing.json" -o "$WORK/manifest.json"
```

**Flag outliers to the user rather than silently harvesting them.** A file many times the
median — a consolidated index, a generated appendix, a changelog — is usually derived from
its siblings, and harvesting both produces near-duplicate entries that merge dedup cannot
catch. Ask whether to exclude it.

## Step 4: Confirm the plan

Print the resolved plan and **wait**:

```
harvest plan
  docs/styleguide/csharp   role: styleguide   16 files  246 KB  3 chunks (sonnet)
  docs/product-spec  (excl. appendix-c-*)    role: spec         23 files  276 KB  4 chunks (sonnet)
  docs/sdk-design-dotnet                     role: design        7 files   63 KB  1 chunk  (sonnet)
  store: yes -> docs/knowledge/harvested/
proceed?
```

Report outcomes — chunk counts and tiers — not the knobs that produced them. A wrong role
inference costs the user one word to correct here, versus a wasted ten-subagent run.

If the request was a dry run, stop here.

## Step 5: Extract

One `knowledge-extractor` subagent per chunk. Dispatch in parallel batches; do not run them
one at a time.

For each chunk in `manifest.json`, call the Agent tool with:

- `subagent_type`: `knowledge-extractor`
- `model`: the chunk's `tier` (`haiku` or `sonnet`) — this overrides the agent's frontmatter
- `run_in_background`: `false`
- `prompt`:

```
role: <chunk.role>
topic hint: <the user's hint, or "none">

Read exactly these files and extract knowledge entries per your output contract:
- <path>            (whole file)
- <path> lines A-B  (this range only)

Emit the "## Entries" and "## Open Questions" sections and nothing else.
```

A chunk file with `"lines": null` is a whole file. A chunk file with `"lines": [A, B]` is
that line range only — say so explicitly, because the extractor will otherwise read the
whole file and duplicate a neighboring chunk's work.

**On failure:** a malformed or empty return gets one retry at the same tier. If it fails
again, mark the chunk `FAILED`, continue the rest, and list every failure with its file
paths at the end so the user can re-run just those. A partial harvest beats a failed one.

### Step 5b: Collect

Save each extractor's output verbatim as `$WORK/outputs/<chunk id>.md` (an extractor's own writer-capable
stand-in may write it directly). Then:

```bash
dotnet run --project .claude/skills/knowledge-harvest/src -- collect \
  --manifest "$WORK/manifest.json" --outputs "$WORK/outputs" \
  [--topics "$WORK/topics.json"] [--extra "$WORK/extra-decisions.json"] \
  --harvested 2026-10-02T00:00:00Z -o "$WORK/entries.json"
```

`collect` prints entry counts per proposed topic and, on stderr, every entry it **dropped** (evidence not in the
manifest, outside the assigned line range, an unknown type, a non-slug topic). A dropped entry is a recall
loss to report or re-extract, never to repair by hand. `--topics` is a JSON object mapping a proposed slug
onto the canonical one (Step 6.1); `--extra` is a JSON array of `supersede` / `conflict` decisions to append.
Run from the repository root: manifest paths are repo-relative and `collect` re-reads the files to check lines.

## Step 6: Normalize and decide

You now hold every chunk's entries. This step is yours alone — you are the only actor that
sees all chunks and the existing corpus.

1. **Normalize topics.** Read `INDEX.md` in the corpus if it exists. Map each proposed slug
   onto an existing topic when it means the same thing (`retries` → `retry-and-resilience`).
   Merge near-identical new slugs with each other. Prefer coarse topics; a corpus of
   forty single-entry topics is useless.
2. **Normalize types.** Apply the discriminators in `references/schema.md` consistently
   across every chunk. Extractors proposed types without seeing each other; identical
   statements from different chunks must not land in different sections.
3. **Drop exact duplicates.** Same normalized statement, same topic, same type. Keep the
   entry with the more specific evidence range.
4. **Detect contradictions** against the existing corpus. Only you can do this —
   `merge` never decides it.

Classify each entry into one action:

| Situation | Action |
|---|---|
| Not in the corpus | `new` |
| Same normalized statement already present | `update` (refreshes evidence, sha, confidence) |
| Contradicts an existing entry, **same** role, source sha differs from `SOURCES.md` | `supersede` |
| Contradicts an existing entry, **same** role, source sha unchanged | `conflict` |
| Contradicts an existing entry, **different** role | `conflict`, always |

The last row matters: a spec disagreeing with a styleguide is a finding worth surfacing,
never a merge to settle silently.

`collect` writes `$WORK/entries.json` with every surviving entry as a `new` decision. Hand-written
decisions (`supersede`, `conflict`, `update`) go in `--extra`. The file's shape:

```json
{
  "harvested": "2026-07-24T10:00:00Z",
  "sources": [
    {"path": "docs/product-spec/09-retry.md", "role": "spec", "sha256": "<from manifest>"}
  ],
  "decisions": [
    {
      "action": "new",
      "topic": "retry-and-resilience",
      "entry": {
        "type": "rule",
        "statement": "Retry budgets are enforced per request rather than per attempt.",
        "evidence": "docs/product-spec/09-retry.md:41-58",
        "confidence": "high",
        "role": "spec",
        "sha256": "<whole-file sha from manifest>"
      }
    },
    {
      "action": "supersede",
      "topic": "retry-and-resilience",
      "replaces": "Retry budgets are enforced per attempt.",
      "entry": { "...same shape as above..." }
    },
    {
      "action": "conflict",
      "topic": "retry-and-resilience",
      "title": "spec vs styleguide",
      "status": "unresolved",
      "text": "the spec permits unbounded backoff jitter; the styleguide forbids non-deterministic timing in tests",
      "sources": ["spec `docs/product-spec/09-retry.md:88`", "styleguide `docs/styleguide/csharp/11-testing.md:204`"]
    }
  ]
}
```

`sha256` always comes from `manifest.json` and is the **whole-file** hash, even for a
split chunk. Staleness is a property of the file, not the slice.

## Step 7: Deliver

### Ephemeral run (no "store it")

Synthesize into the conversation. Nothing is written. Organize by topic, lead with rules
and constraints, keep every citation. Report open questions and any `FAILED` chunks.

Then tell the user the corpus was not written and that saying "store it" would persist it —
`entries.json` is still on disk, so storing later costs nothing.

### Stored run

```bash
dotnet run --project .claude/skills/knowledge-harvest/src -- merge "$WORK/entries.json" \
  --corpus docs/knowledge/harvested
```

Exit codes:

- `0` — applied cleanly.
- `3` — applied, but conflicts with status `unresolved` are on file. (`"status": "conformed"` records a
  contradiction the port settled by conforming to the styleguide; it is shown but does not exit 3.)
  Present each unresolved one to the user with both statements and both sources, apply their decision as an
  `update` or `supersede`, and re-run. Conflicts stay in the file until resolved; this is intentional. In this
  repository a **kept** departure is resolved by a `review`-role note under `docs/knowledge/notes/` that cites
  the Conflicts entry's key (`docs/knowledge/README.md`), never by editing `harvested/`.
- `4` — the corpus has uncommitted git changes. Report it and offer `--force`. Do not pass
  `--force` on your own; unreviewed edits under the corpus would be silently overwritten.
- `2` — malformed `entries.json`. Fix it and re-run.

Use `--dry-run` first when the corpus already has substantial content and the run is large.

Finally, summarize: entries added, updated, superseded, conflicts open, chunks failed.

## Reading the corpus later

When a brainstorming or design session needs grounding and `docs/knowledge/harvested/INDEX.md`
exists, read the relevant topic files instead of the original sources. Every entry carries
`role · evidence · confidence`, so open the cited source only when a specific claim needs
verifying.

## Reference

- `references/schema.md` — entry schema, type discriminators, confidence rubric.
- `references/routing.md` — how to turn statistics into budgets, tiers, and split decisions.

## Testing the scripts

```bash
dotnet test --project .claude/skills/knowledge-harvest/tests --configuration Release
```

The tool is in `tools/Dexpace.Tools.sln` beside the housekeeping and knowledge tools, references no package,
and is deterministic; the tests use no LLM.

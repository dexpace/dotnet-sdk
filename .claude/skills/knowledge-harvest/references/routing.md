# Routing

`scan.py stats` measures. You decide. `scan.py pack` applies. These are heuristics with
their reasoning, not constants to copy blindly — the point of measuring first is that a
fixed budget is wrong for corpora of different shapes.

## What you are deciding

Per root, three values in `routing.json`:

| Field | Effect |
|---|---|
| `budget` | Maximum bytes per chunk. Sets how many extractors run and how much each reads. |
| `tier_threshold` | Chunks below this are routed to `haiku`, at or above to `sonnet`. |
| `split` | Whether a file larger than `budget` may be split at `##` boundaries. |

## Starting points

- `budget`: **100 KB**
- `tier_threshold`: **15 KB**
- `split`: **true**

Then adjust to what the stats actually show.

## Adjusting the budget

The constraint is **extraction completeness, not context window.** A 270 KB markdown file
fits comfortably in a Sonnet context. The failure mode is different: one agent asked to
enumerate several hundred requirements will hit its output limit and start summarizing,
and the result looks fine. You cannot detect that from the output. Smaller chunks buy
recall at near-identical input-token cost, because the same bytes get read either way.

Lower the budget when entry density is high — a normative spec or a rules-heavy styleguide
where nearly every paragraph yields an entry. 50-70 KB is reasonable there.

Raise it when density is low — prose overviews, narrative design documents, rationale
essays where a whole section yields two entries. 150 KB is fine.

Read the `dirs` array: a root with many small directories already produces many chunks
regardless of budget, since chunks never span directories.

## Adjusting the tier threshold

`haiku` is enough for small, self-contained chunks: overviews, READMEs, short config
documents, single-topic files. Use `sonnet` when the chunk is large, dense with normative
wording, or requires holding several sections together to state one entry correctly.

Compare against `median_file_bytes`. If the median sits at 12 KB and the threshold is 15 KB,
almost everything routes to `sonnet` and the tiering does nothing. Either lower the
threshold to split the distribution meaningfully, or accept that this corpus is uniformly
Sonnet work and say so in the plan.

Do not tier down to save money on the chunks that matter. Cost lives in input tokens, which
are the same either way; the model choice buys extraction quality.

## Splitting

Leave `split: true` unless the user asks otherwise. Splitting happens only at heading
boundaries, and a single section larger than the budget is never split — a fragment with no
heading context produces bad entries and unusable provenance.

`stats.json` records `section_level` per file: the shallowest heading level that actually
divides it, `##` when that works and `###` when the document uses `##` only as a title.
A file whose `sections` list has one entry has no usable split point; if it also exceeds
the budget, it goes to one extractor whole and that chunk is a recall risk worth mentioning
in the plan.

Set `split: false` when a file's meaning depends on reading it end to end — a tutorial, a
narrative walkthrough, an argument built across sections.

`stats.json` gives `sections` per markdown file with byte sizes, so you can see before
deciding whether a split would produce balanced chunks or one giant section plus scraps.

## Outliers

`largest_files` per root exists for one purpose: catching a file many times the median.
That is usually a consolidated index, a generated appendix, or a changelog — derived from
its siblings.

Harvesting both the source chapters and a derived index produces near-duplicate entries.
Exact duplicates get deduped at merge; reworded ones do not, and they land in the corpus as
two entries saying the same thing with different citations.

Flag it and ask. Do not decide alone which of the two is authoritative.

# Entry schema

One contract, three consumers: the extractor proposes against it, the main agent
normalizes against it, `merge.py` stores it.

```
type:        rule | constraint | conclusion | reference
topic:       kebab-case slug
statement:   one self-contained sentence
evidence:    <path>:<start>-<end>
confidence:  high | medium | low
role:        attached by the main agent from the chunk, never emitted by the extractor
sha256:      whole-file hash from manifest.json, never the hash of a slice
```

## Type discriminators

| Type | Test | Example |
|---|---|---|
| `rule` | A directive an author must follow. An author *can* violate it. | "Errors are never caught without narrowing to a typed error." |
| `constraint` | A limit that cannot be violated at all — imposed by the system, protocol, or environment. | "A response body can be consumed only once." |
| `conclusion` | A decision that could have gone the other way, ideally with its rationale. | "Pagination is modeled as an async iterator rather than a callback, so consumers can break early." |
| `reference` | A fact, definition, signature, default, or enumeration. No directive force. | "The default request timeout is 30 seconds." |

Ties resolve `constraint` > `rule` > `conclusion` > `reference`.

The common mistake is filing a `constraint` as a `rule`. Ask whether an author could write
code that violates it. If the system makes that impossible, it is a constraint.

The second most common mistake is filing a `conclusion` as a `rule` because it is phrased
imperatively. Ask whether the text gives a reason and an alternative that was rejected. If
so, it is a conclusion.

## Statement quality

- One sentence, present tense, self-contained.
- No pronouns pointing outside itself, no "this", "the above", "as mentioned".
- Readable by someone who has never seen the source.
- Specific over categorical: "Retries use exponential backoff with full jitter, capped at
  30 seconds" beats "the SDK has a retry policy".

Normalization for identity is lowercase, punctuation-stripped, whitespace-collapsed. Two
statements differing only in punctuation are one entry.

## Confidence rubric

| Level | Meaning |
|---|---|
| `high` | Stated explicitly in the text. |
| `medium` | Clearly implied under a single reading. |
| `low` | Inferred across passages, or the text is genuinely ambiguous. |

Confidence measures **textual support, not importance**. That is what makes it comparable
across extractors that never saw each other's files. The main agent does not rewrite it.

## Topic slugs

Lowercase kebab-case, naming the subject rather than the file: `retry-and-resilience`, not
`chapter-09`. Coarse beats fine — `error-handling` over
`error-handling-in-async-contexts`. Extractors guess; the main agent normalizes against
`INDEX.md`, because only it sees the whole corpus.

## Roles

`spec`, `design`, and `styleguide` bias extraction emphasis in the extractor prompt. Any
other string is carried through provenance untouched. Role never affects whether something
is true — only which entries a source is likely to yield.

Role is also the key that makes cross-source disagreement visible: two entries that
contradict each other under *different* roles always become a recorded conflict, never an
automatic supersede.

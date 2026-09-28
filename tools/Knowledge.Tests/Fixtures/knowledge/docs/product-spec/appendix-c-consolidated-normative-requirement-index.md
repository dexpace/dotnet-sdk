## Appendix C — Consolidated Normative Requirement Index

Every normative requirement defined in this specification, aggregated for conformance tracking.

| ID | Level | Subsystem | Requirement |
|---|---|---|---|
| HTTP-1 | MUST | Core HTTP domain model | A request MUST carry an immutable method token. |
| HTTP-2 | MUST | Core HTTP domain model | A response MUST expose its status code as an integer. |
| HTTP-7 | SHOULD | Core HTTP domain model | A request SHOULD carry an opaque tag map for instrumentation. |
| HTTP-70 | MUST | Core HTTP domain model | Header names MUST compare case-insensitively. |
| PAGE-1 | MUST | Pagination | A page MUST be a closeable resource owning exactly one response. |
| PAGE-2 | MUST | Pagination | The item-level view MUST close each page before yielding its items. |
| PAGE-3 | MAY | Pagination | A page MAY expose the request that produced it. |
| PAGE-4 | MUST | Pagination | An empty page with a non-null next-request is a valid non-terminal page. |
| SEAM-1 | MUST | Product vision, pluggable seams and extension model | The core library MUST NOT embed a concrete HTTP transport. |

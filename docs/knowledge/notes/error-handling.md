# error-handling — notes

Hand-written. `../harvested/error-handling.md` is what the documents say; this is what the implementation found, and it wins.
Facts measured on .NET SDK 10.0.401 / runtime 10.0.12, Linux, so a later reader does not have to re-measure them.

## Conclusions
- **Moving `ServiceRequestException` under `IOException` changes no classification: the retry classifier reads `IRetryableError` and the cause chain, never the SDK root.** `TRANSPORT-20` says a transport failure "MUST be a subtype of the platform `IOException`" and `XCUT-4` that it be catchable as a generic I/O error; the port had rooted it in `SdkException` because it chose one SDK root, which no requirement asks for (single inheritance was the only obstacle, design §10 entry 7, retired). The type keeps `IRetryableError` itself, keeps the native exception as `InnerException`, stores the suppressed trail through `ExceptionTrail` and renders it in `ToString` through the renderer `SdkException` shares, so a cycle across the two families still terminates. The cost is on callers: `catch (SdkException)` no longer catches a transport failure, and a negative check written as "is not an `SdkException` that is retryable" goes vacuous and must read `IRetryableError` (the conformance kit had eight such sites, P11-7; `TransportExceptionTaxonomyTests`).
  <sub>review · `docs/work/mvp/phase11/2026-10-19-phase11-deviation-reconciliation-design.md` · high · sha:manual-p11-transport-is-ioexception</sub>

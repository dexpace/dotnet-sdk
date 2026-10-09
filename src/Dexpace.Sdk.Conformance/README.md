# Dexpace.Sdk.Conformance

The transport conformance kit for the dexpace .NET SDK: framework-free assertions for the `TRANSPORT` and `ASYNC`
requirements of the product specification, a loopback HTTP/1.1 wire fixture, and per-requirement reporting with waivers.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`.

Drive the kit from a test: see `docs/sdk-documentation/conformance.md` in the repository.

## Versioning

The kit is versioned in lockstep with `Dexpace.Sdk.Core` and the transport it certifies. **A release that adds an
assertion is at least a minor version**, because a new assertion can turn a previously green third-party run red; a
consumer absorbs it with a waiver by requirement ID.

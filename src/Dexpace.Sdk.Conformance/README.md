# Dexpace.Sdk.Conformance

The conformance kit for the dexpace .NET SDK: four framework-free suites of named assertions over the product specification, a loopback
HTTP/1.1 wire fixture, and per-requirement reporting with waivers. Run them against a `Dexpace.Sdk.Core` transport (`IAsyncHttpClient` /
`IHttpClient`), a pipeline composition, a codec (`ISerde`) or a set of packed `.nupkg` files, from the test framework you already use.

| Suite | Judges | Assertions |
|---|---|---|
| `TransportSuite` | a transport: the `TRANSPORT` and `ASYNC` requirements | 43 |
| `InvariantSuite` | a pipeline composition: `XCUT-1` to `XCUT-24` and the scheduler-agnostic clause of `NFR-11` | 28 |
| `CodecSuite` | a codec: the `SERDE` rows a codec seam can be asked, over the `Dexpace.Sdk.Conformance.Codec` probe models | 11 |
| `PackagingSuite` | the published packages: `NFR-1`, `-2`, `-3`, `-8` to `-10`, `-13` to `-15` | 8 |

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`. Install it in a test
> project, never in production code: `dotnet add package Dexpace.Sdk.Conformance --prerelease`.

## Usage

```csharp
using Dexpace.Sdk.Conformance;

var subject = new TransportSubject { Name = "MyTransport", CreateAsync = settings => new MyTransport(settings.Logger) };
var report = await TransportSuite.RunAllAsync(subject);   // 43 named assertions, each on every face the subject supplies

Assert.True(report.IsGreen, report.ToString());           // the report lists every waiver and every gap
```

One test case per (assertion, face) pair is `TransportSuite.RunAsync(subject, assertion, face)`. A gap you accept is a
`ConformanceWaiver("TRANSPORT-8", "why")` in `TransportSuiteOptions.Waivers`; a waiver must stay needed, so a fixed requirement
that keeps its waiver fails the run. A transport-specific capability (a borrowed native client, a proxy) is an optional hook on the
subject; a missing hook makes its assertions `NotExercised`, never green. The other three suites follow the same shape (`InvariantSuite.RunAllAsync(new InvariantSubject())`,
`CodecSuite.RunAllAsync(new CodecSubject { CreateSerde = ... })`, `PackagingSuite.RunAll(new PackagingSubject { PackagePaths = ... })`), take `SuiteOptions { AssertionTimeout, Waivers }`,
and `ConformanceReport.Combine` merges their reports by requirement. The full guide is
[`docs/sdk-documentation/conformance.md`](https://github.com/dexpace/dotnet-sdk/blob/main/docs/sdk-documentation/conformance.md).

## Versioning

The kit is versioned in lockstep with `Dexpace.Sdk.Core` and the transport it certifies. **A release that adds an assertion is at
least a minor version**, because a new assertion can turn a previously green third-party run red; a consumer absorbs it with a
waiver by requirement ID.

## What a green run does not prove

TLS and certificate handling, HTTP/2 and HTTP/3, and connect-timeout behaviour: the transport fixture is plaintext HTTP/1.1 on the loopback
interface. The invariant suite does not prove a clause whose hook you did not supply (it reports `NotExercised`); the codec suite judges what a codec preserves and refuses, not its byte
spelling; the packaging suite reads the packages it was given and never loads them. Each report says so on every run.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT

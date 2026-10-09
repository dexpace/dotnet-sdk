# Dexpace.Sdk.Conformance

The transport conformance kit for the dexpace .NET SDK: framework-free assertions for the `TRANSPORT` and `ASYNC` requirements
of the product specification, a loopback HTTP/1.1 wire fixture, and per-requirement reporting with waivers. Run it against any
`Dexpace.Sdk.Core` transport (`IAsyncHttpClient` / `IHttpClient`) from the test framework you already use.

> **Pre-release.** Nothing is published yet, and the public API may change before 1.0. Targets `net10.0`. Install it in a test
> project, never in production code: `dotnet add package Dexpace.Sdk.Conformance --prerelease`.

## Usage

```csharp
using Dexpace.Sdk.Conformance;

var subject = new TransportSubject { Name = "MyTransport", CreateAsync = settings => new MyTransport(settings.Logger) };
var report = await TransportSuite.RunAllAsync(subject);   // 42 named assertions, each on every face the subject supplies

Assert.True(report.IsGreen, report.ToString());           // the report lists every waiver and every gap
```

One test case per (assertion, face) pair is `TransportSuite.RunAsync(subject, assertion, face)`. A gap you accept is a
`ConformanceWaiver("TRANSPORT-8", "why")` in `TransportSuiteOptions.Waivers`; a waiver must stay needed, so a fixed requirement
that keeps its waiver fails the run. A transport-specific capability (a borrowed native client, a proxy) is an optional hook on the
subject; a missing hook makes its assertions `NotExercised`, never green. The full guide is
[`docs/sdk-documentation/conformance.md`](https://github.com/dexpace/dotnet-sdk/blob/main/docs/sdk-documentation/conformance.md).

## Versioning

The kit is versioned in lockstep with `Dexpace.Sdk.Core` and the transport it certifies. **A release that adds an assertion is at
least a minor version**, because a new assertion can turn a previously green third-party run red; a consumer absorbs it with a
waiver by requirement ID.

## What a green run does not prove

TLS and certificate handling, HTTP/2 and HTTP/3, and connect-timeout behaviour: the fixture is plaintext HTTP/1.1 on the loopback
interface. The report says so on every run.

## Links

- Repository and design: <https://github.com/dexpace/dotnet-sdk>
- License: MIT

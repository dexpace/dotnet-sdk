# The transport conformance kit

**As built by phase 8a, written against source on 2026-10-09** (branch `88-phase-8a-conformance-kit`). This page describes how an
adapter author drives `Dexpace.Sdk.Conformance` against a transport: the suite and its runner, the subject and its capability
hooks, the six result statuses and the waivers that accept a gap, the loopback wire fixture the assertions run over, and the
catalogue of named assertions. The requirement IDs it cites are the normative text
(`docs/product-spec/17-transport-adapter-conformance-contract.md`, `TRANSPORT-1` to `TRANSPORT-30`, and
`docs/product-spec/18-asynchronous-runtime-adapter-contract.md`, `ASYNC-1` to `ASYNC-22`); the decisions behind the kit are in the
[phase 8a design](../work/mvp/phase8/phase8a/2026-10-09-phase8a-conformance-kit-design.md) and in
[design §9.3](../sdk-design-dotnet/09-toolchain-and-quality-gates.md). Neither is restated here.

## What the kit is

`Dexpace.Sdk.Conformance` is one package that references `Dexpace.Sdk.Core` and nothing else (no test framework, no transport, no
codec). It holds the assertions for the transport contract as plain methods that throw `ConformanceException` (deliberately **not**
an `SdkException`, so a `catch (SdkException)` around a send can never swallow a verdict), a runner that drives a transport through
them and returns `ConformanceResult`s and a `ConformanceReport`, and `Dexpace.Sdk.Conformance.Wire`, the loopback HTTP/1.1 server
the assertions observe the wire through. Your test framework decides what a failing result means.

A green run proves **less** than the whole contract, and the report says so on every run (`ConformanceReport.Preamble`):

> Transport conformance report. A green run does not prove: TLS or certificate handling (the fixture is plaintext HTTP/1.1 on the
> loopback interface); HTTP/2 or HTTP/3; connect-timeout behaviour (a local listener cannot portably accept slowly); or any
> requirement no assertion cites. It proves that every assertion that ran met its clause or is waived below, and nothing more.

## Driving it

One test case per (assertion, face) pair, plus one case that runs everything and writes the report. This is the driver the
repository's second conformance subject uses, copied here verbatim (`ConformancePageTests` keeps the two identical); the xUnit
v3 `Assert.Skip` turns a waived, vacuous or not-exercised result into a visible skip instead of a silent pass. NUnit and MSTest
are the same shape: one parameterised test over `TransportSuite.Assertions`, and a failure on a `Failed` or `Errored` status.

<!-- driver -->
```csharp
public static TheoryData<string, TransportFace> Rows()
{
    var rows = new TheoryData<string, TransportFace>();
    foreach (var assertion in TransportSuite.Assertions)
    {
        foreach (var face in assertion.Faces)
        {
            rows.Add(assertion.Name, face);
        }
    }

    return rows;
}

[Theory]
[MemberData(nameof(Rows))]
public async Task Assertion_holds(string name, TransportFace face)
{
    var assertion = TransportSuite.Assertions.Single(a => a.Name == name);
    var result = await TransportSuite.RunAsync(RawSocketSubject.Create(), assertion, face, RawSocketSubject.Options, TestContext.Current.CancellationToken);

    if (result.Status == ConformanceStatus.Passed)
    {
        return;
    }

    if (result.Status is ConformanceStatus.Failed or ConformanceStatus.Errored)
    {
        Assert.Fail(result.Detail);
    }
    else
    {
        Assert.Skip($"{result.Status}: {result.Detail}");
    }
}

[Fact]
public async Task The_whole_suite_is_green_and_its_report_is_written()
{
    var report = await TransportSuite.RunAllAsync(RawSocketSubject.Create(), RawSocketSubject.Options, TestContext.Current.CancellationToken);

    TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
    Assert.True(report.IsGreen, report.ToString());
}
```

Replace `RawSocketSubject` with your own subject (next section). `TransportSuite.RunAllAsync(subject, options, token)` returns the
`ConformanceReport` directly when you would rather assert on the whole run: `report.IsGreen` is "no `Failed`, no `Errored`",
`report.ByRequirement` is the worst status among the assertions citing each ID, `report.ByAppendixBItem` is the same view over
the checklist items of appendix B.6 and B.7, and `report.ToString()` renders the preamble, both tables, every waiver, every
assertion that was not exercised and every failure.

Every run builds a fresh transport and a fresh `LoopbackServer` per assertion and disposes both, so no assertion sees another's
state and a poisoned connection cannot fail a later row. Every assertion is bounded by `TransportSuiteOptions.AssertionTimeout`
(30 seconds by default); a hang is a `Failed` result naming the bound, never a hung run.

## Describing a transport

A `TransportSubject` is a descriptor of factories, not an instance:

```csharp
var subject = new TransportSubject
{
    Name = "MyCompany.Http.Transport",
    CreateAsync = settings => new MyTransport(settings.Logger),            // IAsyncHttpClient, owned by the suite
    CreateBlocking = settings => new MyTransport(settings.Logger),         // IHttpClient; either face may be absent
    CreateBorrowed = settings => /* a BorrowedTransport over a native client you own */,
    AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
};
```

- **Faces.** `CreateAsync` supplies the asynchronous face and `CreateBlocking` the blocking one. An assertion runs once per face it
  declares and the subject supplies; a face the subject lacks is `NotExercised` for that pair. The blocking face is driven on a
  dedicated thread, so many blocked calls cannot starve one another.
- **The logger.** `TransportSettings.Logger` is the kit's recorder. Pass it to the transport's `ILogger` parameter and the
  assertions can see which header drops were logged by name and prove that no header value ever was (`TRANSPORT-11`,
  `TRANSPORT-13`, `XCUT-18`).
- **Capability hooks.** Each optional hook reaches a clause only a transport-specific construction can reach. A missing hook makes
  the assertion that needs it `NotExercised`, naming the hook: never `Passed`, and never `Vacuous`.

| Hook | For | What it builds |
|---|---|---|
| `CreateBorrowed` | `TRANSPORT-15`, `XCUT-22` | a `BorrowedTransport`: the transport, a way to send straight through the native client, and the native client's owner |
| `CreateWithFaultingAdaptation` | `TRANSPORT-22` | a transport whose adaptation of every live native response fails after the response exists |
| `CreateWithInternalCancel` | `TRANSPORT-8` | an `InternalCancellation`: the transport and a trigger that cancels its in-flight calls from the native side |
| `CreateWithNativeResend` | `TRANSPORT-18` | a transport whose native layer re-sends the request, so a single-use body is serialised twice |
| `CreateWithProxy` | `TRANSPORT-30` | a transport that installs the given `ProxyOptions` |

`AfterDispose` is a declaration and not a hook: `SEAM-15` is a MAY, so a subject that declares `Unspecified` (the default) gets
`Vacuous` for `seam-15.after-dispose`, and one that declares `ThrowsObjectDisposedException` is held to it on the owned transport
and, when it supplies `CreateBorrowed`, on the borrowed one too.

**What the suite never assumes** (the suite contract): the transport's identity (no assertion branches on a type name; a clause
that differs per transport is a hook or a waiver); a response header's exact spelling (names are compared ASCII-case-folded); a
`Content-Length` among a response's headers (the length is asked of `ResponseBody.ContentLength`); anything beyond plaintext
HTTP/1.1.

## Statuses and waivers

| Status | Meaning |
|---|---|
| `Passed` | The assertion ran and the transport met the clause |
| `Failed` | A `ConformanceException`: the transport broke the clause. A transport failure where the clause requires success is a `Failed`, not an `Errored` |
| `Errored` | Anything else was thrown: a bug in the kit or in the assertion's setup. Never silently a failure, and never green |
| `Vacuous` | The assertion established **from inside** that the clause's antecedent is absent (it threw `ConformanceVacuousException` with the reason). Measured, never claimed |
| `Waived` | The assertion failed or errored and a waiver names one of its requirement IDs |
| `NotExercised` | The subject lacks the face or the hook the assertion needs |

A `ConformanceWaiver(RequirementId, Reason)` accepts a gap **by requirement ID**. `RequirementId` must exist in the requirement
catalogue (generated from appendix C, so a typo throws at construction), `Reason` says why, and three optional properties narrow
it: `Owner` (a phase such as `"8b"` for a pending fix, `null` for a permanent decision), `Assertion` (one assertion name) and
`Face`. A waiver must **stay needed**: a waived assertion that passes is reported `Failed` with "waiver for `TRANSPORT-n` is no
longer needed", so a fixed row cannot keep its waiver; a waiver naming an assertion that does not run is itself a failed result;
a waiver never converts `Vacuous` or `NotExercised`. The report lists every waiver on every run, failing or not.

```csharp
var options = new TransportSuiteOptions
{
    AssertionTimeout = TimeSpan.FromSeconds(20),
    ReleaseTimeout = TimeSpan.FromSeconds(5),          // how long to wait for the server to see a connection released
    Waivers =
    [
        new("TRANSPORT-14", "the native client rejects a malformed header name before the adapter sees it")
        {
            Owner = null,                                // permanent
            Assertion = "transport-14.name-malformed-dropped",
        },
    ],
};
```

## The fixture

`Dexpace.Sdk.Conformance.Wire` is a minimal HTTP/1.1 server over a `TcpListener` on the loopback interface. It records every
request byte for byte (`RecordedRequest`: the raw text, the request line, the header lines as sent, the de-chunked body), answers
from a script of `LoopbackResponse`s, and never normalises what a conforming server would. It is the one wire seam: the
repository's own wire tests use it too.

| Script | What the server does |
|---|---|
| `Ok`, `Status` | A fixed-length reply; `keepAlive: true` leaves the connection open (the default closes it) |
| `Streamed`, `HeadersThenGate` | A chunked body written as the test releases it, or a head at once and the body after a gate |
| `Raw` | Exactly the bytes you give it: a malformed header, a close-delimited body, a truncated reply |
| `Redirect` | A body-less 3xx |
| `Gated(gate, then)` | Nothing until the gate opens (a hang before the status line), then `then`; a client that gives up releases the connection |
| `Abort()` | Reads the request, then resets the connection with no response byte |
| `Hang()` | Reads the request and never answers |
| `Large(bytes, seed)` | A deterministic multi-megabyte body (its SHA-256 is computable) |
| `EchoId` | The request's `X-Conformance-Id` as the body, so concurrent callers can check they got their own reply |

Assertions never sleep: `WaitForRequestAsync(index, token)` and `WaitForConnectionReleasedAsync(connection, token)` are conditions to
wait on. **Released means the client closed the connection or sent a further request on it**, observed from the server side,
because a pooling client returns a released connection to its pool instead of closing it; a connection still held by a leaked
response does neither. A release check therefore sends probe requests through the transport (a pool reuses its newest idle
connection first, so the check sends a burst), and it refuses to count a close the **server** initiated (a `Connection: close`
reply): after one the wait proves nothing, so an assertion that checks a release must script a keep-alive reply. `Faults` collects
anything unexpected the server saw; a peer that closes or resets a connection mid-exchange is expected and is not a fault.

## The assertion catalogue

Forty-two assertions, with stable names (renaming one breaks a consumer's waiver list, so the names are frozen at the first
`PublicAPI.Shipped.txt`). `Level` is the strength of the clause the assertion checks, which is not always the level of its ID:
`TRANSPORT-11` is a MUST for its framing clause and a SHOULD for its logging clause, and each has its own assertion.

| Assertion | Requirements | Level | Faces | Needs |
|---|---|---|---|---|
| `transport-1.redirect-not-followed` | TRANSPORT-1 | MUST | both |  |
| `transport-2.no-silent-resend` | TRANSPORT-2 | MUST | both |  |
| `transport-3.cancel-is-terminal` | TRANSPORT-3, SEAM-13, XCUT-1 | MUST | both |  |
| `transport-4.timeout-is-retryable` | TRANSPORT-4, XCUT-2 | MUST | both |  |
| `transport-5.per-call-timeout` | TRANSPORT-5 | MUST | async |  |
| `transport-6.sub-resolution-timeout` | TRANSPORT-6 | SHOULD | both |  |
| `transport-7.cancel-releases-exchange` | TRANSPORT-7 | MUST | async |  |
| `transport-7.attempt-timeout-aborts` | TRANSPORT-7 | MUST | async |  |
| `transport-8.internal-cancel-is-terminal` | TRANSPORT-8 | MUST | async | `CreateWithInternalCancel` |
| `transport-9.settle-race-releases` | TRANSPORT-9, SEAM-30 | MUST | async |  |
| `transport-10.content-type-authoritative` | TRANSPORT-10 | MUST | both |  |
| `transport-11.framing-recomputed` | TRANSPORT-11 | MUST | both |  |
| `transport-11.drop-logged` | TRANSPORT-11 | SHOULD | async |  |
| `transport-12.native-rejected-header-dropped` | TRANSPORT-12 | MUST | both |  |
| `transport-13.drop-log-once-per-name` | TRANSPORT-13, OBS-19 | SHOULD | async |  |
| `transport-14.value-control-dropped` | TRANSPORT-14 | MUST | both |  |
| `transport-14.obs-text-preserved` | TRANSPORT-14 | SHOULD | both |  |
| `transport-14.name-malformed-dropped` | TRANSPORT-14 | MUST | both |  |
| `transport-15.borrowed-survives` | TRANSPORT-15, XCUT-22 | MUST | async | `CreateBorrowed` |
| `transport-15.owned-released` | TRANSPORT-15 | MUST | async |  |
| `transport-16.close-idempotent-nonblocking` | TRANSPORT-16, ASYNC-15 | MUST | both |  |
| `transport-17.single-use-written-once` | TRANSPORT-17 | MUST | both |  |
| `transport-18.native-resend-identical` | TRANSPORT-18 | MUST | async | `CreateWithNativeResend` |
| `transport-19.abandoned-body-unblocks` | TRANSPORT-19 | SHOULD | async |  |
| `transport-20.no-response-is-retryable` | TRANSPORT-20, XCUT-4 | MUST | both |  |
| `transport-20.retried-by-the-pipeline` | TRANSPORT-20 | MUST | async |  |
| `transport-21.pre-dispatch-failure-via-task` | TRANSPORT-21, ASYNC-2 | MUST | async |  |
| `transport-22.adaptation-failure-releases` | TRANSPORT-22 | MUST | async | `CreateWithFaultingAdaptation` |
| `transport-23.never-null` | TRANSPORT-23, ASYNC-1, SEAM-16 | MUST | async |  |
| `transport-24.vendor-status-readable` | TRANSPORT-24 | MUST | both |  |
| `transport-25.lazy-body` | TRANSPORT-25, SEAM-11 | MUST | both |  |
| `transport-25.large-round-trip` | TRANSPORT-25 | MUST | both |  |
| `transport-25.dispose-releases` | TRANSPORT-25 | MUST | both |  |
| `transport-26.bodyless-methods` | TRANSPORT-26 | MUST | both |  |
| `transport-27.inbound-downgrade` | TRANSPORT-27 | SHOULD | both |  |
| `transport-28.file-range-replayable` | TRANSPORT-28 | SHOULD | both |  |
| `transport-29.concurrent-no-crosstalk` | TRANSPORT-29, SEAM-12, ASYNC-22 | MUST | both |  |
| `transport-30.proxy-discoverable-no-leak` | TRANSPORT-30 | SHOULD | async | `CreateWithProxy` |
| `async-1.single-non-null-response` | ASYNC-1 | MUST | async |  |
| `async-20.late-cancel-leaves-response-open` | ASYNC-20, SEAM-16 | MUST | async |  |
| `seam-15.after-dispose` | SEAM-15 | MAY | both | `AfterDispose` (declared) |
| `http-39.short-source-fails` | HTTP-39 | MUST | both |  |

The rows `TRANSPORT-1`, `-4`, `-5`, `-6`, `-8`, `-9`, `-10`, `-12`, `-13`, `-14`, `-17`, `-18`, `-28` and `-30` are phase 8b's work on the
reference transport; the kit asserts them all so that 8b is proven by it. `TRANSPORT-14` is split into its three clauses so a
transport that cannot meet one (a native client that rejects a whole response over a malformed header *name*) waives that one alone.

## Versioning

The kit is versioned in lockstep with `Dexpace.Sdk.Core` and the transport it certifies, and never published ahead of or apart
from them. **A release that adds an assertion is at least a minor version**, because a new assertion can turn a previously green
third-party run red; a consumer absorbs it with a waiver by ID. The first published version is the first release's lockstep
version: `0.1.0` is proposed, and `docs/first-release.md` makes the number phase 12's decision.

## What the kit does not cover

- **TLS, HTTP/2 and connect timeouts**, stated in the preamble: the fixture is plaintext HTTP/1.1 and a local listener cannot
  portably accept slowly.
- **Most of chapter 18.** Of the 22 `ASYNC` rows the kit proves the four per-transport ones (`ASYNC-1`, `-2`, `-20`, `-22`, with
  `ASYNC-15` through `transport-16`); the rest are properties of the two bridges, pinned by `Dexpace.Sdk.Core.Tests/Client`. No
  reactive adapter ships, so `ASYNC-21` is not applicable (design §11 item 21) and `ASYNC-4` is vacuous (thread interruption is
  banned, §10 entry 8).
- **Codec and pagination vectors.** The `SERDE` lift of phase 7a and the pagination vectors of phase 7c are phase 10's invariant and
  codec suites, in this same package.

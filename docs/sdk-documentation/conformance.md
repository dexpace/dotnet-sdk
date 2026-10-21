# The transport conformance kit

**As built by phase 8a, written against source on 2026-10-09** (branch `88-phase-8a-conformance-kit`); **extended by phase 10 on 2026-10-14**
with three more suites in the same package, described from [The invariant suite](#the-invariant-suite-phase-10) on. This page describes how an
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
| `Raw` | Exactly the bytes you give it: a malformed header, a close-delimited body, a truncated reply; empty and closing, an orderly close before any response byte |
| `Redirect` | A body-less 3xx |
| `Gated(gate, then)` | Nothing until the gate opens (a hang before the status line), then `then`; a client that gives up releases the connection |
| `Abort()` | Reads the request, then tears the connection down with no response byte: a FIN, then a reset. Linux reports the end of stream to a client's read, macOS usually the reset, so a check that turns on which one the client saw uses an empty closing `Raw` instead |
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

Forty-three assertions, with stable names (renaming one breaks a consumer's waiver list, so the names are frozen at the first
`PublicAPI.Shipped.txt`). `Level` is the strength of the clause the assertion checks, which is not always the level of its ID:
`TRANSPORT-11` is a MUST for its framing clause and a SHOULD for its logging clause, and each has its own assertion.

| Assertion | Requirements | Level | Faces | Needs |
|---|---|---|---|---|
| `transport-1.redirect-not-followed` | TRANSPORT-1 | MUST | both |  |
| `transport-2.no-silent-resend` | TRANSPORT-2 | MUST | both |  |
| `transport-2.bodyless-not-resent` | TRANSPORT-2 | MUST | both |  |
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
*Dated correction, 2026-10-11 (phase 8b):* the rows are built ([`transport.md`](./transport.md)), the reference transport's driver runs the whole kit
through both of its constructions (the owned one and the handler-taking one, each with a `CreateWithProxy` hook, so `transport-30` is no longer
`NotExercised`), and no waiver with `Owner = "8b"` remains. Two permanent, assertion-scoped waivers are left (`Owner = null`):
`transport-14.name-malformed-dropped` (design §10 entry 32) and `transport-2.bodyless-not-resent` (entry 33). *Corrected 2026-10-20 (phase 11):* the second is deleted for the owned and
the DI-built subjects and kept, rescoped to a `SocketsHttpHandler` the caller built, for the handler-taking subject (design §11 item 71); the first stays on all three. The reference
transport's driver therefore carries one waiver for its owned construction and two for its handler construction, the DI driver one (`TRANSPORT-14`'s name half), and the test-only raw-socket client its own `TRANSPORT-11` one.

`TRANSPORT-2` has two assertions because the clause has two halves. `transport-2.no-silent-resend` is the specification's own
conformance sentence: a single-use body whose exchange was reset reaches the server at most once. A native client never re-writes
content it already wrote, with its retry on or off, so passing it does not show the retry is off. `transport-2.bodyless-not-resent`
does: a body-less `POST`, `DELETE` and `GET` on a warmed keep-alive connection, whose connection the server closes after reading
them and before any response byte, must each arrive exactly once. The close is orderly, not `Abort()`'s FIN and reset: whether a native
client re-sends turns on what its read reports, and only an orderly close is reported the same way, as an end of stream, on every
platform. `SocketsHttpHandler` has no switch for its own retry and re-sends such a request up to three more times, so the reference
transport was waived for it (owner `8b`) on every platform while it passed the first half. *Dated correction, 2026-10-20 (phase 11):* the owned construction now passes the whole
assertion, because the handler the SDK builds stops the native re-send (see `transport.md`), and the waiver is gone for it. *Dated correction, 2026-10-11 (phase 8b):* the
transport now sends a body-less `POST`, `PUT` and `PATCH` once (a zero-length content, the wire unchanged), so the assertion's `POST` step passes, but its `DELETE` and
`GET` steps still arrive four times: the waiver is permanent (`Owner = null`), narrowed to this assertion, and turns stale if the runtime ever stops the retry.

Two assertions measure whether their clause applies instead of asking the subject. `transport-13.drop-log-once-per-name` governs a header
the transport dropped, so a transport that put both headers on the wire dropped none and the result is `Vacuous` (no waiver needed). And
`transport-20.retried-by-the-pipeline` counts the attempts that reach the transport, because a native client that re-sends a reset request
can deliver the 200 inside one attempt: the server answers by the pipeline's attempt number, so only a second pipeline attempt can succeed.

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
- **Codec and pagination vectors.** Phase 10 lifted the `SERDE` rows a codec seam can be asked into [the codec suite](#the-codec-suite-phase-10).
  The pagination vectors of phase 7c are **not** lifted (P10-9): the paging engine has one implementation and is not a seam, so appendix B's
  `PAGE` rows are `by reference` to its own tests.

## The third driver (phase 9, P9-22)

*Added 2026-10-13.* `tests/Dexpace.Sdk.Extensions.DependencyInjection.Tests/Conformance/` runs the kit over the transport `AddDexpaceClient` builds from `IHttpClientFactory`: a fresh container per transport, the kit's logger forwarded through a
single-logger factory, the hooks added as handlers of the named client (the faulting, re-sending and cancel-on-demand handlers are the SystemNet suite's, shared by linked source) and the proxy hook through the `Transport:Proxy:*` keys. It is green on both
faces with the one permanent assertion-scoped waiver of the owned SystemNet driver (design §10 entry 32; *corrected 2026-10-20, phase 11:* entry 33's waiver is gone, because the named client's primary is `CreatePrimaryHandler`'s); `transport-15.borrowed-survives` is `NotExercised` because the package never borrows a client. `transport-1.redirect-not-followed` passing on both
faces is the proof the 8a hand-off named, and `transport-15.owned-released` is what the container-owned primary-handler lease earns: it failed on the first build of the driver, because the factory never releases a handler whose lifetime is infinite.

## The invariant suite (phase 10)

*Added 2026-10-14 (phase 10, P10-3 to P10-7).* `InvariantSuite` runs 28 assertions over the cross-cutting rows `XCUT-1` to `XCUT-24` and the
scheduler-agnostic clause of `NFR-11`, against an `InvariantSubject`: a set of **hooks**, each defaulting to core's own public component, so the same
assertions run over a composition you built (your own policies, credentials, redactor or codec) and so every assertion can have a negative control (a broken
member in place of the default). The transport-level clause of `XCUT-1`, `-2`, `-4` and `-22` stays an assertion of `TransportSuite` (`transport-3`, `-4`, `-20`, `-15`),
and `ConformanceReport.Combine` merges the two reports by requirement, so no clause has two owners (P10-6).

> Invariant conformance report. A green run does not prove: any clause whose hook the driver did not supply (such an assertion is NotExercised, never passed, and a driver pins the list it expects); anything about core's internals except through the hooks; behaviour over a real socket (the transport suite's); or any requirement no assertion cites. It proves that every assertion that ran met its clause over the composition it was given, or is waived below, and nothing more. Run against the defaults it judges core, and nothing about a third party's composition until they supply theirs.

**The subject.** `InvariantSubject` is a sealed record; every member is optional.

- `CreatePipeline(transport, options, time, logger)` builds the `HttpPipeline` under test over the in-memory transport an assertion hands it, capturing the call-level
  options, on the run's recording clock and logger. The default is the idempotency, client-identity and set-date policies plus `AddStandardResilience`.
- `CreateResponseException`, `EnumerateCauses`, `CreateTokenCache`, `RedactUrl`, `RedactHeaderValue`, `Models` (a `ModelFactories` of delegates), `Auth` (an `AuthPolicyFactories`),
  `Credentials`, `CreateDisposables`, `AsAsync` and `AsBlocking` each default to the public core component they stand for.
- **Four hooks have no default**, because the thing they reach is internal to core or lives in another package: `MapStrict` (`XCUT-8`'s strict factory), `CreateCyclicException`
  (`XCUT-9`; the suite only **walks** the exception and never throws it, because a thrown cyclic chain hangs the test host on Windows), `CreateBoundedMap` (`XCUT-14`, a
  `BoundedMapProbe` of insert, count and capacity) and `SeamResolution` (`XCUT-23`). An assertion whose hook is `null` is `NotExercised` with a reason naming the hook, never passed.
- `DrawCnonce` is optional and is not one of the four: `null` means the suite draws through the public `DigestChallengeHandler.Authorize`, so `xcut-21` is always exercised. It judges the
  draw's width (at least 128 bits, written as hex of either case, base64 or base64url; core draws 32 lower-case hex characters) and that ten thousand draws do not repeat; the operating-system CSPRNG source clause is cited by reference to
  `DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator`, because no output test can tell a CSPRNG from `System.Random`.

**The catalogue.** `Faces` is `both` for the asynchronous and blocking surface of `HttpPipeline`, or `none` (`TransportFace.None`) for an assertion that judges a value and drives no call.
`Level` is the strength of the clause, never above the strongest of its IDs.

| Assertion | Requirements | Level | Faces | Reads |
|---|---|---|---|---|
| `xcut-1.cancel-is-terminal` | XCUT-1 | MUST | both | `CreatePipeline` |
| `xcut-2.timeout-is-retryable` | XCUT-2 | MUST | both | `CreatePipeline` |
| `xcut-3.wait-promptly-cancellable` | XCUT-3 | MUST | both | `CreatePipeline`, the recording clock |
| `xcut-4.two-branches` | XCUT-4 | MUST | both | `CreatePipeline` |
| `xcut-5.one-classifier` | XCUT-5 | MUST | none | `CreateResponseException` |
| `xcut-6.capability-queried` | XCUT-6 | MUST | both | `CreatePipeline` |
| `xcut-7.configured-set-authoritative` | XCUT-7 | MUST | both | `CreatePipeline` |
| `xcut-8.strict-factory-rejects` | XCUT-8 | MUST | none | `MapStrict` (**no default**) |
| `xcut-8.convenience-returns-response` | XCUT-8 | MUST | both | `CreatePipeline` (error mapping on) |
| `xcut-9.cycle-safe-walk` | XCUT-9 | MUST | none | `CreateCyclicException` (**no default**), `EnumerateCauses` |
| `xcut-10.safety-gate-uniform` | XCUT-10 | MUST | both | `CreatePipeline` |
| `xcut-11.shared-no-crosstalk` | XCUT-11 | MUST | both | `CreatePipeline` |
| `xcut-12.single-flight-scoped` | XCUT-12 | SHOULD | both | `CreateTokenCache` |
| `xcut-13.dispose-latched` | XCUT-13 | MUST | both | `CreateDisposables` |
| `xcut-13.dispose-non-blocking` | XCUT-13 | MUST | both | `CreateDisposables` |
| `xcut-14.bounded-under-burst` | XCUT-14 | MUST | none | `CreateBoundedMap` (**no default**) |
| `xcut-14.drains-after-insert` | XCUT-14 | MUST | none | `CreateBoundedMap` (**no default**) |
| `xcut-15.no-external-alias` | XCUT-15 | MUST | none | `Models` |
| `xcut-16.no-credential-over-http` | XCUT-16 | MUST | both | `Auth`, `CreatePipeline` |
| `xcut-17.redirect-hygiene` | XCUT-17 | MUST | both | `CreatePipeline` |
| `xcut-18.model-validation` | XCUT-18 | MUST | none | `Models` |
| `xcut-19.redaction-default-deny` | XCUT-19 | MUST | both | `RedactUrl`, `RedactHeaderValue`, `Credentials`, `CreatePipeline` |
| `xcut-20.observability-never-throws` | XCUT-20 | MUST | both | `RedactUrl`, `CreatePipeline` |
| `xcut-21.cnonce-unpredictable` | XCUT-21 | MUST | none | `DrawCnonce` (optional) |
| `xcut-22.core-borrows` | XCUT-22 | MUST | both | `CreatePipeline`, `AsAsync`, `AsBlocking` |
| `xcut-23.resolution-deterministic` | XCUT-23 | MUST | none | `SeamResolution` (**no default**) |
| `xcut-24.preview-capped-non-consuming` | XCUT-24 | SHOULD | both | `CreatePipeline` |
| `nfr-11.scheduler-agnostic` | NFR-11 | SHOULD | both | `CreatePipeline` |

**The first-party drivers pin what they leave `NotExercised`** (P10-28), so a hook not supplied never reads as conformance. The core driver
(`Dexpace.Sdk.Core.Tests/Conformance/InvariantConformanceTests.cs`) supplies the three hooks that reach internals (`MapStrict`, `CreateCyclicException`, `CreateBoundedMap`) and leaves exactly
`xcut-23.resolution-deterministic`; the dependency-injection driver supplies `SeamResolution` over `AddDexpaceClient` and the DI-built pipeline and leaves the four assertions that need core's
internals; the NativeAOT smoke runs the defaults and leaves five. No assertion is waived. The core driver, kept identical to this page by `ConformancePageTests`:

<!-- invariant driver -->
```csharp
public static TheoryData<string, TransportFace> Rows()
{
    var rows = new TheoryData<string, TransportFace>();
    foreach (var assertion in InvariantSuite.Assertions)
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
    var assertion = InvariantSuite.Assertions.Single(a => a.Name == name);
    var result = await InvariantSuite.RunAsync(InvariantCoreSubject.Create(), assertion, face, InvariantCoreSubject.Options, TestContext.Current.CancellationToken);

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
```

**Running it over your own composition.** Build an `InvariantSubject` with the members you replace and run `InvariantSuite.RunAllAsync(subject)`; every member you leave alone judges core. A
custom pipeline policy, a credential-stamping step, a redactor or a token cache goes in through `CreatePipeline`, `Auth`, `RedactUrl` and `CreateTokenCache`; a model that must not alias its input goes
in through `Models`. A waiver by requirement ID accepts a gap the way it does for a transport, and a waived assertion that now passes fails the run.

## The codec suite (phase 10)

*Added 2026-10-14 (phase 10, P10-8).* `CodecSuite` runs 11 assertions over the `SERDE` rows a codec seam can be asked, on both faces of `ISerde` (the asynchronous stream pair and the synchronous buffer
and span pair), against a `CodecSubject { Name, CreateSerde, ExpectedMediaType, MalformedPayload }`. `CreateSerde` builds the codec fresh for each run **exactly as a consumer builds it**, with no extra
configuration: `SERDE-19` asks whether the defaults wire the PATCH semantics. The suite serializes only the **probe models** of `Dexpace.Sdk.Conformance.Codec` (`ProbeContact`, `ProbeAddress`,
`ProbePatch`, `ProbeSequence`, `ProbeUnencodable`), so a codec that needs compile-time metadata registers them in its own context, one line each, in the default generation mode (not the fast-path-only
mode, which cannot carry the tri-state wiring). The tri-state assertions judge what a codec preserves and refuses through its own round trip, not how it spells it on the wire: `Absent` must not
encode like `Null` and must encode shorter (it omits the key), and each state must come back.

> Codec conformance report. A green run does not prove: the codec's own metadata for models it was not given (the probe models are the only types the suite serializes); configuration it was not handed (the codec is built exactly as the driver's factory builds it); the wire format byte for byte (the suite judges what a codec preserves and refuses, not how it spells it); core's typed readers, SerdeExtensions and Tristate<T>'s own API (SERDE-4, SERDE-13, SERDE-14, SERDE-18, SERDE-30, which judge core code and are cited by reference); or any requirement no assertion cites. It proves that every assertion that ran met its clause over the codec it was given, or is waived below, and nothing more.

| Assertion | Requirements | Level | Faces | Reads |
|---|---|---|---|---|
| `serde-1.round-trip` | SERDE-1 | MUST | both | `CreateSerde` |
| `serde-2.declared-media-type` | SERDE-2 | MUST | none | `CreateSerde`, `ExpectedMediaType` |
| `serde-3.target-not-closed` | SERDE-3, SEAM-20 | MUST | both | `CreateSerde` |
| `serde-9.no-library-type-escapes` | SERDE-9 | MUST | both | `CreateSerde`, `MalformedPayload` |
| `serde-10.directional-subtypes` | SERDE-10 | MUST | both | `CreateSerde`, `MalformedPayload` |
| `serde-12.stream-error-unwrapped` | SERDE-12 | MUST | both | `CreateSerde` |
| `serde-15.tristate-emission` | SERDE-15 | MUST | both | `CreateSerde` |
| `serde-16.tristate-decode` | SERDE-16 | MUST | both | `CreateSerde` |
| `serde-17.missing-key-absent` | SERDE-17 | MUST | both | `CreateSerde` |
| `serde-19.default-wiring` | SERDE-19 | MUST | both | `CreateSerde` |
| `serde-20.top-level-degrades` | SERDE-20 | SHOULD | both | `CreateSerde` |

`SERDE-4` (the fixed-buffer encode is core's `SerdeExtensions.Serialize(ISerde, Span<byte>, T)`), `SERDE-13` (the typed readers reject a wire null after the codec has returned) and `SERDE-14`, `-18`, `-30`
(`Tristate<T>`'s own factories and sentinel text) judge core code, so no codec can be broken to fail them: appendix B cites core's tests for them (validation F6). The first driver is
`tests/Dexpace.Sdk.Serialization.SystemTextJson.Tests/Conformance/CodecConformanceTests.cs`, over `SystemTextJsonSerde` and a source-generated context of the probe models, with no waiver and nothing
not exercised.

## The packaging suite (phase 10)

*Added 2026-10-14 (phase 10, P10-10).* `PackagingSuite` runs 8 assertions over the `NFR` rows that are properties of the **published packages**, against a
`PackagingSubject { PackagePaths, CoreId, Policies, DeclaredFloor }`: `PackagePaths` are the `.nupkg` files a pack produced, `CoreId` names the core package (whose only dependency may be the logging
facade, design §10 entry 1), `Policies` says what each other package may depend on, and `DeclaredFloor` is the one target framework every package declares (`net10.0`). It is synchronous (`PackagingSuite.RunAll(subject)` returns the report) and reads the `.nupkg` files with the framework's own
zip, XML and metadata readers, adding no package, and never loads a packed assembly. `PackagePolicy(ThirdPartyRoot, FirstPartyIds)` mirrors the policy record of `scripts/ci/dependency-audit.cs`, and
`PackagingPolicyParityTests` fails if the two tables disagree. CI runs it after `dotnet pack` on every operating system:

```bash
dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages
```

The dependency audit stays beside it: the audit reads the closure of the **build output**, the suite the **metadata that was published** (two checks, two subjects).

> Packaging conformance report. A green run does not prove: anything about a package it was not given (it reads the packages it was given; a package built from another tree is a different subject); how the assemblies behave (it reads their metadata and never loads them); the build's reproducibility, repository signing or the release path (NFR-12, NFR-16: other gates); or any requirement no assertion cites. It proves that every assertion that ran met its clause over the packages it read, or is waived below, and nothing more.

| Assertion | Requirements | Level | Faces | Reads |
|---|---|---|---|---|
| `nfr-1.core-dependencies` | NFR-1 | MUST | none | the core package's nuspec |
| `nfr-2.adapter-budget` | NFR-2 | SHOULD | none | each other package's nuspec, `Policies` |
| `nfr-3.no-internals-granted` | NFR-3 | SHOULD | none | each packed assembly |
| `nfr-8.trim-and-aot-declared` | NFR-8, NFR-9 | MUST | none | each packed assembly |
| `nfr-10.single-floor` | NFR-10 | MUST | none | `lib/`, the dependency groups, `DeclaredFloor` |
| `nfr-13.license-declared` | NFR-13 | SHOULD | none | each nuspec |
| `nfr-14.lockstep-version` | NFR-14 | SHOULD | none | each nuspec |
| `nfr-15.version-resolvable` | NFR-15 | SHOULD | none | each packed assembly and nuspec |

## One report across suites

`ConformanceReport.Combine(subjectName, reports)` merges the reports of several suites run against one subject: the results are concatenated, the worst status wins per requirement and per appendix-B
item, and the waivers are the union. Each report records the suite that produced it (`Suite`: `transport`, `invariant`, `codec` or `packaging`, or `combined`) and its own preamble (`PreambleText`; the static
`ConformanceReport.Preamble` is the transport suite's). `SuiteOptions { AssertionTimeout, Waivers }` bounds and waives a run of the three newer suites; `TransportSuiteOptions` keeps the release bound only a
socket-driving suite needs. `AppendixBMap` maps all 61 items of appendix B, and `tests/Dexpace.Sdk.Conformance.Tests/APPENDIX_B.md` says where each is evidenced; a five-rule test keeps the two honest
(see [cross-cutting invariants](./cross-cutting-invariants.md)).

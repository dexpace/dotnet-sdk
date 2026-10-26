# Write a transport

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). Ported from Node's [`write-a-transport.md`](./write-a-transport.md)
(`nodejs-sdk`, `origin/main`) and restated for the .NET seams: the interfaces are `IAsyncHttpClient` and `IHttpClient`, the proof is `Dexpace.Sdk.Conformance`'s `TransportSuite`, and the rules are the
thirty `TRANSPORT-N` clauses of `docs/product-spec/17-transport-adapter-conformance-contract.md` as the reference transport meets them ([`transport.md`](./transport.md)).

A transport is one method, in two spellings:

```csharp
public interface IAsyncHttpClient : IAsyncDisposable
{
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}

public interface IHttpClient : IDisposable
{
    Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
}
```

There is no registration step. A conforming object is a valid transport; you pass it to `DexpacePipeline.CreateDefault(transport)` or to `PipelineBuilder.Build(transport)` (or, under dependency injection, to
`UseTransport(...)` on the client builder). Write one when the reference transport does not fit: a different HTTP stack, an in-process loopback, a recording double, an instrumented wrapper. Most needs
are smaller than a transport. A custom `DelegatingHandler`, a proxy, a certificate or an HTTP/2 setting belongs under `SystemNetHttpClient(HttpMessageHandler)`, which keeps every rule below for you
(it checks that the primary handler does not follow redirects, and never disposes a handler you keep). A test double is `DelegateHttpClient.Create(...)`
([`seams.md`](./seams.md)). Write a transport when you adapt a different native client.

## Which face

The async face is the primary one (`SEAM-16`, `SEAM-17`): `Task<Response>`, never `null`. Implement `IHttpClient` as well only when the native client has a **real** blocking path
(`HttpClient.Send` is one). A synchronous face that blocks on the asynchronous one is sync-over-async dressed as a seam (styleguide 9.1), and the pipeline already offers the honest bridges:
`AsAsync(TaskScheduler)` for a blocking-only client (it runs each call on the scheduler you choose, because blocking calls starve a shared pool) and `AsBlocking()` as a last resort. Neither disposes
what it wraps.

## The smallest useful one

Complete, and compiled by the documentation build. It answers every request with its own URL as the body, and does the three things every transport does: it hands back a lazily-read body the caller owns,
it never returns `null`, and a failure before dispatch (a `null` request, a cancelled token) arrives through the returned task and never from the call.

```csharp
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

await using var transport = new EchoTransport();
using HttpPipeline pipeline = DexpacePipeline.CreateDefault(transport);

using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/echo"), CancellationToken.None);
Console.WriteLine(await response.Body.ReadAsStringAsync());   // https://api.example.com/echo

internal sealed class EchoTransport : IAsyncHttpClient
{
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Task.FromException<Response>(new ArgumentNullException(nameof(request)));   // TRANSPORT-21: through the task
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Response>(cancellationToken);
        }

        // Received headers take the lenient path: obs-text and the like are accepted from a server, where Headers.With is strict.
        var headers = new Headers.Builder().AddInbound("Content-Type", "text/plain; charset=utf-8").Build();
        var body = ResponseBody.FromBytes(Encoding.UTF8.GetBytes(request.Url.AbsoluteUri), MediaType.Parse("text/plain; charset=utf-8"));
        return Task.FromResult(new Response(request, Status.Ok, Protocol.Http11, headers, body));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Note `AddInbound`, not `Add`: a header a server sent is accepted leniently (an obs-text byte survives), and only a control character is refused. The strict setter on a real server's headers makes a response with
an obs-text byte unreadable.

## Thirteen rules a real transport must follow

The contract is the spec chapter above. These are the clauses that are easy to get wrong, in the form the .NET reference transport meets them.

**1. Never follow redirects** (`TRANSPORT-1`). Turn the native client's following off (`AllowAutoRedirect = false` on `SocketsHttpHandler`) and default any "follow redirects" knob of yours to off. The pipeline's
`RedirectPolicy` is the single redirect authority; a transport that follows a hop silently defeats loop detection, the hop cap, `Authorization` stripping and the downgrade guard at once. The reference
transport rejects a handler whose primary follows redirects, and fails a call whose final URI differs from the one it sent in scheme, host, port or path, because a credential may already have crossed an origin.

**2. Do not let the native client re-send behind the SDK's back** (`TRANSPORT-2`, `-17`, `-18`). A native retry on a connection that ended before any response byte sends the request again, and each send
serialises the body again. Switch it off where the client has a switch, and where it has none (`SocketsHttpHandler` has no switch) stop it by other means (the reference transport's `PlaintextStreamFilter`
turns a premature end into an error, so the SDK's retry policy decides). A single-use request body is written to the wire exactly once; where the native layer can re-send, tee the first write into a
bounded capture and replay it byte for byte, and fail with a retryable `ServiceRequestException` rather than ship a truncated body. A replayable body may be written again.

**3. Drop the framing headers, and log every drop by name** (`TRANSPORT-10` to `-13`; `XCUT-18`). `Host`, `Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Upgrade`, `TE` and `Expect` are
the client's to compute; forwarding a caller's copy corrupts framing. Log the **name**, never the value, and bound the set of names you remember so a hostile caller cannot grow it. The caller's explicit
`Content-Type` wins over the body's; the body's type is emitted only when the caller set none.

**4. Whatever your native client refuses, drop that header, never the request** (`TRANSPORT-12`). `Headers` admits every RFC 9110 token, so a name `HttpRequestMessage` refuses is model-valid. Re-check each
header at the wire and drop the one that fails; a native exception must not escape the send contract, or it looks like a retryable network failure that burns the caller's whole retry budget re-proving a
permanent misconfiguration. On the inbound side, copy leniently (`TRANSPORT-14`): a control byte in a value drops that header alone, and the body and the other headers are still delivered. (The reference
transport cannot meet the malformed-**name** clause, because `SocketsHttpHandler` rejects the whole response first; that is its one permanent waiver.)

**5. Classify a native failure with state first, and shape second** (`TRANSPORT-3`, `-4`, `-8`, `-20`; `XCUT-1`, `XCUT-2`). Four outcomes, and they must not blur:

| What happened | Surface as |
|---|---|
| the caller's token is signalled | an `OperationCanceledException` that carries the caller's token; terminal, never retried |
| your own deadline elapsed, or a read timed out | `ServiceRequestTimeoutException`, retryable; it must **not** look like the caller's cancellation, even when the runtime reports both with the same exception family |
| any other cancellation (the native client or your transport disposed mid-call, `CancelPendingRequests`) | a new terminal `OperationCanceledException`; keep the native exception **off** `InnerException` (put it on the `ExceptionTrail`), because the retry classifier calls any `IOException` in a cause chain retryable |
| the request failed and no response arrived (refused, DNS, TLS, reset) | `ServiceRequestException`, retryable; it is an `IOException`, not an `SdkException` |

Ask the caller's token first, then look at the shape; never match on a message. A defect or a configuration error (`ObjectDisposedException`, `NotSupportedException`) propagates unwrapped.

**6. Failures arrive through the task, and a response is never `null`** (`TRANSPORT-21`, `-23`; `ASYNC-1`, `ASYNC-2`). Everything that can go wrong before dispatch, including a `null` argument and a synchronous
rejection by the native client, is a faulted or cancelled task. Only a truly fatal runtime error propagates synchronously. A transport with no response completes exceptionally.

**7. One deadline per call, for that call only** (`TRANSPORT-5`, `-6`). `RequestOptions.Timeout` replaces your configured default for the call it rides on and never changes a shared native client. Never let
a positive deadline be truncated to zero: where the native API treats zero as "none", clamp up to the smallest finite deadline. Arm the deadline on a `TimeProvider`, so a test can drive it.

**8. Cancellation releases the exchange; a late response is disposed** (`TRANSPORT-7`, `-9`; `SEAM-30`). Pass the call's token into the native exchange so its connection is released promptly. The simplest design
has no race at all: `await` the native call under a token you cancel for a deadline, and the response you return is the response the call produced. If you build a race (a `TaskCompletionSource<Response>`
settled by whoever finishes first), the response that loses must be disposed. The core `RS0030` ban on `TaskCompletionSource<T>.SetResult` and `Task<T>.WaitAsync` exists for exactly this: they orphan a
`Response` against a caller who stopped waiting. (The caller's side of the same race is `await task.WaitOrDisposeAsync(token)`.)

**9. The caller owns the response, and the body is lazy** (`TRANSPORT-22`, `-24`, `-25`; `SEAM-11`). Return the live, unread stream (`HttpCompletionOption.ResponseHeadersRead`, never a buffered copy); closing the
`Response` closes the native body and returns the connection. If adapting a live native response throws, dispose the native response before you propagate, on both faces. Map the status code totally: a vendor
code (499, 520) is a `Status` like any other, its body readable and closeable.

**10. Ownership decides what `Dispose` releases** (`TRANSPORT-15`, `-16`; `SEAM-14`, `SEAM-15`). Dispose only what the transport created; a native client or handler the caller gave you is never disposed or
changed, so the caller may keep using it. `DisposeAsync` is idempotent and does not wait for a call in flight. Decide the post-dispose behaviour and say so: the reference transport throws
`ObjectDisposedException` on every call after dispose (a faulted task on the async face), borrowed clients included. Make the choice once, at construction, and make passing both a borrowed client and an option
that would build one a construction-time `ArgumentException`.

**11. Concurrent and effectively immutable** (`TRANSPORT-29`; `SEAM-12`). One instance serves concurrent calls from many threads; all per-request state lives in locals or in the returned response graph.

**12. Body-less requests, odd inbound values and files** (`TRANSPORT-26`, `-27`, `-28`). A body-less `POST`, `PUT` or `PATCH` is valid: substitute a zero-length body with `Content-Length: 0` if the native client
insists, and attach no body to `GET`, `HEAD` or `TRACE`. An unparseable inbound `Content-Type` is no media type, and a missing or non-numeric `Content-Length` is the unknown length (`-1`), not a failed response.
A file body is replayable and must put exactly its range on the wire; write it through `RequestBody.WriteToAsync` (or `WriteTo`), which opens a fresh handle and copies exactly that range.

**13. Refuse at construction what you cannot honour; never leak a credential** (`TRANSPORT-30`). `ProxyOptions.Type` admits `Http`, `Socks4` and `Socks5`; if your client cannot do one, say so when the
transport is built (a warning naming the feature and never a value, or an `ArgumentException`) rather than on the first send. A proxy credential is answered only to a matching proxy's `407`, never to an
origin's `401`, and is never logged.

## Prove it

Do not hand-roll the assertions. `Dexpace.Sdk.Conformance` holds the suite the reference transport runs against both of its constructions and a raw-socket client runs as the kit's own driver
([`conformance.md`](./conformance.md)). A subject is a descriptor of factories, so each assertion gets a fresh transport; the kit owns a loopback HTTP/1.1 server and never sleeps. This program is
complete and compiled by the documentation build; it drives a decorator over the reference transport, the realistic shape of an instrumented wrapper:

```csharp
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Http.SystemNet;

var subject = new TransportSubject
{
    Name = "Acme.Http.CountingTransport",
    CreateAsync = settings => new CountingTransport(new SystemNetHttpClient(settings.Logger)),   // the suite owns, and disposes, what this returns
    AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,                             // SEAM-15 is a MAY: declare yours, or it is Vacuous
};

var options = new TransportSuiteOptions
{
    Waivers =
    [
        new("TRANSPORT-14", "the native client rejects a whole response on a malformed header name before this transport sees any header")
        {
            Assertion = "transport-14.name-malformed-dropped",
        },
    ],
};

ConformanceReport report = await TransportSuite.RunAllAsync(subject, options);
Console.WriteLine(report);
return report.IsGreen ? 0 : 1;

internal sealed class CountingTransport(IAsyncHttpClient inner) : IAsyncHttpClient
{
    private long _calls;

    public long Calls => Interlocked.Read(ref _calls);

    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return inner.ExecuteAsync(request, options, cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
```

Run as written (measured on 2026-10-26), the report is green with 37 passed, the one waiver used and 30 results `NotExercised`: the blocking face, which the subject does not supply, and the five hooks below. That is
the difference between a green report and a complete proof, and it is why a driver pins the list it leaves unexercised.

In a test project the same call is one xUnit theory over `TransportSuite.Assertions`, one case per (assertion, face): the repository's own driver is `SystemNetConformanceTests` in
`tests/Dexpace.Sdk.Http.SystemNet.Tests/` between its `// <driver>` markers, and [`conformance.md`](./conformance.md#driving-it) prints the same shape, kept identical to the code by
`ConformancePageTests`. `Assert.Skip` turns a waived, vacuous or not-exercised result into a visible skip instead of a silent pass.

- **Statuses.** Each result is `Passed`, `Failed`, `Errored` (a bug in the kit or in the assertion's setup, never green), `Vacuous` (the assertion established from inside that the clause's antecedent is
  absent), `Waived` or `NotExercised` (the subject lacks the face or the hook the assertion needs). A report is green when nothing `Failed` or `Errored`.
- **Faces.** `CreateAsync` and `CreateBlocking` each supply a face; an assertion runs once per face the subject supplies. A transport with one face leaves the other `NotExercised`.
- **Hooks.** `CreateBorrowed`, `CreateWithFaultingAdaptation`, `CreateWithInternalCancel`, `CreateWithNativeResend` and `CreateWithProxy` build the transport-specific constructions that rules 2, 5, 9, 10 and 13
  need. A missing hook makes its assertion `NotExercised`, which is not a pass: a driver should pin the list it leaves unexercised and fail on any other.
- **Waivers.** `new ConformanceWaiver("TRANSPORT-14", reason)` accepts a gap **by requirement ID**; `Assertion` and `Face` narrow it and `Owner` names the phase that will fix it (`null` is a permanent
  decision). A waiver must stay needed: **a waiver whose assertion now passes fails the run**, so fixing a requirement means deleting its waiver in the same change.
- **What a green run does not prove.** Not TLS, HTTP/2 or HTTP/3, connect-timeout behaviour, or any requirement no assertion cites; the report's preamble says so on every run.

## Reuse the plumbing, by reading it

The pieces below are `internal` to `Dexpace.Sdk.Http.SystemNet`; they are not a library to reference, but they are the shortest way to see a correct implementation of the rules above:

| File | Rule |
|---|---|
| `OutboundHeaderMapper.cs`, `HeaderDropLog.cs` | 3 and 4: where each header goes, what is dropped, the bounded case-insensitive set of logged names |
| `SendFailures.cs` | 5: the one classification table of a native failure, state first |
| `CallDeadline.cs` | 7: the per-call deadline over a `TimeProvider`, linked with the caller's token |
| `ResendCapture.cs`, `CapturingStream.cs` | 2: the bounded capture and replay of a single-use body |
| `HttpResponseMessageBody.cs`, `DisposalAwareStream.cs` | 9: the lazy body, and the release on close |
| `HandlerChain.cs` | 1: the check that a caller's handler chain does not follow redirects, without changing it |
| `NativeDisposal.cs` | 9: disposing a native response on a path where a failure is already in flight, attaching a dispose failure to the primary and never replacing it |
| `SystemNetHttpClient.cs` | 10: the four constructors and who owns what |
| `ProxyOptionsWebProxy.cs`, `ProxyWarnings.cs` | 13: the proxy over `ProxyOptions`, and the construction-time warnings |
| `FirstResponseByteStream.cs` | 2: the `PlaintextStreamFilter` that ends the native re-send |

## Package it

Reference `Dexpace.Sdk.Core` and nothing that is a transport or a codec: your package must not drag `Dexpace.Sdk.Http.SystemNet` into a consumer who chose another. Take at most one third-party library
(`NFR-2` is this repository's rule for its own adapters, and the right budget for yours). Version it against the SDK's lockstep minor ([`versioning.md`](./versioning.md)): before 1.0 a minor may break
`IAsyncHttpClient` or the conformance kit, so a transport pins one minor (`[0.1.0, 0.2.0)`) and re-runs the suite on each bump. Add `Dexpace.Sdk.Conformance` to your test project only, never to the package.

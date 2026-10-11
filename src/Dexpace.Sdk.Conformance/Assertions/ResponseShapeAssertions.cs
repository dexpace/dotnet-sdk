// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>The shape of what comes back (and of what fails before anything is sent): <c>TRANSPORT-21</c>, <c>-23</c>, <c>-24</c> and <c>ASYNC-1</c>.</summary>
internal static class ResponseShapeAssertions
{
    private static readonly int[] s_vendorStatuses = [499, 520, 521, 522, 523, 524, 525, 526, 530];

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define(
            "transport-21.pre-dispatch-failure-via-task",
            ["TRANSPORT-21", "ASYNC-2"],
            RequirementLevel.Must,
            AsyncOnly,
            PreDispatchFailureViaTaskAsync);
        yield return Define(
            "transport-23.never-null",
            ["TRANSPORT-23", "ASYNC-1", "SEAM-16"],
            RequirementLevel.Must,
            AsyncOnly,
            NeverNullAsync);
        yield return Define(
            "async-1.single-non-null-response",
            ["ASYNC-1"],
            RequirementLevel.Must,
            AsyncOnly,
            SingleNonNullResponseAsync);
        yield return Define(
            "transport-24.vendor-status-readable",
            ["TRANSPORT-24"],
            RequirementLevel.Must,
            Both,
            VendorStatusReadableAsync);
    }

    // TRANSPORT-21, ASYNC-2: an argument error is delivered through the returned task, never thrown by the call itself.
    private static async Task PreDispatchFailureViaTaskAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateAsyncTransport();
        var good = Request.Get("http://127.0.0.1:1/");

        await ExpectArgumentFaultAsync(() => transport.Client.ExecuteAsync(null!, RequestOptions.Empty, cancellationToken), "a null request").ConfigureAwait(false);
        await ExpectArgumentFaultAsync(() => transport.Client.ExecuteAsync(good, null!, cancellationToken), "null options").ConfigureAwait(false);
    }

    private static async Task ExpectArgumentFaultAsync(Func<Task<Response>> call, string what)
    {
        Task<Response> task;
        try
        {
            task = call();
        }
        catch (Exception ex)
        {
            throw new ConformanceException(
                $"{what}: the transport threw {ex.GetType().Name} from the call instead of failing the returned task (TRANSPORT-21, ASYNC-2)",
                "a faulted task",
                "a synchronous throw");
        }

        try
        {
            using var response = await task.ConfigureAwait(false);
        }
        catch (ArgumentNullException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new ConformanceException($"{what}: the returned task failed with {ex.GetType().Name}, not ArgumentNullException (TRANSPORT-21)", "ArgumentNullException", ex.GetType().Name);
        }

        throw new ConformanceException($"{what}: the returned task completed with a response (TRANSPORT-21)", "a faulted task", "a response");
    }

    // TRANSPORT-23, ASYNC-1, SEAM-16: whatever the server does, the task never completes with null. (The face wrapper turns a
    // null Response into a ConformanceException, so reaching the end of each send is the proof; a failure is allowed here,
    // it is TRANSPORT-20's business.)
    private static async Task NeverNullAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(request => request.Target switch
        {
            "/ok" => LoopbackResponse.Ok("ok"),
            "/missing" => LoopbackResponse.Status(404, "Not Found"),
            "/unavailable" => LoopbackResponse.Status(503, "Service Unavailable"),
            _ => LoopbackResponse.Abort(),
        });
        var transport = context.CreateTransport();

        foreach (var path in new[] { "/ok", "/missing", "/unavailable", "/abort" })
        {
            await SendAllowingFailureAsync(transport, server.Url(path), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task SendAllowingFailureAsync(IFaceTransport transport, Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await transport.SendAsync(Request.Get(url.AbsoluteUri), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (ConformanceException or OperationCanceledException))
        {
            // A transport failure is a legitimate outcome of the abort script; only a null completion is a violation.
        }
    }

    // ASYNC-1: the one response delivered is the one for the request that was sent.
    private static async Task SingleNonNullResponseAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(request => LoopbackResponse.Ok(request.Target));
        var transport = context.CreateTransport();

        foreach (var path in new[] { "/one", "/two/three", "/four?x=1" })
        {
            using var response = await transport.ExpectResponseAsync(Request.Get(server.Url(path).AbsoluteUri), $"a GET of {path}", cancellationToken).ConfigureAwait(false);
            var body = Encoding.UTF8.GetString(await response.ReadBodyAsync($"the body of {path}", cancellationToken).ConfigureAwait(false));
            Check.Equal(body, path, "the body of the response (the server echoes the request target)");
            Check.Equal(response.Request.Url.PathAndQuery, path, "the request the response reports it answers");
        }
    }

    // TRANSPORT-24: a vendor status is delivered as a status with a readable body; disposing the response releases the connection.
    private static async Task VendorStatusReadableAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        foreach (var code in s_vendorStatuses)
        {
            var codeText = code.ToString(CultureInfo.InvariantCulture);
            var server = context.StartProbeableServer(LoopbackResponse.Status(code, "Vendor Status", body: "vendor-" + codeText, keepAlive: true));

            using (var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/vendor").AbsoluteUri), $"a GET answered {codeText}", cancellationToken).ConfigureAwait(false))
            {
                Check.Equal(response.Status.Code, code, "the status code");
                var body = Encoding.UTF8.GetString(await response.ReadBodyAsync($"the body of the {codeText} response", cancellationToken).ConfigureAwait(false));
                Check.Equal(body, "vendor-" + codeText, $"the body of the {codeText} response");
            }

            await Check.ReleasedAsync(context, server, 0, transport, cancellationToken).ConfigureAwait(false);
            Check.NoFaults(server);
        }
    }
}

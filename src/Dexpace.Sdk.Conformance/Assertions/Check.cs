// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The comparisons assertions are written with. Each failure is a <see cref="ConformanceException"/> carrying what the clause
/// requires and what the transport did, and never a request or response body, a credential or a full URL. Header names are
/// compared ASCII-case-folded (suite-contract clause 3), and there is deliberately no overload that reads a
/// <em>response</em>'s <c>Content-Length</c> header (clause 4: the length is asked of the body).
/// </summary>
internal static class Check
{
    /// <summary>The path a server answers with a keep-alive reply, used to prove that a connection was reused (see <see cref="ReleasedAsync"/>).</summary>
    internal const string ProbePath = "/__conformance-probe";

    /// <summary>
    /// Runs a transport operation whose success the clause requires. A transport failure (any exception that is neither a
    /// <see cref="ConformanceException"/> nor the assertion's own cancellation) means the transport broke the clause, so it is
    /// reported as a <see cref="ConformanceException"/> naming <paramref name="what"/> and the exception type, and never as
    /// an <see cref="ConformanceStatus.Errored"/> result: that status is for kit bugs.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What was being done, for the message.</param>
    /// <param name="cancellationToken">The assertion's token: its cancellation propagates.</param>
    internal static async Task<T> GuardAsync<T>(Func<Task<T>> operation, string what, CancellationToken cancellationToken)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ConformanceException && !cancellationToken.IsCancellationRequested)
        {
            throw new ConformanceException($"{what}: the transport failed with {ex.GetType().Name} where success was required", ex);
        }
    }

    /// <summary>The non-generic form of <see cref="GuardAsync{T}"/>.</summary>
    internal static async Task GuardAsync(Func<Task> operation, string what, CancellationToken cancellationToken) =>
        await GuardAsync<bool>(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }, what, cancellationToken).ConfigureAwait(false);

    /// <summary>Fails the assertion.</summary>
    /// <param name="message">The clause that was broken, one line.</param>
    /// <param name="expected">What the clause requires.</param>
    /// <param name="actual">What the transport did.</param>
    internal static void Fail(string message, string? expected = null, string? actual = null) =>
        throw new ConformanceException(message, expected, actual);

    /// <summary>Fails unless <paramref name="condition"/>.</summary>
    internal static void True(bool condition, string message, string? expected = null, string? actual = null)
    {
        if (!condition)
        {
            Fail(message, expected, actual);
        }
    }

    /// <summary>Fails unless <paramref name="actual"/> equals <paramref name="expected"/>.</summary>
    internal static void Equal<T>(T actual, T expected, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            Fail($"{what} differs", Render(expected), Render(actual));
        }
    }

    /// <summary>Fails unless <paramref name="actual"/> and <paramref name="expected"/> hold the same elements in the same order.</summary>
    internal static void SequenceEqual<T>(IEnumerable<T> actual, IEnumerable<T> expected, string what)
    {
        var left = actual.ToArray();
        var right = expected.ToArray();
        if (!left.SequenceEqual(right))
        {
            Fail($"{what} differs", string.Join(", ", right.Select(Render)), string.Join(", ", left.Select(Render)));
        }
    }

    /// <summary>Fails when the server recorded a fault: an unparseable request, a script that ran out, a responder that threw. Names the exception types, never their messages.</summary>
    internal static void NoFaults(LoopbackServer server)
    {
        var faults = server.Faults;
        if (faults.Count > 0)
        {
            Fail("the fixture server recorded a fault while the transport was exercised", "no server fault", string.Join(", ", faults.Select(fault => fault.GetType().Name)));
        }
    }

    /// <summary>The values of the request header <paramref name="name"/> (case-insensitive), in arrival order.</summary>
    internal static IReadOnlyList<string> HeaderValues(RecordedRequest request, string name) => request.HeaderValues(name);

    /// <summary>The single value of the request header <paramref name="name"/> (case-insensitive), <see langword="null"/> when absent; more than one line is a failure.</summary>
    internal static string? HeaderValue(RecordedRequest request, string name)
    {
        var values = request.HeaderValues(name);
        if (values.Count > 1)
        {
            Fail($"the request carried the '{name}' header on {values.Count} lines", "one line", values.Count.ToString(CultureInfo.InvariantCulture));
        }

        return values.Count == 0 ? null : values[0];
    }

    /// <summary>
    /// Fails unless the client has <em>released</em> connection <paramref name="connection"/> within the release bound. A
    /// pooling client returns a released connection to its pool instead of closing it, so when <paramref name="probe"/> is
    /// given the check first sends one request through it to the server's probe path: a pool that reuses the connection
    /// shows it as a further request on it, one that closed it shows the close, and a connection still held by a leaked
    /// response shows neither. The server must answer <see cref="ProbePath"/> with a keep-alive reply
    /// (<see cref="SuiteContext.StartProbeableServer"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The server, not the client, closed the connection (a kit bug, so <see cref="ConformanceStatus.Errored"/>): after a
    /// server-side close the wait proves nothing, so the assertion must script a keep-alive reply (plan reading R13).
    /// </exception>
    internal static async Task ReleasedAsync(SuiteContext context, LoopbackServer server, int connection, IFaceTransport? probe, CancellationToken cancellationToken)
    {
        if (probe is not null)
        {
            using var response = await probe.ExpectResponseAsync(Request.Get(server.Url(ProbePath).AbsoluteUri), "the probe request that shows whether the connection was reused", cancellationToken).ConfigureAwait(false);
            _ = await response.ReadBodyAsync("the probe response", cancellationToken).ConfigureAwait(false);
        }

        await Bounded.WaitAsync(
            server.WaitForConnectionReleasedAsync(connection, cancellationToken),
            context.ReleaseTimeout,
            $"the server to see the client release connection {connection}",
            cancellationToken).ConfigureAwait(false);
        if (server.ServerClosedFirst(connection))
        {
            throw new InvalidOperationException($"Connection {connection} was closed by the server, so its release proves nothing: script a keep-alive reply (plan reading R13).");
        }
    }

    private static string Render<T>(T value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
}

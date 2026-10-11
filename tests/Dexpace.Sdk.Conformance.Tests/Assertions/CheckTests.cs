// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Conformance.Wire;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

[Trait("Category", "Unit")]
public sealed class CheckTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SuiteContext Context(TimeSpan? release = null) =>
        new(RawSocketSubject.Create(), TransportFace.Async, new TransportSuiteOptions { ReleaseTimeout = release ?? TimeSpan.FromMilliseconds(400) });

    [Fact]
    public void Every_comparison_fails_with_expected_and_actual_populated()
    {
        var fail = Assert.Throws<ConformanceException>(() => Check.Fail("clause", "a", "b"));
        var truth = Assert.Throws<ConformanceException>(() => Check.True(false, "clause", "yes", "no"));
        var equal = Assert.Throws<ConformanceException>(() => Check.Equal(520, 502, "status"));
        var sequence = Assert.Throws<ConformanceException>(() => Check.SequenceEqual(["a", "b"], ["a", "c"], "values"));

        Assert.Equal(("a", "b"), (fail.Expected, fail.Actual));
        Assert.Equal(("yes", "no"), (truth.Expected, truth.Actual));
        Assert.Equal(("502", "520"), (equal.Expected, equal.Actual));
        Assert.Equal(("a, c", "a, b"), (sequence.Expected, sequence.Actual));
        Assert.Contains("status", equal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparisons_that_hold_do_not_throw()
    {
        Check.True(true, "x");
        Check.Equal("a", "a", "x");
        Check.SequenceEqual([1, 2], [1, 2], "x");
    }

    [Fact]
    public async Task NoFaults_names_the_exception_types_and_never_their_messages()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        Check.NoFaults(server);
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync("POST /a HTTP/1.1\r\nHost: x\r\nContent-Length: SECRET-VALUE\r\n\r\n", Ct);
        await peer.ReadUntilAsync("\r\n\r\n", Ct);

        var error = Assert.Throws<ConformanceException>(() => Check.NoFaults(server));

        Assert.Contains(nameof(MalformedRequestException), error.Actual, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-VALUE", error.Message + error.Actual + error.Expected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Header_values_are_compared_case_folded_and_a_repeated_line_is_a_failure()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync("GET /a HTTP/1.1\r\nHost: x\r\nX-One: 1\r\nx-two: a\r\nX-TWO: b\r\n\r\n", Ct);
        await peer.ReadUntilAsync("\r\n\r\n", Ct);
        var request = await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);

        Assert.Equal("1", Check.HeaderValue(request, "x-ONE"));
        Assert.Null(Check.HeaderValue(request, "X-Absent"));
        Assert.Equal(["a", "b"], Check.HeaderValues(request, "X-Two"));
        Assert.Throws<ConformanceException>(() => Check.HeaderValue(request, "X-Two"));
    }

    [Fact]
    public async Task A_connection_the_client_closed_is_released()
    {
        await using var context = Context();
        var server = context.StartServer(LoopbackResponse.Ok("x", keepAlive: true));
        var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync("GET /a HTTP/1.1\r\nHost: x\r\n\r\n", Ct);
        await peer.ReadUntilAsync("x", Ct);

        peer.Dispose();

        await Check.ReleasedAsync(context, server, 0, probe: null, Ct);
    }

    [Fact]
    public async Task A_connection_nobody_released_fails_within_the_release_bound_naming_the_connection()
    {
        await using var context = Context(TimeSpan.FromMilliseconds(300));
        var server = context.StartServer(LoopbackResponse.Ok("x", keepAlive: true));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync("GET /a HTTP/1.1\r\nHost: x\r\n\r\n", Ct);
        await peer.ReadUntilAsync("x", Ct);

        var error = await Assert.ThrowsAsync<ConformanceException>(() => Check.ReleasedAsync(context, server, 0, probe: null, Ct));

        Assert.Contains("connection 0", error.Message, StringComparison.Ordinal);
        Assert.Contains("300 ms", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_release_check_after_a_server_side_close_is_a_kit_bug_not_a_pass()
    {
        await using var context = Context();
        var server = context.StartServer(LoopbackResponse.Ok("x"));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync("GET /a HTTP/1.1\r\nHost: x\r\n\r\n", Ct);
        await peer.ReadUntilAsync("x", Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Check.ReleasedAsync(context, server, 0, probe: null, Ct));
    }

    [Fact]
    public async Task A_probe_through_a_transport_that_closes_on_dispose_shows_the_first_connection_released()
    {
        await using var context = Context();
        var server = context.StartProbeableServer(LoopbackResponse.Ok("first", keepAlive: true));
        await using var transport = context.CreateTransport();
        using (var response = await transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get(server.Url("/a").AbsoluteUri), Ct))
        {
            Assert.Equal("first", await response.Body.ReadAsStringAsync(Ct));
        }

        await Check.ReleasedAsync(context, server, 0, transport, Ct);

        Assert.Equal("/a", server.Requests[0].Target);
    }

    [Fact]
    public async Task A_pooled_connection_is_shown_released_by_the_probe_because_the_pool_reuses_it()
    {
        await using var context = new SuiteContext(
            new TransportSubject { Name = "pooled", CreateAsync = _ => new PooledStreamingTransport() },
            TransportFace.Async,
            new TransportSuiteOptions { ReleaseTimeout = TimeSpan.FromSeconds(3) });
        var server = context.StartProbeableServer(LoopbackResponse.Ok("first", keepAlive: true));
        var transport = context.CreateTransport();
        using (var response = await transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get(server.Url("/a").AbsoluteUri), Ct))
        {
            Assert.Equal("first", await response.Body.ReadAsStringAsync(Ct));
        }

        await Check.ReleasedAsync(context, server, 0, transport, Ct);

        Assert.Contains(server.Requests, request => request.Target == Check.ProbePath && request.Connection == 0);
    }

    [Fact]
    public async Task A_connection_held_by_an_undisposed_response_is_never_shown_released_however_many_probes_are_sent()
    {
        await using var context = new SuiteContext(
            new TransportSubject { Name = "pooled", CreateAsync = _ => new PooledStreamingTransport() },
            TransportFace.Async,
            new TransportSuiteOptions { ReleaseTimeout = TimeSpan.FromMilliseconds(800) });
        var server = context.StartProbeableServer(LoopbackResponse.Large(4 * 1024 * 1024, seed: 1, keepAlive: true));
        var transport = context.CreateTransport();
        var held = await transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get(server.Url("/a").AbsoluteUri), Ct);

        var error = await Assert.ThrowsAsync<ConformanceException>(() => Check.ReleasedAsync(context, server, 0, transport, Ct));

        Assert.Contains("connection 0", error.Message, StringComparison.Ordinal);
        Assert.True(server.ConnectionCount > 1, "the probes had to open connections of their own");
        held.Dispose();
    }

    [Fact]
    public async Task A_probeable_server_answers_the_probe_without_consuming_a_script_entry()
    {
        await using var context = Context();
        var server = context.StartProbeableServer(LoopbackResponse.Ok("one", keepAlive: true), LoopbackResponse.Ok("two", keepAlive: true));
        await using var transport = context.CreateTransport();
        string[] paths = ["/a", Check.ProbePath, "/b"];
        var bodies = new List<string>();
        foreach (var path in paths)
        {
            using var response = await transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get(server.Url(path).AbsoluteUri), Ct);
            bodies.Add(await response.Body.ReadAsStringAsync(Ct));
        }

        Assert.Equal(["one", "probe", "two"], bodies);
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/transport_suite/outbound.rb
// (framing_recomputed_and_pass_through_kept: "send a bogus Content-Length/Host plus a pass-through header; assert the
// framing headers are recomputed and the pass-through survives"), re-expressed against the loopback fixture and
// widened to this adapter's whole drop set (design §3.2). Not ported from the same file: the TRANSPORT-10 and
// TRANSPORT-26 rows (phase 8b; TRANSPORT-10 is in phase 1's "Not in phase 1" table). Not ported from
// transport_suite/header_drops.rb: its two rows are TRANSPORT-12's non-token name and TRANSPORT-13's once-per-name
// log latch. This port's model already rejects a non-token name (design §11 item 36), so the Ruby row's antecedent is
// unreachable through the public API; TRANSPORT-13's latch is phase 8b's.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;
using Microsoft.Extensions.Logging;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Roadmap phase 1, S2 (TRANSPORT-11; design §3.2). The verified defect: a caller-set <c>Host: evil.example</c>
/// replaced the real host line on the wire, and the framing headers passed through. The adapter now drops the framing
/// set (<c>Host</c>, <c>Content-Length</c>, <c>Transfer-Encoding</c>, <c>Connection</c>, <c>Keep-Alive</c>,
/// <c>Upgrade</c>, <c>TE</c>, <c>Expect</c>) so <c>HttpClient</c> computes them. Every assertion reads the raw bytes
/// the loopback server received. Permanent (roadmap constraint 5); phase 8b's header mapping owns the structural form.
/// </summary>
[Trait("Category", "Security")]
public sealed class FramingHeaderDropWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Fact]
    public async Task A_caller_set_host_does_not_reach_the_socket_and_the_real_host_does()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        var request = Request.Get(server.Url("/host").ToString())
            .WithHeader("Host", "evil.example")
            .WithHeader("X-Pass", "kept");
        await using var response = await transport.ExecuteAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.DoesNotContain("evil.example", recorded.RawText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"127.0.0.1:{server.BaseUri.Port}", recorded.Header("Host"));
        Assert.Equal("kept", recorded.Header("X-Pass"));
        Assert.Empty(server.Faults);
    }

    // header, a hostile value no correct transport would compute for a 5-byte replayable body
    public static TheoryData<string, string> FramingHeaders => new()
    {
        { "Host", "evil.example" },
        { "Content-Length", "9999" },
        { "Transfer-Encoding", "gzip" },
        { "Connection", "x-smuggled-hop" },
        { "Keep-Alive", "timeout=31337" },
        { "Upgrade", "evil/1.0" },
        { "TE", "x-evil-coding" },
        { "Expect", "x-evil-expectation" },
    };

    [Theory]
    [MemberData(nameof(FramingHeaders))]
    public async Task A_caller_set_framing_header_is_dropped_and_recomputed(string name, string hostile)
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));

        var request = Request.Post(server.Url("/framing").ToString(), RequestBody.FromString("hello", CommonMediaTypes.TextPlain))
            .WithHeader(name, hostile)
            .WithHeader("X-Pass", "kept");
        var error = await Record.ExceptionAsync(async () =>
        {
            await using var response = await transport.ExecuteAsync(request, deadline.Token);
        });

        var recorded = Assert.Single(server.Requests);
        Assert.DoesNotContain(hostile, recorded.RawText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(error);
        Assert.Equal($"127.0.0.1:{server.BaseUri.Port}", recorded.Header("Host"));
        Assert.Equal("5", recorded.Header("Content-Length"));
        Assert.Null(recorded.Header("Transfer-Encoding"));
        Assert.Equal("hello", recorded.BodyText);
        Assert.Equal("kept", recorded.Header("X-Pass"));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task Each_dropped_framing_header_is_logged_at_debug_by_name_and_never_by_value()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        var logger = new RecordingLogger();
        await using var transport = new SystemNetHttpClient(client, logger);

        var request = Request.Get(server.Url("/logged").ToString())
            .WithHeader("Host", "evil.example")
            .WithHeader("Connection", "x-smuggled-hop")
            .WithHeader("X-Pass", "kept");
        await using var response = await transport.ExecuteAsync(request, Ct);

        var drops = logger.Entries.Where(entry => entry.Level == LogLevel.Debug).Select(entry => entry.Message).ToArray();
        Assert.Equal(2, drops.Length);
        Assert.Contains(drops, message => message.Contains("host", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(drops, message => message.Contains("connection", StringComparison.OrdinalIgnoreCase));
        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain("evil.example", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("x-smuggled-hop", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("x-pass", entry.Message, StringComparison.OrdinalIgnoreCase);
        });
    }
}

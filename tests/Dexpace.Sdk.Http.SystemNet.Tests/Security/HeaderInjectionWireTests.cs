// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/transport_suite/outbound.rb
// (forged_header_name_is_rejected_before_dispatch, forged_header_value_is_rejected_before_dispatch), re-expressed
// against the loopback fixture. The Ruby rows forge a duck-typed request that never met a builder; a .NET Request is
// a sealed record over a sealed Headers, so the model-level rejection is what a caller can reach, and the adapter's
// re-check is exercised through the one bypass the model admits: a value accepted on the lenient inbound path
// (HTTP-19) and re-used on a request.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Logging;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Roadmap phase 1, S1 at the wire (HTTP-17, HTTP-18, HTTP-26, XCUT-18, TRANSPORT-12; design §3.2). The verified
/// defect: a model header value <c>"a\r\nX-Injected: yes"</c> went out through <c>TryAddWithoutValidation</c> as two
/// header lines. Every assertion reads the raw bytes the loopback server received. Permanent (roadmap constraint 5);
/// the model half is <c>Dexpace.Sdk.Core.Tests.Security.HeaderInjectionValidationTests</c>.
/// </summary>
[Trait("Category", "Security")]
public sealed class HeaderInjectionWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    private static void AssertNoInjectedLine(LoopbackServer server)
    {
        foreach (var request in server.Requests)
        {
            Assert.DoesNotContain(
                request.HeaderLines,
                line => line.StartsWith("x-injected", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("injected", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData("X-Custom", "a\r\nX-Injected: yes")]
    [InlineData("X-Custom", "a\nX-Injected: yes")]
    [InlineData("X-Custom", "a\rX-Injected: yes")]
    [InlineData("X-Evil\r\nInjected", "v")]
    public async Task A_crlf_in_a_header_never_reaches_the_socket(string name, string value)
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"), LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        var error = await Record.ExceptionAsync(async () =>
        {
            var request = Request.Get(server.Url("/inject").ToString()).WithHeader(name, value);
            await using var response = await transport.ExecuteAsync(request, Ct);
        });

        AssertNoInjectedLine(server);
        var rejected = Assert.IsType<ArgumentException>(error);
        Assert.Contains("U+000", rejected.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Injected", rejected.Message, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task A_crlf_in_a_media_type_parameter_never_reaches_the_socket()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        var error = await Record.ExceptionAsync(async () =>
        {
            var mediaType = MediaType.Of(
                "text",
                "plain",
                new Dictionary<string, string> { ["charset"] = "utf-8\r\nX-Injected: yes" });
            var request = Request.Post(server.Url("/inject").ToString(), RequestBody.FromString("hi", mediaType));
            await using var response = await transport.ExecuteAsync(request, Ct);
        });

        AssertNoInjectedLine(server);
        Assert.IsType<ArgumentException>(error);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task The_adapter_re_checks_at_the_wire_boundary_and_drops_a_header_that_bypassed_outbound_validation()
    {
        // An inbound-lenient value (obs-text, HTTP-19) re-used on a request is the one way past the outbound rule. The
        // adapter drops that header alone, logs its name, and sends the rest (XCUT-18 defence in depth, TRANSPORT-12).
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        var logger = new RecordingLogger();
        await using var transport = new SystemNetHttpClient(client, logger);
        var headers = new Headers.Builder()
            .AddInbound("X-Echoed", "caf\u00E9-secret")
            .Add("X-Pass", "kept")
            .Build();

        await using var response = await transport.ExecuteAsync(
            Request.Create(Method.Get, server.Url("/recheck").ToString(), headers), Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Empty(recorded.HeaderValues("X-Echoed"));
        Assert.Equal("kept", recorded.Header("X-Pass"));
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("x-echoed", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_inbound_header_is_validated_leniently_so_obs_text_parses_and_a_control_byte_is_dropped()
    {
        // XCUT-18's inbound clause and HTTP-19: outbound strictness must not break parsing a response.
        const string Reply = "HTTP/1.1 200 OK\r\nX-Control: a\u0001b\r\nX-Obs: caf\u00E9\r\nX-Normal: n\r\n"
            + "Content-Length: 2\r\nConnection: close\r\n\r\nhi";
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(Reply));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/inbound").ToString()), Ct);

        Assert.Equal("n", response.Headers.Get("X-Normal"));
        Assert.False(response.Headers.Contains("X-Control"));
        Assert.NotNull(response.Headers.Get("X-Obs"));
        Assert.Equal("hi", await response.Body.ReadAsStringAsync(Ct));
    }
}

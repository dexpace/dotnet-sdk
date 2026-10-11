// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-14's value and obs-text clauses, TRANSPORT-11 and OBS-19 (phase 8b, P8b-12, P8b-13): a response header whose value carries
/// a control character costs only itself, is logged by name at <see cref="LogLevel.Debug"/> under event 112, and obs-text in a value is
/// preserved (HTTP-19). The header <em>name</em> clause is not testable here: the native parser refuses the whole response before the
/// adapter sees a header (design §10 entry 32), which the conformance kit asserts and waives.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InboundHeaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class CraftedResponseHandler(Action<HttpResponseMessage> craft) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Build();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Build());

        private HttpResponseMessage Build()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello") };
            craft(response);
            return response;
        }
    }

    [Fact]
    public async Task Obs_text_in_a_value_is_preserved()
    {
        using var handler = new CraftedResponseHandler(response => response.Headers.TryAddWithoutValidation("X-Obs", "café"));
        await using var transport = new SystemNetHttpClient(handler);

        await using var delivered = await transport.ExecuteAsync(Request.Get("https://example.test/x"), RequestOptions.Empty, Ct);

        Assert.Equal("café", delivered.Headers.Get("X-Obs"));
    }

    [Fact]
    public async Task A_control_byte_in_a_value_drops_that_header_alone_and_logs_its_name_at_debug_under_event_112()
    {
        var logger = new RecordingLogger();
        using var handler = new CraftedResponseHandler(response =>
        {
            response.Headers.TryAddWithoutValidation("X-Control", "a\u0001b");
            response.Headers.TryAddWithoutValidation("X-Normal", "n");
        });
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger });

        await using var delivered = await transport.ExecuteAsync(Request.Get("https://example.test/x"), RequestOptions.Empty, Ct);

        Assert.False(delivered.Headers.Contains("X-Control"));
        Assert.Equal("n", delivered.Headers.Get("X-Normal"));
        Assert.Equal("hello", await delivered.Body.ReadAsStringAsync(Ct));
        var drop = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, drop.Level);
        Assert.Equal(DexpaceLogEvents.TransportInboundHeaderDroppedId, drop.EventId.Id);
        Assert.Equal(DexpaceLogEvents.TransportInboundHeaderDropped, drop.EventId.Name);
        Assert.Contains("X-Control", drop.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0001", drop.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dropped_inbound_header_is_debug_every_time_whatever_the_policy()
    {
        var logger = new RecordingLogger();
        using var handler = new CraftedResponseHandler(response => response.Headers.TryAddWithoutValidation("X-Control", "a\u0001b"));
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger, HeaderDropLogging = HeaderDropLogging.Every });

        for (var i = 0; i < 3; i++)
        {
            await using var delivered = await transport.ExecuteAsync(Request.Get("https://example.test/x"), RequestOptions.Empty, Ct);
        }

        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Debug, entry.Level));
    }

    [Fact]
    public async Task A_content_header_with_a_control_byte_is_dropped_the_same_way()
    {
        var logger = new RecordingLogger();
        using var handler = new CraftedResponseHandler(response => response.Content.Headers.TryAddWithoutValidation("Content-Language", "e\u0002n"));
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger });

        await using var delivered = await transport.ExecuteAsync(Request.Get("https://example.test/x"), RequestOptions.Empty, Ct);

        Assert.False(delivered.Headers.Contains("Content-Language"));
        var drop = Assert.Single(logger.Entries);
        Assert.Equal(DexpaceLogEvents.TransportInboundHeaderDroppedId, drop.EventId.Id);
    }
}

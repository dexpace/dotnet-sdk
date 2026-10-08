// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk events_test.rb and step_test.rb EmissionTest (L843) where they assert the five event names, levels
// and keys; the malformed-Location row is stricter than the reference (P6b-20: redacted, not raw).

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.Core.Tests.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>The five redirect events (REDIR-28, OBS-20, OBS-39; P6b-19, P6b-20).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class RedirectLogTests
{
    private static readonly Uri s_current = new("https://api.example.com/a");
    private static readonly Uri s_target = new("https://other.example/b?sig=secret");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IReadOnlyList<RecordedLog> Redirects(RecordingLogger logger) =>
        [.. logger.Entries.Where(e => e.EventId.Name?.StartsWith("http.redirect.", StringComparison.Ordinal) == true)];

    private static async Task<IReadOnlyList<RecordedLog>> RunAsync(
        RecordingLogger logger,
        Request request,
        RedirectOptions options,
        params object[] script)
    {
        using var transport = new ScriptedTransport(script);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Add(new InstrumentationPolicy(logger)).Build(transport);
        try
        {
            using var response = await pipeline.SendAsync(request, new DexpaceClientOptions { Redirect = options }, Ct);
        }
        catch (RedirectException)
        {
            // Expected for the refusal rows: the events were written before the throw.
        }

        return Redirects(logger);
    }

    [Fact]
    public void Each_event_has_its_id_name_level_and_keys()
    {
        var logger = new RecordingLogger();
        var context = TestContexts.For(logger: logger);

        RedirectLog.Hop(context, s_current, s_target, 302, 1, crossOrigin: true);
        RedirectLog.LoopDetected(context, s_current, s_target, 2);
        RedirectLog.DowngradeRejected(context, s_current, s_target);
        RedirectLog.DowngradePermitted(context, s_current, s_target);
        RedirectLog.LocationMalformed(context, s_current, "ftp://x/y");

        var entries = logger.Entries;
        Assert.Equal(5, entries.Count);
        (int Id, string Name, LogLevel Level, string[] Keys)[] expected =
        [
            (150, "http.redirect.hop", LogLevel.Information, ["url.full", "dexpace.redirect.target", "http.response.status_code", "dexpace.redirect.hop", "dexpace.redirect.cross_origin"]),
            (151, "http.redirect.loop_detected", LogLevel.Warning, ["url.full", "dexpace.redirect.target", "dexpace.redirect.hop"]),
            (152, "http.redirect.scheme_downgrade_rejected", LogLevel.Warning, ["url.full", "dexpace.redirect.target"]),
            (153, "http.redirect.scheme_downgrade_permitted", LogLevel.Warning, ["url.full", "dexpace.redirect.target"]),
            (154, "http.redirect.location_malformed", LogLevel.Warning, ["url.full", "dexpace.redirect.location"]),
        ];

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Id, entries[i].EventId.Id);
            Assert.Equal(expected[i].Name, entries[i].EventId.Name);
            Assert.Equal(expected[i].Level, entries[i].Level);
            foreach (var key in expected[i].Keys)
            {
                Assert.True(entries[i].Has(key), $"{expected[i].Name} lacks {key}");
            }
        }
    }

    [Fact]
    public void The_hop_number_is_one_based_and_the_cross_origin_flag_is_a_bool()
    {
        var logger = new RecordingLogger();

        RedirectLog.Hop(TestContexts.For(logger: logger), s_current, s_target, 307, 3, crossOrigin: false);

        var entry = logger.Entries.Single();
        Assert.Equal(3, entry["dexpace.redirect.hop"]);
        Assert.Equal(false, entry["dexpace.redirect.cross_origin"]);
        Assert.Equal(307, entry["http.response.status_code"]);
    }

    [Fact]
    public void A_userinfo_and_token_bearing_target_is_redacted()
    {
        var logger = new RecordingLogger();
        var current = new Uri("https://u:p@api.example.com/a?token=abc");
        var target = new Uri("https://u:p@other.example/b?sig=secret");

        RedirectLog.Hop(TestContexts.For(logger: logger), current, target, 302, 1, crossOrigin: true);

        var entry = logger.Entries.Single();
        Assert.DoesNotContain("secret", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", Assert.IsType<string>(entry["dexpace.redirect.target"]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://user:pw@/x")]
    [InlineData("//u:pw@h")]
    public void A_malformed_location_carrying_userinfo_is_redacted_by_RedactHeaderValue(string raw)
    {
        var logger = new RecordingLogger();

        RedirectLog.LocationMalformed(TestContexts.For(logger: logger), s_current, raw);

        var entry = logger.Entries.Single();
        Assert.DoesNotContain("pw", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pw", Assert.IsType<string>(entry["dexpace.redirect.location"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_throwing_logger_leaves_the_decision_and_the_response_unchanged()
    {
        var logger = new ThrowingLogger { ThrowOnLog = true };
        using var transport = new ScriptedTransport(TestResponses.Redirect(302, "/next"), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Add(new InstrumentationPolicy(logger)).Build(transport);

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/a"), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains(logger.Attempts, a => a.EventId.Name == DexpaceLogEvents.RedirectHop);
        Assert.Contains(logger.Attempts, a => a.EventId.Name == DexpaceLogEvents.LogFailed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_logger_cancelling_under_a_cancelled_token_leaves_the_response_disposed_once(bool sync)
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        var logger = new ThrowingLogger { ThrowOnLog = true, ExceptionFactory = () => new OperationCanceledException(cts.Token) };
        using var transport = new ScriptedTransport(
            () =>
            {
                cts.Cancel();
                return RedirectFixtures.TrackedRedirect("/next", log, "redirect");
            },
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Add(new InstrumentationPolicy(logger)).Build(transport);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            _ = sync
                ? pipeline.Send(Request.Get("https://api.example.com/a"), new DexpaceClientOptions(), cts.Token)
                : await pipeline.SendAsync(Request.Get("https://api.example.com/a"), new DexpaceClientOptions(), cts.Token));

        Assert.Equal(["redirect:dispose"], log);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public void A_disabled_logger_builds_nothing()
    {
        var logger = new DisabledLogger();
        var context = TestContexts.For(logger: logger);

        RedirectLog.Hop(context, s_current, s_target, 302, 1, crossOrigin: false);
        RedirectLog.LoopDetected(context, s_current, s_target, 1);
        RedirectLog.DowngradeRejected(context, s_current, s_target);
        RedirectLog.DowngradePermitted(context, s_current, s_target);
        RedirectLog.LocationMalformed(context, s_current, "raw");

        // DisabledLogger.Log throws, so reaching here proves no delegate formatted and logged anything.
        Assert.True(logger.IsEnabledCalls >= 5);
    }

    [Fact]
    public async Task NotEligible_and_HopCap_emit_no_event()
    {
        var logger = new RecordingLogger();

        var notEligible = await RunAsync(logger, Request.Post("https://api.example.com/a", RequestBody.FromString("x")), new RedirectOptions(), TestResponses.Redirect(302, "/b"));
        var capped = await RunAsync(logger, Request.Get("https://api.example.com/a"), new RedirectOptions { MaxRedirects = 0 }, TestResponses.Redirect(302, "/b"));

        Assert.Empty(notEligible);
        Assert.Empty(capped);
    }

    [Fact]
    public async Task An_absent_location_emits_no_event()
    {
        var events = await RunAsync(new RecordingLogger(), Request.Get("https://api.example.com/a"), new RedirectOptions(), TestResponses.Create(Status.Found));

        Assert.Empty(events);
    }

    [Fact]
    public async Task An_unusable_location_and_a_loop_each_emit_their_warning()
    {
        var malformed = await RunAsync(new RecordingLogger(), Request.Get("https://api.example.com/a"), new RedirectOptions(), TestResponses.Redirect(302, "ftp://x/y"));
        var loop = await RunAsync(new RecordingLogger(), Request.Get("https://api.example.com/a"), new RedirectOptions(), TestResponses.Redirect(302, "/b"), TestResponses.Redirect(302, "/a"));

        Assert.Equal([DexpaceLogEvents.RedirectLocationMalformed], malformed.Select(e => e.EventId.Name));
        Assert.Equal([DexpaceLogEvents.RedirectHop, DexpaceLogEvents.RedirectLoopDetected], loop.Select(e => e.EventId.Name));
    }

    [Theory]
    [InlineData("http://\u00e4.xn--zz/")]
    [InlineData("http://\uffff/")]
    public async Task An_invalid_IDN_location_returns_the_3xx_open_with_the_malformed_event(string location)
    {
        var logger = new RecordingLogger();
        var response = TestResponses.Create(Status.Found, headers: new Headers.Builder().AddInbound("Location", location).Build());
        using var transport = new ScriptedTransport(response);
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Add(new InstrumentationPolicy(logger)).Build(transport);

        using var result = await pipeline.SendAsync(Request.Get("https://api.example.com/a"), Ct);

        Assert.Same(response, result);
        Assert.Equal([DexpaceLogEvents.RedirectLocationMalformed], Redirects(logger).Select(e => e.EventId.Name));
    }

    [Fact]
    public async Task A_rejected_downgrade_emits_the_rejected_event_before_the_throw()
    {
        var events = await RunAsync(new RecordingLogger(), Request.Get("https://api.example.com/a"), new RedirectOptions(), TestResponses.Redirect(302, "http://api.example.com/b"));

        Assert.Equal([DexpaceLogEvents.RedirectSchemeDowngradeRejected], events.Select(e => e.EventId.Name));
        Assert.Equal(LogLevel.Warning, events.Single().Level);
    }

    [Fact]
    public async Task A_permitted_downgrade_emits_the_permitted_event_and_then_the_hop_event()
    {
        var events = await RunAsync(
            new RecordingLogger(),
            Request.Get("https://api.example.com/a"),
            new RedirectOptions { AllowHttpsToHttpDowngrade = true },
            TestResponses.Redirect(302, "http://api.example.com/b"),
            TestResponses.Create(Status.Ok));

        Assert.Equal([DexpaceLogEvents.RedirectSchemeDowngradePermitted, DexpaceLogEvents.RedirectHop], events.Select(e => e.EventId.Name));
        Assert.Equal(true, events[1]["dexpace.redirect.cross_origin"]);
    }

    [Fact]
    public async Task The_events_are_not_gated_by_HttpLoggingOptions_Level()
    {
        var logger = new RecordingLogger();
        using var transport = new ScriptedTransport(TestResponses.Redirect(302, "/b"), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Add(new InstrumentationPolicy(logger)).Build(transport);
        var options = new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.None } };

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/a"), options, Ct);

        Assert.Single(Redirects(logger));
    }

    [Fact]
    public async Task A_followed_hop_adds_a_dexpace_redirect_hop_span_event_on_a_recording_operation_span()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var transport = new ScriptedTransport(TestResponses.Redirect(302, "https://other.example/b?sig=secret"), TestResponses.Create(Status.Ok));
        var pipeline = TracingFixtures.Pipeline(transport);

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/a"), Ct);

        var operation = recorder.Stopped.Single(a => a.Kind == ActivityKind.Internal);
        var hop = Assert.Single(operation.Events, e => e.Name == "dexpace.redirect.hop");
        Assert.Equal(1, TracingFixtures.EventTag(hop, "dexpace.redirect.hop"));
        Assert.Equal(302, TracingFixtures.EventTag(hop, "http.response.status_code"));
        Assert.Equal(true, TracingFixtures.EventTag(hop, "dexpace.redirect.cross_origin"));
        Assert.DoesNotContain("secret", Assert.IsType<string>(TracingFixtures.EventTag(hop, "url.full")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_public_constants_have_the_published_values()
    {
        Assert.Equal("http.redirect.hop", DexpaceLogEvents.RedirectHop);
        Assert.Equal("http.redirect.loop_detected", DexpaceLogEvents.RedirectLoopDetected);
        Assert.Equal("http.redirect.scheme_downgrade_rejected", DexpaceLogEvents.RedirectSchemeDowngradeRejected);
        Assert.Equal("http.redirect.scheme_downgrade_permitted", DexpaceLogEvents.RedirectSchemeDowngradePermitted);
        Assert.Equal("http.redirect.location_malformed", DexpaceLogEvents.RedirectLocationMalformed);
        Assert.Equal([150, 151, 152, 153, 154], new[]
        {
            DexpaceLogEvents.RedirectHopId, DexpaceLogEvents.RedirectLoopDetectedId, DexpaceLogEvents.RedirectSchemeDowngradeRejectedId,
            DexpaceLogEvents.RedirectSchemeDowngradePermittedId, DexpaceLogEvents.RedirectLocationMalformedId,
        });
        Assert.Equal("dexpace.redirect.hop", DexpaceLogKeys.RedirectHop);
        Assert.Equal("dexpace.redirect.target", DexpaceLogKeys.RedirectTarget);
        Assert.Equal("dexpace.redirect.cross_origin", DexpaceLogKeys.RedirectCrossOrigin);
        Assert.Equal("dexpace.redirect.location", DexpaceLogKeys.RedirectLocation);
    }
}

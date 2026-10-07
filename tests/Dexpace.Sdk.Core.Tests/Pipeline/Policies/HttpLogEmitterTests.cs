// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-1, OBS-2, OBS-6, OBS-17, OBS-18, OBS-34, OBS-39: the http.request and http.response events.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class HttpLogEmitterTests
{
    private const string Url = "https://api.example.com/v1/items?token=SECRET&api-version=3";

    private static DexpaceClientOptions Options(HttpLogLevel level = HttpLogLevel.Headers) =>
        new() { Logging = new HttpLoggingOptions { Level = level } };

    private static async Task<Response> SendAsync(
        ILogger logger,
        Func<Request, Response>? respond = null,
        HttpLogLevel level = HttpLogLevel.Headers,
        Request? request = null,
        bool async = true)
    {
        var pipeline = new PipelineBuilder()
            .Add(new InstrumentationPolicy(logger))
            .Build(new RecordingTransport(respond), Options(level));
        request ??= Request.Get(Url);
        return async
            ? await pipeline.SendAsync(request, TestContext.Current.CancellationToken)
            : pipeline.Send(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task At_Headers_a_request_and_a_response_event_are_emitted_at_Information()
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(logger);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal((100, "http.request", LogLevel.Information), (logger.Entries[0].EventId.Id, logger.Entries[0].EventId.Name, logger.Entries[0].Level));
        Assert.Equal((101, "http.response", LogLevel.Information), (logger.Entries[1].EventId.Id, logger.Entries[1].EventId.Name, logger.Entries[1].Level));
    }

    [Fact]
    public async Task The_request_event_carries_method_url_resend_count_and_allowed_headers()
    {
        var logger = new RecordingLogger();
        var request = Request.Get(Url).WithHeaders(
            new Headers.Builder().Add("User-Agent", "ua/1").Add("Authorization", "Bearer SECRET").Build());

        using var response = await SendAsync(logger, request: request);

        var entry = logger.Entries[0];
        Assert.Equal("GET", entry[DexpaceLogKeys.HttpRequestMethod]);
        Assert.Equal("https://api.example.com/v1/items?token=***&api-version=3", entry[DexpaceLogKeys.UrlFull]);
        Assert.Equal(0, entry[DexpaceLogKeys.HttpRequestResendCount]);
        Assert.Equal("ua/1", entry["http.request.header.user-agent"]);
        Assert.Equal("REDACTED", entry["http.request.header.authorization"]);
        Assert.DoesNotContain("SECRET", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(entry.State, p => p.Value is string text && text.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_response_event_carries_status_duration_and_response_headers()
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(
            logger,
            _ => TestResponses.Create(Status.Created, headers: new Headers.Builder().Add("Content-Type", "application/json").Build()));

        var entry = logger.Entries[1];
        Assert.Equal(201, entry[DexpaceLogKeys.HttpResponseStatusCode]);
        var duration = Assert.IsType<double>(entry[DexpaceLogKeys.HttpResponseDurationMs]);
        Assert.True(duration >= 0);
        Assert.Equal("https://api.example.com/v1/items?token=***&api-version=3", entry[DexpaceLogKeys.UrlFull]);
        Assert.Equal("application/json", entry["http.response.header.content-type"]);
        Assert.Equal(0, entry[DexpaceLogKeys.HttpRequestResendCount]);
    }

    [Fact]
    public async Task Body_size_keys_appear_only_for_a_declared_length()
    {
        var known = new RecordingLogger();
        var unknown = new RecordingLogger();
        var knownRequest = Request.Post(Url, RequestBody.FromString("12345"));
        var unknownRequest = Request.Post(Url, RequestBody.FromStream(new MemoryStream([1, 2, 3])));

        using var a = await SendAsync(known, _ => TestResponses.Create(Status.Ok, body: ResponseBody.FromBytes(new byte[7])), request: knownRequest);
        using var b = await SendAsync(unknown, _ => TestResponses.Create(Status.Ok, body: ResponseBody.FromStream(new MemoryStream([1]), null, -1)), request: unknownRequest);

        Assert.Equal(5L, known.Entries[0][DexpaceLogKeys.HttpRequestBodySize]);
        Assert.Equal(7L, known.Entries[1][DexpaceLogKeys.HttpResponseBodySize]);
        Assert.False(unknown.Entries[0].Has(DexpaceLogKeys.HttpRequestBodySize));
        Assert.False(unknown.Entries[1].Has(DexpaceLogKeys.HttpResponseBodySize));
    }

    [Fact]
    public async Task A_failure_emits_one_Warning_event_with_error_type_and_the_exception()
    {
        var logger = new RecordingLogger();
        var failure = new ServiceRequestException("never sent");

        var thrown = await Assert.ThrowsAsync<ServiceRequestException>(() => SendAsync(logger, _ => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(2, logger.Entries.Count);
        var entry = logger.Entries[1];
        Assert.Equal((102, "http.response", LogLevel.Warning), (entry.EventId.Id, entry.EventId.Name, entry.Level));
        Assert.Equal(typeof(ServiceRequestException).FullName, entry[DexpaceLogKeys.ErrorType]);
        Assert.IsType<double>(entry[DexpaceLogKeys.HttpResponseDurationMs]);
        Assert.Same(failure, entry.Exception);
        Assert.False(entry.Has(DexpaceLogKeys.HttpResponseStatusCode));
    }

    [Fact]
    public async Task The_resend_count_follows_the_attempt_number()
    {
        var logger = new RecordingLogger();
        var transport = new ScriptedTransport(TestResponses.Create(Status.ServiceUnavailable), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(new InstrumentationPolicy(logger))
            .Build(transport, Options());

        using var response = await pipeline.SendAsync(Request.Get(Url), TestContext.Current.CancellationToken);

        Assert.Equal([0, 0, 1, 1], logger.Entries.Select(e => (int)e[DexpaceLogKeys.HttpRequestResendCount]!));
    }

    [Fact]
    public async Task The_resend_count_is_the_transmission_ordinal_across_a_redirect_hop_as_on_the_span()
    {
        var logger = new RecordingLogger();
        var transport = new ScriptedTransport(
            TestResponses.Redirect(307, "https://api.example.com/moved"),
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(new InstrumentationPolicy(logger))
            .Build(transport, Options());

        using var response = await pipeline.SendAsync(Request.Get(Url), TestContext.Current.CancellationToken);

        Assert.Equal([0, 0, 1, 1], logger.Entries.Select(e => (int)e[DexpaceLogKeys.HttpRequestResendCount]!));
    }

    [Fact]
    public async Task A_logger_that_disables_Information_receives_nothing_and_is_asked_once_per_attempt()
    {
        var logger = new DisabledLogger();

        using var response = await SendAsync(logger);

        Assert.Equal(1, logger.IsEnabledCalls);
        Assert.Equal(Status.Ok, response.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_url_valued_response_header_is_redacted_on_both_paths(bool async)
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(
            logger,
            _ => TestResponses.Create(Status.Found, headers: new Headers.Builder().Add("Location", "/cb?code=SECRET").Build()),
            async: async);

        Assert.Equal("/cb?***", logger.Entries[1]["http.response.header.location"]);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("SECRET", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.State.Any(p => p.Value is string text && text.Contains("SECRET", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("https:\\\\user:secret@h/p")]
    [InlineData("https:/\\user:secret@h/p")]
    [InlineData("\\/user:secret@h/p")]
    [InlineData("\\\\user:secret@h/p?code=1")]
    public async Task A_backslash_opened_authority_in_a_Location_never_logs_its_userinfo(string location)
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(
            logger,
            _ => TestResponses.Create(Status.Found, headers: new Headers.Builder().Add("Location", location).Build()));

        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("secret", StringComparison.Ordinal));
        Assert.DoesNotContain(
            logger.Entries,
            e => e.State.Any(p => p.Value is string text && text.Contains("secret", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task At_None_nothing_is_emitted_and_IsEnabled_is_not_consulted()
    {
        var logger = new DisabledLogger();

        using var response = await SendAsync(logger, level: HttpLogLevel.None);

        Assert.Equal(0, logger.IsEnabledCalls);
    }

    [Fact]
    public async Task SDK_owned_state_values_are_strings_ints_longs_doubles_or_null()
    {
        var logger = new RecordingLogger();
        var request = Request.Post(Url, RequestBody.FromString("abc"));

        using var ok = await SendAsync(logger, request: request);
        await Assert.ThrowsAsync<ServiceRequestException>(() => SendAsync(logger, _ => throw new ServiceRequestException("x")));

        Assert.All(
            logger.Entries.SelectMany(e => e.State),
            p => Assert.True(p.Value is null or string or int or long or double, $"{p.Key} is {p.Value?.GetType()}"));
    }

    [Fact]
    public async Task The_message_names_the_method_url_and_outcome_but_never_a_header_value()
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(
            logger,
            _ => TestResponses.Create(Status.Ok, headers: new Headers.Builder().Add("ETag", "\"abc123\"").Build()));

        Assert.Equal("HTTP GET https://api.example.com/v1/items?token=***&api-version=3", logger.Entries[0].Message);
        Assert.Equal("HTTP GET https://api.example.com/v1/items?token=***&api-version=3 200", logger.Entries[1].Message);
        Assert.DoesNotContain("abc123", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Event_ids_are_emitted_in_the_documented_order()
    {
        var logger = new RecordingLogger();

        using var response = await SendAsync(logger);

        Assert.Equal([100, 101], logger.Entries.Select(e => e.EventId.Id));
    }
}

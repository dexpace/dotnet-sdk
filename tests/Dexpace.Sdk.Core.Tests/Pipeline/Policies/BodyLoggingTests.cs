// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses, bodies and streams under test are released by the assertions.

using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-34, OBS-36, OBS-37, OBS-38, BODY-34, BODY-18 to BODY-29, P5b-12, P5b-13, P5b-25: body-level logging on both paths.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class BodyLoggingTests
{
    private const string Url = "https://api.example.com/v1/items";

    private static readonly MediaType s_json = MediaType.Parse("application/json");

    private static DexpaceClientOptions Options(HttpLogLevel level = HttpLogLevel.Body, int preview = 8192) =>
        new() { Logging = new HttpLoggingOptions { Level = level, BodyPreviewSize = preview } };

    // A transport that writes the request body (so a tap sees it) and answers with `respond`.
    private sealed class WritingTransport(Func<Request, Response> respond, Exception? failAfterWrite = null) : IAsyncHttpClient, IHttpClient
    {
        public Request? Received { get; private set; }

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Received = request;
            if (request.Body is not null)
            {
                await request.Body.WriteToAsync(Stream.Null, cancellationToken);
            }

            return failAfterWrite is null ? respond(request) : throw failAfterWrite;
        }

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Received = request;
            request.Body?.WriteTo(Stream.Null, cancellationToken);
            return failAfterWrite is null ? respond(request) : throw failAfterWrite;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static Task<Response> Dispatch(HttpPipeline pipeline, Request request, bool async, DexpaceClientOptions? options = null) =>
        DispatchWith(pipeline, request, async, options, TestContext.Current.CancellationToken);

    private static async Task<Response> DispatchWith(HttpPipeline pipeline, Request request, bool async, DexpaceClientOptions? options, CancellationToken token)
    {
        options ??= Options();
        return async
            ? await pipeline.SendAsync(request, options, token)
            : pipeline.Send(request, options, token);
    }

    private static HttpPipeline Pipeline(ILogger logger, IAsyncHttpClient transport, DexpaceClientOptions? options = null) =>
        new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(transport, options ?? Options());

    private static Response Ok(ResponseBody body) => TestResponses.Create(Status.Ok, body: body);

    private static async Task<byte[]> ReadAllAsync(ResponseBody body)
    {
        await using var stream = await body.OpenReadAsync(TestContext.Current.CancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        return buffer.ToArray();
    }

    [Theory]
    [InlineData(HttpLogLevel.None, true)]
    [InlineData(HttpLogLevel.None, false)]
    [InlineData(HttpLogLevel.Headers, true)]
    [InlineData(HttpLogLevel.Headers, false)]
    public async Task At_Headers_and_None_no_wrapper_is_constructed(HttpLogLevel level, bool async)
    {
        var body = ResponseBody.FromBytes("hello"u8.ToArray(), s_json);
        var requestBody = RequestBody.FromString("payload");
        var transport = new WritingTransport(_ => Ok(body));
        var pipeline = Pipeline(new RecordingLogger(), transport);

        using var response = await Dispatch(pipeline, Request.Post(Url, requestBody), async, Options(level));

        Assert.Same(body, response.Body);
        Assert.Same(requestBody, transport.Received!.Body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_logger_that_disables_Information_constructs_no_wrapper_at_Body(bool async)
    {
        var body = ResponseBody.FromBytes("hello"u8.ToArray(), s_json);
        var requestBody = RequestBody.FromString("payload");
        var transport = new WritingTransport(_ => Ok(body));
        var pipeline = Pipeline(new DisabledLogger(), transport);

        using var response = await Dispatch(pipeline, Request.Post(Url, requestBody), async);

        Assert.Same(body, response.Body);
        Assert.Same(requestBody, transport.Received!.Body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task At_Body_the_response_event_carries_a_preview_and_its_size(bool async)
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(ResponseBody.FromBytes("{\"ok\":true}"u8.ToArray(), s_json))));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        var entry = logger.Entries.Single(e => e.EventId.Id == DexpaceLogEvents.HttpResponseId);
        Assert.Equal("{\"ok\":true}", entry[DexpaceLogKeys.HttpResponseBodyPreview]);
        Assert.Equal(11, entry[DexpaceLogKeys.HttpResponseBodyPreviewSize]);
        Assert.Equal(11L, entry[DexpaceLogKeys.HttpResponseBodySize]);
        Assert.Equal("{\"ok\":true}", Encoding.UTF8.GetString(await ReadAllAsync(response.Body)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_binary_response_renders_the_size_only_marker(bool async)
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(ResponseBody.FromBytes(new byte[] { 0, 1, 2, 255 }, MediaType.Parse("image/png")))));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        Assert.Equal("[binary 4 bytes captured]", logger.Entries[^1][DexpaceLogKeys.HttpResponseBodyPreview]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_consumer_still_receives_every_byte_of_an_over_cap_body(bool async)
    {
        var logger = new RecordingLogger();
        var payload = Encoding.ASCII.GetBytes(new string('a', 4096));
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(ResponseBody.FromBytes(payload, MediaType.Parse("text/plain")))), Options(preview: 16));

        using var response = await Dispatch(pipeline, Request.Get(Url), async, Options(preview: 16));

        var entry = logger.Entries[^1];
        Assert.Equal(new string('a', 16), entry[DexpaceLogKeys.HttpResponseBodyPreview]);
        Assert.Equal(16, entry[DexpaceLogKeys.HttpResponseBodyPreviewSize]);
        Assert.Equal(4096L, entry[DexpaceLogKeys.HttpResponseBodySize]);
        Assert.Equal(payload, await ReadAllAsync(response.Body));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_body_that_fits_is_served_from_memory_and_can_be_opened_again(bool async)
    {
        var pipeline = Pipeline(
            new RecordingLogger(),
            new WritingTransport(_ => Ok(ResponseBody.FromStream(new MemoryStream("small"u8.ToArray()), MediaType.Parse("text/plain"), 5))));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        Assert.Equal("small"u8.ToArray(), await ReadAllAsync(response.Body));
        Assert.Equal("small"u8.ToArray(), await ReadAllAsync(response.Body));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_unknown_length_body_is_never_wrapped(bool async)
    {
        var logger = new RecordingLogger();
        var body = ResponseBody.FromStream(new MemoryStream("streamed"u8.ToArray()), MediaType.Parse("text/plain"), -1);
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(body)));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        Assert.Same(body, response.Body);
        Assert.DoesNotContain(logger.Entries, e => e.Has(DexpaceLogKeys.HttpResponseBodyPreview));
        Assert.False(logger.Entries[^1].Has(DexpaceLogKeys.HttpResponseBodySize));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_text_event_stream_body_is_never_wrapped_even_when_it_declares_a_length(bool async)
    {
        var logger = new RecordingLogger();
        var body = ResponseBody.FromBytes("data: x\n\n"u8.ToArray(), CommonMediaTypes.TextEventStream);
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(body)));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        Assert.Same(body, response.Body);
        Assert.DoesNotContain(logger.Entries, e => e.Has(DexpaceLogKeys.HttpResponseBodyPreview));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_request_preview_rides_on_the_response_event(bool async)
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(logger, new WritingTransport(_ => TestResponses.Create(Status.Ok)));
        var request = Request.Post(Url, RequestBody.FromString("hello request", s_json));

        using var response = await Dispatch(pipeline, request, async);

        var requestEvent = logger.Entries.Single(e => e.EventId.Id == DexpaceLogEvents.HttpRequestId);
        var responseEvent = logger.Entries.Single(e => e.EventId.Id == DexpaceLogEvents.HttpResponseId);
        Assert.False(requestEvent.Has(DexpaceLogKeys.HttpRequestBodyPreview));
        Assert.Equal("hello request", responseEvent[DexpaceLogKeys.HttpRequestBodyPreview]);
        Assert.Equal(13, responseEvent[DexpaceLogKeys.HttpRequestBodyPreviewSize]);
        Assert.Equal(13L, requestEvent[DexpaceLogKeys.HttpRequestBodySize]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_request_preview_rides_on_the_failure_event(bool async)
    {
        var logger = new RecordingLogger();
        var failure = new ServiceRequestException("write then fail");
        var pipeline = Pipeline(logger, new WritingTransport(_ => TestResponses.Create(Status.Ok), failure));
        var request = Request.Post(Url, RequestBody.FromString("hello request"));

        await Assert.ThrowsAsync<ServiceRequestException>(() => Dispatch(pipeline, request, async));

        var failureEvent = logger.Entries.Single(e => e.EventId.Id == DexpaceLogEvents.HttpFailureId);
        Assert.Equal("hello request", failureEvent[DexpaceLogKeys.HttpRequestBodyPreview]);
        Assert.False(failureEvent.Has(DexpaceLogKeys.HttpResponseBodyPreview));
    }

    [Fact]
    public async Task Each_attempt_wraps_afresh_and_the_tap_reflects_that_attempt_only()
    {
        var logger = new RecordingLogger();
        var attempts = 0;
        var scripted = DelegateHttpClient.Create(async (request, options, token) =>
        {
            await request.Body!.WriteToAsync(Stream.Null, token);
            return TestResponses.Create(++attempts == 1 ? Status.ServiceUnavailable : Status.Ok);
        });
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(new InstrumentationPolicy(logger))
            .Build(scripted, Options());
        var request = Request.Create(Method.Put, Url, body: RequestBody.FromString("once"));

        using var response = await Dispatch(pipeline, request, async: true);

        var previews = logger.Entries.Where(e => e.EventId.Id == DexpaceLogEvents.HttpResponseId).Select(e => (string)e[DexpaceLogKeys.HttpRequestBodyPreview]!).ToList();
        Assert.Equal(["once", "once"], previews);
    }

    [Fact]
    public async Task The_request_a_policy_holds_is_never_the_logging_wrapper()
    {
        RequestBody? before = null;
        RequestBody? after = null;
        var requestBody = RequestBody.FromString("held");
        var probe = new DelegatePolicy(
            PipelineStage.Auth,
            async (request, context, next) =>
            {
                before = request.Body;
                var response = await next.RunAsync(request, context);
                after = request.Body;
                return response;
            });
        var pipeline = new PipelineBuilder().Add(probe).Add(new InstrumentationPolicy(new RecordingLogger()))
            .Build(new WritingTransport(_ => TestResponses.Create(Status.Ok)), Options());

        using var response = await Dispatch(pipeline, Request.Post(Url, requestBody), async: true);

        Assert.Same(requestBody, before);
        Assert.Same(requestBody, after);
    }

    private sealed class FailingMidStream(int goodBytes, Exception failure) : Stream
    {
        private int _served;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_served >= goodBytes)
            {
                throw failure;
            }

            var n = Math.Min(Math.Min(buffer.Length, goodBytes - _served), 4);
            buffer[..n].Fill((byte)'x');
            _served += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_drain_failure_emits_body_capture_failed_and_the_response_event_still_emits_with_the_partial_preview(bool async)
    {
        var logger = new RecordingLogger();
        var failure = new IOException("upstream broke");
        var body = ResponseBody.FromStream(new FailingMidStream(8, failure), MediaType.Parse("text/plain"), 100);
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(body)));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        var diagnostic = Assert.Single(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.BodyCaptureFailedId);
        Assert.Equal(LogLevel.Warning, diagnostic.Level);
        Assert.Equal(typeof(IOException).FullName, diagnostic[DexpaceLogKeys.ErrorType]);
        Assert.Same(failure, diagnostic.Exception);
        var responseEvent = Assert.Single(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.HttpResponseId);
        Assert.Equal("xxxxxxxx", responseEvent[DexpaceLogKeys.HttpResponseBodyPreview]);
        var thrown = await Assert.ThrowsAsync<IOException>(() => response.Body.OpenReadAsync(TestContext.Current.CancellationToken));
        Assert.Same(failure, thrown);
    }

    private sealed class CancellingBody(CancellationTokenSource cts) : ResponseBody
    {
        private int _disposals;

        public int Disposals => Volatile.Read(ref _disposals);

        public override MediaType? ContentType => MediaType.Parse("text/plain");

        public override long ContentLength => 10;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _disposals);
            base.Dispose(disposing);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_of_the_calls_token_during_the_preview_drain_propagates_and_disposes_the_response(bool async)
    {
        using var cts = new CancellationTokenSource();
        var body = new CancellingBody(cts);
        var logger = new RecordingLogger();
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(body)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DispatchWith(pipeline, Request.Get(Url), async, null, cts.Token));

        Assert.Equal(1, body.Disposals);
        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.LogFailedId);
    }

    [Fact]
    public async Task Disposing_the_returned_response_closes_the_exchange_link_once()
    {
        CallKey key = default;
        var probe = new DelegatePolicy(
            PipelineStage.Serde,
            (request, context, next) =>
            {
                key = context.CallKey;
                return next.RunAsync(request, context);
            });
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(new RecordingLogger())).Add(probe)
            .Build(new WritingTransport(_ => Ok(ResponseBody.FromBytes("x"u8.ToArray(), s_json))), Options());

        var response = await Dispatch(pipeline, Request.Get(Url), async: true);
        var registeredWhileOpen = ContextStore.Shared.TryGet(key, out _);
        await response.DisposeAsync();
        var registeredAfter = ContextStore.Shared.TryGet(key, out _);
        await response.DisposeAsync();

        Assert.True(registeredWhileOpen);
        Assert.False(registeredAfter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContentLength_follows_BODY_29(bool async)
    {
        var pipeline = Pipeline(new RecordingLogger(), new WritingTransport(_ => Ok(ResponseBody.FromBytes("12345"u8.ToArray(), s_json))));

        using var response = await Dispatch(pipeline, Request.Get(Url), async);

        Assert.Equal(5, response.Body.ContentLength);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_zero_preview_size_captures_nothing_and_still_serves_every_byte(bool async)
    {
        var logger = new RecordingLogger();
        var payload = "all of it"u8.ToArray();
        var pipeline = Pipeline(logger, new WritingTransport(_ => Ok(ResponseBody.FromBytes(payload, MediaType.Parse("text/plain")))), Options(preview: 0));

        using var response = await Dispatch(pipeline, Request.Get(Url), async, Options(preview: 0));

        var entry = logger.Entries[^1];
        Assert.Equal(string.Empty, entry[DexpaceLogKeys.HttpResponseBodyPreview]);
        Assert.Equal(0, entry[DexpaceLogKeys.HttpResponseBodyPreviewSize]);
        Assert.Equal(payload, await ReadAllAsync(response.Body));
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>
/// PIPE-37 (BODY-30, BODY-31, HTTP-52 clauses): the policy form of <c>EnsureSuccessAsync</c>. It mirrors the six
/// behaviours of <c>EnsureSuccessErrorMappingTests</c> (S8, <c>Security</c>, unedited) through the pipeline.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ErrorMappingPolicyTests
{
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items");

    private static async Task<Response> SendAsync(HttpPipeline pipeline, bool useAsync) =>
        useAsync
            ? await pipeline.SendAsync(s_request, TestContext.Current.CancellationToken)
            : pipeline.Send(s_request, TestContext.Current.CancellationToken);

    private static HttpPipeline Over(Response response) =>
        new PipelineBuilder().Add(new ErrorMappingPolicy()).Build(new RecordingTransport(_ => response));

    [Fact]
    public void The_policy_is_a_PerCall_stage_and_the_chain_is_static_readonly()
    {
        Assert.Equal(PipelineStage.PerCall, new ErrorMappingPolicy().Stage);
        Assert.Empty(typeof(ErrorMappingPolicy).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        var chain = Assert.Single(typeof(ErrorMappingPolicy).GetFields(BindingFlags.Static | BindingFlags.NonPublic));
        Assert.True(chain.IsInitOnly);
    }

    [Fact]
    public async Task It_runs_once_outside_the_redirect_loop_and_sees_the_final_response()
    {
        var log = new List<string>();
        var counter = new ProbePolicy("outer", PipelineStage.PerCall, log);
        var transport = new ScriptedTransport(
            TestResponses.Redirect(307, "https://api.example.com/v1/items2"),
            TestResponses.Create(Status.NotFound, body: ResponseBody.FromReplayableBytes(Encoding.UTF8.GetBytes("missing"), null)));
        var pipeline = new PipelineBuilder().Add(counter).Add(new ErrorMappingPolicy()).Add(new RedirectPolicy()).Build(transport);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => pipeline.SendAsync(s_request, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, counter.Entered);
        Assert.Equal(404, ex.Status.Code);
        Assert.Equal(2, transport.CallCount);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(204, false)]
    [InlineData(300, true)]
    [InlineData(304, false)]
    [InlineData(307, true)]
    [InlineData(308, false)]
    [InlineData(399, true)]
    [InlineData(600, false)]
    [InlineData(999, true)]
    public async Task A_non_error_status_is_returned_untouched(int code, bool useAsync)
    {
        // PIPE-37, BODY-31: the body is neither opened nor disposed, and the fold is not entered.
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        var original = TestResponses.Create(Status.FromCode(code), s_request, body: body);

        using var response = await SendAsync(Over(original), useAsync);

        Assert.Same(original, response);
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(0, body.DisposeCount);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(404, false)]
    [InlineData(500, true)]
    [InlineData(599, false)]
    public async Task An_error_status_throws(int code, bool useAsync)
    {
        var original = TestResponses.Create(Status.FromCode(code), s_request, body: ResponseBody.FromStream(new MemoryStream([1, 2])));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => SendAsync(Over(original), useAsync));

        Assert.Equal(code, ex.Status.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_error_response_is_mapped_with_a_replayable_buffered_copy(bool useAsync)
    {
        var payload = Encoding.UTF8.GetBytes("{\"error\":\"boom\"}");
        var original = TestResponses.Create(Status.InternalServerError, s_request, body: ResponseBody.FromStream(new MemoryStream(payload), CommonMediaTypes.ApplicationJsonUtf8));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => SendAsync(Over(original), useAsync));

        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_original_response_is_disposed_when_the_error_is_raised(bool useAsync)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        var original = TestResponses.Create(Status.ServiceUnavailable, s_request, body: body);

        await Assert.ThrowsAsync<HttpResponseException>(() => SendAsync(Over(original), useAsync));

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_original_exception_instance_and_stack_surface(bool useAsync)
    {
        // RECOV-10: a failure to buffer surfaces as the same instance, not a wrapper.
        var failure = new IOException("connection reset");
        var log = new List<string>();
        using var body = new FailingBody(log, failure);
        var original = TestResponses.Create(Status.InternalServerError, s_request, body: body);

        var thrown = await Assert.ThrowsAsync<IOException>(() => SendAsync(Over(original), useAsync));

        Assert.Same(failure, thrown);
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_buffered_copy_is_capped_at_one_mebibyte(bool useAsync)
    {
        var payload = new byte[Response.MaxBufferedErrorBytes + 4096];
        Array.Fill(payload, (byte)'x');
        var original = TestResponses.Create(Status.BadGateway, s_request, body: ResponseBody.FromStream(new MemoryStream(payload)));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => SendAsync(Over(original), useAsync));

        Assert.Equal(Response.MaxBufferedErrorBytes, (await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken)).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_response_with_an_empty_replayable_body_is_mapped_without_a_drain(bool useAsync)
    {
        // BODY-30 / P4c-18: the exception carries the response as it is; no read is issued.
        var original = TestResponses.Create(Status.NotFound, s_request);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => SendAsync(Over(original), useAsync));

        Assert.Same(original, ex.Response);
    }

    [Fact]
    public async Task Sync_and_async_agree()
    {
        var syncEx = await Assert.ThrowsAsync<HttpResponseException>(
            () => SendAsync(Over(TestResponses.Create(Status.Forbidden, s_request, body: ResponseBody.FromStream(new MemoryStream([9])))), useAsync: false));
        var asyncEx = await Assert.ThrowsAsync<HttpResponseException>(
            () => SendAsync(Over(TestResponses.Create(Status.Forbidden, s_request, body: ResponseBody.FromStream(new MemoryStream([9])))), useAsync: true));

        Assert.Equal(syncEx.Status, asyncEx.Status);
        Assert.Equal(await syncEx.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken), await asyncEx.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FailingBody(List<string> log, Exception failure) : ResponseBody
    {
        private int _disposes;

        public int DisposeCount => Volatile.Read(ref _disposes);

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<Stream>(failure);

        public override Stream OpenRead(CancellationToken cancellationToken = default) => throw failure;

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _disposes);
            lock (log)
            {
                log.Add("dispose");
            }

            base.Dispose(disposing);
        }
    }
}

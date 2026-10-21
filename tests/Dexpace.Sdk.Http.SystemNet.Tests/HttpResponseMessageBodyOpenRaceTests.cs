// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The messages and bodies under test are released by the tests.

using System.Net;
using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// BODY-14, HTTP-41 under concurrency (phase 11 review fix; design section 10 entry 6). The first open obtains the content stream, and a response content
/// can be consumed once: <c>SocketsHttpHandler</c>'s throws <see cref="InvalidOperationException"/> ("The stream was already consumed") when a second
/// caller reaches it before the first has recorded its stream, and <c>HttpContent</c> refuses a synchronous read of a stream first obtained
/// asynchronously. Two threads opening the body together, in either form, must both be handed the one stream, and a failed or cancelled opener must not
/// take the others down with it. (Two asynchronous opens already share one in-flight task inside <c>HttpContent</c>, so the pairs with a synchronous open
/// are the ones that raced.) The content here is a stand-in with the same one-consume rule and a gate the test holds open, so the overlap is
/// deterministic and not a matter of timing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpResponseMessageBodyOpenRaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // How long the test waits for a second caller to reach the content before it decides there is none: a caller that is going to
    // enter does so in microseconds, so this only bounds the passing case.
    private static readonly TimeSpan s_grace = TimeSpan.FromMilliseconds(250);

    public static TheoryData<string, string> FacePairs() =>
        new() { { "async", "async" }, { "sync", "sync" }, { "sync", "async" }, { "async", "sync" } };

    public static TheoryData<string> Faces() => ["async", "sync"];

    /// <summary>A content that, like a response content, may be consumed once, and whose consume the test can hold open.</summary>
    private sealed class OnceContent(int failFirst = 0) : HttpContent
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        public int Entered => Volatile.Read(ref _entered);

        public void Release() => _gate.TrySetResult();

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 3;
            return true;
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
        {
            var (stream, failure) = Consume();
            _gate.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            return failure is null ? stream : throw failure;
        }

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            var (stream, failure) = Consume();
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return failure is null ? stream : throw failure;
        }

        // The one-consume rule is enforced on entry, as the runtime's is; a failing consume fails once it is let through the gate.
        private (MemoryStream Stream, IOException? Failure) Consume()
        {
            var entry = Interlocked.Increment(ref _entered);
            if (entry > 1 + failFirst)
            {
                throw new InvalidOperationException("The stream was already consumed. It can only be read once.");
            }

            return (new MemoryStream([1, 2, 3], writable: false), entry <= failFirst ? new IOException("The first consume failed.") : null);
        }
    }

    private static HttpResponseMessageBody BodyOver(OnceContent content) =>
        new(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

    private static Task<Stream> OpenAsync(HttpResponseMessageBody body, string face, CancellationToken token) =>
        face == "async" ? body.OpenReadAsync(token) : Task.Run(() => body.OpenRead(token), Ct);

    private static void WaitUntilEntered(OnceContent content, int count) => SpinWait.SpinUntil(() => content.Entered >= count, s_grace);

    [Theory]
    [MemberData(nameof(FacePairs))]
    public async Task Two_concurrent_first_opens_are_handed_one_stream(string firstFace, string secondFace)
    {
        var content = new OnceContent();
        using var body = BodyOver(content);
        try
        {
            var first = OpenAsync(body, firstFace, Ct);
            WaitUntilEntered(content, 1);
            var second = OpenAsync(body, secondFace, Ct);
            WaitUntilEntered(content, 2);
            content.Release();

            var opened = await first.WaitAsync(Ct);
            var other = await second.WaitAsync(Ct);
            Assert.Same(opened, other);
            Assert.Equal(1, content.Entered);
        }
        finally
        {
            content.Release();
        }
    }

    [Fact]
    public async Task Many_concurrent_first_opens_of_either_form_are_handed_one_stream()
    {
        var content = new OnceContent();
        using var body = BodyOver(content);
        try
        {
            var opens = Enumerable.Range(0, 16).Select(i => OpenAsync(body, i % 2 == 0 ? "async" : "sync", Ct)).ToArray();
            WaitUntilEntered(content, 2);
            content.Release();

            var streams = await Task.WhenAll(opens).WaitAsync(Ct);
            Assert.All(streams, stream => Assert.Same(streams[0], stream));
            Assert.Equal(1, content.Entered);
        }
        finally
        {
            content.Release();
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_failed_first_open_does_not_fail_the_open_that_was_waiting_on_it(string secondFace)
    {
        // The opener is held at the gate, a second open queues behind it, and the opener then fails: the second open obtains the stream itself.
        var content = new OnceContent(failFirst: 1);
        using var body = BodyOver(content);
        try
        {
            var first = OpenAsync(body, "sync", Ct);
            WaitUntilEntered(content, 1);
            var second = OpenAsync(body, secondFace, Ct);
            WaitUntilEntered(content, 2);
            content.Release();

            var failure = await Assert.ThrowsAsync<IOException>(() => first.WaitAsync(Ct));
            var opened = await second.WaitAsync(Ct);
            Assert.Equal("The first consume failed.", failure.Message);
            Assert.Same(opened, await OpenAsync(body, secondFace, Ct).WaitAsync(Ct));
            Assert.Equal(2, content.Entered);
        }
        finally
        {
            content.Release();
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_waiting_open_whose_token_is_cancelled_gives_up_alone(string face)
    {
        var content = new OnceContent();
        using var body = BodyOver(content);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        try
        {
            var first = OpenAsync(body, "sync", Ct);
            WaitUntilEntered(content, 1);
            var waiting = OpenAsync(body, face, cancel.Token);
            WaitUntilEntered(content, 2);
            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            content.Release();

            var opened = await first.WaitAsync(Ct);
            Assert.Same(opened, await OpenAsync(body, face, Ct).WaitAsync(Ct));
            Assert.Equal(1, content.Entered);
        }
        finally
        {
            content.Release();
        }
    }

    [Theory]
    [MemberData(nameof(FacePairs))]
    public async Task A_body_disposed_while_its_first_open_is_in_flight_reports_closed(string firstFace, string secondFace)
    {
        var content = new OnceContent();
        var body = BodyOver(content);
        try
        {
            var opening = OpenAsync(body, firstFace, Ct);
            WaitUntilEntered(content, 1);
            var waiting = OpenAsync(body, secondFace, Ct);
            WaitUntilEntered(content, 2);
            await body.DisposeAsync();
            content.Release();

            await Assert.ThrowsAsync<StreamClosedException>(() => opening.WaitAsync(Ct));
            await Assert.ThrowsAsync<StreamClosedException>(() => waiting.WaitAsync(Ct));
        }
        finally
        {
            content.Release();
        }
    }
}

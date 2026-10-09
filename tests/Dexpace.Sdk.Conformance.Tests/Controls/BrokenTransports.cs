// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>The building blocks of negative controls: transports that violate exactly one clause.</summary>
internal static class BrokenTransports
{
    /// <summary>
    /// A transport that delegates to a conforming raw-socket client but lets <paramref name="behave"/> change what the caller
    /// sees. <paramref name="behave"/> gets the call and the inner client.
    /// </summary>
    internal static IAsyncHttpClient Around(Func<Request, RequestOptions, CancellationToken, IAsyncHttpClient, Task<Response>> behave)
    {
#pragma warning disable CA2000 // The raw-socket client holds no resources (a connection belongs to its response), so there is nothing to dispose.
        var inner = new RawSocketHttpClient();
#pragma warning restore CA2000
        return DelegateHttpClient.Create((request, options, ct) => behave(request, options, ct, inner));
    }

    /// <summary>A transport that answers with whatever <paramref name="rewrite"/> makes of the raw-socket client's response.</summary>
    internal static IAsyncHttpClient Rewriting(Func<Response, Response> rewrite) =>
        Around(async (request, options, ct, inner) => rewrite(await inner.ExecuteAsync(request, options, ct)));

    /// <summary>A body stream whose disposal does nothing: a response that never releases its connection.</summary>
    internal sealed class LeakyStream(Stream inner) : Stream
    {
        private readonly Stream _inner = Leaks.Root(inner);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // Deliberately does not dispose the inner stream: the socket stays open.
            base.Dispose(disposing);
        }
    }

    /// <summary>A transport whose call throws synchronously on a null request instead of failing the task.</summary>
    internal sealed class SyncThrowingTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(options);
            return Task.FromException<Response>(new InvalidOperationException("never reached"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A transport that completes its task with a null response.</summary>
    internal sealed class NullResponseTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) => Task.FromResult<Response>(null!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A transport that gives the raw-socket client no token, so a call can only end when the server does, and reports the
    /// caller's cancellation without closing anything: a transport that "accepts the token but leaves the socket open".
    /// </summary>
    internal static IAsyncHttpClient IgnoringCancellation(bool reportCancellation) =>
        Around(async (request, options, ct, inner) =>
        {
            var call = inner.ExecuteAsync(request, options, CancellationToken.None);
            if (!reportCancellation)
            {
                return await call;
            }

            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(() => cancelled.TrySetResult());
            await Task.WhenAny(call, cancelled.Task);
            ct.ThrowIfCancellationRequested();
            return await call;
        });

    /// <summary>A transport that reports a cancelled call as a retryable timeout.</summary>
    internal static IAsyncHttpClient CancellationAsTimeout() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                throw new Dexpace.Sdk.Core.Errors.ServiceRequestTimeoutException("cancelled", ex);
            }
        });

    /// <summary>
    /// A transport that does cancel the call but reports it as a cancellation that does not carry the caller's token: a bare
    /// <c>new OperationCanceledException()</c> (<paramref name="internalSource"/> <see langword="false"/>), or one tied to a
    /// source of its own, as a transport that links its own token does. A caller that filters on <c>ex.CancellationToken == ct</c>
    /// does not recognise either as its own.
    /// </summary>
    internal static IAsyncHttpClient CancellationWithoutCallersToken(bool internalSource) =>
        Around(async (request, options, ct, inner) =>
        {
            using var own = new CancellationTokenSource();
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The original cancellation (which carries the caller's token) is dropped, not chained: a cause chain that
                // still reached the caller's token is accepted by the assertion.
                await own.CancelAsync();
                throw internalSource ? new OperationCanceledException("cancelled", own.Token) : new OperationCanceledException("cancelled");
            }
        });

    /// <summary>
    /// A transport that reacts to the call's token firing with an unrelated, non-timeout SDK failure instead of a cancellation:
    /// the attempt token the pipeline links in means "this attempt timed out", and this transport reports something else.
    /// </summary>
    internal static IAsyncHttpClient CancellationAsUnrelatedSdkFailure() =>
        Around(async (request, options, ct, inner) =>
        {
            var call = inner.ExecuteAsync(request, options, CancellationToken.None);
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(() => cancelled.TrySetResult());
            await Task.WhenAny(call, cancelled.Task);
            return ct.IsCancellationRequested ? throw new Dexpace.Sdk.Core.Errors.SdkException("the exchange failed") : await call;
        });

    /// <summary>A transport whose second disposal throws.</summary>
    internal sealed class ThrowsOnSecondDispose : IAsyncHttpClient
    {
        private int _disposals;

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() =>
            Interlocked.Increment(ref _disposals) > 1 ? throw new InvalidOperationException("already disposed") : ValueTask.CompletedTask;
    }

    /// <summary>A transport whose disposal waits for every call still in flight: the opposite of non-blocking close.</summary>
    internal sealed class DisposeWaitsForInFlight : IAsyncHttpClient
    {
        private readonly RawSocketHttpClient _inner = new();
        private readonly List<Task> _inFlight = [];

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var call = _inner.ExecuteAsync(request, options, CancellationToken.None);
            lock (_inFlight)
            {
                _inFlight.Add(call);
            }

            return call;
        }

        public async ValueTask DisposeAsync()
        {
            Task[] pending;
            lock (_inFlight)
            {
                pending = [.. _inFlight];
            }

            try
            {
                await Task.WhenAll(pending);
            }
#pragma warning disable CA1031 // The wait is the point, not the outcome.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }

    /// <summary>A transport that disposes its response as soon as the call's token is cancelled, even after delivery.</summary>
    internal static IAsyncHttpClient DisposesResponseOnLateCancel() =>
        Around(async (request, options, ct, inner) =>
        {
            var response = await inner.ExecuteAsync(request, options, ct);
            ct.Register(response.Dispose);
            return response;
        });

    /// <summary>A transport that reads the whole response body before returning, so nothing streams.</summary>
    internal static IAsyncHttpClient BufferingBody() =>
        Around(async (request, options, ct, inner) =>
        {
            var response = await inner.ExecuteAsync(request, options, ct);
            var bytes = await response.Body.ReadAsBytesAsync(ct);
            return new Response(response.Request, response.Status, response.Protocol, response.Headers, ResponseBody.FromBytes(bytes, response.Body.ContentType));
        });

    /// <summary>A transport that cuts every response body off after <paramref name="limit"/> bytes.</summary>
    internal static IAsyncHttpClient TruncatingBody(int limit) =>
        Rewriting(response => new Response(
            response.Request,
            response.Status,
            response.Protocol,
            response.Headers,
            ResponseBody.FromStream(new TruncatedStream(response.Body.OpenRead(), limit), null, limit)));

    /// <summary>A transport that gives a body-less POST, PUT or PATCH a one-byte body.</summary>
    internal static IAsyncHttpClient SendsBodyForBodyless() =>
        Around((request, options, ct, inner) =>
            inner.ExecuteAsync(request.Body is null && request.Method.Name is "POST" or "PUT" or "PATCH" ? request.WithBody(RequestBody.FromString("x")) : request, options, ct));

    /// <summary>A transport that fails on a malformed Content-Type instead of downgrading it.</summary>
    internal static IAsyncHttpClient ThrowsOnMalformedContentType() =>
        Rewriting(response => response.Headers.Get("Content-Type") is { } type && !MediaType.TryParse(type, out _)
            ? throw new ArgumentException("malformed media type")
            : response);

    /// <summary>A transport that pads a request body declared as 10 bytes out to 10 bytes, so a short source goes unnoticed.</summary>
    internal static IAsyncHttpClient PadsShortBodies() =>
        Around((request, options, ct, inner) =>
            inner.ExecuteAsync(request.Body is { ContentLength: 10 } ? request.WithBody(RequestBody.FromBytes(new byte[10])) : request, options, ct));

    /// <summary>A read-only stream that reports end-of-stream after <paramref name="limit"/> bytes.</summary>
    internal sealed class TruncatedStream(Stream inner, int limit) : Stream
    {
        private int _remaining = limit;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = _remaining == 0 ? 0 : await inner.ReadAsync(buffer[..Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _remaining == 0 ? 0 : inner.Read(buffer, offset, Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>The conforming raw-socket client behind <c>DelegateHttpClient</c>, which logs nothing.</summary>
    internal static IAsyncHttpClient Plain() => Around((request, options, ct, inner) => inner.ExecuteAsync(request, options, ct));

    /// <summary>A transport that writes the caller's Host, Content-Length and Transfer-Encoding verbatim.</summary>
    internal static IAsyncHttpClient ForwardsCallerFraming()
    {
#pragma warning disable CA2000 // Holds no resources: a connection belongs to its response.
        var inner = new RawSocketHttpClient(forwardCallerFraming: true);
#pragma warning restore CA2000
        return DelegateHttpClient.Create(inner.ExecuteAsync);
    }

    private static readonly string[] s_framingNames = ["Host", "Content-Length", "Transfer-Encoding"];

    private static readonly Action<ILogger, string, string, Exception?> s_droppedWithValue = LoggerMessage.Define<string, string>(
        LogLevel.Debug,
        new EventId(1, "Dropped"),
        "Dropped the '{HeaderName}' header, whose value was '{Value}'.");

    /// <summary>A transport that logs the framing headers it drops together with their values, which it must never do.</summary>
    internal static IAsyncHttpClient LogsDroppedValues(ILogger logger) =>
        Around((request, options, ct, inner) =>
        {
            foreach (var name in s_framingNames)
            {
                if (request.Headers.Get(name) is { } value)
                {
                    s_droppedWithValue(logger, name, value, null);
                }
            }

            return inner.ExecuteAsync(request, options, ct);
        });

    /// <summary>
    /// A transport whose responses cross: it remembers the response that arrived last and, after a pause, hands every caller
    /// that one, the way shared mutable state in a transport does.
    /// </summary>
    internal static IAsyncHttpClient CrossesResponses()
    {
        var gate = new object();
        Response? latest = null;
        return Around(async (request, options, ct, inner) =>
        {
            var response = await inner.ExecuteAsync(request, options, ct);
            lock (gate)
            {
                latest = response;
            }

            await Task.Delay(100, CancellationToken.None);
            lock (gate)
            {
                return latest!;
            }
        });
    }

    /// <summary>A transport that makes a failed send safe to repeat by buffering the body, then sends it again, so the body reaches the server twice.</summary>
    internal static IAsyncHttpClient ResendsBufferedBody() =>
        Around(async (request, options, ct, inner) =>
        {
            var replayable = request.Body is null ? null : await request.Body.ToReplayableAsync(ct);
            var again = replayable is null ? request : request.WithBody(replayable);
            try
            {
                return await inner.ExecuteAsync(again, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestException)
            {
                return await inner.ExecuteAsync(again, options, ct);
            }
        });

    /// <summary>
    /// A transport whose native layer re-sends a body-less request once after a connection failure, as <c>SocketsHttpHandler</c>
    /// does (it re-sends up to three times): the request has no content a second write could be refused for, so nothing stops it.
    /// </summary>
    internal static IAsyncHttpClient RetriesBodylessRequestsOnce() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestException) when (request.Body is null)
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
        });

    /// <summary>A transport that reports a refused or reset connection as a failure that is not retryable.</summary>
    internal static IAsyncHttpClient NonRetryableNoResponse() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestException ex)
            {
                throw new Dexpace.Sdk.Core.Errors.SdkException("send failed", ex);
            }
        });

    /// <summary>
    /// A transport that reports a refused or reset connection as a non-retryable failure with its cause dropped: the retry
    /// classifier walks the cause chain, so a wrapper that keeps the I/O cause would still be retried.
    /// </summary>
    internal static IAsyncHttpClient NonRetryableNoCause() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestException)
            {
                throw new Dexpace.Sdk.Core.Errors.SdkException("send failed");
            }
        });

    /// <summary>A transport that reports a refused or reset connection as a cancellation.</summary>
    internal static IAsyncHttpClient NoResponseAsCancellation() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestException ex)
            {
                throw new OperationCanceledException("send failed", ex);
            }
        });

    /// <summary>A transport that copies the request body without ever looking at the call's token, so an abandoned source is never unblocked.</summary>
    internal static IAsyncHttpClient IgnoresTokenWhileReadingBody() =>
        Around(async (request, options, ct, inner) =>
        {
            if (request.Body is not null)
            {
                await request.Body.WriteToAsync(Stream.Null, CancellationToken.None);
            }

            return await inner.ExecuteAsync(request, options, ct);
        });

    /// <summary>A transport that opens a file body for reading and never closes it.</summary>
    internal static IAsyncHttpClient LeaksFileHandles()
    {
        var leaked = new List<FileStream>();
        return Around((request, options, ct, inner) =>
        {
            if (request.Body is FileRequestBody file)
            {
#pragma warning disable CA2000 // The leak is the point of the control.
                lock (leaked)
                {
                    leaked.Add(new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
#pragma warning restore CA2000
            }

            return inner.ExecuteAsync(request, options, ct);
        });
    }

    /// <summary>A pooling transport over a real <c>HttpClient</c> that is never disposed, so an idle pooled connection outlives the transport.</summary>
    internal sealed class LeakyPooledTransport : IAsyncHttpClient
    {
        private readonly System.Net.Http.HttpClient _client = new(new System.Net.Http.SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            using var message = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, request.Url);
            using var native = await _client.SendAsync(message, cancellationToken);
            var bytes = await native.Content.ReadAsByteArrayAsync(cancellationToken);
            return new Response(request, Status.FromCode((int)native.StatusCode), Protocol.Http11, null, ResponseBody.FromBytes(bytes));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A transport that follows a redirect itself.</summary>
    internal static IAsyncHttpClient FollowsRedirects() =>
        Around(async (request, options, ct, inner) =>
        {
            var response = await inner.ExecuteAsync(request, options, ct);
            if (response.IsRedirect && response.Headers.Get("Location") is { } location)
            {
                response.Dispose();
                return await inner.ExecuteAsync(request.WithUrl(new Uri(location)), options, ct);
            }

            return response;
        });

    /// <summary>A transport that reports an elapsed per-call timeout as a cancellation.</summary>
    internal static IAsyncHttpClient TimeoutAsCancellation() =>
        Around(async (request, options, ct, inner) =>
        {
            try
            {
                return await inner.ExecuteAsync(request, options, ct);
            }
            catch (Dexpace.Sdk.Core.Errors.ServiceRequestTimeoutException ex)
            {
                throw new OperationCanceledException("timed out", ex);
            }
        });

    /// <summary>A transport that never passes the per-call options on, so the timeout is ignored.</summary>
    internal static IAsyncHttpClient IgnoresTimeout() =>
        Around((request, _, ct, inner) => inner.ExecuteAsync(request, RequestOptions.Empty, ct));

    /// <summary>A transport that remembers the first call's timeout and applies it to every later call.</summary>
    internal static IAsyncHttpClient LeaksFirstTimeout()
    {
        TimeSpan? first = null;
        var seen = false;
        return Around((request, options, ct, inner) =>
        {
            lock (inner)
            {
                if (!seen)
                {
                    seen = true;
                    first = options.Timeout;
                }
            }

            return inner.ExecuteAsync(request, new RequestOptions { Timeout = first }, ct);
        });
    }

    /// <summary>A transport that truncates a timeout below one millisecond to "no timeout".</summary>
    internal static IAsyncHttpClient TruncatesSubMillisecondTimeouts() =>
        Around((request, options, ct, inner) =>
            inner.ExecuteAsync(request, options.Timeout is { } timeout && timeout < TimeSpan.FromMilliseconds(1) ? new RequestOptions { Timeout = null } : options, ct));

    /// <summary>
    /// A transport that stops waiting for a call when its token is cancelled but never disposes the response that then
    /// arrives for nobody: the orphaned response of a lost cancel race.
    /// </summary>
    internal static IAsyncHttpClient OrphansLateResponses() =>
        Around(async (request, options, ct, inner) =>
        {
            var call = inner.ExecuteAsync(request, options, CancellationToken.None);
            _ = call.ContinueWith(task => Leaks.Root(task), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(() => cancelled.TrySetResult());
            await Task.WhenAny(call, cancelled.Task);
            ct.ThrowIfCancellationRequested();
            return await call;
        });

    /// <summary>
    /// A transport over the raw-socket client with a handle that cancels every in-flight call from the native side, the way a
    /// native client's own cancel does; <paramref name="mapsToTimeout"/> reports that cancel as a retryable timeout (the F3 defect).
    /// </summary>
    internal sealed class InternalCancelTransport(bool mapsToTimeout) : IAsyncHttpClient
    {
        private readonly CancellationTokenSource _internal = new();
        private readonly RawSocketHttpClient _raw = new();

        /// <summary>Cancels every call in flight, leaving the callers' tokens alone.</summary>
        internal void CancelInFlight() => _internal.Cancel();

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _internal.Token);
            try
            {
                return await _raw.ExecuteAsync(request, options, linked.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && mapsToTimeout)
            {
                throw new Dexpace.Sdk.Core.Errors.ServiceRequestTimeoutException("The request timed out before a response was received.", ex);
            }
        }

        public ValueTask DisposeAsync()
        {
            _internal.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static readonly string[] s_contentClass = ["Content-Language", "Content-Encoding", "Allow"];

    private static readonly Action<ILogger, string, Exception?> s_dropped = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "Dropped"),
        "Dropped the '{HeaderName}' header.");

    /// <summary>A transport that overwrites the caller's Content-Type with the body's, by dropping the caller's.</summary>
    internal static IAsyncHttpClient OverwritesContentType() =>
        Around((request, options, ct, inner) => inner.ExecuteAsync(request.WithHeaders(request.Headers.Without("Content-Type")), options, ct));

    /// <summary>A transport that throws on a header its native layer would refuse instead of dropping it.</summary>
    internal static IAsyncHttpClient ThrowsOnRejectedHeader() =>
        Around((request, options, ct, inner) =>
            s_contentClass.Any(request.Headers.Contains)
                ? throw new ArgumentException("the native layer rejected a header")
                : inner.ExecuteAsync(request, options, ct));

    /// <summary>A transport that drops the headers its native layer refuses without a word.</summary>
    internal static IAsyncHttpClient DropsRejectedHeadersSilently() =>
        Around((request, options, ct, inner) =>
            inner.ExecuteAsync(s_contentClass.Aggregate(request, (current, name) => current.WithHeaders(current.Headers.Without(name))), options, ct));

    /// <summary>A transport that drops the headers its native layer refuses and logs every drop, every time, loudly.</summary>
    internal static IAsyncHttpClient LogsEveryDrop(ILogger logger) =>
        Around((request, options, ct, inner) =>
        {
            foreach (var name in s_contentClass.Where(request.Headers.Contains))
            {
                s_dropped(logger, name, null);
            }

            return inner.ExecuteAsync(s_contentClass.Aggregate(request, (current, name) => current.WithHeaders(current.Headers.Without(name))), options, ct);
        });

    /// <summary>The raw-socket client that fails a whole response on one malformed header.</summary>
    internal static IAsyncHttpClient StrictInbound()
    {
#pragma warning disable CA2000 // Holds no resources: a connection belongs to its response.
        var inner = new RawSocketHttpClient(strictInbound: true);
#pragma warning restore CA2000
        return DelegateHttpClient.Create(inner.ExecuteAsync);
    }

    /// <summary>A transport that strips every non-ASCII byte from response header values.</summary>
    internal static IAsyncHttpClient StripsObsText() =>
        Rewriting(response =>
        {
            var headers = response.Headers.ToBuilder();
            foreach (var name in response.Headers.Names)
            {
                headers.Set(name, new string(response.Headers.Get(name)!.Where(c => c < 0x80).ToArray()));
            }

            return new Response(response.Request, response.Status, response.Protocol, headers.Build(), response.Body);
        });

    /// <summary>A transport that writes a single-use body once to nowhere and then sends it, so the body is written twice.</summary>
    internal static IAsyncHttpClient WritesBodyTwice() =>
        Around(async (request, options, ct, inner) =>
        {
            if (request.Body is not null)
            {
                await request.Body.WriteToAsync(Stream.Null, ct);
            }

            return await inner.ExecuteAsync(request, options, ct);
        });

    /// <summary>A transport that sends a file range as the whole file.</summary>
    internal static IAsyncHttpClient IgnoresFileRange() =>
        Around((request, options, ct, inner) =>
            inner.ExecuteAsync(request.Body is FileRequestBody file ? request.WithBody(RequestBody.FromFile(file.FilePath)) : request, options, ct));

    /// <summary>A transport that re-sends the request natively with an empty body instead of the same bytes.</summary>
    internal static IAsyncHttpClient ResendsAnEmptyBody() =>
        Around(async (request, options, ct, inner) =>
        {
            (await inner.ExecuteAsync(request, options, ct)).Dispose();
            return await inner.ExecuteAsync(request.Body is null ? request : request.WithBody(RequestBody.FromBytes(Array.Empty<byte>())), options, ct);
        });

    /// <summary>A transport that answers the 407 with the proxy credential and then offers the same credential to the origin's 401.</summary>
    internal static IAsyncHttpClient LeaksProxyCredentialToOrigin(ProxyOptions proxy) => ProxyAware(proxy, leakToOrigin: true);

    /// <summary>
    /// A raw-socket transport that talks to a proxy: it answers a 407 with the proxy credential, and only a 407, unless
    /// <paramref name="leakToOrigin"/> makes it offer the same credential to an origin 401 as well.
    /// </summary>
    internal static IAsyncHttpClient ProxyAware(ProxyOptions proxy, bool leakToOrigin)
    {
        var credential = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(proxy.UserName + ":" + proxy.Password));
        return Around(async (request, options, ct, inner) =>
        {
            var viaProxy = request.WithUrl(new Uri($"http://{proxy.Host}:{proxy.Port}{request.Url.PathAndQuery}"));
            var response = await inner.ExecuteAsync(viaProxy, options, ct);
            if (response.Status.Code == 407)
            {
                response.Dispose();
                response = await inner.ExecuteAsync(viaProxy.WithHeader("Proxy-Authorization", credential), options, ct);
            }

            if (leakToOrigin && response.Status.Code == 401)
            {
                response.Dispose();
                response = await inner.ExecuteAsync(viaProxy.WithHeader("Proxy-Authorization", credential).WithHeader("Authorization", credential), options, ct);
            }

            return response;
        });
    }

    /// <summary>A transport that still serves after it was disposed: dispose does nothing.</summary>
    internal static IAsyncHttpClient KeepsServingAfterDispose() => Plain();

    /// <summary>A transport that throws <see cref="ObjectDisposedException"/> from every call once it was disposed.</summary>
    internal sealed class ThrowsAfterDispose(IAsyncHttpClient inner) : IAsyncHttpClient
    {
        private int _disposed;

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return inner.ExecuteAsync(request, options, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _disposed, 1);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The blocking form of <see cref="ThrowsAfterDispose"/>.</summary>
    internal sealed class ThrowsAfterDisposeBlocking(IHttpClient inner) : IHttpClient
    {
        private int _disposed;

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return inner.Execute(request, options, cancellationToken);
        }

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    }
}

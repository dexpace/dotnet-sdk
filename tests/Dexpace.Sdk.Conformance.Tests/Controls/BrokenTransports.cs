// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

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
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

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
}

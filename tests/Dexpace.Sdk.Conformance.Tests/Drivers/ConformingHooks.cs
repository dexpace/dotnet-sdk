// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Controls;
using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// The positive counterparts of the hook assertions (plan 2.4): the raw-socket client cannot supply a capability hook (it
/// has no native client to borrow), so these test-side hook sets are the conforming transports the hook assertions are shown
/// to pass against. The SystemNet driver supplies the real ones.
/// </summary>
internal static class ConformingHooks
{
    /// <summary>The raw-socket subject plus a conforming hook for every capability the raw-socket client cannot supply itself.</summary>
    internal static TransportSubject Create()
    {
        var plain = RawSocketSubject.Create();
        return new TransportSubject
        {
            Name = "RawSocketHttpClient with conforming hooks (test-only)",
            CreateAsync = plain.CreateAsync,
            CreateBlocking = plain.CreateBlocking,
            CreateBorrowed = _ => Borrowed(),
            CreateWithFaultingAdaptation = _ => new FaultsAfterReleasing(),
            CreateWithInternalCancel = _ => InternalCancel(mapsToTimeout: false),
            CreateWithNativeResend = _ => new ResendsBufferedCopy(),
            CreateWithProxy = (_, proxy) => BrokenTransports.ProxyAware(proxy, leakToOrigin: false),
        };
    }

    private static readonly string[] s_contentClass = ["Content-Language", "Content-Encoding", "Allow"];

    private static readonly Action<ILogger, string, Exception?> s_droppedLoudly = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "Dropped"),
        "Dropped the '{HeaderName}' header.");

    private static readonly Action<ILogger, string, Exception?> s_droppedQuietly = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(3, "DroppedAgain"),
        "Dropped the '{HeaderName}' header again.");

    /// <summary>
    /// The positive counterpart of <c>transport-13</c>: a raw-socket client that drops the content-class headers (as a native
    /// client that refuses them on a body-less request does) and logs the first drop of each name, case-insensitively, at
    /// Warning and every later one at Debug. It supplies the async face only, the face the assertion declares.
    /// </summary>
    internal static TransportSubject OncePerNameDropper() => new()
    {
        Name = "RawSocketHttpClient that drops content-class headers and logs each name once (test-only)",
        CreateAsync = settings => DropsAndLogsOncePerName(settings.Logger),
    };

    /// <summary>A transport that drops the content-class headers and logs the first drop of each name loudly and the rest quietly.</summary>
    internal static IAsyncHttpClient DropsAndLogsOncePerName(ILogger logger)
    {
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        return BrokenTransports.Around((request, options, ct, inner) =>
        {
            var forwarded = request;
            foreach (var name in s_contentClass.Where(request.Headers.Contains))
            {
                (seen.TryAdd(name, true) ? s_droppedLoudly : s_droppedQuietly)(logger, name, null);
                forwarded = forwarded.WithHeaders(forwarded.Headers.Without(name));
            }

            return inner.ExecuteAsync(forwarded, options, ct);
        });
    }

    /// <summary>A transport over a "native client" it does not own: disposing the transport leaves the native client working.</summary>
    internal static BorrowedTransport Borrowed()
    {
        var native = new NativeClient();
        return new BorrowedTransport(new OverNative(native, disposesNative: false), native.SendAsync, native);
    }

    /// <summary>A borrowed-client transport that, like the conforming owned one, throws <see cref="ObjectDisposedException"/> once disposed.</summary>
    internal static BorrowedTransport BorrowedThatThrowsAfterDispose()
    {
        var native = new NativeClient();
        return new BorrowedTransport(new BrokenTransports.ThrowsAfterDispose(new OverNative(native, disposesNative: false)), native.SendAsync, native);
    }

    /// <summary>A transport and a handle that cancels its in-flight calls from the native side.</summary>
    internal static InternalCancellation InternalCancel(bool mapsToTimeout)
    {
        var transport = new BrokenTransports.InternalCancelTransport(mapsToTimeout);
        return new InternalCancellation(transport, transport.CancelInFlight);
    }

    /// <summary>A "native client" the transport borrows: it can be disposed, after which it refuses to send.</summary>
    internal sealed class NativeClient : IAsyncDisposable
    {
        private readonly RawSocketHttpClient _raw = new();
        private int _disposed;

        internal bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        internal async Task SendAsync(Uri url, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            using var response = await _raw.ExecuteAsync(Request.Get(url.AbsoluteUri), RequestOptions.Empty, cancellationToken);
            _ = await response.Body.ReadAsBytesAsync(cancellationToken);
        }

        internal Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _raw.ExecuteAsync(request, options, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _disposed, 1);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A transport over a native client; <paramref name="disposesNative"/> makes it close what it only borrowed (the broken form).</summary>
    internal sealed class OverNative(NativeClient native, bool disposesNative) : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            native.ExecuteAsync(request, options, cancellationToken);

        public ValueTask DisposeAsync() => disposesNative ? native.DisposeAsync() : ValueTask.CompletedTask;
    }

    /// <summary>A transport whose adaptation always fails, after releasing the native response, as the clause requires.</summary>
    internal sealed class FaultsAfterReleasing : IAsyncHttpClient
    {
        private readonly RawSocketHttpClient _raw = new();

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var response = await _raw.ExecuteAsync(request, options, cancellationToken);
            response.Dispose();
            throw new InvalidOperationException("adaptation failed");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A transport whose adaptation fails and leaves the native response (and so its socket) open: the broken form.</summary>
    internal sealed class FaultsAndLeaks : IAsyncHttpClient
    {
        private readonly RawSocketHttpClient _raw = new();

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Leaks.Root(await _raw.ExecuteAsync(request, options, cancellationToken));
            throw new InvalidOperationException("adaptation failed");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A transport whose native layer re-sends the request: it buffers a single-use body once into a replayable copy, sends it
    /// twice, and when buffering fails fails the send with the transport-failure type, never shipping a truncated copy.
    /// </summary>
    internal sealed class ResendsBufferedCopy : IAsyncHttpClient
    {
        private readonly RawSocketHttpClient _raw = new();

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            RequestBody? replayable;
            try
            {
                replayable = request.Body is null ? null : await request.Body.ToReplayableAsync(cancellationToken);
            }
            catch (IOException ex)
            {
                throw new Dexpace.Sdk.Core.Errors.ServiceRequestException("The request body could not be buffered for a re-send.", ex);
            }

            var buffered = replayable is null ? request : request.WithBody(replayable);
            (await _raw.ExecuteAsync(buffered, options, cancellationToken)).Dispose();
            return await _raw.ExecuteAsync(buffered, options, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

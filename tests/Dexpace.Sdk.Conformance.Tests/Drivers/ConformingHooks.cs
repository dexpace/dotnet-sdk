// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// The positive counterparts of the hook assertions (plan 2.4): the raw-socket client cannot supply a capability hook (it
/// has no native client to borrow), so these test-side hook sets are the conforming transports the hook assertions are shown
/// to pass against. The SystemNet driver supplies the real ones.
/// </summary>
internal static class ConformingHooks
{
    /// <summary>The raw-socket subject plus a conforming borrowed-client hook and a conforming faulting-adaptation hook.</summary>
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
        };
    }

    /// <summary>A transport over a "native client" it does not own: disposing the transport leaves the native client working.</summary>
    internal static BorrowedTransport Borrowed()
    {
        var native = new NativeClient();
        return new BorrowedTransport(new OverNative(native, disposesNative: false), native.SendAsync, native);
    }

    /// <summary>A transport and a handle that cancels its in-flight calls from the native side.</summary>
    internal static InternalCancellation InternalCancel(bool mapsToTimeout)
    {
        var transport = new Controls.BrokenTransports.InternalCancelTransport(mapsToTimeout);
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
}

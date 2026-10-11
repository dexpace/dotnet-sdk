// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A transport built over a native client the caller owns, with a way to prove the native client is still alive
/// (<c>TRANSPORT-15</c>: ownership-aware close leaves a borrowed client usable; <c>XCUT-22</c>: the SDK closes only what it
/// created). The suite disposes the <see cref="Transport"/> itself as the thing under test, sends one request through
/// <see cref="SendThroughNativeClient"/> to show the native client survived, then disposes this value, which releases the
/// native owner.
/// </summary>
public sealed class BorrowedTransport : IAsyncDisposable
{
    private readonly IAsyncDisposable _nativeClientOwner;
    private int _disposed;

    /// <summary>Creates the value.</summary>
    /// <param name="transport">The transport under test, built over the borrowed native client.</param>
    /// <param name="sendThroughNativeClient">
    /// Sends a request to the given URL straight through the native client, bypassing <paramref name="transport"/>, and
    /// completes once the exchange (including reading and releasing its response) is over. It faults when the native client
    /// is unusable.
    /// </param>
    /// <param name="nativeClientOwner">Releases the native client when the suite is done with it.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public BorrowedTransport(IAsyncHttpClient transport, Func<Uri, CancellationToken, Task> sendThroughNativeClient, IAsyncDisposable nativeClientOwner)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(sendThroughNativeClient);
        ArgumentNullException.ThrowIfNull(nativeClientOwner);
        Transport = transport;
        SendThroughNativeClient = sendThroughNativeClient;
        _nativeClientOwner = nativeClientOwner;
    }

    /// <summary>The transport under test, built over the borrowed native client.</summary>
    public IAsyncHttpClient Transport { get; }

    /// <summary>Sends a request to a URL straight through the native client, not through <see cref="Transport"/>.</summary>
    public Func<Uri, CancellationToken, Task> SendThroughNativeClient { get; }

    /// <summary>Releases the native client once, however often it is called. It never disposes <see cref="Transport"/>: that is the suite's subject.</summary>
    public ValueTask DisposeAsync() =>
        Interlocked.Exchange(ref _disposed, 1) == 0 ? _nativeClientOwner.DisposeAsync() : ValueTask.CompletedTask;
}

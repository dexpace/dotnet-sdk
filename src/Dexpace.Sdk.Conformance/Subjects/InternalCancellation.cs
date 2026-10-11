// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A transport plus the means to cancel its in-flight calls from inside, the way a native client does when it is
/// disposed or told to cancel pending requests, without the caller's token being signalled (<c>TRANSPORT-8</c>: an
/// internal cancellation is terminal, not a retryable timeout).
/// </summary>
public sealed class InternalCancellation
{
    /// <summary>Creates the value.</summary>
    /// <param name="transport">The transport under test.</param>
    /// <param name="cancelInFlight">Cancels every call in flight on <paramref name="transport"/> from the native side.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public InternalCancellation(IAsyncHttpClient transport, Action cancelInFlight)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(cancelInFlight);
        Transport = transport;
        CancelInFlight = cancelInFlight;
    }

    /// <summary>The transport under test.</summary>
    public IAsyncHttpClient Transport { get; }

    /// <summary>Cancels every call in flight from the native side, leaving the caller's token unsignalled.</summary>
    public Action CancelInFlight { get; }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// A sequence over a live stream, which can be walked once: a second walk would resume mid-stream and silently skip
/// events, so asking for a second enumerator throws at the call (SSE-26, SSE-40, P7b-8, P7b-14).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="open">Opens the one enumerator; runs at most once, at the first <see cref="GetEnumerator"/>.</param>
internal sealed class SingleUseSequence<T>(Func<IEnumerator<T>> open) : IEnumerable<T>
{
    private int _taken;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The sequence has already been enumerated.</exception>
    public IEnumerator<T> GetEnumerator()
    {
        SingleUse.Take(ref _taken);
        return open();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>The asynchronous twin of <see cref="SingleUseSequence{T}"/>.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="open">Opens the one enumerator with the enumeration token; runs at most once.</param>
internal sealed class SingleUseAsyncSequence<T>(Func<CancellationToken, IAsyncEnumerator<T>> open) : IAsyncEnumerable<T>
{
    private int _taken;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The sequence has already been enumerated.</exception>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        SingleUse.Take(ref _taken);
        return open(cancellationToken);
    }
}

internal static class SingleUse
{
    internal static void Take(ref int latch)
    {
        if (Interlocked.Exchange(ref latch, 1) != 0)
        {
            throw new InvalidOperationException("This event sequence can be enumerated once; a second walk would resume mid-stream.");
        }
    }
}

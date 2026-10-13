// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// Releases the primary handler the package created for a named client when the container is disposed (P9-13, V8). The factory owns the
/// handler chain and never releases a handler whose lifetime is infinite, so without the lease the pooled connections outlive the container.
/// It is a keyed singleton <em>factory</em>, resolved before the transport, so the container disposes it after the transport. The caller's
/// <c>DelegatingHandler</c>s are the factory's and are never touched (XCUT-22).
/// </summary>
internal sealed class PrimaryHandlerLease : IDisposable
{
    private readonly List<IDisposable> _tracked = [];
    private readonly Lock _gate = new();
    private bool _disposed;

    /// <summary>The handlers the lease holds, for a test to observe.</summary>
    internal IReadOnlyList<IDisposable> Tracked
    {
        get
        {
            lock (_gate)
            {
                return [.. _tracked];
            }
        }
    }

    /// <summary>Takes ownership of <paramref name="handler"/>, built by the package; a second call keeps both and disposes both (R7).</summary>
    /// <param name="handler">The primary handler the package built.</param>
    internal void Track(IDisposable handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var disposeNow = false;
        lock (_gate)
        {
            if (_disposed)
            {
                disposeNow = true;
            }
            else
            {
                _tracked.Add(handler);
            }
        }

        if (disposeNow)
        {
            handler.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        List<IDisposable> release;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            release = [.. _tracked];
        }

        foreach (var handler in release)
        {
            handler.Dispose();
        }
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>The lock-guarded, copy-on-write list of suppressed exceptions behind both trail stores (design §5.2, P4b-14).</summary>
internal sealed class SuppressedTrail
{
    private readonly object _gate = new();
    private Exception[] _items = [];

    /// <summary>Adds <paramref name="secondary"/> unless that same instance is already on the trail.</summary>
    /// <param name="secondary">The suppressed exception.</param>
    internal void Add(Exception secondary)
    {
        lock (_gate)
        {
            foreach (var existing in _items)
            {
                if (ReferenceEquals(existing, secondary))
                {
                    return;
                }
            }

            _items = [.. _items, secondary];
        }
    }

    /// <summary>Returns an immutable snapshot; never the live array.</summary>
    /// <returns>The exceptions attached so far.</returns>
    internal IReadOnlyList<Exception> Snapshot()
    {
        lock (_gate)
        {
            return Array.AsReadOnly([.. _items]);
        }
    }
}

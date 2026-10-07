// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The bounded registry of live call contexts, keyed by <see cref="CallKey"/> (CTX-3, CTX-7..CTX-13, CTX-18, CTX-19;
/// design §5.4, P4a-8, P4a-9, P4a-10).
/// </summary>
/// <remarks>
/// Contexts are held strongly (CTX-19): the capacity, not the collector, is the backstop, so a leaked context stays
/// registered until the cap evicts it. Eviction is arbitrary (CTX-13). Register, overwrite and release on distinct keys
/// need no external lock (CTX-7).
/// </remarks>
internal sealed class ContextStore
{
    /// <summary>The capacity of <see cref="Shared"/> (P4a-10).</summary>
    internal const int DefaultCapacity = 10_000;

    private static readonly ContextStore s_shared = new(DefaultCapacity);

    private readonly BoundedMap<CallKey, CallContext> _map;

    /// <summary>Creates a store with the given capacity.</summary>
    /// <param name="capacity">The bound; at least 1.</param>
    internal ContextStore(int capacity) => _map = new BoundedMap<CallKey, CallContext>(capacity);

    /// <summary>The process-wide store every public context constructor binds.</summary>
    internal static ContextStore Shared => s_shared;

    /// <summary>The tracked entry count.</summary>
    internal int Count => _map.Count;

    /// <summary>The dictionary's own entry count; diagnostic only.</summary>
    internal int EntryCount => _map.EntryCount;

    /// <summary>Installs or replaces the context under its key; never throws (CTX-8, promotion's only path).</summary>
    /// <param name="context">The context.</param>
    internal void Set(CallContext context) => _map.Set(context.Key, context);

    /// <summary>Installs the context only if its key is free (CTX-8).</summary>
    /// <param name="context">The context.</param>
    /// <exception cref="ArgumentException">The key is already registered; the message names the key.</exception>
    internal void Add(CallContext context)
    {
        if (!_map.TryAdd(context.Key, context))
        {
            throw new ArgumentException($"A context is already registered under key {context.Key}.", nameof(context));
        }
    }

    /// <summary>Looks up the context registered under <paramref name="key"/> (CTX-18).</summary>
    /// <param name="key">The key.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when registered.</returns>
    internal bool TryGet(CallKey key, [NotNullWhen(true)] out CallContext? context) => _map.TryGetValue(key, out context);

    /// <summary>Removes <paramref name="context"/> only when it is the occupant of its slot (CTX-9).</summary>
    /// <param name="context">The context.</param>
    /// <returns><see langword="true"/> when it was the occupant and was removed.</returns>
    internal bool Release(CallContext context) => _map.TryRemoveIfSame(context.Key, context);
}

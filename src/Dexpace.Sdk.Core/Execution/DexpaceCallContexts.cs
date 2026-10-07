// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>Public lookup over the process-wide registry of live call contexts (CTX-18).</summary>
public static class DexpaceCallContexts
{
    /// <summary>Looks up the furthest-reached context registered under <paramref name="key"/>.</summary>
    /// <remarks>
    /// The store is bounded (10 000 entries, arbitrary eviction) and holds contexts strongly, so a miss does not mean
    /// the call never existed.
    /// </remarks>
    /// <param name="key">The call key.</param>
    /// <param name="context">The context, or <see langword="null"/> for an unknown key.</param>
    /// <returns><see langword="true"/> when a context is registered.</returns>
    public static bool TryGet(CallKey key, [NotNullWhen(true)] out CallContext? context) =>
        ContextStore.Shared.TryGet(key, out context);
}

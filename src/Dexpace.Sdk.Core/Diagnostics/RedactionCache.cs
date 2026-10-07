// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// A one-slot, reference-keyed holder of the redactor and header renderer built from a call's
/// <see cref="HttpLoggingOptions"/> (OBS-12, P5b-10).
/// </summary>
/// <remarks>
/// Nothing derived is cached on the options record itself: a <see langword="with"/> expression copies private fields, so a
/// cached redactor would survive a copy that changed the allow-list. The slot is a <see langword="volatile"/> reference to
/// an immutable entry, with no lock: the worst race builds an entry twice (XCUT-11). The holder is a separate class so the
/// policy's own field stays <see langword="readonly"/> (PIPE-11).
/// </remarks>
internal sealed class RedactionCache
{
    private volatile Entry? _slot;

    /// <summary>Returns the entry for <paramref name="options"/>, rebuilding it when the instance differs from the cached one.</summary>
    /// <param name="options">The call's logging options.</param>
    /// <returns>The entry; <see cref="Entry.Key"/> is <paramref name="options"/>.</returns>
    internal Entry Get(HttpLoggingOptions options)
    {
        var slot = _slot;
        if (slot is not null && ReferenceEquals(slot.Key, options))
        {
            return slot;
        }

        var redactor = new UrlRedactor(options.AllowedQueryParameters);
        var entry = new Entry(options, redactor, new HeaderLogRenderer(options, redactor));
        _slot = entry;
        return entry;
    }

    /// <summary>The options, and what was derived from them.</summary>
    /// <param name="Key">The options instance the rest was built from.</param>
    /// <param name="Redactor">The URL redactor over <c>AllowedQueryParameters</c>.</param>
    /// <param name="Renderer">The header renderer.</param>
    internal sealed record Entry(HttpLoggingOptions Key, UrlRedactor Redactor, HeaderLogRenderer Renderer);
}

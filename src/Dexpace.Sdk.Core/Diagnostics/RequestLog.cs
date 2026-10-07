// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// What the request half of an attempt's logging leaves for its response half: the request to send (the original, or a copy
/// whose body is tapped at body level), the tap, the call's options and whether the http events are enabled at all. A struct,
/// so the disabled path allocates nothing (OBS-1).
/// </summary>
internal readonly struct RequestLog
{
    internal RequestLog(Request request)
    {
        Request = request;
    }

    internal RequestLog(
        Request request,
        LoggingRequestBody? tap,
        HttpLoggingOptions options,
        RedactionCache.Entry entry,
        bool enabled)
    {
        Request = request;
        Tap = tap;
        Options = options;
        Entry = entry;
        Enabled = enabled;
    }

    /// <summary>The request to send downstream.</summary>
    internal Request Request { get; }

    /// <summary>The tap over the request body, at body level only.</summary>
    internal LoggingRequestBody? Tap { get; }

    /// <summary>The call's logging options; <see langword="null"/> when logging is off for the call.</summary>
    internal HttpLoggingOptions? Options { get; }

    /// <summary>The redactor and header renderer built from <see cref="Options"/>.</summary>
    internal RedactionCache.Entry? Entry { get; }

    /// <summary>Whether the <c>Information</c> events are enabled for this attempt (consulted once, OBS-1).</summary>
    internal bool Enabled { get; }
}

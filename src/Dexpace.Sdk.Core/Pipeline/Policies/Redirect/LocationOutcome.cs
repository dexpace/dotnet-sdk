// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>How a response's <c>Location</c> header read (REDIR-18, REDIR-19).</summary>
internal enum LocationOutcome
{
    /// <summary>A single, dispatchable http(s) target was resolved.</summary>
    Resolved = 0,

    /// <summary>The header is missing, empty or all whitespace: not malformed, just absent (REDIR-19).</summary>
    Absent = 1,

    /// <summary>The header is present but unusable: unparseable, several values, a non-http(s) scheme or an empty host (REDIR-18).</summary>
    Malformed = 2,
}

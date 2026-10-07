// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>How <see cref="ClientIdentityStep"/> composes its token line into the target header (RECOV-33).</summary>
public enum ClientIdentityMode
{
    /// <summary>Compose the line after the first existing value and keep every other value (the default).</summary>
    Append = 0,

    /// <summary>Overwrite every existing value with the line.</summary>
    Replace = 1,
}

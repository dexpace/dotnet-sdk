// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>What the <see cref="LoopbackServer"/> does with the connection when it reaches a scripted reply.</summary>
internal enum ReplyMode
{
    /// <summary>Write the reply's bytes (and chunks), then close or keep the connection as the reply says.</summary>
    Normal,

    /// <summary>Read the request, write nothing, and reset the connection.</summary>
    Abort,

    /// <summary>Read the request, write nothing, and keep the connection open until the client leaves or the server is disposed.</summary>
    Hang,
}

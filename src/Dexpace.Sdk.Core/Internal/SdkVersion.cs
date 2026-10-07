// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// A forwarder to <see cref="BuildInfo.SdkVersion"/>, kept so the diagnostics' <c>ActivitySource</c> and <c>Meter</c>
/// versions and the default <c>User-Agent</c> read the same value (CFG-36, P5a-22).
/// </summary>
internal static class SdkVersion
{
    /// <summary>The Core assembly version without build metadata, or <c>unknown</c>.</summary>
    internal static string Value => BuildInfo.SdkVersion;
}

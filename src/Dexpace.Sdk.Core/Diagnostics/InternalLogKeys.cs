// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>Log-state keys that are internal detail of one event and so not part of the published vocabulary (plan reading R9).</summary>
internal static class InternalLogKeys
{
    /// <summary>The type name of the resource whose disposal failed, on <c>dexpace.dispose.suppressed</c>.</summary>
    internal const string DisposeResourceType = "dexpace.dispose.resource_type";
}

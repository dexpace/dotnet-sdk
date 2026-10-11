// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// Disposes a native object on a path where a failure is already in flight (<c>TRANSPORT-22</c>, design §3.7; phase 8b, P8b-17):
/// the failure that disposal hits is attached to the primary's <see cref="ExceptionTrail"/> and never replaces it. A local
/// equivalent of core's internal <c>Disposal</c>, over the public trail and fatal-exception APIs, so core makes nothing public for it.
/// </summary>
internal static class NativeDisposal
{
    /// <summary>Disposes <paramref name="resource"/>; a non-fatal failure is attached to <paramref name="primary"/> and swallowed.</summary>
    /// <param name="resource">The native response or message to release.</param>
    /// <param name="primary">The exception already in flight, which is never replaced.</param>
    /// <exception cref="Exception">A fatal exception (<see cref="ExceptionFacts.IsFatal"/>) from the dispose propagates.</exception>
    internal static void DisposeQuietly(IDisposable resource, Exception primary)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(primary);
        try
        {
            resource.Dispose();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            ExceptionTrail.AddSuppressed(primary, ex);
        }
    }
}

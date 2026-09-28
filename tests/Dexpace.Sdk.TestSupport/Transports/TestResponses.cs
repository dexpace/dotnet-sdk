// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>Factories for the canned <see cref="Response"/>s that scripts are built from.</summary>
public static class TestResponses
{
    /// <summary>A body-less redirect with the given status code and <c>Location</c> header.</summary>
    /// <param name="statusCode">The 3xx status code.</param>
    /// <param name="location">The <c>Location</c> header value, absolute or relative.</param>
    public static Response Redirect(int statusCode, string location) =>
        new(Status.FromCode(statusCode), new Headers.Builder().Set("Location", location).Build());
}

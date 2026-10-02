// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>Factories for the canned <see cref="Response"/>s that scripts are built from.</summary>
public static class TestResponses
{
    /// <summary>
    /// A response with the given status. A test that does not care which request produced it passes none and gets a
    /// <c>GET https://example.test/</c>; a test that does care, and every <c>Security</c> test, passes the real request.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="request">The request that produced the response, or <see langword="null"/> for the default.</param>
    /// <param name="headers">The response headers, or <see langword="null"/> for none.</param>
    /// <param name="body">The response body, or <see langword="null"/> for an empty buffered body.</param>
    /// <param name="protocol">The negotiated protocol.</param>
    /// <param name="reasonPhrase">The reason phrase, or <see langword="null"/>.</param>
    public static Response Create(
        Status status,
        Request? request = null,
        Headers? headers = null,
        ResponseBody? body = null,
        Protocol protocol = Protocol.Http11,
        string? reasonPhrase = null) =>
        new(request ?? Request.Get("https://example.test/"), status, protocol, headers, body, reasonPhrase);

    /// <summary>A body-less redirect with the given status code and <c>Location</c> header.</summary>
    /// <param name="statusCode">The 3xx status code.</param>
    /// <param name="location">The <c>Location</c> header value, absolute or relative.</param>
    /// <param name="request">The request that produced the redirect, or <see langword="null"/> for the default.</param>
    public static Response Redirect(int statusCode, string location, Request? request = null) =>
        Create(Status.FromCode(statusCode), request, new Headers.Builder().Set("Location", location).Build());
}

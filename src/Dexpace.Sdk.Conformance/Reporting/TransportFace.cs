// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The shape in which a transport is driven: the asynchronous <c>IAsyncHttpClient</c> face or the blocking
/// <c>IHttpClient</c> face. The suite runs an assertion once per face it declares and the subject supplies (P8a-10).
/// </summary>
public enum TransportFace
{
    /// <summary>The asynchronous face: <c>IAsyncHttpClient.ExecuteAsync</c>.</summary>
    Async = 0,

    /// <summary>The blocking face: <c>IHttpClient.Execute</c>, called on a dedicated thread.</summary>
    Blocking = 1,
}

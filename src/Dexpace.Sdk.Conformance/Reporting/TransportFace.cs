// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The shape in which a subject is driven: the asynchronous <c>IAsyncHttpClient</c> face, the blocking <c>IHttpClient</c>
/// face, or no call surface at all. The suite runs an assertion once per face it declares and the subject supplies
/// (P8a-10). The name is the transport suite's; the invariant, codec and packaging suites reuse it (phase 10, P10-7).
/// </summary>
public enum TransportFace
{
    /// <summary>The asynchronous face: <c>IAsyncHttpClient.ExecuteAsync</c>.</summary>
    Async = 0,

    /// <summary>The blocking face: <c>IHttpClient.Execute</c>, called on a dedicated thread.</summary>
    Blocking = 1,

    /// <summary>
    /// No call surface: the assertion judges a value (a model, a classifier, a bounded map, a package) rather than driving a
    /// request, so it runs once and is supplied by definition. Added by phase 10 (P10-7); an exhaustive <c>switch</c> over the
    /// enum in a third-party driver must handle it. The transport suite rejects it.
    /// </summary>
    None = 2,
}

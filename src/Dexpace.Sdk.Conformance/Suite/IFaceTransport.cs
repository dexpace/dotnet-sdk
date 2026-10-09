// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The one send primitive of the suite (P8a-10): an assertion is written once against this and runs on each face the
/// subject supplies, so no assertion is written twice. Disposing it disposes the transport it wraps, the way a caller
/// would.
/// </summary>
internal interface IFaceTransport : IAsyncDisposable
{
    /// <summary>The face this primitive drives.</summary>
    TransportFace Face { get; }

    /// <summary>
    /// Sends <paramref name="request"/> through the transport's own member for the face: <c>ExecuteAsync</c> on the async
    /// face, <c>Execute</c> on a dedicated thread on the blocking face. A <see langword="null"/> response is a
    /// <see cref="ConformanceException"/> (<c>TRANSPORT-23</c>), whatever the face.
    /// </summary>
    Task<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}

/// <summary>The option-less send on any face.</summary>
internal static class FaceTransportExtensions
{
    /// <summary>Sends <paramref name="transport"/>'s <paramref name="request"/> with <see cref="RequestOptions.Empty"/>.</summary>
    /// <param name="transport">The face to send through.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The call's token.</param>
    internal static Task<Response> SendAsync(this IFaceTransport transport, Request request, CancellationToken cancellationToken) =>
        transport.SendAsync(request, RequestOptions.Empty, cancellationToken);

    /// <summary>
    /// Sends <paramref name="request"/> where the clause requires a response: a transport failure is a
    /// <see cref="ConformanceException"/> naming <paramref name="what"/>, not an error of the kit (see <see cref="Check.GuardAsync{T}"/>).
    /// </summary>
    /// <param name="transport">The face to send through.</param>
    /// <param name="request">The request.</param>
    /// <param name="what">What is being sent, for the failure message.</param>
    /// <param name="cancellationToken">The assertion's token.</param>
    internal static Task<Response> ExpectResponseAsync(this IFaceTransport transport, Request request, string what, CancellationToken cancellationToken) =>
        Check.GuardAsync(() => transport.SendAsync(request, RequestOptions.Empty, cancellationToken), what, cancellationToken);

    /// <summary>Reads the whole body of <paramref name="response"/>, a transport failure being a <see cref="ConformanceException"/> naming <paramref name="what"/>.</summary>
    /// <param name="response">The response whose body to read; the body is disposed by the read.</param>
    /// <param name="what">What is being read, for the failure message.</param>
    /// <param name="cancellationToken">The assertion's token.</param>
    internal static Task<byte[]> ReadBodyAsync(this Response response, string what, CancellationToken cancellationToken) =>
        Check.GuardAsync(() => response.Body.ReadAsBytesAsync(cancellationToken), what, cancellationToken);
}

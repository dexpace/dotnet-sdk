// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// The response step that maps a 400 to 599 status to an <see cref="HttpResponseException"/> over a buffered copy of the
/// error body (RECOV-15, RECOV-16).
/// </summary>
/// <remarks>
/// Every other status is returned by reference with its body untouched: never opened, read or disposed. For an error the
/// body is drained up to <see cref="Response.MaxBufferedErrorBytes"/> by the one error-body capture in core, and the
/// original response is disposed. The step holds no state; use <see cref="Instance"/>.
/// </remarks>
public sealed class ErrorMappingStep : IResponseStep
{
    private ErrorMappingStep()
    {
    }

    /// <summary>The shared, stateless instance.</summary>
    public static ErrorMappingStep Instance { get; } = new();

    /// <inheritdoc />
    /// <exception cref="HttpResponseException">The status is in the 400 to 599 range.</exception>
    public Response Apply(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!IsError(response))
        {
            return response;
        }

        throw ErrorMapping.ToException(ErrorBodyBuffer.Capture(response, cancellationToken));
    }

    /// <inheritdoc />
    /// <exception cref="HttpResponseException">The status is in the 400 to 599 range.</exception>
    public async ValueTask<Response> ApplyAsync(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!IsError(response))
        {
            return response;
        }

        throw ErrorMapping.ToException(
            await ErrorBodyBuffer.CaptureAsync(response, cancellationToken).ConfigureAwait(false));
    }

    private static bool IsError(Response response) => response.Status.IsClientError || response.Status.IsServerError;
}

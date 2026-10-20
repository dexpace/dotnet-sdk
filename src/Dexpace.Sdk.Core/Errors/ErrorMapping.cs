// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The one place core turns a buffered error response into an <see cref="HttpResponseException"/> (XCUT-8).
/// </summary>
/// <remarks>
/// A status outside 400..599 is an <see cref="ArgumentException"/>, so no mapping path (<c>ErrorMappingStep</c>,
/// <c>ErrorMappingPolicy</c>, both <c>EnsureSuccess</c> forms) can raise an HTTP error for a response that is not one. Since
/// phase 11 (P11-9) the public <see cref="HttpResponseException"/> constructor carries the same guard, so this is no longer the
/// only line of defence; it stays the one internal entry point the mapping paths share.
/// </remarks>
internal static class ErrorMapping
{
    internal static HttpResponseException ToException(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Status.IsError)
        {
            throw new ArgumentException(
                $"The status {response.Status.Code} is not an error status (400..599); only an error response maps to an HttpResponseException.",
                nameof(response));
        }

        return new HttpResponseException(response);
    }
}

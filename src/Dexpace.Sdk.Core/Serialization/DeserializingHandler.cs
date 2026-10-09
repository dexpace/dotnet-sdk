// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// <see cref="ResponseHandlers.Deserialize{T}"/>: the typed reader plus the dispose of the response it owns (SERDE-27, P7a-17).
/// </summary>
/// <remarks>
/// SERDE-3 (the codec leaves the caller's stream open) and this rule (the handler closes the response it owns) bind different
/// objects at different layers. The reader below already closes the body and the stream; this adds the response, with the
/// primary failure attached to a dispose failure rather than replaced by it.
/// </remarks>
internal sealed class DeserializingHandler<T>(ISerde serde) : IResponseHandler<T>
{
    public async ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        Exception? primary = null;
        try
        {
            return await response.Body.ReadValueAsync<T>(serde, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            primary = ex;
            throw;
        }
        finally
        {
            await Disposal.DisposeQuietlyAsync(response, primary).ConfigureAwait(false);
        }
    }
}

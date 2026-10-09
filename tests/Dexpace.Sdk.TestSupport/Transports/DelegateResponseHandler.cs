// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>An <see cref="IResponseHandler{T}"/> that runs a delegate, for tests that script a handler's body inline.</summary>
/// <typeparam name="T">The handled value type.</typeparam>
/// <param name="handle">What <see cref="HandleAsync"/> runs.</param>
public sealed class DelegateResponseHandler<T>(Func<Response, CancellationToken, ValueTask<T>> handle) : IResponseHandler<T>
{
    /// <inheritdoc />
    public ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken) =>
        handle(response, cancellationToken);
}

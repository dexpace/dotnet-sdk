// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>A response handler a typed response is built with and never asked to run: the assertions that use it only close the response.</summary>
internal sealed class UnusedHandler : IResponseHandler<int>
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(Response response, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}

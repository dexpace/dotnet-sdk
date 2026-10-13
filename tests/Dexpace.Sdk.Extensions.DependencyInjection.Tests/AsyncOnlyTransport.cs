// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>A transport that is <c>IAsyncDisposable</c> only, as <c>IAsyncHttpClient</c> allows: a synchronous provider dispose throws for it (design G, F21).</summary>
internal sealed class AsyncOnlyTransport : IAsyncHttpClient
{
    internal int Disposals { get; private set; }

    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}

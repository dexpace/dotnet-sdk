// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The fixed end of a pipeline: the transport, in both its forms, and the context-chain promotion around each
/// transmission (design §5.4, P4c-21).
/// </summary>
/// <remarks>
/// The synchronous transport is decided once, at build time: the transport itself when it implements
/// <see cref="IHttpClient"/>, otherwise the documented <see cref="HttpClientExtensions.AsBlocking"/> bridge (P4c-13).
/// The terminal never owns or disposes the transport.
/// </remarks>
internal sealed class PipelineTerminal
{
    private readonly IAsyncHttpClient _async;
    private readonly IHttpClient _sync;

    internal PipelineTerminal(IAsyncHttpClient transport)
    {
        _async = transport;
        _sync = transport as IHttpClient ?? transport.AsBlocking();
    }

    internal IAsyncHttpClient Transport => _async;

    internal async ValueTask<Response> ExecuteAsync(Request request, PipelineContext context)
    {
        var link = Promote(request, context);
        var response = await _async
            .ExecuteAsync(request, context.RequestOptions, context.CancellationToken)
            .ConfigureAwait(false);
        return Exchange(link, response, context);
    }

    internal Response Execute(Request request, PipelineContext context)
    {
        var link = Promote(request, context);
        var response = _sync.Execute(request, context.RequestOptions, context.CancellationToken);
        return Exchange(link, response, context);
    }

    // Each transmission promotes the same dispatch again with the request actually sent; the later link takes the slot
    // (CTX-2, CTX-16, CTX-17). The pipeline never calls the store: promotion registers.
    private static RequestContext Promote(Request request, PipelineContext context)
    {
        var link = context.State.Dispatch.PromoteToRequest(request);
        context.State.Reached(link);
        return link;
    }

    private static Response Exchange(RequestContext link, Response? response, PipelineContext context)
    {
        // SEAM-16: nullability is compile-time only, so assert the transport's result before any policy sees it.
        if (response is null)
        {
            throw new PipelineAbortedException("The transport returned no response (SEAM-16).");
        }

        var exchange = link.PromoteToExchange(response);
        context.State.Reached(exchange);
        response.AttachExchange(exchange);
        return response;
    }
}

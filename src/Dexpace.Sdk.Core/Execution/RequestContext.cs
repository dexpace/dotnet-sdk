// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The second stage of a call: the request actually sent and the advisory operation name (CTX-1, CTX-2, CTX-16).
/// </summary>
public sealed record RequestContext : CallContext
{
    /// <summary>Creates an off-chain request context bound to the process-wide store; nothing is registered (CTX-17).</summary>
    /// <param name="request">The request.</param>
    /// <param name="operationName">An advisory operation name: <see langword="null"/> or non-blank.</param>
    /// <param name="instrumentation">The bundle; <see langword="null"/> means <see cref="InstrumentationContext.None"/>.</param>
    /// <param name="key">An explicit key to pin, or <see langword="null"/> to mint one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is blank or <paramref name="key"/> is <c>default</c>.</exception>
    public RequestContext(
        Request request,
        string? operationName = null,
        InstrumentationContext? instrumentation = null,
        CallKey? key = null)
        : this(request, operationName, instrumentation, key, ContextStore.Shared)
    {
    }

    internal RequestContext(
        Request request,
        string? operationName,
        InstrumentationContext? instrumentation,
        CallKey? key,
        ContextStore store)
        : base(instrumentation, key, store)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
        OperationName = OperationNames.Validate(operationName);
    }

    /// <summary>The request actually sent.</summary>
    public Request Request { get; }

    /// <summary>The advisory operation name, or <see langword="null"/>; it never affects the key.</summary>
    public string? OperationName { get; }

    /// <inheritdoc />
    internal override string StageName => "Request";

    /// <summary>Promotes to the exchange stage, carrying the key, bundle, request and name, and registers the link (CTX-2, CTX-17).</summary>
    /// <param name="response">The response received; this context never disposes it.</param>
    /// <returns>The new link.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    public ExchangeContext PromoteToExchange(Response response)
    {
        var next = new ExchangeContext(Request, response, OperationName, Instrumentation, Key, Store);
        Store.Set(next);
        return next;
    }

    /// <inheritdoc />
    internal override void AppendDetail(StringBuilder builder) =>
        builder.Append(' ').Append(Request).Append(OperationNames.Render(OperationName));
}

/// <summary>The operation-name rule shared by the request and exchange stages (CTX-16).</summary>
internal static class OperationNames
{
    /// <summary>Returns <paramref name="operationName"/> when it is null or non-blank, else throws.</summary>
    /// <param name="operationName">The name.</param>
    /// <returns>The same name.</returns>
    /// <exception cref="ArgumentException">The name is empty or whitespace.</exception>
    internal static string? Validate(string? operationName)
    {
        if (operationName is not null && string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("The operation name must be null or non-blank.", nameof(operationName));
        }

        return operationName;
    }

    /// <summary>Renders the name for <c>ToString</c>.</summary>
    /// <param name="operationName">The name.</param>
    /// <returns>An empty string or <c> op=name</c>.</returns>
    internal static string Render(string? operationName) => operationName is null ? string.Empty : " op=" + operationName;
}

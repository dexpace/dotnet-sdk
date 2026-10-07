// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The final stage of a call: the request, the response and the advisory operation name (CTX-1, CTX-2).
/// </summary>
/// <remarks>It declares no promotion member: the chain is one-way (CTX-1).</remarks>
public sealed record ExchangeContext : CallContext
{
    /// <summary>Creates an off-chain exchange context bound to the process-wide store; nothing is registered (CTX-17).</summary>
    /// <param name="request">The request.</param>
    /// <param name="response">The response; this context never disposes it.</param>
    /// <param name="operationName">An advisory operation name: <see langword="null"/> or non-blank.</param>
    /// <param name="instrumentation">The bundle; <see langword="null"/> means <see cref="InstrumentationContext.None"/>.</param>
    /// <param name="key">An explicit key to pin, or <see langword="null"/> to mint one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="response"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is blank or <paramref name="key"/> is <c>default</c>.</exception>
    public ExchangeContext(
        Request request,
        Response response,
        string? operationName = null,
        InstrumentationContext? instrumentation = null,
        CallKey? key = null)
        : this(request, response, operationName, instrumentation, key, ContextStore.Shared)
    {
    }

    internal ExchangeContext(
        Request request,
        Response response,
        string? operationName,
        InstrumentationContext? instrumentation,
        CallKey? key,
        ContextStore store)
        : base(instrumentation, key, store)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        Request = request;
        Response = response;
        OperationName = OperationNames.Validate(operationName);
    }

    /// <summary>The request actually sent.</summary>
    public Request Request { get; }

    /// <summary>The response received.</summary>
    public Response Response { get; }

    /// <summary>The advisory operation name, or <see langword="null"/>.</summary>
    public string? OperationName { get; }

    /// <inheritdoc />
    internal override string StageName => "Exchange";

    /// <inheritdoc />
    internal override void AppendDetail(StringBuilder builder) =>
        builder.Append(' ').Append(Request).Append(OperationNames.Render(OperationName)).Append(" status=").Append(Response.Status.Code);
}

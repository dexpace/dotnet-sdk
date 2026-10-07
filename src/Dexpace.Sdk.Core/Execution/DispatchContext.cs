// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The first stage of a call: the key and the correlation bundle, before any request exists (CTX-1, CTX-2, CTX-5).
/// </summary>
/// <remarks>Constructing a dispatch context registers nothing; <see cref="PromoteToRequest"/> does (CTX-17).</remarks>
public sealed record DispatchContext : CallContext
{
    /// <summary>Creates a dispatch context bound to the process-wide store.</summary>
    /// <param name="instrumentation">The bundle; <see langword="null"/> means <see cref="InstrumentationContext.None"/>.</param>
    /// <param name="key">An explicit key to pin, or <see langword="null"/> to mint one.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>default(CallKey)</c>.</exception>
    public DispatchContext(InstrumentationContext? instrumentation = null, CallKey? key = null)
        : base(instrumentation, key, ContextStore.Shared)
    {
    }

    internal DispatchContext(InstrumentationContext? instrumentation, CallKey? key, ContextStore store)
        : base(instrumentation, key, store)
    {
    }

    /// <inheritdoc />
    internal override string StageName => "Dispatch";

    /// <summary>Promotes to the request stage, carrying the key and bundle, and registers the new link (CTX-2, CTX-17).</summary>
    /// <param name="request">The request actually sent.</param>
    /// <param name="operationName">An advisory operation name: <see langword="null"/> or non-blank (CTX-16).</param>
    /// <returns>The new link.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is blank.</exception>
    public RequestContext PromoteToRequest(Request request, string? operationName = null)
    {
        var next = new RequestContext(request, operationName, Instrumentation, Key, Store);
        Store.Set(next);
        return next;
    }

    /// <inheritdoc />
    internal override void AppendDetail(StringBuilder builder)
    {
    }
}

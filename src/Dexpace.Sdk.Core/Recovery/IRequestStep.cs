// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// A step that transforms the request before it is sent (RECOV-3).
/// </summary>
/// <remarks>
/// <para>
/// A step receives only the value and a <see cref="CancellationToken"/>: no context (design P4b-6), so per-request state
/// lives in the value being transformed. Both forms are required and must agree; the chain calls <see cref="Apply"/> on
/// its synchronous path and <see cref="ApplyAsync"/> on its asynchronous one.
/// </para>
/// <para>
/// <b>Concurrency (RECOV-14):</b> one instance may be applied concurrently by many calls. Keep per-call state in the
/// value, never in a field.
/// </para>
/// </remarks>
public interface IRequestStep
{
    /// <summary>Transforms <paramref name="request"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The request to pass on; never <see langword="null"/>. A throw aborts the rest of the chain.</returns>
    Request Apply(Request request, CancellationToken cancellationToken);

    /// <summary>Transforms <paramref name="request"/> asynchronously.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The request to pass on; never <see langword="null"/>.</returns>
    ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken);
}

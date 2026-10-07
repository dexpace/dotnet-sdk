// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// A step that transforms a received response, or throws to turn it into a failure (RECOV-4, RECOV-7).
/// </summary>
/// <remarks>
/// <para>
/// The response phase runs only while the outcome is a success. A step that throws is converted to a
/// <see cref="Outcome.Failure"/>, the remaining response steps are skipped, and the response in hand is released
/// (RECOV-12). A step that returns a different response owns the one it dropped.
/// </para>
/// <para>
/// <b>Concurrency (RECOV-14):</b> one instance may be applied concurrently by many calls. Keep per-call state in the
/// value, never in a field.
/// </para>
/// </remarks>
public interface IResponseStep
{
    /// <summary>Transforms <paramref name="response"/>.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The response to pass on; never <see langword="null"/>.</returns>
    Response Apply(Response response, CancellationToken cancellationToken);

    /// <summary>Transforms <paramref name="response"/> asynchronously.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">A token the step observes.</param>
    /// <returns>The response to pass on; never <see langword="null"/>.</returns>
    ValueTask<Response> ApplyAsync(Response response, CancellationToken cancellationToken);
}

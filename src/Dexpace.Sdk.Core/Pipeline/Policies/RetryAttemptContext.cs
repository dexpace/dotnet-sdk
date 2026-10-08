// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// What a <see cref="RetryPolicy"/> hook is told about the send that just finished (RETRY-29, RETRY-39, RETRY-40).
/// </summary>
/// <remarks>
/// <b>Do not format this value:</b> <see cref="Response"/> is the live response, and the synthesised <c>ToString</c> of a
/// record struct prints it. A hook must not dispose it, read its body, or keep it past the call: the policy reads its
/// pacing headers and releases it after the hooks return.
/// </remarks>
/// <param name="Attempt">
/// The 1-based number of sends performed so far, which is also the retry ordinal about to be scheduled (the first failed
/// send is <c>1</c>).
/// </param>
/// <param name="Request">The request the policy received; every send re-sends it.</param>
/// <param name="Response">The live response of the send, or <see langword="null"/> when it threw.</param>
/// <param name="Failure">The exception the send threw, or <see langword="null"/> when it returned a response.</param>
/// <param name="Context">The call's pipeline context.</param>
public readonly record struct RetryAttemptContext(
    int Attempt,
    Request Request,
    Response? Response,
    Exception? Failure,
    PipelineContext Context);

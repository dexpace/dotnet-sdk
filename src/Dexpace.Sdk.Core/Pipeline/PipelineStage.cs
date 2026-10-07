// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// Identifies where in the pipeline chain a policy is inserted.
/// Policies execute in ascending numeric order (outermost first on the way in;
/// innermost first on the way out).
/// </summary>
/// <remarks>
/// <para>
/// Numbers are sparse (PIPE-3) to leave room for future stages without breaking existing values.
/// </para>
/// <para>
/// <b>Pillar stages</b> — <see cref="Operation"/>, <see cref="Redirect"/>, <see cref="Retry"/>,
/// <see cref="Auth"/>, <see cref="Diagnostics"/> and <see cref="Serde"/> — admit exactly one policy each.
/// Adding a second, distinct policy to a pillar stage throws at <c>Add</c> time (PIPE-5).
/// </para>
/// <para>
/// <b>Non-pillar stages</b> — <see cref="PerCall"/>, <see cref="PerHop"/> and <see cref="PerAttempt"/> — may hold
/// multiple policies, which execute in the order they were registered.
/// </para>
/// <para>
/// There is no user slot after <see cref="Auth"/>, <see cref="Diagnostics"/> or <see cref="Serde"/>; because the keys
/// are sparse, adding one later is additive (PIPE-3).
/// </para>
/// </remarks>
public enum PipelineStage
{
    /// <summary>
    /// Outermost stage. Runs once per logical operation and applies the overall deadline. The pipeline itself opens the
    /// operation span around this stage (phase 5c, P5c-2), so the span exists for every pipeline shape and encloses the
    /// stage's policy. Pillar: at most one policy.
    /// </summary>
    Operation = 100,

    /// <summary>
    /// Per-call stage (non-pillar). Policies here run once per logical call, outside both the redirect and the retry
    /// loop, and see only the terminal response — suitable for idempotency keys, client identity headers and error
    /// mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking:</b> was 250, inside the redirect loop; now 150, outside both loops, one invocation per call. Source
    /// that says <c>PerCall</c> keeps compiling and changes loop. The old slot is <see cref="PerHop"/>.
    /// </para>
    /// </remarks>
    PerCall = 150,

    /// <summary>
    /// Redirect-following stage. Runs outside the retry loop so each hop triggers a full retry
    /// sequence. Pillar: at most one policy.
    /// </summary>
    Redirect = 200,

    /// <summary>
    /// Per-hop stage (non-pillar). Policies here run once per redirect hop, inside the redirect loop and outside the
    /// retry loop; it is both the specification's post-redirect and pre-retry slot, adjacent with nothing between.
    /// </summary>
    PerHop = 250,

    /// <summary>
    /// Retry stage. Wraps everything below it so that each retry attempt re-executes all
    /// inner stages. Pillar: at most one policy.
    /// </summary>
    Retry = 300,

    /// <summary>
    /// Per-attempt stage (non-pillar). Policies here run on every attempt inside the retry
    /// loop — suitable for per-attempt concerns such as a fresh <c>Date</c> header.
    /// </summary>
    PerAttempt = 400,

    /// <summary>
    /// Auth stage. Placed inside the retry loop so a token refresh applies to the next
    /// retry attempt. Pillar: at most one policy.
    /// </summary>
    Auth = 500,

    /// <summary>
    /// Diagnostics stage. Closest to the transport wire; wraps the per-attempt span,
    /// metrics, and structured log events. Pillar: at most one policy.
    /// </summary>
    Diagnostics = 600,

    /// <summary>
    /// Serialization stage. Reserved and ships with no policy; a custom policy may occupy it. Pillar: at most one
    /// policy.
    /// </summary>
    Serde = 700,
}

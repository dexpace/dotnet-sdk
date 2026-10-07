// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// A policy and the stage it reported once, at insertion. The builder never reads <see cref="HttpPipelinePolicy.Stage"/>
/// again, so a policy whose stage varies cannot pass validation in one slot and run in another (PIPE-22, P4c-8).
/// </summary>
/// <param name="Policy">The policy.</param>
/// <param name="Stage">The stage the policy reported at insertion.</param>
internal readonly record struct PipelineEntry(HttpPipelinePolicy Policy, PipelineStage Stage);

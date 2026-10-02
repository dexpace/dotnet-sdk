// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>One call to a fake transport: the exact request, options and token instances it received.</summary>
/// <param name="Request">The request.</param>
/// <param name="Options">The per-call options.</param>
/// <param name="CancellationToken">The call's cancellation token.</param>
public sealed record RecordedCall(Request Request, RequestOptions Options, CancellationToken CancellationToken);

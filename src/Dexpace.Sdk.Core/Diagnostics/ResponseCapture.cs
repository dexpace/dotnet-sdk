// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>The outcome of capturing a response body preview at body level (OBS-36); <c>default</c> when nothing was captured.</summary>
/// <param name="Wrapper">The wrapper that captured, whose <c>DrainFailure</c> reports a failed drain.</param>
/// <param name="Bytes">The captured bytes, at most the preview size.</param>
internal readonly record struct ResponseCapture(LoggingResponseBody? Wrapper, byte[]? Bytes);

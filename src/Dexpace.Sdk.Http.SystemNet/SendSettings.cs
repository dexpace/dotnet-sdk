// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// What <see cref="SystemNetHttpClient"/> resolved from its <see cref="SystemNetHttpClientOptions"/> and its construction when it
/// was built: an immutable snapshot, so changing the options afterwards changes nothing.
/// </summary>
/// <param name="Logger">Receives the header-drop entries and the construction warnings.</param>
/// <param name="Timeout">The deadline for a call that carries none of its own, or <see langword="null"/> for none (<c>TRANSPORT-5</c>).</param>
/// <param name="HeaderDropLogging">How a dropped request header is logged (<c>TRANSPORT-13</c>).</param>
/// <param name="ResendBufferLimit">The most bytes of a single-use body kept for a native re-send (<c>TRANSPORT-18</c>).</param>
/// <param name="TimeProvider">The clock that arms the per-call deadline.</param>
/// <param name="ResendCapable">Whether the native layer can send a request again behind the transport's back, so a single-use body is captured.</param>
internal sealed record SendSettings(
    ILogger Logger,
    TimeSpan? Timeout,
    HeaderDropLogging HeaderDropLogging,
    long ResendBufferLimit,
    TimeProvider TimeProvider,
    bool ResendCapable);

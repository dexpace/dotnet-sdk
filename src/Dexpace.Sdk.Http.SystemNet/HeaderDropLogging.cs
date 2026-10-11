// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// How loudly <see cref="SystemNetHttpClient"/> logs a request header it drops because the native client cannot send it
/// (<c>TRANSPORT-13</c>, <c>OBS-19</c>). A drop is never silent: every mode logs it, the modes differ in level. The header
/// <em>name</em> is logged, never its value (<c>XCUT-18</c>).
/// </summary>
/// <remarks>
/// <para>
/// The policy governs the drop of a header the native client refuses or that is not valid on the wire. A framing header the
/// transport computes itself (<c>TRANSPORT-11</c>) and a response header dropped on the inbound path (<c>TRANSPORT-14</c>)
/// are always logged at <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/>, whatever the mode.
/// </para>
/// </remarks>
public enum HeaderDropLogging
{
    /// <summary>
    /// The first drop of each header name is a <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>, every later drop of
    /// that name a <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> (names compare ignoring case). The default.
    /// </summary>
    /// <remarks>
    /// The set of names already seen belongs to the transport instance, holds at most 128 names and evicts the oldest first, so a
    /// name evicted and dropped again warns again. It is pure logging state: it never changes a request or a response.
    /// </remarks>
    FirstPerName = 0,

    /// <summary>Every drop is a <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>.</summary>
    Every = 1,

    /// <summary>Every drop is a <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/>: visible at verbose levels only, never invisible.</summary>
    VerboseOnly = 2,
}

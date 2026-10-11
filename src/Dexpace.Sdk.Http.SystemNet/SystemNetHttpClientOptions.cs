// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The settings of a <see cref="SystemNetHttpClient"/> that no <c>HttpClient</c> or handler carries: its logger, proxy, default
/// timeout, header-drop policy, re-send capture limit and clock (phase 8b, <c>TRANSPORT-5</c>, <c>-13</c>, <c>-18</c>,
/// <c>-30</c>).
/// </summary>
/// <remarks>
/// <para>
/// A sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with <see langword="with"/> (CFG-9).
/// Every member validates where it is set, so an invalid value cannot be configured and a <see langword="with"/> expression that
/// sets one throws. Equality is by value; <see cref="Proxy"/> compares as <see cref="ProxyOptions"/> does, and
/// <see cref="object.ToString"/> prints it with its credentials masked (<c>TRANSPORT-30</c>).
/// </para>
/// <para>
/// The transport reads this record once, when it is constructed: changing a copy afterwards affects nothing.
/// </para>
/// </remarks>
public sealed record SystemNetHttpClientOptions
{
    // A timer rejects a due time above uint.MaxValue - 1 ms (about 49.7 days); 49 days is the limit DexpaceClientOptions
    // accepts for its timeouts too (P6a-24), so the two ranges agree.
    private static readonly TimeSpan s_maxTimeout = TimeSpan.FromDays(49);

    /// <summary>
    /// Receives an entry per dropped header, naming it and never its value, and the construction-time proxy warnings
    /// (<c>TRANSPORT-12</c>, <c>-13</c>, <c>-30</c>, <c>OBS-19</c>). Defaults to <see cref="NullLogger.Instance"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public ILogger Logger
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = NullLogger.Instance;

    /// <summary>
    /// The proxy the transport installs on the handler it creates (<c>TRANSPORT-30</c>), or <see langword="null"/> for a direct
    /// connection. Defaults to <see langword="null"/>: the transport never reads <c>HTTP_PROXY</c>, <c>HTTPS_PROXY</c> or
    /// <c>NO_PROXY</c> unless the caller resolves them (<c>CFG-28</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking:</b> <c>new SystemNetHttpClient()</c> used to inherit the runtime's process-wide proxy, which reads those
    /// variables on first use. To keep the environment, resolve it explicitly:
    /// <c>new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() }</c>.
    /// </para>
    /// <para>
    /// The proxy applies to the transport's <em>own</em> handler only. A transport built over a caller's <c>HttpClient</c> or
    /// handler leaves its proxy to its owner (<c>TRANSPORT-15</c>), and a non-<see langword="null"/> value passed with either
    /// throws <see cref="ArgumentException"/> at construction.
    /// </para>
    /// </remarks>
    public ProxyOptions? Proxy { get; init; }

    /// <summary>
    /// The deadline applied to a call that carries no <c>RequestOptions.Timeout</c> (<c>TRANSPORT-5</c>), bounding the exchange
    /// until the response headers arrive; <see langword="null"/> means no transport default. Defaults to 100 seconds, the
    /// <c>HttpClient</c> default it replaces. At most 49 days.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not <see langword="null"/> and is outside <c>(0, 49 days]</c>.</exception>
    public TimeSpan? Timeout
    {
        get;
        init
        {
            if (value is { } timeout && (timeout <= TimeSpan.Zero || timeout > s_maxTimeout))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A timeout must be greater than zero and at most 49 days; use null for no transport default.");
            }

            field = value;
        }
    } = TimeSpan.FromSeconds(100);

    /// <summary>How a dropped request header is logged (<c>TRANSPORT-13</c>). Defaults to <see cref="HeaderDropLogging.FirstPerName"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="HeaderDropLogging"/>.</exception>
    public HeaderDropLogging HeaderDropLogging
    {
        get;
        init
        {
            field = Enum.IsDefined(value)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), "The header drop logging mode is not a defined HeaderDropLogging.");
        }
    } = HeaderDropLogging.FirstPerName;

    /// <summary>
    /// The most bytes of a single-use request body the transport keeps so a re-send by the native client can replay them
    /// (<c>TRANSPORT-18</c>), applied only where the native layer can re-send. Defaults to 1 MiB; <c>0</c> disables the capture, so
    /// every native re-send of a single-use body fails.
    /// </summary>
    /// <remarks>
    /// A body beyond the limit is still sent once; if the native layer then needs to send it again, the call fails with a
    /// <see cref="Dexpace.Sdk.Core.Errors.ServiceRequestException"/> whose cause names this limit. Raise it, or call
    /// <c>RequestBody.ToReplayableAsync()</c> first, to upload a larger single-use stream through a client that can re-send.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long ResendBufferLimit
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 1_048_576;

    /// <summary>
    /// The clock that arms the per-call deadline (<c>TRANSPORT-5</c>). Defaults to <see cref="System.TimeProvider.System"/>; a
    /// test passes a fake one.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TimeProvider.System;
}

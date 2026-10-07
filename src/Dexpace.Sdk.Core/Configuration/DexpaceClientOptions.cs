// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Diagnostics;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Top-level configuration options for the Dexpace SDK client.
/// </summary>
/// <remarks>
/// All properties carry sensible defaults; the client is fully usable with <c>new DexpaceClientOptions()</c>.
/// Per-policy sub-options are exposed as nested records (<see cref="Retry"/>, <see cref="Redirect"/>).
/// <para>
/// This is a sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with
/// <see langword="with"/>, for example <c>options with { Retry = options.Retry with { MaxRetryAttempts = 5 } }</c>,
/// which leaves the source unchanged (CFG-9). Single-property invariants are checked in the accessors, so a
/// <see langword="with"/> expression cannot bypass them; numeric and cross-property rules are not checked here
/// (P5a-4). <see cref="ToString"/> renders <see cref="BaseAddress"/> through <see cref="UrlRedactor"/>.
/// </para>
/// <para>
/// <b>Breaking:</b> this was a mutable class. Assigning a property after construction no longer compiles (use
/// <see langword="with"/>); equality and the hash code are by value (was: by reference); <see cref="BaseAddress"/> is
/// validated when set (was: at <c>BuildRequest</c>); <see cref="UserAgent"/>, <see cref="Retry"/> and
/// <see cref="Redirect"/> reject <see langword="null"/>.
/// </para>
/// </remarks>
public sealed record DexpaceClientOptions
{
    private static readonly string s_defaultUserAgent = BuildDefaultUserAgent();

    /// <summary>
    /// The base address prepended to relative request URLs, or <see langword="null"/> when
    /// requests always use absolute URLs.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="Operations.OperationDescriptor.BuildRequest(DexpaceClientOptions)"/>: it must be an absolute
    /// <c>http</c> or <c>https</c> URI with no fragment, checked when the value is set (P5a-4). No policy or
    /// transport consults it. <para><b>Breaking:</b> was checked at <c>BuildRequest</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The value is relative, not <c>http</c> or <c>https</c>, or carries a fragment; the message carries the value
    /// through <see cref="UrlRedactor"/>.
    /// </exception>
    public Uri? BaseAddress
    {
        get;
        init => field = RequireUsable(value);
    }

    /// <summary>
    /// The <c>User-Agent</c> header value sent with every request.
    /// Defaults to <c>dexpace-dotnet/&lt;sdk-version&gt; dotnet/&lt;runtime-version&gt;</c>, the <see cref="BuildInfo.IdentityTokens"/>
    /// joined (CFG-36). A blank value means "send no header".
    /// </summary>
    /// <remarks>
    /// <b>Breaking (behaviour):</b> the default was the one token <c>dexpace-dotnet/&lt;assembly-version&gt;</c>. A consuming
    /// SDK that composes its own line through <c>ClientIdentityPolicy</c> is unaffected.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string UserAgent
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = s_defaultUserAgent;

    /// <summary>
    /// The wall-clock deadline for an entire operation (all redirect hops and retry attempts
    /// combined), or <see langword="null"/> for no overall deadline.
    /// </summary>
    public TimeSpan? OverallTimeout { get; init; }

    /// <summary>
    /// The deadline for a single send attempt, or <see langword="null"/> for no per-attempt deadline.
    /// </summary>
    /// <remarks>
    /// <b>Not yet read by anything.</b> Setting it has no effect today: no policy bounds a single
    /// attempt, and only <see cref="OverallTimeout"/> is enforced. Roadmap phase 6a wires it
    /// (design §6.1).
    /// </remarks>
    public TimeSpan? AttemptTimeout { get; init; }

    /// <summary>
    /// Retry-policy options. Defaults to <see cref="RetryOptions"/> with its built-in defaults.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public RetryOptions Retry
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <summary>
    /// Redirect-policy options. Defaults to <see cref="RedirectOptions"/> with its built-in defaults.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public RedirectOptions Redirect
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <summary>
    /// How much of each request and response the SDK logs, and how it redacts what it logs. Defaults to
    /// <see cref="HttpLoggingOptions.Default"/>: logging is off (OBS-34).
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public HttpLoggingOptions Logging
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = HttpLoggingOptions.Default;

    private static string BuildDefaultUserAgent() => string.Join(' ', BuildInfo.IdentityTokens);

    private static Uri? RequireUsable(Uri? value)
    {
        if (value is null)
        {
            return null;
        }

        var usable = value.IsAbsoluteUri
            && (value.Scheme == Uri.UriSchemeHttp || value.Scheme == Uri.UriSchemeHttps)
            && value.Fragment.Length == 0;
        if (!usable)
        {
            throw new ArgumentException(
                "The base address must be an absolute http or https URI with no fragment: "
                + UrlRedactor.Default.Redact(value),
                nameof(value));
        }

        return value;
    }

    // The synthesised ToString calls this. BaseAddress goes through the redactor, never Uri.ToString() (design §3.5).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("BaseAddress = ").Append(BaseAddress is null ? "(none)" : UrlRedactor.Default.Redact(BaseAddress));
        builder.Append(", UserAgent = ").Append(UserAgent);
        builder.Append(", OverallTimeout = ").Append(OverallTimeout?.ToString() ?? "(none)");
        builder.Append(", AttemptTimeout = ").Append(AttemptTimeout?.ToString() ?? "(none)");
        builder.Append(", Retry = ").Append(Retry);
        builder.Append(", Redirect = ").Append(Redirect);
        builder.Append(", Logging = ").Append(Logging);
        return true;
    }
}

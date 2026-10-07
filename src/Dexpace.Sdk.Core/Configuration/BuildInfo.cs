// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Runtime.InteropServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// The SDK version and runtime identity, resolved once, each field falling back to <see cref="Unknown"/>, with the
/// ordered identity tokens a <c>User-Agent</c> is composed from (CFG-36; design §8.2).
/// </summary>
/// <remarks>
/// <para>
/// Each probe runs in its own non-fatal <see langword="try"/>, so one failing probe (an attribute read under trimming, a
/// runtime API that throws on an exotic host) yields <see cref="Unknown"/> for that field only. A version is kept only if
/// it is a valid RFC 9110 token (<see cref="HttpHeaderSyntax.IsValidName"/>): an informational version carrying a space or
/// a non-ASCII character would otherwise make every request's <c>User-Agent</c> invalid at the headers' outbound
/// validation. <see cref="RuntimeDescription"/> is free text and is promised non-blank only.
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> an undeterminable SDK version reads <c>unknown</c> (was <c>0.0.0</c>), which also changes
/// the <c>ActivitySource</c> and <c>Meter</c> version in that case. The runtime token is <see cref="Environment.Version"/>,
/// not <see cref="RuntimeInformation.FrameworkDescription"/>, whose value contains a space (P5a-22).
/// </para>
/// </remarks>
public static class BuildInfo
{
    /// <summary>The value of a field that could not be determined.</summary>
    public const string Unknown = "unknown";

    /// <summary>The SDK version, with any <c>+build</c> metadata stripped; <see cref="Unknown"/> when not a valid token.</summary>
    public static string SdkVersion { get; } = Probe(ReadSdkVersion);

    /// <summary>The runtime version (<see cref="Environment.Version"/>, for example <c>10.0.1</c>); <see cref="Unknown"/> on failure.</summary>
    public static string RuntimeVersion { get; } = Probe(() => ToToken(Environment.Version.ToString()));

    /// <summary>The runtime's product description (for example <c>.NET 10.0.1</c>); free text, not a token.</summary>
    public static string RuntimeDescription { get; } = Probe(() => RuntimeInformation.FrameworkDescription);

    /// <summary>The operating system name: <c>windows</c>, <c>linux</c>, <c>macos</c>, <c>freebsd</c>, or <see cref="Unknown"/>.</summary>
    public static string OSName { get; } = Probe(() => OsNameFor(RuntimeInformation.IsOSPlatform));

    /// <summary>
    /// The ordered identity tokens, <c>dexpace-dotnet/&lt;SdkVersion&gt;</c> then <c>dotnet/&lt;RuntimeVersion&gt;</c>, each
    /// non-blank and header-safe; the same instance on every read.
    /// </summary>
    public static IReadOnlyList<string> IdentityTokens { get; } =
        Array.AsReadOnly(["dexpace-dotnet/" + SdkVersion, "dotnet/" + RuntimeVersion]);

    internal static string Probe(Func<string?> probe)
    {
        try
        {
            var value = probe();
            return string.IsNullOrWhiteSpace(value) ? Unknown : value;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return Unknown;
        }
    }

    internal static string ToToken(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed) && HttpHeaderSyntax.IsValidName(trimmed) ? trimmed : Unknown;
    }

    internal static string OsNameFor(Func<OSPlatform, bool> isPlatform)
    {
        if (isPlatform(OSPlatform.Windows))
        {
            return "windows";
        }

        if (isPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (isPlatform(OSPlatform.OSX))
        {
            return "macos";
        }

        return isPlatform(OSPlatform.FreeBSD) ? "freebsd" : Unknown;
    }

    private static string? ReadSdkVersion()
    {
        var assembly = typeof(BuildInfo).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();

        // Strip build metadata (e.g. "0.0.1-alpha.1+abc123" becomes "0.0.1-alpha.1").
        var plus = version?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        return ToToken(plus >= 0 ? version![..plus] : version);
    }
}

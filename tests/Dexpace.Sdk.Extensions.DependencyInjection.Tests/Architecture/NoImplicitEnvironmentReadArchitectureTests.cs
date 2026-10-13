// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Architecture;

/// <summary>
/// CFG-28, CFG-11 (P9-15): in the DI package, <c>ProxyOptions.FromEnvironment</c> is called only from the two <c>UseProxyFromEnvironment</c> code paths, the
/// process environment is read from nowhere, and no configuration switch can turn the environment read on. An IL scan over every method body of the
/// assembly: the second layer behind the <c>RS0030</c> bans, and the only one that sees a call a pragma suppressed.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NoImplicitEnvironmentReadArchitectureTests
{
    [Fact]
    public void FromEnvironment_is_called_only_from_the_two_UseProxyFromEnvironment_paths_and_nothing_reads_the_environment()
    {
        var offenders = IlCallScanner.FindCalls(typeof(DexpaceClientBuilder).Assembly)
            .Where(call => IsForbiddenCallee(call.Callee))
            .Where(call => !IsPermitted(call))
            .Select(call => call.ToString())
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_two_permitted_call_sites_are_still_there()
    {
        // If the overloads were rewritten, the scan above would pass vacuously for the wrong reason.
        var permitted = IlCallScanner.FindCalls(typeof(DexpaceClientBuilder).Assembly)
            .Where(call => IsForbiddenCallee(call.Callee))
            .Where(IsPermitted)
            .ToList();

        Assert.Equal(2, permitted.Count);
        Assert.Equal(2, permitted.Select(static c => c.Callee.ToString()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_scanner_finds_a_planted_call()
    {
        var planted = IlCallScanner.FindCalls(typeof(PlantedCalls).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).Append(typeof(PlantedCalls)))
            .Where(call => IsForbiddenCallee(call.Callee))
            .Select(call => call.CallerMethod.Name + "|" + call.Callee.Name)
            .ToList();

        Assert.Contains("ReadsEnvironment|GetEnvironmentVariable", planted);
        Assert.Contains("ReadsDefaultProxy|get_DefaultProxy", planted);
        Assert.Contains("ReadsFromEnvironment|FromEnvironment", planted);
        Assert.Contains("ReadsDefaultWebProxy|get_DefaultWebProxy", planted);
        Assert.Contains(planted, static entry => entry.StartsWith("MoveNext|GetEnvironmentVariable", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scan_would_catch_a_configuration_switch()
    {
        // P9-15's open half has a standing guard: a type that calls FromEnvironment from a method named Read... fails the permitted-caller rule.
        var planted = IlCallScanner.FindCalls([typeof(PlantedSwitch)])
            .Where(call => IsForbiddenCallee(call.Callee))
            .ToList();

        Assert.NotEmpty(planted);
        Assert.All(planted, static call => Assert.False(IsPermitted(call)));
    }

    private static bool IsPermitted(IlCall call) =>
        call.Callee.DeclaringType == typeof(ProxyOptions)
        && call.Callee.Name == nameof(ProxyOptions.FromEnvironment)
        && call.Caller.FullName?.StartsWith("Dexpace.Sdk.Extensions.DependencyInjection.DexpaceClientBuilder", StringComparison.Ordinal) == true
        && call.CallerMethod.Name.Contains("UseProxyFromEnvironment", StringComparison.Ordinal);

    private static bool IsForbiddenCallee(MethodBase callee) => (callee.DeclaringType?.FullName, callee.Name) switch
    {
        ("System.Environment", "GetEnvironmentVariable" or "GetEnvironmentVariables") => true,
        ("Dexpace.Sdk.Core.Configuration.ProxyOptions", "FromEnvironment") => true,
        ("System.Net.Http.HttpClient", "get_DefaultProxy" or "set_DefaultProxy") => true,
        ("System.Net.WebRequest", "get_DefaultWebProxy" or "set_DefaultWebProxy") => true,
        _ => false,
    };

    // Callees the scanner must see; never called at runtime.
    internal static class PlantedCalls
    {
        internal static string? ReadsEnvironment() => Environment.GetEnvironmentVariable("X");

        internal static IWebProxy ReadsDefaultProxy() => HttpClient.DefaultProxy;

        internal static ProxyOptions? ReadsFromEnvironment() => ProxyOptions.FromEnvironment();

#pragma warning disable SYSLIB0014 // WebRequest is obsolete; the scan must still see a read of its process-wide default.
        internal static IWebProxy? ReadsDefaultWebProxy() => WebRequest.DefaultWebProxy;
#pragma warning restore SYSLIB0014

        internal static async Task<string?> ReadsInsideAnAsyncStateMachine()
        {
            await Task.Yield();
            return Environment.GetEnvironmentVariable("Y");
        }
    }

    // A configuration switch in the shape P9-15 forbids: a reader that turns the environment read on from a value.
    internal static class PlantedSwitch
    {
        internal static ProxyOptions? ReadProxy(bool fromEnvironment) => fromEnvironment ? ProxyOptions.FromEnvironment() : null;
    }
}

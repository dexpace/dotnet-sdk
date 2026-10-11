// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Architecture;

/// <summary>
/// CFG-28, CFG-23, CFG-25, CFG-27 and design §8.2 (phase 8b, P8b-4; phase 5a's F8 hand-off): nothing in the transport assembly
/// reads proxy configuration, or the process environment, implicitly. The `RS0030` bans on the environment readers and on
/// <c>HttpClient.DefaultProxy</c> are the compile-time layer; this IL scan over every method body of the library assembly is
/// the second layer, and the only one that sees a call a pragma suppressed.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NoImplicitEnvironmentReadArchitectureTests
{
    // The one sanctioned environment read: the runtime's own System.Net.Http trace-propagation switch (phase 5c, P5c-11).
    private const string PermittedCallerType = "Dexpace.Sdk.Http.SystemNet.TraceContextStripping";
    private const string PermittedCallerMethod = "ReadRuntimeSwitch";

    [Fact]
    public void No_method_calls_the_environment_or_a_process_wide_default_proxy()
    {
        var offenders = IlCallScanner.FindCalls(typeof(SystemNetHttpClient).Assembly)
            .Where(call => IsForbiddenCallee(call.Callee))
            .Where(call => !IsPermitted(call))
            .Select(call => call.ToString())
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scanner_finds_a_planted_call()
    {
        var planted = IlCallScanner.FindCalls(typeof(PlantedCalls).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).Append(typeof(PlantedCalls)))
            .Where(call => IsForbiddenCallee(call.Callee))
            .Select(call => $"{call.Caller.FullName}|{call.CallerMethod.Name}|{call.Callee.Name}")
            .ToList();

        Assert.Contains(planted, entry => entry.Contains("ReadsEnvironment|GetEnvironmentVariable", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.Contains("ReadsDefaultProxy|get_DefaultProxy", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.Contains("ReadsFromEnvironment|FromEnvironment", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.Contains("ReadsDefaultWebProxy|get_DefaultWebProxy", StringComparison.Ordinal));

        // An async method's body lives in a compiler-generated state machine; the scan must reach it too.
        Assert.Contains(planted, entry => entry.Contains("MoveNext|GetEnvironmentVariable", StringComparison.Ordinal) && entry.Contains("ReadsInsideAnAsyncStateMachine", StringComparison.Ordinal));
    }

    [Fact]
    public void The_permitted_caller_is_still_there()
    {
        // If ReadRuntimeSwitch were deleted or moved, the scan above would pass vacuously for the wrong reason.
        var permitted = IlCallScanner.FindCalls(typeof(SystemNetHttpClient).Assembly)
            .Where(call => IsForbiddenCallee(call.Callee))
            .Where(IsPermitted)
            .ToList();

        Assert.Single(permitted);
        Assert.Equal("GetEnvironmentVariable", permitted[0].Callee.Name);
    }

    private static bool IsPermitted(IlCall call) =>
        call.Caller.FullName == PermittedCallerType
        && call.CallerMethod.Name == PermittedCallerMethod
        && call.Callee.DeclaringType == typeof(Environment)
        && call.Callee.Name == nameof(Environment.GetEnvironmentVariable);

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
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-28 (P5a-8, P5a-17): nothing reads the process environment implicitly. The mechanism is the RS0030 ban on
// Environment.GetEnvironmentVariable with one sanctioned site; these tests are the second line of defence. Hermetic: no
// test reads or sets a process variable. Dexpace.Sdk.Core.Tests cannot see Dexpace.Sdk.Http.SystemNet (SEAM-2), so the
// scan covers the Core assembly only; phase 8b adds the same scan over the transport, with its one sanctioned
// constructor call.
[Trait("Category", "Unit")]
public sealed class NoImplicitProxyReadTests
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void The_default_lookup_is_Environment_GetEnvironmentVariable()
    {
        var expected = typeof(Environment).GetMethod(nameof(Environment.GetEnvironmentVariable), [typeof(string)]);

        Assert.Equal(expected, ProxyResolution.DefaultLookup.Method);
    }

    [Fact]
    public void Both_process_environment_overloads_route_through_ProxyResolution_ResolveFromProcess()
    {
        var resolveFromProcess = typeof(ProxyResolution).GetMethod(
            nameof(ProxyResolution.ResolveFromProcess), BindingFlags.NonPublic | BindingFlags.Static)!;
        var parameterless = typeof(ProxyOptions).GetMethod(nameof(ProxyOptions.FromEnvironment), Type.EmptyTypes)!;
        var withLogger = typeof(ProxyOptions).GetMethod(nameof(ProxyOptions.FromEnvironment), [typeof(ILogger)])!;

        Assert.Contains((MethodBase)resolveFromProcess, CalledMethods(parameterless));
        Assert.Contains((MethodBase)resolveFromProcess, CalledMethods(withLogger));
        var defaultLookup = typeof(ProxyResolution).GetProperty(nameof(ProxyResolution.DefaultLookup), BindingFlags.NonPublic | BindingFlags.Static)!.GetMethod!;
        Assert.Contains((MethodBase)defaultLookup, CalledMethods(resolveFromProcess));
    }

    [Fact]
    public void Null_logger_is_rejected_by_FromEnvironment_ILogger()
    {
        var error = Assert.Throws<ArgumentNullException>(() => ProxyOptions.FromEnvironment((ILogger)null!));

        Assert.Equal("logger", error.ParamName);
    }

    [Fact]
    public void No_method_in_the_Core_assembly_calls_FromEnvironment()
    {
        var callers = FindCallersOfFromEnvironment(typeof(ProxyOptions).Assembly.GetTypes());

        Assert.Empty(callers);
    }

    [Fact]
    public void The_scanner_sees_a_method_that_does_call_it()
    {
        var callers = FindCallersOfFromEnvironment([typeof(Offender)]);

        Assert.Contains("Offender.Call", callers);
    }

    private static List<string> FindCallersOfFromEnvironment(IEnumerable<Type> types)
    {
        var found = new List<string>();
        foreach (var type in types)
        {
            foreach (var method in MethodsOf(type))
            {
                if (CalledMethods(method).Any(m => m.Name == nameof(ProxyOptions.FromEnvironment) && m.DeclaringType == typeof(ProxyOptions)))
                {
                    found.Add(type.Name + "." + method.Name);
                }
            }
        }

        return found;
    }

    private static IEnumerable<MethodBase> MethodsOf(Type type) =>
        type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared));

    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        foreach (var token in OperandTokens(method, 0x28).Concat(OperandTokens(method, 0x6F)))
        {
            MethodBase? resolved = null;
            try
            {
                resolved = method.Module.ResolveMethod(token, TypeArguments(method), method.IsGenericMethod ? method.GetGenericArguments() : null);
            }
            catch (ArgumentException)
            {
                // The byte pair was an operand or an unrelated byte, not a call: ignore it.
            }

            if (resolved is not null)
            {
                yield return resolved;
            }
        }
    }

    private static Type[]? TypeArguments(MethodBase method) =>
        method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;

    // A byte scan for the opcode followed by a four-byte metadata token. A stray match is resolved or rejected by the
    // module, and only a token that resolves to the method or field being looked for counts.
    private static IEnumerable<int> OperandTokens(MethodBase method, byte opcode)
    {
        var body = method.GetMethodBody()?.GetILAsByteArray();
        if (body is null)
        {
            yield break;
        }

        for (var i = 0; i + 4 < body.Length; i++)
        {
            if (body[i] == opcode)
            {
                yield return BitConverter.ToInt32(body, i + 1);
            }
        }
    }

    private static class Offender
    {
        public static ProxyOptions? Call() => ProxyOptions.FromEnvironment(_ => null, NullLogger.Instance);
    }
}

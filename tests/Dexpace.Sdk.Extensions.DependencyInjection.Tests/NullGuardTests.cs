// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// CFG-37: every public method of the extension class and the builder rejects a null argument, by reflection over the public surface (a test-only use of
/// reflection). The test fails when a public parameter type has no example value, so a new method cannot silently skip the guard.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NullGuardTests
{
    private static readonly Dictionary<Type, Func<object>> s_examples = new()
    {
        [typeof(IServiceCollection)] = static () => new ServiceCollection(),
        [typeof(string)] = static () => "github",
        [typeof(IConfigurationSection)] = static () => Config.Section(),
        [typeof(Func<DexpaceClientOptions, DexpaceClientOptions>)] = static () => (Func<DexpaceClientOptions, DexpaceClientOptions>)(static o => o),
        [typeof(Func<SystemNetHttpClientOptions, SystemNetHttpClientOptions>)] = static () => (Func<SystemNetHttpClientOptions, SystemNetHttpClientOptions>)(static o => o),
        [typeof(Func<IServiceProvider, AuthCredentials, AuthCredentials>)] = static () => (Func<IServiceProvider, AuthCredentials, AuthCredentials>)(static (_, c) => c),
        [typeof(Func<string, string?>)] = static () => (Func<string, string?>)(static _ => null),
        [typeof(IAsyncHttpClient)] = static () => new Dexpace.Sdk.TestSupport.Transports.ScriptedTransport(),
        [typeof(Func<IServiceProvider, IAsyncHttpClient>)] = static () => (Func<IServiceProvider, IAsyncHttpClient>)(static _ => new Dexpace.Sdk.TestSupport.Transports.ScriptedTransport()),
        [typeof(ISerde)] = static () => StubSerde.Instance,
        [typeof(Func<IServiceProvider, ISerde>)] = static () => (Func<IServiceProvider, ISerde>)(static _ => StubSerde.Instance),
        [typeof(AuthDescriptor)] = static () => new AuthDescriptor(AuthRequirement.NoAuth),
        [typeof(Func<IServiceProvider, HttpPipelinePolicy>)] = static () => (Func<IServiceProvider, HttpPipelinePolicy>)(static _ => new Dexpace.Sdk.Core.Pipeline.Policies.IdempotencyPolicy()),
    };

    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (var method in PublicMethods())
        {
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].ParameterType.IsValueType)
                {
                    data.Add(Key(method), i);
                }
            }
        }

        return data;
    }

    [Fact]
    public void Every_public_parameter_type_has_an_example_value()
    {
        var missing = PublicMethods()
            .SelectMany(static m => m.GetParameters())
            .Select(static p => p.ParameterType)
            .Where(static t => !s_examples.ContainsKey(t))
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "no example value for: " + string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_null_argument_throws_ArgumentNullException_naming_the_parameter(string key, int index)
    {
        var method = PublicMethods().Single(m => Key(m) == key);
        var target = method.IsStatic ? null : new ServiceCollection().AddDexpaceClient("github");
        var arguments = method.GetParameters().Select(static p => s_examples[p.ParameterType]()).ToArray();
        arguments[index] = null!;

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(target, arguments));

        var inner = Assert.IsType<ArgumentNullException>(exception.InnerException);
        Assert.Equal(method.GetParameters()[index].Name, inner.ParamName);
    }

    [Fact]
    public void A_blank_name_is_an_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddDexpaceClient(" ", Config.Section()));
    }

    private static IEnumerable<MethodInfo> PublicMethods() =>
        new[] { typeof(DexpaceServiceCollectionExtensions), typeof(DexpaceClientBuilder) }
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static m => !m.IsSpecialName);

    private static string Key(MethodInfo method) =>
        method.DeclaringType!.Name + "." + method.Name + "(" + string.Join(",", method.GetParameters().Select(static p => p.ParameterType.Name)) + ")";
}

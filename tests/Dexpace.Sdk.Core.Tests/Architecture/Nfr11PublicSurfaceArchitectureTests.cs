// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// NFR-11 (design §9.2 and §11 item 2; phase 10, P10-11, V10): core's public and protected signatures name only types of the base class library's
/// <c>System</c> namespaces, <c>Microsoft.Extensions.Logging</c> and core's own, so a consumer is never forced to reference a third-party type to
/// call core. The design says "a test scanning <c>PublicAPI.Shipped.txt</c>"; that file was empty when this test was written (phase 12 filled it) and its text format
/// is not a type model, so the subject is the compiled assembly, which is the surface those files describe. <c>System.Net.Http</c> and <c>System.Text.Json</c> sit
/// under <c>System</c> in the framework yet belong to the adapters: the scan refuses them beside the allow-list, and an allow-list under
/// <c>System</c> alone would have admitted <c>HttpRequestMessage</c>, which is the scan's own negative control.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Nfr11PublicSurfaceArchitectureTests
{
    private static readonly string[] s_allowed = ["System", "Microsoft.Extensions.Logging", "Dexpace.Sdk.Core"];

    private static readonly string[] s_refused = ["System.Net.Http", "System.Text.Json", "Microsoft.Extensions.DependencyInjection"];

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Core_public_signatures_name_only_system_logging_and_core_types()
    {
        var violations = Scan(typeof(Request).Assembly.GetExportedTypes());

        Assert.Empty(violations);
    }

    [Fact]
    public void The_scan_visits_a_real_surface()
    {
        // A scan that found nothing to look at would pass for the wrong reason.
        var types = typeof(Request).Assembly.GetExportedTypes();

        Assert.True(types.Length > 150, $"{types.Length} exported types");
        Assert.Contains(typeof(Request), types);
        Assert.True(types.Sum(static t => t.GetMembers(All).Length) > 1500);
    }

    [Fact]
    public void The_scan_can_fail()
    {
        // Negative controls: System.Net.Http is under System but refused, and Xunit is outside every allowed namespace.
        var violations = Scan([typeof(UsesHttpRequestMessage), typeof(UsesXunit), typeof(GenericOverHttp), typeof(Conforming)]);

        Assert.Contains(violations, static v => v.Contains("UsesHttpRequestMessage", StringComparison.Ordinal) && v.Contains("System.Net.Http.HttpRequestMessage", StringComparison.Ordinal));
        Assert.Contains(violations, static v => v.Contains("UsesXunit", StringComparison.Ordinal) && v.Contains("Xunit.FactAttribute", StringComparison.Ordinal));
        Assert.Contains(violations, static v => v.Contains("GenericOverHttp", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, static v => v.Contains("Conforming", StringComparison.Ordinal));
    }

    private static List<string> Scan(IEnumerable<Type> types)
    {
        var violations = new List<string>();
        foreach (var type in types)
        {
            foreach (var (member, used) in SignatureTypes(type))
            {
                foreach (var name in Flatten(used).Where(static t => !Allowed(t)).Select(static t => t.FullName ?? t.Name).Distinct())
                {
                    violations.Add($"{type.FullName}.{member}: {name}");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<(string Member, Type Used)> SignatureTypes(Type type)
    {
        if (type.BaseType is { } baseType)
        {
            yield return ("(base type)", baseType);
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return ("(interface)", contract);
        }

        foreach (var member in type.GetMembers(All))
        {
            switch (member)
            {
                case FieldInfo { IsPublic: true } or FieldInfo { IsFamily: true } or FieldInfo { IsFamilyOrAssembly: true }:
                    yield return (member.Name, ((FieldInfo)member).FieldType);
                    break;
                case MethodBase method when method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly:
                    foreach (var parameter in method.GetParameters())
                    {
                        yield return (method.Name, parameter.ParameterType);
                    }

                    if (method is MethodInfo info)
                    {
                        yield return (method.Name, info.ReturnType);
                    }

                    break;
                case PropertyInfo property when (property.GetMethod?.IsPublic ?? false) || (property.GetMethod?.IsFamily ?? false) || (property.GetMethod?.IsFamilyOrAssembly ?? false)
                    || (property.SetMethod?.IsPublic ?? false) || (property.SetMethod?.IsFamily ?? false) || (property.SetMethod?.IsFamilyOrAssembly ?? false):
                    yield return (property.Name, property.PropertyType);
                    break;
                case EventInfo { AddMethod: { IsPublic: true } or { IsFamily: true } or { IsFamilyOrAssembly: true } } evt:
                    yield return (evt.Name, evt.EventHandlerType!);
                    break;
                default:
                    break;
            }
        }
    }

    // The type and every type it is made of: element types, generic arguments, a nullable's underlying type.
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.IsGenericParameter)
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (var inner in Flatten(type.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
            {
                yield return argument;
            }
        }
    }

    private static bool Allowed(Type type)
    {
        var space = type.Namespace ?? string.Empty;
        return !s_refused.Any(refused => space == refused || space.StartsWith(refused + ".", StringComparison.Ordinal))
            && s_allowed.Any(allowed => space == allowed || space.StartsWith(allowed + ".", StringComparison.Ordinal));
    }

    public sealed class UsesHttpRequestMessage
    {
        public static HttpRequestMessage Message() => new();
    }

    public sealed class UsesXunit
    {
        public static void Take(FactAttribute fact) => _ = fact;
    }

    public sealed class GenericOverHttp
    {
        public List<HttpRequestMessage>? Messages { get; set; }
    }

    public sealed class Conforming
    {
        public static Task<Request?> Take(Uri url, CancellationToken token, IReadOnlyList<string>? names = null) => Task.FromResult<Request?>(null);
    }
}

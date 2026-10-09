// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// The assertions that have a negative control, found statically: every public class in <c>Controls</c> that exposes a static
/// <c>Covered</c> collection of assertion names is a control class (plan 2.4). There is no run-time registry filled by tests
/// (xUnit orders and parallelises classes arbitrarily), and a new control class is found without being listed here.
/// </summary>
internal static class ControlClasses
{
    /// <summary>The union of every control class's covered assertion names.</summary>
    internal static IReadOnlyCollection<string> Covered { get; } =
    [
        .. typeof(ControlClasses).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "Dexpace.Sdk.Conformance.Tests.Controls" && type.Name.EndsWith("ControlTests", StringComparison.Ordinal))
            .Select(type => type.GetProperty("Covered", BindingFlags.Public | BindingFlags.Static))
            .OfType<PropertyInfo>()
            .SelectMany(property => (IReadOnlyCollection<string>)property.GetValue(null)!)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The control classes themselves, so a test can check none was missed.</summary>
    internal static IReadOnlyList<Type> Classes { get; } =
    [
        .. typeof(ControlClasses).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "Dexpace.Sdk.Conformance.Tests.Controls" && type.Name.EndsWith("ControlTests", StringComparison.Ordinal)),
    ];
}

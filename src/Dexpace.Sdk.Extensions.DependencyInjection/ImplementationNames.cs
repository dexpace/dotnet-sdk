// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>Names the implementation behind a keyed descriptor for a failure message (SEAM-5): an instance's type, an implementation type, or a factory's declaring type and method.</summary>
internal static class ImplementationNames
{
    /// <summary>Describes <paramref name="descriptor"/>.</summary>
    /// <param name="descriptor">A keyed descriptor.</param>
    /// <returns>The implementation's name.</returns>
    internal static string Describe(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!descriptor.IsKeyedService)
        {
            return descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name;
        }

        if (descriptor.KeyedImplementationInstance is { } instance)
        {
            return instance.GetType().FullName ?? instance.GetType().Name;
        }

        if (descriptor.KeyedImplementationType is { } type)
        {
            return type.FullName ?? type.Name;
        }

        return descriptor.KeyedImplementationFactory is { } factory ? Describe(factory) : "an unknown implementation";
    }

    /// <summary>Names a factory delegate by its declaring type and method.</summary>
    /// <param name="factory">The delegate.</param>
    /// <returns>The declaring type and method.</returns>
    internal static string Describe(Delegate factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return "a factory (" + factory.Method.DeclaringType?.FullName + "." + factory.Method.Name + ")";
    }
}

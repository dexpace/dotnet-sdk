// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// One role of a named client (the transport or the codec): the descriptor in the collection and the explicit install that won it (SEAM-5,
/// SEAM-6; P9-16). An explicit install replaces the package's default descriptor; the same install again is a no-op and a different one throws
/// at the call, naming the client and both implementations.
/// </summary>
internal sealed class ServiceSlot(string role)
{
    /// <summary>The descriptor currently registered for the role, or <see langword="null"/> when none is.</summary>
    internal ServiceDescriptor? Descriptor { get; set; }

    /// <summary>The instance or delegate of the explicit install; <see langword="null"/> while the default stands.</summary>
    internal object? Identity { get; private set; }

    /// <summary>How the explicit install is named in a message.</summary>
    internal string? Description { get; private set; }

    /// <summary>Installs <paramref name="descriptor"/> as the role's registration.</summary>
    internal void Install(DexpaceClientRegistration registration, object identity, string description, ServiceDescriptor descriptor)
    {
        if (Identity is not null)
        {
            if (ReferenceEquals(Identity, identity) || (Identity is Delegate existing && existing.Equals(identity)))
            {
                return;
            }

            throw new InvalidOperationException(
                "Client '" + registration.Name + "' already has " + role + " " + Description + "; a second, different " + role + " ("
                + description + ") cannot be installed (SEAM-6). Install one " + role + " per client.");
        }

        if (Descriptor is not null)
        {
            registration.Services.Remove(Descriptor);
        }

        registration.Services.Add(descriptor);
        Descriptor = descriptor;
        Identity = identity;
        Description = description;
    }
}

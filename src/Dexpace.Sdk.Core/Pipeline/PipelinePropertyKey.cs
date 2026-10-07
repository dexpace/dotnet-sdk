// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// A typed key into the call-scoped property bag of <see cref="PipelineContext"/>. Keys are compared by reference
/// identity: two keys with the same name do not collide, and a policy that does not hold the key instance cannot read
/// or overwrite the value (P4c-4).
/// </summary>
/// <typeparam name="T">The type of the value stored under this key.</typeparam>
/// <remarks>
/// Declare a key once, as a <c>static readonly</c> field of the policy that owns it.
/// </remarks>
public sealed class PipelinePropertyKey<T>
{
    /// <summary>
    /// Initializes a key.
    /// </summary>
    /// <param name="name">A diagnostic name; it takes no part in equality.</param>
    public PipelinePropertyKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// The diagnostic name of the key.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

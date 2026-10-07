// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>An exception that overrides equality by message, so a default-comparer set cannot tell two apart.</summary>
/// <param name="message">The message that defines equality.</param>
public sealed class StructurallyEqualException(string message) : Exception(message)
{
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StructurallyEqualException other && other.Message == Message;

    /// <inheritdoc />
    public override int GetHashCode() => Message.GetHashCode(StringComparison.Ordinal);
}

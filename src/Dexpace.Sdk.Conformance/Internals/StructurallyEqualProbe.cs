// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// An exception that is <see cref="object.Equals(object?)"/> to every other of its kind with the same message. A cause walk that
/// tracks visited causes by equality instead of reference identity stops at the second of a chain of these, which is how
/// <c>XCUT-9</c> tells the two apart without ever throwing a cyclic chain.
/// </summary>
internal sealed class StructurallyEqualProbe(string message, Exception? inner) : Exception(message, inner)
{
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StructurallyEqualProbe other && other.Message == Message;

    /// <inheritdoc />
    public override int GetHashCode() => Message.GetHashCode(StringComparison.Ordinal);
}

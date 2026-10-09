// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// A visitor that is handed the closed <see cref="Tristate{T}"/> behind an <see cref="ITristate"/> as a statically typed
/// generic argument (P7a-6).
/// </summary>
/// <typeparam name="TResult">What the visit produces.</typeparam>
/// <remarks>
/// This is the codec-adapter hook: an adapter in another assembly builds a codec for the closed type without
/// <c>MakeGenericType</c> (which is not NativeAOT-safe) and without <c>InternalsVisibleTo</c> into core.
/// </remarks>
public interface ITristateVisitor<out TResult>
{
    /// <summary>Visits a <see cref="Tristate{T}"/> whose type argument is <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The wrapped value type.</typeparam>
    /// <param name="value">The visited value.</param>
    /// <returns>The visit's result.</returns>
    TResult Visit<T>(Tristate<T> value)
        where T : notnull;
}

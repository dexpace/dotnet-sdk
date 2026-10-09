// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// Inference-friendly entry points for <see cref="Tristate{T}"/> (SERDE-18, P7a-7). The generic type's own factories need
/// the type argument spelled out; these infer it, and the type-less markers convert to any <c>Tristate&lt;T&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// var clear = patch with { Name = Tristate.Null };       // sent as "name": null
/// var keep  = patch with { Name = Tristate.Absent };     // key omitted
/// var set   = patch with { Size = Tristate.Present(3) }; // sent as "size": 3
/// </code>
/// </example>
public static class Tristate
{
    /// <summary>Gets the Absent marker, implicitly convertible to <c>Tristate&lt;T&gt;</c> for every <c>T</c>.</summary>
    public static TristateSentinel Absent => TristateSentinel.Absent;

    /// <summary>Gets the Null marker, implicitly convertible to <c>Tristate&lt;T&gt;</c> for every <c>T</c>.</summary>
    public static TristateSentinel Null => TristateSentinel.Null;

    /// <summary>Builds the Present state, inferring <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The non-null value.</param>
    /// <returns>A Present value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static Tristate<T> Present<T>(T value)
        where T : notnull => Tristate<T>.Present(value);

    /// <summary>Maps a possibly-null reference to Null or Present, never to Absent.</summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="value">The possibly-null value.</param>
    /// <returns>Null for <see langword="null"/>, else Present.</returns>
    public static Tristate<T> FromNullable<T>(T? value)
        where T : class => Tristate<T>.FromNullable(value);

    /// <summary>Maps a <see cref="Nullable{T}"/> to Null or Present, never to Absent.</summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value.</param>
    /// <returns>Null for <see langword="null"/>, else Present.</returns>
    public static Tristate<T> FromNullable<T>(T? value)
        where T : struct => value.HasValue ? Tristate<T>.Present(value.Value) : Tristate<T>.Null;

    /// <summary>The value of a Present state as a <see cref="Nullable{T}"/>, else <see langword="null"/> (SERDE-18).</summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The tristate.</param>
    /// <returns>The value for Present; <see langword="null"/> for Absent and Null.</returns>
    public static T? GetValueOrNull<T>(this Tristate<T> value)
        where T : struct => value.IsPresent ? value.Value : null;
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// A field that is Absent (not on the wire), Null (the wire's <c>null</c>) or Present (a non-null value): the type a
/// PATCH model uses where "leave it alone" and "clear it" must be different requests (SERDE-14, issue #1).
/// </summary>
/// <typeparam name="T">The value type. It is non-nullable: <c>null</c> is the <see cref="Null"/> state, never a value.</typeparam>
/// <remarks>
/// <para>
/// <c>default(Tristate&lt;T&gt;)</c> is Absent (SERDE-17), so a field, property or record parameter with no initializer
/// is Absent. <see cref="Present"/> rejects <see langword="null"/>, so the fourth state (Present holding <c>null</c>)
/// cannot be built (SERDE-14). The implicit conversion from <typeparamref name="T"/> maps <see langword="null"/> to
/// <see cref="Null"/> and never throws (P7a-2).
/// </para>
/// <para>
/// This is a hand-written <see langword="readonly"/> <see langword="struct"/>, not a record struct, so its
/// <see cref="ToString"/> is stable and it has no public positional constructor that bypasses the null check (P7a-3).
/// A codec adapter reaches the closed type through <see cref="ITristate"/>. Covariance (SERDE-14's SHOULD) is unmet
/// because C# generics on a struct are invariant; <see cref="Tristate.Absent"/> and <see cref="Tristate.Null"/> serve the
/// same ergonomics (design section 10, entry 21).
/// </para>
/// </remarks>
public readonly struct Tristate<T> : IEquatable<Tristate<T>>, ITristate
    where T : notnull
{
    // default(TristateState) == Absent, so SERDE-17 holds for free: no constructor runs for a default field.
    private readonly T? _value;
    private readonly TristateState _state;

    private Tristate(T? value, TristateState state)
    {
        _value = value;
        _state = state;
    }

    /// <summary>The Absent state: the field is not on the wire. Equal to <see langword="default"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "SERDE-18 / design position A: the generic-typed factories are the specified API; the non-generic Tristate.* is the inference-friendly route.")]
    public static Tristate<T> Absent => default;

    /// <summary>The Null state: the field is on the wire as the literal <c>null</c>.</summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "SERDE-18 / design position A: the generic-typed factories are the specified API; the non-generic Tristate.* is the inference-friendly route.")]
    public static Tristate<T> Null => new(default, TristateState.Null);

    /// <summary>Gets the state of this value.</summary>
    public TristateState State => _state;

    /// <summary>Gets a value indicating whether this is the Absent state.</summary>
    public bool IsAbsent => _state == TristateState.Absent;

    /// <summary>Gets a value indicating whether this is the Null state.</summary>
    public bool IsNull => _state == TristateState.Null;

    /// <summary>Gets a value indicating whether this is the Present state.</summary>
    public bool IsPresent => _state == TristateState.Present;

    /// <summary>Gets the value of a Present state.</summary>
    /// <exception cref="InvalidOperationException">The state is Absent or Null; the message names it.</exception>
    public T Value => _state == TristateState.Present
        ? _value!
        : throw new InvalidOperationException($"The Tristate has no value: it is {_state}. Check IsPresent or use TryGetValue.");

    /// <summary>Builds the Present state.</summary>
    /// <param name="value">The non-null value.</param>
    /// <returns>A Present value holding <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>; use <see cref="Null"/>.</exception>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "SERDE-18 / design position A: the generic-typed factories are the specified API; the non-generic Tristate.* is the inference-friendly route.")]
    public static Tristate<T> Present(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Tristate<T>(value, TristateState.Present);
    }

    /// <summary>
    /// Maps a possibly-null value to a state, and never to Absent: <see langword="null"/> is Null, anything else is Present
    /// (SERDE-18).
    /// </summary>
    /// <param name="value">The possibly-null value.</param>
    /// <returns><see cref="Null"/> for <see langword="null"/>, else Present.</returns>
    /// <remarks>For a value-type <typeparamref name="T"/>, <c>T?</c> is <typeparamref name="T"/>, so this is Present; use <see cref="Tristate.FromNullable{T}(T?)"/> for a <see cref="Nullable{T}"/>.</remarks>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "SERDE-18 / design position A: the generic-typed factories are the specified API; the non-generic Tristate.* is the inference-friendly route.")]
    public static Tristate<T> FromNullable(T? value) =>
        value is null ? Null : new Tristate<T>(value, TristateState.Present);

    /// <summary>Converts a possibly-null value with <see cref="FromNullable"/> semantics: <see langword="null"/> is Null (P7a-2).</summary>
    /// <param name="value">The possibly-null value.</param>
    /// <returns>The state <see cref="FromNullable"/> yields; never Absent.</returns>
    public static implicit operator Tristate<T>(T? value) => FromNullable(value);

    /// <summary>Converts <see cref="Tristate.Absent"/> or <see cref="Tristate.Null"/> to the matching state (P7a-7).</summary>
    /// <param name="sentinel">The type-less marker.</param>
    /// <returns>Absent or Null.</returns>
    public static implicit operator Tristate<T>(TristateSentinel sentinel) =>
        sentinel.Kind == TristateState.Null ? Null : Absent;

    /// <summary>Compares two values by state and, for Present, by value.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool operator ==(Tristate<T> left, Tristate<T> right) => left.Equals(right);

    /// <summary>Compares two values by state and, for Present, by value.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(Tristate<T> left, Tristate<T> right) => !left.Equals(right);

    /// <summary>Gets the value when Present.</summary>
    /// <param name="value">The value when Present; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> only for the Present state.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _state == TristateState.Present ? _value : default;
        return _state == TristateState.Present;
    }

    /// <summary>Gets the value when Present, else <see langword="default"/>.</summary>
    /// <returns>The value for Present; <see langword="default"/> for Absent and Null.</returns>
    public T? GetValueOrDefault() => _state == TristateState.Present ? _value : default;

    /// <summary>Gets the value when Present, else <paramref name="defaultValue"/>.</summary>
    /// <param name="defaultValue">What to return for Absent and Null.</param>
    /// <returns>The value for Present; <paramref name="defaultValue"/> otherwise.</returns>
    public T GetValueOrDefault(T defaultValue) => _state == TristateState.Present ? _value! : defaultValue;

    /// <summary>Invokes exactly one of three arms, by state.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="onAbsent">Invoked for Absent.</param>
    /// <param name="onNull">Invoked for Null.</param>
    /// <param name="onPresent">Invoked with the value for Present.</param>
    /// <returns>The invoked arm's result.</returns>
    /// <exception cref="ArgumentNullException">An arm is <see langword="null"/>.</exception>
    public TResult Match<TResult>(Func<TResult> onAbsent, Func<TResult> onNull, Func<T, TResult> onPresent)
    {
        ArgumentNullException.ThrowIfNull(onAbsent);
        ArgumentNullException.ThrowIfNull(onNull);
        ArgumentNullException.ThrowIfNull(onPresent);

        return _state switch
        {
            TristateState.Present => onPresent(_value!),
            TristateState.Null => onNull(),
            _ => onAbsent(),
        };
    }

    /// <inheritdoc/>
    public bool Equals(Tristate<T> other) =>
        _state == other._state
        && (_state != TristateState.Present || EqualityComparer<T>.Default.Equals(_value!, other._value!));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Tristate<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _state == TristateState.Present ? HashCode.Combine(_state, _value) : (int)_state;

    /// <summary>Renders <c>Absent</c>, <c>Null</c> or <c>Present(&lt;value&gt;)</c> (SERDE-30).</summary>
    /// <returns>The rendering; the value is formatted with the invariant culture.</returns>
    public override string ToString() => _state switch
    {
        TristateState.Present => string.Create(CultureInfo.InvariantCulture, $"Present({_value})"),
        TristateState.Null => "Null",
        _ => "Absent",
    };

    /// <inheritdoc/>
    TResult ITristate.Accept<TResult>(ITristateVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return visitor.Visit(this);
    }
}

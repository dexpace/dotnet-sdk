// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>What a typed-adapter mapper decided about one event (SSE-34, P7b-16).</summary>
public enum SseMapResultKind
{
    /// <summary>The event maps to a model, which the adapter yields.</summary>
    Value = 1,

    /// <summary>The event carries no model (a keep-alive, say); the adapter advances silently.</summary>
    Skip = 2,

    /// <summary>The event is the caller's end-of-stream sentinel; the adapter releases the stream and completes.</summary>
    Done = 3,
}

/// <summary>
/// The type-free half of a mapper's verdict: the <see cref="Skip"/> and <see cref="Done"/> signals and the
/// <see cref="Value{T}"/> factory (SSE-34, P7b-16).
/// </summary>
/// <remarks>
/// <para>
/// A mapper returns <see cref="SseMapResult{T}"/>. The signals are non-generic so that a caller writes
/// <c>SseMapResult.Done</c> and not <c>SseMapResult&lt;Chunk&gt;.Done</c>: they convert implicitly to any
/// <see cref="SseMapResult{T}"/>. The end-of-stream sentinel is the caller's, never core's (SSE-37):
/// <c>(name, data) =&gt; data == "[DONE]" ? SseMapResult.Done : SseMapResult.Value(Parse(data))</c>.
/// </para>
/// <para>
/// <c>default</c> is deliberately not a verdict: its <see cref="Kind"/> is <c>0</c>, not a defined kind, and the adapter
/// treats a mapper that returns it as a failure rather than guessing <see cref="Skip"/> (which would drop every event) or
/// <see cref="Done"/> (which would end the stream). Converting a <c>default</c> signal throws.
/// </para>
/// </remarks>
public readonly record struct SseMapResult
{
    private SseMapResult(SseMapResultKind kind) => Kind = kind;

    /// <summary>The "advance silently" signal; converts to any <see cref="SseMapResult{T}"/>.</summary>
    public static SseMapResult Skip { get; } = new(SseMapResultKind.Skip);

    /// <summary>The "release and complete" signal; converts to any <see cref="SseMapResult{T}"/>.</summary>
    public static SseMapResult Done { get; } = new(SseMapResultKind.Done);

    /// <summary>Which signal this is; <c>0</c> (not a defined kind) for <c>default</c>.</summary>
    public SseMapResultKind Kind { get; }

    /// <summary>The named alternative to the implicit conversion to <see cref="SseMapResult{T}"/> (CA2225).</summary>
    /// <typeparam name="T">The model type of the result.</typeparam>
    /// <returns>This signal as a result of model type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException">This signal is <c>default</c>, which is not a verdict.</exception>
    public SseMapResult<T> ToSseMapResult<T>() => this;

    /// <summary>Wraps a model for the adapter to yield.</summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="value">The model.</param>
    /// <returns>A result of kind <see cref="SseMapResultKind.Value"/>.</returns>
    public static SseMapResult<T> Value<T>(T value) => new(SseMapResultKind.Value, value);
}

/// <summary>A mapper's verdict on one event: a model to yield, a silent skip, or the end of the stream (SSE-34).</summary>
/// <typeparam name="T">The model type.</typeparam>
/// <remarks>
/// Build one with <see cref="SseMapResult.Value{T}(T)"/>, <see cref="SseMapResult.Skip"/> or
/// <see cref="SseMapResult.Done"/>. It is a value type with value equality, so a mapper can be unit-tested without a
/// stream. <c>default(SseMapResult&lt;T&gt;)</c> has a <see cref="Kind"/> of <c>0</c>, which the adapter reports as a mapper
/// failure (P7b-16).
/// </remarks>
public readonly record struct SseMapResult<T>
{
    private readonly T _value;

    internal SseMapResult(SseMapResultKind kind, T value)
    {
        Kind = kind;
        _value = value;
    }

    /// <summary>Which verdict this is; <c>0</c> (not a defined kind) for <c>default</c>.</summary>
    public SseMapResultKind Kind { get; }

    /// <summary>The model, available only when <see cref="Kind"/> is <see cref="SseMapResultKind.Value"/>.</summary>
    /// <exception cref="InvalidOperationException">The result is a signal, or <c>default</c>, and holds no model.</exception>
    public T Value => Kind == SseMapResultKind.Value
        ? _value
        : throw new InvalidOperationException("This mapper result holds no value; only a result built by SseMapResult.Value does.");

    /// <summary>Converts a type-free signal to a result of any model type.</summary>
    /// <param name="signal"><see cref="SseMapResult.Skip"/> or <see cref="SseMapResult.Done"/>.</param>
    /// <exception cref="ArgumentException">The signal is <c>default</c>, which is not a verdict.</exception>
    public static implicit operator SseMapResult<T>(SseMapResult signal) => signal.Kind switch
    {
        SseMapResultKind.Skip => new SseMapResult<T>(SseMapResultKind.Skip, default!),
        SseMapResultKind.Done => new SseMapResult<T>(SseMapResultKind.Done, default!),
        _ => throw new ArgumentException("A default SseMapResult is not a verdict; use Skip or Done.", nameof(signal)),
    };

    // The synthesized form would read Value, which throws for a signal, so the string form is written by hand and never
    // touches the stored value of a result that has none.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Kind = ").Append(Kind);
        if (Kind == SseMapResultKind.Value)
        {
            builder.Append(", Value = ").Append(_value);
        }

        return true;
    }
}

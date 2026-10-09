// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson;

/// <summary>
/// A <see cref="ISerde"/> implementation backed by System.Text.Json. Type metadata is resolved from
/// a source-generated <see cref="JsonSerializerContext"/>, keeping serialization trim- and
/// NativeAOT-safe with no runtime reflection.
/// </summary>
public sealed class SystemTextJsonSerde : ISerde
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Initializes a new instance from explicit options.</summary>
    /// <param name="options">
    /// Options whose <see cref="JsonSerializerOptions.TypeInfoResolver"/> is set (typically a
    /// source-generated <see cref="JsonSerializerContext"/>). The serde works on a private copy: the supplied instance is
    /// never made read-only, never gains a converter and keeps its resolver (SERDE-26), so a later change to it does not
    /// affect the serde. The copy is Tristate-wired (SERDE-19, see <see cref="TristateJsonSerializerOptionsExtensions"/>) and
    /// then frozen. The guard only verifies that a <see cref="JsonSerializerOptions.TypeInfoResolver"/>
    /// is present; AOT-safety holds only when that resolver is a source-generated
    /// <see cref="JsonSerializerContext"/> — use the <see cref="SystemTextJsonSerde(JsonSerializerContext)"/>
    /// constructor to make this explicit.
    /// </param>
    /// <remarks>
    /// <b>Breaking:</b> this constructor used to call <see cref="JsonSerializerOptions.MakeReadOnly()"/> on the caller's
    /// instance, which froze it and let a second serde built from the same options fail on a later mutation (SERDE-26).
    /// To start from the SDK's recommended configuration use <see cref="CreateDefaultOptions"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">The options have no type-info resolver.</exception>
    public SystemTextJsonSerde(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TypeInfoResolver is null)
        {
            throw new ArgumentException(
                "The JsonSerializerOptions must have a TypeInfoResolver (for example, a source-generated "
                + "JsonSerializerContext) for AOT-safe serialization.",
                nameof(options));
        }

        // An independent mutable copy, including of a source-generated context's read-only options (design fact 6): the
        // caller's instance is never frozen or wired (SERDE-26), so the "cannot be copied" fallback is unreachable.
        var copy = new JsonSerializerOptions(options);
        copy.AddTristateSupport();
        copy.MakeReadOnly();
        _options = copy;
    }

    /// <summary>Initializes a new instance from a source-generated context.</summary>
    /// <param name="context">The source-generated serializer context.</param>
    public SystemTextJsonSerde(JsonSerializerContext context)
        : this((context ?? throw new ArgumentNullException(nameof(context))).Options)
    {
    }

    /// <summary>
    /// Creates the SDK's recommended <see cref="JsonSerializerOptions"/> over <paramref name="typeInfoResolver"/>: a fresh,
    /// mutable instance per call (SERDE-25), Tristate-wired.
    /// </summary>
    /// <param name="typeInfoResolver">
    /// The metadata source, typically a source-generated <see cref="JsonSerializerContext"/> such as
    /// <c>MyContext.Default</c>. It is required: the AOT-safe path needs one, and the Tristate modifier wraps it.
    /// </param>
    /// <returns>New options; pass them to <see cref="SystemTextJsonSerde(JsonSerializerOptions)"/>.</returns>
    /// <remarks>
    /// <para>
    /// Usage: <c>new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(MyContext.Default))</c>.
    /// </para>
    /// <para>
    /// Three choices are made here (design 7.3, P7a-8). Naming is <see cref="JsonSerializerDefaults.Web"/> (camelCase
    /// properties, case-insensitive reads). <see cref="JsonSerializerOptions.NumberHandling"/> is forced back to
    /// <see cref="JsonNumberHandling.Strict"/>, because <c>Web</c> sets <c>AllowReadingFromString</c>, which would bind
    /// <c>"5"</c> to an integer and so break SERDE-21's strict coercion. <see cref="JsonSerializerOptions.RespectNullableAnnotations"/>
    /// is on, so a JSON <c>null</c> for a non-nullable member is a failure rather than a silent <see langword="null"/>; a
    /// model for a loose server declares the member nullable.
    /// </para>
    /// <para>
    /// This governs the <i>default</i> configuration only. A caller who passes their own options (or a context with its
    /// own <c>[JsonSourceGenerationOptions]</c>) to a constructor keeps those choices: the serde injects Tristate wiring and
    /// nothing else.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfoResolver"/> is <see langword="null"/>.</exception>
    public static JsonSerializerOptions CreateDefaultOptions(IJsonTypeInfoResolver typeInfoResolver)
    {
        ArgumentNullException.ThrowIfNull(typeInfoResolver);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            TypeInfoResolver = typeInfoResolver,
        };
        options.AddTristateSupport();
        return options;
    }

    /// <inheritdoc/>
    public MediaType DefaultMediaType => CommonMediaTypes.ApplicationJsonUtf8;

    /// <inheritdoc/>
    public async ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var info = GetTypeInfo<T>(forSerialize: true);
        try
        {
            await JsonSerializer.SerializeAsync(destination, value, info, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new SerializationException($"Failed to serialize '{typeof(T)}' to JSON.", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var info = GetTypeInfo<T>(forSerialize: false);
        try
        {
            return await JsonSerializer.DeserializeAsync(source, info, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new DeserializationException($"Failed to deserialize JSON to '{typeof(T)}'.", ex);
        }
    }

    /// <summary>
    /// Deserializes a value of type <typeparamref name="T"/> from <paramref name="source"/>, synchronously, streaming: it
    /// does not materialise the payload (it overrides the seam's bounded default, P7a-13).
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="source">The stream to read; left open (SERDE-3).</param>
    /// <returns>The deserialized value, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="DeserializationException">Deserialization failed; the cause is chained (SERDE-9).</exception>
    /// <exception cref="IOException">The stream failed; it propagates unwrapped (SERDE-12).</exception>
    public T? Deserialize<T>(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var info = GetTypeInfo<T>(forSerialize: false);
        try
        {
            return JsonSerializer.Deserialize(source, info);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new DeserializationException($"Failed to deserialize JSON to '{typeof(T)}'.", ex);
        }
    }

    /// <inheritdoc/>
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var info = GetTypeInfo<T>(forSerialize: true);
        using var writer = new Utf8JsonWriter(destination);
        try
        {
            JsonSerializer.Serialize(writer, value, info);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new SerializationException($"Failed to serialize '{typeof(T)}' to JSON.", ex);
        }
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8)
    {
        var info = GetTypeInfo<T>(forSerialize: false);
        try
        {
            return JsonSerializer.Deserialize(utf8, info);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new DeserializationException($"Failed to deserialize JSON to '{typeof(T)}'.", ex);
        }
    }

    private JsonTypeInfo<T> GetTypeInfo<T>(bool forSerialize)
    {
        try
        {
            if (_options.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> info)
            {
                return info;
            }
        }
        catch (NotSupportedException)
        {
            // Source-generated contexts throw NotSupportedException for unregistered types
            // instead of returning null; map to the SDK exception type below.
        }

        return forSerialize
            ? throw new SerializationException(TypeInfoMessage<T>())
            : throw new DeserializationException(TypeInfoMessage<T>());
    }

    private static string TypeInfoMessage<T>() =>
        $"No JsonTypeInfo is registered for '{typeof(T)}'. Add it to a source-generated "
        + "JsonSerializerContext supplied to SystemTextJsonSerde.";
}

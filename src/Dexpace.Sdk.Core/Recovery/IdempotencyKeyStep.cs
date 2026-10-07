// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// A request step that stamps an idempotency key on write requests (RECOV-32).
/// </summary>
/// <remarks>
/// By default it applies to <c>POST</c>, <c>PUT</c> and <c>PATCH</c>, writes <c>Idempotency-Key</c>, and respects an
/// existing header: a request that already carries it passes through untouched and <see cref="KeyStrategy"/> is not
/// invoked. With <see cref="RespectExisting"/> set to <see langword="false"/> the header is overwritten. The strategy runs
/// at most once per applicable request; other methods pass through by reference. The step holds no per-call state
/// (RECOV-14).
/// </remarks>
public sealed class IdempotencyKeyStep : IRequestStep
{
    private static readonly FrozenSet<Method> s_defaultMethods = new[] { Method.Post, Method.Put, Method.Patch }.ToFrozenSet();

    private IReadOnlySet<Method> _methods = s_defaultMethods;

    /// <summary>The header to stamp; defaults to <c>Idempotency-Key</c>.</summary>
    public HttpHeaderName HeaderName { get; init; } = HttpHeaderName.WellKnown.IdempotencyKey;

    /// <summary>The methods that receive a key; defaults to <c>POST</c>, <c>PUT</c> and <c>PATCH</c>. Copied on assignment.</summary>
    public IReadOnlySet<Method> Methods
    {
        get => _methods;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _methods = value.ToFrozenSet();
        }
    }

    /// <summary>Whether a request already carrying the header is left untouched; defaults to <see langword="true"/>.</summary>
    public bool RespectExisting { get; init; } = true;

    /// <summary>
    /// Mints a key; defaults to <c>Guid.NewGuid().ToString("D")</c>. It must be thread-safe, and must not return
    /// <see langword="null"/>, empty or whitespace (that throws <see cref="InvalidOperationException"/>).
    /// </summary>
    public Func<string> KeyStrategy { get; init; } = static () => Guid.NewGuid().ToString("D");

    /// <inheritdoc />
    public Request Apply(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Apply(request, KeyStrategy);
    }

    /// <inheritdoc />
    public ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Apply(request, cancellationToken));

    /// <summary>The logic, with the key source injected so the pipeline policy shares it (RECOV-32, P4b-17).</summary>
    /// <param name="request">The request.</param>
    /// <param name="keySource">Supplies the key; invoked only when the step applies.</param>
    /// <returns>The stamped request, or <paramref name="request"/> by reference when the step does not apply.</returns>
    internal Request Apply(Request request, Func<string> keySource)
    {
        if (!Methods.Contains(request.Method) || (RespectExisting && request.Headers.Contains(HeaderName)))
        {
            return request;
        }

        var key = keySource();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The key strategy of {GetType().FullName} returned null, empty or whitespace.");
        }

        return request.WithHeaders(request.Headers.Set(HeaderName, key));
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// A request step that stamps the SDK's client identity onto a header, by default <c>User-Agent</c> (RECOV-33).
/// </summary>
/// <remarks>
/// The tokens are joined by one space and trimmed. In <see cref="ClientIdentityMode.Append"/> mode (the default) the
/// line is composed after the <b>first</b> existing value and every other value is kept; with no existing value it is the
/// sole value, and an empty first value counts as absent (no leading space). In <see cref="ClientIdentityMode.Replace"/>
/// mode every existing value is overwritten. A blank line is a no-op that emits no header. The step holds no per-call
/// state, so one instance is safe to share (RECOV-14).
/// </remarks>
public sealed class ClientIdentityStep : IRequestStep
{
    /// <summary>Creates a step over a copy of <paramref name="tokens"/>.</summary>
    /// <param name="tokens">The identity tokens, joined by single spaces.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokens"/> is <see langword="null"/>.</exception>
    public ClientIdentityStep(IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Tokens = new ReadOnlyCollection<string>([.. tokens]);
    }

    /// <summary>The tokens, as a read-only copy.</summary>
    public IReadOnlyList<string> Tokens { get; }

    /// <summary>The header to write; defaults to <c>User-Agent</c>.</summary>
    public HttpHeaderName HeaderName { get; init; } = HttpHeaderName.WellKnown.UserAgent;

    /// <summary>How the line is composed; defaults to <see cref="ClientIdentityMode.Append"/>.</summary>
    public ClientIdentityMode Mode { get; init; } = ClientIdentityMode.Append;

    /// <inheritdoc />
    public Request Apply(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Compose(request, string.Join(' ', Tokens));
    }

    /// <inheritdoc />
    public ValueTask<Request> ApplyAsync(Request request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Apply(request, cancellationToken));

    /// <summary>Composes <paramref name="tokenLine"/> into the header; the one place the rule lives (policy and step).</summary>
    /// <param name="request">The request.</param>
    /// <param name="tokenLine">The line to compose; blank is a no-op.</param>
    /// <returns>The request carrying the header, or <paramref name="request"/> itself for a blank line.</returns>
    internal Request Compose(Request request, string? tokenLine)
    {
        var line = tokenLine?.Trim();
        if (string.IsNullOrEmpty(line))
        {
            return request;
        }

        var headers = request.Headers;
        var existing = Mode == ClientIdentityMode.Replace ? [] : headers.GetAll(HeaderName);
        if (existing.Count == 0)
        {
            return request.WithHeaders(headers.Set(HeaderName, line));
        }

        var first = existing[0];
        var composed = string.IsNullOrEmpty(first) ? line : first + " " + line;
        var result = headers.Set(HeaderName, composed);
        for (var i = 1; i < existing.Count; i++)
        {
            result = result.With(HeaderName, existing[i]);
        }

        return request.WithHeaders(result);
    }
}

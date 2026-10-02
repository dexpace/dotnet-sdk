// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Operations;

/// <summary>
/// The operation-input projection (SEAM-26): what generated code declares for one API operation, which
/// <see cref="BuildRequest(Uri)"/> assembles into a <see cref="Request"/> over a base address.
/// </summary>
/// <remarks>
/// <para>
/// A descriptor carries typed values, not a projection table: a <see cref="Http.Request.Query"/> (RFC 3986 by
/// construction), a <see cref="Http.Common.Headers"/> (validated when built) and a <see cref="RequestBody"/>. The typing
/// lives in the generated method's signature. Formatting a non-string path value (an <see cref="int"/>, a
/// <see cref="DateTimeOffset"/>) is the generator's job, with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
/// (<c>CA1305</c>): <see cref="PathParameters"/> holds strings. <see cref="Body"/> is carried by reference and never
/// encoded here.
/// </para>
/// <para>
/// <see cref="Method"/> and <see cref="PathTemplate"/> are <see langword="required"/>, so omitting one is a compile
/// error (design §10 <c>no-builder-objects</c>); a parameterless <c>GET</c> sets only those two. Validation lives in
/// the <see langword="init"/> accessors, so a <see langword="with"/> expression cannot bypass it. Equality compares
/// <see cref="PathParameters"/> by content and every other member by its own <c>Equals</c>.
/// </para>
/// </remarks>
public sealed record OperationDescriptor
{
    private static readonly ImmutableDictionary<string, string> s_noParameters =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>The HTTP method of the request.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public required Method Method
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// The path template: literal text and <c>{name}</c> placeholders. May be empty, which leaves the base path
    /// untouched.
    /// </summary>
    /// <remarks>
    /// Braces must balance and a placeholder name is any non-empty text without braces, compared ordinally; a repeated
    /// name takes one value. Outside placeholders the text must be an RFC 3986 path (<c>pchar</c>, <c>/</c>,
    /// <c>%HH</c>), so <c>?</c>, <c>#</c>, a space and a bare <c>%</c> are rejected, and so is a literal segment that is a
    /// dot-segment (<c>.</c> or <c>..</c>, with <c>%2E</c> read as <c>.</c>): <c>System.Uri</c> would remove it and
    /// escape the base path.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The template is malformed; the message names it.</exception>
    public required string PathTemplate
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            Placeholders = PathTemplateSyntax.PlaceholderNames(PathTemplateSyntax.Parse(value, nameof(PathTemplate)));
            field = value;
        }
    }

    /// <summary>
    /// The path-parameter values by placeholder name; never <see langword="null"/>, empty by default. Keys are compared
    /// ordinally: an assigned dictionary built with another comparer is re-based on <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <remarks>
    /// A value of exactly <c>.</c> or <c>..</c>, or one holding a lone surrogate, is rejected, naming the key and never
    /// the value. Whether each placeholder has a value, and each value a placeholder, is checked at
    /// <see cref="BuildRequest(Uri)"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is not a usable path segment.</exception>
    public ImmutableDictionary<string, string> PathParameters
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (key, parameter) in value)
            {
                RequireUsablePathValue(key, parameter, nameof(PathParameters));
            }

            field = ReferenceEquals(value.KeyComparer, StringComparer.Ordinal)
                ? value
                : value.WithComparers(StringComparer.Ordinal);
        }
    } = s_noParameters;

    /// <summary>The operation's query, rendered by <see cref="Http.Request.Query.Encode"/>; empty by default.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public Query Query
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = Query.Empty;

    /// <summary>The headers carried onto the request as they are; empty by default.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public Headers Headers
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = Headers.Empty;

    /// <summary>The request body, carried by reference and never encoded; <see langword="null"/> for none.</summary>
    public RequestBody? Body { get; init; }

    /// <summary>
    /// A name for the operation (SEAM-28), or <see langword="null"/>. It never reaches the URL, the headers or the body.
    /// </summary>
    /// <remarks>Attaching it to the request's context chain is phase 4a's.</remarks>
    /// <exception cref="ArgumentException">The value is empty or whitespace; use <see langword="null"/> for none.</exception>
    public string? OperationId
    {
        get;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An operation id must be null or contain a non-blank value.", nameof(OperationId));
            }

            field = value;
        }
    }

    /// <summary>The distinct placeholder names of <see cref="PathTemplate"/>, computed once when it is set.</summary>
    internal ImmutableArray<string> Placeholders { get; private init; } = [];

    /// <summary>Returns a copy with the path parameter <paramref name="name"/> added or replaced.</summary>
    /// <param name="name">The placeholder name (ordinal).</param>
    /// <param name="value">The value, unencoded.</param>
    /// <returns>A new <see cref="OperationDescriptor"/>; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a usable path segment.</exception>
    public OperationDescriptor WithPathParameter(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return this with { PathParameters = PathParameters.SetItem(name, value) };
    }

    /// <summary>
    /// Assembles the <see cref="Request"/> over <paramref name="baseAddress"/> (SEAM-27): the path template with each
    /// placeholder replaced by its RFC 3986 encoded value, joined to the base path with exactly one separator, and the
    /// operation query appended to the base query.
    /// </summary>
    /// <param name="baseAddress">An absolute <c>http</c> or <c>https</c> URI with no fragment.</param>
    /// <returns>A new request carrying <see cref="Method"/>, <see cref="Headers"/> and the same <see cref="Body"/> instance.</returns>
    /// <remarks>
    /// The base is composed over its components, never with <c>new Uri(base, relative)</c>, which would resolve and
    /// collapse dot-segments. User info and port are kept; a dangling <c>&amp;</c> on the base query is dropped; an empty
    /// template leaves the base path untouched.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="baseAddress"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseAddress"/> is relative, not <c>http</c> or <c>https</c>, or carries a fragment (even an empty
    /// one); the message carries the base through <c>UrlRedactor</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A placeholder has no value, a path parameter names no placeholder, or a rendered segment is a dot-segment; the
    /// message names the placeholder, parameter or template and never a value.
    /// </exception>
    public Request BuildRequest(Uri baseAddress) => OperationUrlComposer.Build(this, baseAddress);

    /// <summary>
    /// Assembles the <see cref="Request"/> over <see cref="DexpaceClientOptions.BaseAddress"/>; see
    /// <see cref="BuildRequest(Uri)"/>.
    /// </summary>
    /// <param name="options">The client options whose <see cref="DexpaceClientOptions.BaseAddress"/> is read.</param>
    /// <returns>A new request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="DexpaceClientOptions.BaseAddress"/> is null, or fails the rules of <see cref="BuildRequest(Uri)"/>.
    /// </exception>
    public Request BuildRequest(DexpaceClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.BaseAddress is { } baseAddress
            ? BuildRequest(baseAddress)
            : throw new ArgumentException("DexpaceClientOptions.BaseAddress is not set.", nameof(options));
    }

    /// <summary>Equality: <see cref="PathParameters"/> by content, every other member by its own <c>Equals</c>.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool Equals(OperationDescriptor? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other)
            || (Method.Equals(other.Method)
                && string.Equals(PathTemplate, other.PathTemplate, StringComparison.Ordinal)
                && string.Equals(OperationId, other.OperationId, StringComparison.Ordinal)
                && PathParametersEqual(PathParameters, other.PathParameters)
                && Query.Equals(other.Query)
                && Headers.Equals(other.Headers)
                && Equals(Body, other.Body));
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var parameterHash = 0;
        foreach (var (key, value) in PathParameters)
        {
            parameterHash += HashCode.Combine(key, value);
        }

        return HashCode.Combine(Method, PathTemplate, OperationId, parameterHash, Query, Headers, Body);
    }

    /// <summary>Method and template only, such as <c>GET /pets/{id}</c>; never a value, the query, a header or the body.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Method.Name} {PathTemplate}";

    private static bool PathParametersEqual(ImmutableDictionary<string, string> left, ImmutableDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var theirs) || !string.Equals(value, theirs, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireUsablePathValue(string key, string value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentException($"The path parameter '{key}' has a null value.", paramName);
        }

        // "." and ".." survive RFC 3986 encoding (both are unreserved) and System.Uri removes them as dot-segments,
        // whatever the encoding, so no encoding can keep them literal (design §11 item 35).
        if (value is "." or "..")
        {
            throw new ArgumentException($"The value of path parameter '{key}' is a dot-segment and would escape the base path.", paramName);
        }

        if (PathTemplateSyntax.HasLoneSurrogate(value))
        {
            throw new ArgumentException($"The value of path parameter '{key}' contains a lone UTF-16 surrogate, which has no UTF-8 form.", paramName);
        }
    }
}

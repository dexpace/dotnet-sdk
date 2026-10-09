// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// A single page of results returned by a paginated operation: a value, not a resource.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Values"/> holds the page's items, an immutable snapshot taken before the page is yielded.
/// <see cref="Status"/>, <see cref="Headers"/> and <see cref="Request"/> are immutable values too, so a page can be
/// retained and read forever (PAGE-2).
/// </para>
/// <para>
/// A page owns no response and is not <see cref="IDisposable"/>: the engine reads the body, parses it and closes the
/// response <em>before</em> it yields the page, so nothing is live while the consumer holds one (PAGE-3, PAGE-11,
/// PAGE-12; design section 10 entry 17). The class is sealed and not a record, because value equality over an
/// <see cref="IReadOnlyList{T}"/> would be reference equality and mislead (P7c-19).
/// </para>
/// <para>
/// <b>Breaking:</b> the constructor took <c>(values, status, headers)</c>; it now also requires the
/// <see cref="Request"/> the walk sent for the page (PAGE-2, P7c-7).
/// </para>
/// </remarks>
public sealed class Page<T>
{
    /// <summary>Creates a page.</summary>
    /// <param name="values">The items on this page.</param>
    /// <param name="status">The HTTP status of the response that produced this page.</param>
    /// <param name="headers">The HTTP response headers for this page.</param>
    /// <param name="request">The request the walk sent for this page.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/>, <paramref name="headers"/> or <paramref name="request"/> is <see langword="null"/>.</exception>
    public Page(IReadOnlyList<T> values, Status status, Headers headers, Request request)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(request);
        Values = values;
        Status = status;
        Headers = headers;
        Request = request;
    }

    /// <summary>The items on this page; never <see langword="null"/>.</summary>
    public IReadOnlyList<T> Values { get; }

    /// <summary>The HTTP status code of the response that produced this page.</summary>
    public Status Status { get; }

    /// <summary>The HTTP response headers for this page.</summary>
    public Headers Headers { get; }

    /// <summary>
    /// The request the walk sent for this page, before the pipeline ran (PAGE-2, P7c-7). It carries no credential
    /// the authorization policies stamped, so a retained page holds no secret (design fact 10).
    /// </summary>
    public Request Request { get; }
}

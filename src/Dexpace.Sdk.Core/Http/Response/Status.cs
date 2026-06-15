// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// An HTTP response status code with an optional human-readable name.
/// </summary>
/// <remarks>
/// Two <see cref="Status"/> values are equal when their <see cref="Code"/>s are equal, so
/// <c>Status.FromCode(200) == Status.Ok</c>. Canonical codes carry a <see cref="Name"/>;
/// unrecognised codes carry <see langword="null"/>.
/// </remarks>
public readonly record struct Status
{
    private Status(int code, string? name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>The numeric status code as it appears on the wire.</summary>
    public int Code { get; }

    /// <summary>The canonical name (e.g. <c>OK</c>), or <see langword="null"/> for unknown codes.</summary>
    public string? Name { get; }

    /// <summary>True when the code is informational (100–199).</summary>
    public bool IsInformational => Code is >= 100 and <= 199;

    /// <summary>True when the code is in the 2xx success range.</summary>
    public bool IsSuccess => Code is >= 200 and <= 299;

    /// <summary>True when the code is a redirect (3xx).</summary>
    public bool IsRedirect => Code is >= 300 and <= 399;

    /// <summary>True when the code is a client error (4xx).</summary>
    public bool IsClientError => Code is >= 400 and <= 499;

    /// <summary>True when the code is a server error (5xx).</summary>
    public bool IsServerError => Code is >= 500 and <= 599;

    /// <summary>
    /// Returns the <see cref="Status"/> for <paramref name="code"/>. Canonical codes resolve to a
    /// cached instance with a populated <see cref="Name"/>; others get a name-less instance.
    /// </summary>
    /// <param name="code">The numeric status code.</param>
    /// <returns>A <see cref="Status"/> wrapping the code.</returns>
    public static Status FromCode(int code) =>
        KnownByCode.TryGetValue(code, out var known) ? known : new Status(code, null);

    /// <summary>Equality over <see cref="Code"/> only.</summary>
    public bool Equals(Status other) => Code == other.Code;

    /// <inheritdoc/>
    public override int GetHashCode() => Code;

    /// <summary>Returns <c>Name(Code)</c> for known codes, otherwise <c>HTTP Code</c>.</summary>
    public override string ToString() => Name is not null ? $"{Name}({Code})" : $"HTTP {Code}";

    // Informational (1xx)
    /// <summary>100 Continue.</summary>
    public static Status Continue { get; } = new(100, "CONTINUE");

    /// <summary>101 Switching Protocols.</summary>
    public static Status SwitchingProtocols { get; } = new(101, "SWITCHING_PROTOCOLS");

    // Success (2xx)
    /// <summary>200 OK.</summary>
    public static Status Ok { get; } = new(200, "OK");

    /// <summary>201 Created.</summary>
    public static Status Created { get; } = new(201, "CREATED");

    /// <summary>202 Accepted.</summary>
    public static Status Accepted { get; } = new(202, "ACCEPTED");

    /// <summary>204 No Content.</summary>
    public static Status NoContent { get; } = new(204, "NO_CONTENT");

    /// <summary>206 Partial Content.</summary>
    public static Status PartialContent { get; } = new(206, "PARTIAL_CONTENT");

    // Redirection (3xx)
    /// <summary>301 Moved Permanently.</summary>
    public static Status MovedPermanently { get; } = new(301, "MOVED_PERMANENTLY");

    /// <summary>302 Found.</summary>
    public static Status Found { get; } = new(302, "FOUND");

    /// <summary>304 Not Modified.</summary>
    public static Status NotModified { get; } = new(304, "NOT_MODIFIED");

    /// <summary>307 Temporary Redirect.</summary>
    public static Status TemporaryRedirect { get; } = new(307, "TEMPORARY_REDIRECT");

    /// <summary>308 Permanent Redirect.</summary>
    public static Status PermanentRedirect { get; } = new(308, "PERMANENT_REDIRECT");

    // Client error (4xx)
    /// <summary>400 Bad Request.</summary>
    public static Status BadRequest { get; } = new(400, "BAD_REQUEST");

    /// <summary>401 Unauthorized.</summary>
    public static Status Unauthorized { get; } = new(401, "UNAUTHORIZED");

    /// <summary>403 Forbidden.</summary>
    public static Status Forbidden { get; } = new(403, "FORBIDDEN");

    /// <summary>404 Not Found.</summary>
    public static Status NotFound { get; } = new(404, "NOT_FOUND");

    /// <summary>409 Conflict.</summary>
    public static Status Conflict { get; } = new(409, "CONFLICT");

    /// <summary>412 Precondition Failed.</summary>
    public static Status PreconditionFailed { get; } = new(412, "PRECONDITION_FAILED");

    /// <summary>429 Too Many Requests.</summary>
    public static Status TooManyRequests { get; } = new(429, "TOO_MANY_REQUESTS");

    // Server error (5xx)
    /// <summary>500 Internal Server Error.</summary>
    public static Status InternalServerError { get; } = new(500, "INTERNAL_SERVER_ERROR");

    /// <summary>502 Bad Gateway.</summary>
    public static Status BadGateway { get; } = new(502, "BAD_GATEWAY");

    /// <summary>503 Service Unavailable.</summary>
    public static Status ServiceUnavailable { get; } = new(503, "SERVICE_UNAVAILABLE");

    /// <summary>504 Gateway Timeout.</summary>
    public static Status GatewayTimeout { get; } = new(504, "GATEWAY_TIMEOUT");

    private static readonly Dictionary<int, Status> KnownByCode = new[]
    {
        Continue, SwitchingProtocols,
        Ok, Created, Accepted, NoContent, PartialContent,
        MovedPermanently, Found, NotModified, TemporaryRedirect, PermanentRedirect,
        BadRequest, Unauthorized, Forbidden, NotFound, Conflict, PreconditionFailed, TooManyRequests,
        InternalServerError, BadGateway, ServiceUnavailable, GatewayTimeout,
    }.ToDictionary(s => s.Code);
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// Cached <see cref="MediaType"/> instances for the formats the SDK handles most often.
/// </summary>
public static class CommonMediaTypes
{
    /// <summary><c>application/json</c>.</summary>
    public static MediaType ApplicationJson { get; } = MediaType.Of("application", "json");

    /// <summary><c>application/json; charset=utf-8</c>.</summary>
    public static MediaType ApplicationJsonUtf8 { get; } =
        MediaType.Of("application", "json", new Dictionary<string, string> { ["charset"] = "utf-8" });

    /// <summary><c>application/octet-stream</c>.</summary>
    public static MediaType ApplicationOctetStream { get; } = MediaType.Of("application", "octet-stream");

    /// <summary><c>application/x-www-form-urlencoded</c>.</summary>
    public static MediaType ApplicationFormUrlEncoded { get; } =
        MediaType.Of("application", "x-www-form-urlencoded");

    /// <summary><c>text/plain</c>.</summary>
    public static MediaType TextPlain { get; } = MediaType.Of("text", "plain");

    /// <summary><c>text/event-stream</c> (Server-Sent Events).</summary>
    public static MediaType TextEventStream { get; } = MediaType.Of("text", "event-stream");

    /// <summary><c>application/jsonl</c> (JSON Lines).</summary>
    public static MediaType ApplicationJsonLines { get; } = MediaType.Of("application", "jsonl");

    /// <summary><c>multipart/form-data</c>.</summary>
    public static MediaType MultipartFormData { get; } = MediaType.Of("multipart", "form-data");
}

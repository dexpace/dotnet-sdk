// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// <see cref="ResponseHandlers.DeserializeOnSuccess{T}"/>: 2xx decodes, 400 to 599 maps to an
/// <see cref="HttpResponseException"/>, anything else fails with a status-led <see cref="DeserializationException"/>
/// (SERDE-28, P7a-18, P7a-19, P7a-23).
/// </summary>
/// <remarks>
/// <para>
/// The error branch is the one capture site core already has: <see cref="ErrorBodyBuffer"/> drains at most
/// <see cref="Response.MaxBufferedErrorBytes"/> and releases the live response itself, so this branch adds no second dispose, and
/// <see cref="ErrorMapping"/> turns the buffered response into the exception.
/// </para>
/// <para>
/// The third branch's message leads with the code, copies <c>ETag</c> raw (an opaque validator: parsing it on an error path
/// would turn a diagnostic into a second failure), and passes <c>Location</c> through <see cref="UrlRedactor"/> after resolving it
/// against the request URL. Both headers are read raw, never through <c>HTTP-48</c>'s validating helpers (P7a-23), and a control
/// character in any interpolated part becomes <c>?</c> so the message stays on one line.
/// </para>
/// </remarks>
internal sealed class SuccessDeserializingHandler<T>(ISerde serde) : IResponseHandler<T>
{
    private const string Unparseable = "<unparseable>";

    // An instance field: a static cannot capture the primary-constructor parameter.
    private readonly DeserializingHandler<T> _decode = new(serde);

    public async ValueTask<T> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccess)
        {
            return await _decode.HandleAsync(response, cancellationToken).ConfigureAwait(false);
        }

        if (response.IsError)
        {
            var captured = await ErrorBodyBuffer.CaptureAsync(response, cancellationToken).ConfigureAwait(false);
            throw ErrorMapping.ToException(captured);
        }

        var failure = new DeserializationException(NotDecodableMessage(response));
        await Disposal.DisposeQuietlyAsync(response, failure).ConfigureAwait(false);
        throw failure;
    }

    /// <summary>Builds the third branch's message from raw parts (an internal seam for the defensive control-character rule).</summary>
    /// <param name="code">The status code.</param>
    /// <param name="reason">The reason phrase, or <see langword="null"/>.</param>
    /// <param name="eTag">The raw <c>ETag</c> value, or <see langword="null"/> when absent.</param>
    /// <param name="location">The raw <c>Location</c> value, or <see langword="null"/> when absent.</param>
    /// <param name="requestUrl">The URL a relative <paramref name="location"/> resolves against.</param>
    /// <returns>A one-line message.</returns>
    internal static string BuildMessage(int code, string? reason, string? eTag, string? location, Uri requestUrl)
    {
        var message = new StringBuilder();
        message.Append(code.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(reason))
        {
            message.Append(' ').Append(OneLine(reason));
        }

        message.Append(": expected a 2xx response to deserialize as '").Append(typeof(T)).Append("'.");
        if (eTag is not null)
        {
            message.Append(" ETag: ").Append(OneLine(eTag)).Append('.');
        }

        if (location is not null)
        {
            message.Append(" Location: ").Append(RedactLocation(OneLine(location), requestUrl)).Append('.');
        }

        return message.ToString();
    }

    private static string NotDecodableMessage(Response response) =>
        BuildMessage(
            response.Status.Code,
            response.ReasonPhrase ?? Humanize(response.Status.Name),
            response.Headers.Get("ETag"),
            response.Headers.Get("Location"),
            response.Request.Url);

    // The same location resolution the redirect policy uses, but total and redacting: nothing here may throw or leak.
    private static string RedactLocation(string location, Uri requestUrl) =>
        Uri.TryCreate(requestUrl, location, out var resolved) ? UrlRedactor.Default.Redact(resolved) : Unparseable;

    // A control character (CR, LF, HTAB and the rest) would split or garble a log line; headers cannot deliver CR or LF, so
    // this is defensive for the first two and a real rule for HTAB.
    private static string OneLine(string value) =>
        value.Any(char.IsControl) ? string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsControl(source[i]) ? '?' : source[i];
            }
        }) : value;

    // "NOT_MODIFIED" reads as "Not Modified"; an unnamed status (103) has no reason to show.
    private static string? Humanize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var words = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(static word => string.Concat(char.ToUpperInvariant(word[0]), word[1..].ToLowerInvariant())));
    }
}

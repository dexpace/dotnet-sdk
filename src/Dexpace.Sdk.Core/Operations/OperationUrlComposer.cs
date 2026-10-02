// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Operations;

/// <summary>
/// The assembly behind <see cref="OperationDescriptor.BuildRequest(Uri)"/> (SEAM-27; design §3.5): composes over the
/// base address's components, never with <c>new Uri(base, relative)</c>, and encodes every value through the one
/// <c>Rfc3986</c> encoder.
/// </summary>
internal static class OperationUrlComposer
{
    internal static Request Build(OperationDescriptor operation, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        RequireUsableBase(baseAddress);

        var rendered = Render(operation);
        var basePath = baseAddress.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        var path = rendered.Length == 0
            ? basePath
            : DropOneTrailingSlash(basePath) + (rendered[0] == '/' ? string.Empty : "/") + rendered;

        var query = JoinQueries(
            baseAddress.GetComponents(UriComponents.Query, UriFormat.UriEscaped),
            operation.Query.Encode());

        var text = new StringBuilder(baseAddress.GetLeftPart(UriPartial.Authority));
        if (path.Length == 0 || path[0] != '/')
        {
            text.Append('/');
        }

        text.Append(path);
        if (query.Length > 0)
        {
            text.Append('?').Append(query);
        }

        return new Request(operation.Method, new Uri(text.ToString(), UriKind.Absolute), operation.Headers, operation.Body);
    }

    // The base is rejected with its text carried through UrlRedactor (the 2a ruling that URL errors carry the
    // redacted input); an empty fragment is still a fragment (design fact 5).
    private static void RequireUsableBase(Uri baseAddress)
    {
        var usable = baseAddress.IsAbsoluteUri
            && (baseAddress.Scheme == Uri.UriSchemeHttp || baseAddress.Scheme == Uri.UriSchemeHttps)
            && baseAddress.Fragment.Length == 0;
        if (!usable)
        {
            throw new ArgumentException(
                "The base address must be an absolute http or https URI with no fragment: "
                + UrlRedactor.Default.Redact(baseAddress),
                nameof(baseAddress));
        }
    }

    // Literals as written, each placeholder replaced by its encoded value (one segment each); never a value in an error.
    private static string Render(OperationDescriptor operation)
    {
        var template = operation.PathTemplate;
        foreach (var name in operation.PathParameters.Keys)
        {
            if (!operation.Placeholders.Contains(name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The path parameter '{name}' names no placeholder of the template '{template}'.");
            }
        }

        var rendered = new StringBuilder();
        foreach (var part in PathTemplateSyntax.Parse(template, nameof(operation)))
        {
            if (!part.IsPlaceholder)
            {
                rendered.Append(part.Text);
            }
            else if (operation.PathParameters.TryGetValue(part.Text, out var value))
            {
                rendered.Append(Rfc3986.EncodeComponent(value));
            }
            else
            {
                throw new InvalidOperationException(
                    $"The placeholder '{part.Text}' of the template '{template}' has no value.");
            }
        }

        var path = rendered.ToString();
        RejectRenderedDotSegments(path, template);
        return path;
    }

    // Reachable only by concatenating literal dots with an empty value ("/.{a}." with a = ""), design position K.
    private static void RejectRenderedDotSegments(string rendered, string template)
    {
        foreach (var range in rendered.AsSpan().Split('/'))
        {
            if (PathTemplateSyntax.IsDotSegment(rendered.AsSpan(range)))
            {
                throw new InvalidOperationException(
                    $"The template '{template}' renders a dot-segment ('.' or '..') that would escape the base path.");
            }
        }
    }

    // At most one separator is dropped, so an empty placeholder value keeps its segment wherever it sits (SEAM-27).
    private static string DropOneTrailingSlash(string value) =>
        value.EndsWith('/') ? value[..^1] : value;

    private static string JoinQueries(string baseQuery, string operationQuery)
    {
        var kept = baseQuery.TrimEnd('&');
        if (kept.Length == 0)
        {
            return operationQuery;
        }

        return operationQuery.Length == 0 ? kept : kept + "&" + operationQuery;
    }
}

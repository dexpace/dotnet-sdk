// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// One part of a <see cref="RequestBody.Multipart"/> body: a field name, an optional file name and the part's own body
/// (HTTP-51, design P3b-8).
/// </summary>
/// <remarks>
/// This is a sealed class with a validating constructor, not a record: <c>with</c> would bypass validation (the
/// <see cref="Request"/> precedent). <see cref="Name"/> and <see cref="FileName"/> reject CR, LF, every other C0 control
/// character and DEL (<see cref="ArgumentException"/>, naming the code point and never echoing the value), and text that
/// is not valid Unicode (a lone surrogate); <c>"</c> and <c>\</c> are backslash-escaped inside the quoted string and
/// non-ASCII is written as UTF-8. An empty <see cref="Name"/> is allowed. The part never disposes its body.
/// </remarks>
public sealed class MultipartPart
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Creates a part.</summary>
    /// <param name="name">The form field name; may be empty.</param>
    /// <param name="body">The part's body; its media type becomes the part's <c>Content-Type</c> when present.</param>
    /// <param name="fileName">The file name, or <see langword="null"/> for a plain field.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="body"/> is null.</exception>
    /// <exception cref="ArgumentException">A name holds a control character or a lone surrogate.</exception>
    public MultipartPart(string name, RequestBody body, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(body);
        Validate(name, nameof(name));
        if (fileName is not null)
        {
            Validate(fileName, nameof(fileName));
        }

        Name = name;
        FileName = fileName;
        Body = body;
    }

    /// <summary>The form field name.</summary>
    public string Name { get; }

    /// <summary>The file name, or <see langword="null"/>.</summary>
    public string? FileName { get; }

    /// <summary>The part's body.</summary>
    public RequestBody Body { get; }

    private static void Validate(string text, string paramName)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x20 || c == 0x7F)
            {
                throw new ArgumentException(
                    $"The value contains the control character {HeaderSyntax.CodePoint(c)} at index {i}; "
                    + "a multipart name must not hold CR, LF or any other control character.",
                    paramName);
            }
        }

        try
        {
            _ = s_strictUtf8.GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("The value is not valid Unicode text (it holds a lone surrogate).", paramName);
        }
    }
}

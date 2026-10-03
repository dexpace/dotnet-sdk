// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.IO;

/// <summary>Which byte sequences end a line for <see cref="Utf8LineReader"/> (design position G).</summary>
internal enum LineTerminators
{
    /// <summary>
    /// <c>\n</c> and <c>\r\n</c> terminate; a lone <c>\r</c> is content, decided by one byte of lookahead even across a
    /// refill (IO-14).
    /// </summary>
    LineFeedOrCrLf,

    /// <summary>
    /// <c>\r</c>, <c>\n</c> and <c>\r\n</c> all terminate (the WHATWG event-stream rule, design §7.2). A <c>\r</c>
    /// terminates immediately and the reader skips one following <c>\n</c> on the next read, so a CR-terminated line is
    /// never held for the next network chunk.
    /// </summary>
    Whatwg,
}

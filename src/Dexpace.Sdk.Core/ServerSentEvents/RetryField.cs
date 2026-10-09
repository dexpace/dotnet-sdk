// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>The grammar of the <c>retry</c> field's value (SSE-11, P7b-6).</summary>
/// <remarks>
/// Only ASCII digits are accepted (<see cref="char.IsAsciiDigit"/>, never <see cref="char.IsDigit(char)"/>, which would
/// admit Arabic-Indic and fullwidth digits). The value is accumulated in a <see cref="long"/> with an early exit the
/// moment it passes the documented cap of <see cref="int.MaxValue"/> milliseconds, so a thousand-digit value is rejected
/// in time linear in its length without overflow and leading zeros do not count toward the cap. An empty, signed, spaced,
/// fractional or over-cap value is not a retry at all: the caller ignores it.
/// </remarks>
internal static class RetryField
{
    /// <summary>Parses a <c>retry</c> value into a whole-millisecond duration.</summary>
    /// <param name="value">The field value, after the single leading space has been stripped.</param>
    /// <param name="retry">The duration, or <see cref="TimeSpan.Zero"/> when the value is not a valid retry.</param>
    /// <returns><see langword="true"/> when the value is one or more ASCII digits not above the cap.</returns>
    internal static bool TryParse(string value, out TimeSpan retry)
    {
        retry = TimeSpan.Zero;
        if (value.Length == 0)
        {
            return false;
        }

        long milliseconds = 0;
        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            milliseconds = (milliseconds * 10) + (c - '0');
            if (milliseconds > ServerSentEvent.MaxRetryMilliseconds)
            {
                return false;
            }
        }

        retry = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Diagnostics;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>The two refusal messages. Every URL goes through <see cref="UrlRedactor"/> (XCUT-19).</summary>
internal static class RedirectMessages
{
    internal static string Downgrade(int status, Uri from, Uri to) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Refused to follow the {status} redirect from '{UrlRedactor.Default.Redact(from)}' to '{UrlRedactor.Default.Redact(to)}': it downgrades https to http. Set RedirectOptions.AllowHttpsToHttpDowngrade to permit it.");

    internal static string NotReplayable(int status, Uri to) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Refused to follow the {status} redirect to '{UrlRedactor.Default.Redact(to)}': the request body is not replayable, and a redirect re-sends it. Buffer the body with RequestBody.ToReplayableAsync() before sending.");
}

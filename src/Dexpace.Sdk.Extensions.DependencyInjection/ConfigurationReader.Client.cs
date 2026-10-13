// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    private const string BaseAddressRule = "not an absolute http or https URI without a fragment";

    private static DexpaceClientOptions ReadClient(ConfigSection section)
    {
        var binder = new RecordBinder<DexpaceClientOptions>(section, new DexpaceClientOptions());

        // The record's accessor would echo the (redacted) URL; the rule here names the requirement only.
        binder.Bind<Uri>("BaseAddress", ScalarGrammar.TryParseAbsoluteUri, static (r, v) => r with { BaseAddress = v }, BaseAddressRule);

        // An accessor checks only for null, so a control character would pass start-up and fail every call (F15).
        binder.Bind<string>("UserAgent", ParseUserAgent, static (r, v) => r with { UserAgent = v });
        binder.OptionalDuration("OverallTimeout", static (r, v) => r with { OverallTimeout = v });
        binder.OptionalDuration("AttemptTimeout", static (r, v) => r with { AttemptTimeout = v });
        return binder.Value with
        {
            Retry = ReadRetry(section),
            Redirect = ReadRedirect(section),
            Logging = ReadLogging(section),
        };
    }

    private static bool ParseUserAgent(string text, out string value, out string? rule)
    {
        value = text;
        rule = HttpHeaderSyntax.IsValidOutboundValue(text) ? null : "must be a valid header value (printable ASCII, no control characters)";
        return rule is null;
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    private static RetryOptions ReadRetry(ConfigSection section)
    {
        var binder = new RecordBinder<RetryOptions>(section, new RetryOptions());
        binder.Int("Retry:MaxRetryAttempts", static (r, v) => r with { MaxRetryAttempts = v });
        binder.Duration("Retry:BaseDelay", static (r, v) => r with { BaseDelay = v });
        binder.Duration("Retry:MaxDelay", static (r, v) => r with { MaxDelay = v });
        binder.OptionalDuration("Retry:FixedDelay", static (r, v) => r with { FixedDelay = v });
        binder.Number("Retry:Multiplier", static (r, v) => r with { Multiplier = v });
        binder.Number("Retry:Jitter", static (r, v) => r with { Jitter = v });
        binder.Bool("Retry:HonorRetryAfter", static (r, v) => r with { HonorRetryAfter = v });
        binder.Items<int>("Retry:RetryableStatusCodes", ScalarGrammar.TryParseInt32, static (r, v) => r with { RetryableStatusCodes = new HashSet<int>(v) });
        binder.Str("Retry:AttemptHeaderName", static (r, v) => r with { AttemptHeaderName = v });
        return binder.Value;
    }
}

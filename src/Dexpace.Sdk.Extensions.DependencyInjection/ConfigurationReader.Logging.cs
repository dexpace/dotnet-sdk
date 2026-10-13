// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    // OBS-35: the level is read by name only, from the caller's section; a number or an unknown name fails naming the key. The three
    // lists replace the record's defaults (V1: the binder appended). A name that is not a token never matches, so none is validated.
    private static HttpLoggingOptions ReadLogging(ConfigSection section)
    {
        var binder = new RecordBinder<HttpLoggingOptions>(section, new HttpLoggingOptions());
        binder.EnumName<HttpLogLevel>("Logging:Level", static (r, v) => r with { Level = v });
        binder.Int("Logging:BodyPreviewSize", static (r, v) => r with { BodyPreviewSize = v });
        binder.Items<string>("Logging:AllowedHeaderNames", ScalarGrammar.TryParseString, static (r, v) => r with { AllowedHeaderNames = v });
        binder.Items<string>("Logging:AllowedQueryParameters", ScalarGrammar.TryParseString, static (r, v) => r with { AllowedQueryParameters = v });
        binder.Items<string>("Logging:UrlValuedHeaderNames", ScalarGrammar.TryParseString, static (r, v) => r with { UrlValuedHeaderNames = v });
        binder.Bool("Logging:OmitDisallowedHeaders", static (r, v) => r with { OmitDisallowedHeaders = v });
        return binder.Value;
    }
}

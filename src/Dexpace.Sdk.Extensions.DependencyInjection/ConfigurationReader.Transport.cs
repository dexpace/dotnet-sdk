// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Http.SystemNet;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    private const string PooledLifetimeKey = "Transport:PooledConnectionLifetime";

    // Logger and TimeProvider come from the container, never from configuration (design: Transport:* keys).
    private static (SystemNetHttpClientOptions Options, TimeSpan PooledConnectionLifetime) ReadTransport(ConfigSection section)
    {
        var binder = new RecordBinder<SystemNetHttpClientOptions>(section, new SystemNetHttpClientOptions());
        binder.OptionalDuration("Transport:Timeout", static (r, v) => r with { Timeout = v });
        binder.EnumName<HeaderDropLogging>("Transport:HeaderDropLogging", static (r, v) => r with { HeaderDropLogging = v });
        binder.Long("Transport:ResendBufferLimit", static (r, v) => r with { ResendBufferLimit = v });
        var proxy = ReadProxy(section);
        var options = proxy is null ? binder.Value : binder.Value with { Proxy = proxy };
        return (options, ReadPooledLifetime(section));
    }

    // The primary handler's own setting (P9-13): a duration, or none for an infinite lifetime; the default is two minutes.
    private static TimeSpan ReadPooledLifetime(ConfigSection section)
    {
        if (!section.TryRead<TimeSpan?>(PooledLifetimeKey, ScalarGrammar.TryParseOptionalDuration, out var parsed))
        {
            return ClientConfiguration.s_defaultPooledConnectionLifetime;
        }

        if (parsed is null)
        {
            return Timeout.InfiniteTimeSpan;
        }

        if (parsed.Value <= TimeSpan.Zero)
        {
            section.Fail(PooledLifetimeKey, "must be greater than zero, or none for an infinite lifetime");
            return ClientConfiguration.s_defaultPooledConnectionLifetime;
        }

        return parsed.Value;
    }
}

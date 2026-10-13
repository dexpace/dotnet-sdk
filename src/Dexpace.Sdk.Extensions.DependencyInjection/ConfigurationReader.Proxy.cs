// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    private const string ProxyHostKey = "Transport:Proxy:Host";
    private const string ProxyPortKey = "Transport:Proxy:Port";
    private const string ProxyRequired = "required when any proxy key is present";

    // CFG-24's first layer (P9-15): explicit keys build a ProxyOptions, only when one of them is present. Nothing here reads the
    // environment, and no key switches that read on (CFG-28).
    private static ProxyOptions? ReadProxy(ConfigSection section)
    {
        string[] keys =
        [
            "Transport:Proxy:Type", ProxyHostKey, ProxyPortKey, "Transport:Proxy:NonProxyHosts",
            "Transport:Proxy:UserName", "Transport:Proxy:Password", "Transport:Proxy:BypassAll",
        ];

        // Every key is asked for (no short-circuit) so the reader's key set is stable.
        var present = keys.Select(section.Has).ToList().Contains(true);
        if (!present)
        {
            return null;
        }

        var hostOk = TryReadHost(section, out var host);
        var portOk = TryReadPort(section, out var port);
        var binder = new RecordBinder<ProxyOptions>(section, new ProxyOptions { Host = hostOk ? host : "placeholder.invalid", Port = portOk ? port : 1 });
        binder.EnumName<ProxyType>("Transport:Proxy:Type", static (r, v) => r with { Type = v });
        binder.Items<string>(
            "Transport:Proxy:NonProxyHosts",
            ScalarGrammar.TryParseString,
            static (r, v) => r with { NonProxyHosts = v },
            "not a valid bypass pattern");
        binder.Str("Transport:Proxy:UserName", static (r, v) => r with { UserName = v });
        binder.Str("Transport:Proxy:Password", static (r, v) => r with { Password = v });
        binder.Bool("Transport:Proxy:BypassAll", static (r, v) => r with { BypassAll = v });
        return hostOk && portOk ? binder.Value : null;
    }

    private static bool TryReadHost(ConfigSection section, out string host)
    {
        host = string.Empty;
        if (section.Text(ProxyHostKey) is not { } text)
        {
            section.Fail(ProxyHostKey, ProxyRequired);
            return false;
        }

        try
        {
            _ = new ProxyOptions { Host = text, Port = 1 };
            host = text;
            return true;
        }
        catch (ArgumentException ex)
        {
            section.Fail(ProxyHostKey, ConfigSection.Sanitise(ex));
            return false;
        }
    }

    private static bool TryReadPort(ConfigSection section, out int port)
    {
        port = 0;
        if (section.Text(ProxyPortKey) is null)
        {
            section.Fail(ProxyPortKey, ProxyRequired);
            return false;
        }

        if (!section.TryRead<int>(ProxyPortKey, ScalarGrammar.TryParseInt32, out var parsed))
        {
            return false;
        }

        try
        {
            _ = new ProxyOptions { Host = "placeholder.invalid", Port = parsed };
            port = parsed;
            return true;
        }
        catch (ArgumentException ex)
        {
            section.Fail(ProxyPortKey, ConfigSection.Sanitise(ex));
            return false;
        }
    }
}

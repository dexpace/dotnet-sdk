// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// Reads a client's <see cref="IConfigurationSection"/> into the option records (CFG-1, CFG-2, CFG-5 to CFG-7, CFG-38, OBS-35; P9-4). A
/// hand-written reader, because neither mode of the configuration binder can serve (phase 9 V1): it walks the keys of the
/// <see cref="KeyTable"/>, converts each with the package's own grammar and builds each record with <c>with</c> expressions. It never stops
/// at the first failure and never quotes a configured value.
/// </summary>
internal static partial class ConfigurationReader
{
    internal const string RootRule = "pass the client's own section, not the configuration root";

    /// <summary>Reads <paramref name="section"/>; a <see langword="null"/> section binds nothing and yields the defaults.</summary>
    /// <param name="section">The client's section, or <see langword="null"/>.</param>
    /// <returns>The records, the handler setting and every failure.</returns>
    internal static ClientConfiguration Read(IConfigurationSection? section) => Read(section, out _);

    /// <summary>Reads <paramref name="section"/> and reports the keys the reader asked for.</summary>
    /// <param name="section">The client's section, or <see langword="null"/>.</param>
    /// <param name="touched">The keys the reader asked for.</param>
    /// <returns>The records, the handler setting and every failure.</returns>
    internal static ClientConfiguration Read(IConfigurationSection? section, out IReadOnlyCollection<string> touched) => Read(section, null, out touched);

    /// <summary>
    /// Reads <paramref name="section"/> through the providers of <paramref name="registered"/> when it is the root that holds the section, so a blank value
    /// falls through to the next provider that has one (CFG-2, P11-17); otherwise the merged read stands.
    /// </summary>
    /// <param name="section">The client's section, or <see langword="null"/>.</param>
    /// <param name="registered">The <c>IConfiguration</c> registered in the container, or <see langword="null"/>.</param>
    /// <returns>The records, the handler setting and every failure.</returns>
    internal static ClientConfiguration Read(IConfigurationSection? section, IConfiguration? registered) => Read(section, registered, out _);

    /// <summary>Reads <paramref name="section"/> as the two-argument overload does and reports the keys the reader asked for.</summary>
    /// <param name="section">The client's section, or <see langword="null"/>.</param>
    /// <param name="registered">The <c>IConfiguration</c> registered in the container, or <see langword="null"/>.</param>
    /// <param name="touched">The keys the reader asked for.</param>
    /// <returns>The records, the handler setting and every failure.</returns>
    internal static ClientConfiguration Read(IConfigurationSection? section, IConfiguration? registered, out IReadOnlyCollection<string> touched)
    {
        var failures = new List<ConfigurationFailure>();
        if (section is { Path.Length: 0 })
        {
            failures.Add(new ConfigurationFailure(string.Empty, RootRule));
            touched = [];
            return Defaults(failures);
        }

        var providers = section is null ? null : ConfigSection.ProvidersFor(section, registered);
        var reader = new ConfigSection(section, failures, providers);
        var options = ReadClient(reader);
        var (transport, lifetime) = ReadTransport(reader);
        var credentials = ReadAuth(reader);
        if (section is not null)
        {
            CheckUnknownKeys(section, failures);
        }

        touched = reader.Touched;
        return new ClientConfiguration(options, transport, lifetime, credentials, failures);
    }

    private static ClientConfiguration Defaults(List<ConfigurationFailure> failures) =>
        new(new DexpaceClientOptions(), new SystemNetHttpClientOptions(), ClientConfiguration.s_defaultPooledConnectionLifetime, null, failures);

    // A key under the section that is not in the table fails naming the key in the configuration's own spelling (P9-9, R9, R10).
    private static void CheckUnknownKeys(IConfigurationSection section, List<ConfigurationFailure> failures)
    {
        foreach (var child in section.GetChildren())
        {
            Walk(child, child.Key, failures, section.Path);
        }
    }

    private static void Walk(IConfigurationSection node, string relative, List<ConfigurationFailure> failures, string sectionPath)
    {
        if (KeyTable.IsKey(relative, out var isList))
        {
            // A list key owns its indexed children; any other key has no children, so one that does is unknown below it.
            if (!isList)
            {
                WalkChildren(node, relative, failures, sectionPath);
            }

            return;
        }

        if (KeyTable.IsGroup(relative))
        {
            // A group holds keys, never a value; one set there would be dropped silently. The rule text is fixed: the value may be a credential. An empty one is absent (P9-7).
            if (node.Value is { } value && ScalarGrammar.AsciiTrim(value).Length > 0)
            {
                failures.Add(new ConfigurationFailure(sectionPath + ":" + relative, "a group of keys, not a value (set a key beneath it, e.g. " + relative + ":...)"));
            }

            WalkChildren(node, relative, failures, sectionPath);
            return;
        }

        failures.Add(new ConfigurationFailure(sectionPath + ":" + relative, "unknown key"));
    }

    private static void WalkChildren(IConfigurationSection node, string relative, List<ConfigurationFailure> failures, string sectionPath)
    {
        foreach (var child in node.GetChildren())
        {
            Walk(child, relative + ":" + child.Key, failures, sectionPath);
        }
    }
}

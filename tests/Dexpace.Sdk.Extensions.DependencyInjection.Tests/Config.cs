// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>In-memory configuration for the suite: no test reads the real environment but <c>EnvironmentNormalizationTests</c> (CFG-11).</summary>
internal static class Config
{
    /// <summary>The path every test section lives at.</summary>
    internal const string SectionPath = "Dexpace:GitHub";

    /// <summary>A root over one in-memory provider holding <paramref name="values"/> as given.</summary>
    internal static IConfigurationRoot Root(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>The client section at <see cref="SectionPath"/> holding <paramref name="values"/>, keyed relative to the section.</summary>
    internal static IConfigurationSection Section(params (string Key, string? Value)[] values) =>
        Root([.. values.Select(static v => (SectionPath + ":" + v.Key, v.Value))]).GetSection(SectionPath);

    /// <summary>A section over several in-memory providers, the last the highest (appsettings, environment, an override).</summary>
    internal static IConfigurationSection Layered(params (string Key, string? Value)[][] layers)
    {
        var builder = new ConfigurationBuilder();
        foreach (var layer in layers)
        {
            builder.AddInMemoryCollection(layer.Select(static v => new KeyValuePair<string, string?>(SectionPath + ":" + v.Key, v.Value)));
        }

        return builder.Build().GetSection(SectionPath);
    }

    /// <summary>Reads <paramref name="values"/> and returns the reader's result.</summary>
    internal static ClientConfiguration Read(params (string Key, string? Value)[] values) => ConfigurationReader.Read(Section(values));

    /// <summary>The failure texts of a read, in order.</summary>
    internal static string[] Failures(ClientConfiguration configuration) => [.. configuration.Failures.Select(static f => f.ToString())];
}

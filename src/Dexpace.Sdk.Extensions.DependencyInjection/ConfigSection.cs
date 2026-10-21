// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// A client's configuration section as the reader sees it: a lookup that trims ASCII whitespace and treats an empty or whitespace-only value
/// as absent (CFG-2, P9-7), a failure collector, and the list reader. The one place a key is turned into text (CFG-38).
/// </summary>
/// <remarks>
/// Given the <paramref name="providers"/> of the root the section was taken from, a blank value falls through to the next provider down that has a
/// value, which is what "absent" means for a layered configuration (CFG-2, P11-17); without them the merged read stands, where a blank value in the
/// highest provider hides the lower ones and the record default applies.
/// </remarks>
/// <param name="section">The client's section, or <see langword="null"/>.</param>
/// <param name="failures">Where a rejected key records its rule.</param>
/// <param name="providers">The providers of the root that holds <paramref name="section"/>, lowest first, or <see langword="null"/> to read the merged view.</param>
internal sealed class ConfigSection(IConfigurationSection? section, List<ConfigurationFailure> failures, IReadOnlyList<IConfigurationProvider>? providers = null)
{
    private readonly HashSet<string> _touched = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The keys the reader asked for, so a test can hold the reader to the key table.</summary>
    internal IReadOnlyCollection<string> Touched => _touched;

    /// <summary>The section's configuration path.</summary>
    internal string Path => section?.Path ?? string.Empty;

    /// <summary>The ASCII-trimmed value of <paramref name="key"/>, or <see langword="null"/> when it is absent, empty or whitespace-only.</summary>
    /// <param name="key">A key relative to the section.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    internal string? Text(string key)
    {
        _touched.Add(key);
        if (Raw(key) is not { } raw)
        {
            return null;
        }

        var trimmed = ScalarGrammar.AsciiTrim(raw);
        return trimmed.Length == 0 ? null : trimmed;
    }

    // The value of a key relative to the section: the merged read, or the first non-blank value walking the providers from the highest.
    private string? Raw(string key) => providers is null ? section?[key] : FirstNonBlank(providers, FullPath(key));

    // A list item's value, by the same rule as a scalar key (the item's path is the full path).
    private string? RawChild(IConfigurationSection child) => providers is null ? child.Value : FirstNonBlank(providers, child.Path);

    private static string? FirstNonBlank(IReadOnlyList<IConfigurationProvider> providers, string path)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            var provider = providers[i];
            if (provider is ChainedConfigurationProvider { Configuration: IConfigurationRoot inner })
            {
                // A chained configuration answers with its own merged read, so its blank top would hide its lower providers: walk them too.
                if (FirstNonBlank([.. inner.Providers], path) is { } fromInner)
                {
                    return fromInner;
                }

                continue;
            }

            if (provider.TryGet(path, out var value) && value is not null && ScalarGrammar.AsciiTrim(value).Length > 0)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// The providers to walk for <paramref name="section"/>, or <see langword="null"/> when the merged read stands: the registered configuration must be a
    /// root whose slice at the section's path enumerates exactly the pairs the section does, so a section from some other configuration is never read through
    /// providers it does not belong to (P11-17).
    /// </summary>
    /// <param name="section">The client's section.</param>
    /// <param name="registered">The <c>IConfiguration</c> registered in the container, or <see langword="null"/>.</param>
    /// <returns>The root's providers, lowest first, or <see langword="null"/>.</returns>
    internal static IReadOnlyList<IConfigurationProvider>? ProvidersFor(IConfigurationSection section, IConfiguration? registered)
    {
        if (registered is not IConfigurationRoot root || section.Path.Length == 0)
        {
            return null;
        }

        return SamePairs(section, root.GetSection(section.Path)) ? [.. root.Providers] : null;
    }

    private static bool SamePairs(IConfigurationSection left, IConfigurationSection right)
    {
        var expected = Pairs(left);
        var actual = Pairs(right);
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var (key, value) in expected)
        {
            if (!actual.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string?> Pairs(IConfigurationSection section)
    {
        var pairs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in section.AsEnumerable(makePathsRelative: false))
        {
            pairs[key] = value;
        }

        return pairs;
    }

    /// <summary>The children of a list key, in the configuration's order.</summary>
    private List<IConfigurationSection> Children(string key) =>
        section is null ? [] : [.. section.GetSection(key).GetChildren()];

    /// <summary>True when <paramref name="key"/> has a value or child keys; the proxy and credential readers build only what was asked for.</summary>
    /// <param name="key">A key relative to the section.</param>
    /// <returns>Whether the key is present.</returns>
    internal bool Has(string key) => Text(key) is not null || Children(key).Exists(static c => c.Value is not null || c.GetChildren().Any());

    /// <summary>Records a failure for <paramref name="key"/>.</summary>
    /// <param name="key">A key relative to the section.</param>
    /// <param name="rule">The rule it broke.</param>
    internal void Fail(string key, string rule) => failures.Add(new ConfigurationFailure(FullPath(key), rule));

    /// <summary>The full configuration path of <paramref name="key"/>.</summary>
    /// <param name="key">A key relative to the section.</param>
    /// <returns>The section's path plus the key.</returns>
    internal string FullPath(string key) => Path.Length == 0 ? key : Path + ":" + key;

    /// <summary>Reads <paramref name="key"/> through <paramref name="parse"/>; a parse failure is recorded under the key.</summary>
    internal bool TryRead<T>(string key, Parser<T> parse, out T value)
    {
        value = default!;
        if (Text(key) is not { } text)
        {
            return false;
        }

        if (parse(text, out value, out var rule))
        {
            return true;
        }

        Fail(key, rule ?? "invalid value");
        return false;
    }

    /// <summary>
    /// Reads a list key: child keys (<c>Key:0</c>, <c>Key:1</c>, in the configuration's order) or one comma-separated scalar, never both;
    /// <c>none</c> is the empty list. Items parse with <paramref name="element"/>; the list replaces the record's default (V1).
    /// </summary>
    internal bool TryReadList<T>(string key, Parser<T> element, out List<T> items)
    {
        items = [];
        var children = Children(key);
        var scalar = Text(key);
        if (children.Count > 0 && scalar is not null)
        {
            Fail(key, "give child keys or one comma-separated value, not both");
            return false;
        }

        List<string> raw;
        if (children.Count > 0)
        {
            raw = [];
            foreach (var child in children)
            {
                var text = RawChild(child) is { } value ? ScalarGrammar.AsciiTrim(value) : string.Empty;
                if (text.Length == 0)
                {
                    Fail(key, "a list item is empty");
                    return false;
                }

                raw.Add(text);
            }
        }
        else if (scalar is null)
        {
            return false;
        }
        else if (!ScalarGrammar.TrySplitList(scalar, out raw, out var splitRule))
        {
            Fail(key, splitRule!);
            return false;
        }

        return ParseItems(key, raw, element, items);
    }

    private bool ParseItems<T>(string key, List<string> raw, Parser<T> element, List<T> items)
    {
        for (var i = 0; i < raw.Count; i++)
        {
            if (!element(raw[i], out var item, out var rule))
            {
                Fail(key, "item " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + rule);
                return false;
            }

            items.Add(item);
        }

        return true;
    }

    /// <summary>The accessor-rejection text, safe to show: the first line, without the parameter suffix the runtime appends.</summary>
    /// <param name="exception">The accessor's rejection.</param>
    /// <returns>The rule text.</returns>
    internal static string Sanitise(ArgumentException exception)
    {
        var message = exception.Message;
        var newline = message.IndexOf('\n', StringComparison.Ordinal);
        if (newline >= 0)
        {
            message = message[..newline].TrimEnd('\r');
        }

        var suffix = message.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
        return suffix >= 0 ? message[..suffix] : message;
    }
}

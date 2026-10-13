// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// A client's configuration section as the reader sees it: a lookup that trims ASCII whitespace and treats an empty or whitespace-only value
/// as absent (CFG-2, P9-7), a failure collector, and the list reader. The one place a key is turned into text (CFG-38).
/// </summary>
internal sealed class ConfigSection(IConfigurationSection? section, List<ConfigurationFailure> failures)
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
        if (section?[key] is not { } raw)
        {
            return null;
        }

        var trimmed = ScalarGrammar.AsciiTrim(raw);
        return trimmed.Length == 0 ? null : trimmed;
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
                var text = child.Value is null ? string.Empty : ScalarGrammar.AsciiTrim(child.Value);
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

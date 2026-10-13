// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// CFG-38's drift guard (design C, R1/R2): a member added to an option record, or a key added without a member, fails here, not in a
/// deployment. Reflection is a test-only use.
/// </summary>
[Trait("Category", "Unit")]
public sealed class KeyTableTests
{
    private static readonly Type[] s_records =
    [
        typeof(DexpaceClientOptions), typeof(RetryOptions), typeof(RedirectOptions), typeof(HttpLoggingOptions),
        typeof(SystemNetHttpClientOptions), typeof(ProxyOptions), typeof(AuthCredentials),
    ];

    private static readonly Type[] s_credentials = [typeof(ApiKeyCredential), typeof(BasicCredential), typeof(DigestCredential)];

    [Fact]
    public void Every_public_init_property_is_in_the_key_table_or_on_the_code_only_list()
    {
        var missing = Missing(s_records, s_credentials, KeyTable.Entries, KeyTable.CodeOnly, KeyTable.Groups);

        Assert.True(missing.Count == 0, "missing from the key table: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_key_maps_to_a_real_member_or_is_handler_level()
    {
        foreach (var entry in KeyTable.Entries)
        {
            if (KeyTable.HandlerLevel.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                Assert.Null(entry.Target);
                continue;
            }

            var target = Assert.IsAssignableFrom<Type>(entry.Target);
            var exists = s_credentials.Contains(target)
                ? target.GetConstructors().Any(c => c.GetParameters().Any(p => p.Name == entry.Member))
                : target.GetProperty(entry.Member!) is not null;
            Assert.True(exists, entry.Key + " maps to no member");
        }
    }

    [Fact]
    public void A_new_member_would_fail_the_guard()
    {
        // The guard's own pin: a scratch record with one more init property, and a scratch credential class with one more constructor
        // parameter, are fed to the same check and reported.
        var missing = Missing([typeof(ScratchRecord)], [typeof(ScratchCredential)], KeyTable.Entries, KeyTable.CodeOnly, KeyTable.Groups);

        Assert.Contains(nameof(ScratchRecord) + "." + nameof(ScratchRecord.Extra), missing);
        Assert.Contains(nameof(ScratchCredential) + ".extra", missing);
    }

    [Fact]
    public void Every_key_is_documented_once()
    {
        var keys = KeyTable.Entries.Select(static e => e.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_reader_asks_for_exactly_the_keys_of_the_table()
    {
        _ = ConfigurationReader.Read(Config.Section(), out var touched);

        Assert.Equal(
            KeyTable.Entries.Select(static e => e.Key).Order(StringComparer.OrdinalIgnoreCase),
            touched.Order(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> Missing(
        Type[] records,
        Type[] credentials,
        IReadOnlyList<KeyEntry> entries,
        IReadOnlyList<(Type Type, string Member)> codeOnly,
        IReadOnlyList<(Type Type, string Member)> groups)
    {
        var missing = new List<string>();
        foreach (var type in records)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(IsInit))
            {
                var covered = entries.Any(e => e.Target == type && e.Member == property.Name)
                    || codeOnly.Contains((type, property.Name))
                    || groups.Contains((type, property.Name));
                if (!covered)
                {
                    missing.Add(type.Name + "." + property.Name);
                }
            }
        }

        foreach (var type in credentials)
        {
            foreach (var parameter in type.GetConstructors().SelectMany(static c => c.GetParameters()))
            {
                if (!entries.Any(e => e.Target == type && e.Member == parameter.Name))
                {
                    missing.Add(type.Name + "." + parameter.Name);
                }
            }
        }

        return missing;
    }

    private static bool IsInit(PropertyInfo property) =>
        property.SetMethod is { } setter
        && setter.ReturnParameter.GetRequiredCustomModifiers().Any(static m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private sealed record ScratchRecord
    {
        public int Extra { get; init; }
    }

    private sealed class ScratchCredential(string extra)
    {
        public string Extra { get; } = extra;
    }
}

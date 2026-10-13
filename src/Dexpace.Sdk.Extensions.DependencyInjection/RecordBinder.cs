// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// Builds one immutable record member by member (P9-4, P9-9): each key is parsed, applied with a <c>with</c> transform, and an accessor's
/// rejection becomes a failure under that key, so one boot reports every bad key.
/// </summary>
/// <typeparam name="TRecord">The record being built.</typeparam>
internal sealed class RecordBinder<TRecord>(ConfigSection section, TRecord start)
    where TRecord : class
{
    /// <summary>The record so far.</summary>
    internal TRecord Value { get; private set; } = start;

    /// <summary>Binds an integer key.</summary>
    internal void Int(string key, Func<TRecord, int, TRecord> set) => Bind<int>(key, ScalarGrammar.TryParseInt32, set);

    /// <summary>Binds a 64-bit integer key.</summary>
    internal void Long(string key, Func<TRecord, long, TRecord> set) => Bind<long>(key, ScalarGrammar.TryParseInt64, set);

    /// <summary>Binds a boolean key.</summary>
    internal void Bool(string key, Func<TRecord, bool, TRecord> set) => Bind<bool>(key, ScalarGrammar.TryParseBoolean, set);

    /// <summary>Binds a decimal key.</summary>
    internal void Number(string key, Func<TRecord, double, TRecord> set) => Bind<double>(key, ScalarGrammar.TryParseDouble, set);

    /// <summary>Binds a string key.</summary>
    internal void Str(string key, Func<TRecord, string, TRecord> set) => Bind<string>(key, ScalarGrammar.TryParseString, set);

    /// <summary>Binds a duration key.</summary>
    internal void Duration(string key, Func<TRecord, TimeSpan, TRecord> set) => Bind<TimeSpan>(key, ScalarGrammar.TryParseDuration, set);

    /// <summary>Binds a duration key that admits <c>none</c>.</summary>
    internal void OptionalDuration(string key, Func<TRecord, TimeSpan?, TRecord> set) => Bind<TimeSpan?>(key, ScalarGrammar.TryParseOptionalDuration, set);

    /// <summary>Binds an enum key, by name.</summary>
    internal void EnumName<TEnum>(string key, Func<TRecord, TEnum, TRecord> set)
        where TEnum : struct, System.Enum => Bind<TEnum>(key, ScalarGrammar.TryParseEnum, set);

    /// <summary>Binds a key through its own parser; <paramref name="fixedRule"/> replaces the accessor's text where it could echo the value.</summary>
    internal void Bind<T>(string key, Parser<T> parse, Func<TRecord, T, TRecord> set, string? fixedRule = null)
    {
        if (!section.TryRead(key, parse, out var parsed))
        {
            return;
        }

        Apply(key, parsed, set, fixedRule);
    }

    /// <summary>Binds a list key; the list replaces the record's default.</summary>
    internal void Items<T>(string key, Parser<T> element, Func<TRecord, List<T>, TRecord> set, string? fixedRule = null)
    {
        if (section.TryReadList(key, element, out var items))
        {
            Apply(key, items, set, fixedRule);
        }
    }

    /// <summary>Binds a list of HTTP method tokens through <see cref="Method.Of"/>, which never echoes its input (HTTP-20).</summary>
    internal void Methods(string key, Func<TRecord, List<Method>, TRecord> set) => Items(key, ParseMethod, set);

    private void Apply<T>(string key, T parsed, Func<TRecord, T, TRecord> set, string? fixedRule)
    {
        try
        {
            Value = set(Value, parsed);
        }
        catch (ArgumentException ex)
        {
            section.Fail(key, fixedRule ?? ConfigSection.Sanitise(ex));
        }
    }

    private static bool ParseMethod(string text, out Method value, out string? rule)
    {
        try
        {
            value = Method.Of(text);
            rule = null;
            return true;
        }
        catch (ArgumentException)
        {
            value = null!;
            rule = "not an HTTP method token";
            return false;
        }
    }
}

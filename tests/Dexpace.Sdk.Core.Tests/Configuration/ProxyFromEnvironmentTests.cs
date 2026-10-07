// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-24, CFG-25, CFG-26, CFG-27, CFG-28; P5a-15, P5a-16, R8, R10. Hermetic: every case goes through the
// Func<string, string?> overload over a dictionary and never touches the process environment.
[Trait("Category", "Unit")]
public sealed class ProxyFromEnvironmentTests
{
    public static TheoryData<string> AllCases() => ProxyResolutionCase.Names(layer: null);

    private static Func<string, string?> Lookup(Dictionary<string, string> env) =>
        key => env.TryGetValue(key, out var value) ? value : null;

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Resolves_every_vector(string name)
    {
        var vector = ProxyResolutionCase.Named(name);
        var logger = new RecordingLogger();

        var resolved = ProxyOptions.FromEnvironment(Lookup(vector.Env), logger);

        if (vector.Expected is { } expected)
        {
            Assert.NotNull(resolved);
            Assert.Equal(Enum.Parse<ProxyType>(expected.Type), resolved.Type);
            Assert.Equal(expected.Host, resolved.Host);
            Assert.Equal(expected.Port, resolved.Port);
            Assert.Equal(expected.UserName, resolved.UserName);
            Assert.Equal(expected.Password, resolved.Password);
            Assert.Equal(expected.NonProxyHosts, resolved.NonProxyHosts);
            Assert.False(resolved.BypassAll);
        }
        else
        {
            Assert.Null(resolved);
        }

        if (vector.Warning is { } warning)
        {
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(20, entry.EventId.Id);
            Assert.Equal("ProxyConfigurationIgnored", entry.EventId.Name);
            Assert.Contains(warning.Variable, entry.Message, StringComparison.Ordinal);
            Assert.Contains(warning.Reason, entry.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(logger.Entries);
        }
    }

    [Fact]
    public void A_rejection_warns_once_naming_the_variable_and_the_rule_and_never_the_value()
    {
        var logger = new RecordingLogger();
        var env = new Dictionary<string, string> { ["HTTPS_PROXY"] = "http://alice:hunter2@proxy.secret-host.example:99999" };

        var resolved = ProxyOptions.FromEnvironment(Lookup(env), logger);

        Assert.Null(resolved);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("HTTPS_PROXY", entry.Message, StringComparison.Ordinal);
        Assert.Contains("port", entry.Message, StringComparison.Ordinal);
        foreach (var secret in new[] { "hunter2", "alice", "secret-host", "99999", "http://" })
        {
            Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_well_formed_value_warns_about_nothing()
    {
        var logger = new RecordingLogger();

        var resolved = ProxyOptions.FromEnvironment(Lookup(new Dictionary<string, string> { ["HTTP_PROXY"] = "http://p:8080" }), logger);

        Assert.NotNull(resolved);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void A_NullLogger_still_returns_the_options()
    {
        var env = new Dictionary<string, string> { ["HTTPS_PROXY"] = "http://u:p@h:3128", ["NO_PROXY"] = "a.test" };

        var withNull = ProxyOptions.FromEnvironment(Lookup(env), NullLogger.Instance);
        var withRecording = ProxyOptions.FromEnvironment(Lookup(env), new RecordingLogger());

        Assert.NotNull(withNull);
        Assert.Equal(withRecording, withNull);
    }

    [Fact]
    public void A_throwing_logger_does_not_make_FromEnvironment_throw()
    {
        var env = new Dictionary<string, string> { ["HTTPS_PROXY"] = "ftp://x:21" };

        var resolved = ProxyOptions.FromEnvironment(Lookup(env), new RecordingLogger(new InvalidOperationException("logger broke")));

        Assert.Null(resolved);
    }

    [Fact]
    public void A_fatal_exception_from_the_logger_is_not_swallowed()
    {
        var env = new Dictionary<string, string> { ["HTTPS_PROXY"] = "ftp://x:21" };

#pragma warning disable CA2201 // A fatal exception is the point: the logger guard must let it through.
        var fatal = new OutOfMemoryException();
#pragma warning restore CA2201

        Assert.Throws<OutOfMemoryException>(() => ProxyOptions.FromEnvironment(Lookup(env), new RecordingLogger(fatal)));
    }

    [Fact]
    public void A_throwing_lookup_propagates()
    {
        // The never-throw promise covers the input, not a broken seam.
        Assert.Throws<InvalidOperationException>(
            () => ProxyOptions.FromEnvironment(_ => throw new InvalidOperationException("seam broke"), NullLogger.Instance));
    }

    [Fact]
    public void Null_arguments_throw_ArgumentNullException()
    {
        var environment = Assert.Throws<ArgumentNullException>(() => ProxyOptions.FromEnvironment(null!, NullLogger.Instance));
        var logger = Assert.Throws<ArgumentNullException>(() => ProxyOptions.FromEnvironment(_ => null, null!));

        Assert.Equal("environment", environment.ParamName);
        Assert.Equal("logger", logger.ParamName);
    }

    [Fact]
    public void The_resolver_never_throws_for_arbitrary_values()
    {
        var random = new Random(5145);
        string[] schemes = ["http", "socks5", "ftp", "https", ""];
        const string Alphabet = "abcXYZ019_%.:+~@-[]/?# \t";
        for (var i = 0; i < 20_000; i++)
        {
            var length = random.Next(0, 16);
            var body = new string(Enumerable.Range(0, length).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray());
            var env = new Dictionary<string, string>
            {
                ["HTTPS_PROXY"] = schemes[random.Next(schemes.Length)] + "://" + body,
                ["NO_PROXY"] = body,
            };

            var error = Record.Exception(() => ProxyOptions.FromEnvironment(Lookup(env), NullLogger.Instance));

            Assert.Null(error);
        }
    }

    [Fact]
    public void FromEnvironment_reads_only_the_variables_it_documents()
    {
        var asked = new List<string>();
        Func<string, string?> lookup = key =>
        {
            asked.Add(key);
            return null;
        };
        Assert.Empty(asked);

        _ = ProxyOptions.FromEnvironment(lookup, NullLogger.Instance);
        Assert.Equal(["HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy"], asked);

        asked.Clear();
        var env = new Dictionary<string, string> { ["HTTP_PROXY"] = "http://p:80" };
        _ = ProxyOptions.FromEnvironment(key => { asked.Add(key); return env.GetValueOrDefault(key); }, NullLogger.Instance);
        Assert.Equal(["HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "GATEWAY_INTERFACE", "NO_PROXY", "no_proxy"], asked);

        asked.Clear();
        env = new Dictionary<string, string> { ["HTTPS_PROXY"] = "http://p:80", ["NO_PROXY"] = "a" };
        _ = ProxyOptions.FromEnvironment(key => { asked.Add(key); return env.GetValueOrDefault(key); }, NullLogger.Instance);
        Assert.Equal(["HTTPS_PROXY", "NO_PROXY"], asked);
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class RecordingLogger(Exception? failure = null) : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }
}

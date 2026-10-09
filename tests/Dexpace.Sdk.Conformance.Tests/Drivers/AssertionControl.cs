// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// Runs one real assertion against a deliberately broken subject: the negative control without which no assertion is merged
/// (design E, plan convention 12). A control is a transport that violates exactly the clause; the result must be
/// <see cref="ConformanceStatus.Failed"/> (or <see cref="ConformanceStatus.Errored"/> where the contract says so).
/// </summary>
internal static class AssertionControl
{
    /// <summary>Short bounds so the leak and hang controls stay fast.</summary>
    internal static TransportSuiteOptions Options { get; } = new()
    {
        ReleaseTimeout = TimeSpan.FromMilliseconds(500),
        AssertionTimeout = TimeSpan.FromSeconds(10),
    };

    internal static ConformanceAssertion Find(string assertionName) =>
        TransportSuite.Assertions.SingleOrDefault(a => a.Name == assertionName)
        ?? throw new InvalidOperationException($"There is no assertion named '{assertionName}'.");

    /// <summary>Runs <paramref name="assertionName"/> on <paramref name="face"/> against a transport built by <paramref name="broken"/>.</summary>
    internal static Task<ConformanceResult> RunAsync(string assertionName, TransportFace face, Func<IAsyncHttpClient> broken, TransportSuiteOptions? options = null) =>
        RunAsync(
            assertionName,
            face,
            new TransportSubject
            {
                Name = "broken: " + assertionName,
                CreateAsync = _ => broken(),
                CreateBlocking = _ => broken().AsBlocking(),
            },
            options);

    /// <summary>Runs <paramref name="assertionName"/> against a full subject, so a control can break a hook or declare a post-dispose behaviour.</summary>
    internal static Task<ConformanceResult> RunAsync(string assertionName, TransportFace face, TransportSubject subject, TransportSuiteOptions? options = null) =>
        TransportSuite.RunAsync(subject, Find(assertionName), face, options ?? Options, TestContext.Current.CancellationToken);
}

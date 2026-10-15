// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Linq;
using System.Reflection;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Serialization.SystemTextJson;

namespace Dexpace.Sdk.AotSmoke;

// Phase 10 completes the smoke (roadmap constraint 1, P10-17): the suites that now describe every package's runtime-wired surface run in the published
// binary. The invariant suite runs with core's defaults (every hook-less assertion, both faces) and must be green with exactly the pinned list not exercised;
// the codec suite runs over the System.Text.Json adapter and a source-generated context over the kit's probe models; and the version the library reads at run
// time is the version it was built with (NFR-15). The smoke keeps project references, not the packed .nupkg files: the packaging suite proves the packed
// assemblies carry the trim and AOT declarations, and they are the same build output.
internal static partial class SmokeChecks
{
    // The assertions that need a hook only a driver can supply: core's internals (xcut-8, xcut-9, xcut-14) and the DI package (xcut-23). Pinned (P10-28, R13).
    private static readonly string[] s_invariantNotExercised =
    [
        "xcut-14.bounded-under-burst",
        "xcut-14.drains-after-insert",
        "xcut-23.resolution-deterministic",
        "xcut-8.strict-factory-rejects",
        "xcut-9.cycle-safe-walk",
    ];

    private static async Task CheckPhase10Async()
    {
        var options = new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(60) };

        var invariant = await InvariantSuite.RunAllAsync(new InvariantSubject { Name = "core defaults (aot smoke)" }, options, CancellationToken.None);
        Expect(invariant.IsGreen, $"the invariant suite is green under NativeAOT: {invariant}");
        Expect(
            invariant.Results.Where(r => r.Status == ConformanceStatus.NotExercised).Select(r => r.Assertion.Name).Distinct().Order(StringComparer.Ordinal).SequenceEqual(s_invariantNotExercised),
            "exactly the five hook-less invariant assertions are not exercised under NativeAOT");
        Expect(!invariant.Results.Any(r => r.Status is ConformanceStatus.Vacuous or ConformanceStatus.Waived), "no invariant assertion is vacuous or waived under NativeAOT");

        var codec = await CodecSuite.RunAllAsync(
            new CodecSubject
            {
                Name = "Dexpace.Sdk.Serialization.SystemTextJson (aot smoke)",
                CreateSerde = static () => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(SmokeProbeContext.Default)),
                ExpectedMediaType = CommonMediaTypes.ApplicationJson,
            },
            options,
            CancellationToken.None);
        Expect(codec.IsGreen && codec.Results.All(r => r.Status == ConformanceStatus.Passed), $"the codec suite passes whole under NativeAOT: {codec}");

        // NFR-15, the packaged artefact at run time: the SDK version is the assembly's informational version without its + metadata, and never a placeholder.
        var informational = typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Expect(!string.IsNullOrEmpty(informational), "the library carries an informational version");
        Expect(BuildInfo.SdkVersion == informational!.Split('+')[0], $"BuildInfo.SdkVersion ({BuildInfo.SdkVersion}) is the informational version ({informational}) without its + metadata");
        Expect(BuildInfo.SdkVersion is not ("unknown" or "0.0.0"), "BuildInfo.SdkVersion is not a placeholder");
    }
}

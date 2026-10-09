// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Linq;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Http.SystemNet;

namespace Dexpace.Sdk.AotSmoke;

// Phase 8a: the conformance kit. The kit is a library like any other and is IsAotCompatible, so a slice of its suite runs in
// the published binary against the reference transport: the runner, the loopback fixture's sockets, the bounded waits, the
// per-face send primitive and a streamed 8 MiB body must survive trimming and NativeAOT. The full suite stays on the JIT
// run: consumers run the kit in test projects, which are not AOT-published (P8a-20).
internal static partial class SmokeChecks
{
    private static readonly string[] s_conformanceSlice =
    [
        "transport-21.pre-dispatch-failure-via-task",
        "transport-24.vendor-status-readable",
        "transport-25.large-round-trip",
    ];

    private static async Task CheckPhase8aConformanceAsync()
    {
        var subject = new TransportSubject
        {
            Name = "Dexpace.Sdk.Http.SystemNet (aot smoke)",
            CreateAsync = settings => new SystemNetHttpClient(settings.Logger),
            CreateBlocking = settings => new SystemNetHttpClient(settings.Logger),
        };

        foreach (var name in s_conformanceSlice)
        {
            var assertion = TransportSuite.Assertions.SingleOrDefault(a => a.Name == name);
            Expect(assertion is not null, $"The conformance catalogue holds {name}");
            foreach (var face in assertion!.Faces)
            {
                var result = await TransportSuite.RunAsync(subject, assertion, face, cancellationToken: CancellationToken.None);
                Expect(result.Status == ConformanceStatus.Passed, $"{name} on {face} passes under NativeAOT: {result.Status}: {result.Detail}");
            }
        }
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SEAM-1 (design §2.4; §10 <c>logging-abstractions-dependency</c>): core embeds no transport, byte-stream
/// implementation or codec. This pins the "no concrete capability" half over the compiled assembly; the
/// "only the shared framework plus the logging facade" half is <c>scripts/ci/dependency-audit.cs</c>, which checks the
/// <c>deps.json</c> and the nuspec on every pack.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Seam1ArchitectureTests
{
    private static readonly string[] s_concreteCapabilities =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Security",
        "System.Text.Json",
    ];

    [Fact]
    public void Core_references_no_transport_or_codec_assembly()
    {
        var referenced = typeof(Request).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(referenced.Intersect(s_concreteCapabilities, StringComparer.Ordinal));
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SEAM-1 (design §2.4; §10 <c>logging-abstractions-dependency</c>): core embeds no transport, byte-stream
/// implementation or codec. This pins the "no concrete capability" half over the compiled assembly; the
/// "only the shared framework plus the logging facade" half is <c>scripts/ci/dependency-audit.cs</c>, which checks the
/// <c>deps.json</c> and the nuspec on every pack.
/// <para>
/// <b>Phase 5a (P5a-21, design §11 item 23).</b> CFG-35's cause classifier names two exception types that live in
/// transport assemblies of the shared framework: <c>HttpRequestException</c> and <c>SocketException</c>. The blanket ban on
/// those two assemblies therefore became an exact allow-list of the one type each (checked from the assembly's type
/// references), which still fails on any use of <c>HttpClient</c>, a handler, a socket or a stream from them.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class Seam1ArchitectureTests
{
    private static readonly string[] s_concreteCapabilities =
    [
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

    [Fact]
    public void Core_uses_only_the_exception_types_of_System_Net_Http_and_Sockets()
    {
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.Net.Http"] = "System.Net.Http.HttpRequestException",
            ["System.Net.Sockets"] = "System.Net.Sockets.SocketException",
        };
        using var stream = File.OpenRead(typeof(Request).Assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var used = new List<string>();
        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);
            if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference)
            {
                continue;
            }

            var assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
            if (allowed.TryGetValue(assembly, out var permitted))
            {
                var name = reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);
                if (name != permitted)
                {
                    used.Add(assembly + ": " + name);
                }
            }
        }

        Assert.Empty(used);
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Text.Json;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SEAM-2 (design §2.3, §9.2; roadmap constraint 4): core names no concrete implementation of a seam, and core's own
/// suite substitutes in-memory fakes for every seam, so it compiles and runs without any adapter. A transport or
/// serializer adapter reaching this project, directly or through a helper, fails here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Seam2ArchitectureTests
{
    // The only Dexpace assemblies core's suite may see: core, the in-memory fakes, and the suite itself.
    private static readonly HashSet<string> s_allowed = new(StringComparer.Ordinal)
    {
        "Dexpace.Sdk.Core",
        "Dexpace.Sdk.TestSupport",
        "Dexpace.Sdk.Core.Tests",
    };

    [Fact]
    public void Core_references_no_other_dexpace_assembly()
    {
        var core = typeof(Request).Assembly;

        var dexpace = core.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(IsDexpace);

        Assert.Empty(dexpace);
    }

    [Fact]
    public void Core_suite_loads_only_core_and_the_in_memory_fakes()
    {
        // Metadata references, followed through every Dexpace assembly reached, so a transport arriving through
        // Dexpace.Sdk.TestSupport counts as much as one referenced here directly.
        var reached = DexpaceClosure(typeof(Seam2ArchitectureTests).Assembly);

        Assert.Equal(s_allowed.Order(StringComparer.Ordinal), reached.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Core_suite_build_graph_holds_no_adapter()
    {
        // The compiler drops a reference no code uses, so metadata alone would miss a ProjectReference or
        // PackageReference added but not yet called. The deps.json records the whole restored graph.
        var libraries = DepsJsonLibraries();

        Assert.Contains("Dexpace.Sdk.Core", libraries);
        var adapters = libraries.Where(name => IsDexpace(name) && !s_allowed.Contains(name));
        Assert.Empty(adapters);
    }

    [Fact]
    public void Core_suite_drives_the_pipeline_through_a_fake_transport()
    {
        // The conformance clause itself: the fakes are real seam implementations, not adapters.
        Assert.Equal("Dexpace.Sdk.TestSupport", typeof(RecordingTransport).Assembly.GetName().Name);
        Assert.Equal("Dexpace.Sdk.TestSupport", typeof(RecordingSyncTransport).Assembly.GetName().Name);
    }

    private static bool IsDexpace(string name) => name.StartsWith("Dexpace.", StringComparison.Ordinal);

    private static SortedSet<string> DexpaceClosure(Assembly root)
    {
        var seen = new SortedSet<string>(StringComparer.Ordinal) { root.GetName().Name! };
        var pending = new Stack<Assembly>([root]);
        while (pending.TryPop(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies().Where(r => IsDexpace(r.Name!)))
            {
                if (seen.Add(reference.Name!))
                {
                    pending.Push(Assembly.Load(reference));
                }
            }
        }

        return seen;
    }

    private static List<string> DepsJsonLibraries()
    {
        var name = typeof(Seam2ArchitectureTests).Assembly.GetName().Name;
        var path = Path.Combine(AppContext.BaseDirectory, $"{name}.deps.json");
        Assert.True(File.Exists(path), $"{path} is missing; the SEAM-2 build-graph check cannot run.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(library => library.Name.Split('/')[0])
            .ToList();
    }
}

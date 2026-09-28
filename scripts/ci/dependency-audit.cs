// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The zero-dependency audit (SEAM-1, NFR-1, NFR-2; roadmap constraint 2; design §2.4 and §9.2).
//
//   dotnet run scripts/ci/dependency-audit.cs -- <configuration> <package-dir>
//
// Run from the repository root after `dotnet build` and `dotnet pack`. For every library under src/ it reads
//   (1) the compiled <Library>.deps.json: the runtime package closure the library actually carries, and
//   (2) the .nuspec inside the packed .nupkg: the dependency groups a consumer's restore will see,
// and holds both to the rule:
//   - Dexpace.Sdk.Core may reference only the framework, Microsoft.Extensions.Logging.Abstractions (with the
//     closure its own nuspec requires) and build-only PrivateAssets="all" packages, which never reach either
//     file;
//   - an adapter may reference the same, plus Dexpace.Sdk.Core, plus at most one third-party library;
//   - every Microsoft.Extensions.* / System.* package is band-matched to the target framework's major version;
//   - every package targets exactly the floor of roadmap decision D1 (net10.0).
// A new library must be added to the policies table below, which is where a reviewer sees its one third-party library
// named. Exit 0 when every library conforms, 1 on any violation, 2 on a usage or input error.

#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

const string Facade = "Microsoft.Extensions.Logging.Abstractions";
const string Core = "Dexpace.Sdk.Core";
const string TargetFramework = "net10.0";
const int FrameworkMajor = 10;

// The one place an adapter's third-party library is named. SystemTextJson takes none: System.Text.Json is in
// the shared framework, so the adapter's "one library" is the runtime's own (design §9.2).
var policies = new Dictionary<string, string?>(StringComparer.Ordinal)
{
    [Core] = null,
    ["Dexpace.Sdk.Http.SystemNet"] = null,
    ["Dexpace.Sdk.Serialization.SystemTextJson"] = null,
};

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: dependency-audit.cs <configuration> <package-dir>");
    return 2;
}

var configuration = args[0];
var packageDir = args[1];
var violations = new List<string>();
var libraries = Directory.GetDirectories("src").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();

foreach (var library in libraries)
{
    if (!policies.TryGetValue(library, out var thirdParty))
    {
        violations.Add($"{library}: no dependency policy; add it to the policies table in scripts/ci/dependency-audit.cs");
        continue;
    }

    var depsJson = Path.Combine("src", library, "bin", configuration, TargetFramework, $"{library}.deps.json");
    if (!File.Exists(depsJson))
    {
        Console.Error.WriteLine($"dependency-audit: {depsJson} is missing; build {configuration} first");
        return 2;
    }

    AuditDepsJson(library, thirdParty, depsJson, violations);
    if (!AuditNuspec(library, thirdParty, packageDir, violations))
    {
        return 2;
    }
}

foreach (var violation in violations)
{
    Console.Error.WriteLine($"dependency-audit: VIOLATION: {violation}");
}

Console.WriteLine(violations.Count == 0
    ? $"dependency-audit: {libraries.Count} libraries conform (deps.json and nuspec)"
    : $"dependency-audit: {violations.Count} violation(s)");
return violations.Count == 0 ? 0 : 1;

void AuditDepsJson(string library, string? thirdParty, string path, List<string> found)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    var runtimeTarget = root.GetProperty("runtimeTarget").GetProperty("name").GetString();
    if (runtimeTarget != $".NETCoreApp,Version=v{FrameworkMajor}.0")
    {
        found.Add($"{library}: deps.json targets {runtimeTarget}, not {TargetFramework} (decision D1)");
    }

    // name -> dependency names, from the one runtime target; and name -> (type, version).
    var graph = root.GetProperty("targets").EnumerateObject().Single().Value.EnumerateObject()
        .ToDictionary(
            entry => entry.Name.Split('/')[0],
            entry => entry.Value.TryGetProperty("dependencies", out var deps)
                ? deps.EnumerateObject().Select(dep => dep.Name).ToList()
                : [],
            StringComparer.Ordinal);
    var kinds = root.GetProperty("libraries").EnumerateObject().ToDictionary(
        entry => entry.Name.Split('/')[0],
        entry => (Type: entry.Value.GetProperty("type").GetString(), Version: entry.Name.Split('/')[1]),
        StringComparer.Ordinal);

    var allowedPackages = Closure(graph, Facade);
    if (thirdParty is not null)
    {
        allowedPackages.UnionWith(Closure(graph, thirdParty));
    }

    var allowedProjects = library == Core ? new HashSet<string>(StringComparer.Ordinal) { Core } : Closure(graph, Core);
    allowedProjects.Add(library);

    var packages = kinds.Where(kind => kind.Value.Type == "package").ToList();
    foreach (var (name, (_, version)) in packages)
    {
        if (!allowedPackages.Contains(name))
        {
            found.Add($"{library}: runtime closure carries package {name} {version}, which the dependency rule does not allow");
        }

        if ((name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) || name.StartsWith("System.", StringComparison.Ordinal))
            && !version.StartsWith($"{FrameworkMajor}.", StringComparison.Ordinal))
        {
            found.Add($"{library}: {name} {version} is not band-matched to {TargetFramework} ({FrameworkMajor}.x)");
        }
    }

    foreach (var (name, (type, _)) in kinds.Where(kind => kind.Value.Type == "project"))
    {
        if (!allowedProjects.Contains(name))
        {
            found.Add($"{library}: references project {name}; a library references at most {Core}");
        }
    }

    if (library == Core && !packages.Any(package => package.Key == Facade))
    {
        found.Add($"{library}: {Facade} is missing from the runtime closure; the audit's allow-list is stale");
    }
}

bool AuditNuspec(string library, string? thirdParty, string directory, List<string> found)
{
    var nupkg = Directory.Exists(directory)
        ? Directory.GetFiles(directory, $"{library}.*.nupkg")
            .SingleOrDefault(file => char.IsAsciiDigit(Path.GetFileName(file)[library.Length + 1]))
        : null;
    if (nupkg is null)
    {
        Console.Error.WriteLine($"dependency-audit: no {library} package in {directory}; pack {configuration} first");
        return false;
    }

    using var archive = ZipFile.OpenRead(nupkg);
    var entry = archive.Entries.Single(entry => entry.FullName == $"{library}.nuspec");
    using var stream = entry.Open();
    var metadata = XDocument.Load(stream).Root!.Elements().Single(element => element.Name.LocalName == "metadata");
    var version = metadata.Elements().Single(element => element.Name.LocalName == "version").Value;
    var groups = metadata.Elements().Where(element => element.Name.LocalName == "dependencies")
        .Elements().Where(element => element.Name.LocalName == "group").ToList();

    var frameworks = groups.Select(group => (string?)group.Attribute("targetFramework")).ToList();
    if (frameworks.Count != 1 || !string.Equals(frameworks[0], TargetFramework, StringComparison.OrdinalIgnoreCase))
    {
        found.Add($"{library}: nuspec dependency groups are [{string.Join(", ", frameworks)}], expected exactly [{TargetFramework}] (decision D1)");
    }

    // Core lists exactly the facade. An adapter lists Core and its one third-party library, and may also list the
    // facade: CentralPackageTransitivePinningEnabled promotes a pinned transitive package (the facade, reached
    // through Core) to a direct nuspec dependency, and the rule allows an adapter the facade in any case.
    var expected = new HashSet<string>(StringComparer.Ordinal) { library == Core ? Facade : Core };
    if (thirdParty is not null)
    {
        expected.Add(thirdParty);
    }

    var optional = library == Core ? [] : new HashSet<string>(StringComparer.Ordinal) { Facade };

    foreach (var group in groups)
    {
        var dependencies = group.Elements().Where(element => element.Name.LocalName == "dependency")
            .ToDictionary(element => (string)element.Attribute("id")!, element => (string)element.Attribute("version")!, StringComparer.Ordinal);
        var listed = dependencies.Keys.Where(id => !optional.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if (!listed.SetEquals(expected))
        {
            found.Add($"{library}: nuspec group {group.Attribute("targetFramework")?.Value} depends on [{string.Join(", ", dependencies.Keys.Order(StringComparer.Ordinal))}], expected [{string.Join(", ", expected.Order(StringComparer.Ordinal))}]{(optional.Count > 0 ? $" (plus optionally {Facade})" : string.Empty)}");
        }

        if (dependencies.TryGetValue(Core, out var coreVersion) && coreVersion != version)
        {
            found.Add($"{library}: depends on {Core} {coreVersion}, not its own lockstep version {version} (design §2.3)");
        }

        if (dependencies.TryGetValue(Facade, out var facadeVersion) && !facadeVersion.StartsWith($"{FrameworkMajor}.", StringComparison.Ordinal))
        {
            found.Add($"{library}: nuspec asks for {Facade} {facadeVersion}, not the {FrameworkMajor}.x band");
        }
    }

    return true;
}

static HashSet<string> Closure(Dictionary<string, List<string>> graph, string start)
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var pending = new Stack<string>([start]);
    while (pending.TryPop(out var name))
    {
        if (seen.Add(name) && graph.TryGetValue(name, out var deps))
        {
            foreach (var dep in deps)
            {
                pending.Push(dep);
            }
        }
    }

    return seen;
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The release preflight (NFR-16, NFR-12; phase 12, P12-4, P12-13; design "The release workflow" and "Signing").
//
//   dotnet run scripts/ci/release-preflight.cs -- <package-dir> [--tag <tag>] [--repo <root>] [--main-ref <ref>] [--expected-token <16 hex>]
//
// Run from the repository root after `dotnet pack`. Over the packed set it fails, naming the cause, when
//   (1) the five libraries are not all present as .nupkg at ONE version (the version is read from the file names <Id>.<version>.nupkg, as the
//       audits read it; a sixth .nupkg is a finding too);
//   (2) a .nupkg has no .snupkg beside it (the symbol packages are part of what is published and compared, NFR-12);
//   (3) a packed lib/net10.0/<Id>.dll is not strong-name signed with the committed key: it is read in memory with System.Reflection.Metadata and
//       never loaded, its CorFlags must carry StrongNameSigned and its public key must have the expected token. The expected token is derived from
//       DexpacePublicKey in <repo>/Directory.Build.props (the runtime's own token derivation: SHA-1 of the blob, last eight bytes reversed), or given
//       with --expected-token; an unsigned assembly and a wrongly signed one share this code path and the report tells them apart (NFR-16's testable
//       clause: a release build fails an unsigned publication).
// With --tag it also fails when
//   (4) the tag is not v<version> of the packages (a pre-release suffix is accepted: v0.1.0-alpha.1 for 0.1.0-alpha.1; v0.1.0 for it is refused);
//   (5) some <repo>/src/*/PublicAPI.Unshipped.txt holds more than `#nullable enable`, so the release would ship members not recorded as shipped (NFR-4);
//   (6) <repo>/CHANGELOG.md has no `## [<version>]` section;
//   (7) the tag's commit is not an ancestor of --main-ref (default origin/main), so a released commit is always one CI passed on main (NFR-17).
// Without --tag the tag-independent halves of (5) and (6) still run, and a finding is printed as `would fail:` and does not fail the run (the dry
// run); (4) and (7) need a tag and are skipped. A new library is added to Libraries below, beside the policy table of dependency-audit.cs.
// Exit 0 when every check holds, 1 on any failing finding, 2 on a usage or input error.

#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

const string TargetFramework = "net10.0";

string[] libraries =
[
    "Dexpace.Sdk.Core",
    "Dexpace.Sdk.Http.SystemNet",
    "Dexpace.Sdk.Serialization.SystemTextJson",
    "Dexpace.Sdk.Extensions.DependencyInjection",
    "Dexpace.Sdk.Conformance",
];

string? packageDirectory = null;
string? tag = null;
var repo = ".";
var mainRef = "origin/main";
string? expectedToken = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--tag" when i + 1 < args.Length:
            tag = args[++i];
            break;
        case "--repo" when i + 1 < args.Length:
            repo = args[++i];
            break;
        case "--main-ref" when i + 1 < args.Length:
            mainRef = args[++i];
            break;
        case "--expected-token" when i + 1 < args.Length:
            expectedToken = args[++i].ToLowerInvariant();
            break;
        default:
            if (args[i].StartsWith("--", StringComparison.Ordinal) || packageDirectory is not null)
            {
                return Usage();
            }

            packageDirectory = args[i];
            break;
    }
}

if (packageDirectory is null)
{
    return Usage();
}

if (!Directory.Exists(packageDirectory))
{
    Console.Error.WriteLine($"release-preflight: no package directory {packageDirectory}; pack Release first");
    return 2;
}

if (expectedToken is null)
{
    var props = Path.Combine(repo, "Directory.Build.props");
    var match = File.Exists(props)
        ? Regex.Match(File.ReadAllText(props), @"<DexpacePublicKey>\s*([0-9A-Fa-f]+)\s*</DexpacePublicKey>", RegexOptions.CultureInvariant)
        : Match.Empty;
    if (!match.Success)
    {
        Console.Error.WriteLine($"release-preflight: no <DexpacePublicKey> in {props}; pass --expected-token or --repo");
        return 2;
    }

    expectedToken = Token(Convert.FromHexString(match.Groups[1].Value));
}

var failures = new List<string>();
var wouldFail = new List<string>();

// (1) and (2): the packed set.
var nupkgs = Directory.GetFiles(packageDirectory, "*.nupkg").OrderBy(static file => file, StringComparer.Ordinal).ToList();
var byLibrary = new Dictionary<string, (string File, string Version)>(StringComparer.Ordinal);
foreach (var file in nupkgs)
{
    var name = Path.GetFileName(file);
    var library = libraries.FirstOrDefault(id => name.StartsWith(id + ".", StringComparison.Ordinal) && char.IsAsciiDigit(name[id.Length + 1]));
    if (library is null)
    {
        failures.Add($"{name} is not one of the five libraries' packages");
    }
    else if (byLibrary.TryGetValue(library, out var earlier))
    {
        failures.Add($"{library} is packed twice ({Path.GetFileName(earlier.File)} and {name})");
    }
    else
    {
        byLibrary[library] = (file, name[(library.Length + 1)..^".nupkg".Length]);
    }
}

foreach (var library in libraries.Where(id => !byLibrary.ContainsKey(id)))
{
    failures.Add($"{library} has no .nupkg in {packageDirectory}; the release is the five libraries at one version");
}

var versions = byLibrary.GroupBy(static pair => pair.Value.Version, StringComparer.Ordinal).OrderBy(static group => group.Key, StringComparer.Ordinal).ToList();
if (versions.Count > 1)
{
    failures.Add("the packages are at more than one version, not one version train: "
        + string.Join("; ", versions.Select(group => $"{group.Key} ({string.Join(", ", group.Select(static pair => pair.Key).Order(StringComparer.Ordinal))})")));
}

foreach (var (library, (file, _)) in byLibrary.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
{
    var symbols = Path.ChangeExtension(file, ".snupkg");
    if (!File.Exists(symbols))
    {
        failures.Add($"{Path.GetFileName(file)} has no {Path.GetFileName(symbols)} beside it");
    }

    var problem = CheckSigning(library, file, expectedToken);
    if (problem is not null)
    {
        failures.Add(problem);
    }
}

// The version the tag-dependent checks compare against: the one version, or none when the set is not one train.
var version = versions.Count == 1 ? versions[0].Key : null;

// (4) and (7): need a tag.
if (tag is not null)
{
    CheckTag(tag, version, repo, mainRef, failures);
}

// (5) and (6): tag-dependent in what they guard, evaluated with or without the tag; without it they are reported and not failed.
var tagDependent = new List<string>();
CheckUnshipped(repo, tagDependent);
if (version is not null)
{
    CheckChangelog(repo, version, tagDependent);
}

if (tag is null)
{
    wouldFail.AddRange(tagDependent);
}
else
{
    failures.AddRange(tagDependent);
}

foreach (var finding in wouldFail)
{
    Console.WriteLine($"release-preflight: would fail: {finding} (no --tag given: a tagged run fails here)");
}

if (tag is null)
{
    Console.WriteLine("release-preflight: skipped without --tag: the tag matches v<version>; the tag's commit is an ancestor of the main ref");
}

foreach (var failure in failures)
{
    Console.Error.WriteLine($"release-preflight: FAILED {failure}");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"release-preflight: {failures.Count} finding(s)");
    return 1;
}

Console.WriteLine($"release-preflight: ok, {byLibrary.Count} packages at {version}, each with its symbols, each strong-named with token {expectedToken}"
    + (tag is null ? " (dry run: no tag)" : $", tag {tag} on {mainRef}"));
return 0;

int Usage()
{
    Console.Error.WriteLine("usage: release-preflight.cs <package-dir> [--tag <tag>] [--repo <root>] [--main-ref <ref>] [--expected-token <16 hex>]");
    return 2;
}

// SHA-1 of the public key blob, last eight bytes reversed: the runtime derives it, so no hash call is needed here.
string Token(byte[] publicKey)
{
    var name = new AssemblyName("token");
    name.SetPublicKey(publicKey);
    return Convert.ToHexStringLower(name.GetPublicKeyToken()!);
}

string? CheckSigning(string library, string nupkg, string expected)
{
    using var archive = ZipFile.OpenRead(nupkg);
    var entryName = $"lib/{TargetFramework}/{library}.dll";
    var entry = archive.GetEntry(entryName);
    if (entry is null)
    {
        return $"{Path.GetFileName(nupkg)} has no {entryName}";
    }

    using var memory = new MemoryStream();
    using (var stream = entry.Open())
    {
        stream.CopyTo(memory);
    }

    memory.Position = 0;
    using var pe = new PEReader(memory);
    var signed = pe.PEHeaders.CorHeader is { } cor && (cor.Flags & CorFlags.StrongNameSigned) != 0;
    var key = pe.GetMetadataReader() is { } reader ? reader.GetBlobBytes(reader.GetAssemblyDefinition().PublicKey) : [];
    if (!signed || key.Length == 0)
    {
        return $"{entryName} in {Path.GetFileName(nupkg)} is not strong-name signed (expected token {expected}); a release build fails an unsigned publication (NFR-16)";
    }

    var actual = Token(key);
    return actual == expected
        ? null
        : $"{entryName} in {Path.GetFileName(nupkg)} is signed with token {actual}, not the committed key's {expected}";
}

void CheckTag(string tagName, string? packagesVersion, string root, string main, List<string> found)
{
    var bare = tagName.StartsWith("refs/tags/", StringComparison.Ordinal) ? tagName["refs/tags/".Length..] : tagName;
    if (!bare.StartsWith('v'))
    {
        found.Add($"the tag {bare} is not v<version>");
    }
    else if (packagesVersion is null)
    {
        found.Add($"the tag {bare} cannot be compared: the packages are not at one version");
    }
    else if (bare[1..] != packagesVersion)
    {
        found.Add($"the tag {bare} names version {bare[1..]}, but the packages are at {packagesVersion}");
    }

    var (resolved, commit, resolveError) = Git(root, "rev-parse", "--verify", "--quiet", $"refs/tags/{bare}^{{commit}}");
    if (resolved != 0)
    {
        found.Add($"the tag {bare} does not exist in {root}{(resolveError.Length > 0 ? ": " + resolveError : string.Empty)}");
        return;
    }

    var (ancestry, _, ancestryError) = Git(root, "merge-base", "--is-ancestor", commit, main);
    if (ancestry == 1)
    {
        found.Add($"the tag {bare} points at {commit[..Math.Min(12, commit.Length)]}, which is not an ancestor of {main}; a release is cut from main");
    }
    else if (ancestry != 0)
    {
        found.Add($"cannot tell whether the tag {bare} is on {main}: {ancestryError}");
    }
}

void CheckUnshipped(string root, List<string> found)
{
    var src = Path.Combine(root, "src");
    var files = Directory.Exists(src)
        ? Directory.GetFiles(src, "PublicAPI.Unshipped.txt", SearchOption.AllDirectories)
            .Where(static file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(static part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal).ToList()
        : [];
    if (files.Count == 0)
    {
        found.Add($"no PublicAPI.Unshipped.txt under {src}; the settled-surface rule has nothing to read");
        return;
    }

    foreach (var file in files)
    {
        var members = File.ReadAllLines(file).Count(static line => line.Trim().Length > 0 && line.Trim() != "#nullable enable");
        if (members > 0)
        {
            found.Add($"{Path.GetRelativePath(root, file)} holds {members} line(s) beyond `#nullable enable`: the release would ship members not recorded as shipped (NFR-4)");
        }
    }
}

void CheckChangelog(string root, string packagesVersion, List<string> found)
{
    var changelog = Path.Combine(root, "CHANGELOG.md");
    if (!File.Exists(changelog))
    {
        found.Add($"no CHANGELOG.md under {root}");
    }
    else if (!File.ReadLines(changelog).Any(line => line.StartsWith($"## [{packagesVersion}]", StringComparison.Ordinal)))
    {
        found.Add($"CHANGELOG.md has no `## [{packagesVersion}]` section");
    }
}

(int Exit, string Output, string Error) Git(string root, params string[] arguments)
{
    var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add("-C");
    start.ArgumentList.Add(root);
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    return (process.ExitCode, output.Result.Trim(), error.Result.Trim());
}

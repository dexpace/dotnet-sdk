// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The packaging conformance step (NFR-1, NFR-2, NFR-3, NFR-8, NFR-9, NFR-10, NFR-13, NFR-14, NFR-15; phase 10, P10-10).
//
//   dotnet run scripts/ci/packaging-conformance.cs -- <package-dir>
//
// Run from the repository root after `dotnet pack`. It runs Dexpace.Sdk.Conformance's PackagingSuite over every .nupkg in <package-dir> and
// prints the report: the suite reads the published artefacts (the nuspec metadata and the packed assemblies' attributes, never loading them),
// where scripts/ci/dependency-audit.cs reads the build output's closure. Two checks, two subjects; the policies table below mirrors the audit's, and
// tests/Dexpace.Sdk.Conformance.Tests/Gates/PackagingPolicyParityTests.cs fails if the two disagree.
// Exit 0 when every assertion passed, 1 on a red report, 2 on a usage or input error.

#:project ../../src/Dexpace.Sdk.Conformance/Dexpace.Sdk.Conformance.csproj
#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dexpace.Sdk.Conformance;

const string Core = "Dexpace.Sdk.Core";

// The one place an adapter's third-party library and first-party projects are named; the same table as scripts/ci/dependency-audit.cs.
var policies = new Dictionary<string, PackagePolicy>(StringComparer.Ordinal)
{
    [Core] = new(null, []),
    ["Dexpace.Sdk.Http.SystemNet"] = new(null, []),
    ["Dexpace.Sdk.Serialization.SystemTextJson"] = new(null, []),
    ["Dexpace.Sdk.Conformance"] = new(null, []),
    ["Dexpace.Sdk.Extensions.DependencyInjection"] = new("Microsoft.Extensions.Http", ["Dexpace.Sdk.Http.SystemNet"]),
};

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: packaging-conformance.cs <package-dir>");
    return 2;
}

var packageDir = args[0];
if (!Directory.Exists(packageDir))
{
    Console.Error.WriteLine($"packaging-conformance: {packageDir} does not exist; run `dotnet pack` first");
    return 2;
}

var packages = Directory.GetFiles(packageDir, "*.nupkg").Order(StringComparer.Ordinal).ToArray();
if (packages.Length == 0)
{
    Console.Error.WriteLine($"packaging-conformance: no .nupkg in {packageDir}; run `dotnet pack` first");
    return 2;
}

var report = PackagingSuite.RunAll(new PackagingSubject
{
    Name = "the packed Dexpace.Sdk libraries",
    PackagePaths = packages,
    CoreId = Core,
    Policies = policies,
    DeclaredFloor = "net10.0",
});

Console.WriteLine(report.ToString());
return report.IsGreen ? 0 : 1;

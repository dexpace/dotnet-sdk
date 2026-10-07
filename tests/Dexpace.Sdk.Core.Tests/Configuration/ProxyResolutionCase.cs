// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>One row of <c>config/proxy-resolution.json</c>.</summary>
internal sealed record ProxyResolutionCase(
    string Name,
    string Layer,
    Dictionary<string, string> Env,
    ExpectedProxy? Expected,
    ExpectedWarning? Warning,
    string? Note)
{
    internal static IReadOnlyList<ProxyResolutionCase> All() => VectorFile.Load<ProxyResolutionCase>("config/proxy-resolution.json");

    internal static TheoryData<string> Names(string? layer)
    {
        var data = new TheoryData<string>();
        foreach (var vector in All().Where(c => layer is null || c.Layer == layer))
        {
            data.Add(vector.Name);
        }

        return data;
    }

    internal static ProxyResolutionCase Named(string name) => All().Single(c => c.Name == name);
}

/// <summary>The proxy a case expects.</summary>
internal sealed record ExpectedProxy(string Type, string Host, int Port, string? UserName, string? Password, List<string> NonProxyHosts);

/// <summary>The warning a case expects: the variable and the reason keyword.</summary>
internal sealed record ExpectedWarning(string Variable, string Reason);

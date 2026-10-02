// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SEAM-2 (design §3.2, position A, deviation P2b-1): "never implements" is read as "never supplies the external
/// concern behind a seam". Core ships adapters that re-shape a caller-supplied implementation and perform no I/O, and
/// this test makes that mechanical: every core type that implements <see cref="IHttpClient"/>,
/// <see cref="IAsyncHttpClient"/> or <see cref="ISerde"/> is non-public and on a named allow-list with a reason.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SeamImplementationArchitectureTests
{
    // Each entry carries the reason it may exist (P2b-1). Phase 4c adds the public HttpPipeline here (PIPE-26).
    private static readonly Dictionary<string, string> s_allowed = new(StringComparer.Ordinal)
    {
        ["HttpClientExtensions+SyncToAsyncAdapter"] =
            "AsAsync: re-shapes a caller-supplied IHttpClient as IAsyncHttpClient; performs no I/O of its own (SEAM-18).",
        ["HttpClientExtensions+AsyncToSyncAdapter"] =
            "AsBlocking: re-shapes a caller-supplied IAsyncHttpClient as IHttpClient; performs no I/O of its own (SEAM-18).",
        ["DelegateHttpClient+AsyncAdapter"] =
            "DelegateHttpClient.Create: gives a caller-supplied send function the IAsyncHttpClient shape C# nominal typing requires; no I/O (SEAM-11).",
        ["DelegateHttpClient+BlockingAdapter"] =
            "DelegateHttpClient.CreateBlocking: gives a caller-supplied blocking send function the IHttpClient shape; no I/O (SEAM-11).",
    };

    private static readonly Type[] s_seams = [typeof(IHttpClient), typeof(IAsyncHttpClient), typeof(ISerde)];

    [Fact]
    public void Core_implements_a_seam_only_through_the_allow_listed_adapters()
    {
        var offenders = new List<string>();
        foreach (var type in AllCoreTypes().Where(t => !t.IsInterface && s_seams.Any(seam => seam.IsAssignableFrom(t))))
        {
            var name = NameOf(type);
            var isPublic = type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
            if (isPublic)
            {
                offenders.Add($"{name} is public");
            }
            else if (!s_allowed.ContainsKey(name))
            {
                offenders.Add($"{name} is not on the allow-list");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "SEAM-2: core may adapt a caller-supplied seam implementation but never supply one. A new implementer needs "
            + "an entry in this allow-list with a reason (P2b-1), and phase 4c owns the HttpPipeline entry (PIPE-26). "
            + "Offenders: " + string.Join("; ", offenders));
    }

    [Fact]
    public void The_allow_list_names_only_types_that_exist()
    {
        var existing = AllCoreTypes().Select(NameOf).ToHashSet(StringComparer.Ordinal);

        var stale = s_allowed.Keys.Where(name => !existing.Contains(name)).ToArray();

        Assert.True(stale.Length == 0, "Stale allow-list entries: " + string.Join(", ", stale));
        Assert.All(s_allowed.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    private static Type[] AllCoreTypes() => typeof(Request).Assembly.GetTypes();

    // Nested names read "Outer+Inner"; a generic arity suffix is not expected on an adapter.
    private static string NameOf(Type type) => type.IsNested ? $"{NameOf(type.DeclaringType!)}+{type.Name}" : type.Name;
}

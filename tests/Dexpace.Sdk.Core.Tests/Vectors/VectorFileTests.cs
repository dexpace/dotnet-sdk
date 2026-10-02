// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using System.Text.RegularExpressions;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Vectors;

/// <summary>Roadmap constraint 10: shared vectors name their source, and the loader reads them.</summary>
[Trait("Category", "Unit")]
public partial class VectorFileTests
{
    private sealed record CodecCase(string Op, string Input, string Expected);

    [GeneratedRegex(@"@[0-9a-f]{7,40}\b")]
    private static partial Regex ShaPattern();

    [Fact]
    public void Every_vector_file_names_its_source_path_and_sha()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "vectors");
        var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            Assert.True(
                document.RootElement.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String,
                $"{file} has no top-level \"source\" string.");
            var text = source.GetString()!;
            Assert.Contains('@', text);
            Assert.Matches(ShaPattern(), text);
        }
    }

    [Fact]
    public void Load_returns_the_cases_array_of_a_file()
    {
        var cases = VectorFile.Load<CodecCase>("http/rfc3986.json");

        Assert.NotEmpty(cases);
        Assert.All(cases, c => Assert.False(string.IsNullOrEmpty(c.Op)));
    }
}

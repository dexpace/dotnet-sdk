// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;

namespace Dexpace.Sdk.TestSupport.Vectors;

/// <summary>
/// Loads a shared test-vector file (roadmap constraint 10): <c>{ "source": "...", "cases": [ ... ] }</c>, copied to
/// the test output directory under <c>vectors/</c> from <c>tests/vectors/</c>.
/// </summary>
public static class VectorFile
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Reads the <c>cases</c> array of a vector file.</summary>
    /// <typeparam name="TCase">The case shape; deserialised with <c>System.Text.Json</c> (tests are not AOT).</typeparam>
    /// <param name="relativePath">The path below <c>vectors/</c>, for example <c>http/query.json</c>.</param>
    /// <returns>The cases, in file order.</returns>
    public static IReadOnlyList<TCase> Load<TCase>(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        var path = Path.Combine(AppContext.BaseDirectory, "vectors", relativePath);
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var cases = document.RootElement.GetProperty("cases");
        return cases.Deserialize<List<TCase>>(s_options) ?? throw new InvalidDataException($"{relativePath} has no cases.");
    }
}

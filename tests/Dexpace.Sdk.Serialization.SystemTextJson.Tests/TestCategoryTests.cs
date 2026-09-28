// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

// Roadmap constraint 4, design §9.3: every test carries a category, so a --filter-trait run selects it.
[Trait("Category", "Unit")]
public sealed class TestCategoryTests
{
    [Fact]
    public void Every_test_carries_a_design_category() =>
        Assert.Empty(TestCategories.FindUncategorized(typeof(TestCategoryTests).Assembly));
}

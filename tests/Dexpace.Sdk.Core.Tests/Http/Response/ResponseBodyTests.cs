// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>The response body contract: IO-3 now; the sync twins, the cap and the single-open rule join in phase 3a's PR 5.</summary>
[Trait("Category", "Unit")]
public class ResponseBodyTests
{
    [Fact]
    public void ResponseBody_FromStream_rejects_a_content_length_below_minus_one()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => ResponseBody.FromStream(new MemoryStream(), contentLength: -2));

        Assert.Equal("contentLength", error.ParamName);
    }
}

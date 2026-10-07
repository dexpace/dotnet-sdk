// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// SEAM-5, the DI-less half (design §3.6; §10 <c>no-provider-registry</c>): a transport or codec is a non-nullable
/// parameter, so zero candidates is a compile error and a null one is rejected by name. The DI half is phase 9's.
/// </summary>
[Trait("Category", "Unit")]
[Collection("Instrumentation")]
public sealed class SeamParameterTests
{
    [Fact]
    public void CreateDefault_rejects_a_null_transport_naming_the_parameter()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => DexpacePipeline.CreateDefault(null!));

        Assert.Equal("transport", ex.ParamName);
    }

    [Fact]
    public void RequestBody_FromValue_rejects_a_null_serde_naming_the_parameter()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => RequestBody.FromValue("v", null!));

        Assert.Equal("serde", ex.ParamName);
    }
}

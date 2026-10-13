// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>The codec every test client installs: a client with no codec fails validation and every keyed resolution (design D), so tests that are not about the codec go through <see cref="TestClients"/>.</summary>
internal static class StubSerde
{
    internal static ISerde Instance { get; } = new BytesSerde(CommonMediaTypes.ApplicationJson, []);
}

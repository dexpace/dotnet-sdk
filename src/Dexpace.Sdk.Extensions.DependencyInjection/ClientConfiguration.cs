// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>What the reader built from a client's section: the records, the handler's pooled-connection lifetime and every failure (P9-9).</summary>
/// <param name="Options">The client options.</param>
/// <param name="Transport">The transport options, without logger or clock; <see cref="SystemNetHttpClientOptions.Proxy"/> carries the explicit proxy.</param>
/// <param name="PooledConnectionLifetime">The primary handler's pooled-connection lifetime; infinite for <c>none</c> (P9-13).</param>
/// <param name="Credentials">The credentials read from <c>Auth:*</c>, or <see langword="null"/> when no such key is present.</param>
/// <param name="Failures">Every key that failed, in table order.</param>
internal sealed record ClientConfiguration(
    DexpaceClientOptions Options,
    SystemNetHttpClientOptions Transport,
    TimeSpan PooledConnectionLifetime,
    AuthCredentials? Credentials,
    IReadOnlyList<ConfigurationFailure> Failures)
{
    /// <summary>The default pooled-connection lifetime: the factory's own rotation interval (P9-13).</summary>
    internal static readonly TimeSpan s_defaultPooledConnectionLifetime = TimeSpan.FromMinutes(2);
}

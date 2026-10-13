// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// The options-pattern <c>TOptions</c> of one named client (P9-11): the snapshot the reader and the transforms produced, and the failures to
/// report. Internal and mutable by design; the public surface is the immutable records inside it. The public parameterless constructor is
/// what <c>OptionsFactory</c> needs to create it under trimming (R4).
/// </summary>
internal sealed class DexpaceClientState
{
    /// <summary>Creates the state the options factory fills in.</summary>
    public DexpaceClientState()
    {
    }

    /// <summary>The client options: reader output, then the <c>Configure</c> transforms in call order.</summary>
    internal DexpaceClientOptions Options { get; set; } = new();

    /// <summary>The transport options, without logger or clock; the container supplies those.</summary>
    internal SystemNetHttpClientOptions Transport { get; set; } = new();

    /// <summary>The primary handler's pooled-connection lifetime; infinite for <c>none</c>.</summary>
    internal TimeSpan PooledConnectionLifetime { get; set; } = ClientConfiguration.s_defaultPooledConnectionLifetime;

    /// <summary>The credentials from <c>Auth:*</c> and the credential transforms, or <see langword="null"/> when there are none.</summary>
    internal AuthCredentials? Credentials { get; set; }

    /// <summary>Every failure the reader and the transforms found, as <c>"&lt;path&gt;: &lt;rule&gt;"</c>.</summary>
    internal List<string> Failures { get; } = [];
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// Fills a client's <see cref="DexpaceClientState"/>: the reader over the section, then the transforms in call order (CFG-1, P9-6). A transform
/// that returns `null` or throws is a failure naming the client; its message is never quoted, because it can carry a value.
/// </summary>
internal sealed class DexpaceClientStateSetup(DexpaceClientRegistration registration, IServiceProvider services) : IConfigureNamedOptions<DexpaceClientState>
{
    /// <inheritdoc/>
    public void Configure(string? name, DexpaceClientState options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, registration.Name, StringComparison.Ordinal))
        {
            return;
        }

        var configuration = ConfigurationReader.Read(registration.Section);
        options.Failures.AddRange(configuration.Failures.Select(static failure => failure.ToString()));
        options.Options = Transform(options, configuration.Options, registration.OptionsTransforms, "Configure", static (transform, value) => transform(value));
        options.Transport = Transform(options, configuration.Transport, registration.TransportTransforms, "ConfigureTransport", static (transform, value) => transform(value));
        options.PooledConnectionLifetime = configuration.PooledConnectionLifetime;
        options.Credentials = TransformCredentials(options, configuration.Credentials);
    }

    /// <inheritdoc/>
    public void Configure(DexpaceClientState options) => Configure(Options.DefaultName, options);

    private AuthCredentials? TransformCredentials(DexpaceClientState state, AuthCredentials? configured)
    {
        if (registration.CredentialTransforms.Count == 0)
        {
            return configured;
        }

        return Transform(state, configured ?? new AuthCredentials(), registration.CredentialTransforms, "ConfigureCredentials", (transform, value) => transform(services, value));
    }

    private T Transform<T, TTransform>(DexpaceClientState state, T start, List<TTransform> transforms, string method, Func<TTransform, T, T> apply)
        where T : class
    {
        var current = start;
        foreach (var transform in transforms)
        {
            try
            {
                var result = apply(transform, current);
                if (result is null)
                {
                    state.Failures.Add(registration.Name + ": " + method + " returned null");
                    continue;
                }

                current = result;
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                state.Failures.Add(registration.Name + ": " + method + " threw " + ex.GetType().Name);
            }
        }

        return current;
    }
}

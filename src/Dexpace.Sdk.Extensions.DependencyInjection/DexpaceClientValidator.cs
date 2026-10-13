// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// The one validator of a named client (P9-11): it reports every failure the reader and the transforms found, all at once, as an
/// <see cref="OptionsValidationException"/> at host start, or at first resolution with no host (V4). The wiring checks of registration
/// (SEAM-5, the proxy sources, the auth rules) join the same list.
/// </summary>
internal sealed class DexpaceClientValidator(DexpaceClientRegistration registration) : IValidateOptions<DexpaceClientState>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DexpaceClientState options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, registration.Name, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        var failures = new List<string>(options.Failures);
        CheckSingleRegistration(failures);
        CheckProxySources(options, failures);
        CheckAuth(options, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    // SEAM-5: exactly one transport and one codec per client. The collection is complete once the provider is built, and
    // GetRequiredKeyedService would silently resolve the last of several (V4, section 11 item 8).
    private void CheckSingleRegistration(List<string> failures)
    {
        CheckRole<IAsyncHttpClient>("IAsyncHttpClient", "UseTransport", failures);
        CheckRole<ISerde>("ISerde", "UseSerde", failures);
    }

    private void CheckRole<TService>(string role, string installer, List<string> failures)
    {
        var candidates = registration.Services
            .Where(d => d.ServiceType == typeof(TService) && d.IsKeyedService && Equals(d.ServiceKey, registration.Name))
            .ToList();
        if (candidates.Count == 0)
        {
            failures.Add("client '" + registration.Name + "' has no " + role + ": call " + installer + "(...) on AddDexpaceClient(\"" + registration.Name + "\")");
        }
        else if (candidates.Count > 1)
        {
            failures.Add(
                "client '" + registration.Name + "' has " + candidates.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + role
                + " registrations (" + string.Join("; ", candidates.Select(Describe)) + "); register exactly one");
        }
    }

    private string Describe(ServiceDescriptor descriptor) =>
        ReferenceEquals(descriptor, registration.Transport.Descriptor) && registration.Transport.Description is { } transport ? transport
        : ReferenceEquals(descriptor, registration.Serde.Descriptor) && registration.Serde.Description is { } serde ? serde
        : ImplementationNames.Describe(descriptor);

    // P9-15: at most one proxy source per client; ConfigureTransport setting Proxy counts as an explicit one.
    private void CheckProxySources(DexpaceClientState state, List<string> failures)
    {
        if (state.Transport.Proxy is not null && registration.EnvironmentProxy is not null)
        {
            failures.Add(registration.Name + ": two proxy sources: explicit proxy settings and UseProxyFromEnvironment; use one");
        }
    }

    // P9-18: credentials that are silently not sent are the misconfiguration nobody sees.
    private void CheckAuth(DexpaceClientState state, List<string> failures)
    {
        if (state.Credentials is not null && registration.AuthDescriptor is null && registration.AuthPolicyFactory is null)
        {
            failures.Add(registration.Name + ": credentials are configured but nothing sends them: call UseAuth(descriptor) or UseAuthPolicy(...)");
        }
    }
}

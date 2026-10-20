// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>
/// TRANSPORT-20 and XCUT-4 (phase 11, task 2.1; design §10 entry 7, P11-7): a transport failure is an <see cref="IOException"/> that reports itself
/// retryable, and its suppressed trail is readable through <see cref="ExceptionTrail"/> and rendered by <c>ToString()</c> as an
/// <see cref="SdkException"/>'s is. The one shared renderer serves both families.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportExceptionTaxonomyTests
{
    [Fact]
    public void ServiceRequestException_is_an_IOException()
    {
        Assert.IsAssignableFrom<IOException>(new ServiceRequestException("x"));
        Assert.IsAssignableFrom<IOException>(new ServiceRequestTimeoutException("x"));
        Assert.IsAssignableFrom<IOException>(new ServiceRequestException("x", new TimeoutException()));
    }

    [Fact]
    public void ServiceRequestException_reports_itself_retryable()
    {
        Assert.True(((IRetryableError)new ServiceRequestException("x")).IsRetryable);
        Assert.True(((IRetryableError)new ServiceRequestTimeoutException("x")).IsRetryable);
        Assert.True(new ServiceRequestException().IsRetryable);
    }

    [Fact]
    public void A_suppressed_secondary_is_readable_through_the_trail()
    {
        var primary = new ServiceRequestException("primary");
        var secondary = new InvalidOperationException("secondary-failure");

        ExceptionTrail.AddSuppressed(primary, secondary);

        Assert.Same(secondary, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
        var text = primary.ToString();
        Assert.Contains("(Suppressed Exception #0)", text, StringComparison.Ordinal);
        Assert.Contains("secondary-failure", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mutual_trail_between_a_transport_failure_and_an_SdkException_renders_cycle_and_terminates()
    {
        var transport = new ServiceRequestException("transport");
        var sdk = new SdkException("sdk");
        ExceptionTrail.AddSuppressed(transport, sdk);
        ExceptionTrail.AddSuppressed(sdk, transport);

        Assert.Contains("(cycle)", transport.ToString(), StringComparison.Ordinal);
        Assert.Contains("(cycle)", sdk.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_transport_failure_with_no_trail_renders_as_the_base_exception_does()
    {
        var failure = new ServiceRequestException("plain", new TimeoutException("cause"));

        var text = failure.ToString();

        Assert.StartsWith("Dexpace.Sdk.Core.Errors.ServiceRequestException: plain", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Suppressed", text, StringComparison.Ordinal);
    }
}

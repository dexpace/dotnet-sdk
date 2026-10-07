// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

[Trait("Category", "Unit")]
public class DexpaceClientOptionsTests
{
    [Fact]
    public void DexpaceClientOptions_Defaults_AreCorrect()
    {
        var opts = new DexpaceClientOptions();

        Assert.Null(opts.BaseAddress);
        Assert.NotNull(opts.UserAgent);
        Assert.StartsWith("dexpace-dotnet/", opts.UserAgent);
        Assert.Null(opts.OverallTimeout);
        Assert.Null(opts.AttemptTimeout);
        Assert.NotNull(opts.Retry);
        Assert.NotNull(opts.Redirect);
    }

    [Fact]
    public void Logging_defaults_to_HttpLoggingOptions_Default()
    {
        var opts = new DexpaceClientOptions();

        Assert.Same(HttpLoggingOptions.Default, opts.Logging);
        Assert.Equal(HttpLogLevel.None, opts.Logging.Level);
        Assert.Throws<ArgumentNullException>(() => opts.Logging = null!);
    }

    [Fact]
    public void RetryOptions_Defaults_AreCorrect()
    {
        var retry = new RetryOptions();

        Assert.Equal(3, retry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), retry.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), retry.MaxDelay);
        Assert.True(retry.HonorRetryAfter);
        Assert.False(retry.RetryNonIdempotentWhenReplayable);
    }

    [Fact]
    public void RedirectOptions_Defaults_AreCorrect()
    {
        var redirect = new RedirectOptions();

        Assert.Equal(20, redirect.MaxRedirects);
        Assert.False(redirect.AllowHttpsToHttpDowngrade);
        Assert.True(redirect.StripSensitiveHeadersOnCrossOrigin);
    }

    [Fact]
    public void DexpaceClientOptions_RetryAndRedirect_AreNonNullOnFreshInstance()
    {
        var opts = new DexpaceClientOptions();

        // Property bag objects must be initialized — not null — so callers can do
        // opts.Retry.MaxRetryAttempts = 5 without a null ref.
        Assert.NotNull(opts.Retry);
        Assert.NotNull(opts.Redirect);
    }

    [Fact]
    public void BaseAddress_is_read_by_BuildRequest()
    {
        // SEAM-27: BaseAddress is no longer inert; OperationDescriptor.BuildRequest(DexpaceClientOptions) reads it.
        var options = new DexpaceClientOptions { BaseAddress = new Uri("https://api.example.com/v1") };
        var descriptor = new Dexpace.Sdk.Core.Operations.OperationDescriptor { Method = Dexpace.Sdk.Core.Http.Common.Method.Get, PathTemplate = "/pets" };

        Assert.Equal("https://api.example.com/v1/pets", descriptor.BuildRequest(options).Url.AbsoluteUri);
    }
}

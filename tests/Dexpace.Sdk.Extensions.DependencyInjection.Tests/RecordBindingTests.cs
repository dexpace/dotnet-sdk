// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-5, CFG-6, CFG-7 (P9-4, P9-8, P9-9): one row per key of the client, retry, redirect and logging records, with a bad value for each kind.</summary>
[Trait("Category", "Unit")]
public sealed class RecordBindingTests
{
    private static ClientConfiguration Ok(params (string, string?)[] values)
    {
        var configuration = Config.Read(values);
        Assert.Empty(configuration.Failures);
        return configuration;
    }

    private static string Fail(params (string, string?)[] values) => Assert.Single(Config.Failures(Config.Read(values)));

    [Fact]
    public void BaseAddress_binds_an_absolute_http_URI()
    {
        Assert.Equal(new Uri("https://api.test/"), Ok(("BaseAddress", "https://api.test/")).Options.BaseAddress);
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("/relative")]
    [InlineData("ftp://host/")]
    [InlineData("https://user:s3cr3t@api.test/p?token=s3cr3t#frag")]
    public void A_bad_BaseAddress_fails_without_the_URL(string value)
    {
        var failure = Fail(("BaseAddress", value));

        Assert.StartsWith(Config.SectionPath + ":BaseAddress: ", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("api.test", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void UserAgent_binds_and_a_control_character_fails_without_the_value()
    {
        Assert.Equal("acme/1.0", Ok(("UserAgent", "acme/1.0")).Options.UserAgent);

        var failure = Fail(("UserAgent", "bad\r\nX-Injected: s3cr3t"));

        Assert.Contains("UserAgent", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PT30S", 30)]
    [InlineData("30s", 30)]
    public void The_two_timeouts_bind_durations(string text, int seconds)
    {
        var options = Ok(("OverallTimeout", text), ("AttemptTimeout", text)).Options;

        Assert.Equal(TimeSpan.FromSeconds(seconds), options.OverallTimeout);
        Assert.Equal(TimeSpan.FromSeconds(seconds), options.AttemptTimeout);
    }

    [Fact]
    public void None_clears_a_nullable_timeout_and_zero_and_negatives_fail_through_the_record()
    {
        Assert.Null(Ok(("OverallTimeout", "none")).Options.OverallTimeout);
        Assert.Contains("OverallTimeout", Fail(("OverallTimeout", "0")), StringComparison.Ordinal);
        Assert.Contains("AttemptTimeout", Fail(("AttemptTimeout", "-1s")), StringComparison.Ordinal);
        Assert.Contains("greater than zero", Fail(("AttemptTimeout", "50d")), StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_keys_bind()
    {
        var retry = Ok(
            ("Retry:MaxRetryAttempts", "3"),
            ("Retry:BaseDelay", "250ms"),
            ("Retry:MaxDelay", "PT10S"),
            ("Retry:FixedDelay", "1s"),
            ("Retry:Multiplier", "1.5"),
            ("Retry:Jitter", "0.1"),
            ("Retry:HonorRetryAfter", "false"),
            ("Retry:AttemptHeaderName", "X-Attempt")).Options.Retry;

        Assert.Equal(3, retry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(250), retry.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), retry.MaxDelay);
        Assert.Equal(TimeSpan.FromSeconds(1), retry.FixedDelay);
        Assert.Equal(1.5, retry.Multiplier);
        Assert.Equal(0.1, retry.Jitter);
        Assert.False(retry.HonorRetryAfter);
        Assert.Equal("X-Attempt", retry.AttemptHeaderName);
        Assert.Null(Ok(("Retry:FixedDelay", "none")).Options.Retry.FixedDelay);
    }

    [Fact]
    public void A_negative_retry_budget_is_the_records_rejection_reported_under_the_key()
    {
        // CFG-5: the negative is parsed and handed to the record, whose accessor decides.
        var failure = Fail(("Retry:MaxRetryAttempts", "-1"));

        Assert.StartsWith(Config.SectionPath + ":Retry:MaxRetryAttempts: ", failure, StringComparison.Ordinal);
        Assert.Contains("non-negative", failure, StringComparison.Ordinal);
        Assert.Contains("32-bit", Fail(("Retry:MaxRetryAttempts", "2147483648")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Retry:Multiplier", "0.5")]
    [InlineData("Retry:Jitter", "2")]
    [InlineData("Retry:BaseDelay", "abc")]
    [InlineData("Retry:HonorRetryAfter", "yes")]
    [InlineData("Retry:AttemptHeaderName", "bad name")]
    [InlineData("Retry:RetryableStatusCodes", "99")]
    [InlineData("Retry:RetryableStatusCodes", "600")]
    public void A_bad_retry_value_fails_under_its_key(string key, string value)
    {
        var failure = Fail((key, value));

        Assert.StartsWith(Config.SectionPath + ":" + key + ": ", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Retryable_status_codes_replace_the_default_in_both_list_forms_and_none_is_empty()
    {
        Assert.Equal(new HashSet<int> { 429, 503 }, Ok(("Retry:RetryableStatusCodes", "429, 503")).Options.Retry.RetryableStatusCodes);
        Assert.Equal(
            new HashSet<int> { 502 },
            Ok(("Retry:RetryableStatusCodes:0", "502")).Options.Retry.RetryableStatusCodes);
        Assert.Empty(Ok(("Retry:RetryableStatusCodes", "none")).Options.Retry.RetryableStatusCodes);
    }

    [Fact]
    public void Redirect_keys_bind_and_the_methods_replace_the_default_through_Method_Of()
    {
        var redirect = Ok(
            ("Redirect:MaxRedirects", "5"),
            ("Redirect:AllowHttpsToHttpDowngrade", "true"),
            ("Redirect:FollowSeeOther", "true"),
            ("Redirect:AllowedMethods", "GET, HEAD, POST")).Options.Redirect;

        Assert.Equal(5, redirect.MaxRedirects);
        Assert.True(redirect.AllowHttpsToHttpDowngrade);
        Assert.True(redirect.FollowSeeOther);
        Assert.Equal(new HashSet<Method> { Method.Get, Method.Head, Method.Post }, redirect.AllowedMethods);
        Assert.Empty(Ok(("Redirect:AllowedMethods", "none")).Options.Redirect.AllowedMethods);
    }

    [Fact]
    public void A_method_token_with_a_space_fails_without_the_token()
    {
        var failure = Fail(("Redirect:AllowedMethods", "GET, S3CR3T VERB"));

        Assert.Contains("item 1", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("S3CR3T", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Redirect_predicate_is_code_only()
    {
        Assert.Equal(Config.SectionPath + ":Redirect:Predicate: unknown key", Fail(("Redirect:Predicate", "x")));
    }

    [Fact]
    public void Logging_keys_bind_and_the_lists_replace_rather_than_append()
    {
        var logging = Ok(
            ("Logging:BodyPreviewSize", "100"),
            ("Logging:AllowedHeaderNames", "x-one, x-two"),
            ("Logging:AllowedQueryParameters:0", "page"),
            ("Logging:UrlValuedHeaderNames", "none"),
            ("Logging:OmitDisallowedHeaders", "true")).Options.Logging;

        Assert.Equal(100, logging.BodyPreviewSize);
        Assert.Equal(["x-one", "x-two"], logging.AllowedHeaderNames);
        Assert.Equal(["page"], logging.AllowedQueryParameters);
        Assert.Empty(logging.UrlValuedHeaderNames);
        Assert.True(logging.OmitDisallowedHeaders);
        Assert.DoesNotContain("content-type", logging.AllowedHeaderNames);
    }

    [Fact]
    public void A_negative_body_preview_fails_through_the_record()
    {
        Assert.Contains("Logging:BodyPreviewSize", Fail(("Logging:BodyPreviewSize", "-1")), StringComparison.Ordinal);
    }
}

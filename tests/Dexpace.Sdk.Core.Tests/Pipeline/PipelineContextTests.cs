// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public class PipelineContextTests
{
    private static Request MakeRequest() =>
        Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public void Copies_share_the_call_scoped_state_and_not_the_per_drive_state()
    {
        var options = new DexpaceClientOptions();
        var requestOptions = new RequestOptions { MaxRetries = 1 };
        var seed = MakeRequest();
        var context = TestContexts.For(seed, options, requestOptions);

        var attempt = context.ForAttempt(2);
        var hop = context.ForHop(1);

        Assert.NotSame(context, attempt);
        Assert.Equal(2, attempt.AttemptNumber);
        Assert.Equal(0, context.AttemptNumber);
        Assert.Equal(1, hop.HopNumber);
        Assert.Equal(0, context.HopNumber);
        Assert.All(new[] { attempt, hop }, copy =>
        {
            Assert.Same(seed, copy.SeedRequest);
            Assert.Same(requestOptions, copy.RequestOptions);
            Assert.Same(options, copy.Options);
            Assert.Equal(context.CallKey, copy.CallKey);
            Assert.Same(context.Instrumentation, copy.Instrumentation);
        });
    }

    [Fact]
    public void The_property_bag_is_keyed_by_reference_identity()
    {
        var context = TestContexts.For();
        var first = new PipelinePropertyKey<string>("k");
        var sameName = new PipelinePropertyKey<string>("k");

        context.SetProperty(first, "value");

        Assert.True(context.TryGetProperty(first, out var value));
        Assert.Equal("value", value);
        Assert.False(context.TryGetProperty(sameName, out _));
    }

    [Fact]
    public void The_property_bag_is_shared_across_copies()
    {
        var context = TestContexts.For();
        var key = new PipelinePropertyKey<int>("n");

        context.ForAttempt(1).SetProperty(key, 7);

        Assert.True(context.ForHop(3).TryGetProperty(key, out var value));
        Assert.Equal(7, value);
    }

    [Fact]
    public void A_key_set_to_null_reads_back_as_present_with_null()
    {
        var context = TestContexts.For();
        var reference = new PipelinePropertyKey<string?>("reference");
        var nullable = new PipelinePropertyKey<int?>("nullable");

        context.SetProperty(reference, null);
        context.SetProperty(nullable, null);

        Assert.True(context.TryGetProperty(reference, out var referenceValue));
        Assert.Null(referenceValue);
        Assert.True(context.TryGetProperty(nullable, out var nullableValue));
        Assert.Null(nullableValue);
    }

    [Fact]
    public void A_missing_key_reports_false_and_a_default()
    {
        var context = TestContexts.For();

        Assert.False(context.TryGetProperty(new PipelinePropertyKey<string>("missing"), out var value));
        Assert.Null(value);
    }

    [Fact]
    public void WithCancellationToken_and_WithActivity_change_one_drive_value_only()
    {
        using var cts = new CancellationTokenSource();
        using var activity = new Activity("probe");
        var context = TestContexts.For();

        var withToken = context.WithCancellationToken(cts.Token);
        var withActivity = context.WithActivity(activity);

        Assert.Equal(cts.Token, withToken.CancellationToken);
        Assert.Equal(CancellationToken.None, context.CancellationToken);
        Assert.Null(withToken.Activity);
        Assert.Same(activity, withActivity.Activity);
        Assert.Null(context.Activity);
        Assert.Equal(CancellationToken.None, withActivity.CancellationToken);
    }

    [Fact]
    public void The_seed_request_is_the_request_passed_at_entry()
    {
        var seed = MakeRequest();

        Assert.Same(seed, TestContexts.For(seed).SeedRequest);
    }

    [Fact]
    public void The_context_offers_no_way_to_write_the_request_upward()
    {
        var members = typeof(PipelineContext).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members.OfType<PropertyInfo>(),
            property => property.PropertyType == typeof(Request) && property.SetMethod is { IsPublic: true });
        Assert.DoesNotContain(
            members.OfType<MethodInfo>(),
            method => method.ReturnType == typeof(void) && method.GetParameters().Any(p => p.ParameterType == typeof(Request)));
    }

    [Fact]
    public void There_is_no_public_constructor()
    {
        Assert.Empty(typeof(PipelineContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void The_context_exposes_no_Request_or_Response_member()
    {
        Assert.Null(typeof(PipelineContext).GetProperty("Request"));
        Assert.Null(typeof(PipelineContext).GetProperty("Response"));
    }

    [Fact]
    public void RequestOptions_defaults_to_Empty_when_none_is_given()
    {
        Assert.Same(RequestOptions.Empty, TestContexts.For().RequestOptions);
    }

    [Fact]
    public void Defaults_for_attempt_hop_and_activity()
    {
        var context = TestContexts.For();

        Assert.Equal(0, context.AttemptNumber);
        Assert.Equal(0, context.HopNumber);
        Assert.Null(context.Activity);
    }
}

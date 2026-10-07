// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using System.Reflection;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
[Collection("Instrumentation")]
public sealed class ConcurrencyTests
{
    [Fact]
    public async Task Concurrent_calls_share_no_per_call_state()
    {
        // PIPE-10: 64 parallel sends through one pipeline; no context instance is seen by two calls, and each context
        // carries its own call's seed request.
        var seen = new ConcurrentBag<(PipelineContext Context, Uri Seed, Uri Held)>();
        var probe = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            seen.Add((context, context.SeedRequest.Url, request.Url));
            return next.RunAsync(request, context);
        });
        using var probed = new PipelineBuilder().Add(probe).Add(new IdempotencyPolicy()).Add(new ClientIdentityPolicy()).Build(new RecordingTransport());

        var calls = Enumerable.Range(0, 64).Select(i => Task.Run(
            async () =>
            {
                using var response = await probed.SendAsync(Request.Get($"https://api.example.com/{i}"), TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken));
        await Task.WhenAll(calls);

        Assert.Equal(64, seen.Count);
        Assert.Equal(64, seen.Select(s => s.Context).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(seen, s => Assert.Equal(s.Seed, s.Held));
        Assert.Equal(64, seen.Select(s => s.Context.CallKey).Distinct().Count());
    }

    [Fact]
    public void Every_shipped_policy_holds_only_readonly_instance_fields()
    {
        // PIPE-11: per-call state lives on the context, never on the policy.
        var policies = typeof(HttpPipelinePolicy).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == typeof(RetryPolicy).Namespace && typeof(HttpPipelinePolicy).IsAssignableFrom(t))
            .ToArray();

        Assert.NotEmpty(policies);
        foreach (var type in policies)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.All(fields, field => Assert.True(field.IsInitOnly, $"{type.Name}.{field.Name} must be readonly"));
        }
    }

    [Fact]
    public void The_default_pipeline_holds_only_readonly_instance_fields_in_its_policies()
    {
        using var pipeline = DexpacePipeline.CreateDefault(new RecordingTransport());

        Assert.All(pipeline.Policies, policy => Assert.All(
            policy.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => Assert.True(field.IsInitOnly)));
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class AsyncErrorModelTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    public static TheoryData<string> ShippedPolicies => new(
        nameof(OperationPolicy),
        nameof(IdempotencyPolicy),
        nameof(ClientIdentityPolicy),
        nameof(SetDatePolicy),
        nameof(InstrumentationPolicy),
        nameof(RedirectPolicy),
        nameof(RetryPolicy),
        nameof(ErrorMappingPolicy),
        nameof(BasicAuthPolicy),
        nameof(ApiKeyAuthPolicy),
        nameof(BearerTokenAuthPolicy));

    internal static HttpPipelinePolicy Shipped(string name) => name switch
    {
        nameof(OperationPolicy) => new OperationPolicy(),
        nameof(IdempotencyPolicy) => new IdempotencyPolicy(),
        nameof(ClientIdentityPolicy) => new ClientIdentityPolicy(),
        nameof(SetDatePolicy) => new SetDatePolicy(),
        nameof(InstrumentationPolicy) => new InstrumentationPolicy(),
        nameof(RedirectPolicy) => new RedirectPolicy(),
        nameof(RetryPolicy) => new RetryPolicy(),
        nameof(ErrorMappingPolicy) => new ErrorMappingPolicy(),
        nameof(BasicAuthPolicy) => new BasicAuthPolicy(new BasicCredential("u", "p")),
        nameof(ApiKeyAuthPolicy) => new ApiKeyAuthPolicy(new ApiKeyCredential("k")),
        nameof(BearerTokenAuthPolicy) => new BearerTokenAuthPolicy(new StaticTokenCredential(), "scope"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Fact]
    public async Task A_synchronous_throw_from_a_policy_becomes_a_faulted_task()
    {
        var failure = new InvalidOperationException("boom");
        var runner = TestContexts.RunnerOver(
            new RecordingTransport(),
            new ThrowingSynchronouslyPolicy(PipelineStage.Operation, failure));

        // Calling RunAsync does not throw; awaiting does, with the same instance (PIPE-30).
        var task = runner.RunAsync(MakeRequest(), TestContexts.For());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task.AsTask());
        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task An_out_of_memory_exception_is_not_caught_by_any_sdk_frame()
    {
        // Design §10 entry 12: no SDK frame catches a fatal exception, so it surfaces as the same instance with no
        // trail entry.
#pragma warning disable CA2201 // The point: the runtime-reserved fatal type must pass through every SDK frame.
        var fatal = new OutOfMemoryException("simulated");
#pragma warning restore CA2201
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy())
            .Add(new InstrumentationPolicy())
            .Build(new ScriptedTransport(fatal));

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken).AsTask());

        Assert.Same(fatal, thrown);
        Assert.Empty(ExceptionTrail.GetSuppressed(thrown));
    }

    [Theory]
    [MemberData(nameof(ShippedPolicies))]
    public async Task A_shipped_policy_reports_a_failure_as_a_faulted_task(string name)
    {
        var policy = Shipped(name);
        var runner = TestContexts.RunnerOver(new RecordingTransport());

        // PIPE-29: an argument failure never throws synchronously; it faults the task.
        var task = policy.ProcessAsync(null!, TestContexts.For(), runner);

        await Assert.ThrowsAsync<ArgumentNullException>(() => task.AsTask());
    }

    internal sealed class StaticTokenCredential : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}

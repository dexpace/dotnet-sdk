// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

#pragma warning disable CA2000 // ScriptedTransport and the responses it hands out own nothing that needs disposing in a test.

/// <summary>
/// RETRY-38 "MUST preserve any existing idempotency key": the attempt stamp may not be written under the header the
/// idempotency step is configured to use, whatever that header is called, on either retry stack. The literal
/// <c>Idempotency-Key</c> is refused by <c>RetryOptions</c> itself; a configured name is only known where the two are
/// assembled, so each stack refuses the pair where it is assembled and the pipeline refuses a per-call override.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AttemptHeaderKeyCollisionTests
{
    private const string KeyHeader = "X-Request-Key";

    private static Request Post() =>
        Request.Post("https://api.example.com/v1/items", RequestBody.FromBytes(new byte[] { 1, 2, 3 }));

    private static IdempotencyKeyStep KeyStep(string name = KeyHeader) => new() { HeaderName = HttpHeaderName.Of(name) };

    private static DexpaceClientOptions Options(string? attemptHeader) => new()
    {
        Retry = new RetryOptions { MaxRetryAttempts = 2, BaseDelay = TimeSpan.Zero, Jitter = 0, AttemptHeaderName = attemptHeader },
    };

    private static RetryOptions RetryOptionsWith(string? attemptHeader) =>
        new() { MaxRetryAttempts = 2, BaseDelay = TimeSpan.Zero, Jitter = 0, AttemptHeaderName = attemptHeader };

    private static Response Unavailable() => TestResponses.Create(Status.ServiceUnavailable);

    private static PipelineBuilder KeyedRetryBuilder(string keyHeader = KeyHeader) =>
        new PipelineBuilder().Add(new IdempotencyPolicy(KeyStep(keyHeader))).Add(new RetryPolicy(new InstantTimeProvider()));

    // ---- the stage stack, at assembly --------------------------------------------------------------------------------

    [Theory]
    [InlineData("X-Request-Key")]
    [InlineData("x-request-key")]
    [InlineData("X-REQUEST-KEY")]
    public void Build_refuses_an_attempt_header_that_is_the_configured_idempotency_header(string attemptHeader)
    {
        var builder = KeyedRetryBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build(new ScriptedTransport(), Options(attemptHeader)));

        Assert.Contains(KeyHeader, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RETRY-38", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_refuses_the_pair_whatever_the_order_the_policies_were_added_in()
    {
        var builder = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Add(new IdempotencyPolicy(KeyStep()));

        Assert.Throws<InvalidOperationException>(() => builder.Build(new ScriptedTransport(), Options(KeyHeader)));
    }

    [Fact]
    public void The_default_pipeline_with_a_custom_key_header_refuses_the_same_attempt_header()
    {
        var builder = new PipelineBuilder().AddStandardResilience(new InstantTimeProvider()).Add(new IdempotencyPolicy(KeyStep()));

        Assert.Throws<InvalidOperationException>(() => builder.Build(new ScriptedTransport(), Options(KeyHeader)));
    }

    [Fact]
    public void A_flattened_builder_that_gains_a_colliding_key_policy_refuses_at_build()
    {
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(new ScriptedTransport(), Options(KeyHeader));
        var builder = PipelineBuilder.Flatten(pipeline).Add(new IdempotencyPolicy(KeyStep()));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_accepts_distinct_names_and_the_cases_where_the_stamp_cannot_run()
    {
        var transport = new ScriptedTransport();

        Assert.NotNull(KeyedRetryBuilder().Build(transport, Options("X-Attempt")));
        Assert.NotNull(KeyedRetryBuilder().Build(transport, Options(null)));

        // No retry policy: nothing stamps, so the same name is not a collision.
        Assert.NotNull(new PipelineBuilder().Add(new IdempotencyPolicy(KeyStep())).Build(transport, Options(KeyHeader)));

        // No idempotency policy: nothing owns the header.
        Assert.NotNull(new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(transport, Options(KeyHeader)));
    }

    // ---- the stage stack, per call -----------------------------------------------------------------------------------

    [Fact]
    public async Task A_per_call_option_override_that_collides_faults_before_anything_is_sent()
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = KeyedRetryBuilder().Build(transport, Options(null));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => pipeline.SendAsync(Post(), Options(KeyHeader), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("options", ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void A_per_call_option_override_that_collides_throws_on_the_sync_face_before_anything_is_sent()
    {
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = KeyedRetryBuilder().Build(transport, Options(null));

        var ex = Assert.Throws<ArgumentException>(() => pipeline.Send(Post(), Options(KeyHeader), TestContext.Current.CancellationToken));

        Assert.Equal("options", ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task A_per_call_override_that_does_not_collide_keeps_the_key_on_every_attempt()
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = KeyedRetryBuilder().Build(transport, Options(null));

        using var response = await pipeline.SendAsync(Post(), Options("X-Attempt"), TestContext.Current.CancellationToken);

        var keys = transport.Requests.Select(r => r.Headers.Get(KeyHeader)).ToArray();
        Assert.Equal(3, keys.Length);
        Assert.NotNull(keys[0]);
        Assert.All(keys, k => Assert.Equal(keys[0], k));
        Assert.Equal(["1", "2", "3"], transport.Requests.Select(r => r.Headers.Get("X-Attempt")));
    }

    // ---- the recovery stack, at assembly ------------------------------------------------------------------------------

    [Theory]
    [InlineData("X-Request-Key")]
    [InlineData("x-request-key")]
    public void A_dispatcher_refuses_a_retry_whose_attempt_header_is_a_request_step_s_idempotency_header(string attemptHeader)
    {
        var requests = new RequestRecoveryChain([KeyStep()]);
        var retry = new RetryRecovery(RetryOptionsWith(attemptHeader));

        var ex = Assert.Throws<ArgumentException>(() => new RecoveryDispatcher(requests, ResponseRecoveryChain.Empty, retry));

        Assert.Equal("retry", ex.ParamName);
        Assert.Contains(KeyHeader, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RETRY-38", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dispatcher_accepts_distinct_names_and_a_chain_with_no_key_step()
    {
        var keyed = new RequestRecoveryChain([KeyStep()]);

        Assert.NotNull(new RecoveryDispatcher(keyed, ResponseRecoveryChain.Empty, new RetryRecovery(RetryOptionsWith("X-Attempt"))));
        Assert.NotNull(new RecoveryDispatcher(keyed, ResponseRecoveryChain.Empty, new RetryRecovery(RetryOptionsWith(null))));
        Assert.NotNull(new RecoveryDispatcher(RequestRecoveryChain.Empty, ResponseRecoveryChain.Empty, new RetryRecovery(RetryOptionsWith(KeyHeader))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_recovery_stack_with_distinct_names_sends_one_key_on_every_attempt(bool async)
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        var dispatcher = new RecoveryDispatcher(
            new RequestRecoveryChain([KeyStep()]),
            ResponseRecoveryChain.Empty,
            new RetryRecovery(RetryOptionsWith("X-Attempt"), timeProvider: new InstantTimeProvider()));
        var token = TestContext.Current.CancellationToken;

        using var response = async
            ? await dispatcher.DispatchAsync(transport, Post(), RequestOptions.Empty, token)
            : await Task.Run(() => dispatcher.Dispatch(transport, Post(), RequestOptions.Empty, token), token);

        var keys = transport.Requests.Select(r => r.Headers.Get(KeyHeader)).ToArray();
        Assert.Equal(3, keys.Length);
        Assert.NotNull(keys[0]);
        Assert.All(keys, k => Assert.Equal(keys[0], k));
        Assert.Equal(["1", "2", "3"], transport.Requests.Select(r => r.Headers.Get("X-Attempt")));
    }
}

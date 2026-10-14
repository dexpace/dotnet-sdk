// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.TestSupport.Recovery;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-9</c> to <c>xcut-12</c> (plan 1.6).</summary>
[Trait("Category", "Unit")]
public sealed class InvariantResilienceControlTests
{
    // One lock for every cache of the control, so a fetch parked in one cache holds every other: the scope the requirement forbids.
    private static readonly SemaphoreSlim s_globalLock = new(1, 1);

    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        new("xcut-9.cycle-safe-walk", TransportFace.None, "tracks visited causes by Equals", () => Walking(BrokenPipelines.WalkByEquals), Because: "reference identity"),
        new("xcut-9.cycle-safe-walk", TransportFace.None, "has no visited set", () => Walking(BrokenPipelines.WalkWithoutVisitedSet), Because: "must terminate"),
        .. Both("xcut-10.safety-gate-uniform", "special-cases a transport failure", "body-less POST failing with a transport error", () => BrokenPipelines.Subject("gate skips transport failures", _ => [new BrokenPipelines.RetryLoop(gateTransportFailures: false, gateProtocolFailures: true)])),
        .. Both("xcut-10.safety-gate-uniform", "has no safety gate at all", "body-less POST failing with a protocol error", () => BrokenPipelines.Subject("no gate", _ => [new BrokenPipelines.RetryLoop(gateTransportFailures: false, gateProtocolFailures: false)])),
        .. Both("xcut-11.shared-no-crosstalk", "holds the request in a field of the shared step", "answer its own request", () => BrokenPipelines.Subject("field", _ => [new BrokenPipelines.HoldsRequestInAField()])),
        .. Both("xcut-12.single-flight-scoped", "fetches once per caller", "single-flight", () => new InvariantSubject { Name = "broken: per caller", CreateTokenCache = (credential, _) => (context, token) => credential.GetTokenAsync(context, token) }),
        .. Both("xcut-12.single-flight-scoped", "serialises every cache behind one lock", "different credential's cache", () => new InvariantSubject { Name = "broken: global lock", CreateTokenCache = GlobalLockCache }),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows, "xcut-9.cycle-safe-walk");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    [Fact]
    public async Task The_cycle_assertion_is_NotExercised_naming_its_hook_when_none_is_supplied()
    {
        var result = await InvariantSuite.RunAsync(new InvariantSubject(), InvariantControlRow.Find("xcut-9.cycle-safe-walk"), TransportFace.None, InvariantControlRow.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(InvariantSubject.CreateCyclicException), result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public Task A_cycle_safe_walk_passes_over_a_self_cycle_and_a_two_node_cycle() =>
        Task.WhenAll(
            InvariantControlRow.RunPositiveAsync("xcut-9.cycle-safe-walk", TransportFace.None, new InvariantSubject { Name = "self cycle", CreateCyclicException = CyclicExceptions.SelfCycle }),
            InvariantControlRow.RunPositiveAsync("xcut-9.cycle-safe-walk", TransportFace.None, new InvariantSubject { Name = "two-node cycle", CreateCyclicException = CyclicExceptions.TwoNodeCycle }));

    private static InvariantSubject Walking(Func<Exception, IEnumerable<Exception>> walk) =>
        new() { Name = "broken: walk", CreateCyclicException = CyclicExceptions.SelfCycle, EnumerateCauses = walk };

    private static Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> GlobalLockCache(TokenCredential credential, TimeProvider time)
    {
        AccessToken? cached = null;
        return async (context, token) =>
        {
            await s_globalLock.WaitAsync(token);
            try
            {
                cached ??= await credential.GetTokenAsync(context, token);
                return cached.Value;
            }
            finally
            {
                s_globalLock.Release();
            }
        };
    }

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];
}

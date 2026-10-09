// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

[Trait("Category", "Unit")]
public sealed class SuiteContextTests
{
    private static readonly TransportSuiteOptions s_defaults = new();
    private static readonly ProxyOptions s_proxy = new() { Host = "proxy.invalid", Port = 3128 };

    private static SuiteContext ContextFor(TransportSubject subject, TransportFace face = TransportFace.Async) => new(subject, face, s_defaults);

    [Fact]
    public async Task Supplies_says_which_faces_the_subject_can_build()
    {
        var asyncOnly = new FakeSubject().Subject(blocking: false);

        Assert.True(SuiteContext.Supplies(asyncOnly, TransportFace.Async));
        Assert.False(SuiteContext.Supplies(asyncOnly, TransportFace.Blocking));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Each_hook_that_is_missing_names_itself_in_the_not_exercised_reason()
    {
        await using var context = ContextFor(new FakeSubject().Subject());

        AssertNamed(nameof(TransportSubject.CreateBorrowed), () => context.RequireBorrowed());
        AssertNamed(nameof(TransportSubject.CreateWithFaultingAdaptation), () => context.RequireFaultingAdaptation());
        AssertNamed(nameof(TransportSubject.CreateWithInternalCancel), () => context.RequireInternalCancel());
        AssertNamed(nameof(TransportSubject.CreateWithNativeResend), () => context.RequireNativeResend());
        AssertNamed(nameof(TransportSubject.CreateWithProxy), () => context.RequireProxy(s_proxy));
    }

    private static void AssertNamed(string hook, Action require)
    {
        var error = Assert.Throws<NotExercisedException>(require);
        Assert.Contains(hook, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hooks_that_are_supplied_build_through_the_settings_and_their_transports_are_disposed_with_the_context()
    {
        var built = new List<FakeTransport>();
        await using var owner = new OwnerProbe();
        TransportSettings? seen = null;
        var subject = new TransportSubject
        {
            Name = "hooks",
            CreateBorrowed = settings =>
            {
                seen = settings;
                var transport = new FakeTransport();
                built.Add(transport);
                return new BorrowedTransport(transport, (_, _) => Task.CompletedTask, owner);
            },
            CreateWithInternalCancel = _ =>
            {
                var transport = new FakeTransport();
                built.Add(transport);
                return new InternalCancellation(transport, () => { });
            },
            CreateWithNativeResend = _ => Track(new FakeTransport(), built),
            CreateWithFaultingAdaptation = _ => Track(new FakeTransport(), built),
            CreateWithProxy = (_, _) => Track(new FakeTransport(), built),
        };

        var context = ContextFor(subject);
        context.RequireBorrowed();
        context.RequireInternalCancel();
        context.RequireNativeResend();
        context.RequireFaultingAdaptation();
        context.RequireProxy(s_proxy);
        await context.DisposeAsync();

        Assert.Same(context.Logger, seen!.Logger);
        Assert.Equal(5, built.Count);
        Assert.All(built, transport => Assert.Equal(1, transport.Disposed));
        Assert.Equal(1, owner.Disposed);
    }

    private static FakeTransport Track(FakeTransport transport, List<FakeTransport> built)
    {
        built.Add(transport);
        return transport;
    }

    [Fact]
    public async Task Transports_are_disposed_before_servers_and_every_item_is_disposed_even_when_one_throws()
    {
        var order = new List<string>();
        var context = ContextFor(new TransportSubject
        {
            Name = "ordered",
            CreateAsync = _ => new OrderedTransport(order),
        });
        var server = context.StartServer();
        context.Own(new Probe("owned", order));
        context.CreateTransport();

        var error = await Assert.ThrowsAsync<ConformanceException>(async () => await context.DisposeAsync());

        Assert.Equal(["transport", "owned"], order);
        Assert.Contains("TRANSPORT-16", error.Message, StringComparison.Ordinal);
        await Assert.ThrowsAnyAsync<Exception>(async () => await server.WaitForRequestAsync(0, TestContext.Current.CancellationToken).WaitAsync(Waits.Bound, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_face_decides_which_wrapper_CreateTransport_builds()
    {
        var subject = new FakeSubject().Subject();
        await using var asyncContext = ContextFor(subject, TransportFace.Async);
        await using var blockingContext = ContextFor(subject, TransportFace.Blocking);

        Assert.IsType<AsyncFaceTransport>(asyncContext.CreateTransport());
        Assert.IsType<BlockingFaceTransport>(blockingContext.CreateTransport());
        Assert.Equal(TransportFace.Async, asyncContext.CreateAsyncTransport().Face);
    }

    [Fact]
    public async Task Asking_for_the_async_transport_of_a_blocking_only_subject_is_not_exercised()
    {
        await using var context = ContextFor(new FakeSubject().Subject(async: false), TransportFace.Blocking);

        Assert.Throws<NotExercisedException>(() => context.CreateAsyncTransport());
        await Task.CompletedTask;
    }

    private sealed class OwnerProbe : IAsyncDisposable
    {
        internal int Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Probe(string name, List<string> order) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            order.Add(name);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OrderedTransport(List<string> order) : IAsyncHttpClient
    {
        public Task<Dexpace.Sdk.Core.Http.Response.Response> ExecuteAsync(
            Dexpace.Sdk.Core.Http.Request.Request request,
            Dexpace.Sdk.Core.Http.Request.RequestOptions options,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            order.Add("transport");
            throw new InvalidOperationException("transport dispose failed");
        }
    }
}

// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

[Trait("Category", "Unit")]
public sealed class DisposalTests : IDisposable
{
    private readonly ActivitySource _source = new("Dexpace.Sdk.Core.Tests.Disposal");
    private readonly ActivityListener _listener;

    public DisposalTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == _source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private sealed class Resource(Exception? failure = null) : IDisposable, IAsyncDisposable
    {
        public int Disposals { get; private set; }

        public int AsyncDisposals { get; private set; }

        public void Dispose()
        {
            Disposals++;
            if (failure is not null)
            {
                throw failure;
            }
        }

        public ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            return failure is null ? ValueTask.CompletedTask : ValueTask.FromException(failure);
        }
    }

    private sealed class AsyncOnly : IAsyncDisposable
    {
        public int Disposals { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task A_null_resource_is_a_no_op()
    {
        Disposal.DisposeQuietly(null);
        await Disposal.DisposeQuietlyAsync(null);
    }

    [Fact]
    public async Task A_resource_that_disposes_cleanly_is_disposed_once_and_reports_nothing()
    {
        using var activity = _source.StartActivity("scope");
        var logger = new ListLogger();
        var sync = new Resource();
        var async = new Resource();

        Disposal.DisposeQuietly(sync, logger: logger);
        await Disposal.DisposeQuietlyAsync(async, logger: logger);

        Assert.Equal(1, sync.Disposals);
        Assert.Equal(1, async.AsyncDisposals);
        Assert.Empty(logger.Entries);
        Assert.Empty(activity!.Events);
    }

    [Fact]
    public async Task A_throwing_dispose_is_swallowed_and_never_propagates()
    {
        var sync = new Resource(new InvalidOperationException("boom"));
        var async = new Resource(new InvalidOperationException("boom"));

        Disposal.DisposeQuietly(sync);
        await Disposal.DisposeQuietlyAsync(async);

        Assert.Equal(1, sync.Disposals);
        Assert.Equal(1, async.AsyncDisposals);
    }

    [Fact]
    public async Task A_throwing_dispose_adds_an_exception_event_tagged_dexpace_dispose_suppressed_on_the_current_activity()
    {
        using var activity = _source.StartActivity("scope");
        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("boom")));
        await Disposal.DisposeQuietlyAsync(new Resource(new InvalidOperationException("boom")));

        var events = activity!.Events.ToList();
        Assert.Equal(2, events.Count);
        foreach (var e in events)
        {
            var tags = e.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("exception", e.Name);
            Assert.Equal(true, tags["dexpace.dispose.suppressed"]);
            Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
        }
    }

    [Fact]
    public void With_a_primary_in_flight_the_failure_lands_on_the_primary_trail_and_nothing_else()
    {
        using var activity = _source.StartActivity("scope");
        var primary = new TimeoutException("primary");
        var failure = new InvalidOperationException("boom");

        Disposal.DisposeQuietly(new Resource(failure), primary);

        Assert.Equal("primary", primary.Message);
        Assert.Null(primary.InnerException);
        Assert.Same(failure, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
        Assert.Empty(activity!.Events);
    }

    [Fact]
    public void With_a_primary_in_flight_and_a_logger_nothing_is_logged()
    {
        var logger = new ListLogger();

        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("boom")), new TimeoutException("p"), logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_fatal_dispose_exception_still_propagates_with_a_primary()
    {
        var primary = new TimeoutException("primary");

        Assert.Throws<InsufficientMemoryException>(
            () => Disposal.DisposeQuietly(new Resource(new InsufficientMemoryException()), primary));
        await Assert.ThrowsAsync<InsufficientMemoryException>(
            async () => await Disposal.DisposeQuietlyAsync(new Resource(new InsufficientMemoryException()), primary));
        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
    }

    [Fact]
    public async Task The_async_form_with_a_primary_attaches_to_the_trail()
    {
        var primary = new TimeoutException("primary");
        var failure = new InvalidOperationException("boom");

        await Disposal.DisposeQuietlyAsync(new Resource(failure), primary);

        Assert.Same(failure, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
    }

    [Fact]
    public void The_no_primary_branch_is_unchanged()
    {
        using var activity = _source.StartActivity("scope");

        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("boom")));

        Assert.Single(activity!.Events);
    }

    [Fact]
    public void With_a_logger_a_warning_names_the_resource_type_and_the_exception_type_and_never_a_value()
    {
        var logger = new ListLogger();

        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("secret-text")), logger: logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(Resource), entry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-text", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_listener_and_no_logger_nothing_is_observable_and_nothing_throws()
    {
        Assert.Null(Activity.Current);
        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task A_fatal_exception_propagates()
    {
        Assert.Throws<InsufficientMemoryException>(() => Disposal.DisposeQuietly(new Resource(new InsufficientMemoryException())));
        await Assert.ThrowsAsync<InsufficientMemoryException>(
            async () => await Disposal.DisposeQuietlyAsync(new Resource(new InsufficientMemoryException())));
    }

    [Fact]
    public async Task An_async_only_resource_is_disposed_through_DisposeAsync()
    {
        var resource = new AsyncOnly();
        await Disposal.DisposeQuietlyAsync(resource);
        Assert.Equal(1, resource.Disposals);
    }

    [Fact]
    public void The_suppressed_event_is_dexpace_dispose_suppressed_id_130_at_Warning()
    {
        var logger = new RecordingLogger();

        Disposal.DisposeQuietly(new Resource(new InvalidOperationException("boom")), logger: logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(130, entry.EventId.Id);
        Assert.Equal("dexpace.dispose.suppressed", entry.EventId.Name);
    }

    [Fact]
    public async Task The_event_carries_resource_type_and_error_type_keys_never_a_message()
    {
        var logger = new RecordingLogger();

        await Disposal.DisposeQuietlyAsync(new Resource(new InvalidOperationException("SECRET-MESSAGE")), logger: logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(nameof(Resource), entry[InternalLogKeys.DisposeResourceType]);
        Assert.Equal(typeof(InvalidOperationException).FullName, entry[DexpaceLogKeys.ErrorType]);
        Assert.DoesNotContain("SECRET-MESSAGE", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(entry.State, p => p.Value is string text && text.Contains("SECRET-MESSAGE", StringComparison.Ordinal));
    }
}

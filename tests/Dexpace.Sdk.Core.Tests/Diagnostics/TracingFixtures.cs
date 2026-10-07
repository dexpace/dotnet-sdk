// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>Builders shared by the tracing and metrics tests (phase 5c).</summary>
internal static class TracingFixtures
{
    /// <summary>
    /// The default composition's policies (operation, redirect, retry, instrumentation) over <paramref name="transport"/>,
    /// with instant retry waits. <c>DexpacePipeline.CreateDefault</c> does not contain <c>ErrorMappingPolicy</c>, so a
    /// test that needs a mapped 4xx/5xx passes <paramref name="mapped"/> (convention 5a).
    /// </summary>
    internal static HttpPipeline Pipeline(IAsyncHttpClient transport, bool mapped = false, int retries = 2, ILogger? logger = null)
    {
        var builder = new PipelineBuilder().AddStandardResilience(new InstantTimeProvider(), logger);
        if (mapped)
        {
            builder.Add(new ErrorMappingPolicy());
        }

        return builder.Build(transport, new DexpaceClientOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = retries,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                MaxDelay = TimeSpan.FromMilliseconds(5),
            },
        });
    }

    /// <summary>A started operation span under the recorder's listener, and a context whose bundle is that span.</summary>
    internal static (Activity Operation, PipelineContext Context) TracedContext(Request? request = null, DexpaceClientOptions? options = null)
    {
        var operation = DexpaceDiagnostics.ActivitySource.StartActivity("op", ActivityKind.Internal)
            ?? throw new InvalidOperationException("No Dexpace.Sdk listener is active.");
        var context = TestContexts.For(request, options, dispatch: new DispatchContext(InstrumentationContext.FromActivity(operation)));
        return (operation, context);
    }

    /// <summary>The events of an activity, in order, as names.</summary>
    internal static string[] EventNames(Activity activity) => [.. activity.Events.Select(e => e.Name)];

    /// <summary>The value of an event tag, or null.</summary>
    internal static object? EventTag(ActivityEvent activityEvent, string key) =>
        activityEvent.Tags.FirstOrDefault(t => t.Key == key).Value;

    /// <summary>The first event named <paramref name="name"/>.</summary>
    internal static ActivityEvent Event(Activity activity, string name) => activity.Events.First(e => e.Name == name);

    /// <summary>A listener that samples with propagation data only: spans exist but are not recording.</summary>
    internal static ActivityListener NonRecordingListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

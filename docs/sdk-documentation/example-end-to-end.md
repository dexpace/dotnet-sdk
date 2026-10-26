# One SDK slice, end to end

**As built by phase 12, written against source on 2026-10-26** (branch `109-phase-12-first-release`). [`getting-started.md`](./getting-started.md) sends one request; this page builds the thing
the toolkit exists for: the client of a small imaginary `widgets` API, written the way a generated or hand-written service SDK would write it. It has an operation, a typed response
handler that maps a status to the SDK's own exception, bearer authentication, a paged list over a `Link` header, retry and redirect switched on, redacted logging, and the dependency-injection
registration a consumer of that SDK would call. The two programs at the end are complete and are compiled by the documentation build against the built libraries with warnings as errors
(`dotnet run --project .claude/skills/housekeeping/src -- fences`, `quality-gates.md`); every excerpt before them is a slice of the first. The API, its host and its
token variable are made up. Each subsystem's own page ([`pipelines.md`](./pipelines.md), [`auth.md`](./auth.md), [`pagination.md`](./pagination.md), and so on) says what it guarantees; this one
shows them composed.

| Piece | What it shows | Page |
|---|---|---|
| `OperationDescriptor` | a request built from a template, a path parameter and the client's `BaseAddress` | [`seams.md`](./seams.md#the-operation-input-projection) |
| `WidgetHandler` | an `IResponseHandler<T>` that owns the response and maps a 404 to `WidgetNotFoundException` | `write-a-response-handler.md` |
| `BearerTokenAuthPolicy` | a token cache that refreshes early and retries once on a `401` | [`auth.md`](./auth.md) |
| `Pageable.Create` | a lazy walk over a `Link: <…>; rel="next"` header, capped at 50 pages | [`pagination.md`](./pagination.md) |
| `DexpaceClientOptions` | retry, redirect and the deadlines | [`retry.md`](./retry.md), [`redirect.md`](./redirect.md) |
| `HttpLoggingOptions` | redacted `http.request` / `http.response` events | [`logging-and-redaction.md`](./logging-and-redaction.md) |
| `AddDexpaceClient` | the same client registered from configuration | [`dependency-injection.md`](./dependency-injection.md) |

## The operation

A generated SDK declares each operation once, as data, and builds the request from it (`SEAM-26`). `BuildRequest(options)` reads `DexpaceClientOptions.BaseAddress`, joins it to the template with
exactly one separator and encodes each path parameter as one segment, so an id of `a/b` becomes `a%2Fb` and can never add a path segment (`SEAM-27`):

```csharp
private static readonly OperationDescriptor s_get = new() { Method = Method.Get, PathTemplate = "/widgets/{id}", OperationId = "getWidget" };

Request request = s_get.WithPathParameter("id", id).BuildRequest(options);   // https://api.example.com/v1/widgets/7
```

The descriptor is immutable, so one `static readonly` instance serves every call and `WithPathParameter` returns a copy. `OperationId` never reaches the URL, a header or the body.

## The handler

`IResponseHandler<T>` is the seam a generated SDK implements to turn a response into a value. The one rule: **the handler owns the response**, so it disposes it on every path, and the caller never
does. This one gives a 404 its own exception and defers every other status to the core handler, which decodes a 2xx and throws `HttpResponseException` for a 4xx or 5xx:

```csharp
internal sealed class WidgetHandler(ISerde serde) : IResponseHandler<Widget>
{
    private readonly IResponseHandler<Widget> _inner = ResponseHandlers.DeserializeOnSuccess<Widget>(serde);

    public async ValueTask<Widget> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        if (response.Status == Status.NotFound)
        {
            await using (response)   // this path does not delegate, so it disposes
            {
                throw new WidgetNotFoundException(response.Request.Url);
            }
        }

        return await _inner.HandleAsync(response, cancellationToken);
    }
}
```

`pipeline.SendAsync(request, handler.HandleAsync, options, token)` hands the response to the handler and disposes it afterwards in every outcome, attaching a failing dispose to the handler's
exception rather than replacing it ([`pipelines.md`](./pipelines.md)).

## Authentication

A `BearerTokenAuthPolicy` sits at `PipelineStage.Auth` and fronts any `TokenCredential` with an `AccessTokenCache`. The credential below is the part you write: where a token comes from. The cache
refreshes 30 seconds before expiry in the background, coalesces concurrent fetches per scope, and on a `401` with a `Bearer` challenge evicts the rejected token, fetches a fresh one and retries
once ([`auth.md`](./auth.md)). It refuses a non-`https` URL before a credential is resolved, and it withholds the token from a request a redirect moved to another origin.

```csharp
internal sealed class EnvironmentTokenCredential(string variable) : TokenCredential
{
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        ValueTask.FromResult(new AccessToken(
            Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException($"{variable} is not set."),
            DateTimeOffset.UtcNow.AddMinutes(30)));   // a real credential asks the identity provider and reports its real expiry
}
```

## Pagination

A pageable is a per-call object over a transport: build one per call and walk it. Each page is an ordinary pipeline call, so retry, redirect and the bearer policy govern every page. The `Link`
strategy follows `rel="next"`, resolves it against the response URL, and **ends the walk quietly** when the target is on another origin than the first request, so a hostile server cannot ask for your
token by naming itself:

```csharp
public AsyncPageable<Widget> List() =>
    Pageable.Create(
        pipeline,
        Request.Get(new Uri(options.BaseAddress!, "widgets").AbsoluteUri),
        serde,
        PaginationStrategies.LinkHeader<WidgetPage, Widget>(page => page.Items),
        maxPages: 50);   // set a finite cap in production
```

`await foreach (var widget in client.List())` yields items in server order and fetches a page only when the consumer passes the last item of the previous one. `AsPages()` yields the pages
with their status and headers instead. The page envelope is read through the same `ISerde`, and the response is closed before a page is yielded, so no connection is held while you work through the items.

## Retry, redirect and the deadlines

These are options, not code. Each record is validated where it is set, immutable, and changed with `with`:

```csharp
var options = new DexpaceClientOptions
{
    BaseAddress = new Uri("https://api.example.com/v1/"),
    OverallTimeout = TimeSpan.FromSeconds(45),    // across every attempt and redirect; expiry throws OperationTimeoutException
    AttemptTimeout = TimeSpan.FromSeconds(10),    // per attempt; expiry is retryable
    Retry = new RetryOptions { MaxRetryAttempts = 4 },
    Redirect = new RedirectOptions { MaxRedirects = 5 },
    Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers },
};
```

- **Retry** re-sends only what is safe to re-send: a request with no body and an idempotent method, or a body that is replayable (`RETRY-5`). It backs off exponentially with jitter, honours
  `Retry-After`, and stops at `OverallTimeout`. A `POST` with a single-use stream body is sent once unless you called `ToReplayableAsync()`. An `Idempotency-Key` is stamped on `POST`, `PUT` and `PATCH` once per
  call and reused by every attempt.
- **Redirect** follows `GET` and `HEAD` only, three hops by default (five here), and never rewrites a `POST` into a `GET`. `Authorization` is stripped on every hop and `Cookie` and
  `Proxy-Authorization` when the hop leaves the seed request's origin, with no option to turn that off. An `https` to `http` hop throws `RedirectSchemeDowngradeException`.
- The transport never follows a redirect on its own, so exactly one layer does.

## Logging

Logging is **off** by default. `HttpLogLevel.Headers` writes one `http.request` and one `http.response` event per attempt through the `ILogger` you pass to the pipeline. URLs are redacted
by default (userinfo always, query values unless the parameter name is allow-listed), headers are default-deny against an allow-list, and bodies are never written below `HttpLogLevel.Body`. A
credential's `ToString()` shows no secret, so a stray interpolation cannot leak one ([`logging-and-redaction.md`](./logging-and-redaction.md)).

<!-- fragment: an excerpt of the program in "The whole program"; transport, auth and options are declared there -->
```csharp fragment
ILogger logger = NullLoggerFactory.Instance.CreateLogger("Dexpace.Sdk");   // your host's ILoggerFactory in real use
using HttpPipeline pipeline = PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport, auth, logger)).Build(transport, options);
```

The same pipeline emits the spans and metrics ([`tracing-and-metrics.md`](./tracing-and-metrics.md)) to any listener subscribed to the `Dexpace.Sdk` source and meter, with no configuration here.

## The whole program

Everything above in one program. The statements come first and the types after, as top-level statements require; `WidgetsClient` is the shape a generated SDK ships.

```csharp
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var options = new DexpaceClientOptions
{
    BaseAddress = new Uri("https://api.example.com/v1/"),
    OverallTimeout = TimeSpan.FromSeconds(45),    // across every attempt and redirect; expiry throws OperationTimeoutException
    AttemptTimeout = TimeSpan.FromSeconds(10),    // per attempt; expiry is retryable
    Retry = new RetryOptions { MaxRetryAttempts = 4 },
    Redirect = new RedirectOptions { MaxRedirects = 5 },
    Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers },
};

var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(WidgetJson.Default));
ILogger logger = NullLoggerFactory.Instance.CreateLogger("Dexpace.Sdk");   // your host's ILoggerFactory in real use

await using var transport = new SystemNetHttpClient();
var auth = new BearerTokenAuthPolicy(new EnvironmentTokenCredential("WIDGETS_TOKEN"), "widgets.read");
using HttpPipeline pipeline = PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(transport, auth, logger)).Build(transport, options);

var client = new WidgetsClient(pipeline, serde, options);

try
{
    Widget widget = await client.GetAsync("7", CancellationToken.None);
    Console.WriteLine(widget.Name);
}
catch (WidgetNotFoundException ex)
{
    Console.WriteLine(ex.Message);
}

await foreach (Widget item in client.List())
{
    Console.WriteLine($"{item.Id}: {item.Name}");
}

internal sealed class WidgetsClient(HttpPipeline pipeline, ISerde serde, DexpaceClientOptions options)
{
    private static readonly OperationDescriptor s_get = new() { Method = Method.Get, PathTemplate = "/widgets/{id}", OperationId = "getWidget" };

    private readonly WidgetHandler _handler = new(serde);

    public async ValueTask<Widget> GetAsync(string id, CancellationToken cancellationToken)
    {
        Request request = s_get.WithPathParameter("id", id).BuildRequest(options);
        return await pipeline.SendAsync(request, _handler.HandleAsync, RequestOptions.Empty, cancellationToken);
    }

    public AsyncPageable<Widget> List() =>
        Pageable.Create(
            pipeline,
            Request.Get(new Uri(options.BaseAddress!, "widgets").AbsoluteUri),
            serde,
            PaginationStrategies.LinkHeader<WidgetPage, Widget>(page => page.Items),
            maxPages: 50);   // set a finite cap in production
}

internal sealed class WidgetHandler(ISerde serde) : IResponseHandler<Widget>
{
    private readonly IResponseHandler<Widget> _inner = ResponseHandlers.DeserializeOnSuccess<Widget>(serde);

    public async ValueTask<Widget> HandleAsync(Response response, CancellationToken cancellationToken)
    {
        if (response.Status == Status.NotFound)
        {
            await using (response)   // this path does not delegate, so it disposes
            {
                throw new WidgetNotFoundException(response.Request.Url);
            }
        }

        return await _inner.HandleAsync(response, cancellationToken);
    }
}

internal sealed class EnvironmentTokenCredential(string variable) : TokenCredential
{
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        ValueTask.FromResult(new AccessToken(
            Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException($"{variable} is not set."),
            DateTimeOffset.UtcNow.AddMinutes(30)));   // a real credential asks the identity provider and reports its real expiry
}

internal sealed class WidgetNotFoundException(Uri url) : SdkException($"No widget at {url.AbsolutePath}.");

internal sealed record Widget(string Id, string Name);

internal sealed record WidgetPage(IReadOnlyList<Widget> Items);

[JsonSerializable(typeof(Widget))]
[JsonSerializable(typeof(WidgetPage))]
internal sealed partial class WidgetJson : JsonSerializerContext;
```

## The same client under dependency injection

A consumer of that SDK should not build any of the above. The SDK ships one registration method, and the consumer calls it from the host. `AddDexpaceClient` registers the pipeline, the transport,
the codec and the options as four keyed singletons under the client's name, reads the deadlines, `Retry`, `Redirect` and `Logging` from the configuration section it is handed, and builds the
transport over `IHttpClientFactory` with redirects off. What stays in code is what configuration cannot express: the codec, and the auth policy (a credential is an object, not a string).

```csharp
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Extensions.DependencyInjection;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// In a host this is builder.Configuration (appsettings.json, then the environment, as Dexpace__Widgets__Retry__MaxRetryAttempts).
IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Dexpace:Widgets:BaseAddress"] = "https://api.example.com/v1/",
        ["Dexpace:Widgets:OverallTimeout"] = "45s",
        ["Dexpace:Widgets:AttemptTimeout"] = "10s",
        ["Dexpace:Widgets:Retry:MaxRetryAttempts"] = "4",
        ["Dexpace:Widgets:Redirect:MaxRedirects"] = "5",
        ["Dexpace:Widgets:Logging:Level"] = "Headers",
    })
    .Build();

var services = new ServiceCollection();
services.AddLogging();   // the client's http.request / http.response events go through the container's ILoggerFactory
services.AddSingleton(configuration);
services.AddWidgetsClient(configuration);

await using ServiceProvider provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<WidgetsClient>();
Console.WriteLine((await client.GetAsync("7", CancellationToken.None)).Name);

internal static class WidgetsClientRegistration
{
    // What a generated SDK ships: one extension method that owns its client's name, codec and auth.
    public static IServiceCollection AddWidgetsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TokenCredential>(new EnvironmentTokenCredential("WIDGETS_TOKEN"));
        services.AddDexpaceClient(WidgetsClient.Name, configuration.GetSection("Dexpace:Widgets"))
            .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(WidgetJson.Default)))
            .UseAuthPolicy(sp => new BearerTokenAuthPolicy(sp.GetRequiredService<TokenCredential>(), "widgets.read"));
        return services.AddSingleton<WidgetsClient>();
    }
}

internal sealed class WidgetsClient(
    [FromKeyedServices(WidgetsClient.Name)] HttpPipeline pipeline,
    [FromKeyedServices(WidgetsClient.Name)] ISerde serde,
    [FromKeyedServices(WidgetsClient.Name)] DexpaceClientOptions options)
{
    public const string Name = "widgets";

    private static readonly OperationDescriptor s_get = new() { Method = Method.Get, PathTemplate = "/widgets/{id}", OperationId = "getWidget" };

    public async ValueTask<Widget> GetAsync(string id, CancellationToken cancellationToken)
    {
        Request request = s_get.WithPathParameter("id", id).BuildRequest(options);
        return await pipeline.SendAsync(request, ResponseHandlers.DeserializeOnSuccess<Widget>(serde).HandleAsync, RequestOptions.Empty, cancellationToken);
    }
}

internal sealed class EnvironmentTokenCredential(string variable) : TokenCredential
{
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
        ValueTask.FromResult(new AccessToken(Environment.GetEnvironmentVariable(variable) ?? string.Empty, DateTimeOffset.UtcNow.AddMinutes(30)));
}

internal sealed record Widget(string Id, string Name);

[JsonSerializable(typeof(Widget))]
internal sealed partial class WidgetJson : JsonSerializerContext;
```

- **The container owns the lifetime.** The transport is a container-owned singleton, so dispose the provider with `DisposeAsync`. The named client builds its transport over a redirect-off primary
  handler, so the SDK stays the only redirect authority; the settings the configuration does not carry keep the record defaults.
- **A bad key fails at start, not at the first call.** `Dexpace:Widgets:Retry:MaxRetryAtempts` (a typo) fails validation naming the path, and a value that is not a duration, an integer or a defined
  enum name fails the same way, without quoting the value.
- **Compose, do not stack, retries.** If the host also adds `AddStandardResilienceHandler` to the named client, turn its retry off with `o.Retry.ShouldHandle = _ => ValueTask.FromResult(false)`: two
  retry layers multiply attempts ([`dependency-injection.md`](./dependency-injection.md)).
- To test a client like this one, hand the pipeline a `DelegateHttpClient` as its transport ([`seams.md`](./seams.md)) or `UseTransport(...)` on the builder; the conformance kit
  (`write-a-transport.md`) is for proving a transport, not for testing your client.

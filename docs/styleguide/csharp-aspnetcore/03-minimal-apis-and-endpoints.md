# 03 — Minimal APIs & Endpoints

An endpoint is a thin function that maps an HTTP request onto a domain operation and the result back onto a response — nothing more. This chapter is the working form of ASP-1 (endpoints are thin) and ASP-2 (every boundary is parsed): the handler parses, dispatches to one injected service, and maps, while the work lives in a core that never sees `HttpContext`. The minimal API is the default surface; an MVC controller is the exception you have to argue for.

## What good looks like

```csharp
namespace Dexpace.Catalog.Api;

// A request record bound from route + body; parsed into a domain type before any logic runs (3.5).
public sealed record CreateProductRequest([property: FromRoute] Guid CatalogId, string Name, decimal Price);

public static class ProductEndpoints
{
    // Related endpoints share a group; auth, metadata, and the filter attach once to the group (3.3).
    public static RouteGroupBuilder MapProducts(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/catalogs/{catalogId:guid}/products")
            .WithTags("Products")
            .RequireAuthorization()
            .AddEndpointFilter<RequestLoggingFilter>();

        group.MapPost("/", Create);
        return group;
    }

    // Parse, dispatch, map — token threaded, response type statically known (3.2, 3.4, 3.8).
    private static async Task<Results<Created<ProductView>, ValidationProblem>> Create(
        [AsParameters] CreateProductRequest request,
        ProductService products,
        CancellationToken cancellationToken)
    {
        if (NewProduct.TryParse(request, out NewProduct? parsed) is false)
        {
            return TypedResults.ValidationProblem(NewProduct.Errors(request));
        }

        ProductView view = await products.Create(parsed, cancellationToken);
        return TypedResults.Created($"/products/{view.Id}", view);
    }
}
```

`MapProducts` groups the surface and attaches auth, tags, and the logging filter to the group rather than to each endpoint (3.3, 3.6). `Create` is the whole handler: it binds an `[AsParameters]` request record (3.5), parses it into the `NewProduct` domain type and rejects invalid input as a `ProblemDetails` (3.5), calls one injected `ProductService` (3.2), threads the `CancellationToken` into it (3.8), and returns a `Results<Created<ProductView>, ValidationProblem>` so the response shapes are known to OpenAPI (3.4, 3.7). No business logic, no data access, no `HttpContext`.

## Rules

### 3.1 — Default to minimal APIs; reach for an MVC controller only when the surface needs the full filter pipeline, and say why.

**Reasoning, step by step:**
1. A minimal API endpoint is a function wired in `Program.cs` or a `Map*` extension: no base class, no controller activation, no implicit model-binding machinery, and a route table you can read end to end. The cost of a request is the handler plus the filters you opted into, and the indirection a reader has to follow is one delegate. That is the default surface for a dexpace service.
2. An MVC controller buys the full pipeline — action filters, conventions, content negotiation, `[ApiController]` model-state inference — and you pay for all of it whether you use it or not. Reach for a controller only when a surface genuinely needs that machinery (a large CRUD area leaning on conventions, an existing controller family you are extending), and record the reason at the type so the next reader knows it was a choice, not a default.

**Worked example:**
```csharp
// Default: a minimal endpoint, wired and readable in one place.
app.MapProducts();

// Exception, justified at the type: this surface relies on MVC content negotiation and validation conventions.
[ApiController]
[Route("legacy/reports")]
public sealed class ReportsController : ControllerBase { /* ... */ }
```
**Enforcement:** review requires a why-comment on each controller; minimal APIs are the default in new code.

### 3.2 — Keep the handler thin: parse the request, call one injected service, map the result.

**Reasoning, step by step:**
1. A handler that branches on domain state, opens a `DbContext`, or orchestrates several services is logic you cannot test without standing up the pipeline, and logic the framework now owns. Read the handler top to bottom and it should say exactly three things: parse the request into a domain type, dispatch to one injected application service, map the service's result onto an HTTP response. Anything else belongs behind the service interface (ASP-1, core [10](../csharp/10-api-design.md)).
2. The service is a constructor-injected dependency typed as a first-party interface with no `I` prefix, and the handler holds no state of its own. Keeping the handler this thin keeps the transformation core free of `HttpContext` — testable in isolation and callable from a background worker or a different transport — and makes the endpoint's job obvious at a glance.

**Worked example:**
```csharp
private static async Task<Results<Ok<OrderView>, NotFound>> Get(
    Guid id, OrderService orders, CancellationToken cancellationToken)   // one injected service
{
    OrderId orderId = OrderId.From(id);                                  // parse the boundary value
    OrderView? view = await orders.Find(orderId, cancellationToken);     // dispatch
    return view is null ? TypedResults.NotFound() : TypedResults.Ok(view); // map
}
```
**Enforcement:** ASP.NET analyzers; review rejects business logic, data access, or `HttpContext` access in a handler.

### 3.3 — Group related endpoints with `MapGroup` and attach shared metadata, filters, and auth to the group.

**Reasoning, step by step:**
1. Endpoints that share a route prefix usually share more — an authorization policy, a tag, a rate-limit policy, a logging filter. Repeating `RequireAuthorization()` on each of five endpoints is five chances to forget one, and the omission is an open door, not a compile error. A `MapGroup` declares the shared prefix once and becomes the single place that shared concern is attached.
2. Metadata and filters added to the group apply to every endpoint mapped under it, so authorization, tags, and cross-cutting filters are stated once and inherited. The group is also the unit of organization: one `Map*` extension per resource keeps `Program.cs` a table of contents rather than a wall of inline lambdas, and a new endpoint added to the group inherits the policy by construction.

**Worked example:**
```csharp
RouteGroupBuilder admin = routes.MapGroup("/admin")
    .RequireAuthorization("AdminOnly")                  // attached once; every child inherits it
    .WithTags("Admin");

admin.MapGet("/users", ListUsers);                      // inherits AdminOnly without repeating it
admin.MapDelete("/users/{id:guid}", DeleteUser);
```
**Enforcement:** review requires shared auth/metadata on the group, not per endpoint; analyzers flag endpoints missing an authorization policy.

### 3.4 — Return `TypedResults`; never hand-write status codes as magic integers.

**Reasoning, step by step:**
1. `TypedResults.Ok(dto)`, `TypedResults.NotFound()`, and `TypedResults.Created(uri, dto)` return a strongly typed `IResult` whose status code and payload type are part of the return type, so the compiler and OpenAPI both know what the endpoint produces. A `Results<Ok<OrderView>, NotFound>` return declares the complete set of outcomes; the alternative — `return Results.StatusCode(404)` — is a magic integer the tooling cannot read and a reviewer has to decode.
2. Because the response shapes are in the signature, the OpenAPI document is generated from the truth rather than from hand-maintained `[ProducesResponseType]` attributes that drift. A new outcome means widening the `Results<...>` union, which is a visible, reviewable signature change, and a handler returning an undeclared result fails to compile rather than shipping an undocumented status.

**Worked example:**
```csharp
private static Results<Ok<CartView>, NotFound, ProblemHttpResult> Get(Guid id, CartService carts)
{
    CartView? cart = carts.Find(CartId.From(id));
    return cart is null ? TypedResults.NotFound() : TypedResults.Ok(cart);
}
// bad: Results.StatusCode(404) — a magic int OpenAPI cannot see and a reader cannot trust.
```
**Enforcement:** review rejects magic status integers and `Results.StatusCode`; OpenAPI generation in CI compares declared results against the document.

### 3.5 — Parse and validate at the boundary into records; bind `[AsParameters]` request records, not long parameter lists.

**Reasoning, step by step:**
1. Everything a request carries — route values, query string, body, headers — is untrusted and shape-checked at best, so it is bound into a request `record` and then parsed into a validated domain type before any handler logic runs (ASP-2, core [03.2](../csharp/03-nullability-and-the-type-system.md)). Invalid input is rejected at the edge with an RFC 9457 `ProblemDetails` produced by `TypedResults.ValidationProblem` or `Problem`, wired through `AddProblemDetails()` so every failure path returns the same machine-readable shape.
2. A handler with eight bound parameters is a signature no one can read and a binding order no one can verify; collapse the inputs into one `[AsParameters]` request record whose properties carry their binding source (`[FromRoute]`, `[FromQuery]`) explicitly (core [05](../csharp/05-methods-and-functions.md)). The record is the single typed boundary object, the parse turns it into the domain type that flows inward, and the DTO never reaches the core (chapter [05](./05-serialization-and-validation.md)).

**Worked example:**
```csharp
public sealed record SearchRequest([property: FromQuery] string? Term, [property: FromQuery] int Page = 1);

private static async Task<Ok<IReadOnlyList<ProductView>>> Search(
    [AsParameters] SearchRequest request, ProductService products, CancellationToken cancellationToken)
{
    SearchTerms terms = SearchTerms.Parse(request.Term, request.Page);   // parsed into a domain type at the edge
    return TypedResults.Ok(await products.Search(terms, cancellationToken));
}
```
**Enforcement:** `AddProblemDetails()` registered at startup; review requires `[AsParameters]` over long parameter lists and a parse step before logic.

### 3.6 — Put cross-cutting concerns in endpoint filters or middleware, never copied into each handler.

**Reasoning, step by step:**
1. Logging, request enrichment, idempotency checks, and uniform validation are concerns that span endpoints, and pasting them into each handler is duplication that drifts the moment one copy is updated and another is not. An `IEndpointFilter` (`AddEndpointFilter`) runs around a handler or a whole group and is the right place for per-endpoint concerns; middleware in the pipeline is the right place for process-wide ones like correlation and exception mapping.
2. A filter keeps the handler down to its three jobs and makes the cross-cutting behaviour a single reviewable unit attached at the group (3.3). Order matters and is explicit — the filter chain and the middleware pipeline both run in registration order — so a reader sees what wraps a request and in what sequence, rather than inferring it from scattered handler code.

**Worked example:**
```csharp
public sealed class RequestLoggingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // cross-cutting concern lives here, attached once to the group — never pasted into handlers.
        object? result = await next(context);
        return result;
    }
}
```
**Enforcement:** review rejects cross-cutting logic duplicated across handlers; filters and middleware registered in explicit order.

### 3.7 — Version the API explicitly and publish OpenAPI from the code; never break a shipped contract silently.

**Reasoning, step by step:**
1. A shipped HTTP contract is depended upon by clients you cannot recompile, so it evolves under the same discipline as any public API (core [10.6](../csharp/10-api-design.md)): additive changes are safe, a removal or a changed shape is a breaking change that needs a new version. Declare the version explicitly with `Asp.Versioning` — a URL segment or header — so a breaking change ships as `v2` alongside a still-serving `v1`, never as a silent mutation of the live contract.
2. The OpenAPI document is generated from the endpoints by the built-in `Microsoft.AspNetCore.OpenApi`, so it reflects the `TypedResults` shapes (3.4) and request records (3.5) as they actually are, not a hand-written sidecar that drifts. Publishing it lets clients and CI diff the contract: a diff that drops or changes a field is a breaking change a reviewer must see and version before it ships.

**Worked example:**
```csharp
ApiVersionSet versions = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).Build();
app.MapGroup("/v{version:apiVersion}/products").WithApiVersionSet(versions).MapProducts();
app.MapOpenApi();                                   // contract generated from the endpoints, published for CI to diff
```
**Enforcement:** OpenAPI generation in CI diffs the contract against the last shipped version; review treats a field removal or shape change as a versioned breaking change.

### 3.8 — Thread the request `CancellationToken` into every downstream call.

**Reasoning, step by step:**
1. The handler accepts a `CancellationToken` as a parameter — the framework supplies the request's token, which trips when the client disconnects or the request times out. Threading that token into the service call, and on into the database query and the outbound HTTP call, means a client that walks away cancels the work it abandoned instead of leaving a thread tied up holding a connection (ASP-4, core [09](../csharp/09-concurrency.md)).
2. The token is passed last to each downstream method (core [10.5](../csharp/10-api-design.md)) and never swallowed or replaced with `default`; a `default` token is a silent opt-out of cancellation that survives until the load that exposes it. Under the runtime's finite, shared thread pool, work that cannot be cancelled is work that accumulates, so the token is not optional plumbing — it is how the server stays responsive under the disconnects it will certainly see.

**Worked example:**
```csharp
private static async Task<Ok<OrderView>> Get(Guid id, OrderService orders, CancellationToken cancellationToken)
{
    OrderView view = await orders.Get(OrderId.From(id), cancellationToken);  // token threaded, not dropped to default
    return TypedResults.Ok(view);
}
```
**Enforcement:** review requires the request token threaded into every downstream call; analyzers flag `CancellationToken` parameters dropped or defaulted on the request path.

## Cross-references

- `[AsParameters]` request records, parameter-list length, and `ThrowIfNull` at the boundary: [../csharp/05-methods-and-functions.md](../csharp/05-methods-and-functions.md). Threading the `CancellationToken` and async correctness on the request path: [../csharp/09-concurrency.md](../csharp/09-concurrency.md).
- The HTTP surface as a public-API contract, narrow-in/read-only-out, and compatible evolution: [../csharp/10-api-design.md](../csharp/10-api-design.md). Parsing the boundary into records, `ProblemDetails`, and never serializing entities: [./05-serialization-and-validation.md](./05-serialization-and-validation.md).

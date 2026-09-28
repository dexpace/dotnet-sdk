# 05 — Serialization & Validation

Serialization is where untrusted bytes become domain types and where domain types become a wire contract, so it is a boundary in both directions. This chapter embodies ASP-2: every request body is parsed into a proven domain `record` before any handler logic runs, and every response is mapped to an intentional DTO rather than leaked from an entity. It extends the core parse-at-the-boundary rule (core [03.2](../csharp/03-nullability-and-the-type-system.md)) to the JSON edge and binds the serializer choice and its configuration to one place.

## What good looks like

```csharp
namespace Dexpace.Ordering;

[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
public sealed partial class OrderJsonContext : JsonSerializerContext;       // source-gen, not reflection (5.2)

public sealed record CreateOrderRequest(string? Sku, int Quantity);          // wire shape, nullable (5.4)

public static class OrderEndpoint
{
    public static Results<Created<OrderResponse>, ProblemHttpResult> Handle(CreateOrderRequest request, OrderService service)
    {
        if (request.Sku is null || !Sku.TryFrom(request.Sku, out var sku))   // parse into domain type at the edge (5.4)
            return TypedResults.Problem(title: "invalid sku", statusCode: StatusCodes.Status400BadRequest); // RFC 9457 (5.5)

        Order order = service.Place(sku, request.Quantity);                  // domain type flows inward, DTO stays out
        return TypedResults.Created($"/orders/{order.Id.Value}", OrderResponse.From(order)); // mapped, never the entity (5.7)
    }
}
```

The request arrives as a nullable wire `record`, and the handler parses it into a branded `Sku` before doing anything, rejecting failure as `ProblemDetails` (5.4, 5.5). Serialization goes through a source-generated `JsonSerializerContext` rather than reflection (5.2), the response is a purpose-built `OrderResponse` mapped from the aggregate so the contract is intentional and over-posting is impossible (5.7), and `System.Text.Json` is the only serializer in play (5.1).

## Rules

### 5.1 — Use `System.Text.Json` exclusively; no `Newtonsoft.Json` in new code.

**Reasoning, step by step:**
1. `System.Text.Json` is the framework's built-in serializer, the one ASP.NET Core binds and emits by default, and it reads `Span<byte>`/`Utf8JsonReader` directly without the intermediate string allocations that `Newtonsoft.Json` pays per document. On the hot path that difference is throughput; running two serializers in one process doubles the surface for divergent null handling, casing, and date formats.
2. A second serializer also splits the contract: a type may round-trip one way under `Newtonsoft` attributes and another under `System.Text.Json`, so the bug is a mismatch no test that uses one library will catch. Standardize on the built-in serializer so configuration (5.3) lives in one place; `Newtonsoft.Json` is permitted only in a declared, isolated bridge to a legacy contract that genuinely needs its feature set, with a why-comment.

**Enforcement:** review rejects a `Newtonsoft.Json` package reference in new assemblies; an architecture test asserts the dependency is absent outside a named bridge.

### 5.2 — Serialize through the source generator, not reflection, on hot paths.

**Reasoning, step by step:**
1. Reflection-based serialization inspects the type's metadata at runtime on first use, allocating the read/write plan and walking properties dynamically; the source generator (`JsonSerializerContext` with `[JsonSerializable]`) emits that plan at compile time, so the per-call cost drops and there is nothing for the trimmer or AOT compiler to fail to discover. A trimmed or Native AOT deployment (chapter [08](./08-build-and-deployment.md)) breaks under reflection serialization because the metadata it needs has been removed.
2. Declare a `partial` context listing every type that crosses the wire and register it on the JSON options (`TypeInfoResolverChain`), so every serialize and deserialize resolves through generated code. This pairs with the core performance discipline (core [15](../csharp/15-performance.md)): the allocation you avoid per request is the one the source generator removes for you.

**Worked example:**
```csharp
[JsonSerializable(typeof(OrderResponse))]
[JsonSerializable(typeof(ProblemDetails))]
public sealed partial class ApiJsonContext : JsonSerializerContext;          // generated at compile time

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default)); // resolve via generated code
```
**Enforcement:** source-generation analyzers (`SYSLIB1030`–`SYSLIB1039`); trim/AOT warnings as errors (chapter [08](./08-build-and-deployment.md)); review.

### 5.3 — Configure `JsonSerializerOptions` once and reuse the instance; never construct one per call.

**Reasoning, step by step:**
1. `JsonSerializerOptions` caches the type-resolution metadata it builds on first use, and that cache is keyed to the instance. A `new JsonSerializerOptions()` per call throws that cache away every time, so each serialize re-derives the plan — the allocation and CPU cost the analyzer flags as `CA1869`. Construct one configured instance (camelCase policy, `JsonIgnoreCondition`, no indenting in production) and reuse it, or let DI's configured options carry it (chapter [01](./01-host-and-configuration.md)).
2. A single instance is also a single contract: casing, enum handling, and ignore conditions are decided once, in one reviewable place, rather than drifting between call sites. Treat the options object as the shared, frozen resource it is (core [13](../csharp/13-resource-management.md)) — configure at composition, freeze, and reuse for the process lifetime.

**Worked example:**
```csharp
private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,                                          // never indent in production
    TypeInfoResolver = ApiJsonContext.Default,
};
string json = JsonSerializer.Serialize(response, Options);          // reuse, never `new` per call (CA1869)
```
**Enforcement:** `CA1869` (cache and reuse `JsonSerializerOptions`); review rejects per-call construction.

### 5.4 — Parse a deserialized DTO into a validated domain `record` at the boundary.

**Reasoning, step by step:**
1. A successful deserialization proves the JSON had the right shape — the field names matched and the types parsed — but it proves nothing about domain validity: a `Quantity` of `-3` deserializes fine, an empty `Sku` deserializes fine. The DTO is therefore an untrusted carrier, modelled with nullable wire types (core [03.2](../csharp/03-nullability-and-the-type-system.md)), and the handler's first act is to parse it into a domain type whose construction enforces the rules (a branded primitive, a validated `record`).
2. The domain type, not the DTO, flows inward, so the interior trusts its annotations and the validation is not repeated downstream (ASP-2). Parse, don't validate — return the proven type or a typed failure, never a `bool` that leaves the unproven DTO in scope (core [08.7](../csharp/08-error-handling.md)). The single conversion point is the only place wire absence is reconciled with the interior's chosen representation (5.6).

**Worked example:**
```csharp
public sealed record CreateOrderRequest(string? Sku, int Quantity);          // nullable wire shape, untrusted

public static Result<Order, string> ToDomain(CreateOrderRequest request, OrderService service)
{
    if (request.Sku is null || !Sku.TryFrom(request.Sku, out var sku))       // parse into the brand
        return new Result<Order, string>.Err("sku required");
    if (request.Quantity <= 0)
        return new Result<Order, string>.Err("quantity must be positive");
    return new Result<Order, string>.Ok(service.Build(sku, request.Quantity)); // domain type flows on
}
```
**Enforcement:** review at endpoint modules confirms a parse step precedes handler logic; `CA1062` on mapping helpers.

### 5.5 — Report validation failures as RFC 9457 `ProblemDetails`, never a bare 500 or a leaked message.

**Reasoning, step by step:**
1. A validation failure is an expected, routine outcome of untrusted input (core [08.7](../csharp/08-error-handling.md)), so it deserves a `4xx` with a machine-readable body, not a `500` that tells the client the server broke when the client's request did. RFC 9457 `ProblemDetails` is the standard shape — `type`, `title`, `status`, `detail`, `errors` — that clients and gateways already understand, emitted via `TypedResults.Problem` or `ValidationProblem`.
2. The `detail` and `errors` carry only what the caller needs to fix the request; an exception message, a stack trace, or a SQL fragment in the body is an information leak that hands an attacker your internals (see [security.md](../security.md)). Register the `ProblemDetails` middleware so even unhandled exceptions return a sanitized problem document rather than a raw error in production.

**Worked example:**
```csharp
builder.Services.AddProblemDetails();                                        // sanitized error contract for all paths

return result switch
{
    Result<Order, string>.Ok(var order) => TypedResults.Created($"/orders/{order.Id.Value}", OrderResponse.From(order)),
    Result<Order, string>.Err(var reason) => TypedResults.Problem(           // RFC 9457, no leaked internals
        title: "validation failed", detail: reason, statusCode: StatusCodes.Status400BadRequest),
};
```
**Enforcement:** `AddProblemDetails` registered in every host; review rejects raw exception text in a response body.

### 5.6 — Distinguish absent from null on the wire deliberately.

**Reasoning, step by step:**
1. JSON has three states for a field — present with a value, present and `null`, and absent — and a PATCH or partial update needs all three: "set the name to null" and "do not touch the name" are different operations. Collapsing them silently means a PATCH that omits a field accidentally clears it. Decide per field whether absence and null are distinct, and model "not provided" explicitly rather than letting the default value impersonate it.
2. Control emission with `JsonIgnoreCondition.WhenWritingNull` chosen on purpose, not copied by habit, and model an optional input field so the handler can tell omission from an explicit null (core [03](../csharp/03-nullability-and-the-type-system.md)) — a nullable property plus a presence flag, or a small optional wrapper. The interior then receives one absence dialect, reconciled at the parse step (5.4), and never guesses what the client meant.

**Worked example:**
```csharp
public sealed record UpdateProfile(string? DisplayName, bool DisplayNameSet); // absent vs explicit null kept distinct

Profile Apply(Profile current, UpdateProfile patch) =>
    patch.DisplayNameSet                                                      // only touch what was provided
        ? current with { DisplayName = patch.DisplayName }
        : current;
```
**Enforcement:** review of `JsonIgnoreCondition` choices on PATCH contracts; tests cover absent, null, and present cases.

### 5.7 — Never serialize an EF entity or a domain aggregate directly; map to a response DTO.

**Reasoning, step by step:**
1. An EF entity carries navigation properties and a change-tracking identity, so serializing it directly risks a reference cycle, an accidental lazy-load that fires a query inside the serializer, and an over-broad payload that ships columns the client should never see. A domain aggregate carries invariants and internal state that are not a wire contract. Map both to a purpose-built response `record` so the payload is exactly the intended fields (chapter [04](./04-persistence-ef-core.md)).
2. The DTO also closes the over-posting hole in the other direction: a request DTO lists only the fields a client may set, so binding cannot reach a property like `IsAdmin` that the entity happens to expose. The contract is intentional in both directions — what goes out is chosen, what comes in is bounded — and a schema change to the entity does not silently change the API.

**Worked example:**
```csharp
public sealed record OrderResponse(Guid Id, string Sku, int Quantity, string Status)
{
    public static OrderResponse From(Order order) =>                          // mapped, never the entity itself
        new(order.Id.Value, order.Sku.Value, order.Quantity, order.Status.ToString());
}
```
**Enforcement:** review rejects returning an entity or aggregate from an endpoint; project to the DTO in the query (chapter [04](./04-persistence-ef-core.md)).

### 5.8 — Keep validation explicit at the edge; prefer guard/parse code over scattered DataAnnotations.

**Reasoning, step by step:**
1. `DataAnnotations` attributes (`[Required]`, `[Range]`) move validation into metadata the reader cannot see in the handler's control flow, and they validate the DTO shape rather than producing the domain type, so a passing model is still not domain-valid. Explicit guard and parse code — or a small focused validator — keeps the rules where they run, returns the proven type (5.4), and composes the same way the rest of the error path does (core [08](../csharp/08-error-handling.md)).
2. Whichever mechanism a team picks, the contract is the same: validation runs at the edge, before handler logic, and a failure becomes `ProblemDetails` (5.5). If attributes are used, they stay declarative shape checks at the boundary and never carry the domain rules that belong in a validating factory; the handler still parses into the brand. Do not split one rule across an attribute and a guard, where neither place tells the whole story.

**Worked example:**
```csharp
static Result<Sku, string> ParseSku(string? raw) =>                          // explicit, control-flow-visible
    raw is not null && Sku.TryFrom(raw, out var sku)
        ? new Result<Sku, string>.Ok(sku)
        : new Result<Sku, string>.Err("sku must be a non-empty catalogue code");
```
**Enforcement:** review prefers explicit parse over hidden attributes; whichever is chosen runs before handler logic and emits `ProblemDetails` (5.5).

## Cross-references

- Nullable wire types and parse-don't-validate at the boundary: [../csharp/03-nullability-and-the-type-system.md](../csharp/03-nullability-and-the-type-system.md). Shared, frozen options as a managed resource: [../csharp/13-resource-management.md](../csharp/13-resource-management.md).
- Source-generated serialization, allocation, and trim/AOT cost: [../csharp/15-performance.md](../csharp/15-performance.md). Projecting to DTOs in the query and avoiding entity leakage: [04-persistence-ef-core.md](./04-persistence-ef-core.md).

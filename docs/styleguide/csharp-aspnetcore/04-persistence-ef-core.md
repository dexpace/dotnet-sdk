# 04 — Persistence with EF Core

Data access is correct, bounded, and fast, in that order — a query that returns the wrong rows is worse than a slow one, and a query that pulls the whole table into memory is both. This chapter pins the EF Core behaviours that make persistence safe under a request-scoped lifetime and a finite thread pool: the right `DbContext` lifetime, tracking only when you intend to write, explicit loading projected to DTOs, queries parameterized by construction, and writes bounded by a transaction. The change tracker is a tool with a cost; you pay it deliberately or not at all.

## What good looks like

```csharp
namespace Dexpace.Catalog.Data;

// Scoped DbContext, pooled for throughput; reads are no-tracking and projected straight to a DTO record (4.1, 4.2, 4.3, 4.4).
public sealed class ProductRepository(CatalogContext db)
{
    public async Task<IReadOnlyList<ProductView>> Search(SearchTerms terms, CancellationToken cancellationToken) =>
        await db.Products
            .AsNoTracking()                                              // a read, so no change tracking (4.2)
            .Where(p => p.CatalogId == terms.CatalogId.Value)
            .Select(p => new ProductView(p.Id, p.Name, p.Price))        // project to DTO; fetch only these columns (4.3, 4.4)
            .ToListAsync(cancellationToken);                            // token threaded (4.6)

    // A unit of work: one SaveChangesAsync inside the resilient execution strategy (4.6).
    public async Task<ProductId> Add(NewProduct product, CancellationToken cancellationToken)
    {
        ProductRow row = new() { Id = Guid.NewGuid(), CatalogId = product.CatalogId.Value, Name = product.Name };
        db.Products.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ProductId.From(row.Id);
    }
}
```

`ProductRepository` takes a `CatalogContext` resolved per request from the pool (4.1). `Search` is a read, so it is `AsNoTracking` (4.2) and projects straight to a `ProductView` DTO with `Select`, fetching only the three columns the response needs rather than materializing an entity (4.3, 4.4). The `Where` is parameterized by construction from a parsed domain value, never concatenated (4.5), and the token threads into both calls (4.6). `Add` is one `SaveChangesAsync` — a single unit of work — and returns a branded id, never the entity row.

## Rules

### 4.1 — Register the `DbContext` as Scoped (one per request) and pool it for throughput; never Singleton.

**Reasoning, step by step:**
1. A `DbContext` holds a change tracker and an open-ish connection and is **not thread-safe**: two requests sharing one context interleave their tracked entities and their `SaveChanges` calls into corruption. So it is `Scoped` — `AddDbContext` registers it per request, the container disposes it when the request ends, and no two requests ever touch the same instance. A `Singleton` context, or a context captured by a singleton (a captive dependency, ASP-3), is a cross-request data leak waiting for the first concurrent load.
2. Creating a context per request has a small cost — building the model metadata and the internal service provider — that `AddDbContextPool` removes by resetting and reusing context instances from a pool instead of constructing fresh ones. The lifetime stays Scoped to the caller; pooling is an allocation optimization underneath it (chapter [02](./02-dependency-injection.md)). Use the pool for a high-throughput service, and keep no mutable state on the context that a reset would not clear.

**Worked example:**
```csharp
builder.Services.AddDbContextPool<CatalogContext>(options =>
    options.UseNpgsql(connectionString));           // Scoped to the request, reused from the pool — never Singleton
```
**Enforcement:** review rejects a Singleton context or a context captured by a singleton; `ValidateScopes`/`ValidateOnBuild` (chapter [02](./02-dependency-injection.md)) turn a captive dependency into a startup failure.

### 4.2 — Read with `AsNoTracking` by default; track only when you intend to write.

**Reasoning, step by step:**
1. By default a query materializes entities into the change tracker so EF can detect and persist later edits. For a read that only shapes a response, that tracking is pure overhead: a snapshot of every entity, identity-map bookkeeping, and retained references that keep the graph alive longer than the response needs (core [15](../csharp/15-performance.md)). `AsNoTracking()` skips all of it and returns detached objects, which is exactly right when nothing will be saved.
2. Reserve tracking for the path that genuinely writes — load the entity, mutate it, `SaveChangesAsync` — where the tracker is doing the work you want. Make no-tracking the default for queries (a `QueryTrackingBehavior.NoTracking` default on the context, overridden locally where a write needs it) so the expensive behaviour is opt-in and visible, rather than the cheap one being a thing you have to remember.

**Worked example:**
```csharp
// Read: detached, no tracker overhead.
ProductRow[] rows = await db.Products.AsNoTracking().Where(p => p.Active).ToArrayAsync(cancellationToken);

// Write: tracked on purpose, because this entity will be mutated and saved.
ProductRow row = await db.Products.FirstAsync(p => p.Id == id, cancellationToken);
row.Price = newPrice;
await db.SaveChangesAsync(cancellationToken);
```
**Enforcement:** context defaulted to `NoTracking`; review requires tracking to be a deliberate, local choice on a write path; profiler/logging flags needless tracking on read paths.

### 4.3 — Disable lazy loading; load related data with an explicit `Include`, or project to a DTO with `Select`.

**Reasoning, step by step:**
1. Lazy loading turns a property access into a silent database round trip, so a `foreach` over parents that touches `parent.Children` fires one query per parent — the N+1 problem, invisible in the code and catastrophic under load. Disable it (do not install the proxies, do not call `UseLazyLoadingProxies`) so every database round trip is something you wrote on purpose. Related data you actually need is loaded with an explicit `Include`, which is one join you can see and a reviewer can count.
2. Better still, skip the entity entirely and project straight to a DTO `record` with `Select`: EF translates the projection into SQL that fetches only the columns the response uses, with no tracking and no over-fetched graph. An `Include` that pulls a wide entity to read two fields is waste; a projection states the exact shape the boundary needs and lets the database do the narrowing. Reserve `Include` for the case where you load an entity to mutate it (4.2).

**Worked example:**
```csharp
// Explicit when you need the entity graph:
Order order = await db.Orders.Include(o => o.Lines).FirstAsync(o => o.Id == id, cancellationToken);

// Better for a read: project to the DTO, fetch only what the response shows.
OrderSummary? summary = await db.Orders
    .Where(o => o.Id == id)
    .Select(o => new OrderSummary(o.Id, o.Lines.Count, o.Total))
    .FirstOrDefaultAsync(cancellationToken);
```
**Enforcement:** lazy-loading proxies not installed; EF Core analyzers flag multiple-collection `Include`; review prefers `Select` projection over `Include` on read paths.

### 4.4 — Never expose or serialize an entity; project to a DTO at the boundary.

**Reasoning, step by step:**
1. An EF entity is a table row with navigation properties and tracker state, not a wire contract. Serialize it and you leak the schema — column names, foreign keys, and lazily-loaded navigations that fire queries during serialization or throw on a disposed context — and you weld the JSON shape to the table so neither can change without breaking the other. The entity belongs to the data layer and stops there.
2. Project to a DTO `record` at the boundary so the wire contract and the table schema evolve independently: rename a column without touching the API, add a response field without altering the table, drop an internal column the response never showed. The projection is the seam, and it pairs with the endpoint returning that DTO (chapter [03](./03-minimal-apis-and-endpoints.md)) and parsing inbound DTOs into domain types (chapter [05](./05-serialization-and-validation.md)) — the entity is never the thing that crosses the edge.

**Worked example:**
```csharp
public sealed record CustomerView(Guid Id, string Name, int OrderCount);   // wire contract, independent of the table

CustomerView? view = await db.Customers
    .Where(c => c.Id == id)
    .Select(c => new CustomerView(c.Id, c.Name, c.Orders.Count))           // entity -> DTO at the boundary
    .FirstOrDefaultAsync(cancellationToken);
// bad: returning CustomerRow — leaks the schema and ties the JSON to the table.
```
**Enforcement:** review rejects returning or serializing an entity from an endpoint or service; the DTO projection is the contract (chapter [05](./05-serialization-and-validation.md)).

### 4.5 — Keep queries parameterized by construction; never concatenate user input, and watch for client-side evaluation.

**Reasoning, step by step:**
1. A LINQ query is parameterized by construction — `Where(p => p.Name == term)` becomes a parameterized SQL command, with `term` bound, never inlined — so the usual injection surface does not exist as long as you stay in LINQ. When you must drop to raw SQL, use `FromSql` with an interpolated string (the interpolation handler parameterizes the holes) or explicit `DbParameter`s; concatenating user input into a SQL string with `FromSqlRaw($"...{input}...")` reopens injection and is banned (see [security.md](../security.md)).
2. Watch the other failure mode EF makes quiet: client-side evaluation. A query fragment EF cannot translate — a call to a C# method it does not understand inside `Where` — is, in modern EF, a runtime error, but a `.AsEnumerable()` or `.ToList()` placed too early silently moves the rest of the pipeline into memory and pulls the whole table over to filter it in the app. Keep the filter, sort, and paging in the `IQueryable` so the database does the work, and treat an unexpected materialization as a bug to find with logging, not a thing to leave running.

**Worked example:**
```csharp
// Parameterized by construction:
var hits = await db.Products.Where(p => p.Name == term).ToListAsync(cancellationToken);

// Raw SQL when needed: interpolation handler parameterizes the value — not string concatenation.
var rows = await db.Products.FromSql($"SELECT * FROM products WHERE sku = {sku}").ToListAsync(cancellationToken);
// bad: FromSqlRaw($"... WHERE sku = '{sku}'") — concatenated input, injection.
```
**Enforcement:** `CA2100` (review SQL for injection); EF Core client-eval and unparameterized-query warnings promoted to errors; review of every `FromSql`/`FromSqlRaw` call.

### 4.6 — Make a unit of work one `SaveChangesAsync` in one transaction, and pass the `CancellationToken`.

**Reasoning, step by step:**
1. `SaveChangesAsync` wraps all pending changes in a single transaction and commits them atomically, so one logical operation should resolve to one `SaveChangesAsync`: accumulate the inserts and updates, then save once. Calling `SaveChangesAsync` repeatedly inside a loop turns one unit of work into many partial commits, any of which can leave the data half-written if the next one fails. A multi-step write that spans more than one save, or mixes EF with another resource, is wrapped in an explicit transaction so it commits or rolls back as a whole.
2. Transient database failures are normal in a networked deployment, so wrap retryable work in the resilient execution strategy (`EnableRetryOnFailure`); because the strategy may run the block more than once, an explicit user-controlled transaction must go through `strategy.ExecuteAsync` so the retry brackets the whole transaction, not half of it. Every async call on this path carries the request's `CancellationToken` (ASP-4, core [09](../csharp/09-concurrency.md)) so an abandoned request stops doing database work instead of holding a connection to completion.

**Worked example:**
```csharp
IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(cancellationToken);
    db.Orders.Add(order);
    db.Outbox.Add(notification);
    await db.SaveChangesAsync(cancellationToken);       // one save, one transaction, one unit of work
    await tx.CommitAsync(cancellationToken);
});
```
**Enforcement:** review rejects `SaveChangesAsync` in a loop and multi-step writes outside a transaction; the retry strategy wraps explicit transactions; the token is threaded into every async call.

### 4.7 — Apply schema changes through reviewed, checked-in migrations at deploy; never `EnsureCreated` or auto-migrate ungated in production.

**Reasoning, step by step:**
1. The database schema is shared, durable state, and a change to it is as reviewable as a change to code. Generate a migration, check it in, and read it in the pull request — the generated SQL is the artifact a reviewer approves. `EnsureCreated` bypasses migrations entirely (it creates the schema from the model, with no version history and no upgrade path), so it belongs only in throwaway tests; in production it is a schema you cannot evolve and cannot roll back.
2. Apply migrations deliberately at deploy through a gated step — a migration bundle, an init container, or an explicit `dotnet ef database update` in the pipeline — not from `Migrate()` on every app startup. An ungated auto-migrate races when several instances start at once, runs DDL under whatever credentials the app holds, and ties a schema change to a code deploy with no chance to review or stage it. The gate makes the schema change a decision, applied once, by a step that owns it (chapter [08](./08-build-and-deployment.md)).

**Worked example:**
```csharp
// Tests only — no version history, no upgrade path:
await db.Database.EnsureCreatedAsync();

// Production — a reviewed migration, applied by a gated deploy step (not on every startup):
//   dotnet ef migrations add AddProductSku
//   dotnet ef database update            # run by the pipeline's migration step, not the app host
```
**Enforcement:** review of every checked-in migration; `EnsureCreated` and ungated startup `Migrate()` rejected outside tests; migrations applied by a dedicated, gated deploy step.

### 4.8 — Drop to Dapper or `FromSql` for a measured hot-path query, kept parameterized and parsed into a record.

**Reasoning, step by step:**
1. EF's query translation and materialization carry overhead that is invisible on most paths and decisive on a few. When a profiler shows a specific query as a bottleneck — a wide report, a tight read loop — hand-writing the SQL with Dapper or EF's `FromSql` skips the translation and the entity graph and returns rows straight into a `record`. This is an optimization justified by evidence (core [15](../csharp/15-performance.md)), not a default: reach for it because the profiler named the query, not because raw SQL feels faster.
2. Dropping to raw SQL does not drop the rules that keep it safe. The query stays parameterized — Dapper's parameter object, `FromSql`'s interpolation handler — never concatenated input (4.5), and the result is parsed into a domain `record` rather than passed around as a loose tuple or `dynamic` (core [03.5](../csharp/03-nullability-and-the-type-system.md)). The hot-path query is a measured exception to EF, held to the same parameterization and typing discipline as everything else.

**Worked example:**
```csharp
// Justified by profiler evidence: a hot report query, parameterized, materialized into a record.
public sealed record SalesRow(Guid ProductId, decimal Total);

IEnumerable<SalesRow> rows = await connection.QueryAsync<SalesRow>(
    "SELECT product_id AS ProductId, SUM(amount) AS Total FROM sales WHERE day = @day GROUP BY product_id",
    new { day });                                       // parameter object — never concatenated
```
**Enforcement:** review requires profiler evidence for a raw-SQL hot path; `CA2100` and parameterization rules still apply; the result is parsed into a record, never `dynamic`.

## Cross-references

- Branded ids, parsing the boundary into domain types, and banning `dynamic` for query results: [../csharp/03-nullability-and-the-type-system.md](../csharp/03-nullability-and-the-type-system.md). Tracking overhead, projection, and optimizing on profiler evidence: [../csharp/15-performance.md](../csharp/15-performance.md).
- Projecting entities to DTOs at the boundary, `ProblemDetails`, and never serializing entities: [./05-serialization-and-validation.md](./05-serialization-and-validation.md). SQL injection, parameterization, and untrusted input: [../security.md](../security.md).

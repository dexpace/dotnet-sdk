# 11 — Testing

Tests are the executable form of "correctness comes first" — the first value in this guide's order, and the reason most of chapter [03](./03-nullability-and-the-type-system.md) exists. This chapter sets how dexpace tests C# at enterprise scale: which runner, which assertion library, how to fake a dependency, and how to keep every test deterministic and isolated so a green suite means something. The choices below are constrained by supply-chain reality — a test tool that becomes a license trap or injects ads is not fit for an enterprise codebase, and two such episodes shape the picks here.

## What good looks like

```csharp
namespace Dexpace.Billing.Tests;

public sealed class InvoiceTotalTests
{
    // Arrange-Act-Assert, named for the behaviour, deterministic clock injected (11.2, 11.6).
    [Fact]
    public void Total_sums_line_items_and_applies_tax()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calculator = new InvoiceCalculator(clock);            // hand-built, no mock framework (11.4)

        var total = calculator.Total([new LineItem("A", 100m), new LineItem("B", 50m)], taxRate: 0.1m);

        total.ShouldBe(165m);                                     // Shouldly, not FluentAssertions (11.5)
    }

    // Tabulated cases share one body; the data is the test (11.2).
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Total_rejects_non_positive_tax_rate(decimal rate)  // negative space, not just happy path (11.8)
    {
        var calculator = new InvoiceCalculator(new FakeTimeProvider());
        Should.Throw<ArgumentOutOfRangeException>(() => calculator.Total([], rate));
    }
}
```

The class is one test project's worth of `InvoiceCalculator` coverage: each test is Arrange-Act-Assert with a name stating the behaviour (11.2), the clock is a `FakeTimeProvider` so the result does not depend on wall time (11.6), and the calculator is hand-built with no mock framework in sight (11.4). Assertions use Shouldly, the MIT-licensed reader-friendly library, not FluentAssertions (11.5), and the `[Theory]` covers the invalid-input boundary alongside the happy path (11.8). The whole suite runs in parallel on xUnit v3 over Microsoft.Testing.Platform (11.1).

## Rules

### 11.1 — Run xUnit v3 on Microsoft.Testing.Platform, one isolated test project per production assembly.

**Reasoning, step by step:**
1. xUnit v3 is the standard runner: it runs on Microsoft.Testing.Platform — the successor to the VSTest host — which gives a real `Main`, deterministic startup, native `dotnet test` integration, and trimmable, AOT-friendly test executables. Pin one version across the solution through `Directory.Build.props` (chapter [12](./12-project-organization.md)) so every project tests on the same engine. Mirror each production assembly with exactly one test project (`Dexpace.Billing` → `Dexpace.Billing.Tests`) so a failure points at one assembly and `InternalsVisibleTo` grants that one project access to internals (chapter [10](./10-api-design.md)).
2. xUnit runs test classes in parallel by default, which only pays off if tests are isolated — no shared mutable static state, no reliance on execution order, no file or port a sibling also touches. Each test constructs its own dependencies (the class constructor is xUnit's setup, `IDisposable`/`IAsyncDisposable` its teardown) so nothing leaks between cases. Isolation is what makes parallelism safe and a flake rare.

**Worked example:**
```csharp
public sealed class OrderStoreTests : IAsyncDisposable           // ctor is setup, DisposeAsync is teardown
{
    private readonly OrderStore _store = new();                  // each test gets its own, no shared state
    public async ValueTask DisposeAsync() => await _store.DisposeAsync();
}
```
**Enforcement:** xUnit v3 + Microsoft.Testing.Platform pinned in `Directory.Build.props`; `dotnet test` in CI; review rejects shared mutable state between tests.

### 11.2 — Structure each test Arrange-Act-Assert; name it for the behaviour under test.

**Reasoning, step by step:**
1. Three phases — arrange the inputs and the system, act once on the thing under test, assert the outcome — keep a test readable and keep its intent singular. One act per test means a failure names one behaviour, not a tangle; separating the phases with blank lines (chapter [05](./05-methods-and-functions.md)) lets a reader find the act at a glance. The test name is the specification: `Total_sums_line_items_and_applies_tax` reads as a sentence and tells you what broke from the failure list alone, without opening the body.
2. Use `[Fact]` for a single concrete case and `[Theory]` with `[InlineData]` for a handful of literal rows or `[MemberData]` when the cases come from a method (records, computed values, anything not a compile-time constant). A `[Theory]` collapses near-identical tests into one body where the data is the variation, so adding a case is adding a row, and each row reports as its own result.

**Worked example:**
```csharp
[Theory]
[MemberData(nameof(Currencies))]                                 // cases that aren't constants come from a member
public void Round_trips_through_serialization(Money money)
{
    var restored = JsonSerializer.Deserialize<Money>(JsonSerializer.Serialize(money));
    restored.ShouldBe(money);
}
public static TheoryData<Money> Currencies() => [new(10m, "USD"), new(0m, "EUR")];
```
**Enforcement:** review of test naming and one-act structure; `xUnit1003`/`xUnit1008` (theory data well-formed); analyzer flags `[Theory]` without data.

### 11.3 — Write property-based tests with FsCheck for invariants and round-trips.

**Reasoning, step by step:**
1. An example test asserts one input maps to one output; a property test asserts a fact that must hold for *all* inputs — `Deserialize(Serialize(x)) == x` for every `x`, `Reverse(Reverse(xs)) == xs`, "the total is never negative." FsCheck (the C# analog of fast-check) generates hundreds of inputs, including the boundary and pathological ones a human would not think to type, and when it finds a counterexample it shrinks it to the smallest failing case so you debug `[0]`, not a 400-element list. The property catches the class of bug a handful of examples walks straight past.
2. Reach for properties where a law governs the code: round-trips (serialize/parse, encode/decode), invariants (a sorted list stays sorted, a balance never goes negative), and idempotence (applying twice equals applying once). Keep the generated value inside the domain by constraining the generator rather than discarding most inputs, so the run stays fast and every case is meaningful. Properties complement example tests — they prove the law, the examples pin the specific cases.

**Worked example:**
```csharp
[Property]
public Property Parsing_a_formatted_money_round_trips(decimal amount, NonEmptyString currency)
{
    var money = new Money(amount, currency.Item);
    return (Money.Parse(money.ToString()) == money).ToProperty();   // holds for all generated inputs
}
```
**Enforcement:** FsCheck for invariants, round-trips, and idempotence; review expects properties where a law governs the code, not only example tests.

### 11.4 — Prefer hand-written fakes to mocks; reach for NSubstitute only when a fake is impractical, never Moq.

**Reasoning, step by step:**
1. A fake is a small in-memory implementation of an injected interface (named for its role, chapter [02](./02-naming-conventions.md)) — an `InMemoryOrderStore` backed by a dictionary, a `FixedClock`. It tests behaviour, not call sequences: the test asserts the order was stored and can be read back, which survives a refactor of *how* the store is called. A mock that verifies "`Save` was called once with these arguments" couples the test to the implementation, so a harmless refactor reddens the suite and the test stops testing the outcome.
2. When an interface is wide or stubbing it by hand is genuinely impractical, NSubstitute is the sanctioned framework — its API is terse and it is actively, cleanly maintained. Moq is banned: the 4.20 release bundled the SponsorLink dependency, which harvested developers' email addresses at build time and exfiltrated them, an episode that makes it unfit for an enterprise supply chain regardless of later reversals. Trust, once spent that way, is not refunded; we do not ship it.

**Worked example:**
```csharp
internal sealed class InMemoryOrderStore : OrderStore           // a fake: real behaviour, in memory
{
    private readonly Dictionary<OrderId, Order> _orders = [];
    public Task Save(Order order, CancellationToken ct) { _orders[order.Id] = order; return Task.CompletedTask; }
    public Task<Order?> Find(OrderId id, CancellationToken ct) => Task.FromResult(_orders.GetValueOrDefault(id));
}
```
**Enforcement:** review prefers a hand-written fake; NSubstitute permitted when a fake is impractical; Moq banned at the package level (SponsorLink supply-chain risk).

### 11.5 — Assert with xUnit's `Assert` plus Shouldly; do not adopt FluentAssertions v8+.

**Reasoning, step by step:**
1. xUnit's built-in `Assert` covers the core, and Shouldly layers a readable, extension-method surface on top — `result.ShouldBe(165m)`, `act.ShouldThrow<ArgumentException>()` — whose failure messages quote the expression and both values, so a red test reads like a sentence and explains itself. Shouldly is MIT-licensed and free for any use, commercial included, so adopting it across an enterprise codebase carries no per-seat cost and no license review.
2. FluentAssertions was the obvious alternative, but v8 (Xceed, January 2025) moved to a paid commercial license at roughly $130 per developer per year, with only the v7 line remaining free. Standardizing a whole organization's tests on a library that can re-price the assertion you write in every test is an unacceptable supply-chain and budget risk, recorded here as the reason: we use Shouldly and stay on it. Pin the version in `Directory.Build.props` so no one drifts onto the paid line by accident.

**Worked example:**
```csharp
result.Status.ShouldBe(OrderStatus.Paid);                       // Shouldly: reads as a sentence, MIT-licensed
Should.Throw<ArgumentOutOfRangeException>(() => calculator.Total([], rate: -1m));
Assert.Equal(2, receipts.Count);                                // xUnit's built-in Assert for the core
```
**Enforcement:** Shouldly + xUnit `Assert` pinned in `Directory.Build.props`; FluentAssertions v8+ banned (paid commercial license); review rejects new FluentAssertions usage.

### 11.6 — Make every test deterministic: inject `TimeProvider`, seed randomness, no `Thread.Sleep` or real network.

**Reasoning, step by step:**
1. A test that reads `DateTime.Now`, draws an unseeded random, or sleeps to wait for work is non-deterministic — it passes on your machine and flakes in CI, and a flaky suite is one nobody trusts or reads. Inject `TimeProvider` everywhere time is read and substitute `FakeTimeProvider` in tests, which lets you set the clock and advance it explicitly (`Advance(TimeSpan.FromMinutes(5))`) instead of waiting; seed any `Random` with a fixed value so a generated case is reproducible from the log.
2. Banish ambient dependencies: no `Thread.Sleep` to synchronize (await the actual completion or advance the fake clock), no real network or live service (fake the boundary or use a container, 11.7), no assumption about the order parallel tests run or the order a dictionary enumerates. Determinism is not a nicety — it is the precondition for a green suite meaning "correct" rather than "lucky this run."

**Worked example:**
```csharp
var clock = new FakeTimeProvider(start);
var session = new Session(clock, timeout: TimeSpan.FromMinutes(10));
clock.Advance(TimeSpan.FromMinutes(11));                         // advance time, never Thread.Sleep
session.IsExpired.ShouldBeTrue();
```
**Enforcement:** `TimeProvider`/`FakeTimeProvider` injected for time; seeded randomness; review rejects `Thread.Sleep`, `DateTime.Now`, real network, and order-dependence in tests.

### 11.7 — Make integration tests exercise the real thing: `WebApplicationFactory` for the host, Testcontainers for the database.

**Reasoning, step by step:**
1. A unit test fakes the boundary; an integration test must not, because the bugs it exists to catch live in the seams — the SQL the ORM emits, the connection-string parsing, the JSON the host actually serializes. `WebApplicationFactory<TEntryPoint>` boots the real ASP.NET Core pipeline in-process so requests run through the genuine routing, middleware, and DI graph (cross-ref the [csharp-aspnetcore](../csharp-aspnetcore/) guide), and the test asserts the real response, not a mock's idea of one.
2. Back the host with a real datastore: Testcontainers spins up an actual Postgres or Redis in a container per test run, so the test runs against the engine production uses, then tears it down. An in-memory substitute (the EF in-memory provider, a fake Redis) lies about behaviour — it ignores constraints, transactions, and concurrency the real engine enforces — so a test that passes against it can ship a bug the real database would have caught. Test against the real thing; the container makes that cheap and disposable.

**Worked example:**
```csharp
var postgres = new PostgreSqlBuilder().Build();                 // a real Postgres, disposed after the run
await postgres.StartAsync(cancellationToken);
await using var factory = new WebApplicationFactory<Program>()  // the real host pipeline, in-process
    .WithConnectionString(postgres.GetConnectionString());
var response = await factory.CreateClient().GetAsync("/orders", cancellationToken);
```
**Enforcement:** `WebApplicationFactory<TEntryPoint>` for host tests, Testcontainers for real Postgres/Redis; review rejects in-memory database substitutes for integration tests; see [csharp-aspnetcore](../csharp-aspnetcore/).

### 11.8 — Cover the negative space (invalid input, cancellation, boundaries), not only the happy path.

**Reasoning, step by step:**
1. The happy path is the case you already believe works; the bugs hide in the space around it — the empty list, the maximum value, the negative amount, the duplicate key, the cancelled token. For every meaningful behaviour, test the rejection as deliberately as the acceptance: that invalid input throws the specific exception (chapter [08](./08-error-handling.md)), that a cancelled `CancellationToken` aborts promptly with `OperationCanceledException` (chapter [09](./09-concurrency.md)), that the boundary values (`0`, `int.MaxValue`, empty, one element) behave. A suite that only proves the happy path proves almost nothing.
2. To know the negative space is actually covered, gate it with mutation testing where it pays: Stryker.NET mutates the production code — flips a `<` to `<=`, deletes a branch — and reports which mutants the suite failed to kill, which is the honest measure of whether the assertions test behaviour or just execute lines. A high line-coverage number with surviving mutants means the tests run the code without checking it; the mutation score closes that gap.

**Worked example:**
```csharp
[Fact]
public async Task Load_throws_when_cancelled()                  // cancellation is part of the contract
{
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();
    await Should.ThrowAsync<OperationCanceledException>(() => _store.Load(id, cts.Token));
}
```
**Enforcement:** review requires negative-space and boundary cases per behaviour; coverage threshold in CI; optional Stryker.NET mutation gate on critical modules.

## Cross-references

- The nullable flow analysis as the first test suite, and branded types tests rely on: [03-nullability-and-the-type-system.md](./03-nullability-and-the-type-system.md). Arrange-Act-Assert structure, blank-line sections, and the options record: [05-methods-and-functions.md](./05-methods-and-functions.md).
- Specific exception types and the `Result` type tests assert against: [08-error-handling.md](./08-error-handling.md). `CancellationToken`, `OperationCanceledException`, and `async` testing: [09-concurrency.md](./09-concurrency.md).
- `InternalsVisibleTo` for test access and the public surface under test: [10-api-design.md](./10-api-design.md). Host and database integration tests: [csharp-aspnetcore](../csharp-aspnetcore/).

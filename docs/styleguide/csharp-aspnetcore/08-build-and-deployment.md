# 08 — Build & Deployment

A service runs unattended under an orchestrator that can restart it at any moment, so the artifact must be reproducible, minimal, and supervised: the same image promotes unchanged across environments, takes its configuration and secrets from the runtime, reports its health honestly, and drains cleanly on `SIGTERM`. This chapter is about shipping the host as that artifact — building it deterministically, trimming it small, and binding its lifecycle to the orchestrator's. It embodies ASP-6 and extends the core's resource discipline (core [13](../csharp/13-resource-management.md)) to the process and deployment scope.

## What good looks like

```dockerfile
# Multi-stage: build with the SDK, ship on a chiseled, non-root runtime (8.1).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<pinned> AS build
WORKDIR /src
COPY packages.lock.json .
COPY *.csproj .
RUN dotnet restore --locked-mode                       # locked restore — the tested graph (8.7, 8.8)
COPY . .
RUN dotnet publish -c Release -o /app --no-restore     # no rebuild of the restored graph

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:<pinned>   # minimal, distroless-style
WORKDIR /app
COPY --from=build /app .
USER $APP_UID                                          # non-root (8.1)
ENTRYPOINT ["./Service"]
```

The runtime image is a chiseled, non-root base carrying only what the app needs (8.1); the build is multi-stage and restores under `--locked-mode` against a committed lockfile, so the deployed bits are exactly the tested bits with no rebuild between stages (8.7, 8.8). Both base images are pinned by digest, not tag, so the build is reproducible (8.7). Configuration and secrets are absent from the image — they arrive from the environment and the orchestrator at runtime (8.3, 8.4) — and the host exposes liveness and readiness probes and drains on `SIGTERM` (8.5, 8.6).

## Rules

### 8.1 — Containerize on a minimal, non-root base with a multi-stage, locked build.

**Reasoning, step by step:**
1. The runtime image is attack surface and download weight both, so it should carry the runtime and the app and nothing else. A chiseled or distroless base strips the shell, package manager, and unused libraries the app never calls, shrinking the image and removing the tools an attacker would reach for; running as a non-root user (`USER $APP_UID`) means a container escape does not start as root.
2. Build in stages: the SDK stage restores and publishes, the runtime stage copies only the published output, so the compiler and source never ship. The restore runs `--locked-mode` against a committed `packages.lock.json` (core [1.6](../csharp/01-formatting-and-tooling.md)), so the build resolves the exact graph that was tested rather than whatever floated in since.

**Worked example:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final   # no shell, no package manager
WORKDIR /app
COPY --from=build /app .                               # only the published output crosses the stage
USER $APP_UID                                          # not root
ENTRYPOINT ["./Service"]
```
**Enforcement:** review requires a chiseled/distroless base and a non-root `USER`; multi-stage `Dockerfile`; restore runs `--locked-mode`; image scan (8.8) gates the runtime layer.

### 8.2 — Publish trimmed and, where dependencies allow, Native AOT; verify trim/AOT warnings are clean.

**Reasoning, step by step:**
1. A self-contained publish ships the whole runtime, most of which the app never calls. Trimming removes the unreachable code, and Native AOT compiles ahead of time to a native binary — sub-second startup, a fraction of the memory, no JIT — which is what an orchestrator wants when it scales pods up and down. Where the dependency graph cannot go AOT (a library that needs runtime codegen), ReadyToRun precompiles the hot paths for faster startup without the AOT constraints.
2. Trimming and AOT both reason statically about reachability, so reflection-heavy code breaks *silently* — a type reached only by name is trimmed away and fails at runtime, not at build. The trim and AOT analyzers emit warnings for exactly these hazards, so they are promoted to errors and the build fails until the code is trim-safe, which means source-generated serialization and logging instead of reflection (chapter [05](./05-serialization-and-validation.md), source-gen).

**Worked example:**
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>                         <!-- or PublishReadyToRun where AOT is blocked -->
  <TrimMode>full</TrimMode>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>   <!-- trim/AOT (IL2xxx, IL3xxx) warnings fail the build -->
</PropertyGroup>
```
**Enforcement:** trim and AOT analyzer warnings (IL2xxx/IL3xxx) treated as errors; a publish-and-run smoke test in CI catches a trimmed-away type; review requires source-gen over reflection on the publish path.

### 8.3 — Take configuration from the environment at runtime; never bake it into the image.

**Reasoning, step by step:**
1. Configuration baked into the image pins one image to one environment, which defeats the point of an immutable artifact: you would rebuild to promote from staging to production, and the thing you tested is no longer the thing you ship. So the image carries only defaults, and the per-environment values arrive at runtime — environment variables, mounted config files — layered over those defaults by the configuration system (chapter [01](./01-host-and-configuration.md)).
2. One image therefore promotes unchanged across every environment; only the injected configuration differs, so a bug reproduced in staging is reproduced by the same bits in production. The environment name itself (`ASPNETCORE_ENVIRONMENT`) is an injected value, never compiled in, and the host validates its required configuration at startup so a missing value fails fast rather than at first use (chapter [01](./01-host-and-configuration.md), `ValidateOnStart`).

**Worked example:**
```yaml
# the same image; only the injected configuration changes per environment
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: Production
  - name: ConnectionStrings__Orders          # double underscore maps to the config hierarchy
    valueFrom: { secretKeyRef: { name: orders-db, key: connection-string } }
```
**Enforcement:** review rejects environment-specific values baked into the image or `appsettings.json` per environment; one image artifact promotes across environments; required config validated at startup.

### 8.4 — Source secrets from the orchestrator or a vault; never from the image, the repo, or a build arg.

**Reasoning, step by step:**
1. A secret in the image is a secret in every layer that anyone who pulls the image can extract; a secret in the repo is in the history forever; a secret in a build `ARG` is in the build log and the image metadata. None of these can be rotated without a rebuild, and all of them widen the blast radius of a leak far beyond the running process (see [security.md](../security.md)).
2. So secrets arrive only at runtime, from a secret manager or the orchestrator's secret store — a mounted file or an injected environment variable backed by a `Secret` resource — read once at startup into the configuration system and never logged. Rotation is then an orchestrator operation, not a rebuild, and a leaked image reveals no credential.

**Worked example:**
```yaml
volumeMounts:
  - name: db-credentials
    mountPath: /run/secrets                # vault/orchestrator mounts the secret at runtime
    readOnly: true
# the host reads /run/secrets via the configuration provider; nothing secret is in the image or repo
```
**Enforcement:** secret scanner gates CI (no credentials in repo or image layers); review rejects secrets in `Dockerfile` `ARG`/`ENV` or committed config; secrets injected at runtime only.

### 8.5 — Expose liveness and readiness probes; readiness reflects real dependency health.

**Reasoning, step by step:**
1. The orchestrator needs two distinct answers, and conflating them misroutes traffic. Liveness asks "is the process wedged?" — it should be cheap and self-contained, because a failing liveness probe triggers a restart, and a restart that depends on a downstream being up creates a crash loop when that downstream is down. Readiness asks "can this instance serve a request right now?" and gates whether traffic routes to it.
2. So readiness checks the dependencies the instance actually needs — the database connection, the message broker, the cache — and reports unready when one is down, so the orchestrator drains it from the load balancer until it recovers, while liveness stays green so it is not needlessly restarted. The probes are registered as health checks and the readiness check is tagged so the two endpoints expose different sets (chapter [06](./06-logging-and-observability.md)).

**Worked example:**
```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())                      // liveness — cheap, self-contained
    .AddNpgSql(connectionString, tags: ["ready"]);                           // readiness — real dependency
app.MapHealthChecks("/health/live", new() { Predicate = c => c.Name == "self" });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```
**Enforcement:** review requires distinct `/health/live` and `/health/ready` endpoints; readiness includes the real dependencies and excludes them from liveness; the orchestrator probe config references both.

### 8.6 — Implement graceful shutdown: handle `SIGTERM`, drain in flight, dispose, exit within a deadline.

**Reasoning, step by step:**
1. When the orchestrator scales down or rolls out, it sends `SIGTERM` and waits a grace period before `SIGKILL`. A host that ignores the signal drops every in-flight request and orphans every open resource at the hard kill, surfacing as truncated responses and leaked connections during every deploy. ASP.NET Core surfaces the signal through `IHostApplicationLifetime`, and `ShutdownTimeout` bounds how long the host waits.
2. So shutdown is ordered and bounded: stop accepting new connections, let in-flight requests drain, then dispose pools, consumers, and clients in reverse of startup (core [13](../csharp/13-resource-management.md)), and exit. The drain has a deadline shorter than the orchestrator's grace period, after which the host force-exits rather than hang — a clean drain is the goal, but a hung process that never exits is worse than a bounded one that gives up.

**Worked example:**
```csharp
builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(25));  // < orchestrator grace
lifetime.ApplicationStopping.Register(() =>
{
    consumer.StopIntake();                              // stop accepting, then let in-flight drain
});
lifetime.ApplicationStopped.Register(() => pool.Dispose());   // dispose in reverse of startup
```
**Enforcement:** `ShutdownTimeout` set below the orchestrator grace period; a `SIGTERM` test confirms clean drain logs and a bounded exit; review verifies resources dispose in reverse startup order.

### 8.7 — Make the build reproducible and the artifact immutable; the deployed artifact is the tested artifact.

**Reasoning, step by step:**
1. A build that resolves differently on two runs cannot be reasoned about — you can no longer say what is in production. So pin every input: the SDK version (a committed `global.json`), every base image by digest not tag, and every package by the committed lockfile, and set `<Deterministic>true</Deterministic>` so the compiler emits byte-identical output from identical inputs. The same source then produces the same artifact anywhere.
2. The artifact is then immutable and promotes unchanged: the image that passed the test suite is the image that ships, with no rebuild between the test stage and the deploy stage, because a rebuild reintroduces the resolution drift the pinning eliminated. Tag the image by content (digest or build SHA), and deploy by that digest, so what runs is provably what was tested.

**Worked example:**
```xml
<PropertyGroup>
  <Deterministic>true</Deterministic>                   <!-- identical inputs -> identical output -->
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
</PropertyGroup>
<!-- global.json pins the SDK; Dockerfile pins base images by @sha256 digest -->
```
**Enforcement:** `global.json` pins the SDK, base images pinned by digest, `<Deterministic>` on; CI builds the artifact once and promotes it by digest with no rebuild before deploy.

### 8.8 — Gate every merge in CI: locked restore, warnings-as-errors build, full tests, container scan.

**Reasoning, step by step:**
1. A gate that runs after merge is a gate that lets the break in. So every merge passes the full pipeline first: restore under `--locked-mode` (the tested graph), build with `<TreatWarningsAsErrors>` so an analyzer or trim warning fails the build (chapter [05](./05-serialization-and-validation.md) source-gen, rule 8.2), the full test suite including the integration tests against real dependencies (core [11.7](../csharp/11-testing.md), `WebApplicationFactory` + Testcontainers), and a container image scan for known CVEs.
2. A red gate does not deploy — there is no manual override that ships a failing build, because the override is how the 3am incident gets merged. The pipeline is the single path to production, and it either passes whole or blocks; this is zero technical debt at the delivery boundary, the same standard the code holds (core [11](../csharp/11-testing.md)).

**Worked example:**
```yaml
steps:
  - run: dotnet restore --locked-mode                   # the tested dependency graph
  - run: dotnet build -c Release --no-restore           # TreatWarningsAsErrors fails on any warning
  - run: dotnet test -c Release --no-build              # full suite, integration included
  - run: trivy image --exit-code 1 --severity HIGH,CRITICAL $IMAGE   # red scan blocks deploy
```
**Enforcement:** CI runs locked restore, warnings-as-errors build, full test suite, and container scan on every merge; a red gate blocks deploy with no override; the pipeline is the only path to production.

## Cross-references

- The committed lockfile, `global.json` SDK pin, `<Deterministic>`, and warnings-as-errors: [../csharp/01-formatting-and-tooling.md](../csharp/01-formatting-and-tooling.md). Integration tests with `WebApplicationFactory` and Testcontainers, and zero technical debt at the delivery boundary: [../csharp/11-testing.md](../csharp/11-testing.md).
- Disposal order, pools, and resources bound to lifecycle phases: [../csharp/13-resource-management.md](../csharp/13-resource-management.md). Secrets out of the image and the repo: [../security.md](../security.md).
- Runtime configuration layering and `ValidateOnStart`: [01-host-and-configuration.md](./01-host-and-configuration.md). Source-generated serialization for trim/AOT safety: [05-serialization-and-validation.md](./05-serialization-and-validation.md). Health checks and probe endpoints: [06-logging-and-observability.md](./06-logging-and-observability.md).

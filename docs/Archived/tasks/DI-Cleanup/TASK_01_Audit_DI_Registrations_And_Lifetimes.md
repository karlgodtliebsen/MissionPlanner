# TASK 01 — Audit DI Registrations and Lifetimes

## Goal

Review `ApplicationConfigurator` and related service-registration code for registrations that are harder to understand than normal constructor injection.

Do not perform broad refactoring yet. First produce a concrete inventory and lifetime analysis.

## Search scope

Inspect all uses of:

```text
AddSingleton(provider => ...)
AddTransient(provider => ...)
AddScoped(provider => ...)
TryAddSingleton(provider => ...)
TryAddTransient(provider => ...)
Func<...>
IServiceProvider
GetRequiredService(...)
GetRequiredKeyedService(...)
IEnumerable<T>
```

Also inspect registrations for:

- Avalonia controls/views/windows;
- ViewModels;
- navigation factories;
- feature/tool registries;
- Logs views;
- Setup/Advanced views;
- EventHub registrations.

## Classify each non-trivial registration

Use categories:

```text
Normal registration
Alias to same instance
Factory registration
Runtime keyed-selection candidate
Metadata registry
Service-locator smell
Lifetime hazard
Intentional composition-root usage
```

## Known registrations to inspect

At minimum:

```csharp
services.TryAddTransient<Func<int, Avalonia.Controls.Control>>(provider => section => section == 1
    ? provider.GetRequiredService<ApplicationLogsView>()
    : provider.GetRequiredService<TelemetryLogsTabItemView>());
```

and all:

```csharp
services.AddSingleton(provider => new AdvancedToolRegistration(
    AdvancedFeatureId.X,
    () => provider.GetRequiredService<SomePage>()));
```

## Lifetime analysis

For each registration determine:

- lifetime of registered service;
- lifetime of dependencies;
- whether root `IServiceProvider` is captured;
- whether transient/disposable UI objects may be retained by root DI;
- whether scoped services could accidentally be resolved from root;
- whether multiple registrations of one implementation intentionally create separate instances;
- whether `IEnumerable<T>` enumeration order is relied on.

## Output

Create:

```text
docs/DI-Registration-Audit.md
```

with:

```text
Location
Current registration
Purpose
Lifetime
Risk
Recommended pattern
Refactor task
```

## Rules

- Do not mechanically replace every factory.
- `IServiceProvider` is acceptable in configurators and explicit activation factories.
- Do not move UI activation into Core/domain code.
- Preserve behavior during the audit.

## Acceptance criteria

- All non-trivial registrations in `ApplicationConfigurator` are classified.
- Known provider-capturing registrations are documented.
- Lifetime risks are identified.
- Recommended replacements map to later tasks.
- Solution still builds.

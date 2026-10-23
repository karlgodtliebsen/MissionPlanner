# TASK 04 — Remove Remaining ServiceProvider Captures and Lifetime Hazards

## Goal

Apply the Task 01 audit findings to the remaining application registrations.

Remove service-locator patterns where constructor injection, keyed DI, or a typed factory is clearer.

## Review

Search all remaining:

```text
IServiceProvider
GetService
GetRequiredService
GetKeyedService
GetRequiredKeyedService
Func<T>
Func<TKey,T>
Lazy<T>
```

## Keep only at valid boundaries

`IServiceProvider` is acceptable in:

- application composition/configuration;
- explicit typed factory implementations;
- framework activation boundaries.

Avoid it in:

- ViewModels;
- domain services;
- ordinary application services;
- metadata models;
- singleton closures;
- feature/navigation code that can use typed factories.

## Lifetime validation

Pay particular attention to:

```text
Singleton resolving Scoped from root
Singleton holding transient/disposable Views
Root provider retaining transient IDisposable
Duplicate concrete registrations
```

Ensure View/Window ownership is controlled by navigation/window lifetime rather than accidental root-container retention.

## Same instance vs separate instances

Review registrations such as EventHub.

For intentionally **separate** singleton buses prefer:

```csharp
services.AddSingleton<IEventHub, EventHub>();
services.AddSingleton<IVehicleTelemetryEventHub, EventHub>();
```

This creates two distinct `EventHub` singleton instances.

Do not additionally register:

```csharp
services.AddSingleton<EventHub>();
```

unless direct concrete resolution is required.

For intentional **aliasing to the same instance**, use:

```csharp
services.AddSingleton<MyService>();

services.AddSingleton<IFoo>(
    sp => sp.GetRequiredService<MyService>());

services.AddSingleton<IBar>(
    sp => sp.GetRequiredService<MyService>());
```

and document that same-instance semantics are intentional.

## TryAdd review

Use `TryAdd*` only where host/test/plugin override is intentionally supported.

For application-owned registrations where duplicate registration indicates a defect, prefer ordinary `Add*`.

## Typed factories

Where runtime selection is required, create narrow factories such as:

```text
ILogsViewFactory
IAdvancedToolFactory
```

Do not introduce a universal `IViewFactory` that becomes another service locator.

## DI validation

Where practical, validate the service provider using:

```csharp
new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
}
```

## Tests

Add identity tests for important semantics:

```csharp
Assert.NotSame(
    provider.GetRequiredService<IEventHub>(),
    provider.GetRequiredService<IVehicleTelemetryEventHub>());
```

Use `Assert.Same` for intentionally aliased services.

## Acceptance criteria

- Service-provider use is restricted to explicit activation/composition boundaries.
- Unnecessary root-provider captures are removed.
- Scoped/root lifetime hazards are removed.
- Intentional same-vs-separate instance semantics are explicit.
- `TryAdd*` usage is justified.
- DI validation tests pass.

# TASK 05 — DI Regression Tests and Documentation

## Goal

Make the cleaned DI architecture difficult to regress.

## Composition tests

Build the real service collection, or the closest practical application composition subset, with:

```csharp
ValidateOnBuild = true
ValidateScopes = true
```

Validate at least:

```text
Root navigation
Logs views
Advanced tools
Live Telemetry Inspector
IEventHub
IVehicleTelemetryEventHub
Major ViewModels
Browser/WASM composition
```

## Identity tests

Test registrations where instance identity is part of the architecture.

### Separate singleton instances

```text
IEventHub
IVehicleTelemetryEventHub
```

must resolve to different instances.

### Intentional aliases

Where two interfaces intentionally share a singleton, add `Assert.Same`.

## Keyed-service tests

Verify:

```text
LogsSection.Telemetry   -> TelemetryLogsTabItemView
LogsSection.Application -> ApplicationLogsView
```

and all:

```text
AdvancedFeatureId -> expected Advanced page
```

Ensure no duplicate or ambiguous keyed registrations.

## Metadata tests

For `AdvancedToolRegistration` / `IAdvancedToolCatalog`:

- exactly one entry per feature;
- stable order if order matters;
- enumerating metadata must not instantiate pages.

## Documentation

Create:

```text
docs/DependencyInjection.md
```

Document these project conventions.

### Normal registration

```csharp
services.AddSingleton<IMyService, MyService>();
```

### Same implementation type, separate singleton instances

```csharp
services.AddSingleton<IEventHub, EventHub>();
services.AddSingleton<IVehicleTelemetryEventHub, EventHub>();
```

Explain that each registration owns a separate singleton.

### Multiple interfaces aliasing one singleton

```csharp
services.AddSingleton<MyService>();
services.AddSingleton<IFoo>(sp => sp.GetRequiredService<MyService>());
services.AddSingleton<IBar>(sp => sp.GetRequiredService<MyService>());
```

Explain that this deliberately shares one instance.

### Runtime selection

Prefer keyed services + typed factory.

### Metadata/discovery

Prefer metadata records/catalogs without activation delegates.

### `IServiceProvider`

Allowed only in:

```text
Composition root
Explicit activation factories
Framework integration boundaries
```

### `TryAdd*`

Document when override-friendly behavior is intentional.

## Final cleanup

After tests pass:

- remove obsolete delegates;
- remove old registrations;
- remove unused concrete registrations;
- remove dead factory code;
- update comments;
- run formatter;
- check warnings.

## Build validation

Build/test supported desktop and Browser/WASM targets.

## Acceptance criteria

- DI composition validates.
- Keyed mappings have regression tests.
- Instance identity semantics have regression tests.
- `docs/DependencyInjection.md` exists.
- Old provider-capturing/delegate registrations are removed.
- Desktop and Browser builds pass.

# TASK 02 — Refactor Logs View Selection to Keyed Services

## Goal

Replace the current `Func<int, Control>` Logs view-selection registration with typed keyed DI and an explicit factory.

## Current registration to remove

```csharp
services.TryAddTransient<Func<int, Avalonia.Controls.Control>>(provider => section => section == 1
    ? provider.GetRequiredService<ApplicationLogsView>()
    : provider.GetRequiredService<TelemetryLogsTabItemView>());
```

Problems:

- magic integer;
- opaque delegate dependency;
- hidden service resolution;
- difficult to test/discover;
- overly generic return type.

## Typed key

Reuse an existing Logs section enum if one exists. Otherwise introduce something equivalent to:

```csharp
public enum LogsSection
{
    Telemetry,
    Application
}
```

Do not create a duplicate enum if the feature already has an appropriate type.

## Keyed registrations

Use keyed DI, e.g.:

```csharp
services.AddKeyedTransient<Control, TelemetryLogsTabItemView>(
    LogsSection.Telemetry);

services.AddKeyedTransient<Control, ApplicationLogsView>(
    LogsSection.Application);
```

If both views are also resolved directly elsewhere, preserve only the registrations actually needed.

## Typed factory

Introduce:

```csharp
public interface ILogsViewFactory
{
    Control Create(LogsSection section);
}
```

Implementation may use:

```csharp
serviceProvider.GetRequiredKeyedService<Control>(section)
```

This is an acceptable service-provider boundary because runtime activation is the factory's explicit job.

Keep the factory in the UI/application layer.

## Consumers

Replace all consumers of:

```text
Func<int, Control>
```

with:

```text
ILogsViewFactory
```

Remove numeric comparisons.

## Lifetime

Choose and document factory lifetime deliberately.

A singleton factory is acceptable only if it does not violate scoped-service rules.

## Tests

Verify:

```text
Telemetry   -> TelemetryLogsTabItemView
Application -> ApplicationLogsView
Unknown key -> controlled exception
```

Also verify:

- old `Func<int, Control>` is no longer registered;
- expected transient behavior is preserved;
- Browser/WASM composition resolves both keyed views.

## Acceptance criteria

- No `Func<int, Control>` remains for Logs.
- No magic integer selects the view.
- Logs view selection uses keyed DI.
- Consumers depend on `ILogsViewFactory`.
- Supported builds/tests pass.

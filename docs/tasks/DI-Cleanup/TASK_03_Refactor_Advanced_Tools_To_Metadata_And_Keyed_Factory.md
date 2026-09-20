# TASK 03 — Refactor Advanced Tools to Metadata + Keyed Factory

## Goal

Remove page-activation closures from `AdvancedToolRegistration`.

Separate:

```text
Metadata/discovery
```

from:

```text
Activation
```

## Current pattern to remove

Example:

```csharp
services.AddSingleton(provider => new AdvancedToolRegistration(
    AdvancedFeatureId.Nmea,
    () => provider.GetRequiredService<NmeaPage>()));
```

Apply to:

```text
Nmea
Mirror
Signing
Proximity
Inspector
Warnings
```

and any equivalent advanced-tool registrations found by Task 01.

## Metadata model

Refactor `AdvancedToolRegistration` so it contains metadata only.

Example:

```csharp
public sealed record AdvancedToolRegistration(
    AdvancedFeatureId FeatureId,
    string? Title = null,
    string? Icon = null,
    int SortOrder = 0);
```

Preserve existing useful fields.

Do not store:

```text
Func<Page>
Func<Control>
IServiceProvider
```

inside metadata.

## Keyed page registrations

Register each Advanced page keyed by `AdvancedFeatureId`.

Example:

```csharp
services.AddKeyedTransient<Control, NmeaPage>(
    AdvancedFeatureId.Nmea);

services.AddKeyedTransient<Control, MirrorPage>(
    AdvancedFeatureId.Mirror);

services.AddKeyedTransient<Control, SigningPage>(
    AdvancedFeatureId.Signing);

services.AddKeyedTransient<Control, ProximityPage>(
    AdvancedFeatureId.Proximity);

services.AddKeyedTransient<Control, MavLinkInspectorPage>(
    AdvancedFeatureId.Inspector);

services.AddKeyedTransient<Control, WarningManagerPage>(
    AdvancedFeatureId.Warnings);
```

Use a more specific common UI abstraction than `Control` if the existing architecture already has one.

## Typed factory

Add:

```csharp
public interface IAdvancedToolFactory
{
    Control Create(AdvancedFeatureId featureId);
}
```

Implementation resolves keyed pages.

Keep `IServiceProvider` isolated inside this explicit activation factory.

## Catalog/discovery

If consumers currently inject:

```csharp
IEnumerable<AdvancedToolRegistration>
```

preserve this for metadata discovery, or introduce:

```csharp
IAdvancedToolCatalog
```

to make discovery explicit.

Remember:
- `IEnumerable<T>` returns all registrations;
- resolving one `T` returns the last registration.

Prevent ambiguous single-resolution usage if necessary.

## Availability

Tool availability should be driven by metadata/policy/capabilities, not by constructing pages just to inspect them.

## Tests

Verify:

- every expected `AdvancedFeatureId` has one metadata entry;
- every feature resolves the correct page;
- metadata enumeration does not instantiate pages;
- no metadata registration captures `IServiceProvider`;
- unknown feature id fails clearly;
- Browser/WASM behavior remains correct;
- transient views are not rooted by singleton metadata.

## Acceptance criteria

- `AdvancedToolRegistration` is metadata-only.
- Advanced pages use keyed DI.
- Activation is centralized in `IAdvancedToolFactory`.
- Existing ordering/visibility/navigation behavior is preserved.

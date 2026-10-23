# Task 06 — Decoder Catalog, DI Registration, and Validation

## Objective

Make generated and hand-written decoders discoverable without fragile manual registration.

## Required design

Create a decoder catalog that combines:

- generated decoders
- hand-written overrides
- protocol-specific custom decoders

Guarantee exactly one effective typed decoder per message ID.

Avoid runtime assembly scanning if deterministic generated registration is practical. Prefer a generated registration extension such as:

```csharp
services.AddGeneratedMavLinkDecoders();
```

Integrate with `MavLinkConfigurator` and `MavLinkMessageDecoders`.

## Startup validation

Fail fast with a clear exception for:

- duplicate decoder IDs
- decoder ID missing from registry
- hand-written override not declared
- generated model with no effective decoder
- incompatible payload length metadata

## Tests

- the application service provider resolves the complete decoder catalog;
- every generated typed message has one decoder;
- every override replaces exactly one generated decoder;
- duplicate registrations fail deterministically;
- current tests using manually constructed decoder arrays are migrated without losing isolation.

## Acceptance criteria

- Adding a new dialect message through regeneration requires no manual DI edits.
- Existing MAVFTP and parameter decoding remain functional.

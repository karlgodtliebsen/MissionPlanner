# ADR-0005: Catalog-driven map source architecture

- Status: Accepted
- Date: 2026-08-11

## Context

Map providers were represented by UI strings and Mapsui construction code. That cannot consistently express terms, attribution, credentials, offline capability, or future archive formats and encourages provider-specific behavior to spread across views.

## Decision

Introduce a platform-neutral `MissionPlanner.Maps` project with a versioned embedded catalog. Model provider, product, source, access kind, archive format, tile content, credentials, capability, usage policy, and attribution as separate concepts with stable identifiers.

The catalog validates identifiers and cross-references before use. Technical capability and policy permission remain separate. Future candidates may be recorded disabled, but catalog presence alone never enables them. Renderer integration belongs outside views; credentials are referenced rather than embedded; settings persist stable source identifiers.

## Consequences

- Provider additions require catalog, policy, attribution, adapter, UI, and test review.
- Existing map choices can migrate without changing visible behavior.
- Offline packs and hosted providers share a consistent policy boundary.
- Catalog schema changes require explicit version handling.
- A separate decision is required before production vector packs are implemented.

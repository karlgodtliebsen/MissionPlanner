



# Concepts


- The architecture is more important than any individual implementation.

- Code will evolve, protocols will expand, and UI technology may change. 

- The architectural boundaries described in this document should remain stable. 

- New features should fit within these boundaries rather than extending across them.






# Future Direction


- Multi-vehicle mission planning

- Swarm support

- Plugin architecture

- Terrain services

- AI-assisted mission planning

- Offline simulation

- Log replay

- Cloud synchronization

- Video integration

- ROS2 integration











## ADR-0001 - Immutable Vehicle State

Status: Accepted

VehicleState is immutable.

Reasons:
- Thread safety
- Easier testing
- Predictable event flow
- Simpler debugging

Consequences:
- Uses C# record types.
- State transitions happen only inside VehicleSession.






## ADR-0002 - Channels only inside the communication pipeline

Status: Accepted

- Channels are used only for communication pipelines.
- They are NOT used as the application's event bus.
- Ordinary .NET events are limited to narrow, directly owned relationships where their
  synchronous coupling is intentional.
- Cross-component application communication uses EventHub.
- Domain events use `IDomainEventHub`; application-shell navigation uses
  `INavigationEventHub`.

Reason:
- The project already has an EventHub that models domain events.
- Channels solve buffering and throughput problems, not domain communication.
- Purpose-specific EventHub abstractions avoid coupling consumers to the full general hub.

Consequences:
- EventHub subscription handles are owned and disposed by the subscriber.
- A broad event must not require consumers to obtain a specific publisher instance merely
  to attach a .NET event handler.


## ADR-0003 - MAVLink dialect generation is an offline protocol boundary

Status: Accepted

Decision:
- Official MAVLink XML is pinned and vendored with provenance and license data.
- A committed manifest defines the root/include graph, generator ownership, compatibility
  exceptions, promotion catalog, and deterministic outputs.
- Generated protocol files are never edited manually and normal builds never download
  dialect data.

Consequences:
- CI regenerates into a temporary directory and fails when committed output drifts.
- Hand-written wire overrides must be declared in both generator ownership and the manifest.
- Independent conformance vectors are committed data, not a Python runtime dependency.
- Generating a typed packet does not promote it into immutable domain state; promotion stays
  an explicit architectural decision governed by `MAVLINK_DOMAIN_PROMOTION.md`.


## ADR-0004 - Context-aware domain objects use IDomainFactory

Status: Accepted

Decision:
- Constructor dependencies available from DI remain constructor-injected.
- When construction also requires values produced in the local calling context, register
  the service-to-implementation mapping with `IDomainFactory` in `UseDomainServices`.
- The caller passes only the local values through the matching `Create<...>()` overload.
- Callers do not manually construct the implementation or forward its DI services, options,
  and logger.

Reason:
- Context values such as `ParameterEditScope` cannot be registered as global services.
- Manual construction duplicates the composition root and makes constructor changes leak
  into unrelated factories.
- `IDomainFactory` combines explicit local context with normal DI resolution while
  preserving the service abstraction.

Consequences:
- New context-aware domain services require an explicit factory mapping.
- `IDomainFactory` is not used where ordinary constructor injection is sufficient.
- Tests configure the same mapping and service provider behavior as production.


## ADR-0005 - Catalog-driven map source architecture

Map sources are represented by a validated, versioned catalog in the platform-neutral `MissionPlanner.Maps` project. Provider identity, products, access methods, policy, capabilities, attribution, and credential requirements are separate concepts. See [ADR-0005](adr/ADR-0005-map-source-architecture.md).

## ADR-0006 - Defer vector/PMTiles

Production vector/PMTiles support is deferred because the current Mapsui vector renderer is experimental and a supported MAUI alternative spanning Windows, Android, and Mac Catalyst is not established. Raster MBTiles remains the production offline path, and conditional Task 06 is not authorized. See [ADR-0006](adr/ADR-0006-defer-vector-pmtiles.md).

## Naming Conventions

Transport

&#x20;   Bytes



Parser

&#x20;   Frames



Decoder

&#x20;   Messages



Domain

&#x20;   Observations


VehicleSession

&#x20;   State



Application

&#x20;   DTOs


UI

&#x20;   ViewModels




\## Data flow



USB / UDP / TCP

&#x20;       │

&#x20;       ▼

&#x20;IMavLinkTransport

&#x20;       │

&#x20;       ▼

&#x20; MavLinkClient

&#x20;       │

&#x20;       ▼

&#x20;MavLinkConnection

&#x20;       │

&#x20;       ▼

&#x20;Channels

&#x20;       │

&#x20;       ▼

&#x20;FrameParser

&#x20;       │

&#x20;       ▼

&#x20;DecoderRegistry

&#x20;       │

&#x20;       ▼

&#x20;VehicleMessagePump

&#x20;       │

&#x20;       ▼

&#x20;Capability Handlers

&#x20;       │

&#x20;       ▼

&#x20;VehicleSession

&#x20;       │

&#x20;       ▼

&#x20;VehicleRegistry

&#x20;       │

&#x20;       ▼

&#x20;Domain Events

&#x20;       │

&#x20;       ▼

&#x20;UI Services

&#x20;       │

&#x20;       ▼

&#x20;ViewModels

## ADR: Constrained declarative vehicle automation

**Status:** Accepted (2026-08-07)

**Context:** Unrestricted embedded C#, Python, or Lua would execute arbitrary code inside
the GCS process. Out-of-process scripting adds runtime, IPC, permission, and cross-platform
lifecycle complexity. Onboard ArduPilot Lua is a separate deployment workflow that depends
on reviewed MAVFTP upload and mutation support.

**Decision:** MissionPlanner initially supports versioned declarative JSON. Every action is
allow-listed, the complete document is validated before execution, and vehicle changes use
existing typed safety-aware services and operation gates. Execution is sequential,
cancellable, timeout-bounded, stop-on-failure, and auditable. Dry run has no side effects.
Arbitrary code, command IDs, IO, reflection, dynamic compilation, loops, and parallel
execution are prohibited.

**Consequences:** The format is deliberately less expressive, but portable, reviewable,
deterministic, and testable. New actions require an explicit registry and typed-service
change. Out-of-process plugins and onboard Lua remain separate future decisions.









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

Reason:
- The project already has an EventHub that models domain events.
- Channels solve buffering and throughput problems, not domain communication.





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





# UDP Client and WebSocket implementation inventory

Audit date: 2026-09-29. This is an implementation checkpoint, not a claim of
completed network support or SITL verification.

## Existing paths and confirmed gaps

- UI: `src/UI/MissionPlanner.App/Views/Connect/ConnectPopupViewModel.cs`.
  The selection is lowercased before dispatch; its former uppercase UDPCl comparison
  was unreachable. WS/WSS reached an unsupported result, and UDP Client had no service call.
  These options now give an explicit inline explanation before any connection attempt.
- UI configuration previously shared one host/port pair and an IPv4-only control.
  The first review layout uses readable labels while retaining UDP/UDPCl/WS/WSS keys,
  hostname input, local UDP fields, full URL input, validation, and transport-specific
  in-dialog drafts. Baud rate is only shown for serial/Auto.
- Application API: `src/Core/MissionPlanner.Core/Vehicles/Abstractions/IVehicleConnectionService.cs`
  and `VehicleConnectionService.cs`: serial, TCP, and UDP entry points only.
  UDP waits up to ten seconds for a registered vehicle heartbeat; socket readiness
  is not connection success. ActiveConnection currently records UDP plus the local port.
- Session: `VehicleConnectionSession.CreateUdpConnection` constructs the transport
  options and acquires the existing shared message pump and parameter services.
- Pipeline: `src/Core/MissionPlanner.MavLink/Services/MavLinkConnectionSessionFactory.cs`
  creates the transport, client, and connection through the domain factory.
  `Client/MavLinkClient.cs` reads bounded byte blocks and sends through the same transport;
  `Services/MavLinkConnection.cs` parses, decodes, and publishes messages and resets
  parser state on startup. Reuse this pipeline.
- Socket: `src/Core/MissionPlanner.Transport/UdpMavLinkTransport.cs` uses one bound
  UdpClient for reads and writes. It does not filter the sender for a configured peer,
  resolve the configured remote hostname on startup, or send an initial packet.
  It rejects local port zero and truncates datagrams larger than the supplied read buffer.
  Configured remote settings are validated but do not establish a UDP client peer.
- No WebSocket transport or WebSocket session factory path exists in NextGen.
- No periodic outbound GCS heartbeat sender was found in the inspected Core/MAVLink
  connection path. `MavLinkKnownFrames.CreateHeartbeatV2` exists, but must not be
  mistaken for an active heartbeat mechanism.
- Diagnostics: reuse VehicleConnectionMonitor, MavLinkConnectionActivity,
  IVehicleLiveDiagnostics and IActiveVehicleContext. Preserve shared diagnostic panel layouts.
- Reconnect dispatch: `VehicleConnectionService.Reconnect.cs` currently handles serial,
  TCP and UDP. New transport identity and configuration must survive reconnect.
- The documented application service owns one active link. Multiple vehicles on a link
  and multiple simultaneous independent links are different requirements; the latter
  needs explicit connection ownership work, not a UI-only selector change.

## Classic reference

Classic selects dedicated UDP Client and WebSocket transports in
[MainV2](https://github.com/ArduPilot/MissionPlanner/blob/master/MainV2.cs).
Its [UDP Client implementation](https://github.com/ArduPilot/MissionPlanner/blob/master/ExtLibs/Comms/CommsUDPSerialConnect.cs)
resolves hostnames and uses the same socket for sending and receiving.
Its Open method contains no startup heartbeat; any startup MAVLink emission must
be traced above that transport, rather than copied from a presumed socket handshake.

Classic's [WebSocket implementation](https://github.com/ArduPilot/MissionPlanner/blob/master/ExtLibs/Comms/CommsWebSocket.cs)
accepts binary bytes but also sends a Socket.IO text probe and has special text/prefix
handling. NextGen's proposed raw-binary contract is deliberately not Socket.IO compatibility.

## Contract for the implementation after layout review

WebSocket binary messages carry raw MAVLink bytes. Message boundaries are not packet
boundaries: accept fragmentation, split packets, and multiple packets per message.
Reject text/JSON/Socket.IO with a clear error. Preserve URL path/query on connection,
use normal TLS certificate validation, and redact user information and query values
in logs, errors and diagnostic endpoints. Serialize sends, bound buffers, cancel/await
loops on disposal, and never replay queued commands on reconnect.

UDP Client resolves and fixes one remote peer, permits ephemeral local ports, sends
and receives on one socket, and filters unexpected senders. A session-owned GCS
heartbeat starts before vehicle discovery and stops with that session. UDP Listen
retains its existing listening behavior. Both need cancellable startup and useful
heartbeat-timeout diagnostics.

## Checkpoint and remaining work

Task 1 inventory is recorded here. Task 2 has a first compiled dialog layout ready
for the requested review. Settings currently survive switching within the dialog;
persistent per-transport settings and migration are still pending. Custom UDP bind
addresses are displayed but explicitly rejected until the transport API supports them.

Tasks 3–6 remain: UDP Client implementation, WebSocket/WSS implementation, shared
progress/diagnostics/reconnect integration, deterministic fixtures, SITL launch scripts,
WSL instructions, multi-vehicle routing coverage, and verification of telemetry,
parameter download and command acknowledgements. No hardware or SITL connection
has been performed at this checkpoint.

Validation: affected app/test projects built through dotnet test --no-restore;
ConnectPopupTests passed, including serial preference, passive Auto UDP discovery,
draft retention, endpoint validation, and no connection calls for unsupported transports.
Production XAML was rendered at 380 x 650 for UDP Listen, UDP Client, and WebSocket.
An existing ParameterProfilesViewModelTests constructor argument-order mismatch was
corrected to allow the test project to compile.

# Codex Task 8 — DroneCAN Workspace, CubeID Update and ESP8266 Component Setup

## Goal

Implement the Optional Hardware tools that operate on non-primary components/buses:

```text
DroneCAN / UAVCAN
CubeID Update
ESP8266 Setup
```

These features must preserve target component/node identity instead of assuming the autopilot component.

---

# Part A — DroneCAN / UAVCAN

## Classic reference

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigDroneCAN.cs
src-v.1.38/Controls/DroneCAN*
src-v.1.38/ExtLibs/DroneCAN/*
```

The current NextGen source does **not** yet contain an equivalent DroneCAN domain subsystem.

Do not paste the old 1700-line WinForms control into Avalonia.

---

## DroneCAN architecture

Create a dedicated subsystem/project/namespace boundary if needed:

```text
IDroneCanTransport
IDroneCanService
DroneCanNode
DroneCanNodeStatus
DroneCanNodeInfo
DroneCanParameter
DroneCanFirmwareUpdate...
```

Support transport adapters separately:

```text
MAVLink CAN tunnel / SLCAN mode when supported by active vehicle
Direct SLCAN-compatible adapter
```

Do not couple direct adapter and MAVLink-tunneled operation into the UI.

### Minimum usable scope

1. connect/select CAN transport;
2. discover nodes;
3. show node ID, name, health/mode/version;
4. refresh node information;
5. inspect basic node statistics;
6. read/edit supported node parameters;
7. restart node with confirmation;
8. lifecycle-safe disconnect.

### Next scope

9. firmware update;
10. inspector/raw message view;
11. CAN bus statistics;
12. filters/passthrough.

Do not claim full DroneCAN parity until these are implemented/tested.

### CAN FD / protocol variants

Keep DroneCAN/UAVCAN v0 semantics separate from newer UAVCAN/Cyphal concepts.

Do not label a feature as general Cyphal support unless it actually is.

---

# Part B — CubeID Update

Classic reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigCubeID.cs
```

The current generated MAVLink stack already contains CubePilot firmware-update message definitions.

Create a typed updater service using those generated messages.

Requirements:

- detect the target CubeID/ODID component rather than assuming 1/1;
- support official firmware download and local `.bin`;
- CRC calculation;
- chunking via the expected MAVLink encapsulated-data protocol;
- progress based on acknowledged offset;
- retry/timeouts;
- cancellation before/where safe;
- clear diagnostics;
- no blocking thread sleeps;
- firmware source/version displayed before update.

If pass-through setup parameters are required on the autopilot (`SERIAL_PASS*` style), make those explicit and readback-confirmed.

Do not automatically overwrite serial pass-through configuration without preview/confirmation.

---

# Part C — ESP8266 MAVLink component setup

Classic reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigHWesp8266.cs
```

Classic Mission Planner targets:

```text
MAV_COMP_ID_UDP_BRIDGE
```

and reads/writes parameters on that component, including packed string fields.

Build a component-targeted parameter service or extend the current parameter boundary so a caller can explicitly target:

```text
VehicleId/SystemId
ComponentId
```

Do not pollute the primary autopilot parameter registry with ambiguous same-name component parameters.

### Settings

Support only parameters actually reported by the UDP bridge component.

Typical concepts:

```text
SSID
password
station SSID/password
Wi-Fi mode
channel
UART baud
UDP ports
IP/gateway/subnet
debug
```

Use current component metadata/presence where available.

Packed 4-byte string parameter encoding/decoding must be isolated and unit-tested.

Secrets:

- mask passwords in UI by default;
- never log packed password values;
- diagnostic export redacts secrets.

---

## Tests

DroneCAN:

1. transport abstraction independently testable;
2. node discovery/update;
3. target node identity preserved;
4. parameter read/write target correct node;
5. disconnect cleans subscriptions;
6. direct and MAVLink transport state do not collide.

CubeID:

7. CRC/chunk calculation.
8. response offset advances progress.
9. timeout/retry.
10. wrong component cannot consume response.
11. local/official firmware source tracked.

ESP8266:

12. UDP-bridge component discovery.
13. component-target parameter routing.
14. 16-byte packed SSID/password round trip.
15. password redaction.
16. disconnect clears component state.

---

## Acceptance criteria

Complete when DroneCAN has a real node workspace, CubeID update uses typed generated MAVLink messages, and ESP8266 configuration correctly targets the UDP-bridge component.

# Codex Task 6 — External Serial Tool Foundation, SiK Radio and Bluetooth Setup

## Goal

Implement standalone/direct-serial Optional Hardware tools safely:

```text
SiK Radio
Bluetooth Setup
```

These are different from MAVLink vehicle parameter pages because they may talk directly to a serial device when no vehicle is connected.

Build the serial-resource boundary first, then the two tools.

---

## Classic references

```text
src-v.1.38/Radio/Sikradio.cs
src-v.1.38/GCSViews/ConfigurationView/ConfigHWBT.cs
```

Do not port WinForms/thread-sleep code directly.

---

## 1. Exclusive serial-device session abstraction

Create or reuse a transport abstraction that provides:

```text
serial device enumeration
open with explicit baud
exclusive ownership
read/write with cancellation
line/timeout helpers where appropriate
safe close/dispose
```

Before adding a new abstraction, inspect existing serial/device discovery code in:

```text
MissionPlanner.Transport
MissionPlanner.Firmware/Discovery
MissionPlanner.Firmware/Recovery
```

Reuse existing descriptors/enumeration where possible.

### Conflict rule

A direct serial tool must not seize the same COM/TTY device being used by the active MAVLink connection.

If the selected device is currently owned by MissionPlanner's active transport:

```text
Disconnect the vehicle before using this direct serial tool.
```

Fail cleanly rather than trying to share the port.

---

# Part A — SiK Radio

Classic `Sikradio.cs` is large. Port behavior in layers.

## Initial required scope

- select serial port;
- detect/connect to local SiK/RFD-compatible radio;
- enter command/config mode safely;
- read local settings;
- read remote settings when supported;
- edit validated settings;
- write/save/reboot;
- sync appropriate local/remote settings;
- show firmware/version/device identity;
- communication log with secrets redacted;
- cancellation/timeouts.

Do not use fixed sleeps as the primary synchronization mechanism. Use bounded protocol reads with cancellation and explicit state.

## Firmware update

Only add firmware update after configuration transport is stable.

Requirements:

- local firmware file option;
- device/board compatibility validation where possible;
- progress;
- cancellation before destructive phase;
- diagnostics;
- do not reuse the FC firmware installer blindly if protocol is different.

If firmware update is too large for one commit, implement it as a clearly separated second part within this task, but do not leave UI buttons that do nothing.

---

# Part B — Bluetooth module setup

Classic behavior probes common HC-05/HC-06 style AT-command variants at several bauds and writes:

```text
name
baud
PIN/password
role/reset
```

Create a typed `IBluetoothSerialConfigurator` rather than sending strings from the ViewModel.

Requirements:

- select direct serial device;
- probe supported baud rates with bounded timeouts;
- detect the AT dialect from actual responses;
- show detected module/dialect;
- edit Name, Baud and PIN only when supported;
- explicit Apply;
- report command/response status;
- redact PIN from logs;
- do not send every possible command variant blindly after first detecting the dialect.

This feature configures classic serial Bluetooth modules; do not present it as generic Bluetooth LE configuration.

---

## UI/lifecycle

These tabs may remain visible with no vehicle.

On activation:

- enumerate devices;
- do not automatically open a port without user action.

On deactivation:

- cancel operations;
- close direct serial sessions.

---

## Tests

Serial foundation:

1. exclusive ownership blocks active MAVLink port.
2. cancellation closes port.
3. timeout does not leak background reads.

SiK:

4. connect/detect state machine.
5. bounded failed probe.
6. settings parse/write round trip.
7. remote-unavailable case.
8. secrets not logged.

Bluetooth:

9. dialect detection.
10. correct command generated for detected dialect.
11. unsupported field is disabled.
12. baud change reconnect behavior is safe.
13. PIN redaction.

---

## Acceptance criteria

Complete when SiK and classic serial-Bluetooth setup work through a reusable safe direct-serial boundary and are genuinely usable without an FC connection.

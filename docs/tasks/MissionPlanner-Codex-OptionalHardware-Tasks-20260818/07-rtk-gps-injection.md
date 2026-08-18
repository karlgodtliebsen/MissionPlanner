# Codex Task 7 — RTK / GPS Injection Workspace

## Goal

Implement a modern RTK/GPS correction injection workspace.

This is a large Optional Hardware feature and should be treated as a subsystem, not as a code-behind serial utility.

Primary use case:

```text
Correction source -> RTCM3 stream -> active vehicle via MAVLink GPS_RTCM_DATA
```

The application currently supports one active vehicle, so initial injection targets that active vehicle.

---

## Classic reference

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigSerialInjectGPS.cs
```

The classic view includes many source/protocol features (serial, TCP/NTRIP, RTCM, UBX/SBP/base setup).

Use it as behavioral reference, but design a smaller explicit service model first.

NextGen generated MAVLink already contains GPS RTCM message definitions; use the generated protocol path.

---

## Architecture

Suggested boundaries:

```text
IRtkCorrectionSource
IRtkCorrectionSourceFactory
IRtcm3Framer / parser
IRtkInjectionService
RtkInjectionSnapshot
RtkSourceStatus
RtkMessageStatistics
```

Source adapters can include:

```text
Serial
NTRIP/TCP
File/replay for tests
```

Do not make the ViewModel own sockets/serial streams.

---

## Phase 1 required features

### Correction source

Support:

```text
Serial RTCM3 source
NTRIP client
```

For NTRIP:

- caster host/port;
- mount point;
- username/password if required;
- reconnect with bounded backoff;
- secret redaction;
- TLS where supported/appropriate;
- clear connection diagnostics.

Do not store credentials in plaintext logs.

### RTCM framing

Parse enough RTCM3 framing to:

- identify complete frames;
- validate frame length;
- optionally validate CRC when implementation is available;
- count message types;
- reject unbounded/corrupt input;
- preserve exact correction bytes for forwarding.

Do not decode all RTCM observation semantics merely to inject corrections.

### MAVLink fragmentation

Implement correct `GPS_RTCM_DATA` fragmentation/flags semantics for RTCM frames larger than one MAVLink payload.

Requirements:

- no interleaving fragments from two RTCM frames;
- correct sequence/fragments;
- connection-scoped cancellation;
- bounded queue/backpressure;
- do not flood the MAVLink link indefinitely if source outruns transport.

### Target

Injection requires:

- active online target;
- current endpoint/session;
- cancellation on target change/disconnect.

If no vehicle is connected, the RTK tab may still configure/connect the source and show source statistics, but injection status must be:

```text
No active vehicle target
```

---

## UI

Suggested sections:

```text
Correction Source
  Type: Serial / NTRIP
  Connect / Disconnect

Source Status
  data rate
  RTCM messages seen
  last correction age

Vehicle Target
  active vehicle
  correction packets sent
  send errors

Optional Base Position / Survey-in
```

Do not start with the full 1600-line classic UI.

---

## Phase 2 — base receiver/survey-in support

After Phase 1 is stable, port only the high-value classic functionality:

- fixed base position;
- survey-in status;
- saved base positions;
- receiver-specific configuration only behind explicit adapter interfaces.

Classic supports receiver-specific behavior for devices such as u-blox, Septentrio and Unicore. Do not put all vendor commands into one service.

Create vendor adapters only when the protocol is understood/testable.

---

## Security / reliability

- NTRIP password is secret.
- No credential in logs/diagnostic export by default.
- Source disconnect must not block UI.
- Active-vehicle disconnect immediately stops injection.
- reconnecting a vehicle must not automatically inject to a different target without clear state.
- source can remain connected while target is absent only if queueing is disabled/bounded; never replay stale corrections later.

---

## Tests

1. RTCM frame parser handles complete/partial input.
2. malformed length is rejected.
3. large RTCM frame fragments correctly into GPS_RTCM_DATA.
4. flags/sequence are deterministic.
5. no fragment interleaving.
6. active vehicle disconnect stops injection.
7. correction source may remain connected without target but stale frames are not replayed.
8. source backpressure remains bounded.
9. NTRIP auth header generation is tested without logging password.
10. reconnect/backoff cancels cleanly.
11. source statistics update.
12. test source can inject into fake MAVLink transport.

---

## Acceptance criteria

Complete when MissionPlanner can connect to a real RTCM source, show correction health, and inject corrections safely to the active vehicle using the generated MAVLink stack.

# Execution results — 2026-09-23

## Root cause

The parser correctly accepts CRC-valid MAVLink 2 zero-trimmed frames, preserving
their exact wire payload. `ServoOutputRawMessageDecoder` required at least 21
bytes. A valid 12-byte quad output frame therefore failed typed decoding and
became a `RawMavLinkMessage` through the existing raw fallback. It remained visible
in the Raw journal but never reached `RadioTelemetryHandler`, which handles
`ServoOutputRawMessage`.

The Raw description's "decoded length 37" identifies the dialect's full payload
length; it did not demonstrate successful typed servo decoding.

## Implemented

- The servo decoder accepts validated payloads of 1–37 bytes and zero-pads to 37
  bytes before reading fields. This preserves all 16 channels, bank, timestamp,
  and partial little-endian channel values. MAVLink 1 wire-length enforcement
  remains in the existing parser.
- Existing dispatcher, radio handler, `VehicleSession`, domain-event bridge, and
  diagnostic export now receive the typed sample without a parallel state model.
- Outputs distinguishes never seen, fresh, and stale samples. Freshness defaults
  to two seconds, matching servo setup, and also requires an online connection.
  Sample age and reception time are exposed. Physical movement is never inferred.
- Reconnect clears the prior disconnected diagnostic state while retaining
  historical journal and raw entries. The existing registry reset provides new
  authoritative vehicle sessions. Incoming samples repopulate output state.
- Raw capture, frame bytes, recording, and `.tlog` format were not changed.

## Validation

`dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj
--no-restore --filter <selected suites> -v quiet` built the affected production
projects and passed **56 tests, zero failures, zero skips**.

Selected suites: `ServoOutputPropagationTests`, `VehicleAdvancedDiagnosticTests`,
`CoreVehicleStateTelemetryTests`, `VehicleLiveDiagnosticsTests`,
`MavLinkConformanceFixtureTests`, `MavLinkGeneratedWireModelTests`,
`MavLinkRawFallbackTests`, `TelemetryRecordingTests`, `VehicleReconnectTests`, and
`MavLinkDecoderCatalogTests`.

Nine new regression cases cover full MAVLink 1, 12-byte MAVLink 2, base-field and
extension-field partial ushort trimming, full extensions/nonzero bank, all-zero
outputs, value/timestamp replacement (including unchanged values), exact vehicle
identity isolation, freshness expiry, disconnect/reconnect, malformed frames,
and decoded state/Raw/export consistency. They use real CRC-valid wire frames,
the parser, production decoder catalog, dispatcher, registry/session, domain and
telemetry hubs, and the Raw inspection adapter.

Existing warnings remain in unrelated files; no CS1591 or CS1587 warnings were
reported. `git diff --check` passed. No hardware or interactive UI test was run,
and the full solution/test suite was not run. The Inspector already refreshes
output details on each presentation tick, so expiry requires no new UI polling.

Updated canonical documentation: `docs/LiveTelemetryInspector.md` and
`docs/FEATURES.md`.

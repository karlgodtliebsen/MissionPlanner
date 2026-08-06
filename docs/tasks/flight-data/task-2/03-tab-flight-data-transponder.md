# Flight Data 03 — Transponder, ADS-B Out and traffic status

## Objective

Implement `TransponderTabView` with component-scoped discovery, uAvionix ADS-B Out status/control, health reporting and a bounded nearby-traffic view.

Apply all constraints from `00-README.md`.

## Existing generated protocol coverage

```text
AdsbVehicleMessage
UavionixAdsbOutCfgMessage
UavionixAdsbOutDynamicMessage
UavionixAdsbTransceiverHealthReportMessage
UavionixAdsbOutCfgRegistrationMessage
UavionixAdsbOutCfgFlightidMessage
UavionixAdsbGetMessage
UavionixAdsbOutControlMessage
UavionixAdsbOutStatusMessage
MavCmd.DoAdsbOutIdent
```

The promotion catalog currently assigns these to raw/planned protocol workflows.

## Shared component discovery

This task establishes a component registry reused by Payload Control.

1. Add a registry keyed by `VehicleId + ComponentId`.
2. Populate it from component heartbeats and component-information responses.
3. Track MAV type, autopilot type, first/last seen, capabilities/protocol identities and stale/online state.
4. Do not hard-code component ID 100 or assume one transponder.
5. Keep peripheral component state separate from autopilot aggregate `VehicleState`.

## Transponder protocol service

Add dedicated models/services such as:

```text
ITransponderService
TransponderComponentState
TransponderConfiguration
TransponderDynamicState
TransponderHealth
TransponderOperationResult
AdsbTrafficTrack
```

Requirements:

- Route/correlate generated messages by system/component ID.
- Request current configuration/status with bounded timeout.
- Maintain state per discovered component.
- Validate squawk as four-digit octal.
- Expose ICAO/registration/flight ID, squawk, mode A/C/S and TX flags, IDENT, emergency state, pressure altitude/dynamic state, health/faults and timestamps.
- Encode supported configuration/control messages using generated encoders.
- Where no ACK exists, confirm through observed response/status and report timeout separately.
- Implement IDENT through typed `MavCmd.DoAdsbOutIdent` with explicit confirmation.
- Do not silently map unsupported vendor protocols.

## ADS-B traffic store

- Maintain a bounded per-vehicle store of `AdsbVehicleMessage` tracks keyed by ICAO address.
- Update tracks in place, expire stale entries, preserve validity flags, and distinguish unavailable fields.
- Do not add all traffic tracks to `VehicleState`.

## UI

Provide a component selector, unsupported/not-discovered state, configuration/status/health, squawk editor, supported mode/TX controls, confirmed IDENT action, refresh, operation state, and a virtualized traffic list. Disable writes when stale, disconnected or replaying.

## Tests

Cover multi-component discovery, routing, expiration/reconnect, octal validation, config/status correlation, IDENT targeting/confirmation, unsupported behavior, traffic deduplication/expiry, replay/disconnect/cancellation and selected-component ViewModel behavior.

## Documentation

- Add Transponder/ADS-B sections to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`, `docs/MAVLINK_DOMAIN_PROMOTION.md`, and relevant owners/consumers in `docs/mavlink-promotion-catalog.json`.
- Update `docs/MAVLINK.md` if component routing or outbound encoding contracts change.

## Acceptance criteria

- Supported components show real state and controls.
- Unsupported/not-discovered states are explicit.
- Every operation targets the selected component.
- Traffic storage is bounded and vehicle-scoped.

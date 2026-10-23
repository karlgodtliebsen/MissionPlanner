# Task 05 — Generate Typed Wire Models and Decoders

## Objective

Generate typed protocol models and decoders for all non-deprecated messages in the selected dialects, while retaining hand-written adapters where business semantics require them.

## Architecture

Generated wire models belong in `MissionPlanner.MavLink`, not in `MissionPlanner.Core`.

Use generated records/readonly structs that expose fields in protocol order and preserve exact MAVLink field types. Avoid marshaling and unsafe layout unless benchmarks prove it necessary.

Each generated decoder must:

- expose its message ID;
- validate minimum/maximum length through the central registry;
- decode little-endian primitive values;
- decode fixed arrays without allocations where practical;
- handle MAVLink 2 omitted extension fields by returning protocol defaults;
- preserve null-terminated fixed strings correctly;
- never read beyond the payload;
- return false or a structured decode error for malformed payloads.

## Hand-written compatibility

Existing types such as:

- `HeartbeatMessage`
- `AutopilotVersionMessage`
- `FileTransferProtocolMessage`
- mission and parameter messages

may remain hand-written if they contain carefully designed semantics. The generator must support an exclusion/override list so generated duplicates are not emitted.

Document every override and test it against the generated schema.

## Scope management

Generate inbound and outbound wire models, but do not create domain handlers for all of them.

Create generated encoders only where a message is valid for outbound use, or implement a general generated serializer if the design remains maintainable.

## Tests

Create table-driven tests for all generated decoders using generated canonical payload fixtures. Include:

- minimum-length payload
- maximum-length payload
- truncated extension fields
- signed and unsigned integer boundaries
- IEEE float special values where legal
- fixed strings and arrays
- malformed payloads

Add explicit regression tests for high-value messages:

- `AUTOPILOT_VERSION`
- `EXTENDED_SYS_STATE`
- `AHRS`, `AHRS2`, `AHRS3`
- `VIBRATION`
- `RADIO_STATUS` and ArduPilot `RADIO`
- `HWSTATUS`
- `WIND` and `WIND_COV`
- `FLIGHT_INFORMATION`
- ESC telemetry message families

## Acceptance criteria

- Every selected non-deprecated message has a typed wire model and decoder, except documented exclusions.
- Generated code compiles without warnings.
- Existing public semantics are preserved or migrated deliberately.

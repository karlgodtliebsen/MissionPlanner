# Task 11 — Dialect Conformance Fixtures and End-to-End Tests

## Objective

Prove the generated implementation against authoritative vectors and realistic streams.

## Fixture generation

Generate canonical MAVLink 1 and MAVLink 2 frames from the official dialect definitions using an independent implementation, preferably `pymavlink` during fixture generation.

Commit compact deterministic fixture data, not a runtime Python dependency unless intentionally accepted.

Each fixture should record:

- dialect/source revision
- message name and ID
- field values
- payload bytes
- complete unsigned frame bytes
- CRC extra
- expected decoded representation

Include minimum and maximum payload variants for extension-bearing messages.

## Test layers

### Registry tests
All definitions, IDs, CRCs, and lengths.

### Decoder conformance
Every generated decoder against independent vectors.

### Parser stream tests
Chunked input, multiple frames, noise, corrupt CRC, signatures, sequence wrap, and reconnect boundaries.

### Dispatcher tests
Typed decoder precedence, raw fallback, protocol-service routing, and domain-handler routing.

### SITL tests
Use ArduCopter SITL logs or live optional tests to verify representative traffic without requiring SITL for the default unit-test run.

### Performance tests
Measure allocations and throughput for high-rate frame parsing and decoding. Establish regression thresholds that are stable enough for CI.

## Acceptance criteria

- Every generated message decoder has an independent conformance vector.
- Parser tests cover MAVLink 1 and 2.
- High-rate decoding does not regress unreasonably.
- Tests do not depend on `src-v.1.38` at runtime.

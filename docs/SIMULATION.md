# Simulation

## Architecture

The Simulation workspace is an application surface over the Core simulation domain. It
does not start processes or create MAVLink transports directly. `ISimulatorRuntime`
represents a local process, container, or remote adapter, and returns one
`ISimulatorRuntimeSession` with an exact owned identity. Cleanup calls that exact session;
MissionPlanner never searches for or terminates processes by executable name.

`ISimulationSessionManager` owns the lifecycle state machine:

`Stopped → Validating → Starting → WaitingForHeartbeat → Running → Stopping → Stopped`

A clean independent exit becomes `Completed`; validation, startup, readiness, runtime, or
cleanup failure becomes `Failed`. Start cancellation and application shutdown perform a
bounded stop and disposal of the exact runtime session. Navigation away from the page only
detaches UI observation and does not implicitly terminate a running simulator.

Runtime adapters report stdout/stderr lines, runtime identity/PID when applicable,
connection endpoints, heartbeat readiness, and completion. The manager retains a bounded
recent-output window and emits structured lifecycle logs. The default adapter reports an
explicit unavailable capability until the ArduPilot SITL launch adapter is added by
Simulation step 03.

## Profiles

Versioned profiles are persisted through a platform store and contain:

- stable profile identity and name;
- expected ArduPilot firmware family and frame/model identifier;
- latitude, longitude, altitude, heading, and speedup;
- named UDP/TCP endpoints;
- binary version, absolute path, and source;
- additional argument tokens and environment values.

Arguments are stored as individual tokens, not as a shell command. A runtime adapter must
use an argument-list API. Before start, Core validates location and numeric ranges,
supported firmware families, duplicate and occupied ports, absolute binary path and file
existence, Unix execute permission where applicable, and runtime-specific compatibility.
Port availability validation is an early diagnostic; step 03 adds reservation for the
launch handoff.

Corrupt or unsupported profile persistence fails closed to a safe editable default.
Simulator acquisition and verified cached versions are added in step 02.

## Diagnostics and privacy

The workspace can export a JSON diagnostic bundle containing lifecycle state, exact runtime
identity, endpoints, profile, and bounded recent output. Environment names that look like
passwords, secrets, tokens, or API keys are redacted, as are matching `name=value` argument
tokens. A later Simulation diagnostics task extends this bundle with connection and playback
statistics.

Lifecycle limits are configured by the `Simulation` application section. Defaults are a
20-second heartbeat wait, a 10-second graceful stop, and 500 retained output lines.

## Current verification boundary

Task 01 is covered with fake-runtime tests for successful state transitions, explicit stop,
unexpected exit, heartbeat timeout cleanup, shutdown cleanup, profile persistence and
corrupt recovery, port/path validation, diagnostics redaction, and navigation lifecycle.
It does not claim that an ArduPilot binary can launch yet; installation and launch are the
next two sequential tasks.

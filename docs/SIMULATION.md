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
recent-output window and emits structured lifecycle logs. Desktop composition now selects
the direct ArduPilot SITL adapter; unsupported application platforms still fail through an
explicit platform capability.

## Profiles

Versioned profiles are persisted through a platform store and contain:

- stable profile identity and name;
- expected ArduPilot firmware family and frame/model identifier;
- latitude, longitude, altitude, heading, and speedup;
- named UDP/TCP endpoints;
- binary version, absolute path, and source;
- additional argument tokens and environment values.
- instance number, expected SystemId, ordered defaults files, wipe/console/map choices, and
  typed additional serial endpoints.

Arguments are stored as individual tokens, not as a shell command. A runtime adapter must
use an argument-list API. Before start, Core validates location and numeric ranges,
supported firmware families, duplicate and occupied ports, absolute binary path and file
existence, Unix execute permission where applicable, and runtime-specific compatibility.
Port availability validation is an early diagnostic; the runtime also holds an in-process
reservation for the complete owned-session lifetime.

## Direct SITL launch and MAVLink ownership

`ArduPilotSitlRuntime` supports the direct Copter, Plane, Rover, and Sub binaries. Its
conservative frame catalog exposes Copter `quad`, `hexa`, `octa`, `octa-quad`, `tri`, `y6`,
and `heli`; Plane `plane` and `quadplane`; Rover `rover` and `balancebot`; and Sub `vectored`
and `vectored_6dof`. A family/frame outside that catalog fails before process creation. The
catalog is intentionally narrower than arbitrary free-form model text and must be updated
only against supported SITL help/defaults.

The launch-plan builder produces individual process tokens for model, home, speedup,
instance, SystemId, serial zero, defaults files, wipe, and typed serial 1–9 UDP/TCP client
endpoints. Paths and values containing spaces remain one argument-list item. Free-form
tokens cannot override model, home, speedup, instance, SystemId, serial endpoints,
defaults, or wipe. The platform host uses `ProcessStartInfo.ArgumentList` with no shell,
an isolated per-session working/log directory, and redirected stdout/stderr. A visible
console is created only when the profile explicitly requests it. Map integration means the
verified simulated vehicle enters the same registry-backed MissionPlanner map/telemetry
pipeline; it is not an unsupported SITL `--map` shell flag.

`SimulationPortAllocator` atomically leases the endpoint identities against other owned
sessions and rechecks operating-system availability before launch. Separate profiles can
therefore use distinct instance/SystemId/port sets without an in-process collision.

After the exact process starts, the state remains `WaitingForHeartbeat` while an isolated
simulator connection session binds the allocated UDP endpoint. Each SITL owns its transport,
MAVLink client, connection, parameter service, and parameter state; all transports share one
reference-counted inbound message dispatcher and the existing multi-vehicle registry. The
connected heartbeat must match both the allocated SystemId and firmware family. Hardware
connections and other simulator connections are never replaced. Exact channel routes bind
session ID plus `VehicleId` to the correct outbound connection, so commands, mission
transfers, parameter-backed controls, and scenarios cannot fall through to another active
transport. Shutdown order is exact connection, exact process tree, then port lease, and
registry cleanup removes only the disconnected vehicle.

Early process exit and connection timeout are distinct failures. The former includes a
bounded tail of startup stderr, while the latter states that the process is alive but no
verified MAVLink heartbeat arrived.

## Multi-instance orchestration

`ISimulationFleetAllocator` derives every member from one base profile. Allocation is
deterministic: member index increments SITL instance and MAVLink SystemId, applies one fixed
port stride to all primary and additional serial endpoints, applies an ordered north/east/
altitude/heading launch offset, and assigns
`instance-NNN-sysid-NNN/{runtime,telemetry,dataflash,cache}` artifact directories. Allocation
checks connected vehicles plus occupied instances/endpoints atomically and fails before any
process starts on a collision or range overflow. Line and grid helpers only produce launch
offset data; they do not command a formation or implement swarm flight behavior.

`ISimulationFleetManager` creates an independent `ISimulationSessionManager` per allocation.
Start-all and stop-all use caller-bounded concurrency, return an ordered result for every
member, and continue unrelated operations after a per-member failure. The workspace lists
all members with process state, exact vehicle, connection state, endpoints, failure, and an
explicit active selection. A runtime completion event changes only its owning member; peer
processes and the selection remain intact. Scenario run requests continue to carry an exact
runtime session ID and `VehicleId`, including when launched from a selected fleet member.

Local process ownership is persisted separately from runtime artifacts with a random marker
token, session ID, PID, normalized executable path, and operating-system process start time.
On the next application lifetime, recovery attempts only inactive markers. The desktop
recovery service kills a process tree only when PID, path, and start time all match; a reused
PID, access-denied inspection, or any other ambiguity is left untouched and blocks a
potentially colliding fleet launch for operator review.

Corrupt or unsupported profile persistence fails closed to a safe editable default. A
profile may pin the stable identity of a configured external installation or a verified
cache entry. Resolution of an identity pin is exact: if that installation is absent or
incompatible, the workspace reports it instead of substituting a similarly named binary.

## Location and runtime simulation controls

The profile editor provides typed latitude, longitude, altitude, and heading fields,
built-in location presets, and map-click selection. These values remain launch-profile
data and pass through the normal profile validator before SITL is started. Runtime weather,
sensor, and fault values are a separate control surface and reusable scenario presets are
persisted separately from launch profiles.

`ISimulationControlCatalog` contains only controls backed by documented ArduPilot SITL
parameters. Wind speed, direction, and turbulence use `SIM_WIND_SPD`, `SIM_WIND_DIR`, and
`SIM_WIND_TURB`. GPS loss resolves the current `SIM_GPS1_ENABLE` name or the legacy
`SIM_GPS_DISABLE` alias by live parameter presence. Compass and RC failures use
`SIM_MAG1_FAIL` and `SIM_RC_FAIL`; temporary battery voltage uses `SIM_BATT_VOLTAGE`.
The catalog deliberately exposes rangefinder failure as unavailable because ArduPilot
documents simulated rangefinders but no general bounded runtime failure parameter. See the
[SITL simulation parameter guide](https://ardupilot.org/dev/docs/SITL_simulation_parameters.html),
[simulated-device guide](https://ardupilot.org/dev/docs/adding_simulated_devices.html), and
[SITL parameter source](https://github.com/ArduPilot/ardupilot/blob/master/libraries/SITL/SITL.cpp).

Capabilities are discovered from the exact connected simulator vehicle's live parameter
registry and include the observed firmware version, selected parameter alias, current
value, unit, bounds, and an explanation when unavailable. Every write retains both the
simulation session ID and verified `VehicleId`, uses the existing MAVLink parameter service,
and must receive matching registry readback. A session/vehicle change prevents a delayed
operation from crossing into another SITL instance.

Hazardous controls require explicit confirmation and a positive duration no greater than
the catalog limit. Their safe or captured original value is reset automatically, by an
explicit Reset action, on service disposal, or by best effort after a partially failed
apply. Each confirmed apply, reset, automatic reset, or failure is retained as a bounded
event with wall time, elapsed simulation time, exact session, vehicle, parameter, value,
and result. Scenario presets store optional location plus requested control values and
durations in their own versioned document; loading stages values and does not silently
perform hazardous writes.

## Declarative scenario runner

`ISimulationScenarioRunner` executes schema-version 1 JSON documents. The schema is closed:
unknown JSON members are rejected and there is no shell command, script text, expression
engine, reflection hook, or arbitrary MAVLink command field. Variables are exact-name
Boolean, finite-number, or bounded-text values; they cannot refer to other variables or
contain executable expressions. Embedded missions contain only typed MAVLink mission-item
fields. Mission start is an internal typed `MAV_CMD_MISSION_START` operation and is not a
user-selectable command ID.

Supported steps are `waitForState`, `setMode`, `arm`, `takeoff`, `uploadMission`,
`startMission`, `waitCondition`, `injectFault`, `clearFault`, `land`, and
`assertTelemetry`. Every step requires an explicit 1–3600 second timeout. Conditions can
read only the allow-listed online, armed, mode, landed state, GPS fix, position, altitude,
ground speed, and battery fields with typed comparisons and optional numeric tolerance.
Fault steps must resolve to a live, documented, confirmation-required control and fit that
control's catalog safety duration.

A run request captures the exact running simulation session ID and verified `VehicleId`.
The runner checks that pair before every step, then reuses the acknowledged command,
mission-transfer, mode-catalog, registry, and simulation-control services. A dry run
performs schema, target, mode, mission, and live-control capability checks and emits planned
step evidence without changing the vehicle. Arm, takeoff, mission start, and fault
injection additionally require one explicit run confirmation.

Pause is cooperative only at step boundaries: an active command or wait is never suspended
mid-protocol. Cancellation interrupts the current bounded operation, resets controls
injected by the run, and attempts a land command in flight or a confirmed disarm on the
ground when the scenario previously armed/took off and the exact target remains connected. Cleanup never crosses to a
replacement session. Each report records version, run/scenario/target identities, step
start/end/result, acknowledgement or condition evidence, boundary telemetry, validation
capabilities, and final telemetry. The workspace exports both indented JSON and readable
text reports.

Example safe document:

```json
{
  "schemaVersion": 1,
  "id": "6d9f5f05-2906-4c36-af7c-0a8d6abf4d40",
  "name": "Connected simulator check",
  "variables": {},
  "steps": [
    {
      "id": "online",
      "kind": "waitForState",
      "name": "Wait for exact simulator",
      "timeoutSeconds": 10,
      "state": "online"
    }
  ]
}
```

## SITL installations and releases

`ISitlInstallationService` combines two explicitly different sources:

- external binaries listed in the `Sitl:ExternalInstallations` configuration section;
- application-owned packages selected from the static `Sitl:Releases` list or an injected
  official HTTPS manifest at `Sitl:ManifestUrl`.

External paths are probed with an argument-list process API and a short `--version` timeout
when the current host supports native execution. They remain user-owned: cache removal and
retention APIs reject them. MissionPlanner does not crawl the machine for executables and
does not delete or replace a configured external path.

Manifest selection requires an exact firmware family, release channel, platform, and CPU
architecture match. Downloads accept only absolute HTTPS URLs and a 64-character SHA-256
digest. The archive is streamed into a bounded temporary file, checked before extraction,
and then extracted into a unique staging directory. Rooted paths, parent traversal,
symbolic links, non-file tar entries, excessive downloads, and excessive expanded content
are rejected. Only a complete staging tree containing the declared executable is moved to
its versioned installation directory. Cancellation and failure remove temporary staging
content and never publish a partial installation.

The repository intentionally contains no invented artifact endpoint. Deployments must
configure a trusted official manifest or signed-off static release metadata. An HTTPS
transport plus checksum protects the selected bytes; operators remain responsible for the
manifest trust source.

### Runtime combinations

| Application host | Artifact selector | Architectures | Current boundary |
| --- | --- | --- | --- |
| Windows desktop | Windows | x64, Arm64 | Discovery, version probe, install, and selection |
| Linux desktop | Linux | x64, Arm64 | Discovery, version probe, install, and selection |
| App hosted inside WSL | WSL | x64, Arm64 | Supported when MissionPlanner itself runs in that WSL environment |
| macOS desktop | macOS | x64, Arm64 | Discovery, version probe, install, and selection |
| Android/iOS/other | None | None | Explicitly unavailable for local SITL execution |

Native Windows does not currently bridge into a separate WSL distribution. Actual release
availability depends on the trusted manifest containing the exact platform/architecture
artifact. Task 03 supplies the runtime launch adapter; task 02 only acquires and selects the
binary safely.

## Diagnostics and privacy

The workspace can export a JSON diagnostic bundle containing lifecycle state, exact runtime
identity, endpoints, profile, and bounded recent output. Environment names that look like
passwords, secrets, tokens, or API keys are redacted, as are matching `name=value` argument
tokens. A later Simulation diagnostics task extends this bundle with connection and playback
statistics.

Lifecycle limits are configured by the `Simulation` application section. Defaults are a
20-second heartbeat wait, a 10-second graceful stop, and 500 retained output lines.

## Current verification boundary

Tasks 01–06 are covered with fake-runtime tests for successful state transitions, explicit stop,
unexpected exit, heartbeat timeout cleanup, shutdown cleanup, profile persistence and
corrupt recovery, port/path validation, diagnostics redaction, and navigation lifecycle.
Manifest filtering, checksum verification, atomic failure, archive traversal, cache
retention/ownership, platform capability, and exact profile-pin behavior have deterministic
tests. Typed argument/override, frame catalog, port collision/release, heartbeat
timeout/wrong-identity, early stderr, and ownership-order tests cover direct launch.
Control tests cover location/range/unit validation, current-versus-legacy parameter
discovery, firmware reporting, unavailable-control explanations, confirmation, automatic
and explicit reset, cancellation, preset persistence, and replacement-instance isolation.
Scenario tests cover closed-schema/version parsing, typed variables, required timeouts,
dry-run capabilities, success/failure/timeout/cancellation, safe-boundary pause/resume,
wrong targets, missing controls, report exports, and view-model exact-target projection.
Fleet tests cover deterministic allocation, identity/endpoint collisions, exact command
routing, bounded concurrency, partial start/stop failures, per-session output/events,
crash isolation, selection stability, and persisted orphan recovery.

Real SITL smoke cases for all four families are opt-in and have 30-second total and
10-second cleanup bounds. Set `MISSIONPLANNER_SITL_ARDUCOPTER`,
`MISSIONPLANNER_SITL_ARDUPLANE`, `MISSIONPLANNER_SITL_ROVER`, or
`MISSIONPLANNER_SITL_ARDUSUB` to an installed verified executable; absent families are
reported as environmental skips rather than waiting or downloading. Run them with:

When the Copter binary is supplied, its smoke case additionally runs the bounded
wait-for-GPS, arm, Guided-mode, takeoff, altitude, land, and landed-state scenario through
the production runner inside the same total timeout.

```powershell
dotnet test .\src\Tests\MissionPlanner.Core.Tests\MissionPlanner.Core.Tests.csproj --filter FullyQualifiedName~ArduPilotSitlSmokeTests
```

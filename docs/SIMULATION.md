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
therefore use distinct instance/SystemId/port sets without an in-process collision; task 06
adds multi-session orchestration and automatic deterministic allocation.

After the exact process starts, the state remains `WaitingForHeartbeat` while
`SimulatorVehicleConnection` calls the existing `IVehicleConnectionService` UDP path. It
does not construct another MAVLink client, decoder, dispatcher, registry, or parameter
stack. The connected heartbeat must match both the profile SystemId and firmware family.
An existing live connection is never silently replaced: the simulator reports that the
user must disconnect it first. Every successful vehicle connection now carries an opaque
connection-generation ID, so runtime cleanup can disconnect only the generation it owns.
Shutdown order is connection, exact process tree, then port lease; a stale owned-disconnect
request cannot tear down a newer hardware connection.

Early process exit and connection timeout are distinct failures. The former includes a
bounded tail of startup stderr, while the latter states that the process is alive but no
verified MAVLink heartbeat arrived.

Corrupt or unsupported profile persistence fails closed to a safe editable default. A
profile may pin the stable identity of a configured external installation or a verified
cache entry. Resolution of an identity pin is exact: if that installation is absent or
incompatible, the workspace reports it instead of substituting a similarly named binary.

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

Tasks 01–03 are covered with fake-runtime tests for successful state transitions, explicit stop,
unexpected exit, heartbeat timeout cleanup, shutdown cleanup, profile persistence and
corrupt recovery, port/path validation, diagnostics redaction, and navigation lifecycle.
Manifest filtering, checksum verification, atomic failure, archive traversal, cache
retention/ownership, platform capability, and exact profile-pin behavior have deterministic
tests. Typed argument/override, frame catalog, port collision/release, heartbeat
timeout/wrong-identity, early stderr, and ownership-order tests cover direct launch.

Real SITL smoke cases for all four families are opt-in and have 30-second total and
10-second cleanup bounds. Set `MISSIONPLANNER_SITL_ARDUCOPTER`,
`MISSIONPLANNER_SITL_ARDUPLANE`, `MISSIONPLANNER_SITL_ROVER`, or
`MISSIONPLANNER_SITL_ARDUSUB` to an installed verified executable; absent families are
reported as environmental skips rather than waiting or downloading. Run them with:

```powershell
dotnet test .\src\Tests\MissionPlanner.Core.Tests\MissionPlanner.Core.Tests.csproj --filter FullyQualifiedName~ArduPilotSitlSmokeTests
```

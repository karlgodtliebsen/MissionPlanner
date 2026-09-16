# Bench telemetry replay regression harness

The existing Telemetry Logs playback view uses ReplaySessionManager: load a standard tlog, play at
0.1–50 times recorded speed, pause, seek, or close to stop. Playback is isolated from live vehicles.
Outbound MAVLink transmission stays disabled until the replay is closed.

For fast deterministic regression playback, construct ReplaySessionManager with ImmediateReplayDelay.
It reads every indexed packet in file order without wall-clock waits. The same TelemetryLogReader,
MAVLink frame parser, decoder and vehicle telemetry handlers serve both modes. No separate parser or
simplified arming reducer exists in the tests.

ReplayTelemetryPipeline now routes STATUSTEXT through the shared StatusTextHandler and maintains its
own bounded VehicleMessageStore. StatusMessages exposes assembled text in arrival order. Reset replaces
the registry, history and assembler. Replay creates no background chunk-expiry timers; expiry is advanced
before each decoded frame using its recorded timestamp. Incomplete text at end-of-file remains pending
until a later recorded frame reaches its timeout, or is discarded on reset. Playback speed and host load
therefore cannot change chunk assembly or allow old-log callbacks into a new session.

## Synthetic fixtures

Fixtures/BenchReplayFixtures.cs in MissionPlanner.Core.Tests creates small standard tlog streams from
explicit heartbeat, SYS_STATUS and STATUSTEXT packets. Timestamps and source identity are fixed; CRCs use
the existing MAVLink CRC implementation and dialect definitions. Fixtures contain no captured customer
traffic or external licensed recordings.

BenchTelemetryReplayTests covers:

- Accelerometer and compass pre-arm failures retained through unrelated messages.
- Logging ENOSPC retained as onboard-storage evidence.
- RC1 and RC4 rejected arming attempts retained separately from pre-arm feedback.
- Healthy pre-arm SYS_STATUS transitioning to Ready to Arm.
- Armed heartbeat overriding a retained pre-arm blocker.
- Identical outcomes on repeated logs, STATUSTEXT ordering, recorded-time chunk assembly/expiry,
  and clean history/arming state after replacement or close.

SimulationPlaybackDiagnosticsTests additionally verifies scaled intervals, pause at a packet boundary,
seek reconstruction, close/disposal and blocked outbound transmission. Replay publishes the same
VehicleState arming/diagnostic records used for UI projections; these tests do not operate a physical FC.

Run the focused suite:

    dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj -p:UsedAvaloniaProducts= --filter "FullyQualifiedName~BenchTelemetryReplayTests|FullyQualifiedName~SimulationPlaybackDiagnosticsTests"

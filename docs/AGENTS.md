# MissionPlanner Codex Instructions

This file provides repository-level instructions for Codex and other coding agents.
Detailed architecture and coding rules remain authoritative in `src/ai.md`, the files in
`docs/`, and `src/.editorconfig`.

## Read before editing

Read the documents relevant to the task:

1. `src/ai.md`
2. `docs/DESIGN_CONCEPTS.md`
3. `docs/ARCHITECTURE_DECISION_RECORDS.md`
4. `docs/FEATURES.md`
5. The subsystem document, such as `docs/VEHICLE_CONNECTION.md`, `docs/PARAMETERS.md`, or `docs/MISSIONS.md`

Do not duplicate those documents here.

## Repository layout

- `src/Core/MissionPlanner.Transport`: byte transports only.
- `src/Core/MissionPlanner.MavLink`: MAVLink frames, messages, encoding, and decoding.
- `src/Core/MissionPlanner.Core`: domain models, observations, application/domain services.
- `src/UI/MissionPlanner.App`: MAUI views and ViewModels.
- `src/Tests`: unit, simulator, smoke, and hardware integration tests.
- `src-v.1`: original Mission Planner source for behavioral reference only; do not modify it unless explicitly requested.

## Working rules

- Inspect the existing implementation and tests before changing code.
- Make the smallest coherent change that satisfies the task.
- Preserve architectural boundaries and existing public APIs when practical.
- Do not introduce a framework, package, or parallel architecture without explicit justification.
- Keep MAVLink structures out of UI code and domain rules out of transport/protocol code.
- Keep `VehicleState` immutable; state transitions belong to `VehicleSession`.
- Use Channels for bounded communication pipelines, not as the domain event bus.
- Use EventHub for application and domain events.
- Avoid blocking, per-message allocations, and verbose logging in telemetry hot paths.
- Follow `src/.editorconfig`; do not restate or override its formatting and naming rules.
- Write comments and developer documentation in English.

## Verification

From `src/`, prefer:

```powershell
dotnet restore .\MissionPlanner.slnx
dotnet build .\MissionPlanner.slnx --no-restore
dotnet test .\MissionPlanner.slnx --no-build
```

When the full MAUI solution cannot build on the current platform, build and test the
affected non-UI projects explicitly and state what was not verified.

For serial hardware tests:

- Ensure no other process owns the COM port.
- Use cancellation timeouts.
- Always disconnect and dispose in teardown.
- Do not run hardware tests as part of an ordinary unit-test loop unless requested.

## Change reporting

At completion, report:

- The behavior changed.
- Important design choices.
- Tests/build commands executed and their results.
- Remaining risks or work that could not be verified.
- Documentation updated when behavior or architecture changed.

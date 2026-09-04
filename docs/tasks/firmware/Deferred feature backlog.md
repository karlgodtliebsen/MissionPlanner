# Deferred firmware feature backlog

These entries are deliberately deferred. They are not supported by the modern APJ/PX4 serial-bootloader workflow and must not be exposed through partially functional UI actions.

## Legacy firmware installation

Status: Backlog

Create an isolated “Install Firmware Legacy” route and workflow for retired hardware. Research and implement AVR `.hex`, Arduino/STK protocols, VRBrain, board retirement metadata, and prominent retired-board warnings. Keep legacy protocol types outside the modern ArduPilot bootloader client and require protocol-specific compatibility/verification tests before enabling a board.

Definition of ready:

- Supported legacy board/protocol matrix and upstream sources are documented.
- Test hardware and redistributable tooling are identified.
- Recovery and verification behavior is defined for every supported protocol.
- The UI cannot confuse legacy images with APJ/PX4 packages.

## STM32 DFU and bootloader recovery

Status: Backlog

Assess native/vendor dependencies and licensing for STM32 DFU detection, `.hex`, `_with_bl.hex`, bootloader recovery, and device-driver behavior on each desktop platform. This is a distinct transport and safety workflow, not an extension switch in the APJ reader.

Definition of ready:

- DFU discovery and exclusive ownership abstractions are designed.
- Intel HEX parsing, address-range validation, and verification have bounded tests.
- Tool/vendor redistribution and platform installation requirements are approved.
- Recovery from interrupted bootloader writes is documented and hardware-tested.

## Secure firmware and secure bootloaders

Status: Research backlog

Research secure bootloader variants, signed firmware formats, supported boards, trust roots, key storage, key revocation, recovery, and required warnings before designing an API. Secure flashing must fail closed and must not be represented as another APJ boolean or a bypassable compatibility result.

Definition of ready:

- ArduPilot/upstream threat model and supported secure-board matrix are cited.
- Signature-chain and anti-rollback requirements are specified.
- Mission Planner never stores private signing keys unless a separately reviewed design requires it.
- Revocation, lost-key, and recovery procedures have explicit operator consequences.

## Force Bootloader

Status: Research backlog

Study the original Mission Planner behavior and current upstream command semantics before exposing any action. Determine supported firmware/boards, acknowledgement behavior, armed-state requirements, whether it mutates persistent state, and recovery when the application does not return.

Definition of ready:

- Command and parameter semantics are confirmed against authoritative upstream sources.
- Safety gates, confirmations, ACK mapping, and timeout behavior are specified.
- The action cannot compete with an active transport owner or normal firmware operation.
- Simulated and real-hardware recovery tests exist.

## Additional transports

Status: Backlog, one separately scoped feature per transport

- UART through a telemetry adapter: define ownership, bandwidth, reboot transition, and bounded framing behavior.
- DroneCAN: use node/file-server semantics and identity appropriate to CAN; do not tunnel the serial client abstraction.
- BlueOS/network upload: define authenticated network API integration, TLS/trust behavior, progress, and rollback.
- SD-card `.abin`: validate container/signature/target metadata and document on-device activation behavior.
- Mobile USB host: assess Android/iOS/macOS permissions, drivers, lifecycle, background limits, and safe detach behavior.

Each transport requires its own capability model, threat/safety review, simulated tests, supported-platform declaration, and hardware smoke procedure. None is enabled by the initial firmware composition root.

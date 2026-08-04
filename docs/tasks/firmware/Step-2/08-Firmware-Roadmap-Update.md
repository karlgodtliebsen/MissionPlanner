# MissionPlanner Firmware — Updated Roadmap

## Current position

The modern serial/APJ firmware subsystem is substantially implemented.

Current strengths:

- Dedicated UI-independent firmware project.
- ArduPilot manifest/catalogue support.
- APJ/PX4 parsing and checksum validation.
- Bounded serial bootloader protocol.
- Board/capacity compatibility checks.
- Download/artifact storage.
- Installation orchestration.
- Connected embedded-bootloader update.
- Returning-device discovery.
- Connected/disconnected MAUI UI.
- Diagnostic reports.
- Strong automated test inventory.

The next phase should focus on user confidence and recoverability rather than duplicating the original Mission Planner’s frame-picture presentation.

---

# Phase 0 — Verify current baseline

- Run complete firmware tests.
- Run application build.
- Record current failures/skips.
- Repeat F4 hardware protocol with the exact matching APJ target.
- Complete first H7 serial smoke test.

Exit criteria:

- Baseline evidence is current and reproducible.

---

# Phase 1 — Current workflow hardening

Priority defects:

- Interaction-code mismatch.
- Ignored user rejection/cancel.
- Typed device selection.
- Wire application device into temporary MAVLink reboot.
- Serialize catalogue refresh.
- Add pre-destructive Cancel.

Exit criteria:

- No raw interaction codes.
- Cancel is reliable.
- Explicit device can be used for automatic reboot-to-bootloader.
- Rapid release changes do not race.

---

# Phase 2 — Download and selection UX

Implement:

- Explicit platform/manufacturer/board search.
- Hardware-evidence recommendations.
- No first-item auto-selection.
- Download & Validate.
- Package/provenance detail panel.
- Persistent manifest cache.
- Durable atomic artifact cache.
- Copy URL/Save As/cache controls.
- Named firmware HTTP client.

Exit criteria:

- Karl can complete the non-destructive user download protocol.
- Exact hardware target is clear before installation.

---

# Phase 3 — Documentation and support

Implement embedded sections for:

- Target selection.
- Release channels.
- File types.
- Normal serial versus DFU.
- ArduPilot firmware server.
- Custom Firmware Builder.
- Windows Device Manager.
- STM32CubeProgrammer.
- Driver recovery.
- Platform restrictions.
- Recovery guidance.

Exit criteria:

- Essential guidance remains available offline.
- Official links are centralized and tested.

---

# Phase 4 — DFU phase one: external STM32CubeProgrammer CLI

Implement:

- DFU contracts/state model.
- Intel HEX inspector.
- Windows USB DFU device catalogue.
- CubeProgrammer tool locator/version policy.
- Safe process runner.
- CLI provider.
- `_with_bl.hex` resolver/download.
- Explicit target-safety confirmation.
- Advanced/recovery DFU UI.
- Program + verify.
- Driver diagnostics.
- F4/H7 hardware tests.

Exit criteria:

- Initial/recovery ArduPilot install succeeds on supported F4 and H7 boards.
- Wrong target is blocked before provider execution where evidence exists.
- Verify is mandatory.

---

# Phase 5 — DFU hardening and cross-platform assessment

- Linux CubeProgrammer provider.
- macOS provider assessment.
- Improved CubeProgrammer output localization/version support.
- Artifact mirrors and provenance.
- More STM32 family/board coverage.
- External flash board policy.
- Recovery of interrupted operations.

Exit criteria:

- Provider abstraction remains stable across platforms.

---

# Phase 6 — Native DFU provider decision

Research only after phase-one evidence.

Evaluate:

- libusb/WinUSB dependencies.
- DFU and DfuSe extensions.
- STM32 family command differences.
- Intel HEX/memory maps.
- Driver distribution.
- Windows/Linux/macOS support.
- Maintenance and safety burden.
- Licensing.

Possible outcome:

- Keep CubeProgrammer provider permanently; or
- Add native provider behind `IDfuProgrammer`.

Do not commit to native DFU merely for architectural purity.

---

# Phase 7 — Future programming transports

Potential separate providers/workflows:

- ST-LINK/SWD recovery.
- UART STM32 ROM bootloader.
- CAN/I2C/SPI STM32 bootloader where justified.
- DroneCAN firmware.
- SD-card `.abin` update.
- BlueOS/network update.
- Companion-computer update.

Each requires its own safety and identity model.

---

# Explicitly deferred high-risk features

## Secure firmware

Requires dedicated architecture for:

- Signed bootloaders.
- Signed firmware.
- Public/private key lifecycle.
- Recovery keys.
- Secure downgrade policy.
- Audit logging.

## Option bytes and protection

Not part of normal firmware UX.

Includes:

- RDP.
- Write protection.
- Boot addresses.
- TrustZone/security configuration.
- OTP.

## Force flashing

Must remain a separately named expert operation with strong typed acknowledgement and recovery guidance.

## Legacy boards

- AVR/STK protocols.
- Old `.hex` workflows.
- VRBrain.
- Retired board policy.

---

# Long-term user experience

The firmware area should converge on four clear entry points:

```text
1. Browse / Download / Validate Firmware
2. Install Firmware Using ArduPilot Bootloader
3. Initial Install / DFU Recovery
4. Update Embedded Bootloader on Connected Vehicle
```

Each entry point must display:

- Preconditions.
- Supported file types.
- Communication transport.
- Hardware identity evidence.
- Risks.
- Progress.
- Verification result.
- Recovery/help.

## Definition of mature firmware subsystem

- Exact target selection is understandable.
- Download can be tested independently.
- Every destructive operation is preceded by validated evidence.
- Serial and DFU identities are handled correctly.
- Verification is mandatory.
- Diagnostics are copyable.
- Help is embedded and official links are current.
- Windows drivers can be diagnosed without guessing.
- Provider-specific complexity stays behind interfaces.
- The UI never implies that an MCU identity proves the flight-controller PCB.
- New providers can be added without destabilizing the serial/APJ workflow.

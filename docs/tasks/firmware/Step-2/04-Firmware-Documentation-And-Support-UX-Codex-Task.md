# Codex Task — Embedded Firmware Documentation and Support UX

## Objective

Add a documentation and support section directly to the MissionPlanner firmware experience.

The section should help the user answer:

- Which firmware target do I need?
- Which release channel should I choose?
- What is the difference between `.apj`, `.px4`, `.hex`, `_with_bl.hex` and `_bl.hex`?
- When should I use normal serial bootloader installation versus USB DFU?
- How do I put the controller into bootloader/DFU mode?
- What should Windows Device Manager show?
- Which tool/driver should I install?
- What is supported on this platform?
- Where can I download or build custom ArduPilot firmware?
- What evidence should I collect when an operation fails?

## UX decision

Do not reproduce the original Mission Planner frame gallery as the primary firmware interface.

Use a target-first information architecture:

```text
Vehicle family
→ hardware platform/board
→ release/version
→ file type/workflow
```

Optional vehicle-family icons can improve recognition, but the platform name and board identity must remain primary.

---

# Task 1 — Add a link catalogue

Status: Completed on 2026-08-04. `FirmwareSupportLinkProvider` centralizes the official ArduPilot and STMicroelectronics destinations plus a clearly labeled third-party Zadig fallback. Models reject missing text and non-HTTPS destinations, while `IExternalLinkLauncher` keeps host launching out of XAML and enforces HTTPS again at the boundary.

Create a UI-independent link model in the host/presentation layer, for example:

```csharp
FirmwareSupportLink
FirmwareSupportCategory
IFirmwareSupportLinkProvider
```

Do not scatter raw URLs across XAML and view models.

Initial official links:

## ArduPilot

- Firmware server: `https://firmware.ardupilot.org/`
- Pre-built binary/file-type documentation: `https://ardupilot.org/dev/docs/pre-built-binaries.html`
- Firmware manifest/GCS resources: `https://ardupilot.org/dev/docs/gcs-resources.html`
- Loading boards without existing ArduPilot firmware: `https://ardupilot.org/copter/docs/common-loading-firmware-onto-chibios-only-boards.html`
- Bootloader update: `https://ardupilot.org/copter/docs/common-bootloader-update.html`
- Bootloader technical documentation: `https://ardupilot.org/dev/docs/bootloader.html`
- Custom Firmware Builder: `https://custom.ardupilot.org/`
- Custom builder documentation: `https://ardupilot.org/dev/docs/custom-build-server.html`

## STMicroelectronics

- STM32CubeProgrammer product/download: `https://www.st.com/content/st_com/en/stm32cubeprogrammer.html`
- STM32CubeProgrammer current documentation: `https://dev.st.com/stm32cube-docs/prog/latest/en/index.html`
- STM32CubeProgrammer command-line documentation: `https://dev.st.com/stm32cube-docs/prog/latest/en/docs/markup/CubeProg_Command_Lines.html`

## Driver fallback

- ArduPilot’s DFU documentation includes a Zadig alternative.
- Zadig user guide: `https://github.com/pbatard/libwdi/wiki/Zadig`

ImpulseRC Driver Fixer may be mentioned as a third-party Betaflight-community fallback, but:

- Verify the current official vendor download page before embedding a link.
- Label it third-party.
- Do not present it before the ST driver bundled with STM32CubeProgrammer.
- Do not automate driver replacement.

## Opening links

Use a host abstraction such as:

```csharp
IExternalLinkLauncher
```

Validate URI scheme and restrict to HTTPS.

---

# Task 2 — Add firmware Help/Support sections

Status: Completed on 2026-08-04. The firmware page now exposes offline-first target, channel, file-type, serial-versus-DFU, boot-mode, Device Manager, driver-order, platform, and recovery guidance. Curated links launch through the HTTPS-only host abstraction, and a Windows-only host service opens Device Manager without adding platform behavior to the firmware core.

Add a Help or Documentation tab/expander/card group to the firmware page.

Recommended sections:

## Choosing the correct firmware

Explain:

- Vehicle family is not enough.
- The hardware platform must match the flight controller.
- Board ID/bootloader/USB evidence is used before erase.
- Frame geometry is configured later and normally does not choose the board firmware.

## Release channels

Explain:

- Stable: recommended for normal use.
- Beta: wider pre-release testing.
- Latest: development build for experienced users.
- Historical: diagnostic/downgrade use.
- Custom: user-selected feature set/provenance.

## Firmware file types

Explain:

- `.apj`: normal ArduPilot GCS-loadable package.
- `.px4`: older name using the same package family.
- `.hex`: Intel HEX for DFU/programming tools.
- `*_with_bl.hex`: application firmware plus ArduPilot bootloader, normally for initial DFU installation/recovery.
- `*_bl.hex` or bootloader-only image: advanced bootloader recovery/update, not normal application installation.

## Standard installation versus DFU

Standard serial/APJ:

- Existing ArduPilot-compatible bootloader.
- Device appears as serial/COM port.
- MissionPlanner uses ArduPilot/PX4 serial bootloader protocol.

USB DFU:

- Initial install or recovery.
- Board is placed into STM32 system bootloader mode.
- Windows normally shows `STM32 BOOTLOADER` under USB devices.
- Device usually does not appear as a COM port.
- Use `_with_bl.hex` for initial ArduPilot installation when appropriate.

## Entering bootloader/DFU mode

Provide general instructions without pretending all boards are identical:

- Consult board documentation.
- Hold BOOT/DFU button or bridge BOOT pads while connecting USB.
- Release after enumeration.
- Some boards use reset plus boot sequence.
- Do not confuse ArduPilot serial bootloader with STM32 ROM DFU.

## Windows Device Manager

Explain what to inspect:

- Normal application mode: COM/Ports or board-specific USB serial device.
- ArduPilot bootloader mode: may be a bootloader serial device/COM port.
- STM32 DFU mode: `STM32 BOOTLOADER` under Universal Serial Bus devices.
- Unknown device/yellow warning: driver problem.
- Device repeatedly appearing/disappearing: cable, power, boot-mode or driver issue.

Provide a button to launch Device Manager on Windows through a host service. Do not implement this in the firmware core library.

## Driver/tool guidance

Priority order:

1. Install/update STM32CubeProgrammer and its bundled DFU driver.
2. Reconnect the board and inspect Device Manager.
3. Use STM32CubeProgrammer USB refresh to confirm DFU device.
4. Use Zadig only as a documented fallback when the correct device is positively identified.
5. Mention ImpulseRC Driver Fixer only as optional third-party recovery guidance.

Add a strong warning:

> Replacing the driver for the wrong USB device can make that device unavailable to its normal software. Verify VID/PID and device name first.

## Platform limitations

Display current feature support:

| Capability | Windows | Linux | macOS/Mac Catalyst | Mobile |
|---|---:|---:|---:|---:|
| Catalogue/download/validate | Supported/planned cross-platform | Future validation | Future validation | Future |
| Serial APJ install | Windows first | Future | Future | Not initial scope |
| STM32CubeProgrammer DFU provider | Windows first | Later | Later | Not initial scope |
| Device Manager/driver diagnostics | Windows only | N/A | N/A | N/A |
| Connected MAVLink bootloader update | Depends on active transport | Later validation | Later validation | Later validation |

## Recovery

Explain:

- Keep power stable.
- Retry DFU detection before assuming a board is bricked.
- Use a data-capable USB cable.
- Remove hubs where possible.
- Verify exact target before flashing.
- Do not change option bytes from MissionPlanner’s initial DFU workflow.
- Secure or protected devices need specialized recovery guidance.

---

# Task 3 — Add context-sensitive help

Status: Completed on 2026-08-04. `FirmwareContextHelpResolver` separates actionable user guidance from technical errors and prioritizes board mismatch, DFU/driver/tool state, ambiguity, custom provenance, release risk, missing serial devices, and normal installation. The firmware page updates its guidance as channel, target, device, custom-package, and validation state changes; future DFU diagnostics can supply the already-defined DFU evidence flags.

Examples:

- If no serial device: show standard cable/driver/Device Manager help.
- If DFU device present: show DFU workflow and `_with_bl.hex` guidance.
- If manifest target is ambiguous: show hardware identification help.
- If package board mismatch: show both IDs and board-selection help.
- If CubeProgrammer missing: show installation link.
- If wrong driver: show primary ST driver instructions then fallback options.
- If latest/custom selected: show release-risk/provenance warning.

Keep user guidance separate from low-level exception text.

---

# Task 4 — Add documentation models and tests

Status: Completed on 2026-08-04. Offline sections now carry a typed `FirmwareSupportTopic`, with one complete section per topic. Policy tests enforce populated topics, unique HTTPS links, host-specific Device Manager availability, exact-target and `_with_bl.hex` DFU safeguards, the wrong-driver warning, and the target-first rule that frame imagery is not a firmware-selection dependency.

Tests should verify:

- Every support category has a title and content.
- Every embedded external link is HTTPS.
- No duplicate/malformed URI.
- Platform-specific actions are hidden elsewhere.
- DFU instructions mention `_with_bl.hex` and exact-target confirmation.
- Driver fallback warning is present.
- Frame images are not required for selection.

---

# Task 5 — Offline usability

Status: Completed on 2026-08-04. Essential target selection, release risk, file types, serial/DFU distinction, boot entry, Windows enumeration, driver priority and warning, platform limits, recovery, and evidence collection are embedded in the application. Curated external links supplement this content and are never required to understand the safe workflow.

Include concise embedded text so essential recovery instructions remain available without internet.

External links supplement the embedded content; they must not be the only instructions.

Optionally cache selected official documentation summaries with a version/date, but do not scrape or redistribute complete pages.

---

# Acceptance criteria

1. Firmware page contains a discoverable Help/Support area.
2. User can distinguish standard APJ install from DFU recovery.
3. File types are explained accurately.
4. User can open official ArduPilot firmware/custom-build/documentation pages.
5. User can open official STM32CubeProgrammer product/docs pages.
6. Windows Device Manager guidance is available.
7. Primary driver recommendation is STM32CubeProgrammer’s bundled driver.
8. Zadig/ImpulseRC are clearly labeled fallback/third-party options.
9. No frame-geometry gallery is required for firmware selection.
10. Help remains useful offline.
11. Accessibility and keyboard navigation work.
12. Tests validate links and content policy.

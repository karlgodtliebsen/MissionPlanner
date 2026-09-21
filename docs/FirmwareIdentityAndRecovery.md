# Firmware identity and recovery

## Independent observations

ArduPilot BoardId identifies a firmware or bootloader target. It is not automatically
an immutable physical-hardware identity. Wrong firmware can report a perfectly valid
BoardId for the wrong board; a wrongly installed bootloader can do the same.

| Identity | Evidence and source | Authority |
| --- | --- | --- |
| Physical device | USB VID/PID, USB serial/UID; independently observed MCU/DFU descriptors when available | Identifies the endpoint/chip family; generic USB product names do not prove an exact board |
| Bootloader | Protocol board ID, revision, flash capacity, optional chip/target data | Authoritative for compatibility with the currently installed bootloader; never copied from AUTOPILOT_VERSION |
| Running firmware | AUTOPILOT_VERSION board_version upper 16 bits, version, Git; STATUSTEXT target/UID banner | Claims of the current application, not physical-board proof |
| Selected firmware | Official exact platform/vehicle variant/source URI, or embedded APJ target/board/version/Git | Intended artifact identity; filenames and folders are provenance only |

Models live in MissionPlanner.Firmware. Core maps actual connection and MAVLink evidence;
the UI displays the observations and requests explicit actions. No additional USB stack
or uploader was introduced.

## APJ metadata

APJ structure, decompression size and existing artifact hash/signature checks remain in place.
Embedded target, board ID, version, Git and vehicle metadata are authoritative over file/folder names.
A misleading filename produces a warning; it never changes the package identity.

Common official APJs use version=0.1 for the APJ container. This is not an ArduCopter release.
The reader obtains release version and broad vehicle family from the bounded, fixed
AP_FWVersion header in the decoded application image when available. It does not dereference
firmware pointers. Unsupported or absent fields remain unknown.

The AP_FWVersion ArduCopter family does not distinguish multicopter from helicopter.
An official selected catalogue variant supplies that distinction, with its exact artifact URI.
Local packages without explicit variant evidence receive IdentityInsufficient for target
recovery rather than guessing from a filename. APJs with sufficient embedded target,
vehicle/variant and release metadata use the same verified recovery workflow; they are
never fabricated into catalogue releases.

## Authority by operation mode

NormalUpgrade never automatically switches into Recovery. A known running target/board
mismatch blocks normal installation, even when the bootloader is compatible. Exact target,
vehicle variant and embedded-versus-selected metadata are compared; sharing a BoardId is
not enough to merge variants. Conflicting startup targets produce Ambiguous.

Recovery is an explicit action offered for a running-firmware mismatch. It queries the
existing bootloader and evaluates independent evidence:

- Running 134, selected 1002, bootloader 1002: CompatibleForRecovery, explicit confirmation required.
- Running 134, selected 1002, bootloader 134: BootloaderMismatch; blocked.
- Bootloader missing: IdentityInsufficient. Querying it is allowed; erase is not.
- Known incompatible MCU, embedded target or vehicle variant: blocked.
- Unknown exact chip fields are not invented from broad descriptions such as STM32F40x.

The structured result includes status, mode, summary, evidence, CanProceed and
RequiresExplicitConfirmation. The UI can start a bootloader query with insufficient
preflight evidence; the installer must re-evaluate the protocol identity before erase.

## Operator workflow

1. Select a physical controller and a validated official or local APJ.
2. Inspect Physical device, Bootloader, Running firmware and Selected firmware separately.
3. When normal compatibility reports a running-target mismatch, explicitly choose
   **Recovery / Change Firmware Target**.
4. Run installation. The selected disarmed serial connection is handed off using the existing
   cancellation/recording-drain path; the existing temporary MAVLink adapter enters the bootloader.
5. The uploader queries bootloader identity and retains all existing board, size, revision,
   secure-boot and signed-image checks. A conflicting bootloader cannot be overridden.
6. Review attributed evidence and type **RECOVER <exact target>** before erase.
7. Existing erase/program/checksum/reboot services run. Cancellation remains deferred across
   destructive stages; transition cancellation and declined confirmation never erase.
8. Reconnect to the matched controller and re-read AUTOPILOT_VERSION and a fresh startup
   target. Version, family/variant, target, original UID and available Git identity must match
   before reporting completion.

Recovery intent resets when the reviewed controller or artifact changes. Other connections
are never displaced during reconnect. Browser/WASM shares preparation and source-labelled
presentation; direct serial installation remains gated by platform capabilities.

There is **no generic force-flash path**. If the installed bootloader itself has the wrong
target, this workflow stops. STM32 DFU/bootloader repair remains a separate explicit recovery
operation with its existing physical-target review; it is not invoked automatically.

## Diagnostics and verification

User-visible board IDs name their source: Running firmware board ID [AutopilotVersion]
and Bootloader board ID [BootloaderProtocol]. Missing values are omitted instead of rendered
as misleading zeroes. Copyable reports contain operation mode and the original attributed
running/selected/bootloader evidence, including failure stages. Local source provenance is
preserved through post-flash verification.

Regression suites cover the real speedybeef4/134 versus omnibusf4/1002 case, matching and
conflicting bootloaders, confirmation and transition cancellation, local APJ provenance,
unknown/ambiguous identity, MCU family descriptions, exact variants, startup text,
UI recovery visibility/invalidation, fresh post-flash target verification and COM changes.

See [execution results](tasks/firmware-identity/EXECUTION_RESULTS.md) for builds, test totals
and the separately authorized COM12 bootloader inspection.

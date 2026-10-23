# ArduPilot normal bootloader upgrade — execution results

Status: implementation and first-controller hardware verification completed; remaining physical controllers pending.
No commits or staging performed.

## Behavior

Catalogue APJ installs use the existing ArduPilot/PX4 bootloader uploader. A connected,
disarmed serial controller can hand off its connection without manual disconnect or DFU.
The handoff cancels dependent operations, drains recording through the existing disconnect
path, and records FirmwareUpgradeReboot as the expected disconnect reason. The existing
temporary MAVLink adapter then sends the bootloader reboot command using a fresh parser.

The selected family, reported board ID and available board UID banner are checked before
handoff. Protocol bootloader identity and package compatibility remain mandatory before
erase. Automatic normal upgrades do not silently fall back to manual DFU.

After upload/checksum verification/reboot, discovery matches the original hardware identity,
including same-port return. Reconnect retries the matched endpoint for up to 30 seconds
because USB enumeration can precede MAVLink startup. It never replaces another connection.
Success requires a fresh reported release version, family, available board ID, original
hardware UID and available Git identity matching the selected release. A reconnect timeout
is a verification failure; caller cancellation remains cancellation.

Catalogue installs carry ExpectedRelease. Legacy/custom APJ callers without selected release
metadata retain their existing checksum verification and reconnect-suggestion behavior;
they cannot claim the new verified running-release result.

## Physical evidence — 2026-09-21

The operator confirmed propellers removed and selected COM11. Tests used a temporary .NET
console harness composing the actual Windows application services and invoking the production
preparation and installation services. This validates the service workflow, not visual UI interaction.

| Controller USB serial | Original port/version | Selected release | Bootloader | Result |
| --- | --- | --- | --- | --- |
| 5B002E000951353332343134 | COM11 / ArduCopter 4.7.0 Official, 1511f271 | omnibusf4 Copter 4.7.1, dbe79216 | COM11, board 1002, revision 5 | 873,508 bytes programmed; checksum verified; fresh read confirmed 4.7.1. Initial automatic reconnect failed before retry correction. |
| Same controller, repeat acceptance | COM11 / 4.7.1 | Same official APJ | COM11, board 1002, revision 5 | Completed, automatic reconnect COM11, 4.7.1 Official and original UID verified; 23.31 seconds. No manual DFU. |
| 280043001751353232343431 | COM14 / 4.7.1 Official | None flashed | Not probed destructively | Read-only identity check. Operator chose an older-firmware controller instead of reinstalling this one. |
| Remaining physical controllers | Awaiting operator connection/port | 4.7.1 intended | Pending | Not executed. Three distinct successful upgrades are not yet established. |

The first hardware attempt stopped before erase because the target check incorrectly
classified the ChibiOS Git banner as a board banner. The check now recognizes the board
UID banner shape (platform followed by three eight-digit hex words). Regression tests
include both ChibiOS and actual/wrong board banners.

The first upload then exposed early reconnect failure. The controller was independently
read back as 4.7.1, and a repeat install with bounded reconnect retries completed automatically.
This distinction is retained rather than reporting the original run as a complete success.

Completed operation ID: 34b1e43f-e15b-4b09-bbef-e7e7f6ee68d1.
Evidence: [HARDWARE_COM11.md](HARDWARE_COM11.md) (progress condensed; packet/programming repetition excluded).

## Verification

- Firmware suite before latest reboot-failure test: 324 passed.
- New Core handoff/reconnect tests: 6 passed.
- Earlier targeted Core connection/firmware suite: 50 passed, 1 skipped.
- Earlier targeted UI firmware/DFU suite: 71 passed.
- Full solution build before final regression additions: zero errors, 53 warnings;
  no CS1591/CS1587/CS1573 documentation warnings.
- Complete repository test run: 1,291 .NET tests passed, 30 skipped; 7 browser tests passed. No failures. Results: TestResults/all-tests/20260921-020905-910.
- Final incremental solution build: zero errors and zero warnings.

Coverage includes same/different COM return, unrelated device rejection, timeout/cancellation,
wrong board before erase, programming/verification/reboot failures, exact post-flash version
and UID/Git mismatches, guarded handoff and startup reconnect retries.

## Reference and boundaries

Reused the repository uploader; no second protocol implementation was introduced.
The legacy src-v.1.38 source tree was absent in this checkout. Reference inspection used
upstream MissionPlanner ExtLibs/px4uploader/Uploader.cs and the local ArduPilot archive
Tools/scripts/uploader.py. ArduPilot GCS_Common.cpp confirms board_version upper 16 bits
contain APJ_BOARD_ID.

Upstream reference:
https://github.com/ArduPilot/MissionPlanner/blob/master/ExtLibs/px4uploader/Uploader.cs

Physical changed-COM behavior, the remaining two distinct controllers, and visual desktop
acceptance remain unverified. Fake-based tests cover changed-COM discovery.

## Second-controller diagnosis

The next attached controller was COM12, USB serial 52003D001651353232323938.
It reported ArduCopter 4.7.1 Official (dbe79216), platform speedybeef4, board ID 134,
and Config Error: INS: unable to initialise driver. The operator confirmed it is
physically an OmnibusF4 with incorrect firmware. It was left unchanged during diagnosis;
this is an explicit wrong-target recovery case, not a normal 4.7.0-to-4.7.1 upgrade.
The operator asked to preserve COM12 unchanged for the next task set. No recovery, reboot, erase or flash was executed on COM12. The three-controller acceptance requirement remains incomplete; only COM11 completed the normal bootloader workflow.

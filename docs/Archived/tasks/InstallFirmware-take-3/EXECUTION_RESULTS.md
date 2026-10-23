# Install Firmware take 3 — execution results

Implemented on `feature/add-DFU-bootload`, 2026-09-10. Tasks were handled in the
requested order, followed by integrated regression coverage and cleanup. Changes
are left uncommitted for review. The original task specification is unchanged.

## Task outcomes

| Task | Result |
| --- | --- |
| 01 — USB identification | USB VID/PID and product aliases are hints only. Shared 4617/22337 targets cannot auto-select; protocol board ID drives exact matching. |
| 02 — Workflow state | Immutable context and independent preparation, discovery, entry and installation capabilities cover A–G. |
| 03 — Ownership | Normal sessions publish their transport and owned serial port. Probing, entry and installation block that resource; unrelated TCP/UDP and other COM ports remain usable. Landing is informational. |
| 04 — Plan | One immutable plan resolves artifact format, transport, boot entry, evidence, compatibility, blocking reason and expected runtime above the existing installers. |
| 05 — Artifacts | Local APJ uses bounded parse/hash/atomic-cache import. Both sources share an artifact summary while validity and target compatibility remain separate. Clear preserves physical selection. |
| 06 — Policy | Serial uses APJ; normal DFU selection uses combined WITH_BL HEX. Local-only mismatch UI is removed and both APJ sources use strict compatibility. DFU requires exact-platform safety review. |
| 07 — Runtime/entry | Bounded MSP, isolated MAVLink and AP bootloader probes provide runtime evidence. AP entry, Betaflight-to-DFU entry and manual BOOT/RESET are explicit, non-flashing actions. Armed AP heartbeat prevents reboot. |
| 08 — Page | Information / Firmware / Help & Support remain the only top-level tabs. Firmware has one toolbar, physical target, common artifact and validation/compatibility presentation. Catalogue stays an overlay. |
| 09 — Progress/results | Page-owned progress sequences with confirmations. Completed, Failed and Cancelled are distinct. Manual waits have deadlines, and destructive-stage cancellation remains deferred. |
| 10 — Coverage/cleanup | A–G plan/ViewModel scenarios, resource ownership, shared USB, local/online mismatch, invalid APJ, generic HEX rejection, target review invalidation and mocked conversion/verification tests added or updated. Obsolete nested-section state and commented mismatch XAML removed. |

The AP protocol programmer and STM32CubeProgrammer provider remain intact. The
existing installation services still recheck hardware and safety before destructive
work. Typed DFU target review is bound to both prepared artifact and selected endpoint;
changing either invalidates it. An ordinary probe deadline retains completed device
evidence instead of discarding the entire discovery snapshot.

## Verification

Executed from the repository root:

```powershell
dotnet test src/Tests/MissionPlanner.Firmware.Tests/MissionPlanner.Firmware.Tests.csproj --no-restore
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore
dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter 'FullyQualifiedName~TemporaryMavLinkBootloaderGatewayTests|FullyQualifiedName~VehicleConnection'
dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --configuration Release --no-restore -p:Platform=x64
git diff --check
```

- Firmware: **276 passed, 1 skipped**, 0 failed. The skipped test is the explicit hardware smoke-test placeholder.
- Avalonia UI: **102 passed**, 0 failed.
- Targeted Core connection/runtime: **14 passed**, 0 failed.
- Windows desktop Release/x64 build: **succeeded**, 0 errors. Latest build reported 14 existing warnings in unrelated simulation, replay, signing and other UI files. No new CS1591/CS1587 warnings were observed.
- Whitespace check: passed.

Avalonia build telemetry was disabled for these commands with the process-local
`AVALONIA_TELEMETRY_OPTOUT=1` because its telemetry log lies outside the workspace
sandbox. No compiler warnings or errors were suppressed. Validation logs remain in
ignored repository-root `firmware-*.log` files.

## Documentation

Updated `docs/FIRMWARE.md`, `docs/INSTALL_FIRMWARE_VIEWMODELS.md` and
`docs/FEATURES.md`, plus stale in-product mismatch/provenance guidance. The ownership
document includes the A–G matrix, source semantics, plan gates and cancellation model.

## Not verified on physical hardware

No controller was flashed or rebooted. F4/H7 programming, USB driver behavior,
physical Betaflight mappings, real runtime rediscovery and visual desktop acceptance
remain hardware/manual checks. Conversion tests use a fixture mapping, mocked provider
and protocol verifier; they do not constitute physical board qualification. Browser,
mobile and the entire solution test suite were not run; the affected suites and the
Windows desktop CI build target were verified.

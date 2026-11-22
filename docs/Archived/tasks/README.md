# Archived task reconciliation

Reconciled on **22 November 2026**. The [unified task review](../../tasks/README.md) is
the only active backlog. This file records where its requests came from and why many old
tasks were not carried forward as fresh implementation work. It is a provenance ledger,
not a second task list.

The reconciliation covers **296 Markdown/text files in the pre-existing task archive**
(45 top-level task folders plus two root documents), **six former current task notes**, and
the **root INAV idea file**: **303 source documents**. Supporting images, scripts and bundled
archives remain alongside their task packs. The feature/gap reviews and relevant subsystem
guides were also used. Task packs are reconciled as a group; this does not assert that every
historical acceptance statement has been independently retested.

The seven former current notes were moved without content changes into
[backlog-sources-2026-11-22/](backlog-sources-2026-11-22/). Keep their original wording,
dates and completion annotations as evidence. Historical paths, code names and status
statements inside them may be stale; use the current guides and the mapping below.

## Former current notes

| Preserved source | Reconciliation |
|---|---|
| [Postponed work](backlog-sources-2026-11-22/ComingTasks/postponed-2026-10-22.md) | Platform heads already exist. Platform qualification, optional native services, mobile scope and engineering deferrals are [MP-001](../../tasks/README.md#mp-001), [MP-002](../../tasks/README.md#mp-002), [MP-003](../../tasks/README.md#mp-003), [MP-004](../../tasks/README.md#mp-004), [MP-005](../../tasks/README.md#mp-005), [MP-006](../../tasks/README.md#mp-006), [MP-007](../../tasks/README.md#mp-007), [MP-008](../../tasks/README.md#mp-008), [MP-009](../../tasks/README.md#mp-009), [MP-017](../../tasks/README.md#mp-017). |
| [Incomplete task notes](backlog-sources-2026-11-22/ComingTasks/incomplete-tasks.txt) | Preserve Done annotations. Remaining performance/comparison, per-port evidence, logging and hardware checks are [MP-014](../../tasks/README.md#mp-014), [MP-015](../../tasks/README.md#mp-015), [MP-016](../../tasks/README.md#mp-016), [MP-051](../../tasks/README.md#mp-051), [MP-052](../../tasks/README.md#mp-052), [MP-053](../../tasks/README.md#mp-053), [MP-056](../../tasks/README.md#mp-056), [MP-057](../../tasks/README.md#mp-057). PX4Flow and compass ID decoding are implemented. |
| [Deferred firmware](backlog-sources-2026-11-22/firmware/Deferred%20feature%20backlog.md) | Windows DFU and controlled handoff are implemented. Distinct qualification, legacy/security and extra-transport choices are [MP-017](../../tasks/README.md#mp-017), [MP-018](../../tasks/README.md#mp-018), [MP-020](../../tasks/README.md#mp-020), [MP-021](../../tasks/README.md#mp-021), [MP-022](../../tasks/README.md#mp-022), [MP-023](../../tasks/README.md#mp-023), [MP-024](../../tasks/README.md#mp-024), [MP-025](../../tasks/README.md#mp-025), [MP-026](../../tasks/README.md#mp-026), [MP-027](../../tasks/README.md#mp-027). |
| [October task notes](backlog-sources-2026-11-22/20261015/Mission%20Planner%20Next%20gen%20new%20tasks.txt) | Theme/layout/profile requests largely superseded. Compare sources, qualification and broader vehicle intent remain [MP-015](../../tasks/README.md#mp-015), [MP-056](../../tasks/README.md#mp-056), [MP-063](../../tasks/README.md#mp-063), [MP-069](../../tasks/README.md#mp-069). |
| [Missing functionality](backlog-sources-2026-11-22/Missing%20functionality.txt) | Preferences, browser head, simulation, profiles, motor diagnostics and initial FFT have implementations. Tutorial, acceptance and future automation are [MP-054](../../tasks/README.md#mp-054), [MP-060](../../tasks/README.md#mp-060), [MP-061](../../tasks/README.md#mp-061), [MP-062](../../tasks/README.md#mp-062), [MP-063](../../tasks/README.md#mp-063), [MP-064](../../tasks/README.md#mp-064), [MP-065](../../tasks/README.md#mp-065), [MP-066](../../tasks/README.md#mp-066), [MP-067](../../tasks/README.md#mp-067). |
| [General thoughts](backlog-sources-2026-11-22/Some%20MissionPlanner%20thoughts.txt) | Existing FFT, replay and simulation foundations are not new tasks. Real-log validation, advice, TestLab content, DSLs and optional AI are [MP-054](../../tasks/README.md#mp-054), [MP-055](../../tasks/README.md#mp-055), [MP-062](../../tasks/README.md#mp-062), [MP-063](../../tasks/README.md#mp-063), [MP-064](../../tasks/README.md#mp-064), [MP-065](../../tasks/README.md#mp-065), [MP-066](../../tasks/README.md#mp-066). |
| [INAV exploration](backlog-sources-2026-11-22/Thoughts-INAV-MissionPlanner-future-implementation.txt) | Feasibility decision [MP-029](../../tasks/README.md#mp-029); version/build claims in the dated note must be reverified before execution. |

## Archived task packs

“No separate task” means that the request is implemented or superseded according to the
linked evidence, not that all conceivable future improvements are complete. A qualification
ID preserves outstanding acceptance without duplicating the original implementation plan.

| Source pack | Reconciliation and current evidence | Unified follow-up |
|---|---|---|
| [20261015](20261015/) | Theme work implemented; do not recreate Mission Orange. [Current palettes](../../THEME_PALETTES.md) | [MP-069](../../tasks/README.md#mp-069) |
| [ardupilot-bootloader-upgrade](ardupilot-bootloader-upgrade/) | Controlled handoff and upgrade implemented, with board-specific successful and failed physical reports. Reconcile those results, rather than declaring all hardware acceptance complete. [Firmware](../../FIRMWARE.md) | [MP-018](../../tasks/README.md#mp-018) |
| [ArduPilot-Runtime](ArduPilot-Runtime/) | Runtime probing, identity and installation handoff superseded by current firmware workflows. [Identity/recovery](../../FirmwareIdentityAndRecovery.md) | [MP-018](../../tasks/README.md#mp-018), [MP-022](../../tasks/README.md#mp-022) |
| [Arming](Arming/) | Semantic configuration implemented. [Arming](../../ARMING.md) | [MP-056](../../tasks/README.md#mp-056) |
| [arming-page](arming-page/) | Arming page, pending changes, diagnostic authority and guarded actions implemented. [Arming](../../ARMING.md) | [MP-056](../../tasks/README.md#mp-056) |
| [BetaFlight-DFU](BetaFlight-DFU/) | MSP identity and ArduPilot conversion implemented; native Betaflight flashing was explicitly deferred. Combined and split task files describe the same work. [Conversion guide](../../BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md) | [MP-018](../../tasks/README.md#mp-018), [MP-028](../../tasks/README.md#mp-028) |
| [Bootloader](Bootloader/) | Automatic controlled reboot/handoff implemented; not the same as an undefined Force Bootloader feature. [Firmware](../../FIRMWARE.md) | [MP-018](../../tasks/README.md#mp-018), [MP-022](../../tasks/README.md#mp-022) |
| [compass-button-ux](compass-button-ux/) | Layout and action conventions incorporated into the setup pattern. [Setup UX](../../SetupUxPattern.md) | [MP-056](../../tasks/README.md#mp-056), [MP-069](../../tasks/README.md#mp-069) |
| [compass-live-markdown](compass-live-markdown/) | Semantic compass documents and safe live Markdown implemented. [Documents](../../MarkdownDocuments.md) | [MP-056](../../tasks/README.md#mp-056), [MP-069](../../tasks/README.md#mp-069) |
| [Compass-UX](Compass-UX/) | Compass configuration, pending changes and diagnostics implemented. [Compass diagnostics](../../COMPASS_DIAGNOSTICS.md) | [MP-056](../../tasks/README.md#mp-056) |
| [config](config/) | Configuration work superseded by the later implementation ledger and current profile/preferences guides. [Ledger](../../review-20261023/IMPLEMENTATION.md) | [MP-014](../../tasks/README.md#mp-014), [MP-015](../../tasks/README.md#mp-015), [MP-057](../../tasks/README.md#mp-057) |
| [Connection](Connection/) | Controller firmware update checking and continuous connection monitoring implemented. Generic lost-link reconnect remains separate. [Connection guide](../../VEHICLE_CONNECTION.md) | [MP-011](../../tasks/README.md#mp-011), [MP-012](../../tasks/README.md#mp-012), [MP-070](../../tasks/README.md#mp-070) |
| [DI-Cleanup](DI-Cleanup/) | Keyed Logs registrations and explicit factory/lifetime patterns are present; use current composition/lifecycle guidance instead of rerunning the old migration. [Composition root](../../../src/UI/MissionPlanner.App/Configuration/ApplicationConfigurator.cs); [Lifecycle](../../UI_VIEW_LIFECYCLE_AND_NAVIGATION.md) | No separate task |
| [fft](fft/) | Initial analysis domain, imports, numerical tests and application integration implemented. Controller acquisition, broader log review and real-log validation are separate follow-ups. [FFT design](../../FftAnalysisDesign.md) | [MP-051](../../tasks/README.md#mp-051), [MP-052](../../tasks/README.md#mp-052), [MP-054](../../tasks/README.md#mp-054), [MP-055](../../tasks/README.md#mp-055) |
| [firmware](firmware/) | Early roadmap/Step-2 plans are superseded by the current install plan, Windows DFU and identity/recovery guides. Retain download acceptance and distinct platform/transport research. [Firmware](../../FIRMWARE.md) | [MP-017](../../tasks/README.md#mp-017), [MP-018](../../tasks/README.md#mp-018), [MP-019](../../tasks/README.md#mp-019), [MP-020](../../tasks/README.md#mp-020), [MP-021](../../tasks/README.md#mp-021), [MP-022](../../tasks/README.md#mp-022), [MP-023](../../tasks/README.md#mp-023), [MP-024](../../tasks/README.md#mp-024), [MP-025](../../tasks/README.md#mp-025), [MP-026](../../tasks/README.md#mp-026), [MP-027](../../tasks/README.md#mp-027) |
| [firmware-identity](firmware-identity/) | Independent identity evidence and explicit recovery implemented. Historical failure evidence is retained. [Identity/recovery](../../FirmwareIdentityAndRecovery.md) | [MP-018](../../tasks/README.md#mp-018) |
| [firmware-installation](firmware-installation/) | Old custom board-ID override conflicts with current strict mismatch blocking and is superseded. ViewModel redesign is superseded by take-3; grid-performance request needs measurement. [Current firmware policy](../../FIRMWARE.md) | [MP-014](../../tasks/README.md#mp-014) |
| [flight-data](flight-data/) | Both task waves implemented substantial tabs/infrastructure. Readiness now replaces the old Preflight route; remaining capability/qualification limits are tracked separately. [Flight Data](../../FLIGHT_DATA.md); [Readiness](../../READINESS_AND_DIAGNOSTIC_NAVIGATION.md) | [MP-030](../../tasks/README.md#mp-030), [MP-031](../../tasks/README.md#mp-031), [MP-032](../../tasks/README.md#mp-032), [MP-035](../../tasks/README.md#mp-035), [MP-036](../../tasks/README.md#mp-036), [MP-038](../../tasks/README.md#mp-038), [MP-045](../../tasks/README.md#mp-045), [MP-058](../../tasks/README.md#mp-058), [MP-073](../../tasks/README.md#mp-073) |
| [full-parameters](full-parameters/) | Write preview, progress/retry, profile CRUD and live/pending comparison implemented. Independent file/profile comparison is not the same feature. [Parameters](../../PARAMETERS.md); [Profiles](../../PARAMETER_PROFILES.md) | [MP-014](../../tasks/README.md#mp-014), [MP-015](../../tasks/README.md#mp-015) |
| [icon](icon/) | Normalization completed: the existing catalogue records 300 reviewed buttons and separate custom-artwork candidates. [Icon catalogue](../AXAML_BUTTON_INVENTORY.md) | [MP-068](../../tasks/README.md#mp-068) |
| [InstallFirmware-take-3](InstallFirmware-take-3/) | Latest installation-plan workflow supersedes prior navigation proposals. Execution report retained; qualification is still board/platform-specific. [Firmware](../../FIRMWARE.md) | [MP-017](../../tasks/README.md#mp-017), [MP-018](../../tasks/README.md#mp-018), [MP-019](../../tasks/README.md#mp-019) |
| [InstallFirmware-take2](InstallFirmware-take2/) | Intermediate firmware navigation/DFU workflow superseded by take-3; do not restore the old tab layout. [Firmware](../../FIRMWARE.md) | [MP-018](../../tasks/README.md#mp-018), [MP-019](../../tasks/README.md#mp-019) |
| [Logging](Logging/) | Tlog recording, application/telemetry viewers, retention and replay infrastructure implemented. General log graphs and controller download remain separate. [Logging](../../Logging.md); [Replay](../../BENCH_TELEMETRY_REPLAY.md) | [MP-051](../../tasks/README.md#mp-051), [MP-052](../../tasks/README.md#mp-052), [MP-053](../../tasks/README.md#mp-053) |
| [MandatoryHardware-tasks](MandatoryHardware-tasks/) | Implementation and later semantic setup work supersede the initial completion pack. Remaining acceptance is hardware-specific. [Calibration](../../CALIBRATION.md); [Ledger](../../review-20261023/IMPLEMENTATION.md) | [MP-056](../../tasks/README.md#mp-056) |
| [maps](maps/) | Catalog, raster runtime, credentials, attribution, cache/prefetch and MBTiles implemented. Conditional vector tasks remain deferred by ADR; rendering/export/provider and interactive residuals remain. [Maps](../../MAPS.md) | [MP-046](../../tasks/README.md#mp-046), [MP-047](../../tasks/README.md#mp-047), [MP-048](../../tasks/README.md#mp-048), [MP-049](../../tasks/README.md#mp-049), [MP-050](../../tasks/README.md#mp-050) |
| [MAVFTP](MAVFTP/) | Filesystem transfer services and retry/correlation behavior are documented as implemented; retain live acceptance rather than repeating the two historical implementation plans. [MAVFTP](../../MAVFTP.md) | [MP-045](../../tasks/README.md#mp-045) |
| [mavlink](mavlink/) | Unused Asv implementation removal superseded by owned MAVLink architecture. [Accepted ADR](../../adr/0001-own-mavlink-implementation.md) | No separate task |
| [mavlink-codex-tasks](mavlink-codex-tasks/) | Generated wire coverage, promotion catalog, conformance tests and the local verification script exist. The current workflows do not invoke the script; the previously documented mavlink.yml is absent. [MAVLink](../../MAVLINK.md); [Promotion policy](../../MAVLINK_DOMAIN_PROMOTION.md) | [MP-074](../../tasks/README.md#mp-074) |
| [mission-map-menu](mission-map-menu/) | Menu foundation and feature slices implemented, including import, fence/rally, generators, terrain graph, POI and coordinate tools. Combined root file duplicates this pack. Retain editor limitations and qualification. [Missions](../../MISSIONS.md) | [MP-033](../../tasks/README.md#mp-033), [MP-034](../../tasks/README.md#mp-034), [MP-039](../../tasks/README.md#mp-039), [MP-040](../../tasks/README.md#mp-040), [MP-041](../../tasks/README.md#mp-041), [MP-042](../../tasks/README.md#mp-042), [MP-043](../../tasks/README.md#mp-043), [MP-044](../../tasks/README.md#mp-044), [MP-045](../../tasks/README.md#mp-045) |
| [MissionPlanner_Actions_Parity_Codex_Tasks](MissionPlanner_Actions_Parity_Codex_Tasks/) | Wave-1 deferral files are superseded by wave 2 for mission intervention and in-flight adjustments. [Current actions parity](../../flightdata-actions-parity.md) | [MP-032](../../tasks/README.md#mp-032), [MP-045](../../tasks/README.md#mp-045) |
| [MissionPlanner_Actions_Parity_Wave2_Codex_Tasks](MissionPlanner_Actions_Parity_Wave2_Codex_Tasks/) | Set-current-WP, restart/resume, abort landing, display-altitude reset and in-flight adjustments implemented; real SITL matrix remains distinct. [Current actions parity](../../flightdata-actions-parity.md) | [MP-045](../../tasks/README.md#mp-045) |
| [MissionPlanner_NextGen_Bench_Improvement_Docs](MissionPlanner_NextGen_Bench_Improvement_Docs/) | Recorded eight implementation tasks complete; retain physical/UI verification and related follow-ups. [Recorded status](MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md) | [MP-016](../../tasks/README.md#mp-016), [MP-056](../../tasks/README.md#mp-056), [MP-069](../../tasks/README.md#mp-069) |
| [OptionalHardware-Tasks-20260818](OptionalHardware-Tasks-20260818/) | Initial scope matrix superseded by the 24 October ledger, including PX4Flow, joystick, CAN, serial tools and other optional workflows. [Optional Hardware](../../OPTIONAL_HARDWARE.md); [Ledger](../../review-20261023/IMPLEMENTATION.md) | [MP-057](../../tasks/README.md#mp-057), [MP-059](../../tasks/README.md#mp-059) |
| [radio-calibration](radio-calibration/) | Channel meters, trim/review flow and layout implemented; bench qualification is separate. [Calibration](../../CALIBRATION.md) | [MP-056](../../tasks/README.md#mp-056) |
| [receiver-bind-and-follow-up](receiver-bind-and-follow-up/) | Receiver binding, embedded-bootloader guard and raw-capture integrity changes implemented. [Binding](../../RECEIVER_BIND.md); [Recording](../../TELEMETRY_RECORDING.md) | [MP-018](../../tasks/README.md#mp-018), [MP-056](../../tasks/README.md#mp-056) |
| [SerialPorts](SerialPorts/) | Parameter-based serial setup and conflict warnings implemented; verified per-port RC telemetry remains a separate question. [Serial Ports](../../SERIAL_PORTS.md) | [MP-016](../../tasks/README.md#mp-016) |
| [Servo_Output_Raw](Servo_Output_Raw/) | Typed decoding and output-state propagation implemented; do not recreate the decoder. [Output diagnostics](../../MOTOR_OUTPUT_DIAGNOSTICS.md) | [MP-056](../../tasks/README.md#mp-056) |
| [ServoOutput-MotorTest](ServoOutput-MotorTest/) | Motor/output mapping and start-threshold workflow implemented; physical acceptance remains. [Outputs](../../MOTOR_OUTPUT_DIAGNOSTICS.md); [Thresholds](../../MOTOR_START_THRESHOLDS.md) | [MP-056](../../tasks/README.md#mp-056) |
| [setup](setup/) | Older setup tasks superseded by semantic services, Setup UX and the later implementation ledger. [Setup UX](../../SetupUxPattern.md); [Ledger](../../review-20261023/IMPLEMENTATION.md) | [MP-056](../../tasks/README.md#mp-056), [MP-057](../../tasks/README.md#mp-057) |
| [Setup-Advanced](Setup-Advanced/) | 47-group request reconciled by later ledger: 44 implemented groups and three explicit scope decisions; no blanket new implementation task. [Ledger](../../review-20261023/IMPLEMENTATION.md) | [MP-057](../../tasks/README.md#mp-057), [MP-059](../../tasks/README.md#mp-059), [MP-072](../../tasks/README.md#mp-072) |
| [simulation](simulation/) | Tasks 01–07 implemented: profiles, acquisition, controls, scenarios, fleets, isolated replay and fake-runtime coverage. Real binaries, a maintained feed and richer scenario content remain distinct. [Simulation](../../SIMULATION.md) | [MP-060](../../tasks/README.md#mp-060), [MP-061](../../tasks/README.md#mp-061), [MP-062](../../tasks/README.md#mp-062) |
| [telemetry](telemetry/) | Shared telemetry/Inspector infrastructure implemented; current readiness and lifecycle guides supersede old UI placement instructions. [Inspector](../../LiveTelemetryInspector.md); [Readiness](../../READINESS_AND_DIAGNOSTIC_NAVIGATION.md) | [MP-056](../../tasks/README.md#mp-056), [MP-069](../../tasks/README.md#mp-069) |
| [telemetry-arming](telemetry-arming/) | Typed decoders, output propagation, log finalization and richer arming diagnostics implemented. [Arming](../../ARMING.md); [Recording](../../TELEMETRY_RECORDING.md) | [MP-056](../../tasks/README.md#mp-056) |
| [Theming-Codex-Tasks](Theming-Codex-Tasks/) | Theme composition, palette and switching work implemented. Archived verification documents retain manual/platform checks, not a new theme rewrite. [Theming](../../Theming.md) | [MP-069](../../tasks/README.md#mp-069) |
| [vehicle](vehicle/) | Vehicle display-name and firmware identity work implemented. Naming-specific reconnect does not resolve the general lost-link request. [Connection guide](../../VEHICLE_CONNECTION.md); [Identity](../../FirmwareIdentityAndRecovery.md) | [MP-011](../../tasks/README.md#mp-011), [MP-018](../../tasks/README.md#mp-018) |
| [Combined map-menu plan](MISSION_MAP_MENU_CODEX_TASKS.md) | Duplicate of the split mission-map-menu pack; current [mission guide](../../MISSIONS.md) records implementation and limitations. | [MP-033](../../tasks/README.md#mp-033), [MP-034](../../tasks/README.md#mp-034), [MP-039](../../tasks/README.md#mp-039), [MP-040](../../tasks/README.md#mp-040), [MP-041](../../tasks/README.md#mp-041), [MP-042](../../tasks/README.md#mp-042), [MP-043](../../tasks/README.md#mp-043), [MP-044](../../tasks/README.md#mp-044), [MP-045](../../tasks/README.md#mp-045) |
| [Introduction evaluation](MissionPlanner-Introduction-Evaluation-20260816.md) | Dated content/asset/layout audit; findings need rechecking against today's Introduction and Tutorial. | [MP-067](../../tasks/README.md#mp-067) |

## Completed or superseded requests

These are removed from the active implementation queue on the stated evidence. They may
still appear as “missing”, “deferred” or “not started” in an older source.

| Older request | Reconciled status |
|---|---|
| Build Linux/macOS heads; create a web app | Heads and browser build exist. [Platform guide](../../PLATFORMS.md) defines current limits; runtime/hardware qualification remains. |
| Complete Simulator | Profiles, runtime management, controls, scenarios, fleets and isolated replay are implemented. [Simulation](../../SIMULATION.md); [MP-060](../../tasks/README.md#mp-060), [MP-061](../../tasks/README.md#mp-061), [MP-062](../../tasks/README.md#mp-062) retain remaining needs. |
| Preferences, parameter profiles, user collections/favorites, basic comparison | Implemented: [Preferences](../../PLANNER_SETTINGS.md), [Profiles](../../PARAMETER_PROFILES.md), [collection implementation](../../../src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/ParametersEditor/ParametersEditorTabViewModel.Collections.cs). Independent comparison sources remain [MP-015](../../tasks/README.md#mp-015). |
| PX4Flow, joystick, optional tools, setup/config/advanced parity | Use the newer [implementation ledger](../../review-20261023/IMPLEMENTATION.md), not the older missing-feature tables. Hardware acceptance remains [MP-056](../../tasks/README.md#mp-056), [MP-057](../../tasks/README.md#mp-057). |
| Decode compass device IDs on Compass setup | Current [semantic compass service](../../../src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassConfigurationService.Semantic.cs) calls the shared decoder. |
| Firmware tabs, Windows DFU, reboot/handoff and identity recovery | Current immutable installation plan supersedes prior UI proposals; [Firmware](../../FIRMWARE.md) and [Identity/recovery](../../FirmwareIdentityAndRecovery.md) are authoritative. |
| Ship the official firmware manifest URL | [FirmwareOptions](../../../src/Core/MissionPlanner.Firmware/Configuration/FirmwareOptions.cs) supplies the official default. The old null-manifest observation is insufficient to reopen this; user acceptance is [MP-019](../../tasks/README.md#mp-019). |
| Mission intervention and in-flight adjustments deferred in wave 1 | Wave 2 implemented them; use [current actions parity](../../flightdata-actions-parity.md). |
| Map-menu stubs: import, polygon/fence/rally, generators, elevation graph, POI, UTM tools | Implemented according to [Missions](../../MISSIONS.md); old feature-menu lists lag that guide. [MP-033](../../tasks/README.md#mp-033), [MP-034](../../tasks/README.md#mp-034), [MP-039](../../tasks/README.md#mp-039), [MP-040](../../tasks/README.md#mp-040), [MP-041](../../tasks/README.md#mp-041), [MP-042](../../tasks/README.md#mp-042), [MP-043](../../tasks/README.md#mp-043), [MP-044](../../tasks/README.md#mp-044), [MP-045](../../tasks/README.md#mp-045) capture distinct remaining work. |
| Map keys, attribution, cache/prefetch, blank maps, raster MBTiles and hosted raster | Implemented infrastructure/runtime per [Maps](../../MAPS.md). WMS/WMTS rendering, exports and manual platform acceptance are still separate. |
| Duplicate GeoFence attribution and cursor coordinate/terrain readouts | Current [map guide](../../MAPS.md) documents a single deduplicated attribution overlay and pointer coordinate/terrain support. |
| Create FFT, tlog recording, telemetry Inspector and app log viewer | Implemented foundations; [FFT](../../FftAnalysisDesign.md), [Logging](../../Logging.md) and [Inspector](../../LiveTelemetryInspector.md) describe remaining limits. |
| Normalize ordinary button icons and create the catalogue | Completed per the [catalogue](../AXAML_BUTTON_INVENTORY.md); custom artwork remains an optional [MP-068](../../tasks/README.md#mp-068). |
| Remove Asv / generate MAVLink support | Owned implementation and generated wire coverage documented in [MAVLink](../../MAVLINK.md); future dialect updates follow its maintenance workflow. CI wiring remains [MP-074](../../tasks/README.md#mp-074). |

## Decisions that remain in force

- Vector/PMTiles is deferred by [ADR-0006](../../adr/ADR-0006-defer-vector-pmtiles.md); [MP-049](../../tasks/README.md#mp-049) is a request to revisit the decision, not permission to implement around it.
- The initial STM32 DFU provider uses an installed CubeProgrammer CLI under the [DFU ADR](../../adr/ADR-DFU-STM32CubeProgrammer-CLI.md). A native-provider rewrite is not automatically implied by platform qualification.
- Local/custom firmware retains strict board compatibility checks under [Firmware](../../FIRMWARE.md). The older override proposal is superseded.
- Legacy serial CLI Terminal, WinForms docking and GDAL/GeoTIFF requests are intentionally retired/dropped under the [implementation ledger](../../review-20261023/IMPLEMENTATION.md) and [feature inventory](../../FEATURES.md). Onboard scripting remains a distinct implemented workflow.
- The [implementation ledger](../../review-20261023/IMPLEMENTATION.md) records explicit limits such as absent support-server proxy contracts; this cleanup does not invent those contracts or treat them as implementation bugs.
- Output-only telemetry forwarding and isolated replay remain output-only; no implicit reverse command channel is introduced. See [Advanced](../../ADVANCED_SETUP.md) and [Simulation](../../SIMULATION.md).
- Current arming/action safeguards remain authoritative. Historical requests are not an instruction to add forced arming or bypass target/identity checks. See [Arming](../../ARMING.md) and [actions parity](../../flightdata-actions-parity.md).

## Other evidence and historical paths

The [23 October missing-feature baseline](../../MISSING_FEATURES.md) and portions of
[FEATURES.md](../../FEATURES.md) contain older status statements. Their remaining
candidates are incorporated into the unified review with evidence labels; they are not
parallel backlogs. The [24 October implementation ledger](../../review-20261023/IMPLEMENTATION.md)
takes precedence for its four audited areas. Historical test signatures in
[KNOWN_TEST_FAILURES.md](../../KNOWN_TEST_FAILURES.md) are superseded where the
[current test guide](../../../src/Tests/README.md) documents fixes or current skips.
The [DI audit](../DI-Registration-Audit.md) and icon catalogue remain useful
provenance outside the task-pack tree.

Old references beginning `docs/tasks/<pack>/` generally now live under
`docs/Archived/tasks/<pack>/`. The seven files moved in this cleanup have the exact
mapping above. The original Mission Planner source is upstream; resolve old local legacy
paths through [LEGACY_SOURCE.md](../../LEGACY_SOURCE.md). Preserve original execution
reports and failed hardware outcomes when closing follow-ups.


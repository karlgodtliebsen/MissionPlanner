# Mission Planner Next Gen — feature parity review

Review date: **24 October 2026**. Scope: **Mandatory Hardware, Optional Hardware, Config, and Advanced**.

Next Gen has substantial working infrastructure and many completed workflows, but several visible pages still lack the operation their names imply. The largest gaps are operational DroneCAN, joystick, CubeID, ESP8266 and antenna-tracker support; complete tuning workflows; MAVFTP file management; and several Advanced tools. There are also integration defects in existing features, particularly Extended Tuning expansion and optional-hardware parameter editors.

This review compares user capabilities, not the appearance of the old application. The current Next Gen design can be retained while closing these gaps. A parameter accessible in the full editor is not described as entirely unavailable merely because its dedicated setup workflow is incomplete.

## Evidence and interpretation

- Next Gen source: `C:\Projects\MissionPlanner`, branch `migration/compare-and-create-tasks`, HEAD `4cabaed3ac7229d23b54adeb614ae06f9b39db7d` at review time.
- Original source: `C:\Temp\MissionPlanner-v.1.38\src`. The folder name identifies the supplied snapshot; it is not independent verification of its application version or Git revision.
- Screenshots: the four adjacent section folders. The [capture manifest](capture-manifest.json) identifies the Next Gen session as COM21 / 115200, system 21 / component 1, Quadrotor, ArduCopter 4.7.2. Most captures show the initial scroll position. Existing screenshots were preserved.
- Original navigation was traced through [Original: InitialSetup.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/InitialSetup.cs) and [Original: SoftwareConfig.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/SoftwareConfig.cs), including firmware-dependent pages not visible with this quad. Next Gen navigation and implementation were traced through [Next Gen: MainShellViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/Navigation/MainShellViewModel.cs), page view models, services and dependency registration.
- Official online help was consulted on the review date. Source handlers are the primary evidence for what this particular original snapshot implements. Online help corroborates intended behavior; it does not establish what Next Gen implements.

**Status meanings:** **Present** means an implementation and UI route were found, not that every hardware operation was bench-tested. **Partial** means a useful subset exists. **Missing** means no reachable implementation was found in the reviewed source. **Defect** means an existing workflow has a concrete implementation problem. **Relocated** means the capability exists elsewhere. **Decision** identifies legacy or deliberately narrower behavior that should not automatically be copied.

**Priority:** **P1** restores a major advertised workflow or fixes an important usability/compatibility defect; **P2** closes functional parity gaps; **P3** is specialist, convenience, or legacy work. These are implementation priorities, not flight-safety certification.

This was a source, screenshot and documentation review. No vehicle settings were written and no calibration, motor test, firmware update or flight operation was executed for this analysis. No new build/test run was needed for the documentation-only changes. Negative findings were checked against related services and other UI areas, not inferred from an empty screenshot alone.

The inventory below produces **47 backlog groups**: 8 Mandatory Hardware, 16 Optional Hardware, 10 Config and 13 Advanced. These include partial workflows, defects and explicit scope decisions; they are not a count of wholly missing features or a percentage of overall application completeness.

| Screenshot set | Original | Next Gen |
|---|---:|---:|
| [MandatoryHardware](MandatoryHardware/) | 13 | 27 |
| [OptionalHardware](OptionalHardware/) | 20 | 41 |
| [Config](Config/) | 8 | 11 |
| [Advanced](Advanced/) | 3 | 13 |
| **Total** | **44** | **92** |

The uneven counts reflect different tab structures, additional Next Gen pages and extra captures, rather than missing image pairs. Screenshots establish visible state; source establishes whether controls invoke real operations.

## Recommended implementation order

| Order | Work | Reason |
|---|---|---|
| 1 | CF-01, CF-02, CF-03, MH-06 | Make existing tuning editors usable and match current firmware parameter names and units. |
| 2 | OH-09, OH-10 | Complete basic battery setup and preserve parameter types, ranges and signed values in optional-hardware editors. |
| 3 | MH-01 through MH-05 | Close the remaining common calibration and flight-mode workflows. |
| 4 | OH-01 through OH-06 | Replace non-operational hardware-tool backends and complete serial-radio tooling. |
| 5 | CF-05, CF-06, OH-15 | Add MAVFTP management, OSD tuning slots and FFT logging setup. |
| 6 | AD-01 through AD-04 | Complete persistent warning behavior, inspector plotting and signing lifecycle management. |
| 7 | Remaining P2/P3 items | Add specialist hardware, vehicle-family and legacy workflows according to actual demand. |

## Mandatory Hardware

### Page inventory

| Original page / capability | Next Gen assessment | Remaining work |
|---|---|---|
| Intro / setup guidance | Present: Information tabs, Safety and Summary add useful guidance. | No requirement to reproduce the old introduction screen. |
| Frame class/type | Present: firmware-derived choices, pending edits and write/readback. Motor-layout visualization exists under Motor Test. | Frame-selection illustrations would be a convenience improvement, not missing configuration capability. |
| Traditional helicopter setup | Missing dedicated workflow; general servo/parameter editing is available. | MH-07. |
| Accelerometer | Partial: six-position and level calibration are implemented. | Simple calibration: MH-01. |
| Compass | Partial: onboard calibration, accept/cancel/retry, use/orientation/external settings and reboot exist. | Priority/cleanup/advanced controls and fixed-yaw calibration: MH-02/03. |
| Radio calibration / receiver binding | Present: live RC state, calibration workflow and supported DSM binding. | No confirmed major parity gap. ELRS pairing is not a missing DSM command. |
| Servo output | Present: output function, reversal, min/trim/max and live PWM. | Screenshot label clipping is a UI issue, not missing channels. |
| Serial ports | Present: baud, protocol and options, with confirmed values and explicit writes. | No confirmed major parity gap. |
| ESC calibration | Partial: protocol-aware guidance and motor tests exist. | Automatic supported calibration procedure and dedicated motor/ESC settings: MH-04. |
| Flight modes | Partial: six slots and live switch/current-mode context exist. | SIMPLE / SUPER_SIMPLE slot flags: MH-05. |
| Failsafe | Core parameter editing present. | Integrated live test context: MH-08. |
| Initial Tune Parameters | Partial: relevant parameter editor; calculator exists in Core but is not connected to the page. | MH-06. |
| Hardware ID | Present: identity and decoded sensor IDs. | Minor filtering issue in verification notes. |
| ADS-B | Present: reported ADSB_/AVD_ settings, metadata controls, search and favorites. | An empty firmware-specific list is not evidence that the editor is missing. |

### Detailed gaps

#### MH-01 — Simple accelerometer calibration — P2, missing operation

The original exposes simple calibration in addition to six-position and level calibration. `BUT_calib_accel_simple_Click` sends `PREFLIGHT_CALIBRATION` with parameter 5 set to 4. Next Gen exposes only the other two workflows. Evidence: [Original: ConfigAccelerometerCalibration.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAccelerometerCalibration.cs) and [Next Gen: AccelerometerSetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/AccelerometerSetupViewModel.cs). Compare [original](MandatoryHardware/MissionPlanner-Accelerometer.png) with [Next Gen](MandatoryHardware/MissionPlanner.NextGen-Accelerometer-2.png).

**Completion:** add a distinct, firmware-supported simple-calibration action with progress/result handling and cancellation consistent with the existing calibration service. Do not implement it as another label for six-position calibration.

#### MH-02 — Compass ordering, missing-device cleanup and advanced controls — P2, partial

The original supports moving compass priorities, removing missing compasses, selecting calibration fitness and configuring compass learning. Next Gen reads priority information but its semantic edit definitions cover enable/use, orientation, external status and yaw source; there is no corresponding priority/cleanup workflow or dedicated fitness/learning control. Evidence: [Original: ConfigHWCompass2.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWCompass2.cs) (`UpdateFirst3`, priority buttons, missing-device removal, `COMPASS_CAL_FIT`, `COMPASS_LEARN`), [Next Gen: CompassConfigurationService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassConfigurationService.cs), [Next Gen: CompassConfigurationService.Semantic.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassConfigurationService.Semantic.cs).

**Completion:** show detected device identities and priority order, review the proposed ID changes before applying them, expose supported advanced settings, and retain reboot guidance. Raw parameter editing is a workaround for some settings. [Official compass help](https://ardupilot.org/copter/docs/common-compass-calibration-in-mission-planner.html) also documents priority changes and calibration fitness.

#### MH-03 — Large-vehicle / fixed-yaw compass calibration — P2, missing operation

The original has a heading-based `FIXED_MAG_CAL_YAW` workflow for vehicles that cannot conveniently be rotated. No equivalent command flow was found in the Next Gen compass page/service. Evidence: [Original: ConfigHWCompass2.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWCompass2.cs) (large-vehicle calibration handler), [Next Gen: CompassSetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/CompassSetupViewModel.cs).

**Completion:** collect true heading, verify required position/orientation information, send the appropriate command and display its actual result. This is different from manually editing compass orientation. [Official help](https://ardupilot.org/copter/docs/common-compass-calibration-in-mission-planner.html) describes the GPS and true-heading requirements.

#### MH-04 — Complete the ESC calibration workflow — P2, partial

The original page offers `MOT_PWM_TYPE`, PWM endpoints and motor spin settings, and starts automatic calibration by setting `ESC_CALIBRATION=3`. Next Gen primarily provides manual analog-ESC guidance and motor testing. Its Motor Test page already includes spin-arm/minimum assistance, so those features should not be duplicated. Evidence: [Original: ConfigESCCalibration.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigESCCalibration.cs), [Next Gen: EscMotorSetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/EscMotorSetupViewModel.cs), [Next Gen: ActuatorTestService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/ActuatorTestService.cs), [Next Gen: MotorTestViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestViewModel.cs).

**Completion:** expose supported protocol/endpoints and a guided automatic calibration option where the firmware and ESC protocol support it, with power-cycle instructions and verified state. Preserve the existing distinction between analog calibration and protocols such as DShot. The [official setup overview](https://ardupilot.org/planner/docs/mission-planner-initial-setup.html) makes that distinction as well.

#### MH-05 — SIMPLE and SUPER_SIMPLE flight-mode options — P2, partial

The original saves both bitmasks alongside the six mode assignments. Next Gen assigns the six modes but does not expose or update those per-slot flags. Evidence: [Original: ConfigFlightModes.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigFlightModes.cs) (`SaveModes`, `SIMPLE`, `SUPER_SIMPLE`), [Next Gen: FlightModeConfigurationService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/FlightModeConfigurationService.cs). Compare [original](MandatoryHardware/MissionPlanner-FlightModes.png) with [Next Gen](MandatoryHardware/MissionPlanner.NextGen-FlightModes-2.png).

**Completion:** show per-slot choices when supported, preserve unrelated bits and verify changes. The original also contains PX4 `COM_FLTMODE` support; retaining non-ArduPilot firmware support is a separate product-scope decision, not a Copter blocker.

#### MH-06 — Integrate the Initial Tune calculator and update its mappings — P1, integration and compatibility gap

The original calculates a reviewed starting configuration from propeller size, battery cells and voltage/chemistry choices, with optional ESC-specific and battery/fence suggestions. Next Gen contains `InitTuneParametersCalculator`, but references lead to the implementation and tests, not a page invoking it. The current page is a parameter list. Its parameter filter matches `ACCEL` but not the newer `ACC_` spelling, and the calculator uses older Copter names without the original QuadPlane translation. Evidence: [Original: ConfigInitialParams.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigInitialParams.cs), [Next Gen: InitTuneParametersCalculator.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/InitTuneParametersCalculator.cs), [Next Gen: InitTuneParametersService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/InitTuneParametersService.cs), [Next Gen: InitTuneParametersViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/InitTuneParametersViewModel.cs). See [Next Gen page](MandatoryHardware/MissionPlanner.NextGen-InitialTuneParameters-2.png).

**Completion:** add inputs, calculation, a before/after proposal, selective apply and readback; support actual reported old/new parameter names with correct unit conversions and appropriate Q_A/Q_M mappings. Cover all intended filter outputs, including FLTT, rather than assuming the existing mathematical subset is full parity. Validate recommendations against current firmware guidance instead of copying every historical constant. Tests of the calculator alone do not establish UI completion.

#### MH-07 — Traditional helicopter setup — P2, missing dedicated workflow

The original conditionally exposes `ConfigTradHeli4`: grouped helicopter setup parameters with metadata and servo configuration. Next Gen's frame and servo pages do not provide the helicopter-specific swash/collective/rotor setup workflow. Evidence: [Original: InitialSetup.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/InitialSetup.cs), [Original: ConfigTradHeli4.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigTradHeli4.cs), [Next Gen: MainShellViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/Navigation/MainShellViewModel.cs).

**Completion:** implement a helicopter-aware setup section using parameters actually reported by the firmware, including the grouped setup and servo context. This cannot be validated from the quad screenshots; raw parameter access is only a partial workaround.

#### MH-08 — Failsafe test context — P3, workflow gap

The original page combines failsafe settings with live RC/servo and vehicle-state indications. Next Gen's failsafe page is a supported-parameter editor; related telemetry exists elsewhere but is not integrated into this workflow. Evidence: [Original: ConfigFailSafe.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigFailSafe.cs), [Next Gen: FailSafeService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/FailSafeService.cs), [Next Gen: MandatoryParameterViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/MandatoryParameterViewModel.cs).

**Completion:** provide live receiver values and armed/GPS/mode state beside the settings, or a clear linked diagnostic workflow. Do not infer successful failsafe behavior from a saved threshold. The original Failsafe screenshot is a warning dialog, so this finding comes from source rather than a complete visual comparison.

## Optional Hardware

### Page inventory

| Original page / capability | Next Gen assessment | Remaining work |
|---|---|---|
| RTK/GPS Inject | Partial: serial/NTRIP RTCM3 correction forwarding and counters. | Base receiver setup, status, extra sources and multi-target behavior: OH-07/08. |
| CubeID Update | Partial shell: local image inspection and CRC/chunk calculation. | Actual discovery/update transaction: OH-03. |
| SiK radio | Partial: local serial read/write service; remote values can be read internally. | Remote editing, structured configuration and firmware updates: OH-06. |
| CAN GPS order | Partial: raw supported parameter editing. | Detected-node assignment workflow: OH-11. |
| Battery Monitor and Battery Monitor 2 | Combined multi-instance page with telemetry, thresholds and measured calibration. | Backend selection, sensor/board setup and remaining controls: OH-09. |
| DroneCAN/UAVCAN | UI/contracts exist, but production registration uses an unsupported transport factory. | OH-01. |
| Joystick | UI/contracts exist, but no operational input/output adapter. | OH-02. |
| Compass/Motor Calibration | Protocol workflow exists. | Live graphical analysis: OH-14. |
| Rangefinder | Partial parameter setup. | Typed controls and live measurement context: OH-10/12. |
| Airspeed | Reported-parameter setup exists. | Typed controls: OH-10; unsupported hardware is not an implementation failure. |
| PX4Flow | Present: image-stream handling and focus/video mode are implemented. | Real sensor validation remains; do not retain the historical blanket “missing” label. |
| Optical Flow | Reported FLOW settings available; separate from PX4Flow image viewing. | Typed controls: OH-10. |
| OSD | Links to the shared onboard-layout workspace. | Original external-OSD telemetry-rate preset is different: OH-16. |
| Camera Gimbal | Broad camera/mount parameter editing; live payload operation elsewhere. | Signed-value defect and output-assignment workflow: OH-10/13. |
| AntennaTracker firmware setup | Missing dedicated configuration/test workflow. | OH-05. |
| Standalone Antenna Tracker | Information-only Next Gen page. | Serial pan/tilt tracking support: OH-05. |
| Motor Test | Present: individual/all/sequence tests, stop, layout and spin-threshold assistance. | No confirmed major parity gap; operation was not exercised during screenshot capture. |
| Bluetooth Setup | Classic serial AT-module configuration implementation exists. | Validate supported module dialects on hardware; BLE is not implied by the original feature. |
| Parachute | CHUTE parameter editing exists. | Typed choices and integrated servo assignment: OH-10/13. |
| FFT Setup | FFT analysis exists, including a DataFlash workflow. | Vehicle batch-logging configuration: OH-15. |
| Naming | Next Gen addition. | No original parity deficit. |

### Detailed gaps

#### OH-01 — Operational DroneCAN stack and tools — P1, missing backend

`DomainConfigurator` registers `UnsupportedDroneCanTransportFactory`; both direct SLCAN and MAVLink-tunnel connection paths are unsupported. The node UI and interfaces therefore do not establish operational support. The original implements CAN access, node discovery/status, parameter access, node restart, firmware update, inspection and logging, with additional dynamic-ID and passthrough facilities. Evidence: [Next Gen: DomainConfigurator.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs), [Next Gen: ComponentHardwareTools.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/ComponentHardwareTools.cs), [Original: ConfigDroneCAN.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigDroneCAN.cs). See [Next Gen](OptionalHardware/MissionPlanner.NextGen-DroneCANUAVCAN-2.png).

**Completion:** first deliver a real transport and tested discovery/parameter/restart operations; then firmware transfer, inspector/logging and the required allocator/passthrough workflows. Keep direct CAN and autopilot-tunnel support explicit. The [official setup help](https://ardupilot.org/planner/docs/mission-planner-initial-setup.html) describes node firmware, configuration and inspection as part of this tool.

#### OH-02 — Joystick input, mapping and vehicle control — P1, missing backend

Production DI uses `UnsupportedJoystickProvider` and `DisabledJoystickVehicleOutput`. The current view model enumerates/releases devices but does not supply a working desktop sampling/mapping/output workflow. Axis normalization contracts and tests are useful groundwork, not complete joystick support. Evidence: [Next Gen: DomainConfigurator.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs), [Next Gen: JoystickControl.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/JoystickControl.cs), [Next Gen: JoystickViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/JoystickViewModel.cs), [Original: JoystickSetup.cs](C:/Temp/MissionPlanner-v.1.38/src/Joystick/JoystickSetup.cs).

**Completion:** add Windows/Linux/macOS input adapters as applicable, axis/button mapping, calibration, reversal/expo, live preview and explicitly enabled vehicle output with loss-of-input handling. Include both the supported RC-override and manual-control use cases as a conscious protocol choice. Local joystick calibration must remain separate from receiver calibration.

#### OH-03 — CubeID firmware transfer — P1, incomplete advertised operation

Next Gen reads a local `.bin` file and calculates CRC/chunks; it has no end-to-end uploader and does not populate the target-detection state through a component discovery service. The original supports official/custom images and a firmware-transfer workflow, including serial passthrough preparation. Evidence: [Next Gen: CubeIdUpdateViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CubeIdUpdateViewModel.cs), [Next Gen: ComponentHardwareTools.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/ComponentHardwareTools.cs), [Original: ConfigCubeID.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigCubeID.cs).

**Completion:** detect the correct component, select an image, transfer with acknowledgements/progress/cancellation, verify the outcome and restore temporary passthrough settings. Do not enable the button merely because the file parses.

#### OH-04 — ESP8266 bridge read/write/persist/reboot — P1, incomplete advertised operation

Next Gen validates SSID/password data but does not have the component-parameter transaction behind this page. The original reads/writes AP and station credentials, channel, UART baud, network addresses and Wi-Fi mode, then supports storage/reboot/reset operations. Evidence: [Original: ConfigHWesp8266.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWesp8266.cs), [Next Gen: Esp8266SetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/Esp8266SetupViewModel.cs), [Next Gen: ComponentHardwareTools.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/ComponentHardwareTools.cs).

**Completion:** wire discovery and component-targeted parameter operations, add the missing fields and supported persistence/reboot/reset actions, and report confirmed results. Credential encoding alone is not configuration.

#### OH-05 — Both antenna-tracker workflows — P2, missing operation

The original has two distinct capabilities: configuration/test of an ArduPilot AntennaTracker vehicle, and a standalone pan/tilt serial tracker following vehicle position. Next Gen's `AntennaTrackerViewModel` only refreshes explanatory status. Its “Tracker vehicle connected” text currently depends on any active vehicle being online, not a tracker identity. Evidence: [Original: ConfigAntennaTracker.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAntennaTracker.cs), [Original: TrackerUI.cs](C:/Temp/MissionPlanner-v.1.38/src/Antenna/TrackerUI.cs), [Next Gen: AntennaTrackerViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/AntennaTrackerViewModel.cs).

**Completion:** distinguish tracker firmware from a tracked aircraft; add supported yaw/pitch ranges, servo setup, controllers and bounded test actions; separately provide the required standalone tracker drivers, calibration and tracking loop. A generic SERVO parameter report is not either workflow.

#### OH-06 — Complete SiK/RFD radio configuration — P2, partial

The original has coordinated local/remote settings, copy/default workflows and firmware updating. Next Gen reads local and remote settings internally, but presents a local text editor and its apply path sends local AT commands. Remote editing, a structured paired-radio UI and firmware upload are absent. Evidence: [Original: Sikradio.cs](C:/Temp/MissionPlanner-v.1.38/src/Radio/Sikradio.cs), [Next Gen: ExternalSerialTools.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/ExternalSerialTools.cs), [Next Gen: SikRadioViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/SikRadioViewModel.cs). Compare [Next Gen](OptionalHardware/MissionPlanner.NextGen-SIKRadio-2.png) with the original SiK screenshot in this folder.

**Completion:** expose readable named settings, remote edits and comparison, explicit save/reboot behavior, and supported firmware update paths. Verify the parser against real ATI5/RTI5 responses rather than relying solely on synthetic key/value inputs.

#### OH-07 — RTK base configuration and diagnostics — P2, partial

Next Gen forwards corrections; the original additionally configures supported GNSS receivers, starts survey-in with duration/accuracy settings, supports fixed base coordinates and saved positions, and displays receiver/survey/constellation diagnostics. Evidence: [Original: ConfigSerialInjectGPS.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigSerialInjectGPS.cs) (`DoConnect`, UBX configuration, survey-in and base-position handlers), [Next Gen: RtkCorrectionInjection.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/RtkCorrectionInjection.cs), [Next Gen: RtkGpsInjectViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/RtkGpsInjectViewModel.cs).

**Completion:** add receiver-specific configuration where supported, survey/fixed-base management and meaningful receiver status. Keep “receiving RTCM bytes,” “valid correction stream” and “base survey complete” separate states. This is not the Advanced Moving Base tool in AD-08.

#### OH-08 — Additional RTK inputs and target distribution — P3, partial

The Next Gen source-kind enum supports Serial and Ntrip. The original injector also supports other network input modes and broader correction-source handling/distribution. Evidence: [Original: ConfigSerialInjectGPS.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigSerialInjectGPS.cs), [Next Gen: RtkCorrectionInjection.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/RtkCorrectionInjection.cs).

**Completion:** prioritize raw TCP/UDP correction input and deliberate distribution to selected connected vehicles if these are required; evaluate legacy SBP support separately. Do not mistake ordinary TCP/UDP vehicle connections for correction-source support. The idle “No active vehicle target” screenshot is not proof of a connection defect.

#### OH-09 — Battery backend/sensor setup and missing controls — P1, partial

The Next Gen page displays monitor type but has no control for selecting/enabling it. It exposes capacity, low/critical thresholds and measured voltage/current calibration, while the original also provides monitor/sensor/board selection and associated pin/gain configuration. Core contains more battery operations than the view exposes, but does not provide the original analog pin setup as a complete workflow. Evidence: [Original: ConfigBatteryMonitoring.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigBatteryMonitoring.cs), [Next Gen: BatteryConfigurationService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/MandatoryHardware/BatteryConfigurationService.cs), [Next Gen: BatterySetupView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/BatterySetupView.axaml), [Next Gen: BatterySetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/BatterySetupViewModel.cs). See [Next Gen](OptionalHardware/MissionPlanner.NextGen-BatteryMonitor-2.png).

**Completion:** expose supported backend selection, analog input pins or appropriate sensor presets, scaling/offset controls and supported failsafe actions. Preserve the existing multi-instance UI and measured calibration. Generic parameters can work around some omissions; a disabled monitor should be configurable from the dedicated page.

#### OH-10 — Typed optional-hardware parameter editors — P1, UI defect and usability gap

`ParameterSettingViewModel` reduces settings to names and numeric pending values. Several views consequently offer raw numeric/text entry instead of enum choices, bitmask controls, units and limits. More concretely, `CameraGimbalView.axaml` imposes `Minimum="0"` on every setting, excluding legitimate negative mount angles/positions. Repeated rangefinder “type” labels also hide which instance/parameter is being edited. Evidence: [Next Gen: ParameterSettingViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/ParameterSettingViewModel.cs), [Next Gen: CameraGimbalView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CameraGimbalView.axaml), [Next Gen: RangefinderView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/RangefinderView.axaml), [Next Gen: AirspeedView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/AirspeedView.axaml).

**Completion:** preserve firmware metadata through the view model, choose the correct editor, allow legitimate signed values, display instance/name and units, and retain unknown enum values/bits. Reuse the richer metadata behavior already used by mandatory/general parameter editors without redesigning the application.

#### OH-11 — CAN GPS detected-node assignment — P3, workflow gap

The original presents detected GPS node identities and explicit assignment to GPS slots. Next Gen exposes a small set of supported override parameters as raw edits. Evidence: [Original: ConfigGPSOrder.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigGPSOrder.cs), [Next Gen: CanGpsOrderView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CanGpsOrderView.axaml).

**Completion:** show detected versus assigned nodes and provide clear GPS1/GPS2 assignment actions with readback. Parameter editing already exists; node-to-slot interpretation is the missing convenience.

#### OH-12 — Live rangefinder setup readings — P3, partial

The original rangefinder setup shows live distance and voltage. Next Gen's dedicated setup page supplies parameter rows but not that live diagnostic feedback. Legacy unnumbered parameter names also need an explicit support decision. Evidence: [Original: ConfigHWRangeFinder.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWRangeFinder.cs), [Next Gen: RangefinderViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/RangefinderViewModel.cs).

**Completion:** associate fresh distance and any available voltage with the selected sensor, including units and stale/unavailable states. Proximity radar is useful elsewhere but does not fully replace analog sensor setup feedback.

#### OH-13 — Integrated camera/gimbal and parachute output assignment — P3, partial workflow

The original mount and parachute pages associate functions with physical servo outputs and related endpoint/reversal settings. Next Gen edits the CAM/MNT/GMBL or CHUTE settings, while SERVO function assignment remains on the separate Servo Output page. Evidence: [Original: ConfigMount.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigMount.cs), [Original: ConfigHWParachute.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWParachute.cs), [Next Gen: CameraGimbalView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CameraGimbalView.axaml), [Next Gen: ServoOutputSetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/ServoOutputSetupViewModel.cs).

**Completion:** add a linked or embedded output-assignment step with conflict visibility and verified writes. This is an integration gap, not absent servo support or absent live camera operation. Do not silently clear other outputs just to mimic the original handler.

#### OH-14 — CompassMot graphical feedback — P3, partial

Next Gen implements start/stop and sample collection. The original also graphs interference/current against throttle; the Next Gen page lists samples without the equivalent graph. Evidence: [Original: ConfigCompassMot.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigCompassMot.cs), [Next Gen: CompassMotorCalibrationViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/CompassMotorCalibrationViewModel.cs).

**Completion:** graph the recorded samples with labels/units and the final calibration result. An empty pre-calibration screen does not mean the calibration protocol is absent.

#### OH-15 — FFT batch-logging setup — P2, missing setup workflow

The original FFT setup configures `INS_LOG_BAT_CNT`, `INS_LOG_BAT_MASK` and logging selection before opening analysis. Next Gen's FFT setup view accepts samples for analysis and links to a real FFT/DataFlash workspace, but does not configure those logging parameters. Evidence: [Original: ConfigFFT.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigFFT.cs), [Next Gen: FftSetupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/FftSetupViewModel.cs). See [Next Gen configuration](OptionalHardware/MissionPlanner.NextGen-FFTSetup-2.png) and [analysis](OptionalHardware/MissionPlanner.NextGen-FFTSetup-3.png).

**Completion:** add firmware-supported batch-logging configuration and acquisition guidance. Reuse existing offline FFT/spectrogram analysis; it is already implemented and should not be listed as missing.

#### OH-16 — External-OSD telemetry-rate setup — P3, missing legacy workflow

The original Optional Hardware OSD button sets SR stream rates for external OSD telemetry. Next Gen's page is a bridge to onboard layout editing, which is a different task. Evidence: [Original: ConfigHWOSD.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigHWOSD.cs) (`BUT_osdrates_Click`), [Next Gen: OnboardOsdBridgeViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/OnboardOsdBridgeViewModel.cs).

**Completion:** offer an applicable port/device-specific telemetry-rate workflow if external MAVLink OSD support is retained. Do not blindly copy the original hard-coded updates to several SR ports or imply that changing onboard screen coordinates configures an external telemetry device.

## Config

### Page inventory

| Original page / capability | Next Gen assessment | Remaining work |
|---|---|---|
| GeoFence | Present: parameters plus map geometry editing/synchronization, beyond the original configuration page alone. | No confirmed major feature gap in this comparison; geometry was not downloaded during capture. |
| Basic Tuning, Copter | Partial: deliberate pilot-feel/navigation controls rather than the old combined PID sliders. | Modern aliases/units and a decision about simplified PID assistance: CF-02/04. |
| Extended Tuning, Copter | Substantial controller/filter editing infrastructure. | Expansion defect, modern aliases and omitted groups: CF-01/02/04. |
| Plane tuning / QuadPlane extended tuning | Partial profiles; not equivalent to the original vehicle-specific editors. | CF-03. |
| Rover tuning | Speed/steering controllers and several basic settings exist. | Remaining dedicated fields and compatibility aliases: CF-03/04. |
| AntennaTracker tuning | No equivalent dedicated profile. | OH-05; do not count twice. |
| Standard / Advanced Params | Consolidated into full Parameters Editor. | Metadata-level filtering and navigation: CF-08. |
| Onboard OSD | Layout discovery, item enable/position editing, options and preview exist. | Parameter tuning slots and direct visual layout interaction: CF-06/07. |
| MAVFTP | Browse/download implementation exists. | Upload/delete/rename/mkdir/CRC: CF-05. |
| User Params | Replaced by general parameter editing/profiles. | A persistent curated live parameter list: CF-08. |
| Full Parameter Tree / List | General editor supports search, typed values, files, pending edits and comparisons. | Tree/favorites, presets and maintenance operations: CF-08/09. |
| Planner | Relocated to Preferences, with additional modern settings. | Speech is stored but not operational: CF-10. Other legacy option effects need a separate settings audit. |
| CubeLAN 8-port switch | Real DEVICE_OP configuration adapter/codec exists. Original is a plugin. | Hardware verification, not a confirmed missing feature. |
| Flight Modes | Available under Mandatory Hardware. | Relocation; MH-05 contains the actual remaining gap. |
| Ateryx-specific modes/sensors/PIDs | Legacy conditional original integration; no matching Next Gen integration found. | Product-scope decision, not an ordinary ArduPilot parity requirement. |

The original exposes different pages for different vehicles. The [official Config documentation](https://ardupilot.org/planner/docs/mission-planner-configuration-and-tuning.html) also describes that conditional behavior. The connected Copter screenshots cannot establish Plane, Rover or tracker completeness.

### Detailed gaps

#### CF-01 — Extended Tuning expansion does not materialize editors — P1, defect

`ExtendedTuningGroupViewModel.ToggleExpanded()` calls `EnsureMaterialized()` and refreshes visible fields. The AXAML Expander instead binds `IsExpanded` directly and does not invoke that command; there is no property-change hook that performs the materialization. Thus opening a group can show its header/actions without its editor rows. Search-driven materialization is a separate path. Evidence: [Next Gen: ExtendedTuningGroupViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/ExtendedTuningGroupViewModel.cs), [Next Gen: ExtendedTuningTabView.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/ExtendedTuningTabView.axaml). The [expanded screenshot](Config/MissionPlanner.NextGen-ExtendedTuning-2.png) shows this symptom.

**Completion:** make ordinary expansion initialize and display rows, including after refresh/reconnect, without requiring a search first. Verify collapsed groups remain lazy where intended. This is a repair to an implemented editor, not a request to replace the tuning design.

#### CF-02 — Current parameter aliases and unit conversions — P1, compatibility defect

The original Copter editor contains old/new alternatives for controller and navigation names, including `PSC_NE_*`, `PSC_D_*`, `WP_SPD*`, `WP_RADIUS_M`, `LOIT_SPEED_MS` and modern acceleration limits. Next Gen's catalogs largely use older `PSC_VELXY`, `PSC_ACCZ`, `WPNAV_*` and `LOIT_SPEED` names. A few aliases exist, but coverage is incomplete. This explains why a connected modern firmware can yield only a small Basic Tuning subset even though more relevant parameters exist. Evidence: [Original: ConfigArducopter.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArducopter.cs), [Next Gen: BasicTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/BasicTuningProfileCatalog.cs), [Next Gen: ExtendedTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/ExtendedTuningProfileCatalog.cs). See [Basic Tuning](Config/MissionPlanner.NextGen-BasicTuning-1.png).

**Completion:** resolve descriptors against actual firmware parameters, preserve metadata units and convert values where old/new names use different scales. Test representative old and current parameter sets. An alias must not imply numerically interchangeable values. MH-06 is the corresponding Initial Tune problem.

#### CF-03 — Correct Plane/QuadPlane profiles and broaden family compatibility — P1 for Plane, partial

The Next Gen Plane extended profile reuses the `ATC_RAT_{axis}` rate descriptor. Original Plane setup explicitly handles fixed-wing `RLL_RATE_*` / `PTCH_RATE_*` and older `RLL2SRV_*` / `PTCH2SRV_*` alternatives. The original also conditionally opens Copter-style Q_A/Q_P/Q_WP tuning for a QuadPlane. Those are not supplied by the current generic Plane rate group. Evidence: [Original: ConfigArduplane.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArduplane.cs), [Original: ConfigArducopter.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArducopter.cs), [Original: SoftwareConfig.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/SoftwareConfig.cs), [Next Gen: ExtendedTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/ExtendedTuningProfileCatalog.cs).

Rover has useful ATC speed/steering support, but the original also recognizes older controller aliases and exposes additional braking, motor/output, turning and obstacle-response settings. Compare [Original: ConfigArdurover.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArdurover.cs) with [Next Gen: BasicTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/BasicTuningProfileCatalog.cs) and [Next Gen: ExtendedTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/ExtendedTuningProfileCatalog.cs). For example, the Next Gen basic turning field names `TURN_MAX_G`, while the original also recognizes `ATC_TURN_MAX_G`; its unit treatment must be checked rather than assumed equivalent.

**Completion:** create firmware/family-correct definitions, expose QuadPlane groups only when applicable, and validate names/units against representative Plane, QuadPlane and Rover parameter snapshots. ArduSub support in Next Gen is useful, but does not substitute for these original family workflows.

#### CF-04 — Remaining curated tuning controls and simplified assistance — P2, partial

Beyond alias fixes, the current catalogs omit original dedicated controls such as horizontal position P, vertical position/velocity gains, RC tuning assignment and limits, convenient auxiliary-channel assignments, and some motor/navigation settings. Harmonic-notch definitions include frequency/bandwidth/attenuation/mode/harmonics but omit enable/reference/options. Not every parameter in the original family editors has a curated Next Gen counterpart. Evidence: [Original: ConfigArducopter.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArducopter.cs), [Original: ConfigArdurover.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigArdurover.cs), [Next Gen: ExtendedTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/ExtendedTuningProfileCatalog.cs), [Next Gen: BasicTuningProfileCatalog.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Tuning/BasicTuningProfileCatalog.cs).

The original simple Copter PID page also provides coupled roll/pitch and climb-control adjustments. Next Gen intentionally emphasizes pilot-feel/navigation controls in Basic Tuning. Record this as a workflow/product decision: retain the current separation and add an optional reviewed assistant if wanted, rather than inserting hidden PID writes into a pilot-feel slider. Evidence: [Original: ConfigSimplePids.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigSimplePids.cs).

**Completion:** add the missing supported groups to the appropriate existing workspace, document deliberate exclusions and preserve raw-editor access. Do not equate “available through a raw parameter search” with full curated tuning parity, or count it as an absent vehicle capability.

#### CF-05 — MAVFTP upload and file management — P1, partial

Next Gen lists directories, navigates and downloads with cancellation/session handling. The original also uploads, checks uploaded CRC, deletes, renames, creates directories and requests file CRC. Evidence: [Original: MavFTPUI.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/MavFTPUI.cs) (upload/delete/rename/mkdir/CRC handlers), [Next Gen: MAVFtpTabViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/MAVFtpTabViewModel.cs).

**Completion:** extend service and UI with these operations, progress, appropriate overwrite/delete confirmation, cancellation/retry and verified remote outcomes. This is especially useful for onboard script deployment. The [empty captured browser](Config/MissionPlanner.NextGen-MavFTP-1.png) does not prove browsing/download are unimplemented.

#### CF-06 — OSD parameter tuning slots — P2, missing protocol workflow

The original OSD page has a separate tuning-slot setup dialog and uses a provider to get/set a slot's parameter name, type, minimum, maximum and increment. Next Gen's layout editor discovers OSD screen/item parameters but has no equivalent OSD parameter-configuration transaction. Six discovered screens do not establish tuning-slot support. Evidence: [Original: ConfigOSD.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigOSD.cs) (`ShowOsdTuningSlotSetupDialog`, slot get/set), [Next Gen: OnboardOsdTabViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/OnboardOsdTabViewModel.cs).

**Completion:** add supported OSD5/6 slot discovery and assignment using the appropriate protocol, with bounds/increment and actual acknowledgement. [Official OSD parameter-menu help](https://ardupilot.org/copter/docs/common-paramosd.html) distinguishes these adjustable-parameter screens from ordinary layout elements.

#### CF-07 — Direct OSD layout interaction and representative preview — P3, partial

The original OSD configurator supports mouse-based layout interaction and a glyph-based representation. Next Gen's `OsdPreviewCanvas` renders item titles using a default font and has no pointer interaction; coordinates can still be edited through controls. Evidence: [Original: LayoutControl.cs](C:/Temp/MissionPlanner-v.1.38/src/ExtLibs/OSDConfigurator/GUI/LayoutControl.cs), [Original: Visualizer.cs](C:/Temp/MissionPlanner-v.1.38/src/ExtLibs/OSDConfigurator/GUI/Visualizer.cs), [Next Gen: OsdPreviewCanvas.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/OsdPreviewCanvas.cs). See [Next Gen OSD](Config/MissionPlanner.NextGen-OnboardOSD-1.png).

**Completion:** support selecting/moving items on the preview and provide representative symbols/values where the backend is known. Preserve coordinate editing as an accessible alternative. This is not a claim that font upload is missing from a feature the original page actually implemented; no such parity requirement was established here.

#### CF-08 — Curated User Params, favorites and parameter navigation — P2/P3, workflow gaps

The original has a user-configurable live parameter subset, normally exposing RC auxiliary functions; full-list favorites; a parameter tree; and Standard/Advanced filtering driven by metadata. Next Gen has a capable general editor and value profiles, but no corresponding persistent live subset/favorites/tree or active Standard/Advanced filter in that editor. `UserLevel` metadata is retained but is not used as that UI filter. Evidence: [Original: ConfigUserDefined.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigUserDefined.cs), [Original: ConfigRawParams.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigRawParams.cs), [Original: SoftwareConfig.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/SoftwareConfig.cs), [Next Gen: ParametersEditorTabViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/ParametersEditor/ParametersEditorTabViewModel.cs), [Next Gen: ParameterEditSession.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/ParameterEditSession.cs).

**Completion:** add named/pinned parameter collections and useful metadata/group filtering within the current editor. Profiles that store values are not the same as a curated live list. ADS-B already has its own favorites; avoid presenting this as an application-wide absence of favorites. RC auxiliary functions remain editable through the full editor.

#### CF-09 — Parameter preset library and explicit maintenance operations — P2/P3, partial

The original full-parameter editor includes downloadable parameter presets, reset-all and an explicit commit-to-flash action. Next Gen implements local import/export, profiles, comparison, pending edits and confirmed writes, but no equivalent preset-library picker or vehicle reset-all/explicit storage action was found in the editor. Evidence: [Original: ConfigRawParams.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigRawParams.cs) (`BUT_paramfileload_Click`, reset and commit handlers), [Next Gen: ParametersEditorTabViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/ConfigTuning/Sections/ParametersEditor/ParametersEditorTabViewModel.cs).

**Completion:** add a reviewed preset source if desired; implement reset/storage operations only for firmware that supports and needs them, with clear backup/reconnect behavior. Ordinary parameter writes already persist on many firmware paths, so an old “commit” button is not automatically a required extra write step. Do not confuse resetting local Preferences with resetting the vehicle.

#### CF-10 — Operational speech behind Planner preferences — P2, incomplete setting

Planner preferences have moved to the modern Preferences pages, which is valid. However, `SpeechEnabled` is loaded/saved by `PreferencesViewModel` and `PlannerLegacySettings` without a runtime speech implementation consuming it. The original supports actual spoken notifications and warning speech. Evidence: [Next Gen: PlannerLegacySettings.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/Planner/PlannerLegacySettings.cs), [Next Gen: PreferencesViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/Preferences/PreferencesViewModel.cs), [Original: WarningEngine.cs](C:/Temp/MissionPlanner-v.1.38/src/ExtLibs/Utilities/Warnings/WarningEngine.cs).

**Completion:** connect supported speech services and notification sources, or clearly label the option unavailable until implemented. Verify other imported legacy settings separately before claiming full Planner parity; their presence in a settings record alone proves persistence, not their runtime effect. Localization/video-device preferences were not promoted to confirmed missing-feature tasks without a complete consumer audit.

## Advanced

### Page/tool inventory

| Original tool | Next Gen assessment | Remaining work |
|---|---|---|
| Warnings | Rule editing, evaluation, delay/hysteresis/cooldown, acknowledgement and file persistence exist. | Background lifetime/output routing and compound conditions: AD-01/02. |
| MAVLink Inspector | Traffic aggregation, rates, raw/decoded details and export exist. | Field graphing: AD-03; selected-details capture needs verification. |
| MAVLink Mirror | Output-only serial/TCP/UDP mirror with direction filtering. | Return path/listener modes if required: AD-05. |
| MAVLink Signing | Key management, signing setup and verification exist. | Vehicle-side disable workflow: AD-04. |
| Proximity | Horizontal radar and diagnostic processing for distance/obstacle messages exist. | No confirmed major gap in the reviewed common workflow; sensor validation remains. |
| NMEA output | GGA/RMC formatting and serial/TCP/UDP output exist. | Additional original sentences: AD-06. |
| Follow Me | No operational counterpart found. | AD-07. |
| ParamGen | Metadata download/cache infrastructure exists. | Manual force-refresh tool missing; original source parser need not be copied: AD-09. |
| Moving Base | No operational counterpart found. | AD-08. |
| AnonLog | No log-anonymization export counterpart found. | AD-10. |
| FFT | Relocated to analysis/FFT workspaces. | Not missing; OH-15 concerns acquisition setup only. |
| Spectrogram | Relocated to FFT/vibration analysis. | Not missing as a separate Advanced button. |
| Support Proxy | No remote-support proxy tool found. | AD-11. |
| Scripting REPL | No onboard interactive REPL counterpart found. | AD-12. |
| Terminal | No legacy serial CLI tool found. | AD-13, explicit legacy-support decision. |

The original Advanced hub is defined in [Original: ConfigAdvanced.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAdvanced.cs), with Terminal/REPL added in [Original: InitialSetup.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/InitialSetup.cs). Next Gen's [Next Gen: AdvancedPage.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml) exposes six tools. Entries in [Next Gen: AdvancedFeature.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/AdvancedFeature.cs) are descriptive catalog entries; they do not establish that all corresponding operations are implemented.

### Detailed gaps

#### AD-01 — Continuous warning monitoring and delivery — P1, deliberate limitation / parity gap

Next Gen starts polling when the Warnings page activates and cancels it on deactivation. Results are displayed in that page; no corresponding global HUD/message/speech delivery was found. The page explicitly says visual warnings run only while it is open. Original `WarningEngine` runs independently and raises warning text/quick-panel-color events and speech. Evidence: [Next Gen: WarningManagerViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Warnings/WarningManagerViewModel.cs), [Next Gen: WarningEngine.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Warnings/WarningEngine.cs), [Original: WarningEngine.cs](C:/Temp/MissionPlanner-v.1.38/src/ExtLibs/Utilities/Warnings/WarningEngine.cs). See [Next Gen rules](Advanced/MissionPlanner.NextGen-Warnings-2.png).

**Completion:** if operational alerting parity is intended, move evaluation to a session/application service and route results into persistent notification/HUD/message surfaces; make speech optional. Keep freshness, acknowledgement and rate limiting. The current page-scoped visual tool is implemented as described, so this is a scope gap rather than an undisclosed runtime crash.

#### AD-02 — Compound warning conditions and output types — P2, partial

The original warning model permits chained child conditions and distinct text/speech or coloring behavior. Next Gen supports individual numeric comparisons/ranges with useful timing controls, but not equivalent compound condition trees or quick-panel coloring. Evidence: [Original: WarningsManager.cs](C:/Temp/MissionPlanner-v.1.38/src/Warnings/WarningsManager.cs), [Original: WarningEngine.cs](C:/Temp/MissionPlanner-v.1.38/src/ExtLibs/Utilities/Warnings/WarningEngine.cs) (`checkCond`, `Child`, warning types), [Next Gen: WarningManagerViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Warnings/WarningManagerViewModel.cs).

**Completion:** support at least explicit AND combinations, clearly represented to the user, and configurable delivery destinations. Test missing/stale fields in compound rules. Treat coloring as optional presentation; it should not block useful multi-condition alerts.

#### AD-03 — Graph MAVLink message fields — P2, missing inspector workflow

The original inspector can graph selected fields, including array elements. Next Gen supplies message counts/rates and detailed values but no inspector time-series plotting workflow. Evidence: [Original: MAVLinkInspector.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/MAVLinkInspector.cs) (`GraphIt`), [Next Gen: InspectorAggregator.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Inspector/InspectorAggregator.cs), [Next Gen: MavLinkInspectorViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Inspector/MavLinkInspectorViewModel.cs).

**Completion:** select numeric fields, retain bounded timestamped history and plot/export it with units and source identity. Existing log FFT analysis is not a substitute for a live arbitrary-message field graph.

#### AD-04 — Disable signing on the vehicle — P2, partial lifecycle

The original signing tool includes a vehicle-side disable action. Next Gen supports key management and configuring/verifying signing, but no equivalent disable transaction is exposed. Deleting a locally stored key is not vehicle-side signing removal. Evidence: [Original: AuthKeys.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/AuthKeys.cs), [Next Gen: SigningSetupService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Signing/SigningSetupService.cs), [Next Gen: SigningViewModel.cs](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/Signing/SigningViewModel.cs).

**Completion:** add an explicit supported disable workflow over an appropriate connection, verify its outcome and explain reconnect implications. Preserve existing key-storage and signing verification behavior.

#### AD-05 — Bidirectional mirror and listening endpoints — P3, scope decision

Original mirror tooling can forward traffic received from the secondary endpoint back to the flight controller and supports host/listening modes. Next Gen is explicitly output-only: its sink has a write path, and selecting Both means copying both directions observed on the primary link, not accepting secondary commands. Evidence: [Original: SerialOutputPass.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/SerialOutputPass.cs), [Next Gen: MirrorSession.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Output/MirrorSession.cs), [Next Gen: OutputEndpoint.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Output/OutputEndpoint.cs).

**Completion:** if parity is wanted, add a separately enabled relay mode, listening endpoint configuration and loop prevention, with clear ownership of inbound commands. Do not silently broaden the existing read-only mirror. This is a deliberate narrower design, not a broken output mirror.

#### AD-06 — Additional NMEA sentences — P3, partial

Next Gen emits GGA and RMC. The original additionally emits GLL, HDG and VTG, plus its custom HOM/RPY messages for consumers that rely on them. GSA/GSV/ZDA code in the original is commented out and is not counted as implemented parity. Evidence: [Original: SerialOutputNMEA.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/SerialOutputNMEA.cs), [Next Gen: NmeaFormatter.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/Nmea/NmeaFormatter.cs).

**Completion:** add selectable standard sentences based on real consumer needs, then explicitly opt-in legacy custom messages if necessary. Preserve correct checksums, source freshness and meaningful missing-data handling.

#### AD-07 — Follow Me from operator GPS — P2, missing tool

The original reads an operator GPS/NMEA position and sends guided target updates. No corresponding application workflow was found in Next Gen; a feature-catalog label does not implement it. Evidence: [Original: FollowMe.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/FollowMe.cs), [Next Gen: AdvancedFeature.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/AdvancedFeature.cs), [Next Gen: AdvancedPage.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml).

**Completion:** add source selection, live operator-position status, explicit start/stop and bounded guided-target updates, including loss-of-fix and connection handling. This is more than displaying the vehicle on a map.

#### AD-08 — Moving Base position/rally workflow — P2, missing tool

The original Moving Base tool consumes external position data and updates moving-base/rally-related state. No corresponding Next Gen tool was found. Evidence: [Original: MovingBase.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/MovingBase.cs) (position input and rally update), [Next Gen: AdvancedFeature.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/AdvancedFeature.cs), [Next Gen: AdvancedPage.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml).

**Completion:** provide the intended serial/network position input and explicit update policy, with clear stale-source behavior. Do not count RTK correction injection or GPS-for-yaw moving-baseline support as the same capability.

#### AD-09 — Manual parameter-metadata refresh — P3, integration gap

The original ParamGen action downloads/rebuilds metadata and reloads it. Next Gen already has automatic metadata retrieval and `RefreshMetadataAsync`/repository refresh APIs, but no UI consumer of the force-refresh operation was found. Evidence: [Original: ConfigAdvanced.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAdvanced.cs), [Next Gen: IVehicleParameterMetadataService.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Vehicles/Abstractions/IVehicleParameterMetadataService.cs), [Next Gen: IParameterMetadataRepository.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.MavLink/Parameters/Metadata/Abstractions/IParameterMetadataRepository.cs).

**Completion:** expose a cancellable force-refresh operation with source/cache status and refresh the affected editors. Copying the original legacy C++ parameter scraping is not necessary if maintained metadata feeds supply the required behavior.

#### AD-10 — Anonymized log export — P2, missing tool

The original AnonLog handler processes telemetry/DataFlash log formats through `Privacy.anonymise`. No equivalent user-facing log transformation/export was found in Next Gen. Redacted application diagnostics are a different feature. Evidence: [Original: ConfigAdvanced.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAdvanced.cs), [Next Gen: AdvancedFeature.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/Advanced/AdvancedFeature.cs).

**Completion:** define which identifying/location fields are transformed, export to a new artifact and validate the result with parsers and representative logs. Do not promise anonymity solely because one coordinate field was removed; the original itself labels this operation experimental.

#### AD-11 — Remote support proxy — P3, missing tool / scope decision

The original opens `SerialSupportProxy`; Next Gen's output mirror does not implement that remote-support connection workflow. Evidence: [Original: SerialSupportProxy.cs](C:/Temp/MissionPlanner-v.1.38/src/Controls/SerialSupportProxy.cs), [Original: ConfigAdvanced.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigAdvanced.cs), [Next Gen: AdvancedPage.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml).

**Completion:** implement a user-started, visible, stoppable support connection only if the intended endpoint/protocol and support workflow are still wanted. A historical service URL should not become a new default without checking its current ownership and compatibility.

#### AD-12 — Onboard scripting REPL — P2/P3, missing firmware-dependent tool

The original provides an interactive AP_REPL client with command input/history and asynchronous responses. Next Gen has no equivalent onboard console. Desktop scripts/log analysis are different capabilities. Evidence: [Original: ConfigREPL.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigREPL.cs), [Next Gen: AdvancedPage.axaml](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml).

**Completion:** discover scripting/REPL capability, provide command history/output and explicit session lifecycle, and report unsupported firmware. The original REPL screenshot itself shows a startup failure, so this cannot be claimed operational on the captured controller merely because the old page exists.

#### AD-13 — Legacy serial CLI Terminal — P3, legacy-support decision

The original exposes a Terminal page; Next Gen does not provide that historical serial CLI workflow. Modern firmware support must be checked independently, and the old screenshot itself describes legacy limitations. Evidence: [Original: InitialSetup.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/InitialSetup.cs) (Terminal registration) and the [Advanced screenshots](Advanced/).

**Completion if retained:** a separate legacy serial-console tool with explicit connection ownership and unsupported-firmware guidance. Otherwise document it as intentionally retired. This should not block current ArduPilot desktop support.

## Findings that should not become “missing feature” tickets

- **PX4Flow:** real image-stream/focus-mode code now exists in [PX4FlowViewModel](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/PX4FlowViewModel.cs) and [PX4FlowImageAssembler](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/Setup/OptionalHardware/PX4FlowImageAssembler.cs). Hardware qualification remains, but the old “not implemented” statement is stale.
- **FFT and spectrogram:** implemented in the analysis areas, including [FftAnalysisViewModel](C:/Projects/MissionPlanner/src/UI/MissionPlanner.App/Views/Analysis/FftAnalysisViewModel.cs). The missing part is logging setup, OH-15.
- **Flight-mode assignment, radio calibration, DSM binding, servo output, serial settings, motor tests and geofence editing:** implementations exist. Their narrower gaps are listed above.
- **Full parameter access:** many settings absent from a dedicated page can still be edited in the general editor. This is a workflow distinction, not full absence of the setting.
- **Planner / Preferences:** navigation relocation is valid. Runtime speech is the confirmed gap; a blanket “Planner missing” label would be wrong.
- **CubeLAN:** the actual codec/DEVICE_OP adapter is implemented. Compare [Original: example23-switch.cs](C:/Temp/MissionPlanner-v.1.38/src/Plugins/example23-switch.cs) with [Next Gen: CubeLanDeviceAdapter.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/VendorDevices/CubeLan/CubeLanDeviceAdapter.cs) and [Next Gen: CubeLanConfigurationCodec.cs](C:/Projects/MissionPlanner/src/Core/MissionPlanner.Core/ConfigTuning/VendorDevices/CubeLan/CubeLanConfigurationCodec.cs). PoE or authentication features not implemented in this original plugin are not parity requirements just because they would be useful.
- **ADS-B flight-ID/registration buttons:** some apparent original handlers are commented out in [Original: ConfigADSB.cs](C:/Temp/MissionPlanner-v.1.38/src/GCSViews/ConfigurationView/ConfigADSB.cs). A button label is not proof of a functioning original capability.
- **OSD font upload:** not established as an original feature of the reviewed page, so it is not included as a confirmed missing task.
- **Blank hardware pages:** absent firmware parameters, disabled backends, lack of a peripheral or no active operation can legitimately produce an empty view. These require capability/availability explanations, not invented parameter writes.

## Verification follow-ups, separate from confirmed feature gaps

| Observation | What is established | Next check |
|---|---|---|
| Extended Tuning expands without fields | Source wiring and screenshot agree; CF-01 is a confirmed defect. | Verify normal expansion and search paths after repair. |
| Traffic Inspector selected-message details remained blank | Recorded in capture manifest; a details implementation exists. | Verify row selection, Selected binding and message-detail refresh using recorded traffic. Do not label all inspector decoding missing. |
| CubeLAN status requested a vehicle while the app showed a connection | Screenshot symptom; a real device adapter exists, and no supported switch was demonstrated. | Check active-vehicle propagation before and after refresh, then test with a supported switch. |
| Hardware ID includes `MOT_IDLE_SEC` | Screenshot shows a non-device setting among hardware identifiers. | Tighten device-ID parameter matching; retain raw identifiers without decoding unrelated `_ID` substrings. |
| Repeated rangefinder labels and clipped servo index labels | Visible presentation limitations. | Include sensor instance/parameter name and allow sufficient label width. |
| Status/footer text sometimes describes the previously visited workflow | Several captures show unrelated prior status text. | Check status ownership/clearing on navigation; distinguish this from unavailable hardware. |
| Airspeed, CAN GPS, optical flow, ADS-B or proximity show no data | The quad does not establish the necessary capability/data stream for every page. | Use representative parameter/telemetry fixtures, then supported peripherals. |
| Original Failsafe image is only a dialog; REPL image reports failure | Screenshot evidence is incomplete or unsuccessful. | Use the original source as capability evidence and test actual firmware support separately. |

For implementation acceptance, use focused unit/protocol tests for codecs and parameter mappings, UI tests for navigation/materialization/selection, and hardware tests for the actual peripheral workflows. Windows, Debian Linux and macOS support should be considered when adding desktop adapters, especially joystick, serial-radio and speech support. This review does not claim those hardware operations have already passed on all three platforms.

## Scope decisions to preserve

The inventory includes original pages hidden on the captured quad, but not unrelated mission planning, flight-data, firmware-installation, map, simulation or log-analysis parity except where a reviewed page links to them. It covers reachable page workflows rather than every inactive/commented source fragment or every third-party plugin.

Keep the modern UI organization. Existing review/apply/readback behavior, cancellation and operation gating should carry over to any completed workflow. When implementing asynchronous UI actions, use cancellable async commands where possible and reserve `SafeFireAndForget` for unavoidable fire-and-forget paths, consistent with the project preference.

Legacy CLI, non-ArduPilot/Ateryx support, raw mirror return traffic and historical remote-support endpoints need explicit product decisions. None should be silently expanded or resurrected merely to make a parity table green. Previously postponed unrelated work is not reopened by this report.

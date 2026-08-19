# Optional Hardware

The NextGen Optional Hardware page is a stable, capability-driven tab workspace. The tab order is fixed by `OptionalHardwareTabCatalog`; headers become visible only when their connection, firmware-family, and parameter-presence rules are satisfied. A vehicle change recomputes the catalog and invalidates the previous connection cancellation boundary.

## Architecture and policies

- Views are static XAML lifecycle content. View models are transient and release subscriptions, devices, credentials, and active operations on deactivation.
- Vehicle parameter pages use parameters actually reported by the connected component and explicit Apply operations with readback confirmation.
- Direct serial tools use exclusive sessions and reject the serial port carrying the active MAVLink connection.
- Commands preserve active system/component, DroneCAN node, or external-device identity. Device and vehicle transports are not conflated.
- Dangerous output is disabled by default. Motor, calibration, joystick, tracker, and updater paths require a suitable operation gate/adapter before commands can be emitted.
- Password/PIN fields are masked, cleared at lifecycle end, and never included in diagnostics. NTRIP credentials are kept only for the active session.
- DataFlash Logs owns log acquisition. FFT accepts existing sample/log artifacts and does not introduce a second download path.

Offline tools are RTK source setup, SiK radio, DroneCAN direct-adapter selection, joystick local setup, and classic serial Bluetooth setup. Their platform adapters report unsupported status explicitly when absent.

## Classic migration and verification

| Classic capability | NextGen location | Availability rule / intentional change | Automated coverage | Hardware verification still required |
|---|---|---|---|---|
| RTK/GPS Inject | RTK / GPS Inject | Offline source; forwards only to active vehicle | RTCM framing, CRC, fragmentation | NTRIP caster and serial receiver |
| CubeID Update | CubeID Update | Connected target component; local image inspection and typed codec boundary | CRC/chunk offsets | CubeID bootloader/update acknowledgements |
| SiK Radio | SiK Radio | Offline exclusive serial | AT parsing and session ownership | Representative SiK firmware |
| CAN GPS Order | CAN GPS Order | Reported GPS CAN parameters | Catalog/parameter tests | Dual CAN GPS |
| Battery Monitor 1/2 | Battery Monitors | Merged instances from BATT/BATT2 metadata | Read/write service tests | Analog and CAN battery monitors |
| DroneCAN/UAVCAN | DroneCAN / UAVCAN | Explicit MAVLink tunnel or direct SLCAN adapter; v0 only | Transport isolation and node identity | Install/validate platform adapters and real bus |
| Joystick | Joystick | Local setup offline; vehicle output session-disabled until MANUAL_CONTROL adapter exists | Calibration/dead-zone/platform state | Platform input and flight-safe output adapter |
| Compass/Motor Calib | Compass / Motor Calibration | Supported vehicle families only | State/cancellation tests | Props-off vehicle test |
| Range Finder | Rangefinder | Reported RNGFND parameters | Parameter module tests | Representative sensor buses |
| Airspeed | Airspeed | Reported ARSPD parameters | Parameter module tests | Analog and digital sensors |
| PX4Flow / Optical Flow | Optical Flow | Combined metadata setup and calibration | Calibration/parser tests | PX4Flow and other flow devices |
| OSD | Onboard OSD bridge | Reuses the existing Config/Tuning OSD subsystem | Navigation bridge tests | Render/upload on vehicle |
| Camera Gimbal | Camera / Gimbal | CAM/MNT/GMBL metadata; payload operation stays in Flight Data | Module tests | Gimbal/camera protocols |
| Motor Test | Motor Test | Frame-aware, gated, explicit stop | Layout and command tests | Props-off each vehicle family |
| Bluetooth Setup | Bluetooth Setup | Offline classic serial AT modules, not BLE | Dialect/redaction tests | HC-05/HC-06 variants |
| Parachute | Parachute | Reported CHUTE parameters | Parameter module tests | Safe bench verification |
| ESP8266 Setup | ESP8266 Setup | Explicit MAV_COMP_ID_UDP_BRIDGE target; packed strings isolated | Packed string/redaction tests | Component discovery and read/write adapter |
| Antenna Tracker | Antenna Tracker | AntennaTracker firmware plus reported servo/PID settings; operation is separate | Capability catalog tests | Tracker actuator adapter and operation gate |
| FFT Setup | FFT Setup | Reported FFT/INS_LOG_BAT parameters; consumes existing artifacts | Synthetic known-frequency tests | DataFlash format integration |

## Test strategy

Pure parameter, protocol, framing, calibration, identity, cancellation, and signal-processing behavior is automated in `MissionPlanner.Core.Tests`. Hardware-dependent checks remain explicit in the final column above rather than being represented by skipped or fake unit tests. Representative SITL checks should cover Copter, Plane, Rover, AntennaTracker, disconnect/reconnect, and selection fallback before release.

The older Mandatory Hardware `OptionalHardwareSetupView` remains an internal generic parameter-module inspector. It is not registered as the user-facing Optional Hardware navigation destination; the catalog workspace above is the sole top-level entry.

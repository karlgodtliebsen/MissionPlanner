# Optional Hardware Scope / Migration Matrix

This matrix describes the intended NextGen destination. It is not a demand for pixel-for-pixel classic parity.

| Classic item | NextGen direction | Availability |
|---|---|---|
| RTK/GPS Inject | New dedicated RTCM/NTRIP workspace | Source setup may exist offline; injection requires active target |
| CubeID Update | New CubePilot component firmware updater | Only when the relevant component/capability is detected |
| SiK Radio | New external serial-radio utility | May work without a vehicle connection |
| CAN GPS Order | New parameter-backed tab | Active vehicle + relevant GPS/CAN parameters |
| Battery Monitor / Battery Monitor 2 | **One** multi-instance Battery Monitors tab using existing battery service | Active vehicle + battery parameters |
| DroneCAN/UAVCAN | New dedicated DroneCAN workspace | Direct adapter mode can be standalone; MAVLink tunnel requires active vehicle |
| Joystick | New cross-platform joystick setup/input workspace | Device setup offline; vehicle output requires active target |
| Compass/Motor Calib | New safety-gated CompassMot workflow | Active supported vehicle |
| Range Finder | Rich view over existing Rangefinder module/service | Active vehicle + RNGFND parameters |
| Airspeed | Rich view over existing Airspeed module/service | Active vehicle + ARSPD parameters |
| PX4Flow | Fold sensor setup into Optical Flow; retain focus/calibration utility when supported | Active supported vehicle |
| Optical Flow | New parameter-backed Optical Flow tab | Active vehicle + FLOW parameters |
| OSD | Reuse/link existing NextGen Onboard OSD subsystem | Active vehicle + OSD parameters |
| Camera Gimbal | Reuse current payload protocol services; add configuration projection | Active vehicle + camera/mount capability/parameters |
| Motor Test | New frame-aware Optional Hardware tab using existing ActuatorTestService | Active supported vehicle; disarmed |
| Bluetooth Setup | New external serial AT utility | Standalone serial device |
| Parachute | New parameter-backed tab | Active vehicle + CHUTE parameters |
| ESP8266 Setup | New MAVLink component-targeted parameter editor | Active UDP-bridge component |
| Antenna Tracker | New tracker configuration/control workspace | Tracker-specific capability; some setup can be offline |
| FFT Setup | New parameter setup + link to log/FFT analysis | Active vehicle + FFT/log parameters |

## Deliberate improvements over classic Mission Planner

1. Do not duplicate Battery 1 and Battery 2 pages; discover all available battery instances.
2. Do not show a tab merely because classic Mission Planner showed its menu entry. Show it when its real capability/source is available.
3. OSD and Camera/Gimbal should reuse existing NextGen subsystems rather than creating second implementations.
4. Motor tests must be derived from the connected vehicle frame and must distinguish motor **number/output** from motor **test order**.
5. Parameter-backed pages should use metadata and confirmed writes instead of hard-coded WinForms control ranges where metadata is available.
6. Large specialty tools (RTK, DroneCAN, SiK, Joystick) must have their own services and lifecycle boundaries rather than being embedded as code-behind utilities.

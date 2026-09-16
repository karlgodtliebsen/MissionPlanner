# Compass hardware diagnostics

The existing Compass Setup view includes a read-only configured-versus-detected
section. It shows COMPASS_ENABLE, COMPASS_USE/USE2/USE3, all three device IDs,
external flags, and EK3_SRC1/2/3_YAW values. Zero device ID means no reported
device; missing parameters remain unknown. Refresh explicitly requests these
configuration fields, including currently absent entries.

The summary separates Configured, Detected, Required, and Healthy. A required but
missing configured sensor is an error. A disabled compass with known non-compass
yaw sources is OK for that configuration. A device ID alone is not proof of health:
positive health needs fresh SYS_STATUS magnetometer evidence. Per-instance health
remains unknown because the aggregate bitmap cannot identify which compass is bad.

Required means required by at least one configured EKF3 yaw source set, including
GPS-with-compass fallback; it does not assert which source set is currently active.
Unknown or unrecognized yaw values cannot establish a compassless configuration.

Device decoding follows ArduPilot's 24-bit layout: 3-bit bus type, 5-bit bus number,
8-bit bus address, 8-bit device-class type. Raw IDs remain visible. External status
comes from the explicit compass parameter, not a guessed device-ID bit.

Primary source references checked 2026-09-16:
- [ArduPilot AP_HAL Device identity](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_HAL/Device.h)
- [ArduPilot EKF source definitions](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_NavEKF/AP_NavEKF_Source.cpp)

Six focused tests cover healthy/missing/compassless configurations, zero and missing
IDs, raw layout decoding, and a disabled detected device that cannot satisfy a
missing required sensor. No configuration parameters are written by diagnostics.

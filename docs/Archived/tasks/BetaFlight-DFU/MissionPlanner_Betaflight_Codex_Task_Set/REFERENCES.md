# References

Verified while preparing this task set on **2026-09-08**.

## MissionPlanner

Repository:

```text
karlgodtliebsen/MissionPlanner
branch: main
```

Current anchors to re-check before implementation:

```text
src/Core/MissionPlanner.Firmware/Configuration/FirmwareConfigurator.cs
src/Core/MissionPlanner.Firmware/FirmwareFamily.cs
src/Core/MissionPlanner.Firmware/Entry/
src/Core/MissionPlanner.Firmware/Discovery/
src/Core/MissionPlanner.Firmware/Devices/
src/Core/MissionPlanner.Firmware/Connected/
src/Core/MissionPlanner.Firmware/Dfu/
src/Core/MissionPlanner.Firmware/Installation/
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

The current firmware subsystem already contains STM32 DFU discovery/monitoring/backends, bootloader-entry strategies, ArduPilot firmware selection, installation and recovery. Reuse them.

## Betaflight upstream

MSP implementation:

```text
https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp.c
```

MSP protocol definitions:

```text
https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol.h
```

MSP documentation:

```text
https://betaflight.com/docs/development/API/MSP-Extensions
```

USB/DFU flashing:

```text
https://betaflight.com/docs/wiki/guides/current/USB-Flashing
```

Current implementation facts to verify again when coding:

- Betaflight identifies itself via `MSP_FC_VARIANT` as `BTFL`.
- `MSP_REBOOT` supports reboot to the MCU ROM bootloader.
- current ROM-bootloader mode is `1`;
- Betaflight's current implementation calls `systemResetToBootloader(BOOTLOADER_REQUEST_ROM)`;
- reboot processing is protected against the armed state;
- STM32 ROM DFU normally enumerates as ST `0483:DF11`.

If current upstream source disagrees with a copied numeric value in these tasks, **current upstream source wins**.

Use named constants/enums and exact protocol tests so such changes remain localized.

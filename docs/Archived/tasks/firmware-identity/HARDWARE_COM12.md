# COM12 identity inspection

```text
Evidence: identity-com12-inspect.log
DEVICE COM12 52003D001651353232323938 UsbIdentifier { VendorId = 4617, ProductId = 22337 }
CONNECTED VehicleConnectionResult { Success = True, VehicleId = 1:1, ConnectionSession = MissionPlanner.Core.Vehicles.VehicleConnectionSession, ErrorMessage = , ConnectionId = 8e6a4bec-f88d-4629-9c1b-616f9051a0b2 }
IDENTITY VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 63983, BoardVersion = 8781824, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 52003d001651353232323938000000000000 }
ARMED False
BANNER Config Error: fix problem then reboot
BANNER Config Error: INS: unable to initialise driver
BANNER Config Error: fix problem then reboot
BANNER Config Error: INS: unable to initialise driver
BANNER ArduCopter V4.7.1 (dbe79216)
BANNER ChibiOS: d103983f
BANNER speedybeef4 003D0052 32355116 38393232
BANNER RCOut: Initialising
BANNER Frame: QUAD/X
SELECTED omnibusf4 4.7.1 board 1002 https://firmware.ardupilot.org/Copter/stable-4.7.1/omnibusf4/arducopter.apj
EMBEDDED SelectedFirmwareIdentity { Target = omnibusf4, BoardId = 1002, VehicleType = Copter, Version = 4.7.1, GitHash = dbe79216, McuFamily = STM32F405xx, Source = ApjManifest, Uri =  }
INSPECT ENTRY BootloaderIdentified entry.temporary-mavlink-reboot-sent
BOOTLOADER BootloaderIdentity { Target = , Source = BootloaderProtocol, BoardId = 134, BootloaderRevision = 5, FlashSize = 983040, BoardRevision = 0, ExternalFlashSize = 0, ChipDescription = STM32F40x,?, IsSecure =  }
RECOVERY DECISION PhysicalDeviceMismatch: Observed MCU differs from the APJ MCU requirement.
Running firmware [AUTOPILOT_VERSION/STATUSTEXT]: speedybeef4 / 134; 4.7.1
Selected firmware [OfficialCatalogue]: omnibusf4 / 1002; Copter; 4.7.1
Bootloader [BootloaderProtocol]: target not reported / 134; revision 5
EXISTING APPLICATION REBOOT REQUESTED; NO ERASE OR PROGRAM
Evidence: identity-com12-after-inspection.log
DEVICE COM12 52003D001651353232323938 UsbIdentifier { VendorId = 4617, ProductId = 22337 }
CONNECTED VehicleConnectionResult { Success = True, VehicleId = 1:1, ConnectionSession = MissionPlanner.Core.Vehicles.VehicleConnectionSession, ErrorMessage = , ConnectionId = bd6b9447-6b98-4855-b2c7-311b9171a80c }
IDENTITY VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 63983, BoardVersion = 8781824, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 52003d001651353232323938000000000000 }
ARMED False
BANNER ArduCopter V4.7.1 (dbe79216)
BANNER ChibiOS: d103983f
BANNER speedybeef4 003D0052 32355116 38393232
BANNER RCOut: Initialising
BANNER Frame: QUAD/X
```

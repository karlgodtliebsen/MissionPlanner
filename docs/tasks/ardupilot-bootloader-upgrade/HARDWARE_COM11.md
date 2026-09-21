# COM11 hardware evidence

```text
Evidence file: upgrade-bench-com11-install.log
DEVICE COM11 5B002E000951353332343134 UsbIdentifier { VendorId = 4617, ProductId = 22337 }
CONNECTED VehicleConnectionResult { Success = True, VehicleId = 1:1, ConnectionSession = MissionPlanner.Core.Vehicles.VehicleConnectionSession, ErrorMessage = , ConnectionId = c5b767fc-3cc5-4929-8343-e9f7e09086bc }
IDENTITY VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 0, ReleaseType = Official }, FlightGitHash = 3135313166323731, Capabilities = 64495, BoardVersion = 65667072, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 5b002e000951353332343134000000000000 }
ARMED False
BANNER ArduCopter V4.7.0 (1511f271)
BANNER ChibiOS: 4f34e217
BANNER omnibusf4 002E005B 33355109 34313432
BANNER RCOut: PWM:1-6
BANNER IMU0: fast sampling enabled 8.0kHz/1.0kHz
BANNER Frame: QUAD/X
SELECTED omnibusf4 4.7.1 board 1002 https://firmware.ardupilot.org/Copter/stable-4.7.1/omnibusf4/arducopter.apj
PROGRESS FirmwareProgress { State = ValidatingPackage, Percentage = , MessageCode = installation.package-validated, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForDevice, Percentage = , MessageCode = installation.waiting-for-device, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = EnteringBootloader, Percentage = , MessageCode = installation.entering-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = CheckingForBootloader, Percentage = , MessageCode = entry.checking-for-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = RequestingBootloaderReboot, Percentage = , MessageCode = entry.requesting-bootloader-reboot, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForBootloader, Percentage = , MessageCode = entry.waiting-for-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = IdentifyingBootloader, Percentage = , MessageCode = installation.bootloader-identified, CompletedBytes = , TotalBytes = , TechnicalDetail = Device: COM11; board ID: 1002; bootloader revision: 5 }
PROGRESS FirmwareProgress { State = CheckingCompatibility, Percentage = , MessageCode = installation.checking-compatibility, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Erasing, Percentage = , MessageCode = installation.erasing, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Programming, Percentage = , MessageCode = installation.programming, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Verifying, Percentage = , MessageCode = installation.verifying, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Rebooting, Percentage = , MessageCode = installation.rebooting, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForApplication, Percentage = , MessageCode = installation.waiting-for-application, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForApplication, Percentage = , MessageCode = installation.verifying-running-firmware, CompletedBytes = , TotalBytes = , TechnicalDetail = Reconnecting on COM11 and verifying 4.7.1. }
PROGRESS FirmwareProgress { State = Failed, Percentage = , MessageCode = installation.verification-failed, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
RESULT Failed FirmwareOperationFailure { Code = installation.verification-failed, Stage = WaitingForApplication, TechnicalDetail = The flashed controller did not reconnect with a heartbeat., ExceptionType = FirmwareVerificationException } 
Operation: 71ae847e-976d-4064-9c14-ddea43e7ad93
State: Failed
Firmware source: official/catalogue (prepared package)
Firmware board ID: 1002
Detected board ID: 1002
Bootloader revision: 5
Original device: USB\VID_1209&PID_5741\5B002E000951353332343134
Bootloader device: USB\VID_1209&PID_5741\5B002E000951353332343134
Bytes programmed: 873508
Verification: Succeeded
Failure: installation.verification-failed
Failure stage: WaitingForApplication
Failure detail: The flashed controller did not reconnect with a heartbeat.
Elapsed: 00:00:21.2301460
Evidence file: upgrade-bench-com11-postprobe.log
DEVICE COM11 5B002E000951353332343134 UsbIdentifier { VendorId = 4617, ProductId = 22337 }
CONNECTED VehicleConnectionResult { Success = True, VehicleId = 1:1, ConnectionSession = MissionPlanner.Core.Vehicles.VehicleConnectionSession, ErrorMessage = , ConnectionId = 0699183f-b795-45de-b258-1f0854518e11 }
IDENTITY VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 64495, BoardVersion = 65667072, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 5b002e000951353332343134000000000000 }
ARMED False
BANNER ArduCopter V4.7.1 (dbe79216)
BANNER ChibiOS: d103983f
BANNER omnibusf4 002E005B 33355109 34313432
BANNER RCOut: PWM:1-6
BANNER IMU0: fast sampling enabled 8.0kHz/1.0kHz
BANNER Frame: QUAD/X
Evidence file: upgrade-bench-com11-retry.log
DEVICE COM11 5B002E000951353332343134 UsbIdentifier { VendorId = 4617, ProductId = 22337 }
CONNECTED VehicleConnectionResult { Success = True, VehicleId = 1:1, ConnectionSession = MissionPlanner.Core.Vehicles.VehicleConnectionSession, ErrorMessage = , ConnectionId = 2cecc8d2-453a-4e12-b826-cd19c531b457 }
IDENTITY VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 64495, BoardVersion = 65667072, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 5b002e000951353332343134000000000000 }
ARMED False
BANNER ArduCopter V4.7.1 (dbe79216)
BANNER ChibiOS: d103983f
BANNER omnibusf4 002E005B 33355109 34313432
BANNER RCOut: PWM:1-6
BANNER IMU0: fast sampling enabled 8.0kHz/1.0kHz
BANNER Frame: QUAD/X
SELECTED omnibusf4 4.7.1 board 1002 https://firmware.ardupilot.org/Copter/stable-4.7.1/omnibusf4/arducopter.apj
PROGRESS FirmwareProgress { State = ValidatingPackage, Percentage = , MessageCode = installation.package-validated, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForDevice, Percentage = , MessageCode = installation.waiting-for-device, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = EnteringBootloader, Percentage = , MessageCode = installation.entering-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = CheckingForBootloader, Percentage = , MessageCode = entry.checking-for-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = RequestingBootloaderReboot, Percentage = , MessageCode = entry.requesting-bootloader-reboot, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForBootloader, Percentage = , MessageCode = entry.waiting-for-bootloader, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = IdentifyingBootloader, Percentage = , MessageCode = installation.bootloader-identified, CompletedBytes = , TotalBytes = , TechnicalDetail = Device: COM11; board ID: 1002; bootloader revision: 5 }
PROGRESS FirmwareProgress { State = CheckingCompatibility, Percentage = , MessageCode = installation.checking-compatibility, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Erasing, Percentage = , MessageCode = installation.erasing, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Programming, Percentage = , MessageCode = installation.programming, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Verifying, Percentage = , MessageCode = installation.verifying, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = Rebooting, Percentage = , MessageCode = installation.rebooting, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForApplication, Percentage = , MessageCode = installation.waiting-for-application, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
PROGRESS FirmwareProgress { State = WaitingForApplication, Percentage = , MessageCode = installation.verifying-running-firmware, CompletedBytes = , TotalBytes = , TechnicalDetail = Reconnecting on COM11 and verifying 4.7.1. }
PROGRESS FirmwareProgress { State = Completed, Percentage = , MessageCode = installation.completed, CompletedBytes = , TotalBytes = , TechnicalDetail =  }
RESULT Completed  VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 64495, BoardVersion = 65667072, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 5b002e000951353332343134000000000000 }
Operation: 34b1e43f-e15b-4b09-bbef-e7e7f6ee68d1
State: Completed
Firmware source: official/catalogue (prepared package)
Firmware board ID: 1002
Detected board ID: 1002
Bootloader revision: 5
Original device: USB\VID_1209&PID_5741\5B002E000951353332343134
Bootloader device: USB\VID_1209&PID_5741\5B002E000951353332343134
Application device: USB\VID_1209&PID_5741\5B002E000951353332343134
Bytes programmed: 873508
Verification: Succeeded
Installed firmware: VehicleFirmwareIdentity { Family = ArduCopter, MavType = 2, Autopilot = 3, FlightVersion = FirmwareSemanticVersion { Major = 4, Minor = 7, Patch = 1, ReleaseType = Official }, FlightGitHash = 6462653739323136, Capabilities = 64495, BoardVersion = 65667072, VendorId = 4617, ProductId = 22337, HardwareUid = , HardwareUid2 = 5b002e000951353332343134000000000000 }
Elapsed: 00:00:23.3126060

```

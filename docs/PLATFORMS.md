# Platforms

MissionPlanner Next Gen is a desktop application. The shared code (`Core/*`, `MissionPlanner.App`,
`MissionPlanner.Themes`) targets plain `net10.0`; each operating system adds a small platform library and
an application head.

| Platform | Head | Platform library | Status |
|---|---|---|---|
| Windows | `Platforms/MissionPlanner.Desktop` | `UI/MissionPlanner.Library.Windows` | Supported, tested |
| Linux | `Platforms/MissionPlanner.Linux` | `UI/MissionPlanner.Library.Linux` | Builds in CI; started and run for 25 s under WSLg (Debian 13); 1,492 of the 1,493 portable unit tests that ran pass there; not yet used with a vehicle |
| macOS | `Platforms/MissionPlanner.MacOs` | `UI/MissionPlanner.Library.MacOs` | Builds and publishes; **never run** (no Mac available) |
| Browser | `Platforms/MissionPlanner.Browser` | `UI/MissionPlanner.Library.Browser` | UDP through the local bridge only |
| Android, iOS | `Platforms/MissionPlanner.Android`, `.iOS` | none | Template heads that compile; **not functional** (see below) |

`UI/MissionPlanner.Library.Desktop` holds what every desktop platform shares: serial/network output,
warning-rule storage and offline MBTiles map support. A platform library only adds what differs:
secret storage, location, and Avalonia backend selection.

## Building and running

```bash
# Linux (x64 or arm64)
dotnet publish src/Platforms/MissionPlanner.Linux -c Release -r linux-x64 --self-contained -o out/linux-x64
./out/linux-x64/MissionPlanner

# macOS (Apple silicon or Intel); run the bundle script on a Mac
dotnet publish src/Platforms/MissionPlanner.MacOs -c Release -r osx-arm64 --self-contained -o out/osx-arm64
scripts/create-macos-bundle.sh out/osx-arm64 out 2.0.0
```

Any platform's head can be built from any host with `dotnet build`; only the bundle script needs macOS.
`src/Platforms/MissionPlanner.Linux/missionplanner.desktop` is a starting point for a menu entry.

## Linux notes

- **Serial ports.** Flight controllers appear as `/dev/ttyACM*` or `/dev/ttyUSB*`. Add yourself to the
  group that owns them, then sign out and in again: `sudo usermod -aG dialout $USER`.
- **ModemManager** probes new USB serial devices and can hold a flight controller for several seconds after
  it is plugged in or reboots. If connections fail intermittently, stop it
  (`sudo systemctl disable --now ModemManager`) or add a udev rule that ignores ArduPilot boards
  (`ENV{ID_MM_DEVICE_IGNORE}="1"`).
- **Secrets** (MAVLink signing keys, map provider keys) use the desktop keyring through `secret-tool`
  (`sudo apt install libsecret-tools`) when it is installed. Without it they are kept in memory for the
  session only, and the Advanced tools report that secure key storage is unavailable.
- **Runtime libraries.** The self-contained build needs the usual desktop libraries
  (`libfontconfig1`, `libx11-6`, `libice6`, `libsm6`, `libicu`). Wayland sessions run through XWayland.

## macOS notes

- The bundle is ad-hoc signed, not notarized. On first start use *Open* from the context menu or run
  `xattr -dr com.apple.quarantine MissionPlanner.app`.
- Secrets use the login keychain through Security.framework (`MacOsKeychainSecretStore`). This code has
  not been run on a Mac. If it fails, the error is reported when a secret is saved.
- Serial devices appear as `/dev/cu.usbmodem*`; use the `cu` device for outgoing connections.

## What differs from Windows

| Feature | Linux / macOS |
|---|---|
| Serial connection, parameters, missions, MAVFTP, calibration, SITL | Same code as Windows |
| ArduPilot firmware installation over the serial bootloader | Same code as Windows |
| STM32 DFU installation (STM32CubeProgrammer) | Not available: the tool discovery and device catalog are Windows-only |
| Betaflight identity probing and DFU hand-off | Not available |
| USB topology matching (`IUsbTopologyProvider`) | Not available |
| Device location for the map | Not available; the map keeps its default position |
| Secret storage | Linux: keyring via `secret-tool`, else session only. macOS: keychain |

## Mobile heads

`MissionPlanner.Android` and `MissionPlanner.iOS` are the Avalonia templates. They compile (the namespace
clash with `MissionPlanner.App` was fixed), but they do not register any platform services, do not call
`MissionPlannerProgram.BuildAvaloniaApp`, and Android and iOS have no `System.IO.Ports`. They need a design
of their own (network/Bluetooth transports, file access, location) before they can run the application.

## Running the tests on Linux

The platform-neutral suites (`MissionPlanner.Tests`, `Analysis.Tests`, `Firmware.Tests`, `Core.Tests`) run
with `dotnet test` on Linux. `MissionPlanner.AvaloniaUI.Tests` targets Windows and is not run there.

- A checkout on Linux has LF line endings, which the "generated source is current" tests compare against.
  Copying a Windows working tree (CRLF) into Linux makes three of them fail; that is a copy artefact.
- `NetworkTransportTests.UdpFiltersOtherPeersRetainsLargeDatagramsAndHonorsCancellation` sends a 2,048-byte
  datagram over loopback. On WSL2 with `networkingMode=mirrored` every UDP datagram above 1,472 bytes
  (anything that needs IP fragmentation) is silently dropped on loopback, so the test times out there.
  It is an environment problem, not an application problem (MAVLink datagrams are below 300 bytes). Check
  it on a real Linux machine or in the `build-linux` CI job.

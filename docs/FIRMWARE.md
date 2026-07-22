# Firmware management

The Setup Firmware workflow is read-only by default and displays identity already captured
from `HEARTBEAT` and `AUTOPILOT_VERSION`. Discovery and download are enabled only for
manifest entries that match the reported firmware family, vendor ID, product ID, and board
version. A manifest-provided technical board target is required; UI labels and marketing
names are never used to choose a binary.

## Configuration

Firmware discovery uses the `Firmware` configuration section:

```json
{
  "Firmware": {
    "ManifestUrl": "https://example.invalid/missionplanner-firmware.json",
    "Releases": []
  }
}
```

`ManifestUrl` is optional but, when present, must be absolute HTTPS. Static `Releases` use
the same `FirmwareManifestEntry` schema and are useful for managed/offline deployments.
Every release contains a channel (`Stable`, `Beta`, or `Development`), firmware family,
technical board target, vendor/product IDs, optional exact board version (zero means every
revision for that vendor/product pair), version, HTTPS download URI, 64-character SHA-256,
release notes, and publication timestamp.

## Safety boundaries

1. `FirmwareManifestSelector` performs exact technical identity matching.
2. `FirmwarePackageManager` checks an existing cache entry or downloads over HTTPS, computes
   SHA-256, and refuses mismatches before caching.
3. `FirmwareUpdateCoordinator` refuses flashing without a verified package and confirmed
   parameter backup.
4. Normal MAVLink must disconnect before an adapter is invoked.
5. A successful adapter result enters `WaitingForReconnect`; the workflow completes only
   when the same vehicle identity reconnects.
6. `UnsupportedFirmwareFlashingService` is the default. Platform bootloader support must be
   supplied through `IFirmwareFlashingService`; it must validate package/board compatibility
   again and must not place serial bootloader logic in a view model.

After flashing, reconnect, verify the newly reported identity, and restore only reviewed
parameters from the pre-flash backup.

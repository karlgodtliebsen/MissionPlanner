# Logging

Telemetry recordings and application diagnostics are separate logical storage areas.
Telemetry uses classic MAVLink tlog records; application diagnostics use Serilog.

## Storage

Desktop storage resolves lazily to `<Documents>/Mission Planner/logs`.
Telemetry files live directly in that directory; application files live in
`application`. If Documents is unavailable or unwritable, local application data
and then the user profile are tried. Failure to find a writable location produces
an explicit diagnostic. Paths use platform-native separators.

`ILogStorage` owns file creation, listing, read, deletion, and export streams.
Identifiers must be single file names. Existing files are never overwritten by
creation. Active writers prevent deletion. ViewModels use these streams and the
existing platform file picker/save services rather than filesystem calls.
`ILogPathProvider` is available only on desktop.

Browser storage is session-only, with a default limit of 32 MiB and 1,024 files.
Quota exhaustion fails the write; it never silently evicts a recording. Export
logs before closing or reloading the browser. Deleting a closed log reclaims its
quota. Browser metadata contains no physical path.

## Implementation sequence

1. Storage contracts and platform implementations: implemented.
2. Classic tlog recorder integration: implemented.
3. Structured application logging and runtime level control: implemented.
4. Logs root navigation: implemented.
5. Telemetry viewer: implemented.
6. Application viewer: pending.
7. Integration, health, retention, and final verification: pending.


## Telemetry format and lifecycle

The connection owns a bounded 4,096-frame recording queue. Incoming frames are captured before message decoding; outgoing commands are excluded. Unsupported dialect candidates retain their bytes even when their CRC extra is unknown. Known frames with invalid or replayed signatures and SETUP_SIGNING key material are excluded.

Each record contains an unsigned 64-bit UTC Unix microsecond timestamp in big-endian order, immediately followed by the original MAVLink frame including any v2 signature. There is no header or direction byte. No metadata sidecar is required. Names use `yyyy-MM-dd HH-mm-ss.tlog`, with `-1`, `-2`, etc. on collision. First reception creates the file; empty connections do not.

Disconnect and connection disposal drain the queue and close the stream. Storage failures and dropped frames report errors without stopping the vehicle connection. Recordings are never automatically deleted.

Validation: storage tests (16) and recorder/onboard-status tests (18) passed on Windows. The browser library build passed. Binary tests cover v1, v2, signed v2, unknown IDs, timestamp endianness, receive-only capture, and disconnect draining.


## Application diagnostics

Serilog still reads appsettings. The File sink's `{ApplicationLogPath}` token resolves to `application/MissionPlanner.NextGen.Application-.log`; Serilog appends the rolling date. Rolling interval, file-size limit, and retention stay in appsettings. Browser configuration removes File sinks and their assembly hints before constructing the logger.

`ApplicationLogBuffer` retains structured events (default 5,000, configurable through `ApplicationLogging:MemoryCapacity`) and discards the oldest when full. Snapshot sequence cursors and coalesced asynchronous notifications support batched viewers. Subscriber exceptions cannot escape into logging.

`IApplicationLogLevelController` changes the session's default Serilog level. Startup uses `Serilog:MinimumLevel:Default`; category overrides remain authoritative. Normal configuration uses Information with overrides for transport, protocol services, and EventHub. For development Verbose diagnostics, configure both the default and relevant category override to Verbose. The existing text output template remains the historical file format.

Validation: six application logging tests passed, covering concurrent bounded ordering, structured properties/exceptions, subscriber isolation, path resolution, browser exclusion, rolling settings, and runtime levels. Browser library compilation passed.


## Logs workspace

Open Logs from the root navigation menu. Telemetry is selected initially; the last selected section is retained for the session. The existing telemetry view moved out of Flight Data without duplication. Only the selected child is attached to the visual tree. Hidden telemetry views unsubscribe, while recording and replay services remain connection/session owned.

Validation: navigation section switching and selection retention passed. Shared desktop UI and browser library compilation passed; an interactive visual check remains part of final integration.


## Telemetry viewer

The Telemetry section lists stored recordings newest first and supports refresh, packet inspection, replay, import, export, delete, and desktop folder opening. Imports copy through the storage boundary and remove incomplete imports on failure. Packet inspection indexes timestamps and offsets, then decodes at most 200 rows per page (hard service limit 256) through the live decoder registry. The compact index grows with packet count; decoded messages and raw rows do not accumulate for the whole file.

Filter by text/hex, message ID/name, system, component, and maximum numeric MAV severity. Timestamp jumps use binary search. Follow replay advances packet pages with the replay clock. Replay retains its isolated pipeline and outbound-transmission guard. ACK rows expose command IDs for correlation; receive-only tlogs cannot establish a complete request/response history when requests were transmitted locally.

Optional `.tlog.meta.json` files may contain Started, Ended, Vehicle, and Firmware. Missing or malformed sidecars do not block browsing. Opening a log fills in its actual start and duration. Complete records before a truncated final record remain readable.

Validation: 20 packet-browser, recorder, and replay tests passed, including 100,000 synthetic records, bounded decoding, unknown bytes, filters, cancellation, partial records, and browser import/export without sidecars. Shared UI and browser library builds passed.

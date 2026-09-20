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
6. Application viewer: implemented.
7. Integration, health, retention, and final verification: implemented.


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

Validation: navigation section switching and selection retention passed. Shared desktop UI and browser library compilation passed; final browser rendering checks are documented below.


## Telemetry viewer

The Telemetry section lists stored recordings newest first and supports refresh, packet inspection, replay, import, export, delete, and desktop folder opening. Imports copy through the storage boundary and remove incomplete imports on failure. Packet inspection indexes timestamps and offsets, then decodes at most 200 rows per page (hard service limit 256) through the live decoder registry. The compact index grows with packet count; decoded messages and raw rows do not accumulate for the whole file.

Filter by text/hex, message ID/name, system, component, and maximum numeric MAV severity. Timestamp jumps use binary search. Follow replay advances packet pages with the replay clock. Replay retains its isolated pipeline and outbound-transmission guard. ACK rows expose command IDs for correlation; receive-only tlogs cannot establish a complete request/response history when requests were transmitted locally.

Optional `.tlog.meta.json` files may contain Started, Ended, Vehicle, and Firmware. Missing or malformed sidecars do not block browsing. Opening a log fills in its actual start and duration. Complete records before a truncated final record remain readable.

Validation: 20 packet-browser, recorder, and replay tests passed, including 100,000 synthetic records, bounded decoding, unknown bytes, filters, cancellation, partial records, and browser import/export without sidecars. Shared UI and browser library builds passed.


## Application viewer

Application displays structured current-session events directly from memory. Changes are coalesced into 250 ms UI batches. Pause freezes display only; Resume catches up to the retained buffer. Follow tail scrolls after each batch. Clear view advances a display cursor without clearing the sink or deleting files.

Display filters include minimum/exact level, source category, text, UTC time bounds, and exception-only. Quick filters use SourceContext for MAVLink, Transport, Parameters, and Firmware. Event details include the original template, full exception, and structured properties. Copy and export operate on selected events or the filtered view. Runtime logging level is separate from display filtering, and Verbose has an explicit indicator.

Desktop files can be refreshed, opened, exported, and deleted when old. The historical reader accepts the configured text template, including older rows without SourceContext, and retains the newest bounded window of events. It tolerates malformed trailing lines and retains exception continuation lines. All files in the current rolling interval are conservatively protected from deletion, including size rolls. The storage layer also rejects deletion while a writer owns the file. Browser hides desktop file controls and retains live viewing and explicit view export.

Validation: nine application logging/history tests and two viewer/navigation tests passed. The viewer test emits 10,000 events into a 100-event buffer and verifies batched resume, clear-view isolation, runtime levels, filtering, and hide/reopen behavior. Browser library build passed without warnings.

## Clipboard snapshots

Both log toolbars offer a complete snapshot as indented JSON. Application's existing
selected-event/filtered-view copy remains available. Its separate snapshot button copies
all retained live-buffer events, ignoring pause, clear-view and display filters, or all
loaded historical events when viewing a file. Exports include timestamps, templates,
rendered messages, severity, source context, full exception text and nested structured
properties. Historical snapshots remain limited to the loaded history window.

Telemetry's snapshot button copies every indexed packet from the selected recording,
ignoring the displayed page and filters. It includes recording metadata, index statistics,
packet timestamps, identities, decoded summaries, severity/command fields and complete raw
frame hex. Packets are newest first. A separate stream and bounded decode batches preserve
browser/replay cursors; a cancellable progress dialog covers indexing and copying.
The resulting JSON text occupies memory until clipboard submission. An actively growing
recording is represented only through the stream length indexed for this capture.

Both formats include a schema version, UTC capture time and scope. Formatting runs on
demand off the UI thread. No recording or logging settings are changed.

## Health, enrichment, and retention

Telemetry health reports Recording, Stopped, or Error, bytes written, and the UTC start time. The health label tooltip contains the current recording name. Application health reports the runtime minimum level, file logging state/current file, and memory count/capacity. `LoggingHealthService` exposes a lightweight combined snapshot without owning either pipeline.

The central receive loop adds a ConnectionId scope property. Verbose decoded-message events include Transport, MavLinkMessageId, SystemId, and ComponentId. Normal operation does not enable per-packet diagnostic logging. Browser enrichment excludes machine/process lookups.

Telemetry files are user data and are never automatically deleted. Application retention remains Serilog configuration: the supplied native configuration rolls daily and at 10 MiB, retaining seven files. Change these values in appsettings; runtime level changes last only for the session.

GCS telemetry recordings contain received MAVLink traffic. Next Gen application logs contain software diagnostics and exceptions. Vehicle onboard DataFlash/file logging is a separate firmware feature: its status appears beside PC recording status, and neither PC log proves that onboard logging is active.

The legacy LogDirectory preference no longer controls recording. Preferences points users to Logs and the platform storage policy. Native Windows and Linux paths use the same platform resolver; Browser session storage never resolves a desktop path. Serilog.Sinks.File is referenced only by native hosts, and Browser configuration removes file sinks even when supplied a native configuration. The Browser appsettings resource has an explicit manifest name matching startup.

## Final verification and limits

Integration tests cover the actual native Serilog configuration, resolved file creation, restart/history reading, Browser DI with native File settings, session recording/export/reimport, and serial/UDP/TCP connection pipelines using test transports. The latter verify recording before a blocked decoder and flushing on disconnect. Existing binary fixtures verify classic timestamp/frame layout, including signed frames and unknown IDs.

The full supported solution builds Desktop and Browser/WASM. Browser startup and both Logs sections were rendered in the in-app browser at 1280×720; Application showed a structured startup event and disabled file logging. This check exposed and fixed the embedded-settings resource name, missing navigation toggle, and insufficient packet-table height. Temporary startup routing used for the previews was restored to Flight Data.

Automated browser pointer/keyboard interaction with the Avalonia canvas could not be reliably exercised, so interactive import/export, navigation, and replay still need a manual UI check. Real serial hardware, an external classic Mission Planner application, Linux execution, and Android/iOS builds were not tested. Format compatibility is verified through binary fixtures and reader round trips. Historical JSON-formatted application logs are not supported by the text history reader.

Final full-suite run (2026-09-19): 1,229 .NET tests and seven JavaScript tests passed, with 30 existing skipped .NET tests. Results: TestResults/all-tests/20260919-010141-495.

Final solution build passed with zero errors and 17 existing nullable-analysis warnings; no XML documentation warnings were reported.

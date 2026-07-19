# MAVFTP implementation review — 2026-07-19

## Corrected defects

1. **UDP response correlation used object identity.** Each received UDP datagram creates a new
   `TransportEndPoint`; the dispatcher therefore rejected valid replies. `TransportEndPoint` now
   implements value equality and a matching hash code.
2. **FTP request sequence progression was incompatible with the protocol conversation.** The next
   request is now allocated after the latest server response sequence. Burst responses update the
   per-target sequence state for every packet; retries retain the same request sequence.
3. **Response queue overflow was silent.** Registrations now use a bounded wait-mode channel and
   overflow completes the operation with a logged protocol failure.
4. **Session cleanup could delay cancellation for the full normal retry period.** Cleanup now has
   separate short timeout and attempt options.
5. **Application progress mapping captured a synchronization context in Core.** It now uses a
   synchronous mapping adapter and leaves UI dispatch policy to the caller.
6. **The MAVFTP tab repeatedly selected the first registered vehicle.** It now remembers the most
   recently connected active vehicle and falls back only when that vehicle disappears.

## Verification still required

The environment used for this review does not contain the .NET SDK, so compilation and tests could
not be executed here. Run the repository build and test commands locally, then test against SITL:

1. Connect to SITL and refresh `/`.
2. Confirm an ACK/NAK is received without timeout.
3. List multiple directories.
4. Download a known multi-packet file and compare SHA-256.
5. Repeat the download to verify session cleanup and sequence continuity.
6. Cancel a transfer, then immediately refresh and download again.

## Remaining work

- Deterministic fake MAVFTP server and client/dispatcher integration tests.
- Automated opt-in ArduPilot SITL tests.
- Capability-state decoding (`MAV_PROTOCOL_CAPABILITY_FTP`) with observed-support fallback.
- Upload, write, rename, create/remove directory, remove file, and CRC operations.
- Direct destination streaming for very large files instead of cache-file then local copy.
- Move the concrete Core-to-MAVLink adapter if the project dependency policy requires strict
  infrastructure inversion.
- Packed parameter file download and decoding only after SITL download tests are stable.

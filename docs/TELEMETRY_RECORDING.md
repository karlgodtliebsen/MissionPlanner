# PC telemetry recording

Every live MAVLink connection starts PC recording before transport reception. The
connection's existing bounded inspection tap supplies exact inbound and successfully
sent outbound frames independently of domain handlers. Unknown-dialect candidates
are retained as diagnostic traffic. SETUP_SIGNING is excluded by the existing tap
because it contains secret key material.

Files use Mission Planner tlog framing: eight-byte unsigned big-endian Unix
microseconds followed immediately by one complete MAVLink 1 or 2 frame (including
signature bytes). Records preserve observer arrival order. Direction is not encoded
in this compatible format; source system/component IDs remain in each frame.
The existing TelemetryLogReader and read-only replay path can open these files.

Names are UTC yyyyMMdd-HHmmss-ffffff-NNN.tlog, created with CreateNew so reconnects
and concurrent connections never overwrite files. Preferences' Log directory is
read at connection start; empty uses LocalApplicationData/MissionPlanner/Telemetry.
No retention deletion is performed by this recorder.

Flight Data's Telemetry Logs tab displays **PC Telemetry Recording**, its exact
selectable file path, and any failure. Status describes the latest opened connection;
each concurrent connection still owns and finalizes its own file.
Vehicle LOG_BACKEND_TYPE and onboard/DataFlash logging do not control PC recording.

The observer queue is bounded to 4096 frames. Overflow marks the recording incomplete
and reports dropped frames without blocking vehicle communications. Disk errors
are isolated and visible. Disconnect/shutdown detaches the recording observer,
drains its queued frames, flushes, and closes the file. Finalization has a five-second
cancellation deadline; a failure remains visible as Error rather than Completed.

Validation: focused recording tests cover both directions, readable framing,
reconnect paths, graceful queue draining, invalid-directory isolation, and the
production MavLinkConnection start/send/stop lifecycle. Hardware recording and
interactive desktop verification have not been run.

## Vehicle onboard logging

Telemetry Logs separately projects the active vehicle's LOG_BACKEND_TYPE together
with SYS_STATUS logger-health evidence and retained logger STATUSTEXT. Backend zero
displays Disabled even when historical failure text remains visible. No backend
value is changed by diagnostics.

An ENOSPC message is retained separately from a later generic Logging failed
message. PreArm/Arm logger failures flag arming impact; later readiness, arming, or
reported healthy logger telemetry clears that impact. Disconnect resets retained
evidence. Positive logger-health telemetry clears the active storage failure.
Storage free space is explicitly unavailable: the current flight-controller
telemetry does not provide a reliable capacity value, and camera storage reports
are not presented as autopilot/DataFlash storage.

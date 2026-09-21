# Receiver binding

Radio Setup includes **Bind receiver**. The typed `IVehicleCommandService.StartReceiverBindAsync`
uses MAV_CMD_START_RX_PAIR (500), param1=1 (CRSF), remaining parameters zero, through the
existing safety policy, per-vehicle operation gate and command ACK tracker.

Availability requires a registered online ArduPilot vehicle, a fresh heartbeat, disarmed
state, and explicit CRSF configuration (`RC_PROTOCOLS=512`, bit 9 exclusively). This is
reported as **Expected** capability, not a guarantee about receiver hardware/firmware.
Missing, automatic/all, or mixed protocol configuration remains **Unknown**. Generic serial
configuration is not evidence of receiver type. Binding does not change these parameters.

An accepted ACK means ArduPilot accepted/dispatched the command, not that a receiver
entered bind mode. Unsupported/failed/denied/timeout results remain separate. Expert-command
execution cannot bypass the typed binding policy. Cancellation releases the ACK waiter
and command operation lease.

After acceptance, Radio Setup observes RC input for at most 20 seconds. It only reports
LinkRestored after observed input loss followed by fresh plausible channel data. Continuous
pre-existing input is not proof of re-pairing. A bounded wait ending without recovery retains
command acceptance and suggests **Use Bind in the ExpressLRS Lua script**.
Navigation, active-vehicle changes and connection cancellation stop monitoring.
Calibration and binding buttons are mutually unavailable while their UI workflow runs.

The Inspector journal includes ReceiverBindRequested, TX, ACK and meaningful recovery
transitions using the command correlation ID. Protocol evidence, command ID, ACK result
and vehicle result_parameter2 are retained in command diagnostics; transition messages
are emitted only when evidence changes, not for each packet.

No parameter changes, FC reboot, receiver/transmitter firmware writes or automatic retry
are part of this workflow. Software tests do not establish receiver hardware support.

References:
- [MAVLink START_RX_PAIR](https://mavlink.io/en/messages/common.html#MAV_CMD_START_RX_PAIR)
- ArduPilot `GCS_MAVLINK::handle_START_RX_PAIR` dispatches `AP::RC().start_bind()`.
- ArduPilot `RC_Channels_VarInfo.h` defines RC_PROTOCOLS bit 9 as CRSF.

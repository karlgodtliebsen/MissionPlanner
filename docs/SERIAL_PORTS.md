# Serial Ports

Open **Setup > Optional Hardware > Serial Ports** while connected to an ArduPilot vehicle.
The page configures flight-controller parameters, not host-PC COM ports.

Each reported SERIALn group becomes a row, sorted by its numeric ArduPilot index. Sparse layouts,
multi-digit indexes and missing BAUD/OPTIONS parameters are supported. Future group suffixes do not
cause errors. SERIALn is a logical ArduPilot index: the available parameter metadata does not reliably
identify a board's physical UART/pad labels, so the page does not invent that mapping.

- **Protocol** edits SERIALn_PROTOCOL using firmware metadata labels and encoded values.
  RCIN means ArduPilot serial RC input.
- **Speed** edits SERIALn_BAUD. Metadata supplies human-readable baud labels and their encoded
  parameter values; for example, a 115200 label can represent parameter value 115.
  This is the configured setting, not a measurement of runtime electrical baud. Some RC protocols
  select their runtime rate automatically.
- **Options** edits SERIALn_OPTIONS using the shared metadata-backed multi-select bitmask control.
  The raw mask and last confirmed readback remain visible. Bits absent from metadata are preserved.

Unknown current enum values remain visible as Unknown (numeric value). Without enum metadata, the
shared numeric editor shows the raw parameter value. Missing fields are blank; metadata read-only
fields are disabled.

## Applying changes

Opening the page and changing selections only stage edits in the existing shared parameter session.
**Apply changes** writes modified serial fields only, through the normal parameter service, and requires
matching vehicle readback. Other staged parameters, including RC_OPTIONS, are not written by this page.
Failures remain visible with the previous confirmed readback and the pending value shown separately.
Applying unchanged values sends nothing.

The page displays the reboot information from confirmed serial fields in the shared session and always
reminds the user that serial changes may not take effect until the flight controller is rebooted.
It never reboots the controller automatically.

Disconnecting or changing the active vehicle cancels pending page work. Reconnection rebuilds the rows
from that vehicle's reported parameters. Late metadata responses cannot replace a newer vehicle's rows.
Closing the view detaches its subscriptions; the existing shared session factory owns session disposal.

## Receiver diagnostics

Two or more ports whose metadata identifies the selected protocol as RCIN produce a non-blocking warning.
If RC_OPTIONS metadata identifies a multiple-receiver support bit, the warning distinguishes its disabled
state. Missing metadata produces no unsupported numeric assumptions. Multiple receivers can be intentional;
the warning never modifies a port. Duplicate GPS, MAVLink and other protocols are not treated as errors.
Diagnostics include pending edits and update after confirmed changes.

## Implementation and verification

SerialPortConfiguration owns grouping and receiver diagnostics. SerialPortsModule uses the same grouping.
SerialPortsViewModel and SerialPortRowViewModel compose IParameterEditSession and ParameterItemViewModel;
the page introduces no transport, platform serial APIs or additional parameter-write mechanism.
The shared editor preserves existing unknown bits while rejecting changes to unadvertised bits, and treats
numeric enum labels as labels rather than raw values.

Automated coverage includes grouping, metadata labels, baud round trips, unknown values/bits, read-only
validation, duplicate receiver diagnostics, unchanged writes, rejected/unconfirmed writes, reconnects and
late metadata/parameter arrivals. See [implementation results](tasks/SerialPorts/IMPLEMENTATION_STATUS.md).

The physical Pavo20 Pro procedure in the task specification has not been run. Reboot persistence and
receiver operation on its actual UART3 therefore remain hardware acceptance checks.

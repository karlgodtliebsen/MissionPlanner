# CubeLAN 8-port switch configuration

## Supported protocol

The Config page uses the only CubeLAN mechanism documented in this repository: MAVLink
`DEVICE_OP_READ` and `DEVICE_OP_WRITE` proxying I²C bus 0, address `0x50`. Discovery reads
100 bytes from register offset 0. A supported document begins with `AA 55`, contains
four-byte register commands (`PHY`, register, high byte, low byte), and ends with `55 AA`
followed by the legacy erased-value suffix.

The implementation exposes exactly the register bits present in the legacy CubeLAN plugin:

| Setting | Register mapping |
|---|---|
| Class of service enabled | PHY 21, register = port 0–7, bit 10 |
| Class of service high priority | PHY 21, register = port 0–7, bit 11 |
| Energy Efficient Ethernet | PHY 22, register 0, bit = port 0–7 |
| Tagged VLAN egress | PHY 23, register 13, bit = port 0–7 |
| VLAN membership | PHY 23, registers 15–18, one bit for each source/destination pair |

Eight hardware ports are projected as `Port 0` through `Port 7`. Unknown register commands
from a valid document are retained during editing and writing, but are omitted from export.
No protocol evidence is available in the repository for PoE, port enablement, operating
mode, VLAN identifiers, authentication, editable labels, firmware version, serial number,
reboot, or reconnect commands. Those fields and commands are therefore not exposed.

## Safety and failure behavior

The page always reads a complete snapshot before enabling edits. An apply validates the
eight ports and the complete 8-by-8 membership matrix, compares every output byte with the
device, writes only differing bytes, and reads each written byte back. It then performs a
fresh full read and compares the verified settings. A failed apply triggers a best-effort
write and readback of the original snapshot using a bounded, independent rollback token.

The generic vendor-device contract keeps transport, identity, capabilities, validation,
configuration snapshots, apply results, rollback state, and reboot/reconnect state outside
the CubeLAN-specific model. Authentication values are optional, excluded from JSON, and
redacted when formatted. CubeLAN does not currently advertise authentication because no
verified authentication mechanism exists.

Discovery states are deliberately explicit:

- **Not connected** — no active vehicle can proxy the request.
- **Not discovered** — the documented request timed out.
- **Unsupported** — the vehicle rejected the operation or returned an unknown document.
- **Authentication required** — available to future adapters; not emitted by CubeLAN.

Exports contain only the schema, device type, verified port fields, and VLAN membership.
They contain neither credentials nor raw/unknown registers.

## Real-hardware verification

Automated tests use an in-memory device-operation endpoint and verify discovery, decode,
eight-port projection, validation, changed-byte writes, full readback, rollback, export
boundaries, authentication redaction, wire encoding, and unavailable UI state. Before
calling the feature hardware-validated, perform this separate test with a CubeLAN switch
and a supported ArduPilot flight controller:

1. Back up the switch configuration with a known-good tool and connect through the flight
   controller used for the test.
2. Confirm discovery reads I²C bus 0/address `0x50`, displays exactly eight ports, and that
   every displayed bit matches the known-good tool.
3. Change one setting at a time in each verified register family, apply it, power-cycle as
   appropriate, and confirm both the app readback and the known-good tool agree.
4. Exercise representative entries across the 8-by-8 VLAN membership matrix and verify
   traffic behavior on the physical ports.
5. Interrupt a write or force a rejected write, then confirm the UI reports failure and the
   original configuration is restored.
6. Disconnect/reconnect during discovery and apply, ensuring cancellation targets the old
   vehicle session and a later discovery succeeds.
7. Save an export and inspect it for the absence of credentials and raw registers.

Record the CubeLAN hardware revision, flight-controller board, ArduPilot firmware/version,
connection transport, and observed `DEVICE_OP` result codes with the verification report.

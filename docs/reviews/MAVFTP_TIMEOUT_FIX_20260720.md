# MAVFTP SITL Timeout Fix — 2026-07-20

## Observed failure

The UI log showed that each `ListDirectory` request was sent successfully to the
SITL/MAVProxy UDP endpoint and that an immediate `FILE_TRANSFER_PROTOCOL` reply
was received. The reply was then rejected by `MavFtpPacketCodec` with:

```text
MAVFTP payload is truncated.
```

The timeout was therefore not a UDP connectivity failure.

## Root cause

`FILE_TRANSFER_PROTOCOL` contains a fixed `uint8_t payload[251]` field. MAVLink 2
may omit trailing zero bytes from message payloads on the wire. The decoder passed
only the shortened wire representation to the MAVFTP packet codec. ACK and NAK
responses often end with zero-filled header and data bytes, so a valid response
could appear shorter than the 12-byte MAVFTP packet header.

## Correction

`FileTransferProtocolMessageDecoder` now reconstructs the fixed 251-byte FTP
payload by zero-padding the received field before protocol decoding. A regression
test covers a deliberately shortened MAVLink 2 ACK response.

## Additional outbound routing correction

The same log revealed unrelated failures in `REQUEST_DATA_STREAM`, home-position,
and parameter commands. Those services used the placeholder endpoint
`mavlink/unknown:0`, which is not a valid UDP destination. They now resolve the
actual endpoint from the registered `VehicleSession`.

No files under `src-v.1` were modified.

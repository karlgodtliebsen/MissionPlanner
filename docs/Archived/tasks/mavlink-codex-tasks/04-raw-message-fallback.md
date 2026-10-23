# Task 04 — Lossless Raw-Message Fallback

## Objective

Ensure every CRC-valid selected-dialect frame reaches consumers even when no typed decoder exists.

## Current problem

`MavLinkMessageDecoders.TryDecode` returns false when a message ID has no registered typed decoder. Depending on the calling path, this can make valid traffic invisible.

## Required behaviour

Implement this pipeline:

```text
frame parsed and CRC validated
    -> typed decoder registered?
       -> yes: typed message
       -> no: RawMavLinkMessage containing complete envelope and payload
```

`RawMavLinkMessage` must retain:

- MAVLink version
- sequence
- system ID
- component ID
- message ID
- incompatibility and compatibility flags where applicable
- signature metadata or original signature bytes where available
- exact payload bytes
- message definition/name when known
- receive timestamp if that is part of the existing envelope conventions

Do not use a fictitious fallback message ID. Remove any such concept if present.

## Dispatcher behaviour

A raw message without a domain handler must not produce warning spam at normal log levels. Use structured debug/trace logging with message ID and known name.

Allow protocol-specific subscribers such as MAVFTP to continue receiving the typed messages they require.

## Tests

- a valid known-but-untyped frame becomes `RawMavLinkMessage`;
- exact payload bytes are preserved;
- a typed decoder still wins when registered;
- invalid CRC never becomes a raw message;
- unknown future ID with no registry entry follows the documented policy and remains diagnosable;
- no fake `DefaultFallback` ID is needed.

## Acceptance criteria

- Full dialect registry coverage no longer requires hundreds of immediate hand-written decoders.
- No valid selected-dialect packet is silently discarded solely due to missing typed support.

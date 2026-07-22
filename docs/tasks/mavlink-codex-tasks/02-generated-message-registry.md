# Task 02 — Generated MAVLink Message-Definition Registry

## Objective

Replace the small hand-written CRC switch with a generated registry covering all inherited messages in `common.xml` and `ardupilotmega.xml`.

## New protocol model

Add a generated-neutral definition such as:

```csharp
public sealed record MavLinkMessageDefinition(
    uint MessageId,
    string Name,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    string Dialect,
    bool IsDeprecated);
```

Add an abstraction:

```csharp
public interface IMavLinkMessageDefinitionRegistry
{
    bool TryGet(uint messageId, out MavLinkMessageDefinition definition);
    IReadOnlyCollection<MavLinkMessageDefinition> Definitions { get; }
}
```

Prefer an array plus dictionary/frozen dictionary generated once at startup or emitted as static generated data. Avoid reflection on the hot path.

## Integration

Refactor:

- `IMavLinkCrcExtraProvider`
- `CommonMavLinkCrcExtraProvider`
- `MavLinkV2FrameParser`
- packet builders used in tests and simulators

The CRC provider may remain as a compatibility adapter backed by the new registry.

Use the registry for:

- CRC extra lookup
- known-message diagnostics
- minimum payload validation
- maximum payload validation
- message-name logging

## MAVLink 2 length rules

Implement and test:

- payloads may omit trailing zero-valued extension fields;
- payload length must not be below the non-extension minimum;
- payload length must not exceed the maximum;
- messages with zero-length payloads are valid when defined that way;
- signed-frame length remains handled independently.

## Generated files

Place generated registry data under a clear path, for example:

`src/Core/MissionPlanner.MavLink/Generated/`

Every generated file must include:

- generated-code marker
- source dialect names
- source revision or content hash
- warning not to edit manually

## Tests

- every official inherited message has exactly one registry entry;
- CRC extras equal the dialect values;
- minimum and maximum lengths equal generated dialect values;
- current known frames continue parsing;
- formerly unknown ArduPilot messages such as IDs 163, 168, 241, and 11030 pass CRC validation when their frames are valid;
- invalid length and invalid CRC are rejected with useful diagnostics.

## Acceptance criteria

- The frame parser knows all selected-dialect message IDs.
- `CommonMavLinkCrcExtraProvider` no longer contains a manually maintained message switch.
- No typed decoder is required merely to validate a frame.

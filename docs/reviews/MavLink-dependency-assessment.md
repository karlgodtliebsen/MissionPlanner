# MissionPlanner MAVLink dependency assessment

## Executive conclusion

The current MissionPlanner source references `Asv.Mavlink`, but no C# source in the uploaded repository uses an `Asv.Mavlink`, `Asv.Store`, or other `Asv.*` API.

References found:

```text
src/Directory.Packages.props
    PackageVersion Include="Asv.Mavlink" Version="4.2.0"

src/Core/MissionPlanner.MavLink/MissionPlanner.MavLink.csproj
    PackageReference Include="Asv.Mavlink"
```

No source usages were found by repository-wide search.

Therefore, in the current source tree, `Asv.Mavlink` appears to provide no runtime or compile-time functionality. It contributes only its dependency graph, including `Asv.Store`.

The recommended first action is not to select another MAVLink package. Remove the unused package reference and verify the existing solution.

## What Asv.Mavlink normally provides

Asv.Mavlink is a broad .NET MAVLink framework that provides packet/message communication, commands, telemetry, vehicle and payload APIs, routing/proxy infrastructure, simulation/test tooling and dialect generation.

Its current project graph includes:

```text
Asv.Cfg
Asv.Common
Asv.IO
Asv.Store
ZLogger
System.IO abstractions
```

Those features are useful in an application using the Asv architecture. MissionPlanner does not currently use them.

## What MissionPlanner already implements

The project under:

```text
src/Core/MissionPlanner.MavLink
```

contains about 156 C# source files and already provides the core application capabilities.

### Framing and validation

```text
Services/MavLinkV2FrameParser.cs
MavLinkCrc.cs
MavLinkFrame.cs
```

Capabilities include:

- MAVLink 1 and MAVLink 2 framing;
- fragmented and concatenated frame parsing;
- CRC-extra validation;
- signed-frame length handling;
- payload-length validation;
- resynchronization after malformed frames.

### Generated dialect coverage

```text
Generated/MavLinkMessageDefinitions.g.cs
Generated/MavLinkWireMessages.g.cs
Generated/MavLinkWireDecoders.g.cs
Generated/MavLinkEnums.g.cs
```

The generated source is based on official `ardupilotmega.xml` and transitive includes. The reviewed source contains approximately:

```text
325 message definitions
287 generated wire message records/decoders
221 generated enums
```

### Typed and raw decoding

```text
Decoding/*MessageDecoder.cs
Decoding/GeneratedMavLinkMessageDecoder.cs
Decoding/RawMavLinkMessageDecoder.cs
```

### Encoding

```text
Encoding/MavLinkWireMessageEncoder.cs
Encoding/MavLinkCommandEncoder.cs
Encoding/MavLinkParameterEncoder.cs
Encoding/MavLinkMissionEncoder.cs
Encoding/MavLinkPacketBuilder.cs
```

### Connection and protocols

```text
Client/MavLinkClient.cs
Services/MavLinkConnection.cs
Configuration/MavLinkConfigurator.cs
MavFtp/
Parameters/
mission protocol services
command ACK tracking
vehicle identity and telemetry
```

MissionPlanner has already written the replacement it needs.

## Recommended action

Remove:

```xml
<PackageReference Include="Asv.Mavlink" />
```

and its central version, then run:

```text
dotnet restore
dotnet build src/MissionPlanner.slnx -c Release
dotnet test <deterministic projects>
dotnet list src/MissionPlanner.slnx package --include-transitive
dotnet list src/MissionPlanner.slnx package --vulnerable --include-transitive
```

Then run SITL and real serial/USB smoke tests.

If compilation succeeds, no replacement package is required.

## Harden the owned library

Before treating it as a reusable general library, address:

1. **MAVLink signing**
   - Parser accounts for signature length but does not verify signatures.
   - Generic encoder emits unsigned MAVLink 2.

2. **Unknown/custom dialects**
   - Frames whose IDs are absent from the registry are dropped.
   - Make the policy configurable and support registering additional dialects.
   - Do not call an unknown frame valid without its CRC extra.

3. **Generator reproducibility**
   - Pin official MAVLink revision.
   - Keep output deterministic.
   - Fail CI on drift.

4. **Conformance**
   - Compare bytes/fields against official or pymavlink-generated fixtures.
   - Cover truncated payloads, CRC failure, signed lengths, resynchronization, fragmentation and concatenation.

5. **Performance**
   - Parser copies frame and payload arrays. Profile before adding pooled ownership complexity.

## Alternatives

### Official MAVLink XML/tooling

Use this as the source of truth. It is primarily dialect XML and Python generation tools, not a complete modern .NET 10 communication stack.

### MAVSDK

MAVSDK is a mature C++ high-level API with language clients through gRPC. It brings a native/server boundary and duplicates much of MissionPlanner's application architecture. It is not a good drop-in replacement here.

### Legacy `MAVLink` NuGet package

Useful as a compatibility/reference source, but old and based on the legacy generated model. It should not become the new .NET 10 foundation.

### MavLink.Net.Core

It advertises MAVLink 1/2 support, but its visible package is an old alpha release. It is not preferable to the more complete code already in MissionPlanner.

## Can a replacement be written?

Yes. The wire layer is well specified:

```text
framing
CRC-extra registry
dialect definitions
payload codecs
packet encoder
sequence handling
transport-independent connection pipeline
```

But reproducing all high-level Asv features—routing, vehicle abstractions, commands, microservices, simulation, payloads and tooling—would be a separate large project.

MissionPlanner should own only the layers it needs. The current repository is already close to that target.

## Recommended ADR

```text
Remove Asv.Mavlink because it is unused.
Retain MissionPlanner.MavLink as the owned protocol implementation.
Use official MAVLink XML as the authoritative dialect source.
Add external packages only for a concrete missing capability.
```

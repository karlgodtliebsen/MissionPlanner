# MAVLink dependency removal record

Date: 2026-07-29

## Source usage audit

Case-sensitive and case-insensitive searches were run for:

```text
Asv.Mavlink
Asv.Store
using Asv
Asv.
```

Before removal, executable source contained no `Asv.*` API usage. The only
dependency declarations were:

```text
src/Directory.Packages.props
src/Core/MissionPlanner.MavLink/MissionPlanner.MavLink.csproj
```

Other matches were documentation describing the dependency assessment or
this removal task.

## Pre-removal package graph

The top-level package was `Asv.Mavlink` 4.2.0. Its distinctive transitive
chain included:

```text
Asv.Cfg 3.5.0
Asv.Common 3.5.0
Asv.IO 3.5.0
Asv.Store 3.5.0
MessagePack 3.1.4
MessagePack.Annotations 3.1.4
MessagePackAnalyzer 3.1.4
DotNext 5.26.1
DotNext.Threading 5.26.1
ObservableCollections 3.3.4
ObservableCollections.R3 3.3.4
R3 1.3.0
ZLogger 2.5.10
ZstdSharp.Port 0.8.6
```

It also pulled broad hosting, Serilog, filesystem, configuration, serial-port,
and storage dependencies.

NuGet reported twelve known advisories against transitive MessagePack 3.1.4:
three high severity and nine moderate severity. NuGet reported no deprecated
top-level package.

## Change

The project reference and its central package version were removed without a
replacement. See [ADR 0001](../adr/0001-own-mavlink-implementation.md).

## Validation

Post-removal validation on 2026-07-29:

- `dotnet restore src/MissionPlanner.slnx`: succeeded.
- `dotnet build src/MissionPlanner.slnx`: succeeded with zero errors (existing,
  unrelated warnings remain).
- Targeted MAVLink/parser/CRC/registry/command/parameter/mission/MAVFTP/lifecycle
  suite: 137 passed, 0 failed.
- Full deterministic Core suite after the related parameter work: 450 passed,
  0 failed, 11 hardware/SITL tests explicitly skipped.
- Post-removal transitive report: the `Asv.*`, MessagePack, DotNext, R3,
  ObservableCollections, ZLogger, and ZstdSharp chain is absent.
- Post-removal vulnerability report: no vulnerable packages.
- Post-removal deprecation report: no deprecated packages.

The targeted suite initially exposed the packed-parameter preference path as
disabled. MAVFTP packed loading was restored with classic `PARAM_REQUEST_LIST`
fallback and a zero-total progress guard; its coverage now passes.

Opt-in SITL was not run because no bounded SITL environment was configured for
this validation. A real-flight-controller serial smoke test also remains
pending connected hardware; neither result is simulated or claimed by CI.

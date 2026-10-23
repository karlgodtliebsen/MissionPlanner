# MAVLink dependency task 01 — Remove unused Asv.Mavlink

## Objective

Prove that `Asv.Mavlink` is unused, remove it and its transitive dependency chain, and preserve all current MAVLink behavior.

## Constraints

- Modify only the new solution.
- Never modify `src-v.1.38`.
- Do not replace the package unless a concrete compile/test failure proves a required capability is missing.
- Do not weaken NuGet vulnerability auditing.

## Current evidence

Only these references exist:

```text
src/Directory.Packages.props
src/Core/MissionPlanner.MavLink/MissionPlanner.MavLink.csproj
```

No C# source uses `Asv.Mavlink`, `Asv.Store`, or another `Asv.*` API.

## Requirements

1. Search case-sensitively and case-insensitively for `Asv.Mavlink`, `Asv.Store`, `using Asv`, and `Asv.`.
2. Record the pre-change transitive dependency and vulnerability graph.
3. Remove the package reference from `MissionPlanner.MavLink.csproj`.
4. Remove its central version when no project needs it.
5. Restore and build the complete solution.
6. Run parser/CRC, generated registry, command, parameter, mission, MAVFTP and lifecycle tests.
7. Run SITL UDP smoke tests.
8. Run a manual real-FC serial smoke test and document it.
9. Run vulnerable/deprecated/outdated package reports after removal.
10. Add an ADR naming official MAVLink XML as source of truth and `MissionPlanner.MavLink` as the owned implementation.

## Acceptance criteria

- No `Asv.Mavlink`, `Asv.Store`, or unwanted transitive MessagePack remains.
- Solution builds on intended targets.
- Existing behavior remains green.
- No blanket replacement package is introduced.

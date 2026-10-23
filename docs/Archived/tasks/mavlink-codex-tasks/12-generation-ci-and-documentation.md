# Task 12 — Reproducible Generation, CI Drift Detection, and Documentation

## Objective

Make full dialect support maintainable when MAVLink evolves.

## Generator

Add a repository script/tool, for example:

```text
scripts/Generate-MavLinkDialect.ps1
src/Tools/MissionPlanner.MavLink.Generator/
```

Inputs must be explicit:

- MAVLink repository revision/tag
- root dialect (`ardupilotmega.xml`)
- inherited dialects
- hand-written override manifest
- domain-promotion catalog

Outputs must be deterministic.

## CI checks

Add checks that:

1. regenerate into a temporary directory;
2. compare against committed generated files;
3. fail on drift;
4. run registry and conformance tests;
5. report coverage counts;
6. ensure generated files were not manually edited.

Do not make normal application builds require network access. Vendor the required XML files with license/provenance information, or pin and restore them in a separate explicit update step.

## Documentation

Document:

- source dialect revision;
- how inheritance is resolved;
- how CRC and payload lengths are generated;
- how to add a hand-written override;
- how raw fallback works;
- when to promote a message into the domain;
- how to regenerate after a MAVLink update;
- how to inspect coverage;
- how to update conformance fixtures.

Update `docs/AI.md`, `docs/CODEX.md`, and relevant architecture documentation with generator boundaries and the prohibition against editing generated output manually.

## Final acceptance criteria

- A clean checkout can build and test without downloading dialect data.
- A maintainer can intentionally update the MAVLink revision with one documented workflow.
- CI detects stale generated artifacts.
- Coverage is complete and measurable.
- No file under `src-v.1.38` has been modified.

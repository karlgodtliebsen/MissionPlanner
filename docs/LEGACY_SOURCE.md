# Original Mission Planner source reference

The original Mission Planner source is maintained in the
[ArduPilot/MissionPlanner GitHub repository](https://github.com/ArduPilot/MissionPlanner).
The former local `src-v.1.38` directory has been deleted and is not a required checkout
dependency. Use the upstream repository for future behavioral and implementation reference.

Older task documents and historical reports may still use paths such as
`src-v.1.38/Controls/fftui.cs`. Interpret the part after `src-v.1.38/` as a path
relative to the upstream repository root, not as a file available in this workspace.
For example, find `Controls/fftui.cs` under the upstream `Controls` directory.
Files may have moved or changed since those tasks were written; check upstream history
when a historical path no longer exists.

Record the inspected upstream commit or tag in new audits when behavior depends on a
particular version. Do not assume current upstream code is identical to the removed
local snapshot. Existing audit reports may retain historical local paths and Git
revisions as evidence of what was inspected; those are provenance, not setup instructions.

Use legacy code for behavioral reference. Preserve Next Gen's architecture and avoid
copying legacy UI structure, global state or protocol coupling into new domain code.

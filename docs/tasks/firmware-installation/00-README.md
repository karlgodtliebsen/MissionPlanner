# MissionPlanner NextGen — Codex Task Set

Prepared from the uploaded `MissionPlanner-202600816-v1` source snapshot.

Run the tasks in this order:

1. `01-local-custom-firmware-board-id-override.md`
2. `02-firmware-installation-viewmodel-observable-cleanup.md`
3. `03-virtualized-datagrid-search-debounce.md`

The first two intentionally touch the same firmware ViewModel, so Task 2 should be performed after Task 1.

## Repository rules

Before editing, Codex must read:

- `docs/AGENTS.md`
- `docs/AI.md`
- `docs/CODEX.md`
- `docs/DESIGN_CONCEPTS.md`
- `docs/ARCHITECTURE_DECISION_RECORDS.md`
- `docs/FIRMWARE.md`
- relevant firmware task/ADR documents under `docs/tasks/firmware/` and `docs/adr/`

For the VirtualizedDataGrid task also read:

- `docs/reviews/VirtualizedDataGrid-review.md`

Do not modify `src-v.1.38`; it is behavioral reference only.

## Verification

From `src/`, prefer:

```powershell
dotnet restore .\MissionPlanner.slnx
dotnet build .\MissionPlanner.slnx --no-restore
dotnet test .\MissionPlanner.slnx --no-build
```

If the full MAUI solution cannot build on the executing platform, build/test the affected projects explicitly and report exactly what was and was not verified.

At completion of each task, Codex should report:

- behavior changed;
- files changed;
- important design decisions;
- tests added/changed;
- commands executed and results;
- remaining risks or hardware-only verification.

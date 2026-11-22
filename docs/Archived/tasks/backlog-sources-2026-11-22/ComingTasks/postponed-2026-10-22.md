# Postponed and follow-up tasks (2026-10-22)

Decided during the 2026-10-22 improvement review. None of these are started.

## Linux and macOS versions (started 2026-10-22)

Implemented: `MissionPlanner.Library.Desktop` (shared), `MissionPlanner.Library.Linux`,
`MissionPlanner.Library.MacOs`, the `MissionPlanner.Linux` and `MissionPlanner.MacOs` heads, CI jobs and
`docs/PLATFORMS.md`. Still open:

- Run the macOS build on a Mac and verify the keychain store and the app bundle.
- Run the Linux build with a real flight controller (serial permissions, ModemManager, USB hot-plug).
- Run the platform-neutral test suites on Linux and make the CI job blocking.
- Optional: location through GeoClue (Linux) and CoreLocation (macOS), DFU through the Linux
  `STM32_Programmer_CLI`, a Linux/macOS `IUsbTopologyProvider`.
- Decide whether to delete the unused `Platforms/MissionPlanner.Android` and `.iOS` folders; they were removed from the solution and are not functional.

## Localization (postponed)

User-facing English strings are embedded in the ViewModels (see item 12 of
`docs/reviews/CODE_REVIEW_20260717.md`). Move them to resources and centralize status codes/messages when
the Help/multi-language feature starts. Not needed before then.

## Warnings-as-errors beyond `MissionPlanner.Firmware` (postponed)

Only `MissionPlanner.Firmware` sets `TreatWarningsAsErrors`. Rolling it out is a large task because the
solution also produces XML-comment warnings and warnings for properties/fields that are not properly
initialized. The task must first clean up those warnings (XML comments and nullable initialization) and
then enable the setting project by project, starting with `Core`, `MavLink` and `Transport`.

> The request describing this task ended mid-sentence ("Please ensure XML comment are ..."). Confirm the
> rest of the requirement about XML comments before starting.

## Async hygiene analyzer

`Microsoft.VisualStudio.Threading.Analyzers` is enabled for `VSTHRD100` (async void) and `VSTHRD110`
(unobserved task) only; `VSTHRD002` (blocking waits) is reported as a suggestion. See `.editorconfig`.
Promote `VSTHRD002` to a warning once the remaining blocking waits are reviewed.

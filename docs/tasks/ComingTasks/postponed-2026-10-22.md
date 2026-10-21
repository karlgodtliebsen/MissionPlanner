# Postponed and follow-up tasks (2026-10-22)

Decided during the 2026-10-22 improvement review. None of these are started.

## Linux and macOS versions

`MissionPlanner.Library.Linux` and `MissionPlanner.Library.MacOs` are empty placeholder projects.
Only `MissionPlanner.Library.Windows` has platform code, and the Desktop head references only that.
The App no longer references `MissionPlanner.Library.Linux` or the `Tools/MissionPlanner.Simulator`
project (neither was used by the App at build or run time).

Creating the Linux and macOS versions needs:

- Platform implementations in `MissionPlanner.Library.Linux` / `.MacOs` for what `Library.Windows` provides
  (serial/USB discovery, DFU tooling location, clipboard/file pickers where Avalonia does not cover them,
  SITL cache paths and process hosting).
- A Desktop head per platform (or runtime-identifier conditions in `MissionPlanner.Desktop`) that references
  the matching library.
- CI jobs on `ubuntu-latest` and `macos-latest`, build first and tests second.
- Updating `MAPS_PLATFORM_VERIFICATION.md`, which currently covers only the Windows desktop target.

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

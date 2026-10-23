# Compass button UX implementation

Implemented the focused refinement while preserving the persistent summary and
Information/Configuration tabs.

- Configuration toolbar contains only Refresh and Open Full Parameters.
- Calibration actions use icon-and-text `EmbeddedTextButton` controls inside
  Calibration / Actions. Existing command enablement is preserved: the current
  availability properties do not provide the exact proposed visibility states.
- Discard and Apply sit together, right-aligned inside Pending Changes.
- Reboot is beside its warning and remains visible only when required.
- Per-setting reset uses the 50-pixel `EmbeddedIconButton` style.
- Added the explicit toolbar and dropdown classes. The current repository uses
  `ToolbarButton` rather than `IsToolbarButton`; existing consumers retain that
  style. Unclassified legacy dropdowns retain their appearance through a fallback
  selector that excludes all three new explicit dropdown classes.
- No ViewModel, parameter service, calibration protocol, Markdown, or copy behavior
  was changed. No dropdown or general page-layout abstraction was introduced.

## Validation

- Desktop build: passed (existing unrelated warnings, zero errors).
- Browser/WASM build: passed (zero errors).
- Existing Compass UI/document tests: 44 passed.
- Existing Compass Core tests: 26 passed.
- XML comparison against HEAD confirmed every command, enablement/visibility
  binding, tooltip, and accessible name was retained (Apply tooltip whitespace
  normalized), and only the two page actions remain in the Configuration toolbar.
- `git diff --check`: passed.

Interactive visual/scrolling checks remain manual; no hardware actions were run.
The broader design notes remain guidance for future tasks.

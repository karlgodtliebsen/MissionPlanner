# TASK 04 — Rebuild ArmingPage Using the Compass Setup UX Pattern

## Goal
Replace the placeholder Arming page with the proven Setup UX.

## Remove demo code
Current page/code-behind contains:
```text
direct MarkdownRenderer
ObservableStringBuilder
# Hello, Markdown!
```
Remove it. Use `InformationDocumentView`. Code-behind returns to normal initialization.

## Layout
Follow current `docs/SetupUxPattern.md` / Compass conventions:

```text
Persistent summary

Information | Configuration
```

Use the current centered constrained column convention, not `Bounds.Width` bindings.

## Persistent summary
Examples:
```text
Arming
DISARMED / READY · RC input live · Arm switch RC8
```
or:
```text
Arming
DISARMED / NOT READY · 2 current blockers
```
This summary is structured UI state, not parsed Markdown.

## Information tab
```text
InformationDocumentView(StatusDocument)

Advanced diagnostics [collapsed]
    InformationDocumentView(DiagnosticDocument)
```

Copy behavior remains owned by `InformationDocumentView`.

## Configuration toolbar
Only page-level actions:
```text
Refresh
Open Full Parameters
```
Use `ToolbarIconButton`.

## Configuration sections

### Pre-arm checks
Friendly editor:
```text
Pre-arm checks
[ All checks (recommended) ▼ ]

Custom checks...
```
Custom bit choices come from metadata.

### Arming methods
```text
Stick arming
[ Disabled / Arm only / Arm and disarm ]

RC Arm/Disarm switch
[ None / RC5 / RC6 / ... ]

Live input
RC8: 999 -> 2000 -> 999
```
Conflicts appear inline.

### Position / vehicle-specific requirements
Only show supported settings, e.g.:
```text
Require location before arming
Arming requirement
```
Missing parameters are not fake Off values.

### Arming actions
SectionCard:
```text
Current: DISARMED / READY
[ Arm Vehicle ]
```
or:
```text
Current: ARMED
[ Disarm Vehicle ]
```
Use `EmbeddedTextButton`. Task 05 implements behavior.

### Pending changes
Compass-style:
```text
3 unsaved changes
<review>
[Discard] [Apply]
```

## Advanced
Do not recreate Full Parameters. Keep only relevant evidence and existing navigation.

## Responsive/accessibility
Validate desktop, narrower width, high DPI, light/dark, Browser/WASM, long documents and
custom-check lists. One vertical scroll region per tab.

Icon-only buttons retain tooltip + AutomationProperties.Name.

## Acceptance
The page visually belongs to the same UX family as Compass and normal Arming configuration
no longer requires Full Parameters.

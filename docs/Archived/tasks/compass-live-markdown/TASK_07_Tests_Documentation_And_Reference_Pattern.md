# TASK 07 — Tests, Documentation and Reference Pattern

## Goal

Make Compass the proven reference implementation for future MissionPlanner Setup UX.

## Automated tests

### Compass semantic/configuration service

Cover:

- disabled/no Compass;
- enabled/detected;
- enabled/no device;
- yaw dependency conflict;
- orientation change;
- missing parameters;
- metadata defaults;
- reboot requirement;
- partial write failure;
- reread verification.

### Document factory

Cover:

- loading/disconnected;
- disabled consistent;
- disabled conflicting;
- healthy;
- no device;
- current pre-arm blocker;
- stale historical blocker;
- pending-vs-current distinction;
- Markdown escaping.

### ViewModel

Cover:

- clean/dirty state;
- Apply;
- Discard;
- reset default;
- calibration availability;
- disconnect/reconnect;
- document regeneration only on relevant changes.

### InformationDocumentView

Where practical cover:

- document binding;
- LiveMarkdown update;
- toolbar visibility;
- Copy All;
- Copy Markdown;
- light/dark resources.

## Documentation

Create/update:

```text
docs/SetupUxPattern.md
docs/MarkdownDocuments.md
```

## SetupUxPattern.md

Document the standard page structure:

```text
Status / explanation
Configuration
Actions
Advanced
Pending changes / Apply
```

Document the dependency direction:

```text
View
 -> ViewModel
 -> Feature Configuration Service
 -> Parameter subsystem
```

## MarkdownDocuments.md

Document:

```text
Core produces facts.
Application produces explanations.
UI renders documents.
```

Also document:

- `UserDocument`;
- LiveMarkdown.Avalonia 2.4.3 integration;
- `InformationDocumentView`;
- escaping rules;
- copy behavior;
- when Markdown is appropriate.

Recommended control selection:

```text
Short label/status
    -> TextBlock

Few selectable lines
    -> SelectableTextBlock

Long explanation/report/table
    -> InformationDocumentView / LiveMarkdown

Raw logs/huge telemetry
    -> dedicated read-only log/text viewer

Interactive settings
    -> native Avalonia controls
```

## Architecture guard

If architecture tests exist, assert:

- Core/domain projects do not reference LiveMarkdown;
- Core/domain projects do not reference `UserDocument`;
- third-party Markdown API is isolated behind the UI wrapper.

## Real-hardware checklist

Test on the remaining drones:

1. no Compass / disabled;
2. enabled/detected Compass;
3. calibration;
4. yaw source;
5. orientation;
6. pending changes;
7. Apply and reread;
8. reset default;
9. selection + Ctrl+C;
10. Copy All;
11. Copy Markdown;
12. dark/light;
13. disconnect/reconnect;
14. Browser/WASM smoke test.

## Future guidance

Document, but do not implement, the next likely page:

```text
Arming
```

using the same pattern.

## Acceptance criteria

Compass is a stable, documented reference implementation ready to be reused deliberately across MissionPlanner Next Gen.

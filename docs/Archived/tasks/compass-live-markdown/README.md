# MissionPlanner Next Gen — Compass UX + LiveMarkdown.Avalonia 2.4.3

This package supersedes the earlier Compass UX task package. It incorporates the chosen Markdown renderer:

```xml
<PackageVersion Include="LiveMarkdown.Avalonia" Version="2.4.3" />
```

The Compass page is the first reference implementation for the new MissionPlanner Setup UX.

## Architectural rule

```text
Core / Domain / Firmware / MAVLink
        ↓
structured facts, state and results

Application
        ↓
semantic configuration services
        ↓
user-facing document composition

Avalonia UI
        ↓
native controls for interaction
        +
LiveMarkdown for selectable explanatory/report content
```

Do not generate Markdown in Core, firmware, MAVLink, transport, or other backend/domain code.

## LiveMarkdown-specific direction

Use `LiveMarkdown.Avalonia` 2.4.3 and its actual installed API.

The upstream component provides concepts including:

```text
MarkdownRenderer
ObservableStringBuilder
MarkdownTextBlock.IsSelectionScope
```

Codex must inspect the installed 2.4.3 package/API before implementation and adapt exact calls to that version.

For ordinary completed Compass documents, prefer replacing/updating a bounded document rather than token-by-token streaming. Use LiveMarkdown's efficient update model without coupling ViewModels to the third-party control.

## Task order

1. `TASK_01_LiveMarkdown_Wrapper_And_Theme.md`
2. `TASK_02_UserDocument_And_Safe_Markdown_Composition.md`
3. `TASK_03_Compass_Configuration_Service_And_Semantic_Model.md`
4. `TASK_04_Compass_Status_Document_Factory.md`
5. `TASK_05_Rebuild_CompassSetupView.md`
6. `TASK_06_Pending_Changes_Copy_And_Live_Refresh.md`
7. `TASK_07_Tests_Documentation_And_Reference_Pattern.md`

Execute one task at a time.

Do not migrate Arming, Firmware, Battery, GPS or the rest of Setup yet. Compass must be validated on real hardware first.

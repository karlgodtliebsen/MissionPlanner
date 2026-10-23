# Compass LiveMarkdown implementation

Implemented September 24, 2026. The implementation covers the seven tasks; real
hardware and interactive browser acceptance remain pending.

1. **Wrapper and theme:** inspected installed LiveMarkdown 2.4.3 documentation and
   pinned upstream source. Reused the existing package/style registrations. Added
   `InformationDocumentView`, scoped Semi theme mapping, a single selection scope,
   and a restricted completed-document parser. Links, images, and HTML are disabled.
2. **Safe composition:** added application-layer `UserDocument` and
   `UserDocumentBuilder`; dynamic text is escaped and multiline values normalized.
   Inline code handles backtick delimiters and pipe-table literal values.
3. **Semantic configuration:** reused the existing `ICompassConfigurationService`,
   metadata, parameter registry, explicit dependency review, verified writes,
   defaults, partial-failure handling, and guarded reboot. No second cache or
   backend Markdown dependency was introduced.
4. **Status factory:** added a pure Compass report factory with current/pending
   distinction, loading/disconnected/unsupported states, health, device identities,
   current arming evidence, calibration, and available parameter evidence.
5. **Page:** status document, native configuration, calibration actions, Advanced,
   and conditional pending footer share a responsive scrollable layout. Existing
   icon buttons and their commands remain intact.
6. **Copy and refresh:** cross-block selection/Ctrl+C, Copy All, exact Copy Markdown,
   and local feedback. Semantic comparisons exclude unrelated timestamps; identical
   Markdown does not replace the rendered document or selection.
7. **Tests and documentation:** deterministic escaping tests, 15 golden reports,
   semantic refresh/architecture checks, and headless renderer checks. Updated
   `SetupUxPattern.md`, `FEATURES.md`, and added `MarkdownDocuments.md`.

## Verification

Commands run from the repository, with explicit project paths:

- `dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter FullyQualifiedName~Compass`: **44 passed**.
- `dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-build --no-restore`: **242 passed**.
- `dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~Compass`: **26 passed**.
- `dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore`: **passed**, zero errors.
- `dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore`: **passed**, zero errors; no wrapper fallback required.
- `git diff --check`: **passed**.

Headless coverage includes actual cross-block drag selection, Ctrl+C, copy actions,
tables, theme changes, unchanged source, narrow/normal widths, 2× scaling, and long
documents. It caught and fixed a read-only `CanCopy` XAML assignment and a theme
lookup that initially ignored the actual variant. No new documentation warnings
were found in the changed production code. Existing unrelated nullable warnings
remain in the test compilation.

Use the [manual checklist](../../MarkdownDocuments.md#manual-acceptance-checklist)
for remaining drones and interactive browser/visual acceptance. Other Setup pages
have deliberately not been migrated.

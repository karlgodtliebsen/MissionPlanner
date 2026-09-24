# TASK 01 — Integrate LiveMarkdown.Avalonia 2.4.3 Behind a MissionPlanner Wrapper

## Goal

Integrate `LiveMarkdown.Avalonia` 2.4.3 as a MissionPlanner-owned read-only information/document control.

The rest of the application must not depend directly on LiveMarkdown APIs.

## Inspect the installed package first

Before coding, inspect the actual 2.4.3 package and confirm:

- `MarkdownRenderer` namespace and public API;
- `ObservableStringBuilder` API;
- selection behavior;
- `MarkdownTextBlock.IsSelectionScope`;
- clipboard behavior for selected text;
- dynamic update behavior;
- style/resource names;
- link callbacks/navigation behavior;
- Browser/WASM support in the current solution;
- whether raw HTML is enabled by default;
- whether images/network content require explicit opt-in.

Do not infer APIs from older package versions.

## Application resources

Register the LiveMarkdown styles/resources required by 2.4.3 in the appropriate Avalonia application/theme location.

Keep MissionPlanner theme overrides separate from package defaults.

Map renderer resources to existing MissionPlanner theme resources so light/dark mode remains coherent.

At minimum verify:

```text
foreground
normal font size
headings
inline code
code blocks
tables
block quotes
borders
secondary/card backgrounds
```

Do not hard-code colors inside Markdown strings.

## MissionPlanner wrapper

Create a reusable control, suggested name:

```text
InformationDocumentView
```

The wrapper owns the LiveMarkdown renderer internally.

Expose MissionPlanner-level properties such as:

```csharp
UserDocument? Document
bool ShowToolbar
bool AllowExternalLinks
```

If a direct Markdown property simplifies binding, it may also expose:

```csharp
string? Markdown
```

but ViewModels should normally expose `UserDocument`.

## Update strategy

Hide `ObservableStringBuilder` / LiveMarkdown-specific update objects inside the wrapper.

ViewModels must not depend on them.

The wrapper should:

1. compare incoming Markdown with current source;
2. do nothing if unchanged;
3. update the internal renderer efficiently when changed.

For Compass documents, use whole-document updates unless 2.4.3 provides a clearly better completed-document API.

## Text selection

Ensure the whole renderer is one selection scope.

Users must be able to drag-select text across:

```text
paragraphs
headings
tables
inline code
code blocks
```

and use normal `Ctrl+C`.

## Links

For the first Compass implementation:

- links may be disabled;
- raw network image loading should not be enabled;
- arbitrary URI/action schemes must not execute.

If links are enabled later, route them through MissionPlanner's navigation abstraction.

## Acceptance criteria

- `LiveMarkdown.Avalonia` 2.4.3 is isolated behind `InformationDocumentView`.
- Text selection across blocks works.
- Ctrl+C works for selected rendered text.
- Markdown tables render correctly.
- Light/dark theme is readable.
- Long documents remain responsive.
- Browser/WASM build remains valid or has a documented wrapper-level fallback.
- No backend/domain project references LiveMarkdown.

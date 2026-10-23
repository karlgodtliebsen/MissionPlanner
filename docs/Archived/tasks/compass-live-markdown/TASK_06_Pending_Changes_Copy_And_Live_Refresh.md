# TASK 06 — Pending Changes, Copy Actions and Efficient Live Refresh

## Goal

Finish the Compass interaction model and make the information document useful for notes, diagnostics and LLM conversations.

## Pending changes

Do not write every UI edit immediately.

Maintain:

```text
Current
Pending
Default
Changed
RequiresReboot
```

Show a footer only when dirty:

```text
3 unsaved changes

[ Discard ]                     [ Apply ]
```

## Apply

Before writing:

1. validate desired semantic configuration;
2. calculate explicit parameter change set;
3. include dependency changes;
4. apply through `ICompassConfigurationService`;
5. reread actual FC state;
6. update current state;
7. clear pending state only after confirmed success.

## Defaults

Use metadata defaults.

Provide `Reset to default` where a default is known.

Never guess a default.

## Reboot

If any changed parameter requires reboot:

```text
Changes applied. Reboot required.
[ Reboot Vehicle ]
```

Use existing safe reboot infrastructure.

Never reboot automatically.

## Copy behavior

`InformationDocumentView` should provide:

```text
normal selection + Ctrl+C
Copy All
Copy Markdown
```

### Copy All

Copy complete human-readable rendered text if LiveMarkdown 2.4.3 exposes a suitable text API.

If not, implement a safe presentation-layer Markdown-to-plain-text conversion.

### Copy Markdown

Copy exactly the underlying `UserDocument.Markdown`.

Use the existing clipboard abstraction or add a small UI-platform clipboard service.

Provide subtle non-modal `Copied` feedback.

## Live document refresh

Regenerate only on semantic Compass changes:

```text
Compass parameters
health/device discovery
calibration
current Compass pre-arm state
pending Compass configuration
connection state
```

Do not refresh for every ATTITUDE/heartbeat/other unrelated telemetry packet.

Before updating LiveMarkdown:

```csharp
if (newMarkdown == currentMarkdown)
    return;
```

## LiveMarkdown update behavior

Keep `ObservableStringBuilder` / update producer details inside `InformationDocumentView`.

Do not expose them to the ViewModel.

Minimize source replacement while the user is selecting/copying text.

## Safety

Escape dynamic content before Markdown composition.

Generated MissionPlanner documents must not permit vehicle text to inject:

```text
links
images
raw HTML
custom URI actions
```

unless explicitly supported later.

## Acceptance criteria

- Pending edits are reviewable and reversible.
- Copying selected/all/source text works.
- Relevant state changes update the document.
- High-rate telemetry does not cause high-rate Markdown rendering.
- Repeated updates remain responsive.

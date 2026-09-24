# Application information documents

Core produces facts. Application produces explanations. UI renders documents.

`UserDocument` is an immutable Markdown/title record in the App presentation layer.
`UserDocumentBuilder` escapes dynamic text, normalizes newlines inside values, and
provides headings, paragraphs, bullets, notes, inline code, and two-column tables.
Use code values for parameter evidence. Do not concatenate vehicle text into raw
Markdown. Core, firmware, MAVLink, and transport must not reference these types or
the renderer.

## LiveMarkdown 2.4.3 integration

The centrally pinned package and its existing `App.axaml` defaults/styles are used.
The installed package XML documentation/README and upstream source at package
commit `bd9532fa414e4a0d5219393609c7c87d06268b61` were inspected. In this version:

- `MarkdownRenderer`, `ObservableStringBuilder`, `MarkdownDocumentUpdate`, and
  `MarkdownTextBlock` are in `LiveMarkdown.Avalonia`.
- The builder supports Append/Clear and must be updated on the UI thread.
- Completed documents can use `MarkdownDocumentUpdate.Full`; assigning it updates
  the renderer synchronously. The wrapper uses this API with its own parser.
- `MarkdownTextBlock.IsSelectionScope` supports cross-block selection, including
  headings, tables, inline code, and code blocks. `CanCopy` reports selection availability; the default copy gesture is Ctrl+C.
  Do not set this read-only property from XAML.
- `RenderedTextProjection.Buffers` exposes the rendered text used by Copy All.
- Package image nodes can load HTTP, local, and avares content automatically.
  They are therefore excluded at parsing time, rather than merely hidden.
- Link callbacks exist (`LinkClick`/`LinkCommand`), but links are not enabled here.
- The package does not implement HTML rendering; the wrapper also disables HTML
  parsing explicitly. It removes link/image and autolink parsers. No URI is launched.

`InformationDocumentView` owns all LiveMarkdown API usage. ViewModels expose
`UserDocument`; they never own third-party builders, parse trees, or controls.
The wrapper compares source strings before replacing the document. It exposes
`Document` and `ShowToolbar`; there is deliberately no external-links opt-in yet.
Any future navigation support must go through MissionPlanner navigation and keep
images disabled unless explicitly authorized by the feature.

Copy All joins the renderer's text buffers with newlines (table cells become
separate readable lines). Copy Markdown copies exactly `UserDocument.Markdown`.
Both use the owning Avalonia TopLevel clipboard, which also supports browser
hosting without assuming a desktop Window. Feedback is non-modal and clears on
the next document change. Clipboard failures stay local to the toolbar.

## Theme and sizing

`InformationDocumentStyles.axaml` is scoped to the wrapper. Package defaults remain
separate. Renderer resources map foreground, inline code, table/card and secondary
backgrounds, borders, and quote borders to the existing Semi resources on attachment
and light/dark changes. Normal text and heading sizes use the package's 14/16/18/20/24
scale; code highlighting follows LightPlus/DarkPlus. No Markdown embeds colors.

Documents use the page's scroll viewer without a fixed height. Compass uses a
maximum width rather than a minimum desktop width. Long raw telemetry/log streams
belong in dedicated viewers, not in this control.

## Compass reference

`CompassSetupDocumentFactory` performs no I/O. It consumes the existing semantic
Compass service state and calibration/pending presentation context. It distinguishes
confirmed values from pending edits. Only current service arming evidence is shown;
historical diagnostic text is not promoted into a blocker. Missing parameters are
omitted from the evidence table. Device IDs are retained without invented sensor models.

The existing one-second registry refresh still catches health expiry and changed
configuration. `CompassDocumentContext.HasSameContent` excludes observation timestamps
and unrelated diagnostics, preventing factory calls on redundant samples. A second
source comparison prevents redundant renderer updates. No high-rate telemetry
subscription was added. Actual semantic changes may reset selection; unchanged refreshes
preserve it.

## Choosing a control

| Content | Control |
| --- | --- |
| Short label/status | TextBlock |
| A few selectable lines | SelectableTextBlock |
| Explanation, report, table | InformationDocumentView |
| Raw logs or huge telemetry | Dedicated read-only log/text viewer |
| Interactive settings | Native Avalonia controls |

## Manual acceptance checklist

Builds and deterministic tests cannot establish real-device UX correctness. Before
migrating Arming or other Setup pages, check the remaining drones:

- Disabled/no compass and enabled/detected compass, including stale health evidence.
- Calibration start/progress/accept/cancel, yaw source, and orientation.
- Pending dependencies, Discard, Apply/readback, partial failure, metadata defaults,
  and explicitly requested reboot.
- Cross-block drag selection and Ctrl+C through headings, paragraphs, tables,
  inline code, and code blocks; Copy All and exact Copy Markdown.
- Narrow and normal widths, high DPI, long documents, and light/dark theme changes.
- Disconnect/reconnect during pending edits and calibration.
- Browser/WASM runtime rendering, selection, clipboard permission/feedback, and
  no image fetches or URI actions from hostile input.

Automated headless tests cover cross-block drag selection, Ctrl+C, both copy
actions, unchanged-document retention, light/dark resource changes, table rendering,
360-pixel and 1100-pixel layouts, 2× scaling, long content, and disabled image/link
nodes. Fifteen golden reports cover the major Compass variants. These checks do not
replace visual inspection or platform-specific clipboard testing in a real browser.

The checklist remains unverified on hardware. No other Setup page is migrated.

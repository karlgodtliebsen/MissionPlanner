# MissionPlanner Next Gen — Setup / Advanced Codex task bundle

## Objective

Complete **Setup → Advanced** in MissionPlanner Next Gen while preserving the modern .NET 10/Avalonia architecture and Browser/WASM support.

The current page is only a centered `Advanced setup` placeholder and its ViewModel has no behavior. The legacy `ConfigAdvanced` page exposes thirteen tools. This bundle recreates those capabilities as modern, testable features rather than direct WinForms ports.

## Legacy parity inventory

| ID | Legacy launch point | Next Gen task |
|---:|---|---|
| 01 | `WarningsManager` | Warning Manager |
| 02 | `MAVLinkInspector` | MAVLink Inspector |
| 03 | `ProximityControl` | Proximity Viewer |
| 04 | `AuthKeys` | MAVLink 2 Signing |
| 05 | `SerialOutputPass` | MAVLink Output / Mirror |
| 06 | `SerialOutputNMEA` | NMEA Output |
| 07 | `FollowMe` | Follow Me |
| 08 | Parameter metadata parser/repositories | Parameter Metadata Generator |
| 09 | `MovingBase` | Moving Base |
| 10 | `Privacy.anonymise` | Anonymous Log Export |
| 11 | `fftui` | FFT Analysis |
| 12 | `SpectrogramUI` | Spectrogram |
| 13 | `SerialSupportProxy` | Serial Support Proxy |

`ConfigTerminal` and `ConfigREPL` are separate children added by legacy `InitialSetup.cs`; they are intentionally outside this `ConfigAdvanced` parity bundle.

## Execution order

Run one task at a time, in filename order. Commit after each accepted task.

1. `00` establishes the Advanced hub, navigation contract, capability model, and lifecycle.
2. `01`–`04` add vehicle/telemetry tools.
3. `05` establishes reusable output-sink infrastructure; `06` reuses it.
4. `07`–`10` add advanced operational and data-management tools.
5. `11` establishes reusable signal-analysis primitives; `12` reuses them.
6. `13` adds the support proxy.
7. `14` performs complete parity, platform, accessibility, lifecycle, build, and test validation.

## Dependency graph

- Task `06` depends on task `05`.
- Task `12` depends on task `11`.
- Task `14` depends on tasks `00`–`13`.
- Other tasks depend only on task `00`, but must reuse abstractions introduced by earlier completed tasks.

## Platform policy

All thirteen tools remain visible in the Advanced hub. Availability is evaluated at runtime and represented as one of:

- Available
- Connection required
- Vehicle required
- Permission required
- Unsupported on this platform
- Temporarily unavailable, with a concrete reason

Do not hide a tool because a platform cannot provide its required capability. Disable its launch action and explain the missing capability. Browser support is feature-specific:

- Telemetry-only tools should normally work through the current browser transport.
- Geolocation should use browser permission APIs through an abstraction.
- File workflows should use browser upload/download abstractions and respect memory limits.
- Serial/socket forwarding must use an audited bridge when available; otherwise remain visibly unavailable.
- Secure key persistence must be implemented safely or explicitly limited to the current session.

## Global definition of done

- Setup → Advanced contains all thirteen parity entries and no placeholder-only pages.
- Shared logic is UI-framework-independent and covered by unit tests.
- Navigation, disconnects, page disposal, cancellation, and repeated open/close cycles do not leak subscriptions, tasks, transports, or sensitive state.
- Browser/WASM and desktop builds succeed.
- Existing tests remain green.
- User-visible failures are actionable and do not report success before the underlying operation is verified.
- Each task's implementation contains no unexplained `TODO`, `NotImplementedException`, empty command, or dummy success path.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 00-advanced-hub-and-platform-capabilities.md -->

# ADV-00 — Build the Setup / Advanced hub and platform-capability contract

**Priority:** P0  
**Dependencies:** None  
**Scope:** Foundation and navigation only; do not implement the thirteen tool internals in this task.

## Goal

Replace the current placeholder with a responsive Advanced tools hub that exposes every legacy `ConfigAdvanced` capability, routes to modern child pages, and explains platform/connection availability consistently.

## Required implementation

### 1. Define a stable feature catalog

Introduce presentation-neutral identifiers and descriptors. Names may follow existing project conventions, but the model must represent at least:

- Stable feature ID
- Display title
- Concise description
- Category
- Risk/help text
- Sort order
- Connection/vehicle/parameter/platform prerequisites
- Availability state
- Human-readable unavailability reason
- Navigation target or launch command

The catalog must include exactly these parity features:

1. Warning Manager
2. MAVLink Inspector
3. Proximity
4. MAVLink Signing
5. MAVLink Output / Mirror
6. NMEA Output
7. Follow Me
8. Parameter Metadata Generator
9. Moving Base
10. Anonymous Log Export
11. FFT Analysis
12. Spectrogram
13. Serial Support Proxy

### 2. Add an availability service

Create an injected service that derives availability from existing application state and platform capabilities. It must react to at least:

- Current platform/runtime
- Browser bridge availability
- Connection state
- Active vehicle availability
- Parameter-load state where applicable
- File open/save support
- Serial and network endpoint support
- Location/geolocation support and permission state
- Secure-key storage support

Do not query global singletons directly from the view. Do not encode platform tests throughout individual ViewModels.

### 3. Implement the Avalonia hub

Replace the placeholder with a scrollable, responsive card/list layout consistent with Mandatory Hardware and Optional Hardware styling. Each entry must show:

- Title and description
- Current availability/status
- Required warning for risky operations
- An enabled launch action only when prerequisites are met
- The exact reason when disabled

All thirteen entries must remain visible, including in Browser/WASM.

### 4. Establish child navigation and lifecycle

Use the application's existing navigation/content-host conventions rather than opening native windows. Define a common lifecycle for tool pages so that leaving a page:

- Cancels active work where appropriate
- Unsubscribes from telemetry streams
- Closes owned transports/sinks
- Clears sensitive transient state
- Does not dispose shared application services

Repeatedly opening and closing a tool must not accumulate subscriptions or commands.

### 5. Provide test seams

The feature catalog and availability evaluation must be testable without Avalonia startup. Use fake platform capabilities, fake connection state, and fake vehicle state.

## Acceptance criteria

1. Setup → Advanced no longer displays only `Advanced setup`.
2. Exactly thirteen parity cards/items are visible and ordered deterministically.
3. A disconnected desktop session and a disconnected browser session produce different, correct availability reasons where their capabilities differ.
4. Connection-state changes update relevant entries without recreating the application shell.
5. Unsupported features remain visible and disabled with a nonempty reason.
6. A card with satisfied prerequisites navigates to its registered child page through the existing navigation system.
7. Opening and closing the same child page ten times leaves one or zero active subscriptions according to the page lifecycle, never ten.
8. Keyboard navigation, focus indicators, screen-reader labels, and narrow-window layout are usable.

## Required tests

- Feature catalog contains all thirteen unique IDs and titles.
- Catalog order is stable.
- Availability matrix tests for desktop, Browser/WASM with bridge, and Browser/WASM without bridge.
- Connection-required and vehicle-required transitions.
- Permission-required state for location.
- Navigation registry rejects duplicate IDs and missing targets.
- Lifecycle/disposal test using a fake subscribed tool page.

## Out of scope

- Implementing actual Warning Manager, inspection, signing, forwarding, analysis, or other tool behavior.
- Terminal and Script REPL.
- Hiding unfinished tools to make the page appear complete.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 01-warning-manager.md -->

# ADV-01 — Implement Warning Manager

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Implement a modern rule-based warning system equivalent in purpose to legacy `WarningsManager`, using current vehicle-state and telemetry infrastructure and remaining independent of Avalonia.

## Required implementation

### 1. Warning rule domain model

Create a serializable model that supports at least:

- Stable rule ID and user-visible name
- Enabled state
- Telemetry/state source selection
- Comparison operator: `<`, `<=`, `==`, `!=`, `>=`, `>`, inside range, outside range
- One or two threshold values as required by the operator
- Severity
- User message template
- Activation delay/debounce
- Hysteresis or clear threshold
- Repeat/cooldown interval
- Optional acknowledgement requirement

Validate rule/source/operator/type combinations before persistence. Invalid rules must never enter the evaluator.

### 2. Source catalog

Build source descriptors from the authoritative Next Gen vehicle-state/telemetry model. Do not hardcode reflection against UI ViewModels. Each source descriptor must define value type, display name, units, and whether the value can be unavailable/stale.

### 3. Deterministic evaluation engine

Implement a service that consumes state updates and produces warning lifecycle events:

- Inactive → pending
- Pending → active after activation delay
- Active → acknowledged, if applicable
- Active/acknowledged → cleared using hysteresis
- Repeated notification only after cooldown

Inject a clock/time provider. Handle missing, NaN, infinite, and stale source values explicitly. A source becoming unavailable must not generate an endless alert loop.

### 4. Persistence

Persist user rules through an injected repository appropriate to current application settings architecture. Writes must be atomic. Corrupt data must be diagnosed and isolated without preventing application startup.

### 5. UI

Add an Advanced child page with:

- Rule list with enable/disable, severity, source, condition, and state
- Create/edit/delete/duplicate
- Validation errors adjacent to fields
- Test/preview against a supplied value without affecting live warning state
- Active warning panel with timestamp, current value, message, acknowledge, and clear state
- Import/export only if a reusable settings file service already exists; otherwise leave this out rather than add a second file framework

Optional speech/system notification behavior must be behind a platform capability/service. Browser audio restrictions and permission failures must be visible, not silently ignored.

## Acceptance criteria

1. A valid threshold rule activates only after its configured delay.
2. Hysteresis prevents threshold chatter.
3. Cooldown prevents repeated notifications before the configured interval.
4. Acknowledging one warning does not acknowledge another.
5. Rules survive application restart through the repository.
6. Corrupt persisted rules are reported and skipped; valid rules still load.
7. Disconnect/stale telemetry leaves each warning in a defined state and does not fabricate a current value.
8. Browser/WASM supports rule management and visual warnings; unavailable speech/notification capability is accurately indicated.

## Required tests

- All operators, including inclusive boundaries and ranges.
- Activation delay, hysteresis, cooldown, acknowledgement, and clearing using a fake clock.
- Missing/stale/NaN/infinite inputs.
- Rule validation and serialization round trip.
- Corrupt repository content recovery.
- Subscription disposal and no duplicate evaluation after repeated navigation.

## Out of scope

- Automatically changing vehicle parameters or flight modes in response to a warning.
- Reusing the legacy WinForms warning classes directly.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 02-mavlink-inspector.md -->

# ADV-02 — Implement MAVLink Inspector

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Provide a live, low-overhead inspector for raw and decoded MAVLink traffic, equivalent in purpose to legacy `MAVLinkInspector`, without introducing another parser or compromising signed/unknown frames.

## Required implementation

### 1. Reuse the existing message pipeline

Locate the current raw-frame and decoded-message observation points. Add a read-only inspection tap that:

- Never consumes messages needed by normal processing
- Does not decode frames a second time when decoded data already exists
- Preserves raw bytes and MAVLink 2 signature bytes
- Includes unknown/unsupported message IDs
- Can distinguish inbound and outbound traffic when the pipeline exposes both

### 2. Aggregation service

Maintain bounded statistics keyed by direction, system ID, component ID, and message ID:

- Message name when known
- Count
- Total bytes
- First and last observation time
- Rolling message rate and byte rate over a documented window
- Payload length
- Latest sequence number
- Latest decoded field snapshot, when available
- Signature/verification status, when available

Use a deterministic time provider. Do not retain an unbounded history. Define and test the overflow/retention policy.

### 3. UI

Implement:

- Search/filter by name, ID, system, component, and direction
- Sortable aggregate list
- Detail view with decoded fields and raw hexadecimal bytes
- Pause/freeze display without stopping message collection, plus an optional explicit collection pause
- Clear statistics
- Copy selected row/details through the existing clipboard abstraction
- Export a bounded snapshot through the existing save service when supported
- Visible dropped/omitted observation counter if the inspection channel overflows

Throttle/coalesce UI refreshes; high-rate telemetry must not schedule one dispatcher operation per frame.

### 4. Lifecycle

Subscribe only while the page/tool is active, or through one shared inspector service with explicit observer leases. Closing the page must release its lease immediately.

## Acceptance criteria

1. Known and unknown MAVLink messages appear in the inspector.
2. Signed frames retain their signature bytes in raw display/export.
3. Aggregate counts match injected fixtures exactly.
4. Pause display freezes visible values without corrupting later totals.
5. Filtering does not mutate the underlying aggregate collection.
6. A sustained high-rate fake stream keeps memory bounded and the UI responsive.
7. Browser/WASM operates over the current browser transport/bridge with the same aggregate behavior.
8. Navigating away and back does not double counts because of duplicate subscriptions.

## Required tests

- Aggregation key separation for direction/system/component/message.
- Rate calculation with fake time.
- Bounded-buffer/overflow policy.
- Unknown-message and signed-frame preservation.
- Search and numeric filters.
- Pause, clear, export snapshot, and disposal behavior.
- A load test using a deterministic generated stream.

## Out of scope

- Editing or injecting arbitrary MAVLink frames.
- Replacing the production decoder or transport.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 03-proximity-view.md -->

# ADV-03 — Implement Proximity Viewer

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Implement a live proximity/obstacle visualization equivalent in purpose to legacy `ProximityControl`, using existing MAVLink decoding and vehicle-state services.

## Required implementation

### 1. Telemetry inputs

Inspect the current MAVLink library before adding anything. Support the messages needed by current ArduPilot proximity reporting, at minimum where available:

- `DISTANCE_SENSOR`
- `OBSTACLE_DISTANCE`

Add missing records/decoders/handlers only through the established message infrastructure. Retain sensor ID, type, orientation/frame, angular increment/offset, min/max distance, covariance/quality, and observation time where the protocol supplies them.

### 2. Normalized proximity model

Create a UI-independent model/service that:

- Normalizes distances to meters
- Normalizes angles to a documented convention
- Represents unknown, out-of-range, too-close, and stale values distinctly
- Combines multiple sensors without losing source identity
- Computes nearest valid obstacle and its bearing
- Exposes sector/point snapshots suitable for rendering
- Uses a configurable staleness timeout and injected clock

Do not infer that zero means a valid obstacle unless the protocol definition says so.

### 3. UI

Implement a responsive polar/radar-style view and a tabular diagnostic view. Show:

- Vehicle-relative sectors/points
- Distance rings and orientation labels
- Nearest obstacle and bearing
- Sensor identity/type/orientation
- Min/max range
- Last update age and stale state
- Unsupported/malformed sample counts

Rendering must be retained/coalesced rather than rebuilding a large visual tree on every message.

### 4. Message-rate ownership

Do not independently request telemetry rates if an existing stream-rate coordinator exists. If rate requests are necessary, acquire/release them through the established ownership mechanism so other pages are not disrupted.

## Acceptance criteria

1. Fixture messages produce the expected normalized bearings and distances.
2. Multiple sensors remain distinguishable and combine deterministically.
3. Stale data visibly changes state and is excluded from nearest-obstacle calculation.
4. Unknown/out-of-range samples are not plotted as real obstacles.
5. The page remains responsive under high-rate obstacle arrays.
6. Browser/WASM renders the same normalized snapshot from its telemetry connection.
7. Closing the page releases all subscriptions/rate leases.

## Required tests

- Unit conversions and angular normalization, including wraparound.
- `DISTANCE_SENSOR` orientations and invalid values.
- `OBSTACLE_DISTANCE` array indexing, offsets, increments, and sentinel values.
- Multi-sensor merge and nearest-obstacle selection.
- Staleness with fake time.
- Renderer/ViewModel update coalescing and disposal.

## Out of scope

- Collision avoidance commands or automatic flight intervention.
- A 3D point-cloud viewer.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 04-mavlink2-signing.md -->

# ADV-04 — Implement MAVLink 2 Signing

**Priority:** P0 security  
**Dependencies:** ADV-00

## Goal

Implement safe MAVLink 2 signing setup, key management, outbound signing, and inbound verification equivalent in purpose to legacy `AuthKeys`.

## Security requirements

This task handles secret key material. Never log, trace, serialize to diagnostics, include in exceptions, copy implicitly to clipboard, or retain it longer than necessary. Use constant-time comparison where appropriate and clear mutable buffers when practical.

## Required implementation

### 1. Protocol primitives

Inspect existing signing support first. Implement or complete standards-compliant MAVLink 2 signing:

- 32-byte secret key
- Link ID
- 48-bit timestamp in MAVLink signing units/epoch
- Signature generation over the exact required packet bytes
- Inbound signature verification
- Replay/old-timestamp rejection per link/source policy
- Verification status propagated with inspected frames without dropping valid unsigned traffic unless policy explicitly requires signing

Use known official vectors or generated cross-implementation fixtures.

### 2. Vehicle setup workflow

Configure the vehicle using the supported MAVLink signing setup mechanism (including `SETUP_SIGNING` where appropriate) through existing connection and acknowledged-command infrastructure. The workflow must:

1. Confirm a connected target vehicle.
2. Present a clear irreversible/lockout warning.
3. Validate key/link/timestamp inputs.
4. Send the setup request.
5. Wait for protocol-level confirmation or a verifiable signed exchange.
6. Enable local outbound signing only after successful vehicle setup.
7. Report partial failure accurately and offer a safe recovery path.

Never report success solely because bytes were written.

### 3. Key lifecycle and storage

Support:

- Cryptographically secure key generation
- Explicit import and export with confirmation
- Display as masked/fingerprint form by default
- Link ID selection/validation
- Session status: disabled, configuring, active, verification failing, unavailable
- Secure persistence through an injected secure-storage abstraction where the platform supports it

For Browser/WASM, implement a safe supported storage option or explicitly offer session-only use. Do not silently put the key in ordinary local storage.

### 4. UI and diagnostics

Show signing state, key fingerprint, link ID, signed/unsigned/invalid/replay counters, last verified timestamp, and actionable error information. Key reveal/export requires an explicit user action and confirmation.

## Acceptance criteria

1. Signing output matches known vectors byte-for-byte.
2. Valid signatures verify; changed payload/header/signature bytes fail verification.
3. Replay/old timestamp policy rejects duplicated stale signed frames without rejecting a valid newer frame.
4. Local outbound signing is not activated before vehicle setup is confirmed.
5. Failed/timeout setup leaves the previous operational state intact where possible and never displays success.
6. Logs and exception messages contain no key material.
7. Browser/WASM clearly states secure-persistence/session limitations and still builds.
8. Restart restores only the platform-approved persisted state.

## Required tests

- Known-vector generation and verification.
- Timestamp encoding, monotonicity, wrap/boundary handling, and restart restoration.
- Replay detection by source/link.
- Key import validation and fingerprinting.
- Redaction tests for logs/exceptions/diagnostics.
- Setup workflow success, rejection, timeout, disconnect, and cancellation using fake transport/ACK services.
- Secure-storage unavailable and Browser session-only paths.

## Out of scope

- Inventing a proprietary signing protocol.
- Logging keys for troubleshooting.
- Enforcing signed-only traffic globally without a separately reviewed policy.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 05-mavlink-output-mirror.md -->

# ADV-05 — Implement MAVLink Output / Mirror

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Implement raw MAVLink forwarding equivalent in purpose to legacy `SerialOutputPass`, while creating reusable output-sink infrastructure for NMEA and related features.

## Required implementation

### 1. Raw frame tap

Forward complete raw MAVLink frames from the authoritative connection pipeline. Preserve frame bytes exactly, including MAVLink 2 incompatibility flags, extensions, checksum, and signature. Do not decode/re-encode frames for mirroring.

The user must be able to select direction where the pipeline supports it:

- Inbound from vehicle
- Outbound to vehicle
- Both, with clear loop-safety rules

### 2. Reusable endpoint/sink abstraction

Create or extend a UI-independent abstraction for byte/text output endpoints. Support only transports already appropriate to the platform architecture, normally:

- Serial
- UDP client/output
- TCP client
- TCP listener/host only when current security/platform conventions permit it

Represent endpoint configuration as validated profiles. Do not leak native serial/socket types into shared ViewModels.

### 3. Forwarding session

A session must expose:

- Starting, active, reconnecting, stopping, stopped, faulted
- Endpoint summary
- Frames/bytes forwarded
- Frames/bytes dropped
- Last successful write
- Last error, sanitized
- Configurable reconnect policy
- Bounded queue and documented overflow policy

Use one active owner per configured endpoint. Stop/dispose must complete predictably even when a write is blocked.

### 4. Feedback-loop protection

Prevent a mirrored stream from being re-ingested and mirrored indefinitely. Use transport/session identity or a clearly documented equivalent. Warn before mirroring bidirectionally to an endpoint that could route back to the same connection.

### 5. UI and platform behavior

Implement endpoint editing, validation, start/stop, status, counters, and errors. Browser/WASM may use the existing BrowserBridge only through an explicit bridge capability. Without a suitable bridge, keep the tool visible and unavailable with a concrete reason.

## Acceptance criteria

1. A signed input frame arrives at the fake sink byte-for-byte unchanged.
2. Ordering is preserved for accepted frames.
3. Queue overflow follows the documented policy and increments visible drop counters.
4. Stop/cancellation releases the endpoint and frame subscription with no background writer left running.
5. Reconnect behavior is bounded and does not spin.
6. Feedback-loop fixtures do not recurse indefinitely.
7. Invalid endpoint profiles cannot start a session.
8. Browser/WASM either forwards through the audited bridge or reports the missing bridge capability; it never invokes raw sockets directly from shared browser code.

## Required tests

- Byte-exact forwarding for MAVLink 1, MAVLink 2, signed MAVLink 2, and unknown messages.
- Direction filtering and ordering.
- Bounded queue overflow and counters.
- Endpoint profile validation.
- Start/stop/restart, write timeout, endpoint failure, reconnect, disconnect, and cancellation.
- Single-owner enforcement and loop prevention.
- Browser capability paths.

## Out of scope

- Modifying, filtering, or synthesizing forwarded MAVLink frames.
- Exposing unauthenticated network listeners by default.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 06-nmea-output.md -->

# ADV-06 — Implement NMEA Output

**Priority:** P1  
**Dependencies:** ADV-00, ADV-05

## Goal

Implement NMEA 0183 output equivalent in purpose to legacy `SerialOutputNMEA`, reusing the endpoint/session abstractions from ADV-05.

## Required implementation

### 1. NMEA formatter

Create a UI-independent formatter driven by the authoritative vehicle state. At minimum support:

- GGA: time, latitude, longitude, fix quality, satellites, HDOP, altitude, geoid separation when known
- RMC: time/date, validity, latitude, longitude, speed over ground, course over ground

Add VTG only if it is useful and can be populated correctly. Do not emit fabricated values as though they were measured.

Formatting requirements:

- Coordinates in NMEA degrees/minutes format with correct zero padding
- Correct N/S/E/W hemisphere
- UTC date/time
- Invariant culture and `.` decimal separator
- Knots for NMEA speed fields
- XOR checksum between `$` and `*`
- Uppercase two-digit hexadecimal checksum
- `\r\n` termination
- Explicit invalid/no-fix status when data is unavailable or stale

### 2. Output scheduling

Support a configurable sentence set and output rate within validated limits. Use a monotonic time source. Coalesce state updates into scheduled sentence emission rather than emitting on every telemetry message.

### 3. Endpoint reuse

Use the ADV-05 output endpoint abstraction and lifecycle. Do not create separate serial/TCP/UDP implementations. Text encoding must be explicitly ASCII-compatible.

### 4. UI

Provide:

- Sentence selection
- Output rate
- Endpoint profile
- Live preview of the latest complete sentence set
- Start/stop and session state
- Sentence/byte/drop/error counters
- Clear indication of stale/no-fix source data

Browser availability follows the endpoint capability/bridge behavior established in ADV-05.

## Acceptance criteria

1. Known input states produce exact expected GGA and RMC strings, checksums, and CRLF endings.
2. Negative latitude/longitude produce correct hemispheres and absolute coordinate fields.
3. Speed conversion from meters/second to knots is correct.
4. Missing or stale GPS data emits a defined invalid/no-fix representation rather than a valid fix.
5. Output rate remains within tolerance under a fake clock and does not multiply when navigating back to the page.
6. Endpoint failures are reported by the shared session model.
7. Browser/WASM behavior is consistent with the shared endpoint capability.

## Required tests

- Golden strings for northern/southern/eastern/western coordinates.
- Coordinate carry/rounding near degree and minute boundaries.
- UTC midnight/date transition.
- Checksums and line endings.
- Missing HDOP, satellites, altitude, course, speed, and stale fix.
- Scheduler rate, cancellation, restart, and endpoint failure.

## Out of scope

- Parsing NMEA input; that belongs to Moving Base.
- Proprietary NMEA sentences unless separately specified.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 07-follow-me.md -->

# ADV-07 — Implement Follow Me

**Priority:** P0 safety  
**Dependencies:** ADV-00

## Goal

Implement an explicit, permission-aware Follow Me workflow equivalent in purpose to legacy `FollowMe`, using platform location services and supported ArduPilot commands without silently arming or changing mode.

## Required implementation

### 1. Location-provider abstraction

Define an injected provider that exposes:

- Capability and permission state
- Start/stop observation
- Latitude, longitude, altitude when available
- Horizontal/vertical accuracy
- Course/speed when available
- Timestamp and stale state
- Provider error/status

Implement platform adapters only where supported by current projects. Browser geolocation requires secure context and explicit permission. Permission denied/unavailable must be an actionable state.

### 2. Follow target domain service

Inspect the legacy behavior and current ArduPilot/MAVLink support, then select a standards-compliant command/message strategy based on actual vehicle capability. The service must:

- Require a connected vehicle
- Validate vehicle type/mode/capability
- Reject stale or insufficiently accurate operator location
- Rate-limit updates
- Apply configured horizontal/vertical offsets and altitude-frame semantics
- Track last requested and last accepted target update
- Stop on disconnect, permission loss, location loss, page exit, or explicit user stop
- Use existing acknowledged-command/operation-gate infrastructure where applicable

Do not auto-arm. Do not silently change the flight mode. When a compatible mode is required, explain it and require a separate explicit user action through existing mode-change UI/service.

### 3. Safety UI

Show:

- Location permission and provider state
- Current operator location age and accuracy
- Current vehicle mode/armed state
- Chosen update interval/minimum movement
- Altitude source/frame and offsets
- Last sent/accepted target
- Start/stop with a prominent running indicator
- Detailed reason why start is disabled

Starting requires a confirmation that explains vehicle motion risk. Ensure the command cannot remain running after the user leaves the page.

## Acceptance criteria

1. Start is impossible without connection, compatible vehicle state, valid permission, and fresh/accurate location.
2. Starting does not arm or change vehicle mode.
3. Location updates are rate/distance limited according to configuration.
4. Disconnect, stale location, permission revocation, page close, and cancellation each stop the workflow deterministically.
5. An acknowledgement/rejection is surfaced accurately where the chosen protocol provides it.
6. Browser/WASM uses browser geolocation only after permission and secure-context checks.
7. Reopening the page does not leave a previous location watcher or follow loop active.

## Required tests

- Provider capability/permission state transitions.
- Accuracy and staleness gates with fake time.
- Rate and minimum-distance filtering using a deterministic route.
- Coordinate/offset/altitude-frame mapping.
- Vehicle capability/mode checks.
- Command accepted, rejected, timeout, disconnect, permission loss, and cancellation.
- Assertions that arm/mode commands are never invoked by Follow Me start.

## Out of scope

- Autonomous mode selection.
- Background Follow Me after application/page shutdown.
- Using IP geolocation as a substitute for device GPS.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 08-parameter-metadata-generator.md -->

# ADV-08 — Implement Parameter Metadata Generator / Refresh

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Modernize the legacy `BUT_paramgen_Click` workflow so advanced users/developers can refresh, validate, inspect, and atomically install parameter metadata without hardcoded obsolete ArduPilot branch URLs.

## Required implementation

### 1. Reuse current metadata architecture

Inspect all current metadata loaders, repositories, caches, XML/PDEF sources, and parameter-editor consumers. Extend the canonical pipeline rather than adding a parallel format. Ensure vehicle-specific parameters and library parameters continue to merge correctly.

### 2. Source manifest

Represent metadata sources in a versioned/configurable manifest with:

- Source URI or embedded source
- Vehicle/firmware family
- Channel/version information
- Expected content type/format
- Optional checksum/signature/ETag metadata
- Priority/merge rules

Do not hardcode Copter 3.5/3.6 or other obsolete branches. Defaults must refer to maintained official ArduPilot metadata sources already used by the application or verified current official endpoints.

### 3. Refresh pipeline

Implement:

1. Resolve sources.
2. Download/read with bounded retries and cancellation.
3. Validate transport result and content.
4. Parse to the canonical metadata model.
5. Normalize and merge deterministically.
6. Produce diagnostics for duplicate/conflicting/malformed definitions.
7. Compare against the installed cache and show a summary.
8. Write to a temporary location.
9. Atomically replace the installed cache only after complete success.
10. Reload the canonical repository and verify representative lookups.

A partial download or parse failure must leave the previous metadata intact.

### 4. UI

Show source/channel selection, progress, cancellation, per-source result, counts, warnings/errors, before/after summary, cache location abstraction, and last successful refresh. Make the developer/advanced nature clear.

Browser/WASM must use browser-safe HTTP and cache/download abstractions. When persistent replacement is impossible, permit generating/downloading the artifact or show a concrete unavailable state; do not attempt native paths.

## Acceptance criteria

1. Current official sources are configurable and no obsolete legacy URLs are embedded in code.
2. Vehicle and library metadata merge into one canonical lookup result.
3. A failed source or malformed document does not replace the current cache.
4. Successful replacement is atomic and the repository reload is verified.
5. Conflicts are deterministic and reported with source provenance.
6. Cancellation leaves no installed partial output.
7. Offline mode continues using the last known valid cache.
8. Browser/WASM builds and follows its declared cache/download behavior.

## Required tests

- Source-manifest parsing and validation.
- Vehicle plus library merge with golden fixtures.
- Duplicate/conflict precedence and diagnostics.
- Malformed/truncated/HTML-error documents.
- Network timeout/retry/cancellation through a fake HTTP source.
- Atomic replacement and rollback.
- Repository reload/representative lookup verification.
- Browser persistence-unavailable/download path.

## Out of scope

- Scraping arbitrary web pages.
- Changing vehicle parameter values.
- Retaining the legacy branch list as a hidden fallback.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 09-moving-base.md -->

# ADV-09 — Implement Moving Base

**Priority:** P0 safety  
**Dependencies:** ADV-00

## Goal

Modernize legacy `Controls/MovingBase.cs`: read NMEA position from a selected source, validate it, expose moving-base state, and optionally update the vehicle's base/rally data through proper domain services.

## Legacy behavior to preserve deliberately

The legacy tool accepts serial, TCP host/client, or UDP host/client input; parses `$GPGGA`/`$GNGGA`; checks checksum and fix validity; converts position/altitude; updates the vehicle base position; optionally updates one rally point; and writes a diagnostic text file. Preserve useful behavior, not its static fields, unbounded retry settings, UI-thread access, or raw `Thread` loop.

## Required implementation

### 1. Input-source abstraction

Reuse or extend transport endpoint abstractions where appropriate. Support available serial/TCP/UDP input modes behind injected capabilities. Use async reads, cancellation, bounded buffering, timeouts, and explicit ownership.

Browser/WASM may consume a bridge-provided stream only if the bridge capability is explicit and audited. Otherwise the feature remains visible but unavailable.

### 2. Incremental NMEA parser

Implement a reusable parser that handles:

- Fragmented lines across reads
- Multiple lines in one read
- `\r`, `\n`, and `\r\n` boundaries
- `$GPGGA` and `$GNGGA`
- XOR checksum validation
- Fix-quality validation
- Latitude/longitude degrees/minutes conversion
- N/S/E/W hemispheres
- Altitude and units
- Satellites and HDOP
- Malformed, oversized, unsupported, and stale data

Use invariant culture. Set a maximum sentence length and recover after malformed input.

### 3. Moving-base service

Expose source state, latest valid fix, age, error counters, last applied base update, and optional rally update state. Update the authoritative vehicle/base model through an explicit service rather than writing UI/global state.

If rally-point updates are retained:

- Make them opt-in with a clear warning
- Validate vehicle/firmware support
- Rate-limit to a safe documented interval
- Preserve or explicitly define existing rally collection behavior
- Use acknowledged mission/parameter services where possible
- Never silently overwrite unrelated rally points

Define relative-versus-absolute altitude semantics and display them clearly.

### 4. UI and diagnostics

Provide endpoint configuration, connect/disconnect, update rate, parsed position/fix/satellites/HDOP/altitude, stale/error status, optional rally toggle, and sanitized session log/export. Do not write precise position logs automatically without user awareness.

## Acceptance criteria

1. Fragmented valid GGA input produces one correct fix.
2. Invalid checksum/no-fix sentences do not update the base.
3. Latest fix becomes stale after the configured timeout.
4. Base updates follow the configured rate and stop immediately on disconnect/cancel/page exit.
5. Optional rally updates are explicit, supported, rate-limited, and do not overwrite unrelated points.
6. No static worker/thread/session state remains from the legacy pattern.
7. Reconnect/reopen creates a fresh parser/session and does not reuse stale channels.
8. Browser/WASM uses the declared bridge capability or displays an unavailable reason.

## Required tests

- Golden GGA samples for both talker IDs and all hemispheres.
- Fragmentation, batching, line endings, maximum length, noise, malformed fields, bad checksum, and no fix.
- Coordinate and altitude conversion.
- Staleness and update-rate behavior with fake time.
- Input failure/reconnect/cancellation/disposal.
- Base-update and optional rally-update service calls with fakes.
- Assertions that unrelated rally points remain unchanged.

## Out of scope

- Direct port of legacy static/thread code.
- Automatic rally-point overwrite without confirmation.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 10-anonymous-log-export.md -->

# ADV-10 — Implement Anonymous Log Export

**Priority:** P0 privacy  
**Dependencies:** ADV-00

## Goal

Implement streaming anonymization for MissionPlanner telemetry/dataflash logs equivalent in purpose to legacy `Privacy.anonymise`, with a documented privacy policy and verifiably parseable output.

## Required implementation

### 1. Define the anonymization policy

Document in code and user help what is transformed, removed, retained, and not guaranteed. Cover at least:

- Absolute latitude/longitude and home/origin/base/rally coordinates
- GPS-derived positions in relevant MAVLink/DataFlash records
- Text/status fields that may contain coordinates or identifiers where safely detectable
- Device/vehicle identifiers and serial numbers where format permits
- Timestamps and relative motion data
- Unknown/unparsed records

Prefer a consistent geographic translation/rotation strategy when retaining route shape is useful; otherwise redact fields according to format constraints. The same source location must transform consistently across the file. Never claim full anonymization when unknown records remain uninspected.

### 2. Format-aware streaming pipeline

Support the legacy file types that the current parsers can safely read/write:

- `.tlog`
- `.bin`
- `.log`

Reuse canonical parsers/codecs. Preserve framing, timing, checksums, signatures/format integrity, and unknown records whenever possible. Do not load an entire large log into memory.

Write to a temporary output and commit/rename only after successful completion. Cancellation/error removes partial output unless the user explicitly chooses to retain a diagnostic artifact.

### 3. Verification

After writing, re-open the output with the canonical parser and verify:

- It is structurally parseable
- Expected record/message counts are within documented tolerances
- Targeted sensitive coordinate classes no longer contain original absolute positions within a defined tolerance
- The original file is unchanged

Provide a report of transformed, removed, retained, unknown, and failed record counts.

### 4. UI/platform behavior

Use existing open/save abstractions. Show file size, detected format, policy summary, progress, cancellation, output report, and warning about unknown/unverified content. Do not log source coordinates or telemetry payloads.

Browser/WASM should use upload/download streams and enforce explicit memory/size constraints. If the current browser file API cannot stream safely, refuse files above a documented threshold rather than risking exhaustion.

## Acceptance criteria

1. Output is produced only after explicit destination selection and confirmation.
2. Original files are never modified.
3. Golden fixtures remain parseable after anonymization.
4. Original absolute positions are absent from all supported/targeted record classes within the defined tolerance.
5. Route-relative shape is preserved when the selected policy promises it.
6. Unknown records are counted and disclosed, not silently claimed safe.
7. Cancellation/error leaves no committed partial output.
8. Browser/WASM follows documented file-size/stream behavior and builds.

## Required tests

- Golden `.tlog`, `.bin`, and `.log` fixtures containing multiple coordinate-bearing record types.
- Consistent transform across home, position, rally, and repeated points.
- Structural reparse and record-count validation.
- Detection that original coordinates are no longer present.
- Unknown/truncated/corrupt record handling.
- Streaming memory behavior on a generated large fixture.
- Atomic output, cancellation cleanup, and original-file hash unchanged.
- Redaction tests for logs/errors/diagnostic reports.

## Out of scope

- Claiming legal/GDPR-grade anonymization without a defined threat model and verification.
- Uploading logs to a server.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 11-fft-analysis.md -->

# ADV-11 — Implement FFT Analysis

**Priority:** P1  
**Dependencies:** ADV-00

## Goal

Implement a reusable signal-processing pipeline and interactive FFT analysis page equivalent in purpose to legacy `fftui`.

## Required implementation

### 1. Reuse canonical log parsing

Use the existing DataFlash/telemetry log reader and signal catalogs. Do not introduce an isolated parser. Expose selectable numeric series by message, instance, field, axis, and timestamp.

### 2. Signal preparation service

Create UI-independent primitives for:

- Time-range selection
- Sample-rate estimation
- Gap/duplicate/out-of-order detection
- Optional resampling to an evenly spaced series
- Mean removal/detrending
- Window selection, at minimum rectangular and Hann
- Segment selection/averaging where implemented
- Unit preservation

Document the mathematical conventions and normalization.

### 3. FFT service

Return a result containing at least:

- Frequency bins in Hz
- Magnitude and/or power spectral density with explicit units/normalization
- Nyquist/sample-rate information
- Window and sample count
- Data-quality warnings
- Detected peaks using a configurable, testable algorithm

Use an established numerical implementation already present or an appropriate maintained dependency; do not write a fragile ad-hoc DFT for production-size data.

### 4. UI

Provide:

- File selection
- Message/instance/field/axis selection
- Time range
- Preprocessing/window controls
- Compute/cancel/progress
- Frequency plot with zoom, cursor values, and peak markers
- Peak table
- Export of plotted numeric results through the existing save abstraction
- Data-quality warnings

All heavy work must run off the UI thread. Avoid retaining duplicate copies of large logs. Browser/WASM must apply explicit file-size/resolution limits and remain responsive.

### 5. Shared foundation for spectrogram

Design preprocessing and FFT primitives so ADV-12 can call them without copying logic.

## Acceptance criteria

1. A synthetic sine wave produces its dominant peak within one frequency-bin tolerance.
2. Amplitude/PSD normalization matches the documented convention and golden expectations.
3. Window selection changes leakage as expected without changing frequency-axis correctness.
4. Irregular/gapped data produces a warning and follows the selected resampling policy.
5. Cancellation stops processing and releases buffers/file handles.
6. Repeated analyses do not accumulate duplicate log copies or background tasks.
7. Browser/WASM enforces its limits with a clear message instead of exhausting memory.

## Required tests

- Single sine, two-tone, DC offset, white noise, and no-data fixtures.
- Frequency-bin, Nyquist, sample-rate, and normalization calculations.
- Detrending and window behavior.
- Irregular timestamps, gaps, duplicate timestamps, and resampling.
- Peak detection and sorting.
- Cancellation and bounded-memory behavior.
- Golden log fixture integration through the canonical parser.

## Out of scope

- Automatic parameter tuning recommendations.
- Spectrogram/STFT UI, which belongs to ADV-12.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 12-spectrogram.md -->

# ADV-12 — Implement Spectrogram

**Priority:** P1  
**Dependencies:** ADV-00, ADV-11

## Goal

Implement an STFT spectrogram page equivalent in purpose to legacy `SpectrogramUI`, reusing ADV-11 signal extraction, preprocessing, windows, FFT normalization, and numerical infrastructure.

## Required implementation

### 1. STFT service

Create a UI-independent service that accepts a prepared evenly sampled signal and parameters:

- Window length in samples/seconds
- Overlap or hop size
- Window type
- Frequency range
- Magnitude/PSD and linear/dB scale
- Optional dynamic range floor/ceiling

Return time bins, frequency bins, intensity matrix/tile source, units, and data-quality diagnostics. Validate parameters so overlap, window size, and memory requirements cannot produce invalid or excessive allocations.

### 2. Bounded computation and rendering

Estimate memory before computing. Support adaptive time/frequency downsampling or tiling for long logs. Avoid per-cell Avalonia controls and per-pixel dispatcher calls. The renderer should use an efficient bitmap/custom-draw path consistent with project conventions.

### 3. UI

Reuse the ADV-11 file/signal/time-range selection experience where practical. Provide:

- Window/overlap/frequency/scale controls
- Compute/cancel/progress
- Heatmap with time and frequency axes
- Intensity/color legend with units
- Zoom/pan and cursor readout
- Optional dominant-frequency trace
- Export of numeric matrix/visible image where the existing platform services support it
- Explicit memory/resolution warnings

Do not hardcode a color palette in shared signal-processing code; rendering owns visualization choices and must remain legible in light/dark themes.

### 4. Browser/mobile constraints

Use a platform/resource policy to cap input samples, bins, and rendered resolution. Degrade resolution predictably and disclose it. Never let one analysis monopolize the UI thread.

## Acceptance criteria

1. A synthetic chirp appears as a monotonically changing dominant-frequency ridge within expected resolution.
2. A stationary sine appears at the correct frequency across time.
3. Time/frequency axes and dB conversion match ADV-11 conventions.
4. Invalid window/overlap combinations are rejected before allocation.
5. Memory estimate/cap triggers adaptive resolution or a clear refusal.
6. Cancellation releases large matrices and file resources.
7. Theme changes preserve readable axes/legend/heatmap.
8. Browser/WASM remains responsive under its declared limits.

## Required tests

- Synthetic sine, chirp, impulse, silence, and two-tone signals.
- Window/hop/time-bin/frequency-bin calculations.
- Linear-to-dB conversion and floor handling.
- Dominant-frequency extraction.
- Memory estimate, adaptive downsampling, and refusal paths.
- Cancellation/disposal.
- Integration test proving ADV-12 uses ADV-11 preprocessing/FFT abstractions rather than duplicated implementations.

## Out of scope

- Live real-time spectrogram unless the existing architecture makes it trivial and bounded.
- Duplicating the FFT/log parser from ADV-11.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 13-serial-support-proxy.md -->

# ADV-13 — Implement Serial Support Proxy

**Priority:** P0 security  
**Dependencies:** ADV-00

## Goal

Implement a consent-driven support proxy equivalent in purpose to legacy `SerialSupportProxy`, forwarding an owned vehicle/link byte stream to a configured support endpoint with strict lifecycle, security, and transparency.

## Required implementation

### 1. Define the support-session threat model

Document:

- What data is forwarded
- Whether forwarding is read-only telemetry or bidirectional command traffic
- Who configures/authenticates the endpoint
- How transport security is established
- Which metadata is logged
- How the user knows the session is active
- How the session is stopped/revoked

Default to the least-privileged mode. If bidirectional command injection is supported, it must be a separate explicit permission with a persistent visible indicator.

### 2. Session service

Build a UI-independent, single-owner service with states:

- Idle
- Awaiting consent
- Connecting
- Active
- Reconnecting
- Stopping
- Stopped
- Faulted

It must use bounded channels, write/read timeouts, cancellation, sanitized errors, and a documented reconnect policy. Expose directional byte/frame counters, drops, last activity, endpoint identity/fingerprint, and session duration.

Do not automatically open local listeners, enumerate/expose ports remotely, or start on application launch.

### 3. Connection integration

Tap or lease the current transport through established ownership rules. Do not compete with the main MAVLink parser for bytes. Preserve raw frames when proxying framed MAVLink. Prevent loops if the support endpoint feeds back into the same connection.

### 4. Endpoint security

Use secure transport/authentication supported by the current architecture. Plaintext remote forwarding must be rejected by default or require an unmistakable expert override and local-only restriction. Never embed support credentials in source code or settings logs.

### 5. UI and platform behavior

Provide endpoint/profile selection, mode, security summary, explicit consent, start/stop, prominent active banner, counters, last error, and a minimal metadata-only session report. Payload capture is disabled by default.

Desktop may use direct transport capabilities. Browser/WASM must use only an audited BrowserBridge path with explicit user initiation; otherwise show the missing capability reason.

## Acceptance criteria

1. No session starts without explicit user consent.
2. Only one support session can own the proxy at a time.
3. Default mode cannot inject remote command bytes into the vehicle connection.
4. Enabling bidirectional mode, if implemented, requires a separate confirmation and remains visibly indicated for the entire session.
5. Stop, disconnect, page exit, and application shutdown close the endpoint and release the source lease.
6. Bounded queues report drops and never grow without limit.
7. Errors/reports reveal no credentials or raw telemetry payloads.
8. Browser/WASM uses an audited bridge or reports unsupported status; no hidden listener/socket is opened.

## Required tests

- Consent gate and single-owner enforcement.
- Read-only versus bidirectional direction rules.
- Exact raw-frame forwarding through fake endpoints.
- Queue overflow, timeout, endpoint failure, bounded reconnect, cancellation, and disposal.
- Loop prevention.
- Credential/payload redaction in logs and reports.
- Browser bridge available/unavailable policy.

## Out of scope

- A cloud support service implementation.
- Silent remote control.
- Automatic port forwarding or firewall changes.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.


---

<!-- Source task file: 14-integration-parity-and-platform-validation.md -->

# ADV-14 — Complete Setup / Advanced parity and platform validation

**Priority:** P0 release gate  
**Dependencies:** ADV-00 through ADV-13

## Goal

Perform the final integration pass proving that Setup → Advanced is complete, coherent, safe, testable, and compatible with desktop and Browser/WASM targets.

## Required implementation and verification

### 1. Source-to-feature parity matrix

Create or update project documentation with one row for every legacy `ConfigAdvanced` launch point and columns for:

- Legacy class/action
- Next Gen feature/page
- Shared service(s)
- Required MAVLink message/command
- Platform capabilities
- Desktop status
- Browser/WASM status
- Test coverage
- Intentional behavioral differences and rationale

No legacy launch point may be omitted or marked complete based solely on a placeholder page.

### 2. Advanced hub integration

Verify all thirteen descriptors route to the implemented pages and that availability is correct for:

- Disconnected desktop
- Connected desktop without a fully available vehicle state
- Connected supported vehicle
- Browser/WASM without BrowserBridge
- Browser/WASM with BrowserBridge
- File/location/secure-storage permission denial

Remove temporary registrations, duplicate feature IDs, dummy commands, and obsolete placeholders.

### 3. Lifecycle and concurrency audit

For every tool, validate:

- Repeated open/close
- Connection loss and reconnection
- Page navigation while work is active
- Application shutdown
- Cancellation
- Endpoint/provider failure
- Only one owner where required
- No duplicate subscriptions, orphaned tasks, blocked threads, or reused stale parser/channels

Add integration tests or diagnostics counters where ownership cannot otherwise be proven.

### 4. Browser/WASM audit

Build and exercise the Browser project. Search shared/UI code for accidental direct use of unsupported native APIs introduced by these tasks. Capability-disabled features must remain visible with clear reasons. Browser file/location/bridge workflows must require normal browser permissions and user gestures.

### 5. UX/accessibility audit

Ensure:

- Responsive narrow and wide layouts
- Keyboard-only navigation
- Visible focus
- Accessible names/descriptions/status
- Light/dark theme support
- Long error text is selectable/wrapped
- Dangerous operations have explicit warnings/confirmations
- Active Follow Me, signing setup, output, moving base, or support proxy states are visually unmistakable

### 6. Test/build quality gate

Run all solution tests plus targeted desktop and Browser builds. Fix regressions. Add a compact set of cross-feature integration tests covering the hub, lifecycle, capability changes, and navigation.

## Acceptance criteria

1. The parity matrix contains all thirteen legacy actions and links each to implemented, tested Next Gen behavior.
2. Setup → Advanced has no placeholder-only destination, empty command, or `NotImplementedException`.
3. All tools remain visible on every platform and explain unavailable prerequisites.
4. Desktop and Browser/WASM builds succeed from a clean checkout.
5. Full existing and new test suites pass.
6. Ten open/close cycles of each page do not increase active subscription/task/endpoint counts after returning to the hub.
7. Disconnect/reconnect does not reuse disposed or stale parser/channel/session state.
8. Security/privacy audit finds no signing keys, credentials, complete payloads, or source coordinates in logs.
9. All destructive/risky workflows require explicit user action and report protocol-level failure accurately.
10. Documentation records commands run, results, known intentional limitations, and no unresolved acceptance criteria.

## Required tests

- Hub catalog and navigation integration.
- Availability matrix across representative platform/capability states.
- Repeated navigation lifecycle stress test for all thirteen features.
- Disconnect/reconnect during each live feature category.
- Browser startup/build smoke test and bridge available/unavailable smoke tests.
- Accessibility automation/smoke tests supported by the current test stack.
- Redaction scan against captured test logs.

## Out of scope

- Terminal and Script REPL.
- Unrelated Setup/Mandatory Hardware, Optional Hardware, or Config/Tuning features.
- Declaring completion while suppressing failing tests or hiding unsupported tools.

## Repository baseline

Implement against the current `main` branch of `karlgodtliebsen/MissionPlanner`. The task set was prepared from main commit `ae2d5a9ccece880b96dda99ca18be63c62985ae4` (2026-09-05, `Resurrected all tests`). Reinspect `main` before changing code because later tasks may already have introduced reusable abstractions.

Current Next Gen placeholders:

- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Advanced/AdvancedViewModel.cs`

Legacy reference implementation:

- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.Designer.cs`
- `src-v.1.38/GCSViews/ConfigurationView/ConfigAdvanced.resx`

## Mandatory engineering rules

1. Do not edit or compile code from `src-v.1.38`; it is reference material only.
2. Do not mechanically port WinForms forms, static globals, modal-window launching, blocking I/O, or unmanaged background threads.
3. Put reusable behavior in the appropriate shared project (`MissionPlanner.Core`, `MissionPlanner.MavLink`, `MissionPlanner.Transport`, or another already-established shared project). Keep Avalonia views and presentation-only behavior in `MissionPlanner.App`.
4. Hide platform-specific file, serial, socket, geolocation, secure-storage, notification, and browser-bridge behavior behind injected interfaces with explicit capability reporting.
5. Browser/WASM must continue to compile. Shared code must not directly call APIs unavailable in the browser. Use the existing Browser/BrowserBridge architecture where it is appropriate; otherwise expose an explicit unavailable state and reason.
6. Use async APIs, cancellation tokens, bounded queues, deterministic cleanup, and one-operation ownership. Never block the UI thread.
7. Do not duplicate existing connection, MAVLink decoding, acknowledged-command, parameter, log parsing, file-picker, dialog, or navigation infrastructure. Inspect and extend it.
8. Do not log secrets, signing keys, complete telemetry payloads, precise coordinates from anonymization workflows, or other sensitive data.
9. Tests must be deterministic and run without physical hardware. Use fixtures, fake transports, fake clocks, and fake platform services.
10. A feature is not complete merely because a page exists. All acceptance criteria and tests below are required.

## Required verification

Run the repository's current CI-equivalent commands. At minimum, verify the full solution/tests and the Browser project. Discover the exact current solution and project paths rather than assuming they are unchanged. Record every command and result in the final Codex response. The expected commands are similar to:

```text
dotnet build src/MissionPlanner.DotNet.slnx
dotnet test src/MissionPlanner.DotNet.slnx
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj
```

Also build the relevant desktop/platform project when it is present in the current solution.

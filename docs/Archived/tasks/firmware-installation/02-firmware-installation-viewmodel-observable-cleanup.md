# Codex Task 2 — Cleanup `InstallFirmwareViewModel` Observable State and Command Notifications

## Goal

Refactor the firmware installation ViewModel so observable state and command availability use the established CommunityToolkit.Mvvm patterns consistently.

Reduce:

- manually maintained notification code;
- broad `NotifyAll()` command invalidation;
- scattered `OnPropertyChanged(nameof(...))`;
- duplicated boolean state that can become inconsistent.

Prefer:

- `[ObservableProperty]` for real mutable UI state;
- generated partial `On<Property>Changed(...)` hooks for related state;
- targeted `NotifyCanExecuteChanged()` for only the commands affected by a change;
- computed read-only properties where duplicating state would be worse than computing it.

This is a **behavior-preserving refactor** except for correcting clear notification bugs discovered by tests.

Perform this task after the board-ID override task, so the new custom-firmware safety state follows the same cleaned-up pattern.

## Current source snapshot

Main file:

```text
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.cs
```

It is currently approximately 1,300+ lines and contains a mixture of:

- `[ObservableProperty]` properties;
- simple computed properties;
- writable boolean flags derived from selections;
- direct calls to `OnPropertyChanged`;
- a broad `NotifyAll()` method that invalidates many unrelated commands.

Examples currently include:

```text
HasPreparedFirmware
HasCustomFirmware
CanRequestCancellation
CanNavigateAway
HasDiagnosticReport
HasLocalDfuFirmware

HasDevice
HasDfuBootLoader
HasSelectedFirmware

OnPropertyChanged(nameof(...))
NotifyAll()
```

Do not mechanically convert every property to stored mutable state. First classify the state and its source of truth.

---

## Required refactor

### 1. Build a dependency map before editing

Document in the change description which source properties affect:

- derived visibility/state properties;
- command `CanExecute`;
- mode/capability state.

At minimum inspect these sources:

```text
PreparedFirmware
CustomPackage
LastDiagnosticReport
LocalDfuFirmwarePath
LocalDfuPlatform
SelectedFirmware
SelectedDevice
SelectedDfuDevice
IsOperationInProgress
IsCatalogRefreshRunning
CanInstall
CanUpdateBootloader
SelectedChannel
SelectedVersion
SelectedFrameType
SelectedManufacturer
```

Include the board-ID strict/override property introduced by Task 1.

### 2. Remove broad `NotifyAll()`

The current `NotifyAll()` invalidates many commands regardless of what changed.

Remove it rather than renaming it.

Each source property should notify only the commands whose `CanExecute` actually depends on that source.

Examples:

- `SelectedDevice` affects local custom-file availability and application install.
- `SelectedDfuDevice` affects local DFU-file availability and DFU install.
- `LocalDfuPlatform` affects DFU install.
- `SelectedFirmware` affects application install and DFU install.
- `IsOperationInProgress` affects install, DFU install, bootloader update, navigation safety and cancellation state.
- `IsCatalogRefreshRunning` affects cancellation.
- `CanInstall` affects application install.
- `CanUpdateBootloader` affects embedded bootloader update.

Verify the exact dependency graph from the code rather than relying only on this list.

### 3. Consolidate related state in partial change hooks

Use generated partial methods such as:

```csharp
partial void OnSelectedDeviceChanged(FirmwareDeviceItemViewModel? value)
{
    ...
}
```

to update related state and command availability.

Do not scatter notification calls across loading, clearing, selection, refresh and install methods when the same result can be derived from the changed property itself.

For example:

- changes to `PreparedFirmware` should be the single trigger for `HasPreparedFirmware`;
- changes to `CustomPackage` should be the single trigger for `HasCustomFirmware`;
- changes to `LastDiagnosticReport` should be the single trigger for `HasDiagnosticReport`;
- changes to `LocalDfuFirmwarePath` should be the single trigger for `HasLocalDfuFirmware`;
- changes to `SelectedFirmware` should be the single trigger for `HasSelectedFirmware`;
- changes to `SelectedDevice` should be the single trigger for `HasDevice`;
- changes to `SelectedDfuDevice` should be the single trigger for the corresponding DFU-device-present state.

Then remove manual `OnPropertyChanged(nameof(...))` calls from the command bodies that become redundant.

### 4. Avoid unnecessary duplicate state

For each derived boolean choose one of these deliberately:

#### Option A — computed read-only property

Use this when it is cheap and there is no value in storing a second source of truth:

```csharp
public bool HasPreparedFirmware => PreparedFirmware is not null;
```

Notify it from the source property's generated change hook, or use the CommunityToolkit source-generator dependency attribute if that is already supported and idiomatic in this repository.

#### Option B — observable derived state

Use this only when the value is genuinely treated as UI state and the existing project convention favors an observable generated property:

```csharp
[ObservableProperty]
public partial bool HasPreparedFirmware { get; private set; }
```

with exactly one source hook assigning it:

```csharp
partial void OnPreparedFirmwareChanged(FirmwarePreparationResult? value)
{
    HasPreparedFirmware = value is not null;
}
```

Do not have both a computed getter and independently writable state for the same concept.

### 5. Make operation-state propagation predictable

`SetOperation(...)` currently performs several assignments and manual notifications.

Refactor so changing:

```text
IsOperationInProgress
CurrentOperationState
```

causes their dependent properties/commands to update through the normal observable dependency mechanism.

In particular keep these semantics correct:

```text
CanNavigateAway == !IsOperationInProgress
CanRequestCancellation == IsCatalogRefreshRunning || IsOperationInProgress
```

and keep the cancellation command state current.

Do not change the destructive-stage cancellation policy.

### 6. Keep `ApplyMode(...)` focused on mode resolution

`ApplyMode(...)` should set resolved mode/capability state:

```text
IsConnectedMode
IsDisconnectedMode
IsUnsupportedMode
CanInstall
CanUpdateBootloader
```

It should not need to manually invalidate every command after doing so.

The relevant observable property changes should invalidate only their dependent commands.

### 7. Clean up command predicates

Review command predicates such as:

```text
CanStartInstall
CanInstallDfuFirmware
CanStartBootloaderUpdate
CanRequestCancellation
HasDevice
HasDfuBootLoader
```

Use consistent naming:

- methods that calculate command availability should read as predicates;
- UI properties should be properties;
- do not keep a public method named as though it were a property unless the source generator specifically requires that shape.

Do not change public API unnecessarily if XAML or tests depend on it.

### 8. Preserve thread/dispatcher behavior

Do not change the existing rule that UI-bound observable state is updated on the Avalonia dispatcher where required.

This refactor is about state dependency and notification, not about moving firmware work onto different threads.

### 9. Remove redundant calls after the refactor

Search the finished ViewModel for:

```text
NotifyAll(
OnPropertyChanged(nameof(
```

Every remaining occurrence must have a specific reason that cannot be represented safely by the source observable dependency.

The expected result is that `NotifyAll()` no longer exists and manual property notifications are rare and justified.

### 10. Do not split the ViewModel merely to reduce line count

A later responsibility extraction may be useful, but do not create speculative coordinator/helper classes just to make this task look smaller.

If a clear cohesive extraction is already required by the refactor, explain it first and keep it minimal.

---

## Suggested dependency examples

These are starting points; verify against the source.

```text
PreparedFirmware
  -> HasPreparedFirmware

CustomPackage
  -> HasCustomFirmware
  -> custom board-ID override visibility/state from Task 1

LastDiagnosticReport
  -> HasDiagnosticReport

LocalDfuFirmwarePath
  -> HasLocalDfuFirmware
  -> InstallDfuFirmwareCommand

LocalDfuPlatform
  -> InstallDfuFirmwareCommand

SelectedFirmware
  -> selectedFirmwareTarget
  -> HasSelectedFirmware
  -> InstallCommand
  -> InstallDfuFirmwareCommand

SelectedDevice
  -> HasDevice
  -> LoadCustomFirmwareCommand
  -> InstallCommand

SelectedDfuDevice
  -> HasDfuBootLoader
  -> LoadCustomBlWithFirmwareCommand
  -> InstallDfuFirmwareCommand

IsCatalogRefreshRunning
  -> CanRequestCancellation
  -> CancelCommand

IsOperationInProgress
  -> CanNavigateAway
  -> CanRequestCancellation
  -> InstallCommand
  -> InstallDfuFirmwareCommand
  -> UpdateBootloaderCommand
  -> CancelCommand

CanInstall
  -> InstallCommand

CanUpdateBootloader
  -> UpdateBootloaderCommand
```

Do not notify commands that do not actually have a `CanExecute` predicate unless the task also deliberately adds one.

---

## Tests / verification

There is no reason to introduce a large ViewModel UI test framework solely for this cleanup.

However, add focused tests where the existing test infrastructure allows it, or extract only pure dependency logic that genuinely warrants a unit test.

At minimum verify manually/in code that:

1. selecting/clearing a firmware row updates the correct panels and install command;
2. selecting/clearing a serial device updates custom-firmware availability and install command;
3. selecting/clearing a DFU device updates DFU controls and command;
4. selecting/clearing a local DFU HEX updates DFU install availability;
5. operation start/end immediately updates navigation, install, update and cancel availability;
6. catalogue refresh start/end updates cancellation availability;
7. prepared firmware visibility follows `PreparedFirmware`;
8. custom firmware visibility follows `CustomPackage`;
9. diagnostic-report visibility follows `LastDiagnosticReport`;
10. the Task 1 board-ID override resets and notifies correctly.

Run the normal firmware tests and build the UI project where the current platform permits it.

---

## Acceptance criteria

The task is complete when:

- the ViewModel retains the same user-visible firmware behavior;
- `NotifyAll()` is removed;
- command invalidation is targeted;
- derived state has a clear single source of truth;
- generated observable partial change hooks are the normal place for dependency updates;
- redundant manual `OnPropertyChanged(nameof(...))` calls are removed;
- operation/mode transitions still update all relevant command states;
- Task 1's board-ID override follows the same pattern;
- firmware tests pass and the affected UI project builds on a supported environment.

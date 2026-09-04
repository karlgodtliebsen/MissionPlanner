# Avalonia dialog lifecycle

MissionPlanner dialogs are opened through `IDialogService` and the adapters in
`MissionPlanner.AvaloniaUI.App/Utilities/Dialogs`. Prefer Ursa dialogs and notifications;
use `ViewDialogWindow` for application-owned dialog content that needs a dedicated window.

## Ownership rules

- ViewModels request an interaction through `IDialogService`; they do not construct windows.
- Resolve the owner through `IWindowProvider`.
- Marshal dialog creation, result publication, and observable state changes to the Avalonia
  UI thread.
- Tie long-running dialog work to an operation token and the owning ViewModel activation.
- Make close and cancellation idempotent. A late result must not update a deactivated view.
- Detach events and dispose operation resources when the dialog closes.
- Use the shared storage-provider adapter for file dialogs so the last directory is persisted.

Do not use arbitrary delays to hide lifecycle races. Complete or cancel background work,
then request close on the UI thread. If a command is invoked from dialog content, return its
typed result to the dialog service rather than walking the visual tree to find a window.

## Verification

Exercise accept, cancel, title-bar close, owner-window close, repeated open/close, and
activation cancellation. Verify that no callback updates disposed state and that exceptions
are observed and logged.

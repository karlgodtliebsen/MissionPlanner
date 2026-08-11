# Deferred Popup Close on .NET MAUI

## Context

This note records the analysis and close strategy developed while replacing the
UraniumUI `MultiplePickerField` popup with a local lightweight dialog.

Closing UraniumUI or CommunityToolkit popups from a bound button command can
intermittently cause:

```text
System.InvalidOperationException:
PlatformView cannot be null here
```

The same issue can occur with modal page navigation through:

```csharp
await navigation.PopModalAsync(...);
```

The exception is timing-dependent and occurs primarily during Windows
visual-tree teardown.

## Failure Sequence

The likely sequence is:

1. A Windows button or input event invokes an `ICommand`.
2. The command directly awaits popup or modal closure.
3. MAUI begins unloading the page or popup and disconnecting handlers.
4. The asynchronous command remains active and can raise completion,
   `CanExecuteChanged`, `IsRunning`, binding, focus, or other UI notifications.
5. One of these operations reaches a handler whose native `PlatformView` has
   already been cleared.
6. MAUI throws `PlatformView cannot be null here`.

The precise faulty handler has not been conclusively identified, but repeated
testing established the following behavior:

- Awaiting the real popup-close task from the button command is unreliable.
- Returning the real scheduled close task is unreliable.
- Adding an extra `await`, dispatcher invocation, `Task.Yield`, or semaphore
  does not reliably solve the problem.
- A semaphore remains useful for preventing concurrent navigation, but it does
  not solve the teardown race.
- The reliable strategy is for the button command to finish immediately while
  actual popup closure is scheduled independently.
- The actual UI close operation must still execute through the MAUI dispatcher.
- The independently scheduled operation must catch and log its own exceptions.

## Required Close Semantics

The public close operation means:

> Accept and schedule a close request.

It does not mean:

> Return a task that completes when the native popup has finished closing.

The button command must be synchronous, or return an already-completed task, so
the Windows input event and command execution pipeline can finish before the
popup visual tree is removed.

Preferred command pattern:

```csharp
[RelayCommand]
private void Close()
{
    popupController.RequestClose();
}
```

Avoid returning the native close task from the command:

```csharp
[RelayCommand]
private Task CloseAsync()
{
    return popup.CloseAsync();
}
```

## Deferred Close Controller

A controller can enforce these semantics:

```csharp
public interface IDeferredPopupController
{
    void RequestClose(bool accepted);
}

public sealed class DeferredPopupController : IDeferredPopupController
{
    private readonly IDispatcher dispatcher;
    private readonly ILogger<DeferredPopupController> logger;
    private readonly SemaphoreSlim closeGate = new(1, 1);

    private Func<bool, Task>? closeAction;
    private int closeRequested;

    public DeferredPopupController(
        IDispatcher dispatcher,
        ILogger<DeferredPopupController> logger)
    {
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public void SetCloseAction(Func<bool, Task> action)
    {
        closeAction = action
            ?? throw new ArgumentNullException(nameof(action));

        Volatile.Write(ref closeRequested, 0);
    }

    public void RequestClose(bool accepted)
    {
        if (Interlocked.CompareExchange(
                ref closeRequested,
                1,
                comparand: 0) != 0)
        {
            return;
        }

        /*
         * Deliberately do not expose this task to the bound command.
         *
         * The command and native Windows input event must finish before
         * popup teardown begins.
         */
        _ = Task.Run(
            () => CloseSafelyAsync(accepted),
            CancellationToken.None);
    }

    private async Task CloseSafelyAsync(bool accepted)
    {
        try
        {
            await closeGate.WaitAsync(CancellationToken.None)
                .ConfigureAwait(false);

            try
            {
                var action = closeAction;

                if (action is null)
                {
                    return;
                }

                await dispatcher.DispatchAsync(
                    () => action(accepted))
                    .ConfigureAwait(false);
            }
            finally
            {
                closeGate.Release();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "An error occurred while closing the popup.");
        }
        finally
        {
            closeAction = null;
            Volatile.Write(ref closeRequested, 0);
        }
    }
}
```

The exact class structure may vary, but the lifecycle behavior must remain the
same.

## Opening and Closing

Opening can remain awaitable because it does not tear down the control that
initiated the command.

For a CommunityToolkit popup:

```csharp
var resultTask = currentPage.ShowPopupAsync<bool>(
    popup,
    popupOptions,
    CancellationToken.None);
```

Configure the deferred controller with the real close operation:

```csharp
deferredPopupController.SetCloseAction(
    accepted => popup.CloseAsync(
        accepted,
        CancellationToken.None));
```

The popup buttons must only request closure:

```csharp
var okCommand = new Command(
    () => deferredPopupController.RequestClose(true));

var cancelCommand = new Command(
    () => deferredPopupController.RequestClose(false));
```

The caller may await the popup result. The OK and Cancel commands must not await
or return the actual close operation.

## Cancellation

Do not pass a caller-owned cancellation token to native popup teardown. Once
teardown has been requested, allow it to complete:

```csharp
await popup.CloseAsync(
    result,
    CancellationToken.None);
```

Cancellation may prevent opening or initial scheduling, but it must not
interrupt an active native teardown operation.

## Duplicate Close Protection

A semaphore alone is insufficient because it serializes duplicate requests. A
second request could run after the first popup closes and affect a subsequent
popup or modal.

Use an atomic close-request flag:

```csharp
if (Interlocked.CompareExchange(
        ref closeRequested,
        1,
        comparand: 0) != 0)
{
    return;
}
```

Reset the flag only after the independently scheduled operation completes or
fails.

## Exception Handling

The scheduled operation is intentionally detached from the button command, but
it must not discard exceptions. Catch and log exceptions inside the scheduled
operation:

```csharp
catch (Exception exception)
{
    logger.LogError(
        exception,
        "Failed to close the popup.");
}
```

Do not rethrow onto the UI synchronization context after the originating popup
controls have begun unloading.

## Cleanup

After the popup closes:

- Detach event handlers owned by the custom popup.
- Clear temporary collection or selection subscriptions.
- Clear retained popup and close-delegate references.
- Stop timers or animations.
- Do not manually call `DisconnectHandler`.
- Do not manually set `Handler = null`.
- Allow MAUI to disconnect the visual tree once.

## Acceptance Criteria

The close implementation is complete when:

1. OK and Cancel close reliably on Windows.
2. Outside-tap dismissal behaves as configured.
3. Repeated opening and closing does not cause:
   - `PlatformView cannot be null here`;
   - stale-parent errors;
   - duplicate modal-stack entries;
   - unobserved task exceptions.
4. Rapid duplicate close requests schedule only one operation.
5. Selection is committed only on OK.
6. Cancel and outside dismissal preserve the prior selection.
7. Light and dark theme appearance remains consistent.

Do not replace this lifecycle strategy with an arbitrary delay:

```csharp
await Task.Delay(50);
```

The essential behavior is to detach native teardown from the lifetime of the
bound button command, then marshal teardown back through the dispatcher.

## Typed UraniumUI Prompts

`IExtendedDialogService` provides typed string, clock-time, `int`, `long`, `float`, and
`double` prompts. Strings use an auto-sizing `AlignedEditorField` with end-aligned text;
the other prompts use `TimePickerField` or the culture-aware `NumericField`. String
prompts return `null` on Cancel or Clear so callers cannot mistake displayed initial
text for accepted input. Numeric and time prompts retain the original nullable value
on Cancel and return `null` on Clear. All prompts return the edited value on OK.
The supplied message is rendered between the dialog header
and input field when it is not empty. Numeric fields format after editing and clamp committed
values to the supplied range. The time prompt represents a clock time and accepts
values from `00:00:00` through the final tick before 24 hours.

These prompts use the same detached, semaphore-gated close strategy described
above. Button commands only request completion; modal teardown runs afterward
through the UI dispatcher. New prompt implementations should reuse this pipeline
instead of calling `PopModalAsync` directly from a button command.

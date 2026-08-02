using System.Collections;
using System.Diagnostics;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Reversible native-host lifecycle for the internal CollectionView.
///
/// Logical grid state is allowed to change at any time. Native ItemsSource
/// assignment is deferred until the CollectionView has a usable handler and
/// the grid or rows host is loaded.
/// </summary>
public partial class VirtualizedDataGrid
{
    private const int MaximumRowsApplyRetryCount = 20;

    private static readonly TimeSpan rowsApplyRetryDelay = TimeSpan.FromMilliseconds(1);

    private IList? desiredRowsSource;
    private IList? appliedRowsSource;

    private bool rowsViewLoaded;
    private bool rowsHandlerReady;
    private bool rowsSourceUpdatePending;
    private bool forceRowsSourceRebind;

    private bool rowsRetryScheduled;
    private int rowsRetryAttempt;
    private int rowsRetryGeneration = -1;

    private int rowsApplyScheduledGeneration = -1;
    private int rowsHandlerGeneration;
    private int appliedRowsHandlerGeneration = -1;

    /// <summary>
    /// Gets whether the internal CollectionView can currently accept a platform
    /// presentation operation.
    /// </summary>
    protected bool IsRowsHostReady => CanUseRowsPlatformHost;

    /// <summary>
    /// Gets whether the latest desired row source still needs to be applied to
    /// the current native handler generation.
    /// </summary>
    protected bool HasPendingRowsSourceUpdate => rowsSourceUpdatePending;

    /// <summary>
    /// Do not use cached Loaded flags as the sole gate. Shell/routed navigation
    /// can retain the managed visual tree while Loaded/Unloaded event timing does
    /// not mirror handler timing exactly.
    ///
    /// PlatformView is deliberately not inspected here. The narrow assignment
    /// fallback handles the short MAUI state where Handler exists but its native
    /// view has already been cleared.
    /// </summary>
    private bool CanUseRowsPlatformHost =>
        !visualResourcesReleased &&
        rowsView.Handler is not null &&
        (rowsView.IsLoaded || IsLoaded);

    private int RowsHandlerGeneration => rowsHandlerGeneration;

    private void AttachRowsViewLifecycle()
    {
        rowsView.HandlerChanging += RowsView_HandlerChanging;
        rowsView.HandlerChanged += RowsView_HandlerChanged;
        rowsView.Loaded += RowsView_Loaded;
        rowsView.Unloaded += RowsView_Unloaded;

        // The outer control is often the more reliable visibility signal when
        // Shell keeps the child CollectionView instance alive.
        Loaded += VirtualizedDataGrid_Loaded;
        Unloaded += VirtualizedDataGrid_Unloaded;
    }

    private void VirtualizedDataGrid_Loaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.GridLoadedCount++;
            Diagnostics.LastGridLoadedAt = DateTimeOffset.UtcNow;
        }

        RefreshRowsHostState();

        // Reapply even when the managed CollectionView still contains the same
        // ItemsSource reference. A newly connected native handler may not have
        // received that retained bindable value.
        forceRowsSourceRebind = desiredRowsSource is not null;
        InvalidateAppliedRowsSource();
        ResetRowsRetry();
        QueueRowsSourceApplication();
    }

    private void VirtualizedDataGrid_Unloaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.GridUnloadedCount++;
            Diagnostics.LastGridUnloadedAt = DateTimeOffset.UtcNow;
        }

        MarkRowsHostUnavailable();
    }

    private void RowsView_HandlerChanging(object? sender, HandlerChangingEventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHandlerChangingCount++;
            Diagnostics.LastRowsHandlerChangingAt = DateTimeOffset.UtcNow;
        }

        MarkRowsHostUnavailable();
    }

    private void RowsView_HandlerChanged(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHandlerChangedCount++;
            Diagnostics.LastRowsHandlerChangedAt = DateTimeOffset.UtcNow;
        }

        rowsHandlerGeneration++;
        RefreshRowsHostState();

        forceRowsSourceRebind = desiredRowsSource is not null;
        InvalidateAppliedRowsSource();
        ResetRowsRetry();

        ApplyRowsViewConfiguration();
        UpdateEmptyViewVisibility();
        UpdateSearchBarVisibility();
        QueueRowsSourceApplication();
    }

    private void RowsView_Loaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsViewLoadedCount++;
            Diagnostics.LastRowsViewLoadedAt = DateTimeOffset.UtcNow;
        }

        RefreshRowsHostState();

        forceRowsSourceRebind = desiredRowsSource is not null;
        InvalidateAppliedRowsSource();
        ResetRowsRetry();

        ApplyRowsViewConfiguration();
        UpdateEmptyViewVisibility();
        UpdateSearchBarVisibility();
        QueueRowsSourceApplication();
    }

    private void RowsView_Unloaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsViewUnloadedCount++;
            Diagnostics.LastRowsViewUnloadedAt = DateTimeOffset.UtcNow;
        }

        MarkRowsHostUnavailable();
    }

    /// <summary>
    /// Marks presentation unavailable without changing logical grid state.
    /// This method is safe to call more than once during nested MAUI teardown.
    /// </summary>
    private void SuspendRowsPresentation()
    {
        MarkRowsHostUnavailable();
    }

    /// <summary>
    /// Re-evaluates readiness when the reusable managed control receives a new
    /// parent handler.
    /// </summary>
    private void ResumeRowsPresentation()
    {
        rowsHandlerGeneration++;
        RefreshRowsHostState();

        forceRowsSourceRebind = desiredRowsSource is not null;
        InvalidateAppliedRowsSource();
        ResetRowsRetry();

        ApplyRowsViewConfiguration();
        UpdateEmptyViewVisibility();
        UpdateSearchBarVisibility();
        QueueRowsSourceApplication();
    }

    private void RefreshRowsHostState()
    {
        rowsHandlerReady = rowsView.Handler is not null;
        rowsViewLoaded = rowsView.IsLoaded || IsLoaded;
    }

    private void MarkRowsHostUnavailable()
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHostUnavailableCount++;
        }

        rowsHandlerGeneration++;
        rowsHandlerReady = false;
        rowsViewLoaded = false;

        // Any callback already queued for an older generation will self-cancel.
        rowsSourceUpdatePending = true;
        forceRowsSourceRebind = desiredRowsSource is not null;

        appliedRowsSource = null;
        appliedRowsHandlerGeneration = -1;

        ResetRowsRetry();
    }

    /// <summary>
    /// Stores the latest logical source. The latest call wins while the native
    /// host is unavailable.
    /// </summary>
    private void SetDesiredRowsSource(IList? source)
    {
        desiredRowsSource = source;

        rowsSourceUpdatePending =
            forceRowsSourceRebind ||
            appliedRowsHandlerGeneration != rowsHandlerGeneration ||
            !ReferenceEquals(appliedRowsSource, source);

        ResetRowsRetry();

        // A null source is a release operation, not ordinary data loading. Apply
        // it synchronously while the native host is still usable; otherwise Shell
        // can detach the handler before the queued callback runs and the retained
        // CollectionView continues to root the previous rows until navigation back.
        RefreshRowsHostState();
        if (source is null && CanUseRowsPlatformHost)
        {
            ApplyPendingRowsSource(rowsHandlerGeneration);
            return;
        }

        QueueRowsSourceApplication();
    }

    /// <summary>
    /// Forces the same logical source to be reapplied to the current or next
    /// native handler generation.
    /// </summary>
    private void InvalidateAppliedRowsSource()
    {
        appliedRowsSource = null;
        appliedRowsHandlerGeneration = -1;
        rowsSourceUpdatePending = true;

        QueueRowsSourceApplication();
    }

    private void QueueRowsSourceApplication()
    {
        if (!rowsSourceUpdatePending)
        {
            return;
        }

        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsSourceApplyQueuedCount++;
        }

        RefreshRowsHostState();

        if (!CanUseRowsPlatformHost)
        {
            ScheduleRowsSourceRetry();
            return;
        }

        if (rowsApplyScheduledGeneration == rowsHandlerGeneration)
        {
            return;
        }

        var generation = rowsHandlerGeneration;
        rowsApplyScheduledGeneration = generation;

        var dispatched = Dispatcher.Dispatch(() =>
        {
            if (rowsApplyScheduledGeneration == generation)
            {
                rowsApplyScheduledGeneration = -1;
            }

            if (generation != rowsHandlerGeneration)
            {
                QueueRowsSourceApplication();
                return;
            }

            RefreshRowsHostState();

            if (!CanUseRowsPlatformHost)
            {
                ScheduleRowsSourceRetry();
                return;
            }

            ApplyPendingRowsSource(generation);
        });

        if (!dispatched)
        {
            rowsApplyScheduledGeneration = -1;
            ScheduleRowsSourceRetry();
        }
    }

    private void ApplyPendingRowsSource(int generation)
    {
        if (!rowsSourceUpdatePending ||
            generation != rowsHandlerGeneration)
        {
            return;
        }

        RefreshRowsHostState();

        if (!CanUseRowsPlatformHost)
        {
            ScheduleRowsSourceRetry();
            return;
        }

        var source = desiredRowsSource;

        if (!forceRowsSourceRebind &&
            appliedRowsHandlerGeneration == generation &&
            ReferenceEquals(appliedRowsSource, source))
        {
            rowsSourceUpdatePending = false;
            return;
        }

        var diagnosticsStarted = Diagnostics.StartTiming();

        try
        {
            // A retained CollectionView can still contain the same managed source
            // reference after navigation. Setting the same reference again does not
            // raise a BindableProperty change and can leave the new/reconnected
            // native handler with no rendered rows.
            //
            // Force a real property transition once per handler/load generation.
            if (forceRowsSourceRebind &&
                source is not null &&
                ReferenceEquals(rowsView.ItemsSource, source))
            {
                SetNativeRowsItemsSource(null);
            }

            if (!ReferenceEquals(rowsView.ItemsSource, source) ||
                forceRowsSourceRebind)
            {
                SetNativeRowsItemsSource(source);
            }

            appliedRowsSource = source;
            appliedRowsHandlerGeneration = generation;
            rowsSourceUpdatePending = false;
            forceRowsSourceRebind = false;

            ResetRowsRetry();

            // Visibility may have been skipped while the host was detached.
            UpdateEmptyViewVisibility();
            UpdateSearchBarVisibility();

            rowsView.InvalidateMeasure();
            rowsHost.InvalidateMeasure();
            InvalidateMeasure();
            Diagnostics.RecordRowsSourceApply(
                diagnosticsStarted,
                source is null);
        }
        catch (InvalidOperationException exception)
            when (IsPlatformViewUnavailable(exception))
        {
            if (Diagnostics.IsEnabled)
            {
                Diagnostics.RowsSourceApplyFailureCount++;
            }

            // A narrow fallback for the short MAUI transition where Handler exists
            // but its native PlatformView has already been cleared.
            rowsHandlerReady = false;
            rowsSourceUpdatePending = true;
            forceRowsSourceRebind = desiredRowsSource is not null;

            appliedRowsSource = null;
            appliedRowsHandlerGeneration = -1;

            Debug.WriteLine(
                $"VirtualizedDataGrid deferred CollectionView ItemsSource update: " +
                $"{exception.Message}");

            ScheduleRowsSourceRetry();
        }
    }

    private void SetNativeRowsItemsSource(IList? source)
    {
        var diagnosticsStarted = Diagnostics.StartTiming();
        try
        {
            rowsView.ItemsSource = source;
        }
        finally
        {
            Diagnostics.RecordNativeItemsSourceSet(
                diagnosticsStarted,
                source is null);
        }
    }


    private void ScheduleRowsSourceRetry()
    {
        if (!rowsSourceUpdatePending || rowsRetryScheduled || rowsRetryAttempt >= MaximumRowsApplyRetryCount)
        {
            return;
        }

        // Do not keep a timer alive while the entire managed control is detached.
        // Handler/Loaded events will restart the process when it returns.
        if (Handler is null && rowsView.Handler is null)
        {
            return;
        }

        rowsRetryScheduled = true;
        rowsRetryGeneration = rowsHandlerGeneration;

        var scheduled = Dispatcher.DispatchDelayed(
            rowsApplyRetryDelay,
            () =>
            {
                if (Diagnostics.IsEnabled)
                {
                    Diagnostics.RowsSourceApplyRetryCount++;
                }
                rowsRetryScheduled = false;

                if (!rowsSourceUpdatePending)
                {
                    ResetRowsRetry();
                    return;
                }

                if (rowsRetryGeneration != rowsHandlerGeneration)
                {
                    ResetRowsRetry();
                    QueueRowsSourceApplication();
                    return;
                }

                rowsRetryAttempt++;
                RefreshRowsHostState();
                QueueRowsSourceApplication();
            });

        if (!scheduled)
        {
            rowsRetryScheduled = false;
        }
    }

    private void ResetRowsRetry()
    {
        rowsRetryScheduled = false;
        rowsRetryAttempt = 0;
        rowsRetryGeneration = -1;
    }

    private void ApplyRowsViewConfiguration()
    {
        RefreshRowsHostState();

        if (!CanUseRowsPlatformHost)
        {
            ScheduleRowsSourceRetry();
            return;
        }

        try
        {
            rowsView.ItemSizingStrategy = ItemSizingStrategy;
            rowsView.VerticalScrollBarVisibility =
                VerticalScrollBarVisibility;
        }
        catch (InvalidOperationException exception)
            when (IsPlatformViewUnavailable(exception))
        {
            rowsHandlerReady = false;
            rowsSourceUpdatePending = true;

            Debug.WriteLine(
                $"VirtualizedDataGrid deferred CollectionView configuration: " +
                $"{exception.Message}");

            ScheduleRowsSourceRetry();
        }
    }

    private static bool IsPlatformViewUnavailable(InvalidOperationException exception)
    {
        return exception.Message.Contains(
                   "PlatformView",
                   StringComparison.OrdinalIgnoreCase) &&
               exception.Message.Contains(
                   "null",
                   StringComparison.OrdinalIgnoreCase);
    }
}

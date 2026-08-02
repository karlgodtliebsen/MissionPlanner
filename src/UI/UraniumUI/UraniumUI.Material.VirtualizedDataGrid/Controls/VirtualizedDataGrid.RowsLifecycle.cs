using System.Collections;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Lifecycle integration for the lightweight rows viewport. Unlike a native
/// items control, the viewport owns no adapter for the logical collection and
/// can therefore update and release its bounded presenter pool synchronously.
/// </summary>
public partial class VirtualizedDataGrid
{
    private IList? desiredRowsSource;
    private int rowsHandlerGeneration;

    /// <summary>Gets whether the lightweight rows host can accept updates.</summary>
    protected bool IsRowsHostReady =>
        !visualResourcesReleased && rowsView.Handler is not null;

    /// <summary>
    /// Gets whether a source update is waiting for a native handler. The
    /// lightweight host always applies updates synchronously.
    /// </summary>
    protected bool HasPendingRowsSourceUpdate => false;

    private int RowsHandlerGeneration => rowsHandlerGeneration;

    private void AttachRowsViewLifecycle()
    {
        rowsView.HandlerChanging += RowsView_HandlerChanging;
        rowsView.HandlerChanged += RowsView_HandlerChanged;
        rowsView.Loaded += RowsView_Loaded;
        rowsView.Unloaded += RowsView_Unloaded;
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

        ResumeRowsPresentation();
    }

    private void VirtualizedDataGrid_Unloaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.GridUnloadedCount++;
            Diagnostics.LastGridUnloadedAt = DateTimeOffset.UtcNow;
        }

        SuspendRowsPresentation();
    }

    private void RowsView_HandlerChanging(object? sender, HandlerChangingEventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHandlerChangingCount++;
            Diagnostics.LastRowsHandlerChangingAt = DateTimeOffset.UtcNow;
        }

        rowsHandlerGeneration++;
        SuspendRowsPresentation();
    }

    private void RowsView_HandlerChanged(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHandlerChangedCount++;
            Diagnostics.LastRowsHandlerChangedAt = DateTimeOffset.UtcNow;
        }

        rowsHandlerGeneration++;
        ApplyRowsViewConfiguration();
        ResumeRowsPresentation();
    }

    private void RowsView_Loaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsViewLoadedCount++;
            Diagnostics.LastRowsViewLoadedAt = DateTimeOffset.UtcNow;
        }

        ResumeRowsPresentation();
    }

    private void RowsView_Unloaded(object? sender, EventArgs args)
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsViewUnloadedCount++;
            Diagnostics.LastRowsViewUnloadedAt = DateTimeOffset.UtcNow;
        }

        SuspendRowsPresentation();
    }

    private void SuspendRowsPresentation()
    {
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsHostUnavailableCount++;
        }

        // Work is proportional to the bounded presenter pool, never the source.
        // Keep the managed source reference so a temporary unload can resume.
        ReleaseRealizedRows();
    }

    private void ResumeRowsPresentation()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        rowsView.SetItemsSource(desiredRowsSource);
        rowsView.RefreshRows();
        UpdateEmptyViewVisibility();
        UpdateSearchBarVisibility();
    }

    private void SetDesiredRowsSource(IList? source)
    {
        desiredRowsSource = source;
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.RowsSourceApplyQueuedCount++;
        }

        var started = Diagnostics.StartTiming();
        rowsView.SetItemsSource(source);
        Diagnostics.RecordRowsSourceApply(started, source is null);
        Diagnostics.RecordRowsHostSourceSet(started, source is null);
    }

    private void InvalidateAppliedRowsSource()
    {
        rowsView.RefreshRows();
    }

    private void ApplyRowsViewConfiguration()
    {
        rowsView.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        rowsView.RefreshRows();
    }
}

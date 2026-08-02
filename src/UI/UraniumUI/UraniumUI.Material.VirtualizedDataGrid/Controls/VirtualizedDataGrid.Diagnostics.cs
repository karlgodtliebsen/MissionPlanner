using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

public partial class VirtualizedDataGrid
{
    /// <summary>
    /// Gets lifecycle and rendering measurements for this grid instance.
    /// Diagnostics are enabled by default and can be reset between scenarios.
    /// </summary>
    public VirtualizedDataGridDiagnostics Diagnostics { get; } = new();
}

/// <summary>
/// Allocation-light counters and timings collected by a
/// <see cref="VirtualizedDataGrid"/> instance. Durations describe synchronous
/// managed work. Native CollectionView work that continues after a property
/// setter returns is represented by the lifecycle timestamps and event counts.
/// </summary>
#pragma warning disable CS1591
public sealed class VirtualizedDataGridDiagnostics
{
    /// <summary>Gets or sets whether new measurements are recorded.</summary>
    public bool IsEnabled { get; set; } = true;

    public int ItemsSourceChangeCount { get; internal set; }
    public int ItemsSourceCollectionChangeCount { get; internal set; }
    public int DataViewRefreshCount { get; internal set; }
    public int RowsSourceApplyCount { get; internal set; }
    public int RowsSourceClearCount { get; internal set; }
    public int RowsSourceApplyQueuedCount { get; internal set; }
    public int RowsSourceApplyRetryCount { get; internal set; }
    public int RowsSourceApplyFailureCount { get; internal set; }
    public int NativeItemsSourceSetCount { get; internal set; }
    public int NativeItemsSourceClearCount { get; internal set; }

    public int PresenterCreatedCount { get; internal set; }
    public int PresenterBindingContextChangeCount { get; internal set; }
    public int PresenterBuildCount { get; internal set; }
    public int PresenterReleaseCount { get; internal set; }
    public int ReleasedCellCount { get; internal set; }
    public int LivePresenterCount { get; internal set; }
    public int PeakLivePresenterCount { get; internal set; }

    public int RebuildCount { get; internal set; }
    public int RealizedRowsRefreshCount { get; internal set; }
    public int ColumnLayoutCount { get; internal set; }
    public int AutoColumnMeasurementCount { get; internal set; }
    public int RealizedRowsReleaseCount { get; internal set; }

    public int GridLoadedCount { get; internal set; }
    public int GridUnloadedCount { get; internal set; }
    public int RowsViewLoadedCount { get; internal set; }
    public int RowsViewUnloadedCount { get; internal set; }
    public int RowsHandlerChangingCount { get; internal set; }
    public int RowsHandlerChangedCount { get; internal set; }
    public int RowsHostUnavailableCount { get; internal set; }

    public TimeSpan LastDataViewRefreshDuration { get; internal set; }
    public TimeSpan TotalDataViewRefreshDuration { get; internal set; }
    public TimeSpan MaximumDataViewRefreshDuration { get; internal set; }
    public TimeSpan LastRowsSourceApplyDuration { get; internal set; }
    public TimeSpan TotalRowsSourceApplyDuration { get; internal set; }
    public TimeSpan MaximumRowsSourceApplyDuration { get; internal set; }
    public TimeSpan LastNativeItemsSourceSetDuration { get; internal set; }
    public TimeSpan TotalNativeItemsSourceSetDuration { get; internal set; }
    public TimeSpan MaximumNativeItemsSourceSetDuration { get; internal set; }
    public TimeSpan LastRealizedRowsReleaseDuration { get; internal set; }
    public TimeSpan TotalRealizedRowsReleaseDuration { get; internal set; }
    public TimeSpan MaximumRealizedRowsReleaseDuration { get; internal set; }
    public TimeSpan LastRebuildDuration { get; internal set; }
    public TimeSpan TotalRebuildDuration { get; internal set; }
    public TimeSpan MaximumRebuildDuration { get; internal set; }
    public TimeSpan LastRealizedRowsRefreshDuration { get; internal set; }
    public TimeSpan TotalRealizedRowsRefreshDuration { get; internal set; }
    public TimeSpan LastColumnLayoutDuration { get; internal set; }
    public TimeSpan TotalColumnLayoutDuration { get; internal set; }
    public TimeSpan LastAutoColumnMeasurementDuration { get; internal set; }
    public TimeSpan TotalAutoColumnMeasurementDuration { get; internal set; }

    public DateTimeOffset? LastGridLoadedAt { get; internal set; }
    public DateTimeOffset? LastGridUnloadedAt { get; internal set; }
    public DateTimeOffset? LastRowsViewLoadedAt { get; internal set; }
    public DateTimeOffset? LastRowsViewUnloadedAt { get; internal set; }
    public DateTimeOffset? LastRowsHandlerChangingAt { get; internal set; }
    public DateTimeOffset? LastRowsHandlerChangedAt { get; internal set; }
    public DateTimeOffset? LastRowsSourceClearedAt { get; internal set; }

    /// <summary>Clears all collected values while preserving <see cref="IsEnabled"/>.</summary>
    public void Reset()
    {
        var enabled = IsEnabled;
        foreach (var property in GetType().GetProperties()
                     .Where(property => property.CanWrite &&
                                        property.Name != nameof(IsEnabled)))
        {
            property.SetValue(this, property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null);
        }

        IsEnabled = enabled;
    }

    /// <summary>
    /// Creates a human-readable multiline snapshot of the currently collected
    /// diagnostics.
    /// </summary>
    public string CreateReport()
    {
        var report = new StringBuilder();
        report.AppendLine("VirtualizedDataGrid diagnostics");
        report.AppendLine($"Enabled: {IsEnabled}");

        AppendSection(report, "Sources");
        AppendCount(report, "ItemsSource changes", ItemsSourceChangeCount);
        AppendCount(report, "Collection changes", ItemsSourceCollectionChangeCount);
        AppendCount(report, "Data-view refreshes", DataViewRefreshCount);
        AppendDuration(report, "Data-view refresh", LastDataViewRefreshDuration,
            TotalDataViewRefreshDuration, MaximumDataViewRefreshDuration);
        AppendCount(report, "Rows source applications", RowsSourceApplyCount);
        AppendCount(report, "Rows source clears", RowsSourceClearCount);
        AppendCount(report, "Queued applications", RowsSourceApplyQueuedCount);
        AppendCount(report, "Application retries", RowsSourceApplyRetryCount);
        AppendCount(report, "Application failures", RowsSourceApplyFailureCount);
        AppendDuration(report, "Rows source application", LastRowsSourceApplyDuration,
            TotalRowsSourceApplyDuration, MaximumRowsSourceApplyDuration);
        AppendCount(report, "Native ItemsSource sets", NativeItemsSourceSetCount);
        AppendCount(report, "Native ItemsSource clears", NativeItemsSourceClearCount);
        AppendDuration(report, "Native ItemsSource setter", LastNativeItemsSourceSetDuration,
            TotalNativeItemsSourceSetDuration, MaximumNativeItemsSourceSetDuration);

        AppendSection(report, "Presenters and cells");
        AppendCount(report, "Presenters created", PresenterCreatedCount);
        AppendCount(report, "BindingContext changes", PresenterBindingContextChangeCount);
        AppendCount(report, "Presenter builds", PresenterBuildCount);
        AppendCount(report, "Presenters released", PresenterReleaseCount);
        AppendCount(report, "Cells released", ReleasedCellCount);
        AppendCount(report, "Presenters currently tracked", LivePresenterCount);
        AppendCount(report, "Peak tracked presenters", PeakLivePresenterCount);
        AppendCount(report, "Realized-row releases", RealizedRowsReleaseCount);
        AppendDuration(report, "Realized-row release", LastRealizedRowsReleaseDuration,
            TotalRealizedRowsReleaseDuration, MaximumRealizedRowsReleaseDuration);

        AppendSection(report, "Rendering and layout");
        AppendCount(report, "Grid rebuilds", RebuildCount);
        AppendDuration(report, "Grid rebuild", LastRebuildDuration,
            TotalRebuildDuration, MaximumRebuildDuration);
        AppendCount(report, "Realized-row refreshes", RealizedRowsRefreshCount);
        AppendDuration(report, "Realized-row refresh", LastRealizedRowsRefreshDuration,
            TotalRealizedRowsRefreshDuration);
        AppendCount(report, "Column layouts", ColumnLayoutCount);
        AppendDuration(report, "Column layout", LastColumnLayoutDuration,
            TotalColumnLayoutDuration);
        AppendCount(report, "Auto-column measurements", AutoColumnMeasurementCount);
        AppendDuration(report, "Auto-column measurement", LastAutoColumnMeasurementDuration,
            TotalAutoColumnMeasurementDuration);

        AppendSection(report, "Lifecycle");
        AppendCount(report, "Grid loaded", GridLoadedCount);
        AppendCount(report, "Grid unloaded", GridUnloadedCount);
        AppendCount(report, "Rows view loaded", RowsViewLoadedCount);
        AppendCount(report, "Rows view unloaded", RowsViewUnloadedCount);
        AppendCount(report, "Rows handler changing", RowsHandlerChangingCount);
        AppendCount(report, "Rows handler changed", RowsHandlerChangedCount);
        AppendCount(report, "Rows host unavailable", RowsHostUnavailableCount);

        AppendSection(report, "Latest lifecycle timestamps (UTC)");
        AppendTimestamp(report, "Grid loaded", LastGridLoadedAt);
        AppendTimestamp(report, "Grid unloaded", LastGridUnloadedAt);
        AppendTimestamp(report, "Rows view loaded", LastRowsViewLoadedAt);
        AppendTimestamp(report, "Rows view unloaded", LastRowsViewUnloadedAt);
        AppendTimestamp(report, "Rows handler changing", LastRowsHandlerChangingAt);
        AppendTimestamp(report, "Rows handler changed", LastRowsHandlerChangedAt);
        AppendTimestamp(report, "Rows source cleared", LastRowsSourceClearedAt);

        return report.ToString().TrimEnd();
    }

    internal long StartTiming() => IsEnabled ? Stopwatch.GetTimestamp() : 0;

    internal void RecordDataViewRefresh(long started)
    {
        if (!IsEnabled) return;
        DataViewRefreshCount++;
        RecordDuration(started,
            duration => LastDataViewRefreshDuration = duration,
            duration => TotalDataViewRefreshDuration += duration,
            duration => MaximumDataViewRefreshDuration = Max(MaximumDataViewRefreshDuration, duration));
    }

    internal void RecordRowsSourceApply(long started, bool cleared)
    {
        if (!IsEnabled) return;
        RowsSourceApplyCount++;
        if (cleared)
        {
            RowsSourceClearCount++;
            LastRowsSourceClearedAt = DateTimeOffset.UtcNow;
        }

        RecordDuration(started,
            duration => LastRowsSourceApplyDuration = duration,
            duration => TotalRowsSourceApplyDuration += duration,
            duration => MaximumRowsSourceApplyDuration = Max(MaximumRowsSourceApplyDuration, duration));
    }

    internal void RecordNativeItemsSourceSet(long started, bool cleared)
    {
        if (!IsEnabled) return;
        NativeItemsSourceSetCount++;
        if (cleared)
        {
            NativeItemsSourceClearCount++;
            LastRowsSourceClearedAt = DateTimeOffset.UtcNow;
        }

        RecordDuration(started,
            duration => LastNativeItemsSourceSetDuration = duration,
            duration => TotalNativeItemsSourceSetDuration += duration,
            duration => MaximumNativeItemsSourceSetDuration = Max(MaximumNativeItemsSourceSetDuration, duration));
    }

    internal void RecordRealizedRowsRelease(long started, int presenters, int cells)
    {
        if (!IsEnabled) return;
        RealizedRowsReleaseCount++;
        PresenterReleaseCount += presenters;
        ReleasedCellCount += cells;
        LivePresenterCount = Math.Max(0, LivePresenterCount - presenters);
        RecordDuration(started,
            duration => LastRealizedRowsReleaseDuration = duration,
            duration => TotalRealizedRowsReleaseDuration += duration,
            duration => MaximumRealizedRowsReleaseDuration = Max(MaximumRealizedRowsReleaseDuration, duration));
    }

    internal void RecordRebuild(long started)
    {
        if (!IsEnabled) return;
        RebuildCount++;
        RecordDuration(started,
            duration => LastRebuildDuration = duration,
            duration => TotalRebuildDuration += duration,
            duration => MaximumRebuildDuration = Max(MaximumRebuildDuration, duration));
    }

    internal void RecordRealizedRowsRefresh(long started)
    {
        if (!IsEnabled) return;
        RealizedRowsRefreshCount++;
        RecordDuration(started,
            duration => LastRealizedRowsRefreshDuration = duration,
            duration => TotalRealizedRowsRefreshDuration += duration);
    }

    internal void RecordColumnLayout(long started)
    {
        if (!IsEnabled) return;
        ColumnLayoutCount++;
        RecordDuration(started,
            duration => LastColumnLayoutDuration = duration,
            duration => TotalColumnLayoutDuration += duration);
    }

    internal void RecordAutoColumnMeasurement(long started)
    {
        if (!IsEnabled) return;
        AutoColumnMeasurementCount++;
        RecordDuration(started,
            duration => LastAutoColumnMeasurementDuration = duration,
            duration => TotalAutoColumnMeasurementDuration += duration);
    }

    internal void PresenterCreated()
    {
        if (!IsEnabled) return;
        PresenterCreatedCount++;
        LivePresenterCount++;
        PeakLivePresenterCount = Math.Max(PeakLivePresenterCount, LivePresenterCount);
    }

    private static void RecordDuration(
        long started,
        Action<TimeSpan> setLast,
        Action<TimeSpan> addTotal,
        Action<TimeSpan>? setMaximum = null)
    {
        if (started == 0) return;
        var duration = Stopwatch.GetElapsedTime(started);
        setLast(duration);
        addTotal(duration);
        setMaximum?.Invoke(duration);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;

    private static void AppendSection(StringBuilder report, string title)
    {
        report.AppendLine();
        report.AppendLine($"[{title}]");
    }

    private static void AppendCount(StringBuilder report, string label, int value) =>
        report.AppendLine($"{label}: {value.ToString(CultureInfo.InvariantCulture)}");

    private static void AppendDuration(
        StringBuilder report,
        string label,
        TimeSpan last,
        TimeSpan total,
        TimeSpan? maximum = null)
    {
        report.Append($"{label}: last {FormatMilliseconds(last)}, total {FormatMilliseconds(total)}");
        if (maximum.HasValue)
        {
            report.Append($", max {FormatMilliseconds(maximum.Value)}");
        }

        report.AppendLine();
    }

    private static void AppendTimestamp(
        StringBuilder report,
        string label,
        DateTimeOffset? timestamp) =>
        report.AppendLine($"{label}: {(timestamp.HasValue ? timestamp.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : "-")}");

    private static string FormatMilliseconds(TimeSpan duration) =>
        $"{duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms";
}
#pragma warning restore CS1591

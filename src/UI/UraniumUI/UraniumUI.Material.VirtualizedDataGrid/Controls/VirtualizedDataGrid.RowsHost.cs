using System.Collections;
using Microsoft.Maui.Layouts;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// A lightweight vertical rows viewport. The logical item collection is never
/// assigned to a native items adapter; only a bounded pool of row presenters is
/// placed inside a tall extent canvas.
/// </summary>
internal sealed class VirtualizedDataGridRowsHost : ScrollView
{
    private readonly VirtualizedDataGrid owner;
    private readonly AbsoluteLayout extent = new();
    private readonly Dictionary<int, VirtualizedDataGridRowPresenter> realized = [];
    private readonly Stack<VirtualizedDataGridRowPresenter> recycled = [];

    private IList? itemsSource;
    private double[] measuredHeights = [];
    private double[] rowOffsets = [0];
    private bool offsetsInvalid = true;
    private bool updateScheduled;
    private bool updating;
    private bool updatesSuspended;
    private int updateGeneration;
    private double uniformMeasuredHeight;

    internal VirtualizedDataGridRowsHost(VirtualizedDataGrid owner)
    {
        this.owner = owner;
        Orientation = ScrollOrientation.Vertical;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Content = extent;
        Scrolled += OnScrolled;
        SizeChanged += OnViewportSizeChanged;
    }

    internal IList? ItemsSource => itemsSource;
    internal int RealizedCount => realized.Count;
    internal int RecycledCount => recycled.Count;
    internal double ExtentHeight => extent.HeightRequest;
    internal IReadOnlyDictionary<int, VirtualizedDataGridRowPresenter> RealizedRows => realized;
    internal IReadOnlyCollection<int> RealizedIndices => realized.Keys;

    internal void SetItemsSource(IList? source)
    {
        if (!ReferenceEquals(itemsSource, source))
        {
            itemsSource = source;
            measuredHeights = source is null
                ? []
                : Enumerable.Repeat(double.NaN, source.Count).ToArray();
            rowOffsets = new double[(source?.Count ?? 0) + 1];
            uniformMeasuredHeight = 0;
            offsetsInvalid = true;
        }
        else if (source is not null && measuredHeights.Length != source.Count)
        {
            Array.Resize(ref measuredHeights, source.Count);
            for (var index = 0; index < measuredHeights.Length; index++)
            {
                if (measuredHeights[index] <= 0)
                {
                    measuredHeights[index] = double.NaN;
                }
            }

            rowOffsets = new double[source.Count + 1];
            offsetsInvalid = true;
        }

        if (source is null || source.Count == 0)
        {
            ReleaseRows();
            SetExtent(0);
            return;
        }

        QueueViewportUpdate();
    }

    internal void RefreshRows()
    {
        offsetsInvalid = true;
        QueueViewportUpdate();
    }

    internal void SuspendUpdates()
    {
        updatesSuspended = true;
        updateScheduled = false;
        updateGeneration++;
    }

    internal void ResumeUpdates()
    {
        updatesSuspended = false;
        updateScheduled = false;
        updateGeneration++;
    }

    internal void ApplyColumnWidth(double width)
    {
        var finiteWidth = double.IsFinite(width) && width > 0 ? width : 0;
        extent.WidthRequest = finiteWidth;
        WidthRequest = finiteWidth > 0 ? finiteWidth : -1;

        foreach (var pair in realized)
        {
            PositionPresenter(pair.Key, pair.Value);
        }
    }

    internal (int Presenters, int Cells) ReleaseRows()
    {
        var presenters = realized.Values
            .Concat(recycled)
            .Distinct()
            .ToArray();
        var cells = 0;

        foreach (var presenter in presenters)
        {
            cells += presenter.ReleaseVisualTree();
            presenter.BindingContext = null;
        }

        realized.Clear();
        recycled.Clear();
        extent.Children.Clear();
        updateScheduled = false;

        return (presenters.Length, cells);
    }

    internal Task ScrollToTopAsync() => ScrollToAsync(0, 0, false);

    internal void UpdateViewportNow()
    {
        updateScheduled = false;
        if (updatesSuspended || updating)
        {
            return;
        }

        updating = true;
        try
        {
            UpdateOffsets();

            if (itemsSource is not { Count: > 0 })
            {
                ReleaseRows();
                SetExtent(0);
                return;
            }

            var viewportHeight = Height > 0
                ? Height
                : HeightRequest > 0
                    ? HeightRequest
                    : owner.EstimatedRowHeight * 8;
            UpdateViewportCore(Math.Max(0, ScrollY), viewportHeight);
        }
        finally
        {
            updating = false;
        }
    }

    internal void UpdateViewport(double scrollOffset, double viewportHeight)
    {
        if (updatesSuspended)
        {
            return;
        }

        UpdateOffsets();
        UpdateViewportCore(
            Math.Max(0, scrollOffset),
            Math.Max(1, viewportHeight));
    }

    private void UpdateViewportCore(double scrollOffset, double viewportHeight)
    {
        var started = owner.Diagnostics.StartTiming();
        if (itemsSource is not { Count: > 0 })
        {
            ReleaseRows();
            SetExtent(0);
            owner.Diagnostics.RecordViewportUpdate(started, 0);
            return;
        }

        var maximumOffset = Math.Max(0, rowOffsets[^1] - viewportHeight);
        var effectiveOffset = Math.Min(scrollOffset, maximumOffset);
        var firstVisible = FindRowIndex(effectiveOffset);
        var lastVisible = FindRowIndex(effectiveOffset + viewportHeight);
        var first = Math.Max(0, firstVisible - owner.OverscanRowCount);
        var last = Math.Min(
            itemsSource.Count - 1,
            lastVisible + owner.OverscanRowCount);

        RecycleOutsideRange(first, last);
        for (var index = first; index <= last; index++)
        {
            Realize(index);
        }

        SetExtent(rowOffsets[^1]);
        owner.Diagnostics.RecordViewportUpdate(started, realized.Count);
    }

    internal void ReportMeasuredHeight(int index, double height)
    {
        if (itemsSource is null ||
            index < 0 ||
            index >= measuredHeights.Length ||
            height <= 0 ||
            owner.RowHeight > 0)
        {
            return;
        }

        if (owner.ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem)
        {
            if (uniformMeasuredHeight > 0 || index != 0)
            {
                return;
            }

            uniformMeasuredHeight = height;
        }
        else
        {
            var previous = measuredHeights[index];
            if (!double.IsNaN(previous) && Math.Abs(previous - height) <= 0.5)
            {
                return;
            }

            measuredHeights[index] = height;
        }

        offsetsInvalid = true;
        QueueViewportUpdate();
    }

    private void OnScrolled(object? sender, ScrolledEventArgs args) =>
        QueueViewportUpdate();

    private void OnViewportSizeChanged(object? sender, EventArgs args) =>
        QueueViewportUpdate();

    private void QueueViewportUpdate()
    {
        if (updatesSuspended || updateScheduled)
        {
            return;
        }

        updateScheduled = true;
        var generation = updateGeneration;

        try
        {
            if (!(Dispatcher?.Dispatch(() =>
                {
                    if (updatesSuspended || generation != updateGeneration)
                    {
                        updateScheduled = false;
                        return;
                    }

                    UpdateViewportNow();
                }) ?? false))
            {
                updateScheduled = false;
                if (!updatesSuspended && generation == updateGeneration)
                {
                    UpdateViewportNow();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // MAUI's dispatcher service can be disposed before the final size or
            // scroll notification is delivered during application shutdown.
            updateScheduled = false;
        }
    }

    private void UpdateOffsets()
    {
        if (!offsetsInvalid || itemsSource is null)
        {
            return;
        }

        if (rowOffsets.Length != itemsSource.Count + 1)
        {
            rowOffsets = new double[itemsSource.Count + 1];
        }

        rowOffsets[0] = 0;
        for (var index = 0; index < itemsSource.Count; index++)
        {
            rowOffsets[index + 1] = rowOffsets[index] + GetRowHeight(index);
        }

        offsetsInvalid = false;
    }

    private double GetRowHeight(int index)
    {
        if (owner.RowHeight > 0)
        {
            return owner.RowHeight;
        }

        if (owner.ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem &&
            uniformMeasuredHeight > 0)
        {
            return uniformMeasuredHeight;
        }

        if (index < measuredHeights.Length &&
            !double.IsNaN(measuredHeights[index]) &&
            measuredHeights[index] > 0)
        {
            return measuredHeights[index];
        }

        return Math.Max(1, owner.EstimatedRowHeight);
    }

    private int FindRowIndex(double offset)
    {
        if (rowOffsets.Length <= 1)
        {
            return 0;
        }

        var index = Array.BinarySearch(rowOffsets, offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return Math.Clamp(index, 0, rowOffsets.Length - 2);
    }

    private void RecycleOutsideRange(int first, int last)
    {
        foreach (var index in realized.Keys
                     .Where(index => index < first || index > last)
                     .ToArray())
        {
            var presenter = realized[index];
            realized.Remove(index);
            presenter.RealizedIndex = -1;
            presenter.BindingContext = null;
            presenter.IsVisible = false;
            recycled.Push(presenter);
        }
    }

    private void Realize(int index)
    {
        if (realized.TryGetValue(index, out var existing))
        {
            PositionPresenter(index, existing);
            return;
        }

        var presenter = recycled.Count > 0
            ? recycled.Pop()
            : new VirtualizedDataGridRowPresenter(owner);
        presenter.RealizedIndex = index;
        presenter.IsVisible = true;
        presenter.BindingContext = itemsSource![index];

        if (!extent.Children.Contains(presenter))
        {
            extent.Children.Add(presenter);
        }

        realized[index] = presenter;
        PositionPresenter(index, presenter);
    }

    private void PositionPresenter(int index, VirtualizedDataGridRowPresenter presenter)
    {
        var width = extent.WidthRequest > 0
            ? extent.WidthRequest
            : Width > 0
                ? Width
                : owner.Width;
        var height = owner.RowHeight > 0
            ? owner.RowHeight
            : AbsoluteLayout.AutoSize;

        AbsoluteLayout.SetLayoutFlags(presenter, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(
            presenter,
            new Rect(0, rowOffsets[index], Math.Max(0, width), height));
    }

    private void SetExtent(double height)
    {
        extent.HeightRequest = Math.Max(0, height);
        MinimumHeightRequest = 0;
    }
}

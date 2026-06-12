using static Microsoft.Maui.Controls.Grid;

namespace MissionPlanner.Controls;

/// <summary>
/// A vertical grid splitter control for resizing grid columns in .NET MAUI.
/// </summary>
public class GridSplitter : ContentView
{
    private double startX;
    private ColumnDefinition? leftColumn;
    private ColumnDefinition? rightColumn;
    private double leftColumnStartWidth;
    private double rightColumnStartWidth;
    private Grid? parentGrid;

    /// <summary>
    /// Gets or sets the thickness of the splitter. Default is 4.
    /// </summary>
    public double SplitterThickness { get; set; } = 4;

    /// <summary>
    /// Gets or sets the color of the splitter. Default is DarkGray.
    /// </summary>
    public Color SplitterColor { get; set; } = Colors.DarkGray;

    /// <summary>
    /// Gets or sets the hover color of the splitter. Default is Gray.
    /// </summary>
    public Color HoverColor { get; set; } = Colors.Gray;

    public GridSplitter()
    {
        BackgroundColor = SplitterColor;
        WidthRequest = SplitterThickness;

        PanGestureRecognizer panGesture = new();
        panGesture.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(panGesture);

        PointerGestureRecognizer pointerGesture = new();
        pointerGesture.PointerEntered += OnPointerEntered;
        pointerGesture.PointerExited += OnPointerExited;
        GestureRecognizers.Add(pointerGesture);

#if WINDOWS
        // Set Windows cursor for resize
        HandlerChanged += OnHandlerChanged;
#endif
    }

#if WINDOWS
    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
        {
            element.PointerEntered += (s, args) =>
            {
                try
                {
                    Microsoft.UI.Xaml.Window.Current.CoreWindow.PointerCursor =
                        new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.SizeWestEast, 0);
                }
                catch
                {
                    // Cursor setting may fail in some contexts, that's OK
                }
            };

            element.PointerExited += (s, args) =>
            {
                try
                {
                    Microsoft.UI.Xaml.Window.Current.CoreWindow.PointerCursor =
                        new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
                }
                catch
                {
                    // Cursor setting may fail in some contexts, that's OK
                }
            };
        }
    }
#endif

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        BackgroundColor = HoverColor;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        BackgroundColor = SplitterColor;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        InitializeColumns();
    }

    private void InitializeColumns()
    {
        if (Parent is not Grid grid)
        {
            return;
        }

        parentGrid = grid;
        var columnIndex = GetColumn(this);

        // Ensure we have valid left and right columns
        if (columnIndex > 0 && columnIndex + 1 < grid.ColumnDefinitions.Count)
        {
            leftColumn = grid.ColumnDefinitions[columnIndex - 1];
            rightColumn = grid.ColumnDefinitions[columnIndex + 1];
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (leftColumn == null || rightColumn == null || parentGrid == null)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                startX = 0;

                // Get actual rendered widths from the parent grid
                leftColumnStartWidth = GetActualColumnWidth(leftColumn, parentGrid.ColumnDefinitions.IndexOf(leftColumn));
                rightColumnStartWidth = GetActualColumnWidth(rightColumn, parentGrid.ColumnDefinitions.IndexOf(rightColumn));
                break;

            case GestureStatus.Running:
                var delta = e.TotalX;
                var newLeftWidth = leftColumnStartWidth + delta;
                var newRightWidth = rightColumnStartWidth - delta;

                // Enforce minimum widths
                const double minWidth = 100;
                if (newLeftWidth >= minWidth && newRightWidth >= minWidth)
                {
                    leftColumn.Width = new GridLength(newLeftWidth, GridUnitType.Absolute);
                    rightColumn.Width = new GridLength(newRightWidth, GridUnitType.Absolute);
                }

                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                break;
        }
    }

    private double GetActualColumnWidth(ColumnDefinition column, int columnIndex)
    {
        // If already absolute, return the value
        if (column.Width.IsAbsolute)
        {
            return column.Width.Value;
        }

        // For Star or Auto sizing, find a child element in this column to get its actual width
        if (parentGrid != null)
        {
            foreach (var child in parentGrid.Children)
            {
                if (child is BindableObject bindable)
                {
                    var childColumn = GetColumn(bindable);
                    if (childColumn == columnIndex && child is View viewElement && viewElement.Width > 0)
                    {
                        return viewElement.Width;
                    }
                }
            }
        }

        // Fallback: estimate based on grid width and column definitions
        if (parentGrid != null && parentGrid.Width > 0)
        {
            // For star sizing, calculate proportional width
            if (column.Width.IsStar)
            {
                var totalStars = parentGrid.ColumnDefinitions.Sum(c => c.Width.IsStar ? c.Width.Value : 0);
                var availableWidth = parentGrid.Width;

                // Subtract absolute widths
                foreach (var col in parentGrid.ColumnDefinitions)
                {
                    if (col.Width.IsAbsolute)
                    {
                        availableWidth -= col.Width.Value;
                    }
                }

                return totalStars > 0 ? availableWidth * column.Width.Value / totalStars : 300;
            }
        }

        // Final fallback
        return 300;
    }
}
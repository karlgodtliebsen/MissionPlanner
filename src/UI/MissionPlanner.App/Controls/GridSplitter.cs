using static Microsoft.Maui.Controls.Grid;

namespace MissionPlanner.App.Controls;

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

    private readonly BoxView? boxView;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridSplitter"/> class.
    /// </summary>
    public GridSplitter()
    {
        WidthRequest = SplitterThickness;

        // Create a BoxView as content to make the control hit-testable
        // ContentView with only BackgroundColor won't receive touch/gesture events
        boxView = new BoxView
        {
            Color = SplitterColor,
            WidthRequest = SplitterThickness,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        Content = boxView;

        // Ensure the control can receive input
        InputTransparent = false;

        PanGestureRecognizer panGesture = new();
        panGesture.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(panGesture);

        PointerGestureRecognizer pointerGesture = new();
        pointerGesture.PointerEntered += OnPointerEntered;
        pointerGesture.PointerExited += OnPointerExited;
        GestureRecognizers.Add(pointerGesture);

#if WINDOWS
        // Set Windows cursor for resize
        // HandlerChanged += OnHandlerChanged;
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
        boxView?.Color = HoverColor;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        boxView?.Color = SplitterColor;
    }

    /// <inheritdoc/>
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
        int columnIndex = GetColumn(this);

        // Ensure we have valid left and right columns
        if (columnIndex > 0 && columnIndex + 1 < grid.ColumnDefinitions.Count)
        {
            leftColumn = grid.ColumnDefinitions[columnIndex - 1];
            rightColumn = grid.ColumnDefinitions[columnIndex + 1];
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"GridSplitter PanUpdated: Status={e.StatusType}, TotalX={e.TotalX}");

        if (leftColumn == null || rightColumn == null || parentGrid == null)
        {
            System.Diagnostics.Debug.WriteLine("GridSplitter: columns not initialized");
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                startX = 0;

                // Get actual rendered widths from the parent grid
                leftColumnStartWidth = GetActualColumnWidth(leftColumn, parentGrid.ColumnDefinitions.IndexOf(leftColumn));
                rightColumnStartWidth = GetActualColumnWidth(rightColumn, parentGrid.ColumnDefinitions.IndexOf(rightColumn));

                System.Diagnostics.Debug.WriteLine($"GridSplitter Started: Left={leftColumnStartWidth}, Right={rightColumnStartWidth}");
                break;

            case GestureStatus.Running:
                double delta = e.TotalX;
                double newLeftWidth = leftColumnStartWidth + delta;
                double newRightWidth = rightColumnStartWidth - delta;

                System.Diagnostics.Debug.WriteLine($"GridSplitter Running: Delta={delta}, NewLeft={newLeftWidth}, NewRight={newRightWidth}");

                // Enforce minimum widths
                const double minWidth = 100;
                if (newLeftWidth >= minWidth && newRightWidth >= minWidth)
                {
                    // Never convert a star column to an absolute width: the star column is what
                    // absorbs window resizes (maximize/restore). Pin only the non-star side and
                    // let the star side take the remaining space, which yields the same visual
                    // drag behavior while keeping the layout responsive.
                    var leftIsStar = leftColumn.Width.IsStar;
                    var rightIsStar = rightColumn.Width.IsStar;

                    if (leftIsStar && rightIsStar)
                    {
                        // Both proportional: adjust the star weights so the ratio follows the drag.
                        leftColumn.Width = new GridLength(newLeftWidth, GridUnitType.Star);
                        rightColumn.Width = new GridLength(newRightWidth, GridUnitType.Star);
                    }
                    else if (leftIsStar)
                    {
                        rightColumn.Width = new GridLength(newRightWidth, GridUnitType.Absolute);
                    }
                    else if (rightIsStar)
                    {
                        leftColumn.Width = new GridLength(newLeftWidth, GridUnitType.Absolute);
                    }
                    else
                    {
                        leftColumn.Width = new GridLength(newLeftWidth, GridUnitType.Absolute);
                        rightColumn.Width = new GridLength(newRightWidth, GridUnitType.Absolute);
                    }

                    // Force the grid to re-layout immediately
                    ForceGridLayout();
                }

                break;

            case GestureStatus.Completed:
                System.Diagnostics.Debug.WriteLine("GridSplitter Completed");
                // Final layout update to ensure everything is settled
                ForceGridLayout();
                break;

            case GestureStatus.Canceled:
                System.Diagnostics.Debug.WriteLine("GridSplitter Canceled");
                break;
        }
    }

    /// <summary>
    /// Forces the parent grid to re-layout its children.
    /// </summary>
    private void ForceGridLayout()
    {
        if (parentGrid == null)
        {
            return;
        }

        // In .NET MAUI, we need to force a layout update
        // Using Dispatcher ensures this runs on the UI thread and triggers a visual update

        Dispatcher.Dispatch(() =>
        {
            // Invalidate the grid's measure - this forces re-measurement
            parentGrid.InvalidateMeasure();

            // Also invalidate all direct children to ensure they re-layout with new column widths
            foreach (var child in parentGrid.Children)
            {
                if (child is IView view)
                {
                    view.InvalidateMeasure();
                }
            }

            // Request a layout pass - this is more aggressive and ensures visual update
            if (parentGrid.Handler?.PlatformView != null)
            {
#if WINDOWS
                if (parentGrid.Handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
                {
                    element.InvalidateMeasure();
                    element.InvalidateArrange();
                }
#elif ANDROID
                if (parentGrid.Handler.PlatformView is Android.Views.View androidView)
                {
                    androidView.RequestLayout();
                }
#elif IOS || MACCATALYST
                if (parentGrid.Handler.PlatformView is UIKit.UIView iosView)
                {
                    iosView.SetNeedsLayout();
                    iosView.LayoutIfNeeded();
                }
#endif
            }
        });
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
                    int childColumn = GetColumn(bindable);
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
                double totalStars = parentGrid.ColumnDefinitions.Sum(c => c.Width.IsStar ? c.Width.Value : 0);
                double availableWidth = parentGrid.Width;

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
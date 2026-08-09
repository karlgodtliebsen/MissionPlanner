using System.Windows.Input;

namespace UraniumUI.Material.Controls;

/// <summary>
/// A control that allows the user to resize columns in a Grid by dragging a splitter.
/// </summary>
public class GridSplitter : ContentView
{
    private double startX;
    private ColumnDefinition? leftColumn;
    private ColumnDefinition? rightColumn;
    private double leftColumnStartWidth;
    private double rightColumnStartWidth;
    private Grid? parentGrid;
    private readonly BoxView boxView;
    private bool isDragging;

    public event EventHandler<(double? Previous, double? Next)>? ResizeStarted;
    public event EventHandler<(double? Previous, double? Next)>? ResizeChanged;
    public event EventHandler<(double? Previous, double? Next)>? ResizeCompleted;

    /// <inheritdoc />
    public GridSplitter()
    {
        WidthRequest = Thickness;
        MinimumWidthRequest = Thickness;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        InputTransparent = false;

        boxView = new BoxView
        {
            Color = SplitterColor,
            WidthRequest = Thickness,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        Content = boxView;

        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(panGesture);

        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += OnPointerEntered;
        pointerGesture.PointerExited += OnPointerExited;
        pointerGesture.PointerMoved += OnPointerMoved;
        GestureRecognizers.Add(pointerGesture);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;
        GestureRecognizers.Add(doubleTap);

        SizeChanged += (_, _) => ApplyAppearance();
    }

    public static readonly BindableProperty ResizeModeProperty =
        BindableProperty.Create(nameof(ResizeMode), typeof(GridSplitterResizeMode), typeof(GridSplitter), GridSplitterResizeMode.PreviousAndNext);

    public GridSplitterResizeMode ResizeMode
    {
        get => (GridSplitterResizeMode)GetValue(ResizeModeProperty);
        set => SetValue(ResizeModeProperty, value);
    }

    public static readonly BindableProperty ThicknessProperty =
        BindableProperty.Create(nameof(Thickness), typeof(double), typeof(GridSplitter), 8d, propertyChanged: OnAppearanceChanged);

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public static readonly BindableProperty SplitterColorProperty =
        BindableProperty.Create(nameof(SplitterColor), typeof(Color), typeof(GridSplitter), Colors.DarkGray, propertyChanged: OnAppearanceChanged);

    public Color SplitterColor
    {
        get => (Color)GetValue(SplitterColorProperty);
        set => SetValue(SplitterColorProperty, value);
    }

    public static readonly BindableProperty HoverColorProperty =
        BindableProperty.Create(nameof(HoverColor), typeof(Color), typeof(GridSplitter), Colors.Gray);

    public Color HoverColor
    {
        get => (Color)GetValue(HoverColorProperty);
        set => SetValue(HoverColorProperty, value);
    }

    public static readonly BindableProperty MinPreviousWidthProperty =
        BindableProperty.Create(nameof(MinPreviousWidth), typeof(double), typeof(GridSplitter), 100d);

    public double MinPreviousWidth
    {
        get => (double)GetValue(MinPreviousWidthProperty);
        set => SetValue(MinPreviousWidthProperty, value);
    }

    public static readonly BindableProperty MinNextWidthProperty =
        BindableProperty.Create(nameof(MinNextWidth), typeof(double), typeof(GridSplitter), 100d);

    public double MinNextWidth
    {
        get => (double)GetValue(MinNextWidthProperty);
        set => SetValue(MinNextWidthProperty, value);
    }

    public static readonly BindableProperty MaxPreviousWidthProperty =
        BindableProperty.Create(nameof(MaxPreviousWidth), typeof(double), typeof(GridSplitter), double.PositiveInfinity);

    public double MaxPreviousWidth
    {
        get => (double)GetValue(MaxPreviousWidthProperty);
        set => SetValue(MaxPreviousWidthProperty, value);
    }

    public static readonly BindableProperty MaxNextWidthProperty =
        BindableProperty.Create(nameof(MaxNextWidth), typeof(double), typeof(GridSplitter), double.PositiveInfinity);

    public double MaxNextWidth
    {
        get => (double)GetValue(MaxNextWidthProperty);
        set => SetValue(MaxNextWidthProperty, value);
    }

    public static readonly BindableProperty ResetOnDoubleTapProperty =
        BindableProperty.Create(nameof(ResetOnDoubleTap), typeof(bool), typeof(GridSplitter), false);

    public bool ResetOnDoubleTap
    {
        get => (bool)GetValue(ResetOnDoubleTapProperty);
        set => SetValue(ResetOnDoubleTapProperty, value);
    }

    public static readonly BindableProperty ResetPreviousWidthProperty =
        BindableProperty.Create(nameof(ResetPreviousWidth), typeof(double), typeof(GridSplitter), -1d);

    public double ResetPreviousWidth
    {
        get => (double)GetValue(ResetPreviousWidthProperty);
        set => SetValue(ResetPreviousWidthProperty, value);
    }

    public static readonly BindableProperty ResetNextWidthProperty =
        BindableProperty.Create(nameof(ResetNextWidth), typeof(double), typeof(GridSplitter), -1d);

    public double ResetNextWidth
    {
        get => (double)GetValue(ResetNextWidthProperty);
        set => SetValue(ResetNextWidthProperty, value);
    }

    public static readonly BindableProperty ResizeStartedCommandProperty =
        BindableProperty.Create(nameof(ResizeStartedCommand), typeof(ICommand), typeof(GridSplitter));

    public ICommand? ResizeStartedCommand
    {
        get => (ICommand?)GetValue(ResizeStartedCommandProperty);
        set => SetValue(ResizeStartedCommandProperty, value);
    }

    public static readonly BindableProperty ResizeChangedCommandProperty =
        BindableProperty.Create(nameof(ResizeChangedCommand), typeof(ICommand), typeof(GridSplitter));

    public ICommand? ResizeChangedCommand
    {
        get => (ICommand?)GetValue(ResizeChangedCommandProperty);
        set => SetValue(ResizeChangedCommandProperty, value);
    }

    public static readonly BindableProperty ResizeCompletedCommandProperty =
        BindableProperty.Create(nameof(ResizeCompletedCommand), typeof(ICommand), typeof(GridSplitter));

    public ICommand? ResizeCompletedCommand
    {
        get => (ICommand?)GetValue(ResizeCompletedCommandProperty);
        set => SetValue(ResizeCompletedCommandProperty, value);
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        InitializeColumns();
    }

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GridSplitter splitter)
        {
            splitter.ApplyAppearance();
        }
    }

    private void ApplyAppearance()
    {
        WidthRequest = Thickness;
        MinimumWidthRequest = Thickness;
        boxView.WidthRequest = Thickness;
        boxView.Color = isDragging ? HoverColor : SplitterColor;
    }

    private void InitializeColumns()
    {
        if (Parent is not Grid grid)
        {
            return;
        }

        parentGrid = grid;
        var columnIndex = Grid.GetColumn(this);

        if (columnIndex > 0 && columnIndex + 1 < grid.ColumnDefinitions.Count)
        {
            leftColumn = grid.ColumnDefinitions[columnIndex - 1];
            rightColumn = grid.ColumnDefinitions[columnIndex + 1];
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!isDragging)
        {
            boxView.Color = HoverColor;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!isDragging)
        {
            boxView.Color = SplitterColor;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isDragging)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (point is null)
        {
            return;
        }

        UpdateDrag(point.Value.X);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!ResetOnDoubleTap || parentGrid == null)
        {
            return;
        }

        var columnIndex = Grid.GetColumn(this);

        if (columnIndex > 0 && ResetPreviousWidth >= 0)
        {
            parentGrid.ColumnDefinitions[columnIndex - 1].Width = new GridLength(ResetPreviousWidth, GridUnitType.Absolute);
        }

        if (columnIndex + 1 < parentGrid.ColumnDefinitions.Count && ResetNextWidth >= 0)
        {
            parentGrid.ColumnDefinitions[columnIndex + 1].Width = new GridLength(ResetNextWidth, GridUnitType.Absolute);
        }

        ForceGridLayout();
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
                isDragging = true;
                startX = 0;
                leftColumnStartWidth = GetActualColumnWidth(leftColumn, parentGrid.ColumnDefinitions.IndexOf(leftColumn));
                rightColumnStartWidth = GetActualColumnWidth(rightColumn, parentGrid.ColumnDefinitions.IndexOf(rightColumn));

                (double? Previous, double? Next) startedArgs = (leftColumnStartWidth, rightColumnStartWidth);
                ResizeStarted?.Invoke(this, startedArgs);
                if (ResizeStartedCommand?.CanExecute(startedArgs) == true)
                {
                    ResizeStartedCommand.Execute(startedArgs);
                }

                boxView.Color = HoverColor;
                break;

            case GestureStatus.Running:
                UpdateDrag(e.TotalX);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                isDragging = false;
                boxView.Color = SplitterColor;

                var completedArgs = ((double?)GetActualColumnWidth(leftColumn, parentGrid.ColumnDefinitions.IndexOf(leftColumn)),
                    (double?)GetActualColumnWidth(rightColumn, parentGrid.ColumnDefinitions.IndexOf(rightColumn)));

                ForceGridLayout();
                ResizeCompleted?.Invoke(this, completedArgs);
                if (ResizeCompletedCommand?.CanExecute(completedArgs) == true)
                {
                    ResizeCompletedCommand.Execute(completedArgs);
                }

                break;
        }
    }

    private void UpdateDrag(double currentX)
    {
        if (leftColumn == null || rightColumn == null || parentGrid == null)
        {
            return;
        }

        var delta = currentX - startX;

        double? newLeft = null;
        double? newRight = null;

        switch (ResizeMode)
        {
            case GridSplitterResizeMode.Previous:
                newLeft = Clamp(leftColumnStartWidth + delta, MinPreviousWidth, MaxPreviousWidth);
                ApplyColumnWidth(leftColumn, newLeft.Value, parentGrid.ColumnDefinitions.IndexOf(leftColumn), true);
                break;

            case GridSplitterResizeMode.Next:
                newRight = Clamp(rightColumnStartWidth - delta, MinNextWidth, MaxNextWidth);
                ApplyColumnWidth(rightColumn, newRight.Value, parentGrid.ColumnDefinitions.IndexOf(rightColumn), true);
                break;

            case GridSplitterResizeMode.PreviousAndNext:
                var total = leftColumnStartWidth + rightColumnStartWidth;

                var proposedLeft = Clamp(leftColumnStartWidth + delta, MinPreviousWidth, MaxPreviousWidth);
                var proposedRight = total - proposedLeft;

                if (proposedRight < MinNextWidth)
                {
                    proposedRight = MinNextWidth;
                    proposedLeft = total - proposedRight;
                }

                if (!double.IsPositiveInfinity(MaxNextWidth) && proposedRight > MaxNextWidth)
                {
                    proposedRight = MaxNextWidth;
                    proposedLeft = total - proposedRight;
                }

                proposedLeft = Clamp(proposedLeft, MinPreviousWidth, MaxPreviousWidth);
                proposedRight = Clamp(proposedRight, MinNextWidth, MaxNextWidth);

                var leftIsStar = leftColumn.Width.IsStar;
                var rightIsStar = rightColumn.Width.IsStar;

                if (leftIsStar && rightIsStar)
                {
                    leftColumn.Width = new GridLength(proposedLeft, GridUnitType.Star);
                    rightColumn.Width = new GridLength(proposedRight, GridUnitType.Star);
                }
                else if (leftIsStar)
                {
                    rightColumn.Width = new GridLength(proposedRight, GridUnitType.Absolute);
                }
                else if (rightIsStar)
                {
                    leftColumn.Width = new GridLength(proposedLeft, GridUnitType.Absolute);
                }
                else
                {
                    leftColumn.Width = new GridLength(proposedLeft, GridUnitType.Absolute);
                    rightColumn.Width = new GridLength(proposedRight, GridUnitType.Absolute);
                }

                newLeft = proposedLeft;
                newRight = proposedRight;
                break;
        }

        ForceGridLayout();

        (double? Previous, double? Next) changedArgs = (newLeft, newRight);
        ResizeChanged?.Invoke(this, changedArgs);
        if (ResizeChangedCommand?.CanExecute(changedArgs) == true)
        {
            ResizeChangedCommand.Execute(changedArgs);
        }
    }

    private void ApplyColumnWidth(ColumnDefinition column, double width, int columnIndex, bool keepStarIfPossible)
    {
        if (parentGrid == null)
        {
            return;
        }

        if (!keepStarIfPossible)
        {
            column.Width = new GridLength(width, GridUnitType.Absolute);
            return;
        }

        if (column.Width.IsStar)
        {
            var oppositeIndex = parentGrid.ColumnDefinitions.IndexOf(column) == 0 ? 1 : parentGrid.ColumnDefinitions.IndexOf(column) - 1;
            if (oppositeIndex >= 0 && oppositeIndex < parentGrid.ColumnDefinitions.Count)
            {
                column.Width = new GridLength(width, GridUnitType.Star);
                return;
            }
        }

        column.Width = new GridLength(width, GridUnitType.Absolute);
    }

    private void ForceGridLayout()
    {
        if (parentGrid == null)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            parentGrid.InvalidateMeasure();

            foreach (var child in parentGrid.Children)
            {
                if (child is IView view)
                {
                    view.InvalidateMeasure();
                }
            }
        });
    }

    private double GetActualColumnWidth(ColumnDefinition column, int columnIndex)
    {
        if (column.Width.IsAbsolute)
        {
            return column.Width.Value;
        }

        if (parentGrid != null)
        {
            foreach (var child in parentGrid.Children)
            {
                if (child is BindableObject bindable)
                {
                    var childColumn = Grid.GetColumn(bindable);
                    if (childColumn == columnIndex && child is View viewElement && viewElement.Width > 0)
                    {
                        return viewElement.Width;
                    }
                }
            }
        }

        if (parentGrid != null && parentGrid.Width > 0)
        {
            if (column.Width.IsStar)
            {
                var totalStars = parentGrid.ColumnDefinitions.Sum(c => c.Width.IsStar ? c.Width.Value : 0);
                var availableWidth = parentGrid.Width;

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

        return 300;
    }

    private static double Clamp(double value, double min, double max)
    {
        return double.IsNaN(value) ? min : double.IsPositiveInfinity(max) ? Math.Max(min, value) : Math.Max(min, Math.Min(value, max));
    }
}

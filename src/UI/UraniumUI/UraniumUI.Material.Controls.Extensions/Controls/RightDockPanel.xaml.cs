using System.Windows.Input;

namespace UraniumUI.Material.Controls;

public partial class RightDockPanel : ContentView
{
    private double _savedExpandedWidth = 320;
    private double _panStartWidth;
    private bool _isLoaded;

    public RightDockPanel()
    {
        InitializeComponent();

        ToggleCommand = new Command(() =>
        {
            if (IsExpanded)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        });

        Loaded += RightDockPanel_Loaded;
        SizeChanged += RightDockPanel_SizeChanged;
    }

    private void RightDockPanel_Loaded(object? sender, EventArgs e)
    {
        _isLoaded = true;
        ApplyState(false);
    }

    private void RightDockPanel_SizeChanged(object? sender, EventArgs e)
    {
        if (_isLoaded)
        {
            CoerceDockWidth();
        }
    }

    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(nameof(MainContent), typeof(View), typeof(RightDockPanel), default(View));

    public View? MainContent
    {
        get => (View?)GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public static readonly BindableProperty DockContentProperty =
        BindableProperty.Create(nameof(DockContent), typeof(View), typeof(RightDockPanel), default(View), propertyChanged: OnLayoutPropertyChanged);

    public View? DockContent
    {
        get => (View?)GetValue(DockContentProperty);
        set => SetValue(DockContentProperty, value);
    }

    public static readonly BindableProperty HeaderContentProperty =
        BindableProperty.Create(nameof(HeaderContent), typeof(View), typeof(RightDockPanel), default(View));

    public View? HeaderContent
    {
        get => (View?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(RightDockPanel), "Panel");

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(RightDockPanel), true, BindingMode.TwoWay, propertyChanged: OnLayoutPropertyChanged);

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly BindableProperty DockWidthProperty =
        BindableProperty.Create(nameof(DockWidth), typeof(double), typeof(RightDockPanel), 320d, BindingMode.TwoWay, propertyChanged: OnLayoutPropertyChanged);

    public double DockWidth
    {
        get => (double)GetValue(DockWidthProperty);
        set => SetValue(DockWidthProperty, value);
    }

    public static readonly BindableProperty MinDockWidthProperty =
        BindableProperty.Create(nameof(MinDockWidth), typeof(double), typeof(RightDockPanel), 220d);

    public double MinDockWidth
    {
        get => (double)GetValue(MinDockWidthProperty);
        set => SetValue(MinDockWidthProperty, value);
    }

    public static readonly BindableProperty MaxDockWidthProperty =
        BindableProperty.Create(nameof(MaxDockWidth), typeof(double), typeof(RightDockPanel), 600d);

    public double MaxDockWidth
    {
        get => (double)GetValue(MaxDockWidthProperty);
        set => SetValue(MaxDockWidthProperty, value);
    }

    public static readonly BindableProperty AutoSizeToContentProperty =
        BindableProperty.Create(nameof(AutoSizeToContent), typeof(bool), typeof(RightDockPanel), false, propertyChanged: OnLayoutPropertyChanged);

    public bool AutoSizeToContent
    {
        get => (bool)GetValue(AutoSizeToContentProperty);
        set => SetValue(AutoSizeToContentProperty, value);
    }

    public static readonly BindableProperty ShowEdgeToggleWhenExpandedProperty =
        BindableProperty.Create(nameof(ShowEdgeToggleWhenExpanded), typeof(bool), typeof(RightDockPanel), false, propertyChanged: OnLayoutPropertyChanged);

    public bool ShowEdgeToggleWhenExpanded
    {
        get => (bool)GetValue(ShowEdgeToggleWhenExpandedProperty);
        set => SetValue(ShowEdgeToggleWhenExpandedProperty, value);
    }

    public static readonly BindableProperty SplitterColorProperty =
        BindableProperty.Create(nameof(SplitterColor), typeof(Color), typeof(RightDockPanel), Colors.LightGray);

    public Color SplitterColor
    {
        get => (Color)GetValue(SplitterColorProperty);
        set => SetValue(SplitterColorProperty, value);
    }

    public static readonly BindableProperty DockBackgroundProperty =
        BindableProperty.Create(nameof(DockBackground), typeof(Color), typeof(RightDockPanel), Colors.White);

    public Color DockBackground
    {
        get => (Color)GetValue(DockBackgroundProperty);
        set => SetValue(DockBackgroundProperty, value);
    }

    public static readonly BindableProperty DockBorderColorProperty =
        BindableProperty.Create(nameof(DockBorderColor), typeof(Color), typeof(RightDockPanel), Colors.Gray);

    public Color DockBorderColor
    {
        get => (Color)GetValue(DockBorderColorProperty);
        set => SetValue(DockBorderColorProperty, value);
    }

    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(nameof(HeaderBackground), typeof(Color), typeof(RightDockPanel), Color.FromArgb("#F3F3F3"));

    public Color HeaderBackground
    {
        get => (Color)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public static readonly BindableProperty ExpandGlyphProperty =
        BindableProperty.Create(nameof(ExpandGlyph), typeof(string), typeof(RightDockPanel), "◀");

    public string ExpandGlyph
    {
        get => (string)GetValue(ExpandGlyphProperty);
        set => SetValue(ExpandGlyphProperty, value);
    }

    public static readonly BindableProperty CollapseGlyphProperty =
        BindableProperty.Create(nameof(CollapseGlyph), typeof(string), typeof(RightDockPanel), "▶");

    public string CollapseGlyph
    {
        get => (string)GetValue(CollapseGlyphProperty);
        set => SetValue(CollapseGlyphProperty, value);
    }

    public static readonly BindableProperty ToggleCommandProperty =
        BindableProperty.Create(nameof(ToggleCommand), typeof(ICommand), typeof(RightDockPanel));

    public ICommand ToggleCommand
    {
        get => (ICommand)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RightDockPanel panel && panel._isLoaded)
        {
            panel.ApplyState(false);
        }
    }

    public void Expand()
    {
        IsExpanded = true;
        ApplyState(true);
    }

    public void Collapse()
    {
        if (DockColumn.Width.Value > 0)
        {
            _savedExpandedWidth = DockColumn.Width.Value;
        }

        IsExpanded = false;
        ApplyState(true);
    }

    private void ApplyState(bool animated)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (IsExpanded)
        {
            var width = ResolveExpandedWidth();
            width = ClampWidth(width);

            DockColumn.Width = new GridLength(width, GridUnitType.Absolute);
            SplitterColumn.Width = new GridLength(6, GridUnitType.Absolute);
            DockBorder.IsVisible = true;
            SplitterHost.IsVisible = true;
            EdgeToggleButton.IsVisible = ShowEdgeToggleWhenExpanded;
            EdgeToggleButton.Text = CollapseGlyph;
            _savedExpandedWidth = width;
        }
        else
        {
            DockColumn.Width = new GridLength(0, GridUnitType.Absolute);
            SplitterColumn.Width = new GridLength(0, GridUnitType.Absolute);
            DockBorder.IsVisible = false;
            SplitterHost.IsVisible = false;
            EdgeToggleButton.IsVisible = true;
            EdgeToggleButton.Text = ExpandGlyph;
        }
    }

    private double ResolveExpandedWidth()
    {
        if (!AutoSizeToContent)
        {
            return DockWidth > 0 ? DockWidth : _savedExpandedWidth;
        }

        if (DockContent is not VisualElement view)
        {
            return DockWidth > 0 ? DockWidth : _savedExpandedWidth;
        }

        var desiredWidth = view.DesiredSize.Width;
        if (desiredWidth > 0 && !double.IsNaN(desiredWidth) && !double.IsInfinity(desiredWidth))
        {
            return desiredWidth;
        }

        var availableHeight = Height > 0 ? Height : double.PositiveInfinity;
        var measuredWidth = view.Measure(double.PositiveInfinity, availableHeight).Width;

        return measuredWidth > 0 && !double.IsNaN(measuredWidth) && !double.IsInfinity(measuredWidth)
            ? measuredWidth
            : DockWidth > 0
                ? DockWidth
                : _savedExpandedWidth;
    }

    private void CoerceDockWidth()
    {
        if (!IsExpanded)
        {
            return;
        }

        var width = ClampWidth(DockColumn.Width.Value <= 0 ? ResolveExpandedWidth() : DockColumn.Width.Value);
        DockColumn.Width = new GridLength(width, GridUnitType.Absolute);
        _savedExpandedWidth = width;
    }

    private double ClampWidth(double width)
    {
        var maxAllowed = Math.Min(MaxDockWidth, Math.Max(MinDockWidth, Width - 100));
        return Math.Max(MinDockWidth, Math.Min(width, maxAllowed));
    }

    private void OnSplitterPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsExpanded)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartWidth = DockColumn.Width.Value;
                break;

            case GestureStatus.Running:
                var newWidth = _panStartWidth - e.TotalX;
                newWidth = ClampWidth(newWidth);
                DockColumn.Width = new GridLength(newWidth, GridUnitType.Absolute);
                DockWidth = newWidth;
                _savedExpandedWidth = newWidth;
                break;
        }
    }

    private void OnSplitterTapped(object? sender, TappedEventArgs e)
    {
        if (IsExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }
}

using System.Windows.Input;

namespace UraniumUI.Material.Controls;

public class RightDockPanel3 : ContentView
{
    private readonly Grid _root;
    private readonly Grid _mainHost;
    private readonly Grid _splitterHost;
    private readonly Border _dockBorder;
    private readonly Grid _headerGrid;
    private readonly ContentView _headerHost;
    private readonly ContentView _dockHost;
    private readonly Button _headerToggleButton;
    private readonly Button _edgeToggleButton;

    private readonly ColumnDefinition _mainColumn;
    private readonly ColumnDefinition _splitterColumn;
    private readonly ColumnDefinition _dockColumn;

    private double _savedExpandedWidth = 320;
    private double _panStartWidth;
    private bool _isBuilt;

    public RightDockPanel3()
    {
        _mainColumn = new ColumnDefinition { Width = GridLength.Star };
        _splitterColumn = new ColumnDefinition { Width = 6 };
        _dockColumn = new ColumnDefinition { Width = 320 };

        _root = new Grid { ColumnSpacing = 0, RowSpacing = 0, ColumnDefinitions = { _mainColumn, _splitterColumn, _dockColumn } };

        _mainHost = [];
        Grid.SetColumn(_mainHost, 0);

        _splitterHost = [];
        Grid.SetColumn(_splitterHost, 1);

        var splitterPan = new PanGestureRecognizer();
        splitterPan.PanUpdated += OnSplitterPanUpdated;
        _splitterHost.GestureRecognizers.Add(splitterPan);

        _headerToggleButton = new Button { WidthRequest = 32, HeightRequest = 32, Padding = 0 };
        _headerToggleButton.Clicked += OnToggleClicked;

        _headerHost = new ContentView();

        _headerGrid = new Grid { Padding = new Thickness(8, 6), ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };

        _headerGrid.Add(_headerToggleButton);
        Grid.SetColumn(_headerToggleButton, 0);

        var titleLabel = new Label { VerticalOptions = LayoutOptions.Center, FontAttributes = FontAttributes.Bold };
        titleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        _headerGrid.Add(titleLabel);
        Grid.SetColumn(titleLabel, 1);

        _headerGrid.Add(_headerHost);
        Grid.SetColumn(_headerHost, 2);

        _dockHost = new ContentView();

        var dockLayout = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star } } };
        dockLayout.Add(_headerGrid);
        dockLayout.Add(_dockHost);
        Grid.SetRow(_dockHost, 1);

        _dockBorder = new Border { Content = dockLayout, StrokeThickness = 1 };
        Grid.SetColumn(_dockBorder, 2);

        _edgeToggleButton = new Button
        {
            WidthRequest = 36,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 8, 8, 0),
            ZIndex = 100
        };
        _edgeToggleButton.Clicked += OnToggleClicked;
        Grid.SetColumnSpan(_edgeToggleButton, 3);

        _root.Children.Add(_mainHost);
        _root.Children.Add(_splitterHost);
        _root.Children.Add(_dockBorder);
        _root.Children.Add(_edgeToggleButton);

        Content = _root;

        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);

        SizeChanged += (_, _) =>
        {
            if (_isBuilt)
            {
                ApplyState();
            }
        };

        Loaded += (_, _) =>
        {
            _isBuilt = true;
            ApplyHostedContent();
            ApplyStyling();
            ApplyState();
        };
    }

    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(
            nameof(MainContent),
            typeof(View),
            typeof(RightDockPanel2),
            default(View),
            propertyChanged: OnHostedContentChanged);

    public View? MainContent
    {
        get => (View?)GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public static readonly BindableProperty DockContentProperty =
        BindableProperty.Create(
            nameof(DockContent),
            typeof(View),
            typeof(RightDockPanel2),
            default(View),
            propertyChanged: OnHostedContentChanged);

    public View? DockContent
    {
        get => (View?)GetValue(DockContentProperty);
        set => SetValue(DockContentProperty, value);
    }

    public static readonly BindableProperty HeaderContentProperty =
        BindableProperty.Create(
            nameof(HeaderContent),
            typeof(View),
            typeof(RightDockPanel2),
            default(View),
            propertyChanged: OnHostedContentChanged);

    public View? HeaderContent
    {
        get => (View?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(RightDockPanel2),
            "Panel");

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(
            nameof(IsExpanded),
            typeof(bool),
            typeof(RightDockPanel2),
            true,
            BindingMode.TwoWay,
            propertyChanged: OnLayoutStateChanged);

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly BindableProperty DockWidthProperty =
        BindableProperty.Create(
            nameof(DockWidth),
            typeof(double),
            typeof(RightDockPanel2),
            320d,
            BindingMode.TwoWay,
            propertyChanged: OnLayoutStateChanged);

    public double DockWidth
    {
        get => (double)GetValue(DockWidthProperty);
        set => SetValue(DockWidthProperty, value);
    }

    public static readonly BindableProperty MinDockWidthProperty =
        BindableProperty.Create(
            nameof(MinDockWidth),
            typeof(double),
            typeof(RightDockPanel2),
            220d,
            propertyChanged: OnLayoutStateChanged);

    public double MinDockWidth
    {
        get => (double)GetValue(MinDockWidthProperty);
        set => SetValue(MinDockWidthProperty, value);
    }

    public static readonly BindableProperty MaxDockWidthProperty =
        BindableProperty.Create(
            nameof(MaxDockWidth),
            typeof(double),
            typeof(RightDockPanel2),
            600d,
            propertyChanged: OnLayoutStateChanged);

    public double MaxDockWidth
    {
        get => (double)GetValue(MaxDockWidthProperty);
        set => SetValue(MaxDockWidthProperty, value);
    }

    public static readonly BindableProperty AutoSizeToContentProperty =
        BindableProperty.Create(
            nameof(AutoSizeToContent),
            typeof(bool),
            typeof(RightDockPanel2),
            false,
            propertyChanged: OnLayoutStateChanged);

    public bool AutoSizeToContent
    {
        get => (bool)GetValue(AutoSizeToContentProperty);
        set => SetValue(AutoSizeToContentProperty, value);
    }

    public static readonly BindableProperty ShowEdgeToggleWhenExpandedProperty =
        BindableProperty.Create(
            nameof(ShowEdgeToggleWhenExpanded),
            typeof(bool),
            typeof(RightDockPanel2),
            false,
            propertyChanged: OnLayoutStateChanged);

    public bool ShowEdgeToggleWhenExpanded
    {
        get => (bool)GetValue(ShowEdgeToggleWhenExpandedProperty);
        set => SetValue(ShowEdgeToggleWhenExpandedProperty, value);
    }

    public static readonly BindableProperty SplitterColorProperty =
        BindableProperty.Create(
            nameof(SplitterColor),
            typeof(Color),
            typeof(RightDockPanel2),
            Colors.LightGray,
            propertyChanged: OnStyleChanged);

    public Color SplitterColor
    {
        get => (Color)GetValue(SplitterColorProperty);
        set => SetValue(SplitterColorProperty, value);
    }

    public static readonly BindableProperty DockBackgroundProperty =
        BindableProperty.Create(
            nameof(DockBackground),
            typeof(Color),
            typeof(RightDockPanel2),
            Colors.White,
            propertyChanged: OnStyleChanged);

    public Color DockBackground
    {
        get => (Color)GetValue(DockBackgroundProperty);
        set => SetValue(DockBackgroundProperty, value);
    }

    public static readonly BindableProperty DockBorderColorProperty =
        BindableProperty.Create(
            nameof(DockBorderColor),
            typeof(Color),
            typeof(RightDockPanel2),
            Colors.Gray,
            propertyChanged: OnStyleChanged);

    public Color DockBorderColor
    {
        get => (Color)GetValue(DockBorderColorProperty);
        set => SetValue(DockBorderColorProperty, value);
    }

    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(
            nameof(HeaderBackground),
            typeof(Color),
            typeof(RightDockPanel2),
            Color.FromArgb("#F3F3F3"),
            propertyChanged: OnStyleChanged);

    public Color HeaderBackground
    {
        get => (Color)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public static readonly BindableProperty ExpandGlyphProperty =
        BindableProperty.Create(
            nameof(ExpandGlyph),
            typeof(string),
            typeof(RightDockPanel2),
            "◀",
            propertyChanged: OnStyleChanged);

    public string ExpandGlyph
    {
        get => (string)GetValue(ExpandGlyphProperty);
        set => SetValue(ExpandGlyphProperty, value);
    }

    public static readonly BindableProperty CollapseGlyphProperty =
        BindableProperty.Create(
            nameof(CollapseGlyph),
            typeof(string),
            typeof(RightDockPanel2),
            "▶",
            propertyChanged: OnStyleChanged);

    public string CollapseGlyph
    {
        get => (string)GetValue(CollapseGlyphProperty);
        set => SetValue(CollapseGlyphProperty, value);
    }

    public static readonly BindableProperty ToggleCommandProperty =
        BindableProperty.Create(
            nameof(ToggleCommand),
            typeof(ICommand),
            typeof(RightDockPanel2),
            default(ICommand));

    public ICommand? ToggleCommand
    {
        get => (ICommand?)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        PropagateBindingContext(MainContent);
        PropagateBindingContext(DockContent);
        PropagateBindingContext(HeaderContent);
    }

    private static void OnHostedContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel3)bindable;

        if (newValue is View newView)
        {
            panel.PropagateBindingContext(newView);
        }

        if (panel._isBuilt)
        {
            panel.ApplyHostedContent();
        }
    }

    private static void OnLayoutStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel3)bindable;
        if (panel._isBuilt)
        {
            panel.ApplyState();
        }
    }

    private static void OnStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel3)bindable;
        if (panel._isBuilt)
        {
            panel.ApplyStyling();
        }
    }

    private void PropagateBindingContext(View? view)
    {
        if (view == null)
        {
            return;
        }

        view.BindingContext = BindingContext;
    }

    private void ApplyHostedContent()
    {
        _mainHost.Children.Clear();

        if (MainContent != null)
        {
            _mainHost.Children.Add(MainContent);
        }

        _dockHost.Content = DockContent;
        _headerHost.Content = HeaderContent;
    }

    private void ApplyStyling()
    {
        _splitterHost.BackgroundColor = SplitterColor;
        _dockBorder.BackgroundColor = DockBackground;
        _dockBorder.Stroke = DockBorderColor;
        _headerGrid.BackgroundColor = HeaderBackground;

        _headerToggleButton.Text = CollapseGlyph;
        _edgeToggleButton.Text = ExpandGlyph;
    }

    private void ApplyState()
    {
        if (!_isBuilt)
        {
            return;
        }

        if (IsExpanded)
        {
            var width = ResolveExpandedWidth();
            width = ClampWidth(width);

            _dockColumn.Width = new GridLength(width, GridUnitType.Absolute);
            _splitterColumn.Width = new GridLength(6, GridUnitType.Absolute);

            _dockBorder.IsVisible = true;
            _splitterHost.IsVisible = true;
            _edgeToggleButton.IsVisible = ShowEdgeToggleWhenExpanded;
            _edgeToggleButton.Text = CollapseGlyph;
            _headerToggleButton.Text = CollapseGlyph;

            _savedExpandedWidth = width;
        }
        else
        {
            if (_dockColumn.Width.Value > 0)
            {
                _savedExpandedWidth = _dockColumn.Width.Value;
            }

            _dockColumn.Width = new GridLength(0, GridUnitType.Absolute);
            _splitterColumn.Width = new GridLength(0, GridUnitType.Absolute);

            _dockBorder.IsVisible = false;
            _splitterHost.IsVisible = false;
            _edgeToggleButton.IsVisible = true;
            _edgeToggleButton.Text = ExpandGlyph;
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

    private double ClampWidth(double width)
    {
        var available = Math.Max(0, Width - 60);
        var upper = Math.Min(MaxDockWidth, available);

        if (upper < MinDockWidth)
        {
            upper = MinDockWidth;
        }

        return Math.Max(MinDockWidth, Math.Min(width, upper));
    }

    private void OnToggleClicked(object? sender, EventArgs e)
    {
        if (ToggleCommand?.CanExecute(null) == true)
        {
            ToggleCommand.Execute(null);
        }
        else
        {
            IsExpanded = !IsExpanded;
        }
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
                _panStartWidth = _dockColumn.Width.Value;
                break;

            case GestureStatus.Running:
                var newWidth = _panStartWidth - e.TotalX;
                newWidth = ClampWidth(newWidth);

                _dockColumn.Width = new GridLength(newWidth, GridUnitType.Absolute);
                DockWidth = newWidth;
                _savedExpandedWidth = newWidth;
                break;
        }
    }
}

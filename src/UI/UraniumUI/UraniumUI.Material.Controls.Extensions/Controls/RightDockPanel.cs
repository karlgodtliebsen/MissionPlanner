using System.Windows.Input;

namespace UraniumUI.Material.Controls;

/// <summary>
/// A panel that docks content to the right side with a collapsible feature.
/// </summary>
public class RightDockPanel : ContentView
{
    private readonly Grid root;
    private readonly Grid mainHost;
    private readonly Grid splitterHost;
    private readonly Border dockBorder;
    private readonly Grid headerGrid;
    private readonly ContentView headerHost;
    private readonly ContentView dockHost;
    private readonly Button headerToggleButton;
    private readonly Button edgeToggleButton;

    private readonly ColumnDefinition mainColumn;
    private readonly ColumnDefinition splitterColumn;
    private readonly ColumnDefinition dockColumn;

    private double savedExpandedWidth = 320;
    private double panStartWidth;
    private bool isBuilt;

    /// <summary>
    /// Initializes a new instance of the <see cref="RightDockPanel"/> class.
    /// </summary>
    public RightDockPanel()
    {
        mainColumn = new ColumnDefinition { Width = GridLength.Star };
        splitterColumn = new ColumnDefinition { Width = 6 };
        dockColumn = new ColumnDefinition { Width = 320 };

        root = new Grid { ColumnSpacing = 0, RowSpacing = 0, ColumnDefinitions = { mainColumn, splitterColumn, dockColumn } };

        mainHost = [];
        Grid.SetColumn(mainHost, 0);

        splitterHost = [];
        Grid.SetColumn(splitterHost, 1);

        var splitterPan = new PanGestureRecognizer();
        splitterPan.PanUpdated += OnSplitterPanUpdated;
        splitterHost.GestureRecognizers.Add(splitterPan);

        headerToggleButton = new Button { WidthRequest = 32, HeightRequest = 32, Padding = 0 };
        headerToggleButton.Clicked += OnToggleClicked;

        headerHost = new ContentView();

        headerGrid = new Grid { Padding = new Thickness(8, 6), ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };

        headerGrid.Add(headerToggleButton);
        Grid.SetColumn(headerToggleButton, 0);

        var titleLabel = new Label { VerticalOptions = LayoutOptions.Center, FontAttributes = FontAttributes.Bold, Margin = new Thickness(5, 0, 0, 0) };
        titleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        headerGrid.Add(titleLabel);
        Grid.SetColumn(titleLabel, 1);

        headerGrid.Add(headerHost);
        Grid.SetColumn(headerHost, 2);

        dockHost = new ContentView();

        var dockLayout = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star } } };
        dockLayout.Add(headerGrid);
        dockLayout.Add(dockHost);
        Grid.SetRow(dockHost, 1);

        dockBorder = new Border { Content = dockLayout, StrokeThickness = 1 };
        Grid.SetColumn(dockBorder, 2);

        edgeToggleButton = new Button
        {
            WidthRequest = 36,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 8, 8, 0),
            ZIndex = 100
        };
        edgeToggleButton.Clicked += OnToggleClicked;
        Grid.SetColumnSpan(edgeToggleButton, 3);

        root.Children.Add(mainHost);
        root.Children.Add(splitterHost);
        root.Children.Add(dockBorder);
        root.Children.Add(edgeToggleButton);

        Content = root;

        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);

        SizeChanged += (_, _) =>
        {
            if (isBuilt)
            {
                ApplyState();
            }
        };

        Loaded += (_, _) =>
        {
            isBuilt = true;
            ApplyHostedContent();
            ApplyStyling();
            ApplyState();
        };
    }

    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(
            nameof(MainContent),
            typeof(View),
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
            typeof(RightDockPanel),
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
        var panel = (RightDockPanel)bindable;

        if (newValue is View newView)
        {
            panel.PropagateBindingContext(newView);
        }

        if (panel.isBuilt)
        {
            panel.ApplyHostedContent();
        }
    }

    private static void OnLayoutStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel)bindable;
        if (panel.isBuilt)
        {
            panel.ApplyState();
        }
    }

    private static void OnStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel)bindable;
        if (panel.isBuilt)
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
        mainHost.Children.Clear();

        if (MainContent != null)
        {
            mainHost.Children.Add(MainContent);
        }

        dockHost.Content = DockContent;
        headerHost.Content = HeaderContent;
    }

    private void ApplyStyling()
    {
        splitterHost.BackgroundColor = SplitterColor;
        dockBorder.BackgroundColor = DockBackground;
        dockBorder.Stroke = DockBorderColor;
        headerGrid.BackgroundColor = HeaderBackground;

        headerToggleButton.Text = CollapseGlyph;
        edgeToggleButton.Text = ExpandGlyph;
    }

    private void ApplyState()
    {
        if (!isBuilt)
        {
            return;
        }

        if (IsExpanded)
        {
            var width = ResolveExpandedWidth();
            width = ClampWidth(width);

            dockColumn.Width = new GridLength(width, GridUnitType.Absolute);
            splitterColumn.Width = new GridLength(6, GridUnitType.Absolute);

            dockBorder.IsVisible = true;
            splitterHost.IsVisible = true;
            edgeToggleButton.IsVisible = ShowEdgeToggleWhenExpanded;
            edgeToggleButton.Text = CollapseGlyph;
            headerToggleButton.Text = CollapseGlyph;

            savedExpandedWidth = width;
        }
        else
        {
            if (dockColumn.Width.Value > 0)
            {
                savedExpandedWidth = dockColumn.Width.Value;
            }

            dockColumn.Width = new GridLength(0, GridUnitType.Absolute);
            splitterColumn.Width = new GridLength(0, GridUnitType.Absolute);

            dockBorder.IsVisible = false;
            splitterHost.IsVisible = false;
            edgeToggleButton.IsVisible = true;
            edgeToggleButton.Text = ExpandGlyph;
        }
    }

    private double ResolveExpandedWidth()
    {
        if (!AutoSizeToContent)
        {
            return DockWidth > 0 ? DockWidth : savedExpandedWidth;
        }

        if (DockContent is not VisualElement view)
        {
            return DockWidth > 0 ? DockWidth : savedExpandedWidth;
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
                : savedExpandedWidth;
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
                panStartWidth = dockColumn.Width.Value;
                break;

            case GestureStatus.Running:
                var newWidth = panStartWidth - e.TotalX;
                newWidth = ClampWidth(newWidth);

                dockColumn.Width = new GridLength(newWidth, GridUnitType.Absolute);
                DockWidth = newWidth;
                savedExpandedWidth = newWidth;
                break;
        }
    }
}

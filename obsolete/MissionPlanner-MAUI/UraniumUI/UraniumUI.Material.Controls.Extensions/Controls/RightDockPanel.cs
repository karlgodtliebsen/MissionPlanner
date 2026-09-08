using System.Windows.Input;
using UraniumUI.Icons.MaterialSymbols;

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

        edgeToggleButton = new Button
        {
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 4, 0),
            ZIndex = 100
        };
        edgeToggleButton.Clicked += OnToggleClicked;
        Grid.SetColumnSpan(edgeToggleButton, 3);


        headerHost = new ContentView();
        headerGrid = new Grid { Padding = new Thickness(8, 6), ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };

        headerGrid.Add(headerToggleButton);
        Grid.SetColumn(headerToggleButton, 0);

        var titleLabel = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold,
            Margin =
                new Thickness(5, 0, 0, 0)
        };
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

    /// <summary>Identifies the <see cref="MainContent"/> bindable property.</summary>
    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(
            nameof(MainContent),
            typeof(View),
            typeof(RightDockPanel),
            default(View),
            propertyChanged: OnHostedContentChanged);

    /// <summary>Gets or sets the primary content displayed beside the dock.</summary>
    public View? MainContent
    {
        get => (View?)GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    /// <summary>Identifies the <see cref="DockContent"/> bindable property.</summary>
    public static readonly BindableProperty DockContentProperty =
        BindableProperty.Create(
            nameof(DockContent),
            typeof(View),
            typeof(RightDockPanel),
            default(View),
            propertyChanged: OnHostedContentChanged);

    /// <summary>Gets or sets the content displayed inside the right dock.</summary>
    public View? DockContent
    {
        get => (View?)GetValue(DockContentProperty);
        set => SetValue(DockContentProperty, value);
    }

    /// <summary>Identifies the <see cref="HeaderContent"/> bindable property.</summary>
    public static readonly BindableProperty HeaderContentProperty =
        BindableProperty.Create(
            nameof(HeaderContent),
            typeof(View),
            typeof(RightDockPanel),
            default(View),
            propertyChanged: OnHostedContentChanged);

    /// <summary>Gets or sets custom content displayed in the dock header.</summary>
    public View? HeaderContent
    {
        get => (View?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Identifies the <see cref="Title"/> bindable property.</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(RightDockPanel),
            "Panel");

    /// <summary>Gets or sets the dock header title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Identifies the <see cref="IsExpanded"/> bindable property.</summary>
    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(
            nameof(IsExpanded),
            typeof(bool),
            typeof(RightDockPanel),
            true,
            BindingMode.TwoWay,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets whether the right dock is expanded.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Identifies the <see cref="DockWidth"/> bindable property.</summary>
    public static readonly BindableProperty DockWidthProperty =
        BindableProperty.Create(
            nameof(DockWidth),
            typeof(double),
            typeof(RightDockPanel),
            320d,
            BindingMode.TwoWay,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets the expanded dock width.</summary>
    public double DockWidth
    {
        get => (double)GetValue(DockWidthProperty);
        set => SetValue(DockWidthProperty, value);
    }

    /// <summary>Identifies the <see cref="MinDockWidth"/> bindable property.</summary>
    public static readonly BindableProperty MinDockWidthProperty =
        BindableProperty.Create(
            nameof(MinDockWidth),
            typeof(double),
            typeof(RightDockPanel),
            220d,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets the minimum dock width during resizing.</summary>
    public double MinDockWidth
    {
        get => (double)GetValue(MinDockWidthProperty);
        set => SetValue(MinDockWidthProperty, value);
    }

    /// <summary>Identifies the <see cref="MaxDockWidth"/> bindable property.</summary>
    public static readonly BindableProperty MaxDockWidthProperty =
        BindableProperty.Create(
            nameof(MaxDockWidth),
            typeof(double),
            typeof(RightDockPanel),
            600d,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets the maximum dock width during resizing.</summary>
    public double MaxDockWidth
    {
        get => (double)GetValue(MaxDockWidthProperty);
        set => SetValue(MaxDockWidthProperty, value);
    }

    /// <summary>Identifies the <see cref="AutoSizeToContent"/> bindable property.</summary>
    public static readonly BindableProperty AutoSizeToContentProperty =
        BindableProperty.Create(
            nameof(AutoSizeToContent),
            typeof(bool),
            typeof(RightDockPanel),
            false,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets whether the expanded dock sizes itself to its content.</summary>
    public bool AutoSizeToContent
    {
        get => (bool)GetValue(AutoSizeToContentProperty);
        set => SetValue(AutoSizeToContentProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowEdgeToggleWhenExpanded"/> bindable property.</summary>
    public static readonly BindableProperty ShowEdgeToggleWhenExpandedProperty =
        BindableProperty.Create(
            nameof(ShowEdgeToggleWhenExpanded),
            typeof(bool),
            typeof(RightDockPanel),
            false,
            propertyChanged: OnLayoutStateChanged);

    /// <summary>Gets or sets whether the edge toggle remains visible while expanded.</summary>
    public bool ShowEdgeToggleWhenExpanded
    {
        get => (bool)GetValue(ShowEdgeToggleWhenExpandedProperty);
        set => SetValue(ShowEdgeToggleWhenExpandedProperty, value);
    }

    /// <summary>Identifies the <see cref="SplitterColor"/> bindable property.</summary>
    public static readonly BindableProperty SplitterColorProperty =
        BindableProperty.Create(
            nameof(SplitterColor),
            typeof(Color),
            typeof(RightDockPanel),
            Colors.LightGray,
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the dock splitter color.</summary>
    public Color SplitterColor
    {
        get => (Color)GetValue(SplitterColorProperty);
        set => SetValue(SplitterColorProperty, value);
    }

    /// <summary>Identifies the <see cref="DockBackground"/> bindable property.</summary>
    public static readonly BindableProperty DockBackgroundProperty =
        BindableProperty.Create(
            nameof(DockBackground),
            typeof(Color),
            typeof(RightDockPanel),
            Colors.White,
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the dock content background color.</summary>
    public Color DockBackground
    {
        get => (Color)GetValue(DockBackgroundProperty);
        set => SetValue(DockBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="DockBorderColor"/> bindable property.</summary>
    public static readonly BindableProperty DockBorderColorProperty =
        BindableProperty.Create(
            nameof(DockBorderColor),
            typeof(Color),
            typeof(RightDockPanel),
            Colors.Gray,
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the dock border color.</summary>
    public Color DockBorderColor
    {
        get => (Color)GetValue(DockBorderColorProperty);
        set => SetValue(DockBorderColorProperty, value);
    }

    /// <summary>Identifies the <see cref="HeaderBackground"/> bindable property.</summary>
    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(
            nameof(HeaderBackground),
            typeof(Color),
            typeof(RightDockPanel),
            Color.FromArgb("#F3F3F3"),
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the dock header background color.</summary>
    public Color HeaderBackground
    {
        get => (Color)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="ExpandGlyph"/> bindable property.</summary>
    public static readonly BindableProperty ExpandGlyphProperty =
        BindableProperty.Create(
            nameof(ExpandGlyph),
            typeof(string),
            typeof(RightDockPanel),
            "◀",
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the glyph shown for the expand action.</summary>
    public string ExpandGlyph
    {
        get => (string)GetValue(ExpandGlyphProperty);
        set => SetValue(ExpandGlyphProperty, value);
    }

    /// <summary>Identifies the <see cref="CollapseGlyph"/> bindable property.</summary>
    public static readonly BindableProperty CollapseGlyphProperty =
        BindableProperty.Create(
            nameof(CollapseGlyph),
            typeof(string),
            typeof(RightDockPanel),
            "▶",
            propertyChanged: OnStyleChanged);

    /// <summary>Gets or sets the glyph shown for the collapse action.</summary>
    public string CollapseGlyph
    {
        get => (string)GetValue(CollapseGlyphProperty);
        set => SetValue(CollapseGlyphProperty, value);
    }

    /// <summary>Identifies the <see cref="ToggleCommand"/> bindable property.</summary>
    public static readonly BindableProperty ToggleCommandProperty =
        BindableProperty.Create(
            nameof(ToggleCommand),
            typeof(ICommand),
            typeof(RightDockPanel),
            default(ICommand));

    /// <summary>Gets or sets the command that toggles the dock state.</summary>
    public ICommand? ToggleCommand
    {
        get => (ICommand?)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    /// <inheritdoc />
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
        if (MainContent is null)
        {
            if (mainHost.Children.Count != 0)
            {
                mainHost.Children.Clear();
            }
        }
        else if (mainHost.Children.Count != 1 || !ReferenceEquals(mainHost.Children[0], MainContent))
        {
            mainHost.Children.Clear();
            mainHost.Children.Add(MainContent);
        }

        if (!ReferenceEquals(dockHost.Content, DockContent))
        {
            dockHost.Content = DockContent;
        }

        if (!ReferenceEquals(headerHost.Content, HeaderContent))
        {
            headerHost.Content = HeaderContent;
        }
    }

    private void ApplyStyling()
    {
        splitterHost.BackgroundColor = SplitterColor;
        dockBorder.BackgroundColor = DockBackground;
        dockBorder.Stroke = DockBorderColor;
        headerGrid.BackgroundColor = HeaderBackground;

        //ImageSource="{FontImageSource Glyph={x:Static uranium:MaterialSharp.Arrow_circle_left}, FontFamily=MaterialSharp}"
        //ImageSource="{FontImageSource Glyph={x:Static uranium:MaterialSharp.Arrow_circle_right},
        //FontFamily=MaterialSharp}"
        headerToggleButton.ImageSource = new FontImageSource { Glyph = MaterialSharp.Arrow_circle_right, FontFamily = "MaterialSharp" };
        edgeToggleButton.ImageSource = new FontImageSource { Glyph = MaterialSharp.Arrow_circle_left, FontFamily = "MaterialSharp" };

        //headerToggleButton.Text = CollapseGlyph;
        //edgeToggleButton.Text = ExpandGlyph;
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

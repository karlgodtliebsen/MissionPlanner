namespace UraniumUI.Material.Controls;

/// <summary>
/// A panel that docks content to the right side and can be expanded or collapsed.
/// </summary>
public partial class RightDockPanel2 : ContentView
{
    private double savedExpandedWidth = 320;
    private double panStartWidth;
    private bool isLoaded;

    public RightDockPanel2()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            isLoaded = true;
            ApplyHostedContent();
            ApplyState();
        };
    }

    public static readonly BindableProperty MainContentProperty =
        BindableProperty.Create(
            nameof(MainContent),
            typeof(View),
            typeof(RightDockPanel2),
            propertyChanged: OnHostedViewChanged);

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
            propertyChanged: OnHostedViewChanged);

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
            propertyChanged: OnHostedViewChanged);

    public View? HeaderContent
    {
        get => (View?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(RightDockPanel2), "Panel");

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
            propertyChanged: OnStateChanged);

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
            propertyChanged: OnStateChanged);

    public double DockWidth
    {
        get => (double)GetValue(DockWidthProperty);
        set => SetValue(DockWidthProperty, value);
    }

    public static readonly BindableProperty MinDockWidthProperty =
        BindableProperty.Create(nameof(MinDockWidth), typeof(double), typeof(RightDockPanel2), 220d);

    public double MinDockWidth
    {
        get => (double)GetValue(MinDockWidthProperty);
        set => SetValue(MinDockWidthProperty, value);
    }

    public static readonly BindableProperty MaxDockWidthProperty =
        BindableProperty.Create(nameof(MaxDockWidth), typeof(double), typeof(RightDockPanel2), 600d);

    public double MaxDockWidth
    {
        get => (double)GetValue(MaxDockWidthProperty);
        set => SetValue(MaxDockWidthProperty, value);
    }

    public static readonly BindableProperty SplitterColorProperty =
        BindableProperty.Create(nameof(SplitterColor), typeof(Color), typeof(RightDockPanel2), Colors.LightGray);

    public Color SplitterColor
    {
        get => (Color)GetValue(SplitterColorProperty);
        set => SetValue(SplitterColorProperty, value);
    }

    public static readonly BindableProperty DockBackgroundProperty =
        BindableProperty.Create(nameof(DockBackground), typeof(Color), typeof(RightDockPanel2), Colors.White);

    public Color DockBackground
    {
        get => (Color)GetValue(DockBackgroundProperty);
        set => SetValue(DockBackgroundProperty, value);
    }

    public static readonly BindableProperty DockBorderColorProperty =
        BindableProperty.Create(nameof(DockBorderColor), typeof(Color), typeof(RightDockPanel2), Colors.Gray);

    public Color DockBorderColor
    {
        get => (Color)GetValue(DockBorderColorProperty);
        set => SetValue(DockBorderColorProperty, value);
    }

    public static readonly BindableProperty HeaderBackgroundProperty =
        BindableProperty.Create(nameof(HeaderBackground), typeof(Color), typeof(RightDockPanel2), Color.FromArgb("#F3F3F3"));

    public Color HeaderBackground
    {
        get => (Color)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public static readonly BindableProperty ExpandGlyphProperty =
        BindableProperty.Create(nameof(ExpandGlyph), typeof(string), typeof(RightDockPanel2), "◀");

    public string ExpandGlyph
    {
        get => (string)GetValue(ExpandGlyphProperty);
        set => SetValue(ExpandGlyphProperty, value);
    }

    public static readonly BindableProperty CollapseGlyphProperty =
        BindableProperty.Create(nameof(CollapseGlyph), typeof(string), typeof(RightDockPanel2), "▶");

    public string CollapseGlyph
    {
        get => (string)GetValue(CollapseGlyphProperty);
        set => SetValue(CollapseGlyphProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        PropagateBindingContext(MainContent);
        PropagateBindingContext(DockContent);
        PropagateBindingContext(HeaderContent);
    }

    private static void OnHostedViewChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel2)bindable;
        if (oldValue is View oldView)
        {
            oldView.RemoveBinding(BindingContextProperty);
        }

        if (newValue is View newView)
        {
            panel.PropagateBindingContext(newView);
        }

        if (panel.isLoaded)
        {
            panel.ApplyHostedContent();
        }
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var panel = (RightDockPanel2)bindable;
        if (panel.isLoaded)
        {
            panel.ApplyState();
        }
    }

    private void PropagateBindingContext(View? view)
    {
        if (view == null)
        {
            return;
        }

        view.SetBinding(BindingContextProperty, new Binding(nameof(BindingContext), source: this));
    }

    private void ApplyHostedContent()
    {
        MainHost.Children.Clear();
        if (MainContent != null)
        {
            MainHost.Children.Add(MainContent);
        }

        DockHost.Content = DockContent;
        HeaderHost.Content = HeaderContent;
    }

    private void ApplyState()
    {
        if (IsExpanded)
        {
            var width = ClampWidth(DockWidth > 0 ? DockWidth : savedExpandedWidth);
            DockColumn.Width = new GridLength(width, GridUnitType.Absolute);
            SplitterColumn.Width = new GridLength(6, GridUnitType.Absolute);
            DockBorder.IsVisible = true;
            SplitterHost.IsVisible = true;
            EdgeToggleButton.IsVisible = false;
            savedExpandedWidth = width;
        }
        else
        {
            DockColumn.Width = new GridLength(0, GridUnitType.Absolute);
            SplitterColumn.Width = new GridLength(0, GridUnitType.Absolute);
            DockBorder.IsVisible = false;
            SplitterHost.IsVisible = false;
            EdgeToggleButton.IsVisible = true;
        }
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
        IsExpanded = !IsExpanded;
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
                panStartWidth = DockColumn.Width.Value;
                break;

            case GestureStatus.Running:
                var newWidth = ClampWidth(panStartWidth - e.TotalX);
                DockColumn.Width = new GridLength(newWidth, GridUnitType.Absolute);
                DockWidth = newWidth;
                savedExpandedWidth = newWidth;
                break;
        }
    }
}

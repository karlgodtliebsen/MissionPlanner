using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Interaction logic for FlightDataPage.xaml
/// </summary>
public partial class FlightDataPage : NavigationViewBase<FlightDataViewModel>
{
    private readonly FlightDataPanelWindow hudWindow;
    private readonly FlightDataPanelWindow tabsWindow;
    private GridLength sidebarWidth = new(400);
    private GridLength hudHeight = new(400);
    private GridLength tabsHeight = GridLength.Star;
    private bool bothDocked = true;
    private bool sidebarVisible = true;

    private bool CanPopOut => !OperatingSystem.IsBrowser() &&
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataPage"/> class.
    /// </summary>
    public FlightDataPage()
    {
        InitializeComponent();
        hudWindow = new FlightDataPanelWindow(HudHost, "Flight Data — HUD", new Size(600, 550), UpdatePanelLayout);
        tabsWindow = new FlightDataPanelWindow(TabsHost, "Flight Data — Tabs", new Size(650, 800), UpdatePanelLayout);
    }

    private void PopOutHud(object? sender, RoutedEventArgs e)
    {
        if (CanPopOut && TopLevel.GetTopLevel(this) is Window owner) hudWindow.Show(owner);
    }

    private void PopOutTabs(object? sender, RoutedEventArgs e)
    {
        if (CanPopOut && TopLevel.GetTopLevel(this) is Window owner) tabsWindow.Show(owner);
    }

    private void UpdatePanelLayout()
    {
        var hudDocked = !hudWindow.IsOpen;
        var tabsDocked = !tabsWindow.IsOpen;
        var both = hudDocked && tabsDocked;
        var any = hudDocked || tabsDocked;
        if (bothDocked && !both)
        {
            hudHeight = LeftPanels.RowDefinitions[0].Height;
            tabsHeight = LeftPanels.RowDefinitions[2].Height;
        }
        if (sidebarVisible && !any) sidebarWidth = FlightLayout.ColumnDefinitions[0].Width;
        if (sidebarVisible != any)
        {
            FlightLayout.ColumnDefinitions[0].Width = any ? sidebarWidth : new GridLength(0);
            FlightLayout.ColumnDefinitions[1].Width = new GridLength(any ? 5 : 0);
        }
        LeftPanels.RowDefinitions[0].Height = hudDocked ? (both ? hudHeight : GridLength.Star) : new GridLength(0);
        LeftPanels.RowDefinitions[1].Height = new GridLength(both ? 5 : 0);
        LeftPanels.RowDefinitions[2].Height = tabsDocked ? (both ? tabsHeight : GridLength.Star) : new GridLength(0);
        LeftPanels.RowSpacing = both ? 2 : 0;
        HudHost.IsVisible = hudDocked;
        TabsHost.IsVisible = tabsDocked;
        LeftPanels.IsVisible = any;
        PanelSplitter.IsVisible = both;
        SidebarSplitter.IsVisible = any;
        PopOutHudButton.IsVisible = CanPopOut && hudDocked;
        PopOutTabsButton.IsVisible = CanPopOut && tabsDocked;
        bothDocked = both;
        sidebarVisible = any;
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdatePanelLayout();
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            return;
        }
        MapLoadingIndicator.IsVisible = true;
        ViewModel.Map.ActivateAsync().SafeFireAndForget();
        MapView.ActivateAsync(ViewModel.Map).SafeFireAndForget();
        MapLoadingIndicator.IsVisible = false;

    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        // Restore the sections when leaving Flight Data instead of leaving orphan windows.
        hudWindow.Close();
        tabsWindow.Close();
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            base.OnUnloaded(e);
            return;
        }
        MapView.DeactivateAsync().SafeFireAndForget();
        ViewModel.Map.Deactivate();
        base.OnUnloaded(e);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks menu dismissal and constrained firmware scrolling with production markup.</summary>
[Collection("Document rendering")]
public sealed class DiagnosticMenuAndFirmwareLayoutTests
{
    /// <summary>Creates the existing offline firmware fixture with production themes.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(FirmwarePanelViewModelTests.CreateServices()))
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>All four diagnostic menu items close their flyout on keyboard activation.</summary>
    [Fact]
    public async Task SelectingDiagnosticMenuItemClosesFlyout()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DiagnosticMenuAndFirmwareLayoutTests));
        try
        {
            await session.Dispatch(() =>
            {
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                TopBarView topbar;
                try
                {
                    designMode.SetValue(null, true);
                    topbar = new TopBarView();
                }
                finally
                {
                    designMode.SetValue(null, previous);
                }
                var dropdown = topbar.FindControl<DropDownButton>("CompactDiagnostics")!;
                var menu = Assert.IsType<MenuFlyout>(dropdown.Flyout);
                var items = menu.Items.Cast<MenuItem>().ToArray();
                Assert.Equal(4, items.Length);
                var anchor = new Button { Content = "Diagnostics", Flyout = menu };
                var window = new Window { Content = anchor, Width = 800, Height = 450 };
                window.Show();
                foreach (var item in items)
                {
                    var selected = false;
                    item.Command = new RelayCommand(() => selected = true);
                    menu.ShowAt(anchor);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(menu.IsOpen);
                    item.Focus();
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(selected);
                    Assert.False(menu.IsOpen);
                }
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    /// <summary>Configuration content stays inside the viewport and can scroll to its bottom in short windows.</summary>
    [Fact]
    public async Task FirmwareConfigurationScrollsInShortWindow()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DiagnosticMenuAndFirmwareLayoutTests));
        try
        {
            await session.Dispatch(() =>
            {
                var page = new InstallFirmwarePage();
                var window = new Window { Content = page, Width = 850, Height = 450 };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                page.GetVisualDescendants().OfType<TabControl>().First().SelectedIndex = 1;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var scroll = page.FindControl<ScrollViewer>("ConfigurationScroll")!;
                Assert.InRange(scroll.Viewport.Height, 1, 449);
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                scroll.ScrollToEnd();
                Dispatcher.UIThread.RunJobs();
                Assert.True(scroll.Offset.Y > 0);
                var origin = scroll.TranslatePoint(default, window)!.Value;
                Assert.True(origin.Y + scroll.Bounds.Height <= window.ClientSize.Height + 1);
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

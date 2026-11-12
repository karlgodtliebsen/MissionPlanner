using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class FlightDataPanelWindowTests(FlightDataPanelWindowTests.UiFixture ui) : IClassFixture<FlightDataPanelWindowTests.UiFixture>
{
    public static AppBuilder BuildAvaloniaApp() => BuildApplication(desktop: true);

    private static AppBuilder BuildApplication(bool desktop)
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<IUiDispatcher>(_ => new AvaloniaUiDispatcher(Dispatcher.UIThread))
            .AddSingleton(Substitute.For<IDomainEventHub>())
            .AddTransient<LifecycleModel>()
            .BuildServiceProvider();
        return AppBuilder.Configure(() => new TestApp(services, desktop)).UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public Task MovingTabsPreservesControlsAndActivatesEachModelOnce() => RunAsync(() =>
    {
        var tab = new TabItemViewBase<LifecycleModel>();
        var model = (LifecycleModel)tab.DataContext!;
        var text = new TextBox { Text = "Keep this edit" };
        tab.Content = text;
        var tabs = new TabControl
        {
            Items = { new TabItem { Header = "Quick" }, new TabItem { Header = "Actions", Content = tab } },
            SelectedIndex = 1
        };
        var context = new object();
        var dock = new ContentControl { Content = tabs, DataContext = context };
        var owner = new UrsaWindow { Content = dock, Width = 700, Height = 800 };
        var panel = new FlightDataPanelWindow(dock, "Test Flight Data tabs", new Size(650, 800), () => { });
        try
        {
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, model.Activations);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                panel.Show(owner);
                Dispatcher.UIThread.RunJobs();
                var popup = Assert.Single(owner.OwnedWindows);
                Assert.Null(dock.Content);
                Assert.Same(popup, TopLevel.GetTopLevel(tabs));
                Assert.Same(context, tabs.DataContext);
                Assert.Same(model, tab.DataContext);
                Assert.Equal(1, tabs.SelectedIndex);
                Assert.Equal("Keep this edit", text.Text);
                Assert.Equal(2 + cycle * 2, model.Activations);
                Assert.Equal(1 + cycle * 2, model.Deactivations);
                panel.Show(owner);
                Assert.Single(owner.OwnedWindows);
                Assert.Equal(2 + cycle * 2, model.Activations);

                popup.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.False(panel.IsOpen);
                Assert.Same(tabs, dock.Content);
                Assert.Same(owner, TopLevel.GetTopLevel(tabs));
                Assert.Equal(3 + cycle * 2, model.Activations);
                Assert.Equal(2 + cycle * 2, model.Deactivations);
            }
        }
        finally
        {
            panel.Close();
            owner.Close();
            Dispatcher.UIThread.RunJobs();
            model.Dispose();
        }
        Assert.Equal(model.Activations, model.Deactivations);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task SectionsMoveIndependentlyAndRestoreTheSidebar(bool hudFirst) => RunAsync(() =>
    {
        var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
        var previousDesignMode = Design.IsDesignMode;
        designMode.SetValue(null, true);
        Window? owner = null;
        try
        {
            var page = new FlightDataPage();
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            owner = new Window { Content = page, Width = 1400, Height = 1000, RequestedThemeVariant = ThemeVariant.Dark };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            var layout = page.FindControl<Grid>("FlightLayout")!;
            var sidebar = page.FindControl<Grid>("LeftPanels")!;
            var hud = page.FindControl<ContentControl>("HudHost")!;
            var tabs = page.FindControl<ContentControl>("TabsHost")!;
            var hudContent = hud.Content;
            var tabsContent = tabs.Content;
            var tabset = page.FindControl<TabControl>("TabView")!;
            tabset.SelectedIndex = 5;
            layout.ColumnDefinitions[0].Width = new GridLength(460);
            sidebar.RowDefinitions[0].Height = new GridLength(410);
            owner.UpdateLayout();
            SaveScreenshot(owner, "docked");

            void PopOut(bool isHud)
            {
                var button = page.FindControl<Button>(isHud ? "PopOutHudButton" : "PopOutTabsButton")!;
                Assert.True(button.IsVisible);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                owner.UpdateLayout();
            }
            PopOut(hudFirst);
            Assert.Single(owner.OwnedWindows);
            Assert.True(sidebar.IsVisible);
            Assert.Equal(GridLength.Star, sidebar.RowDefinitions[hudFirst ? 2 : 0].Height);
            PopOut(!hudFirst);
            Assert.Equal(2, owner.OwnedWindows.Count);
            Assert.False(sidebar.IsVisible);
            Assert.Equal(0, layout.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(0, layout.ColumnDefinitions[1].ActualWidth);
            Assert.Equal(5, tabset.SelectedIndex);
            foreach (var popup in owner.OwnedWindows) SaveScreenshot(popup, popup.Title!.Contains("HUD") ? "hud" : "tabs");

            var hudPopup = owner.OwnedWindows.Single(window => window.Title!.Contains("HUD"));
            var tabsPopup = owner.OwnedWindows.Single(window => window.Title!.Contains("Tabs"));
            (hudFirst ? hudPopup : tabsPopup).Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(sidebar.IsVisible);
            Assert.Equal(new GridLength(460), layout.ColumnDefinitions[0].Width);
            (hudFirst ? tabsPopup : hudPopup).Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(hudContent, hud.Content);
            Assert.Same(tabsContent, tabs.Content);
            Assert.Equal(new GridLength(410), sidebar.RowDefinitions[0].Height);
            Assert.Equal(GridLength.Star, sidebar.RowDefinitions[2].Height);
            Assert.Equal(5, tabset.SelectedIndex);

            PopOut(true);
            PopOut(false);
            owner.Content = null; // Navigation away must close and return both sections.
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(owner.OwnedWindows);
            Assert.Same(hudContent, hud.Content);
            Assert.Same(tabsContent, tabs.Content);
        }
        finally
        {
            owner?.Close();
            Dispatcher.UIThread.RunJobs();
            designMode.SetValue(null, previousDesignMode);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task NonDesktopHostsKeepTheSectionsDocked() => RunAsync(() =>
    {
        var application = Application.Current!;
        var lifetime = application.ApplicationLifetime;
        var lifetimeField = typeof(Application).GetField("_applicationLifetime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        lifetimeField.SetValue(application, null);
        var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
        var previous = Design.IsDesignMode;
        designMode.SetValue(null, true);
        Window? owner = null;
        try
        {
            var page = new FlightDataPage();
            owner = new Window { Content = page };
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            foreach (var name in new[] { "PopOutHudButton", "PopOutTabsButton" })
            {
                var button = page.FindControl<Button>(name)!;
                Assert.False(button.IsVisible);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Assert.Empty(owner.OwnedWindows);
            Assert.NotNull(page.FindControl<ContentControl>("HudHost")!.Content);
            Assert.NotNull(page.FindControl<ContentControl>("TabsHost")!.Content);
        }
        finally
        {
            owner?.Close();
            Dispatcher.UIThread.RunJobs();
            designMode.SetValue(null, previous);
            lifetimeField.SetValue(application, lifetime);
        }
        return Task.CompletedTask;
    });

    [Fact]
    public Task ConfirmationsStayInTheFocusedPopup() => RunAsync(async () =>
    {
        var app = (MissionPlanner.App.App)Application.Current!;
        var owner = new UrsaWindow { Width = 900, Height = 700 };
        var popup = new UrsaWindow { Width = 650, Height = 800 };
        var lifetime = (IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;
        lifetime.Windows.Returns(new Window[] { owner, popup });
        try
        {
            owner.Show();
            popup.Show(owner);
            popup.Activate();
            Dispatcher.UIThread.RunJobs();
            var windows = new WindowProvider();
            windows.SetMainWindow(owner);
            Assert.True(popup.IsActive);
            Assert.Same(popup, windows.ActiveWindow);
            var dialogs = new AvaloniaDialogService(app.ServiceProvider.GetRequiredService<IUiDispatcher>(), windows);
            var options = dialogs.CreateOptions("Confirm test action");
            var result = dialogs.ConfirmAsync(options, "Test confirmation", TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(popup.GetHashCode(), options.TopLevelHashCode);
            Assert.Empty(owner.GetVisualDescendants().OfType<ConfirmDialogView>());
            var confirmation = Assert.Single(popup.GetVisualDescendants().OfType<ConfirmDialogView>());
            ((ConfirmDialogViewModel)confirmation.DataContext!).Cancel();
            Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            popup.Close();
            owner.Close();
        }
    });

    private Task RunAsync(Func<Task> action)
    {
        return ui.Session.Dispatch(async () => { await action(); return true; }, TestContext.Current.CancellationToken);
    }

    private static void SaveScreenshot(Window window, string name)
    {
        window.UpdateLayout();
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "flight-data-popouts");
        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    public sealed class LifecycleModel(ILogger<LifecycleModel> logger) : ViewModelBase(logger)
    {
        public int Activations { get; private set; }
        public int Deactivations { get; private set; }
        public override Task ActivateAsync() { Activations++; return Task.CompletedTask; }
        public override Task DeactivateAsync() { Deactivations++; return Task.CompletedTask; }
    }

    public sealed class UiFixture : IAsyncDisposable
    {
        public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(FlightDataPanelWindowTests));
        public ValueTask DisposeAsync() => new(Task.Run(Session.Dispose));
    }

    private sealed class TestApp : MissionPlanner.App.App
    {
        private readonly bool desktop;

        public TestApp(IServiceProvider services, bool desktop) : base(services)
        {
            this.desktop = desktop;
        }

        public override void Initialize()
        {
            // Headless setup locks in a null lifetime. Supply only the desktop capability
            // boundary in this isolated test application; no desktop event loop is started.
            if (desktop)
            {
                typeof(Application).GetField("_applicationLifetime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(this, Substitute.For<IClassicDesktopStyleApplicationLifetime>());
            }
            base.Initialize();
        }

        // Tests create their own windows instead of constructing the production main shell.
        public override void OnFrameworkInitializationCompleted() { }
    }
}

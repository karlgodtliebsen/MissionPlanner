using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Models;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.Preferences;
using MissionPlanner.App.Views.Preferences.Sections;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Settings;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class HudFrameTests(HudFrameTests.UiFixture ui) : IClassFixture<HudFrameTests.UiFixture>
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var feed = new Feed();
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(feed)
            .AddSingleton(feed.Active)
            .AddSingleton(feed.Data)
            .AddSingleton<TimeProvider>(feed.Clock)
            .AddSingleton<IUiDispatcher>(_ => new AvaloniaUiDispatcher(Dispatcher.UIThread))
            .AddSingleton(Substitute.For<IDomainEventHub>())
            .AddSingleton<IPlannerSettingsStore>(new MemoryStore())
            .AddSingleton<IPlannerSettingsService, PlannerSettingsService>()
            .AddTransient<HudViewModel>().BuildServiceProvider();
        return AppBuilder.Configure(() => new TestApp(services)).UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public Task PreferencesSetLiveAndPersistedAttitudeSelection() => ui.Session.Dispatch(async () =>
    {
        var settings = Services.GetRequiredService<IPlannerSettingsService>();
        var store = Services.GetRequiredService<IPlannerSettingsStore>();
        // This section only loads/saves local settings; the map/file adapters are not used.
        using var preferences = new PreferencesViewModel(settings, null!, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<PreferencesViewModel>.Instance);
        preferences.SelectedMapSource = MapSettingsSourceCatalog.Resolve(
            MapSettingsSourceCatalog.Create(BuiltInMapCatalog.Load()), "osm-standard", true);
        await preferences.ResetSectionCommand.ExecuteAsync("Telemetry");
        Dispatcher.UIThread.RunJobs();
        Assert.Null(preferences.ErrorMessage);
        Services.GetRequiredService<Feed>().Select(new VehicleId(4, 1));
        var hud = new HudView();
        using var model = (HudViewModel)hud.DataContext!;
        var section = new TelemetryDisplayTabView { DataContext = preferences };
        var preferenceWindow = new Window { Content = section, Width = 650, Height = 440 };
        var hudWindow = new Window { Content = hud, Width = 800, Height = 550 };
        try
        {
            preferenceWindow.Show();
            hudWindow.Show();
            Dispatcher.UIThread.RunJobs();
            var selector = section.FindControl<ComboBox>("DefaultAttitudeDisplaySelector")!;
            var hudSelector = hud.FindControl<ComboBox>("DisplaySelector")!;
            Assert.Equal(3, selector.ItemCount);
            foreach (var choice in HudDisplayChoice.All)
            {
                selector.SelectedItem = choice;
                Dispatcher.UIThread.RunJobs();
                Assert.Same(choice, preferences.SelectedAttitudeDisplay);
                await preferences.SaveCommand.ExecuteAsync(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Planner preferences saved.", preferences.StatusMessage);
                Assert.Equal(choice.Mode, settings.Current.Telemetry.DefaultAttitudeDisplay);
                Assert.Same(choice, hudSelector.SelectedItem);
                using var newlyOpened = Services.GetRequiredService<HudViewModel>();
                Assert.Same(choice, newlyOpened.SelectedDisplay);
                var reloaded = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
                await reloaded.InitializeAsync(TestContext.Current.CancellationToken);
                Assert.Equal(choice.Mode, reloaded.Current.Telemetry.DefaultAttitudeDisplay);
                if (choice.Mode == AttitudeDisplayMode.Frame) SaveScreenshot(preferenceWindow, "preferences");
            }

            // A local override survives unrelated saves and moving the panel, while changed defaults apply on return.
            model.SelectedDisplay = HudDisplayChoice.Resolve(AttitudeDisplayMode.Frame);
            await settings.SaveAsync(settings.Current with { Telemetry = settings.Current.Telemetry with { ChartHistorySeconds = 180 } });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AttitudeDisplayMode.Frame, model.SelectedDisplay.Mode);
            await model.DeactivateAsync();
            await settings.SaveAsync(settings.Current with { Telemetry = settings.Current.Telemetry with { DefaultAttitudeDisplay = AttitudeDisplayMode.Hud } });
            await model.ActivateAsync();
            Assert.Equal(AttitudeDisplayMode.Hud, model.SelectedDisplay.Mode);
            await preferences.ResetSectionCommand.ExecuteAsync("Telemetry");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AttitudeDisplayMode.Both, model.SelectedDisplay.Mode);
            Assert.Equal(AttitudeDisplayMode.Both, preferences.SelectedAttitudeDisplay.Mode);
        }
        finally
        {
            preferenceWindow.Close();
            hudWindow.Close();
            await settings.ResetSectionAsync(PlannerSettingsSection.Telemetry);
            Dispatcher.UIThread.RunJobs();
        }
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExistingSettingsDefaultToBothAndInvalidModesAreRejected()
    {
        var settings = new PlannerSettingsService(new MemoryStore(), NullLogger<PlannerSettingsService>.Instance);
        var imported = await settings.ImportAsync("""{"schemaVersion":5,"telemetry":{"displayRateHz":12,"chartHistorySeconds":180}}""",
            TestContext.Current.CancellationToken);
        Assert.True(imported.Success);
        Assert.Equal(AttitudeDisplayMode.Both, settings.Current.Telemetry.DefaultAttitudeDisplay);
        Assert.Equal(12, settings.Current.Telemetry.DisplayRateHz);
        var invalid = await settings.SaveAsync(settings.Current with
        {
            Telemetry = settings.Current.Telemetry with { DefaultAttitudeDisplay = (AttitudeDisplayMode)999 }
        }, TestContext.Current.CancellationToken);
        Assert.False(invalid.Success);
        Assert.Contains(invalid.Errors, error => error.Property == nameof(PlannerTelemetrySettings.DefaultAttitudeDisplay));
        Assert.Equal(AttitudeDisplayMode.Both, settings.Current.Telemetry.DefaultAttitudeDisplay);
    }

    [Fact]
    public Task AttitudeFreshnessReferenceAndVehicleSwitching() => RunAsync(() =>
    {
        var feed = Services.GetRequiredService<Feed>();
        feed.Select(new VehicleId(1, 1));
        using var model = Services.GetRequiredService<HudViewModel>();
        feed.Publish(25, -17, 90);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.FrameAttitude.HasAttitude && model.FrameAttitude.IsFresh);
        Assert.Equal(90, model.FrameAttitude.Yaw, 6);
        Assert.Equal(model.Roll, model.FrameAttitude.Roll, 6);
        Assert.Equal(model.Pitch, model.FrameAttitude.Pitch, 6);
        model.ResetModelHeadingCommand.Execute(null);
        Assert.Equal(0, model.FrameAttitude.Yaw);
        Assert.Equal(90, model.Heading);
        Assert.Equal(25, model.Roll);
        Assert.Equal(-17, model.Pitch);
        feed.Publish(25, -17, 100);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(10, model.FrameAttitude.Yaw, 6);

        feed.Clock.Now += TimeSpan.FromSeconds(3);
        model.RefreshHeadingReference();
        Assert.True(model.FrameAttitude.HasAttitude);
        Assert.False(model.FrameAttitude.IsFresh);
        Assert.False(model.ResetModelHeadingCommand.CanExecute(null));
        var stale = model.FrameAttitude;
        model.ResetModelHeadingCommand.Execute(null);
        Assert.Equal(stale, model.FrameAttitude);
        feed.Publish(5, 10, 120);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.FrameAttitude.IsFresh);

        using var reconnected = new CancellationTokenSource();
        feed.Active.ConnectionCancellationToken.Returns(reconnected.Token);
        model.RefreshHeadingReference();
        Assert.Equal(120, model.FrameAttitude.Yaw, 6);
        model.ResetModelHeadingCommand.Execute(null);
        feed.Select(new VehicleId(2, 1));
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.FrameAttitude.HasAttitude);
        feed.Streams[new VehicleId(1, 1)].OnNext(VehicleHudData.CreateDefault(new VehicleId(1, 1)) with { Roll = 80 });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, model.Roll);
        feed.Publish(-30, 20, 270);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(270, model.FrameAttitude.Yaw, 6);
        Assert.Equal(-30, model.Roll);
        Assert.True(model.FrameAttitude.IsFresh);

        feed.State = feed.State! with { Motion = feed.State.Motion with { RollRadians = double.NaN } };
        model.RefreshHeadingReference();
        Assert.False(model.FrameAttitude.HasAttitude);
        feed.Publish(0, 0, 0);
        feed.State = feed.State! with { Connection = feed.State.Connection with { State = VehicleConnectionState.Offline } };
        model.RefreshHeadingReference();
        Assert.False(model.FrameAttitude.IsFresh);
        Assert.False(model.CanResetModelHeading);
        model.Dispose();
        Assert.False(feed.Streams[feed.Id].HasObservers);
    });

    [Fact]
    public Task ModesResizeAndSurvivePopOutWithLiveUpdates() => RunAsync(() =>
    {
        var feed = Services.GetRequiredService<Feed>();
        feed.Select(new VehicleId(3, 1));
        var view = new HudView();
        using var model = (HudViewModel)view.DataContext!;
        var dock = new ContentControl { Content = view, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        var owner = new Window { Content = dock, Width = 400, Height = 330 };
        var popup = new FlightDataPanelWindow(dock, "Attitude", new Size(800, 550), () => { });
        try
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            owner.Show();
            Dispatcher.UIThread.RunJobs();
            SaveScreenshot(owner, "unavailable");
            feed.Publish(12, 22, 25);
            Dispatcher.UIThread.RunJobs();
            var selector = view.FindControl<ComboBox>("DisplaySelector")!;
            var frame = view.FindControl<VehicleFrameCanvas>("FrameCanvas")!;
            var hud = view.FindControl<HudCanvas>("Canvas")!;
            Assert.Equal(3, selector.ItemCount);
            Assert.True(frame.IsVisible && hud.IsVisible);
            SaveScreenshot(owner, "both-sidebar");

            selector.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            owner.UpdateLayout();
            Assert.True(model.ShowFrame && !model.ShowHud);
            Assert.False(hud.IsVisible);
            Assert.Equal(view.Bounds.Width, frame.Bounds.Width, 1);
            SaveScreenshot(owner, "frame-sidebar");

            selector.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            owner.UpdateLayout();
            Assert.True(hud.IsVisible && !frame.IsVisible);
            Assert.Equal(view.Bounds.Width, hud.Bounds.Width, 1);
            selector.SelectedIndex = 2;
            popup.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(model, view.DataContext);
            Assert.Equal(2, selector.SelectedIndex);
            var window = Assert.Single(owner.OwnedWindows);
            window.UpdateLayout();
            Assert.Equal(1, Grid.GetColumn(frame));
            Assert.True(frame.Bounds.Width > 300);
            Assert.Equal(12, model.Roll, 6); // Cached telemetry is restored during reparenting.
            feed.Publish(-25, 35, 310);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(model.FrameAttitude, frame.Attitude);
            Assert.Equal(-25, frame.Attitude.Roll, 6);
            SaveScreenshot(window, "both-popup");
            window.Width = 400;
            window.Height = 750;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, Grid.GetRow(frame));
            Assert.Equal(0, Grid.GetColumn(frame));
            SaveScreenshot(window, "both-tall");
            window.Width = 800;
            window.Height = 550;
            selector.SelectedIndex = 1;
            foreach (var (roll, pitch, yaw, name) in new[]
            {
                (0, 0, 0, "level"), (0, 60, 0, "pitch"), (60, 0, 0, "roll"),
                (0, 0, 90, "yaw"), (180, 0, 0, "inverted")
            })
            {
                feed.Publish(roll, pitch, yaw);
                Dispatcher.UIThread.RunJobs();
                SaveScreenshot(window, name);
            }
            feed.Clock.Now += TimeSpan.FromSeconds(3);
            model.RefreshHeadingReference();
            SaveScreenshot(window, "stale");
            popup.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(view, dock.Content);
            Assert.Equal(1, selector.SelectedIndex);
            feed.Publish(10, 15, 20);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(model.FrameAttitude, frame.Attitude);
            Assert.True(frame.Attitude.IsFresh);
        }
        finally
        {
            popup.Close();
            owner.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(feed.Streams[feed.Id].HasObservers);
        }
    });

    private static IServiceProvider Services => ((TestApp)Application.Current!).ServiceProvider;
    private Task RunAsync(Action action) => ui.Session.Dispatch(() => { action(); return true; }, TestContext.Current.CancellationToken);

    private static void SaveScreenshot(Window window, string name)
    {
        window.UpdateLayout();
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "attitude-frame");
        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    public sealed class UiFixture : IAsyncDisposable
    {
        public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HudFrameTests));
        public ValueTask DisposeAsync() => new(Task.Run(Session.Dispose));
    }

    private sealed class TestApp(IServiceProvider services) : MissionPlanner.App.App(services)
    {
        public override void OnFrameworkInitializationCompleted() { }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MemoryStore : IPlannerSettingsStore
    {
        private string? document;
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(document);
        public ValueTask WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            document = value;
            return ValueTask.CompletedTask;
        }
        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            document = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Feed
    {
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public IVehicleHudDataService Data { get; } = Substitute.For<IVehicleHudDataService>();
        public Clock Clock { get; } = new();
        public VehicleId Id { get; private set; }
        public VehicleState? State { get; set; }
        public Dictionary<VehicleId, Subject<VehicleHudData>> Streams { get; } = [];
        private readonly Dictionary<VehicleId, VehicleHudData> latest = [];

        public Feed()
        {
            Active.VehicleId.Returns(_ => Id);
            Active.State.Returns(_ => State);
            Active.IsOnline.Returns(_ => State?.ConnectionState == VehicleConnectionState.Online);
            Data.GetHudData(Arg.Any<VehicleId>()).Returns(call => latest.GetValueOrDefault(call.Arg<VehicleId>()));
        }

        public void Select(VehicleId id)
        {
            var previous = new ActiveVehicleSnapshot(Id, State);
            Id = id;
            State = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, Clock.Now,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
            Streams[id] = new Subject<VehicleHudData>();
            Data.ObserveHudData(id).Returns(Streams[id]);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(previous, new(id, State)));
        }

        public void Publish(double roll, double pitch, double yaw)
        {
            State = State! with { Motion = State!.Motion with
            {
                RollRadians = roll * Math.PI / 180, PitchRadians = pitch * Math.PI / 180,
                YawRadians = yaw * Math.PI / 180, AttitudeObservedAt = Clock.Now
            } };
            latest[Id] = VehicleHudData.CreateDefault(Id) with { Roll = roll, Pitch = pitch, Yaw = yaw, Heading = yaw };
            Streams[Id].OnNext(latest[Id]);
        }
    }
}

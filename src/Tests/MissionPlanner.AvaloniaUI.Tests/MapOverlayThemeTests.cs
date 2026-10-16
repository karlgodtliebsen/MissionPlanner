using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Material.Icons;
using Material.Icons.Avalonia;
using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.Themes;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks map overlay colors in the production views, including the Flight Data map host.</summary>
[Collection("Document rendering")]
public sealed class MapOverlayThemeTests
{
    /// <summary>Loads production styles without starting map services or connecting a vehicle.</summary>
    public static AppBuilder BuildAvaloniaApp() => PaletteThemeRenderingTests.BuildAvaloniaApp();

    /// <summary>The four map navigation icons stay black through theme and interaction changes.</summary>
    [Theory]
    [InlineData("GeoFence")]
    [InlineData("FlightData")]
    [InlineData("FlightPlanner")]
    public async Task ProductionMapIconsStayBlack(string host)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MapOverlayThemeTests));
        try
        {
            await session.Dispatch(() =>
            {
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                designMode.SetValue(null, true);
                Window? window = null;
                UserControl? view = null;
                try
                {
                    view = host switch
                    {
                        "GeoFence" => new GeoFenceMapView(),
                        "FlightData" => new FlightDataMissionMapView(),
                        _ => new FlightPlannerMissionMapView()
                    };
                    window = new Window { Content = view, Width = 900, Height = 600 };
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    var buttons = view.GetVisualDescendants().OfType<Button>()
                        .Where(button => button.Content is MaterialIcon
                        {
                            Kind: MaterialIconKind.MagnifyPlusOutline or MaterialIconKind.MagnifyMinusOutline
                                or MaterialIconKind.CrosshairsGps or MaterialIconKind.CompassOutline
                        }).ToArray();
                    Assert.Equal(4, buttons.Length);
                    foreach (var theme in AvaloniaThemeCatalog.Items)
                    {
                        AvaloniaThemeCatalog.Apply(theme);
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        foreach (var button in buttons)
                        {
                            var states = (IPseudoClasses)button.Classes;
                            CheckForeground("normal");
                            states.Set(":pointerover", true);
                            CheckForeground("hover");
                            states.Set(":pressed", true);
                            CheckForeground("pressed");
                            states.Set(":pressed", false);
                            states.Set(":pointerover", false);
                            button.IsEnabled = false;
                            CheckForeground("disabled");
                            button.IsEnabled = true;

                            void CheckForeground(string state)
                            {
                                var icon = (MaterialIcon)button.Content!;
                                var presenter = button.GetVisualDescendants().OfType<ContentPresenter>()
                                    .Single(control => control.Name == "PART_ContentPresenter");
                                foreach (var brush in new[] { button.Foreground, presenter.Foreground, icon.Foreground })
                                {
                                    Assert.True(brush is ISolidColorBrush { Color: var color } && color == Colors.Black,
                                        $"{host}/{theme.Id}/{icon.Kind}/{state}: expected black, got {brush}");
                                }
                            }
                        }
                        if (theme.Id == "dark-orange" && Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT") is { Length: > 0 } directory)
                        {
                            Directory.CreateDirectory(directory);
                            using var bitmap = new RenderTargetBitmap(new PixelSize(900, 600));
                            bitmap.Render(window);
                            bitmap.Save(Path.Combine(directory, $"map-overlay-{host}.png"), PngBitmapEncoderOptions.Default);
                        }
                    }
                }
                finally
                {
                    window?.Close();
                    if (view is not null)
                    {
                        foreach (var map in view.GetVisualDescendants().OfType<Mapsui.UI.Avalonia.MapControl>())
                        {
                            map.Dispose();
                        }
                        (view as IDisposable)?.Dispose();
                    }
                    designMode.SetValue(null, previous);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

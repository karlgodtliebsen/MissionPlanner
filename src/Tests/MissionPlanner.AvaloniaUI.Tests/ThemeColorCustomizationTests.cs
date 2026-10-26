using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Preferences;
using MissionPlanner.App.Views.Preferences.Sections;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Themes;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class ThemeColorCustomizationTests
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(new ServiceCollection().AddLogging()
            .AddSingleton<IUiDispatcher>(_ => new AvaloniaUiDispatcher(Dispatcher.UIThread))
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider()))
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    [Fact(Skip = "UI-SKIP-03: Headless rendering accesses Avalonia objects owned by another thread. See src/Tests/README.md.")]
    public async Task LiveColorsUpdateStaticAliasesAndRemainIsolatedAcrossThemes()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ThemeColorCustomizationTests));
        try
        {
            await session.Dispatch(() =>
            {
                var app = Application.Current!;
                var customizer = AvaloniaThemeCatalog.Colors;
                var accent = ThemeColorCatalog.Items.Single(item => item.Id == "accent");
                try
                {
                    // Exercise every installed provider, including named palettes and high contrast.
                    foreach (var theme in AvaloniaThemeCatalog.Items)
                    {
                        AvaloniaThemeCatalog.Apply(theme);
                        accent = customizer.Items.Single(item => item.Id == "accent");
                        foreach (var item in customizer.Items)
                        {
                            Assert.Contains(item.ResourceKeys, key =>
                                app.TryFindResource(key, app.ActualThemeVariant, out var resource) && resource is SolidColorBrush);
                        }
                        var original = customizer.GetColor(accent);
                        Assert.True(app.TryFindResource("ButtonSolidPrimaryBackground", app.ActualThemeVariant, out var alias));
                        var brush = Assert.IsType<SolidColorBrush>(alias);
                        Assert.True(app.TryFindResource("BannerInformationBorderBrush", app.ActualThemeVariant, out var ursaAlias));
                        var ursaBrush = Assert.IsType<SolidColorBrush>(ursaAlias);
                        customizer.SetColor(accent, Colors.Magenta);
                        Assert.Equal(Colors.Magenta, brush.Color);
                        if (customizer.Items == ThemeColorCatalog.Items)
                            Assert.Equal(Colors.Magenta, ursaBrush.Color);
                        else
                        {
                            customizer.SetColor(customizer.Items.Single(item => item.Id == "text"), Colors.Cyan);
                            Assert.Equal(Colors.Cyan, ursaBrush.Color);
                        }
                        customizer.ResetTheme();
                        Assert.Equal(original, brush.Color);
                    }

                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("dark-orange"));
                    accent = customizer.Items.Single(item => item.Id == "accent");
                    var buttonBackground = customizer.Items.Single(item => item.Id == "button-background");
                    var inputBackground = customizer.Items.Single(item => item.Id == "input-background");
                    var originalButton = customizer.GetColor(buttonBackground);
                    var originalInput = customizer.GetColor(inputBackground);
                    customizer.SetColor(buttonBackground, Colors.Red);
                    Assert.Equal(Colors.Red, customizer.GetColor(buttonBackground));
                    Assert.Equal(originalInput, customizer.GetColor(inputBackground));
                    customizer.SetColor(inputBackground, Colors.Blue);
                    Assert.Equal(Colors.Red, customizer.GetColor(buttonBackground));
                    Assert.Equal(Colors.Blue, customizer.GetColor(inputBackground));
                    var sampleButton = new Button { Content = "Button preview" };
                    var sampleInput = new TextBox { Text = "Input preview" };
                    var sampleWindow = new Window
                    {
                        Width = 400, Height = 180,
                        Content = new StackPanel { Children = { sampleButton, sampleInput } }
                    };
                    sampleWindow.Show();
                    try
                    {
                        Dispatcher.UIThread.RunJobs();
                        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(sampleButton.Background).Color);
                        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(sampleInput.Background).Color);
                        customizer.SetColor(buttonBackground, Colors.Green);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Equal(Colors.Green, Assert.IsAssignableFrom<ISolidColorBrush>(sampleButton.Background).Color);
                        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(sampleInput.Background).Color);
                        customizer.SetColor(buttonBackground, Colors.Red);
                    }
                    finally { sampleWindow.Close(); }
                    var controlSnapshot = customizer.Snapshot();
                    customizer.Load(new());
                    Assert.Equal(originalButton, customizer.GetColor(buttonBackground));
                    customizer.Load(controlSnapshot);
                    Assert.Equal(Colors.Red, customizer.GetColor(buttonBackground));
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("light-purple"));
                    Assert.NotEqual(Colors.Red, customizer.GetColor(buttonBackground));
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("dark-orange"));
                    Assert.Equal(Colors.Red, customizer.GetColor(buttonBackground));
                    customizer.SetColor(buttonBackground, null);
                    Assert.Equal(originalButton, customizer.GetColor(buttonBackground));
                    Assert.Equal(Colors.Blue, customizer.GetColor(inputBackground));
                    customizer.ResetTheme();
                    var darkDefault = customizer.GetColor(accent);
                    var row = new ThemeColorViewModel(customizer, accent);
                    row.Hex = "#FF123456";
                    Assert.Null(row.Error);
                    Assert.Equal(Color.Parse("#FF123456"), customizer.GetColor(accent));
                    row.Hex = "invalid";
                    Assert.NotNull(row.Error);
                    Assert.Equal(Color.Parse("#FF123456"), customizer.GetColor(accent));
                    var saved = customizer.Snapshot();
                    saved["dark-orange"]["accent"] = "#FF000000";
                    Assert.Equal(Color.Parse("#FF123456"), customizer.GetColor(accent));

                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("light-purple"));
                    var lightDefault = customizer.GetColor(accent);
                    Assert.NotEqual(Color.Parse("#FF123456"), lightDefault);
                    customizer.SetColor(accent, Colors.Lime);
                    Assert.Equal("#FF00FF00", customizer.Snapshot()["light-purple"]["accent"]);
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("dark-orange"));
                    Assert.Equal(Color.Parse("#FF123456"), customizer.GetColor(accent));
                    row.ResetCommand.Execute(null);
                    Assert.Equal(darkDefault, customizer.GetColor(accent));
                    Assert.Null(row.Error);
                    Assert.False(row.IsOverridden);
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("light-purple"));
                    Assert.Equal(Colors.Lime, customizer.GetColor(accent));
                    customizer.Load(new());
                    Assert.Equal(lightDefault, customizer.GetColor(accent));
                    Dispatcher.UIThread.RunJobs();

                    // This section only uses appearance state; unrelated map/file services are never invoked.
                    using var preferences = new PreferencesViewModel(
                        new PlannerSettingsService(new MemoryStore(), NullLogger<PlannerSettingsService>.Instance),
                        null!, null!, null!, null!, null!, null!, null!, null!, NullLogger<PreferencesViewModel>.Instance);
                    preferences.SelectedTheme = AvaloniaThemeCatalog.Resolve("dark-orange");
                    var section = new ApplicationAppearanceTabView { DataContext = preferences };
                    var window = new Window { Width = 800, Height = 700, Content = section };
                    window.Show();
                    try
                    {
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        var pickers = section.GetVisualDescendants().OfType<ColorPicker>().ToArray();
                        Assert.Equal(ThemeColorCatalog.Items.Count, pickers.Length);
                        preferences.ColorSearch = "button";
                        Assert.All(preferences.FilteredThemeColors, item => Assert.Contains("button", item.Name, StringComparison.OrdinalIgnoreCase));
                        Assert.NotEmpty(preferences.FilteredThemeColors);
                        preferences.ColorSearch = string.Empty;
                        Dispatcher.UIThread.RunJobs();
                        pickers = section.GetVisualDescendants().OfType<ColorPicker>().ToArray();
                        pickers[0].Color = Colors.Cyan;
                        Assert.Equal(Colors.Cyan, customizer.GetColor(accent));
                        Assert.Equal("#FF00FFFF", preferences.ThemeColors[0].Hex);
                        Assert.Equal(Colors.Cyan, preferences.ThemeColors.Single(item => item.Name == "Solid button background").Color);
                        preferences.ResetThemeColorsCommand.Execute(null);
                        Assert.Equal(darkDefault, pickers[0].Color);
                        var output = Path.Combine(AppContext.BaseDirectory, "TestResults", "theme-colors");
                        Directory.CreateDirectory(output);
                        foreach (var id in new[] { "dark-orange", "light-purple" })
                        {
                            preferences.SelectedTheme = AvaloniaThemeCatalog.Resolve(id);
                            Dispatcher.UIThread.RunJobs();
                            window.UpdateLayout();
                            using var bitmap = new RenderTargetBitmap(new PixelSize(800, 700));
                            bitmap.Render(window);
                            bitmap.Save(Path.Combine(output, $"{id}.png"), new PngBitmapEncoderOptions());
                            preferences.ColorSearch = "button";
                            Dispatcher.UIThread.RunJobs();
                            window.UpdateLayout();
                            using var buttonsBitmap = new RenderTargetBitmap(new PixelSize(800, 700));
                            buttonsBitmap.Render(window);
                            buttonsBitmap.Save(Path.Combine(output, $"{id}-buttons.png"), PngBitmapEncoderOptions.Default);
                            preferences.ColorSearch = string.Empty;
                        }
                    }
                    finally { window.Close(); }
                }
                finally
                {
                    customizer.Load(new());
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("system"));
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    [Fact]
    public async Task OverridesRoundTripThroughSettingsAndAppearanceReset()
    {
        var store = new MemoryStore();
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
        await service.InitializeAsync(cancellationToken);
        service.Current.Appearance = service.Current.Appearance with
        {
            ColorOverrides = new()
            {
                ["dark-orange"] = new() { ["accent"] = "#FF123456" },
                ["light-purple"] = new() { ["text"] = "#AA654321" }
            }
        };
        Assert.True((await service.SaveAsync(service.Current, cancellationToken)).Success);
        var reloaded = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
        await reloaded.InitializeAsync(cancellationToken);
        Assert.Equal("#FF123456", reloaded.Current.Appearance.ColorOverrides["dark-orange"]["accent"]);
        Assert.Equal("#AA654321", reloaded.Current.Appearance.ColorOverrides["light-purple"]["text"]);
        reloaded.Current.Appearance.ColorOverrides["dark-orange"]["accent"] = "invalid";
        Assert.False((await reloaded.SaveAsync(reloaded.Current, cancellationToken)).Success);
        Assert.True((await reloaded.ResetSectionAsync(PlannerSettingsSection.Appearance, cancellationToken)).Success);
        Assert.Empty(reloaded.Current.Appearance.ColorOverrides);
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
}

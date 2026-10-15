using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises palette changes on real installed templates and persisted settings.</summary>
[Collection("Document rendering")]
public sealed class MissionOrangeTests
{
    /// <summary>Creates a renderer using the production theme providers.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(new ServiceCollection().AddLogging().BuildServiceProvider()))
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Open Semi and Ursa controls change together and Original restores their resources.</summary>
    [Fact]
    public async Task OpenControlsSwitchAndRestoreWithoutChangingStatusColors()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MissionOrangeTests));
        try
        {
            await session.Dispatch(() =>
            {
                var application = Application.Current!;
                application.RequestedThemeVariant = ThemeVariant.Dark;
                var button = new Button { Content = "Primary action" };
                button.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                var dropdown = new Ursa.Controls.IconDropDownButton { Content = "Parameters" };
                dropdown.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidIconButton"));
                var warning = new Button { Content = "Warning", Classes = { "Warning" } };
                warning.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                var hovered = new Button { Content = "Hover" };
                hovered.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                ((IPseudoClasses)hovered.Classes).Set(":pointerover", true);
                var pressed = new Button { Content = "Pressed" };
                pressed.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                ((IPseudoClasses)pressed.Classes).Set(":pressed", true);
                var toggle = new ToggleButton { Content = "Checked toggle", IsChecked = true };
                var input = new TextBox { Text = "Keyboard focus", Width = 350 };
                var controls = new StackPanel
                {
                    Margin = new Thickness(24), Spacing = 20,
                    Children =
                    {
                        new TextBlock { Text = "MissionPlanner — offline control preview", FontSize = 24 },
                        new WrapPanel { ItemSpacing = 12, Children = { button, dropdown, hovered, pressed, warning } },
                        toggle,
                        new CheckBox { Content = "Checked parameter", IsChecked = true },
                        new RadioButton { Content = "Selected mode", IsChecked = true },
                        new ToggleSwitch { Content = "Enabled", IsChecked = true },
                        new Slider { Value = 60 },
                        new ProgressBar { Value = 60 },
                        input,
                        new ListBox { ItemsSource = new[] { "Selected parameter", "Other parameter" }, SelectedIndex = 0 },
                        new TabControl
                        {
                            Items = { new TabItem { Header = "Parameters" }, new TabItem { Header = "Information" } },
                            SelectedIndex = 0
                        },
                        new DataGrid
                        {
                            ItemsSource = new[] { "Selected grid parameter", "Other grid parameter" },
                            SelectedIndex = 0, Height = 110,
                            Columns = { new DataGridTextColumn { Header = "Parameter", Binding = new Avalonia.Data.Binding(".") } }
                        },
                        new Button { Content = "Disabled", IsEnabled = false },
                        new TextBlock { Text = "Warning and success retain their semantic colors." }
                    }
                };
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                try
                {
                    designMode.SetValue(null, true);
                    var topbar = new MissionPlanner.App.Views.Common.TopBarView();
                    var panel = new Border { Child = controls, Classes = { "SectionCard" } };
                    Grid.SetRow(panel, 1);
                    var window = new Window
                    {
                        Width = 1100, Height = 1000,
                        Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { topbar, panel } }
                    };
                    var secondButton = new Button { Content = "Already open dialog" };
                    secondButton.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                    var document = new MissionPlanner.App.Controls.InformationDocumentView();
                    var secondWindow = new Window
                    {
                        Width = 320, Height = 120,
                        Content = new StackPanel { Children = { secondButton, document } }
                    };
                    window.Show();
                    secondWindow.Show(window);
                    var popupButton = new Button { Content = "Open popup action" };
                    popupButton.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
                    var flyout = new Flyout { Content = popupButton };
                    flyout.ShowAt(dropdown);
                    Settle(window);
                    var original = Brush(button.Background);
                    var status = Brush(warning.GetVisualDescendants().OfType<ContentPresenter>().Single().Background);
                    foreach (var mode in new[] { ThemeVariant.Dark, ThemeVariant.Light })
                    {
                        application.RequestedThemeVariant = mode;
                        AvaloniaThemeCatalog.ApplyColorScheme(AvaloniaThemeCatalog.ResolveColorScheme("original"));
                        Settle(window);
                        controls.Children.OfType<DataGrid>().Single().SelectedIndex = 0;
                        Settle(window);
                        Save(window, $"controls-original-{mode}");
                        AvaloniaThemeCatalog.ApplyColorScheme(AvaloniaThemeCatalog.ResolveColorScheme("mission-orange"));
                        Settle(window);
                        Assert.Equal(Resource("SemiColorPrimary"), Brush(button.Background));
                        Assert.Equal(Brush(button.Background), Brush(dropdown.Background));
                        Assert.Equal(Brush(button.Background), Brush(secondButton.Background));
                        Assert.Equal(Brush(button.Background), Brush(popupButton.Background));
                        var renderer = document.FindControl<Control>("Renderer")!;
                        Assert.Equal(Resource("SemiColorPrimary"), Brush(renderer.Resources["QuoteBorderColor"] as IBrush));
                        ((IPseudoClasses)hovered.Classes).Set(":pointerover", true);
                        ((IPseudoClasses)pressed.Classes).Set(":pressed", true);
                        Settle(window);
                        var dropdownPresenter = dropdown.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
                        Assert.True(Contrast(Brush(dropdownPresenter.Foreground), Brush(dropdown.Background)) >= 4.5, "Ursa primary foreground");
                        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
                        Assert.True(Contrast(Brush(presenter.Foreground), Brush(button.Background)) >= 4.5);
                        var togglePresenter = toggle.GetVisualDescendants().OfType<ContentPresenter>().Single();
                        Assert.True(Contrast(Brush(togglePresenter.Foreground), Brush(togglePresenter.Background)) >= 4.5, "Checked toggle");
                        input.Focus(Avalonia.Input.NavigationMethod.Tab);
                        foreach (var stateButton in new[] { hovered, pressed })
                        {
                            var statePresenter = stateButton.GetVisualDescendants().OfType<ContentPresenter>().Single();
                            Assert.True(Contrast(Brush(statePresenter.Foreground), Brush(statePresenter.Background)) >= 4.5);
                        }
                        foreach (var (foreground, background) in new[]
                        {
                            ("MissionOnAccent", "SemiColorPrimary"),
                            ("MissionOnAccentHover", "SemiColorPrimaryPointerover"),
                            ("MissionOnAccentPressed", "SemiColorPrimaryActive"),
                            ("SemiColorLink", "SemiColorBackground2"),
                            ("TextBoxSelectionForeground", "TextBoxSelectionBackground"),
                            ("MissionAccentText", "SemiColorPrimaryLightPointerover")
                        })
                        {
                            Assert.True(Contrast(Resource(foreground), Resource(background)) >= 4.5, foreground + " on " + background);
                        }
                        foreach (var surface in new[] { "SemiColorBackground0", "SemiColorBackground1", "SemiColorBackground2" })
                        {
                            Assert.True(Contrast(Resource("SemiColorFocusBorder"), Resource(surface)) >= 3, "Focus on " + surface);
                        }
                        Save(window, $"controls-orange-{mode}");
                    }
                    application.RequestedThemeVariant = ThemeVariant.Dark;
                    Settle(window);
                    Assert.Equal(status, Brush(warning.GetVisualDescendants().OfType<ContentPresenter>().Single().Background));
                    foreach (var highContrast in new[] { "aquatic", "desert", "dusk", "night-sky" })
                    {
                        AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve(highContrast));
                        Settle(window);
                        Assert.False(application.TryFindResource("MissionOnAccent", application.ActualThemeVariant, out _));
                    }
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("system"));
                    Settle(window);
                    Assert.Equal(ThemeVariant.Default, application.RequestedThemeVariant);
                    Assert.Equal("mission-orange", AvaloniaThemeCatalog.ColorScheme.Id);
                    Assert.Equal(Resource("SemiColorPrimary"), Brush(button.Background));
                    AvaloniaThemeCatalog.Apply(AvaloniaThemeCatalog.Resolve("dark"));
                    Settle(window);
                    Assert.Equal(Resource("SemiColorPrimary"), Brush(button.Background));
                    AvaloniaThemeCatalog.ApplyColorScheme(AvaloniaThemeCatalog.ResolveColorScheme("original"));
                    Settle(window);
                    Assert.Equal(original, Brush(button.Background));
                    Assert.Equal(original, Brush(secondButton.Background));
                    Assert.Equal(original, Brush(popupButton.Background));
                    Assert.False(application.TryFindResource("MissionOnAccent", ThemeVariant.Dark, out _));
                    flyout.Hide();
                    secondWindow.Close();
                    window.Close();
                }
                finally
                {
                    designMode.SetValue(null, previous);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    /// <summary>Scheme survives a new service instance; older JSON retains its theme and Original.</summary>
    [Fact]
    public async Task SettingsRoundTripAndLegacyDefault()
    {
        string? document = null;
        var store = Substitute.For<IPlannerSettingsStore>();
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(document));
        store.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            document = call.ArgAt<string>(0);
            return ValueTask.CompletedTask;
        });
        var service = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
        await service.InitializeAsync(TestContext.Current.CancellationToken);
        await service.SaveAsync(service.Current with
        {
            Appearance = service.Current.Appearance with { ThemeId = "dark", ColorSchemeId = "mission-orange" }
        }, TestContext.Current.CancellationToken);
        var restarted = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
        await restarted.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("mission-orange", restarted.Current.Appearance.ColorSchemeId);
        Assert.Equal("dark", restarted.Current.Appearance.ThemeId);
        document = document!.Replace("\"colorSchemeId\": \"mission-orange\",", "");
        var legacy = new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
        await legacy.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("original", legacy.Current.Appearance.ColorSchemeId);
        Assert.Equal("dark", legacy.Current.Appearance.ThemeId);
        Assert.NotEmpty(legacy.Validate(legacy.Current with
        {
            Appearance = legacy.Current.Appearance with { ColorSchemeId = "invalid" }
        }));
    }

    private static Color Resource(string key)
    {
        var application = Application.Current!;
        Assert.True(application.TryFindResource(key, application.ActualThemeVariant, out var value));
        return Brush(value as IBrush);
    }

    private static Color Brush(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
        }
    }
}

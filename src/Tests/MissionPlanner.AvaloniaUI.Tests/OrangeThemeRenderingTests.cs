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
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.App.Views.Preferences.Sections;
using MissionPlanner.Themes;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.ConfigTuning.Planner;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises installed templates, variant fallback and live resource replacement.</summary>
[Collection("Document rendering")]
public sealed class OrangeThemeRenderingTests
{
    /// <summary>Uses production providers and real Skia rendering, without vehicle connections.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var services = FirmwarePanelViewModelTests.CreateServices(collection =>
        {
            var settings = Substitute.For<IPlannerSettingsService>();
            settings.Current.Returns(new PlannerSettings { Appearance = new PlannerAppearanceSettings { ThemeId = "dark-orange" } });
            collection.AddSingleton(settings);
        });
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>Open controls and secondary windows follow repeated theme changes without leaking orange.</summary>
    [Fact]
    public async Task RuntimeSwitchingRendersScopedTemplatesAndPreservesBaseSemantics()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(OrangeThemeRenderingTests));
        try
        {
            await session.Dispatch(() =>
            {
                var app = Application.Current!;
                Assert.Same(MissionPlannerThemeVariants.DarkOrange, app.RequestedThemeVariant);
                var provider = app.Styles.OfType<MissionPlannerTheme>().Single();
                var dictionaries = provider.Resources.ThemeDictionaries.ToArray();
                VerifyControlAliases(app, MissionPlannerThemeVariants.DarkOrange);
                VerifyControlAliases(app, MissionPlannerThemeVariants.LightOrange);
                var normal = Solid("Primary");
                var hover = Solid("Hover");
                var pressed = Solid("Pressed");
                var disabled = Solid("Disabled");
                disabled.IsEnabled = false;
                var warning = Solid("⚠ Warning");
                warning.Classes.Add("Warning");
                var secondaryAction = Solid("Secondary");
                secondaryAction.Classes.Add("Secondary");
                var icon = new Ursa.Controls.IconDropDownButton { Content = "Ursa", Width = 100 };
                icon.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidIconButton"));
                var panel = new Border { Classes = { "SectionCard" }, Padding = new Thickness(20) };
                var input = new TextBox { Text = "Selected input text", SelectionStart = 0, SelectionEnd = 8 };
                var list = new ListBox { ItemsSource = new[] { "Selected parameter", "Another parameter" }, SelectedIndex = 0 };
                panel.Child = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = "OFFLINE CONTROL PREVIEW", FontSize = 22 },
                        new WrapPanel { ItemSpacing = 12, Children = { normal, hover, pressed, disabled, warning, secondaryAction, icon } },
                        new WrapPanel { ItemSpacing = 20, Children =
                        {
                            new CheckBox { Content = "Checked", IsChecked = true },
                            new RadioButton { Content = "Selected", IsChecked = true },
                            new ToggleButton { Content = "Toggle", IsChecked = true, Classes = { "Primary" } },
                            new ToggleSwitch { Content = "Enabled", IsChecked = true }
                        } },
                        input,
                        new Slider { Value = 65 }, new ProgressBar { Value = 65 },
                        new TabControl { Items = { new TabItem { Header = "Parameters", Content = list }, new TabItem { Header = "Status" } } },
                        new DataGrid { Height = 110, AutoGenerateColumns = true, ItemsSource = new[] { new { Parameter = "EXAMPLE_PARAM", Value = 42 }, new { Parameter = "EXAMPLE_OTHER", Value = 7 } }, SelectedIndex = 0 },
                        new TextBlock { Text = "Warnings retain their icon, label and semantic palette." },
                        new WrapPanel
                        {
                            ItemSpacing = 10,
                            Children =
                            {
                                Banner(Avalonia.Controls.Notifications.NotificationType.Information),
                                Banner(Avalonia.Controls.Notifications.NotificationType.Success),
                                Banner(Avalonia.Controls.Notifications.NotificationType.Warning),
                                Banner(Avalonia.Controls.Notifications.NotificationType.Error)
                            }
                        }
                    }
                };
                var window = new Window { Width = 1000, Height = 750, Content = panel };
                var secondaryButton = Solid("Secondary window");
                var document = new MissionPlanner.App.Controls.InformationDocumentView
                {
                    Document = new MissionPlanner.App.Presentation.Documents.UserDocument("> Theme-aware document\n\n```\nEXAMPLE_PARAM = 42\n```")
                };
                var secondary = new Window { Width = 300, Height = 300, Content = new StackPanel { Children = { secondaryButton, document } } };
                var flyoutButton = Solid("Open flyout");
                var flyout = new Flyout { Content = flyoutButton };
                var tooltip = new TextBlock { Text = "Primary action tooltip" };
                ToolTip.SetTip(icon, tooltip);
                window.Show();
                secondary.Show(window);
                flyout.ShowAt(normal);
                ToolTip.SetIsOpen(icon, true);
                try
                {
                    foreach (var item in AvaloniaThemeCatalog.Items.Concat(AvaloniaThemeCatalog.Items.Reverse()))
                    {
                        AvaloniaThemeCatalog.Apply(item);
                        ((IPseudoClasses)hover.Classes).Set(":pointerover", true);
                        ((IPseudoClasses)pressed.Classes).Set(":pressed", true);
                        Flush(window);
                        var variant = window.ActualThemeVariant;
                        Assert.Equal(variant, tooltip.ActualThemeVariant);
                        var renderer = document.FindControl<LiveMarkdown.Avalonia.MarkdownRenderer>("Renderer")!;
                        Assert.Equal(MissionPlannerThemeVariants.IsDark(variant) ? TextMateSharp.Grammars.ThemeName.DarkPlus : TextMateSharp.Grammars.ThemeName.LightPlus, renderer.CodeBlockColorTheme);
                        Assert.Equal(Brush(app, variant, "SemiColorPrimary"), ColorOf((IBrush)renderer.Resources["QuoteBorderColor"]!));
                        var orange = variant == MissionPlannerThemeVariants.DarkOrange || variant == MissionPlannerThemeVariants.LightOrange;
                        Assert.Equal(Brush(app, variant, "ButtonSolidPrimaryBackground"), ColorOf(Presenter(normal).Background));
                        Assert.Equal(ColorOf(Presenter(normal).Background), ColorOf(Presenter(secondaryButton).Background));
                        Assert.Equal(ColorOf(Presenter(normal).Background), ColorOf(Presenter(flyoutButton).Background));
                        Assert.Equal(Brush(app, variant, "ButtonSolidWarningBackground"), ColorOf(Presenter(warning).Background));
                        if (orange)
                        {
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(Presenter(normal).Foreground));
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(Presenter(secondaryAction).Foreground));
                            foreach (var banner in panel.GetVisualDescendants().OfType<Ursa.Controls.Banner>())
                            {
                                var container = banner.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Container");
                                Assert.Equal(Brush(app, variant, $"Banner{banner.Type}Background"), ColorOf(container.Background));
                            }
                            Assert.Equal(Brush(app, variant, "MissionOnPrimaryHover"), ColorOf(Presenter(hover).Foreground));
                            Assert.Equal(Brush(app, variant, "MissionOnPrimaryPressed"), ColorOf(Presenter(pressed).Foreground));
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(Presenter(icon).Foreground));
                            var toggle = panel.GetVisualDescendants().OfType<ToggleButton>().Single(x => Equals(x.Content, "Toggle"));
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(Presenter(toggle).Foreground));
                            var checkGlyph = panel.GetVisualDescendants().OfType<PathIcon>().First(x => x.Name == "CheckGlyph");
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(checkGlyph.Foreground));
                            var radioGlyph = panel.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(x => x.Name == "CheckGlyph");
                            Assert.Equal(Brush(app, variant, "MissionOnPrimary"), ColorOf(radioGlyph.Fill));
                            Assert.Equal(Brush(app, variant, "SemiColorPrimary"), Brush(app, variant, "SemiColorInformation"));
                            Assert.Equal(Brush(app, variant, "SemiColorPrimary"), Brush(app, variant, "SemiColorSecondary"));
                            Assert.Equal(Brush(app, variant, "SemiColorPrimaryLight"), Brush(app, variant, "BannerInformationBackground"));
                            Assert.Equal(Brush(app, variant, "MissionAccentText"), Brush(app, variant, "LoadingIconForeground"));
                            foreach (var key in new[] { "SemiColorBackground0", "SemiColorBackground1", "SemiColorBackground2", "SemiColorWarning", "SemiColorDanger", "SemiColorSuccess" })
                            {
                                Assert.Equal(Brush(app, variant.InheritVariant!, key), Brush(app, variant, key));
                            }
                            foreach (var button in new[] { normal, hover, pressed })
                            {
                                Assert.True(Contrast(ColorOf(Presenter(button).Foreground), ColorOf(Presenter(button).Background)) >= 4.5);
                                Assert.True(Contrast(ColorOf(Presenter(button).BorderBrush), ColorOf(panel.Background)) >= 3);
                                Assert.True(Presenter(button).BorderThickness.Left > 0);
                            }
                            Assert.Equal(Brush(app, variant, "SemiColorPrimaryLightPointerover"), ColorOf(input.SelectionBrush));
                            Assert.True(Contrast(Brush(app, variant, "SemiColorFocusBorder"), Composite(input.Background!, ColorOf(panel.Background))) >= 3);
                        }
                        else
                        {
                            Assert.NotEqual(Color.Parse("#D56F0F"), ColorOf(Presenter(normal).Background));
                        }
                        flyout.Hide();
                        ToolTip.SetIsOpen(icon, false);
                        Save(window, "controls-" + item.Id);
                        flyout.ShowAt(normal);
                        ToolTip.SetIsOpen(icon, true);
                    }
                    secondary.RequestedThemeVariant = MissionPlannerThemeVariants.LightOrange;
                    window.RequestedThemeVariant = MissionPlannerThemeVariants.DarkOrange;
                    Flush(window);
                    Assert.Equal(Color.Parse("#D56F0F"), ColorOf(Presenter(normal).Background));
                    Assert.Equal(Color.Parse("#A84A00"), ColorOf(Presenter(secondaryButton).Background));
                    Assert.Equal(dictionaries, provider.Resources.ThemeDictionaries.ToArray());
                }
                finally
                {
                    flyout.Hide();
                    ToolTip.SetIsOpen(icon, false);
                    secondary.Close();
                    window.Close();
                }
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                try
                {
                    designMode.SetValue(null, true);
                    var preferences = new MissionPlanner.App.Views.Preferences.PreferencesViewModel(
                        null!, null!, null!, null!, null!, null!, null!, null!, null!,
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<MissionPlanner.App.Views.Preferences.PreferencesViewModel>.Instance);
                    var preview = new Window
                    {
                        Width = 1200,
                        Height = 1000,
                        Content = new DockPanel
                        {
                            Children = { new MissionPlanner.App.Views.Preferences.PreferencesPage { DataContext = preferences } }
                        }
                    };
                    preview.Show();
                    foreach (var item in AvaloniaThemeCatalog.Items.Where(x => x.Id is "light" or "dark" or "light-orange" or "dark-orange"))
                    {
                        AvaloniaThemeCatalog.Apply(item);
                        preferences.SelectedTheme = item;
                        Flush(preview);
                        if (item.Id.Contains("orange"))
                        {
                            var banner = preview.GetVisualDescendants().OfType<Ursa.Controls.Banner>().First();
                            var container = banner.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Container");
                            var glyph = banner.GetVisualDescendants().OfType<PathIcon>().Single(x => x.Name == "PART_BuildInIcon");
                            Assert.Equal(Brush(app, item.Theme, "SemiColorPrimaryLight"), ColorOf(container.Background));
                            Assert.Equal(Brush(app, item.Theme, "SemiColorPrimary"), ColorOf(container.BorderBrush));
                            Assert.Equal(Brush(app, item.Theme, "SemiColorPrimary"), ColorOf(glyph.Foreground));
                        }
                        Save(preview, "preferences-" + item.Id);
                    }
                    preview.Close();
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

    private static Button Solid(string text)
    {
        var button = new Button { Content = text };
        button.Bind(Control.ThemeProperty, new DynamicResourceExtension("SolidButton"));
        return button;
    }

    private static Ursa.Controls.Banner Banner(Avalonia.Controls.Notifications.NotificationType type) => new()
    {
        Type = type,
        Header = type.ToString(),
        Width = 220,
        Classes = { "Bordered" }
    };

    private static void VerifyControlAliases(Application app, ThemeVariant variant)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, ".git")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "src", "UI", "MissionPlanner.Themes", "Themes", variant.ToString());
        var index = System.Xml.Linq.XDocument.Load(Path.Combine(directory, "_index.axaml"));
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var aliases = new Dictionary<string, string>();
        foreach (var include in index.Descendants().Where(e => e.Name.LocalName == "MergeResourceInclude"))
        {
            var file = System.Xml.Linq.XDocument.Load(Path.Combine(directory, include.Attribute("Source")!.Value));
            foreach (var alias in file.Descendants().Where(e => e.Name.LocalName == "StaticResource"))
            {
                aliases[alias.Attribute(x + "Key")!.Value] = alias.Attribute("ResourceKey")!.Value;
            }
        }
        Assert.True(aliases.Count > 500, "Both complete Semi and Ursa control resource sets must be composed.");
        foreach (var (key, target) in aliases)
        {
            Assert.True(app.TryFindResource(key, variant, out var actual), key);
            Assert.True(app.TryFindResource(target, variant, out var expected), target);
            Assert.Equal(expected, actual);
        }
    }

    private static ContentPresenter Presenter(Control button) => button.GetVisualDescendants().OfType<ContentPresenter>().First(x => x.Name == "PART_ContentPresenter");
    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    private static Color Composite(IBrush brush, Color background)
    {
        var color = ColorOf(brush);
        var alpha = brush.Opacity * color.A / 255d;
        return Color.FromRgb((byte)Math.Round(color.R * alpha + background.R * (1 - alpha)),
            (byte)Math.Round(color.G * alpha + background.G * (1 - alpha)),
            (byte)Math.Round(color.B * alpha + background.B * (1 - alpha)));
    }
    private static Color Brush(Application app, ThemeVariant variant, string key)
    {
        Assert.True(app.TryFindResource(key, variant, out var value), key);
        return ColorOf(Assert.IsAssignableFrom<IBrush>(value));
    }

    private static double Contrast(Color first, Color second)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        Flush(window);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}


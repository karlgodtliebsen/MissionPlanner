using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Themes;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks compiled palette scope, live switching and real Semi/Ursa control rendering.</summary>
[Collection("Document rendering")]
public sealed class PaletteThemeRenderingTests
{
    private static readonly IReadOnlyDictionary<ThemeVariant, string> expectedColors = new Dictionary<ThemeVariant, string>
    {
        [MissionPlannerThemeVariants.DarkOrange] = "#D56F0F",
        [MissionPlannerThemeVariants.LightOrange] = "#A84A00",
        [MissionPlannerThemeVariants.DarkPurple] = "#CA78D1",
        [MissionPlannerThemeVariants.LightPurple] = "#871E9E",
        [MissionPlannerThemeVariants.DarkRed] = "#FC725A",
        [MissionPlannerThemeVariants.LightRed] = "#D52515",
        [MissionPlannerThemeVariants.DarkPink] = "#EF5686",
        [MissionPlannerThemeVariants.LightPink] = "#C51356",
        [MissionPlannerThemeVariants.DarkViolet] = "#A288DF",
        [MissionPlannerThemeVariants.LightViolet] = "#6A3AC7",
        [MissionPlannerThemeVariants.DarkIndigo] = "#8191D4",
        [MissionPlannerThemeVariants.LightIndigo] = "#3F51B5",
        [MissionPlannerThemeVariants.DarkYellow] = "#FDDE43",
        [MissionPlannerThemeVariants.LightYellow] = "#7D6A00",
        [MissionPlannerThemeVariants.DarkLime] = "#AEDC3A",
        [MissionPlannerThemeVariants.LightLime] = "#486800",
        [MissionPlannerThemeVariants.DarkLightGreen] = "#97C65F",
        [MissionPlannerThemeVariants.LightLightGreen] = "#4E7926",
        [MissionPlannerThemeVariants.DarkLightBlue] = "#40B4F3",
        [MissionPlannerThemeVariants.LightLightBlue] = "#0063A7",
        [MissionPlannerThemeVariants.DarkTeal] = "#33C2B0",
        [MissionPlannerThemeVariants.LightTeal] = "#00776F",
        [MissionPlannerThemeVariants.DarkCyan] = "#38BBC6",
        [MissionPlannerThemeVariants.LightCyan] = "#016979",
        [MissionPlannerThemeVariants.DarkGreen] = "#5DC264",
        [MissionPlannerThemeVariants.LightGreen] = "#25772F",
        [MissionPlannerThemeVariants.DarkGrey] = "#888D92",
        [MissionPlannerThemeVariants.LightGrey] = "#6B7075",
        [MissionPlannerThemeVariants.DarkAIPurple] = "#C375FF",
        [MissionPlannerThemeVariants.LightAIPurple] = "#8636DB",
    };

    /// <summary>Builds the real application styles without a vehicle or desktop lifetime.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(new ServiceCollection().AddLogging().BuildServiceProvider()))
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Shared control aliases resolve independently for each palette before and after live switching.</summary>
    [Fact]
    public async Task PalettesResolveAndRenderIndependentlyAcrossWindows()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(PaletteThemeRenderingTests));
        try
        {
            await session.Dispatch(() =>
            {
                var app = Application.Current!;
                Assert.All(expectedColors.Keys, variant =>
                    Assert.Contains(AvaloniaThemeCatalog.Items, item => item.Theme == variant));
                app.RequestedThemeVariant = ThemeVariant.Dark;
                // Resolve inactive palettes first: this catches aliases binding to the active base theme.
                foreach (var variant in expectedColors.Keys.Reverse())
                {
                    VerifyAliases(app, variant);
                    VerifyStatusAndContrast(app, variant);
                }

                var preview = new PalettePreview();
                var otherPreview = new PalettePreview();
                var window = new Window { Width = 960, Height = 780, Content = preview };
                var other = new Window
                {
                    Width = 960, Height = 780, Content = otherPreview,
                    RequestedThemeVariant = MissionPlannerThemeVariants.DarkPurple
                };
                window.Show();
                other.Show();
                try
                {
                    foreach (var item in AvaloniaThemeCatalog.Items.Concat(AvaloniaThemeCatalog.Items.Reverse()))
                    {
                        AvaloniaThemeCatalog.Apply(item);
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        other.UpdateLayout();
                        Assert.Equal(MissionPlannerThemeVariants.DarkPurple, other.ActualThemeVariant);
                        VerifyRendered(otherPreview, MissionPlannerThemeVariants.DarkPurple);
                        if (expectedColors.ContainsKey(item.Theme))
                        {
                            Assert.Equal(item.Theme, window.ActualThemeVariant);
                            VerifyAliases(app, item.Theme);
                            VerifyRendered(preview, item.Theme);
                            SavePreview(window, item.Id);
                        }
                    }

                    // Two palettes of the same brightness must also have separate control brushes.
                    window.RequestedThemeVariant = MissionPlannerThemeVariants.DarkOrange;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    VerifyRendered(preview, MissionPlannerThemeVariants.DarkOrange);
                    VerifyRendered(otherPreview, MissionPlannerThemeVariants.DarkPurple);
                    Assert.NotEqual(Brush(app, "ButtonSolidPrimaryBackground", window.ActualThemeVariant).Color,
                        Brush(app, "ButtonSolidPrimaryBackground", other.ActualThemeVariant).Color);
                }
                finally
                {
                    other.Close();
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private static void VerifyAliases(Application app, ThemeVariant variant)
    {
        var mode = MissionPlannerThemeVariants.IsDark(variant) ? "Dark" : "Light";
        var directory = RepositoryPath("src", "UI", "MissionPlanner.Themes", "Themes", mode);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var aliases = Directory.GetFiles(directory, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Where(element => element.Name.LocalName == "StaticResource")
            .ToArray();
        Assert.True(aliases.Length > 700);
        foreach (var alias in aliases)
        {
            var key = (string)alias.Attribute(x + "Key")!;
            var target = (string)alias.Attribute("ResourceKey")!;
            Assert.True(app.TryFindResource(key, variant, out var actual), $"{variant}: {key}");
            Assert.True(app.TryFindResource(target, variant, out var expected), $"{variant}: {target}");
            Assert.Equal(expected, actual);
        }
    }

    private static void VerifyStatusAndContrast(Application app, ThemeVariant variant)
    {
        foreach (var key in new[] { "SemiColorBackground0", "SemiColorBackground2", "SemiColorText0",
            "SemiColorSuccess", "SemiColorSuccessLight", "SemiColorWarning", "SemiColorDanger" })
        {
            Assert.Equal(Brush(app, key, variant.InheritVariant!).Color, Brush(app, key, variant).Color);
            Assert.Equal(Brush(app, key, variant.InheritVariant!).Opacity, Brush(app, key, variant).Opacity);
        }
        var expected = expectedColors[variant];
        foreach (var key in new[] { "SemiColorPrimary", "SemiColorSecondary", "SemiColorInformation" })
        {
            Assert.Equal(Color.Parse(expected), Brush(app, key, variant).Color);
        }
        foreach (var (foreground, background) in new[]
        {
            ("MissionOnPrimary", "SemiColorPrimary"),
            ("MissionOnPrimaryHover", "SemiColorPrimaryPointerover"),
            ("MissionOnPrimaryPressed", "SemiColorPrimaryActive"),
            ("MissionAccentText", "SemiColorBackground2"),
            ("MissionSelectionText", "SemiColorPrimaryLightPointerover")
        })
        {
            var contrast = Contrast(Brush(app, foreground, variant).Color, Brush(app, background, variant).Color);
            Assert.True(contrast >= 4.5, $"{variant}: {foreground}/{background} contrast {contrast:F2}");
        }
    }

    private static void VerifyRendered(PalettePreview preview, ThemeVariant variant)
    {
        var app = Application.Current!;
        foreach (var name in new[] { "Primary", "Secondary" })
        {
            var button = preview.FindControl<Button>(name)!;
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");
            Assert.Equal(Brush(app, "SemiColorPrimary", variant).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
            Assert.Equal(Brush(app, "MissionOnPrimary", variant).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Foreground).Color);
        }
        foreach (var (name, key) in new[] { ("Information", "SemiColorPrimaryLight"),
            ("Success", "SemiColorSuccessLight"), ("Warning", "SemiColorWarningLight"), ("Error", "SemiColorDangerLight") })
        {
            var banner = preview.FindControl<Banner>(name)!;
            var border = banner.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PART_Container");
            Assert.Equal(Brush(app, key, variant).Color, Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
        }
        var editor = preview.FindControl<TextBox>("Editor")!;
        Assert.Equal(Brush(app, "SemiColorPrimaryLightPointerover", variant).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(editor.SelectionBrush).Color);
    }

    private static ISolidColorBrush Brush(Application app, string key, ThemeVariant variant)
    {
        Assert.True(app.TryFindResource(key, variant, out var resource), $"{variant}: missing {key}");
        return Assert.IsAssignableFrom<ISolidColorBrush>(resource);
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) =>
            0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static void SavePreview(Window window, string id)
    {
        var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(new PixelSize(960, 780));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, $"palette-{id}.png"), PngBitmapEncoderOptions.Default);
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }
}

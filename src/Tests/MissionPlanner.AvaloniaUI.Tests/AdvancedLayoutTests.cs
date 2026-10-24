using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using MissionPlanner.App.Views.Common;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks Advanced tab composition, stable layout and configurable diagnostic plot labels.</summary>
[Collection("Document rendering")]
public sealed class AdvancedLayoutTests
{
    private static readonly XNamespace ui = "https://github.com/avaloniaui";
    private static readonly XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Uses an isolated renderer without shared application theme objects or hardware services.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>All page frames keep the available bounded width regardless of the selected tab's desired size.</summary>
    [Fact]
    public async Task PageWidthsRemainStableAcrossContentAndWindowSizes()
    {
        var definitions = Pages().Select(path => (Path: path, Grid: XDocument.Load(path).Root!.Element(ui + "Grid")!)).ToArray();
        Assert.Equal(9, definitions.Length);
        var session = HeadlessUnitTestSession.StartNew(typeof(AdvancedLayoutTests));
        try
        {
            await session.Dispatch(() =>
            {
                foreach (var definition in definitions)
                {
                    foreach (var available in new[] { 640d, 800d, 1400d })
                    {
                        var content = new Border { Height = 100 };
                        var grid = new Grid
                        {
                            HorizontalAlignment = Enum.Parse<HorizontalAlignment>(definition.Grid.Attribute("HorizontalAlignment")!.Value),
                            MinWidth = double.Parse(definition.Grid.Attribute("MinWidth")!.Value, CultureInfo.InvariantCulture),
                            MaxWidth = double.Parse(definition.Grid.Attribute("MaxWidth")!.Value, CultureInfo.InvariantCulture),
                            Children = { content }
                        };
                        foreach (var desired in new[] { 250d, 700d, 1100d, 250d })
                        {
                            content.Width = desired;
                            grid.Measure(new Size(available, 800));
                            grid.Arrange(new Rect(0, 0, available, 800));
                            var expected = Math.Min(available, 900);
                            Assert.True(Math.Abs(expected - grid.Bounds.Width) < .01,
                                $"{definition.Path}: width {grid.Bounds.Width} changed for content {desired} at viewport {available}.");
                            Assert.Equal((available - expected) / 2, grid.Bounds.X, 3);
                        }
                    }
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    /// <summary>Parent tabs stretch their content and expose every operational root page once.</summary>
    [Fact]
    public void RootPagesAreUniqueAndTabsStretchTheirContents()
    {
        var root = XDocument.Load(Path.Combine(AdvancedDirectory(), "AdvancedPage.axaml"));
        var pages = root.Descendants(ui + "TabItem").Select(tab => Assert.Single(tab.Elements()).Name.LocalName).ToArray();
        Assert.Equal(9, pages.Length);
        Assert.Equal(pages.Length, pages.Distinct().Count());
        Assert.Single(pages, name => name == "ScriptingConsolePage");
        foreach (var path in Pages().Append(Path.Combine(AdvancedDirectory(), "AdvancedPage.axaml")))
        {
            foreach (var tabs in XDocument.Load(path).Descendants(ui + "TabControl"))
            {
                Assert.Equal("Stretch", (string?)tabs.Attribute("HorizontalContentAlignment"));
                Assert.Equal("Stretch", (string?)tabs.Attribute("VerticalContentAlignment"));
                Assert.NotEmpty(tabs.Elements(ui + "TabItem"));
            }
        }
    }

    /// <summary>Only output workflows embed endpoint/status controls, with state provided by the owning page.</summary>
    [Fact]
    public void OutputViewsHaveWorkflowOwnedContextsAndPrivacyHasNoOutputTabs()
    {
        var placements = Pages().SelectMany(path => XDocument.Load(path).Descendants()
            .Where(element => element.Name.LocalName is "OutputEndpointView" or "OutputStatusView")
            .Select(element => (Page: Path.GetFileName(path), Element: element))).ToArray();
        Assert.Equal(4, placements.Length);
        Assert.All(placements, item =>
        {
            Assert.Contains(item.Page, new[] { "MirrorPage.axaml", "NmeaPage.axaml" });
            Assert.Equal(item.Element.Name.LocalName == "OutputEndpointView" ? "{Binding Endpoint}" : "{Binding Status}",
                (string?)item.Element.Attribute("DataContext"));
        });
        var privacy = XDocument.Load(Path.Combine(AdvancedDirectory(), "Privacy", "PrivacyExportPage.axaml"));
        Assert.Empty(privacy.Descendants(ui + "TabControl"));
        foreach (var path in Directory.EnumerateFiles(AdvancedDirectory(), "*View.axaml", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path) == "AdvancedToolTabView.axaml")
            {
                continue;
            }
            Assert.Null(XDocument.Load(path).Root!.Attribute(xaml + "TypeArguments"));
        }
    }

    /// <summary>Font-size bindings update the rendered labels and invalid sizes cannot reach text rendering.</summary>
    [Fact]
    public async Task PlotFontSizeBindsValidatesAndChangesRendering()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(AdvancedLayoutTests));
        try
        {
            await session.Dispatch(() =>
            {
                var plot = new TelemetryLinePlot
                {
                    Caption = "Test field / seconds / units",
                    Samples = [new Point(0, 2), new Point(1, 5), new Point(2, 3)]
                };
                Assert.Equal(11, plot.EmSize);
                foreach (var invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    Assert.Throws<ArgumentException>(() => plot.EmSize = invalid);
                }
                var sizeSource = new Slider { Minimum = 1, Maximum = 50, Value = 14 };
                using var binding = plot.Bind(TelemetryLinePlot.EmSizeProperty, new Binding(nameof(Slider.Value)) { Source = sizeSource });
                Assert.Equal(14, plot.EmSize);
                var smaller = Render(plot);
                sizeSource.Value = 22;
                Assert.Equal(22, plot.EmSize);
                var larger = Render(plot);
                Assert.False(smaller.SequenceEqual(larger), "Changing EmSize must affect the rendered plot, not only store a property value.");
                plot.Samples = [];
                Assert.NotEmpty(Render(plot));
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private static byte[] Render(TelemetryLinePlot plot)
    {
        plot.Measure(new Size(600, 300));
        plot.Arrange(new Rect(0, 0, 600, 300));
        using var bitmap = new RenderTargetBitmap(new PixelSize(600, 300), new Vector(96, 96));
        bitmap.Render(plot);
        using var output = new MemoryStream();
        bitmap.Save(output, PngBitmapEncoderOptions.Default);
        return output.ToArray();
    }

    private static IEnumerable<string> Pages() => Directory.EnumerateFiles(AdvancedDirectory(), "*Page.axaml", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(path) != "AdvancedPage.axaml");

    private static string AdvancedDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "UI", "MissionPlanner.App", "Views", "InitSetup", "Advanced");
    }
}

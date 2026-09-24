using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MissionPlanner.App.Controls;
using MissionPlanner.App.Presentation.Documents;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises the real document renderer with Avalonia's isolated headless platform.</summary>
public sealed class CompassInformationDocumentTests
{
    /// <summary>Provides the actual application resources without starting a vehicle session.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<MissionPlanner.App.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    /// <summary>Completed documents render, copy, preserve unchanged state and follow both themes at narrow widths.</summary>
    [Fact]
    public async Task RenderSelectCopyAndTheme()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(CompassInformationDocumentTests));
        try
        {
            await session.Dispatch(async () =>
            {
                const string markdown = "## Heading\n\nParagraph with `inline`.\n\n| Parameter | Value |\n| --- | --- |\n| COMPASS_ENABLE | 0 |\n\n```\ncode block\n```\n\n> Note\n";
                var view = new InformationDocumentView();
                view.Document = new UserDocument(markdown);
                var window = new Window { Content = view, Width = 360, Height = 800 };
                window.Show();
                window.UpdateLayout();
                var renderer = view.FindControl<Control>("Renderer")!;
                var rendererType = renderer.GetType();
                var update = rendererType.GetProperty("DocumentUpdate")!.GetValue(renderer);
                view.Document = new UserDocument(markdown, "Different title");
                Assert.Same(update, rendererType.GetProperty("DocumentUpdate")!.GetValue(renderer));
                Assert.Contains(renderer.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("Table"));
                Assert.True(double.IsFinite(view.DesiredSize.Height));
                Assert.True(view.DesiredSize.Width <= 360);

                var blocks = renderer.GetVisualDescendants().OfType<Control>()
                    .Where(control => control.GetType().Name == "MarkdownTextBlock").ToArray();
                var first = blocks[0].TranslatePoint(new Point(1, 1), window)!.Value;
                var last = blocks[^1].TranslatePoint(new Point(blocks[^1].Bounds.Width, blocks[^1].Bounds.Height / 2), window)!.Value;
                window.MouseDown(first, MouseButton.Left);
                window.MouseMove(last, RawInputModifiers.LeftMouseButton);
                window.MouseUp(last, MouseButton.Left);
                var dragged = (string)rendererType.GetProperty("SelectedText")!.GetValue(renderer)!;
                Assert.Contains("COMPASS_ENABLE", dragged);
                Assert.Contains("inline", dragged);
                Assert.Contains("code block", dragged);

                rendererType.GetMethod("SelectAll")!.Invoke(renderer, null);
                var selected = (string)rendererType.GetProperty("SelectedText")!.GetValue(renderer)!;
                Assert.Contains("Heading", selected);
                Assert.Contains("COMPASS_ENABLE", selected);
                Assert.Contains("code block", selected);
                renderer.Focus();
                window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Heading", await window.Clipboard!.TryGetTextAsync());

                var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
                buttons.Single(button => AutomationProperties.GetName(button) == "Copy Markdown")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(markdown, await window.Clipboard!.TryGetTextAsync());
                buttons.Single(button => AutomationProperties.GetName(button) == "Copy All")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                var plain = await window.Clipboard!.TryGetTextAsync();
                Assert.Contains("Heading", plain);
                Assert.Contains("COMPASS_ENABLE", plain);
                Assert.DoesNotContain("##", plain);
                Assert.Equal("Copied", view.FindControl<TextBlock>("CopyFeedback")!.Text);
                view.ShowToolbar = false;
                Assert.False(view.FindControl<WrapPanel>("Toolbar")!.IsVisible);

                window.RequestedThemeVariant = ThemeVariant.Light;
                window.UpdateLayout();
                var light = renderer.Resources["ForegroundColor"];
                window.RequestedThemeVariant = ThemeVariant.Dark;
                window.UpdateLayout();
                Assert.NotEqual(light, renderer.Resources["ForegroundColor"]);
                foreach (var key in new[] { "BorderColor", "CodeInlineColor", "CardBackgroundColor", "SecondaryCardBackgroundColor", "QuoteBorderColor" })
                {
                    Assert.NotNull(renderer.Resources[key]);
                }

                view.Document = new UserDocument("![image](https://example.test/image) [action](file:///secret) <img src='https://example.test/image'>");
                window.UpdateLayout();
                Assert.Empty(renderer.GetVisualDescendants().OfType<Image>());
                Assert.DoesNotContain(renderer.GetVisualDescendants(), control => control.GetType().Name == "Link");
                view.Document = new UserDocument(string.Join("\n\n", Enumerable.Repeat("Long paragraph", 200)));
                window.UpdateLayout();
                Assert.True(double.IsFinite(view.DesiredSize.Height));
                window.SetRenderScaling(2);
                window.Width = 1100;
                window.UpdateLayout();
                Assert.True(double.IsFinite(view.DesiredSize.Height));
                window.Close();
                return true;
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Session continuations may run on its UI thread; dispose from the pool.
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

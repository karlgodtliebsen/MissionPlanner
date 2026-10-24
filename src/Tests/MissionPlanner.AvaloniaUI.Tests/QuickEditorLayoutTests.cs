using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class QuickEditorLayoutTests
{
    public static AppBuilder BuildAvaloniaApp() => DiagnosticMenuAndFirmwareLayoutTests.BuildAvaloniaApp();

    [Theory]
    [InlineData(800, 550, Skip = "UI-SKIP-03: Headless rendering accesses Avalonia objects owned by another thread. See src/Tests/README.md.")]
    [InlineData(1200, 850, Skip = "UI-SKIP-03: Headless rendering accesses Avalonia objects owned by another thread. See src/Tests/README.md.")]
    [InlineData(1920, 1030, Skip = "UI-SKIP-03: Headless rendering accesses Avalonia objects owned by another thread. See src/Tests/README.md.")]
    public async Task EditorSectionsDoNotOverlapAtDifferentWindowSizes(int width, int height)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(QuickEditorLayoutTests));
        try
        {
            await session.Dispatch(() =>
            {
                using var model = new ParametersEditorViewModel(Substitute.For<IDialogService>());
                var view = new ParametersEditorView { DataContext = model };
                var window = new Window { Width = width, Height = height, Content = view };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var scroll = view.FindControl<ScrollViewer>("EditorScroll")!;
                    var input = view.FindControl<TextBox>("ParameterTextEditor")!;
                    var current = view.FindControl<TextBox>("CurrentParameterValues")!;
                    var search = view.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
                    var searchTop = search.TranslatePoint(default, view)!.Value.Y;
                    var inputTop = input.TranslatePoint(default, view)!.Value.Y;
                    var currentTop = current.TranslatePoint(default, view)!.Value.Y;
                    Assert.True(inputTop >= searchTop + search.Bounds.Height);
                    Assert.True(currentTop >= inputTop + input.Bounds.Height);
                    Assert.InRange(input.Bounds.Width, 100, width);
                    // The compact editor can fit without scrolling in a medium window.
                    // Whether scrolling is needed or not, both the viewport and final field must be reachable.
                    Assert.InRange(scroll.TranslatePoint(default, view)!.Value.Y + scroll.Bounds.Height, 0, height);
                    scroll.ScrollToEnd();
                    Dispatcher.UIThread.RunJobs();
                    Assert.InRange(current.TranslatePoint(default, view)!.Value.Y + current.Bounds.Height, 0, height);
                    scroll.ScrollToHome();
                    Dispatcher.UIThread.RunJobs();
                    var output = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(width, height));
                        bitmap.Render(window);
                        bitmap.Save(Path.Combine(output, $"quick-editor-{width}x{height}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    }
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Controls;
using MissionPlanner.App.Views.InitSetup.Arming;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Loads the real compiled Arming page with production styles and Markdown controls.</summary>
[Collection("Document rendering")]
public sealed class ArmingPageViewTests
{
    /// <summary>Composes a headless production application with isolated page dependencies.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var fixture = new ArmingViewModelTests.Fixture();
        var services = new ServiceCollection().AddLogging().AddSingleton(fixture).AddSingleton(fixture.Model).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services)).UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>Both tabs render at narrow and wide widths in light and dark themes.</summary>
    [Fact]
    public async Task RendersTabsAndReleasesHiddenWork()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ArmingPageViewTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var services = ((MissionPlanner.App.App)Application.Current!).ServiceProvider;
                var fixture = services.GetRequiredService<ArmingViewModelTests.Fixture>();
                var page = new ArmingPage();
                var window = new Window { Content = page, Width = 700, Height = 700 };
                window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var tabs = Assert.Single(page.GetVisualDescendants().OfType<TabControl>());
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    foreach (var width in new[] { 700, 1100 })
                    {
                        window.SetRenderScaling(width == 1100 ? 2 : 1);
                        window.Width = width;
                        tabs.SelectedIndex = 0;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        Assert.NotEmpty(page.GetVisualDescendants().OfType<InformationDocumentView>());
                        Assert.InRange(tabs.Bounds.Width, 1, width);
                        tabs.SelectedIndex = 1;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        Assert.NotEmpty(page.GetVisualDescendants().OfType<ComboBox>());
                    }
                }
                window.Close(); Dispatcher.UIThread.RunJobs();
                await fixture.Model.DeactivateAsync();
                Assert.False(fixture.Model.CanArm);
                fixture.Dispose();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

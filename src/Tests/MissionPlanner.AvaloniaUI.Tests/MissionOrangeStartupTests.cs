using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks that saved appearance is installed before the first window is created.</summary>
[Collection("Document rendering")]
public sealed class MissionOrangeStartupTests
{
    /// <summary>Creates a production application with an already-loaded settings snapshot.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var settings = Substitute.For<IPlannerSettingsService>();
        settings.Current.Returns(new PlannerSettings
        {
            Appearance = new PlannerAppearanceSettings { ThemeId = "dark", ColorSchemeId = "mission-orange" }
        });
        var services = new ServiceCollection().AddLogging().AddSingleton(settings).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>Startup uses saved mode and scheme without waiting for the Preferences page.</summary>
    [Fact]
    public async Task SavedAppearanceIsReadyBeforeFirstWindow()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MissionOrangeStartupTests));
        try
        {
            await session.Dispatch(() =>
            {
                var application = Application.Current!;
                Assert.Equal(ThemeVariant.Dark, application.RequestedThemeVariant);
                Assert.Equal("mission-orange", AvaloniaThemeCatalog.ColorScheme.Id);
                Assert.True(application.TryFindResource("MissionOnAccent", ThemeVariant.Dark, out _));
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

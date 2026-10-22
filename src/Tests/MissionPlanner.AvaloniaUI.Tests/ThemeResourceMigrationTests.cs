using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using MissionPlanner.Themes.Controls;
using MissionPlanner.Themes.Converters;
using MissionPlanner.Themes.Documents;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class ThemeResourceMigrationTests
{
    [Fact]
    public async Task SharedUiTypesAndAssetsAreAvailableWithoutAnAppDependency()
    {
        var assembly = typeof(VirtualizedItemsGrid).Assembly;
        Assert.Equal("MissionPlanner.Themes", assembly.GetName().Name);
        Assert.Same(assembly, typeof(InformationDocumentView).Assembly);
        Assert.Same(assembly, typeof(SetupInformationView).Assembly);
        Assert.Same(assembly, typeof(InvertedBoolConverter).Assembly);
        Assert.Same(assembly, typeof(UserDocument).Assembly);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "MissionPlanner.App");

        var session = HeadlessUnitTestSession.StartNew(typeof(ParameterEditorToolbarTests));
        try
        {
            await session.Dispatch(() =>
            {
                foreach (var path in new[]
                {
                    "Resources/AppIcon/mpdesktop.ico",
                    "Resources/Images/ardupilot.png",
                    "Resources/Assets/avalonia-logo.ico"
                })
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://MissionPlanner.Themes/{path}"));
                    Assert.True(stream.Length > 0, path);
                }
                foreach (var path in new[]
                {
                    "Resources/Geometries/Geometry.axaml",
                    "Resources/Styles/SharedStyles.axaml",
                    "Resources/Styles/VirtualizedItemsGrid.axaml",
                    "Resources/Styles/InformationDocumentStyles.axaml"
                })
                {
                    Assert.NotNull(AvaloniaXamlLoader.Load(new Uri($"avares://MissionPlanner.Themes/{path}")));
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}

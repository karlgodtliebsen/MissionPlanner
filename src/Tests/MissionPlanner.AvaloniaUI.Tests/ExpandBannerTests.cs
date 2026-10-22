using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MissionPlanner.Themes.Controls;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks that the application loads the banner and its button resources.</summary>
[Collection("Document rendering")]
public sealed class ExpandBannerTests
{
    [Fact]
    public async Task ApplicationResourcesProvideWorkingBannerTemplate()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ParameterEditorToolbarTests));
        try
        {
            await session.Dispatch(() =>
            {
                var banner = new ExpandBanner { Header = "Configuration", CanExpand = true };
                var window = new Window { Width = 900, Height = 300, Content = banner };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var buttons = banner.GetVisualDescendants().OfType<Button>().ToArray();
                    var expand = buttons.Single(button => button.Name == ExpandBanner.PART_ToggleExpandButton);
                    var shrink = buttons.Single(button => button.Name == ExpandBanner.PART_ToggleShrinkButton);
                    Assert.NotNull(expand.Theme);
                    Assert.NotNull(shrink.Theme);
                    Assert.NotNull(expand.Content);
                    Assert.NotNull(shrink.Content);
                    Assert.True(expand.IsVisible);
                    Assert.False(shrink.IsVisible);
                    expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(banner.IsExpanded);
                    Assert.False(expand.IsVisible);
                    Assert.True(shrink.IsVisible);
                    shrink.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(banner.IsExpanded);
                    Assert.True(expand.IsVisible);
                    Assert.False(shrink.IsVisible);

                    // View-model changes must update the buttons without a click.
                    banner.IsExpanded = true;
                    Assert.False(expand.IsVisible);
                    Assert.True(shrink.IsVisible);
                    banner.CanExpand = false;
                    Assert.False(expand.IsEffectivelyVisible);
                    Assert.False(shrink.IsEffectivelyVisible);
                    Assert.True(banner.IsExpanded);
                    banner.CanExpand = true;
                    Assert.True(shrink.IsEffectivelyVisible);
                    shrink.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(banner.IsExpanded);
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

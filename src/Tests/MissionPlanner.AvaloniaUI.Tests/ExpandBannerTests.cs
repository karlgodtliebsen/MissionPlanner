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
                    var toggle = banner.GetVisualDescendants().OfType<Button>()
                        .Single(button => button.Name == ExpandBanner.PART_ToggleExpandButton);
                    var expandTheme = banner.FindResource("OverlayExpandButton");
                    var shrinkTheme = banner.FindResource("OverlayShrinkButton");
                    Assert.Same(expandTheme, toggle.Theme);
                    Assert.NotNull(toggle.Content);
                    Assert.True(toggle.IsVisible);
                    toggle.Focus();
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.UpdateLayout();
                    Assert.True(banner.IsExpanded);
                    Assert.Same(shrinkTheme, toggle.Theme);
                    Assert.True(toggle.IsFocused);
                    Assert.Same(toggle, banner.GetVisualDescendants().OfType<Button>()
                        .Single(button => button.Name == ExpandBanner.PART_ToggleExpandButton));
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(banner.IsExpanded);
                    Assert.Same(expandTheme, toggle.Theme);

                    // View-model changes must update the same button without a click.
                    banner.IsExpanded = true;
                    Assert.Same(shrinkTheme, toggle.Theme);
                    banner.CanExpand = false;
                    Assert.False(toggle.IsEffectivelyVisible);
                    Assert.True(banner.IsExpanded);
                    banner.CanExpand = true;
                    Assert.True(toggle.IsEffectivelyVisible);
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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

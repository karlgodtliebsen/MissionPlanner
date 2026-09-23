using Avalonia.Controls;
using Avalonia.Interactivity;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Isolates Avalonia's process-wide design flag from other tests.</summary>
[CollectionDefinition("Design preview", DisableParallelization = true)]
public sealed class DesignPreviewCollection;

/// <summary>Protects resource-only previews from runtime service resolution and activation.</summary>
[Collection("Design preview")]
public sealed class DesignPreviewTests
{
    /// <summary>View bases can be constructed, loaded and unloaded without an application service provider.</summary>
    [Fact]
    public void DesignViewsDoNotResolveOrActivateRuntimeViewModels()
    {
        var previous = Design.IsDesignMode;
        // Avalonia exposes this setter only to its designer host. Isolate the
        // same flag here without adding test hooks to production view classes.
        var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
        try
        {
            designMode.SetValue(null, true);
            var control = new PreviewControl();
            var page = new PreviewPage();
            var data = new object();
            control.DataContext = data;
            control.Cycle();
            page.Cycle();
            new PreviewTab().Cycle();
            new PreviewTabbedPage().Cycle();
            Assert.Same(data, control.DataContext);
            Assert.Null(page.DataContext);
            Assert.Null(new UserControlViewBase().DataContext);
            Assert.Null(new NavigationViewBase().DataContext);
            Assert.Null(new ContentViewBase<FirmwareHelpViewModel>().DataContext);
            Assert.Null(new ContentViewBase().DataContext);
            Assert.Null(new TabbedPageViewBase().DataContext);
        }
        finally
        {
            designMode.SetValue(null, previous);
        }
    }

    private sealed class PreviewControl : UserControlViewBase<FirmwareHelpViewModel>
    {
        public void Cycle()
        {
            OnLoaded(new RoutedEventArgs(LoadedEvent));
            OnUnloaded(new RoutedEventArgs(UnloadedEvent));
        }
    }

    private sealed class PreviewPage : NavigationViewBase<InstallFirmwareViewModel>
    {
        public void Cycle()
        {
            OnLoaded(new RoutedEventArgs(LoadedEvent));
            OnUnloaded(new RoutedEventArgs(UnloadedEvent));
        }
    }

    private sealed class PreviewTab : TabItemViewBase<FirmwareHelpViewModel>
    {
        public void Cycle()
        {
            OnLoaded(new RoutedEventArgs(LoadedEvent));
            OnUnloaded(new RoutedEventArgs(UnloadedEvent));
        }
    }

    private sealed class PreviewTabbedPage : TabbedPageViewBase<FirmwareHelpViewModel>
    {
        public void Cycle()
        {
            OnLoaded(new RoutedEventArgs(LoadedEvent));
            OnUnloaded(new RoutedEventArgs(UnloadedEvent));
        }
    }
}

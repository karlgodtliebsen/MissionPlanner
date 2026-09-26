using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Diagnostics;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class NavigationSectionTests
{
    [Fact]
    public async Task SectionNavigationSelectsTabAndMenuWithoutNavigatingBackToRoot()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(HardwareRefreshTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var dispatcher = Substitute.For<IUiDispatcher>();
                dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()());
                var factory = Substitute.For<INavigationPageFactory>();
                var configuration = new SectionPage();
                factory.Create(MissionPlannerRoutes.Configuration).Returns(configuration);
                factory.Create(MissionPlannerRoutes.FlightData).Returns(new ContentPage());
                factory.Create(MissionPlannerRoutes.HelpInstallFirmware).Returns(new ContentPage());
                var navigation = new AvaloniaNavigationService(factory, dispatcher);
                using var inspector = new LiveTelemetryInspectorViewModel(
                    Substitute.For<IVehicleLiveDiagnostics>(), Substitute.For<IActiveVehicleContext>(),
                    Substitute.For<ITextClipboardService>(), Substitute.For<IInspectorWindowService>(),
                    TimeProvider.System, dispatcher, Substitute.For<IDomainEventHub>(),
                    NullLogger<LiveTelemetryInspectorViewModel>.Instance);
                using var shell = new MainShellViewModel(navigation, Substitute.For<IWindowProvider>(),
                    Substitute.For<IDomainEventHub>(), inspector);

                await navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
                Assert.Equal("ParametersEditor", configuration.Selected);
                Assert.Equal(MissionPlannerRoutes.Configuration, shell.SelectedMenuItem!.Route);
                Assert.Same(configuration, shell.Content);
                factory.Received(1).Create(MissionPlannerRoutes.Configuration);

                // A user may change tabs manually before following the same shortcut again.
                configuration.SelectSection("OnboardOsd");
                await navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
                Assert.Equal("ParametersEditor", configuration.Selected);
                factory.Received(1).Create(MissionPlannerRoutes.Configuration);

                await navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationOnboardOsd);
                Assert.Equal("OnboardOsd", configuration.Selected);
                Assert.Same(configuration, shell.Content);
                factory.Received(1).Create(MissionPlannerRoutes.Configuration);

                await navigation.PushAsync(new ContentPage());
                await navigation.GoBackAsync();
                Assert.Equal(MissionPlannerRoutes.Configuration, shell.SelectedMenuItem!.Route);
                Assert.Same(configuration, shell.Content);

                await Assert.ThrowsAsync<ArgumentException>(() =>
                    navigation.NavigateAsync(MissionPlannerRoutes.Configuration + "#Missing"));
                Assert.Equal(MissionPlannerRoutes.ConfigurationOnboardOsd, navigation.CurrentRoute);
                Assert.Equal("OnboardOsd", configuration.Selected);

                await navigation.NavigateAsync(MissionPlannerRoutes.HelpInstallFirmware);
                Assert.Equal(MissionPlannerRoutes.Help, shell.SelectedMenuItem!.Route);
                factory.DidNotReceive().Create(MissionPlannerRoutes.Help);
            }, TestContext.Current.CancellationToken);
        }
        finally { await Task.Run(session.Dispose, CancellationToken.None); }
    }

    private sealed class SectionPage : ContentPage, ISectionNavigationPage
    {
        public string? Selected { get; private set; }
        public void SelectSection(string section)
        {
            if (section is not ("ParametersEditor" or "OnboardOsd"))
                throw new ArgumentException("Unknown section.", nameof(section));
            Selected = section;
        }
    }
}

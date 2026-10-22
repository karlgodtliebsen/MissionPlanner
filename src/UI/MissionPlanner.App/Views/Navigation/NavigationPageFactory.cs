using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.AI;
using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup.Advanced;
using MissionPlanner.App.Views.InitSetup.Arming;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.App.Views.InitSetup.OptionalHardware;
using MissionPlanner.App.Views.Preferences;
using MissionPlanner.App.Views.Samples;
using MissionPlanner.App.Views.Simulation;

namespace MissionPlanner.App.Views.Navigation;
/// <summary>
/// Factory class responsible for creating navigation pages based on the provided route.
/// </summary>
public sealed class NavigationPageFactory(IServiceProvider services) : INavigationPageFactory
{
    /// <summary>Creates the page of the route, resolving it from the service container.</summary>
    public Page Create(string route)
    {
        var page = route switch
        {
            MissionPlannerRoutes.DataGridDemo =>
                  services.GetRequiredService<DataGridPage>(),

            MissionPlannerRoutes.DialogDemo =>
                services.GetRequiredService<DialogDemoPage>(),


            MissionPlannerRoutes.Logs => services.GetRequiredService<MissionPlanner.App.Views.Logs.LogsView>(),
            MissionPlannerRoutes.FlightData =>
                services.GetRequiredService<FlightDataPage>(),

            MissionPlannerRoutes.FlightPlanner =>
                services.GetRequiredService<FlightPlannerPage>(),

            MissionPlannerRoutes.SetupInstallFirmware =>
                services.GetRequiredService<InstallFirmwarePage>(),

            MissionPlannerRoutes.SetupMandatoryHardware =>
                  services.GetRequiredService<MandatoryHardwarePage>(),

            MissionPlannerRoutes.SetupOptionalHardware =>
                services.GetRequiredService<OptionalHardwarePage>(),

            MissionPlannerRoutes.SetupArming =>
                services.GetRequiredService<ArmingPage>(),

            MissionPlannerRoutes.SetupAdvanced =>
                services.GetRequiredService<AdvancedPage>(),

            MissionPlannerRoutes.ConfigurationGeoFence => services.GetRequiredService<GeoFenceTabView>(),
            MissionPlannerRoutes.ConfigurationBasicTuning => services.GetRequiredService<BasicTuningTabView>(),
            MissionPlannerRoutes.ConfigurationExtendedTuning => services.GetRequiredService<ExtendedTuningTabView>(),
            MissionPlannerRoutes.ConfigurationParametersEditor => services.GetRequiredService<ParametersEditorTabView>(),
            MissionPlannerRoutes.ConfigurationMavFtp => services.GetRequiredService<MAVFtpTabView>(),
            MissionPlannerRoutes.ConfigurationCubeLan8PortSwitch => services.GetRequiredService<CubeLan8PortSwitchTabView>(),
            MissionPlannerRoutes.ConfigurationOnboardOsd => services.GetRequiredService<OnboardOSDTabView>(),


            MissionPlannerRoutes.Preferences =>
                services.GetRequiredService<PreferencesPage>(),

            MissionPlannerRoutes.Simulation =>
                services.GetRequiredService<SimulationPage>(),


            MissionPlannerRoutes.AI =>
                services.GetRequiredService<AIPage>(),


            MissionPlannerRoutes.Introduction =>
                services.GetRequiredService<HelpPage>(),

            MissionPlannerRoutes.Help =>
                services.GetRequiredService<HelpPage>(),

            MissionPlannerRoutes.HelpInstallFirmware => CreateFirmwareHelpPage(),

            _ when route.StartsWith("SetupAdvanced/", StringComparison.Ordinal) =>
                services.GetRequiredService<AdvancedToolRegistry>().Create(route),

            _ => throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Unknown navigation route.")
        };

        return page;
    }

    private HelpPage CreateFirmwareHelpPage()
    {
        var page = services.GetRequiredService<HelpPage>();
        page.SelectInstallFirmware();
        return page;
    }
}

using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup.Advanced;
using MissionPlanner.App.Views.InitSetup.Arming;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.App.Views.InitSetup.OptionalHardware;
using MissionPlanner.App.Views.Introduction;
using MissionPlanner.App.Views.Preferences;
using MissionPlanner.App.Views.Samples;
using MissionPlanner.App.Views.Simulation;

namespace MissionPlanner.App.Views.Navigation;
/// <summary>
/// Factory class responsible for creating navigation pages based on the provided route.
/// </summary>
public sealed class NavigationPageFactory(IServiceProvider services) : INavigationPageFactory
{
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

            MissionPlannerRoutes.Configuration => services.GetRequiredService<ConfigurationPage>(),
            MissionPlannerRoutes.Preferences =>
                services.GetRequiredService<PreferencesPage>(),

            MissionPlannerRoutes.Simulation =>
                services.GetRequiredService<SimulationPage>(),

            MissionPlannerRoutes.Introduction =>
                services.GetRequiredService<IntroductionPage>(),

            MissionPlannerRoutes.Help =>
                services.GetRequiredService<HelpPage>(),

            _ when route.StartsWith("SetupAdvanced/", StringComparison.Ordinal) =>
                services.GetRequiredService<AdvancedToolRegistry>().Create(route),

            _ => throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Unknown navigation route.")
        };

        return page;
    }
}

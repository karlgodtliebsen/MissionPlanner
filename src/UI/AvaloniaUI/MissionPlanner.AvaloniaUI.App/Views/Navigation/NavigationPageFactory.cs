using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.AvaloniaUI.App.Views.Exit;
using MissionPlanner.AvaloniaUI.App.Views.Config;
using MissionPlanner.AvaloniaUI.App.Views.FlightData;
using MissionPlanner.AvaloniaUI.App.Views.FlightPlanner;
using MissionPlanner.AvaloniaUI.App.Views.Help;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.AvaloniaUI.App.Views.Introduction;
using MissionPlanner.AvaloniaUI.App.Views.Preferences;
using MissionPlanner.AvaloniaUI.App.Views.Simulation;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public sealed class NavigationPageFactory : INavigationPageFactory
{
    private readonly IServiceProvider services;

    public NavigationPageFactory(IServiceProvider services)
    {
        this.services = services;
    }

    public Page Create(string route)
    {
        return route switch
        {
            MissionPlannerRoutes.FlightData =>
                services.GetRequiredService<FlightDataPage>(),

            MissionPlannerRoutes.FlightPlanner =>
                services.GetRequiredService<FlightPlannerPage>(),

            MissionPlannerRoutes.SetupInstallFirmware =>
                services.GetRequiredService<InstallFirmwarePage>(),

            MissionPlannerRoutes.SetupMandatoryHardware =>
                  services.GetRequiredService<MandatoryHardwarePage>(),

            // These route identities are retained for the future Config tab views.
            // Until those AXAML views are migrated, both open the existing Config shell.
            MissionPlannerRoutes.ConfigOnboardOSD or MissionPlannerRoutes.ConfigFullParameters =>
                services.GetRequiredService<ConfigPage>(),

            //MissionPlannerRoutes.SetupInstallFirmware =>
            //    CreateViewPage<InstallFirmwarePage>("Install Firmware"),

            //MissionPlannerRoutes.SetupMandatoryHardware =>
            //    CreateViewPage<MandatoryHardwareView>("Mandatory Hardware"),

            //MissionPlannerRoutes.SetupOptionalHardware =>
            //    CreateViewPage<OptionalHardwareView>("Optional Hardware"),

            //MissionPlannerRoutes.SetupAdvanced =>
            //    CreateViewPage<AdvancedView>("Advanced"),


            //MissionPlannerRoutes.ConfigGeoFence =>
            //    CreateViewPage<GeoFenceTabView>("Geo Fence"),

            //MissionPlannerRoutes.ConfigBasicTuning =>
            //    CreateViewPage<BasicTuningTabView>("Basic Tuning"),

            //MissionPlannerRoutes.ConfigExtendedTuning =>
            //    CreateViewPage<ExtendedTuningTabView>("Extended Tuning"),

            //MissionPlannerRoutes.ConfigOnboardOSD =>
            //    CreateViewPage<OnboardOSDTabView>("Onboard OSD"),

            //MissionPlannerRoutes.ConfigMavFtp =>
            //    CreateViewPage<MAVFtpTabView>("MAV Ftp"),

            //MissionPlannerRoutes.ConfigFullParameters =>
            //    CreateViewPage<FullParametersListTabView>(
            //        "Full Parameters List"),

            //MissionPlannerRoutes.ConfigCubeLan8PortSwitch =>
            //    CreateViewPage<CubeLan8PortSwitchTabView>(
            //        "CubeLan 8 Port Switch"),


            MissionPlannerRoutes.Preferences =>
                services.GetRequiredService<PreferencesPage>(),

            MissionPlannerRoutes.Simulation =>
                services.GetRequiredService<SimulationPage>(),

            MissionPlannerRoutes.Introduction =>
                services.GetRequiredService<IntroductionPage>(),

            MissionPlannerRoutes.Help =>
                services.GetRequiredService<HelpPage>(),

            MissionPlannerRoutes.Exit =>
                CreateViewPage<ExitView>("Exit"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Unknown navigation route.")
        };
    }

    private ContentPage CreateViewPage<TView>(string header) where TView : Control
    {
        return new ContentPage
        {
            Header = header,
            Content = services.GetRequiredService<TView>()
        };
    }
}

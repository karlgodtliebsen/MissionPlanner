using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup.Advanced;
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
        Page page = route switch
        {
            MissionPlannerRoutes.DataGridDemo =>
                  services.GetRequiredService<DataGridPage>(),

            MissionPlannerRoutes.DialogDemo =>
                services.GetRequiredService<DialogDemoPage>(),


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

            MissionPlannerRoutes.SetupAdvanced =>
                services.GetRequiredService<AdvancedPage>(),

            //MissionPlannerRoutes.ConfigGeoFence => CreateViewPage<GeoFenceTabView>("Geo Fence"),
            //MissionPlannerRoutes.ConfigBasicTuning => CreateViewPage<BasicTuningTabView>("Basic Tuning"),
            //MissionPlannerRoutes.ConfigExtendedTuning => CreateViewPage<ExtendedTuningTabView>("Extended Tuning"),
            //MissionPlannerRoutes.ConfigOnboardOSD => CreateViewPage<OnboardOSDTabView>("Onboard OSD"),
            //MissionPlannerRoutes.ConfigMavFtp => CreateViewPage<MAVFtpTabView>("MAV FTP"),
            //MissionPlannerRoutes.ConfigFullParameters => CreateViewPage<FullParametersListTabView>("Full Parameters List"),
            //MissionPlannerRoutes.ConfigCubeLan8PortSwitch => CreateViewPage<CubeLan8PortSwitchTabView>("CubeLAN 8 Port Switch"),

            MissionPlannerRoutes.ConfigGeoFence => services.GetRequiredService<GeoFenceTabView>(),
            MissionPlannerRoutes.ConfigBasicTuning => services.GetRequiredService<BasicTuningTabView>(),
            MissionPlannerRoutes.ConfigExtendedTuning => services.GetRequiredService<ExtendedTuningTabView>(),
            MissionPlannerRoutes.ConfigOnboardOSD => services.GetRequiredService<OnboardOSDTabView>(),
            MissionPlannerRoutes.ConfigMavFtp => services.GetRequiredService<MAVFtpTabView>(),
            MissionPlannerRoutes.ConfigFullParameters => services.GetRequiredService<FullParametersListTabView>(),
            MissionPlannerRoutes.ConfigCubeLan8PortSwitch => services.GetRequiredService<CubeLan8PortSwitchTabView>(),


            MissionPlannerRoutes.Preferences =>
                services.GetRequiredService<PreferencesPage>(),

            MissionPlannerRoutes.Simulation =>
                services.GetRequiredService<SimulationPage>(),

            MissionPlannerRoutes.Introduction =>
                services.GetRequiredService<IntroductionPage>(),

            MissionPlannerRoutes.Help =>
                services.GetRequiredService<HelpPage>(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Unknown navigation route.")
        };

        return page;
    }

    //private ContentPage CreateViewPage<TView>(string header) where TView : Control
    //{
    //    return new ContentPage
    //    {
    //        Header = header,
    //        Content = services.GetRequiredService<TView>()
    //    };
    //}
}

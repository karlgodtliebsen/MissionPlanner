using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.InitSetup.Advanced.Position;
using MissionPlanner.App.Views.InitSetup.Advanced.Privacy;
using MissionPlanner.App.Views.InitSetup.Advanced.Scripting;
using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.DroneCan;
using MissionPlanner.Library.Desktop.Configuration;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Checks production composition of new workflows without constructing controls or opening hardware.</summary>
public sealed class FeatureParityCompositionTests
{
    /// <summary>Workflow view models expected to resolve through the production application factory.</summary>
    public static TheoryData<Type> Workflows => new()
    {
        typeof(CubeIdUpdateViewModel), typeof(Esp8266SetupViewModel), typeof(DroneCanUavCanViewModel),
        typeof(AntennaTrackerViewModel), typeof(SikRadioViewModel), typeof(SikFirmwareViewModel), typeof(RtkBaseSetupViewModel),
        typeof(JoystickViewModel), typeof(CompassPriorityViewModel), typeof(TraditionalHelicopterViewModel),
        typeof(OsdTuningSlotsViewModel), typeof(PidAdjustmentViewModel), typeof(ParameterMaintenanceViewModel),
        typeof(PrivacyExportViewModel), typeof(OperatorPositionViewModel), typeof(ScriptingConsoleViewModel)
    };

    /// <summary>Every direct dependency has a production registration; the operational CAN factory replaces the placeholder.</summary>
    [Theory]
    [MemberData(nameof(Workflows))]
    public void WorkflowsAndTheirConstructorDependenciesAreRegistered(Type workflow)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        var services = new ServiceCollection();
        services.AddApplicationConfiguration(configuration);
        services.AddDesktopServices();
        services.AddLogging();
        Assert.Contains(services, service => service.ServiceType == workflow);
        foreach (var parameter in workflow.GetConstructors().Single().GetParameters())
        {
            var type = parameter.ParameterType;
            Assert.True(parameter.HasDefaultValue || services.Any(service => service.ServiceType == type ||
                type.IsGenericType && service.ServiceType == type.GetGenericTypeDefinition()),
                $"{workflow.Name} requires an unregistered {type.Name}.");
        }
        Assert.Equal(typeof(DroneCanTransportFactory), services.Last(service => service.ServiceType == typeof(IDroneCanTransportFactory)).ImplementationType);
    }
}

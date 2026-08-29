using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.ConfigTuning.Osd;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.ConfigTuning.VendorDevices;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Missions.Validation;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Services;
using MissionPlanner.Shared.Models.Services.Abstractions;
using MissionPlanner.Simulation;
using MissionPlanner.Simulation.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;
using IMavLinkCommandService = MissionPlanner.Core.Services.Abstractions.IMavLinkCommandService;

namespace MissionPlanner.Core.Configuration;

/// <summary>
/// 
/// </summary>
public static class DomainConfigurator
{
    /// <summary>
    /// Adds domain services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to which domain services will be added.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SimulationWorkspaceOptions>(configuration.GetSection(SimulationWorkspaceOptions.SectionName));
        services.Configure<SimulationControlOptions>(configuration.GetSection(SimulationControlOptions.SectionName));
        services.Configure<SimulationScenarioOptions>(configuration.GetSection(SimulationScenarioOptions.SectionName));
        services.Configure<SitlManifestOptions>(configuration.GetSection(SitlManifestOptions.SectionName));
        services.Configure<VehicleMessageStoreOptions>(configuration.GetSection(VehicleMessageStoreOptions.SectionName));
        services.Configure<CalibrationOptions>(configuration.GetSection(CalibrationOptions.SectionName));
        services.Configure<CompassCalibrationOptions>(configuration.GetSection(CompassCalibrationOptions.SectionName));
        services.Configure<FirmwareManifestOptions>(configuration.GetSection(FirmwareManifestOptions.SectionName));
        services.Configure<ParameterEditSessionOptions>(configuration.GetSection(ParameterEditSessionOptions.SectionName));
        services.Configure<ParameterProfileRepositoryOptions>(configuration.GetSection(ParameterProfileRepositoryOptions.SectionName));

        services.TryAddTransient<IMissionTransferService, MissionTransferService>();
        services.TryAddSingleton<IOnboardMissionSnapshotStore, OnboardMissionSnapshotStore>();
        services.TryAddTransient<IMissionProtocolMapper, MissionProtocolMapper>();
        services.TryAddTransient<IMissionValidator, MissionValidator>();
        services.TryAddTransient<IMissionFileCodec, MissionFileCodec>();
        services.TryAddTransient<IAdvancedMissionItemService, AdvancedMissionItemService>();
        services.TryAddTransient<IPlanningPolygonService, PlanningPolygonService>();
        services.TryAddTransient<IGeospatialImportService, GeospatialImportService>();
        services.TryAddSingleton<IAutoWaypointGenerator, AutoWaypointGenerator>();
        services.TryAddSingleton<ISurveyMissionGenerator, SurveyMissionGenerator>();
        services.TryAddTransient<IMissionElevationProfileService, MissionElevationProfileService>();
        services.TryAddSingleton<ITrackerHomeService, TrackerHomeService>();
        services.TryAddSingleton<IRallyProtocolMapper, RallyProtocolMapper>();
        services.TryAddSingleton<IRallyConfigurationService, RallyConfigurationService>();
        services.TryAddSingleton<IRallyPlanFileCodec, RallyPlanFileCodec>();
        services.TryAddSingleton<IFenceProtocolMapper, FenceProtocolMapper>();
        services.TryAddSingleton<IFenceGeometryValidator, FenceGeometryValidator>();
        services.TryAddSingleton<IFencePlanFileCodec, FencePlanFileCodec>();
        services.TryAddSingleton<IFenceConfigurationService, FenceConfigurationService>();
        services.TryAddSingleton<IBasicTuningProfileCatalog, BasicTuningProfileCatalog>();
        services.TryAddTransient<IBasicTuningService, BasicTuningService>();
        services.TryAddSingleton<IExtendedTuningProfileCatalog, ExtendedTuningProfileCatalog>();
        services.TryAddTransient<IExtendedTuningService, ExtendedTuningService>();
        services.TryAddSingleton<IControlResponseMetricsService, ControlResponseMetricsService>();
        services.TryAddTransient<IOsdConfigurationService, OsdConfigurationService>();
        services.TryAddSingleton<IDeviceOperationClient, DeviceOperationClient>();
        services.TryAddSingleton<ICubeLanConfigurationCodec, CubeLanConfigurationCodec>();
        services.TryAddSingleton<IVendorDeviceAdapter<CubeLanConfiguration>, CubeLanDeviceAdapter>();
        services.TryAddSingleton<IVehicleMessagePumpCoordinator, VehicleMessagePumpCoordinator>();

        services.TryAddSingleton<ISimulationVehicleChannelRegistry, SimulationVehicleChannelRegistry>();
        services.TryAddSingleton<ISimulatorVehicleConnectionFactory, SimulatorVehicleConnectionFactory>();
        services.TryAddSingleton<ISimulationDiagnosticsService, SimulationDiagnosticsService>();
        services.TryAddSingleton<ISimulationControlService, SimulationControlService>();
        services.TryAddSingleton<ISimulationScenarioRunner, SimulationScenarioRunner>();
        services.TryAddSingleton<ISimulationFleetAllocator, SimulationFleetAllocator>();
        services.TryAddSingleton<ISimulatorVehicleConnection, SimulatorVehicleConnection>();

        services.TryAddSingleton<ITelemetryLogReader, TelemetryLogReader>();
        services.TryAddSingleton<IReplayTelemetryPipeline, ReplayTelemetryPipeline>();
        services.TryAddSingleton<IReplayDelay, ReplayDelay>();
        services.TryAddSingleton<IReplaySessionManager, ReplaySessionManager>();
        services.TryAddSingleton<IMavLinkTransmissionPolicy, ReplayTransmissionPolicy>();

        services.TryAddTransient<IVehicleMessagePump, VehicleMessagePump>();
        services.TryAddTransient<IVehicleConnectionMonitor, VehicleConnectionMonitor>();

        services.TryAddSingleton<IReplayClock>(provider => provider.GetRequiredService<IReplaySessionManager>());

        // Command ACK correlation must be shared by the command sender and inbound control handler.
        services.TryAddSingleton<ICommandAckTracker, CommandAckTracker>();
        services.TryAddSingleton<IVehicleOperationGate, VehicleOperationGate>();
        services.TryAddTransient<IVehicleCommandPolicy, VehicleCommandPolicy>();
        services.TryAddSingleton<IArduPilotModeCatalog, ArduPilotModeCatalog>();
        services.TryAddSingleton<IVehicleMessageStore, VehicleMessageStore>();
        services.TryAddSingleton<IApplicationNotificationStore, ApplicationNotificationStore>();
        services.TryAddSingleton<ISetupWorkflowCatalog, SetupWorkflowCatalog>();
        services.TryAddTransient<IFrameConfigurationService, FrameConfigurationService>();
        services.TryAddTransient<IArduPilotCalibrationService, ArduPilotCalibrationService>();
        services.TryAddTransient<ICompassConfigurationService, CompassConfigurationService>();
        services.TryAddTransient<IArduPilotCompassCalibrationService, ArduPilotCompassCalibrationService>();
        services.TryAddTransient<IRadioCalibrationService, RadioCalibrationService>();
        services.TryAddTransient<IFlightModeConfigurationService, FlightModeConfigurationService>();
        services.TryAddTransient<IBatteryConfigurationService, BatteryConfigurationService>();
        services.TryAddTransient<IActuatorTestService, ActuatorTestService>();
        services.TryAddTransient<IMotorSpinParameterService, MotorSpinParameterService>();
        services.TryAddTransient<ICompassMotorCalibrationService, CompassMotorCalibrationService>();
        services.TryAddTransient<IDirectSerialSessionFactory, DirectSerialSessionFactory>();
        services.TryAddTransient<ISikRadioConfigurator, SikRadioConfigurator>();
        services.TryAddTransient<IBluetoothSerialConfigurator, BluetoothSerialConfigurator>();
        services.TryAddTransient<IRtkCorrectionSourceFactory, RtkCorrectionSourceFactory>();
        services.TryAddTransient<IRtkInjectionService, RtkInjectionService>();
        services.TryAddSingleton<IDroneCanTransportFactory, UnsupportedDroneCanTransportFactory>();
        services.TryAddTransient<IDroneCanService, DroneCanService>();
        services.TryAddSingleton<IJoystickProvider, UnsupportedJoystickProvider>();
        services.TryAddSingleton<IJoystickVehicleOutput, DisabledJoystickVehicleOutput>();
        services.TryAddSingleton<IFftAnalysisService, FftAnalysisService>();
        services.TryAddTransient<IServoOutputConfigurationService, ServoOutputConfigurationService>();
        services.TryAddSingleton<IMotorOutputResolver, MotorOutputResolver>();
        services.TryAddTransient<IFailSafeService, FailSafeService>();
        services.TryAddTransient<IInitTuneParametersService, InitTuneParametersService>();
        services.TryAddTransient<IHwIdService, HwIdService>();
        services.TryAddTransient<IAdsbService, AdsbService>();

        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, SerialPortsModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, OpticalFlowModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, ParachuteModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, GpsModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, RangefinderModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, AirspeedModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, CanBusModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, CanGpsOrderModule>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IOptionalHardwareModule, CameraGimbalModule>());

        services.AddHttpClient("Firmware");
        services.AddHttpClient("SITL");

        services.TryAddSingleton<IOptionalHardwareCatalog, OptionalHardwareCatalog>();
        services.TryAddTransient<IOptionalHardwareService, OptionalHardwareService>();
        services.TryAddTransient<ISafetyAssessmentService, SafetyAssessmentService>();
        services.TryAddTransient<ISetupSummaryService, SetupSummaryService>();
        services.TryAddSingleton<IFirmwareManifestProvider, JsonFirmwareManifestProvider>();
        services.TryAddTransient<IFirmwarePackageManager, FirmwarePackageManager>();
        services.TryAddSingleton<IFirmwareFlashingService, UnsupportedFirmwareFlashingService>();
        services.TryAddTransient<IFirmwareUpdateCoordinator, FirmwareUpdateCoordinator>();

        services.TryAddSingleton<IVehicleConnectionSession, VehicleConnectionSession>();

        services.TryAddSingleton<IVehicleRegistry, VehicleRegistry>();
        services.TryAddSingleton<IActiveVehicleContext, ActiveVehicleContext>();
        services.TryAddSingleton<IVehicleParameterRegistry, VehicleParameterRegistry>();
        services.TryAddSingleton<IVehicleParameterLoadStatusContext, VehicleParameterLoadStatusContext>();

        services.TryAddTransient<ISerialPortDiscoveryService, SerialPortDiscoveryService>();

        services.TryAddSingleton<IVehicleConnectionService, VehicleConnectionService>();
        services.TryAddSingleton<IVehicleHudDataService, VehicleHudDataService>();
        services.TryAddSingleton<ILocalAltitudeReferenceService, LocalAltitudeReferenceService>();
        services.TryAddSingleton<IVehicleFileSystemService, VehicleFileSystemService>();

        services.TryAddSingleton<IStatusTextHandler, StatusTextHandler>();
        services.TryAddTransient<IParamValueVehicleHandler, ParamValueVehicleHandler>();

        services.TryAddTransient<IVehicleMessageDispatcher, VehicleMessageDispatcher>();

        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, FlightTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, NavigationTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, PowerTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, RadioTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, HealthTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, SensorTelemetryHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IVehicleMessageHandler, ControlMessageHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IVehicleMessageHandler, FlightData.Components.PeripheralComponentHandler>());

        services.TryAddTransient<IVehicleCommandService, VehicleCommandService>();
        services.TryAddTransient<FlightData.Preflight.IPreflightAssessmentService, FlightData.Preflight.PreflightAssessmentService>();
        services.TryAddTransient<FlightData.Preflight.IPreflightCommandService, FlightData.Preflight.PreflightCommandService>();
        services.TryAddSingleton<FlightData.Telemetry.ITelemetryFieldCatalog, FlightData.Telemetry.TelemetryFieldCatalog>();
        services.TryAddSingleton<FlightData.Telemetry.ITelemetrySnapshotProjector, FlightData.Telemetry.TelemetrySnapshotProjector>();
        services.TryAddSingleton<FlightData.Components.VehicleComponentRegistry>();
        services.TryAddSingleton<FlightData.Components.IVehicleComponentRegistry>(provider => provider.GetRequiredService<FlightData.Components.VehicleComponentRegistry>());
        services.TryAddTransient<FlightData.Actuators.IVehicleActuatorService, FlightData.Actuators.VehicleActuatorService>();
        services.TryAddSingleton<FlightData.Auxiliary.IAuxiliaryFunctionCatalog, FlightData.Auxiliary.AuxiliaryFunctionCatalog>();
        services.TryAddTransient<FlightData.Auxiliary.IAuxiliaryFunctionPolicy, FlightData.Auxiliary.AuxiliaryFunctionPolicy>();
        services.TryAddTransient<FlightData.Auxiliary.IAuxiliaryFunctionService, FlightData.Auxiliary.AuxiliaryFunctionService>();
        services.TryAddSingleton<FlightData.Scripting.IVehicleScriptActionRegistry, FlightData.Scripting.VehicleScriptActionRegistry>();
        services.TryAddSingleton<FlightData.Scripting.IVehicleScriptParser, FlightData.Scripting.VehicleScriptParser>();
        services.TryAddTransient<FlightData.Scripting.IVehicleScriptValidator, FlightData.Scripting.VehicleScriptValidator>();
        services.TryAddTransient<FlightData.Scripting.IVehicleScriptExecutor, FlightData.Scripting.VehicleScriptExecutor>();
        services.TryAddTransient<FlightData.Payload.PayloadProtocolService>();
        services.TryAddTransient<FlightData.Payload.ICameraProtocolService>(provider => provider.GetRequiredService<FlightData.Payload.PayloadProtocolService>());
        services.TryAddTransient<FlightData.Payload.IGimbalProtocolService>(provider => provider.GetRequiredService<FlightData.Payload.PayloadProtocolService>());
        services.TryAddTransient<IVehicleService, VehicleService>();

        // MAVLink command sending services
        services.TryAddTransient<IMavLinkCommandService, MavLinkCommandService>();

        // MAVLink parameter services
        services.TryAddTransient<IVehicleParameterService, VehicleParameterService>();
        services.TryAddSingleton<IVehicleParameterMetadataService, VehicleParameterMetadataService>();
        services.TryAddSingleton<IArduPilotPackedParameterDecoder, ArduPilotPackedParameterDecoder>();
        services.TryAddTransient<IVehicleParameterStreamService, VehicleParameterStreamService>();

        services.TryAddSingleton<IParameterValueEquivalence, ParameterValueEquivalence>();
        services.TryAddSingleton<IParameterComparisonService, ParameterComparisonService>();
        services.TryAddSingleton<IParameterProfileRepository, JsonParameterProfileRepository>();
        services.TryAddSingleton<IParameterProfileService, ParameterProfileService>();
        services.TryAddSingleton<IParameterEditSessionFactory, ParameterEditSessionFactory>();

        return services;
    }

    /// <summary>
    /// Configures serviceProvider that are being instantiated through the IDomainFactory. These typical requires constructor arguments, that are not registered in the DI container.
    /// This method registers the domain serviceProvider with the domain factory, allowing them to be created as needed.
    /// 
    /// </summary>
    /// <param name="serviceProvider">The service provider from which IDomainFactory will be resolved.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseDomainServices(this IServiceProvider serviceProvider)
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        domainFactory.Add<IVehicleFileSystemService, VehicleFileSystemService>();

        domainFactory.Add<IHeartbeatVehicleHandler, HeartbeatVehicleHandler>();
        domainFactory.Add<IAttitudeVehicleHandler, AttitudeVehicleHandler>();
        domainFactory.Add<IBatteryVehicleHandler, BatteryVehicleHandler>();
        domainFactory.Add<IPositionVehicleHandler, PositionVehicleHandler>();
        domainFactory.Add<IParamValueVehicleHandler, ParamValueVehicleHandler>();

        domainFactory.Add<IVehicleMessagePump, VehicleMessagePump>();
        domainFactory.Add<ISerialMavLinkTransport, SerialMavLinkTransport>();
        domainFactory.Add<IUdpMavLinkTransport, UdpMavLinkTransport>();
        domainFactory.Add<ITcpMavLinkTransport, TcpMavLinkTransport>();
        domainFactory.Add<IMavLinkClient, MavLinkClient>();
        domainFactory.Add<IMavLinkConnection, MavLinkConnection>();
        domainFactory.Add<IMavLinkCommandService, MavLinkCommandService>();
        domainFactory.Add<IVehicleParameterService, VehicleParameterService>();
        domainFactory.Add<IVehicleParameterStreamService, VehicleParameterStreamService>();
        domainFactory.Add<IParameterEditSession, ParameterEditSession>();


        return serviceProvider;
    }
}

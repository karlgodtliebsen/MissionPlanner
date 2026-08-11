using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <inheritdoc />
public partial class FlightDataMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightDataMissionMapViewModel(IActiveVehicleContext activeVehicle, IMissionProtocolMapper protocolMapper,
        IFileSaver fileSaver, IPlannerSettingsService settingsService, IMissionFileCodec fileCodec,
        IDomainEventHub domainEventHub, IDispatcher dispatcher, IDateTimeProvider dateTimeProvider, ILogger<MissionMapViewModel> logger)
        : base(activeVehicle, protocolMapper, fileSaver, settingsService, fileCodec, domainEventHub, dispatcher, dateTimeProvider, logger)
    {
    }
}

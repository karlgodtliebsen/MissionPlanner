using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Maps.Prefetch;
using MissionPlanner.Maps.Coordinates;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <inheritdoc />
public partial class FlightPlannerMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightPlannerMissionMapViewModel(IActiveVehicleContext activeVehicle, IMissionProtocolMapper protocolMapper, IFileSaver fileSaver,
        IPlannerSettingsService settingsService, IMissionFileCodec fileCodec,
        IDomainEventHub domainEventHub, IDispatcher dispatcher, IDateTimeProvider dateTimeProvider, ILogger<MissionMapViewModel> logger,
        IMissionMapInteractionService interactionService, IAdvancedMissionItemService advancedMissionItems,
        IUserPromptService promptService, IUserConfirmationService confirmationService,
        IPlanningPolygonService polygonService, IFileOpenService fileOpenService, IFileSaveService fileSaveService,
        IUserChoiceService choiceService, IGeospatialImportService geospatialImportService,
        IFenceConfigurationService fenceService, IFencePlanFileCodec fenceFileCodec,
        IRallyConfigurationService rallyService, IRallyPlanFileCodec rallyFileCodec, IAutoWaypointGenerator autoWaypointGenerator,
        ISurveyMissionGenerator surveyMissionGenerator, IMapTilePrefetchService mapTilePrefetchService,
        IMissionElevationProfileService elevationProfileService, IPoiService poiService, ITrackerHomeService trackerHomeService,
        IGeodeticCoordinateConverter geodeticConverter)
        : base(activeVehicle, protocolMapper, fileSaver, settingsService, fileCodec, domainEventHub, dispatcher, dateTimeProvider, logger,
            interactionService, advancedMissionItems, promptService, confirmationService, polygonService, fileOpenService, fileSaveService,
            choiceService, geospatialImportService, fenceService, fenceFileCodec, rallyService, rallyFileCodec, autoWaypointGenerator,
            surveyMissionGenerator, mapTilePrefetchService, elevationProfileService, poiService, trackerHomeService, geodeticConverter)
    {
    }
}

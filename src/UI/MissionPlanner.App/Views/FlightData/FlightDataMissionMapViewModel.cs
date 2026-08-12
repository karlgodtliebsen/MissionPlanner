using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.FlightData;

/// <inheritdoc />
public partial class FlightDataMissionMapViewModel : MissionMapViewModel
{
    /// <inheritdoc />
    public FlightDataMissionMapViewModel(IServiceFactory factory, ILogger<FlightDataMissionMapViewModel> logger) : base(factory, logger)
    {
    }
}


//Dumped to be used later on when refactoring of the many, many services made by AI has been completed ->
//fewer services categorized and refactored into only a few services that can be injected into the MissionMapViewModel constructor.

//IActiveVehicleContext activeVehicle, IMissionProtocolMapper protocolMapper,
//IFileSaver fileSaver, IPlannerSettingsService settingsService,
//IMissionFileCodec fileCodec, IDomainEventHub domainEventHub, IDispatcher Dispatcher,
//IDateTimeProvider dateTimeProvider, ILogger<MissionMapViewModel> logger,
//IMissionMapInteractionService interactionService, IAdvancedMissionItemService advancedMissionItems,
//IUserConfirmationService confirmationService,
//IPlanningPolygonService polygonService, IFileOpenService fileOpenService, IFileSaveService fileSaveService,
//IUserChoiceService choiceService, IGeospatialImportService geospatialImportService,
//IFenceConfigurationService fenceService, IFencePlanFileCodec fenceFileCodec,
//IRallyConfigurationService rallyService, IRallyPlanFileCodec rallyFileCodec,
//IAutoWaypointGenerator autoWaypointGenerator, ISurveyMissionGenerator surveyMissionGenerator,
//IMapTilePrefetchService mapTilePrefetchService, IMissionElevationProfileService elevationProfileService,
//IPoiService poiService, ITrackerHomeService trackerHomeService, IGeodeticCoordinateConverter geodeticConverter,
//IReplaySessionManager replaySession, IExtendedDialogService dialogService


//IActiveVehicleContext activeVehicle, IMissionProtocolMapper protocolMapper,
//IFileSaver fileSaver, IPlannerSettingsService settingsService, IMissionFileCodec fileCodec,
//IDomainEventHub domainEventHub, IDispatcher Dispatcher, IDateTimeProvider dateTimeProvider, ILogger<MissionMapViewModel> logger,
//IMissionMapInteractionService interactionService, IAdvancedMissionItemService advancedMissionItems,
//IUserConfirmationService confirmationService,
//IPlanningPolygonService polygonService, IFileOpenService fileOpenService, IFileSaveService fileSaveService,
//IUserChoiceService choiceService, IGeospatialImportService geospatialImportService,
//IFenceConfigurationService fenceService, IFencePlanFileCodec fenceFileCodec,
//IRallyConfigurationService rallyService, IRallyPlanFileCodec rallyFileCodec, IAutoWaypointGenerator autoWaypointGenerator,
//ISurveyMissionGenerator surveyMissionGenerator, IMapTilePrefetchService mapTilePrefetchService,
//IMissionElevationProfileService elevationProfileService, IPoiService poiService, ITrackerHomeService trackerHomeService,
//IGeodeticCoordinateConverter geodeticConverter, IReplaySessionManager replaySession, IExtendedDialogService dialogService


//activeVehicle, protocolMapper, fileSaver, settingsService, fileCodec, domainEventHub, Dispatcher, dateTimeProvider, logger,
//interactionService, advancedMissionItems, confirmationService, polygonService, fileOpenService, fileSaveService,
//choiceService, geospatialImportService, fenceService, fenceFileCodec, rallyService, rallyFileCodec, autoWaypointGenerator,
//surveyMissionGenerator, mapTilePrefetchService, elevationProfileService, poiService, trackerHomeService, geodeticConverter, replaySession, dialogService

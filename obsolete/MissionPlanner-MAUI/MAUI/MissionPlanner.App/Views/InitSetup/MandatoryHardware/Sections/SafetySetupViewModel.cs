using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using SetupWorkflowDetailViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models.SetupWorkflowDetailViewModel;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects the evidence-based safety assessment into Setup controls.</summary>
public sealed partial class SafetySetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ISafetyAssessmentService safetyService;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<SafetySetupViewModel> logger;

    /// <summary>Initializes the safety Setup workflow.</summary>
    /// <param name="workflowCatalog"></param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="safetyService">The safety assessment service.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public SafetySetupViewModel(
        ISetupWorkflowCatalog workflowCatalog,
        IActiveVehicleContext activeVehicle,
        ISafetyAssessmentService safetyService,
        IVehicleParameterRegistry parameterRegistry,
        IDispatcher dispatcher, ILogger<SafetySetupViewModel> logger)
        : base(workflowCatalog.Workflows.First(w => w.Key == SetupWorkflowKey.Safety), logger)
    {
        this.activeVehicle = activeVehicle;
        this.safetyService = safetyService;
        this.parameterRegistry = parameterRegistry;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the assessed safety checks.</summary>
    public ObservableRangeCollection<SafetyCheckItem> Items { get; } = [];

    /// <summary>Gets the evidence-based warnings.</summary>
    public ObservableRangeCollection<string> Warnings { get; } = [];


    /// <summary>Gets whether any warnings were raised.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        SetMessages("Connect a vehicle to assess safety configuration.");
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnParameterChanged;
        Refresh();
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Cancel();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnParameterChanged;
        return base.DeactivateAsync();
    }

    [RelayCommand]
    private void Refresh()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Items.Clear();
            Warnings.Clear();
            SetMessages("Connect a vehicle to assess safety configuration.");
            //  OnPropertyChanged(nameof(HasWarnings));
            return;
        }

        try
        {
            var assessment = safetyService.BuildAssessment(vehicleId);
            Items.ReplaceRange(assessment.Items);
            Warnings.ReplaceRange(assessment.Warnings);
            SetMessages(assessment.Warnings.Count == 0
                ? "No safety contradictions detected. This is not a safe-to-fly certification."
                : $"{assessment.Warnings.Count} safety item(s) need attention. This is not a safe-to-fly certification.");
            //OnPropertyChanged(nameof(HasWarnings));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Building safety assessment failed.");
            SetMessages(exception);
        }
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            dispatcher.Dispatch(() =>
            {
                //if (IsActive)
                //{
                //    Refresh();
                //}
            });
        }
    }

    private void OnParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(Refresh);
        }
    }
}

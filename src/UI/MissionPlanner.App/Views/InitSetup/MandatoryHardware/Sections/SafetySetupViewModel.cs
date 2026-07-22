using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

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
    /// <param name="descriptor">The safety workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="safetyService">The safety assessment service.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public SafetySetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        ISafetyAssessmentService safetyService,
        IVehicleParameterRegistry parameterRegistry,
        IDispatcher dispatcher,
        ILogger<SafetySetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.safetyService = safetyService;
        this.parameterRegistry = parameterRegistry;
        this.dispatcher = dispatcher;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnParameterChanged;
        Refresh();
    }

    /// <summary>Gets the assessed safety checks.</summary>
    public ObservableCollection<SafetyCheckItem> Items { get; } = [];

    /// <summary>Gets the evidence-based warnings.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Connect a vehicle to assess safety configuration.";

    /// <summary>Gets whether any warnings were raised.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnParameterChanged;
        base.Dispose();
    }

    [RelayCommand]
    private void Refresh()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Items.Clear();
            Warnings.Clear();
            Status = "Connect a vehicle to assess safety configuration.";
            OnPropertyChanged(nameof(HasWarnings));
            return;
        }

        try
        {
            var assessment = safetyService.BuildAssessment(vehicleId);
            Items.Clear();
            foreach (var item in assessment.Items)
            {
                Items.Add(item);
            }

            Warnings.Clear();
            foreach (var warning in assessment.Warnings)
            {
                Warnings.Add(warning);
            }

            Status = assessment.Warnings.Count == 0
                ? "No safety contradictions detected. This is not a safe-to-fly certification."
                : $"{assessment.Warnings.Count} safety item(s) need attention. This is not a safe-to-fly certification.";
            OnPropertyChanged(nameof(HasWarnings));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Building safety assessment failed.");
            Error = exception.Message;
        }
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(Refresh);
    }

    private void OnParameterChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(Refresh);
        }
    }
}

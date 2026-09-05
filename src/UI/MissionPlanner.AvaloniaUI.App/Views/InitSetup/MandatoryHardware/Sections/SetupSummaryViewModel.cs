using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects the consolidated, exportable setup summary into Setup controls.</summary>
public sealed partial class SetupSummaryViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ISetupSummaryService summaryService;
    private SetupSummary? current;

    /// <summary>Initializes the summary Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="summaryService">The setup summary service.</param>
    /// <param name="logger">The logger.</param>
    public SetupSummaryViewModel(
        IActiveVehicleContext activeVehicle,
        ISetupSummaryService summaryService, ILogger<SetupSummaryViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.summaryService = summaryService;
    }

    /// <summary>Gets the summary sections.</summary>
    public ObservableRangeCollection<SetupSummarySectionViewModel> Sections
    {
        get;
    } = [];

    /// <summary>Gets the aggregated warnings.</summary>
    public ObservableRangeCollection<string> Warnings
    {
        get;
    } = [];

    /// <summary>Gets the vehicle heading.</summary>
    [ObservableProperty]
    public partial string Heading
    {
        get;
        private set;
    } = "Connect a vehicle to build a setup summary.";

    /// <summary>Gets the exported report text, when generated.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExport))]
    public partial string? ExportedText
    {
        get;
        private set;
    }

    /// <summary>Gets whether exported report text is available.</summary>
    public bool HasExport => !string.IsNullOrEmpty(ExportedText);

    /// <summary>Gets whether any warnings were raised.</summary>
    public bool HasWarnings => Warnings.Count > 0;


    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        Refresh();
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        return base.DeactivateAsync();
    }

    [RelayCommand]
    private void Refresh()
    {
        ExportedText = null;
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            current = null;
            Sections.Clear();
            Warnings.Clear();
            Heading = "Connect a vehicle to build a setup summary.";
            OnPropertyChanged(nameof(HasWarnings));
            return;
        }

        try
        {
            var summary = summaryService.BuildSummary(vehicleId);
            current = summary;
            Heading = $"{summary.DisplayName} · {summary.Firmware}";
            Sections.Clear();
            foreach (var section in summary.Sections)
            {
                Sections.Add(new SetupSummarySectionViewModel(section));
            }

            Warnings.Clear();
            foreach (var warning in summary.Warnings)
            {
                Warnings.Add(warning);
            }

            OnPropertyChanged(nameof(HasWarnings));
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Building setup summary failed.");
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private void ExportJson()
    {
        if (current is { } summary)
        {
            ExportedText = summaryService.ExportJson(summary);
        }
    }

    [RelayCommand]
    private void ExportMarkdown()
    {
        if (current is { } summary)
        {
            ExportedText = summaryService.ExportMarkdown(summary);
        }
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            Dispatcher.Dispatch(Refresh);
        }
    }
}


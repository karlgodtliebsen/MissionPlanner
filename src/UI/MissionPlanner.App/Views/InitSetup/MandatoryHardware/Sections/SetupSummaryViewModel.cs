using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects the consolidated, exportable setup summary into Setup controls.</summary>
public sealed partial class SetupSummaryViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ISetupSummaryService summaryService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<SetupSummaryViewModel> logger;
    private SetupSummary? current;

    /// <summary>Initializes the summary Setup workflow.</summary>
    /// <param name="descriptor">The summary workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="summaryService">The setup summary service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public SetupSummaryViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        ISetupSummaryService summaryService,
        IDispatcher dispatcher,
        ILogger<SetupSummaryViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.summaryService = summaryService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the summary sections.</summary>
    public ObservableCollection<SetupSummarySectionViewModel> Sections { get; } = [];

    /// <summary>Gets the aggregated warnings.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>Gets the vehicle heading.</summary>
    [ObservableProperty]
    public partial string Heading { get; private set; } = "Connect a vehicle to build a setup summary.";

    /// <summary>Gets the exported report text, when generated.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExport))]
    public partial string? ExportedText { get; private set; }

    /// <summary>Gets whether exported report text is available.</summary>
    public bool HasExport => !string.IsNullOrEmpty(ExportedText);

    /// <summary>Gets whether any warnings were raised.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <inheritdoc />
    protected override void OnActivated()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDeactivated()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.OnDeactivated();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.Dispose();
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
            logger.LogError(exception, "Building setup summary failed.");
            Error = exception.Message;
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

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            dispatcher.Dispatch(() =>
            {
                if (IsActive)
                {
                    Refresh();
                }
            });
        }
    }
}

/// <summary>Presents one summary section for display.</summary>
public sealed class SetupSummarySectionViewModel
{
    /// <summary>Initializes a section view model.</summary>
    /// <param name="section">The summary section.</param>
    public SetupSummarySectionViewModel(SetupSummarySection section)
    {
        Title = section.Title;
        Entries = section.Entries;
    }

    /// <summary>Gets the section title.</summary>
    public string Title { get; }

    /// <summary>Gets the section entries.</summary>
    public IReadOnlyList<SetupSummaryEntry> Entries { get; }
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Aggregates setup completion evidence, live parameters, and safety into an exportable summary.</summary>
public sealed class SetupSummaryService : ISetupSummaryService
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupWorkflowCatalog catalog;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISafetyAssessmentService safetyService;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<SetupSummaryService> logger;

    /// <summary>Initializes the setup-summary service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="catalog">The setup workflow catalog.</param>
    /// <param name="completionStore">The setup completion-evidence store.</param>
    /// <param name="safetyService">The safety assessment service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public SetupSummaryService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        ISetupWorkflowCatalog catalog,
        ISetupCompletionStore completionStore,
        ISafetyAssessmentService safetyService,
        IDateTimeProvider clock,
        ILogger<SetupSummaryService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.catalog = catalog;
        this.completionStore = completionStore;
        this.safetyService = safetyService;
        this.clock = clock;
        this.logger = logger;
    }

    /// <inheritdoc />
    public SetupSummary BuildSummary(VehicleId vehicleId)
    {
        var snapshot = activeVehicle.Current;
        if (snapshot.VehicleId != vehicleId || snapshot.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active vehicle.");
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var firmware = state.Identity.Firmware;
        var sections = new List<SetupSummarySection>();
        var warnings = new List<string>();

        sections.Add(new SetupSummarySection("Identity",
        [
            new SetupSummaryEntry("Vehicle", snapshot.DisplayName, SetupAssessmentStatus.Pass),
            new SetupSummaryEntry("Firmware family", firmware.Family.ToString(), SetupAssessmentStatus.Pass),
            new SetupSummaryEntry("Firmware version", firmware.FlightVersion?.ToString() ?? "Unknown", SetupAssessmentStatus.Pass),
            new SetupSummaryEntry("Board", firmware.BoardVersion.ToString(CultureInfo.InvariantCulture), SetupAssessmentStatus.Pass)
        ]));

        sections.Add(new SetupSummarySection("Frame",
        [
            ParameterEntry("Frame class", parameters, ["FRAME_CLASS", "Q_FRAME_CLASS"]),
            ParameterEntry("Frame type", parameters, ["FRAME_TYPE", "Q_FRAME_TYPE"])
        ]));

        var evaluations = catalog.Evaluate(snapshot, parameters, completionStore.GetAll());
        sections.Add(new SetupSummarySection("Workflows", evaluations
            .Where(evaluation => evaluation.IsVisible)
            .Select(evaluation =>
            {
                var status = MapWorkflowStatus(evaluation.State);
                if (status == SetupAssessmentStatus.Warning)
                {
                    warnings.Add($"{evaluation.Descriptor.Title}: {evaluation.StatusText}");
                }

                return new SetupSummaryEntry(evaluation.Descriptor.Title, evaluation.StatusText, status);
            })
            .ToArray()));

        var assessment = safetyService.BuildAssessment(vehicleId);
        sections.Add(new SetupSummarySection("Safety", assessment.Items
            .Select(item => new SetupSummaryEntry($"{item.Category} · {item.Name}", item.Detail, item.Status))
            .ToArray()));
        warnings.AddRange(assessment.Warnings);

        logger.LogInformation("Built setup summary for {VehicleId} with {Sections} sections.", vehicleId, sections.Count);
        return new SetupSummary(CreateVehicleKey(state), snapshot.DisplayName, DescribeFirmware(state), clock.UtcNow, sections, warnings);
    }

    /// <inheritdoc />
    public string ExportJson(SetupSummary summary) => JsonSerializer.Serialize(summary, jsonOptions);

    /// <inheritdoc />
    public string ExportMarkdown(SetupSummary summary)
    {
        var builder = new StringBuilder();
        builder.Append("# Setup summary — ").AppendLine(summary.DisplayName);
        builder.Append("- Firmware: ").AppendLine(summary.Firmware);
        builder.Append("- Vehicle key: ").AppendLine(summary.VehicleKey);
        builder.Append("- Generated: ").AppendLine(summary.GeneratedAt.ToString("u", CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("This report describes configuration evidence only and is not a certification that the vehicle is safe to fly.");
        builder.AppendLine();

        foreach (var section in summary.Sections)
        {
            builder.Append("## ").AppendLine(section.Title);
            builder.AppendLine("| Item | Value | Status |");
            builder.AppendLine("| --- | --- | --- |");
            foreach (var entry in section.Entries)
            {
                builder.Append("| ").Append(entry.Label).Append(" | ").Append(entry.Value).Append(" | ").Append(entry.Status).AppendLine(" |");
            }

            builder.AppendLine();
        }

        if (summary.Warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            foreach (var warning in summary.Warnings)
            {
                builder.Append("- ").AppendLine(warning);
            }
        }

        return builder.ToString();
    }

    private static SetupSummaryEntry ParameterEntry(string label, IReadOnlyDictionary<string, VehicleParameter> parameters, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            if (parameters.TryGetValue(name, out var parameter))
            {
                return new SetupSummaryEntry(label, parameter.Value.ToString("0.###", CultureInfo.InvariantCulture), SetupAssessmentStatus.Pass);
            }
        }

        return new SetupSummaryEntry(label, "Not available", SetupAssessmentStatus.NotAssessed);
    }

    private static SetupAssessmentStatus MapWorkflowStatus(SetupWorkflowState state) => state switch
    {
        SetupWorkflowState.Completed => SetupAssessmentStatus.Pass,
        SetupWorkflowState.Warning or SetupWorkflowState.Failed => SetupAssessmentStatus.Warning,
        SetupWorkflowState.Unsupported => SetupAssessmentStatus.Unsupported,
        _ => SetupAssessmentStatus.NotAssessed
    };

    private static string DescribeFirmware(VehicleState state)
    {
        var firmware = state.Identity.Firmware;
        return $"{firmware.Family} {firmware.FlightVersion?.ToString() ?? "unknown"} ({firmware.FlightGitHash})";
    }

    private static string CreateVehicleKey(VehicleState state)
    {
        var firmware = state.Identity.Firmware;
        return firmware.HardwareUid2 ?? firmware.HardwareUid?.ToString("X16", CultureInfo.InvariantCulture) ??
            $"{state.VehicleId.SystemId}:{state.VehicleId.ComponentId}";
    }
}

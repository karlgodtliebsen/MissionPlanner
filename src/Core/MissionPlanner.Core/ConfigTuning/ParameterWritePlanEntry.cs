namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Captures one immutable parameter change proposed for writing.</summary>
public sealed record ParameterWritePlanEntry(
    string Name,
    string DisplayName,
    double LiveValue,
    double PendingValue,
    string? Units,
    double Difference,
    bool RebootRequired,
    bool IsReadOnly,
    string? ValidationError);

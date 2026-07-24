using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Projects the editable state of one parameter within a session.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The parameter wire type.</param>
/// <param name="OriginalValue">The value captured when the field first entered the session.</param>
/// <param name="LiveValue">The last confirmed on-vehicle value.</param>
/// <param name="PendingValue">The pending, not-yet-written value.</param>
/// <param name="Metadata">The firmware metadata.</param>
/// <param name="ValidationError">The current validation error, when invalid.</param>
/// <param name="WriteStatus">The current write state.</param>
/// <param name="WriteMessage">The latest write-state explanation.</param>
public sealed record ParameterEditField(
    string Name,
    MavParamType Type,
    double OriginalValue,
    double LiveValue,
    double PendingValue,
    ParameterFieldMetadata Metadata,
    string? ValidationError,
    ParameterEditWriteStatus WriteStatus = ParameterEditWriteStatus.Unchanged,
    string? WriteMessage = null)
{
    /// <summary>Gets whether the pending value differs from the live value.</summary>
    public bool IsModified
    {
        get
        {
            const double tolerance = 0.0001;
            var scale = Math.Max(1, Math.Max(Math.Abs(PendingValue), Math.Abs(LiveValue)));
            return Math.Abs(PendingValue - LiveValue) > tolerance * scale;
        }
    }

    /// <summary>Gets whether the pending value is valid.</summary>
    public bool IsValid => ValidationError is null;
}

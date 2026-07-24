namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Identifies the current write state of an editable parameter.</summary>
public enum ParameterEditWriteStatus
{
    /// <summary>The field has no pending change.</summary>
    Unchanged,

    /// <summary>The field has an unapplied pending value.</summary>
    Pending,

    /// <summary>The pending value is being written.</summary>
    Applying,

    /// <summary>The pending value was confirmed by live readback.</summary>
    Confirmed,

    /// <summary>The pending value could not be written or confirmed.</summary>
    Failed
}

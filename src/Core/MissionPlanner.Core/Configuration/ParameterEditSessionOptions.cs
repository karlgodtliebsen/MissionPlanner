namespace MissionPlanner.Core.Configuration;

/// <summary>Configures confirmed parameter editing behavior.</summary>
public sealed class ParameterEditSessionOptions
{
    /// <summary>Gets the application configuration section name.</summary>
    public const string SectionName = "ParameterEditing";

    /// <summary>Gets or sets how long a write may wait for matching registry readback.</summary>
    public TimeSpan ReadbackTimeout { get; set; } = TimeSpan.FromSeconds(3);
}

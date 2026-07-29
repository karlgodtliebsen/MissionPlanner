namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Configures local parameter profile persistence.</summary>
public sealed class ParameterProfileRepositoryOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ParameterProfiles";

    /// <summary>Optional storage directory; defaults to local application data.</summary>
    public string? Directory { get; set; }
}

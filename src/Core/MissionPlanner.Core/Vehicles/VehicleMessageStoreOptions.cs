namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Configures bounded vehicle status-text histories and chunk assembly.
/// </summary>
public sealed class VehicleMessageStoreOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "VehicleMessages";

    /// <summary>Gets or sets the maximum messages retained for each vehicle.</summary>
    public int Capacity { get; set; } = 500;

    /// <summary>Gets or sets the time allowed for the next MAVLink 2 text chunk.</summary>
    public TimeSpan ChunkTimeout { get; set; } = TimeSpan.FromSeconds(2);
}

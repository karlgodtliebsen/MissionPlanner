using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Provides curated Basic Tuning profiles by firmware family.</summary>
public interface IBasicTuningProfileCatalog
{
    /// <summary>Gets the profile for a supported firmware family.</summary>
    /// <param name="family">The connected firmware family.</param>
    /// <returns>The profile, or <see langword="null"/> when Basic Tuning is unsupported.</returns>
    BasicTuningProfile? GetProfile(FirmwareFamily family);
}

using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Provides and expands structured advanced tuning descriptors.</summary>
public interface IExtendedTuningProfileCatalog
{
    /// <summary>Gets the profile for a supported firmware family.</summary>
    /// <param name="family">The firmware family.</param>
    /// <returns>The profile, or <see langword="null"/> when unsupported.</returns>
    ExtendedTuningProfile? GetProfile(FirmwareFamily family);

    /// <summary>Expands a repeated axis/instance descriptor into exact parameter fields.</summary>
    /// <param name="descriptor">The descriptor to expand.</param>
    /// <returns>The expanded fields.</returns>
    IReadOnlyList<AdvancedTuningFieldDefinition> Expand(AdvancedTuningDescriptor descriptor);
}

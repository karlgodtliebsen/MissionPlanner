using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.Library.Desktop;

/// <summary>Reports the capabilities of a desktop platform to the Advanced tools.</summary>
public sealed class DesktopAdvancedCapabilities : AdvancedPlatformCapabilitySource
{
    /// <summary>Creates the capability source for a desktop platform.</summary>
    /// <param name="platform">The platform name shown to the operator.</param>
    /// <param name="location">Whether the platform can provide the device location.</param>
    /// <param name="secureKeyStorage">Whether the platform provides a secure store for secrets.</param>
    public DesktopAdvancedCapabilities(string platform, bool location, bool secureKeyStorage)
    {
        Update(new(platform, FileOpen: true, FileSave: true, SerialOutput: true,
            NetworkOutput: true, Location: location, SecureKeyStorage: secureKeyStorage, SessionKeyStorage: true));
    }
}

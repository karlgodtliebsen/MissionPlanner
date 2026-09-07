using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.Library.Windows;

/// <summary>Reports Windows capabilities without opening ports or prompting for location.</summary>
public sealed class WindowsAdvancedCapabilities : AdvancedPlatformCapabilitySource
{
    /// <summary>Initializes capabilities supplied by the Windows host.</summary>
    public WindowsAdvancedCapabilities()
    {
        Update(new("Windows", FileOpen: true, FileSave: true, SerialOutput: true,
            NetworkOutput: true, Location: true, SecureKeyStorage: true, SessionKeyStorage: true));
    }
}

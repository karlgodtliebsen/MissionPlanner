using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.Library.Browser;

/// <summary>Reports browser support; vehicle connectivity is not output-bridge authorization.</summary>
public sealed class BrowserAdvancedCapabilities : AdvancedPlatformCapabilitySource
{
    /// <summary>Initializes capabilities without triggering browser permission prompts.</summary>
    public BrowserAdvancedCapabilities()
    {
        Update(new("Browser", IsBrowser: true, FileOpen: true, FileSave: true,
            Location: true, SessionKeyStorage: true));
    }
}

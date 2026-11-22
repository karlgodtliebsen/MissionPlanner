using System.Runtime.CompilerServices;
using Material.Icons;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Configures process-wide dependencies before headless UI sessions start.</summary>
internal static class TestAssemblyInitialization
{
    /// <summary>Prevents cached icon geometry from retaining a previous session's dispatcher.</summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Each headless session owns a different UI thread. Material Icons' process-wide
        // geometry cache is appropriate for the application, but cannot span these sessions.
        MaterialIconDataProvider.DisableCache();
    }
}

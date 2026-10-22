using Avalonia;

namespace MissionPlanner.Library.MacOs.Configuration;

/// <summary>Configures the native Avalonia backend for the macOS host.</summary>
public static class MacOsAvaloniaConfigurator
{
    /// <summary>Selects the native macOS backend and, in debug builds, enables the developer tools.</summary>
    /// <param name="builder">The Avalonia application builder.</param>
    /// <returns>The builder.</returns>
    public static AppBuilder UseMacOsPlatform(this AppBuilder builder)
    {
        builder.UsePlatformDetect();
#if DEBUG
        builder.WithDeveloperTools();
#endif
        return builder;
    }
}

using Avalonia;

namespace MissionPlanner.Library.Linux.Configuration;

/// <summary>Configures the native Avalonia backend for the Linux host.</summary>
public static class LinuxAvaloniaConfigurator
{
    /// <summary>Selects the X11 backend (also used under XWayland) and, in debug builds, enables the developer tools.</summary>
    /// <param name="builder">The Avalonia application builder.</param>
    /// <returns>The builder.</returns>
    public static AppBuilder UseLinuxPlatform(this AppBuilder builder)
    {
        builder.UsePlatformDetect();
#if DEBUG
        builder.WithDeveloperTools();
#endif
        return builder;
    }
}

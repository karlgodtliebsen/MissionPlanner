using Avalonia;
using Avalonia.Dialogs;

namespace MissionPlanner.Library.Windows.Configuration;

/// <summary>Configures the native Avalonia backend for the Windows host.</summary>
public static class WindowsAvaloniaConfigurator
{
    public static AppBuilder UseWindowsPlatform(this AppBuilder builder)
    {
        builder.UseManagedSystemDialogs().UsePlatformDetect();
#if DEBUG
        builder.WithDeveloperTools();
#endif
        return builder;
    }
}

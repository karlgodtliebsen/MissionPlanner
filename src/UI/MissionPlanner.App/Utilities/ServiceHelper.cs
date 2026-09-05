using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace MissionPlanner.App.Utilities;

/// <summary>
/// Helper class for retrieving services from the Avalonia application's service provider.
/// </summary>
public static class ServiceHelper
{
    /// <summary>
    /// Retrieves a required service from the Avalonia application's service provider.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <returns>The requested service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Avalonia app is not initialized.</exception>
    public static T GetRequiredService<T>() where T : notnull
    {
        return ((App)Application.Current!).ServiceProvider.GetRequiredService<T>()
               ?? throw new InvalidOperationException("AvaloniaUI app not initialized.");
    }

    /// <summary>
    /// Retrieves a required keyed service from the Avalonia application's service provider.
    /// </summary>
    /// <param name="key">The key associated with the service.</param>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <returns>The requested service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Avalonia app is not initialized.</exception>
    public static T GetRequiredKeyedService<T>(string key) where T : notnull
    {
        return ((App)Application.Current!).ServiceProvider.GetRequiredKeyedService<T>(key)
               ?? throw new InvalidOperationException("AvaloniaUI app not initialized.");
    }
}

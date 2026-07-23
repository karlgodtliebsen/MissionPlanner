namespace MissionPlanner.App.Helpers;

/// <summary>
/// Helper class for retrieving services from the MAUI application's service provider.
/// </summary>
public static class ServiceHelper
{
    /// <summary>
    /// Retrieves a required service from the MAUI application's service provider.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <returns>The requested service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the MAUI app is not initialized.</exception>
    public static T GetRequiredService<T>() where T : notnull
    {
        return IPlatformApplication.Current!.Services.GetRequiredService<T>()
               ?? throw new InvalidOperationException("MAUI app not initialized.");
    }
}

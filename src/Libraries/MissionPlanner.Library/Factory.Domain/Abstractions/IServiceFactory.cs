namespace Domain.Library.Factory.Domain.Abstractions;

/// <summary>
/// A factory interface for creating instances of services.
/// </summary>
public interface IServiceFactory
{
    /// <summary>
    /// Creates an instance of the specified service type.
    /// </summary>
    /// <typeparam name="TService">The type of the service to create.</typeparam>
    /// <returns>An instance of the specified service type.</returns>
    TService Create<TService>() where TService : notnull;

    /// <summary>
    /// Creates an instance of the specified service type using the provided key.
    /// </summary>
    /// <typeparam name="TService">The type of the service to create.</typeparam>
    /// <param name="key">The key to identify the service.</param>
    /// <returns>An instance of the specified service type.</returns>
    TService Create<TService>(string key) where TService : notnull;

    /// <summary>
    /// Creates a scoped instance of the specified service type.
    /// </summary>
    /// <typeparam name="TService">The type of the service to create.</typeparam>
    /// <returns>A scoped instance of the specified service type.</returns>
    TService CreateScoped<TService>() where TService : notnull;

    /// <summary>
    /// Creates a scoped instance of the specified service type using the provided key.
    /// </summary>
    /// <typeparam name="TService">The type of the service to create.</typeparam>
    /// <param name="key">The key to identify the service.</param>
    /// <returns>A scoped instance of the specified service type.</returns>
    TService CreateScoped<TService>(string key) where TService : notnull;
}

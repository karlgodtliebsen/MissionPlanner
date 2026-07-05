using Domain.Library.Factory.Domain.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Domain.Library.Factory.Domain;

/// <summary>
/// 
/// </summary>
/// <param name="serviceProvider"></param>
public class ServiceFactory(IServiceProvider serviceProvider) : IServiceFactory
{
    /// <summary>
    /// Creates an instance of the specified service type.
    /// </summary>
    /// <typeparam name="TService">The type of the service to create.</typeparam>
    /// <returns>An instance of the specified service type.</returns>
    public TService Create<TService>() where TService : notnull
    {
        return serviceProvider.GetRequiredService<TService>();
    }

    /// <inheritdoc/>
    public TService Create<TService>(string key) where TService : notnull
    {
        return serviceProvider.GetRequiredKeyedService<TService>(key);
    }

    /// <inheritdoc/>
    public TService CreateScoped<TService>() where TService : notnull
    {
        return serviceProvider.CreateScope().ServiceProvider.GetRequiredService<TService>();
    }

    /// <inheritdoc/>
    public TService CreateScoped<TService>(string key) where TService : notnull
    {
        return serviceProvider.CreateScope().ServiceProvider.GetRequiredKeyedService<TService>(key);
    }
}

using Domain.Library.DateTime.Domain;
using Domain.Library.EventHub.Abstractions;
using Domain.Library.Factory.Domain;
using Domain.Library.Factory.Domain.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Domain.Library.Configuration;

/// <summary>
///   Configures the domain factories for dependency injection.
/// </summary>
public static class LibraryConfiguration
{
    /// <summary>
    /// Adds the domain factories to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the factories to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddLibraryServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IDomainFactory, DomainFactory>();
        services.TryAddSingleton<IFactory, ActivatorFactory>();
        services.TryAddSingleton<IServiceFactory, ServiceFactory>();
        services.TryAddSingleton<IEventHub, EventHub.EventHub>();
        services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        return services;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddLoggingServices(this IServiceCollection services)
    {
        return services;
    }
}

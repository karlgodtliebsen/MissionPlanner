using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Library.Configuration;

/// <summary>
/// EventHubConfigurator
/// </summary>
public static class EventHubConfigurator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddEventHubServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventHub, EventHub.EventHub>();
        services.TryAddSingleton<IDomainEventHub, DomainEventHub>();
        return services;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static IServiceCollection AddEventHubServices(this IServiceCollection services, string key)
    {
        services.TryAddSingleton<IEventHub, EventHub.EventHub>();
        services.TryAddKeyedSingleton<IEventHub, EventHub.EventHub>(key);
        services.TryAddSingleton<IDomainEventHub, DomainEventHub>();
        services.TryAddKeyedSingleton<IDomainEventHub, DomainEventHub>(key);
        return services;
    }
}

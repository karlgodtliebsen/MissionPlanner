using Domain.Library.EventHub.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Domain.Library.Configuration;

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
        return services;
    }
}

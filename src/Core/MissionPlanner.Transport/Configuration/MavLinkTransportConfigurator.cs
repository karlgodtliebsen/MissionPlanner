using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Transport.Configuration;

/// <summary>
/// 
/// </summary>
public static class MavLinkTransportConfigurator
{
    /// <summary>
    /// Adds MAVLink Transport services and dependencies to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to which MAVLink Transport services will be added.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMavLinkTransportServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddTransient<IMavLinkTransport, UdpMavLinkTransport>();

        //TODO: must be based on configuration data
        services.AddSingleton(Options.Create(new TransportEndpoint()));

        return services;
    }

    /// <summary>
    /// Configures and initializes MAVLink Transport services using the specified <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve dependencies.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseMavLinkTransportServices(this IServiceProvider serviceProvider)
    {
        //var factory = serviceProvider.GetRequiredService<IDomainFactory>();
        //factory.Add<IMavLinkTransport, UdpMavLinkTransport>();

        return serviceProvider;
    }
}
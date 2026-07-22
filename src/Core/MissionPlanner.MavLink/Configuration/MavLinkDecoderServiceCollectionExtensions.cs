using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Configuration;

/// <summary>
/// Registers the deterministic generated and declared-custom MAVLink decoder catalog.
/// </summary>
public static class MavLinkDecoderServiceCollectionExtensions
{
    /// <summary>
    /// Adds the complete generated MAVLink decoder catalog and decoding facade.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddGeneratedMavLinkDecoders(this IServiceCollection services)
    {
        services.TryAddSingleton<IMavLinkMessageDecoderCatalog, MavLinkMessageDecoderCatalog>();
        services.TryAddSingleton(provider => new MavLinkMessageDecoders(
            provider.GetRequiredService<IMavLinkMessageDecoderCatalog>().Decoders,
            provider.GetRequiredService<IMavLinkMessageDefinitionRegistry>()));
        return services;
    }
}

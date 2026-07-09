using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Parameters.Metadata;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Configuration;

/// <summary>
/// Configures MAVLink services and dependencies.
/// </summary>
public static class MavLinkConfigurator
{
    /// <summary>
    /// Adds MAVLink services and dependencies to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to which MAVLink services will be added.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMavLinkServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IMavLinkClient, MavLinkClient>();
        services.TryAddSingleton<IMavLinkConnection, MavLinkConnection>();
        //services.AddMavLinkPipeline();
        services.TryAddTransient<IMavLinkCrcExtraProvider, CommonMavLinkCrcExtraProvider>();
        services.TryAddTransient<IMavLinkFrameParser, MavLinkV2FrameParser>();
        services.TryAddTransient<IMavLinkCommandEncoder, MavLinkCommandEncoder>();
        services.TryAddTransient<IMavLinkParameterEncoder, MavLinkParameterEncoder>();
        services.TryAddTransient<IMavLinkMessageDecoder, MavLinkMessageDecoder>();

        services.AddSingleton(Options.Create(new MavLinkClientPipelineOptions()));
        services.AddSingleton(Options.Create(new MavLinkConnectionPipelineOptions()));

        //IList<IMavLinkMessageDecoder> decoders =
        //[
        //    new StatusTextMessageDecoder(),
        //    new HeartbeatMessageDecoder(),
        //    new CommandAckMessageDecoder(),
        //    new AttitudeMessageDecoder(),
        //    new GlobalPositionIntMessageDecoder(),
        //    new SysStatusMessageDecoder(),
        //    new ParamValueMessageDecoder(),
        //    new RawMavLinkMessageDecoder()
        //];

        IList<IMavLinkMessageDecoder> decoders =
        [
            new StatusTextMessageDecoder(),
            new HeartbeatMessageDecoder(),
            new CommandAckMessageDecoder(),
            new AttitudeMessageDecoder(),
            new GlobalPositionIntMessageDecoder(),
            new SysStatusMessageDecoder(),
            new ParamValueMessageDecoder(),

            new GpsRawIntMessageDecoder(),
            new RawImuMessageDecoder(),
            new ScaledPressureMessageDecoder(),

            new RawMavLinkMessageDecoder()
        ];


        services.TryAddSingleton(new MavLinkMessageDecoders(decoders));

        // Parameter metadata services
        services.TryAddSingleton<IParameterMetadataParser, ParameterMetadataXmlParser>();
        services.TryAddSingleton<IParameterMetadataDownloader, ParameterMetadataDownloader>();
        services.TryAddSingleton<IParameterMetadataRepository, ParameterMetadataRepository>();

        // HttpClient for parameter metadata downloads
        services.AddHttpClient("ParameterMetadata")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "MissionPlanner-NextGen/1.0");
            });

        return services;
    }

    //private static IServiceCollection AddMavLinkPipeline(
    //    this IServiceCollection services,
    //    Action<MavLinkClientPipelineOptions>? configureClient = null,
    //    Action<MavLinkConnectionPipelineOptions>? configureConnection = null)
    //{
    //    services.AddOptions<MavLinkClientPipelineOptions>()
    //        .Configure(options => configureClient?.Invoke(options))
    //        .Validate(options =>
    //        {
    //            try
    //            {
    //                options.Validate();
    //                return true;
    //            }
    //            catch
    //            {
    //                return false;
    //            }
    //        }, "Invalid MAVLink client pipeline options.");

    //    services.AddOptions<MavLinkConnectionPipelineOptions>()
    //        .Configure(options => configureConnection?.Invoke(options))
    //        .Validate(options =>
    //        {
    //            try
    //            {
    //                options.Validate();
    //                return true;
    //            }
    //            catch
    //            {
    //                return false;
    //            }
    //        }, "Invalid MAVLink connection pipeline options.");

    //    return services;
    //}

    /// <summary>
    /// Configures MAVLink services and dependencies using the specified <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="services">The service provider to which MAVLink services will be added.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseMavLinkServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        //domainFactory.Add<IMavLinkMessageDecoder, MavLinkMessageDecoder>();
        //domainFactory.Add<IMavLinkClient, MavLinkClient>();
        return services;
    }
}

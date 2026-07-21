using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Parameters.Metadata;
using MissionPlanner.MavLink.Parameters.Metadata.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

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
        services.TryAddTransient<IMavLinkCrcExtraProvider, CommonMavLinkCrcExtraProvider>();
        services.TryAddTransient<IMavLinkFrameParser, MavLinkV2FrameParser>();
        services.TryAddTransient<IMavLinkCommandEncoder, MavLinkCommandEncoder>();
        services.TryAddTransient<IMavLinkParameterEncoder, MavLinkParameterEncoder>();
        services.TryAddTransient<IMavLinkMessageDecodeHandler, MavLinkMessageDecoderHandler>();
        services.TryAddTransient<IMavLinkMissionEncoder, MavLinkMissionEncoder>();
        services.TryAddSingleton<IMavFtpPacketCodec, MavFtpPacketCodec>();
        services.TryAddSingleton<IMavFtpMessageEncoder, MavFtpMessageEncoder>();
        services.TryAddSingleton<IMavFtpResponseDispatcher, MavFtpResponseDispatcher>();
        services.TryAddSingleton<IMavFtpSequenceStore, MavFtpSequenceStore>();
        services.TryAddSingleton<IMavFtpClient, MavFtpClient>();
        services.AddSingleton(Options.Create(new MavFtpOptions()));

        services.AddSingleton(Options.Create(new MavLinkClientPipelineOptions()));
        services.AddSingleton(Options.Create(new MavLinkConnectionPipelineOptions()));
        IList<IMavLinkMessageDecoder> decoders =
        [
            new Ahrs2MessageDecoder(),
            new AttitudeMessageDecoder(),
            new AutopilotVersionMessageDecoder(),
            new BatteryStatusMessageDecoder(),
            new CommandAckMessageDecoder(),
            new EkfStatusReportMessageDecoder(),
            new FileTransferProtocolMessageDecoder(),
            new GlobalPositionIntMessageDecoder(),
            new GpsRawIntMessageDecoder(),
            new HeartbeatMessageDecoder(),
            new HomePositionMessageDecoder(),
            new LocalPositionNedMessageDecoder(),
            new MemInfoMessageDecoder(),
            new MissionCurrentMessageDecoder(),
            new MissionAckMessageDecoder(),
            new MissionCountMessageDecoder(),
            new MissionItemIntMessageDecoder(),
            new MissionItemReachedMessageDecoder(),
            new MissionRequestIntMessageDecoder(),
            new NavControllerOutputMessageDecoder(),
            new ParamValueMessageDecoder(),
            new PowerStatusMessageDecoder(),
            new RawImuMessageDecoder(),
            new RcChannelsMessageDecoder(),
            new ScaledPressureMessageDecoder(),
            new ServoOutputRawMessageDecoder(),
            new StatusTextMessageDecoder(),
            new SysStatusMessageDecoder(),
            new TimeSyncMessageDecoder(),
            new VfrHudMessageDecoder(),
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

    /// <summary>
    /// Configures MAVLink services and dependencies using the specified <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="services">The service provider to which MAVLink services will be added.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseMavLinkServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        domainFactory.Add<IMavFtpClient, MavFtpClient>();
        return services;
    }
}

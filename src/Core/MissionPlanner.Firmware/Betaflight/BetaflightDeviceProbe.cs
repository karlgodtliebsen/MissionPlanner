using MissionPlanner.Firmware.Betaflight.Protocol;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Reads identity only after exact BTFL proof, tolerating unavailable optional commands.</summary>
public sealed class BetaflightDeviceProbe(MspPortConnector connector, IBetaflightMspClient client,
    Microsoft.Extensions.Options.IOptions<BetaflightOptions>? options = null) : IBetaflightDeviceProbe
{
    /// <inheritdoc />
    public async Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
    {
        await using var connection = await connector.OpenAsync(portName, cancellationToken).ConfigureAwait(false);
        if (connection.Port is not { } port)
        {
            return Failed(connection.Failure);
        }
        async Task<MspResponse> Query(ushort command) => await client.RequestAsync(port, command,
            ReadOnlyMemory<byte>.Empty, options?.Value.RequestTimeout ?? TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

        var apiReply = await Query(MspCommand.ApiVersion).ConfigureAwait(false);
        if (apiReply.Failure != MspFailure.None)
        {
            return apiReply.Failure == MspFailure.Timeout ? new(BetaflightProbeOutcome.NotMsp, Failure: apiReply.Failure) : Failed(apiReply.Failure);
        }
        var apiBytes = apiReply.Frame!.Payload;
        if (apiBytes.Length != 3)
        {
            return new(BetaflightProbeOutcome.ProtocolError);
        }
        if (apiBytes[0] != 0 || apiBytes[1] != 1)
        {
            return new(BetaflightProbeOutcome.UnsupportedApi);
        }
        var variantReply = await Query(MspCommand.FcVariant).ConfigureAwait(false);
        if (variantReply.Failure != MspFailure.None)
        {
            return Failed(variantReply.Failure);
        }
        if (!variantReply.Frame!.Payload.AsSpan().SequenceEqual("BTFL"u8))
        {
            return new(BetaflightProbeOutcome.MspButNotBetaflight);
        }
        var identity = new BetaflightDeviceInfo(portName, new Version(apiBytes[1], apiBytes[2]), "BTFL");
        var incomplete = false;
        foreach (var command in new[] { MspCommand.FcVersion, MspCommand.BoardInfo, MspCommand.Uid,
                     MspCommand.BuildInfo, MspCommand.Name, MspCommand.McuInfo })
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new(BetaflightProbeOutcome.Cancelled);
            }
            if (!port.IsOpen)
            {
                incomplete = true;
                break;
            }
            var response = await Query(command).ConfigureAwait(false);
            if (response.Failure == MspFailure.Cancelled)
            {
                return new(BetaflightProbeOutcome.Cancelled);
            }
            if (response.Failure != MspFailure.None)
            {
                incomplete = true;
                continue;
            }
            var data = response.Frame!.Payload;
            switch (command)
            {
                case MspCommand.FcVersion when data.Length >= 3:
                    var offset = 3;
                    BetaflightIdentityParser.ReadString(data, ref offset, out var label);
                    identity = identity with { FirmwareVersion = new Version(data[0], data[1], data[2]), FirmwareVersionLabel = label };
                    break;
                case MspCommand.BoardInfo:
                    var board = BetaflightIdentityParser.ParseBoard(data, identity.MspApiVersion);
                    incomplete |= board is null;
                    identity = identity with { Board = board };
                    break;
                case MspCommand.Uid when data.Length == 12:
                    identity = identity with { McuUniqueId = Convert.ToHexString(data) };
                    break;
                case MspCommand.BuildInfo when data.Length >= 26:
                    identity = identity with { BuildInformation = BetaflightIdentityParser.Text(data.AsSpan(0, 19)),
                        SourceRevision = BetaflightIdentityParser.Text(data.AsSpan(19, 7)) };
                    break;
                case MspCommand.Name:
                    identity = identity with { CraftName = BetaflightIdentityParser.Text(data) };
                    break;
                case MspCommand.McuInfo when data.Length >= 2:
                    var mcuOffset = 1;
                    if (BetaflightIdentityParser.ReadString(data, ref mcuOffset, out var mcu))
                    {
                        identity = identity with { McuType = mcu };
                    }
                    break;
                default:
                    incomplete = true;
                    break;
            }
        }
        return new(BetaflightProbeOutcome.Success, identity,
            Diagnostic: incomplete ? "Betaflight identified; some optional identity details were unavailable." : null);
    }

    private static BetaflightProbeResult Failed(MspFailure failure) => new(failure switch
    {
        MspFailure.Cancelled => BetaflightProbeOutcome.Cancelled,
        MspFailure.Timeout => BetaflightProbeOutcome.Timeout,
        MspFailure.Disconnected => BetaflightProbeOutcome.Disconnected,
        MspFailure.PortUnavailableOrBusy => BetaflightProbeOutcome.PortBusy,
        MspFailure.Unsupported => BetaflightProbeOutcome.UnsupportedApi,
        _ => BetaflightProbeOutcome.ProtocolError
    }, Failure: failure);
}

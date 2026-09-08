using System.Text;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Betaflight.Protocol;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Tests;

public sealed class BetaflightIdentityTests
{
    [Theory]
    [InlineData("BTFL", BetaflightProbeOutcome.Success)]
    [InlineData("INAV", BetaflightProbeOutcome.MspButNotBetaflight)]
    [InlineData("CLFL", BetaflightProbeOutcome.MspButNotBetaflight)]
    [InlineData("BTFL ", BetaflightProbeOutcome.MspButNotBetaflight)]
    public async Task ExactVariantProofAndOptionalFailures(string variant, BetaflightProbeOutcome expected)
    {
        var client = new FakeClient(variant);
        var ports = new FakePorts();
        var probe = new BetaflightDeviceProbe(new MspPortConnector(ports, TimeProvider.System), client);
        var result = await probe.ProbeAsync("COM11", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Outcome);
        Assert.False(ports.Port.IsOpen);
        if (expected == BetaflightProbeOutcome.Success)
        {
            Assert.Equal(new Version(1, 46), result.Identity!.MspApiVersion);
            Assert.Equal(new Version(4, 5, 2), result.Identity.FirmwareVersion);
            Assert.Equal("000102030405060708090A0B", result.Identity.McuUniqueId);
            Assert.Equal("TEST", result.Identity.Board!.Identifier);
            Assert.NotNull(result.Diagnostic);
        }
        else
        {
            Assert.Equal(new ushort[] { 1, 2 }, client.Calls);
            Assert.Null(result.Identity);
        }
    }

    [Fact]
    public void BoardPrefixExtensionsAndMalformedFields()
    {
        var api = new Version(1, 46);
        var prefix = Convert.FromHexString("544553540100");
        Assert.Equal((ushort)1, BetaflightIdentityParser.ParseBoard(prefix, api)!.HardwareRevision);
        Assert.Null(BetaflightIdentityParser.ParseBoard(prefix[..5], api));
        var board = new List<byte>(prefix) { 2, 9, 4 };
        board.AddRange("TEST"u8.ToArray());
        board.Add(5);
        board.AddRange("BOARD"u8.ToArray());
        board.Add(4);
        board.AddRange("ZZZZ"u8.ToArray());
        board.AddRange(new byte[32]);
        board.AddRange(new byte[] { 2, 1, 0xaa, 0xbb, 0xcc });
        var parsed = BetaflightIdentityParser.ParseBoard(board.ToArray(), api)!;
        Assert.Equal("ZZZZ", parsed.ManufacturerId);
        Assert.Equal("BOARD", parsed.BoardName);
        Assert.Equal("TEST", parsed.TargetName);
        Assert.Equal((byte)2, parsed.McuId);
        Assert.Equal(BetaflightTargetCapabilities.VirtualComPort | BetaflightTargetCapabilities.FlashBootloader, parsed.Capabilities);
        Assert.Null(BetaflightIdentityParser.ParseBoard(Convert.FromHexString("5445535400000001FF"), api));
        Assert.Null(BetaflightIdentityParser.ParseBoard(board.Take(26).ToArray(), api));
    }

    [Theory]
    [InlineData(MspFailure.Timeout, BetaflightProbeOutcome.NotMsp)]
    [InlineData(MspFailure.Disconnected, BetaflightProbeOutcome.Disconnected)]
    [InlineData(MspFailure.MalformedFrame, BetaflightProbeOutcome.ProtocolError)]
    public async Task MandatoryFailureNeverProducesIdentity(MspFailure failure, BetaflightProbeOutcome expected)
    {
        var client = new FakeClient("BTFL") { ApiFailure = failure };
        var probe = new BetaflightDeviceProbe(new MspPortConnector(new FakePorts(), TimeProvider.System), client);
        var result = await probe.ProbeAsync("COM11", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task BusyAndUnsupportedApiAreTyped()
    {
        var ports = new FakePorts { Busy = true };
        var client = new FakeClient("BTFL");
        var probe = new BetaflightDeviceProbe(new MspPortConnector(ports, TimeProvider.System), client);
        Assert.Equal(BetaflightProbeOutcome.PortBusy, (await probe.ProbeAsync("COM11", TestContext.Current.CancellationToken)).Outcome);
        Assert.Empty(client.Calls);
        ports.Busy = false;
        client.ApiMajor = 2;
        Assert.Equal(BetaflightProbeOutcome.UnsupportedApi, (await probe.ProbeAsync("COM11", TestContext.Current.CancellationToken)).Outcome);
    }

    private sealed class FakeClient(string variant) : IBetaflightMspClient
    {
        public MspFailure ApiFailure { get; init; }
        public byte ApiMajor { get; set; } = 1;
        public List<ushort> Calls { get; } = [];
        public Task<MspResponse> RequestAsync(IFirmwareSerialPort port, ushort command, ReadOnlyMemory<byte> payload,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Calls.Add(command);
            if (command == MspCommand.ApiVersion && ApiFailure != MspFailure.None)
            {
                return Task.FromResult(new MspResponse(ApiFailure));
            }
            byte[]? data = command switch
            {
                MspCommand.ApiVersion => [0, ApiMajor, 46],
                MspCommand.FcVariant => Encoding.ASCII.GetBytes(variant),
                MspCommand.FcVersion => [4, 5, 2],
                MspCommand.BoardInfo => Convert.FromHexString("544553540000"),
                MspCommand.Uid => Convert.FromHexString("000102030405060708090A0B"),
                _ => null
            };
            return Task.FromResult(data is null ? new MspResponse(MspFailure.MspErrorResponse)
                : new MspResponse(MspFailure.None, new(MspProtocolVersion.V1, command, data, false), true));
        }
    }

    private sealed class FakePorts : IFirmwareSerialPortFactory
    {
        public FakePort Port { get; } = new();
        public bool Busy { get; set; }
        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
            => Busy ? Task.FromException<IFirmwareSerialPort>(new UnauthorizedAccessException()) : Task.FromResult<IFirmwareSerialPort>(Port);
    }

    private sealed class FakePort : IFirmwareSerialPort
    {
        public string PortName => "COM11";
        public Stream Stream => Stream.Null;
        public bool IsOpen { get; private set; } = true;
        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }
}

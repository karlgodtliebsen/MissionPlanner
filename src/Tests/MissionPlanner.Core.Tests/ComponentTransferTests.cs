using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning.Osd;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class ComponentTransferTests
{
    [Theory]
    [InlineData("valid-password", true)]
    [InlineData("æøå network", false)]
    public void BridgeStringsRoundTripWithoutNumericConversion(string value, bool password)
    {
        Assert.Equal(value, PackedParameterStringCodec.Decode(EspBridgeSettings.EncodeText(value, password)));
    }

    [Theory]
    [InlineData("1234567890123456", false)]
    [InlineData("short", true)]
    [InlineData("abc\0def", false)]
    [InlineData("😀😀😀😀", false)]
    public void BridgeStringsRejectTruncationAndInvalidCredentials(string value, bool password) =>
        Assert.Throws<ArgumentException>(() => EspBridgeSettings.EncodeText(value, password));

    [Fact]
    public void BridgeIntegerAndAddressEncodingUsesByteUnion()
    {
        var baud = EspBridgeSettings.EncodeInteger("UART_BAUDRATE", 921600);
        Assert.Equal(921600u, EspBridgeSettings.DecodeInteger(baud));
        Assert.NotEqual(921600f, baud);
        var address = EspBridgeSettings.EncodeAddress("192.168.4.1");
        Assert.Equal(0x0104a8c0u, EspBridgeSettings.DecodeInteger(address));
        Assert.Equal("192.168.4.1", EspBridgeSettings.DecodeAddress(address));
        Assert.Throws<ArgumentOutOfRangeException>(() => EspBridgeSettings.EncodeInteger("WIFI_MODE", 3));
    }

    [Fact]
    public async Task BridgeIgnoresForeignRepliesPreservesBitsAndWaitsForSaveAcknowledgement()
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkParameterEncoder>();
        encoder.EncodeParamRequestList(1, 240).Returns([1]);
        encoder.EncodeParamSet(1, 240, "WIFI_SSID1", Arg.Any<float>(), MavParamType.Uint32).Returns([2]);
        var commands = Substitute.For<IMavLinkCommandEncoder>();
        commands.EncodeCommandLong(1, 240, 245, Arg.Any<IReadOnlyList<float>>()).Returns([3]);
        var raw = BitConverter.Int32BitsToSingle(unchecked((int)0xffc0abcd));
        var transmitted = new List<byte>();
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(call => new ValueTask(RespondAsync(call.ArgAt<ReadOnlyMemory<byte>>(0))));
        async Task RespondAsync(ReadOnlyMemory<byte> bytes)
        {
            var command = bytes.Span[0];
            transmitted.Add(command);
            await f.Emit(new ParamValueMessage(1, 1, f.Endpoint, "WIFI_SSID1", 777, MavParamType.Real32, 1, 0, f.Now));
            await f.Emit(new ParamValueMessage(1, 240, new("wrong-link"), "WIFI_SSID1", 777, MavParamType.Real32, 1, 0, f.Now));
            if (command == 3)
                await f.Emit(new CommandAckMessage(1, 240, f.Endpoint, 245, 0, f.Now));
            else
                await f.Emit(new ParamValueMessage(1, 240, f.Endpoint, "WIFI_SSID1", command == 1 ? 0 : raw, MavParamType.Uint32, 1, 0, f.Now));
        }
        var service = new ComponentParameterService(f.Active, f.Registry, f.Events, f.Connection, encoder, commands, new VehicleOperationGate());
        var snapshot = await service.ReadAsync(new(1, 240), TestContext.Current.CancellationToken);
        Assert.Equal(0, snapshot["WIFI_SSID1"]);
        await service.ApplyAsync(new(1, 240), new Dictionary<string, float> { ["WIFI_SSID1"] = raw }, true, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 1, 2, 3 }, transmitted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CubeTransferUses252ByteChunksAndRejectsOutOfBoundsOffsets(bool invalidOffset)
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>();
        var packets = new List<GeneratedMavLinkMessage>();
        encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call =>
        {
            packets.Add(call.Arg<GeneratedMavLinkMessage>());
            return new byte[] { 1 };
        });
        var updater = new CubeIdFirmwareService(f.Active, f.Registry, f.Events, f.Connection, encoder, new VehicleOperationGate());
        var detection = updater.DetectAsync(TestContext.Current.CancellationToken);
        await f.Emit(new HeartbeatMessage(1, 236, f.Endpoint, 0, 0, 8, 0, 0, 3, f.Now));
        var target = await detection;
        var data = Enumerable.Range(0, 600).Select(i => (byte)i).ToArray();
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(_ => new ValueTask(RespondAsync()));
        async Task RespondAsync()
        {
            uint offset = packets[^1] is EncapsulatedDataMessage chunk ? (uint)Math.Min((chunk.Seqnr + 1) * 252, data.Length) : 0;
            if (invalidOffset) offset = 601;
            // A flight-controller reply cannot advance the component-specific transfer.
            await f.Emit(new CubepilotFirmwareUpdateRespMessage(1, 1, f.Endpoint, 255, 190, 999, f.Now));
            await f.Emit(new CubepilotFirmwareUpdateRespMessage(1, 236, f.Endpoint, 255, 190, offset, f.Now));
        }
        var transfer = updater.TransferAsync(target, new("cube.bin", FirmwareSourceKind.Local, data), true, null, TestContext.Current.CancellationToken);
        if (invalidOffset)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => transfer);
            Assert.Single(packets);
        }
        else
        {
            await transfer;
            var chunks = packets.OfType<EncapsulatedDataMessage>().ToArray();
            Assert.Equal(new ushort[] { 0, 1, 2 }, chunks.Select(chunk => chunk.Seqnr));
            Assert.Equal(data, chunks.SelectMany(chunk => chunk.Data.Take(252)).Take(600));
            Assert.All(chunks, chunk => Assert.Equal(0, chunk.Data[252]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OsdSlotRequiresCorrectRequestIdAndVerifiedReadback(bool badReadback)
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>();
        GeneratedMavLinkMessage? request = null;
        encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call =>
        {
            request = call.Arg<GeneratedMavLinkMessage>();
            return new byte[] { 1 };
        });
        var parameters = new VehicleParameterRegistry();
        parameters.StoreParameter(new(1, 1), new("ATC_RAT_RLL_P", .1f, MavParamType.Real32, 0, 1), CancellationToken.None);
        var service = new OsdTuningSlotService(f.Active, f.Registry, parameters, f.Events, f.Connection, encoder, new VehicleOperationGate());
        var current = new OsdTuningSlot(5, 1, "ATC_RAT_RLL_P", 0, 0, 1, .01f);
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask(ReplyAsync()));
        async Task ReplyAsync()
        {
            if (request is OsdParamShowConfigMessage read)
            {
                await f.Emit(new OsdParamShowConfigReplyMessage(1, 1, f.Endpoint, read.RequestId + 1000, 3, "WRONG", 0, 0, 0, 0, f.Now));
                await f.Emit(new OsdParamShowConfigReplyMessage(1, 1, f.Endpoint, read.RequestId, 0, current.Parameter, current.Type, current.Minimum, current.Maximum, current.Increment, f.Now));
            }
            else if (request is OsdParamConfigMessage write)
            {
                if (!badReadback) current = current with { Maximum = write.MaxValue };
                await f.Emit(new OsdParamConfigReplyMessage(1, 1, f.Endpoint, write.RequestId, 0, f.Now));
            }
        }
        var before = await service.ReadAsync(new(1, 1), 5, 1, TestContext.Current.CancellationToken);
        Assert.Equal(current, before);
        var operation = service.ApplyAsync(new(1, 1), before, before with { Maximum = 2 }, true, TestContext.Current.CancellationToken);
        if (badReadback) await Assert.ThrowsAsync<IOException>(() => operation);
        else Assert.Equal(2, (await operation).Maximum);
    }

    [Theory]
    [InlineData(JoystickOutputMode.ManualControl)]
    [InlineData(JoystickOutputMode.RcOverride)]
    public async Task JoystickOutputCapturesTargetAndEncodesRelease(JoystickOutputMode mode)
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>();
        var messages = new List<GeneratedMavLinkMessage>();
        encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call => { messages.Add(call.Arg<GeneratedMavLinkMessage>()); return new byte[] { 1 }; });
        var parameters = new VehicleParameterRegistry();
        void Store(string name, float value) => parameters.StoreParameter(new(1, 1), new(name, value, MavParamType.Real32, 0, 20), CancellationToken.None);
        var index = 1;
        foreach (var function in new[] { "ROLL", "PITCH", "THROTTLE", "YAW" })
        {
            Store("RCMAP_" + function, index);
            Store($"RC{index}_MIN", 1000);
            Store($"RC{index}_TRIM", 1500);
            Store($"RC{index}_MAX", 2000);
            index++;
        }
        var output = new MavLinkJoystickOutput(f.Active, f.Registry, parameters, f.Connection, encoder, new VehicleOperationGate());
        await Assert.ThrowsAsync<InvalidOperationException>(() => output.SendManualControlAsync(new(0, 0, -1, 0), TestContext.Current.CancellationToken));
        await output.EnableAsync(mode, true, TestContext.Current.CancellationToken);
        await output.SendManualControlAsync(new(.5, -.5, -1, 0), TestContext.Current.CancellationToken);
        if (mode == JoystickOutputMode.ManualControl)
        {
            var manual = Assert.IsType<ManualControlMessage>(messages[0]);
            Assert.Equal((500, 500, 0, 0), ((int)manual.X, (int)manual.Y, (int)manual.Z, (int)manual.R));
        }
        else
        {
            var pwm = Assert.IsType<RcChannelsOverrideMessage>(messages[0]);
            Assert.Equal((1750, 1250, 1000, 1500), ((int)pwm.Chan1Raw, (int)pwm.Chan2Raw, (int)pwm.Chan3Raw, (int)pwm.Chan4Raw));
            Assert.Equal(ushort.MaxValue, pwm.Chan5Raw);
        }
        await output.ReleaseAsync(TestContext.Current.CancellationToken);
        var release = Assert.IsType<RcChannelsOverrideMessage>(messages[^1]);
        Assert.Equal(0, release.Chan1Raw);
        Assert.Equal(65534, release.Chan9Raw);
        Assert.False(output.IsEnabled);
    }

    [Fact]
    public async Task LuaConsoleDoesNotRunAnythingOnOpenAndDoesNotReplayCommands()
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>();
        var messages = new List<GeneratedMavLinkMessage>();
        encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call => { lock (messages) messages.Add(call.Arg<GeneratedMavLinkMessage>()); return new byte[] { 1 }; });
        var parameters = new VehicleParameterRegistry();
        parameters.StoreParameter(new(1, 1), new("SCR_ENABLE", 1, MavParamType.Real32, 0, 1), CancellationToken.None);
        var service = new MissionPlanner.Core.Setup.Advanced.Scripting.ScriptingConsoleService(f.Active, f.Registry, parameters,
            Substitute.For<IVehicleConnectionSession>(), f.Events, f.Connection, encoder, new VehicleOperationGate());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var run = service.RunAsync(MissionPlanner.Core.Setup.Advanced.Scripting.ScriptingConsoleMode.LuaApplet, true, new Progress<string>(), cancellation.Token);
        while (!service.CanSend) await Task.Delay(10, cancellation.Token);
        Assert.Equal(0, Assert.IsType<SerialControlMessage>(messages[0]).Count);
        await service.SendAsync("print('hello')", cancellation.Token);
        Assert.Equal("print('hello')\n", System.Text.Encoding.UTF8.GetString(Assert.IsType<SerialControlMessage>(messages[1]).Data).TrimEnd('\0'));
        Assert.Equal(2, messages.Count);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.False(service.CanSend);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task NativeConsoleRejectionDoesNotCreateAnFtpClient()
    {
        var f = new Fixture();
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>();
        encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(new byte[] { 1 });
        var parameters = new VehicleParameterRegistry();
        parameters.StoreParameter(new(1, 1), new("SCR_ENABLE", 1, MavParamType.Real32, 0, 1), CancellationToken.None);
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        var service = new MissionPlanner.Core.Setup.Advanced.Scripting.ScriptingConsoleService(f.Active, f.Registry, parameters,
            connectionSession, f.Events, f.Connection, encoder, new VehicleOperationGate());
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask(f.Emit(new CommandAckMessage(1, 1, f.Endpoint, 42701, 2, f.Now))));
        await Assert.ThrowsAsync<IOException>(() => service.RunAsync(MissionPlanner.Core.Setup.Advanced.Scripting.ScriptingConsoleMode.LegacyNative,
            true, new Progress<string>(), TestContext.Current.CancellationToken));
        connectionSession.DidNotReceive().CreateMavFtpConnection();
        Assert.False(service.CanSend);
    }

    [Fact]
    public async Task ParameterResetRejectsChangesAfterBackupAndInvalidatesAfterReadback()
    {
        var f = new Fixture();
        var parameters = new VehicleParameterRegistry();
        void Store(string name, float value) => parameters.StoreParameter(new(1, 1), new(name, value, MavParamType.Real32, 0, 2), CancellationToken.None);
        Store("FORMAT_VERSION", 120); Store("BATT_CAPACITY", 5000);
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        var parameterService = Substitute.For<IVehicleParameterService>();
        connectionSession.ParameterService.Returns(parameterService);
        var edits = Substitute.For<MissionPlanner.Core.ConfigTuning.IParameterEditSessionFactory>();
        var edit = Substitute.For<MissionPlanner.Core.ConfigTuning.IParameterEditSession>();
        edits.Create(new(1, 1)).Returns(edit);
        var service = new MissionPlanner.Core.ConfigTuning.ParameterMaintenanceService(f.Active, f.Registry, parameters, connectionSession, new VehicleOperationGate(), edits);
        var snapshot = service.Capture();
        Store("BATT_CAPACITY", 6000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareResetAsync(snapshot, true, TestContext.Current.CancellationToken));
        Assert.Empty(parameterService.ReceivedCalls());
        snapshot = service.Capture();
        parameterService.SetParameterAsync(new(1, 1), "FORMAT_VERSION", 0, MavParamType.Real32, Arg.Any<CancellationToken>()).Returns(_ => { Store("FORMAT_VERSION", 0); return Task.FromResult(true); });
        await service.PrepareResetAsync(snapshot, true, TestContext.Current.CancellationToken);
        edit.Received(1).Invalidate(Arg.Any<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareResetAsync(snapshot, true, TestContext.Current.CancellationToken));
        edits.HasUnappliedChanges.Returns(true);
        Assert.Throws<InvalidOperationException>(() => service.Capture());
    }

    private sealed class Fixture
    {
        internal readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        internal readonly TransportEndPoint Endpoint = new("component-test");
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IVehicleRegistry Registry = Substitute.For<IVehicleRegistry>();
        internal readonly IMavLinkConnection Connection = Substitute.For<IMavLinkConnection>();
        internal readonly EventHub Events = new(NullLogger<EventHub>.Instance);
        internal Fixture()
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(Now);
            var state = new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                Now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.IsOnline.Returns(true);
            Active.VehicleId.Returns(new VehicleId(1, 1));
            Active.State.Returns(state);
            Registry.GetRequired(new(1, 1)).Returns(new VehicleSession(state, Endpoint, clock));
        }
        internal Task Emit(MavLinkMessage message) => Events.PublishAsync(MavLinkEventTopics.ReceivedMessage, message, TestContext.Current.CancellationToken);
    }
}

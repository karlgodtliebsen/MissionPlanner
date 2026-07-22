using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;
using NSubstitute;
using MavAutopilot = MissionPlanner.MavLink.Generated.MavAutopilot;
using MavType = MissionPlanner.MavLink.Generated.MavType;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies vehicle firmware identity decoding and domain behavior.
/// </summary>
public sealed class VehicleFirmwareIdentityTests
{
    /// <summary>Verifies complete AUTOPILOT_VERSION payload decoding.</summary>
    [Fact]
    public void DecodesCompleteAutopilotVersion()
    {
        var payload = CreatePayload(78);
        var decoder = new AutopilotVersionMessageDecoder();

        decoder.TryDecode(CreateFrame(payload), out var decoded).Should().BeTrue();
        var version = decoded.Should().BeOfType<AutopilotVersionMessage>().Subject;
        version.Capabilities.Should().Be(0x1122334455667788UL);
        version.Uid.Should().Be(0x8877665544332211UL);
        version.FlightSoftwareVersion.Should().Be(0x040602FFU);
        version.BoardVersion.Should().Be(0x01020304U);
        version.VendorId.Should().Be(1209);
        version.ProductId.Should().Be(42);
        version.FlightCustomVersion.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        version.Uid2.Should().Equal(Enumerable.Range(1, 18).Select(value => (byte)value));
    }

    /// <summary>Verifies absent MAVLink 2 extension bytes are zero-filled.</summary>
    [Fact]
    public void ZeroFillsTruncatedUid2Extension()
    {
        var decoder = new AutopilotVersionMessageDecoder();
        decoder.TryDecode(CreateFrame(CreatePayload(60)), out var decoded).Should().BeTrue();
        decoded.Should().BeOfType<AutopilotVersionMessage>().Subject.Uid2.Should().OnlyContain(value => value == 0);
    }

    /// <summary>Verifies all standardized firmware release encodings.</summary>
    [Theory]
    [InlineData(255, FirmwareReleaseType.Official)]
    [InlineData(0, FirmwareReleaseType.Development)]
    [InlineData(128, FirmwareReleaseType.Beta)]
    [InlineData(192, FirmwareReleaseType.ReleaseCandidate)]
    public void DecodesPackedReleaseTypes(byte encoded, FirmwareReleaseType expected)
    {
        var version = FirmwareSemanticVersion.FromPacked(0x04060200U | encoded);
        version.Should().Be(new FirmwareSemanticVersion(4, 6, 2, expected));
    }

    /// <summary>Verifies firmware family mapping is restricted to ArduPilot.</summary>
    [Theory]
    [InlineData(MavType.Quadrotor, MavAutopilot.ArduPilotMega, FirmwareFamily.ArduCopter)]
    [InlineData(MavType.FixedWing, MavAutopilot.ArduPilotMega, FirmwareFamily.ArduPlane)]
    [InlineData(MavType.Quadrotor, MavAutopilot.Px4, FirmwareFamily.Unknown)]
    [InlineData(MavType.Airship, MavAutopilot.ArduPilotMega, FirmwareFamily.Blimp)]
    public void MapsHeartbeatIdentity(MavType mavType, MavAutopilot autopilot, FirmwareFamily expected)
    {
        VehicleFirmwareIdentityFactory.MapFamily(mavType, autopilot).Should().Be(expected);
    }

    /// <summary>Verifies session enrichment, hash handling, and heartbeat preservation.</summary>
    [Fact]
    public void EnrichesSessionAndPreservesVersionAcrossHeartbeats()
    {
        var session = CreateSession();
        session.ApplyFirmwareIdentity(new VehicleFirmwareObservation(32, 0x040602FF, 17, [0, 0, 0, 0, 0, 0, 0, 0], 1, 2, 3, [], DateTimeOffset.UtcNow));

        session.State.Identity.Firmware.FlightVersion.Should().Be(new FirmwareSemanticVersion(4, 6, 2, FirmwareReleaseType.Official));
        session.State.Identity.Firmware.FlightGitHash.Should().BeNull();
        session.State.Identity.Firmware.Supports(MavProtocolCapability.Ftp).Should().BeTrue();
        session.ApplyHeartbeat(0, 2, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        session.State.Identity.Firmware.FlightVersion.Should().NotBeNull();
    }

    /// <summary>Verifies a new connection session does not inherit prior enrichment.</summary>
    [Fact]
    public void NewSessionHasCleanConnectionIdentity()
    {
        var previous = CreateSession();
        previous.ApplyFirmwareIdentity(new VehicleFirmwareObservation(32, 0x040602FF, 17, [1], 1, 2, 3, [], DateTimeOffset.UtcNow));

        var reconnected = CreateSession();
        reconnected.State.Identity.Firmware.FlightVersion.Should().BeNull();
        reconnected.State.Identity.Firmware.Capabilities.Should().Be(0);
    }

    /// <summary>Verifies user-facing firmware version formatting.</summary>
    [Fact]
    public void FormatsFirmwareDisplayName()
    {
        var identity = VehicleFirmwareIdentityFactory.FromHeartbeat(2, 3) with
        {
            FlightVersion = new FirmwareSemanticVersion(4, 6, 2, FirmwareReleaseType.Development)
        };
        VehicleFirmwareDisplayFormatter.Format(identity).Should().Be("ArduCopter 4.6.2-dev");
    }

    /// <summary>Verifies friendly vehicle names for known families and MAV type fallbacks.</summary>
    [Theory]
    [InlineData(1, FirmwareFamily.ArduCopter, 2, "SysID 1:Copter")]
    [InlineData(2, FirmwareFamily.ArduPlane, 1, "SysID 2:Plane")]
    [InlineData(3, FirmwareFamily.Rover, 10, "SysID 3:Rover")]
    [InlineData(4, FirmwareFamily.Unknown, 2, "SysID 4:Quadrotor")]
    [InlineData(9, FirmwareFamily.Unknown, 0, "SysID 9:Unknown")]
    public void FormatsVehicleDisplayName(byte systemId, FirmwareFamily family, byte mavType, string expected)
    {
        VehicleDisplayNameFormatter.Format(new VehicleId(systemId, 1), family, mavType).Should().Be(expected);
    }

    /// <summary>Verifies identical heartbeats preserve identity and changed types update the name.</summary>
    [Fact]
    public void HeartbeatUpdatesDisplayNameOnlyWhenIdentityChanges()
    {
        var session = CreateSession();
        var originalIdentity = session.State.Identity;

        session.ApplyHeartbeat(0, 2, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        session.State.Identity.Should().BeSameAs(originalIdentity);
        session.State.DisplayName.Should().Be("SysID 1:Copter");

        session.ApplyHeartbeat(0, 1, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        session.State.Identity.Should().NotBeSameAs(originalIdentity);
        session.State.DisplayName.Should().Be("SysID 1:Plane");
        session.Id.ComponentId.Should().Be(1);
    }

    /// <summary>Verifies one request emits MAV_CMD_REQUEST_MESSAGE for AUTOPILOT_VERSION.</summary>
    [Fact]
    public async Task SendsOneAutopilotVersionRequest()
    {
        var session = CreateSession();
        var client = Substitute.For<IMavLinkClient>();
        client.IsConnected.Returns(true);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        var service = new MavLinkCommandService(client, registry, Substitute.For<ILogger<MavLinkCommandService>>());

        (await service.RequestAutopilotVersionAsync(session.Id, TestContext.Current.CancellationToken)).Should().BeTrue();

        await client.Received(1).SendAsync(
            Arg.Is<ReadOnlyMemory<byte>>(data => IsAutopilotVersionRequest(data.ToArray())),
            session.EndPoint,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cancellation is consumed without an escaped or unobserved exception.</summary>
    [Fact]
    public async Task AutopilotVersionRequestIsCancellationSafe()
    {
        var session = CreateSession();
        var client = Substitute.For<IMavLinkClient>();
        client.IsConnected.Returns(true);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        client.SendAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), cancellation.Token)
            .Returns(ValueTask.FromCanceled(cancellation.Token));
        var service = new MavLinkCommandService(client, registry, Substitute.For<ILogger<MavLinkCommandService>>());

        (await service.RequestAutopilotVersionAsync(session.Id, cancellation.Token)).Should().BeFalse();
    }

    private static byte[] CreatePayload(int length)
    {
        var payload = new byte[length];
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(0), 0x1122334455667788UL);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(8), 0x8877665544332211UL);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), 0x040602FFU);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), 0x01020304U);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(32), 1209);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(34), 42);
        for (var index = 0; index < 8; index++)
        {
            payload[36 + index] = (byte)(index + 1);
        }
        if (length > 60)
        {
            for (var index = 60; index < length; index++) payload[index] = (byte)(index - 59);
        }
        return payload;
    }

    private static MavLinkFrame CreateFrame(byte[] payload) =>
        new(1, 1, new TransportEndPoint("test"), MessageIds.AutopilotVersion, 0, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);

    private static VehicleSession CreateSession()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleSession(state, new TransportEndPoint("test"), clock);
    }

    private static bool IsAutopilotVersionRequest(byte[] packet)
    {
        const int payloadOffset = 6;
        var command = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(payloadOffset + 28, 2));
        var requestedMessage = BinaryPrimitives.ReadSingleLittleEndian(packet.AsSpan(payloadOffset, 4));
        return command == 512 && requestedMessage == MessageIds.AutopilotVersion;
    }
}

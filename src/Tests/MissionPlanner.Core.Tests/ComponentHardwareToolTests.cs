using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.Core.Tests;

public sealed class ComponentHardwareToolTests
{
    [Fact]
    public void PackedStringRoundTripsSixteenBytesAndRedactsPassword()
    {
        var encoded = PackedParameterStringCodec.Encode("MissionPlanner16");
        Assert.Equal(4, encoded.Length);
        Assert.Equal("MissionPlanner16", PackedParameterStringCodec.Decode(encoded));
        Assert.Equal("<redacted>", PackedParameterStringCodec.Redact("WIFI_PASSWORD", "secret"));
    }

    [Fact]
    public void CubeFirmwareChunksPreserveOffsetAndPayload()
    {
        var input = Enumerable.Range(0, 600).Select(i => (byte)i).ToArray();
        var chunks = CubeFirmwareCodec.Chunk(input);
        Assert.Equal([0u, 253u, 506u], chunks.Select(x => x.Offset));
        Assert.Equal(input, chunks.SelectMany(x => x.Data));
        Assert.Equal(0x2B00C0C1u, CubeFirmwareCodec.Crc32(input));
    }

    [Fact]
    public async Task DroneCanServicePreservesTransportAndNodeIdentity()
    {
        var transport = new FakeTransport();
        await using var service = new DroneCanService(new FakeFactory(transport));
        await service.ConnectAsync(DroneCanTransportKind.DirectSlcan, TestContext.Current.CancellationToken);
        var node = Assert.Single(await service.DiscoverAsync(TestContext.Current.CancellationToken));
        await service.WriteParameterAsync(node.NodeId, "foo", 3, TestContext.Current.CancellationToken);
        Assert.Equal((42, "foo"), transport.LastWrite);
        await service.DisconnectAsync(TestContext.Current.CancellationToken);
        Assert.True(transport.Disposed);
    }

    private sealed record FakeFactory(FakeTransport Transport) : IDroneCanTransportFactory
    {
        public IDroneCanTransport Create(DroneCanTransportKind kind)
        {
            Transport.RequestedKind = kind;
            return Transport;
        }
    }
    private sealed class FakeTransport : IDroneCanTransport
    {
        public DroneCanTransportKind RequestedKind; public DroneCanTransportKind Kind => RequestedKind; public bool IsConnected
        {
            get; private set;
        }
        public (byte, string) LastWrite; public bool Disposed;
        public Task ConnectAsync(CancellationToken ct)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }
        public Task DisconnectAsync(CancellationToken ct)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<DroneCanNode>> DiscoverAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DroneCanNode>>([new(42, "node", DroneCanNodeHealth.Ok, DroneCanNodeMode.Operational, "1", 1, 2)]);
        }

        public Task<IReadOnlyList<DroneCanParameter>> ReadParametersAsync(byte id, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DroneCanParameter>>([]);
        }

        public Task WriteParameterAsync(byte id, string name, object value, CancellationToken ct)
        {
            LastWrite = (id, name);
            return Task.CompletedTask;
        }
        public Task RestartNodeAsync(byte id, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

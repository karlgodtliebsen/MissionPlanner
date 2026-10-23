using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

public sealed class PX4FlowImageAssemblerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static DataTransmissionHandshakeMessage Header() => new(1, 50, new TransportEndPoint("sensor"), 2, 6, 3, 2, 2, 4, 0, Now);
    private static EncapsulatedDataMessage Packet(ushort sequence, params byte[] bytes) => new(1, 50, new TransportEndPoint("sensor"), sequence, bytes, Now);

    [Fact]
    public void OutOfOrderAndDuplicatePacketsProduceOneCompleteImage()
    {
        var assembler = new PX4FlowImageAssembler();
        Assert.Null(assembler.Accept(Header()));
        Assert.Null(assembler.Accept(Packet(1, 5, 6, 99, 99)));
        Assert.Null(assembler.Accept(Packet(1, 5, 6)));
        var frame = Assert.IsType<PX4FlowImage>(assembler.Accept(Packet(0, 1, 2, 3, 4)));
        Assert.Equal(3, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, frame.Pixels);
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4)));
    }

    [Fact]
    public void MissingShortForeignAndExpiredPacketsNeverCompleteAnImage()
    {
        var assembler = new PX4FlowImageAssembler();
        assembler.Accept(Header());
        Assert.Null(assembler.Accept(Packet(0, 1)));
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4) with { ComponentId = 51 }));
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4) with { EndPoint = new("other") }));
        Assert.Null(assembler.Accept(Packet(1, 5, 6)));
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4) with { ReceivedAt = Now.AddSeconds(4) }));
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4)));
    }

    [Fact]
    public void InvalidOrNewHandshakeDiscardsPreviousFrame()
    {
        var assembler = new PX4FlowImageAssembler();
        foreach (var invalid in new[] { Header() with { Payload = 0 }, Header() with { Size = uint.MaxValue },
                     Header() with { Packets = 1 }, Header() with { Type = 0 }, Header() with { Width = 0 } })
        {
            assembler.Accept(Header());
            assembler.Accept(Packet(0, 1, 2, 3, 4));
            Assert.Null(assembler.Accept(invalid));
            Assert.Null(assembler.Accept(Packet(1, 5, 6)));
        }
        assembler.Accept(Header());
        assembler.Accept(Packet(0, 1, 2, 3, 4));
        assembler.Accept(Header());
        Assert.Null(assembler.Accept(Packet(1, 5, 6)));
        assembler.Reset();
        Assert.Null(assembler.Accept(Packet(0, 1, 2, 3, 4)));
    }
}

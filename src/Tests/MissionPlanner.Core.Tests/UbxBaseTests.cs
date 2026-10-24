using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using MissionPlanner.Core.Setup.OptionalHardware;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates u-blox framing, precision, survey evidence and acknowledged configuration without serial hardware.</summary>
public sealed class UbxBaseTests
{
    [Fact]
    public void FramerAcceptsSplitPacketsAndRejectsCorruptFrames()
    {
        Assert.Equal(new byte[] { 0xB5, 0x62, 6, 1, 0, 0, 7, 27 }, UbxFramer.Encode(6, 1, []));
        var parser = new UbxFramer(); var frame = UbxFramer.Encode(1, 0x35, [0, 1, 2, 3]);
        Assert.Empty(parser.Push(frame.AsSpan(0, 5)));
        var packet = Assert.Single(parser.Push(frame.AsSpan(5))); Assert.Equal(new byte[] { 0, 1, 2, 3 }, packet.Payload);
        var corrupt = frame.ToArray(); corrupt[^1] ^= 1;
        Assert.Empty(parser.Push(corrupt));
        Assert.Single(parser.Push(frame)); Assert.Equal(1, parser.ChecksumErrors);
        var oversize = new byte[] { 0xB5, 0x62, 1, 1, 0xFF, 0xFF };
        Assert.Single(parser.Push(oversize.Concat(frame).ToArray()));
    }
    [Fact]
    public void FixedPositionUsesSignedFineUnitsAndAccuracyInTenthsOfMillimetres()
    {
        var bytes = UbxBaseProtocol.Fixed(new("surveyed", -55.123456789, 12.987654321, -123.4567, .02));
        Assert.Equal(258, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(-551234567, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(-89, unchecked((sbyte)bytes[16]));
        Assert.Equal(129876543, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8))); Assert.Equal(21, bytes[17]);
        Assert.Equal(-12345, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12))); Assert.Equal(-67, unchecked((sbyte)bytes[18]));
        Assert.Equal(200u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        var survey = UbxBaseProtocol.Survey(120, .5);
        Assert.Equal(120u, BinaryPrimitives.ReadUInt32LittleEndian(survey.AsSpan(24)));
        Assert.Equal(5000u, BinaryPrimitives.ReadUInt32LittleEndian(survey.AsSpan(28)));
        Assert.Throws<ArgumentException>(() => UbxBaseProtocol.Survey(1, 1));
        Assert.Throws<ArgumentException>(() => UbxBaseProtocol.Fixed(new("invalid", double.NaN, 0, 0, 1)));
    }
    [Fact]
    public void SurveyAndConstellationEvidenceUseDocumentedFields()
    {
        var bytes = new byte[40]; bytes[36] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 120);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 637813700); bytes[24] = 50;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 200);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 110);
        var survey = UbxBaseProtocol.DecodeSurvey(bytes, DateTimeOffset.UtcNow);
        Assert.True(survey.Valid); Assert.False(survey.Active); Assert.Equal(.02, survey.Accuracy);
        Assert.Equal(120u, survey.Duration); Assert.Equal(110u, survey.Observations);
        Assert.NotNull(survey.MeanPosition); Assert.InRange(survey.MeanPosition.EllipsoidHeight, .00499, .00501);
        Assert.Equal(0, survey.MeanPosition.Latitude); Assert.Equal(0, survey.MeanPosition.Longitude);
        bytes[36] = 0; Assert.Null(UbxBaseProtocol.DecodeSurvey(bytes, DateTimeOffset.UtcNow).MeanPosition);
        var satellites = new byte[20]; satellites[4] = 1; satellites[5] = 1; satellites[8] = 2; satellites[9] = 12; satellites[10] = 42; satellites[11] = 30; satellites[16] = 8;
        var sat = Assert.Single(UbxBaseProtocol.DecodeSatellites(satellites));
        Assert.Equal("Galileo", sat.Constellation); Assert.True(sat.Used); Assert.Equal(42, sat.CarrierNoise);
        satellites[5] = 255; Assert.Throws<InvalidDataException>(() => UbxBaseProtocol.DecodeSatellites(satellites));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SurveyWriteRequiresAcknowledgementAndMatchingReadback(bool mismatch)
    {
        var f = new Fixture();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = f.Service.RunAsync("fake", 115200, new Progress<UbxBaseSnapshot>(), lifetime.Token);
        await f.WaitReady();
        Assert.DoesNotContain(f.Port.Sent, p => p.Class == 6 && p.Payload.Length >= 3);
        var review = f.Service.Review();
        f.Port.MismatchReadback = mismatch;
        var change = f.Service.StartSurveyAsync(review, 90, 1.25, true, lifetime.Token);
        if (mismatch) await Assert.ThrowsAsync<IOException>(() => change);
        else
        {
            await change;
            Assert.Equal(90u, BinaryPrimitives.ReadUInt32LittleEndian(f.Port.Mode.AsSpan(24)));
            Assert.Equal(12500u, BinaryPrimitives.ReadUInt32LittleEndian(f.Port.Mode.AsSpan(28)));
            Assert.Contains("read back", f.Service.Current!.Status);
        }
        Assert.DoesNotContain(f.Port.Sent, p => p.Class == 6 && p.Id is 9 or 4); // No save/reset.
        lifetime.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(f.Port.Disposed); Assert.Null(f.Service.Current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DisableAsync(review, true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task CorrectionSetupPreservesPortBaudAndUsesCurrentPortRates()
    {
        var f = new Fixture();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = f.Service.RunAsync("fake", 115200, new Progress<UbxBaseSnapshot>(), lifetime.Token); await f.WaitReady();
        await f.Service.EnableCorrectionsAsync(f.Service.Review(), true, false, true, lifetime.Token);
        Assert.Equal(115200u, BinaryPrimitives.ReadUInt32LittleEndian(f.Port.Configuration.AsSpan(8)));
        Assert.Equal(0x23, BinaryPrimitives.ReadUInt16LittleEndian(f.Port.Configuration.AsSpan(14)));
        var rates = f.Port.Sent.Where(p => p.Class == 6 && p.Id == 1 && p.Payload.Length == 3).ToArray();
        Assert.Equal(new byte[] { 5, 0x4A, 0x54, 0xE6, 0x5E }, rates.Select(p => p.Payload[1]));
        Assert.All(rates, p => Assert.Equal(1, p.Payload[2]));
        lifetime.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
    [Fact]
    public async Task StaleReviewAndReceiverNakNeverReportSuccess()
    {
        var f = new Fixture();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = f.Service.RunAsync("fake", 115200, new Progress<UbxBaseSnapshot>(), lifetime.Token); await f.WaitReady();
        var review = f.Service.Review(); f.Port.Mode[2] = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.StartSurveyAsync(review, 60, 2, true, lifetime.Token));
        Assert.DoesNotContain(f.Port.Sent, packet => packet.Class == 6 && packet.Id == 0x71 && packet.Payload.Length != 0);
        f.Port.Nak = true;
        await Assert.ThrowsAsync<NotSupportedException>(() => f.Service.SetFixedAsync(f.Service.Review(), new("base", 55, 12, 30, .02), true, lifetime.Token));
        lifetime.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
    private sealed class Fixture
    {
        internal readonly FakePort Port = new();
        internal readonly UbxBaseSession Service;
        internal Fixture()
        {
            var factory = Substitute.For<IDirectSerialSessionFactory>(); factory.OpenAsync("fake", 115200, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IDirectSerialSession>(Port));
            Service = new(factory);
        }
        internal async Task WaitReady()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(3));
            while (Service.Current?.Ready != true) await Task.Delay(10, timeout.Token);
        }
    }
    private sealed class FakePort : IDirectSerialSession
    {
        private readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();
        internal readonly ConcurrentQueue<UbxPacket> Sent = new();
        internal byte[] Mode = new byte[40];
        internal byte[] Configuration = new byte[20];
        internal bool MismatchReadback;
        internal bool Nak;
        internal bool Disposed;
        internal FakePort() { Configuration[0] = 1; Configuration[14] = 3; BinaryPrimitives.WriteUInt32LittleEndian(Configuration.AsSpan(8), 115200); }
        public string PortName => "fake";
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task WriteBytesAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            var packet = Assert.Single(new UbxFramer().Push(bytes.Span)); Sent.Enqueue(packet);
            void Reply(byte cls, byte id, byte[] data) => replies.Writer.TryWrite(UbxFramer.Encode(cls, id, data));
            if (packet.Class == 0x0A) { var data = new byte[70]; Encoding.ASCII.GetBytes("HPG 1.32 ZED-F9P").CopyTo(data, 0); Reply(0x0A, 4, data); }
            else if (packet.Class == 6 && packet.Id == 0x71)
            {
                if (packet.Payload.Length == 0) Reply(6, 0x71, Mode);
                else { if (!Nak) Mode = MismatchReadback ? new byte[40] : packet.Payload; Reply(5, (byte)(Nak ? 0 : 1), [6, 0x71]); }
            }
            else if (packet.Class == 6 && packet.Id == 0)
            {
                if (packet.Payload.Length == 0) Reply(6, 0, Configuration);
                else { Configuration = packet.Payload; Reply(5, 1, [6, 0]); }
            }
            else if (packet.Class == 6 && packet.Id == 1)
            {
                if (packet.Payload.Length == 3) Reply(5, 1, [6, 1]);
                else Reply(6, 1, [0xF5, packet.Payload[1], 0, 1, 0, 0, 0, 0]);
            }
            return Task.CompletedTask;
        }
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytes = await replies.Reader.ReadAsync(cancellationToken); bytes.CopyTo(buffer); return bytes.Length;
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}

using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using MissionPlanner.Core.Setup.OptionalHardware;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises image policy and both radio bootloaders without serial hardware.</summary>
public sealed class SikFirmwareTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static SikFirmwareBoard Board(byte id) => SikFirmwareBoard.Supported.Single(board => board.Id == id);
    private static string Record(ushort address, byte type, params byte[] data)
    {
        byte[] record = [(byte)data.Length, (byte)(address >> 8), (byte)address, type, .. data];
        return ":" + Convert.ToHexString([.. record, unchecked((byte)-record.Sum(value => value))]);
    }
    private static byte[] Hex(SikFirmwareBoard board)
    {
        var lines = new List<string> { Record(0x400, 0, Encoding.ASCII.GetBytes(board.ImageToken + "\0")) };
        if (board.Banked) { lines.Add(Record(0, 4, 0, 1)); lines.Add(Record(0x8000, 0, [1, 2, 3])); lines.Add(Record(0, 4, 0, 3)); }
        lines.Add(Record(0xF7FE, 0, 0x3D, 0xC2)); lines.Add(Record(0, 1));
        return Encoding.ASCII.GetBytes(string.Join("\n", lines));
    }
    private static byte[] Binary(SikFirmwareBoard board)
    {
        var data = new byte[300]; Encoding.ASCII.GetBytes(board.ImageToken + "\0HastaLaVistaBaby\0").CopyTo(data, 24); return data;
    }
    [Fact]
    public void ImageValidationRejectsProtectedFlashBadChecksumOverlapAndBoardMismatch()
    {
        var board = Board(0x4E); var source = Hex(board); var image = SikFirmwareImage.Parse("radio.ihx", source, board);
        Assert.Equal("radio.ihx", image.Name); Assert.Equal(64, image.Sha256.Length); Assert.Equal((byte)0x3D, image.Bytes[0xF7FE]);
        var corrupt = source.ToArray(); corrupt[10] ^= 1; Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.ihx", corrupt, board));
        Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.ihx", source, Board(0x43)));
        foreach (var address in new ushort[] { 0, 0x3FF, 0xF800, 0xFFFF })
        {
            var unsafeImage = Encoding.ASCII.GetBytes(Record(address, 0, 1) + "\n" + Encoding.ASCII.GetString(source));
            Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.ihx", unsafeImage, board));
        }
        var duplicate = Encoding.ASCII.GetBytes(Record(0x400, 0, (byte)'H') + "\n" + Encoding.ASCII.GetString(source));
        Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.ihx", duplicate, board));
        Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.ihx", source[..^12], board));
        var banked = SikFirmwareImage.Parse("banked.ihx", Hex(Board(0x82)), Board(0x82)); Assert.Contains(0x18000u, banked.Bytes.Keys); Assert.Contains(0x3F7FEu, banked.Bytes.Keys);
        Assert.False(Board(0x82).CanWrite(0x10000)); Assert.False(Board(0x82).CanWrite(0x3F800)); Assert.False(Board(0x82).CanWrite(0x40000));
    }
    [Fact]
    public void XmodemUsesKnownCrcAndExactBlockIdentityAndRequiresCertifiedRevisionOneImage()
    {
        Assert.Equal(0x31C3, RfdXmodemCodec.Crc(Encoding.ASCII.GetBytes("123456789")));
        var block = RfdXmodemCodec.Block(255, [1, 2]); Assert.Equal(133, block.Length); Assert.Equal(new byte[] { 1, 255, 0, 1, 2 }, block[..5]); Assert.Equal(0x26, block[5]);
        Assert.Equal(RfdXmodemCodec.Crc(block.AsSpan(3, 128)), (ushort)(block[131] << 8 | block[132]));
        var board = Board(0x83); Assert.NotNull(SikFirmwareImage.Parse("radio.bin", Binary(board), board));
        var uncertified = new byte[300]; Encoding.ASCII.GetBytes("RFD900x\0").CopyTo(uncertified, 0);
        Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.bin", uncertified, board));
        Assert.Throws<InvalidDataException>(() => SikFirmwareImage.Parse("radio.bin", Binary(Board(0x88)), board));
    }
    [Theory]
    [InlineData(0x4E)]
    [InlineData(0x82)]
    public async Task ClassicFlashesVerifiedBytesAndWritesSignatureLast(int id)
    {
        var board = Board((byte)id); var port = new Radio(board); var updater = Updater(port); var image = SikFirmwareImage.Parse("radio.ihx", Hex(board), board);
        var result = await updater.UploadAsync("fake", 57600, image, true, true, null, null, Token);
        Assert.True(port.Disposed); Assert.True(port.Rebooted); Assert.Equal(image.Bytes.OrderBy(pair => pair.Key), port.Memory.OrderBy(pair => pair.Key));
        Assert.Equal(board.SignatureAddress, port.Programmed.Last()); Assert.Contains("matched flash readback", result);
        Assert.DoesNotContain(port.BinaryCommands, bytes => bytes[0] == 0x29);
    }
    [Fact]
    public async Task ClassicWrongBoardAndReadbackFailureNeverBootAnUnverifiedApplication()
    {
        var image = SikFirmwareImage.Parse("radio.ihx", Hex(Board(0x4E)), Board(0x4E));
        var wrong = new Radio(Board(0x43));
        await Assert.ThrowsAsync<IOException>(() => Updater(wrong).UploadAsync("fake", 57600, image, true, true, null, null, Token)); Assert.False(wrong.Erased); Assert.False(wrong.Rebooted);
        var mismatch = new Radio(Board(0x4E)) { Mismatch = true };
        await Assert.ThrowsAsync<IOException>(() => Updater(mismatch).UploadAsync("fake", 57600, image, true, true, null, null, Token));
        Assert.True(mismatch.Erased); Assert.False(mismatch.Rebooted); Assert.DoesNotContain(0xF7FEu, mismatch.Memory.Keys); Assert.True(mismatch.Disposed);
    }
    [Fact]
    public async Task XmodemRetriesTheIdenticalBlockThenAcknowledgesEofBeforeBootnew()
    {
        var board = Board(0x83); var port = new Radio(board) { NakFirst = true }; var image = SikFirmwareImage.Parse("radio.bin", Binary(board), board);
        var result = await Updater(port).UploadAsync("fake", 57600, image, true, true, null, null, Token);
        var blocks = port.BinaryCommands.Where(bytes => bytes[0] == 1).ToArray(); Assert.Equal(4, blocks.Length); Assert.Equal(blocks[0], blocks[1]);
        Assert.True(port.Rebooted); Assert.True(port.Disposed); Assert.Contains("no flash readback", result);
        Assert.Equal(image.Source.ToArray(), port.Received.Take(image.Source.Length));
    }
    [Fact]
    public async Task XmodemReceiverCancelSendsAbortAndNeverBootnew()
    {
        var board = Board(0x83); var port = new Radio(board) { Abort = true }; var image = SikFirmwareImage.Parse("radio.bin", Binary(board), board);
        await Assert.ThrowsAsync<IOException>(() => Updater(port).UploadAsync("fake", 57600, image, true, true, null, null, Token));
        Assert.False(port.Rebooted); Assert.True(port.Disposed); Assert.Contains(port.BinaryCommands, bytes => bytes.SequenceEqual(new byte[] { 0x18, 0x18 }));
    }
    private static SikFirmwareUpdater Updater(Radio port)
    {
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("fake", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IDirectSerialSession>(port)); return new(factory);
    }
    private sealed class Radio(SikFirmwareBoard board) : IDirectSerialSession
    {
        private readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();
        private uint address;
        private bool rejected;
        internal bool Disposed, Rebooted, Erased, Mismatch, NakFirst, Abort;
        internal readonly Dictionary<uint, byte> Memory = [];
        internal readonly List<uint> Programmed = [];
        internal readonly List<byte> Received = [];
        internal readonly ConcurrentQueue<byte[]> BinaryCommands = new();
        public string PortName => "fake";
        public Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            if (value.Contains("CHIPID", StringComparison.Ordinal)) replies.Writer.TryWrite(Encoding.ASCII.GetBytes(board.ChipId + "\r\n"));
            else if (value == "UPLOAD\r") replies.Writer.TryWrite(Encoding.ASCII.GetBytes("Ready\r\nC"));
            else if (value.Contains("BOOTNEW", StringComparison.Ordinal)) Rebooted = true;
            return Task.CompletedTask;
        }
        public Task WriteBytesAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            var bytes = value.ToArray(); BinaryCommands.Enqueue(bytes);
            if (board.XSeries)
            {
                if (bytes[0] == 1)
                {
                    if (Abort) replies.Writer.TryWrite([0x18]);
                    else if (NakFirst && !rejected) { rejected = true; replies.Writer.TryWrite([0x15]); }
                    else { Received.AddRange(bytes.AsSpan(3, 128).ToArray()); replies.Writer.TryWrite([0x06]); }
                }
                else if (bytes[0] == 4) replies.Writer.TryWrite([0x06]);
                return Task.CompletedTask;
            }
            byte[] result = [];
            switch (bytes[0])
            {
                case 0: return Task.CompletedTask;
                case 0x21: break;
                case 0x22: result = [board.Id, 0x91]; break;
                case 0x23: Erased = true; Memory.Clear(); break;
                case 0x24: address = (uint)(bytes[1] | bytes[2] << 8 | (board.Banked ? bytes[3] << 16 : 0)); break;
                case 0x27: Programmed.Add(address); for (var i = 0; i < bytes[1]; i++) Memory[address++] = bytes[2 + i]; break;
                case 0x28: result = Enumerable.Range(0, bytes[1]).Select(_ => Memory.GetValueOrDefault(address++, (byte)255)).ToArray(); if (Mismatch) result[0] ^= 1; break;
                case 0x30: Rebooted = true; return Task.CompletedTask;
                default: throw new InvalidDataException("Unexpected bootloader command.");
            }
            replies.Writer.TryWrite([.. result, 0x12, 0x10]); return Task.CompletedTask;
        }
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytes = await replies.Reader.ReadAsync(cancellationToken); bytes.CopyTo(buffer); return bytes.Length;
        }
        public Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}


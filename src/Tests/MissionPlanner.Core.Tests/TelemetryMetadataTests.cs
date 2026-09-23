using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.Logging;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies metadata from growing and reopened classic recordings.</summary>
public sealed class TelemetryMetadataTests
{
    /// <summary>The index replaces stale initial metadata and protocol identity wins over startup text.</summary>
    [Fact]
    public async Task RebuildsMetadataAfterRecordingGrows()
    {
        var token = TestContext.Current.CancellationToken;
        var storage = new BrowserLogStorage();
        var reader = new TelemetryLogReader();
        var definitions = new MavLinkMessageDefinitionRegistry();
        var decoder = new MavLinkMessageDecoderHandler(new MavLinkMessageDecoders(
            new MavLinkMessageDecoderCatalog(definitions).Decoders, definitions), NullLogger<MavLinkMessageDecoderHandler>.Instance);
        var catalog = new TelemetryLogCatalog(storage, reader, decoder);
        await using (var output = await storage.CreateAsync(LogStorageArea.Telemetry, "identity.tlog", token))
        {
            var initial = Assert.Single(await catalog.ListAsync(token));
            Assert.Equal(0, initial.Size);
            Assert.Equal("Unknown", initial.Vehicle);
            byte[] heartbeat = [0, 0, 0, 0, 2, 3, 0, 3, 3];
            Write(output, 0, 0, heartbeat);
            var firmware = new byte[20]; // Deliberately trimmed AUTOPILOT_VERSION.
            BinaryPrimitives.WriteUInt32LittleEndian(firmware.AsSpan(16), 0x040507FF);
            Write(output, 1, 148, firmware);
            var text = new byte[51];
            Encoding.ASCII.GetBytes("ArduCopter V4.0.0").CopyTo(text, 1);
            Write(output, 2, 253, text);
            Array.Clear(text);
            Encoding.ASCII.GetBytes("CubeOrange 00123456").CopyTo(text, 1);
            Write(output, 35, 253, text);
            await output.FlushAsync(token);
        }
        var actual = Assert.Single(await catalog.ListAsync(token));
        Assert.True(actual.Size > 0);
        Assert.Equal(TimeSpan.FromSeconds(35), actual.Duration);
        Assert.Equal(4, actual.Metadata.PacketCount);
        Assert.Equal((byte)1, actual.Metadata.SystemId);
        Assert.Contains("4.5.7", actual.Firmware);
        Assert.Contains("CubeOrange", actual.Metadata.Board);
        Assert.Equal(actual, Assert.Single(await new TelemetryLogCatalog(storage, reader, decoder).ListAsync(token)));
    }

    private static void Write(Stream output, int seconds, byte id, byte[] payload)
    {
        Span<byte> time = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(time, (ulong)seconds * 1000000);
        output.Write(time);
        output.Write(new byte[] { 0xFD, (byte)payload.Length, 0, 0, 0, 1, 1, id, 0, 0 });
        output.Write(payload);
        output.Write(new byte[2]); // Reader/catalog inspect structurally indexed frames, like the packet browser.
    }
}

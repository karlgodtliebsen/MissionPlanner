using System.Buffers.Binary;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.Logging;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class TelemetryPacketBrowserTests
{
    [Fact]
    public async Task LargeLogUsesBoundedDecodedPagesAndUnknownBytesRemainVisible()
    {
        using var input = CreateLog(100000);
        var reader = new TelemetryLogReader();
        var index = await reader.IndexAsync(input, "large.tlog", TestContext.Current.CancellationToken);
        var decoder = Substitute.For<IMavLinkMessageDecodeHandler>();
        var browser = new TelemetryPacketBrowser(reader, decoder, new MavLinkMessageDefinitionRegistry());
        var page = await browser.ReadPageAsync(input, index, 0, new(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(200, page.Rows.Count);
        Assert.Equal(200, page.NextIndex);
        Assert.Equal(200, decoder.ReceivedCalls().Count());
        Assert.All(page.Rows, row =>
        {
            Assert.Equal("Unknown", row.Name);
            Assert.Equal("FDC000", row.RawHex.Substring(14, 6));
        });
        Assert.Equal(50000, TelemetryPacketBrowser.FindIndex(index, DateTimeOffset.UnixEpoch.AddSeconds(50000)));
    }

    [Fact]
    public async Task FiltersByIdentityNameAndRawBytesAndSupportsCancellation()
    {
        using var input = CreateLog(5);
        var reader = new TelemetryLogReader();
        var index = await reader.IndexAsync(input, "small.tlog", TestContext.Current.CancellationToken);
        var browser = new TelemetryPacketBrowser(reader, Substitute.For<IMavLinkMessageDecodeHandler>(), new MavLinkMessageDefinitionRegistry());
        var page = await browser.ReadPageAsync(input, index, 0, new("FDC000", "Unknown", 1, 2),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, page.Rows.Count);
        Assert.Empty((await browser.ReadPageAsync(input, index, 0, new(SystemId: 2),
            cancellationToken: TestContext.Current.CancellationToken)).Rows);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => browser.ReadPageAsync(input, index, 0, new(),
            cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(19)]
    public async Task TruncatedFinalRecordKeepsCompleteRecords(int truncatedBytes)
    {
        using var input = CreateLog(3);
        input.SetLength(input.Length - truncatedBytes);
        var index = await new TelemetryLogReader().IndexAsync(input, "partial.tlog", TestContext.Current.CancellationToken);
        Assert.Equal(2, index.Entries.Count);
    }

    [Fact]
    public async Task BrowserImportExportRoundTripsWithoutSidecarAndCleansFailedImports()
    {
        var storage = new BrowserLogStorage(4096);
        var files = new LogFileOperations(storage);
        using var input = CreateLog(2);
        var id = await files.ImportAsync(LogStorageArea.Telemetry, "classic.tlog", input, TestContext.Current.CancellationToken);
        await using var export = await storage.ExportAsync(LogStorageArea.Telemetry, id, TestContext.Current.CancellationToken);
        var index = await new TelemetryLogReader().IndexAsync(export.Content, id, TestContext.Current.CancellationToken);
        Assert.Equal(2, index.Entries.Count);
        using var oversized = new MemoryStream(new byte[8192]);
        await Assert.ThrowsAsync<IOException>(() => files.ImportAsync(LogStorageArea.Telemetry, "too-large.tlog",
            oversized, TestContext.Current.CancellationToken));
        Assert.Single(await storage.ListAsync(LogStorageArea.Telemetry, TestContext.Current.CancellationToken));
    }

    private static MemoryStream CreateLog(int count)
    {
        var stream = new MemoryStream();
        var timestamp = new byte[8];
        byte[] frame = [0xFD, 0, 0, 0, 0, 1, 2, 0xFD, 0xC0, 0, 0, 0];
        for (var i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt64BigEndian(timestamp, (ulong)i * 1000000);
            stream.Write(timestamp);
            stream.Write(frame);
        }

        stream.Position = 0;
        return stream;
    }
}

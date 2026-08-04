using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class IntelHexInspectorTests
{
    [Fact]
    public async Task ValidWithBootloaderPackageReturnsMergedRangesAndEvidence()
    {
        var source = Hex(Record(0x04, 0, 0x08, 0x00), Record(0x00, 0x0000, 1, 2, 3, 4),
            Record(0x00, 0x0004, 5, 6), Record(0x00, 0x1000, 7, 8),
            Record(0x05, 0, 0x08, 0x01, 0x00, 0x00), Record(0x01, 0));

        var result = await CreateInspector().InspectAsync(source, TestContext.Current.CancellationToken);

        result.DataBytes.Should().Be(8);
        result.LowestAddress.Should().Be(0x08000000);
        result.HighestAddress.Should().Be(0x08001001);
        result.EntryAddress.Should().Be(0x08010000);
        result.Ranges.Should().HaveCount(2);
        result.Ranges[0].Data.ToArray().Should().Equal(1, 2, 3, 4, 5, 6);
        result.AppearsToContainBootloader.Should().BeTrue();
        result.AppearsToContainApplication.Should().BeFalse();
        result.Sha256.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public async Task ExtendedLinearAddressAndSparseDataRemainSeparateRanges()
    {
        var source = Hex(Record(0x04, 0, 0x08, 0x00), Record(0x00, 0x0000, 0xaa),
            Record(0x00, 0x2000, 0xbb), Record(0x01, 0));

        var result = await CreateInspector().InspectAsync(source, TestContext.Current.CancellationToken);

        result.Ranges.Select(range => range.StartAddress).Should().Equal(0x08000000, 0x08002000);
        result.DataBytes.Should().Be(2);
    }

    [Fact]
    public async Task ExtendedSegmentAddressIsSupported()
    {
        var options = ValidOptions();
        options.Stm32FlashStartAddress = 0x00080000;
        options.Stm32FlashEndAddressExclusive = 0x000A0000;
        options.ExpectedApplicationStartAddress = 0x00090000;
        var source = Hex(Record(0x02, 0, 0x80, 0x00), Record(0x00, 0x0010, 0x42), Record(0x01, 0));

        var result = await CreateInspector(options).InspectAsync(source, TestContext.Current.CancellationToken);

        result.LowestAddress.Should().Be(0x00080010);
    }

    [Fact]
    public async Task IdenticalOverlapIsAcceptedWithWarning()
    {
        var source = Hex(Record(0x04, 0, 0x08, 0x00), Record(0x00, 0, 1, 2),
            Record(0x00, 1, 2, 3), Record(0x01, 0));

        var result = await CreateInspector().InspectAsync(source, TestContext.Current.CancellationToken);

        result.DataBytes.Should().Be(3);
        result.Warnings.Should().Contain(message => message.Contains("overlaps identical", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConflictingOverlapIsRejected()
    {
        var source = Hex(Record(0x04, 0, 0x08, 0x00), Record(0x00, 0, 1, 2),
            Record(0x00, 1, 9), Record(0x01, 0));

        var action = () => CreateInspector().InspectAsync(source);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*conflicts*");
    }

    [Theory]
    [InlineData(":0100000001FF\n:00000001FF\n", "checksum")]
    [InlineData(":020000040800F2\n:0100000001FE\n", "EOF")]
    [InlineData(":GG00000000\n", "hexadecimal")]
    public async Task MalformedInputIsRejected(string text, string expected)
    {
        await using var source = new MemoryStream(Encoding.ASCII.GetBytes(text));
        var action = () => CreateInspector().InspectAsync(source);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage($"*{expected}*");
    }

    [Fact]
    public async Task AddressOverflowIsRejected()
    {
        var options = ValidOptions();
        options.Stm32FlashStartAddress = 0;
        options.Stm32FlashEndAddressExclusive = uint.MaxValue;
        options.ExpectedApplicationStartAddress = 1;
        var source = Hex(Record(0x04, 0, 0xff, 0xff), Record(0x00, 0xffff, 1, 2), Record(0x01, 0));

        var action = () => CreateInspector(options).InspectAsync(source);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*overflowed*");
    }

    [Fact]
    public async Task SourceLargerThanConfiguredLimitIsRejectedBeforeParsing()
    {
        var options = ValidOptions();
        options.MaximumIntelHexSourceBytes = 8;
        var action = () => CreateInspector(options).InspectAsync(Hex(Record(0x01, 0)));

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*exceeds*");
    }

    [Fact]
    public async Task DataOutsideConfiguredFlashPolicyIsRejected()
    {
        var source = Hex(Record(0x04, 0, 0x07, 0xff), Record(0x00, 0, 1), Record(0x01, 0));
        var action = () => CreateInspector().InspectAsync(source);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*outside*");
    }

    private static IntelHexInspector CreateInspector(DfuOptions? options = null) =>
        new(Options.Create(options ?? ValidOptions()), TimeProvider.System);

    private static DfuOptions ValidOptions() => new()
    {
        MaximumIntelHexSourceBytes = 1024 * 1024,
        MaximumIntelHexDataBytes = 1024 * 1024,
        MaximumIntelHexAddressSpan = 1024 * 1024
    };

    private static MemoryStream Hex(params string[] records) =>
        new(Encoding.ASCII.GetBytes(string.Join('\n', records) + "\n"));

    private static string Record(byte type, ushort address, params byte[] payload)
    {
        var bytes = new List<byte> { checked((byte)payload.Length), (byte)(address >> 8), (byte)address, type };
        bytes.AddRange(payload);
        bytes.Add(unchecked((byte)(0 - bytes.Sum(value => value))));
        return ":" + Convert.ToHexString(bytes.ToArray());
    }
}

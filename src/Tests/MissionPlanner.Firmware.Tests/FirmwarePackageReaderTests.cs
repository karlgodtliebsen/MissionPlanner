using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Images;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwarePackageReaderTests
{
    [Fact]
    public async Task ReadsInternalAndExternalImagesAndMetadata()
    {
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.apj"));

        var package = await CreateReader().ReadAsync(stream, TestContext.Current.CancellationToken);

        package.BoardId.Should().Be(50);
        package.Image.ToArray().Should().Equal(1, 2, 3, 4, 5);
        package.ExternalImage.ToArray().Should().Equal(9, 8, 7);
        package.ImageMaximumSize.Should().Be(1024);
        package.RawMetadata.Should().ContainKey("future_metadata");
    }

    [Theory]
    [InlineData("bad", 50, 5, 1024, "eJxjZGJmYQUAACgAEA==")]
    [InlineData("APJFWv1", 0, 5, 1024, "eJxjZGJmYQUAACgAEA==")]
    [InlineData("APJFWv1", 50, 5, 1024, "not-base64")]
    [InlineData("APJFWv1", 50, 6, 1024, "eJxjZGJmYQUAACgAEA==")]
    [InlineData("APJFWv1", 50, 5, 4, "eJxjZGJmYQUAACgAEA==")]
    public async Task RejectsInvalidPackages(string magic, int boardId, int imageSize, int maximum, string image)
    {
        var json = JsonSerializer.Serialize(new
        {
            magic,
            board_id = boardId,
            image_size = imageSize,
            image_maxsize = maximum,
            image
        });
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var act = async () => await CreateReader().ReadAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwarePackageException>();
    }

    [Fact]
    public async Task RejectsConfiguredDecompressionLimit()
    {
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.apj"));
        var reader = new ApjFirmwarePackageReader(Options.Create(new FirmwareOptions { MaximumFirmwareImageBytes = 4 }));

        var act = async () => await reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwarePackageException>();
    }

    [Fact]
    public void ChecksumMatchesUpstreamVectorsAndPadding()
    {
        ArduPilotFirmwareChecksum.Update(0, Encoding.ASCII.GetBytes("123456789")).Should().Be(0x2dfd2d88);
        ArduPilotFirmwareChecksum.Calculate(Encoding.ASCII.GetBytes("abc"), 16).Should().Be(0x708ff7d5);
    }

    private static ApjFirmwarePackageReader CreateReader()
    {
        return new ApjFirmwarePackageReader(Options.Create(new FirmwareOptions()));
    }
}

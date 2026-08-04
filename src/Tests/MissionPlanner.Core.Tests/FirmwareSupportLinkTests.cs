using FluentAssertions;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Core.Tests;

public sealed class FirmwareSupportLinkTests
{
    [Fact]
    public void CatalogueContainsUniqueHttpsLinksAndEveryCategory()
    {
        var links = new FirmwareSupportLinkProvider().GetLinks();

        links.Should().NotBeEmpty();
        links.Should().OnlyContain(link =>
            !string.IsNullOrWhiteSpace(link.Title) &&
            !string.IsNullOrWhiteSpace(link.Description) &&
            link.Uri.IsAbsoluteUri &&
            link.Uri.Scheme == Uri.UriSchemeHttps);
        links.Select(link => link.Uri).Should().OnlyHaveUniqueItems();
        links.Select(link => link.Category).Distinct().Should().BeEquivalentTo(Enum.GetValues<FirmwareSupportCategory>());
        links.Where(link => link.Category == FirmwareSupportCategory.DriverFallback).Should().OnlyContain(link => link.IsThirdParty);
    }

    [Fact]
    public async Task LinkModelAndLauncherRejectNonHttpsDestinations()
    {
        var create = () => new FirmwareSupportLink(
            "Unsafe", "Unsafe destination", new Uri("http://example.test"), FirmwareSupportCategory.DriverFallback);
        var launch = async () => await new ExternalLinkLauncher().OpenAsync(new Uri("file:///tmp/help.html"));

        create.Should().Throw<ArgumentException>().WithMessage("*HTTPS*");
        await launch.Should().ThrowAsync<ArgumentException>().WithMessage("*HTTPS*");
    }

    [Theory]
    [InlineData(true, false, false, true, false, false, FirmwareReleaseChannel.Stable, false, "*_with_bl.hex")]
    [InlineData(false, true, false, true, false, false, FirmwareReleaseChannel.Stable, false, "Zadig")]
    [InlineData(false, false, true, true, false, false, FirmwareReleaseChannel.Stable, false, "exact hardware target")]
    [InlineData(false, false, false, false, true, false, FirmwareReleaseChannel.Stable, false, "does not match")]
    [InlineData(false, false, false, true, false, false, FirmwareReleaseChannel.Latest, false, "development build")]
    [InlineData(false, false, false, true, false, false, FirmwareReleaseChannel.Stable, true, "provenance")]
    public void ContextHelpPrioritizesActionableGuidance(
        bool dfuPresent,
        bool wrongDriver,
        bool ambiguous,
        bool serialPresent,
        bool boardMismatch,
        bool cubeProgrammerAvailable,
        FirmwareReleaseChannel channel,
        bool custom,
        string expected)
    {
        var help = FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(
            dfuPresent,
            cubeProgrammerAvailable,
            wrongDriver,
            serialPresent,
            ambiguous,
            boardMismatch,
            channel,
            custom));

        (help.Title + " " + help.Content).Should().Contain(expected);
    }

    [Fact]
    public void OfflineSectionsCoverEveryTopicAndSafetyPolicy()
    {
        var sections = FirmwareSupportContent.Sections;
        var allContent = string.Join(" ", sections.Select(section => section.Content));

        sections.Select(section => section.Topic).Should().BeEquivalentTo(Enum.GetValues<FirmwareSupportTopic>());
        sections.Should().OnlyContain(section =>
            !string.IsNullOrWhiteSpace(section.Title) && !string.IsNullOrWhiteSpace(section.Content));
        allContent.Should().Contain("*_with_bl.hex");
        allContent.Should().Contain("exact target");
        allContent.Should().Contain("Replacing the driver for the wrong USB device");
        allContent.Should().Contain("Frame geometry is configured later");
        allContent.Should().NotContain("frame gallery");
    }

    [Fact]
    public void DeviceManagerAvailabilityMatchesTheHostPlatform()
    {
        new DeviceManagerLauncher().IsAvailable.Should().Be(OperatingSystem.IsWindows());
    }
}

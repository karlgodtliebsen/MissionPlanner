using FluentAssertions;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;

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
}

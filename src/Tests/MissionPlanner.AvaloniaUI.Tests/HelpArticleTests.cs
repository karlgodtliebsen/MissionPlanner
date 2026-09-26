using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class HelpArticleTests
{
    [Fact]
    public void FirmwareArticlePreservesAllOfflineSections()
    {
        var document = FirmwareHelpDocumentFactory.Create();
        Assert.Equal("Install Firmware", document.Title);
        foreach (var section in FirmwareSupportContent.Sections)
        {
            Assert.Contains("### " + UserDocumentBuilder.Escape(section.Title), document.Markdown);
            Assert.Contains(UserDocumentBuilder.Escape(section.Content), document.Markdown);
        }
    }

    [Fact]
    public async Task SharedArticleRetainsExternalLinksAndDeviceManagerAction()
    {
        var links = new FirmwareSupportLinkProvider();
        var launcher = Substitute.For<IExternalLinkLauncher>();
        var devices = Substitute.For<IDeviceManagerLauncher>();
        devices.IsAvailable.Returns(true);
        using var model = new FirmwareHelpViewModel(links, launcher, devices,
            Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(), NullLogger<FirmwareHelpViewModel>.Instance);
        var article = model.Article;
        Assert.Equal("Install Firmware", article.Title);
        Assert.Equal(links.GetLinks().Count, article.Resources.Count);
        var motors = Assert.Single(article.Resources.Where(resource => resource.Uri.Fragment == "#motor-order-diagrams"));
        await article.OpenResourceCommand.ExecuteAsync(motors);
        await launcher.Received(1).OpenAsync(motors.Uri, Arg.Any<CancellationToken>());
        Assert.True(article.CanOpenDeviceManager);
        await article.OpenDeviceManagerCommand.ExecuteAsync(null);
        await devices.Received(1).OpenAsync(Arg.Any<CancellationToken>());
    }
}

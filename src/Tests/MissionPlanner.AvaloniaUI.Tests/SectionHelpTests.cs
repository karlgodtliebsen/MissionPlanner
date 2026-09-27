using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SectionHelpTests
{
    [Theory]
    [InlineData("Mandatory Hardware", "InitSetup/MandatoryHardware/MandatoryHardwarePage.axaml")]
    [InlineData("Optional Hardware", "InitSetup/OptionalHardware/OptionalHardwarePage.axaml")]
    [InlineData("Configuration", "ConfigTuning/ConfigurationPage.axaml")]
    public void EveryVisibleSectionHasOneCompleteHelpArticle(string group, string view)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src/UI/MissionPlanner.App")))
            root = root.Parent;
        Assert.NotNull(root);
        var page = XDocument.Load(Path.Combine(root.FullName, "src/UI/MissionPlanner.App/Views", view));
        var headers = page.Descendants().Where(element => element.Name.LocalName == "TabItem")
            .Select(element => element.Attribute("Header")!.Value.Trim()).ToArray();
        var articles = SectionHelpCatalog.Sections.Where(section => section.Group == group).ToArray();
        Assert.Equal(headers, articles.Select(article => article.Title).ToArray());
        foreach (var article in articles)
        {
            Assert.NotEmpty(article.Steps);
            Assert.All(article.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step)));
            var document = SectionHelpCatalog.CreateDocument(article);
            Assert.Equal(article.Title, document.Title);
            Assert.Contains("### How to use this section", document.Markdown);
            Assert.Contains("### Verify the result", document.Markdown);
            Assert.Contains("### Important details", document.Markdown);
            Assert.Equal("https", article.Documentation.Scheme);
            Assert.Equal("ardupilot.org", article.Documentation.Host);
        }
    }

    [Fact]
    public void HelpSearchFindsCategoriesAndContentAndFirmwareLinkClearsFilter()
    {
        using var model = new HelpViewModel(new FirmwareSupportLinkProvider(),
            Substitute.For<IExternalLinkLauncher>(), Substitute.For<IDeviceManagerLauncher>(),
            Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>(), NullLogger<HelpViewModel>.Instance);
        Assert.Equal(44, model.Topics.Count);
        Assert.Equal("Tutorial", model.Topics[0].Title);
        Assert.Equal("Install Firmware", model.Topics[1].Title);
        model.SearchText = "  CubeID  ";
        Assert.Equal("CubeID Update", Assert.Single(model.VisibleTopics).Title);
        model.SearchText = "Mandatory Hardware";
        Assert.Equal(14, model.VisibleTopics.Count());
        model.SearchText = "no firmware-write command";
        Assert.Equal("CubeID Update", Assert.Single(model.VisibleTopics).Title);
        model.SelectInstallFirmware();
        Assert.Equal(string.Empty, model.SearchText);
        Assert.Equal("Install Firmware", model.SelectedTopic!.Title);
        Assert.Contains(model.SelectedTopic, model.VisibleTopics);
    }
}

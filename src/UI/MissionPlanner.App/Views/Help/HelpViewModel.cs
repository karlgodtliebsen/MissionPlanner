using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Help;

/// <summary>Owns the general Help topic catalogue and selected topic.</summary>
public partial class HelpViewModel : ViewModelBase
{
    private readonly HelpTopic installFirmware;

    /// <summary>Creates the Help hub with Tutorial first, followed by firmware and section guides.</summary>
    public HelpViewModel(IFirmwareSupportLinkProvider supportLinks, IExternalLinkLauncher links,
        IDeviceManagerLauncher devices, IUiDispatcher dispatcher, IDomainEventHub events,
        ILogger<HelpViewModel> logger) : base(logger, dispatcher, events)
    {
        var article = new HelpArticleViewModel("Install Firmware", FirmwareHelpDocumentFactory.Create(),
            supportLinks.GetLinks().Select(link => new HelpResource(link.Title, link.Description, link.Uri)).ToArray(),
            links, devices, logger, dispatcher, events);
        installFirmware = new HelpTopic("Install Firmware", article);
        Topics = [new HelpTopic("Tutorial"), installFirmware, .. SectionHelpCatalog.Sections.Select(section =>
            new HelpTopic(section.Title, new HelpArticleViewModel(section.Title,
                SectionHelpCatalog.CreateDocument(section),
                [new HelpResource("ArduPilot documentation", "Open the official guide in your default browser. Select documentation for your vehicle type and firmware version.", section.Documentation)],
                links, null, logger, dispatcher, events), section.Group))];
        SelectedTopic = Topics[0];
    }

    /// <summary>Gets available help topics in display order.</summary>
    public IReadOnlyList<HelpTopic> Topics { get; }

    /// <summary>Filters the catalogue by section name, category or help text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTopics))]
    public partial string SearchText { get; set; } = string.Empty;

    public IEnumerable<HelpTopic> VisibleTopics => Topics.Where(topic =>
        string.IsNullOrWhiteSpace(SearchText) ||
        (topic.Category + " " + topic.Title + " " + topic.Article?.Document.Markdown.Replace("\\", string.Empty))
            .Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Gets or sets the topic displayed in Help.</summary>
    [ObservableProperty]
    public partial HelpTopic? SelectedTopic { get; set; }

    /// <summary>Opens the firmware article independently of the current topic selection.</summary>
    public void SelectInstallFirmware()
    {
        SearchText = string.Empty;
        SelectedTopic = installFirmware;
    }
}

/// <summary>A Help topic containing either the interactive tutorial or a Markdown article.</summary>
public sealed record HelpTopic(string Title, HelpArticleViewModel? Article = null, string Category = "Getting started")
{
    public bool IsTutorial => Article is null;
}

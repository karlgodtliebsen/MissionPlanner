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

    /// <summary>Creates the Help hub with Tutorial first and the firmware guide.</summary>
    public HelpViewModel(IFirmwareSupportLinkProvider supportLinks, IExternalLinkLauncher links,
        IDeviceManagerLauncher devices, IUiDispatcher dispatcher, IDomainEventHub events,
        ILogger<HelpViewModel> logger) : base(logger, dispatcher, events)
    {
        var article = new HelpArticleViewModel("Install Firmware", FirmwareHelpDocumentFactory.Create(),
            supportLinks.GetLinks().Select(link => new HelpResource(link.Title, link.Description, link.Uri)).ToArray(),
            links, devices, logger, dispatcher, events);
        installFirmware = new HelpTopic("Install Firmware", article);
        Topics = [new HelpTopic("Tutorial"), installFirmware];
        SelectedTopic = Topics[0];
    }

    /// <summary>Gets available help topics in display order.</summary>
    public IReadOnlyList<HelpTopic> Topics { get; }

    /// <summary>Gets or sets the topic displayed in Help.</summary>
    [ObservableProperty]
    public partial HelpTopic? SelectedTopic { get; set; }

    /// <summary>Opens the firmware article independently of the current topic selection.</summary>
    public void SelectInstallFirmware() => SelectedTopic = installFirmware;
}

/// <summary>A Help topic containing either the interactive tutorial or a Markdown article.</summary>
public sealed record HelpTopic(string Title, HelpArticleViewModel? Article = null)
{
    public bool IsTutorial => Article is null;
}

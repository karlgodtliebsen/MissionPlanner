using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;

/// <summary>Owns help panel state and commands.</summary>
public sealed partial class FirmwareHelpViewModel : ViewModelBase
{
    private readonly IExternalLinkLauncher externalLinkLauncher;
    private readonly IDeviceManagerLauncher deviceManagerLauncher;
    /// <summary>Initializes the help panel.</summary>
    public FirmwareHelpViewModel(
        IFirmwareSupportLinkProvider supportLinkProvider,
        IExternalLinkLauncher externalLinkLauncher,
        IDeviceManagerLauncher deviceManagerLauncher,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub,
        ILogger<FirmwareHelpViewModel> logger) : base(logger, dispatcher, eventHub)
    {
        SupportLinks = supportLinkProvider.GetLinks();
        Article = new MissionPlanner.App.Views.Help.HelpArticleViewModel("Install Firmware",
            MissionPlanner.App.Presentation.Documents.FirmwareHelpDocumentFactory.Create(),
            SupportLinks.Select(link => new MissionPlanner.App.Views.Help.HelpResource(link.Title, link.Description, link.Uri)).ToArray(),
            externalLinkLauncher, deviceManagerLauncher, logger, dispatcher, eventHub);
        this.externalLinkLauncher = externalLinkLauncher;
        this.deviceManagerLauncher = deviceManagerLauncher;
    }
    /// <summary>Gets concise help that remains available offline.</summary>
    public MissionPlanner.App.Views.Help.HelpArticleViewModel Article { get; }

    /// <summary>Gets concise help that remains available offline.</summary>
    public IReadOnlyList<FirmwareSupportSection> SupportSections { get; } = FirmwareSupportContent.Sections;

    /// <summary>Gets curated official and fallback support destinations.</summary>
    public IReadOnlyList<FirmwareSupportLink> SupportLinks
    {
        get;
    }

    /// <summary>Gets whether this host can open Windows Device Manager.</summary>
    public bool CanOpenDeviceManager => deviceManagerLauncher.IsAvailable;

    [RelayCommand]
    private Task OpenSupportLinkAsync(FirmwareSupportLink link, CancellationToken cancellationToken)
    {
        return externalLinkLauncher.OpenAsync(link.Uri, cancellationToken);
    }

    [RelayCommand]
    private Task OpenDeviceManagerAsync(CancellationToken cancellationToken)
    {
        return deviceManagerLauncher.OpenAsync(cancellationToken);
    }
}

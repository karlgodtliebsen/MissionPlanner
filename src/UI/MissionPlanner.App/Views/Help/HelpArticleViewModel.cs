using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Help;

/// <summary>A reusable Markdown help topic with explicit external resources and optional host tools.</summary>
public sealed partial class HelpArticleViewModel : ViewModelBase
{
    private readonly IExternalLinkLauncher links;
    private readonly IDeviceManagerLauncher? deviceManager;

    /// <summary>Creates an offline topic and its supporting actions.</summary>
    public HelpArticleViewModel(string title, UserDocument document, IReadOnlyList<HelpResource> resources,
        IExternalLinkLauncher links, IDeviceManagerLauncher? deviceManager,
        ILogger logger, IUiDispatcher dispatcher, IDomainEventHub events) : base(logger, dispatcher, events)
    {
        Title = title;
        Document = document;
        Resources = resources;
        this.links = links;
        this.deviceManager = deviceManager;
    }

    /// <summary>Gets the topic title used in navigation.</summary>
    public string Title { get; }
    /// <summary>Gets the offline Markdown article.</summary>
    public UserDocument Document { get; }
    /// <summary>Gets the external supporting resources.</summary>
    public IReadOnlyList<HelpResource> Resources { get; }
    /// <summary>Gets whether this topic offers Device Manager on this host.</summary>
    public bool CanOpenDeviceManager => deviceManager?.IsAvailable == true;

    [RelayCommand]
    private async Task OpenResourceAsync(HelpResource resource, CancellationToken cancellationToken)
    {
        try { await links.OpenAsync(resource.Uri, cancellationToken); }
        catch (Exception error) { SetMessages(errorMessage: $"Could not open {resource.Title}: {error.Message}"); }
    }

    [RelayCommand]
    private async Task OpenDeviceManagerAsync(CancellationToken cancellationToken)
    {
        if (!CanOpenDeviceManager) return;
        try { await deviceManager!.OpenAsync(cancellationToken); }
        catch (Exception error) { SetMessages(errorMessage: $"Could not open Device Manager: {error.Message}"); }
    }
}

/// <summary>A titled external resource with supporting explanatory text.</summary>
public sealed record HelpResource(string Title, string Description, Uri Uri);

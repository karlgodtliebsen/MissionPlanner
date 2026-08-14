using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.App.Views.Introduction.Services;

namespace MissionPlanner.App.Views.Introduction;

/// <summary>
/// Provides the public API for IntroductionViewModel.
/// </summary>
public partial class IntroductionViewModel(IIntroductionContentLoader contentLoader, ILogger<IntroductionViewModel> logger) : ObservableObject, IDisposable
{
    private bool isInitialized;

    public ObservableRangeCollection<IntroductionTopic> Topics { get; } = [];

    [ObservableProperty] public partial IntroductionTopic? SelectedTopic { get; set; }

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string Title { get; set; } = "Introduction";

    [ObservableProperty] public partial string Subtitle { get; set; } = "Quick guide to MissionPlanner NextGeneration";

    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Initializes the IntroductionViewModel by loading the introduction content.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (isInitialized)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var document = await contentLoader.LoadAsync(cancellationToken);

            Topics.Clear();
            Topics.AddRange(document.Topics);
            Title = document.Title;
            Subtitle = document.Subtitle;
            SelectedTopic = Topics.FirstOrDefault();
            isInitialized = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.Print(ex.ToString());
            logger.LogError(ex, "Could not initialize the MissionPlanner Introduction page.");
            ErrorMessage =
                "MissionPlanner could not load the Introduction content. " +
                "Check that the Introduction Content files are packaged as MauiAsset items.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool SelectTopic(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var topic = Topics.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        if (topic is null)
        {
            return false;
        }

        SelectedTopic = topic;
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

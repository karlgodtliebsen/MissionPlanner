using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Services;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Introduction;

/// <summary>
/// Provides the public API for IntroductionViewModel.
/// </summary>
public partial class IntroductionViewModel(IIntroductionContentLoader contentLoader, ILogger<IntroductionViewModel> logger) : ViewModelBase(logger)
{
    private bool isInitialized;
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    public ObservableRangeCollection<IntroductionTopic> Topics
    {
        get;
    } = [];

    [ObservableProperty]
    public partial IntroductionTopic? SelectedTopic
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string Title
    {
        get;
        set;
    } = "Introduction";

    [ObservableProperty]
    public partial string Subtitle
    {
        get;
        set;
    } = "Quick guide to MissionPlanner NextGeneration";


    /// <summary>
    /// Selects a topic by its ID.
    /// </summary>
    /// <param name="id">The ID of the topic to select.</param>
    /// <returns>True if the topic was found and selected; otherwise, false.</returns>
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
    public override async Task ActivateAsync()
    {
        if (isInitialized)
        {
            return;
        }

        SetBusy();
        SetMessages(null, null);
        cancellationTokenSource = new();
        try
        {
            var document = await contentLoader.LoadAsync(cancellationTokenSource.Token);

            Topics.Clear();
            Topics.AddRange(document.Topics);
            Title = document.Title;
            Subtitle = document.Subtitle;
            SelectedTopic = Topics.FirstOrDefault();
            isInitialized = true;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
        {
            Debug.Print("OperationCanceledException");
            throw;
        }
        catch (Exception ex)
        {
            Debug.Print("Exception in ActivateAsync:\n" + ex.ToString());
            Logger.LogError(ex, "Could not initialize the MissionPlanner Introduction page.");
            SetMessages(null,
                "MissionPlanner could not load the Introduction content. " +
                "Check that the Introduction Content files are packaged as MauiAsset items.");
        }
        finally
        {
            ResetBusy();
        }
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new();
        return Task.CompletedTask;
    }
}


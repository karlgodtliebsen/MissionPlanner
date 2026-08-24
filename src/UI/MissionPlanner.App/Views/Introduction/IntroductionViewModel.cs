using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.App.Views.Introduction.Services;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.Introduction;

/// <summary>
/// Provides the public API for IntroductionViewModel.
/// </summary>
public partial class IntroductionViewModel(IIntroductionContentLoader contentLoader, ILogger<IntroductionViewModel> logger) : BaseViewModel(logger)
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

        IsBusy = true;
        ErrorMessage = null;
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

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new();
        return Task.CompletedTask;
    }
}

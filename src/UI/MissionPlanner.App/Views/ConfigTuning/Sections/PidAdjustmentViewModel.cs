using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

/// <summary>Presents an optional local P/I preview, leaving writes to the shared parameter workspace.</summary>
public sealed partial class PidAdjustmentViewModel(PidAdjustmentAssistant assistant, ILogger<PidAdjustmentViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();
    private PidAdjustmentReview? review;
    /// <summary>Gets or sets whether coupled roll/pitch P/I is selected.</summary>
    [ObservableProperty] public partial bool RollPitch { get; set; } = true;
    /// <summary>Gets or sets whether vertical acceleration P/I is selected.</summary>
    [ObservableProperty] public partial bool Climb { get; set; }
    /// <summary>Gets or sets a modest scale relative to current gains; 100 leaves them unchanged.</summary>
    [ObservableProperty] public partial double Percent { get; set; } = 100;
    /// <summary>Gets the immutable preview rows.</summary>
    public ObservableCollection<ReviewedParameterEdit> Changes { get; } = [];
    /// <summary>Gets the exact controls captured by the current preview.</summary>
    [ObservableProperty] public partial string PreviewDescription { get; private set; } = "Preview before staging.";
    partial void OnRollPitchChanged(bool value) => ClearReview();
    partial void OnClimbChanged(bool value) => ClearReview();
    partial void OnPercentChanged(double value) => ClearReview();
    private void ClearReview() { review = null; Changes.Clear(); PreviewDescription = "Settings changed. Preview again."; }
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PreviewAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            ClearReview();
            var rollPitch = RollPitch; var climb = Climb; var percent = Percent;
            var candidate = await assistant.PreviewAsync(rollPitch, climb, percent, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (rollPitch != RollPitch || climb != Climb || percent != Percent) throw new InvalidOperationException("Controls changed while loading; preview again.");
            review = candidate;
            foreach (var change in candidate.Changes) Changes.Add(change);
            PreviewDescription = $"Vehicle {candidate.Session.VehicleId}: {percent:0}% of current P/I; roll/pitch={rollPitch}, climb={climb}. Values are rounded to advertised increments. D, feed-forward and limits are unchanged.";
            SetMessages("Preview only. No values have been staged or sent.");
        }
        catch (OperationCanceledException) { SetMessages("Preview cancelled."); }
        catch (Exception ex) { SetMessages(ex); }
    }
    [RelayCommand]
    private void Stage()
    {
        try
        {
            assistant.Stage(review ?? throw new InvalidOperationException("Preview the changes first."));
            review = null;
            SetMessages("Reviewed P/I changes staged locally. Apply them through Extended Tuning or Parameters Editor after reviewing the complete pending changes. No vehicle write was made by the assistant.");
        }
        catch (Exception ex) { SetMessages(ex); }
    }
    /// <inheritdoc />
    public override Task ActivateAsync() { if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); } return base.ActivateAsync(); }
    /// <inheritdoc />
    public override async Task DeactivateAsync() { lifetime.Cancel(); if (PreviewCommand.ExecutionTask is { } task) await task; ClearReview(); await base.DeactivateAsync(); }
    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }
}

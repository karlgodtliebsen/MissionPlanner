using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Saves the mission to a file and loads or appends mission files.</summary>
public interface IMissionFileWorkflow : IMissionWorkflow
{
    /// <summary>Asks for a format and file, then saves the mission.</summary>
    Task SaveWpFileAsync(CancellationToken cancellationToken);

    /// <summary>Asks for a mission file and replaces the mission with it.</summary>
    Task LoadWpFileAsync(CancellationToken cancellationToken);

    /// <summary>Asks for a mission file and appends its items to the mission.</summary>
    Task LoadAndAppendAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IMissionFileWorkflow" />
public sealed class MissionFileWorkflow : MissionWorkflowBase, IMissionFileWorkflow
{
    private readonly IMissionFileInteraction fileInteraction;
    private readonly ILogger<MissionFileWorkflow> logger;

    /// <summary>Initializes a new instance of the <see cref="MissionFileWorkflow"/> class.</summary>
    public MissionFileWorkflow(IMissionFileInteraction fileInteraction, ILogger<MissionFileWorkflow> logger)
    {
        this.fileInteraction = fileInteraction;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveWpFileAsync(CancellationToken cancellationToken)
    {
        var result = await fileInteraction.SaveMissionAsync(Mission, HomePosition, cancellationToken);
        if (result.Message is not null)
        {
            ShowStatus(result.Message);
        }

        if (result.ClearDirtyRows)
        {
            Host.ClearPendingRowEdits();
        }
    }

    /// <inheritdoc />
    public Task LoadWpFileAsync(CancellationToken cancellationToken)
    {
        return LoadMissionFileAsync(false, cancellationToken);
    }

    /// <inheritdoc />
    public Task LoadAndAppendAsync(CancellationToken cancellationToken)
    {
        return LoadMissionFileAsync(true, cancellationToken);
    }

    private async Task LoadMissionFileAsync(bool append, CancellationToken cancellationToken)
    {
        var loaded = await fileInteraction.LoadMissionAsync(cancellationToken);
        if (loaded.Message is not null)
        {
            ShowStatus(loaded.Message);
            return;
        }

        if (loaded.Content is not { } parsed)
        {
            return;
        }

        try
        {
            var target = append
                ? Mission
                : new Mission(MissionId.New(), parsed.Name ?? Path.GetFileNameWithoutExtension(loaded.FileName));
            if (!append && parsed.Home is not null)
            {
                Host.HomePosition = parsed.Home;
            }

            foreach (var item in parsed.Items)
            {
                target.Add(item);
            }

            Host.SetMission(target, loaded.SuccessMessage);
            Host.RequestFitToMission();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mission file");
            ShowStatus($"Load failed: {ex.Message}");
        }
    }
}

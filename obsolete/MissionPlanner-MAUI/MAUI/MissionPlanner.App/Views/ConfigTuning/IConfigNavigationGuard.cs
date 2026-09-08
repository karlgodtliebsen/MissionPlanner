namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>Protects Config navigation when the shared parameter session has unapplied edits.</summary>
public interface IConfigNavigationGuard
{
    /// <summary>Determines whether a requested navigation may continue.</summary>
    /// <param name="staysWithinConfig">Whether the destination remains inside the Config workspace.</param>
    /// <param name="cancellationToken">A token that cancels the confirmation prompt.</param>
    /// <returns><see langword="true"/> when navigation may continue.</returns>
    Task<bool> CanNavigateAsync(bool staysWithinConfig, CancellationToken cancellationToken = default);
}

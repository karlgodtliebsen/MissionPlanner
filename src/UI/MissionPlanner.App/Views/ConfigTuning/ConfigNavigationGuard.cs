using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>Prompts before leaving Config with unapplied vehicle parameter changes.</summary>
public sealed class ConfigNavigationGuard(
    IParameterEditSessionFactory editSessions,
    IUserConfirmationService confirmation) : IConfigNavigationGuard
{
    /// <inheritdoc />
    public async Task<bool> CanNavigateAsync(bool staysWithinConfig, CancellationToken cancellationToken = default)
    {
        if (staysWithinConfig || !editSessions.HasUnappliedChanges)
        {
            return true;
        }

        var accepted = await confirmation.ConfirmAsync(
            "Unapplied parameter changes",
            "Leaving Config will discard parameter values that have not been confirmed by the vehicle.",
            "Leave and discard",
            cancellationToken);
        if (accepted)
        {
            editSessions.DiscardPendingChanges();
        }

        return accepted;
    }
}

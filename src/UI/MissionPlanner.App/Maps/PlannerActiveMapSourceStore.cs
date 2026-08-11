using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.App.Maps;

/// <summary>Adapts the authoritative Planner source selection to offline-pack ownership.</summary>
public sealed class PlannerActiveMapSourceStore(IPlannerSettingsService settings) : IActiveMapSourceStore
{
    /// <inheritdoc />
    public string SelectedSourceId => settings.Current.Map.SelectedSourceId;

    /// <inheritdoc />
    public async ValueTask SetSelectedSourceIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var result = await settings.SaveAsync(settings.Current with { Map = settings.Current.Map with { SelectedSourceId = sourceId } }, cancellationToken).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException("The fallback map source could not be persisted.");
    }
}

namespace MissionPlanner.Maps.Offline;

/// <summary>Gets and changes the authoritative active map source ID.</summary>
public interface IActiveMapSourceStore
{
    /// <summary>Gets the active stable source identifier.</summary>
    string SelectedSourceId { get; }

    /// <summary>Changes the active stable source identifier.</summary>
    ValueTask SetSelectedSourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}

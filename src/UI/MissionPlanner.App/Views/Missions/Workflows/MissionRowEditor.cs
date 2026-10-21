using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.Missions.Workflows;

/// <summary>Owns the mission list rows and the pending (not yet applied) edits made in them.</summary>
public interface IMissionRowEditor : IMissionWorkflow, IDisposable
{
    /// <summary>Raised when the one-line mission summary changes.</summary>
    event Action<string>? SummaryChanged;

    /// <summary>Raised when rows were rebuilt or the set of edited rows changed.</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>Gets the display rows for the mission items, kept in sync with the mission.</summary>
    ObservableCollection<MissionItemRow> Rows { get; }

    /// <summary>Gets the rows that have been edited but not yet applied.</summary>
    ObservableCollection<MissionItemRow> DirtyRows { get; }

    /// <summary>Returns whether any row has pending edits.</summary>
    bool HasDirtyRows();

    /// <summary>Rebuilds every row from the mission, discarding pending edits.</summary>
    void Rebuild();

    /// <summary>Applies every pending row edit back to the mission.</summary>
    void ApplyAll();

    /// <summary>Forgets the pending edits without rebuilding the rows.</summary>
    void ClearDirty();
}

/// <inheritdoc cref="IMissionRowEditor" />
public sealed class MissionRowEditor : MissionWorkflowBase, IMissionRowEditor
{
    private readonly IMissionRowProjector rowProjector;
    private readonly ILogger<MissionRowEditor> logger;

    /// <summary>Initializes a new instance of the <see cref="MissionRowEditor"/> class.</summary>
    public MissionRowEditor(IMissionRowProjector rowProjector, ILogger<MissionRowEditor> logger)
    {
        this.rowProjector = rowProjector;
        this.logger = logger;
    }

    /// <inheritdoc />
    public event Action<string>? SummaryChanged;

    /// <inheritdoc />
    public event EventHandler? DirtyStateChanged;

    /// <inheritdoc />
    public ObservableCollection<MissionItemRow> Rows { get; } = [];

    /// <inheritdoc />
    public ObservableCollection<MissionItemRow> DirtyRows { get; } = [];

    /// <inheritdoc />
    public bool HasDirtyRows()
    {
        return DirtyRows.Any();
    }

    /// <inheritdoc />
    public void Rebuild()
    {
        DisposeRows();
        DirtyRows.Clear();
        Rows.Clear();

        var projection = rowProjector.Project(Mission, Host.HomePosition);
        foreach (var row in projection.Rows)
        {
            row.AttachNotifications(ApplyRowEdit, OnRowEdited);
            Rows.Add(row);
        }

        SummaryChanged?.Invoke(projection.Summary);
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ApplyAll()
    {
        if (!HasDirtyRows())
        {
            return;
        }

        foreach (var row in DirtyRows.ToList())
        {
            ApplyRowEdit(row);
        }

        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ClearDirty()
    {
        DirtyRows.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeRows();
    }

    private void DisposeRows()
    {
        foreach (var row in Rows)
        {
            row.Dispose();
        }
    }

    private void OnRowEdited(MissionItemRow row)
    {
        if (!DirtyRows.Any(r => r.Equals(row)))
        {
            DirtyRows.Add(row);
        }

        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        SummaryChanged?.Invoke("EditStateChanged IsDirty: " + HasDirtyRows());
    }

    private void ApplyRowEdit(MissionItemRow row)
    {
        row.Dispose();
        var index = Mission.Items.ToList().FindIndex(x => x.Id == row.Id);
        if (index < 0)
        {
            return;
        }

        try
        {
            Mission.Replace(row.Id, rowProjector.ToMissionItem(row, index));
            OnMissionChanged($"Item {row.Number} updated.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply row edit for item {Number}", row.Number);
            ShowStatus($"Edit failed: {ex.Message}");
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using ParameterItemViewModel = MissionPlanner.App.Models.ParameterItemViewModel;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one metadata-backed advanced tuning field.</summary>
public sealed partial class AdvancedTuningFieldViewModel : ObservableObject
{
    /// <summary>Initializes an advanced field projection.</summary>
    /// <param name="resolved">The resolved field.</param>
    /// <param name="session">The shared parameter session.</param>
    public AdvancedTuningFieldViewModel(ResolvedAdvancedTuningField resolved, IParameterEditSession session)
    {
        Definition = resolved.Definition;
        ParameterName = resolved.ParameterName;
        Editor = new ParameterItemViewModel(session, session.GetField(ParameterName)!);
    }

    /// <summary>Gets the expanded field definition.</summary>
    public AdvancedTuningFieldDefinition Definition
    {
        get;
    }

    /// <summary>Gets the resolved parameter name.</summary>
    public string ParameterName
    {
        get;
    }

    /// <summary>Gets the shared-session editor.</summary>
    public ParameterItemViewModel Editor
    {
        get;
    }

    /// <summary>Gets the axis label, when applicable.</summary>
    public string AxisText => string.IsNullOrWhiteSpace(Definition.Axis) ? string.Empty : $"Axis {Definition.Axis}";

    /// <summary>Gets the instance label, when applicable.</summary>
    public string InstanceText => Definition.Instance == 0 ? string.Empty : $"Instance {Definition.Instance}";

    /// <summary>Gets the component title.</summary>
    public string Title => Definition.Component.Title;

    /// <summary>Gets the component explanation.</summary>
    public string Description => Definition.Component.Description;

    /// <summary>Gets metadata units with a descriptor fallback.</summary>
    public string Units => string.IsNullOrWhiteSpace(Editor.Units)
        ? Definition.Component.FallbackUnits
        : Editor.Units;

    /// <summary>Gets the normalized pending magnitude for axis comparisons.</summary>
    [ObservableProperty]
    public partial double NormalizedMagnitude
    {
        get;
        set;
    }

    /// <summary>Refreshes the editor from shared state.</summary>
    /// <param name="session">The shared parameter session.</param>
    public void Refresh(IParameterEditSession session)
    {
        if (session.GetField(ParameterName) is { } state)
        {
            Editor.SetField(state);
        }
    }
}


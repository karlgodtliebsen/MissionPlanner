using System.Collections.ObjectModel;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Osd;
using ParameterItemViewModel = MissionPlanner.AvaloniaUI.App.Models.ParameterItemViewModel;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one discovered OSD screen.</summary>
public sealed class OsdScreenViewModel
{
    /// <summary>Initializes a screen projection.</summary>
    /// <param name="definition">The discovered screen.</param>
    /// <param name="session">The shared editing session.</param>
    /// <param name="move">The validated item-position callback.</param>
    public OsdScreenViewModel(
        OsdScreenDefinition definition,
        IParameterEditSession session,
        Func<OsdItemViewModel, int, int, string?> move)
    {
        Definition = definition;
        Parameters = new ObservableCollection<ParameterItemViewModel>(
            definition.ScreenParameterNames
                .Select(session.GetField)
                .Where(state => state is not null)
                .Select(state => new ParameterItemViewModel(session, state!)));
        Items = new ObservableCollection<OsdItemViewModel>(
            definition.Items.Select(item => new OsdItemViewModel(item, session, move)));
    }

    /// <summary>Gets the screen definition.</summary>
    public OsdScreenDefinition Definition
    {
        get;
    }

    /// <summary>Gets the one-based screen number.</summary>
    public int Number => Definition.Number;

    /// <summary>Gets the screen title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets a grid dimension label.</summary>
    public string GridSizeText => $"{Definition.GridWidth}×{Definition.GridHeight} characters";

    /// <summary>Gets whether metadata advertises dynamic overlapping items.</summary>
    public bool SupportsDynamicOverlaps => Definition.SupportsDynamicOverlaps;

    /// <summary>Gets screen enable/options/resolution parameters.</summary>
    public ObservableCollection<ParameterItemViewModel> Parameters
    {
        get;
    }

    /// <summary>Gets discovered screen items.</summary>
    public ObservableCollection<OsdItemViewModel> Items
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Title;
    }
}


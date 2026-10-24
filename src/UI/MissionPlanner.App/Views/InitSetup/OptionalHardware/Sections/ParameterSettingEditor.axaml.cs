using Avalonia.Controls;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Displays one optional hardware setting using its firmware metadata.</summary>
public partial class ParameterSettingEditor : UserControl
{
    /// <summary>Initializes the typed setting template without replacing its inherited data context.</summary>
    public ParameterSettingEditor()
    {
        InitializeComponent();
    }
}

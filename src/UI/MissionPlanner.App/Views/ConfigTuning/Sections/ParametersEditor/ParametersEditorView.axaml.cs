using Avalonia.Controls;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>Displays the ParametersEditorView configuration workflow.</summary>
public partial class ParametersEditorView : UserControl
{
    /// <summary>Initializes the ParametersEditorView.</summary>
    public ParametersEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (IsLoaded)
            {
                ParameterTextEditor.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Input);
    }
}

using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

/// <summary>
/// Represents the controls view.
/// </summary>
public partial class ControlsView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlsView"/> class.
    /// </summary>
    public ControlsView()
    {
        InitializeComponent();
        BindingContext = new ControlsViewModel();
    }
}

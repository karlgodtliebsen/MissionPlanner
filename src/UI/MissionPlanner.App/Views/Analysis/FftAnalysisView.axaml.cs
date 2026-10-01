using MissionPlanner.App.Utilities;

using Avalonia;

namespace MissionPlanner.App.Views.Analysis;

/// <summary>Shared offline FFT analysis content.</summary>
public partial class FftAnalysisView : UserControlViewBase<FftAnalysisViewModel>
{
    /// <summary>Whether this host only presents the log-opening entry point.</summary>
    public static readonly StyledProperty<bool> IsDrawerModeProperty =
        AvaloniaProperty.Register<FftAnalysisView, bool>(nameof(IsDrawerMode));

    /// <summary>Hides the full workspace while keeping Open log available in the drawer.</summary>
    public bool IsDrawerMode
    {
        get => GetValue(IsDrawerModeProperty);
        set => SetValue(IsDrawerModeProperty, value);
    }

    /// <summary>Initializes the compiled view.</summary>
    public FftAnalysisView()
    {
        InitializeComponent();
    }
}

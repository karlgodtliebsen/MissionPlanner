using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays the safety and arming assessment.</summary>
public partial class SafetySetupView : SetupSectionView
{
    private readonly SafetySetupViewModel viewModel;

    /// <summary>Initializes a new instance of the <see cref="SafetySetupView"/> class.</summary>
    public SafetySetupView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<SafetySetupViewModel>();
        BindingContext = viewModel;
        OwnedViewModel = viewModel;
    }
}

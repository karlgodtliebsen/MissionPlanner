using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>Provides the public API for the Mandatory Hardware setup shell.</summary>
public partial class MandatoryHardwareView : UraniumContentPage
{
    private readonly MandatoryHardwareViewModel viewModel;
    private readonly IReadOnlyList<SetupSectionView> sectionViews;

    /// <summary>Initializes the Mandatory Hardware setup shell and its section views.</summary>
    public MandatoryHardwareView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MandatoryHardwareViewModel>();
        BindingContext = viewModel;
        sectionViews =
        [
            FirmwareSectionView,
            FrameSectionView,
            AccelerometerSectionView,
            CompassSectionView,
            RadioSectionView,
            FlightModesSectionView,
            BatterySectionView,
            EscMotorSectionView,
            ServoOutputSectionView,
            OptionalHardwareSectionView,
            SafetySectionView,
            SummarySectionView
        ];
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Activate();
        foreach (var sectionView in sectionViews)
        {
            sectionView.Activate();
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        foreach (var sectionView in sectionViews)
        {
            sectionView.Deactivate();
        }

        viewModel.Deactivate();
        base.OnDisappearing();
    }
}

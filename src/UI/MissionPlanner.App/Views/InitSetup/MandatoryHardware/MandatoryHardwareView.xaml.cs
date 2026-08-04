using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>Provides the public API for the Mandatory Hardware setup shell.</summary>
public partial class MandatoryHardwareView : ContentPageView<MandatoryHardwareViewModel>
{
    private MandatoryHardwareViewModel viewModel;
    private readonly IReadOnlyList<SetupSectionView> sectionViews;

    /// <summary>Initializes the Mandatory Hardware setup shell and its section views.</summary>
    public MandatoryHardwareView()
    {
        InitializeComponent();
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

    /// <inheritdoc/>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        foreach (var sectionView in sectionViews)
        {
            sectionView.Deactivate();
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        viewModel = ServiceHelper.GetRequiredService<MandatoryHardwareViewModel>();
        BindingContext = viewModel;
        foreach (var sectionView in sectionViews)
        {
            sectionView.Activate();
        }
    }

    ///// <inheritdoc />
    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();
    //    viewModel.Activate();
    //    foreach (var sectionView in sectionViews)
    //    {
    //        sectionView.Activate();
    //    }
    //}

    ///// <inheritdoc />
    //protected override void OnDisappearing()
    //{
    //    foreach (var sectionView in sectionViews)
    //    {
    //        sectionView.Deactivate();
    //    }

    //    viewModel.Deactivate();
    //    base.OnDisappearing();
    //}
}

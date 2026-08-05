using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware;

/// <summary>Provides the public API for the Mandatory Hardware setup shell.</summary>
public partial class MandatoryHardwareView : ContentPageView<MandatoryHardwareViewModel>
{
    /// <summary>Initializes the Mandatory Hardware setup shell.</summary>
    public MandatoryHardwareView()
    {
        InitializeComponent();
    }

    ///// <inheritdoc />
    //protected override void OnNavigatedTo(NavigatedToEventArgs args)
    //{
    //    base.OnNavigatedTo(args);
    //    if (ViewModel is null)
    //    {
    //        ViewModel = ServiceHelper.GetRequiredService<MandatoryHardwareViewModel>();
    //        BindingContext = ViewModel;
    //    }
    //    ViewModel?.Activate();
    //}
}

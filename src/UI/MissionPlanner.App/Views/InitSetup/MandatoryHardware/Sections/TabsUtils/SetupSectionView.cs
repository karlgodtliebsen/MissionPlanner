namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.TabsUtils;

///// <summary>Creates and disposes a setup workflow ViewModel at selected-tab lifecycle boundaries.</summary>
//public abstract class SetupSectionView : ContentView, ITabViewLifecycleContent
//{
//    private Func<SetupWorkflowDetailViewModel>? createViewModel;
//    private SetupWorkflowDetailViewModel? viewModel;

//    /// <summary>Configures the transient ViewModel owned by this tab content.</summary>
//    protected void ConfigureViewModel<TViewModel>() where TViewModel : SetupWorkflowDetailViewModel =>
//        createViewModel = ServiceHelper.GetRequiredService<TViewModel>;

//    /// <inheritdoc />
//    public void Activate()
//    {
//        if (viewModel is not null) return;
//        viewModel = createViewModel?.Invoke() ?? throw new InvalidOperationException("A setup section must configure its ViewModel.");
//        BindingContext = viewModel;
//        viewModel.Activate();
//    }

//    /// <inheritdoc />
//    public void Deactivate()
//    {
//        BindingContext = null;
//        viewModel?.Dispose();
//        viewModel = null;
//    }
//}

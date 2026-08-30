using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.Avalonia.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to MissionPlanner Next Generation on Avalonia UI!";

    /// <inheritdoc />
    public override void Dispose()
    {
        //
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabHeaderModel : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial string Content { get; set; }
}

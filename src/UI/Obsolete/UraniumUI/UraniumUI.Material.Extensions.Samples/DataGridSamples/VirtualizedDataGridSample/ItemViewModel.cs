using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class ItemViewModel : ObservableObject
{
    [ObservableProperty] public partial double AValue { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = null!;
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial double Value { get; set; }
    [ObservableProperty] public partial double LiveValue { get; set; }
    [ObservableProperty] public partial double StepSize { get; set; }
    [ObservableProperty] public partial string? SelectedValue { get; set; }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class ControlsViewModel : ObservableObject
{
    [ObservableProperty] public partial double Altitude { get; set; } = 0.42;

    [ObservableProperty] public partial ObservableCollection<object> SelectedItems { get; set; }
    [ObservableProperty] public partial ObservableCollection<object> Items { get; set; }
    [ObservableProperty] public partial double Value { get; set; }
    [ObservableProperty] public partial double Minimum { get; set; } = 0.0;
    [ObservableProperty] public partial double Maximum { get; set; } = 42.0;
    [ObservableProperty] public partial double StepSize { get; set; } = 0.1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlsViewModel"/> class.
    /// </summary>
    public ControlsViewModel()
    {
        SelectedItems = [];
        Items = ["Logging", "GPS", "A", "B", "Longlonglong", "extralonglonglongevenlonger"];
    }
}

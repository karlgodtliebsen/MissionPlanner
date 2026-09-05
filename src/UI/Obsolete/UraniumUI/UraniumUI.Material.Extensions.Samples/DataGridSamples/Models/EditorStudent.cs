using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public partial class EditorStudent : ObservableObject
{
    [ObservableProperty] public partial int Id { get; set; }
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial int Age { get; set; }
}

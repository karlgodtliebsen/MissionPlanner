using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Dialogs;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class CustomDataGridPageViewModel : ObservableObject
{
    private StudentDataStore DataStore { get; } = new();

    public ObservableRangeCollection<CustomDataGridStudent> Items { get; } = [];

    [ObservableProperty] public partial bool IsBusy { get; set; }

    public CustomDataGridPageViewModel()
    {
        Initialize();
    }

    protected virtual async void Initialize()
    {
        IsBusy = true;
        Items.AddRange(await DataStore.GetListAsync(1000));
        IsBusy = false;
    }
}

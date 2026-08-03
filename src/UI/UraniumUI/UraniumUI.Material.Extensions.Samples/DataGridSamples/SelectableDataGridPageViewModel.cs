using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SelectableDataGridPageViewModel : ObservableObject
{
    public ObservableCollection<CustomDataGridStudent> Items { get; private set; } = [];
    public ObservableCollection<CustomDataGridStudent> SelectedItems { get; set; } = [];

    public ICommand RemoveSelectedCommand { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel()
    {
        Initialize().FireAndForget();

        RemoveSelectedCommand = new Command(() =>
        {
            for (var i = 0; i < SelectedItems.Count; i++)
            {
                Items.Remove(SelectedItems[i]);
            }
        });
    }

    private async Task Initialize()
    {
        Items = new ObservableCollection<CustomDataGridStudent>(await DataStore.GetListAsync(1000, false));
    }
}

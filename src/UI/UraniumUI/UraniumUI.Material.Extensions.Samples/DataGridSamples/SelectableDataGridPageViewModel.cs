using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SelectableDataGridPageViewModel : ObservableObject
{
    public ObservableCollection<CustomDataGridStudent> Items { get; private set; } = [];
    public ObservableCollection<CustomDataGridStudent> SelectedItems { get; set; } = [];

    public ICommand RemoveSelectedCommand { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel()
    {
        Initialize();

        RemoveSelectedCommand = new Command(() =>
        {
            for (var i = 0; i < SelectedItems.Count; i++)
            {
                Items.Remove(SelectedItems[i]);
            }
        });
    }

    private async void Initialize()
    {
        Items = new ObservableCollection<CustomDataGridStudent>(await DataStore.GetListAsync(1000, false));
        SelectedItems.Add(Items[0]);
        SelectedItems.Add(Items[2]);
    }
}

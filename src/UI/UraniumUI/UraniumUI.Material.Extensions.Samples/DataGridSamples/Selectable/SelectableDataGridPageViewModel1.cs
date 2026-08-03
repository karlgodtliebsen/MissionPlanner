using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel1 : ObservableObject
{
    public ObservableRangeCollection<CustomDataGridStudent> Items { get; } = [];
    public ObservableCollection<CustomDataGridStudent> SelectedItems { get; } = [];

    public ICommand RemoveSelectedCommand { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel1()
    {
        Initialize().FireAndForget();

        RemoveSelectedCommand = new Command(() =>
        {
            var allItems = Items.ToList();
            foreach (var customDataGridStudent in SelectedItems)
            {
                allItems.Remove(customDataGridStudent);
            }

            Items.Clear();
            Items.AddRange(allItems);
            SelectedItems.Clear();
        });
    }

    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection);
    }
}

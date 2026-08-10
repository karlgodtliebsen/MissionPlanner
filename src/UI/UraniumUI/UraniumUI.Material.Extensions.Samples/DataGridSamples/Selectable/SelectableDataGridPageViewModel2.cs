using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel2 : ObservableObject
{
    public ObservableRangeCollection<CustomDataGridStudent> Items { get; } = [];


    [ObservableProperty] public partial CustomDataGridStudent? SelectedItem { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel2()
    {
        Initialize().FireAndForget();
    }

    private bool CanRemoveSelectedSingle()
    {
        return SelectedItem is not null;
    }


    [RelayCommand(CanExecute = nameof(CanRemoveSelectedSingle))]
    private void RemoveSelectedSingle()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var allItems = Items.ToList();
        allItems.Remove(SelectedItem);

        Items.Clear();
        Items.AddRange(allItems);
        SelectedItem = null;
    }


    [RelayCommand]
    private void SelectionChanged()
    {
        RemoveSelectedSingleCommand.NotifyCanExecuteChanged();
    }

    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection);
    }
}

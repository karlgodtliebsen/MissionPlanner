using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel4 : ObservableObject
{
    public ObservableRangeCollection<CustomDataGridStudent> Items { get; private set; } = [];

    public ObservableRangeCollection<CustomDataGridStudent> SelectedItems { get; private set; } = [];

    [ObservableProperty] public partial CustomDataGridStudent? SelectedItem { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel4()
    {
        Initialize().FireAndForget();
    }


    [RelayCommand]
    private void SelectionChanged()
    {
        RemoveMultiSelectedCommand.NotifyCanExecuteChanged();
        RemoveSingleSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveMultiSelected()
    {
        return SelectedItems.Any();
    }

    private bool CanRemoveSingleSelected()
    {
        return SelectedItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveMultiSelected))]
    private void RemoveMultiSelected()
    {
        var allItems = Items.ToList();
        var items = SelectedItems.ToList();
        foreach (var customDataGridStudent in items)
        {
            allItems.Remove(customDataGridStudent);
        }

        Items.Clear();
        Items.AddRange(allItems);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSingleSelected))]
    private void RemoveSingleSelected()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var allItems = Items.ToList();
        allItems.Remove(SelectedItem);
        Items.Clear();
        Items.AddRange(allItems);
    }

    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection);
    }
}

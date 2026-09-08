using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel6 : ObservableObject
{
    public ObservableRangeCollection<CustomDataGridStudent> Items { get; private set; } = [];

    public ObservableRangeCollection<CustomDataGridStudent> SelectedItems { get; private set; } = [];

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel6()
    {
        Initialize().FireAndForget();
    }

    private bool CanRemoveSelected()
    {
        return SelectedItems.Any();
    }

    [RelayCommand]
    private void SelectionChanged()
    {
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        RemoveMultiSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
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

    private bool CanRemoveMultiSelected()
    {
        return SelectedItems.Any();
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

    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection);
    }
}

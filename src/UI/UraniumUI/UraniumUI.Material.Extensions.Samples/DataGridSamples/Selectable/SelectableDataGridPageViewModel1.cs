using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel1 : ObservableObject
{
    public ObservableRangeCollection<CustomDataGridStudent> Items { get; } = [];

    public ObservableCollection<CustomDataGridStudent> SelectedItems { get; } = [];

    [ObservableProperty] public partial CustomDataGridStudent? SelectedItem { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel1()
    {
        Initialize().FireAndForget();
    }

    private bool CanRemoveSelectedSingle()
    {
        return SelectedItem is not null;
    }

    private bool CanRemoveSelected()
    {
        return SelectedItems.Any();
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
        SelectedItems.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        var allItems = Items.ToList();
        foreach (var customDataGridStudent in SelectedItems)
        {
            allItems.Remove(customDataGridStudent);
        }

        Items.Clear();
        Items.AddRange(allItems);
        SelectedItems.Clear();
    }


    [RelayCommand]
    private void SelectionChanged()
    {
        RemoveSelectedSingleCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }


    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection);
    }
}

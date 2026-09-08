using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel3 : ObservableObject
{
    public ObservableRangeCollection<SelectableCustomDataGridStudent> Items { get; private set; } = [];

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel3()
    {
        Initialize().FireAndForget();
    }

    private bool CanRemoveSelected()
    {
        return Items.Any(item => item.IsSelected);
    }

    [RelayCommand]
    private void SelectionChanged()
    {
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        var allItems = Items.ToList();
        var items = Items.Where(item => item.IsSelected).ToList();
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
        Items.AddRange(collection.Select(item => new SelectableCustomDataGridStudent(item)));
    }
}

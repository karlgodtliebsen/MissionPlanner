using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPageViewModel2 : ObservableObject
{
    public ObservableRangeCollection<SelectableCustomDataGridStudent> Items { get; private set; } = [];

    public ICommand RemoveSelectedCommand { get; set; }

    private StudentDataStore DataStore { get; } = new StudentDataStore();

    public SelectableDataGridPageViewModel2()
    {
        Initialize().FireAndForget();

        RemoveSelectedCommand = new Command(() =>
        {
            var allItems = Items.ToList();
            var items = Items.Where(item => item.IsSelected).ToList();
            foreach (var customDataGridStudent in items)
            {
                allItems.Remove(customDataGridStudent);
            }

            Items.Clear();
            Items.AddRange(allItems);
        });
    }

    private async Task Initialize()
    {
        var collection = await DataStore.GetListAsync(1000, false);
        Items.AddRange(collection.Select(item => new SelectableCustomDataGridStudent(item)));
    }
}

public partial class SelectableCustomDataGridStudent : ObservableObject
{
    //may use this property to bind to the selection column in the DataGrid
    //else use the  public ObservableCollection<CustomDataGridStudent> SelectedItems { get; set; } = []; on the viewmodel
    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <inheritdoc />
    public SelectableCustomDataGridStudent(CustomDataGridStudent customDataGridStudent)
    {
        Student = customDataGridStudent;
    }

    public CustomDataGridStudent Student { get; set; }
}

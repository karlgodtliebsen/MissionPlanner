using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

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

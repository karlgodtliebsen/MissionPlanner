using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

public partial class CustomDataGridStudent : ObservableObject
{
    //may use this property to bind to the selection column in the DataGrid
    //else use the  public ObservableCollection<CustomDataGridStudent> SelectedItems { get; set; } = []; on the viewmodel
    [ObservableProperty] public partial bool IsSelected { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Age { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public DateTime RegistrationDate { get; set; }
}

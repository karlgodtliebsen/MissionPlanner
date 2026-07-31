using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Dialogs;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class CustomDataGridPageViewModel : ObservableObject
{
    private StudentDataStore DataStore { get; } = new();

    public ObservableRangeCollection<CustomDataGridStudent> Items { get; } = [];

    [ObservableProperty] public partial bool IsBusy { get; set; }

    public ICommand AddNewCommand { get; set; }
    public ICommand RemoveItemCommand { get; set; }
    public int Row { get; set; }

    public CustomDataGridPageViewModel(IDialogService dialogService)
    {
        Initialize();

        AddNewCommand = new Command(async () =>
        {
            var newStudent = StudentDataStore.faker.Generate();

            var result = await dialogService.DisplayFormViewAsync("New Student", newStudent, "OK", "Cancel");
            if (result != null)
            {
                Items.Add(result);
            }
        });

        RemoveItemCommand = new Command((item) =>
        {
            if (item is CustomDataGridStudent student)
            {
                Items.Remove(student);
            }
        });
    }

    protected virtual async void Initialize()
    {
        IsBusy = true;
        Items.AddRange(await DataStore.GetListAsync(1000));
        IsBusy = false;
    }
}

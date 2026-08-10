using System.Windows.Input;
using Bogus;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Dialogs;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class EditorDataGridPageViewModel : ObservableObject
{
    public ObservableRangeCollection<EditorStudent> Items { get; } = [];


    [ObservableProperty] public partial bool IsBusy { get; set; }

    public ICommand AddNewCommand { get; set; }
    public ICommand RemoveItemCommand { get; set; }

    public EditorDataGridPageViewModel(IDialogService dialogService)
    {
        IsBusy = true;
        IList<EditorStudent> newItems = [];
        for (var i = 0; i < 1000; i++)
        {
            newItems.Add(GenerateStudent());
        }

        Items.AddRange(newItems);
        IsBusy = false;

        AddNewCommand = new Command(async () =>
        {
            var newStudent = GenerateStudent();

            var result = await dialogService.DisplayFormViewAsync("New Student", newStudent, "OK", "Cancel");
            if (result != null)
            {
                var allItems = Items.ToList();
                allItems.Add(result);
                Items.Clear();
                Items.AddRange(allItems);
            }
        });

        RemoveItemCommand = new Command((item) =>
        {
            if (item is EditorStudent student)
            {
                var allItems = Items.ToList();
                allItems.Remove(student);
                Items.Clear();
                Items.AddRange(allItems);
            }
        });
    }

    private static readonly Faker<EditorStudent> studentFaker = new Faker<EditorStudent>()
        .RuleFor(x => x.Id, f => f.IndexFaker)
        .RuleFor(x => x.Name, f => f.Person.FullName)
        .RuleFor(x => x.Age, f => f.Random.Number(14, 85));

    private static EditorStudent GenerateStudent()
    {
        return studentFaker.Generate();
    }
}

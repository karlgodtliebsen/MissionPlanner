using Bogus;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class EditorDataGridPageViewModel : ObservableObject
{
    public ObservableRangeCollection<EditorStudent> Items { get; } = [];

    public EditorDataGridPageViewModel()
    {
        for (var i = 0; i < 1000; i++)
        {
            Items.Add(GenerateStudent());
        }
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

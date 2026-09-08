using Bogus;
using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SimpleDataGridPageViewModel : ObservableObject
{
    public List<SimpleStudent> Items { get; } = [];

    public List<SimpleStudent> FewItems { get; } = [];
    public List<SimpleStudent> OneItems { get; } = [];

    public SimpleDataGridPageViewModel()
    {
        for (var i = 0; i < 1000; i++)
        {
            Items.Add(GenerateStudent());
        }

        for (var i = 0; i < 5; i++)
        {
            FewItems.Add(GenerateStudent());
        }

        for (var i = 0; i < 1; i++)
        {
            OneItems.Add(GenerateStudent());
        }
    }

    private static readonly Faker<SimpleStudent> studentFaker = new Faker<SimpleStudent>()
        .RuleFor(x => x.Id, f => f.IndexFaker)
        .RuleFor(x => x.Name, f => f.Person.FullName)
        .RuleFor(x => x.Age, f => f.Random.Number(14, 85));

    public static SimpleStudent GenerateStudent()
    {
        return studentFaker.Generate();
    }
}

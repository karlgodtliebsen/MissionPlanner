using Bogus;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public class StudentDataStore
{
    internal static Faker<CustomDataGridStudent> faker = new Faker<CustomDataGridStudent>()
        .RuleFor(x => x.Id, f => f.IndexFaker)
        .RuleFor(x => x.Name, f => f.Person.FullName)
        .RuleFor(x => x.Age, f => f.Random.Number(14, 85))
        .RuleFor(x => x.SecurityStamp, f => f.Random.Guid())
        .RuleFor(x => x.RegistrationDate, f => f.Date.Past(1));

    public async Task<List<CustomDataGridStudent>> GetListAsync(int number, bool simulateNetwork = false)
    {
        if (simulateNetwork)
        {
            await Task.Delay(Random.Shared.Next(500, 2000));
        }

        var list = new List<CustomDataGridStudent>();

        for (var i = 0; i < number; i++)
        {
            list.Add(faker.Generate());
        }

        return list;
    }

    public List<CustomDataGridStudent> GetList(int number)
    {
        var list = new List<CustomDataGridStudent>();

        for (var i = 0; i < number; i++)
        {
            list.Add(faker.Generate());
        }

        return list;
    }
}

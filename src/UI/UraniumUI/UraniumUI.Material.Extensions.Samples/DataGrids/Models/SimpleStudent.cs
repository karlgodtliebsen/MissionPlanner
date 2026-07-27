using System.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public class SimpleStudent
{
    [DisplayName("Identity")] public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Age { get; set; }
}

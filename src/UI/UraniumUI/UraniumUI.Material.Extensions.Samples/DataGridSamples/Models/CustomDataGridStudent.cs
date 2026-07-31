namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public class CustomDataGridStudent
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Age { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public DateTime RegistrationDate { get; set; }
}

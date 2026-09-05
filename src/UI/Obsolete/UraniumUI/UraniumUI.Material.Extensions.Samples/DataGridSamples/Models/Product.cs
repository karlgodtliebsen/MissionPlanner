namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public class Product
{
    public int id { get; set; }
    public string title { get; set; } = null!;
    public string description { get; set; } = null!;
    public float price { get; set; }
    public float discountPercentage { get; set; }
    public float rating { get; set; }
    public int stock { get; set; }
    public string brand { get; set; } = null!;
    public string category { get; set; } = null!;
    public string thumbnail { get; set; } = null!;
    public string[] images { get; set; } = null!;
}

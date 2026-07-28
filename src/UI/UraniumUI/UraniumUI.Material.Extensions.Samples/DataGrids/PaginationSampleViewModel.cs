using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class PaginationSampleViewModel : ObservableObject
{
    public ObservableRangeCollection<Product> Products { get; } = [];

    [ObservableProperty] public partial bool IsBusy { get; set; }

    public PaginationSampleViewModel()
    {
        LoadPagesAsync().FireAndForget();
    }

    private async Task LoadPagesAsync()
    {
        IsBusy = true;
        var response = await GetProductsAsync();
        Products.Clear();
        if (response is not null)
        {
            Products.AddRange(response.products);
        }

        IsBusy = false;
    }

    private async Task<ApiResponse?> GetProductsAsync()
    {
        using var client = new HttpClient();
        var response = await client.GetFromJsonAsync<ApiResponse>($"https://dummyjson.com/products");
        return response;
    }
}

public class ApiResponse
{
    public required Product[] products { get; set; }
    public int total { get; set; }
    public int skip { get; set; }
    public int limit { get; set; }
}

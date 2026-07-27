using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using UraniumUI.Extensions;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class PaginationSampleViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; } = [];

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial int CurrentPage { get; set; }
    [ObservableProperty] public partial int TotalPages { get; set; }

    public const int limit = 10;

    public ICommand GoNextCommand { get; }

    public ICommand GoPreviousCommand { get; }

    public ICommand SetPageCommand { get; }

    public PaginationSampleViewModel()
    {
        LoadPagesAsync().FireAndForget();
    }

    private async Task LoadPagesAsync()
    {
        IsBusy = true;

        var response = await GetProductsAsync();

        IsBusy = false;

        TotalPages = (int)Math.Ceiling((double)response.total / limit);

        Products.Clear();

        foreach (var product in response.products)
        {
            Products.Add(product);
        }
    }

    private async Task<ApiResponse> GetProductsAsync()
    {
        using var client = new HttpClient();
        var response = await client.GetFromJsonAsync<ApiResponse>(
            $"https://dummyjson.com/products");

        return response;
    }
}

public class ApiResponse
{
    public Product[] products { get; set; }
    public int total { get; set; }
    public int skip { get; set; }
    public int limit { get; set; }
}

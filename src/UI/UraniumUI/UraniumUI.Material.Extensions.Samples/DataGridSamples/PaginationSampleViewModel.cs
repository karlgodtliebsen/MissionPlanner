using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class PaginationSampleViewModel : ObservableObject
{
    private readonly IDispatcher dispatcher;
    public ObservableRangeCollection<Product> Products { get; } = [];

    [ObservableProperty] public partial bool IsBusy { get; set; } = true;

    public PaginationSampleViewModel(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        _ = Task.Run(LoadPagesAsync);
    }

    //For the version with pagination coupled to http request, see the UraniumUI Pagination sample
    private async Task LoadPagesAsync()
    {
        var products = await GetAllProductsAsync();
        if (products is not null)
        {
            await dispatcher.DispatchAsync(() => Products.AddRange(products.OrderBy(p => p.title)));
        }

        await dispatcher.DispatchAsync(() => IsBusy = false);
    }

    private async Task<List<Product>?> GetAllProductsAsync()
    {
        var products = new List<Product>();

        using var client = new HttpClient();
        var skip = 0;

        while (true)
        {
            var response = await client.GetFromJsonAsync<ApiResponse>($"https://dummyjson.com/products?skip={skip}");
            if (response is null)
            {
                return products;
            }

            products.AddRange(response.products);
            if (response.products.Length == 0 || products.Count >= response.total)
            {
                return products;
            }

            skip += response.products.Length;
        }
    }
}

public class ApiResponse
{
    public required Product[] products { get; set; }
    public int total { get; set; }
    public int skip { get; set; }
    public int limit { get; set; }
}

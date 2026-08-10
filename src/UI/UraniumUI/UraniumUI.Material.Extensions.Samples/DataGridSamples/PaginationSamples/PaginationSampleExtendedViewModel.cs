using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Material.Dialogs;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSampleExtendedViewModel : ObservableObject
{
    private IDisposable? disposable;
    private readonly IExtendedDialogService dialogService;
    private readonly IDispatcher dispatcher;
    public ObservableRangeCollection<Product> Products { get; } = [];

    [ObservableProperty] public partial string Message { get; set; }

    /// <inheritdoc />
    public PaginationSampleExtendedViewModel(IExtendedDialogService dialogService, IDispatcher dispatcher)
    {
        this.dialogService = dialogService;
        this.dispatcher = dispatcher;


        _ = Task.Run(LoadPagesAsync);
    }

    private async Task ShowProgressAsync()
    {
        Message = "Loading products...";
        disposable = await dialogService.DisplayProgressCancellableAsync("Progressing", () => Message, "Cancel");
    }

    private void StopProgress()
    {
        disposable?.Dispose();
    }

    //For the version with pagination coupled to http request, see the UraniumUI Pagination sample
    private async Task LoadPagesAsync()
    {
        await dispatcher.DispatchAsync(async () => await ShowProgressAsync());
        try
        {
            var products = await GetAllProductsAsync();
            if (products is not null)
            {
                await dispatcher.DispatchAsync(() => Products.AddRange(products.OrderBy(p => p.title)));
            }
        }
        finally
        {
            await dispatcher.DispatchAsync(() => StopProgress());
        }
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
            await dispatcher.DispatchAsync(() => Message = $"Loaded {products.Count} products...");

            if (response.products.Length == 0 || products.Count >= response.total)
            {
                return products;
            }

            skip += response.products.Length;
        }
    }

    public class ApiResponse
    {
        public required Product[] products { get; set; }
        public int total { get; set; }
        public int skip { get; set; }
        public int limit { get; set; }
    }
}

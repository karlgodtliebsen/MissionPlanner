using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;
using UraniumUI.Material.VirtualizedDataGrid.Controls;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSampleViewModel : ObservableObject
{
    private static readonly HttpClient HttpClient = new();

    private readonly IDispatcher dispatcher;
    private int remoteRequestVersion;

    public PaginationSampleViewModel(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        //_ = LoadAllProductsSafelyAsync();
    }

    /// <summary>
    /// Gets the complete data set used by the non-paged virtualization example.
    /// </summary>
    public ObservableRangeCollection<Product> Products { get; } = [];


    [ObservableProperty] public partial bool IsBusy { get; set; } = true;

    [ObservableProperty] public partial bool IsRemoteBusy { get; set; }

    [ObservableProperty] public partial int RemoteTotalItemCount { get; set; }

    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Loads the page requested by the VirtualizedDataGrid. Concurrent requests
    /// are permitted, but only the newest response is applied to the collection.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadRemotePageAsync(VirtualizedDataGridPageRequestEventArgs request)
    {
        var requestVersion = Interlocked.Increment(ref remoteRequestVersion);

        await dispatcher.DispatchAsync(() =>
        {
            IsRemoteBusy = true;
            ErrorMessage = null;
        });

        try
        {
            var response = await GetProductsAsync(request.Limit, request.Skip);
            if (requestVersion != Volatile.Read(ref remoteRequestVersion))
            {
                return;
            }

            await dispatcher.DispatchAsync(() =>
            {
                RemoteTotalItemCount = response.total;
                Products.Clear();
                Products.AddRange(response.products);
            });
        }
        catch (Exception exception)
        {
            if (requestVersion == Volatile.Read(ref remoteRequestVersion))
            {
                await dispatcher.DispatchAsync(() =>
                    ErrorMessage = $"Unable to load the requested page: {exception.Message}");
            }
        }
        finally
        {
            if (requestVersion == Volatile.Read(ref remoteRequestVersion))
            {
                await dispatcher.DispatchAsync(() => IsRemoteBusy = false);
            }
        }
    }

    private static async Task<ProductApiResponse> GetProductsAsync(int limit, int skip)
    {
        return await HttpClient.GetFromJsonAsync<ProductApiResponse>(
                   $"https://dummyjson.com/products?limit={limit}&skip={skip}")
               ?? throw new InvalidOperationException("The product service returned no data.");
    }

    public sealed class ProductApiResponse
    {
        public required Product[] products { get; set; }
        public int total { get; set; }
        public int skip { get; set; }
        public int limit { get; set; }
    }
}

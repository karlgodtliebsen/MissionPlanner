using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

public partial class MavFtpTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleFileSystemService fileSystem;
    private readonly IFileSaver fileSaver;
    private readonly ILogger<MavFtpTabViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private readonly IList<IDisposable> disposables = [];

    private const string NoConnection = "No connected vehicle.";
    private const string NoFiles = "No files available.";


    [ObservableProperty] private string currentPath = "/";
    [ObservableProperty] private VehicleFileSystemEntryViewModel? selectedEntry;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double transferProgress;
    [ObservableProperty] private string transferDetails = string.Empty;
    [ObservableProperty] private string? statusText;
    [ObservableProperty] private string? emptyText;
    [ObservableProperty] private string? errorText;

    /// <summary>
    /// Gets the collection of file system entries.
    /// </summary>
    public ObservableCollection<VehicleFileSystemEntryViewModel> Entries { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the user can navigate up in the file system.
    /// </summary>
    public bool CanNavigateUp => CurrentPath != "/" && !IsBusy;

    /// <summary>
    /// Gets a value indicating whether there is a connected vehicle.
    /// </summary>
    public bool HasVehicle => vehicleRegistry.Vehicles.Count > 0;


    /// <summary>
    /// Initializes a new instance of the <see cref="MavFtpTabViewModel"/> class.
    /// </summary>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="connectionSession">The vehicle connection session.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="fileSaver">The file saver.</param>
    /// <param name="logger"></param>
    public MavFtpTabViewModel(IVehicleRegistry vehicleRegistry, IVehicleConnectionSession connectionSession, IDomainEventHub domainEventHub, IFileSaver fileSaver, ILogger<MavFtpTabViewModel> logger)
    {
        fileSystem = connectionSession.CreateMavFtpConnection();
        this.vehicleRegistry = vehicleRegistry;
        this.fileSaver = fileSaver;
        this.logger = logger;
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        SetConnectionStatus();
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        SetConnectionStatus();
    }


    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        SetConnectionStatus();
    }

    private void SetConnectionStatus()
    {
        ErrorText = null;
        StatusText = null;
        EmptyText = NoFiles;
        var vehicle = vehicleRegistry.Vehicles.FirstOrDefault();
        if (vehicle is null)
        {
            StatusText = NoConnection;
            ErrorText = StatusText;
            EmptyText = NoConnection;
            return;
        }

        EmptyText = NoFiles;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDirectoryAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task OpenSelectedAsync()
    {
        if (SelectedEntry?.IsDirectory == true)
        {
            await LoadDirectoryAsync(RemotePath.Join(CurrentPath, SelectedEntry.Name));
        }
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (CanNavigateUp)
        {
            await LoadDirectoryAsync(RemotePath.Parent(CurrentPath));
        }
    }

    [RelayCommand]
    private async Task ResetSessionsAsync()
    {
        ErrorText = null;
        var vehicle = vehicleRegistry.Vehicles.FirstOrDefault();
        if (vehicle is null)
        {
            SetConnectionStatus();
            return;
        }

        await RunAsync(async ct =>
        {
            await fileSystem.ResetSessionsAsync(vehicle.Id, ct);
            StatusText = "MAVFTP sessions reset.";
        });
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var vehicle = vehicleRegistry.Vehicles.FirstOrDefault();
        if (vehicle is null || SelectedEntry is null || SelectedEntry.IsDirectory)
        {
            SetConnectionStatus();
            return;
        }

        var remotePath = RemotePath.Join(CurrentPath, SelectedEntry.Name);
        await RunAsync(async ct =>
        {
            var temporary = Path.Combine(FileSystem.CacheDirectory, $"mavftp-{Guid.NewGuid():N}.tmp");
            try
            {
                await using var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var progress = new Progress<VehicleFileTransferProgress>(p =>
                {
                    TransferProgress = p.TotalBytes > 0 ? (double)p.BytesTransferred / p.TotalBytes.Value : 0;
                    TransferDetails = $"{p.BytesTransferred:N0} / {p.TotalBytes?.ToString("N0") ?? "?"} bytes · {p.BytesPerSecond ?? 0:N0} B/s";
                });
                await fileSystem.DownloadFileAsync(vehicle.Id, remotePath, destination, progress, ct);
                destination.Position = 0;
                var saved = await fileSaver.SaveAsync(SelectedEntry.Name, destination, ct);
                StatusText = saved.IsSuccessful ? $"Downloaded to {saved.FilePath}." : "Download destination selection cancelled.";
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        operationCancellation?.Cancel();
    }

    private async Task LoadDirectoryAsync(string path)
    {
        var vehicle = vehicleRegistry.Vehicles.FirstOrDefault();
        if (vehicle is null)
        {
            Entries.Clear();
            SetConnectionStatus();
            return;
        }

        await RunAsync(async ct =>
        {
            var entries = await fileSystem.ListDirectoryAsync(vehicle.Id, path, ct);
            Entries.Clear();
            foreach (var entry in entries.OrderBy(x => x.Type).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                Entries.Add(new VehicleFileSystemEntryViewModel(entry.Name, entry.Type, entry.Size));
            }

            CurrentPath = RemotePath.Normalize(path);
            StatusText = Entries.Count == 0 ? "Directory is empty." : $"{Entries.Count} entries.";
            SelectedEntry = null;
        });
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ErrorText = null;
        OnPropertyChanged(nameof(CanNavigateUp));
        try
        {
            await operation(operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation cancelled.";
        }
        catch (TimeoutException ex)
        {
            ErrorText = "MAVFTP transfer timed out.";
            logger.LogWarning(ex, "MAVFTP UI operation timed out.");
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = "Vehicle is not connected.";
            logger.LogWarning(ex, "MAVFTP operation has no vehicle.");
        }
        catch (Exception ex)
        {
            ErrorText = "MAVFTP operation failed. The vehicle may not support MAVFTP.";
            logger.LogError(ex, "MAVFTP UI operation failed.");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanNavigateUp));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
    }
}

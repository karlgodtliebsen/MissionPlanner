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
using MissionPlanner.MavLink.MavFtp;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the MAVFTP tab in the configuration tuning section of the application.
/// </summary>
public partial class MavFtpTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IFileSaver fileSaver;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MavFtpTabViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private VehicleId? activeVehicleId;
    private readonly IList<IDisposable> disposables = [];

    private IVehicleFileSystemService? fileSystem;
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
    /// <param name="dispatcher"></param>
    /// <param name="logger"></param>
    public MavFtpTabViewModel(IVehicleRegistry vehicleRegistry, IVehicleConnectionSession connectionSession, IDomainEventHub domainEventHub, IFileSaver fileSaver,
        IDispatcher dispatcher, ILogger<MavFtpTabViewModel> logger)
    {
        fileSystem = connectionSession.CreateMavFtpConnection();
        this.vehicleRegistry = vehicleRegistry;
        this.connectionSession = connectionSession;
        this.fileSaver = fileSaver;
        this.dispatcher = dispatcher;
        this.logger = logger;
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        SetConnectionStatus();
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        if (activeVehicleId == evt.VehicleId)
        {
            activeVehicleId = vehicleRegistry.Vehicles.FirstOrDefault()?.Id;
        }

        if (fileSystem is not null)
        {
            await fileSystem.DisposeAsync();
        }

        fileSystem = null;
        SetConnectionStatus();
    }


    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        activeVehicleId = evt.VehicleId;
        SetConnectionStatus();
        if (fileSystem is not null)
        {
            await fileSystem.DisposeAsync();
        }

        fileSystem = connectionSession.CreateMavFtpConnection();
    }

    private void SetConnectionStatus()
    {
        dispatcher.Dispatch(() =>
        {
            ErrorText = null;
            StatusText = null;
            EmptyText = NoFiles;
            var vehicle = ResolveActiveVehicle();
            if (vehicle is null)
            {
                StatusText = NoConnection;
                ErrorText = StatusText;
                EmptyText = NoConnection;
                return;
            }

            EmptyText = NoFiles;
        });
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
        dispatcher.Dispatch(() => ErrorText = null);
        var vehicle = ResolveActiveVehicle();
        if (vehicle is null)
        {
            SetConnectionStatus();
            return;
        }

        await RunAsync(async ct =>
        {
            if (fileSystem is null)
            {
                dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                return;
            }

            await fileSystem.ResetSessionsAsync(vehicle.Id, ct);
            dispatcher.Dispatch(() => StatusText = "MAVFTP sessions reset.");
        });
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var vehicle = ResolveActiveVehicle();
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
                if (fileSystem is null)
                {
                    dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                    return;
                }

                await fileSystem.DownloadFileAsync(vehicle.Id, remotePath, destination, progress, ct);
                destination.Position = 0;
                var saved = await fileSaver.SaveAsync(SelectedEntry.Name, destination, ct);
                dispatcher.Dispatch(() => StatusText = saved.IsSuccessful ? $"Downloaded to {saved.FilePath}." : "Download destination selection cancelled.");
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
        var vehicle = ResolveActiveVehicle();
        if (vehicle is null)
        {
            dispatcher.Dispatch(() => Entries.Clear());
            SetConnectionStatus();
            return;
        }

        await RunAsync(async ct =>
        {
            if (fileSystem is null)
            {
                dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                return;
            }

            var entries = await fileSystem.ListDirectoryAsync(vehicle.Id, path, ct);
            dispatcher.Dispatch(() =>
            {
                Entries.Clear();
                foreach (var entry in entries.OrderBy(x => x.Type).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Entries.Add(new VehicleFileSystemEntryViewModel(entry.Name, entry.Type, entry.Size));
                }

                CurrentPath = RemotePath.Normalize(path);
                StatusText = Entries.Count == 0 ? "Directory is empty." : $"{Entries.Count} entries.";
                SelectedEntry = null;
            });
        });
    }

    private VehicleSession? ResolveActiveVehicle()
    {
        if (activeVehicleId is { } id)
        {
            var selected = vehicleRegistry.Vehicles.FirstOrDefault(x => x.Id == id);
            if (selected is not null)
            {
                return selected;
            }
        }

        var fallback = vehicleRegistry.Vehicles.FirstOrDefault();
        activeVehicleId = fallback?.Id;
        return fallback;
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();

        dispatcher.Dispatch(() =>
        {
            IsBusy = true;
            ErrorText = null;
        });

        OnPropertyChanged(nameof(CanNavigateUp));
        try
        {
            await operation(operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            dispatcher.Dispatch(() => StatusText = "Operation cancelled.");
        }
        catch (TimeoutException ex)
        {
            dispatcher.Dispatch(() => ErrorText = "MAVFTP transfer timed out.");
            logger.LogWarning(ex, "MAVFTP transfer timed out.");
        }
        catch (MavFtpRemoteException ex) when (
            ex.Error == MavFtpNakError.UnknownCommand)
        {
            dispatcher.Dispatch(() => ErrorText = "The connected vehicle does not support this MAVFTP operation.");
        }
        catch (MavFtpRemoteException ex) when (
            ex.Error == MavFtpNakError.FileNotFound)
        {
            dispatcher.Dispatch(() => ErrorText = "The remote file or directory was not found.");
        }
        catch (MavFtpProtocolException ex)
        {
            dispatcher.Dispatch(() => ErrorText = "The vehicle returned an invalid MAVFTP response.");
            logger.LogError(ex, "Invalid MAVFTP protocol response.");
        }
        catch (InvalidOperationException ex)
        {
            dispatcher.Dispatch(() => ErrorText = "Vehicle is not connected.");
            logger.LogWarning(ex, "MAVFTP operation has no vehicle.");
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(() => ErrorText = "MAVFTP operation failed. The vehicle may not support MAVFTP.");
            logger.LogError(ex, "MAVFTP UI operation failed.");
        }

        finally
        {
            dispatcher.Dispatch(() =>
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanNavigateUp));
            });
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
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }
}

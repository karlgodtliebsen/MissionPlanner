using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.MavFtp;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the MAVFTP tab in the configuration tuning section of the application.
/// </summary>
public partial class MavFtpTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleConnectionSession connectionSession;
    private readonly ApplicationStateService stateService;
    private readonly IFileSaver fileSaver;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MavFtpTabViewModel> logger;
    private readonly Lock lifecycleSync = new();
    private readonly Lock operationSync = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private CancellationTokenSource? operationCancellation;
    private VehicleId? activeVehicleId;
    private readonly IList<IDisposable> disposables = [];
    private IDispatcherTimer? timer;
    private volatile bool disposed;

    private IVehicleFileSystemService? fileSystem;
    private const string NoConnection = "No connected vehicle.";
    private const string NoFiles = "No files available.";

    private const string NoRegisteredConnection = "No Connection registered with the vehicle. Please connect to the vehicle first.";

    [ObservableProperty] public partial string CurrentPath { get; set; } = "/";
    [ObservableProperty] public partial VehicleFileSystemEntryViewModel? SelectedEntry { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double TransferProgress { get; set; }
    [ObservableProperty] public partial string TransferDetails { get; set; } = string.Empty;
    [ObservableProperty] public partial string? StatusText { get; set; }
    [ObservableProperty] public partial string? EmptyText { get; set; }
    [ObservableProperty] public partial string? ErrorText { get; set; }
    [ObservableProperty] public partial bool HasEntries { get; set; }
    [ObservableProperty] public partial bool HasConnection { get; set; }


    /// <summary>
    /// Gets the collection of file system entries.
    /// </summary>
    public ObservableCollection<VehicleFileSystemEntryViewModel> Entries { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the user can navigate up in the file system.
    /// </summary>
    public bool CanNavigateUp => CurrentPath != "/" && !IsBusy;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavFtpTabViewModel"/> class.
    /// </summary>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="connectionSession">The vehicle connection session.</param>
    /// <param name="stateService"></param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="fileSaver">The file saver.</param>
    /// <param name="dispatcher"></param>
    /// <param name="logger"></param>
    public MavFtpTabViewModel(IVehicleRegistry vehicleRegistry, IVehicleConnectionSession connectionSession, ApplicationStateService stateService, IDomainEventHub domainEventHub, IFileSaver fileSaver,
        IDispatcher dispatcher, ILogger<MavFtpTabViewModel> logger)
    {
        this.vehicleRegistry = vehicleRegistry;
        this.connectionSession = connectionSession;
        this.stateService = stateService;
        this.fileSaver = fileSaver;
        this.dispatcher = dispatcher;
        this.logger = logger;
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        fileSystem = connectionSession.CreateMavFtpConnection();
        SetConnectionStatus();
        StartDelayedRefresh(1);
    }

    private async Task ResetFilesystemService(VehicleId vehicleId, CancellationToken ct)
    {
        CancelActiveOperation();
        IVehicleFileSystemService? ownedFileSystem;
        lock (lifecycleSync)
        {
            ownedFileSystem = fileSystem;
            fileSystem = null;
        }

        if (ownedFileSystem is not null)
        {
            try
            {
                await ownedFileSystem.ResetSessionsAsync(vehicleId, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Non Critical Error while resetting file system for vehicle {VehicleId}", vehicleId);
            }
            finally
            {
                await ownedFileSystem.DisposeAsync();
            }
        }
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        if (disposed)
        {
            return;
        }

        StopDelayedRefresh();
        activeVehicleId = null;
        HasConnection = false;
        HasEntries = false;
        Entries.Clear();
        await ResetFilesystemService(evt.VehicleId, ct);

        SetConnectionStatus();
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        if (disposed)
        {
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeCancellation.Token);
        var cancellationToken = linkedCancellation.Token;
        try
        {
            activeVehicleId = evt.VehicleId;
            Entries.Clear();
            SetConnectionStatus();
            await ResetFilesystemService(evt.VehicleId, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (disposed)
            {
                return;
            }

            SetConnectionStatus();
            if (stateService.IsConnected)
            {
                var newFileSystem = connectionSession.CreateMavFtpConnection();
                lock (lifecycleSync)
                {
                    if (!disposed)
                    {
                        fileSystem = newFileSystem;
                        newFileSystem = null;
                    }
                }

                if (newFileSystem is not null)
                {
                    await newFileSystem.DisposeAsync();
                    return;
                }

                await ResetSessionsAsync();
                SetConnectionStatus();
                StartDelayedRefresh(1);
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            // The transient page ViewModel was disposed during connection initialization.
        }
    }

    private void StartDelayedRefresh(int seconds)
    {
        if (disposed)
        {
            return;
        }

        StopDelayedRefresh();
        var newTimer = dispatcher.CreateTimer();
        if (newTimer is null)
        {
            return;
        }

        newTimer.Interval = TimeSpan.FromSeconds(seconds);
        newTimer.Tick += OnRefreshTimerTick;
        var shouldStart = false;
        lock (lifecycleSync)
        {
            if (!disposed)
            {
                timer = newTimer;
                shouldStart = true;
            }
        }

        if (shouldStart)
        {
            newTimer.Start();
            if (!disposed)
            {
                return;
            }
        }

        newTimer.Tick -= OnRefreshTimerTick;
        newTimer.Stop();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        StopDelayedRefresh();
        if (disposed || !stateService.IsConnected)
        {
            return;
        }

        RefreshAsync().FireAndForget();
        SetConnectionStatus();
    }

    private void StopDelayedRefresh()
    {
        IDispatcherTimer? activeTimer;
        lock (lifecycleSync)
        {
            activeTimer = timer;
            timer = null;
        }

        if (activeTimer is null)
        {
            return;
        }

        activeTimer.Stop();
        activeTimer.Tick -= OnRefreshTimerTick;
    }

    private void SetConnectionStatus()
    {
        if (disposed)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            ErrorText = null;
            StatusText = null;
            EmptyText = NoFiles;
            HasConnection = false;
            if (!stateService.IsConnected)
            {
                ErrorText = NoRegisteredConnection;
                return;
            }

            var vehicle = ResolveActiveVehicle();
            if (vehicle is null)
            {
                StatusText = NoConnection;
                ErrorText = StatusText;
                EmptyText = NoConnection;
                HasConnection = false;
                return;
            }

            HasConnection = true;
            HasEntries = Entries.Any();
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
        if (SelectedEntry is not null)
        {
            await OpenEntryAsync(SelectedEntry);
        }
    }

    [RelayCommand]
    private async Task OpenEntryAsync(VehicleFileSystemEntryViewModel entry)
    {
        SelectedEntry = entry;
        if (entry.IsDirectory)
        {
            await LoadDirectoryAsync(RemotePath.Join(CurrentPath, entry.Name));
            return;
        }

        await DownloadSelectedAsync();
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
            try
            {
                var activeFileSystem = fileSystem;
                if (activeFileSystem is null)
                {
                    dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                    return;
                }

                await activeFileSystem.ResetSessionsAsync(vehicle.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Non Critical Error while resetting Session for vehicle {VehicleId}", vehicle.Id);
            }

            dispatcher.Dispatch(() =>
            {
                StatusText = "MAVFTP sessions reset.";
                Entries.Clear();
                HasEntries = Entries.Any();
            });
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
                var activeFileSystem = fileSystem;
                if (activeFileSystem is null)
                {
                    dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                    return;
                }

                await activeFileSystem.DownloadFileAsync(vehicle.Id, remotePath, destination, progress, ct);
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
        CancelActiveOperation();
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
            var activeFileSystem = fileSystem;
            if (activeFileSystem is null)
            {
                dispatcher.Dispatch(() => StatusText = "MAVFTP sessions not initialized.");
                return;
            }

            var entries = await activeFileSystem.ListDirectoryAsync(vehicle.Id, path, ct);
            dispatcher.Dispatch(() =>
            {
                Entries.Clear();
                foreach (var entry in entries.OrderBy(x => x.Type).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Entries.Add(new VehicleFileSystemEntryViewModel(entry.Name, entry.Type, entry.Size));
                }

                HasEntries = Entries.Any();
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
        var enteredGate = false;
        CancellationTokenSource? operationSource = null;
        try
        {
            await operationGate.WaitAsync(lifetimeCancellation.Token);
            enteredGate = true;
            if (disposed)
            {
                return;
            }

            operationSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
            lock (operationSync)
            {
                operationCancellation = operationSource;
            }

            dispatcher.Dispatch(() =>
            {
                IsBusy = true;
                ErrorText = null;
                OnPropertyChanged(nameof(CanNavigateUp));
            });

            await operation(operationSource.Token);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            // Navigation disposed this transient ViewModel. Do not update its detached view.
        }
        catch (OperationCanceledException)
        {
            dispatcher.Dispatch(() => StatusText = "Operation cancelled.");
        }
        catch (TimeoutException ex)
        {
            dispatcher.Dispatch(() => ErrorText = "MAVFTP transfer timed out. Retrying Connection.");
            logger.LogWarning(ex, "MAVFTP transfer timed out. Retrying Connection.");
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
            dispatcher.Dispatch(() => ErrorText = "The vehicle returned an invalid MAVFTP response: " + ex.Message);
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
            lock (operationSync)
            {
                if (ReferenceEquals(operationCancellation, operationSource))
                {
                    operationCancellation = null;
                }
            }

            operationSource?.Dispose();
            if (enteredGate)
            {
                operationGate.Release();
            }

            if (!disposed)
            {
                dispatcher.Dispatch(() =>
                {
                    IsBusy = false;
                    OnPropertyChanged(nameof(CanNavigateUp));
                });
            }
        }
    }

    private void CancelActiveOperation()
    {
        lock (operationSync)
        {
            operationCancellation?.Cancel();
        }
    }

    private async Task DisposeFileSystemAfterOperationsAsync(IVehicleFileSystemService ownedFileSystem)
    {
        await operationGate.WaitAsync();
        try
        {
            await ownedFileSystem.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non Critical Error while disposing the MAVFTP view file system.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IVehicleFileSystemService? ownedFileSystem;
        lock (lifecycleSync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ownedFileSystem = fileSystem;
            fileSystem = null;
        }

        StopDelayedRefresh();
        lifetimeCancellation.Cancel();
        CancelActiveOperation();

        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();

        if (ownedFileSystem is not null)
        {
            _ = DisposeFileSystemAfterOperationsAsync(ownedFileSystem);
        }
    }
}

using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Configuration;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>
/// ViewModel for the MAVFTP tab in the configuration tuning section of the application.
/// </summary>
public partial class MavFtpTabViewModel : ViewModelBase
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleConnectionSession connectionSession;
    private readonly ApplicationStateService stateService;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDialogService dialogService;
    private readonly IFileSaveService fileSaver;
    private readonly Lock lifecycleSync = new();
    private readonly Lock operationSync = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private CancellationTokenSource? operationCancellation;
    private VehicleId? activeVehicleId;
    private readonly IList<IDisposable> disposables = [];
    private volatile bool disposed;

    private IVehicleFileSystemService? fileSystem;
    private const string NoConnection = "No connected vehicle.";
    private const string NoFiles = "No files available.";

    private const string NoRegisteredConnection = "No Connection registered with the vehicle. Please connect to the vehicle first.";

    [ObservableProperty]
    public partial string CurrentPath
    {
        get;
        set;
    } = "/";


    [ObservableProperty]
    public partial double TransferProgress
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string TransferDetails
    {
        get;
        set;
    } = string.Empty;


    [ObservableProperty]
    public partial string? EmptyText
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool HasEntries
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool HasConnection
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string Message
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Gets the collection of file system entries.
    /// </summary>
    public ObservableRangeCollection<VehicleFileSystemEntryViewModel> Entries { get; } = [];


    /// <summary>
    /// Gets or sets the currently selected file system entry.
    /// </summary>
    public VehicleFileSystemEntryViewModel? SelectedEntry
    {
        get; set;
    }

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
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="fileSaver">The file saver.</param>
    /// <param name="logger"></param>
    public MavFtpTabViewModel(IVehicleRegistry vehicleRegistry, IVehicleConnectionSession connectionSession, ApplicationStateService stateService,
        IDomainEventHub domainEventHub,
        IDialogService dialogService,
        IFileSaveService fileSaver, ILogger<MavFtpTabViewModel> logger) : base(logger)
    {
        this.vehicleRegistry = vehicleRegistry;
        this.connectionSession = connectionSession;
        this.stateService = stateService;
        this.domainEventHub = domainEventHub;
        this.dialogService = dialogService;
        this.fileSaver = fileSaver;
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
                Logger.LogDebug(ex, "Non Critical Error while resetting file system for vehicle {VehicleId}", vehicleId);
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

        activeVehicleId = null;
        Dispatcher.Dispatch(() =>
        {
            HasConnection = false;
            HasEntries = false;
            Entries.Clear();
            SelectedEntry = null;
        });
        await ResetFilesystemService(evt.VehicleId, ct);
        await ResetSessionsAsync();
        SetConnectionStatus();
        SelectionChanged();
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        if (disposed)
        {
            return;
        }

        operationCancellation ??= new CancellationTokenSource();

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, operationCancellation.Token);
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
                await Start();
            }
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            // The transient page ViewModel was disposed during connection initialization.
            Debug.Print("The transient page ViewModel was disposed during connection initialization.");
        }
    }
    private async Task Start()
    {
        await RefreshAsync();
        SetConnectionStatus();
        SelectionChanged();
    }

    private void SetConnectionStatus()
    {
        if (disposed)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            SetMessages(null, null);
            EmptyText = NoFiles;
            HasConnection = false;
            if (!stateService.IsConnected)
            {
                SetMessages(null, NoRegisteredConnection);
                return;
            }

            var vehicle = ResolveActiveVehicle();
            if (vehicle is null)
            {
                SetMessages(NoConnection, StatusMessage);
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
    private void SelectionChanged()
    {
        DownloadSelectedCommand.NotifyCanExecuteChanged();
        OpenSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpen()
    {
        return SelectedEntry is not null && SelectedEntry.IsDirectory;
    }

    private bool CanDownload()
    {
        return SelectedEntry is not null && !SelectedEntry.IsDirectory;
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenSelectedAsync()
    {
        if (SelectedEntry is not null)
        {
            await OpenEntryAsync(SelectedEntry);
        }
    }

    private async Task OpenEntryAsync(VehicleFileSystemEntryViewModel entry)
    {
        if (entry.IsDirectory)
        {
            await LoadDirectoryAsync(RemotePath.Join(CurrentPath, entry.Name));
            return;
        }

        await DownloadSelectedAsync(operationCancellation.Token);
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
        await Dispatcher.DispatchAsync(() => SetMessages(null, null));
        var vehicle = ResolveActiveVehicle();
        if (vehicle is null)
        {
            SetConnectionStatus();
            return;
        }

        operationCancellation ??= new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, async ct =>
        {
            try
            {
                var activeFileSystem = fileSystem;
                if (activeFileSystem is null)
                {
                    await Dispatcher.DispatchAsync(() => SetMessages("MAVFTP sessions not initialized."));
                    return;
                }

                await activeFileSystem.ResetSessionsAsync(vehicle.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogDebug(ex, "Non Critical Error while resetting Session for vehicle {VehicleId}", vehicle.Id);
            }

            await Dispatcher.DispatchAsync(() =>
            {
                SetMessages("MAVFTP sessions reset.");
                SelectedEntry = null;
                Entries.Clear();
                HasEntries = false;
            });
        });
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadSelectedAsync(CancellationToken cancellationToken = default)
    {
        var vehicle = ResolveActiveVehicle();
        if (vehicle is null || SelectedEntry is null || SelectedEntry.IsDirectory)
        {
            SetConnectionStatus();
            return;
        }

        var remotePath = RemotePath.Join(CurrentPath, SelectedEntry.Name);
        operationCancellation ??= new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, async ct =>
        {
            var temporary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), $"mavftp-{Guid.NewGuid():N}.tmp");
            try
            {
                Dispatcher.Dispatch(() => Message = string.Empty);
                using var disposable = await dialogService.DisplayProgressCancellableAsync(
                    () => Message,
                    new DialogOptions()
                    {
                        Title = "Loading Remote File"
                    },
                    operationCancellation.Token);

                await using var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var progress = new Progress<VehicleFileTransferProgress>(p =>
                {
                    TransferProgress = p.TotalBytes > 0 ? (double)p.BytesTransferred / p.TotalBytes.Value : 0;
                    TransferDetails = $"{p.BytesTransferred:N0} / {p.TotalBytes?.ToString("N0") ?? "?"} bytes · {p.BytesPerSecond ?? 0:N0} B/s";
                    Message = $"Downloading \n{TransferProgress} ({TransferDetails})";
                });


                var activeFileSystem = fileSystem;
                if (activeFileSystem is null)
                {
                    Dispatcher.Dispatch(() => SetMessages("MAVFTP sessions not initialized."));
                    return;
                }

                await activeFileSystem.DownloadFileAsync(vehicle.Id, remotePath, destination, progress, ct);
                destination.Position = 0;
                var savedPath = await fileSaver.SaveAsync(SelectedEntry.Name, destination, ct);
                Dispatcher.Dispatch(() => SetMessages(savedPath is not null
                    ? $"Downloaded to {savedPath}."
                    : "Download destination selection cancelled."));
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }

                SelectionChanged();
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelActiveOperation();
        SelectionChanged();
    }

    private async Task LoadDirectoryAsync(string path)
    {
        Dispatcher.Dispatch(() => Message = string.Empty);

        using var disposable = await dialogService.DisplayProgressCancellableAsync(
            () => Message,
            new DialogOptions()
            {
                Title = "Loading Remote Information"
            },
             CancellationToken.None);

        // using var disposable = await dialogService.DisplayProgressCancellableAsync("Loading Remote Information", () => Message);

        var vehicle = ResolveActiveVehicle();
        if (vehicle is null)
        {
            Dispatcher.Dispatch(() => Entries.Clear());
            SetConnectionStatus();
            SelectionChanged();
            return;
        }

        operationCancellation ??= new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, async ct =>
        {
            var activeFileSystem = fileSystem;
            if (activeFileSystem is null)
            {
                Dispatcher.Dispatch(() => SetMessages("MAVFTP sessions not initialized."));
                return;
            }

            var progress = new Progress<VehicleDirectoryProgress>(p => Message = $"Loading Directory {p.RemotePath}");
            var entries = await activeFileSystem.ListDirectoryAsync(vehicle.Id, path, progress, ct);

            Dispatcher.Dispatch(() =>
            {
                Message = $"Found {entries.Count} Remote Entries";
                var entryViewModels = new List<VehicleFileSystemEntryViewModel>();
                foreach (var entry in entries.OrderBy(x => x.Type).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    entryViewModels.Add(new VehicleFileSystemEntryViewModel(entry.Name, entry.Type, entry.Size));
                }

                Entries.ReplaceRange(entryViewModels);


                HasEntries = Entries.Any();
                CurrentPath = RemotePath.Normalize(path);
                SetMessages(Entries.Count == 0 ? "Directory is empty." : $"{Entries.Count} entries.");
                SelectedEntry = null;
                SelectionChanged();
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

    /// <inheritdoc />
    protected override async Task RunAsync(CancellationToken cancellationToken, Func<CancellationToken, Task> operation)
    {
        var enteredGate = false;
        var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            operationCancellation ??= new CancellationTokenSource();
            await operationGate.WaitAsync(operationCancellation.Token);
            enteredGate = true;
            if (disposed)
            {
                return;
            }

            lock (operationSync)
            {
                operationCancellation = operationSource;
            }

            Dispatcher.Dispatch(() =>
            {
                SetBusy();
                SetMessages(null, null);
                OnPropertyChanged(nameof(CanNavigateUp));
            });

            await operation(operationSource.Token);
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            // Navigation disposed this transient ViewModel. Do not update its detached view.
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Dispatch(() => SetMessages("Operation cancelled."));
        }
        catch (TimeoutException ex)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "MAVFTP transfer timed out. Retrying Connection."));
            Logger.LogWarning(ex, "MAVFTP transfer timed out. Retrying Connection.");
        }
        catch (MavFtpRemoteException ex) when (
            ex.Error == MavFtpNakError.UnknownCommand)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "The connected vehicle does not support this MAVFTP operation."));
        }
        catch (MavFtpRemoteException ex) when (
            ex.Error == MavFtpNakError.FileNotFound)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "The remote file or directory was not found."));
        }
        catch (MavFtpProtocolException ex)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "The vehicle returned an invalid MAVFTP response: " + ex.Message));
            Logger.LogError(ex, "Invalid MAVFTP protocol response.");
        }
        catch (InvalidOperationException ex)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "Vehicle is not connected."));
            Logger.LogWarning(ex, "MAVFTP operation has no vehicle.");
        }
        catch (Exception ex)
        {
            Dispatcher.Dispatch(() => SetMessages(null, "MAVFTP operation failed. The vehicle may not support MAVFTP."));
            Logger.LogError(ex, "MAVFTP UI operation failed.");
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
                Dispatcher.Dispatch(() =>
                {
                    ResetBusy();
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
            Logger.LogDebug(ex, "Non Critical Error while disposing the MAVFTP view file system.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    private bool isActivated = false;

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        isActivated = true;
        disposables.Clear();
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        fileSystem = connectionSession.CreateMavFtpConnection();
        SetConnectionStatus();
        await Start();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        if (disposed)
        {
            return;
        }
        if (!isActivated)
        {
            return;
        }
        Deactivate();
        IVehicleFileSystemService? ownedFileSystem;
        lock (lifecycleSync)
        {
            ownedFileSystem = fileSystem;
            fileSystem = null;
        }
        if (ownedFileSystem is not null)
        {
            await DisposeFileSystemAfterOperationsAsync(ownedFileSystem);
        }
    }

    /// <inheritdoc />
    private void Deactivate()
    {
        if (disposed)
        {
            return;
        }
        if (!isActivated)
        {
            return;
        }
        isActivated = false;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();

    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        CancelActiveOperation();
        Deactivate();
        disposed = true;

        IVehicleFileSystemService? ownedFileSystem;
        lock (lifecycleSync)
        {
            ownedFileSystem = fileSystem;
            fileSystem = null;
        }
        if (ownedFileSystem is not null)
        {
            _ = DisposeFileSystemAfterOperationsAsync(ownedFileSystem);
        }
    }

}

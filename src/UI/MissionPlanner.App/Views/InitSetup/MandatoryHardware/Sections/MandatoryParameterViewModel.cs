using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Provides lifecycle-safe presentation for metadata-backed mandatory parameter pages.</summary>
public abstract partial class MandatoryParameterViewModel : ViewModelBase
{
    private readonly INavigationService navigation;

    private readonly IActiveVehicleContext activeVehicle;
    private CancellationTokenSource? operationCancellation;
    private MissionPlanner.Shared.Models.Vehicles.Models.VehicleId? loadedVehicle;

    /// <summary>Gets or sets the parameter search query.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;
    /// <summary>Gets filtered settings with favorites first.</summary>
    public IEnumerable<PeripheralSettingViewModel> VisibleSettings => Settings
        .Where(row => string.IsNullOrWhiteSpace(SearchText) || $"{row.Name} {row.DisplayName} {row.Description}".Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(row => row.IsFavorite).ThenBy(row => row.Name, StringComparer.Ordinal);
    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleSettings));

    /// <summary>Gets whether a parameter has a persisted favorite preference.</summary>
    protected virtual bool IsFavorite(string name) => false;

    /// <summary>Persists a changed favorite preference when supported by the page.</summary>
    protected virtual Task SaveFavoriteAsync(string name, bool favorite) => Task.CompletedTask;

    private async void OnSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is not PeripheralSettingViewModel row || args.PropertyName != nameof(PeripheralSettingViewModel.IsFavorite))
        {
            return;
        }
        OnPropertyChanged(nameof(VisibleSettings));
        try
        {
            await SaveFavoriteAsync(row.Name, row.IsFavorite);
        }
        catch (Exception exception)
        {
            SetMessages("Could not save favorite preference.", exception.Message);
        }
    }

    /// <summary>Initializes a metadata-backed mandatory workflow.</summary>
    protected MandatoryParameterViewModel(IActiveVehicleContext activeVehicle, ILogger logger, INavigationService navigation)
        : base(logger)
    {
        this.navigation = navigation;
        this.activeVehicle = activeVehicle;
    }

    /// <summary>Gets the supported settings.</summary>
    public ObservableRangeCollection<PeripheralSettingViewModel> Settings
    {
        get;
    } = [];

    /// <summary>Gets workflow guidance.</summary>
    public ObservableRangeCollection<string> Guidance
    {
        get;
    } = [];


    /// <summary>Gets whether supported settings were found.</summary>
    public bool HasSettings => Settings.Count > 0;


    /// <summary>Loads the parameter configuration from the workflow service.</summary>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            ShowDisconnected();
            return;
        }

        var token = StartOperation();
        SetBusy();
        try
        {
            var configuration = await LoadConfigurationAsync(vehicleId, token);
            token.ThrowIfCancellationRequested();
            Dispatcher.Dispatch(() =>
            {
                if (token.IsCancellationRequested || activeVehicle.VehicleId != vehicleId)
                {
                    return;
                }
                if (loadedVehicle != vehicleId)
                {
                    ClearSettings();
                }
                loadedVehicle = vehicleId;
                Show(configuration);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Loading mandatory parameter configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
        finally
        {
            if (operationCancellation?.Token == token)
            {
                ResetBusy();
            }
        }
    }

    /// <inheritdoc />
    public virtual void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        SetMessages("Connect a vehicle to load settings.");
        activeVehicle.Changed += OnActiveVehicleChanged;
        await base.ActivateAsync();
        await LoadAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        Cancel();
        ResetBusy();
        return base.DeactivateAsync();
    }


    /// <summary>Loads configuration for the concrete workflow.</summary>
    protected abstract Task<MandatoryParameterConfiguration> LoadConfigurationAsync(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId,
        CancellationToken cancellationToken);

    /// <summary>Applies one setting for the concrete workflow.</summary>
    protected abstract Task<MandatoryParameterApplyResult> ApplySettingAsync(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId,
        string name, double value, CancellationToken cancellationToken);

    /// <summary>Opens the shared Parameters Editor workspace.</summary>
    [RelayCommand]
    private Task OpenFullParametersAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return IsBusy ? Task.CompletedTask : LoadAsync();
    }

    /// <summary>Downloads current vehicle parameters before rebuilding the settings.</summary>
    protected virtual Task ReloadParametersAsync(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId, CancellationToken token) => Task.CompletedTask;

    [RelayCommand]
    private async Task ReloadFromVehicleAsync()
    {
        if (IsBusy || activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
        {
            return;
        }
        var token = StartOperation();
        SetBusy();
        try
        {
            await ReloadParametersAsync(id, token);
            token.ThrowIfCancellationRequested();
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            if (operationCancellation?.Token == token)
            {
                ResetBusy();
            }
        }
    }

    [RelayCommand]
    private Task ApplyModifiedAsync() => ApplyChanges(Settings.Where(row => row.IsDirty)
        .OrderBy(row => row.Name.EndsWith("_ENABLE", StringComparison.Ordinal) || row.Name.EndsWith("_ENABLED", StringComparison.Ordinal))
        .Select(row => (row.Name, row.Value)).ToArray());

    private async Task Apply((string Name, double Value) change)
    {
        await ApplyChanges([change]);
    }

    private async Task ApplyChanges(IReadOnlyList<(string Name, double Value)> changes)
    {
        if (IsBusy)
        {
            return;
        }
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            ShowDisconnected();
            return;
        }

        var token = StartOperation();
        SetBusy();
        try
        {
            var applied = 0;
            if (loadedVehicle != vehicleId)
            {
                SetMessages("Wait for parameters from the selected vehicle before applying edits.");
                return;
            }
            foreach (var change in changes)
            {
                token.ThrowIfCancellationRequested();
                var result = await ApplySettingAsync(vehicleId, change.Name, change.Value, token);
                token.ThrowIfCancellationRequested();
                if (!result.Success)
                {
                    SetMessages($"Confirmed {applied} of {changes.Count} changes. Remaining edits are preserved.", result.Message);
                    return;
                }
                Settings.FirstOrDefault(row => row.Name == change.Name)?.AcceptValue(change.Value);
                applied++;
            }
            SetMessages($"Confirmed {applied} parameter change(s). Other pending edits are preserved.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages("Connect a vehicle to load settings.", exception.Message);
        }
        finally
        {
            if (operationCancellation?.Token == token)
            {
                ResetBusy();
            }
        }
    }

    private CancellationToken StartOperation()
    {
        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetMessages(null);
        return operationCancellation.Token;
    }

    private void Show(MandatoryParameterConfiguration configuration)
    {
        var previous = Settings.ToDictionary(row => row.Name);
        var allSettings = configuration.Settings.Select(s =>
        {
            if (previous.TryGetValue(s.Name, out var old) && old.IsDirty)
            {
                return old;
            }
            var row = new PeripheralSettingViewModel(s, Apply) { IsFavorite = old?.IsFavorite ?? IsFavorite(s.Name) };
            row.PropertyChanged += OnSettingChanged;
            return row;
        }).ToArray();
        Settings.ReplaceRange(allSettings);
        foreach (var old in previous.Values.Where(old => !allSettings.Contains(old)))
        {
            old.PropertyChanged -= OnSettingChanged;
        }
        Guidance.ReplaceRange(configuration.Guidance);

        SetMessages(Settings.Count == 0
            ? "This firmware does not report settings for this workflow."
            : $"{Settings.Count} supported setting(s) loaded. Review changes before applying.");
        OnPropertyChanged(nameof(HasSettings));
        OnPropertyChanged(nameof(VisibleSettings));
    }

    private void ShowDisconnected()
    {
        Cancel();
        ResetBusy();
        Dispatcher.Dispatch(() =>
        {
            ClearSettings();
            loadedVehicle = null;
            OnPropertyChanged(nameof(VisibleSettings));
            Guidance.Clear();
            OnPropertyChanged(nameof(HasSettings));
        });
    }

    private void ClearSettings()
    {
        foreach (var row in Settings)
        {
            row.PropertyChanged -= OnSettingChanged;
        }
        Settings.Clear();
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.DispatchAsync(LoadAsync);
    }
}


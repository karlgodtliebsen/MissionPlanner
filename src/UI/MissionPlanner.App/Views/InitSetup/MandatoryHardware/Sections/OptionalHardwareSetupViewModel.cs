using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Hosts discovered optional-hardware modules as independent editable groups.</summary>
public sealed partial class OptionalHardwareSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService hardwareService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<OptionalHardwareSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the optional-hardware Setup workflow.</summary>
    /// <param name="descriptor">The optional-hardware workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="hardwareService">The optional-hardware service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public OptionalHardwareSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IOptionalHardwareService hardwareService,
        IDispatcher dispatcher,
        ILogger<OptionalHardwareSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.hardwareService = hardwareService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        _ = LoadAsync();
    }

    /// <summary>Gets the discovered optional-hardware modules.</summary>
    public ObservableCollection<OptionalHardwareModuleViewModel> Modules { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's optional hardware.";

    /// <summary>Gets whether a confirmed change requires a reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; private set; }

    /// <summary>Gets whether any optional-hardware modules were discovered.</summary>
    public bool HasModules => Modules.Count > 0;

    /// <summary>Loads the available optional-hardware modules for the active vehicle.</summary>
    /// <returns>A task that completes after the modules are projected.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before loading optional hardware.";
            return;
        }

        var token = StartOperation();
        try
        {
            var modules = await hardwareService.GetModulesAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(modules));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading optional hardware failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <summary>Writes one peripheral setting and reloads on success.</summary>
    /// <param name="parameterName">The parameter to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplyAsync(string parameterName, double value)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before editing optional hardware.";
            return;
        }

        var token = StartOperation();
        try
        {
            var result = await hardwareService.SetValueAsync(vehicleId, parameterName, value, token);
            Status = result.Message;
            if (result.Success)
            {
                RebootRequired |= result.RequiresReboot;
                var modules = await hardwareService.GetModulesAsync(vehicleId, token);
                dispatcher.Dispatch(() => Show(modules, true));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying optional-hardware setting failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            return;
        }

        var token = StartOperation();
        try
        {
            await hardwareService.RefreshAsync(vehicleId, token);
            var modules = await hardwareService.GetModulesAsync(vehicleId, token);
            dispatcher.Dispatch(() =>
            {
                RebootRequired = false;
                Show(modules);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Refreshing optional hardware failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        return operationCancellation.Token;
    }

    private void Show(IReadOnlyList<OptionalHardwareModuleView> modules, bool preserveStatus = false)
    {
        Modules.Clear();
        foreach (var module in modules)
        {
            Modules.Add(new OptionalHardwareModuleViewModel(module, this));
        }

        if (!preserveStatus)
        {
            Status = Modules.Count == 0
                ? "No optional hardware was detected for this vehicle."
                : "Configure detected optional hardware. Only applicable modules are shown.";
        }

        OnPropertyChanged(nameof(HasModules));
    }
}

/// <summary>Presents one optional-hardware module as an independent group of settings.</summary>
public sealed class OptionalHardwareModuleViewModel
{
    /// <summary>Initializes a module group.</summary>
    /// <param name="module">The module projection.</param>
    /// <param name="parent">The owning workflow.</param>
    public OptionalHardwareModuleViewModel(OptionalHardwareModuleView module, OptionalHardwareSetupViewModel parent)
    {
        Title = module.Title;
        Description = module.Description;
        Issues = module.Issues.Select(issue => $"[{issue.Severity}] {issue.Message}").ToArray();
        Settings = module.Settings.Select(setting => new PeripheralSettingViewModel(setting, parent)).ToArray();
    }

    /// <summary>Gets the module title.</summary>
    public string Title { get; }

    /// <summary>Gets the module description.</summary>
    public string Description { get; }

    /// <summary>Gets the module configuration issues.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Gets whether the module has issues.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets the module settings.</summary>
    public IReadOnlyList<PeripheralSettingViewModel> Settings { get; }
}

/// <summary>Presents one editable peripheral setting with either options or numeric entry.</summary>
public sealed partial class PeripheralSettingViewModel : ObservableObject
{
    private readonly OptionalHardwareSetupViewModel parent;
    private readonly PeripheralSetting setting;

    /// <summary>Initializes a peripheral setting row.</summary>
    /// <param name="setting">The setting projection.</param>
    /// <param name="parent">The owning workflow.</param>
    public PeripheralSettingViewModel(PeripheralSetting setting, OptionalHardwareSetupViewModel parent)
    {
        this.setting = setting;
        this.parent = parent;
        NumericValue = setting.CurrentValue;
        SelectedOption = setting.Options.FirstOrDefault(option => Math.Abs(option.Value - setting.CurrentValue) <= 0.0005);
    }

    /// <summary>Gets the parameter display name.</summary>
    public string DisplayName => setting.DisplayName;

    /// <summary>Gets the parameter name.</summary>
    public string Name => setting.Name;

    /// <summary>Gets the metadata options.</summary>
    public IReadOnlyList<PeripheralSettingOption> Options => setting.Options;

    /// <summary>Gets whether the setting exposes discrete options.</summary>
    public bool HasOptions => setting.Options.Count > 0;

    /// <summary>Gets whether the setting is free numeric entry.</summary>
    public bool IsNumeric => setting.Options.Count == 0;

    /// <summary>Gets whether the setting is sensitive.</summary>
    public bool IsSecret => setting.IsSecret;

    /// <summary>Gets whether a reboot is required after changing this setting.</summary>
    public bool RebootRequired => setting.RebootRequired;

    /// <summary>Gets or sets the selected discrete option.</summary>
    [ObservableProperty]
    public partial PeripheralSettingOption? SelectedOption { get; set; }

    /// <summary>Gets or sets the free numeric value.</summary>
    [ObservableProperty]
    public partial double NumericValue { get; set; }

    [RelayCommand]
    private Task Apply()
    {
        var value = HasOptions ? SelectedOption?.Value ?? setting.CurrentValue : NumericValue;
        return parent.ApplyAsync(setting.Name, value);
    }
}

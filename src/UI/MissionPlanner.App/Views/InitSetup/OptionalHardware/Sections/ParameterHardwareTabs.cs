using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Shared lifecycle model for one metadata-backed Optional Hardware module.</summary>
public abstract partial class ParameterHardwareViewModel : OptionalHardware.OptionalHardwareBaseViewModel
{
    private readonly string moduleKey;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService service;
    private CancellationTokenSource? cancellation;

    protected ParameterHardwareViewModel(string moduleKey, IActiveVehicleContext activeVehicle, IOptionalHardwareService service)
    {
        this.moduleKey = moduleKey;
        this.activeVehicle = activeVehicle;
        this.service = service;
        activeVehicle.Changed += Changed;
        _ = LoadAsync();
    }

    /// <summary>Gets editable settings.</summary>
    public ObservableCollection<ParameterSettingViewModel> Settings { get; } = [];

    /// <summary>Gets status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Loading supported settings…";

    /// <summary>Gets reboot state.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; private set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        if (activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle to load this hardware.";
            Settings.Clear();
            return;
        }

        try
        {
            IsBusy = true;
            var module = (await service.GetModulesAsync(id, cancellation.Token)).FirstOrDefault(x => x.Key == moduleKey);
            Settings.Clear();
            if (module is null)
            {
                Status = "This hardware is not reported by the active vehicle.";
                return;
            }

            foreach (var setting in module.Settings)
            { Settings.Add(new ParameterSettingViewModel(setting, ApplyAsync)); }

            Status = module.Description;
        }
        catch (OperationCanceledException) { }
        finally { IsBusy = false; }
    }

    private async Task ApplyAsync(PeripheralSetting setting, double value)
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var result = await service.SetValueAsync(id, setting.Name, value, cancellation?.Token ?? default);
        Status = result.Message;
        RebootRequired |= result.RequiresReboot;
        if (result.Success)
        {
            await LoadAsync();
        }
    }

    private void Changed(object? s, ActiveVehicleChangedEventArgs e)
    {
        _ = LoadAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= Changed;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }
}

/// <summary>One explicit-apply metadata-backed setting.</summary>
public sealed partial class ParameterSettingViewModel(PeripheralSetting setting, Func<PeripheralSetting, double, Task> apply) : ObservableObject
{
    public string Name => setting.Name;
    public string DisplayName => setting.DisplayName;
    public double CurrentValue => setting.CurrentValue;
    [ObservableProperty] public partial double PendingValue { get; set; } = setting.CurrentValue;

    [RelayCommand]
    private Task ApplyAsync()
    {
        return apply(setting, PendingValue);
    }
}

public sealed class CanGpsOrderViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("can-gps-order", v, s);

public sealed class RangefinderViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("rangefinder", v, s);

public sealed class AirspeedViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("airspeed", v, s);

public sealed class OpticalFlowViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("optical-flow", v, s)
{
    /// <summary>Gets the focus/image capability status.</summary>
    public string FocusCapabilityStatus => "PX4Flow focus imagery requires a compatible image handshake stream. Focus mode remains unavailable until that stream is detected; parameter configuration is independent.";
}

public sealed class ParachuteViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("parachute", v, s);

public abstract class ParameterHardwareView<T> : TabViewLifecycleContent<T> where T : ParameterHardwareViewModel
{
    protected ParameterHardwareView()
    {
        var list = new VerticalStackLayout { Spacing = 8 };
        list.SetBinding(BindableLayout.ItemsSourceProperty, "Settings");
        BindableLayout.SetItemTemplate(list, new DataTemplate(() =>
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(220), new ColumnDefinition(GridLength.Star), new ColumnDefinition(90) } };
            var name = new Label();
            name.SetBinding(Label.TextProperty, "DisplayName");
            var entry = new Entry { Keyboard = Keyboard.Numeric };
            entry.SetBinding(Entry.TextProperty, "PendingValue");
            Grid.SetColumn(entry, 1);
            var button = new Button { Text = "Apply" };
            button.SetBinding(Button.CommandProperty, "ApplyCommand");
            Grid.SetColumn(button, 2);
            grid.Add(name);
            grid.Add(entry);
            grid.Add(button);
            return grid;
        }));
        var status = new Label();
        status.SetBinding(Label.TextProperty, "Status");
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 10, Children = { status, list } } };
    }
}

public sealed class CanGpsOrderView : ParameterHardwareView<CanGpsOrderViewModel>;

public sealed class RangefinderView : ParameterHardwareView<RangefinderViewModel>;

public sealed class AirspeedView : ParameterHardwareView<AirspeedViewModel>;

public sealed class OpticalFlowView : ParameterHardwareView<OpticalFlowViewModel>
{
    public OpticalFlowView()
    {
        if (Content is ScrollView { Content: VerticalStackLayout layout })
        {
            var focus = new Label { TextColor = Colors.Orange };
            focus.SetBinding(Label.TextProperty, "FocusCapabilityStatus");
            layout.Insert(1, focus);
        }
    }
}

public sealed class ParachuteView : ParameterHardwareView<ParachuteViewModel>;

using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FirmwarePanelViewModelTests
{
    [Fact]
    public async Task ParentSubscribesOnlyWhileActiveAndDoesNotDuplicateAfterReactivation()
    {
        using var services = CreateServices();
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        var devices = services.GetRequiredService<DetectedDeviceViewModel>();
        Assert.Same(devices, parent.Catalogue.Devices);
        Assert.Same(devices, parent.Custom.Devices);
        Assert.Same(parent.Selected, parent.Dfu.Selected);
        var notifications = 0;
        parent.InstallCommand.CanExecuteChanged += (_, _) => notifications++;

        await parent.ActivateAsync();
        notifications = 0;
        devices.SelectedDevice = new(new SerialDeviceDescriptor("COM11"), true, "test");
        Assert.Equal(1, notifications);
        Assert.True(parent.Custom.HasDevice);

        await parent.DeactivateAsync();
        notifications = 0;
        devices.SelectedDevice = null;
        Assert.Equal(0, notifications);

        await parent.ActivateAsync();
        notifications = 0;
        devices.SelectedDevice = new(new SerialDeviceDescriptor("COM14"), true, "test");
        Assert.Equal(1, notifications);
        await parent.DeactivateAsync();
    }

    [Fact]
    public void CatalogueFiltersAndRetainsSelectionAcrossRefresh()
    {
        using var services = CreateServices();
        var catalogue = services.GetRequiredService<FirmwareCatalogViewModel>();
        FirmwareManifestEntry Entry(int id, string version) => new(new FirmwareVersion(version), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(id, "board" + id, FirmwareVehicleType.Copter),
            new FirmwareArtifact(new Uri($"https://example.test/{id}.apj"), FirmwareImageFormat.Apj));
        var entries = new[] { Entry(50, "4.6.0"), Entry(51, "4.5.0") };
        catalogue.SetCatalogue(entries, [], false);
        Assert.Equal(2, catalogue.FilteredFirmwareChoices.Count);
        catalogue.SelectedVersion = entries[0].Version.ToString();
        Assert.Single(catalogue.FilteredFirmwareChoices);
        catalogue.SelectedFirmware = catalogue.FilteredFirmwareChoices[0];
        catalogue.SetCatalogue(entries, [], false);
        Assert.Equal(50, catalogue.SelectedFirmware!.BoardId);
        Assert.Equal(2, catalogue.FilteredFirmwareChoices.Count);
    }

    [Fact]
    public async Task PanelCommandAwaitsParentOperation()
    {
        using var services = CreateServices();
        var selected = services.GetRequiredService<SelectedFirmwareViewModel>();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        selected.OperationRequested += request => request.Completion = completion.Task;
        var pending = selected.DownloadAndValidateCommand.ExecuteAsync(null);
        Assert.False(pending.IsCompleted);
        completion.SetResult();
        await pending;
    }

    [Fact]
    public async Task ClosingCustomPanelRejectsLateFilePickerResult()
    {
        using var services = CreateServices();
        var custom = services.GetRequiredService<CustomFirmwareViewModel>();
        var picker = services.GetRequiredService<IFirmwareFilePicker>();
        var response = new TaskCompletionSource<FirmwareFileSelection?>();
        picker.PickAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        await custom.ActivateAsync();
        custom.HasDevice = true;
        var changed = 0;
        custom.PackageChanged += _ => changed++;
        var pending = custom.LoadCustomFirmwareCommand.ExecuteAsync(null);
        await custom.DeactivateAsync();
        response.SetResult(new("custom.apj", _ => throw new InvalidOperationException("Late file must not be opened")));
        await pending;
        Assert.Null(custom.CustomPackage);
        Assert.Equal(0, changed);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IUiDispatcher>(new InlineDispatcher());
        services.AddSingleton(Substitute.For<IDomainEventHub>());
        services.AddSingleton(Substitute.For<ITextClipboardService>());
        services.AddSingleton(Substitute.For<IFirmwareFilePicker>());
        services.AddSingleton(Substitute.For<IFirmwarePackageReader>());
        services.AddSingleton(Substitute.For<IFirmwareSupportLinkProvider>());
        services.AddSingleton(Substitute.For<IExternalLinkLauncher>());
        services.AddSingleton(Substitute.For<IDeviceManagerLauncher>());
        services.AddSingleton(Substitute.For<IFirmwareCatalogService>());
        services.AddSingleton(Substitute.For<IFirmwareInstallationService>());
        services.AddSingleton(Substitute.For<IFirmwarePreparationService>());
        services.AddSingleton(Substitute.For<IDfuInstallationService>());
        services.AddSingleton(Substitute.For<IDfuDeviceCatalog>());
        services.AddSingleton(Substitute.For<IDfuToolLocator>());
        services.AddSingleton(Substitute.For<IEmbeddedBootloaderUpdateService>());
        services.AddSingleton(Substitute.For<IFirmwareSerialDeviceCatalog>());
        services.AddSingleton<IFirmwarePageModeResolver, FirmwarePageModeResolver>();
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(true); // Prevent hardware/catalogue I/O during activation.
        services.AddSingleton(active);
        services.AddSingleton(Substitute.For<IUserConfirmationService>());
        services.AddSingleton(Substitute.For<IDialogService>());
        services.AddSingleton<FirmwareDialogCoordinator>();
        services.AddSingleton<FirmwareCatalogViewModel>();
        services.AddSingleton<DetectedDeviceViewModel>();
        services.AddSingleton<CustomFirmwareViewModel>();
        services.AddSingleton<STM32BootloaderViewModel>();
        services.AddSingleton<SelectedFirmwareViewModel>();
        services.AddSingleton<ValidatedPackageViewModel>();
        services.AddSingleton<DiagnosticsReportViewModel>();
        services.AddSingleton<FirmwareHelpViewModel>();
        services.AddTransient<InstallFirmwareViewModel>();
        return services.BuildServiceProvider();
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}

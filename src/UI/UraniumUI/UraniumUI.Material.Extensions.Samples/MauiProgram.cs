using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;
using UraniumUI.Material.Dialogs;
using UraniumUI.Material.Extensions.Samples.AppViewModels;
using UraniumUI.Material.Extensions.Samples.ControlsSamples;
using UraniumUI.Material.Extensions.Samples.DataGrids;
using UraniumUI.Material.Extensions.Samples.DataGrids.ArduPilotSample;
using UraniumUI.Material.Extensions.Samples.DataGridSamples;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;
using UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;
using UraniumUI.Material.Extensions.Samples.DialogSamples;

namespace UraniumUI.Material.Extensions.Samples;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .UseUraniumUIBlurs(false)
            .ConfigureMopups()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                fonts.AddMaterialSymbolsFonts();
            });

        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure => configure.AddDebug());
        builder.Services.AddCommunityToolkitDialogs();
        builder.Services.AddMopupsDialogs();


        builder.Services.AddSingleton<AppShellContentViewModel>();
        builder.Services.AddSingleton<ThemeChangeViewModel>();
        builder.Services.AddSingleton<ParametersFileHandler>();
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
        builder.Services.AddSingleton<IExtendedDialogService, ExtendedDialogService>();
        builder.Services.AddSingleton<VirtualizedDataGridViewModel>();
        builder.Services.AddTransient<VirtualizedDataGridSampleViewModel>();
        builder.Services.AddTransient<EditorDataGridPageViewModel>();
        builder.Services.AddSingleton<SelectableDataGridPageViewModel1>();
        builder.Services.AddSingleton<SelectableDataGridPageViewModel2>();
        builder.Services.AddSingleton<SimpleDataGridPageViewModel>();
        builder.Services.AddSingleton<CustomDataGridPageViewModel>();
        builder.Services.AddSingleton<CompareDataGridsViewModel>();
        builder.Services.AddSingleton<PaginationSampleViewModel>();
        builder.Services.AddSingleton<PaginationSampleExtendedViewModel>();
        //builder.Services.AddSingleton<CompareDataGridsViewModel>();
        builder.Services.AddSingleton<DialogSampleViewModel>();

        builder.Services.AddTransient<TabViewModel1>();
        builder.Services.AddTransient<TabViewModel2>();
        builder.Services.AddTransient<TabViewModel3>();

        return builder.Build();
    }
}

using CommunityToolkit.Maui;
using UraniumUI.Tests.Core;

namespace UraniumUI.Material.Tests.UraniumUI.Core.Tests;

public static class ApplicationExtensions
{
    public static void CreateAndSetMockApplication(Action<MauiAppBuilder>? builder = null)
    {
        // The upstream UraniumUI tests use xUnit v2, whose test context allows MAUI
        // to resolve a dispatcher. This project uses xUnit v3 and targets neutral
        // net10.0, so install an explicit dispatcher for headless BindableObjects.
        DispatcherProvider.SetCurrent(
            TestDispatcherProvider.Instance);

        var appBuilder = MauiApp
            .CreateBuilder()
            .UseMauiApp<MockApplication>()
            .UseMauiCommunityToolkit()
            .UseUraniumUI();

        appBuilder.Services.AddSingleton<IDispatcherProvider>(
            TestDispatcherProvider.Instance);
        appBuilder.Services.AddSingleton<IDispatcher>(
            TestDispatcherProvider.Instance.Dispatcher);

        builder?.Invoke(appBuilder);
        appBuilder.ConfigureDispatching();

        var mauiApp = appBuilder.Build();
        var application = mauiApp.Services.GetRequiredService<IApplication>();

        Application.Current = application as Application;

        application.Handler = new ApplicationHandlerStub();
        application.Handler.SetMauiContext(new HandlersContextStub(mauiApp.Services));
    }
}

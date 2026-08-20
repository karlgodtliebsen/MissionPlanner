using Microsoft.UI.Windowing;
using MissionPlanner.App.Services;
using MissionPlanner.App.Theming;
using MauiColor = Microsoft.Maui.Graphics.Color;
using MauiWindow = Microsoft.Maui.Controls.Window;
using WindowsColor = Windows.UI.Color;

namespace MissionPlanner.WinUI;

/// <summary>
/// Service to manage the theme of the window title bar in a WinUI application.
/// </summary>
internal sealed class WinUiWindowTitleBarThemeService : IWindowTitleBarThemeService
{
    private MauiWindow? window;
    private IThemeManager? themeManager;
    private ResourceDictionary? activeResources;

    /// <summary>
    /// Attaches the service to a MauiWindow, IThemeManager, and ResourceDictionary to manage the title bar theme.
    /// </summary>
    /// <param name="win"></param>
    /// <param name="themeMan"></param>
    /// <param name="activeResour"></param>
    public void Attach(MauiWindow win, IThemeManager themeMan, ResourceDictionary activeResour)
    {
        Detach();
        window = win;
        themeManager = themeMan;
        activeResources = activeResour;
        window.HandlerChanged += OnWindowHandlerChanged;
        window.Destroying += OnWindowDestroying;
        themeManager.ThemeChanged += OnThemeChanged;
        ApplyColors();
    }

    private void OnWindowHandlerChanged(object? sender, EventArgs args)
    {
        ApplyColors();
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs args)
    {
        ApplyColors();
    }

    private void OnWindowDestroying(object? sender, EventArgs args)
    {
        Detach();
    }

    private void ApplyColors()
    {
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window platformWindow ||
            activeResources is null ||
            !TryGetColor(ThemeResourceKeys.Primary, out var primary) ||
            !TryGetColor(ThemeResourceKeys.OnPrimary, out var onPrimary) ||
            !TryGetColor(ThemeResourceKeys.PrimaryContainer, out var primaryContainer) ||
            !TryGetColor(ThemeResourceKeys.OnPrimaryContainer, out var onPrimaryContainer))
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        var wnd = AppWindow.GetFromWindowId(windowId);
        var titleBar = wnd.TitleBar;
        titleBar.BackgroundColor = ToWindowsColor(primary); // ToWindowsColor(MauiColor.Parse("Red")); // WindowsColor.FromArgb(64, 64, 64, 128); /
        titleBar.ForegroundColor = ToWindowsColor(onPrimary);
        titleBar.InactiveBackgroundColor = ToWindowsColor(primary);
        titleBar.InactiveForegroundColor = ToWindowsColor(onPrimary);
        titleBar.ButtonBackgroundColor = ToWindowsColor(primary);
        titleBar.ButtonForegroundColor = ToWindowsColor(onPrimary);
        titleBar.ButtonHoverBackgroundColor = ToWindowsColor(primaryContainer);
        titleBar.ButtonHoverForegroundColor = ToWindowsColor(onPrimaryContainer);
        titleBar.ButtonPressedBackgroundColor = ToWindowsColor(primaryContainer);
        titleBar.ButtonPressedForegroundColor = ToWindowsColor(onPrimaryContainer);
        titleBar.ButtonInactiveBackgroundColor = ToWindowsColor(primary);
        titleBar.ButtonInactiveForegroundColor = ToWindowsColor(onPrimary);
    }

    private bool TryGetColor(string key, out MauiColor color)
    {
        if (activeResources!.TryGetValue(key, out var value) && value is MauiColor resourceColor)
        {
            color = resourceColor;
            return true;
        }

        color = null!;
        return false;
    }

    private static WindowsColor ToWindowsColor(MauiColor color)
    {
        return WindowsColor.FromArgb(
            ToByte(color.Alpha),
            ToByte(color.Red),
            ToByte(color.Green),
            ToByte(color.Blue));
    }

    private static byte ToByte(float component)
    {
        return (byte)Math.Clamp((int)Math.Round(component * byte.MaxValue), byte.MinValue, byte.MaxValue);
    }

    private void Detach()
    {
        if (window is not null)
        {
            window.HandlerChanged -= OnWindowHandlerChanged;
            window.Destroying -= OnWindowDestroying;
            window = null;
        }

        themeManager?.ThemeChanged -= OnThemeChanged;
        themeManager = null;

        activeResources = null;
    }
}

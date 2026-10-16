using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Themes;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises theme submenus, native radio selection and the host's two-way binding.</summary>
[Collection("Document rendering")]
public sealed class ThemeMenuTests
{
    /// <summary>Uses the production application themes with no vehicle services.</summary>
    public static AppBuilder BuildAvaloniaApp() => PaletteThemeRenderingTests.BuildAvaloniaApp();

    /// <summary>Every persisted theme appears once, with colors nested under Dark and Light.</summary>
    [Fact]
    public async Task MenuGroupsAllCatalogChoices()
    {
        await WithAvalonia(() =>
        {
            var menu = new ThemeMenuFlyout();
            var roots = menu.Items.OfType<MenuItem>().ToArray();
            Assert.Equal(new[] { "Default", "Dark", "Light", "High contrast" }, roots.Select(item => item.Header));
            Assert.True(roots[0].IsChecked);
            foreach (var appearance in roots.Skip(1).Take(2))
            {
                var colors = appearance.Items.Cast<MenuItem>().ToArray();
                Assert.Equal(16, colors.Length);
                Assert.Equal("Blue", colors[0].Header);
                Assert.All(colors, color => Assert.Equal(
                    Equals(color.Header, "Blue") ? appearance.Header : $"{appearance.Header} {color.Header}",
                    AutomationProperties.GetName(color)));
            }
            Assert.Equal(roots[1].Items.Cast<MenuItem>().Select(item => item.Header),
                roots[2].Items.Cast<MenuItem>().Select(item => item.Header));
            var leaves = Leaves(menu).ToArray();
            Assert.Equal(AvaloniaThemeCatalog.Items.Select(theme => theme.Name).Order(),
                leaves.Select(AutomationProperties.GetName).Order());
            Assert.All(leaves, item => Assert.Equal(MenuItemToggleType.Radio, item.ToggleType));
            Assert.Single(leaves.Select(item => item.GroupName).Distinct());
        });
    }

    /// <summary>Keyboard selection closes the submenu, updates its binding and keeps other selectors synchronized.</summary>
    [Fact]
    public async Task SelectingThemeUpdatesBindingAndRadioState()
    {
        await WithAvalonia(() =>
        {
            var source = new ThemeSelection();
            var menu = new ThemeMenuFlyout();
            var otherMenu = new ThemeMenuFlyout();
            foreach (var selector in new[] { menu, otherMenu })
            {
                selector.Bind(ThemeMenuFlyout.SelectedThemeProperty,
                    new Binding(nameof(ThemeSelection.SelectedTheme)) { Source = source, Mode = BindingMode.TwoWay });
            }
            var button = new Button { Content = "Theme", Flyout = menu };
            var window = new Window { Content = button, Width = 900, Height = 850 };
            window.Show();
            try
            {
                foreach (var id in new[] { "dark-orange", "light-purple", "light", "aquatic", "system", "system" })
                {
                    var theme = AvaloniaThemeCatalog.Resolve(id);
                    menu.ShowAt(button);
                    Dispatcher.UIThread.RunJobs();
                    var leaf = Leaves(menu).Single(item => AutomationProperties.GetName(item) == theme.Name);
                    var parent = menu.Items.OfType<MenuItem>().SingleOrDefault(item => item.Items.Contains(leaf));
                    if (parent is not null)
                    {
                        parent.IsSubMenuOpen = true;
                        Dispatcher.UIThread.RunJobs();
                    }
                    leaf.Focus();
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(menu.IsOpen);
                    Assert.Same(theme, source.SelectedTheme);
                    AssertSelection(menu, theme);
                    AssertSelection(otherMenu, theme);
                }

                // A settings reload or another selector can change the bound value while closed.
                source.SelectedTheme = AvaloniaThemeCatalog.Resolve("dark-teal");
                Dispatcher.UIThread.RunJobs();
                AssertSelection(menu, source.SelectedTheme);
                AssertSelection(otherMenu, source.SelectedTheme);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static async Task WithAvalonia(Action action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ThemeMenuTests));
        try
        {
            await session.Dispatch(action, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private static void AssertSelection(ThemeMenuFlyout menu, ThemeItem theme)
    {
        Assert.Same(theme, menu.SelectedTheme);
        Assert.Equal(theme.Name, AutomationProperties.GetName(Assert.Single(Leaves(menu), item => item.IsChecked)));
    }

    private static IEnumerable<MenuItem> Leaves(ThemeMenuFlyout menu) => menu.Items.OfType<MenuItem>()
        .SelectMany(item => item.ItemCount == 0 ? new[] { item } : item.Items.Cast<MenuItem>());

    private sealed class ThemeSelection : ObservableObject
    {
        private ThemeItem selectedTheme = AvaloniaThemeCatalog.Resolve("dark-orange");

        public ThemeItem SelectedTheme
        {
            get => selectedTheme;
            set => SetProperty(ref selectedTheme, value);
        }
    }
}

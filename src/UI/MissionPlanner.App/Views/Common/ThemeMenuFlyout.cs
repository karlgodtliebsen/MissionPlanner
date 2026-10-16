using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using MissionPlanner.Themes;

namespace MissionPlanner.App.Views.Common;

/// <summary>A catalog-backed theme menu shared by the top bar and Preferences.</summary>
public sealed class ThemeMenuFlyout : MenuFlyout
{
    /// <summary>Defines the selected theme, bound to the host's existing settings flow.</summary>
    public static readonly StyledProperty<ThemeItem?> SelectedThemeProperty =
        AvaloniaProperty.Register<ThemeMenuFlyout, ThemeItem?>(nameof(SelectedTheme),
            defaultBindingMode: BindingMode.TwoWay);

    private readonly Dictionary<ThemeItem, MenuItem> choices = [];
    private readonly string groupName = $"Theme-{Guid.NewGuid():N}";

    /// <summary>Groups catalog colors under Dark and Light without duplicating the theme list.</summary>
    public ThemeMenuFlyout()
    {
        Items.Add(CreateChoice("Default", AvaloniaThemeCatalog.Resolve("system")));
        Items.Add(new Separator());

        foreach (var (id, name) in new[] { ("dark", "Dark"), ("light", "Light") })
        {
            var appearance = new MenuItem { Header = name };
            appearance.Items.Add(CreateChoice("Blue", AvaloniaThemeCatalog.Resolve(id)));
            foreach (var theme in AvaloniaThemeCatalog.Items.Where(theme => theme.Id.StartsWith($"{id}-", StringComparison.Ordinal)))
            {
                appearance.Items.Add(CreateChoice(theme.Name[(name.Length + 1)..], theme));
            }
            Items.Add(appearance);
        }

        var highContrast = new MenuItem { Header = "High contrast" };
        foreach (var theme in AvaloniaThemeCatalog.Items.Where(theme => !choices.ContainsKey(theme)))
        {
            highContrast.Items.Add(CreateChoice(theme.Name, theme));
        }
        if (highContrast.ItemCount > 0)
        {
            Items.Add(new Separator());
            Items.Add(highContrast);
        }
        UpdateSelection();
    }

    /// <summary>Gets or sets the selected theme without applying or persisting it in the view.</summary>
    public ThemeItem? SelectedTheme
    {
        get => GetValue(SelectedThemeProperty);
        set => SetValue(SelectedThemeProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedThemeProperty)
        {
            UpdateSelection();
        }
    }

    private MenuItem CreateChoice(string header, ThemeItem theme)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            GroupName = groupName
        };
        AutomationProperties.SetName(item, theme.Name);
        item.Click += (_, _) =>
        {
            SetCurrentValue(SelectedThemeProperty, theme);
            UpdateSelection();
        };
        choices.Add(theme, item);
        return item;
    }

    private void UpdateSelection()
    {
        var id = SelectedTheme?.Id ?? "system";
        foreach (var (theme, item) in choices)
        {
            item.IsChecked = theme.Id == id;
        }
    }
}

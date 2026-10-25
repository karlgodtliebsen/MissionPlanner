using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlanner.Themes;

/// <summary>A stable preference identifier and its theme brush resources.</summary>
public sealed record ThemeColorItem(string Id, string Name, params string[] ResourceKeys)
{
    /// <summary>Overrides control resources without changing their shared palette brushes.</summary>
    public bool IsControlOverride { get; init; }
}

/// <summary>Curated editable colors. Resource names remain an implementation detail.</summary>
public static class ThemeColorCatalog
{
    /// <summary>Uses a stable encoding even when Avalonia recognizes a named color.</summary>
    public static string Format(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static readonly IReadOnlyList<ThemeColorItem> highContrastItems = new ThemeColorItem[]
    {
        new("background", "Window background", "SemiColorWindow"),
        new("text", "Text and borders", "SemiColorWindowText"),
        new("accent-text", "Links", "SemiColorHotlight"),
        new("disabled-text", "Disabled text", "SemiColorGrayText"),
        new("selection-text", "Selected text", "SemiColorHighlightText"),
        new("accent", "Accent and selection", "SemiColorHighlight"),
        new("button-text", "Button text", "SemiColorButtonText"),
        new("button-background", "Button background", "SemiColorButtonFace")
    };

    public static IReadOnlyList<ThemeColorItem> ForTheme(string themeId) =>
        themeId is "aquatic" or "desert" or "dusk" or "night-sky" ? highContrastItems : Items;

    public static IReadOnlyList<ThemeColorItem> Items { get; } = new ThemeColorItem[]
    {
        new("accent", "Accent", "SemiColorPrimary"),
        new("accent-hover", "Accent · hover", "SemiColorPrimaryPointerover"),
        new("accent-pressed", "Accent · pressed", "SemiColorPrimaryActive"),
        new("accent-disabled", "Accent · disabled", "SemiColorPrimaryDisabled"),
        new("accent-text", "Accent text and links", "SemiColorLink", "SemiColorLinkPointerover", "SemiColorLinkActive", "SemiColorLinkVisited", "MissionAccentText"),
        new("background", "Window background", "SemiColorBackground0"),
        new("surface", "Panel background", "SemiColorBackground1", "SemiColorNavBackground"),
        new("raised-surface", "Raised surface", "SemiColorBackground2"),
        new("text", "Primary text", "SemiColorText0"),
        new("secondary-text", "Secondary text", "SemiColorText1"),
        new("muted-text", "Muted text", "SemiColorText2"),
        new("border", "Borders", "SemiColorBorder"),
        new("focus", "Focus outline", "SemiColorFocusBorder"),
        new("selection", "Selection and subtle accent", "SemiColorPrimaryLightPointerover", "SemiColorPrimaryLight"),
        new("success", "Success", "SemiColorSuccess"),
        new("warning", "Warning", "SemiColorWarning"),
        new("error", "Error", "SemiColorDanger"),
        new("success-hover", "Success · hover", "SemiColorSuccessPointerover"),
        new("success-pressed", "Success · pressed", "SemiColorSuccessActive"),
        new("warning-hover", "Warning · hover", "SemiColorWarningPointerover"),
        new("warning-pressed", "Warning · pressed", "SemiColorWarningActive"),
        new("error-hover", "Error · hover", "SemiColorDangerPointerover"),
        new("error-pressed", "Error · pressed", "SemiColorDangerActive"),
        new("disabled-text", "Disabled text", "SemiColorDisabledText"),
        new("disabled-background", "Disabled button background", "SemiColorDisabledBackground"),
        new("disabled-fill", "Disabled input background", "SemiColorDisabledFill"),
        Control("button-background", "Button background", "ButtonDefaultBackground"),
        Control("button-background-hover", "Button background · hover", "ButtonDefaultPointeroverBackground"),
        Control("button-background-pressed", "Button background · pressed", "ButtonDefaultPressedBackground"),
        Control("button-text", "Button text", "ButtonDefaultPrimaryForeground"),
        Control("button-border", "Button border", "ButtonDefaultBorderBrush"),
        Control("button-border-hover", "Button border · hover", "ButtonDefaultPointeroverBorderBrush"),
        Control("button-border-pressed", "Button border · pressed", "ButtonDefaultPressedBorderBrush"),
        Control("solid-button-background", "Solid button background", "ButtonSolidPrimaryBackground"),
        Control("solid-button-background-hover", "Solid button background · hover", "ButtonSolidPrimaryPointeroverBackground"),
        Control("solid-button-background-pressed", "Solid button background · pressed", "ButtonSolidPrimaryPressedBackground"),
        Control("solid-button-text", "Solid button text", "MissionOnPrimary", "MissionOnPrimaryHover", "ButtonSolidForeground"),
        Control("solid-button-text-pressed", "Solid button text · pressed", "MissionOnPrimaryPressed", "ButtonSolidPrimaryPressedForeground"),
        Control("solid-button-border", "Solid button border", "ButtonSolidPrimaryBorderBrush"),
        Control("solid-button-border-hover", "Solid button border · hover", "ButtonSolidPrimaryPointeroverBorderBrush"),
        Control("solid-button-border-pressed", "Solid button border · pressed", "ButtonSolidPrimaryPressedBorderBrush"),
        Control("outline-button-background", "Outline button background", "ButtonOutlineBackground"),
        Control("outline-button-border", "Outline button border", "ButtonOutlineBorderBrush"),
        Control("borderless-button-background", "Borderless button background", "ButtonBorderlessBackground"),
        Control("input-background", "Input background", "TextBoxDefaultBackground"),
        Control("input-background-hover", "Input background · hover", "TextBoxPointeroverBackground"),
        Control("input-background-pressed", "Input background · pressed", "TextBoxPressedBackground"),
        Control("input-text", "Input text", "TextBoxForeground"),
        Control("input-placeholder", "Input placeholder text", "TextBoxPlaceholderForeground"),
        Control("input-border", "Input border", "TextBoxDefaultBorderBrush"),
        Control("input-border-hover", "Input border · hover", "TextBoxPointeroverBorderBrush"),
        Control("input-border-focus", "Input border · focused", "TextBoxFocusBorderBrush"),
        Control("input-caret", "Input caret", "TextBoxTextCaretBrush"),
        Control("selection-text", "Selected input text", "TextBoxSelectionForeground")
    };

    private static ThemeColorItem Control(string id, string name, params string[] keys) =>
        new(id, name, keys) { IsControlOverride = true };
}

/// <summary>
/// Applies the selected theme's preferences to shared mutable brushes on the UI thread.
/// Updating brush instances also updates the providers' static control aliases.
/// </summary>
public sealed class ThemeColorCustomizer(Application application)
{
    private readonly Dictionary<SolidColorBrush, (Color Color, double Opacity)> originals = new(ReferenceEqualityComparer.Instance);
    private Dictionary<string, Dictionary<string, string>> profiles = new();
    private readonly ResourceDictionary controlOverrides = new();

    public string ThemeId { get; private set; } = "system";

    public IReadOnlyList<ThemeColorItem> Items => ThemeColorCatalog.ForTheme(ThemeId);

    public Dictionary<string, Dictionary<string, string>> Snapshot() =>
        profiles.ToDictionary(pair => pair.Key, pair => new Dictionary<string, string>(pair.Value));

    public void Load(Dictionary<string, Dictionary<string, string>>? values)
    {
        profiles = values?.Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => new Dictionary<string, string>(pair.Value)) ?? new();
        Apply(ThemeId);
    }

    public void Restore()
    {
        controlOverrides.ThemeDictionaries.Clear();
        foreach (var (brush, original) in originals)
        {
            brush.Color = original.Color;
            brush.Opacity = original.Opacity;
        }
        originals.Clear();
    }

    public void Apply(string themeId)
    {
        Restore();
        ThemeId = themeId;
        if (!application.Resources.MergedDictionaries.Contains(controlOverrides))
            application.Resources.MergedDictionaries.Add(controlOverrides);
        if (!profiles.TryGetValue(themeId, out var colors)) return;
        var activeControlOverrides = new ResourceDictionary();
        controlOverrides.ThemeDictionaries[application.ActualThemeVariant] = activeControlOverrides;
        foreach (var item in Items)
        {
            if (!colors.TryGetValue(item.Id, out var value) || !Color.TryParse(value, out var color)) continue;
            foreach (var key in item.ResourceKeys)
            {
                if (item.IsControlOverride)
                {
                    activeControlOverrides[key] = new SolidColorBrush(color);
                    continue;
                }
                if (FindBrush(key) is not { } brush) continue;
                originals.TryAdd(brush, (brush.Color, brush.Opacity));
                brush.Color = color;
                brush.Opacity = 1;
            }
        }
    }

    public Color GetColor(ThemeColorItem item)
    {
        var brush = item.ResourceKeys.Select(FindBrush).FirstOrDefault(value => value is not null);
        return brush is null ? Colors.Transparent :
            Color.FromArgb((byte)Math.Round(brush.Color.A * brush.Opacity), brush.Color.R, brush.Color.G, brush.Color.B);
    }

    public bool IsOverridden(ThemeColorItem item) =>
        profiles.TryGetValue(ThemeId, out var colors) && colors.ContainsKey(item.Id);

    public void SetColor(ThemeColorItem item, Color? color)
    {
        if (color is { } value)
        {
            if (!profiles.TryGetValue(ThemeId, out var colors)) profiles[ThemeId] = colors = new();
            colors[item.Id] = ThemeColorCatalog.Format(value);
        }
        else if (profiles.TryGetValue(ThemeId, out var colors))
        {
            colors.Remove(item.Id);
            if (colors.Count == 0) profiles.Remove(ThemeId);
        }
        Apply(ThemeId);
    }

    public void ResetTheme()
    {
        profiles.Remove(ThemeId);
        Apply(ThemeId);
    }

    private SolidColorBrush? FindBrush(string key) =>
        application.TryFindResource(key, application.ActualThemeVariant, out var value) ? value as SolidColorBrush : null;
}

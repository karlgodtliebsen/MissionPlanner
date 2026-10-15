using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>Owns the removable resource overlay used by the existing theme catalog.</summary>
internal sealed class MissionOrangePalette(Application application)
{
    private ResourceDictionary? overlay;
    private StyleInclude? controlStyles;

    internal void Apply(bool enabled)
    {
        if (controlStyles is not null)
        {
            application.Styles.Remove(controlStyles);
            controlStyles = null;
        }
        if (overlay is not null)
        {
            application.Resources.MergedDictionaries.Remove(overlay);
            overlay = null;
        }

        // Semi's named variants are accessibility themes. Never recolor them.
        var variant = application.ActualThemeVariant;
        if (!enabled || (variant != ThemeVariant.Light && variant != ThemeVariant.Dark))
        {
            return;
        }

        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://MissionPlanner.App/"))
        {
            Source = new Uri($"avares://MissionPlanner.App/Resources/Themes/MissionOrange{variant.Key}.axaml")
        });

        // Inherit installed templates. Only primary solid foregrounds need extra styles;
        // ButtonSolidForeground is deliberately left untouched for semantic buttons.
        foreach (var key in new[] { "SolidButton", "SolidIconButton", "SolidIconDropDownButton" })
        {
            if (application.TryFindResource(key, variant, out var value) && value is ControlTheme original)
            {
                // The app also applies SolidIconButton to ordinary Buttons and dropdowns.
                // ContentControl allows the nested rules to match all those consumers.
                var theme = new ControlTheme(typeof(ContentControl)) { BasedOn = original };
                AddForeground(theme, null, "MissionOnAccent");
                AddForeground(theme, ":pointerover", "MissionOnAccentHover");
                AddForeground(theme, ":pressed", "MissionOnAccentPressed");
                resources[key] = theme;
            }
        }

        overlay = resources;
        application.Resources.MergedDictionaries.Add(resources);
        controlStyles = new StyleInclude(new Uri("avares://MissionPlanner.App/"))
        {
            Source = new Uri("avares://MissionPlanner.App/Resources/Themes/MissionOrangeControls.axaml")
        };
        application.Styles.Add(controlStyles);
    }

    private static void AddForeground(ControlTheme theme, string? state, string resource)
    {
        var style = new Style(selector =>
        {
            var selected = selector.Nesting().Not(s => s.Class(":disabled"));
            foreach (var semantic in new[] { "Secondary", "Tertiary", "Success", "Warning", "Danger", "Colorful" })
            {
                selected = selected.Not(s => s.Class(semantic));
            }
            if (state is not null)
            {
                selected = selected.Class(state);
            }
            return selected.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
        });
        style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension(resource)));
        theme.Children.Add(style);
    }
}
